using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace BackSolutions.Data;

/// <summary>
/// Permite que <c>dotnet ef</c> construya el contexto sin arrancar la API.
/// Es importante que viva acá y no solo en Program.cs: si dependiera del host,
/// los comandos de migración se romperían con cualquier validación de arranque.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var apiProjectDirectory = ResolveApiProjectDirectory();

        var configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(apiProjectDirectory)
            // Orden importante: el archivo base primero y el de entorno encima,
            // que es lo que hace ASP.NET Core. Al revés, appsettings.json pisaría
            // los valores de appsettings.Development.json.
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables();

        var configuration = configurationBuilder.Build();
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var readFiles = string.Join(", ",
                configurationBuilder.Sources.OfType<Microsoft.Extensions.Configuration.FileConfigurationSource>()
                    .Select(source => Path.GetFileName(source.Path)));

            throw new InvalidOperationException(
                $"No se encontró 'ConnectionStrings:DefaultConnection'. "
                + $"Se buscó en: {apiProjectDirectory}. "
                + $"Archivos leídos: {readFiles}. "
                + "Verificá que appsettings.Development.json exista en esa carpeta y tenga la connection string.");
        }

        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options);
    }

    /// <summary>
    /// Sube desde el directorio actual hasta la raíz de la solución y devuelve la
    /// carpeta del proyecto Api. Así el comando funciona sin importar desde dónde se ejecute.
    /// </summary>
    private static string ResolveApiProjectDirectory()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (current is not null)
        {
            if (current.EnumerateFiles("BackSolutions.sln").Any())
                return Path.Combine(current.FullName, "src", "BackSolutions.Api");

            current = current.Parent;
        }

        throw new InvalidOperationException(
            "No se encontró BackSolutions.sln subiendo desde el directorio actual. "
            + "Ejecutá los comandos dotnet ef desde la raíz de la solución.");
    }
}
