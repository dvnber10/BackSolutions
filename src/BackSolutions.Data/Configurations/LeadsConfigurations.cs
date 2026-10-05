using BackSolutions.Core.Entities.Content;
using BackSolutions.Core.Entities.Leads;
using BackSolutions.Core.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BackSolutions.Data.Configurations;

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("Leads");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.PublicToken).HasMaxLength(64).IsRequired();
        builder.HasIndex(l => l.PublicToken).IsUnique().HasDatabaseName("UX_Leads_PublicToken");

        builder.Property(l => l.Name).HasMaxLength(160).IsRequired();
        builder.Property(l => l.Email).HasMaxLength(320).IsRequired();
        builder.Property(l => l.Phone).HasMaxLength(32);
        builder.Property(l => l.Details).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(l => l.InternalNotes).HasColumnType("nvarchar(max)");
        builder.Property(l => l.Currency).HasMaxLength(3);
        builder.Property(l => l.IpAddress).HasMaxLength(64);
        builder.Property(l => l.UserAgent).HasMaxLength(400);

        builder.Property(l => l.BudgetMin).HasPrecision(18, 2);
        builder.Property(l => l.BudgetMax).HasPrecision(18, 2);
        builder.Property(l => l.TargetStartDate).HasColumnType("date");

        // SET NULL: borrar un servicio no debe borrar los leads que lo contrataron.
        builder.HasOne(l => l.Service)
            .WithMany(s => s.Leads)
            .HasForeignKey(l => l.ServiceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(l => l.HandledByUser)
            .WithMany()
            .HasForeignKey(l => l.HandledByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // La bandeja del panel filtra por estado y ordena por fecha de alta.
        builder.HasIndex(l => new { l.Status, l.CreatedAtUtc })
            .HasDatabaseName("IX_Leads_Status_CreatedAtUtc");

        // Búsqueda por email del cliente.
        builder.HasIndex(l => l.Email).HasDatabaseName("IX_Leads_Email");
    }
}

public class ProposalConfiguration : IEntityTypeConfiguration<Proposal>
{
    public void Configure(EntityTypeBuilder<Proposal> builder)
    {
        builder.ToTable("Proposals");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Number).HasMaxLength(32).IsRequired();
        builder.HasIndex(p => p.Number).IsUnique().HasDatabaseName("UX_Proposals_Number");

        builder.Property(p => p.Title).HasMaxLength(250).IsRequired();
        builder.Property(p => p.SummaryHtml).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(p => p.NotesHtml).HasColumnType("nvarchar(max)");
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();

        builder.Property(p => p.Subtotal).HasPrecision(18, 2);
        builder.Property(p => p.DiscountAmount).HasPrecision(18, 2);
        builder.Property(p => p.TaxAmount).HasPrecision(18, 2);
        builder.Property(p => p.TotalAmount).HasPrecision(18, 2);

        builder.HasOne(p => p.Lead)
            .WithMany(l => l.Proposals)
            .HasForeignKey(p => p.LeadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.CreatedByUser)
            .WithMany()
            .HasForeignKey(p => p.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(p => p.Items)
            .WithOne(i => i.Proposal)
            .HasForeignKey(i => i.ProposalId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProposalItemConfiguration : IEntityTypeConfiguration<ProposalItem>
{
    public void Configure(EntityTypeBuilder<ProposalItem> builder)
    {
        builder.ToTable("ProposalItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Description).HasMaxLength(500).IsRequired();

        builder.Property(i => i.Quantity).HasPrecision(18, 2);
        builder.Property(i => i.UnitPrice).HasPrecision(18, 2);
        builder.Property(i => i.DiscountPercent).HasPrecision(5, 2);

        builder.HasIndex(i => new { i.ProposalId, i.SortOrder }).HasDatabaseName("IX_ProposalItems_ProposalId_SortOrder");
    }
}
