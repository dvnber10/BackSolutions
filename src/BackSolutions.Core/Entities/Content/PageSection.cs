using BackSolutions.Core.Common;
using BackSolutions.Core.Enums;

namespace BackSolutions.Core.Entities.Content;

/// <summary>
/// Bloque visual dentro de una página. El diseño es "contenedor flexible": cada
/// <see cref="PageSectionType"/> define la forma de <see cref="ContentJson"/>,
/// así agregar un bloque nuevo no requiere cambiar el esquema de la base.
/// </summary>
public class PageSection : IHasTimestamps
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PageId { get; set; }
    public Page Page { get; set; } = null!;

    public PageSectionType Type { get; set; }

    public string? Title { get; set; }
    public string? Subtitle { get; set; }

    /// <summary>Payload en JSON con los datos del bloque. Ver <see cref="PageSectionType"/>.</summary>
    public string ContentJson { get; set; } = "{}";

    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
