using System.Globalization;
using System.Text;

namespace BackSolutions.Core.Common;

/// <summary>
/// Generación de slugs para URLs públicas (/blog/mi-articulo, /servicios/diseno-web).
///
/// Las tildes se transliteran ("Diseño" -> "diseno") en vez de eliminarse, para no
/// terminar con slugs como "diso" que se ven mal y colisionan entre sí.
/// </summary>
public static class Slug
{
    private const int MaxLength = 120;

    private static readonly Dictionary<char, string> Transliterations = new()
    {
        ['á'] = "a", ['à'] = "a", ['ä'] = "a", ['â'] = "a", ['ã'] = "a", ['å'] = "a",
        ['é'] = "e", ['è'] = "e", ['ë'] = "e", ['ê'] = "e",
        ['í'] = "i", ['ì'] = "i", ['ï'] = "i", ['î'] = "i",
        ['ó'] = "o", ['ò'] = "o", ['ö'] = "o", ['ô'] = "o", ['õ'] = "o",
        ['ú'] = "u", ['ù'] = "u", ['ü'] = "u", ['û'] = "u",
        ['ñ'] = "n", ['ç'] = "c", ['ß'] = "ss"
    };

    /// <summary>
    /// Normaliza el texto a un slug. Devuelve cadena vacía si no queda nada usable:
    /// el servicio la trata como error de validación, porque un slug vacío rompería la URL.
    /// </summary>
    public static string Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var normalized = input.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var pendingSeparator = false;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (Transliterations.TryGetValue(char.ToLowerInvariant(character), out var replacement))
            {
                AppendCharacter(builder, replacement[0], ref pendingSeparator);
                continue;
            }

            AppendCharacter(builder, char.ToLowerInvariant(character), ref pendingSeparator);
        }

        var slug = builder.ToString().Trim('-');

        return slug.Length > MaxLength ? slug[..MaxLength].Trim('-') : slug;
    }

    private static void AppendCharacter(StringBuilder builder, char character, ref bool pendingSeparator)
    {
        if (char.IsLetterOrDigit(character))
        {
            if (pendingSeparator && builder.Length > 0)
            {
                builder.Append('-');
            }

            builder.Append(character);
            pendingSeparator = false;
        }
        else if (builder.Length > 0)
        {
            pendingSeparator = true;
        }
    }
}
