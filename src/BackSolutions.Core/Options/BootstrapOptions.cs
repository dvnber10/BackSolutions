namespace BackSolutions.Core.Options;

/// <summary>
/// Datos del primer usuario Owner. Se usa solo para arrancar un sistema vacío:
/// una vez que existe al menos un Owner, el bloque se ignora para siempre.
/// </summary>
public class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    public string? OwnerEmail { get; set; }

    /// <summary>
    /// Si viene vacía se genera una contraseña aleatoria y se muestra por consola
    /// una única vez. Es lo preferible en producción: nadie elige la clave inicial.
    /// </summary>
    public string? OwnerPassword { get; set; }

    public string OwnerFullName { get; set; } = "Owner";
}
