using BackSolutions.Core.Common;
using BackSolutions.Core.Enums;
using BackSolutions.Core.Entities.Identity;

namespace BackSolutions.Core.Entities.Content;

/// <summary>
/// Página del sitio (Home, Nosotros, Servicios, Portafolio, Blog, Contacto).
/// El contenido editable de cada una son sus <see cref="PageSection"/>.
/// </summary>
public class Page : IHasTimestamps
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Identificador de URL: "home", "about", "contact". Es la clave que consume el frontend.</summary>
    public string Slug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Eyebrow { get; set; }
    public string? Intro { get; set; }

    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? OgImageUrl { get; set; }

    public ContentStatus Status { get; set; } = ContentStatus.Draft;
    public DateTimeOffset? PublishedAtUtc { get; set; }

    public bool ShowInNavigation { get; set; }
    public int SortOrder { get; set; }

    public Guid? AuthorId { get; set; }
    public User? Author { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<PageSection> Sections { get; set; } = [];
}
