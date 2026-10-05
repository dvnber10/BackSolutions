using System.ComponentModel.DataAnnotations.Schema;
using BackSolutions.Core.Common;

namespace BackSolutions.Core.Entities.Identity;

/// <summary>
/// Refresh token de sesión. Permite que la app Kotlin y la web renueven el access token
/// sin que el usuario vuelva a escribir su contraseña.
///
/// Se aplica rotación: cada refresh revoca el token usado y emite uno nuevo.
/// Si un token ya revocado vuelve a presentarse, se revoca toda la sesión
/// (señal de robo de token).
/// </summary>
public class RefreshToken : IHasTimestamps
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>SHA-256 en hexadecimal del token entregado al cliente. El token en claro nunca se persiste.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    /// <summary>Hash del token que reemplazó a este, para detectar reutilización.</summary>
    public string? ReplacedByTokenHash { get; set; }

    public string? DeviceName { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    [NotMapped]
    public bool IsUsable => RevokedAtUtc is null && ExpiresAtUtc > DateTimeOffset.UtcNow;
}
