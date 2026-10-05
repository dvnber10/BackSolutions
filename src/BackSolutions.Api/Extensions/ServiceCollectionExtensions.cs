using BackSolutions.Api.Middleware;
using BackSolutions.Api.Services;
using BackSolutions.Core.Common;
using BackSolutions.Core.Interfaces;
using BackSolutions.Data;
using Microsoft.EntityFrameworkCore;

namespace BackSolutions.Api.Extensions;

/// <summary>
/// Registro de dependencias de la capa de aplicación.
///
/// Vive en una extensión aparte para que Program.cs quede reducido aComposition Root:
/// se lee de un vistazo qué depende el contenedor y dónde se cambia cada cosa.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        // Contexto y transacciones. Scoped, que es el ciclo de vida de una petición.
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Falta 'ConnectionStrings:DefaultConnection'. Configurala en appsettings.Development.json.");
        }

        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

        // La transacción la abre la costura, no cada servicio. Scoped porque se apoya
        // en el AppDbContext de la petición.
        services.AddScoped<ITransactionRunner, EfTransactionRunner>();

        // ── Contexto de la petición ──
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddSingleton<IClock, SystemClock>();

        // ── Seguridad ──
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        // ── Contenido público ──
        services.AddScoped<ISiteSettingsService, SiteSettingsService>();
        services.AddScoped<IPageService, PageService>();
        services.AddScoped<IServiceItemService, ServiceItemService>();
        services.AddScoped<IPortfolioService, PortfolioService>();
        services.AddScoped<IBlogService, BlogService>();

        // ── Comercial ──
        services.AddScoped<ILeadService, LeadService>();
        services.AddScoped<IProposalService, ProposalService>();
        services.AddScoped<IProposalPdfGenerator, QuestPdfProposalGenerator>();

        // ── Chat ──
        // Scoped a propósito: IChatService publica eventos (MessageAdded, EscalationChanged)
        // que el Hub consume. Con Singleton, los handlers de una petición quedarían
        // colgados en una instancia compartida entre usuarios.
        services.AddScoped<IChatService, ChatService>();

        // ── Panel ──
        services.AddScoped<IDashboardService, DashboardService>();

        // Arranque del sistema vacío. Scoped porque usa el DbContext, pero se
        // resuelve una sola vez desde Program al inicio.
        services.AddScoped<OwnerBootstrapper>();

        return services;
    }

    /// <summary>El manejo de errores global va primero del todo en el pipeline.</summary>
    public static IApplicationBuilder UseApplicationErrorHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionMiddleware>();

}
