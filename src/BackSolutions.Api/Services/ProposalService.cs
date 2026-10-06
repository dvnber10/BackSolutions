using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Proposals;
using BackSolutions.Core.Entities.Content;
using BackSolutions.Core.Entities.Leads;
using BackSolutions.Core.Enums;
using BackSolutions.Core.Interfaces;
using BackSolutions.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace BackSolutions.Api.Services;

/// <summary>
/// Propuestas comerciales.
///
/// Los importes se calculan siempre en el servidor. El cliente manda líneas y un
/// descuento global, pero nunca el total: si aceptáramos un total del cliente, cualquier
/// couldn alterarlo en el JSON y el PDF saldría con un importe falso.
///
/// Definición de las cifras, para que los tres campos sean coherentes entre sí:
///   Subtotal       = Σ (cantidad × precio) de cada línea, ya con su descuento por línea.
///   TotalAmount    = Subtotal − DiscountAmount + TaxAmount
/// </summary>
public sealed class ProposalService : IProposalService
{
    private const string NumberPrefix = "PRP";

    private readonly AppDbContext _db;
    private readonly ITransactionRunner _transactions;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IProposalPdfGenerator _pdf;
    private readonly IEmailSender _emailSender;

    public ProposalService(AppDbContext db, ICurrentUser currentUser, IClock clock, IProposalPdfGenerator pdf, ITransactionRunner transactions, IEmailSender emailSender)
    {
        _transactions = transactions;
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _pdf = pdf;
        _emailSender = emailSender;
    }

