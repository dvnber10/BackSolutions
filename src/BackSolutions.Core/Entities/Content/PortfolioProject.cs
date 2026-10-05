using BackSolutions.Core.Common;
using BackSolutions.Core.Enums;

namespace BackSolutions.Core.Entities.Content;

/// <summary>Proyecto del portafolio.</summary>
public class PortfolioProject : IHasTimestamps
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    /// <summary>Texto corto para las tarjetas del grid.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Cuerpo completo en HTML, editable desde el panel con un editor visual.</summary>
    public string? DescriptionHtml { get; set; }

    public string? CoverImageUrl { get; set; }
    public string? CoverImageAlt { get; set; }

    /// <summary>Null en proyectos de portfolio propio que no son de un cliente.</summary>
    public string? ClientName { get; set; }

    /// <summary>Array JSON de strings: ["React", "NestJS", "PostgreSQL"].</summary>
    public string TechStackJson { get; set; } = "[]";

    public DateOnly? StartedOn { get; set; }
    public DateOnly? DeliveredOn { get; set; }

    public string? LiveUrl { get; set; }
    public string? RepositoryUrl { get; set; }

    /// <summary>Aparece en la_HOME antes que el resto.</summary>
    public bool IsFeatured { get; set; }

    public ContentStatus Status { get; set; } = ContentStatus.Draft;
    public DateTimeOffset? PublishedAtUtc { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ProjectImage> Images { get; set; } = [];
}
