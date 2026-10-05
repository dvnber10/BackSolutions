using BackSolutions.Core.Dtos.Admin;
using BackSolutions.Core.Enums;
using BackSolutions.Core.Interfaces;
using BackSolutions.Data;
using Microsoft.EntityFrameworkCore;

namespace BackSolutions.Api.Services;

/// <summary>
/// Métricas de la pantalla inicial del panel.
///
/// Se resuelven con un puñado de consultas agregadas en vez de una por tarjeta: el panel
/// se abre en cada cambio de sesión y en Android puede ser con datos móviles.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private const int VolumeDays = 30;

    private const int TopServicesLimit = 5;

    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public DashboardService(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;
        var since = today.AddDays(-(VolumeDays - 1));
        var sinceUtc = new DateTimeOffset(since.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        // Los agrupados por día se arman en memoria porque SQL Server traduce mal
        // DATEADD/DATEDIFF sobre columnas DateTimeOffset y el conjunto es chico (30 filas).
        var leadsByDay = await _db.Leads
            .AsNoTracking()
            .Where(l => l.CreatedAtUtc >= sinceUtc)
            .Select(l => new { l.CreatedAtUtc, l.Status })
            .ToListAsync(cancellationToken);

        var proposalsByDay = await _db.Proposals
            .AsNoTracking()
            .Where(p => p.SentAtUtc != null && p.SentAtUtc >= sinceUtc)
            .Select(p => p.SentAtUtc!.Value)
            .ToListAsync(cancellationToken);

        var conversationsByDay = await _db.Conversations
            .AsNoTracking()
            .Where(c => c.CreatedAtUtc >= sinceUtc)
            .Select(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var volume = new List<DailyVolumeDto>(VolumeDays);

        for (var offset = 0; offset < VolumeDays; offset++)
        {
            var day = since.AddDays(offset);

            volume.Add(new DailyVolumeDto(
                day,
                leadsByDay.Count(l => DateOnly.FromDateTime(l.CreatedAtUtc.UtcDateTime) == day),
                proposalsByDay.Count(p => DateOnly.FromDateTime(p.UtcDateTime) == day),
                conversationsByDay.Count(c => DateOnly.FromDateTime(c.UtcDateTime) == day)));
        }

        var totalLeads = await _db.Leads.CountAsync(l => l.Status != LeadStatus.Spam, cancellationToken);

        // WonRevenue suma el total de las propuestas aceptadas, que es la única cifra
        // que refleja ingreso confirmado y no solamente enviado.
        var wonRevenue = await _db.Proposals
            .Where(p => p.Status == ProposalStatus.Accepted)
            .SumAsync(p => (decimal?)p.TotalAmount, cancellationToken) ?? 0m;

        // Dos detalles de EF Core 8 que costaron encontrar:
        //  1) agrupar por Lead.ServiceId!.Value no se traduce, y el join con GroupBy sobre
        //     un tipo anónimo tampoco; el conteo va como subconsulta correlacionada.
        //  2) proyectar directo a TopServiceDto y ordenar por uno de sus miembros no se
        //     traduce cuando la proyección trae un Count(); con un tipo anónimo sí.
        // Por eso la consulta termina en un tipo anónimo y el DTO se arma en memoria.
        var topServiceRows = await _db.Services
            .AsNoTracking()
            .Where(s => s.Leads.Any())
            .Select(s => new { s.Id, s.Name, Leads = s.Leads.Count() })
            .OrderByDescending(x => x.Leads)
            .ThenBy(x => x.Name)
            .Take(TopServicesLimit)
            .ToListAsync(cancellationToken);

        var topServices = topServiceRows
            .Select(x => new TopServiceDto(x.Id, x.Name, x.Leads))
            .ToList();

        return new DashboardStatsDto(
            totalLeads,
            await _db.Leads.CountAsync(l => l.Status == LeadStatus.New, cancellationToken),
            await _db.Leads.CountAsync(l => l.Status == LeadStatus.InReview, cancellationToken),
            await _db.Leads.CountAsync(l => l.Status == LeadStatus.Won, cancellationToken),
            await _db.Leads.CountAsync(l => l.Status == LeadStatus.Lost, cancellationToken),
            wonRevenue,
            await _db.Conversations.CountAsync(c => c.Status != ConversationStatus.Closed, cancellationToken),
            await _db.Conversations.CountAsync(c => c.Status == ConversationStatus.AwaitingTeam, cancellationToken),
            await _db.Escalations.CountAsync(e => e.Status == EscalationStatus.Open, cancellationToken),
            await _db.Escalations.CountAsync(e => e.Status == EscalationStatus.Open && e.Severity >= EscalationSeverity.High, cancellationToken),
            await _db.Users.CountAsync(u => u.IsActive, cancellationToken),
            await _db.Pages.CountAsync(p => p.Status == ContentStatus.Published, cancellationToken),
            await _db.Services.CountAsync(s => s.Status == ContentStatus.Published, cancellationToken),
            await _db.Projects.CountAsync(p => p.Status == ContentStatus.Published, cancellationToken),
            await _db.BlogPosts.CountAsync(p => p.Status == ContentStatus.Published, cancellationToken),
            volume,
            topServices);
    }
}
