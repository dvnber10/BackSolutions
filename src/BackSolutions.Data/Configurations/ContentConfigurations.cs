using BackSolutions.Core.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BackSolutions.Data.Configurations;

public class SiteSettingsConfiguration : IEntityTypeConfiguration<SiteSettings>
{
    public void Configure(EntityTypeBuilder<SiteSettings> builder)
    {
        builder.ToTable("SiteSettings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.CompanyName).HasMaxLength(160).IsRequired();
        builder.Property(s => s.LegalName).HasMaxLength(200).IsRequired();
        builder.Property(s => s.TaxId).HasMaxLength(64);
        builder.Property(s => s.LogoUrl).HasMaxLength(500);
        builder.Property(s => s.FaviconUrl).HasMaxLength(500);

        builder.Property(s => s.ContactEmail).HasMaxLength(320).IsRequired();
        builder.Property(s => s.Phone).HasMaxLength(32);
        builder.Property(s => s.WhatsApp).HasMaxLength(32);
        builder.Property(s => s.Address).HasMaxLength(300);

        builder.Property(s => s.FacebookUrl).HasMaxLength(500);
        builder.Property(s => s.InstagramUrl).HasMaxLength(500);
        builder.Property(s => s.LinkedInUrl).HasMaxLength(500);
        builder.Property(s => s.GithubUrl).HasMaxLength(500);
        builder.Property(s => s.XUrl).HasMaxLength(500);

        builder.Property(s => s.HeroTitle).HasMaxLength(200).IsRequired();
        builder.Property(s => s.HeroSubtitle).HasMaxLength(500).IsRequired();
        builder.Property(s => s.HeroImageUrl).HasMaxLength(500);
        builder.Property(s => s.HeroPrimaryCtaText).HasMaxLength(60).IsRequired();
        builder.Property(s => s.HeroPrimaryCtaUrl).HasMaxLength(300).IsRequired();
        builder.Property(s => s.HeroSecondaryCtaText).HasMaxLength(60).IsRequired();
        builder.Property(s => s.HeroSecondaryCtaUrl).HasMaxLength(300).IsRequired();

        builder.Property(s => s.FooterText).HasMaxLength(500).IsRequired();
        builder.Property(s => s.FooterLegalText).HasMaxLength(1000);

        builder.Property(s => s.DefaultSeoTitle).HasMaxLength(200).IsRequired();
        builder.Property(s => s.DefaultSeoDescription).HasMaxLength(500);
        builder.Property(s => s.DefaultOgImageUrl).HasMaxLength(500);

        builder.Property(s => s.DefaultCurrency).HasMaxLength(3).IsRequired();

        builder.HasData(new SiteSettings
        {
            Id = SiteSettings.SingletonId,
            CompanyName = "BackSolutions",
            LegalName = "BackSolutions",
            ContactEmail = "contacto@backsolutions.dev",
            HeroTitle = "Soluciones digitales a medida",
            HeroSubtitle = "Desarrollamos software que resuelve problemas reales de negocio.",
            HeroPrimaryCtaText = "Solicitar cotización",
            HeroPrimaryCtaUrl = "/contact",
            HeroSecondaryCtaText = "Ver portafolio",
            HeroSecondaryCtaUrl = "/portfolio",
            FooterText = "© BackSolutions. Todos los derechos reservados.",
            DefaultSeoTitle = "BackSolutions | Soluciones digitales a medida",
            DefaultSeoDescription = "Agencia de desarrollo de software: sitios web, aplicaciones, integraciones y automatización.",
            DefaultCurrency = "USD",
            ProposalValidityDays = 30,
            CreatedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        });
    }
}

public class PageConfiguration : IEntityTypeConfiguration<Page>
{
    public void Configure(EntityTypeBuilder<Page> builder)
    {
        builder.ToTable("Pages");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Slug).HasMaxLength(80).IsRequired();
        builder.HasIndex(p => p.Slug).IsUnique().HasDatabaseName("UX_Pages_Slug");

        builder.Property(p => p.Title).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Eyebrow).HasMaxLength(80);
        builder.Property(p => p.Intro).HasMaxLength(1000);
        builder.Property(p => p.SeoTitle).HasMaxLength(200);
        builder.Property(p => p.SeoDescription).HasMaxLength(500);
        builder.Property(p => p.OgImageUrl).HasMaxLength(500);

