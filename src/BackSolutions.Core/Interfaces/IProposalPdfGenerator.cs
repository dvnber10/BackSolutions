using BackSolutions.Core.Dtos.Proposals;

namespace BackSolutions.Core.Interfaces;

/// <summary>
/// Renderiza una propuesta a PDF.
///
/// Es una interfaz y no una clase concreta para poder testear ProposalService con
/// un doble, y para dejar la puerta abierta a otro motor de PDF sin tocar el servicio.
/// La implementación real vive en Api/Services/QuestPdfProposalGenerator.
/// </summary>
public interface IProposalPdfGenerator
{
    Task<ProposalPdf> GenerateAsync(
        Core.Entities.Leads.Proposal proposal,
        Core.Entities.Content.SiteSettings settings,
        CancellationToken cancellationToken = default);
}
