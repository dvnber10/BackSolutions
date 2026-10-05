using System.Security.Cryptography;
using System.Text;

namespace BackSolutions.Core.Common;

/// <summary>
/// Generación y hasheo de tokens opacos (refresh tokens y PublicToken de leads y conversaciones).
///
/// El patrón es siempre el mismo: se genera un valor aleatorio criptográficamente seguro,
/// se devuelve al cliente en claro una sola vez y se persiste únicamente su SHA-256.
/// Si alguien lee la base de datos no puede suplantar a nadie.
/// </summary>
public static class SecureToken
{
    /// <summary>Token en base64url, apto para ir en query string o header sin escaparse.</summary>
    public static string Generate(int byteLength = 32)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Base64UrlEncode(bytes);
    }

    /// <summary>
    /// SHA-256 en hexadecimal minúsculas. Coincide con el máximo de 64 caracteres
    /// configurado para RefreshToken.TokenHash.
    /// </summary>
    public static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Comparación en tiempo constante. Evita que el tiempo de respuesta revele
    /// cuántos caracteres correctos lleva el token.
    /// </summary>
    public static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a),
            Encoding.UTF8.GetBytes(b));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
