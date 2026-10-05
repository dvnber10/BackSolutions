namespace BackSolutions.Api.Middleware;

/// <summary>
/// Cabeceras de seguridad básicas.
///
/// Son las que un navegador aplica por defecto solo si se lo pides, y esta API devuelve
/// datos de clientes (nombres, emails, teléfonos) y PDFs de propuestas. Sin
/// <c>X-Content-Type-Options</c>, un archivo devuelto como <c>application/pdf</c> podría
/// ser interpretado como HTML por un navegador viejo y ejecutar lo que contenga.
///
/// No se agregan aquí ni CSP ni HSTS: esta API no sirve la web, la sirve el front en
/// Vercel, que tiene su propia configuración. Ponerlas acá sería publicidad engañosa: una
/// cabecera que no protege nada porque nadie la lee.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Evita que el navegador adivine el content-type y ejecute algo que no es.
        headers["X-Content-Type-Options"] = "nosniff";

        // La respuesta no se puede embeber en un iframe de otro sitio: sirve para
        // clickjacking contra el panel.
        headers["X-Frame-Options"] = "DENY";

        // No se filtra la versión del servidor ni del framework.
        headers["Server"] = "";

        // La API no carga recursos de terceros: todo lo que devuelve es JSON o PDF. Dejar
        // '*' sería ampliar la superficie de XSS sin necesidad.
        headers["Referrer-Policy"] = "no-referrer";

        return _next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();
}
