using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Auth;
using BackSolutions.Core.Entities.Identity;
using BackSolutions.Core.Interfaces;
using BackSolutions.Data;
using Microsoft.EntityFrameworkCore;

namespace BackSolutions.Api.Services;

/// <summary>
/// Autenticación del equipo.
///
/// Dos decisiones de seguridad que conviene no tocar sin pensarlo:
/// el email se normaliza a minúsculas porque el índice único de SQL Server es
/// case-insensitive y "Ana@x.com" y "ana@x.com" serían el mismo usuario; y los
/// fallos de login responden siempre el mismo mensaje, para que el endpoint no
/// sirva de oráculo de qué emails existen.
/// </summary>
public sealed class AuthService : IAuthService
{
    private const int MaxFailedAttempts = 5;

    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private const string InvalidCredentialsMessage = "Email o contraseña incorrectos.";

    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IClock _clock;

    public AuthService(AppDbContext db, ITokenService tokens, IPasswordHasher passwordHasher, IClock clock)
    {
        _db = db;
        _tokens = tokens;
        _passwordHasher = passwordHasher;
        _clock = clock;
    }

    public async Task<TokenResponse> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Email)] = [string.IsNullOrWhiteSpace(email) ? "El email es obligatorio." : "El email no tiene un formato válido."],
                [nameof(request.Password)] = ["La contraseña es obligatoria."]
            });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            // Se verifica un hash señuelo para que el tiempo de respuesta no delate
            // si el email existe o no.
            _passwordHasher.Verify(request.Password, DummyHash);
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        var now = _clock.UtcNow;

        if (user.LockoutEndsAtUtc is not null && user.LockoutEndsAtUtc > now)
        {
            throw new ForbiddenException($"Cuenta bloqueada por intentos fallidos. Reintentá después de las {user.LockoutEndsAtUtc:HH:mm} UTC.");
        }

        if (!user.IsActive)
        {
            throw new ForbiddenException("La cuenta está desactivada.");
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            await RegisterFailedAttemptAsync(user, now, cancellationToken);
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        // Login correcto: limpiar el contador y opportunisticamente rehashear
        // si el coste de BCrypt subió desde que se creó la contraseña.
        user.FailedLoginAttempts = 0;
        user.LockoutEndsAtUtc = null;
        user.LastLoginAtUtc = now;

        if (_passwordHasher.NeedsRehash(user.PasswordHash))
        {
            user.PasswordHash = _passwordHasher.Hash(request.Password);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await _tokens.IssueAsync(user, request.DeviceName, cancellationToken);
    }

    public async Task<TokenResponse> RefreshAsync(
        RefreshRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var response = await _tokens.RotateAsync(request.RefreshToken, null, cancellationToken);

        // Tras rotar, se sincroniza LastLoginAtUtc para que el panel refleje actividad real.
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == response.User.Id, cancellationToken);
        if (user is not null)
        {
            user.LastLoginAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return response;
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default) =>
        await _tokens.RevokeAsync(request.RefreshToken, cancellationToken);

    public async Task<UserProfileResponse> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("El usuario no existe.");

        var roles = user.UserRoles
            .Select(ur => ur.Role.Name)
            .OrderBy(name => name)
            .ToList();

        return new UserProfileResponse(
            user.Id,
            user.Email,
            user.FullName,
            user.AvatarUrl,
            user.Phone,
            user.IsActive,
            user.MustChangePassword,
            roles,
            user.LastLoginAtUtc);
    }

    public async Task ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("El usuario no existe.");

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.CurrentPassword)] = ["La contraseña actual no es correcta."]
            });
        }

        ValidatePasswordStrength(request.NewPassword);

        if (_passwordHasher.Verify(request.NewPassword, user.PasswordHash))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.NewPassword)] = ["La nueva contraseña debe ser distinta de la actual."]
            });
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.MustChangePassword = false;
        user.FailedLoginAttempts = 0;
        user.LockoutEndsAtUtc = null;

        await _db.SaveChangesAsync(cancellationToken);

        // Cambiar la clave cierra el resto de las sesiones: si la clave estaba
        // filtrada, perder el acceso a las demás sesiones es parte de la respuesta.
        await _tokens.RevokeAllForUserAsync(userId, cancellationToken);
    }

    private async Task RegisterFailedAttemptAsync(User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        user.FailedLoginAttempts += 1;

        if (user.FailedLoginAttempts >= MaxFailedAttempts)
        {
            user.LockoutEndsAtUtc = now.Add(LockoutDuration);
            user.FailedLoginAttempts = 0;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    internal static string NormalizeEmail(string? email) => email?.Trim().ToLowerInvariant() ?? string.Empty;

    /// <summary>Valida robustez mínima. Los mensajes son en español y van al formulario tal cual.</summary>
    internal static void ValidatePasswordStrength(string? password)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("La contraseña es obligatoria.");
        }
        else
        {
            if (password.Length < 10)
            {
                errors.Add("Debe tener al menos 10 caracteres.");
            }

            if (password.Length > 128)
            {
                errors.Add("No puede superar los 128 caracteres.");
            }

            if (!password.Any(char.IsUpper))
            {
                errors.Add("Debe incluir al menos una mayúscula.");
            }

            if (!password.Any(char.IsLower))
            {
                errors.Add("Debe incluir al menos una minúscula.");
            }

            if (!password.Any(char.IsDigit))
            {
                errors.Add("Debe incluir al menos un número.");
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["newPassword"] = [.. errors]
            });
        }
    }

    /// <summary>
    /// Hash BCrypt válido y fijo. Solo se verifica para gastar el mismo tiempo que
    /// con un usuario real; nunca corresponde a ninguna contraseña conocida.
    /// </summary>
    private const string DummyHash = "$2a$12$C6UzMDM.H6dfI/f/IKcEeO1s1vBgUXBLtGOLdC8I1zD4eVHhx7qCLm";
}
