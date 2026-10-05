using BackSolutions.Core.Enums;

namespace BackSolutions.Core.Dtos.Proposals;

public sealed record ProposalItemDto(
    Guid Id,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal? DiscountPercent,
    int SortOrder)
{
    /// <summary>Importe de la línea ya con el descuento de la línea aplicado.</summary>
    public decimal LineTotal => Math.Round(
        Quantity * UnitPrice * (1 - (DiscountPercent ?? 0m) / 100m), 2);
}

public sealed record ProposalDto(
    Guid Id,
    string Number,
    Guid LeadId,
    string LeadName,
    string LeadEmail,
    string Title,
    string SummaryHtml,
    string? NotesHtml,
    ProposalStatus Status,
    string Currency,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    DateTimeOffset? SentAtUtc,
    DateTimeOffset? ViewedAtUtc,
    DateTimeOffset? ValidUntil,
    Guid? CreatedByUserId,
    string? CreatedByName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<ProposalItemDto> Items);

public sealed record ProposalListItemDto(
    Guid Id,
    string Number,
    string Title,
    Guid LeadId,
    string LeadName,
    ProposalStatus Status,
    string Currency,
    decimal TotalAmount,
    DateTimeOffset? SentAtUtc,
    DateTimeOffset? ValidUntil,
    Guid? CreatedByUserId,
    string? CreatedByName,
    DateTimeOffset CreatedAtUtc);

public sealed record SaveProposalItemRequest(
    Guid? Id,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal? DiscountPercent,
    int SortOrder);

public sealed record SaveProposalRequest(
    Guid LeadId,
    string Title,
    string SummaryHtml,
    string? NotesHtml,
    string? Currency,
    decimal DiscountAmount,
    decimal TaxAmount,
    DateTimeOffset? ValidUntil,
    IReadOnlyList<SaveProposalItemRequest> Items);

public sealed record UpdateProposalStatusRequest(ProposalStatus Status, string? Reason = null);

/// <summary>Resultado de renderizar el PDF. El servicio devuelve bytes, no guarda archivos.</summary>
public sealed record ProposalPdf(byte[] Content, string FileName, string ContentType)
{
    public const string PdfContentType = "application/pdf";
}
