namespace BackSolutions.Core.Entities.Content;

/// <summary>Unión post-etiqueta (relación muchos a muchos).</summary>
public class BlogPostTag
{
    public Guid PostId { get; set; }
    public BlogPost Post { get; set; } = null!;

    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
