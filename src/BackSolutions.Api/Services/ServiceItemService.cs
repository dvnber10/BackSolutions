using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Content;
using BackSolutions.Core.Entities.Content;
using BackSolutions.Core.Enums;
using BackSolutions.Core.Interfaces;
using BackSolutions.Data;
using Microsoft.EntityFrameworkCore;

namespace BackSolutions.Api.Services;

/// <summary>
/// Servicios ofrecidos. La web los consume por slug en la home y en las páginas de detalle,
/// así que la vista pública expone el precio base como "desde".
/// </summary>
public sealed class ServiceItemService : IServiceItemService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public ServiceItemService(AppDbContext db, ICurrentUser currentUser, IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PagedResult<ServiceItemDto>> ListAsync(
        int? page,
        int? pageSize,
        ContentStatusFilter status,
        CancellationToken cancellationToken = default)
    {
        var (currentPage, size) = Paging.Normalize(page, pageSize);

        var query = ApplyStatusFilter(_db.Services.AsNoTracking(), status)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((currentPage - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return PagedResult<ServiceItemDto>.Create([.. items.Select(Map)], total, currentPage, size);
    }

    public async Task<IReadOnlyList<PublicServiceItemDto>> ListPublicAsync(CancellationToken cancellationToken = default)
    {
        var services = await _db.Services
            .AsNoTracking()
            .Where(s => s.Status == ContentStatus.Published)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync(cancellationToken);

        return [.. services.Select(MapPublic)];
    }

    public async Task<PublicServiceItemDto?> GetPublicBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalized = Slug.Create(slug);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var service = await _db.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Slug == normalized && s.Status == ContentStatus.Published, cancellationToken);

        if (service is null)
        {
            return null;
        }

        return MapPublic(service);
    }

    public async Task<ServiceItemDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var service = await _db.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("El servicio no existe.");

        return Map(service);
    }

    public async Task<ServiceItemDto> SaveAsync(Guid? id, SaveServiceItemRequest request, CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors[nameof(request.Name)] = ["El nombre es obligatorio."];
        }

        if (string.IsNullOrWhiteSpace(request.ShortDescription))
        {
            errors[nameof(request.ShortDescription)] = ["La descripción corta es obligatoria: es la que se muestra en la grilla."];
        }

        if (request.BasePrice is < 0)
        {
            errors[nameof(request.BasePrice)] = ["El precio no puede ser negativo."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        ServiceItem service;

        if (id is null)
        {
            service = new ServiceItem { Id = Guid.NewGuid(), CreatedAtUtc = _clock.UtcNow };

            service.Slug = await SlugGuard.ResolveAsync(
                request.Slug,
                request.Name,
                nameof(request.Slug),
                async (candidate, ct) => await _db.Services.AnyAsync(s => s.Slug == candidate, ct),
                cancellationToken);

            _db.Services.Add(service);
        }
        else
        {
            service = await _db.Services.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
                ?? throw new NotFoundException("El servicio no existe.");

            if (!string.IsNullOrWhiteSpace(request.Slug))
            {
                var candidate = Slug.Create(request.Slug);
                var isTaken = await _db.Services.AnyAsync(s => s.Slug == candidate && s.Id != service.Id, cancellationToken);

                service.Slug = isTaken
                    ? await SlugGuard.ResolveAsync(request.Slug, request.Name, nameof(request.Slug),
                        async (slug, ct) => await _db.Services.AnyAsync(s => s.Slug == slug && s.Id != service.Id, ct),
                        cancellationToken)
                    : candidate;
            }
        }

        service.Name = request.Name.Trim();
        service.ShortDescription = request.ShortDescription.Trim();
        service.FullDescription = request.FullDescription?.Trim();
        service.Icon = request.Icon?.Trim();
        service.ImageUrl = request.ImageUrl?.Trim();
        service.TechnologiesJson = ContentMapper.SerializeStringArray(request.Technologies);
        service.BasePrice = request.BasePrice;
        service.PriceNote = request.PriceNote?.Trim();
        service.SortOrder = request.SortOrder;

        if (request.Status == ContentStatus.Published && service.PublishedAtUtc is null)
        {
            service.PublishedAtUtc = _clock.UtcNow;
        }

        service.Status = request.Status;

        await _db.SaveChangesAsync(cancellationToken);

        return Map(service);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var service = await _db.Services
            .Include(s => s.Leads)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("El servicio no existe.");

        // Leads.ServiceId es SET NULL: borrarlos del catálogo no puede destruir el historial
        // comercial. Quedan sin servicio asociado y el equipo los reasigna desde la bandeja.
        if (service.Leads.Count > 0)
        {
            service.Leads.Clear();
            await _db.SaveChangesAsync(cancellationToken);
        }

        _db.Services.Remove(service);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<ServiceItem> ApplyStatusFilter(IQueryable<ServiceItem> query, ContentStatusFilter status)
    {
        var targetStatus = ToStatus(status);

        return status switch
        {
            ContentStatusFilter.All => query,
            _ => query.Where(s => s.Status == targetStatus)
        };
    }

    private static ServiceItemDto Map(ServiceItem s) => new(
        s.Id,
        s.Slug,
        s.Name,
        s.ShortDescription,
        s.FullDescription,
        s.Icon,
        s.ImageUrl,
        ContentMapper.ParseStringArray(s.TechnologiesJson),
        s.BasePrice,
        s.PriceNote,
        s.Status,
        s.SortOrder,
        s.PublishedAtUtc,
        s.UpdatedAtUtc);

    private static PublicServiceItemDto MapPublic(ServiceItem s) => new(
        s.Id,
        s.Slug,
        s.Name,
        s.ShortDescription,
        s.FullDescription,
        s.Icon,
        s.ImageUrl,
        ContentMapper.ParseStringArray(s.TechnologiesJson),
        s.BasePrice,
        s.PriceNote,
        s.SortOrder);

    /// <summary>
    /// Traduce el filtro de estado al enum real. El cast tiene que quedar fuera del árbol de
    /// expresiones: EF Core no traduce (ContentStatus)(int)status dentro del Where y termina
    /// generando CAST(CAST(@p AS int) AS nvarchar(32)), que se compara contra 'Published' y no
    /// matchea nunca, dejando todos los filtros por estado en cero resultados sin avisar.
    /// </summary>
    private static ContentStatus ToStatus(ContentStatusFilter filter) => (ContentStatus)(int)filter;
}
