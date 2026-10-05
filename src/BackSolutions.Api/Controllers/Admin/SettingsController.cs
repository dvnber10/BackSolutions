using BackSolutions.Api.Security;
using BackSolutions.Core.Dtos.Content;
using BackSolutions.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackSolutions.Api.Controllers.Admin;

/// <summary>
/// Configuración global del sitio. Es un singleton (Id = 1) y la consumen tanto el panel
/// como el layout público, así que cualquier cambio se refleja en la web de inmediato.
/// </summary>
[ApiController]
[Route("api/admin/settings")]
[Authorize(Policy = AuthorizationPolicies.OwnerOrAdmin)]
[Produces("application/json")]
public sealed class SettingsController : ControllerBase
{
    private readonly ISiteSettingsService _settings;

    public SettingsController(ISiteSettingsService settings) => _settings = settings;

    /// <summary>Datos actuales: datos de la empresa, contacto, redes y pie de página.</summary>
    [HttpGet]
    [ProducesResponseType<SiteSettingsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SiteSettingsDto>> Get(CancellationToken cancellationToken) =>
        Ok(await _settings.GetAsync(cancellationToken));

    /// <summary>Actualiza la configuración. Es un PUT completo: lo que no se manda se conserva.</summary>
    [HttpPut]
    [ProducesResponseType<SiteSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SiteSettingsDto>> Update(
        [FromBody] UpdateSiteSettingsRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _settings.UpdateAsync(request, cancellationToken));
}
