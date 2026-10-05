using BackSolutions.Core.Common;

namespace BackSolutions.Api.Services;

/// <summary>
/// Resolución de slugs únicos para el contenido público.
///
/// Si el cliente no manda slug, o manda algo que no se puede convertir (solo acentos,
/// solo símbolos), se deriva del título. Si el slug ya está en uso se prueba con
/// sufijo -2, -3... para no bloquear al editor con un error por un duplicado que él
/// no provocaró conscientemente.
/// </summary>
internal static class SlugGuard
{
    private const int MaxAttempts = 50;

    /// <param name="requestedSlug">Slug pedido por el editor. Puede venir vacío.</param>
    /// <param name="fallbackSource">Texto del cual derivar el slug si el pedido no sirve.</param>
    /// <param name="fieldName">Campo al que se atribuye el error de validación.</param>
    /// <param name="existsAsync">Indica si un slug ya está tomado.</param>
    public static async Task<string> ResolveAsync(
        string? requestedSlug,
        string fallbackSource,
        string fieldName,
        Func<string, CancellationToken, Task<bool>> existsAsync,
        CancellationToken cancellationToken = default)
    {
        var baseSlug = Slug.Create(requestedSlug);

        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = Slug.Create(fallbackSource);
        }

        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [fieldName] = ["El texto no se puede convertir en un slug válido. Agregá letras o números."]
            });
        }

        var candidate = baseSlug;

        for (var attempt = 2; attempt <= MaxAttempts; attempt++)
        {
            if (!await existsAsync(candidate, cancellationToken))
            {
                return candidate;
            }

            candidate = $"{baseSlug}-{attempt}";
        }

        throw new ConflictException(
            $"No se encontró un slug libre para '{baseSlug}' después de {MaxAttempts} intentos.");
    }
}
