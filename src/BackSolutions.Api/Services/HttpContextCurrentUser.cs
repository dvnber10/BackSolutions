using System.Security.Claims;
using BackSolutions.Core.Common;
using BackSolutions.Core.Interfaces;

namespace BackSolutions.Api.Services;

/// <summary>
/// Implementación de <see cref="ICurrentUser"/> sobre el contexto HTTP.
///
/// Nota sobre la IP: MonsterASP termina TLS en un reverse proxy, así que
/// RemoteIpAddress devuelve la del proxy. Se prefiere X-Forwarded-For, que es lo
/// que realmente sirve para una geolocalización o un rate limit por IP.
/// </summary>
public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId
    {
        get
        {
            var raw = Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? Principal?.FindFirstValue("sub");

            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);

    public Guid RequireUserId() =>
        UserId ?? throw new UnauthorizedException("La solicitud no está autenticada.");

    public string? IpAddress
    {
        get
        {
            var forwarded = GetHeader("X-Forwarded-For");
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                // Puede venir una cadena de proxies: "cliente, proxy1, proxy2".
                var first = forwarded.Split(',')[0].Trim();
                if (!string.IsNullOrWhiteSpace(first))
                {
                    return first;
                }
            }

            return _accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        }
    }

    public string? UserAgent => GetHeader("User-Agent");

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;

    public string? GetHeader(string name) =>
        _accessor.HttpContext?.Request.Headers[name].ToString();

    /// <summary>
    /// Exige alguno de los roles indicados. Lo usan los servicios para decidir sobre
    /// operaciones sensibles sin repetir la comprobación en cada método.
    /// </summary>
    public void RequireAnyRole(params string[] roles)
    {
        if (!IsAuthenticated)
        {
            throw new UnauthorizedException("La solicitud no está autenticada.");
        }

        if (!roles.Any(IsInRole))
        {
            throw new ForbiddenException($"Se requiere alguno de estos roles: {string.Join(", ", roles)}.");
        }
    }
}
