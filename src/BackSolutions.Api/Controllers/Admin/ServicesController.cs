using BackSolutions.Api.Security;
using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Content;
using BackSolutions.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackSolutions.Api.Controllers.Admin;

/// <summary>Servicios que se ofrecen. El precio es orientativo: no es un checkout.</summary>
[ApiController]
[Route("api/admin/services")]
[Authorize(Policy = AuthorizationPolicies.OwnerOrAdmin)]
[Produces("application/json")]
public sealed class ServicesController : ControllerBase
{
    private readonly IServiceItemService _services;

    public ServicesController(IServiceItemService services) => _services = services;

    /// <summary>Listado paginado del panel, con borradores incluidos.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ServiceItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ServiceItemDto>>> List(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] ContentStatusFilter status = ContentStatusFilter.All,
        CancellationToken cancellationToken = default) =>
        Ok(await _services.ListAsync(page, pageSize, status, cancellationToken));

    /// <summary>Vista pública: solo publicados. La usa la home para mostrar la grilla.</summary>
    [HttpGet("public")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<PublicServiceItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PublicServiceItemDto>>> ListPublic(CancellationToken cancellationToken) =>
        Ok(await _services.ListPublicAsync(cancellationToken));

    /// <summary>Ficha pública por slug.</summary>
    [HttpGet("public/{slug}")]
    [AllowAnonymous]
    [ProducesResponseType<PublicServiceItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicServiceItemDto>> GetPublicBySlug(string slug, CancellationToken cancellationToken)
    {
        var service = await _services.GetPublicBySlugAsync(slug, cancellationToken);

        return service is null ? NotFound() : Ok(service);
    }

    /// <summary>Servicio completo para editarlo, con su borrador intacto.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ServiceItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceItemDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await _services.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<ServiceItemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ServiceItemDto>> Create(
        [FromBody] SaveServiceItemRequest request,
        CancellationToken cancellationToken)
    {
        var service = await _services.SaveAsync(null, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = service.Id }, service);
    }

    /// <summary>Actualiza el servicio. Si el slug está tomado se resuelve con sufijo numérico.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<ServiceItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceItemDto>> Update(
        Guid id,
        [FromBody] SaveServiceItemRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _services.SaveAsync(id, request, cancellationToken));

    /// <summary>Elimina el servicio. Los leads que lo referencian quedan sin servicio asociado.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _services.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
