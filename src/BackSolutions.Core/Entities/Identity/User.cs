using BackSolutions.Core.Common;

namespace BackSolutions.Core.Entities.Identity;

/// <summary>
/// Miembro del equipo: propietario, administrador, proveedor o soporte.
/// Los clientes del sitio web NO son usuarios: son leads anónimos con PublicToken.
/// </summary>
public class User : IHasTimestamps
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Siempre en minúsculas: el índice único es case-insensitive, pero normalizar evita duplicados.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Hash BCrypt. Nunca se almacena ni se registra la contraseña en texto plano.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Obliga a cambiar la clave en el próximo login (usado cuando la app Kotlin se instala en un dispositivo compartido).</summary>
    public bool MustChangePassword { get; set; }

    public int FailedLoginAttempts { get; set; }

    public DateTimeOffset? LockoutEndsAtUtc { get; set; }
    public DateTimeOffset? LastLoginAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
