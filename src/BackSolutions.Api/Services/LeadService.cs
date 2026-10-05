using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Leads;
using BackSolutions.Core.Entities.Leads;
using BackSolutions.Core.Enums;
using BackSolutions.Core.Interfaces;
using BackSolutions.Data;
using Microsoft.EntityFrameworkCore;

namespace BackSolutions.Api.Services;

/// <summary>
/// Leads: alta pública, seguimiento por el equipo y vista del cliente sobre su consulta.
/// </summary>
public sealed class LeadService : ILeadService
{
    private const int MinDetailsLength = 10;

    private const int MaxDetailsLength = 5000;

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public LeadService(AppDbContext db, ICurrentUser currentUser, IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PublicLeadCreatedResponse> CreatePublicAsync(
        CreateLeadRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        // Campo trampa: un navegador real nunca lo completa. Se responde como si
        // hubiera funcionado para que el bot no pueda distinguirse del resultado.
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            return new PublicLeadCreatedResponse(Guid.Empty, SecureToken.Generate(), LeadStatus.New, _clock.UtcNow);
        }

        var errors = Validate(request);

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var email = request.Email.Trim().ToLowerInvariant();

        if (request.ServiceId is { } serviceId)
        {
            var exists = await _db.Services
                .AnyAsync(s => s.Id == serviceId && s.Status == ContentStatus.Published, cancellationToken);

            if (!exists)
            {
                errors[nameof(request.ServiceId)] = ["El servicio seleccionado no existe o ya no está disponible."];
                throw new ValidationException(errors);
            }
        }

        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            PublicToken = SecureToken.Generate(),
            Name = request.Name.Trim(),
            Email = email,
            Phone = request.Phone?.Trim(),
            ServiceId = request.ServiceId,
            Details = request.Details.Trim(),
            Status = LeadStatus.New,
            Source = request.Source,
            BudgetMin = request.BudgetMin,
            BudgetMax = request.BudgetMax,
            Currency = request.Currency?.Trim().ToUpperInvariant(),
            TargetStartDate = request.TargetStartDate,
            IpAddress = Trim(ipAddress, 64),
            UserAgent = Trim(userAgent, 400),
            CreatedAtUtc = _clock.UtcNow
        };

        _db.Leads.Add(lead);
        await _db.SaveChangesAsync(cancellationToken);

