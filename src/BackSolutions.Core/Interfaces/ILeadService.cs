using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Leads;
using BackSolutions.Core.Enums;

namespace BackSolutions.Core.Interfaces;

/// <summary>
/// Gestión de leads.
///
/// Un lead es un contacto comercial anónimo: no tiene usuario ni contraseña. El acceso
/// del cliente a su propia consulta se hace con <c>Lead.PublicToken</c>, que es un
/// secreto que se entrega una vez y se guarda en el navegador del visitante.
/// Por eso el servicio separa las operaciones públicas (autenticadas por token)
/// de las del equipo (autenticadas por JWT).
/// </summary>
public interface ILeadService
{
    /// <summary>
    /// Alta desde el formulario público. Incluye un campo trampa (<c>Website</c>) que
    /// los humanos no ven: si viene relleno se descarta el lead en silencio y se
    /// devuelve una respuesta idéntica a la de un alta real, para que el bot no aprenda.
    /// </summary>
    Task<PublicLeadCreatedResponse> CreatePublicAsync(CreateLeadRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);

    /// <summary>Consulta pública del lead por su token. Null si el token no existe.</summary>
    Task<PublicLeadDto?> GetPublicByTokenAsync(string publicToken, CancellationToken cancellationToken = default);

    /// <summary>Bandeja del equipo, con filtros combinables y orden por más reciente.</summary>
    Task<PagedResult<LeadListItemDto>> ListAsync(
        int? page,
        int? pageSize,
        LeadStatusFilter status,
        Guid? assignedToUserId,
        bool unassignedOnly,
        string? search,
        CancellationToken cancellationToken = default);

    Task<LeadDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<LeadDetailDto> UpdateStatusAsync(Guid id, UpdateLeadStatusRequest request, CancellationToken cancellationToken = default);

    /// <summary>Asigna o desasigna un responsable. Null en UserId desasigna.</summary>
    Task<LeadDetailDto> AssignAsync(Guid id, AssignLeadRequest request, CancellationToken cancellationToken = default);

    Task<LeadDetailDto> UpdateNotesAsync(Guid id, UpdateLeadNotesRequest request, CancellationToken cancellationToken = default);

    /// <summary>Elimina el lead con sus propuestas (CASCADE). Reservado al Owner.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Filtro de estado para la bandeja de leads. <see cref="All"/> no es un estado persistido.</summary>
public enum LeadStatusFilter
{
    All = -1,
    New = 0,
    InReview = 1,
    QuoteSent = 2,
    Won = 3,
    Lost = 4,
    Spam = 5,
    Archived = 6
}
