namespace BackSolutions.Core.Common;

/// <summary>
/// Envoltura de un resultado paginado. Viaja igual que cualquier otro DTO: el
/// cliente lee <see cref="Items"/> y usa <see cref="TotalCount"/> para el total.
/// </summary>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public static PagedResult<T> Create(IReadOnlyList<T> items, int totalCount, int page, int pageSize) => new()
    {
        Items = items,
        TotalCount = totalCount,
        Page = page,
        PageSize = pageSize
    };

    public static PagedResult<T> Empty(int page, int pageSize) =>
        Create([], 0, page, pageSize);
}

/// <summary>
/// Tope de paginación compartido. Evita que un cliente pida pageSize=100000 y
/// se lleve la tabla entera en memoria.
/// </summary>
public static class Paging
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 100;

    /// <summary>Normaliza page/pageSize recibidos del exterior. Siempre devuelve valores seguros.</summary>
    public static (int Page, int PageSize) Normalize(int? page, int? pageSize)
    {
        var normalizedPage = page is null or < 1 ? 1 : page.Value;
        var requested = pageSize ?? DefaultPageSize;

        var normalizedSize = requested switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => requested
        };

        return (normalizedPage, normalizedSize);
    }
}
