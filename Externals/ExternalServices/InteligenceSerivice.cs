using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BackSolutions.Externals.Interfaces;
using BackSolutions.Settings;
using Microsoft.Extensions.Options;

namespace BackSolutions.Externals.ExternalServices
{
    public class InteligenceSerivice : InteligenceInterface
    {
        private readonly string _huggingFaceUrl;
        private readonly HttpClient _client;

        // Recibimos HttpClient desde el contenedor de dependencias de .NET
        public InteligenceSerivice(HttpClient client, IOptions<HFSettings> hfSettings)
        {
            _client = client;
            
            var settings = hfSettings.Value;
            _huggingFaceUrl = settings.ModelUrl;

            // Configurar la cabecera de autenticación de forma segura y limpia
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.Token);
        }

        public async Task<string> GenerateResponse(string prompt)
        {
            // Añadimos parámetros para controlar la longitud y creatividad de la respuesta
            var requestData = new
            {
                inputs = prompt,
                parameters = new
                {
                    max_new_tokens = 1200, // Nos asegura espacio suficiente para el correo y el análisis
                    temperature = 0.7
                }
            };

            var jsonRequest = JsonSerializer.Serialize(requestData);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            try
            {
                var response = await _client.PostAsync(_huggingFaceUrl, content);
                
                // En lugar de lanzar una excepción genérica, podemos validar el estado
                if (!response.IsSuccessStatusCode)
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    return $"Error API HuggingFace ({response.StatusCode}): {errorDetails}";
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();

                // Usamos JsonDocument para parsear dinámicamente de forma eficiente y segura
                using (var doc = JsonDocument.Parse(jsonResponse))
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                    {
                        var firstElement = doc.RootElement[0];
                        if (firstElement.TryGetProperty("generated_text", out var generatedTextProperty))
                        {
                            var text = generatedTextProperty.GetString();

                            // Hugging Face a veces repite el prompt al inicio del resultado. Lo limpiamos:
                            if (!string.IsNullOrEmpty(text) && text.Contains(prompt))
                            {
                                text = text.Replace(prompt, "").Trim();
                            }

                            return text ?? string.Empty;
                        }
                    }
                }

                return "La IA devolvió un formato de respuesta inesperado.";
            }
            catch (Exception ex)
            {
                return $"Error interno en el servicio de inteligencia: {ex.Message}";
            }
        }
    }
}