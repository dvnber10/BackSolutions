using BackSolutions.Api.Hubs;
using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Chat;
using BackSolutions.Core.Entities.Chat;
using BackSolutions.Core.Enums;
using BackSolutions.Core.Interfaces;
using BackSolutions.Data;
using Microsoft.EntityFrameworkCore;

namespace BackSolutions.Api.Services;

/// <summary>
/// Chat cliente ↔ equipo.
///
/// Reglas que sostienen todo el servicio:
///
/// - Los mensajes internos (IsInternal) existen para que el equipo hable entre sí sin que
///   el cliente lo lea. Se filtran en <see cref="GetPublicConversationAsync"/> y
///   <see cref="SendPublicMessageAsync"/>.
/// - El estado de la conversación se mueve solo según quién escribió el último mensaje:
///   si escribió el equipo, queda esperando al cliente, y al revés. Así el panel puede
///   filtrar "esperando respuesta" sin lógica adicional.
/// - La primera respuesta del equipo sella FirstResponseAtUtc, que es el indicador de SLA.
/// - HighestSeverity guarda el peor escalamiento de la conversación y solo sube, para
///   que la bandeja pueda ordenar por urgencia sin unir contra Escalations.
/// </summary>
public sealed class ChatService : IChatService
{
    /// <summary>Tope del adjunto. Coherente con el varbinary(max) de la columna.</summary>
    private const long MaxAttachmentBytes = 5 * 1024 * 1024;

    private const int MaxMessageLength = 4000;

    private const int MaxSubjectLength = 200;

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly ChatEventBus _bus;

