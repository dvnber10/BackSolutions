using BackSolutions.Core.Enums;

namespace BackSolutions.Core.Dtos.Chat;

public sealed record StartConversationRequest(
    string Subject,
    string Message,
    string Name,
    string Email,
    string? Phone,
    Guid? LeadId,
    Guid? ServiceId,
    string? AttachmentName = null,
    string? AttachmentContentType = null,
    string? AttachmentBase64 = null);

/// <summary>
/// Respuesta del alta de conversación. El cliente guarda el PublicToken en el navegador
/// y lo manda en cada llamada; es lo único que lo identifica.
/// </summary>
public sealed record ConversationStartedResponse(
    Guid Id,
    string PublicToken,
    ConversationStatus Status,
    DateTimeOffset CreatedAtUtc,
    MessageDto FirstMessage);

/// <summary>Mensaje del hilo. Nunca incluye el binario del adjunto, solo su metadata.</summary>
public sealed record MessageDto(
    Guid Id,
    MessageSenderType SenderType,
    Guid? SenderUserId,
    string? SenderName,
    string Body,
    bool IsInternal,
    string? AttachmentName,
    string? AttachmentContentType,
    bool HasAttachment,
    DateTimeOffset SentAtUtc,
    DateTimeOffset? ReadAtUtc);

public sealed record SendMessageRequest(string Body, bool IsInternal = false);

/// <summary>
/// Historial del hilo visto por el cliente. Los mensajes internos del equipo se filtran
/// acá, no en el endpoint: es la regla que no debe depender de que se recuerde filtrarlos.
/// </summary>
public sealed record PublicConversationDto(
    Guid Id,
    string PublicToken,
    string Subject,
    ConversationStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastMessageAtUtc,
    string? CloseReason,
    IReadOnlyList<MessageDto> Messages);

public sealed record EscalationDto(
    Guid Id,
    EscalationStatus Status,
    EscalationSeverity Severity,
    string Reason,
    Guid? RaisedByUserId,
    string? RaisedByName,
    Guid? AssignedToUserId,
    string? AssignedToName,
    string? ResolutionNote,
    DateTimeOffset? ResolvedAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record ConversationListItemDto(
    Guid Id,
    string Subject,
    string ClientName,
    string ClientEmail,
    ConversationStatus Status,
    int HighestSeverity,
    Guid? AssignedToUserId,
    string? AssignedToName,
    int MessageCount,
    int UnreadCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastMessageAtUtc,
    DateTimeOffset? FirstResponseAtUtc);

public sealed record ConversationDetailDto(
    Guid Id,
    string PublicToken,
    Guid? LeadId,
    string? LeadName,
    string ClientName,
    string ClientEmail,
    string? ClientPhone,
    string Subject,
    ConversationStatus Status,
    int HighestSeverity,
    Guid? AssignedToUserId,
    string? AssignedToName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? FirstResponseAtUtc,
    DateTimeOffset? LastMessageAtUtc,
    DateTimeOffset? ClosedAtUtc,
    string? CloseReason,
    IReadOnlyList<MessageDto> Messages,
    IReadOnlyList<EscalationDto> Escalations);

public sealed record CreateEscalationRequest(EscalationSeverity Severity, string Reason, Guid? MessageId = null);

public sealed record UpdateEscalationStatusRequest(EscalationStatus Status, string? ResolutionNote = null);

public sealed record CloseConversationRequest(string Reason);

public sealed record AssignConversationRequest(Guid? UserId);
