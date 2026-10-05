namespace BackSolutions.Core.Common;

/// <summary>
/// Marca una entidad cuyos timestamps de auditoría mantiene automáticamente AppDbContext.
/// Evita repetir la lógica de CreatedAtUtc / UpdatedAtUtc en cada servicio.
/// </summary>
public interface IHasTimestamps
{
    DateTimeOffset CreatedAtUtc { get; set; }
    DateTimeOffset UpdatedAtUtc { get; set; }
}
