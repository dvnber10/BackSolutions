using BackSolutions.Core.Dtos.Proposals;
using BackSolutions.Core.Entities.Content;
using BackSolutions.Core.Entities.Leads;
using BackSolutions.Core.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BackSolutions.Api.Services;

/// <summary>
/// Genera el PDF de la propuesta con QuestPDF.
///
/// Criterios que se siguieron:
///
/// - El PDF se arma en memoria y se devuelve como bytes. No se guarda en disco ni en la
///   base: la propuesta vive en SQL Server y el PDF es una proyección, así que siempre
///   refleja los datos actuales sin depender de regenerar un archivo obsoleto.
///
/// - El contenido llega como HTML (SummaryHtml, NotesHtml). QuestPDF no es un motor de
///   navegador, así que se sanea el marcado y se conserva la estructura semántica
///   (títulos, párrafos, listas y negritas). Cualquier etiqueta o script se descarta:
///   el PDF es una superficie que el equipo controla, pero el HTML lo escribe una app
///   cliente y no conviene confiar en él.
///
/// - La moneda y los importes se formatean con la cultura es-US para que los símbolos
///   y separadores sean los que el cliente espera en una propuesta comercial.
///
/// - Si el PDF falla al generarse, el error real se loguea pero el cliente recibe un
///   mensaje genérico: las excepciones de QuestPDF incluyen fragmentos del documento.
/// </summary>
public sealed class QuestPdfProposalGenerator : IProposalPdfGenerator
{
    private const string DefaultCurrencySymbol = "$";

    /// <summary>Colores de marca. Sobrescribibles desde SiteSettings si algún día hace falta.</summary>
    private static readonly string Ink = Colors.Grey.Darken3;

    private static readonly string Accent = Colors.Indigo.Medium;

