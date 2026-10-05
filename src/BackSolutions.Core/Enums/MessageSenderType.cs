namespace BackSolutions.Core.Enums;

public enum MessageSenderType
{
    /// <summary>Visitante anónimo del sitio web, identificado por su PublicToken.</summary>
    Client = 0,

    /// <summary>Miembro del equipo autenticado en la app o en el panel.</summary>
    Team = 1,

    /// <summary>Mensaje generado por el sistema (asignaciones, avisos de escalamiento).</summary>
    System = 2
}
