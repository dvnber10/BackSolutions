using BackSolutions.Core.Interfaces;

namespace BackSolutions.Api.Services;

/// <summary>
/// Implementación BCrypt (coste 12). El hash devuelto por BCrypt.Net ya incluye
/// versión, coste y salt, así que <see cref="NeedsRehash"/> compara contra el
/// coste configurado aquí y no contra nada externo.
/// </summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    /// <summary>
    /// Coste 12: ~250 ms por hash en hardware de escritorio. Es el punto que recomienda
    /// la documentación de BCrypt para 2026. Bajarlo para "acelerar" el login abre la
    /// puerta a fuerza bruta sobre hashes filtrados.
    /// </summary>
    private const int WorkFactor = 12;

    private const string Prefix = "$2";

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Hash corrupto o de otra familia. Se trata como credencial inválida.
            return false;
        }
    }

    public bool NeedsRehash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash) || !passwordHash.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return true;
        }

        // Formato: $2a$12$[salt+hash]. Solo el segundo segmento es el coste.
        var parts = passwordHash.Split('$');
        return parts.Length < 4
            || !int.TryParse(parts[2], out var cost)
            || cost < WorkFactor;
    }
}
