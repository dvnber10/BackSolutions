using BackSolutions.Api.Security;
using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Content;
using BackSolutions.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackSolutions.Api.Controllers.Admin;

/// <summary>
/// Páginas del sitio y sus secciones.
///
/// Borrar queda restringido a Owner porque el contenido publicado ya está indexado por
/// buscadores y tiene URLs compartidas: la decisión es del dueño, no del editor.
/// </summary>
[ApiController]
[Route("api/admin/pages")]
[Authorize(Policy = AuthorizationPolicies.OwnerOrAdmin)]
[Produces("application/json")]
public sealed class PagesController : ControllerBase
{
    private readonly IPageService _pages;

    public PagesController(IPageService pages) => _pages = pages;

    /// <summary>Listado paginado del panel. Incluye borradores y archivadas.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<PageDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PageDto>>> List(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] ContentStatusFilter status = ContentStatusFilter.All,
        CancellationToken cancellationToken = default) =>
        Ok(await _pages.ListAsync(page, pageSize, status, cancellationToken));

    /// <summary>Métodos de menú. Es un dato de lectura del panel, no del sitio público.</summary>
    [HttpGet("navigation")]
    [ProducesResponseType<IReadOnlyList<PageNavItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PageNavItemDto>>> Navigation(CancellationToken cancellationToken) =>
        Ok(await _pages.GetNavigationAsync(cancellationToken));

    /// <summary>Devuelve la página con sus secciones, visibles o no, para poder editarla.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<PageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PageDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await _pages.GetByIdAsync(id, cancellationToken));

    /// <summary>Crea una página. El id de la ruta es opcional: si viene informado, actualiza.</summary>
    [HttpPost]
    [ProducesResponseType<PageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PageDto>> Create(
        [FromBody] SavePageRequest request,
        CancellationToken cancellationToken)
    {
        var page = await _pages.SaveAsync(null, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = page.Id }, page);
    }

    /// <summary>Actualiza una página y reconstruye sus secciones con las que vienen en el cuerpo.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<PageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PageDto>> Update(
        Guid id,
        [FromBody] SavePageRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _pages.SaveAsync(id, request, cancellationToken));

    /// <summary>Elimina la página y sus secciones. Solo Owner.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.OwnerOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _pages.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