    public Task<ProposalPdf> GenerateAsync(
        Proposal proposal,
        SiteSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var document = BuildDocument(proposal, settings);
            var bytes = document.GeneratePdf();

            var fileName = $"propuesta-{proposal.Number}.pdf";

            return Task.FromResult(new ProposalPdf(bytes, fileName, ProposalPdf.PdfContentType));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"No se pudo generar el PDF de la propuesta {proposal.Number}.", exception);
        }
    }

    private static IDocument BuildDocument(Proposal proposal, SiteSettings settings)
    {
        var currency = string.IsNullOrWhiteSpace(proposal.Currency) ? "USD" : proposal.Currency;
        var company = string.IsNullOrWhiteSpace(settings.CompanyName) ? "BackSolutions" : settings.CompanyName;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(48);
                page.DefaultTextStyle(style => style.FontFamily("Segoe UI").FontSize(10).LineHeight(1.4f));

                page.Header().Element(ComposeHeader);
                page.Content().Element(c => ComposeBody(c, proposal, settings, currency, company));
                page.Footer().Element(ComposeFooter);

                void ComposeHeader(IContainer header)
                {
                    // IContainer es un contenedor de un solo hijo: encadenar Row,
                    // LineHorizontal y PaddingBottom sobre el mismo header tira
                    // DocumentComposeException. Todo tiene que ir dentro de una columna,
                    // y ColumnDescriptor no expone Padding: el aire de abajo va en el
                    // último ítem de la columna.
                    header.Column(column =>
                    {
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Column(left =>
                            {
                                if (!string.IsNullOrWhiteSpace(settings.LogoUrl))
                                {
                                    left.Item().Height(38).Image(settings.LogoUrl);
                                }
                                else
                                {
                                    left.Item().Text(company).FontSize(18).SemiBold().FontColor(Accent);
                                }

                                if (!string.IsNullOrWhiteSpace(settings.TaxId))
                                {
                                    left.Item().PaddingTop(2).Text($"CUIT / Tax ID: {settings.TaxId}").FontSize(8).FontColor(Colors.Grey.Medium);
                                }
                            });

                            row.ConstantItem(170).Column(right =>
                            {
                                right.Item().AlignRight().Text("PROPUESTA").FontSize(9).LetterSpacing(0.2f).FontColor(Colors.Grey.Medium);
                                right.Item().AlignRight().Text(proposal.Number).FontSize(16).SemiBold();
                                right.Item().AlignRight().PaddingTop(2)
                                    .Text($"Emitida el {proposal.CreatedAtUtc:dd/MM/yyyy}").FontSize(8).FontColor(Colors.Grey.Medium);
                            });
                        });

                        column.Item().PaddingTop(10).PaddingBottom(16)
                            .LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });
                }

                void ComposeBody(IContainer body, Proposal target, SiteSettings site, string currencyCode, string companyName)
                {
                    body.Column(column =>
                    {
                        column.Spacing(18);

                        column.Item().Text(target.Title).FontSize(20).SemiBold().FontColor(Ink);

                        // Datos del cliente y de la empresa, en dos columnas.
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Column(left =>
                            {
                                left.Item().Text("CLIENTE").SemiBold().FontSize(8).FontColor(Colors.Grey.Medium);
                                left.Item().PaddingTop(4).Text(target.Lead.Name).SemiBold();
                                left.Item().Text(target.Lead.Email).FontSize(9).FontColor(Colors.Grey.Darken1);

                                if (!string.IsNullOrWhiteSpace(target.Lead.Phone))
                                {
                                    left.Item().Text(target.Lead.Phone).FontSize(9);
                                }
                            });

                            row.RelativeItem().Column(right =>
                            {
                                right.Item().Text("EMISOR").SemiBold().FontSize(8).FontColor(Colors.Grey.Medium);
                                right.Item().PaddingTop(4).Text(companyName);
                                right.Item().Text(site.ContactEmail).FontSize(9).FontColor(Colors.Grey.Darken1);

                                if (!string.IsNullOrWhiteSpace(site.Phone))
                                {
                                    right.Item().Text(site.Phone).FontSize(9);
                                }

                                if (!string.IsNullOrWhiteSpace(site.Address))
                                {
                                    right.Item().Text(site.Address).FontSize(9);
                                }
                            });
                        });

                        column.Item().Element(c => ComposeHtmlBlock(c, target.SummaryHtml));

                        column.Item().Text("Detalle").FontSize(13).SemiBold().FontColor(Ink);

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(30);    // #
                                columns.RelativeColumn();       // descripción
                                columns.ConstantColumn(70);    // cantidad
                                columns.ConstantColumn(95);    // precio
                                columns.ConstantColumn(55);    // desc. %
                                columns.ConstantColumn(105);   // importe
                            });

                            table.Header(headerRow =>
                            {
                                headerRow.Cell().Element(ComposeHeaderCell).Text("Ítem").SemiBold().FontSize(8).FontColor(Colors.Grey.Medium);
                                headerRow.Cell().Element(ComposeHeaderCell).Text("Descripción").SemiBold().FontSize(8).FontColor(Colors.Grey.Medium);
                                headerRow.Cell().Element(ComposeHeaderCell).AlignRight().Text("Cant.").SemiBold().FontSize(8).FontColor(Colors.Grey.Medium);
                                headerRow.Cell().Element(ComposeHeaderCell).AlignRight().Text("Precio").SemiBold().FontSize(8).FontColor(Colors.Grey.Medium);
                                headerRow.Cell().Element(ComposeHeaderCell).AlignRight().Text("Desc.").SemiBold().FontSize(8).FontColor(Colors.Grey.Medium);
                                headerRow.Cell().Element(ComposeHeaderCell).AlignRight().Text("Importe").SemiBold().FontSize(8).FontColor(Colors.Grey.Medium);
                            });

                            foreach (var item in target.Items.OrderBy(i => i.SortOrder))
                            {
                                var lineTotal = Math.Round(
                                    item.Quantity * item.UnitPrice * (1 - (item.DiscountPercent ?? 0m) / 100m), 2);

                                table.Cell().Element(ComposeBodyCell).Text((item.SortOrder + 1).ToString()).FontSize(9);
                                table.Cell().Element(ComposeBodyCell).Text(item.Description).FontSize(9);
                                table.Cell().Element(ComposeBodyCell).AlignRight().Text(item.Quantity.ToString("0.##")).FontSize(9);
                                table.Cell().Element(ComposeBodyCell).AlignRight().Text(Format(item.UnitPrice, currencyCode)).FontSize(9);
                                table.Cell().Element(ComposeBodyCell).AlignRight().Text(item.DiscountPercent is > 0 ? $"{item.DiscountPercent:0.#}%" : "-").FontSize(9);
                                table.Cell().Element(ComposeBodyCell).AlignRight().Text(Format(lineTotal, currencyCode)).FontSize(9).SemiBold();
                            }

                            static IContainer ComposeHeaderCell(IContainer cell) => cell.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(4);

                            static IContainer ComposeBodyCell(IContainer cell) =>
                                cell.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(5);
                        });

                        // Totales alineados a la derecha, en una caja.
                        column.Item().AlignRight().Width(260).PaddingTop(10).Column(totals =>
                        {
                            totals.Item().Element(TotalRow).Text("Subtotal").FontSize(9);
                            totals.Item().Element(TotalRow).Text(Format(target.Subtotal, currencyCode)).FontSize(9).SemiBold();

                            if (target.DiscountAmount > 0)
                            {
                                totals.Item().Element(TotalRow).Text("Descuento").FontSize(9);
                                totals.Item().Element(TotalRow).Text($"-{Format(target.DiscountAmount, currencyCode)}").FontSize(9).SemiBold();
                            }

                            if (target.TaxAmount > 0)
                            {
                                totals.Item().Element(TotalRow).Text("Impuestos").FontSize(9);
                                totals.Item().Element(TotalRow).Text(Format(target.TaxAmount, currencyCode)).FontSize(9).SemiBold();
                            }

                            totals.Item().PaddingTop(4).Element(TotalRowStrong).Text("TOTAL").FontSize(11).SemiBold();
                            totals.Item().PaddingTop(4).Element(TotalRowStrong).Text(Format(target.TotalAmount, currencyCode)).FontSize(12).SemiBold().FontColor(Accent);

                            static IContainer TotalRow(IContainer cell) =>
                                cell.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(4);

                            static IContainer TotalRowStrong(IContainer cell) => cell;
                        });

                        if (target.ValidUntil is { } validUntil)
                        {
                            column.Item().PaddingTop(12).Text($"Vigencia de la propuesta: hasta el {validUntil:dd/MM/yyyy}.").FontSize(9).Italic().FontColor(Colors.Grey.Darken1);
                        }

                        if (!string.IsNullOrWhiteSpace(target.NotesHtml))
                        {
                            column.Item().PaddingTop(10).Text("Notas").FontSize(11).SemiBold();
                            column.Item().Element(c => ComposeHtmlBlock(c, target.NotesHtml));
                        }

                        if (!string.IsNullOrWhiteSpace(site.FooterLegalText))
                        {
                            column.Item().PaddingTop(20).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                            column.Item().PaddingTop(8).Text(site.FooterLegalText).FontSize(8).FontColor(Colors.Grey.Medium);
                        }
                    });

                    void ComposeHtmlBlock(IContainer target, string? html)
                    {
                        var blocks = HtmlToBlocks.Parse(html);

                        if (blocks.Count == 0)
                        {
                            return;
                        }

                        target.Column(inner =>
                        {
                            inner.Spacing(6);

                            foreach (var block in blocks)
                            {
                                var isHeading = block.Kind == HtmlBlockKind.Heading;

                                // QuestPDF no tiene SemiBold(condicional): el peso se aplica
                                // después de fijar tamaño y color.
                                var text = inner.Item()
                                    .Text(isHeading ? block.Text.ToUpperInvariant() : block.Text)
                                    .FontSize(isHeading ? 12 : 10)
                                    .FontColor(isHeading ? Ink : Colors.Grey.Darken2);

                                if (isHeading)
                                {
                                    text.SemiBold();
                                }
                                else if (block.Kind == HtmlBlockKind.Bold)
                                {
                                    text.Bold();
                                }
                            }
                        });
                    }
                }

                void ComposeFooter(IContainer footer)
                {
                    footer.Column(column =>
                    {
                        column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        column.Item().PaddingTop(6).Row(row =>
                        {
                            row.RelativeItem().Text($"{company} · Propuesta {proposal.Number}")
                                .FontSize(8).FontColor(Colors.Grey.Medium);

                            row.RelativeItem().AlignRight().Text(text =>
                            {
                                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Medium));
                                text.Span("Página ");
                                text.CurrentPageNumber();
                                text.Span(" de ");
                                text.TotalPages();
                            });
                        });
                    });
                }
            });
        });
    }

    /// <summary>
    /// Formatea con el símbolo de la moneda. Se usa un mapa corto en vez de la cultura
    /// completa porque el PDF es un documento comercial: un cliente argentino espera
    /// "US$ 1.500,00" y uno de Estados Unidos "$1,500.00", pero cambiar la configuración
    /// regional de toda la app por eso no vale la pena.
    /// </summary>
    private static string Format(decimal amount, string currency) => currency.ToUpperInvariant() switch
    {
        "USD" => amount.ToString("C", new System.Globalization.CultureInfo("en-US")),
        "EUR" => $"€{amount:N2}",
        "ARS" => amount.ToString("C", new System.Globalization.CultureInfo("es-AR")),
        "MXN" => amount.ToString("C", new System.Globalization.CultureInfo("es-MX")),
        "COP" => amount.ToString("C", new System.Globalization.CultureInfo("es-CO")),
        "CLP" => amount.ToString("C", new System.Globalization.CultureInfo("es-CL")),
        "BRL" => amount.ToString("C", new System.Globalization.CultureInfo("pt-BR")),
        _ => $"{DefaultCurrencySymbol}{amount:N2}"
    };
}
