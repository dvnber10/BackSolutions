using System.Threading.RateLimiting;
using BackSolutions.Api.Security;
using Microsoft.AspNetCore.RateLimiting;

namespace BackSolutions.Api.Extensions;

/// <summary>
/// Límites de peticiones.
///
/// Se usa el rate limiter que trae el framework en vez de uno propio: ya tiene la
/// integración con <c>Retry-After</c>, la respuesta 429 y la cancelación de la petición,
/// que son las partes donde un limitador casero suele quedar corto.
///
/// Los límites son por IP y por partición. La partición importa: si el límite fuera global
/// para toda la API, un visitante anónimo consumiendo el cupo bloquearía a todo el equipo
/// que entra con JWT desde la misma red corporativa.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Registra los límites de peticiones.
    ///
    /// Los números salen de configuración para poder ajustarlos sin recompilar: el límite
    /// correcto depende de cuánta gente realmente usa la API y de detrás de cuántos proxies
    /// está la oficina, y eso se descubre en producción, no en el código.
    /// </summary>
    public static IServiceCollection AddApplicationRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        // conservative: un límite alto por defecto es un límite que nadie va a revisar.
        var general = ReadLimit(configuration, "General", 120);
        var publicWrites = ReadLimit(configuration, "PublicWrites", 30);
        var auth = ReadLimit(configuration, "Auth", 10);

        services.AddRateLimiter(options =>
        {
            // Por defecto, si un endpoint no dice nada, se aplica el límite general.
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter =
                    context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                        ? ((int)retryAfter.TotalSeconds).ToString()
                        : "60";

                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    type = "https://httpstatuses.com/429",
                    title = "Demasiadas peticiones",
                    status = StatusCodes.Status429TooManyRequests,
                    detail = "Vas demasiado rápido. Probá de nuevo en unos instantes.",
                    errorCode = "rate_limit_exceeded",
                    traceId = context.HttpContext.TraceIdentifier
                }, cancellationToken);
            };

            options.AddPolicy(RateLimitPolicies.PublicWrites, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ClientPartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = publicWrites,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));

            options.AddPolicy(RateLimitPolicies.Auth, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ClientPartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = auth,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));

            // Límite general para todo lo que no dice otra cosa. Es el suelo, no el
            // techo: los endpoints marcados con [EnableRateLimiting] lo reemplazan.
            //
            // Va como GlobalLimiter y no como política con nombre porque AddFixedWindowLimiter
            // no puede inferir el tipo de la partición desde un lambda, y porque así el
            // comportamiento por defecto es de verdad "todo está limitado".
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ClientPartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = general,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));
        });

        return services;
    }

    /// <summary>Lee un límite de la configuración y descarta valores absurdos o inválidos.</summary>
    private static int ReadLimit(IConfiguration configuration, string name, int fallback)
    {
        var configured = configuration.GetValue<int?>($"RateLimiting:{name}");

        return configured is > 0 and <= 10000 ? configured.Value : fallback;
    }

    /// <summary>
    /// La IP real del cliente, no la del proxy. Sin esto, todo el tráfico de una oficina
    /// detrás de un proxy natural comparte una sola partición y se bloquea entre sí.
    /// </summary>
    private static string ClientPartitionKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
