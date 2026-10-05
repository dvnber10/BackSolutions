using System.Text.Json;
using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Content;

namespace BackSolutions.Core.Interfaces;

/// <summary>
/// Lectura y escritura de la configuración global del sitio (singleton, Id = 1).
/// </summary>
public interface ISiteSettingsService
{
    /// <summary>Datos actuales. La web pública los carga en el arranque del layout.</summary>
    Task<SiteSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    Task<SiteSettingsDto> UpdateAsync(UpdateSiteSettingsRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Páginas y sus secciones. Es la unidad con la que se arma el sitio público.</summary>
public interface IPageService
{
    /// <summary>Lista paginada para el panel. Incluye borradores.</summary>
    Task<PagedResult<PageDto>> ListAsync(int? page, int? pageSize, ContentStatusFilter status, CancellationToken cancellationToken = default);

    /// <summary>Métodos del menú público: solo publicadas y marcadas para navegación.</summary>
    Task<IReadOnlyList<PageNavItemDto>> GetNavigationAsync(CancellationToken cancellationToken = default);

    /// <summary>Vista pública por slug, con sus secciones visibles ordenadas. Devuelve null si no está publicada.</summary>
    Task<PageDto?> GetPublicBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<PageDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Crea o actualiza la página. Si el id viene null se crea; si viene informado se actualiza.</summary>
    Task<PageDto> SaveAsync(Guid? id, SavePageRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina la página y sus secciones. Solo lo permite Owner: es contenido publicado
    /// y borrarlo rompe las URLs indexadas.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Servicios que se ofrecen (diseño web, cloud, etc.).</summary>
public interface IServiceItemService
{
    Task<PagedResult<ServiceItemDto>> ListAsync(int? page, int? pageSize, ContentStatusFilter status, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PublicServiceItemDto>> ListPublicAsync(CancellationToken cancellationToken = default);

    Task<PublicServiceItemDto?> GetPublicBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<ServiceItemDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceItemDto> SaveAsync(Guid? id, SaveServiceItemRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Proyectos del portfolio.</summary>
public interface IPortfolioService
{
    Task<PagedResult<PortfolioProjectDto>> ListAsync(int? page, int? pageSize, ContentStatusFilter status, bool? featuredOnly, CancellationToken cancellationToken = default);

    /// <summary>Portfolio público. IncludeFeaturedCount permite pedir solo los destacados para el hero.</summary>
    Task<IReadOnlyList<PublicPortfolioProjectDto>> ListPublicAsync(bool onlyFeatured = false, CancellationToken cancellationToken = default);

    Task<PublicPortfolioProjectDto?> GetPublicBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<PortfolioProjectDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PortfolioProjectDto> SaveAsync(Guid? id, SavePortfolioProjectRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Blog y sus etiquetas.</summary>
public interface IBlogService
{
    Task<PagedResult<BlogPostDto>> ListAsync(int? page, int? pageSize, ContentStatusFilter status, string? search, CancellationToken cancellationToken = default);

    /// <summary>Listado público paginado, con filtro opcional por etiqueta.</summary>
    Task<PagedResult<BlogPostSummaryDto>> ListPublicAsync(int? page, int? pageSize, string? tag, CancellationToken cancellationToken = default);

    Task<BlogPostDto?> GetPublicBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Suma una vista al post. Se llama desde el frontend al abrir el artículo, así que
    /// es idempotente por diseño del endpoint y no lanza si el post no existe.
    /// </summary>
    Task RegisterViewAsync(string slug, CancellationToken cancellationToken = default);

    Task<BlogPostDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<BlogPostDto> SaveAsync(Guid? id, SaveBlogPostRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TagDto>> ListTagsAsync(CancellationToken cancellationToken = default);

    Task<TagDto> SaveTagAsync(Guid? id, SaveTagRequest request, CancellationToken cancellationToken = default);

    Task DeleteTagAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Filtro de estado para los listados del panel. <see cref="All"/> no es un valor del
/// enum <c>ContentStatus</c>: es una opción de consulta, no un estado almacenado.
/// </summary>
public enum ContentStatusFilter
{
    All = -1,
    Draft = 0,
    Published = 1,
    Archived = 2
}

/// <summary>Mapeo entre entidades y DTOs. Centralizado para que los controllers no repitan projections.</summary>
public static class ContentMapper
{
    /// <summary>TechStackJson es texto en la base; la web lo espera como array.</summary>
    public static IReadOnlyList<string> ParseStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            // Contenido malformado no debe tumbar la web pública: se trata como lista vacía.
            return [];
        }
    }

    public static string SerializeStringArray(IReadOnlyList<string>? values) =>
        JsonSerializer.Serialize(values ?? []);

    /// <summary>
    /// ContentJson guarda el payload de cada tipo de sección. Si viene vacío o corrupto
    /// se devuelve un objeto vacío, que es lo que los renderizadores esperan.
    /// </summary>
    public static JsonElement ParseJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return EmptyObject();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return EmptyObject();
        }
    }

    public static string SerializeJsonObject(JsonElement? content) =>
        content is null || content.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? "{}"
            : content.Value.GetRawText();

    private static JsonElement EmptyObject()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }
}
