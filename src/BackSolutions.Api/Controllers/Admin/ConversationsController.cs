using BackSolutions.Api.Security;
using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Chat;
using BackSolutions.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackSolutions.Api.Controllers.Admin;

/// <summary>
/// Bandeja de conversaciones del equipo.
///
/// El filtrado de mensajes internos lo hace <see cref="IChatService"/>, no el endpoint:
/// acá solo se enruta. Quien esté mirando el código tiene que poder confiar en que la
/// separación cliente/equipo no depende de acordarse de filtrar.
/// </summary>
[ApiController]
[Route("api/admin/conversations")]
[Authorize(Policy = AuthorizationPolicies.TeamMember)]
[Produces("application/json")]
public sealed class ConversationsController : ControllerBase
{
    private readonly IChatService _chat;

    public ConversationsController(IChatService chat) => _chat = chat;

    /// <summary>
    /// Bandeja. <c>minSeverity</c> prioriza las conversaciones con escalamientos graves,
    /// que es lo primero que hay que atender.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ConversationListItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ConversationListItemDto>>> List(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] ConversationStatusFilter status = ConversationStatusFilter.All,
        [FromQuery] Guid? assignedToUserId = null,
        [FromQuery] EscalationSeverityFilter minSeverity = EscalationSeverityFilter.Any,
        CancellationToken cancellationToken = default) =>
        Ok(await _chat.ListAsync(page, pageSize, status, assignedToUserId, minSeverity, cancellationToken));

    /// <summary>Hilo completo, con mensajes internos y escalamientos.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ConversationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationDetailDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await _chat.GetByIdAsync(id, cancellationToken));

    /// <summary>
    /// Responde al cliente. <c>isInternal</c> en true deja la nota solo para el equipo:
    /// nunca se envía al Hub del cliente ni aparece en el endpoint público.
    /// </summary>
    [HttpPost("{id:guid}/messages")]
    [ProducesResponseType<MessageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MessageDto>> SendMessage(
        Guid id,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var message = await _chat.SendTeamMessageAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, message);
    }

    /// <summary>Asigna la conversación a un miembro del equipo.</summary>
    [HttpPut("{id:guid}/assignee")]
    [ProducesResponseType<ConversationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationDetailDto>> Assign(
        Guid id,
        [FromBody] AssignConversationRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _chat.AssignAsync(id, request, cancellationToken));

    /// <summary>Cierra la conversación. Desde acá el cliente no puede volver a escribir.</summary>
    [HttpPut("{id:guid}/close")]
    [ProducesResponseType<ConversationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConversationDetailDto>> Close(
        Guid id,
        [FromBody] CloseConversationRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _chat.CloseAsync(id, request, cancellationToken));

    // ── Escalamientos ─────────────────────────────────────────

    /// <summary>
    /// Escala la conversación. El mensaje origen queda registrado aunque la relación se
    /// guarde sin FK: es la evidencia de qué mensaje Provocó la escalación.
    /// </summary>
    [HttpPost("{id:guid}/escalations")]
    [ProducesResponseType<EscalationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EscalationDto>> CreateEscalation(
        Guid id,
        [FromBody] CreateEscalationRequest request,
        CancellationToken cancellationToken)
    {
        var escalation = await _chat.CreateEscalationAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, escalation);
    }

    [HttpPut("escalations/{escalationId:guid}/status")]
    [ProducesResponseType<EscalationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EscalationDto>> UpdateEscalationStatus(
        Guid escalationId,
        [FromBody] UpdateEscalationStatusRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _chat.UpdateEscalationStatusAsync(escalationId, request, cancellationToken));
}
