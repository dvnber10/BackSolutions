using BackSolutions.Core.Entities.Identity;

namespace BackSolutions.Core.Dtos.Auth;

/// <summary>Login del equipo. El email se normaliza a minúsculas antes de buscarse.</summary>
public sealed record LoginRequest(string Email, string Password, string? DeviceName = null);

/// <summary>
/// Respuesta de login y de refresh. <see cref="RefreshToken"/> viaja una sola vez,
/// en claro: la base guarda su SHA-256 y no hay forma de recuperarlo después.
/// </summary>
public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    UserProfileResponse User);

/// <summary>Datos del usuario que el panel necesita para decidir qué mostrar.</summary>
public sealed record UserProfileResponse(
    Guid Id,
    string Email,
    string FullName,
    string? AvatarUrl,
    string? Phone,
    bool IsActive,
    bool MustChangePassword,
    IReadOnlyList<string> Roles,
    DateTimeOffset? LastLoginAtUtc);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
