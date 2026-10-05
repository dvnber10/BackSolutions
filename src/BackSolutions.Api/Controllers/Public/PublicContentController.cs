using BackSolutions.Core.Common;
using BackSolutions.Core.Dtos.Content;
using BackSolutions.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackSolutions.Api.Controllers.Public;

/// <summary>
/// Contenido público del sitio: settings, servicios, páginas, portfolio y blog.
///
/// Es lo único que consume el sitio público sin sesión, así que va cacheado. Las
/// respuestas se marcan con <c>public, max-age=60</c> y un <c>ETag</c> derivado del
/// contenido: el navegador y el CDN revalidan con un 304 en vez de bajar el JSON entero.
///
/// Escribir contenido invalida la caché. Por eso la escritura no va acá sino en los
/// controllers de admin: si estuviera en el mismo lugar, un endpoint anónimo podría
/// terminar invalidando la caché de todo el sitio.
/// </summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class PublicContentController : ControllerBase
{
    /// <summary>
    /// Un minuto es un compromiso: si el Owner publica un cambio y no se ve al instante,
    /// parece que el sistema falló. Con el ETag, un cliente que ya tiene la versión
    /// anterior tampoco baja el cuerpo completo.
    /// </summary>
    private const int CacheSeconds = 60;

    private readonly ISiteSettingsService _settings;
    private readonly IPageService _pages;
    private readonly IServiceItemService _services;
    private readonly IPortfolioService _portfolio;
    private readonly IBlogService _blog;

    public PublicContentController(
        ISiteSettingsService settings,
        IPageService pages,
        IServiceItemService services,
        IPortfolioService portfolio,
        IBlogService blog)
    {
        _settings = settings;
        _pages = pages;
        _services = services;
        _portfolio = portfolio;
        _blog = blog;
    }

    /// <summary>Configuración global: datos de la empresa, contacto, redes y pie.</summary>
    [HttpGet("settings")]
    [ProducesResponseType<SiteSettingsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SiteSettingsDto>> Settings(CancellationToken cancellationToken) =>
        await Cached("settings", () => _settings.GetAsync(cancellationToken));

    /// <summary>Servicios publicados, ordenados.</summary>
    [HttpGet("services")]
    [ProducesResponseType<IReadOnlyList<PublicServiceItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PublicServiceItemDto>>> Services(CancellationToken cancellationToken) =>
        await Cached("services", () => _services.ListPublicAsync(cancellationToken));

    /// <summary>Ficha de un servicio por slug.</summary>
    [HttpGet("services/{slug}")]
    [ProducesResponseType<PublicServiceItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicServiceItemDto>> ServiceBySlug(string slug, CancellationToken cancellationToken)
    {
        var service = await _services.GetPublicBySlugAsync(slug, cancellationToken);
        return service is null ? NotFound() : Ok(service);
    }

    /// <summary>Menú de navegación. Solo páginas publicadas y marcadas para el menú.</summary>
    [HttpGet("pages/navigation")]
    [ProducesResponseType<IReadOnlyList<PageNavItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PageNavItemDto>>> Navigation(CancellationToken cancellationToken) =>
        await Cached("navigation", () => _pages.GetNavigationAsync(cancellationToken));

    /// <summary>
    /// Página publicada por slug, con sus secciones visibles ordenadas.
    /// Devuelve 404 si la página no existe o no está publicada: el sitio público no
    /// debe poder distinguir entre un borrador y una página inexistente.
    /// </summary>
    [HttpGet("pages/{slug}")]
    [ProducesResponseType<PageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PageDto>> PageBySlug(string slug, CancellationToken cancellationToken)
    {
        var page = await _pages.GetPublicBySlugAsync(slug, cancellationToken);
        return page is null ? NotFound() : Ok(page);
    }

    /// <summary>Proyectos publicados. <c>onlyFeatured</c> devuelve solo los del hero.</summary>
    [HttpGet("portfolio")]
    [ProducesResponseType<IReadOnlyList<PublicPortfolioProjectDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PublicPortfolioProjectDto>>> Portfolio(
        [FromQuery] bool onlyFeatured = false,
        CancellationToken cancellationToken = default) =>
        await Cached($"portfolio:{onlyFeatured}", () => _portfolio.ListPublicAsync(onlyFeatured, cancellationToken));

    [HttpGet("portfolio/{slug}")]
    [ProducesResponseType<PublicPortfolioProjectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicPortfolioProjectDto>> PortfolioBySlug(string slug, CancellationToken cancellationToken)
    {
        var project = await _portfolio.GetPublicBySlugAsync(slug, cancellationToken);
        return project is null ? NotFound() : Ok(project);
    }

    /// <summary>Listado paginado del blog, con filtro opcional por etiqueta.</summary>
    [HttpGet("blog/posts")]
    [ProducesResponseType<PagedResult<BlogPostSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<BlogPostSummaryDto>>> BlogPosts(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? tag,
        CancellationToken cancellationToken = default) =>
        await Cached($"blog:{page}:{pageSize}:{tag}", () => _blog.ListPublicAsync(page, pageSize, tag, cancellationToken));

    /// <summary>
    /// Artículo publicado por slug. Suma una vista en el mismo paso: así el contador
    /// refleja aperturas reales y no solo hits de bots que nunca leen.
    /// </summary>
    [HttpGet("blog/posts/{slug}")]
    [ProducesResponseType<BlogPostDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BlogPostDto>> BlogPost(string slug, CancellationToken cancellationToken)
    {
        var post = await _blog.GetPublicBySlugAsync(slug, cancellationToken);

        if (post is null)
        {
            return NotFound();
        }

        await _blog.RegisterViewAsync(slug, cancellationToken);

        return Ok(post);
    }

    /// <summary>
    /// Etiquetas con conteo de artículos, para la nube de tags del blog.
    /// </summary>
    [HttpGet("blog/tags")]
    [ProducesResponseType<IReadOnlyList<TagDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TagDto>>> BlogTags(CancellationToken cancellationToken) =>
        await Cached("tags", () => _blog.ListTagsAsync(cancellationToken));

    /// <summary>
    /// Responde 200 con la cabecera <c>ETag</c> para que el cliente pueda revalidar.
    /// El ETag se calcula sobre los bytes ya serializados, así que un cambio real de
    /// contenido produce siempre un ETag distinto.
    /// </summary>
    private async Task<ActionResult<T>> Cached<T>(string key, Func<Task<T>> factory)
    {
        var payload = await factory();

        var json = System.Text.Json.JsonSerializer.Serialize(payload, JsonOptions);

        var etag = $"\"{Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)))[..16]}\"";

        if (string.Equals(Request.Headers.IfNoneMatch, etag, StringComparison.Ordinal))
        {
            // 304: el cliente ya tiene esta versión, no hay cuerpo que mandar.
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = $"public, max-age={CacheSeconds}";

        return new JsonResult(payload, JsonOptions);
    }

    /// <summary>Mismas opciones de serialización que registra <c>Program.cs</c>.</summary>
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };
}
