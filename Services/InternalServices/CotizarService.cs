using System;
using System.Text.Json;
using System.Threading.Tasks;
using BackSolutions.Dtos;
using BackSolutions.Externals.Interfaces;
using BackSolutions.Services.interfaces;

namespace BackSolutions.Services.InternalServices
{
    public class CotizarService : CotizarInterface
    {
        private readonly InteligenceInterface _inteligenceService;
        private readonly GeneratePDFInterface _pdfService;
        private readonly EmailInterface _emailService;

        public CotizarService(
            InteligenceInterface inteligenceService,
            GeneratePDFInterface pdfService,
            EmailInterface emailService)
        {
            _inteligenceService = inteligenceService;
            _pdfService = pdfService;
            _emailService = emailService;
        }

        public async Task<bool> ProcesarCotizacionAsync(CotizarDto datos)
        {
            // 1. Estructurar el prompt maestro mapeando tus propiedades reales del DTO
            var prompt = $@"
            Actúas como el CTO senior de la agencia de desarrollo BackSolutions.
            Analiza el requerimiento de este cliente y genera una propuesta formal.
            Cliente: {datos.Nombre}
            Servicio Solicitado: {datos.Servicio}
            Detalles del Requerimiento: {datos.Detalles}

            Debes responder EXCLUSIVAMENTE con un JSON válido, sin bloques de código ```json ni texto adicional.
            Estructura exacta requerida:
            {{
              ""AsuntoCorreo"": ""Propuesta Comercial Preliminar: {datos.Servicio} - BackSolutions"",
              ""CuerpoCorreoHtml"": ""Escribe un mensaje de email muy profesional, empático y comercial en HTML saludando a {datos.Nombre}, validando su idea para el desarrollo de su {datos.Servicio} y mencionando que adjunto va su reporte de arquitectura."",
              ""PdfIntroduccion"": ""Resumen ejecutivo técnico adaptado a su requerimiento específico."",
              ""PdfArquitectura"": ""Explicación detallada de componentes de frontend, backend y base de datos idóneos para implementar este servicio."",
              ""PdfFases"": [
                {{ ""Fase"": ""Fase 1: Descubrimiento y Prototipado"", ""Duracion"": ""1-2 semanas"", ""Entregables"": ""Estructura del proyecto y Mockups interactivos."" }},
                {{ ""Fase"": ""Fase 2: Desarrollo e Integración"", ""Duracion"": ""3-4 semanas"", ""Entregables"": ""Backend API, Base de Datos y despliegue inicial."" }}
              ],
              ""PdfConclusion"": ""Mensaje final invitando a agendar una llamada técnica sin costo para revisar el alcance exacto con un ingeniero.""
            }}";

            try
            {
                // 2. Consumir Hugging Face
                string respuestaIaRaw = await _inteligenceService.GenerateResponse(prompt);

                // 💡 VALIDACIÓN CRÍTICA: Si no empieza por '{', no es un JSON válido
                if (string.IsNullOrWhiteSpace(respuestaIaRaw) || !respuestaIaRaw.Trim().StartsWith("{"))
                {
                    // Esto te pintará en la consola el texto exacto que causó el problema (ej: el mensaje de error de HF)
                    Console.WriteLine($"[HF ERROR DETECTADO]: El modelo no devolvió un JSON. Devolvió: {respuestaIaRaw}");
                    return false;
                }

                // 3. Parsear el JSON generado por la IA de forma segura
                using var jsonDoc = JsonDocument.Parse(respuestaIaRaw);
                var root = jsonDoc.RootElement;

                string asunto = root.GetProperty("AsuntoCorreo").GetString()!;
                string cuerpoEmail = root.GetProperty("CuerpoCorreoHtml").GetString()!;

                // 4. Delegar la construcción del PDF
                byte[] pdfBytes = await _pdfService.CrearPdfPropuestaAsync(datos.Nombre, datos.Servicio, root);

                // 5. Enviar correo al CLIENTE usando su propiedad "Email"
                string nombreArchivoPdf = $"Propuesta_BackSolutions_{datos.Nombre.Replace(" ", "_")}.pdf";

                bool enviadoAlCliente = await _emailService.SendEmail(
                    to: datos.Email,
                    subject: asunto,
                    body: cuerpoEmail,
                    pdfBytes: pdfBytes,
                    pdfName: nombreArchivoPdf
                );

                // 6. Notificación de control interna para ti (BackSolutions) incluyendo el "Telefono"
                string correoEmpresa = "proyectos@backsolutions.dev"; // Tu correo de administración
                string cuerpoNotificacionEmpresa = $@"
                    <h3>🚀 ¡Nuevo Lead Registrado en BackSolutions!</h3>
                    <p><strong>Cliente:</strong> {datos.Nombre}</p>
                    <p><strong>Email:</strong> {datos.Email}</p>
                    <p><strong>Teléfono:</strong> {datos.Telefono}</p>
                    <p><strong>Servicio de Interés:</strong> {datos.Servicio}</p>
                    <p><strong>Detalles proporcionados:</strong> {datos.Detalles}</p>
                    <hr />
                    <p><em>El sistema ha enviado de forma automática la propuesta inicial en PDF adjunta a este correo.</em></p>";

                await _emailService.SendEmail(
                    to: correoEmpresa,
                    subject: $"[NUEVO LEAD] {datos.Nombre} - {datos.Servicio}",
                    body: cuerpoNotificacionEmpresa,
                    pdfBytes: pdfBytes,
                    pdfName: nombreArchivoPdf
                );

                return enviadoAlCliente;
            }
            catch (Exception ex)
            {
                throw new Exception("Error al procesar la cotización. Verifica que el JSON generado por la IA sea válido y que los servicios externos estén funcionando correctamente. \n ex: " + ex.Message);
                // Aquí puedes registrar el log de errores si el JSON de la IA vino corrupto

            }
        }
    }
}