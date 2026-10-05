using BackSolutions.Core.Common;
using BackSolutions.Core.Enums;
using BackSolutions.Core.Entities.Leads;

namespace BackSolutions.Core.Entities.Content;

/// <summary>
/// Servicio que ofrecés. Alimenta la página de Servicios, el selector del formulario
/// de cotización y el filtro del portafolio.
/// </summary>
public class ServiceItem : IHasTimestamps
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Clave estable usada por el formulario ("desarrollo-web"). No cambia aunque renombres el servicio.</summary>
    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string? FullDescription { get; set; }

    /// <summary>Clave de ícono del set del frontend (ver public/icons.svg).</summary>
    public string? Icon { get; set; }

    public string? ImageUrl { get; set; }

    /// <summary>Array JSON de strings: ["React", ".NET", "PostgreSQL"].</summary>
    public string TechnologiesJson { get; set; } = "[]";

    /// <summary>Precio de referencia. Null significa "a cotizar".</summary>
    public decimal? BasePrice { get; set; }

    public string? PriceNote { get; set; }

    public ContentStatus Status { get; set; } = ContentStatus.Draft;
    public DateTimeOffset? PublishedAtUtc { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Lead> Leads { get; set; } = [];
}
