using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Chat;
using BackSolutions.Core.Enums;

namespace BackSolutions.Core.Interfaces;

/// <summary>
/// Chat de clientes con el equipo.
///
/// El cliente web es anónimo y se identifica por <c>Conversation.PublicToken</c>; el
/// equipo entra con JWT. El servicio es la única capa que decide qué puede ver cada uno:
/// en particular, los mensajes internos (<c>IsInternal</c>) nunca se devuelven al cliente,
/// y eso se aplica en el servicio y no en el endpoint, para que no dependa de que alguien
/// recuerde filtrarlos.
///
/// El tiempo real (SignalR) es una preocupación de la capa de transporte: el servicio
/// expone <see cref="NotifyMessageAdded"/> como evento para que el Hub lo reemita, sin
/// que el servicio dependa de SignalR.
/// </summary>
public interface IChatService
{
    // ── Cliente web (público, autenticado por PublicToken) ──

    /// <summary>Abre una conversación con el primer mensaje del cliente.</summary>
    Task<ConversationStartedResponse> StartConversationAsync(StartConversationRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);

    /// <summary>Historial del hilo. Null si el token no existe. Filtra los mensajes internos.</summary>
    Task<PublicConversationDto?> GetPublicConversationAsync(string publicToken, CancellationToken cancellationToken = default);

    /// <summary>El cliente escribe en el hilo. No puede reabrir una conversación cerrada.</summary>
    Task<MessageDto> SendPublicMessageAsync(string publicToken, SendMessageRequest request, CancellationToken cancellationToken = default);

    /// <summary>Descarga el adjunto de un mensaje. Solo si el mensaje es visible para quien pregunta.</summary>
    Task<(byte[] Content, string FileName, string ContentType)> GetAttachmentAsync(string publicToken, Guid messageId, CancellationToken cancellationToken = default);

    // ── Equipo (JWT) ──

    Task<PagedResult<ConversationListItemDto>> ListAsync(
        int? page,
        int? pageSize,
        ConversationStatusFilter status,
        Guid? assignedToUserId,
        EscalationSeverityFilter minSeverity,
        CancellationToken cancellationToken = default);

    Task<ConversationDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<MessageDto> SendTeamMessageAsync(Guid conversationId, SendMessageRequest request, CancellationToken cancellationToken = default);

    Task<ConversationDetailDto> AssignAsync(Guid id, AssignConversationRequest request, CancellationToken cancellationToken = default);

    Task<ConversationDetailDto> CloseAsync(Guid id, CloseConversationRequest request, CancellationToken cancellationToken = default);

    Task<EscalationDto> CreateEscalationAsync(Guid conversationId, CreateEscalationRequest request, CancellationToken cancellationToken = default);

    Task<EscalationDto> UpdateEscalationStatusAsync(Guid escalationId, UpdateEscalationStatusRequest request, CancellationToken cancellationToken = default);

    // ── Notificaciones para el Hub ──

    /// <summary>Se dispara cuando entra un mensaje nuevo, para que el Hub lo reemita por SignalR.</summary>
    event Func<MessageDto, Guid, CancellationToken, Task>? MessageAdded;

    /// <summary>Se dispara cuando cambia el estado de un escalamiento.</summary>
    event Func<EscalationDto, Guid, CancellationToken, Task>? EscalationChanged;
}

/// <summary>Filtro de estado para la bandeja del equipo.</summary>
public enum ConversationStatusFilter
{
    All = -1,
    Open = 0,
    AwaitingClient = 1,
    AwaitingTeam = 2,
    Closed = 3
}

/// <summary>Filtro de severidad mínima para priorizar la bandeja.</summary>
public enum EscalationSeverityFilter
{
    Any = -1,
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
