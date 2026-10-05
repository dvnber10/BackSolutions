using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Content;
using BackSolutions.Core.Entities.Content;
using BackSolutions.Core.Interfaces;
using BackSolutions.Data;
using Microsoft.EntityFrameworkCore;

namespace BackSolutions.Api.Services;

/// <summary>
/// Configuración global del sitio. Una sola fila con Id = SiteSettings.SingletonId.
///
/// La fila siempre existe (la crea la migración inicial), pero el servicio la crea
/// igual si faltara, para que un entorno recién provisionado no devuelva 500.
/// </summary>
public sealed class SiteSettingsService : ISiteSettingsService
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public SiteSettingsService(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<SiteSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateAsync(cancellationToken);
        return Map(settings);
    }

    public async Task<SiteSettingsDto> UpdateAsync(UpdateSiteSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.CompanyName))
        {
            errors[nameof(request.CompanyName)] = ["El nombre de la empresa es obligatorio."];
        }

        if (!IsValidEmail(request.ContactEmail))
        {
            errors[nameof(request.ContactEmail)] = ["El email de contacto es obligatorio y debe ser válido."];
        }

        if (request.ProposalValidityDays is < 1 or > 365)
        {
            errors[nameof(request.ProposalValidityDays)] = ["La validez debe estar entre 1 y 365 días."];
        }

        if (request.DefaultCurrency is { Length: not 3 })
        {
            errors[nameof(request.DefaultCurrency)] = ["La moneda debe ser un código ISO de 3 letras, por ejemplo USD."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var settings = await GetOrCreateAsync(cancellationToken);

        settings.CompanyName = request.CompanyName.Trim();
        settings.LegalName = request.LegalName?.Trim() ?? string.Empty;
        settings.TaxId = request.TaxId?.Trim();
        settings.LogoUrl = request.LogoUrl?.Trim();
        settings.FaviconUrl = request.FaviconUrl?.Trim();
        settings.ContactEmail = request.ContactEmail.Trim().ToLowerInvariant();
        settings.Phone = request.Phone?.Trim();
        settings.WhatsApp = request.WhatsApp?.Trim();
        settings.Address = request.Address?.Trim();

        if (request.Social is not null)
        {
            settings.FacebookUrl = request.Social.FacebookUrl?.Trim();
            settings.InstagramUrl = request.Social.InstagramUrl?.Trim();
            settings.LinkedInUrl = request.Social.LinkedInUrl?.Trim();
            settings.GithubUrl = request.Social.GithubUrl?.Trim();
            settings.XUrl = request.Social.XUrl?.Trim();
        }

        if (request.Hero is not null)
        {
            settings.HeroTitle = request.Hero.Title.Trim();
            settings.HeroSubtitle = request.Hero.Subtitle.Trim();
            settings.HeroImageUrl = request.Hero.ImageUrl?.Trim();
            settings.HeroPrimaryCtaText = request.Hero.PrimaryCta.Text.Trim();
            settings.HeroPrimaryCtaUrl = request.Hero.PrimaryCta.Url.Trim();
            settings.HeroSecondaryCtaText = request.Hero.SecondaryCta.Text.Trim();
            settings.HeroSecondaryCtaUrl = request.Hero.SecondaryCta.Url.Trim();
        }

        settings.FooterText = request.FooterText.Trim();
        settings.FooterLegalText = request.FooterLegalText?.Trim();

        if (request.Seo is not null)
        {
            settings.DefaultSeoTitle = request.Seo.Title.Trim();
            settings.DefaultSeoDescription = request.Seo.Description?.Trim();
            settings.DefaultOgImageUrl = request.Seo.OgImageUrl?.Trim();
        }

        if (request.DefaultCurrency is not null)
        {
            settings.DefaultCurrency = request.DefaultCurrency.ToUpperInvariant();
        }

        if (request.ProposalValidityDays is { } validity)
        {
            settings.ProposalValidityDays = validity;
        }

        // UpdatedAtUtc lo mantiene SaveChangesAsync vía IHasTimestamps.
        await _db.SaveChangesAsync(cancellationToken);

        return Map(settings);
    }

    private async Task<SiteSettings> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var settings = await _db.SiteSettings
            .FirstOrDefaultAsync(s => s.Id == SiteSettings.SingletonId, cancellationToken);

        if (settings is not null)
        {
            return settings;
        }

        settings = new SiteSettings { Id = SiteSettings.SingletonId };
        _db.SiteSettings.Add(settings);
        await _db.SaveChangesAsync(cancellationToken);

        return settings;
    }

    private static SiteSettingsDto Map(SiteSettings s) => new(
        s.Id,
        s.CompanyName,
        s.LegalName,
        s.TaxId,
        s.LogoUrl,
        s.FaviconUrl,
        s.ContactEmail,
        s.Phone,
        s.WhatsApp,
        s.Address,
        new SocialLinksDto(s.FacebookUrl, s.InstagramUrl, s.LinkedInUrl, s.GithubUrl, s.XUrl),
        new HeroDto(
            s.HeroTitle,
            s.HeroSubtitle,
            s.HeroImageUrl,
            new CtaDto(s.HeroPrimaryCtaText, s.HeroPrimaryCtaUrl),
            new CtaDto(s.HeroSecondaryCtaText, s.HeroSecondaryCtaUrl)),
        s.FooterText,
        s.FooterLegalText,
        new SeoDefaultsDto(s.DefaultSeoTitle, s.DefaultSeoDescription, s.DefaultOgImageUrl),
        s.DefaultCurrency,
        s.ProposalValidityDays,
        s.UpdatedAtUtc);

    /// <summary>Validación deliberadamente simple: alcanza para un email de contacto.</summary>
    private static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && email.Length <= 320
        && email.Contains('@')
        && !email.Any(char.IsWhiteSpace);
}
