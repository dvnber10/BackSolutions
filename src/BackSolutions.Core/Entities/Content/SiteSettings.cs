using BackSolutions.Core.Common;

namespace BackSolutions.Core.Entities.Content;

/// <summary>
/// Configuración global del sitio. Es un registro único con <see cref="SingletonId"/> fijo,
/// de modo que el panel siempre lee y escribe la misma fila.
/// Reemplaza todo lo que hoy está hardcodeado en el frontend React.
/// </summary>
public class SiteSettings : IHasTimestamps
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    // ── Identidad de la empresa ──
    public string CompanyName { get; set; } = "BackSolutions";
    public string LegalName { get; set; } = string.Empty;
    public string? TaxId { get; set; }
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }

    // ── Contacto ──
    public string ContactEmail { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }
    public string? Address { get; set; }

    // ── Redes sociales ──
    public string? FacebookUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? GithubUrl { get; set; }
    public string? XUrl { get; set; }

    // ── Home ──
    public string HeroTitle { get; set; } = string.Empty;
    public string HeroSubtitle { get; set; } = string.Empty;
    public string? HeroImageUrl { get; set; }
    public string HeroPrimaryCtaText { get; set; } = string.Empty;
    public string HeroPrimaryCtaUrl { get; set; } = string.Empty;
    public string HeroSecondaryCtaText { get; set; } = string.Empty;
    public string HeroSecondaryCtaUrl { get; set; } = string.Empty;

    public string FooterText { get; set; } = string.Empty;
    public string? FooterLegalText { get; set; }

    // ── SEO por defecto ──
    public string DefaultSeoTitle { get; set; } = string.Empty;
    public string? DefaultSeoDescription { get; set; }
    public string? DefaultOgImageUrl { get; set; }

    // ── Facturación / propuestas ──
    public string DefaultCurrency { get; set; } = "USD";
    public int ProposalValidityDays { get; set; } = 30;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
