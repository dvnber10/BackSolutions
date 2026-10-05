using BackSolutions.Api.Security;
using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Leads;
using BackSolutions.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackSolutions.Api.Controllers.Admin;

/// <summary>
/// Bandeja comercial.
///
/// El alta pública vive en <see cref="Public.PublicLeadsController"/>; acá solo se opera
/// sobre lo que ya entró. Provider y Admin acceden; Support no, porque no trabaja ventas.
/// </summary>
[ApiController]
[Route("api/admin/leads")]
[Authorize(Policy = AuthorizationPolicies.OwnerOrAdmin)]
[Produces("application/json")]
public sealed class LeadsController : ControllerBase
{
    private readonly ILeadService _leads;

    public LeadsController(ILeadService leads) => _leads = leads;

    /// <summary>
    /// Bandeja con filtros combinables. <c>unassignedOnly</c> es un atajo para "mis leads
    /// pendientes de asignar" y por eso no es un valor más de <c>assignedToUserId</c>.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<LeadListItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<LeadListItemDto>>> List(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] LeadStatusFilter status = LeadStatusFilter.All,
        [FromQuery] Guid? assignedToUserId = null,
        [FromQuery] bool unassignedOnly = false,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default) =>
        Ok(await _leads.ListAsync(page, pageSize, status, assignedToUserId, unassignedOnly, search, cancellationToken));

    /// <summary>Ficha completa del lead: notas internas, propuestas y conversaciones.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<LeadDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeadDetailDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await _leads.GetByIdAsync(id, cancellationToken));

    /// <summary>Mueve el lead en el embudo. Los estados finales se validan en el servicio.</summary>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType<LeadDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LeadDetailDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateLeadStatusRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _leads.UpdateStatusAsync(id, request, cancellationToken));

    /// <summary>Asigna un responsable. <c>null</c> en el id deja el lead sin asignar.</summary>
    [HttpPut("{id:guid}/assignee")]
    [ProducesResponseType<LeadDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeadDetailDto>> Assign(
        Guid id,
        [FromBody] AssignLeadRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _leads.AssignAsync(id, request, cancellationToken));

    /// <summary>
    /// Guarda las notas internas. Nunca se exponen por los endpoints públicos: son la
    /// vía por la que el equipo anota contexto comercial del cliente.
    /// </summary>
    [HttpPut("{id:guid}/notes")]
    [ProducesResponseType<LeadDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeadDetailDto>> UpdateNotes(
        Guid id,
        [FromBody] UpdateLeadNotesRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _leads.UpdateNotesAsync(id, request, cancellationToken));

    /// <summary>Elimina el lead con sus propuestas. Solo Owner, porque arrastra historial comercial.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.OwnerOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _leads.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
