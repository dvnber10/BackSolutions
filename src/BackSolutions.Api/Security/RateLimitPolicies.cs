namespace BackSolutions.Api.Security;

/// <summary>
/// Nombres de las políticas de rate limiting. Igual que las de autorización, viven en un
/// solo lugar para que un typo no deje un endpoint sin límite.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>
    /// Escrituras públicas: alta de lead, chat y descarga de PDF. Es lo que un bot
    /// martillearía para inflar el embudo o agotar la CPU generando PDFs.
    /// </summary>
    public const string PublicWrites = nameof(PublicWrites);

    /// <summary>
    /// Login. Mucho más estricto que el resto porque es el objetivo natural de un ataque
    /// de fuerza bruta; el bloqueo por intentos del servicio es la segunda capa.
    /// </summary>
    public const string Auth = nameof(Auth);
}
