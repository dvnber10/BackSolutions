using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Proposals;

namespace BackSolutions.Core.Interfaces;

/// <summary>
/// Propuestas comerciales.
///
/// No hay IA: el equipo redacta la propuesta en la app Kotlin y el backend se
/// encarga de lo mecánico (numeración, importes, validity, PDF). Los totales se
/// calculan siempre acá y nunca se aceptan desde el cliente, para que no exista
/// una forma de colar un importe manipulado.
/// </summary>
public interface IProposalService
{
    Task<PagedResult<ProposalListItemDto>> ListAsync(
        int? page,
        int? pageSize,
        Guid? leadId,
        ProposalStatusFilter status,
        string? search,
        CancellationToken cancellationToken = default);

    Task<ProposalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Crea la propuesta con el próximo número correlativo y recalcula los totales.</summary>
    Task<ProposalDto> CreateAsync(SaveProposalRequest request, CancellationToken cancellationToken = default);

    /// <summary>Actualiza una propuesta. Solo editable mientras esté en borrador.</summary>
    Task<ProposalDto> UpdateAsync(Guid id, SaveProposalRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia el estado. Un borrador pasa a Sent, que sella SentAtUtc y la fecha de
    /// validación si el equipo no la fijó. Los estados terminales no se pueden abandonar
    /// sin pasar por Draft otra vez, y eso exige confirmación explícita en el panel.
    /// </summary>
    Task<ProposalDto> UpdateStatusAsync(Guid id, UpdateProposalStatusRequest request, CancellationToken cancellationToken = default);

    /// <summary>Marca la propuesta como vista por el cliente. Lo invoca el endpoint público.</summary>
    Task MarkAsViewedAsync(Guid proposalId, string publicToken, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Renderiza el PDF con QuestPDF. Los bytes se devuelven en memoria, no se persisten.</summary>
    Task<ProposalPdf> GeneratePdfAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renderiza el PDF para el cliente validando su <c>publicToken</c> contra el lead de
    /// la propuesta. Un borrador responde 404 y un token ajeno 401.
    ///
    /// Es el único método que debe usar el endpoint público: <see cref="GeneratePdfAsync"/>
    /// no lleva ningún control de acceso porque es de uso interno del equipo.
    /// </summary>
    Task<ProposalPdf> GeneratePublicPdfAsync(Guid proposalId, string publicToken, CancellationToken cancellationToken = default);
}

/// <summary>Filtro de estado para listados. <see cref="All"/> no es un estado persistido.</summary>
public enum ProposalStatusFilter
{
    All = -1,
    Draft = 0,
    Sent = 1,
    Viewed = 2,
    Accepted = 3,
    Rejected = 4,
    Expired = 5
}
