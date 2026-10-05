using BackSolutions.Core.Common;
using BackSolutions.Core.Enums;
using BackSolutions.Core.Entities.Identity;

namespace BackSolutions.Core.Entities.Content;

/// <summary>Entrada del blog.</summary>
public class BlogPost : IHasTimestamps
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    /// <summary>Bajada que se ve en la tarjeta del listado y en las metaetiquetas.</summary>
    public string? Excerpt { get; set; }

    /// <summary>Cuerpo en HTML.</summary>
    public string ContentHtml { get; set; } = string.Empty;

    public string? CoverImageUrl { get; set; }
    public string? CoverImageAlt { get; set; }

    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }

    public ContentStatus Status { get; set; } = ContentStatus.Draft;
    public DateTimeOffset? PublishedAtUtc { get; set; }

    public int ReadingMinutes { get; set; }
    public long ViewCount { get; set; }

    public Guid? AuthorId { get; set; }
    public User? Author { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<BlogPostTag> PostTags { get; set; } = [];
}
