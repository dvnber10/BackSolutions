using System.Text.Json;
using BackSolutions.Core.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BackSolutions.Api.Middleware;

/// <summary>
/// Traduce las excepciones de negocio (<see cref="AppException"/>) y las de EF a
/// respuestas ProblemDetails, para que los controladores nunca armen el error a mano.
///
/// Reglas: los detalles de validación y de las app se exponen al cliente; los errores
/// inesperados se loguean con detalle y responden un mensaje genérico, porque la
/// excepción real puede contener nombres de tablas, rutas o datos del cliente.
/// </summary>
public sealed class ExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await WriteProblemAsync(context, exception);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception)
    {
        var (status, title, detail, errorCode, errors) = Describe(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Error no controlado en {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        else if (exception is DbUpdateException or DbUpdateConcurrencyException)
        {
            // Un error de integridad es un fallo del servidor, no un error del usuario: por
            // más que la respuesta sea genérica, el log tiene que decir qué restricción
            // falló. Con el LogInformation de abajo se perdía y no había forma de
            // diagnosticar un 400 sin Attach Debugger.
            _logger.LogError(exception, "Error de base de datos en {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogInformation("{ErrorCode}: {Detail}", errorCode, detail);
        }

        if (context.Response.HasStarted)
        {
            // La respuesta ya está en curso (por ejemplo una descarga de PDF).
            // Agregar cabeceras ahora lanzaría; se corta la conexión y se registra.
            _logger.LogWarning("No se pudo escribir el error: la respuesta ya había comenzado.");
            context.Abort();
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var problem = new Dictionary<string, object?>
        {
            ["type"] = $"https://httpstatuses.com/{status}",
            ["title"] = title,
            ["status"] = status,
            ["detail"] = detail,
            ["errorCode"] = errorCode,
            ["traceId"] = context.TraceIdentifier
        };

        if (errors is not null)
        {
            // Las claves de un Dictionary<string, string[]> no pasan por
            // JsonNamingPolicy, así que se normalizan acá. Si no, los errores de
            // validación llegarían como "Name"/"Email" en un API que en el resto
            // de la respuesta usa camelCase, y el front no los encontraría.
            problem["errors"] = errors.ToDictionary(
                entry => ToCamelCase(entry.Key),
                entry => entry.Value);
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }

    private static string ToCamelCase(string value) =>
        string.IsNullOrEmpty(value) || char.IsLower(value[0])
            ? value
            : char.ToLowerInvariant(value[0]) + value[1..];

    private static (int Status, string Title, string Detail, string ErrorCode, IReadOnlyDictionary<string, string[]>? Errors) Describe(Exception exception)
    {
        switch (exception)
        {
            case ValidationException validation:
                return (validation.StatusCode, "Solicitud inválida", validation.Message, validation.ErrorCode, validation.Errors);

            case NotFoundException notFound:
                return (notFound.StatusCode, "No encontrado", notFound.Message, notFound.ErrorCode, null);

            case ForbiddenException forbidden:
                return (forbidden.StatusCode, "Sin permiso", forbidden.Message, forbidden.ErrorCode, null);

            case UnauthorizedException unauthorized:
                return (unauthorized.StatusCode, "No autenticado", unauthorized.Message, unauthorized.ErrorCode, null);

            case ConflictException conflict:
                return (conflict.StatusCode, "Conflicto", conflict.Message, conflict.ErrorCode, null);

            // Un cambio de contenido rompe la restricción única: el cliente debe elegir otro valor.
            case DbUpdateException dbUpdate when IsUniqueViolation(dbUpdate):
                return (409, "Conflicto", "Ya existe un registro con ese valor único.", "unique_violation", null);

            case DbUpdateException dbUpdate:
                return (400, "No se pudo guardar", "Los cambios violan una regla de integridad.", "integrity_error", null);

            case OperationCanceledException:
                return (499, "Cancelada", "La solicitud fue cancelada.", "client_closed", null);

            default:
                return (500, "Error interno", "Ocurrió un error inesperado. Intentá de nuevo más tarde.", "internal_error", null);
        }
    }

    /// <summary>Detecta el 2601/2627 de SQL Server sin depender del número de error de EF.</summary>
    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase) == true
        || exception.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true;
}
