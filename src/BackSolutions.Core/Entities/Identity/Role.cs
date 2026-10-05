using BackSolutions.Core.Common;

namespace BackSolutions.Core.Entities.Identity;

/// <summary>Rol del equipo. Los nombres válidos están en <see cref="RoleNames"/>.</summary>
public class Role : IHasTimestamps
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Los roles del sistema no se pueden eliminar desde el panel.</summary>
    public bool IsSystem { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<UserRole> UserRoles { get; set; } = [];
}