    public async Task<PagedResult<ProposalListItemDto>> ListAsync(
        int? page,
        int? pageSize,
        Guid? leadId,
        ProposalStatusFilter status,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var (currentPage, size) = Paging.Normalize(page, pageSize);

        var query = _db.Proposals
            .AsNoTracking()
            .Include(p => p.Lead)
            .Include(p => p.CreatedByUser)
            .AsQueryable();

        if (leadId is { } filterLeadId)
        {
            query = query.Where(p => p.LeadId == filterLeadId);
        }

        var targetStatus = ToStatus(status);


        query = status switch
        {
            ProposalStatusFilter.All => query,
            _ => query.Where(p => p.Status == targetStatus)
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var like = $"%{term}%";
            query = query.Where(p => EF.Functions.Like(p.Number, like) || EF.Functions.Like(p.Title, like));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((currentPage - 1) * size)
            .Take(size)
            .Select(p => new ProposalListItemDto(
                p.Id, p.Number, p.Title, p.LeadId, p.Lead.Name, p.Status, p.Currency,
                p.TotalAmount, p.SentAtUtc, p.ValidUntil, p.CreatedByUserId,
                p.CreatedByUser!.FullName, p.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return PagedResult<ProposalListItemDto>.Create(items, total, currentPage, size);
    }

    public async Task<ProposalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var proposal = await LoadAsync(id, tracked: false, cancellationToken);
        return Map(proposal);
    }

    public async Task<ProposalDto> CreateAsync(
        SaveProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        var lines = Validate(request);
        var currency = await ResolveCurrency(request.Currency, cancellationToken);

        var leadExists = await _db.Leads.AnyAsync(l => l.Id == request.LeadId, cancellationToken);
        if (!leadExists)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.LeadId)] = ["El lead no existe."]
            });
        }

        return await _transactions.RunAsync(
            token => CreateCoreAsync(request, lines, currency, token),
            cancellationToken);
    }

    private async Task<ProposalDto> CreateCoreAsync(
        SaveProposalRequest request,
        IReadOnlyList<SaveProposalItemRequest> lines,
        string currency,
        CancellationToken cancellationToken)
    {
        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            Number = await NextNumberAsync(cancellationToken),
            LeadId = request.LeadId,
            Title = request.Title.Trim(),
            SummaryHtml = request.SummaryHtml,
            NotesHtml = request.NotesHtml?.Trim(),
            Currency = currency,
            Status = ProposalStatus.Draft,
            CreatedByUserId = _currentUser.UserId,
            CreatedAtUtc = _clock.UtcNow
        };

        _db.Proposals.Add(proposal);
        AddItems(proposal, lines);

        await RecalculateAsync(proposal, request, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(proposal.Id, cancellationToken);
    }

    public async Task<ProposalDto> UpdateAsync(
        Guid id,
        SaveProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        var lines = Validate(request);

        var proposal = await LoadAsync(id, tracked: true, cancellationToken);

        if (proposal.Status != ProposalStatus.Draft)
        {
            throw new ConflictException(
                $"La propuesta {proposal.Number} está en estado {proposal.Status} y ya no es editable. Reabrila como borrador para cambiar el contenido.");
        }

        if (proposal.LeadId != request.LeadId)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.LeadId)] = ["No se puede cambiar el lead de una propuesta existente."]
            });
        }

        proposal.Title = request.Title.Trim();
        proposal.SummaryHtml = request.SummaryHtml;
        proposal.NotesHtml = request.NotesHtml?.Trim();
        proposal.Currency = await ResolveCurrency(request.Currency, cancellationToken);

        SyncItems(proposal, lines);

        await RecalculateAsync(proposal, request, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<ProposalDto> UpdateStatusAsync(
        Guid id,
        UpdateProposalStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var proposal = await LoadAsync(id, tracked: true, cancellationToken);
        var target = request.Status;

        if (proposal.Status == target)
        {
            return Map(proposal);
        }

        var allowed = AllowedTransitions[proposal.Status];

        if (!allowed.Contains(target))
        {
            throw new ConflictException(
                $"No se puede pasar de {proposal.Status} a {target}. Transiciones válidas: {string.Join(", ", allowed)}.");
        }

        proposal.Status = target;

        if (target == ProposalStatus.Sent)
        {
            proposal.SentAtUtc = _clock.UtcNow;
            proposal.ValidUntil ??= DefaultValidUntil(await ValidityDaysAsync(cancellationToken));

            if (!string.IsNullOrWhiteSpace(proposal.Lead?.Email))
            {
                try
                {
                    var pdf = await GeneratePdfAsync(id, cancellationToken);
                    var subject = $"Propuesta comercial {proposal.Number}: {proposal.Title}";
                    var htmlBody = $@"
                        <p>Hola <strong>{proposal.Lead.Name}</strong>,</p>
                        <p>Te enviamos adjunta la propuesta comercial <strong>{proposal.Number}</strong> correspondiente a <em>{proposal.Title}</em>.</p>
                        <p>Quedamos a tu disposición por cualquier consulta.</p>
                        <p>Atentamente,<br/><strong>Equipo BackSolutions</strong></p>";

                    await _emailSender.SendEmailAsync(
                        proposal.Lead.Email,
                        subject,
                        htmlBody,
                        pdf.Content,
                        pdf.FileName,
                        cancellationToken);
                }
                catch
                {
                }
            }
        }

        if (target == ProposalStatus.Accepted)
        {
            // Al aceptar se avisa al lead: la conversación pasa a AwaitingClient.
            await MarkLeadStatusAsync(proposal.LeadId, LeadStatus.Won, cancellationToken);
        }

        if (target == ProposalStatus.Rejected)
        {
            await MarkLeadStatusAsync(proposal.LeadId, LeadStatus.Lost, cancellationToken);
        }

        if (target == ProposalStatus.Draft)
        {
            // Reabrir una propuesta ya enviada deja de corresponder a lo que vio el
            // cliente: se limpian las marcas de envío y vista para no mentir.
            proposal.SentAtUtc = null;
            proposal.ViewedAtUtc = null;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task MarkAsViewedAsync(
        Guid proposalId,
        string publicToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(publicToken))
        {
            return;
        }

        // El token del lead es la credencial: sin él, no se puede marcar nada.
        var affected = await _db.Proposals
            .Where(p => p.Id == proposalId && p.Lead!.PublicToken == publicToken && p.ViewedAtUtc == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.ViewedAtUtc, _clock.UtcNow)
                .SetProperty(p => p.Status, ProposalStatus.Viewed), cancellationToken);

        if (affected == 0)
        {
            return;
        }
    }

    /// <summary>
    /// Renderiza el PDF para el cliente, validando su token.
    ///
    /// Existe separada de <see cref="GeneratePdfAsync"/> a propósito. El endpoint público
    /// no puede confiar en que alguien se acuerde de chequear el token antes de generar:
    /// sin este chequeo, cualquiera con el GUID de una propuesta se la descarga, y un
    /// borrador no tiene por qué estar protegido.
    ///
    /// Un token que no corresponde a la propuesta responde 401 y uno que sí pero apunta a
    /// un borrador responde 404: el cliente no debe poder ni ver que existe algo ahí.
    /// </summary>
    public async Task<ProposalPdf> GeneratePublicPdfAsync(
        Guid proposalId,
        string publicToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(publicToken))
        {
            throw new UnauthorizedException("Token de acceso inválido.");
        }

        var proposal = await LoadAsync(proposalId, tracked: false, cancellationToken);

        if (proposal.Lead!.PublicToken is not { } leadToken
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(leadToken),
                Encoding.UTF8.GetBytes(publicToken)))
        {
            throw new UnauthorizedException("Token de acceso inválido.");
        }

        if (proposal.Status == ProposalStatus.Draft)
        {
            // Sigue siendo un 404 y no un 403: el cliente no tiene por qué saber que hay
            // un borrador en curso con ese identificador.
            throw new NotFoundException("La propuesta no existe.");
        }

        if (proposal.ValidUntil is { } validUntil && validUntil <= _clock.UtcNow)
        {
            var tracked = await _db.Proposals.FirstAsync(p => p.Id == proposalId, cancellationToken);

            if (tracked.Status != ProposalStatus.Expired)
            {
                tracked.Status = ProposalStatus.Expired;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        return await GeneratePdfAsync(proposalId, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var proposal = await _db.Proposals.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("La propuesta no existe.");

        if (proposal.Status != ProposalStatus.Draft && !_currentUser.IsInRole(RoleNames.Owner))
        {
            throw new ForbiddenException(
                "Solo se pueden eliminar propuestas en borrador. Para una enviada, pedile al Owner que la rechace.");
        }

        // ProposalItems caen en CASCADE.
        _db.Proposals.Remove(proposal);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProposalPdf> GeneratePdfAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var proposal = await LoadAsync(id, tracked: false, cancellationToken);

        var settings = await _db.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == SiteSettings.SingletonId, cancellationToken)
            ?? new SiteSettings();

        return await _pdf.GenerateAsync(proposal, settings, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────
    // Cálculo de importes
    // ─────────────────────────────────────────────────────────────

    private void AddItems(Proposal proposal, IReadOnlyList<SaveProposalItemRequest> lines)
    {
        foreach (var line in lines.OrderBy(l => l.SortOrder))
        {
            var item = new ProposalItem
            {
                Id = line.Id ?? Guid.NewGuid(),
                ProposalId = proposal.Id,
                Description = line.Description.Trim(),
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                DiscountPercent = line.DiscountPercent,
                SortOrder = line.SortOrder
            };

            proposal.Items.Add(item);
            _db.AddNew(item);
        }
    }

    /// <summary>
    /// Sincroniza las líneas de una propuesta existente reutilizando las entidades ya trackeadas.
    ///
    /// No se puede con <c>Items.Clear()</c> seguido de <c>AddNew</c>: <c>Clear()</c> solo saca las
    /// líneas de la colección y las deja trackeadas como borradas, así que al volver a agregarlas con
    /// el mismo Id (que es justo lo que manda el cliente al editar) EFCore lanza
    /// <c>InvalidOperationException</c> por doble seguimiento de la misma clave y la actualización
    /// terminaba en un 500 sin detalle.
    /// </summary>
    private void SyncItems(Proposal proposal, IReadOnlyList<SaveProposalItemRequest> lines)
    {
        var byId = proposal.Items.ToDictionary(i => i.Id);
        var sortOrder = 0;

        foreach (var line in lines.OrderBy(l => l.SortOrder))
        {
            ProposalItem? item = null;

            if (line.Id is Guid id && byId.Remove(id, out var existing))
            {
                item = existing;
            }

            if (item is null)
            {
                item = new ProposalItem
                {
                    Id = line.Id ?? Guid.NewGuid(),
                    ProposalId = proposal.Id
                };

                proposal.Items.Add(item);
                _db.AddNew(item);
            }

            item.Description = line.Description.Trim();
            item.Quantity = line.Quantity;
            item.UnitPrice = line.UnitPrice;
            item.DiscountPercent = line.DiscountPercent;
            item.SortOrder = sortOrder;
            sortOrder++;
        }

        // Las líneas que el cliente quitó: sacarlas de la colección basta para que EF las borre.
        foreach (var removed in byId.Values)
        {
            proposal.Items.Remove(removed);
        }
    }

    private async Task RecalculateAsync(
        Proposal proposal,
        SaveProposalRequest request,
        CancellationToken cancellationToken)
    {
        // Subtotal: suma de las líneas ya con su descuento propio.
        var subtotal = proposal.Items.Sum(i => Math.Round(
            i.Quantity * i.UnitPrice * (1 - (i.DiscountPercent ?? 0m) / 100m), 2));

        var globalDiscount = Math.Clamp(request.DiscountAmount, 0m, subtotal);
        var tax = Math.Max(0m, request.TaxAmount);

        proposal.Subtotal = subtotal;
        proposal.DiscountAmount = globalDiscount;
        proposal.TaxAmount = tax;
        proposal.TotalAmount = Math.Round(subtotal - globalDiscount + tax, 2);
        proposal.ValidUntil = request.ValidUntil
            ?? proposal.ValidUntil
            ?? DefaultValidUntil(await ValidityDaysAsync(cancellationToken));
    }

    private async Task MarkLeadStatusAsync(Guid leadId, LeadStatus status, CancellationToken cancellationToken)
    {
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == leadId, cancellationToken);

        if (lead is not null)
        {
            lead.Status = status;
        }
    }

    /// <summary>Fecha de vencimiento por defecto, a las 23:59 UTC del día límite.</summary>
    private DateTimeOffset DefaultValidUntil(int days) =>
        new DateTimeOffset(DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime).AddDays(days).ToDateTime(new TimeOnly(23, 59)), TimeSpan.Zero);

    private async Task<int> ValidityDaysAsync(CancellationToken cancellationToken)
    {
        var configured = await _db.SiteSettings
            .AsNoTracking()
            .Where(s => s.Id == SiteSettings.SingletonId)
            .Select(s => s.ProposalValidityDays)
            .FirstOrDefaultAsync(cancellationToken);

        return configured > 0 ? configured : 30;
    }

    /// <summary>
    /// Número correlativo por año (PRP-2026-0001). Se calcula dentro de la transacción
    /// de creación. Con dos proposals simultáneos el índice único de Number hace que
    /// uno falle y el cliente reintente, que es preferible a un hueco silencioso.
    /// </summary>
    private async Task<string> NextNumberAsync(CancellationToken cancellationToken)
    {
        var year = _clock.UtcNow.Year;
        var prefix = $"{NumberPrefix}-{year}-";

        var last = await _db.Proposals
            .Where(p => p.Number.StartsWith(prefix))
            .OrderByDescending(p => p.Number)
            .Select(p => p.Number)
            .FirstOrDefaultAsync(cancellationToken);

        var sequence = last is null ? 1 : int.Parse(last[(prefix.Length)..]) + 1;

        return $"{prefix}{sequence:D4}";
    }

    private async Task<string> ResolveCurrency(string? requested, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            if (requested.Trim().Length != 3)
            {
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    ["currency"] = ["La moneda debe ser un código ISO de 3 letras."]
                });
            }

            return requested.Trim().ToUpperInvariant();
        }

        var fallback = await _db.SiteSettings
            .AsNoTracking()
            .Where(s => s.Id == SiteSettings.SingletonId)
            .Select(s => s.DefaultCurrency)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(fallback) ? "USD" : fallback;
    }

    private IReadOnlyList<SaveProposalItemRequest> Validate(SaveProposalRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var lineErrors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors[nameof(request.Title)] = ["El título es obligatorio."];
        }

        if (string.IsNullOrWhiteSpace(request.SummaryHtml))
        {
            errors[nameof(request.SummaryHtml)] = ["El resumen es obligatorio: es lo primero que lee el cliente."];
        }

        var lines = request.Items?.Where(l => !string.IsNullOrWhiteSpace(l.Description)).ToList() ?? [];

        if (lines.Count == 0)
        {
            errors[nameof(request.Items)] = ["La propuesta necesita al menos una línea."];
        }

        for (var index = 0; index < (request.Items?.Count ?? 0); index++)
        {
            var line = request.Items![index];

            if (string.IsNullOrWhiteSpace(line.Description))
            {
                continue;
            }

            if (line.Quantity <= 0)
            {
                lineErrors.Add($"Línea {index + 1}: la cantidad debe ser mayor a cero.");
            }

            if (line.UnitPrice < 0)
            {
                lineErrors.Add($"Línea {index + 1}: el precio unitario no puede ser negativo.");
            }

            if (line.DiscountPercent is < 0 or > 100)
            {
                lineErrors.Add($"Línea {index + 1}: el descuento debe estar entre 0 y 100.");
            }
        }

        if (lineErrors.Count > 0)
        {
            errors[nameof(request.Items)] = [.. lineErrors];
        }

        if (request.DiscountAmount < 0)
        {
            errors[nameof(request.DiscountAmount)] = ["El descuento no puede ser negativo."];
        }

        if (request.TaxAmount < 0)
        {
            errors[nameof(request.TaxAmount)] = ["El impuesto no puede ser negativo."];
        }

        if (request.ValidUntil is { } validUntil && validUntil <= _clock.UtcNow)
        {
            errors[nameof(request.ValidUntil)] = ["La fecha de validez debe ser futura."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return lines;
    }

    private static readonly Dictionary<ProposalStatus, HashSet<ProposalStatus>> AllowedTransitions = new()
    {
        [ProposalStatus.Draft] = [ProposalStatus.Sent],
        [ProposalStatus.Sent] = [ProposalStatus.Viewed, ProposalStatus.Accepted, ProposalStatus.Rejected, ProposalStatus.Expired],
        [ProposalStatus.Viewed] = [ProposalStatus.Accepted, ProposalStatus.Rejected, ProposalStatus.Expired],
        // Desde un estado terminal solo se vuelve a borrador, y solo con motivo explícito.
        [ProposalStatus.Accepted] = [ProposalStatus.Draft],
        [ProposalStatus.Rejected] = [ProposalStatus.Draft],
        [ProposalStatus.Expired] = [ProposalStatus.Draft]
    };

    private async Task<Proposal> LoadAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var query = _db.Proposals
            .Include(p => p.Items)
            .Include(p => p.Lead)
            .Include(p => p.CreatedByUser)
            .AsQueryable();

        if (!tracked)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("La propuesta no existe.");
    }

    private static ProposalDto Map(Proposal p) => new(
        p.Id,
        p.Number,
        p.LeadId,
        p.Lead.Name,
        p.Lead.Email,
        p.Title,
        p.SummaryHtml,
        p.NotesHtml,
        p.Status,
        p.Currency,
        p.Subtotal,
        p.DiscountAmount,
        p.TaxAmount,
        p.TotalAmount,
        p.SentAtUtc,
        p.ViewedAtUtc,
        p.ValidUntil,
        p.CreatedByUserId,
        p.CreatedByUser?.FullName,
        p.CreatedAtUtc,
        p.UpdatedAtUtc,
        [.. p.Items
            .OrderBy(i => i.SortOrder)
            .Select(i => new ProposalItemDto(
                i.Id, i.Description, i.Quantity, i.UnitPrice, i.DiscountPercent, i.SortOrder))]);

    /// <summary>
    /// Traduce el filtro de estado al enum real. El cast tiene que quedar fuera del árbol de
    /// expresiones: EF Core no traduce (ContentStatus)(int)status dentro del Where y termina
    /// generando CAST(CAST(@p AS int) AS nvarchar(32)), que se compara contra 'Published' y no
    /// matchea nunca, dejando todos los filtros por estado en cero resultados sin avisar.
    /// </summary>
    private static ProposalStatus ToStatus(ProposalStatusFilter filter) => (ProposalStatus)(int)filter;
}
