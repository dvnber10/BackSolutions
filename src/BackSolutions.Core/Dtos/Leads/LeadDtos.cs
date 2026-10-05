using BackSolutions.Core.Enums;

namespace BackSolutions.Core.Dtos.Leads;

/// <summary>
/// Alta de lead desde el formulario público. No lleva email de remitente autenticado:
/// el visitante es anónimo y se lo identifica por el PublicToken que se le devuelve.
/// </summary>
public sealed record CreateLeadRequest(
    string Name,
    string Email,
    string? Phone,
    Guid? ServiceId,
    string Details,
    decimal? BudgetMin,
    decimal? BudgetMax,
    string? Currency,
    DateOnly? TargetStartDate,
    LeadSource Source = LeadSource.Web,
    string? Website = null);

/// <summary>
/// Respuesta del alta. El token viaja una vez y es la única credencial del cliente:
/// con él puede consultar el estado de su consulta y sus propuestas.
/// </summary>
public sealed record PublicLeadCreatedResponse(Guid Id, string PublicToken, LeadStatus Status, DateTimeOffset CreatedAtUtc);

/// <summary>
/// Vista del cliente sobre su propia consulta. Deliberadamente no expone
/// InternalNotes ni datos del usuario interno que atiende el lead.
/// </summary>
public sealed record PublicLeadDto(
    Guid Id,
    string PublicToken,
    string Name,
    LeadStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateOnly? TargetStartDate,
    IReadOnlyList<PublicProposalSummaryDto> Proposals);

/// <summary>Propuesta tal como la ve el cliente: sin notas internas ni márgenes.</summary>
public sealed record PublicProposalSummaryDto(
    Guid Id,
    string Number,
    string Title,
    ProposalStatus Status,
    string Currency,
    decimal TotalAmount,
    DateTimeOffset? SentAtUtc,
    DateTimeOffset? ValidUntil,
    string? PdfUrl);

public sealed record LeadListItemDto(
    Guid Id,
    string Name,
    string Email,
    string? Phone,
    LeadStatus Status,
    LeadSource Source,
    decimal? BudgetMin,
    decimal? BudgetMax,
    string? Currency,
    Guid? HandledByUserId,
    string? HandledByName,
    int ProposalCount,
    int ConversationCount,
    DateTimeOffset CreatedAtUtc);

public sealed record LeadDetailDto(
    Guid Id,
    string PublicToken,
    string Name,
    string Email,
    string? Phone,
    Guid? ServiceId,
    string? ServiceName,
    string Details,
    LeadStatus Status,
    LeadSource Source,
    decimal? BudgetMin,
    decimal? BudgetMax,
    string? Currency,
    DateOnly? TargetStartDate,
    string? InternalNotes,
    Guid? HandledByUserId,
    string? HandledByName,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<ProposalSummaryDto> Proposals,
    IReadOnlyList<ConversationSummaryDto> Conversations);

public sealed record ProposalSummaryDto(
    Guid Id,
    string Number,
    string Title,
    ProposalStatus Status,
    string Currency,
    decimal TotalAmount,
    DateTimeOffset? SentAtUtc,
    DateTimeOffset? ValidUntil,
    Guid? CreatedByUserId,
    string? CreatedByName,
    DateTimeOffset CreatedAtUtc);

public sealed record ConversationSummaryDto(
    Guid Id,
    string Subject,
    ConversationStatus Status,
    int HighestSeverity,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastMessageAtUtc);

public sealed record UpdateLeadStatusRequest(LeadStatus Status);

public sealed record AssignLeadRequest(Guid? UserId);

public sealed record UpdateLeadNotesRequest(string? InternalNotes);
