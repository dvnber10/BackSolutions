using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Auth;
using BackSolutions.Core.Entities.Identity;
using BackSolutions.Core.Interfaces;
using BackSolutions.Core.Options;
using BackSolutions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BackSolutions.Api.Services;

/// <summary>
/// Emite y rota los tokens de sesión.
///
/// El access token es un JWT corto que el cliente adjunta en cada llamada. El refresh
/// token es opaco, aleatorio y de un solo uso: se persiste su SHA-256 y se devuelve el
/// valor en claro únicamente al emitirlo. La rotación con detección de reuso es lo que
/// convierte un refresh token robado en un incidente detectable en vez de silencioso.
/// </summary>
public sealed class TokenService : ITokenService
{
    private readonly AppDbContext _db;
    private readonly JwtOptions _options;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public TokenService(
        AppDbContext db,
        IOptions<JwtOptions> options,
        ICurrentUser currentUser,
        IClock clock)
    {
        _db = db;
        _options = options.Value;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<TokenResponse> IssueAsync(User user, string? deviceName, CancellationToken cancellationToken = default)
    {
        var roles = await ResolveRolesAsync(user.Id, cancellationToken);
        var accessToken = CreateAccessToken(user, roles);
        var refreshToken = await PersistRefreshTokenAsync(user, deviceName, cancellationToken);

        var now = _clock.UtcNow;

        return new TokenResponse(
            accessToken,
            refreshToken.PlainText,
            now.AddMinutes(_options.AccessTokenMinutes),
            refreshToken.Entity.ExpiresAtUtc,
            MapProfile(user, roles));
    }

    public async Task<TokenResponse> RotateAsync(string refreshToken, string? deviceName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(refreshToken)] = ["El refresh token es obligatorio."]
            });
        }

        var hash = SecureToken.Sha256Hex(refreshToken);
        var stored = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (stored is null)
        {
            // Token desconocido: puede ser inventado o de otra base. No se revela cuál.
            throw new UnauthorizedException("Refresh token inválido.");
        }

        // Reutilización de un token ya revocado: alguien copió un token viejo.
        // Se corta toda la sesión del usuario, no solo este token.
        if (stored.RevokedAtUtc is not null)
        {
            await RevokeAllForUserAsync(stored.UserId, cancellationToken);
            throw new UnauthorizedException("Refresh token revocado. Por seguridad se cerraron todas las sesiones.");
        }

        if (stored.ExpiresAtUtc <= _clock.UtcNow)
        {
            throw new UnauthorizedException("Refresh token expirado. Volvé a iniciar sesión.");
        }

        if (!stored.User.IsActive)
        {
            throw new ForbiddenException("La cuenta está desactivada.");
        }

        var roles = await ResolveRolesAsync(stored.UserId, cancellationToken);
        var accessToken = CreateAccessToken(stored.User, roles);

        // Rotación: el token viejo queda revocado y apunta a su reemplazo.
        var replacement = await PersistRefreshTokenAsync(
            stored.User,
            deviceName ?? stored.DeviceName,
            cancellationToken);

        stored.RevokedAtUtc = _clock.UtcNow;
        stored.ReplacedByTokenHash = replacement.Hash;
        await _db.SaveChangesAsync(cancellationToken);

        return new TokenResponse(
            accessToken,
            replacement.PlainText,
            _clock.UtcNow.AddMinutes(_options.AccessTokenMinutes),
            replacement.Entity.ExpiresAtUtc,
            MapProfile(stored.User, roles));
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = SecureToken.Sha256Hex(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (stored is null || stored.RevokedAtUtc is not null)
        {
            // Idempotente: revocar un token ya revocado no es un error.
            return;
        }

        stored.RevokedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Se actualizan entidades trackeadas y no con ExecuteUpdateAsync: el update
        // masivo va directo a la base y deja el change tracker desincronizado, así que
        // un token ya cargado seguiría pareciendo vigente en memoria y la detección de
        // reutilización no cortaría la sesión.
        var now = _clock.UtcNow;

        var active = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        if (active.Count == 0)
        {
            return;
        }

        foreach (var token in active)
        {
            token.RevokedAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<(string PlainText, string Hash, RefreshToken Entity)> PersistRefreshTokenAsync(
        User user,
        string? deviceName,
        CancellationToken cancellationToken)
    {
        var plainText = SecureToken.Generate(32);
        var now = _clock.UtcNow;

        var entity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = SecureToken.Sha256Hex(plainText),
            ExpiresAtUtc = now.AddDays(_options.RefreshTokenDays),
            DeviceName = Trim(deviceName, 120),
            UserAgent = Trim(_currentUser.UserAgent, 400),
            IpAddress = Trim(_currentUser.IpAddress, 64),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return (plainText, entity.TokenHash, entity);
    }

    private string CreateAccessToken(User user, IReadOnlyList<string> roles)
    {
        var now = _clock.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(_options.AccessTokenMinutes).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<IReadOnlyList<string>> ResolveRolesAsync(Guid userId, CancellationToken cancellationToken) =>
        await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role!.Name)
            .ToListAsync(cancellationToken);

    private static UserProfileResponse MapProfile(User user, IReadOnlyList<string> roles) => new(
        user.Id,
        user.Email,
        user.FullName,
        user.AvatarUrl,
        user.Phone,
        user.IsActive,
        user.MustChangePassword,
        roles,
        user.LastLoginAtUtc);

    private static string? Trim(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= maxLength ? value : value[..maxLength];
}
