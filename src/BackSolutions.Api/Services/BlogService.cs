using System.Text.RegularExpressions;
using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Content;
using BackSolutions.Core.Entities.Content;
using BackSolutions.Core.Enums;
using BackSolutions.Core.Interfaces;
using BackSolutions.Data;
using Microsoft.EntityFrameworkCore;

namespace BackSolutions.Api.Services;

/// <summary>
/// Blog y etiquetas.
///
/// Un detalle que suele morder: ReadingMinutes se calcula a partir del HTML del post,
/// contando palabras. Se recalcula en cada guardado para que el dato nunca quede
/// desfasado respecto del contenido.
/// </summary>
public sealed partial class BlogService : IBlogService
{
    /// <summary>200 palabras por minuto es el promedio de lectura en pantalla.</summary>
    private const int WordsPerMinute = 200;

    private readonly AppDbContext _db;
    private readonly ITransactionRunner _transactions;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public BlogService(AppDbContext db, ICurrentUser currentUser, IClock clock, ITransactionRunner transactions)
    {
        _transactions = transactions;
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PagedResult<BlogPostDto>> ListAsync(
        int? page,
        int? pageSize,
        ContentStatusFilter status,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var (currentPage, size) = Paging.Normalize(page, pageSize);

        var query = ApplyStatusFilter(_db.BlogPosts.AsNoTracking(), status)
            .Include(p => p.Author)
            .Include(p => p.PostTags)
            .ThenInclude(pt => pt.Tag)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => EF.Functions.Like(p.Title, $"%{term}%"));
        }

        query = query.OrderByDescending(p => p.PublishedAtUtc ?? p.CreatedAtUtc);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((currentPage - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return PagedResult<BlogPostDto>.Create([.. items.Select(p => Map(p))], total, currentPage, size);
    }

    public async Task<PagedResult<BlogPostSummaryDto>> ListPublicAsync(
        int? page,
        int? pageSize,
        string? tag,
        CancellationToken cancellationToken = default)
    {
        var (currentPage, size) = Paging.Normalize(page, pageSize);

        var query = _db.BlogPosts
            .AsNoTracking()
            .Include(p => p.PostTags)
            .ThenInclude(pt => pt.Tag)
            .Where(p => p.Status == ContentStatus.Published)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var normalizedTag = Slug.Create(tag);
            query = query.Where(p => p.PostTags.Any(pt => pt.Tag!.Slug == normalizedTag));
        }

        var total = await query.CountAsync(cancellationToken);

        var posts = await query
            .OrderByDescending(p => p.PublishedAtUtc)
            .Skip((currentPage - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        var summaries = posts
            .Select(p => new BlogPostSummaryDto(
                p.Id,
                p.Slug,
                p.Title,
                p.Excerpt,
                p.CoverImageUrl,
                p.CoverImageAlt,
                // PublishedAtUtc nunca es null en un post publicado: la fecha la sella ApplyStatus.
                p.PublishedAtUtc ?? p.CreatedAtUtc,
                p.ReadingMinutes,
                p.PostTags.Select(pt => pt.Tag!.Name).ToList()))
            .ToList();

        return PagedResult<BlogPostSummaryDto>.Create(summaries, total, currentPage, size);
    }

    public async Task<BlogPostDto?> GetPublicBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalized = Slug.Create(slug);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var post = await _db.BlogPosts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.PostTags)
            .ThenInclude(pt => pt.Tag)
            .FirstOrDefaultAsync(p => p.Slug == normalized && p.Status == ContentStatus.Published, cancellationToken);

        return post is null ? null : Map(post);
    }

