using System.Text.Json;
using System.Text.Json.Serialization;
using BackSolutions.Core.Dtos.Proposals;

namespace BackSolutions.Tests;

/// <summary>
/// Guarda de contrato entre la app Android y la API.
///
/// El cliente móvil sends los importes como números JSON sin comillas; este test falla si alguien
/// vuelve a serializarlos como texto (lo que <c>System.Text.Json</c> rechaza con 400 al bindear
/// a <c>decimal</c>) o si degrada la respuesta de vuelta. El payload literal fue copiado de la
/// salida real de <c>SaveProposalSerializationTest</c> en la app.
/// </summary>
public sealed class SaveProposalContractTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private const string MobilePayload = """
        {"leadId":"11111111-1111-1111-1111-111111111111","title":"Desarrollo web \" premium \\ 50%",
         "summaryHtml":"<p>Resumen</p>","currency":"ARS","discountAmount":1000.00,"taxAmount":2100.00,
         "validUntil":"2026-11-01T00:00:00Z","items":[
           {"description":"Landing page","quantity":2,"unitPrice":750.00,"discountPercent":10.00,"sortOrder":0},
           {"description":"Segundo item","quantity":0.10,"unitPrice":99999999999.99,"sortOrder":1}]}
        """;

    [Fact]
    public void El_payload_de_la_app_mobile_bindea_sin_perder_precision()
    {
        var request = JsonSerializer.Deserialize<SaveProposalRequest>(MobilePayload, Options);

        Assert.NotNull(request);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), request.LeadId);
        Assert.Equal("Desarrollo web \" premium \\ 50%", request.Title);
        Assert.Equal(1000.00m, request.DiscountAmount);
        Assert.Equal(2100.00m, request.TaxAmount);
        Assert.Equal(DateTimeOffset.Parse("2026-11-01T00:00:00Z"), request.ValidUntil);

        Assert.Equal(2, request.Items.Count);
        Assert.Equal(2m, request.Items[0].Quantity);
        Assert.Equal(750.00m, request.Items[0].UnitPrice);
        Assert.Equal(10.00m, request.Items[0].DiscountPercent);

        // El caso que un Double rompería: 14 dígitos enteros con centavos.
        Assert.Equal(0.10m, request.Items[1].Quantity);
        Assert.Equal(99999999999.99m, request.Items[1].UnitPrice);
    }

    [Fact]
    public void Los_importes_entrecomillados_se_rechazan()
    {
        // Documenta por qué el cliente no puede enviar "750.00": el bind a decimal falla.
        var conTextos = """{"leadId":"11111111-1111-1111-1111-111111111111","title":"T","summaryHtml":"S","discountAmount":"0.00","taxAmount":"0.00","items":[]}""";

        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<SaveProposalRequest>(conTextos, Options));
    }

    [Fact]
    public void Los_importes_ausentes_se_leen_como_cero()
    {
        // La app omite nulos (explicitNulls = false): discountPercent ausente debe ser null, no 0,
        // porque 0% y "sin descuento" secoatnan distinto.
        var sinDescuentos = """
            {"leadId":"11111111-1111-1111-1111-111111111111","title":"T","summaryHtml":"S",
             "discountAmount":0.00,"taxAmount":0.00,"items":[
               {"description":"D","quantity":1,"unitPrice":100.00,"sortOrder":0}]}
            """;

        var request = JsonSerializer.Deserialize<SaveProposalRequest>(sinDescuentos, Options);

        Assert.NotNull(request);
        Assert.Null(request.Items[0].DiscountPercent);
    }
}