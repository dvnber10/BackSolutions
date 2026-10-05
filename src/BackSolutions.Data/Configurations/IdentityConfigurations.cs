using BackSolutions.Core.Common;
using BackSolutions.Core.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BackSolutions.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName("UX_Users_Email");

        builder.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
        builder.Property(u => u.FullName).HasMaxLength(160).IsRequired();
        builder.Property(u => u.AvatarUrl).HasMaxLength(500);
        builder.Property(u => u.Phone).HasMaxLength(32);
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasMaxLength(64).IsRequired();
        builder.HasIndex(r => r.Name).IsUnique().HasDatabaseName("UX_Roles_Name");

        builder.Property(r => r.Description).HasMaxLength(256).IsRequired();

        // Los roles del sistema se insertan con la migración: existen desde el primer
        // despliegue y no dependen de que alguien ejecute un seeder.
        var createdAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        builder.HasData(
            new Role
            {
                Id = SystemRoleIds.Owner,
                Name = RoleNames.Owner,
                Description = "Dueño del sistema. Acceso total.",
                IsSystem = true,
                CreatedAtUtc = createdAt,
                UpdatedAtUtc = createdAt
            },
            new Role
            {
                Id = SystemRoleIds.Admin,
                Name = RoleNames.Admin,
                Description = "Administra contenido, leads, usuarios y conversaciones.",
                IsSystem = true,
                CreatedAtUtc = createdAt,
                UpdatedAtUtc = createdAt
            },
            new Role
            {
                Id = SystemRoleIds.Provider,
                Name = RoleNames.Provider,
                Description = "Trabaja leads y conversaciones asignadas.",
                IsSystem = true,
                CreatedAtUtc = createdAt,
                UpdatedAtUtc = createdAt
            },
            new Role
            {
                Id = SystemRoleIds.Support,
                Name = RoleNames.Support,
                Description = "Atiende conversaciones de chat.",
                IsSystem = true,
                CreatedAtUtc = createdAt,
                UpdatedAtUtc = createdAt
            });
    }
}

/// <summary>GUIDs fijos de los roles del sistema. Cambiarlos rompería los usuarios ya asignados.</summary>
public static class SystemRoleIds
{
    public static readonly Guid Owner = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Admin = new("22222222-2222-2222-2222-222222222222");
    public static readonly Guid Provider = new("33333333-3333-3333-3333-333333333333");
    public static readonly Guid Support = new("44444444-4444-4444-4444-444444444444");
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");

        // Clave compuesta: un usuario no puede tener dos veces el mismo rol.
        builder.HasKey(ur => new { ur.UserId, ur.RoleId });

        builder.HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Índice adicional para buscar "usuarios con este rol".
        builder.HasIndex(ur => ur.RoleId).HasDatabaseName("IX_UserRoles_RoleId");
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("UX_RefreshTokens_TokenHash");

        builder.Property(t => t.ReplacedByTokenHash).HasMaxLength(64);
        builder.Property(t => t.DeviceName).HasMaxLength(120);
        builder.Property(t => t.UserAgent).HasMaxLength(400);
        builder.Property(t => t.IpAddress).HasMaxLength(64);

        builder.HasOne(t => t.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Revocar todos los tokens de un usuario es una consulta por UserId.
        builder.HasIndex(t => t.UserId).HasDatabaseName("IX_RefreshTokens_UserId");
    }
}
