using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Content;
using BackSolutions.Core.Entities.Content;
using BackSolutions.Core.Enums;
using BackSolutions.Core.Interfaces;
using BackSolutions.Data;
using Microsoft.EntityFrameworkCore;

namespace BackSolutions.Api.Services;

/// <summary>
/// Páginas y secciones. La web pública no tiene rutas fijas: todo se arma con las
/// secciones publicadas de la página que coincida con el slug de la URL.
///
/// Guardar el contenido de una página es una operación de reemplazo: se sincronizan
/// las secciones en una transacción para que nunca quede una página con secciones
/// a medio actualizar.
/// </summary>
public sealed class PageService : IPageService
{
    private readonly AppDbContext _db;
    private readonly ITransactionRunner _transactions;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public PageService(AppDbContext db, ICurrentUser currentUser, IClock clock, ITransactionRunner transactions)
    {
        _transactions = transactions;
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PagedResult<PageDto>> ListAsync(
        int? page,
        int? pageSize,
        ContentStatusFilter status,
        CancellationToken cancellationToken = default)
    {
        var (currentPage, size) = Paging.Normalize(page, pageSize);

        var query = ApplyStatusFilter(_db.Pages.AsNoTracking(), status)
            .Include(p => p.Sections)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Title);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((currentPage - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return PagedResult<PageDto>.Create([.. items.Select(p => Map(p))], total, currentPage, size);
    }

    public async Task<IReadOnlyList<PageNavItemDto>> GetNavigationAsync(CancellationToken cancellationToken = default) =>
        await _db.Pages
            .AsNoTracking()
            .Where(p => p.Status == ContentStatus.Published && p.ShowInNavigation)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Title)
            .Select(p => new PageNavItemDto(p.Id, p.Slug, p.Title, p.SortOrder))
            .ToListAsync(cancellationToken);

    public async Task<PageDto?> GetPublicBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalized = Slug.Create(slug);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var page = await _db.Pages
            .AsNoTracking()
            .Include(p => p.Sections)
            .FirstOrDefaultAsync(p => p.Slug == normalized && p.Status == ContentStatus.Published, cancellationToken);

        return page is null ? null : Map(page, visibleOnly: true);
    }

    public async Task<PageDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var page = await _db.Pages
            .AsNoTracking()
            .Include(p => p.Sections)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("La página no existe.");

