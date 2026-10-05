using BackSolutions.Api.Security;
using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Content;
using BackSolutions.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackSolutions.Api.Controllers.Admin;

/// <summary>Artículos del blog y sus etiquetas.</summary>
[ApiController]
[Route("api/admin/blog")]
[Authorize(Policy = AuthorizationPolicies.OwnerOrAdmin)]
[Produces("application/json")]
public sealed class BlogController : ControllerBase
{
    private readonly IBlogService _blog;

    public BlogController(IBlogService blog) => _blog = blog;

    /// <summary>Listado paginado del panel. <c>search</c> filtra por título y extracto.</summary>
    [HttpGet("posts")]
    [ProducesResponseType<PagedResult<BlogPostDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<BlogPostDto>>> List(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] ContentStatusFilter status = ContentStatusFilter.All,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default) =>
        Ok(await _blog.ListAsync(page, pageSize, status, search, cancellationToken));

    [HttpGet("posts/{id:guid}")]
    [ProducesResponseType<BlogPostDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BlogPostDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await _blog.GetByIdAsync(id, cancellationToken));

    [HttpPost("posts")]
    [ProducesResponseType<BlogPostDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BlogPostDto>> Create(
        [FromBody] SaveBlogPostRequest request,
        CancellationToken cancellationToken)
    {
        var post = await _blog.SaveAsync(null, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = post.Id }, post);
    }

    /// <summary>
    /// Guarda el artículo. Las etiquetas se sincronizan por nombre: las que no existen se
    /// crean y las que se quitaron se desvinculan, sin borrar la etiqueta en sí porque
    /// puede estar en uso por otros artículos.
    /// </summary>
    [HttpPut("posts/{id:guid}")]
    [ProducesResponseType<BlogPostDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BlogPostDto>> Update(
        Guid id,
        [FromBody] SaveBlogPostRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _blog.SaveAsync(id, request, cancellationToken));

    [HttpDelete("posts/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePost(Guid id, CancellationToken cancellationToken)
    {
        await _blog.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    // ── Etiquetas ─────────────────────────────────────────────

    [HttpGet("tags")]
    [ProducesResponseType<IReadOnlyList<TagDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TagDto>>> ListTags(CancellationToken cancellationToken) =>
        Ok(await _blog.ListTagsAsync(cancellationToken));

    [HttpPost("tags")]
    [ProducesResponseType<TagDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TagDto>> CreateTag([FromBody] SaveTagRequest request, CancellationToken cancellationToken)
    {
        var tag = await _blog.SaveTagAsync(null, request, cancellationToken);
        return CreatedAtAction(nameof(ListTags), tag);
    }

    [HttpPut("tags/{id:guid}")]
    [ProducesResponseType<TagDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TagDto>> UpdateTag(
        Guid id,
        [FromBody] SaveTagRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _blog.SaveTagAsync(id, request, cancellationToken));

    /// <summary>Elimina la etiqueta y sus vínculos con artículos.</summary>
    [HttpDelete("tags/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTag(Guid id, CancellationToken cancellationToken)
    {
        await _blog.DeleteTagAsync(id, cancellationToken);
        return NoContent();
    }
}
