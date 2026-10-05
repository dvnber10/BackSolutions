namespace BackSolutions.Core.Common;

/// <summary>
/// Nombres de rol del sistema. Son cadenas (no enums) para poder sumar roles
/// sin necesidad de una migración, y para que los claims del JWT sean legibles.
/// </summary>
public static class RoleNames
{
    /// <summary>Dueño del sistema. Acceso total, incluido el borrado de contenido.</summary>
    public const string Owner = "Owner";

    /// <summary>Administrador. Gestiona contenido, leads y usuarios, pero no puede eliminar el sistema.</summary>
    public const string Admin = "Admin";

    /// <summary>Proveedor de servicio. Trabaja leads y chat asignados, sin acceso a configuración global.</summary>
    public const string Provider = "Provider";

    /// <summary>Soporte. Solo atiende conversaciones de chat.</summary>
    public const string Support = "Support";

    public static readonly string[] All = [Owner, Admin, Provider, Support];
}
