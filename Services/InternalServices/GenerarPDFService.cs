using System.Threading.Tasks;
using System.Text.Json;
using BackSolutions.Services.interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BackSolutions.Services.InternalServices
{
    public class GenerarPDFService : GeneratePDFInterface
    {
        public GenerarPDFService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public Task<byte[]> CrearPdfPropuestaAsync(string nombreCliente, string tipoServicio, JsonElement datosIa)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Grey.Darken3));

                    // Header con la marca BackSolutions
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("BackSolutions").FontSize(22).Bold().FontColor(Colors.Blue.Darken4);
                            c.Item().Text("Análisis de Requerimiento Técnico Inicial").FontSize(10).FontColor(Colors.Grey.Darken1);
                        });
                    });

                    // Construcción de los bloques de información del reporte
                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                    {
                        col.Item().Text($"Análisis de Factibilidad para: {nombreCliente}").FontSize(14).Bold();
                        col.Item().PaddingBottom(10).Text($"Línea de Servicio: {tipoServicio}").FontSize(12).FontColor(Colors.Blue.Darken1);
                        
                        // Nota legal / de responsabilidad obligatoria
                        col.Item().Background(Colors.Grey.Lighten4).Padding(8).Text("⚠️ NOTA INTERNA/EXTERNA: Este documento representa un análisis arquitectónico preliminar automatizado mediante algoritmos de IA. Todo diseño final está condicionado a la revisión técnica de un ingeniero de BackSolutions.").FontSize(9).Italic();

                        col.Item().PaddingTop(15).Text("1. Resumen de Enfoque").FontSize(12).Bold().FontColor(Colors.Blue.Darken3);
                        col.Item().PaddingBottom(10).Text(datosIa.GetProperty("PdfIntroduccion").GetString());

                        col.Item().Text("2. Arquitectura de Sistemas Sugerida").FontSize(12).Bold().FontColor(Colors.Blue.Darken3);
                        col.Item().PaddingBottom(15).Text(datosIa.GetProperty("PdfArquitectura").GetString());

                        // Renderizado de las filas de la tabla de fases creadas por el LLM
                        col.Item().PaddingBottom(5).Text("3. Cronograma Estimado de Ejecución").FontSize(12).Bold().FontColor(Colors.Blue.Darken3);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns => {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(3);
                            });

                            table.Header(h => {
                                h.Cell().Background(Colors.Blue.Darken4).Padding(5).Text("Fase").Bold().FontColor(Colors.White);
                                h.Cell().Background(Colors.Blue.Darken4).Padding(5).Text("Duración").Bold().FontColor(Colors.White);
                                h.Cell().Background(Colors.Blue.Darken4).Padding(5).Text("Entregables Clave").Bold().FontColor(Colors.White);
                            });

                            foreach (var fase in datosIa.GetProperty("PdfFases").EnumerateArray())
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(fase.GetProperty("Fase").GetString());
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(fase.GetProperty("Duracion").GetString());
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(fase.GetProperty("Entregables").GetString());
                            }
                        });

                        col.Item().PaddingTop(15).Text("4. Siguientes Pasos Recomendados").FontSize(12).Bold().FontColor(Colors.Blue.Darken3);
                        col.Item().Text(datosIa.GetProperty("PdfConclusion").GetString());
                    });

                    // Render current page number in the footer
                    page.Footer().AlignRight().Text(t => t.CurrentPageNumber());
                });
            });

            using var stream = new System.IO.MemoryStream();
            document.GeneratePdf(stream);
            return Task.FromResult(stream.ToArray());
        }
    }
}