    public ChatService(AppDbContext db, ICurrentUser currentUser, IClock clock, ChatEventBus bus)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _bus = bus;
    }

    public event Func<MessageDto, Guid, CancellationToken, Task>? MessageAdded;

    public event Func<EscalationDto, Guid, CancellationToken, Task>? EscalationChanged;

    // ─────────────────────────────────────────────────────────────
    // Cliente web
    // ─────────────────────────────────────────────────────────────

    public async Task<ConversationStartedResponse> StartConversationAsync(
        StartConversationRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>();
        var subject = request.Subject?.Trim() ?? string.Empty;
        var body = request.Message?.Trim() ?? string.Empty;

        if (subject.Length == 0)
        {
            errors[nameof(request.Subject)] = ["El asunto es obligatorio."];
        }
        else if (subject.Length > MaxSubjectLength)
        {
            errors[nameof(request.Subject)] = [$"El asunto no puede superar los {MaxSubjectLength} caracteres."];
        }

        if (body.Length == 0)
        {
            errors[nameof(request.Message)] = ["Escribí tu consulta."];
        }
        else if (body.Length > MaxMessageLength)
        {
            errors[nameof(request.Message)] = [$"El mensaje no puede superar los {MaxMessageLength:N0} caracteres."];
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors[nameof(request.Name)] = ["El nombre es obligatorio."];
        }

        if (string.IsNullOrWhiteSpace(request.Email) || !LooksLikeEmail(request.Email))
        {
            errors[nameof(request.Email)] = ["El email es obligatorio y debe ser válido."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var attachment = DecodeAttachment(request, errors);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var now = _clock.UtcNow;

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            PublicToken = SecureToken.Generate(),
            LeadId = request.LeadId,
            ClientName = request.Name.Trim(),
            ClientEmail = request.Email.Trim().ToLowerInvariant(),
            ClientPhone = request.Phone?.Trim(),
            Subject = subject,
            Status = ConversationStatus.AwaitingTeam,
            HighestSeverity = -1,
            LastMessageAtUtc = now,
            CreatedAtUtc = now
        };

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            SenderType = MessageSenderType.Client,
            Body = body,
            SentAtUtc = now,
            AttachmentName = attachment.Name,
            AttachmentContentType = attachment.ContentType,
            AttachmentContent = attachment.Content
        };

        conversation.Messages.Add(message);

        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = MapMessage(message, null);

        await NotifyMessageAsync(dto, conversation.Id, cancellationToken);

        return new ConversationStartedResponse(conversation.Id, conversation.PublicToken, conversation.Status, now, dto);
    }

    public async Task<PublicConversationDto?> GetPublicConversationAsync(string publicToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(publicToken))
        {
            return null;
        }

        var conversation = await _db.Conversations
            .AsNoTracking()
            .Include(c => c.Messages)
            .ThenInclude(m => m.SenderUser)
            .FirstOrDefaultAsync(c => c.PublicToken == publicToken, cancellationToken);

        if (conversation is null)
        {
            return null;
        }

        // Un mensaje interno nunca sale por acá, aunque se pida el historial completo.
        var visible = conversation.Messages
            .Where(m => !m.IsInternal)
            .OrderBy(m => m.SentAtUtc)
            .Select(m => MapMessage(m, m.SenderUser?.FullName))
            .ToList();

        return new PublicConversationDto(
            conversation.Id,
            conversation.PublicToken,
            conversation.Subject,
            conversation.Status,
            conversation.CreatedAtUtc,
            conversation.LastMessageAtUtc,
            conversation.CloseReason,
            visible);
    }

    public async Task<MessageDto> SendPublicMessageAsync(
        string publicToken,
        SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(publicToken))
        {
            throw new NotFoundException("La conversación no existe.");
        }

        var conversation = await _db.Conversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.PublicToken == publicToken, cancellationToken)
            ?? throw new NotFoundException("La conversación no existe.");

        if (conversation.Status == ConversationStatus.Closed)
        {
            throw new ConflictException("Esta conversación está cerrada. Abrí una nueva para continuar.");
        }

        // El cliente no puede crear mensajes internos: se ignora el flag en vez de confiar en él.
        await AddMessageAsync(conversation, request.Body, MessageSenderType.Client, null, isInternal: false, cancellationToken);

        var message = conversation.Messages.OrderByDescending(m => m.SentAtUtc).First();
        var dto = MapMessage(message, null);

        await NotifyMessageAsync(dto, conversation.Id, cancellationToken);

        return dto;
    }

    public async Task<(byte[] Content, string FileName, string ContentType)> GetAttachmentAsync(
        string publicToken,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        var message = await _db.Messages
            .AsNoTracking()
            .FirstOrDefaultAsync(m =>
                m.Id == messageId
                && m.Conversation!.PublicToken == publicToken
                && !m.IsInternal,
                cancellationToken)
            ?? throw new NotFoundException("El adjunto no existe.");

        if (message.AttachmentContent is null || message.AttachmentContent.Length == 0)
        {
            throw new NotFoundException("El mensaje no tiene adjunto.");
        }

        return (message.AttachmentContent, message.AttachmentName ?? "adjunto", message.AttachmentContentType ?? "application/octet-stream");
    }

    // ─────────────────────────────────────────────────────────────
    // Equipo
    // ─────────────────────────────────────────────────────────────

    public async Task<PagedResult<ConversationListItemDto>> ListAsync(
        int? page,
        int? pageSize,
        ConversationStatusFilter status,
        Guid? assignedToUserId,
        EscalationSeverityFilter minSeverity,
        CancellationToken cancellationToken = default)
    {
        var (currentPage, size) = Paging.Normalize(page, pageSize);

        var query = _db.Conversations
            .AsNoTracking()
            .Include(c => c.AssignedToUser)
            .AsQueryable();

        var targetStatus = ToStatus(status);


        query = status switch
        {
            ConversationStatusFilter.All => query,
            _ => query.Where(c => c.Status == targetStatus)
        };

        if (assignedToUserId is { } userId)
        {
            query = query.Where(c => c.AssignedToUserId == userId);
        }

        // HighestSeverity arranca en -1 (sin escalamientos), así que un filtro
        // Low (0) también incluye las conversaciones nunca escaladas.
        if (minSeverity != EscalationSeverityFilter.Any)
        {
            var threshold = (int)minSeverity;
            query = query.Where(c => c.HighestSeverity >= threshold);
        }

        // Primero lo más urgente y lo más reciente; una conversación escalada
        // nunca debe quedar debajo de un chat trivial.
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.HighestSeverity)
            .ThenByDescending(c => c.LastMessageAtUtc ?? c.CreatedAtUtc)
            .Skip((currentPage - 1) * size)
            .Take(size)
            .Select(c => new ConversationListItemDto(
                c.Id, c.Subject, c.ClientName, c.ClientEmail, c.Status, c.HighestSeverity,
                c.AssignedToUserId, c.AssignedToUser!.FullName,
                c.Messages.Count,
                c.Messages.Count(m => m.SenderType == MessageSenderType.Client && m.ReadAtUtc == null),
                c.CreatedAtUtc, c.LastMessageAtUtc, c.FirstResponseAtUtc))
            .ToListAsync(cancellationToken);

        return PagedResult<ConversationListItemDto>.Create(items, total, currentPage, size);
    }

    public async Task<ConversationDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var conversation = await LoadDetailAsync(id, cancellationToken);
        return MapDetail(conversation);
    }

    public async Task<MessageDto> SendTeamMessageAsync(
        Guid conversationId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _db.Conversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken)
            ?? throw new NotFoundException("La conversación no existe.");

        if (conversation.Status == ConversationStatus.Closed)
        {
            throw new ConflictException("La conversación está cerrada. Reabrila si necesitás continuarla.");
        }

        var senderId = _currentUser.RequireUserId();
        var senderName = _currentUser.Email;

        var lastMessage = await AddMessageAsync(conversation, request.Body, MessageSenderType.Team, senderId, request.IsInternal, cancellationToken);

        var dto = MapMessage(lastMessage, senderName);

        await NotifyMessageAsync(dto, conversation.Id, cancellationToken);

        return dto;
    }

    public async Task<ConversationDetailDto> AssignAsync(
        Guid id,
        AssignConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.UserId is { } userId)
        {
            var canServe = await _db.Users
                .Where(u => u.Id == userId && u.IsActive)
                .Select(u => u.UserRoles.Any(ur => ur.Role!.Name != "Admin"))
                .FirstOrDefaultAsync(cancellationToken);

            if (!canServe)
            {
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    [nameof(request.UserId)] = ["El usuario no existe, está desactivado o no atiende chat."]
                });
            }
        }

        var conversation = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("La conversación no existe.");

        conversation.AssignedToUserId = request.UserId;
        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<ConversationDetailDto> CloseAsync(
        Guid id,
        CloseConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Reason)] = ["Indicá el motivo de cierre: queda en el historial."]
            });
        }

        var conversation = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("La conversación no existe.");

        conversation.Status = ConversationStatus.Closed;
        conversation.ClosedAtUtc = _clock.UtcNow;
        conversation.CloseReason = request.Reason.Trim();

        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<EscalationDto> CreateEscalationAsync(
        Guid conversationId,
        CreateEscalationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Reason)] = ["El motivo del escalamiento es obligatorio."]
            });
        }

        var conversation = await _db.Conversations
            .Include(c => c.Escalations)
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken)
            ?? throw new NotFoundException("La conversación no existe.");

        var severity = (int)request.Severity;
        var now = _clock.UtcNow;

        var escalation = new Escalation
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            MessageId = request.MessageId,
            Severity = request.Severity,
            Status = EscalationStatus.Open,
            Reason = request.Reason.Trim(),
            RaisedByUserId = _currentUser.RequireUserId(),
            AssignedToUserId = _currentUser.UserId,
            CreatedAtUtc = now
        };

        _db.Escalations.Add(escalation);

        // HighestSeverity solo sube: resolver un escalamiento crítico no debe bajar
        // la urgencia histórica de la conversación.
        conversation.HighestSeverity = Math.Max(conversation.HighestSeverity, severity);

        await _db.SaveChangesAsync(cancellationToken);

        var dto = MapEscalation(escalation, conversation.Id);

        await NotifyEscalationAsync(dto, conversation.Id, cancellationToken);

        return dto;
    }

    public async Task<EscalationDto> UpdateEscalationStatusAsync(
        Guid escalationId,
        UpdateEscalationStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var escalation = await _db.Escalations
            .FirstOrDefaultAsync(e => e.Id == escalationId, cancellationToken)
            ?? throw new NotFoundException("El escalamiento no existe.");

        if (escalation.Status == EscalationStatus.Resolved && request.Status != EscalationStatus.Resolved)
        {
            throw new ConflictException("Un escalamiento resuelto no vuelve a abrirse. Creá uno nuevo.");
        }

        escalation.Status = request.Status;

        if (request.Status == EscalationStatus.Resolved)
        {
            escalation.ResolvedAtUtc = _clock.UtcNow;
            escalation.ResolutionNote = request.ResolutionNote?.Trim();
            escalation.AssignedToUserId ??= _currentUser.UserId;
        }

        if (request.Status == EscalationStatus.Acknowledged)
        {
            escalation.AssignedToUserId ??= _currentUser.UserId;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var dto = MapEscalation(escalation, escalation.ConversationId);

        await NotifyEscalationAsync(dto, escalation.ConversationId, cancellationToken);

        return dto;
    }

    // ─────────────────────────────────────────────────────────────
    // Internos
    // ─────────────────────────────────────────────────────────────

    private async Task<Message> AddMessageAsync(
        Conversation conversation,
        string? body,
        MessageSenderType senderType,
        Guid? senderUserId,
        bool isInternal,
        CancellationToken cancellationToken)
    {
        var text = body?.Trim() ?? string.Empty;

        if (text.Length == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["body"] = ["El mensaje no puede estar vacío."]
            });
        }

        if (text.Length > MaxMessageLength)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["body"] = [$"El mensaje no puede superar los {MaxMessageLength:N0} caracteres."]
            });
        }

        var now = _clock.UtcNow;

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            SenderType = senderType,
            SenderUserId = senderType == MessageSenderType.Team ? senderUserId : null,
            Body = text,
            IsInternal = isInternal,
            SentAtUtc = now
        };

        conversation.Messages.Add(message);

        // AddNew va despues del Add a la coleccion: si se fija el estado antes, el fixup
        // de EF ya deja el mensaje en conversation.Messages y el Add explicito lo duplica
        // (las colecciones de navegacion son listas y no deduplican).
        _db.AddNew(message);

        conversation.LastMessageAtUtc = now;

        // Un mensaje interno no es visible para el cliente, así que no mueve el estado
        // de espera ni cuenta como respuesta.
        if (!isInternal)
        {
            conversation.Status = senderType == MessageSenderType.Client
                ? ConversationStatus.AwaitingTeam
                : ConversationStatus.AwaitingClient;

            if (senderType == MessageSenderType.Team && conversation.FirstResponseAtUtc is null)
            {
                conversation.FirstResponseAtUtc = now;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return message;
    }

    private async Task<Conversation> LoadDetailAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.Conversations
            .AsNoTracking()
            .Include(c => c.Lead)
            .Include(c => c.AssignedToUser)
            .Include(c => c.Messages)
            .ThenInclude(m => m.SenderUser)
            .Include(c => c.Escalations)
            .ThenInclude(e => e.RaisedByUser)
            .Include(c => c.Escalations)
            .ThenInclude(e => e.AssignedToUser)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("La conversación no existe.");

    private static (byte[]? Content, string? Name, string? ContentType) DecodeAttachment(
        StartConversationRequest request,
        Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(request.AttachmentBase64))
        {
            return (null, null, null);
        }

        byte[] content;

        try
        {
            // Puede venir con el prefijo data: URL que produce el FileReader del navegador.
            var payload = request.AttachmentBase64;
            var commaIndex = payload.IndexOf(',');

            if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex > 0)
            {
                payload = payload[(commaIndex + 1)..];
            }

            content = Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            errors[nameof(request.AttachmentBase64)] = ["El adjunto no es un base64 válido."];
            return (null, null, null);
        }

        if (content.LongLength > MaxAttachmentBytes)
        {
            errors[nameof(request.AttachmentBase64)] = [$"El adjunto supera el máximo de {MaxAttachmentBytes / 1024 / 1024} MB."];
            return (null, null, null);
        }

        return (content, request.AttachmentName?.Trim(), request.AttachmentContentType?.Trim() ?? "application/octet-stream");
    }

    private static bool LooksLikeEmail(string email) =>
        !string.IsNullOrWhiteSpace(email)
        && email.Contains('@')
        && !email.Any(char.IsWhiteSpace)
        && email.Length <= 320;

    /// <summary>
    /// Notifica un mensaje nuevo por los dos caminos.
    ///
    /// El evento local alcanza solo a quien se suscribió en este mismo scope, y el Hub no
    /// lo está: resuelve otra instancia del servicio. Por eso además se publica en
    /// <see cref="ChatEventBus"/>, que es singleton y sí llega al transporte.
    /// </summary>
    private async Task NotifyMessageAsync(MessageDto dto, Guid conversationId, CancellationToken cancellationToken)
    {
        await RaiseAsync(MessageAdded, dto, conversationId, cancellationToken);
        await _bus.PublishMessageAsync(dto, conversationId, cancellationToken);
    }

    /// <summary>Igual que <see cref="NotifyMessageAsync"/>, para cambios de escalamiento.</summary>
    private async Task NotifyEscalationAsync(EscalationDto dto, Guid conversationId, CancellationToken cancellationToken)
    {
        await RaiseAsync(EscalationChanged, dto, conversationId, cancellationToken);
        await _bus.PublishEscalationAsync(dto, conversationId, cancellationToken);
    }

    private static async Task RaiseAsync<T>(Func<T, Guid, CancellationToken, Task>? handler, T payload, Guid conversationId, CancellationToken cancellationToken)
    {
        if (handler is null)
        {
            return;
        }

        try
        {
            await handler(payload, conversationId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // El cliente cortó la conexión: la notificación es best-effort.
        }
    }

    private static MessageDto MapMessage(Message m, string? senderName) => new(
        m.Id,
        m.SenderType,
        m.SenderUserId,
        senderName,
        m.Body,
        m.IsInternal,
        m.AttachmentName,
        m.AttachmentContentType,
        m.AttachmentContent is { Length: > 0 },
        m.SentAtUtc,
        m.ReadAtUtc);

    private static EscalationDto MapEscalation(Escalation e, Guid conversationId) => new(
        e.Id,
        e.Status,
        e.Severity,
        e.Reason,
        e.RaisedByUserId,
        e.RaisedByUser?.FullName,
        e.AssignedToUserId,
        e.AssignedToUser?.FullName,
        e.ResolutionNote,
        e.ResolvedAtUtc,
        e.CreatedAtUtc);

    private static ConversationDetailDto MapDetail(Conversation c) => new(
        c.Id,
        c.PublicToken,
        c.LeadId,
        c.Lead?.Name,
        c.ClientName,
        c.ClientEmail,
        c.ClientPhone,
        c.Subject,
        c.Status,
        c.HighestSeverity,
        c.AssignedToUserId,
        c.AssignedToUser?.FullName,
        c.CreatedAtUtc,
        c.FirstResponseAtUtc,
        c.LastMessageAtUtc,
        c.ClosedAtUtc,
        c.CloseReason,
        [.. c.Messages.OrderBy(m => m.SentAtUtc).Select(m => MapMessage(m, m.SenderUser?.FullName))],
        [.. c.Escalations.OrderByDescending(e => e.CreatedAtUtc)
            .Select(e => MapEscalation(e, c.Id))]);

    /// <summary>
    /// Traduce el filtro de estado al enum real. El cast tiene que quedar fuera del árbol de
    /// expresiones: EF Core no traduce (ContentStatus)(int)status dentro del Where y termina
    /// generando CAST(CAST(@p AS int) AS nvarchar(32)), que se compara contra 'Published' y no
    /// matchea nunca, dejando todos los filtros por estado en cero resultados sin avisar.
    /// </summary>
    private static ConversationStatus ToStatus(ConversationStatusFilter filter) => (ConversationStatus)(int)filter;
}
