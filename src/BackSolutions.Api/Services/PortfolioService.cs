using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Content;
using BackSolutions.Core.Entities.Content;
using BackSolutions.Core.Enums;
using BackSolutions.Core.Interfaces;
using BackSolutions.Data;
using Microsoft.EntityFrameworkCore;

namespace BackSolutions.Api.Services;

/// <summary>
/// Proyectos del portfolio. Las imágenes son una colección daughter: al guardar se
/// sincronizan con lo que venga en el request, dentro de una transacción.
/// </summary>
public sealed class PortfolioService : IPortfolioService
{
    private readonly AppDbContext _db;
    private readonly ITransactionRunner _transactions;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public PortfolioService(AppDbContext db, ICurrentUser currentUser, IClock clock, ITransactionRunner transactions)
    {
        _transactions = transactions;
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PagedResult<PortfolioProjectDto>> ListAsync(
        int? page,
        int? pageSize,
        ContentStatusFilter status,
        bool? featuredOnly,
        CancellationToken cancellationToken = default)
    {
        var (currentPage, size) = Paging.Normalize(page, pageSize);

        var query = ApplyStatusFilter(_db.Projects.AsNoTracking(), status)
            .Include(p => p.Images)
            .AsQueryable();

        if (featuredOnly == true)
        {
            query = query.Where(p => p.IsFeatured);
        }

        query = query.OrderBy(p => p.SortOrder).ThenByDescending(p => p.PublishedAtUtc);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((currentPage - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return PagedResult<PortfolioProjectDto>.Create([.. items.Select(p => Map(p))], total, currentPage, size);
    }

    public async Task<IReadOnlyList<PublicPortfolioProjectDto>> ListPublicAsync(
        bool onlyFeatured = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Projects
            .AsNoTracking()
            .Include(p => p.Images)
            .Where(p => p.Status == ContentStatus.Published)
            .AsQueryable();

        if (onlyFeatured)
        {
            query = query.Where(p => p.IsFeatured);
        }

        var projects = await query
            .OrderBy(p => p.SortOrder)
            .ThenByDescending(p => p.PublishedAtUtc)
            .ToListAsync(cancellationToken);

        return [.. projects.Select(MapPublic)];
    }

    public async Task<PublicPortfolioProjectDto?> GetPublicBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalized = Slug.Create(slug);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var project = await _db.Projects
            .AsNoTracking()
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Slug == normalized && p.Status == ContentStatus.Published, cancellationToken);

        return project is null ? null : MapPublic(project);
    }

    public async Task<PortfolioProjectDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await _db.Projects
            .AsNoTracking()
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("El proyecto no existe.");

        return Map(project);
    }

    public async Task<PortfolioProjectDto> SaveAsync(
        Guid? id,
        SavePortfolioProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors[nameof(request.Title)] = ["El título es obligatorio."];
        }

        if (string.IsNullOrWhiteSpace(request.Summary))
        {
            errors[nameof(request.Summary)] = ["El resumen es obligatorio: es la card del portfolio."];
        }

        if (request.StartedOn is { } started && request.DeliveredOn is { } delivered && delivered < started)
        {
            errors[nameof(request.DeliveredOn)] = ["La fecha de entrega no puede ser anterior al inicio."];
        }

