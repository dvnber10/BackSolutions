namespace BackSolutions.Core.Entities.Identity;

/// <summary>
/// Unión usuario-rol. Permite que un mismo miembro tenga varios roles
/// (por ejemplo Owner + Provider) sin duplicar cuentas.
/// </summary>
public class UserRole
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public DateTimeOffset GrantedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
