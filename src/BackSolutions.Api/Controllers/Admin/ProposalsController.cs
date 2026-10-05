using BackSolutions.Api.Security;
using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Proposals;
using BackSolutions.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackSolutions.Api.Controllers.Admin;

/// <summary>
/// Propuestas comerciales.
///
/// El cliente nunca envía importes calculados: manda precio unitario, cantidad y
/// descuento por línea, y el servicio recalcula subtotal, descuento, impuestos y total.
/// Así no existe una forma de colar un total manipulado desde el cliente.
/// </summary>
[ApiController]
[Route("api/admin/proposals")]
[Authorize(Policy = AuthorizationPolicies.OwnerOrAdmin)]
[Produces("application/json")]
public sealed class ProposalsController : ControllerBase
{
    private readonly IProposalService _proposals;

    public ProposalsController(IProposalService proposals) => _proposals = proposals;

    /// <summary>Listado paginado, con filtro por lead, por estado y búsqueda libre.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ProposalListItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProposalListItemDto>>> List(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] Guid? leadId = null,
        [FromQuery] ProposalStatusFilter status = ProposalStatusFilter.All,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default) =>
        Ok(await _proposals.ListAsync(page, pageSize, leadId, status, search, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProposalDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProposalDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await _proposals.GetByIdAsync(id, cancellationToken));

    /// <summary>
    /// Crea la propuesta con el próximo número correlativo del año. Los totales se
    /// calculan en el servidor a partir de las líneas enviadas.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<ProposalDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProposalDto>> Create(
        [FromBody] SaveProposalRequest request,
        CancellationToken cancellationToken)
    {
        var proposal = await _proposals.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = proposal.Id }, proposal);
    }

    /// <summary>Actualiza la propuesta. Solo mientras esté en borrador.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<ProposalDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProposalDto>> Update(
        Guid id,
        [FromBody] SaveProposalRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _proposals.UpdateAsync(id, request, cancellationToken));

    /// <summary>
    /// Cambia el estado. Pasar a <c>Sent</c> sella la fecha de envío y la de validación
    /// si el equipo no la fijó. Los estados terminales no se pueden abandonar.
    /// </summary>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType<ProposalDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProposalDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateProposalStatusRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _proposals.UpdateStatusAsync(id, request, cancellationToken));

    /// <summary>
    /// Descarga el PDF. Los bytes se generan al vuelo y no se persisten: la propuesta
    /// vive en la base y el PDF es una representación, no el documento maestro.
    /// </summary>
    [HttpGet("{id:guid}/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadPdf(Guid id, CancellationToken cancellationToken)
    {
        var pdf = await _proposals.GeneratePdfAsync(id, cancellationToken);
        return File(pdf.Content, pdf.ContentType, pdf.FileName);
    }

    /// <summary>Elimina la propuesta. Solo Owner.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.OwnerOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _proposals.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
