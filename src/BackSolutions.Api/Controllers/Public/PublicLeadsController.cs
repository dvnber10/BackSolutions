using BackSolutions.Api.Security;
using BackSolutions.Core.Dtos.Leads;
using BackSolutions.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BackSolutions.Api.Controllers.Public;

/// <summary>
/// Alta de consultas desde el formulario web y seguimiento por parte del cliente.
///
/// El cliente no tiene usuario ni contraseña: se lo identifica con el
/// <c>publicToken</c> que recibe al enviar la consulta. Ese token es la credencial, así
/// que viaja por la URL y hay que tratarlo como un secreto: no se loguea en texto claro
/// ni se acepta por query en endpoints que devuelven datos del lead.
///
/// El alta pasa por un límite de peticiones más estricto que el resto de la API pública
/// (política <c>PublicWrites</c>): es el endpoint que un bot martillearía para inflar el
/// embudo, y el honeypot del servicio solo lo frena después de haber llegao a la base.
/// </summary>
[ApiController]
[Route("api/public/leads")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class PublicLeadsController : ControllerBase
{
    private readonly ILeadService _leads;
    private readonly ICurrentUser _currentUser;

    public PublicLeadsController(ILeadService leads, ICurrentUser currentUser)
    {
        _leads = leads;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Envía una consulta y devuelve el token con el que el cliente puede seguirla.
    /// Responde siempre 201, incluso si el honeypat descartó el envío, para que un bot no
    /// pueda distinguir un caso del otro.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.PublicWrites)]
    [ProducesResponseType<PublicLeadCreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<PublicLeadCreatedResponse>> Create(
        [FromBody] CreateLeadRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _leads.CreatePublicAsync(
            request,
            _currentUser.IpAddress,
            _currentUser.UserAgent,
            cancellationToken);

        return Created(string.Empty, created);
    }

    /// <summary>
    /// Seguimiento de la consulta por token. Devuelve 404 si el token no existe, en vez de
    /// 403: un token inválido y uno revocado se comportan igual para quien lo prueba.
    /// </summary>
    [HttpGet("{publicToken}")]
    [ProducesResponseType<PublicLeadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicLeadDto>> Track(string publicToken, CancellationToken cancellationToken)
    {
        var lead = await _leads.GetPublicByTokenAsync(publicToken, cancellationToken);
        return lead is null ? NotFound() : Ok(lead);
    }
}
