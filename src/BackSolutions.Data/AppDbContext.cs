using BackSolutions.Core.Common;
using BackSolutions.Core.Entities.Chat;
using BackSolutions.Core.Entities.Content;
using BackSolutions.Core.Entities.Identity;
using BackSolutions.Core.Entities.Leads;
using Microsoft.EntityFrameworkCore;

namespace BackSolutions.Data;

/// <summary>Contexto de EF Core. Único punto donde se conoce el modelo relacional.</summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // ── Identidad ──
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // ── Contenido ──
    public DbSet<SiteSettings> SiteSettings => Set<SiteSettings>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<PageSection> PageSections => Set<PageSection>();
    public DbSet<ServiceItem> Services => Set<ServiceItem>();
    public DbSet<PortfolioProject> Projects => Set<PortfolioProject>();
    public DbSet<ProjectImage> ProjectImages => Set<ProjectImage>();
    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<BlogPostTag> BlogPostTags => Set<BlogPostTag>();

    // ── Cotizaciones ──
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Proposal> Proposals => Set<Proposal>();
    public DbSet<ProposalItem> ProposalItems => Set<ProposalItem>();

    // ── Chat ──
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Escalation> Escalations => Set<Escalation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Cada entidad define su tabla, longitudes e índices en Data/Configurations.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Política de borrado hacia Users: NO ACTION (RESTRICT), no SET NULL.
        //
        // Razón 1 (técnica): SET NULL en una FK que además cuelga de un padre en CASCADE
        // genera el error 1785 de SQL Server. Por ejemplo, Users -> Proposals (SET NULL)
        // convive con Leads -> Proposals (CASCADE), y aparece una segunda ruta de borrado.
        // Afecta a Proposals, Messages y Escalations.
        //
        // Razón 2 (de negocio): los usuarios no se borran, se desactivan con User.IsActive.
        // RESTRICT impide dejar contenido huérfano de autor. Para dar de baja definitiva a
        // alguien hay que reasignar antes su contenido.
        //
        // Lo que sí se borra en cascada al eliminar un usuario es lo que le pertenece
        // exclusivamente: UserRoles, RefreshTokens y ConversationParticipants.

        StoreEnumsAsStrings(modelBuilder);
    }

    /// <summary>
    /// Persiste todos los enums como texto de longitud fija (nvarchar(32)).
    /// Sumar un valor a un enum después no rompe los datos ya guardados, que es
    /// el motivo principal de hacerlo así. La longitud fija además permite
    /// indexarlos y evita el nvarchar(max) que EF genera por defecto.
    /// </summary>
    private static void StoreEnumsAsStrings(ModelBuilder modelBuilder)
    {
        const int enumColumnLength = 32;

        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(entityType => entityType.GetProperties())
                     .Where(property => property.ClrType.IsEnum
                                       || Nullable.GetUnderlyingType(property.ClrType)?.IsEnum is true))
        {
            property.SetMaxLength(enumColumnLength);
            property.SetProviderClrType(typeof(string));
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyTimestamps()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IHasTimestamps>())
        {
            if (entry.State is EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.UpdatedAtUtc = now;
            }
            else if (entry.State is EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
            }
        }
    }
}
