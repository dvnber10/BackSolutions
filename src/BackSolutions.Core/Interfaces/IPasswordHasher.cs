namespace BackSolutions.Core.Interfaces;

/// <summary>
/// Hasheo y verificación de contraseñas.
///
/// Vive detrás de una interfaz para que los servicios no dependan de BCrypt y para
/// que el algoritmo se pueda cambiar sin tocar la lógica de negocio. El hash es
/// auto-descriptivo (incluye versión, costo y salt), así que migrar de BCrypt a
/// Argon2 más adelante es cambiar esta implementación, no la base de datos.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Devuelve el hash auto-descriptivo listo para persistir en User.PasswordHash.</summary>
    string Hash(string password);

    /// <summary>
    /// Verifica contra el hash persistido. Devuelve false en vez de lanzar cuando el
    /// hash está corrupto: un login con datos basura es un login fallado, no un 500.
    /// </summary>
    bool Verify(string password, string passwordHash);

    /// <summary>
    /// Indica si el hash usa parámetros por debajo de los actuales y conviene
    /// rehashear la contraseña en el próximo login exitoso.
    /// </summary>
    bool NeedsRehash(string passwordHash);
}
