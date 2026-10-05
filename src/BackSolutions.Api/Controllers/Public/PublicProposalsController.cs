using BackSolutions.Api.Security;
using BackSolutions.Core.Dtos.Proposals;
using BackSolutions.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BackSolutions.Api.Controllers.Public;

/// <summary>
/// Propuestas desde el punto de vista del cliente.
///
/// La credencial es el <c>publicToken</c> del lead, que va en la ruta. La validación
/// vive en <see cref="IProposalService.GeneratePublicPdfAsync"/> y no acá: el endpoint no
/// tiene forma de saber si una propuesta es visible para el cliente que pregunta, y un
/// chequeo hecho en el controller se olvida en cuanto se agrega un endpoint al lado.
///
/// Un token que no corresponde a la propuesta responde 401 y una propuesta en borrador
/// responde 404, así que el cliente no puede enumerar lo que hay.
/// </summary>
[ApiController]
[Route("api/public/proposals")]
[AllowAnonymous]
public sealed class PublicProposalsController : ControllerBase
{
    private readonly IProposalService _proposals;

    public PublicProposalsController(IProposalService proposals) => _proposals = proposals;

    /// <summary>
    /// Descarga el PDF y marca la propuesta como vista. La marca es idempotente: volver a
    /// descargar el mismo archivo no vuelve a sumar una vista.
    /// </summary>
    [HttpGet("{publicToken}/{id:guid}/pdf")]
    [EnableRateLimiting(RateLimitPolicies.PublicWrites)]
    [Produces("application/pdf")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> DownloadPdf(
        string publicToken,
        Guid id,
        CancellationToken cancellationToken)
    {
        // El servicio valida el token y descarta los borradores antes de renderizar.
        var pdf = await _proposals.GeneratePublicPdfAsync(id, publicToken, cancellationToken);

        // Va después de generar: si el token no corresponde, el servicio ya cortó con 401
        // y acá no se llega.
        await _proposals.MarkAsViewedAsync(id, publicToken, cancellationToken);

        return File(pdf.Content, pdf.ContentType, pdf.FileName);
    }
}
