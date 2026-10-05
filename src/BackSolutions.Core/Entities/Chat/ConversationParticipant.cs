using BackSolutions.Core.Entities.Identity;

namespace BackSolutions.Core.Entities.Chat;

/// <summary>
/// Miembro del equipo agregado a una conversación. Permite que varios proveedores
/// colaboren en el mismo hilo sin que todos figuren como asignados.
/// </summary>
public class ConversationParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTimeOffset JoinedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LeftAtUtc { get; set; }
    public DateTimeOffset? LastReadAtUtc { get; set; }
}