        var images = request.Images ?? [];
        if (images.Any(i => string.IsNullOrWhiteSpace(i.Url)))
        {
            errors[nameof(request.Images)] = ["Todas las imágenes deben tener URL."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return await _transactions.RunAsync(
            token => SaveCoreAsync(id, request, images, token),
            cancellationToken);
    }

    private async Task<PortfolioProjectDto> SaveCoreAsync(
        Guid? id,
        SavePortfolioProjectRequest request,
        IReadOnlyList<SaveProjectImageRequest> images,
        CancellationToken cancellationToken)
    {
        PortfolioProject project;

        if (id is null)
        {
            project = new PortfolioProject { Id = Guid.NewGuid(), CreatedAtUtc = _clock.UtcNow };

            project.Slug = await SlugGuard.ResolveAsync(
                request.Slug,
                request.Title,
                nameof(request.Slug),
                async (candidate, ct) => await _db.Projects.AnyAsync(p => p.Slug == candidate, ct),
                cancellationToken);

            _db.Projects.Add(project);
        }
        else
        {
            project = await _db.Projects
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
                ?? throw new NotFoundException("El proyecto no existe.");

            if (!string.IsNullOrWhiteSpace(request.Slug))
            {
                var candidate = Slug.Create(request.Slug);
                var isTaken = await _db.Projects.AnyAsync(p => p.Slug == candidate && p.Id != project.Id, cancellationToken);

                project.Slug = isTaken
                    ? await SlugGuard.ResolveAsync(request.Slug, request.Title, nameof(request.Slug),
                        async (slug, ct) => await _db.Projects.AnyAsync(p => p.Slug == slug && p.Id != project.Id, ct),
                        cancellationToken)
                    : candidate;
            }
        }

        project.Title = request.Title.Trim();
        project.Summary = request.Summary.Trim();
        project.DescriptionHtml = request.DescriptionHtml?.Trim();
        project.CoverImageUrl = request.CoverImageUrl?.Trim();
        project.CoverImageAlt = request.CoverImageAlt?.Trim();
        project.ClientName = request.ClientName?.Trim();
        project.TechStackJson = ContentMapper.SerializeStringArray(request.TechStack);
        project.StartedOn = request.StartedOn;
        project.DeliveredOn = request.DeliveredOn;
        project.LiveUrl = request.LiveUrl?.Trim();
        project.RepositoryUrl = request.RepositoryUrl?.Trim();
        project.IsFeatured = request.IsFeatured;
        project.SortOrder = request.SortOrder;

        if (request.Status == ContentStatus.Published && project.PublishedAtUtc is null)
        {
            project.PublishedAtUtc = _clock.UtcNow;
        }

        project.Status = request.Status;

        SyncImages(project, images);

        await _db.SaveChangesAsync(cancellationToken);

        return Map(project);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("El proyecto no existe.");

        // ProjectImages caen por CASCADE desde Projects.
        _db.Projects.Remove(project);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private void SyncImages(PortfolioProject project, IReadOnlyList<SaveProjectImageRequest> requested)
    {
        var existing = project.Images.ToDictionary(i => i.Id);

        foreach (var image in requested)
        {
            if (image.Id is { } imageId && existing.Remove(imageId, out var target))
            {
                target.Url = image.Url.Trim();
                target.AltText = image.AltText?.Trim();
                target.SortOrder = image.SortOrder;
                continue;
            }

            var entity = new ProjectImage
            {
                Id = image.Id ?? Guid.NewGuid(),
                ProjectId = project.Id,
                Url = image.Url.Trim(),
                AltText = image.AltText?.Trim(),
                SortOrder = image.SortOrder
            };

            project.Images.Add(entity);
            _db.AddNew(entity);
        }

        foreach (var removed in existing.Values)
        {
            project.Images.Remove(removed);
        }
    }

    private static IQueryable<PortfolioProject> ApplyStatusFilter(IQueryable<PortfolioProject> query, ContentStatusFilter status)
    {
        var targetStatus = ToStatus(status);

        return status switch
        {
            ContentStatusFilter.All => query,
            _ => query.Where(p => p.Status == targetStatus)
        };
    }

    private static IReadOnlyList<ProjectImageDto> MapImages(PortfolioProject project) =>
        [.. project.Images
            .OrderBy(i => i.SortOrder)
            .Select(i => new ProjectImageDto(i.Id, i.Url, i.AltText, i.SortOrder))];

    private static PortfolioProjectDto Map(PortfolioProject p) => new(
        p.Id, p.Slug, p.Title, p.Summary, p.DescriptionHtml, p.CoverImageUrl, p.CoverImageAlt,
        p.ClientName, ContentMapper.ParseStringArray(p.TechStackJson), p.StartedOn, p.DeliveredOn,
        p.LiveUrl, p.RepositoryUrl, p.IsFeatured, p.Status, p.SortOrder, MapImages(p), p.UpdatedAtUtc);

    private static PublicPortfolioProjectDto MapPublic(PortfolioProject p) => new(
        p.Id, p.Slug, p.Title, p.Summary, p.DescriptionHtml, p.CoverImageUrl, p.CoverImageAlt,
        p.ClientName, ContentMapper.ParseStringArray(p.TechStackJson), p.LiveUrl, p.IsFeatured, MapImages(p));

    /// <summary>
    /// Traduce el filtro de estado al enum real. El cast tiene que quedar fuera del árbol de
    /// expresiones: EF Core no traduce (ContentStatus)(int)status dentro del Where y termina
    /// generando CAST(CAST(@p AS int) AS nvarchar(32)), que se compara contra 'Published' y no
    /// matchea nunca, dejando todos los filtros por estado en cero resultados sin avisar.
    /// </summary>
    private static ContentStatus ToStatus(ContentStatusFilter filter) => (ContentStatus)(int)filter;
}
