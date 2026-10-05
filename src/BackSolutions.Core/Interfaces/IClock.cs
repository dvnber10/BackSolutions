namespace BackSolutions.Core.Interfaces;

/// <summary>
/// Fuente del tiempo. Existe para que los servicios que Depends de fechas (expiración de
/// tokens, validity de propuestas, lockout) se puedan testear sin esperar.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    DateOnly Today { get; }
}