        return Map(page);
    }

    public async Task<PageDto> SaveAsync(Guid? id, SavePageRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Title)] = ["El título es obligatorio."]
            });
        }

        return await _transactions.RunAsync(
            token => SaveCoreAsync(id, request, token),
            cancellationToken);
    }

    private async Task<PageDto> SaveCoreAsync(Guid? id, SavePageRequest request, CancellationToken cancellationToken)
    {
        Page page;

        if (id is null)
        {
            page = new Page
            {
                Id = Guid.NewGuid(),
                AuthorId = _currentUser.UserId,
                CreatedAtUtc = _clock.UtcNow
            };

            page.Slug = await SlugGuard.ResolveAsync(
                request.Slug,
                request.Title,
                nameof(request.Slug),
                async (candidate, ct) => await _db.Pages.AnyAsync(p => p.Slug == candidate, ct),
                cancellationToken);

            _db.Pages.Add(page);
        }
        else
        {
            page = await _db.Pages
                .Include(p => p.Sections)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
                ?? throw new NotFoundException("La página no existe.");

            // El slug solo se recalcula si viene informado: si el editor lo deja vacío
            // se conserva la URL ya publicada.
            if (!string.IsNullOrWhiteSpace(request.Slug))
            {
                var candidate = Slug.Create(request.Slug);
                var isTaken = await _db.Pages.AnyAsync(p => p.Slug == candidate && p.Id != page.Id, cancellationToken);

                page.Slug = isTaken
                    ? await SlugGuard.ResolveAsync(request.Slug, request.Title, nameof(request.Slug),
                        async (slug, ct) => await _db.Pages.AnyAsync(p => p.Slug == slug && p.Id != page.Id, ct),
                        cancellationToken)
                    : candidate;
            }
        }

        page.Title = request.Title.Trim();
        page.Eyebrow = request.Eyebrow?.Trim();
        page.Intro = request.Intro?.Trim();
        page.SeoTitle = request.SeoTitle?.Trim();
        page.SeoDescription = request.SeoDescription?.Trim();
        page.OgImageUrl = request.OgImageUrl?.Trim();
        page.ShowInNavigation = request.ShowInNavigation;
        page.SortOrder = request.SortOrder;

        ApplyStatus(page, request.Status);

        SyncSections(page, request.Sections);

        await _db.SaveChangesAsync(cancellationToken);

        return Map(page);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Borrar contenido publicado destruye URLs ya indexadas: reservado al Owner.
        if (!_currentUser.IsInRole(RoleNames.Owner))
        {
            throw new ForbiddenException("Solo el Owner puede eliminar páginas publicadas.");
        }

        var page = await _db.Pages.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("La página no existe.");

        // Las secciones caen por CASCADE desde Pages.
        _db.Pages.Remove(page);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Al publicar por primera vez se sella la fecha; al volver a borrador se conserva.</summary>
    private void ApplyStatus(Page page, ContentStatus status)
    {
        page.Status = status;

        if (status == ContentStatus.Published && page.PublishedAtUtc is null)
        {
            page.PublishedAtUtc = _clock.UtcNow;
        }
    }

    private void SyncSections(Page page, IReadOnlyList<SavePageSectionRequest>? requested)
    {
        if (requested is null)
        {
            return;
        }

        var requestedIds = requested
            .Where(s => s.Id is not null)
            .Select(s => s.Id!.Value)
            .ToHashSet();

        // Secciones que vienen en el request y ya pertenecen a esta página.
        var existing = page.Sections.ToDictionary(s => s.Id);

        foreach (var section in requested)
        {
            if (section.Id is { } sectionId && existing.Remove(sectionId, out var target))
            {
                target.Type = section.Type;
                target.Title = section.Title?.Trim();
                target.Subtitle = section.Subtitle?.Trim();
                target.ContentJson = ContentMapper.SerializeJsonObject(section.Content);
                target.SortOrder = section.SortOrder;
                target.IsVisible = section.IsVisible;
                continue;
            }

            // Id informado pero que no es de esta página: se trata como creación
            // en vez de fallar, porque el cliente puede estar reusando ids tras un undo.
            var entity = new PageSection
            {
                Id = section.Id ?? Guid.NewGuid(),
                PageId = page.Id,
                Type = section.Type,
                Title = section.Title?.Trim(),
                Subtitle = section.Subtitle?.Trim(),
                ContentJson = ContentMapper.SerializeJsonObject(section.Content),
                SortOrder = section.SortOrder,
                IsVisible = section.IsVisible
            };

            page.Sections.Add(entity);
            _db.AddNew(entity);
        }

        // Lo que quedó en `existing` y no volvió en el request fue borrada por el editor.
        foreach (var removed in existing.Values)
        {
            page.Sections.Remove(removed);
        }
    }

    private static IQueryable<Page> ApplyStatusFilter(IQueryable<Page> query, ContentStatusFilter status)
    {
        var targetStatus = ToStatus(status);

        return status switch
        {
            ContentStatusFilter.All => query,
            _ => query.Where(p => p.Status == targetStatus)
        };
    }

    private static PageDto Map(Page page, bool visibleOnly = false)
    {
        var sections = page.Sections
            .Where(s => !visibleOnly || s.IsVisible)
            .OrderBy(s => s.SortOrder)
            .Select(s => new PageSectionDto(
                s.Id,
                s.Type,
                s.Title,
                s.Subtitle,
                ContentMapper.ParseJsonObject(s.ContentJson),
                s.SortOrder,
                s.IsVisible))
            .ToList();

        return new PageDto(
            page.Id,
            page.Slug,
            page.Title,
            page.Eyebrow,
            page.Intro,
            page.SeoTitle,
            page.SeoDescription,
            page.OgImageUrl,
            page.Status,
            page.ShowInNavigation,
            page.SortOrder,
            page.PublishedAtUtc,
            sections,
            page.UpdatedAtUtc);
    }

    /// <summary>
    /// Traduce el filtro de estado al enum real. El cast tiene que quedar fuera del árbol de
    /// expresiones: EF Core no traduce (ContentStatus)(int)status dentro del Where y termina
    /// generando CAST(CAST(@p AS int) AS nvarchar(32)), que se compara contra 'Published' y no
    /// matchea nunca, dejando todos los filtros por estado en cero resultados sin avisar.
    /// </summary>
    private static ContentStatus ToStatus(ContentStatusFilter filter) => (ContentStatus)(int)filter;
}
