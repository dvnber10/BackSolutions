using BackSolutions.Core.Common;
using BackSolutions.Core.Enums;
using BackSolutions.Core.Entities.Identity;
using BackSolutions.Core.Entities.Leads;

namespace BackSolutions.Core.Entities.Chat;

/// <summary>
/// Hilo de chat entre un cliente del sitio y el equipo. Puede originarse
/// por el formulario de cotización (queda ligado a un <see cref="Lead"/>) o
/// por una consulta directa desde la web.
/// </summary>
public class Conversation : IHasTimestamps
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Token con el que el cliente anónimo retoma su conversación.</summary>
    public string PublicToken { get; set; } = string.Empty;

    public Guid? LeadId { get; set; }
    public Lead? Lead { get; set; }

    public string ClientName { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string? ClientPhone { get; set; }

    public string Subject { get; set; } = string.Empty;

    public ConversationStatus Status { get; set; } = ConversationStatus.Open;

    /// <summary>Peor severidad alcanzada en los escalamientos. 0 = sin escalamiento.</summary>
    public int HighestSeverity { get; set; }

    public Guid? AssignedToUserId { get; set; }
    public User? AssignedToUser { get; set; }

    /// <summary>Se usa para medir el tiempo de primera respuesta.</summary>
    public DateTimeOffset? FirstResponseAtUtc { get; set; }

    public DateTimeOffset? LastMessageAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }

    public string? CloseReason { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Message> Messages { get; set; } = [];
    public ICollection<ConversationParticipant> Participants { get; set; } = [];
    public ICollection<Escalation> Escalations { get; set; } = [];
}
