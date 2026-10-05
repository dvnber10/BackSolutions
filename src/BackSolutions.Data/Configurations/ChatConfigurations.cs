using BackSolutions.Core.Entities.Chat;
using BackSolutions.Core.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BackSolutions.Data.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.PublicToken).HasMaxLength(64).IsRequired();
        builder.HasIndex(c => c.PublicToken).IsUnique().HasDatabaseName("UX_Conversations_PublicToken");

        builder.Property(c => c.ClientName).HasMaxLength(160).IsRequired();
        builder.Property(c => c.ClientEmail).HasMaxLength(320).IsRequired();
        builder.Property(c => c.ClientPhone).HasMaxLength(32);
        builder.Property(c => c.Subject).HasMaxLength(200).IsRequired();
        builder.Property(c => c.CloseReason).HasMaxLength(500);

        // SET NULL: el historial de chat sobrevive al lead.
        builder.HasOne(c => c.Lead)
            .WithMany(l => l.Conversations)
            .HasForeignKey(c => c.LeadId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.AssignedToUser)
            .WithMany()
            .HasForeignKey(c => c.AssignedToUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Participants)
            .WithOne(p => p.Conversation)
            .HasForeignKey(p => p.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Escalations)
            .WithOne(e => e.Conversation)
            .HasForeignKey(e => e.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Bandeja del equipo: conversación abierta, más reciente primero.
        builder.HasIndex(c => new { c.Status, c.LastMessageAtUtc })
            .HasDatabaseName("IX_Conversations_Status_LastMessageAtUtc");

        // Prioridad de la bandeja, de mayor severidad a menor.
        builder.HasIndex(c => c.HighestSeverity).HasDatabaseName("IX_Conversations_HighestSeverity");
    }
}

public class ConversationParticipantConfiguration : IEntityTypeConfiguration<ConversationParticipant>
{
    public void Configure(EntityTypeBuilder<ConversationParticipant> builder)
    {
        builder.ToTable("ConversationParticipants");
        builder.HasKey(p => p.Id);

        builder.HasIndex(p => new { p.ConversationId, p.UserId }).IsUnique()
            .HasDatabaseName("UX_ConversationParticipants_ConversationId_UserId");

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Body).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(m => m.AttachmentName).HasMaxLength(255);
        builder.Property(m => m.AttachmentContentType).HasMaxLength(150);

        builder.HasOne(m => m.SenderUser)
            .WithMany()
            .HasForeignKey(m => m.SenderUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Carga del historial completo del hilo, en orden cronológico.
        builder.HasIndex(m => new { m.ConversationId, m.SentAtUtc })
            .HasDatabaseName("IX_Messages_ConversationId_SentAtUtc");
    }
}

public class EscalationConfiguration : IEntityTypeConfiguration<Escalation>
{
    public void Configure(EntityTypeBuilder<Escalation> builder)
    {
        builder.ToTable("Escalations");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.ResolutionNote).HasMaxLength(2000);

        // MessageId no se mapea como FK: ver la nota en la entidad Escalation.
        // Si algún día se necesita la navegación Message, hay que rethinkar el ciclo
        // entre Conversations, Messages y Escalations antes de agregarla.

        builder.HasOne(e => e.RaisedByUser)
            .WithMany()
            .HasForeignKey(e => e.RaisedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.AssignedToUser)
            .WithMany()
            .HasForeignKey(e => e.AssignedToUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Bandeja de escalamientos pendientes.
        builder.HasIndex(e => new { e.Status, e.Severity }).HasDatabaseName("IX_Escalations_Status_Severity");
    }
}
