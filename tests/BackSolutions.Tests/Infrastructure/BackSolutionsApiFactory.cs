using System.Data;
using System.Data.Common;
using System.Reflection;
using BackSolutions.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace BackSolutions.Tests.Infrastructure;

/// <summary>
/// Levanta la API real contra la base de desarrollo y ejecuta cada test dentro de una
/// transacción que se revierte.
///
/// La cadena de conexión sale de <c>TestDatabase:ConnectionString</c> en user-secrets, o
/// de la variable de entorno BACKSOLUTIONS_TEST_DB. Nunca está en el repo: el
/// .gitignore ignora appsettings.Development.json y los secrets viven fuera del repo.
///
/// Ojo con esto: los tests escriben de verdad en la base que tenemos apuntada,
/// aunque sea de forma transitoria. Por eso el runner no arranca sin
/// BACKSOLUTIONS_ALLOW_SHARED_DB=1.
/// </summary>
public sealed class BackSolutionsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>
    /// Interruptor de seguridad. Sin esta variable el harness se niega a arrancar, para
    /// que un dotnet test sin querer no escriba en la base de desarrollo.
    /// </summary>
    public const string AllowSharedDatabaseVariable = "BACKSOLUTIONS_ALLOW_SHARED_DB";

    private DbConnection? _connection;
    private TestTransaction? _interceptor;

    public static string ResolveConnectionString()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("BACKSOLUTIONS_TEST_DB");

        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        // El secret es del proyecto de tests (tiene su propio UserSecretsId), no del de
        // la API: si se usara el de la API se leeria su cadena de producción por error.
        // Sin argumentos extra: el UserSecretsId está en el csproj, que es la forma
        // canónica y la que sobrevive a que el proyecto se renombre.
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(Assembly.GetExecutingAssembly())
            .Build();

        return configuration["TestDatabase:ConnectionString"]
            ?? throw new InvalidOperationException(
                "Falta la cadena de conexión de tests. Definí TestDatabase:ConnectionString en "
                + "user-secrets del proyecto de tests, o la variable BACKSOLUTIONS_TEST_DB.");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Development" y no "Testing" a proposito: la API toma Jwt:SigningKey y Smtp de
        // sus user-secrets, y CreateBuilder solo los carga cuando el ambiente es
        // Development. Con "Testing" el host aborta al arrancar por falta del signing key.
        // La connection string que se carga tambien se ignora: el DbContext se
        // re-registra unas lineas mas abajo con la de tests.
        builder.UseEnvironment("Development");

        // El middleware de errores responde 500 con un mensaje generico a proposito, asi que
        // cuando un test falla con 500 el detalle real solo existe en el log del servidor.
        // BACKSOLUTIONS_TEST_LOG=1 lo manda a la consola de dotnet test para poder verlo.
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();

            if (Environment.GetEnvironmentVariable("BACKSOLUTIONS_TEST_LOG") == "1")
            {
                logging.AddSimpleConsole().SetMinimumLevel(LogLevel.Debug);
            }
        });

        builder.ConfigureServices(services =>
        {
            // Se saca el registro original de AppDbContext: se vuelve a registrar con la
            // conexión compartida y el interceptor, que es lo único que mantiene la
            // transacción del test viva entre requests.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            var connectionString = ResolveConnectionString();

            services.AddSingleton(sp =>
            {
                _interceptor ??= new TestTransaction();

                return _interceptor;
            });

            services.AddSingleton(sp =>
            {
                _connection ??= new Microsoft.Data.SqlClient.SqlConnection(connectionString);

                if (_connection.State != ConnectionState.Open)
                {
                    _connection.Open();
                }

                return _connection;
            });

            services.AddScoped(sp =>
            {
                var transaction = sp.GetRequiredService<TestTransaction>();
                var connection = sp.GetRequiredService<DbConnection>();

                var options = new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlServer(connection)
                    .EnableSensitiveDataLogging()
                    .Options;

                var context = new AppDbContext(options);

                // Los contextos nacen por request; hay que enlistarlos a mano en la
                // transacción del test.
                transaction.Enlist(context);

                return context;
            });
        });
    }

    /// <summary>
    /// IAsyncLifetime lo pide el fixture de xUnit; la migración se chequea desde la clase
    /// base, que es quien sabe cuándo empieza el test.
    /// </summary>
    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>Abre la transacción del test. La llama la clase base antes de cada test.</summary>
    public async Task BeginTestTransactionAsync()
    {
        var connection = Services.GetRequiredService<DbConnection>();
        var interceptor = Services.GetRequiredService<TestTransaction>();

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await interceptor.BeginAsync(connection);
    }

    /// <summary>Revierte lo que hizo el test.</summary>
    public async Task EndTestTransactionAsync()
    {
        var interceptor = Services.GetRequiredService<TestTransaction>();

        await interceptor.ResetAsync();
    }

    /// <summary>
    /// Crea la base si falta. Contra la base compartida es un no-op: si ya existe no hace
    /// nada, y si no existe el test falla al conectar con un mensaje claro.
    /// </summary>
    public async Task MigrateAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if ((await context.Database.GetPendingMigrationsAsync()).Any())
        {
            // No se migra solo contra la base compartida: aplicar migraciones es un cambio
            // deliberado, no un efecto secundario de correr tests. Si esto falla, el
            // mensaje es el que hay que leer primero.
            throw new InvalidOperationException(
                "La base de tests tiene migraciones pendientes. Aplicalas a mano con "
                + "dotnet ef database update antes de correr los tests.");
        }
    }

    public new async Task DisposeAsync()
    {
        if (_interceptor is not null)
        {
            await _interceptor.ResetAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }

        await base.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}
