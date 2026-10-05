using BackSolutions.Core.Interfaces;

namespace BackSolutions.Api.Services;

/// <summary>Reloj del sistema. Suficiente en producción; los tests lo sustituyen por uno fijo.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}
