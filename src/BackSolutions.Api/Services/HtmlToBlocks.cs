using System.Text;
using System.Text.RegularExpressions;

namespace BackSolutions.Api.Services;

internal enum HtmlBlockKind
{
    Heading,
    Paragraph,
    Bold,
    ListItem
}

/// <summary>
/// Convierte el HTML que guarda el equipo en las propuestas en bloques planos que
/// QuestPDF pueda dibujar.
///
/// QuestPDF no interpreta HTML: hay que extraer el texto. No se busca un parser completo
/// a propósito, sino lo que una propuesta comercial realmente usa (títulos, párrafos,
/// listas, negritas). Todo lo demás se descarta, lo que además sirve de sanitización:
/// un &lt;script&gt; en la propuesta no llega a ejecutarse ni a ensuciar el PDF.
/// </summary>
internal static partial class HtmlToBlocks
{
    private const int MaxBlocks = 200;

    public static IReadOnlyList<HtmlBlock> Parse(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        // Se descartan por completo las zonas que no son contenido: script, style y comentarios.
        var cleaned = ScriptStylePattern().Replace(html, " ");
        cleaned = CommentPattern().Replace(cleaned, " ");

        var blocks = new List<HtmlBlock>();

        foreach (Match match in BlockPattern().Matches(cleaned))
        {
            if (blocks.Count >= MaxBlocks)
            {
                break;
            }

            var kind = match.Groups["tag"].Value.ToLowerInvariant() switch
            {
                "h1" or "h2" or "h3" or "h4" or "h5" or "h6" => HtmlBlockKind.Heading,
                "li" => HtmlBlockKind.ListItem,
                "strong" or "b" => HtmlBlockKind.Bold,
                _ => HtmlBlockKind.Paragraph
            };

            var text = DecodeEntities(StripTags(match.Groups["content"].Value));

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            blocks.Add(new HtmlBlock(text.Trim(), kind));
        }

        // Si el patrón por bloques no encontró nada (HTML sin <p> ni <div>, por ejemplo),
        // se cae al texto plano para no devolver un PDF vacío.
        if (blocks.Count == 0)
        {
            var fallback = DecodeEntities(StripTags(cleaned));

            if (!string.IsNullOrWhiteSpace(fallback))
            {
                blocks.Add(new HtmlBlock(fallback.Trim(), HtmlBlockKind.Paragraph));
            }
        }

        return blocks;
    }

    private static string StripTags(string html) => TagPattern().Replace(html, string.Empty);

    /// <summary>
    /// Desescapa las entidades HTML más comunes. Sin esto, "&amp;" y "&nbsp;" aparecerían
    /// literalmente en el PDF del cliente.
    /// </summary>
    private static string DecodeEntities(string text)
    {
        if (!text.Contains('&'))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '&')
            {
                builder.Append(text[index]);
                continue;
            }

            var semicolon = text.IndexOf(';', index);

            if (semicolon == -1 || semicolon - index > 10)
            {
                builder.Append(text[index]);
                continue;
            }

            var entity = text[(index + 1)..semicolon];
            var replacement = entity switch
            {
                "amp" => "&",
                "lt" => "<",
                "gt" => ">",
                "quot" => "\"",
                "apos" => "'",
                "nbsp" => " ",
                "hellip" => "…",
                "mdash" => "—",
                "ndash" => "–",
                "laquo" => "«",
                "raquo" => "»",
                _ => null
            };

            if (replacement is null)
            {
                builder.Append(text[index]);
                continue;
            }

            builder.Append(replacement);
            index = semicolon;
        }

        return builder.ToString();
    }

    [GeneratedRegex("<(script|style)[^>]*>.*?</\\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled)]
    private static partial Regex ScriptStylePattern();

    [GeneratedRegex("<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled)]
    private static partial Regex CommentPattern();

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex TagPattern();

    /// <summary>Un bloque por elemento de bloque; strong y b se tratan aparte porque son inline.</summary>
    [GeneratedRegex(
        "<(?<tag>h[1-6]|p|li|div|ul|ol|blockquote|strong|b|br)[^>]*>(?<content>.*?)</\\k<tag>>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled)]
    private static partial Regex BlockPattern();
}

internal sealed record HtmlBlock(string Text, HtmlBlockKind Kind);
