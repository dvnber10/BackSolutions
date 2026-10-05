using BackSolutions.Api.Security;
using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Auth;
using BackSolutions.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BackSolutions.Api.Controllers.Auth;

/// <summary>
/// Sesión del equipo.
///
/// Login y refresh son anónimos a propósito: son el mecanismo para conseguir un token.
/// Logout también, porque se autentica con el refresh token que el cliente ya tiene y
/// así sigue funcionando aunque el access token haya expirado.
///
/// No se recibe email ni IP por parámetro: salen de <see cref="ICurrentUser"/>, que los
/// lee de la petición real. Si el controller los aceptara, un cliente podría declarar
/// una IP cualquiera y el registro de auditoría no serviría para nada.
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly ICurrentUser _currentUser;

    public AuthController(IAuthService auth, ICurrentUser currentUser)
    {
        _auth = auth;
        _currentUser = currentUser;
    }

    /// <summary>Inicia sesión y devuelve el access token, el refresh token y el perfil.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status423Locked)]
    public async Task<ActionResult<TokenResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var tokens = await _auth.LoginAsync(request, _currentUser.IpAddress, _currentUser.UserAgent, cancellationToken);
        return Ok(tokens);
    }

    /// <summary>
    /// Renueva la sesión. Devuelve un refresh token nuevo: el anterior queda revocado, así
    /// que el cliente tiene que reemplazar los dos valores de forma atómica.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        var tokens = await _auth.RefreshAsync(request, _currentUser.IpAddress, _currentUser.UserAgent, cancellationToken);
        return Ok(tokens);
    }

    /// <summary>Cierra la sesión del refresh token dado. Es idempotente.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        await _auth.LogoutAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Perfil del usuario autenticado. Los roles se leen de la base en cada llamada para
    /// que un cambio de rol surta efecto sin esperar a que expire el access token.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileResponse>> Me(CancellationToken cancellationToken)
    {
        var profile = await _auth.GetProfileAsync(_currentUser.RequireUserId(), cancellationToken);
        return Ok(profile);
    }

    /// <summary>
    /// Cambia la contraseña. Revoca todas las sesiones, incluida la actual, así que el
    /// cliente tiene que hacer login de nuevo con la contraseña nueva.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        await _auth.ChangePasswordAsync(_currentUser.RequireUserId(), request, cancellationToken);
        return NoContent();
    }
}
