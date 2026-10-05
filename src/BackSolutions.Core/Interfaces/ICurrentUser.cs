namespace BackSolutions.Core.Interfaces;

/// <summary>
/// Usuario de la petición en curso y contexto de red. Lo consumen los servicios para
/// no obligar a los controladores a pasar el userId y la IP por parámetro en cada método.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Id del usuario autenticado, o null si la petición es anónima.</summary>
    Guid? UserId { get; }

    string? Email { get; }

    bool IsAuthenticated { get; }

    /// <summary>Lanza si la petición no está autenticada. Para servicios que exigen sesión.</summary>
    Guid RequireUserId();

    string? IpAddress { get; }

    string? UserAgent { get; }

    /// <summary>Claims del token, para decisiones finer-grained (Owner puede borrar, Admin no).</summary>
    bool IsInRole(string role);

    /// <summary>Devuelve el email del cliente si viene en los headers de un cliente anónimo.</summary>
    string? GetHeader(string name);
}
