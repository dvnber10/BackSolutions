using BackSolutions.Core.Common;
using BackSolutions.Core.Enums;
using BackSolutions.Core.Entities.Identity;

namespace BackSolutions.Core.Entities.Leads;

/// <summary>
/// Propuesta comercial. La redacta el equipo desde la app Kotlin; el sistema
/// se encarga de renderizarla a PDF y de enviarla por correo al cliente.
/// </summary>
public class Proposal : IHasTimestamps
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Correlativo legible, por ejemplo "BS-2026-014".</summary>
    public string Number { get; set; } = string.Empty;

    public Guid LeadId { get; set; }
    public Lead Lead { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    /// <summary>Alcance y condiciones en HTML. Es lo que se convierte en el cuerpo del PDF.</summary>
    public string SummaryHtml { get; set; } = string.Empty;

    public ProposalStatus Status { get; set; } = ProposalStatus.Draft;

    public string Currency { get; set; } = "USD";

    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public string? NotesHtml { get; set; }

    public DateTimeOffset? SentAtUtc { get; set; }
    public DateTimeOffset? ViewedAtUtc { get; set; }
    public DateTimeOffset? ValidUntil { get; set; }

    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ProposalItem> Items { get; set; } = [];
}
