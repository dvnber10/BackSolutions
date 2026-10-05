using System.Net.Http.Json;
using System.Text.Json;
using BackSolutions.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BackSolutions.Tests.Infrastructure;

/// <summary>
/// Base de los tests de integración: una transacción por test que se revierte sola.
///
/// Derivar de acá garantiza que el test corre contra la API real (mismos middlewares,
/// mismo JWT, mismo binding, mismo SQL) y que no deja rastro en la base.
/// </summary>
[Collection(IntegrationCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private BackSolutionsApiFactory? _factory;
    private HttpClient? _client;

    protected BackSolutionsApiFactory Factory => _factory!;

    protected HttpClient Client => _client!;

    /// <summary>Cliente con helpers que devuelven status + cuerpo + headers.</summary>
    protected ApiClient Api = null!;

    public virtual async Task InitializeAsync()
    {
        // Barrera de seguridad: sin este flag no se toca la base. Existe porque los
        // tests escriben de verdad en la base compartida, aunque sea de forma transitoria.
        //
        // Va por variable de entorno y no como switch de linea de comandos porque
        // `dotnet test -- --algo` no llega a AppContext.TryGetSwitch: el runner se come
        // lo que va despues del --. La variable si se propaga al proceso de test.
        if (Environment.GetEnvironmentVariable(BackSolutionsApiFactory.AllowSharedDatabaseVariable) != "1")
        {
            throw new InvalidOperationException(
                "Los tests de integracion escriben en la base compartida y necesitan el flag "
                + $"para correr. Ejecuta: {BackSolutionsApiFactory.AllowSharedDatabaseVariable}=1 dotnet test");
        }

        _factory = new BackSolutionsApiFactory();

        await _factory.MigrateAsync();

        await _factory.BeginTestTransactionAsync();

        _client = _factory.CreateClient();
        Api = new ApiClient(_client);
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.EndTestTransactionAsync();
            await _factory.DisposeAsync();
        }

        _client?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Un scope del contenedor para tocar los servicios directamente.</summary>
    protected async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();

        return await action(scope.ServiceProvider);
    }

    /// <summary>
    /// Ejecuta un caso contra los servicios reales dentro del scope de la aplicación, que
    /// es la misma instancia que usan los controllers. Sirve para probar reglas de negocio
    /// sin pasar por HTTP cuando el HTTP no es lo que se está probando.
    /// </summary>
    protected Task<T> ScopedAsync<T>(Func<IServiceProvider, Task<T>> action) => WithScopeAsync(action);

    /// <summary>Lee un servicio del contenedor de la aplicación.</summary>
    protected T GetService<T>()
        where T : notnull
        => Factory.Services.GetRequiredService<T>();
}

/// <summary>
/// Colección única: xUnit crea una instancia de la clase por test, pero todos los tests
/// comparten la misma conexión y la misma transacción ambiental. Sin esta colección
/// explícita, cada test tendría su propia factory y su propia conexión contra la misma
/// base, y las transacciones se pisarían.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<BackSolutionsApiFactory>
{
    public const string Name = "integracion";
}

/// <summary>
/// Cliente HTTP con helpers: devuelve status, cuerpo y headers juntos, que es lo que
/// casi siempre hay que verificar en esta API (muchos asserts son sobre 401/403/404 y
/// sobre cabeceras de seguridad).
/// </summary>
public sealed record ApiResponse(int Status, string Body, HttpResponseHeadersWrapper Headers)
{
    public bool IsSuccess => Status is >= 200 and < 300;

    /// <summary>El cuerpo deserializado, o null si no es JSON válido.</summary>
    public JsonElement? Json
    {
        get
        {
            try
            {
                using var document = JsonDocument.Parse(Body);

                return document.RootElement.Clone();
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    /// <summary>Valor de una propiedad del cuerpo JSON, o null si no está.</summary>
    public string? Str(string property)
        => Json is { } root && root.TryGetProperty(property, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString()
            : null;
}

public sealed record HttpResponseHeadersWrapper(IReadOnlyDictionary<string, string> Values)
{
    public string? Get(string name)
        => Values.TryGetValue(name.ToLowerInvariant(), out var value) ? value : null;

    public bool Has(string name) => Get(name) is not null;
}

/// <summary>
/// Envoltura fina sobre HttpClient. Existe como clase y no como extensiones porque
/// HttpClient ya trae GetAsync/PostAsync/PutAsync/DeleteAsync como metodos de instancia,
/// y el metodo de instancia gana siempre: con extensiones los tests compilaban contra el
/// HttpResponseMessage crudo en vez de contra ApiResponse, que es el error que uno quiere.
/// </summary>
public sealed class ApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<ApiResponse> Get(string path, string? token = null)
        => SendAsync(HttpMethod.Get, path, token: token);

    public Task<ApiResponse> Post(string path, object? body = null, string? token = null)
        => SendAsync(HttpMethod.Post, path, body, token);

    public Task<ApiResponse> Put(string path, object? body = null, string? token = null)
        => SendAsync(HttpMethod.Put, path, body, token);

    public Task<ApiResponse> Delete(string path, string? token = null)
        => SendAsync(HttpMethod.Delete, path, token: token);

    public async Task<ApiResponse> SendAsync(
        HttpMethod method,
        string path,
        object? body = null,
        string? token = null)
    {
        using var request = new HttpRequestMessage(method, path);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        if (token is not null)
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await http.SendAsync(request);

        var text = await response.Content.ReadAsStringAsync();

        var headers = response.Headers
            .Concat(response.Content.Headers)
            .ToDictionary(
                header => header.Key.ToLowerInvariant(),
                header => string.Join(", ", header.Value),
                StringComparer.OrdinalIgnoreCase);

        return new ApiResponse((int)response.StatusCode, text, new HttpResponseHeadersWrapper(headers));
    }

    /// <summary>Login y devuelve el access token.</summary>
    public async Task<string> LoginAsync(string email, string password)
    {
        var response = await Post("/api/auth/login", new { email, password });

        if (!response.IsSuccess)
        {
            throw new InvalidOperationException($"El login de {email} devolvio {response.Status}: {response.Body}");
        }

        using var document = JsonDocument.Parse(response.Body);

        return document.RootElement.GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("El login no devolvio accessToken.");
    }
}
