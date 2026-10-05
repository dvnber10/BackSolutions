using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Auth;
using BackSolutions.Core.Entities.Identity;

namespace BackSolutions.Core.Interfaces;

/// <summary>
/// Emisión y revocación de tokens. Vive separado de <see cref="IAuthService"/> porque
/// el chat (SignalR) también necesita abrir sesión sobre un refresh token.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Emite un access token JWT y un refresh token nuevo, persistiendo solo el hash.
    /// </summary>
    Task<TokenResponse> IssueAsync(User user, string? deviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rota un refresh token válido: revoca el usado y emite un par nuevo.
    /// Si el token presentado ya estaba revocado se considera robo y se corta toda
    /// la sesión del usuario, que es el motivo de la rotación.
    /// </summary>
    Task<TokenResponse> RotateAsync(string refreshToken, string? deviceName, CancellationToken cancellationToken = default);

    /// <summary>Revoca un refresh token. Es idempotente: revocar dos veces no es error.</summary>
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Cierra todas las sesiones del usuario. Se usa en logout global y en cambios de contraseña.</summary>
    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>Login, refresh, logout y cambio de contraseña del equipo.</summary>
public interface IAuthService
{
    Task<TokenResponse> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);

    Task<TokenResponse> RefreshAsync(RefreshRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);

    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Perfil del usuario autenticado. Los roles se leen de la base, no del JWT, para
    /// que un cambio de rol surta efecto sin esperar a que expire el access token.
    /// </summary>
    Task<UserProfileResponse> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia la contraseña y revoca todas las sesiones, incluida la actual.
    /// Tras el cambio el cliente debe hacer login de nuevo.
    /// </summary>
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
}