    public async Task RegisterViewAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalized = Slug.Create(slug);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        // ExecuteUpdate evita cargar la entidad y hace el incremento atómico.
        await _db.BlogPosts
            .Where(p => p.Slug == normalized && p.Status == ContentStatus.Published)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ViewCount, p => p.ViewCount + 1), cancellationToken);
    }

    public async Task<BlogPostDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var post = await _db.BlogPosts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.PostTags)
            .ThenInclude(pt => pt.Tag)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("El artículo no existe.");

        return Map(post);
    }

    public async Task<BlogPostDto> SaveAsync(Guid? id, SaveBlogPostRequest request, CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors[nameof(request.Title)] = ["El título es obligatorio."];
        }

        if (string.IsNullOrWhiteSpace(request.ContentHtml))
        {
            errors[nameof(request.ContentHtml)] = ["El contenido es obligatorio."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return await _transactions.RunAsync(
            token => SaveCoreAsync(id, request, token),
            cancellationToken);
    }

    private async Task<BlogPostDto> SaveCoreAsync(Guid? id, SaveBlogPostRequest request, CancellationToken cancellationToken)
    {
        BlogPost post;

        if (id is null)
        {
            post = new BlogPost { Id = Guid.NewGuid(), CreatedAtUtc = _clock.UtcNow };

            post.Slug = await SlugGuard.ResolveAsync(
                request.Slug,
                request.Title,
                nameof(request.Slug),
                async (candidate, ct) => await _db.BlogPosts.AnyAsync(p => p.Slug == candidate, ct),
                cancellationToken);

            _db.BlogPosts.Add(post);
        }
        else
        {
            post = await _db.BlogPosts
                .Include(p => p.PostTags)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
                ?? throw new NotFoundException("El artículo no existe.");

            if (!string.IsNullOrWhiteSpace(request.Slug))
            {
                var candidate = Slug.Create(request.Slug);
                var isTaken = await _db.BlogPosts.AnyAsync(p => p.Slug == candidate && p.Id != post.Id, cancellationToken);

                post.Slug = isTaken
                    ? await SlugGuard.ResolveAsync(request.Slug, request.Title, nameof(request.Slug),
                        async (slug, ct) => await _db.BlogPosts.AnyAsync(p => p.Slug == slug && p.Id != post.Id, ct),
                        cancellationToken)
                    : candidate;
            }
        }

        post.Title = request.Title.Trim();
        post.Excerpt = request.Excerpt?.Trim();
        post.ContentHtml = request.ContentHtml;
        post.CoverImageUrl = request.CoverImageUrl?.Trim();
        post.CoverImageAlt = request.CoverImageAlt?.Trim();
        post.SeoTitle = request.SeoTitle?.Trim();
        post.SeoDescription = request.SeoDescription?.Trim();
        post.ReadingMinutes = EstimateReadingMinutes(request.ContentHtml);
        post.AuthorId = request.AuthorId ?? post.AuthorId ?? _currentUser.UserId;

        if (request.Status == ContentStatus.Published && post.PublishedAtUtc is null)
        {
            post.PublishedAtUtc = _clock.UtcNow;
        }

        post.Status = request.Status;

        await SyncTagsAsync(post, request.Tags, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(post.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsInRole(RoleNames.Owner))
        {
            throw new ForbiddenException("Solo el Owner puede eliminar artículos publicados.");
        }

        var post = await _db.BlogPosts.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("El artículo no existe.");

        _db.BlogPosts.Remove(post);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TagDto>> ListTagsAsync(CancellationToken cancellationToken = default) =>
        await _db.Tags
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TagDto(t.Id, t.Name, t.Slug))
            .ToListAsync(cancellationToken);

    public async Task<TagDto> SaveTagAsync(Guid? id, SaveTagRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Name)] = ["El nombre de la etiqueta es obligatorio."]
            });
        }

        Tag tag;

        if (id is null)
        {
            tag = new Tag { Id = Guid.NewGuid(), Name = request.Name.Trim() };

            tag.Slug = await SlugGuard.ResolveAsync(
                request.Slug,
                request.Name,
                nameof(request.Slug),
                async (candidate, ct) => await _db.Tags.AnyAsync(t => t.Slug == candidate, ct),
                cancellationToken);

            _db.Tags.Add(tag);
        }
        else
        {
            tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
                ?? throw new NotFoundException("La etiqueta no existe.");

            tag.Name = request.Name.Trim();
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new TagDto(tag.Id, tag.Name, tag.Slug);
    }

    public async Task DeleteTagAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException("La etiqueta no existe.");

        // Los BlogPostTag caen por CASCADE desde Tags.
        _db.Tags.Remove(tag);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Deja las etiquetas del post exactamente iguales a las pedidas, reutilizando las
    /// que ya existen por nombre para no duplicar "React" y "react" como etiquetas distintas.
    /// </summary>
    private async Task SyncTagsAsync(BlogPost post, IReadOnlyList<string>? requested, CancellationToken cancellationToken)
    {
        if (requested is null)
        {
            return;
        }

        var names = requested
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (names.Count == 0)
        {
            post.PostTags.Clear();
            return;
        }

        // Se buscan por nombre y NO se resuelve el slug antes: si se resolviera contra
        // todos los tags, un tag ya existente siempre aparecería como ocupado y
        // devolvería "react-2", duplicando la etiqueta en cada guardado. Además los tags
        // nuevos que se agregan abajo todavía no están en la base, así que tampoco se
        // pueden buscar por slug hasta después de SaveChanges.
        var existing = await _db.Tags
            .Where(t => names.Contains(t.Name))
            .ToListAsync(cancellationToken);

        var byName = existing.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var created = new List<Tag>();

        foreach (var name in names)
        {
            if (byName.TryGetValue(name, out var found))
            {
                slugs.Add(found.Slug);
                continue;
            }

            var slug = await SlugGuard.ResolveAsync(
                null,
                name,
                "tags",
                async (candidate, ct) =>
                    slugs.Contains(candidate) || await _db.Tags.AnyAsync(t => t.Slug == candidate, ct),
                cancellationToken);

            var tag = new Tag { Id = Guid.NewGuid(), Name = name, Slug = slug };
            _db.Tags.Add(tag);
            created.Add(tag);
            slugs.Add(slug);
        }

        var links = post.PostTags.ToList();

        foreach (var link in links.Where(l => !slugs.Contains(l.Tag!.Slug)))
        {
            post.PostTags.Remove(link);
        }

        var alreadyLinked = post.PostTags
            .Select(l => l.Tag!.Slug)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in existing.Concat(created))
        {
            if (slugs.Contains(tag.Slug) && !alreadyLinked.Contains(tag.Slug))
            {
                var link = new BlogPostTag { PostId = post.Id, TagId = tag.Id };
                post.PostTags.Add(link);
                _db.AddNew(link);
            }
        }
    }

    /// <summary>Cuenta palabras del HTML visible y lo convierte en minutos, mínimo 1.</summary>
    internal static int EstimateReadingMinutes(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return 1;
        }

        var text = HtmlTagPattern().Replace(html, " ");
        var words = text.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).Length;

        return Math.Max(1, (int)Math.Ceiling(words / (double)WordsPerMinute));
    }

    private static IQueryable<BlogPost> ApplyStatusFilter(IQueryable<BlogPost> query, ContentStatusFilter status)
    {
        var targetStatus = ToStatus(status);

        return status switch
        {
            ContentStatusFilter.All => query,
            _ => query.Where(p => p.Status == targetStatus)
        };
    }

    private static BlogPostDto Map(BlogPost p) => new(
        p.Id,
        p.Slug,
        p.Title,
        p.Excerpt,
        p.ContentHtml,
        p.CoverImageUrl,
        p.CoverImageAlt,
        p.SeoTitle,
        p.SeoDescription,
        p.Status,
        p.PublishedAtUtc,
        p.ReadingMinutes,
        p.ViewCount,
        p.AuthorId,
        p.Author?.FullName,
        p.PostTags.Select(pt => pt.Tag!.Name).OrderBy(n => n).ToList(),
        p.UpdatedAtUtc);

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagPattern();

    /// <summary>
    /// Traduce el filtro de estado al enum real. El cast tiene que quedar fuera del árbol de
    /// expresiones: EF Core no traduce (ContentStatus)(int)status dentro del Where y termina
    /// generando CAST(CAST(@p AS int) AS nvarchar(32)), que se compara contra 'Published' y no
    /// matchea nunca, dejando todos los filtros por estado en cero resultados sin avisar.
    /// </summary>
    private static ContentStatus ToStatus(ContentStatusFilter filter) => (ContentStatus)(int)filter;
}
