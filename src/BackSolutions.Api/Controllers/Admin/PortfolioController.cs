using BackSolutions.Api.Security;
using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Content;
using BackSolutions.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackSolutions.Api.Controllers.Admin;

/// <summary>Proyectos del portfolio y sus imágenes.</summary>
[ApiController]
[Route("api/admin/portfolio")]
[Authorize(Policy = AuthorizationPolicies.OwnerOrAdmin)]
[Produces("application/json")]
public sealed class PortfolioController : ControllerBase
{
    private readonly IPortfolioService _portfolio;

    public PortfolioController(IPortfolioService portfolio) => _portfolio = portfolio;

    /// <summary>Listado paginado del panel. <c>featuredOnly</c> filtra los destacados.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<PortfolioProjectDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PortfolioProjectDto>>> List(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] ContentStatusFilter status = ContentStatusFilter.All,
        [FromQuery] bool? featuredOnly = null,
        CancellationToken cancellationToken = default) =>
        Ok(await _portfolio.ListAsync(page, pageSize, status, featuredOnly, cancellationToken));

    [HttpGet("public")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<PublicPortfolioProjectDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PublicPortfolioProjectDto>>> ListPublic(
        [FromQuery] bool onlyFeatured = false,
        CancellationToken cancellationToken = default) =>
        Ok(await _portfolio.ListPublicAsync(onlyFeatured, cancellationToken));

    [HttpGet("public/{slug}")]
    [AllowAnonymous]
    [ProducesResponseType<PublicPortfolioProjectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicPortfolioProjectDto>> GetPublicBySlug(string slug, CancellationToken cancellationToken)
    {
        var project = await _portfolio.GetPublicBySlugAsync(slug, cancellationToken);
        return project is null ? NotFound() : Ok(project);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PortfolioProjectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortfolioProjectDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await _portfolio.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<PortfolioProjectDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PortfolioProjectDto>> Create(
        [FromBody] SavePortfolioProjectRequest request,
        CancellationToken cancellationToken)
    {
        var project = await _portfolio.SaveAsync(null, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = project.Id }, project);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<PortfolioProjectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortfolioProjectDto>> Update(
        Guid id,
        [FromBody] SavePortfolioProjectRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _portfolio.SaveAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _portfolio.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