        return new PublicLeadCreatedResponse(lead.Id, lead.PublicToken, lead.Status, lead.CreatedAtUtc);
    }

    public async Task<PublicLeadDto?> GetPublicByTokenAsync(string publicToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(publicToken))
        {
            return null;
        }

        var lead = await _db.Leads
            .AsNoTracking()
            .Include(l => l.Proposals)
            .FirstOrDefaultAsync(l => l.PublicToken == publicToken, cancellationToken);

        if (lead is null)
        {
            return null;
        }

        // Solo las propuestas ya enviadas se muestran al cliente; las borradores
        // son material de trabajo interno.
        var proposals = lead.Proposals
            .Where(p => p.Status != ProposalStatus.Draft)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new PublicProposalSummaryDto(
                p.Id,
                p.Number,
                p.Title,
                p.Status,
                p.Currency,
                p.TotalAmount,
                p.SentAtUtc,
                p.ValidUntil,
                p.Status == ProposalStatus.Draft ? null : $"api/public/leads/{publicToken}/proposals/{p.Id}/pdf"))
            .ToList();

        return new PublicLeadDto(
            lead.Id,
            lead.PublicToken,
            lead.Name,
            lead.Status,
            lead.CreatedAtUtc,
            lead.TargetStartDate,
            proposals);
    }

    public async Task<PagedResult<LeadListItemDto>> ListAsync(
        int? page,
        int? pageSize,
        LeadStatusFilter status,
        Guid? assignedToUserId,
        bool unassignedOnly,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var (currentPage, size) = Paging.Normalize(page, pageSize);

        var query = _db.Leads
            .AsNoTracking()
            .Include(l => l.HandledByUser)
            .AsQueryable();

        var targetStatus = ToStatus(status);


        query = status switch
        {
            LeadStatusFilter.All => query,

            _ => query.Where(l => l.Status == targetStatus)
        };

        if (unassignedOnly)
        {
            query = query.Where(l => l.HandledByUserId == null);
        }
        else if (assignedToUserId is { } userId)
        {
            query = query.Where(l => l.HandledByUserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var like = $"%{term}%";
            query = query.Where(l => EF.Functions.Like(l.Name, like) || EF.Functions.Like(l.Email, like));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(l => l.CreatedAtUtc)
            .Skip((currentPage - 1) * size)
            .Take(size)
            .Select(l => new LeadListItemDto(
                l.Id, l.Name, l.Email, l.Phone, l.Status, l.Source, l.BudgetMin, l.BudgetMax,
                l.Currency, l.HandledByUserId, l.HandledByUser!.FullName,
                l.Proposals.Count, l.Conversations.Count, l.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return PagedResult<LeadListItemDto>.Create(items, total, currentPage, size);
    }

    public async Task<LeadDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var lead = await _db.Leads
            .AsNoTracking()
            .Include(l => l.Service)
            .Include(l => l.HandledByUser)
            .Include(l => l.Proposals)
            .ThenInclude(p => p.CreatedByUser)
            .Include(l => l.Conversations)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken)
            ?? throw new NotFoundException("El lead no existe.");

        return MapDetail(lead);
    }

    public async Task<LeadDetailDto> UpdateStatusAsync(
        Guid id,
        UpdateLeadStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var lead = await LoadDetailEntityAsync(id, cancellationToken);

        if (lead.Status == request.Status)
        {
            return MapDetail(lead);
        }

        lead.Status = request.Status;
        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<LeadDetailDto> AssignAsync(
        Guid id,
        AssignLeadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.UserId is { } userId)
        {
            var canHandle = await _db.Users
                .Where(u => u.Id == userId && u.IsActive)
                .Select(u => u.UserRoles.Any(ur => ur.Role!.Name == "Provider"
                    || ur.Role.Name == "Support"
                    || ur.Role.Name == "Admin"
                    || ur.Role.Name == "Owner"))
                .FirstOrDefaultAsync(cancellationToken);

            if (!canHandle)
            {
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    [nameof(request.UserId)] = ["El usuario no existe, está desactivado o no puede atender leads."]
                });
            }
        }

        var lead = await LoadDetailEntityAsync(id, cancellationToken);
        lead.HandledByUserId = request.UserId;

        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<LeadDetailDto> UpdateNotesAsync(
        Guid id,
        UpdateLeadNotesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.InternalNotes is { Length: > 10000 })
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.InternalNotes)] = ["Las notas internas no pueden superar los 10.000 caracteres."]
            });
        }

        var lead = await LoadDetailEntityAsync(id, cancellationToken);
        lead.InternalNotes = request.InternalNotes?.Trim();

        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsInRole(RoleNames.Owner))
        {
            throw new ForbiddenException("Solo el Owner puede eliminar leads.");
        }

        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == id, cancellationToken)
            ?? throw new NotFoundException("El lead no existe.");

        // Proposals y ProposalItems caen en CASCADE; las Conversations quedan
        // huérfanas a propósito porque son historial de atención, no datos comerciales.
        _db.Leads.Remove(lead);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Lead> LoadDetailEntityAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.Leads
            .Include(l => l.Service)
            .Include(l => l.HandledByUser)
            .Include(l => l.Proposals)
            .ThenInclude(p => p.CreatedByUser)
            .Include(l => l.Conversations)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken)
            ?? throw new NotFoundException("El lead no existe.");

    private Dictionary<string, string[]> Validate(CreateLeadRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors[nameof(request.Name)] = ["El nombre es obligatorio."];
        }
        else if (request.Name.Trim().Length > 160)
        {
            errors[nameof(request.Name)] = ["El nombre no puede superar los 160 caracteres."];
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors[nameof(request.Email)] = ["El email es obligatorio."];
        }
        else if (!LooksLikeEmail(request.Email) || request.Email.Trim().Length > 320)
        {
            errors[nameof(request.Email)] = ["El email no tiene un formato válido."];
        }

        if (request.Phone is { Length: > 32 })
        {
            errors[nameof(request.Phone)] = ["El teléfono no puede superar los 32 caracteres."];
        }

        var details = request.Details?.Trim() ?? string.Empty;

        if (details.Length < MinDetailsLength)
        {
            errors[nameof(request.Details)] = [$"Contanos un poco más: al menos {MinDetailsLength} caracteres."];
        }
        else if (details.Length > MaxDetailsLength)
        {
            errors[nameof(request.Details)] = [$"El detalle no puede superar los {MaxDetailsLength:N0} caracteres."];
        }

        if (request.BudgetMin is not null && request.BudgetMax is not null && request.BudgetMin > request.BudgetMax)
        {
            errors[nameof(request.BudgetMin)] = ["El presupuesto mínimo no puede ser mayor que el máximo."];
        }

        if (request.BudgetMin is < 0 || request.BudgetMax is < 0)
        {
            errors[nameof(request.BudgetMin)] = ["El presupuesto no puede ser negativo."];
        }

        if (request.Currency is { Length: not 3 })
        {
            errors[nameof(request.Currency)] = ["La moneda debe ser un código ISO de 3 letras, por ejemplo USD."];
        }

        if (request.TargetStartDate is { } target && target < _today)
        {
            errors[nameof(request.TargetStartDate)] = ["La fecha objetivo no puede ser pasada."];
        }

        return errors;
    }

    private DateOnly _today => _clock.Today;

    private static bool LooksLikeEmail(string email)
    {
        var value = email.Trim();
        return value.Contains('@') && !value.Any(char.IsWhiteSpace) && value.Length <= 320;
    }

    private static LeadDetailDto MapDetail(Lead l) => new(
        l.Id,
        l.PublicToken,
        l.Name,
        l.Email,
        l.Phone,
        l.ServiceId,
        l.Service?.Name,
        l.Details,
        l.Status,
        l.Source,
        l.BudgetMin,
        l.BudgetMax,
        l.Currency,
        l.TargetStartDate,
        l.InternalNotes,
        l.HandledByUserId,
        l.HandledByUser?.FullName,
        l.IpAddress,
        l.UserAgent,
        l.CreatedAtUtc,
        l.UpdatedAtUtc,
        [.. l.Proposals
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new ProposalSummaryDto(
                p.Id, p.Number, p.Title, p.Status, p.Currency, p.TotalAmount,
                p.SentAtUtc, p.ValidUntil, p.CreatedByUserId, p.CreatedByUser?.FullName, p.CreatedAtUtc))],
        [.. l.Conversations
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new ConversationSummaryDto(
                c.Id, c.Subject, c.Status, c.HighestSeverity, c.CreatedAtUtc, c.LastMessageAtUtc))]);

    private static string? Trim(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= maxLength ? value : value[..maxLength];

    /// <summary>
    /// Traduce el filtro de estado al enum real. El cast tiene que quedar fuera del árbol de
    /// expresiones: EF Core no traduce (LeadStatus)(int)status dentro del Where y termina
    /// generando CAST(CAST(@p AS int) AS nvarchar(32)), que se compara contra 'New' y no
    /// matchea nunca, dejando todos los filtros por estado en cero resultados sin avisar.
    /// </summary>
    private static LeadStatus ToStatus(LeadStatusFilter filter) => (LeadStatus)(int)filter;
}
