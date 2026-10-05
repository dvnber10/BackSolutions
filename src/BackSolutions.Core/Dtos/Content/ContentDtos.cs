using System.Text.Json;
using BackSolutions.Core.Enums;

namespace BackSolutions.Core.Dtos.Content;

// ─────────────────────────────────────────────────────────────
// SiteSettings
// ─────────────────────────────────────────────────────────────

/// <summary>
/// Configuración global del sitio. Es un singleton (Id = 1): la lectura pública
/// expone contacto y SEO, y el panel escribe todos los campos.
/// </summary>
public sealed record SiteSettingsDto(
    int Id,
    string CompanyName,
    string LegalName,
    string? TaxId,
    string? LogoUrl,
    string? FaviconUrl,
    string ContactEmail,
    string? Phone,
    string? WhatsApp,
    string? Address,
    SocialLinksDto Social,
    HeroDto Hero,
    string FooterText,
    string? FooterLegalText,
    SeoDefaultsDto Seo,
    string DefaultCurrency,
    int ProposalValidityDays,
    DateTimeOffset UpdatedAtUtc);

public sealed record SocialLinksDto(
    string? FacebookUrl,
    string? InstagramUrl,
    string? LinkedInUrl,
    string? GithubUrl,
    string? XUrl);

public sealed record HeroDto(
    string Title,
    string Subtitle,
    string? ImageUrl,
    CtaDto PrimaryCta,
    CtaDto SecondaryCta);

public sealed record CtaDto(string Text, string Url);

public sealed record SeoDefaultsDto(string Title, string? Description, string? OgImageUrl);

/// <summary>
/// Actualización de la configuración. Se mandan todos los campos (PATCH parcial
/// se complica sin necesidad) y los valores ausentes se conservan.
/// </summary>
public sealed record UpdateSiteSettingsRequest(
    string CompanyName,
    string? LegalName,
    string? TaxId,
    string? LogoUrl,
    string? FaviconUrl,
    string ContactEmail,
    string? Phone,
    string? WhatsApp,
    string? Address,
    SocialLinksDto? Social,
    HeroDto? Hero,
    string FooterText,
    string? FooterLegalText,
    SeoDefaultsDto? Seo,
    string? DefaultCurrency,
    int? ProposalValidityDays);

// ─────────────────────────────────────────────────────────────
// Page + PageSection
// ─────────────────────────────────────────────────────────────

public sealed record PageSectionDto(
    Guid Id,
    PageSectionType Type,
    string? Title,
    string? Subtitle,
    JsonElement Content,
    int SortOrder,
    bool IsVisible);

public sealed record PageDto(
    Guid Id,
    string Slug,
    string Title,
    string? Eyebrow,
    string? Intro,
    string? SeoTitle,
    string? SeoDescription,
    string? OgImageUrl,
    ContentStatus Status,
    bool ShowInNavigation,
    int SortOrder,
    DateTimeOffset? PublishedAtUtc,
    IReadOnlyList<PageSectionDto> Sections,
    DateTimeOffset UpdatedAtUtc);

/// <summary>Vista reducida para el menú de navegación, sin sections ni metadatos SEO.</summary>
public sealed record PageNavItemDto(Guid Id, string Slug, string Title, int SortOrder);

public sealed record SavePageRequest(
    string? Slug,
    string Title,
    string? Eyebrow,
    string? Intro,
    string? SeoTitle,
    string? SeoDescription,
    string? OgImageUrl,
    ContentStatus Status,
    bool ShowInNavigation,
    int SortOrder,
    IReadOnlyList<SavePageSectionRequest>? Sections);

public sealed record SavePageSectionRequest(
    Guid? Id,
    PageSectionType Type,
    string? Title,
    string? Subtitle,
    JsonElement? Content,
    int SortOrder,
    bool IsVisible);

// ─────────────────────────────────────────────────────────────
// ServiceItem
// ─────────────────────────────────────────────────────────────

