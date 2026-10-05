namespace BackSolutions.Core.Entities.Content;

/// <summary>Etiqueta del blog. Se reutiliza entre varios posts.</summary>
public class Tag
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    public ICollection<BlogPostTag> PostTags { get; set; } = [];
}
