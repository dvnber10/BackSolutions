namespace BackSolutions.Core.Common;

/// <summary>
/// Raíz de los errores de negocio. Los servicios lanzan estas excepciones y el
/// middleware global las traduce a ProblemDetails, de modo que los controladores
/// no repiten try/catch ni arman responses de error a mano.
/// </summary>
public abstract class AppException : Exception
{
    /// <summary>Código HTTP con el que se responde.</summary>
    public abstract int StatusCode { get; }

    /// <summary>Tipo estable para que el cliente (React / Kotlin) pueda reaccionar sin parsear texto.</summary>
    public abstract string ErrorCode { get; }

    protected AppException(string message) : base(message) { }

    protected AppException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>La entrada no cumple las reglas del dominio. Responde 400.</summary>
public sealed class ValidationException : AppException
{
    /// <summary>Errores por campo, para que el formulario pueda señalar cada input.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(string message) : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("La solicitud contiene datos inválidos.")
    {
        Errors = errors;
    }

    public override int StatusCode => 400;

    public override string ErrorCode => "validation_error";
}

/// <summary>El recurso no existe o no es visible para quien pregunta. Responde 404.</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message) { }

    public override int StatusCode => 404;

    public override string ErrorCode => "not_found";
}

/// <summary>Existe sesión pero no alcanza el permiso. Responde 403.</summary>
public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message) : base(message) { }

    public override int StatusCode => 403;

    public override string ErrorCode => "forbidden";
}

/// <summary>Faltan credenciales o no son válidas. Responde 401.</summary>
public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string message) : base(message) { }

    public override int StatusCode => 401;

    public override string ErrorCode => "unauthorized";
}

/// <summary>Choca con el estado actual: email duplicado, slug repetido, transición inválida. Responde 409.</summary>
public sealed class ConflictException : AppException
{
    public ConflictException(string message) : base(message) { }

    public ConflictException(string message, Exception inner) : base(message, inner) { }

    public override int StatusCode => 409;

    public override string ErrorCode => "conflict";
}
