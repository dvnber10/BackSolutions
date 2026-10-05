using BackSolutions.Core.Common;
using BackSolutions.Core.Enums;
using BackSolutions.Core.Entities.Chat;
using BackSolutions.Core.Entities.Content;
using BackSolutions.Core.Entities.Identity;

namespace BackSolutions.Core.Entities.Leads;

/// <summary>
/// Cotización enviada desde el formulario web. El visitante es anónimo:
/// recibe un <see cref="PublicToken"/> con el que recupera su cotización.
/// A partir de un lead nacen la propuesta comercial y la conversación de chat.
/// </summary>
public class Lead : IHasTimestamps
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Token opaco (GUID) con el que el cliente accede a su cotización sin registrarse.</summary>
    public string PublicToken { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }

    public Guid? ServiceId { get; set; }
    public ServiceItem? Service { get; set; }

    public string Details { get; set; } = string.Empty;

    public LeadStatus Status { get; set; } = LeadStatus.New;
    public LeadSource Source { get; set; } = LeadSource.Web;

    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public string? Currency { get; set; }

    public DateOnly? TargetStartDate { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    /// <summary>Notas internas. Nunca se exponen en endpoints públicos.</summary>
    public string? InternalNotes { get; set; }

    public Guid? HandledByUserId { get; set; }
    public User? HandledByUser { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Proposal> Proposals { get; set; } = [];
    public ICollection<Conversation> Conversations { get; set; } = [];
}