        builder.HasOne(p => p.Author)
            .WithMany()
            .HasForeignKey(p => p.AuthorId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(p => p.Sections)
            .WithOne(s => s.Page)
            .HasForeignKey(s => s.PageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PageSectionConfiguration : IEntityTypeConfiguration<PageSection>
{
    public void Configure(EntityTypeBuilder<PageSection> builder)
    {
        builder.ToTable("PageSections");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Title).HasMaxLength(200);
        builder.Property(s => s.Subtitle).HasMaxLength(500);

        // nvarchar(max): el payload JSON de cada tipo de bloque tiene forma distinta.
        builder.Property(s => s.ContentJson).HasColumnType("nvarchar(max)").IsRequired();

        builder.HasIndex(s => new { s.PageId, s.SortOrder }).HasDatabaseName("IX_PageSections_PageId_SortOrder");
    }
}

public class ServiceItemConfiguration : IEntityTypeConfiguration<ServiceItem>
{
    public void Configure(EntityTypeBuilder<ServiceItem> builder)
    {
        builder.ToTable("Services");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Slug).HasMaxLength(80).IsRequired();
        builder.HasIndex(s => s.Slug).IsUnique().HasDatabaseName("UX_Services_Slug");

        builder.Property(s => s.Name).HasMaxLength(160).IsRequired();
        builder.Property(s => s.ShortDescription).HasMaxLength(300).IsRequired();
        builder.Property(s => s.FullDescription).HasColumnType("nvarchar(max)");
        builder.Property(s => s.Icon).HasMaxLength(80);
        builder.Property(s => s.ImageUrl).HasMaxLength(500);
        builder.Property(s => s.TechnologiesJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(s => s.PriceNote).HasMaxLength(300);

        builder.Property(s => s.BasePrice).HasPrecision(18, 2);

        builder.HasIndex(s => new { s.Status, s.SortOrder }).HasDatabaseName("IX_Services_Status_SortOrder");
    }
}

public class PortfolioProjectConfiguration : IEntityTypeConfiguration<PortfolioProject>
{
    public void Configure(EntityTypeBuilder<PortfolioProject> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Slug).HasMaxLength(120).IsRequired();
        builder.HasIndex(p => p.Slug).IsUnique().HasDatabaseName("UX_Projects_Slug");

        builder.Property(p => p.Title).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Summary).HasMaxLength(500).IsRequired();
        builder.Property(p => p.DescriptionHtml).HasColumnType("nvarchar(max)");
        builder.Property(p => p.CoverImageUrl).HasMaxLength(500);
        builder.Property(p => p.CoverImageAlt).HasMaxLength(300);
        builder.Property(p => p.ClientName).HasMaxLength(200);
        builder.Property(p => p.TechStackJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(p => p.LiveUrl).HasMaxLength(500);
        builder.Property(p => p.RepositoryUrl).HasMaxLength(500);

        builder.Property(p => p.StartedOn).HasColumnType("date");
        builder.Property(p => p.DeliveredOn).HasColumnType("date");

        builder.HasMany(p => p.Images)
            .WithOne(i => i.Project)
            .HasForeignKey(i => i.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.Status, p.SortOrder }).HasDatabaseName("IX_Projects_Status_SortOrder");
    }
}

public class ProjectImageConfiguration : IEntityTypeConfiguration<ProjectImage>
{
    public void Configure(EntityTypeBuilder<ProjectImage> builder)
    {
        builder.ToTable("ProjectImages");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Url).HasMaxLength(500).IsRequired();
        builder.Property(i => i.AltText).HasMaxLength(300);

        builder.HasIndex(i => new { i.ProjectId, i.SortOrder }).HasDatabaseName("IX_ProjectImages_ProjectId_SortOrder");
    }
}

public class BlogPostConfiguration : IEntityTypeConfiguration<BlogPost>
{
    public void Configure(EntityTypeBuilder<BlogPost> builder)
    {
        builder.ToTable("BlogPosts");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Slug).HasMaxLength(160).IsRequired();
        builder.HasIndex(p => p.Slug).IsUnique().HasDatabaseName("UX_BlogPosts_Slug");

        builder.Property(p => p.Title).HasMaxLength(250).IsRequired();
        builder.Property(p => p.Excerpt).HasMaxLength(500);
        builder.Property(p => p.ContentHtml).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(p => p.CoverImageUrl).HasMaxLength(500);
        builder.Property(p => p.CoverImageAlt).HasMaxLength(300);
        builder.Property(p => p.SeoTitle).HasMaxLength(200);
        builder.Property(p => p.SeoDescription).HasMaxLength(500);

        builder.HasOne(p => p.Author)
            .WithMany()
            .HasForeignKey(p => p.AuthorId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(p => p.PostTags)
            .WithOne(pt => pt.Post)
            .HasForeignKey(pt => pt.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        // Índice compuesto para el listado del blog: solo publicados, del más nuevo al más viejo.
        builder.HasIndex(p => new { p.Status, p.PublishedAtUtc })
            .HasDatabaseName("IX_BlogPosts_Status_PublishedAtUtc");
    }
}

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).HasMaxLength(60).IsRequired();
        builder.HasIndex(t => t.Name).IsUnique().HasDatabaseName("UX_Tags_Name");

        builder.Property(t => t.Slug).HasMaxLength(60).IsRequired();
        builder.HasIndex(t => t.Slug).IsUnique().HasDatabaseName("UX_Tags_Slug");
    }
}

public class BlogPostTagConfiguration : IEntityTypeConfiguration<BlogPostTag>
{
    public void Configure(EntityTypeBuilder<BlogPostTag> builder)
    {
        builder.ToTable("BlogPostTags");
        builder.HasKey(pt => new { pt.PostId, pt.TagId });

        builder.HasOne(pt => pt.Tag)
            .WithMany(t => t.PostTags)
            .HasForeignKey(pt => pt.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(pt => pt.TagId).HasDatabaseName("IX_BlogPostTags_TagId");
    }
}