public sealed record ServiceItemDto(
    Guid Id,
    string Slug,
    string Name,
    string ShortDescription,
    string? FullDescription,
    string? Icon,
    string? ImageUrl,
    IReadOnlyList<string> Technologies,
    decimal? BasePrice,
    string? PriceNote,
    ContentStatus Status,
    int SortOrder,
    DateTimeOffset? PublishedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>Versión pública: incluye BasePrice para mostrar "desde $X" en la web.</summary>
public sealed record PublicServiceItemDto(
    Guid Id,
    string Slug,
    string Name,
    string ShortDescription,
    string? FullDescription,
    string? Icon,
    string? ImageUrl,
    IReadOnlyList<string> Technologies,
    decimal? BasePrice,
    string? PriceNote,
    int SortOrder);

public sealed record SaveServiceItemRequest(
    string? Slug,
    string Name,
    string ShortDescription,
    string? FullDescription,
    string? Icon,
    string? ImageUrl,
    IReadOnlyList<string>? Technologies,
    decimal? BasePrice,
    string? PriceNote,
    ContentStatus Status,
    int SortOrder);

// ─────────────────────────────────────────────────────────────
// PortfolioProject + ProjectImage
// ─────────────────────────────────────────────────────────────

public sealed record ProjectImageDto(Guid Id, string Url, string? AltText, int SortOrder);

public sealed record PortfolioProjectDto(
    Guid Id,
    string Slug,
    string Title,
    string Summary,
    string? DescriptionHtml,
    string? CoverImageUrl,
    string? CoverImageAlt,
    string? ClientName,
    IReadOnlyList<string> TechStack,
    DateOnly? StartedOn,
    DateOnly? DeliveredOn,
    string? LiveUrl,
    string? RepositoryUrl,
    bool IsFeatured,
    ContentStatus Status,
    int SortOrder,
    IReadOnlyList<ProjectImageDto> Images,
    DateTimeOffset UpdatedAtUtc);

public sealed record PublicPortfolioProjectDto(
    Guid Id,
    string Slug,
    string Title,
    string Summary,
    string? DescriptionHtml,
    string? CoverImageUrl,
    string? CoverImageAlt,
    string? ClientName,
    IReadOnlyList<string> TechStack,
    string? LiveUrl,
    bool IsFeatured,
    IReadOnlyList<ProjectImageDto> Images);

public sealed record SaveProjectImageRequest(Guid? Id, string Url, string? AltText, int SortOrder);

public sealed record SavePortfolioProjectRequest(
    string? Slug,
    string Title,
    string Summary,
    string? DescriptionHtml,
    string? CoverImageUrl,
    string? CoverImageAlt,
    string? ClientName,
    IReadOnlyList<string>? TechStack,
    DateOnly? StartedOn,
    DateOnly? DeliveredOn,
    string? LiveUrl,
    string? RepositoryUrl,
    bool IsFeatured,
    ContentStatus Status,
    int SortOrder,
    IReadOnlyList<SaveProjectImageRequest>? Images);

// ─────────────────────────────────────────────────────────────
// BlogPost + Tag
// ─────────────────────────────────────────────────────────────

public sealed record BlogPostDto(
    Guid Id,
    string Slug,
    string Title,
    string? Excerpt,
    string ContentHtml,
    string? CoverImageUrl,
    string? CoverImageAlt,
    string? SeoTitle,
    string? SeoDescription,
    ContentStatus Status,
    DateTimeOffset? PublishedAtUtc,
    int ReadingMinutes,
    long ViewCount,
    Guid? AuthorId,
    string? AuthorName,
    IReadOnlyList<string> Tags,
    DateTimeOffset UpdatedAtUtc);

/// <summary>Listado público: no manda el HTML completo, solo el extracto y la metadata.</summary>
public sealed record BlogPostSummaryDto(
    Guid Id,
    string Slug,
    string Title,
    string? Excerpt,
    string? CoverImageUrl,
    string? CoverImageAlt,
    DateTimeOffset PublishedAtUtc,
    int ReadingMinutes,
    IReadOnlyList<string> Tags);

public sealed record TagDto(Guid Id, string Name, string Slug);

public sealed record SaveBlogPostRequest(
    string? Slug,
    string Title,
    string? Excerpt,
    string ContentHtml,
    string? CoverImageUrl,
    string? CoverImageAlt,
    string? SeoTitle,
    string? SeoDescription,
    ContentStatus Status,
    Guid? AuthorId,
    IReadOnlyList<string>? Tags);

public sealed record SaveTagRequest(string Name, string? Slug);
