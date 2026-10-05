using BackSolutions.Core.Common;
using BackSolutions.Core.Entities.Identity;
using BackSolutions.Core.Enums;
using BackSolutions.Core.Interfaces;
using BackSolutions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BackSolutions.Tests.Infrastructure;

/// <summary>
/// Crea datos de prueba por la vía corta: directo a la base con el hasher real, en vez de
/// pasar por UserService. El alta de usuarios exige un Owner ya autenticado, y en el
/// primer test no hay ninguno; para evitar el problema del huevo y la gallina se inserta
/// el usuario directamente y después se entra por HTTP como cualquier otro.
/// </summary>
internal static class TestData
{
    internal const string DefaultPassword = "ClaveDePrueba123!";

    internal static async Task<TestUser> CreateUserAsync(
        IServiceProvider services,
        string email,
        params string[] roles)
    {
        var context = services.GetRequiredService<AppDbContext>();
        var hasher = services.GetRequiredService<IPasswordHasher>();

        var rolesToAssign = roles.Length > 0 ? roles : [RoleNames.Admin];

        var roleEntities = await context.Roles
            .Where(role => rolesToAssign.Contains(role.Name))
            .ToListAsync();

        var missing = rolesToAssign.Except(roleEntities.Select(role => role.Name)).ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Faltan los roles {string.Join(", ", missing)} en la base. Corré el seeder.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = hasher.Hash(DefaultPassword),
            FullName = "Usuario de prueba",
            IsActive = true,
            MustChangePassword = false,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        context.Users.Add(user);

        foreach (var role in roleEntities)
        {
            user.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id
            });
        }

        await context.SaveChangesAsync();

        return new TestUser(user.Id, email, DefaultPassword, rolesToAssign);
    }

    /// <summary>Crea un lead directo en la base, para probar endpoints que lo necesitan.</summary>
    internal static async Task<Guid> CreateLeadAsync(
        IServiceProvider services,
        string name = "Lead de prueba",
        string email = "lead@prueba.test",
        Guid? serviceId = null)
    {
        var context = services.GetRequiredService<AppDbContext>();

        var lead = new BackSolutions.Core.Entities.Leads.Lead
        {
            Id = Guid.NewGuid(),
            PublicToken = SecureToken.Generate(),
            Name = name,
            Email = email,
            Status = LeadStatus.New,
            Details = "Lead creado por los tests",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        if (serviceId is not null)
        {
            lead.ServiceId = serviceId;
        }

        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        return lead.Id;
    }
}

internal sealed record TestUser(Guid Id, string Email, string Password, string[] Roles);
