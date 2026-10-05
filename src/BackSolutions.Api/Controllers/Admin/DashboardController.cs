using BackSolutions.Api.Security;
using BackSolutions.Core.Dtos.Admin;
using BackSolutions.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackSolutions.Api.Controllers.Admin;

/// <summary>
/// Métricas de la pantalla inicial del panel.
///
/// Cualquier miembro del equipo puede verlas: son números de negocio, no datos
/// personales. Los listados con datos de contacto sí están restringidos por rol.
/// </summary>
[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Policy = AuthorizationPolicies.TeamMember)]
[Produces("application/json")]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    /// <summary>Totales, volumen por día, distribución por estado y tops de la semana.</summary>
    [HttpGet("stats")]
    [ProducesResponseType<DashboardStatsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DashboardStatsDto>> Stats(CancellationToken cancellationToken) =>
        Ok(await _dashboard.GetStatsAsync(cancellationToken));
}
