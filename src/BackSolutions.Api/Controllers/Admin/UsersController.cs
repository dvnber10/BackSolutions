using BackSolutions.Api.Security;
using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Admin;
using BackSolutions.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackSolutions.Api.Controllers.Admin;

/// <summary>
/// Gestión del equipo.
///
/// Ningún endpoint acepta un id de usuario por ruta como origen de autorización: siempre
/// se usa el id del token. Así un Provider no puede editar al Owner por mucho que se le
/// pase el id en la URL.
/// </summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = AuthorizationPolicies.OwnerOrAdmin)]
[Produces("application/json")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _users;
    private readonly ICurrentUser _currentUser;

    public UsersController(IUserService users, ICurrentUser currentUser)
    {
        _users = users;
        _currentUser = currentUser;
    }

    /// <summary>Listado paginado. <c>activeOnly</c> filtra los dados de baja.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<UserListItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UserListItemDto>>> List(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? search,
        [FromQuery] bool? activeOnly,
        CancellationToken cancellationToken = default) =>
        Ok(await _users.ListAsync(page, pageSize, search, activeOnly, cancellationToken));

    /// <summary>Roles del sistema con la cantidad de usuarios que los tienen.</summary>
    [HttpGet("roles")]
    [ProducesResponseType<IReadOnlyList<RoleDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> Roles(CancellationToken cancellationToken) =>
        Ok(await _users.ListRolesAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<UserListItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserListItemDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await _users.GetByIdAsync(id, cancellationToken));

    /// <summary>Da de alta un miembro del equipo. La contraseña inicial es temporal.</summary>
    [HttpPost]
    [ProducesResponseType<UserListItemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserListItemDto>> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _users.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    /// <summary>Edita los datos del usuario. <c>null</c> en un campo lo deja sin cambios.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<UserListItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserListItemDto>> Update(
        Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _users.UpdateAsync(id, request, cancellationToken));

    /// <summary>Reemplaza el conjunto de roles. El servicio impide quedarse sin ninguno.</summary>
    [HttpPut("{id:guid}/roles")]
    [ProducesResponseType<UserListItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserListItemDto>> AssignRoles(
        Guid id,
        [FromBody] AssignRolesRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _users.AssignRolesAsync(id, request, cancellationToken));

    /// <summary>Desbloquea a un usuario que quedó trabado por intentos fallidos.</summary>
    [HttpPost("{id:guid}/unlock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unlock(Guid id, CancellationToken cancellationToken)
    {
        await _users.UnlockAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Fija una contraseña nueva. Cierra todas las sesiones del usuario para que no siga
    /// Operating con un access token emitido con la contraseña anterior.
    /// </summary>
    [HttpPost("{id:guid}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await _users.ResetPasswordAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Da de baja al usuario. No borra la fila: queda con IsActive en false para que los
    /// leads y conversaciones que gestionó conserven la trazabilidad.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await _users.DeactivateAsync(id, cancellationToken);
        return NoContent();
    }
}
