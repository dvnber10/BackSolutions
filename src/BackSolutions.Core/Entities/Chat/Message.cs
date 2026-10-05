using BackSolutions.Core.Enums;
using BackSolutions.Core.Entities.Identity;

namespace BackSolutions.Core.Entities.Chat;

/// <summary>
/// Mensaje del chat. No implementa IHasTimestamps porque usa
/// <see cref="SentAtUtc"/> como marca temporal de negocio.
/// </summary>
public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;

    public MessageSenderType SenderType { get; set; }

    /// <summary>Null cuando el mensaje lo escribió el cliente anónimo.</summary>
    public Guid? SenderUserId { get; set; }
    public User? SenderUser { get; set; }

    public string Body { get; set; } = string.Empty;

    /// <summary>Nota interna: la ven solo los usuarios del equipo, nunca se envía al cliente.</summary>
    public bool IsInternal { get; set; }

    // ── Adjunto opcional (archivo generado, por ejemplo el PDF de la propuesta) ──
    public string? AttachmentName { get; set; }
    public string? AttachmentContentType { get; set; }
    public byte[]? AttachmentContent { get; set; }

    public DateTimeOffset SentAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAtUtc { get; set; }
}
