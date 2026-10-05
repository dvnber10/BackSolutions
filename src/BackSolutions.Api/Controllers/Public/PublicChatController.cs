using BackSolutions.Api.Security;
using BackSolutions.Core.Dtos.Chat;
using BackSolutions.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BackSolutions.Api.Controllers.Public;

/// <summary>
/// Chat del cliente web.
///
/// El visitante se identifica con el <c>publicToken</c> de la conversación, que recibe
/// al abrirla. No hay login ni cookie: es una conversación con un desconocido que no
/// llegó a registrarse en el sistema.
///
/// El filtro de mensajes internos no está en este controller sino en
/// <see cref="IChatService"/>: si el filtrado dependiera de acordarse de excluir
/// <c>IsInternal</c>, bastaría un endpoint nuevo para filtrar notas internas.
/// </summary>
[ApiController]
[Route("api/public/conversations")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class PublicChatController : ControllerBase
{
    private readonly IChatService _chat;
    private readonly ICurrentUser _currentUser;

    public PublicChatController(IChatService chat, ICurrentUser currentUser)
    {
        _chat = chat;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Abre una conversación con el primer mensaje. Devuelve el token con el que el
    /// cliente va a seguir escribiendo y conectar al Hub.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.PublicWrites)]
    [ProducesResponseType<ConversationStartedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ConversationStartedResponse>> Start(
        [FromBody] StartConversationRequest request,
        CancellationToken cancellationToken)
    {
        var started = await _chat.StartConversationAsync(
            request,
            _currentUser.IpAddress,
            _currentUser.UserAgent,
            cancellationToken);

        return Created(string.Empty, started);
    }

    /// <summary>
    /// Historial del hilo. Es el endpoint que la app usa al recargar: sin esto, un
    /// refresh perdería los mensajes que llegaron mientras la pestaña estaba cerrada.
    /// </summary>
    [HttpGet("{publicToken}")]
    [ProducesResponseType<PublicConversationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicConversationDto>> History(string publicToken, CancellationToken cancellationToken)
    {
        var conversation = await _chat.GetPublicConversationAsync(publicToken, cancellationToken);
        return conversation is null ? NotFound() : Ok(conversation);
    }

    /// <summary>El cliente escribe en el hilo. No puede reabrir una conversación cerrada.</summary>
    [HttpPost("{publicToken}/messages")]
    [EnableRateLimiting(RateLimitPolicies.PublicWrites)]
    [ProducesResponseType<MessageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<MessageDto>> Send(
        string publicToken,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var message = await _chat.SendPublicMessageAsync(publicToken, request, cancellationToken);
        return Created(string.Empty, message);
    }

    /// <summary>
    /// Descarga un adjunto. El servicio verifica que el mensaje pertenezca a la
    /// conversación del token, así que un token no alcanza para leer adjuntos ajenos.
    /// </summary>
    [HttpGet("{publicToken}/attachments/{messageId:guid}")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Attachment(
        string publicToken,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var (content, fileName, contentType) =
            await _chat.GetAttachmentAsync(publicToken, messageId, cancellationToken);

        return File(content, contentType, fileName);
    }
}
