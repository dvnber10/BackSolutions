using System.Security.Cryptography;
using BackSolutions.Core.Common;
using BackSolutions.Core.Entities.Identity;
using BackSolutions.Core.Interfaces;
using BackSolutions.Core.Options;
using BackSolutions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BackSolutions.Api.Services;

/// <summary>
/// Crea el primer Owner cuando el sistema está vacío.
///
/// Es la diferencia entre un despliegue que arranca con acceso y uno que queda
/// inaccesible. Solo actúa si no existe ningún usuario con rol Owner, así que es
/// seguro correrlo en cada arranque.
///
/// Si no se configuró contraseña, se genera una aleatoria y se imprime por consola
/// una única vez: es preferible a que la clave inicial del sistema sea conocida de
/// antemano. Si la configuración trae contraseña, se exige además que el usuario la
/// cambie en el primer login.
/// </summary>
public sealed class OwnerBootstrapper
{
    private const int GeneratedPasswordLength = 20;

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly BootstrapOptions _options;
    private readonly IClock _clock;

    public OwnerBootstrapper(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        IOptions<BootstrapOptions> options,
        IClock clock)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _options = options.Value;
        _clock = clock;
    }

    public async Task<BootstrapResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var existingOwners = await _db.UserRoles
            .AnyAsync(ur => ur.Role!.Name == RoleNames.Owner, cancellationToken);

        if (existingOwners)
        {
            return BootstrapResult.Skipped();
        }

        var email = AuthService.NormalizeEmail(_options.OwnerEmail);

        if (string.IsNullOrWhiteSpace(email))
        {
            return BootstrapResult.Skipped("No hay ningún Owner y tampoco se configuró 'Bootstrap:OwnerEmail'.");
        }

        if (await _db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return BootstrapResult.Skipped($"El usuario {email} existe pero no tiene el rol Owner. Asignáselo desde el panel.");
        }

        var generated = string.IsNullOrWhiteSpace(_options.OwnerPassword);
        var password = generated ? GeneratePassword() : _options.OwnerPassword!.Trim();

        if (!generated)
        {
            AuthService.ValidatePasswordStrength(password);
        }

        var ownerRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.Owner, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Falta el rol {RoleNames.Owner}. El sistema no fue inicializado correctamente.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FullName = string.IsNullOrWhiteSpace(_options.OwnerFullName) ? "Owner" : _options.OwnerFullName.Trim(),
            PasswordHash = _passwordHasher.Hash(password),
            IsActive = true,
            // Siempre se exige rotar la clave inicial, venga de configuración o no.
            MustChangePassword = true,
            CreatedAtUtc = _clock.UtcNow
        };

        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = ownerRole.Id });

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        return new BootstrapResult(
            Created: true,
            Email: email,
            GeneratedPassword: generated ? password : null,
            Message: generated
                ? $"Owner creado: {email}. Contraseña inicial (se muestra una sola vez): {password}"
                : $"Owner creado: {email}.");
    }

    /// <summary>
    /// Contraseña aleatoria de 20 caracteres sobre un alfabeto sin símbolos ambiguos
    /// (sin 0/O, ni 1/l/I) para que se pueda transcribir a mano desde un log.
    /// </summary>
    private static string GeneratePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%&*?";

        var alphabet = upper + lower + digits + symbols;
        var all = RandomNumberGenerator.GetBytes(GeneratedPasswordLength);

        // Se garantiza al menos uno de cada clase para que cumpla la validación de robustez.
        var characters = new List<char>
        {
            upper[all[0] % upper.Length],
            lower[all[1] % lower.Length],
            digits[all[2] % digits.Length],
            symbols[all[3] % symbols.Length]
        };

        for (var index = 4; index < GeneratedPasswordLength; index++)
        {
            characters.Add(alphabet[all[index] % alphabet.Length]);
        }

        // Mezcla con Fisher-Yates sobre RNG criptográfico para que los caracteres
        // no salgan en un orden predecible por clase.
        for (var index = characters.Count - 1; index > 0; index--)
        {
            var swap = RandomNumberGenerator.GetInt32(index + 1);
            (characters[index], characters[swap]) = (characters[swap], characters[index]);
        }

        return new string([.. characters]);
    }
}

public sealed record BootstrapResult(bool Created, string? Email, string? GeneratedPassword, string Message)
{
    public static BootstrapResult Skipped(string? message = null) =>
        new(false, null, null, message ?? "El sistema ya tiene un Owner: no se creó nada.");
}
