using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BackSolutions.Api.Extensions;
using BackSolutions.Api.Hubs;
using BackSolutions.Api.Middleware;
using BackSolutions.Api.Security;
using BackSolutions.Api.Services;
using BackSolutions.Core.Options;
using BackSolutions.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────
// 1. OPTIONS TIPADAS (fail-fast: si falta algo, no arranca)
// ─────────────────────────────────────────────────────────────
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(o => o.IsValid(out _), "La sección 'Jwt' de appsettings.json está incompleta.")
    .ValidateOnStart();

builder.Services.AddOptions<SmtpOptions>()
    .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName))
    .Validate(o => o.IsValid(out _), "La sección 'Smtp' de appsettings.json está incompleta.")
    .ValidateOnStart();

// El bootstrap no valida: si faltan datos solo se omite la creación del Owner
// inicial y el arranque continúa, que es lo que se necesita para poder entrar
// a configurar el sistema.
builder.Services.AddOptions<BootstrapOptions>()
    .Bind(builder.Configuration.GetSection(BootstrapOptions.SectionName));

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var signingKey = jwtSection[nameof(JwtOptions.SigningKey)] ?? string.Empty;

if (string.IsNullOrWhiteSpace(signingKey))
{
    throw new InvalidOperationException(
        "Falta 'Jwt:SigningKey'. Configuralo en appsettings.Development.json.");
}

// QuestPDF exige declarar el nivel de licencia antes de generar el primer documento.
// La propiedad <QuestPDFLicense> del csproj no alcanza: hay que fijar el ajuste en
// runtime, y si queda sin setear la generación falla en el primer PDF.
var questPdfLicense = builder.Configuration["QuestPdf:License"] ?? nameof(QuestPDF.Infrastructure.LicenseType.Community);

if (!Enum.TryParse<QuestPDF.Infrastructure.LicenseType>(questPdfLicense, ignoreCase: true, out var parsedLicense))
{
    throw new InvalidOperationException(
        $"'QuestPdf:License' = '{questPdfLicense}' no es un nivel válido (Community, Professional, Enterprise o Evaluation).");
}

QuestPDF.Settings.License = parsedLicense;

// ─────────────────────────────────────────────────────────────
// 2. BASE DE DATOS Y SERVICIOS DE APLICACIÓN
// ─────────────────────────────────────────────────────────────
// AddDbContext<AppDbContext> vive dentro de AddApplicationServices, junto al resto
// de los registros del contenedor.
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddApplicationRateLimiting(builder.Configuration);

// ─────────────────────────────────────────────────────────────
// 3. SIGNALR — chat en tiempo real
// ─────────────────────────────────────────────────────────────
// El bus es singleton a propósito: es el puente entre el servicio de chat, que es scoped,
// y el Hub. Sin un punto de encuentro único, las notificaciones nunca llegarían.
builder.Services.AddSignalR();
builder.Services.AddSingleton<ChatEventBus>();
builder.Services.AddHostedService<ChatHubBroadcaster>();

// ─────────────────────────────────────────────────────────────
// 3. CONTROLADORES + JSON EN CAMELCASE (contrato único con React y Kotlin)
// ─────────────────────────────────────────────────────────────
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });

builder.Services.AddEndpointsApiExplorer();

// ─────────────────────────────────────────────────────────────
// 4. SWAGGER CON AUTENTICACIÓN BEARER
// ─────────────────────────────────────────────────────────────
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BackSolutions API",
        Version = "v1",
        Description = "API del sistema BackSolutions: contenido público, autenticación, administración, leads y chat."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Pega el access token JWT. Formato: eyJhbGciOiJIUzI1NiIs...",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ─────────────────────────────────────────────────────────────
// 5. CORS — lista blanca explícita (nunca AllowAnyOrigin con credenciales)
// ─────────────────────────────────────────────────────────────
const string CorsPolicy = "CorsPolicy";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// ─────────────────────────────────────────────────────────────
// 6. AUTENTICACIÓN JWT
// ─────────────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection[nameof(JwtOptions.Issuer)],
            ValidAudience = jwtSection[nameof(JwtOptions.Audience)],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // Sin esto, ASP.NET responde 401 con el cuerpo vacío y el cliente móvil no puede
        // distinguir "sesión expirada" de un fallo cualquiera: lo reportaba como error
        // inesperado. Ahora siempre sale ProblemDetails, igual que el resto de los errores.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();

                if (context.Response.HasStarted)
                {
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/problem+json";

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "No autenticado",
                    Detail = "Tu sesión expiró o el token no es válido. Volvé a iniciar sesión.",
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                    Instance = context.HttpContext.Request.Path
                };
                problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                problem.Extensions["errorCode"] = "session_expired";

                await context.Response.WriteAsJsonAsync(
                    problem,
                    options: null,
                    contentType: "application/problem+json");
            },
            OnAuthenticationFailed = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options => AuthorizationPolicies.Configure(options));

// ─────────────────────────────────────────────────────────────
// 7. HEALTH CHECKS
// ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

var app = builder.Build();

// ─────────────────────────────────────────────────────────────
// 8. PIPELINE
// ─────────────────────────────────────────────────────────────
// El manejo de errores va primero para que capture también los fallos de Swagger,
// CORS y los middlewares que quedan después.
app.UseApplicationErrorHandling();

// Cabeceras de seguridad antes de todo lo que pueda responder.
app.UseSecurityHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "BackSolutions API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseCors(CorsPolicy);

// El rate limiter va después de CORS para que un 429 salga como respuesta CORS válida, y
// antes de la autenticación para no gastar CPU de verificación de token en peticiones que
// ya fueron rechazadas.
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Chat en tiempo real. El Hub del equipo exige JWT por la política TeamMember; el del
// cliente se autentica con el token de su conversación, no con el de sesión.
app.MapHub<ChatHub>("/hubs/chat/team");
app.MapHub<ConversationHub>("/hubs/chat/conversation");

// Liveness: el proceso responde. No consulta la base de datos.
app.MapHealthChecks("/health").DisableRateLimiting();

// Readiness: incluye el chequeo de la base de datos.
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).DisableRateLimiting();

// ─────────────────────────────────────────────────────────────
// 9. BOOTSTRAP DEL PRIMER OWNER
// ─────────────────────────────────────────────────────────────
await using (var scope = app.Services.CreateAsyncScope())
{
    var bootstrapper = scope.ServiceProvider.GetRequiredService<OwnerBootstrapper>();
    var result = await bootstrapper.RunAsync();

    if (result.Created)
    {
        app.Logger.LogWarning("Bootstrap: {Message}", result.Message);

        if (result.GeneratedPassword is not null)
        {
            app.Logger.LogWarning(
                "Anotá esta contraseña ahora, no se vuelve a mostrar: {Email}",
                result.Email);
        }
    }
    else if (!string.IsNullOrWhiteSpace(result.Message))
    {
        app.Logger.LogInformation("Bootstrap: {Message}", result.Message);
    }
}

app.Run();

public partial class Program;
