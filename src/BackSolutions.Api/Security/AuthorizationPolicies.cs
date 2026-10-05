using BackSolutions.Core.Common;

namespace BackSolutions.Api.Security;

/// <summary>
/// Políticas de autorización. Los nombres viven acá y no como strings sueltos en los
/// atributos <c>[Authorize]</c>, para que un typo falle al compilar y no en producción.
///
/// La matriz de permisos:
///
/// | Recurso              | Support | Provider | Admin | Owner |
/// |----------------------|:-------:|:--------:|:-----:|:-----:|
/// | Chat (leer/escribir) |    sí   |    sí    |  sí   |  sí   |
/// | Leads y propuestas   |   no    |    sí    |  sí   |  sí   |
/// | Contenido (editar)   |   no    |    no    |  sí   │  sí   |
/// | Usuarios y ajustes   |   no    |    no    |  sí   │  sí   |
/// | Borrar contenido     |   no    │    no    │  no   │  sí   |
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Cualquier miembro del equipo con sesión abierta. Incluye a los cuatro roles.</summary>
    public const string TeamMember = nameof(TeamMember);

    /// <summary>Puede trabajar leads, propuestas, contenido y usuarios. Todo menos borrar contenido publicado.</summary>
    public const string OwnerOrAdmin = nameof(OwnerOrAdmin);

    /// <summary>Dueño del sistema: además puede eliminar contenido, páginas y usuarios.</summary>
    public const string OwnerOnly = nameof(OwnerOnly);

    public static void Configure(Microsoft.AspNetCore.Authorization.AuthorizationOptions options)
    {
        options.AddPolicy(TeamMember, policy => policy.RequireRole(
            RoleNames.Owner,
            RoleNames.Admin,
            RoleNames.Provider,
            RoleNames.Support));

        options.AddPolicy(OwnerOrAdmin, policy => policy.RequireRole(
            RoleNames.Owner,
            RoleNames.Admin));

        options.AddPolicy(OwnerOnly, policy => policy.RequireRole(RoleNames.Owner));
    }
}
