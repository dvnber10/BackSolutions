using BackSolutions.Core.Common;
using BackSolutions.Core.Enums;
using BackSolutions.Core.Entities.Identity;

namespace BackSolutions.Core.Entities.Chat;

/// <summary>
/// Escalamiento: marca que una conversación requiere atención prioritaria
/// y deja rastro de quién la pidió, quién la tomó y cómo se resolvió.
/// </summary>
public class Escalation : IHasTimestamps
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;

    /// <summary>
    /// Mensaje que originó el escalamiento, si aplica.
    ///
    /// Referencia SIN clave foránea, a propósito. Una FK desde Escalations hacia
    /// Messages crea un ciclo que SQL Server rechaza con error 1785, porque existirían
    /// dos rutas de borrado entre Conversations y Escalations: la directa por
    /// ConversationId y la indirecta a través de Messages. Romper el ciclo aquí es
    /// correcto: los mensajes del chat nunca se borran (son el historial) y al
    /// escalamiento le interesa el contexto, no la integridad referencial.
    /// </summary>
    public Guid? MessageId { get; set; }

    public EscalationSeverity Severity { get; set; }
    public EscalationStatus Status { get; set; } = EscalationStatus.Open;

    public string Reason { get; set; } = string.Empty;

    public Guid? RaisedByUserId { get; set; }
    public User? RaisedByUser { get; set; }

    public Guid? AssignedToUserId { get; set; }
    public User? AssignedToUser { get; set; }

    public string? ResolutionNote { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
