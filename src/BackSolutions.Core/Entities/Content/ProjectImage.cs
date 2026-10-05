namespace BackSolutions.Core.Entities.Content;

/// <summary>Imagen de la galería de un proyecto del portafolio.</summary>
public class ProjectImage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }
    public PortfolioProject Project { get; set; } = null!;

    public string Url { get; set; } = string.Empty;
    public string? AltText { get; set; }

    public int SortOrder { get; set; }
}
