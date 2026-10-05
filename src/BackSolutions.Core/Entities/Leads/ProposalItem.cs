namespace BackSolutions.Core.Entities.Leads;

/// <summary>Línea del presupuesto de una propuesta.</summary>
public class ProposalItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProposalId { get; set; }
    public Proposal Proposal { get; set; } = null!;

    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1m;
    public decimal UnitPrice { get; set; }

    /// <summary>Descuento por línea, de 0 a 100.</summary>
    public decimal? DiscountPercent { get; set; }

    public int SortOrder { get; set; }
}
