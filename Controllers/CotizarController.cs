using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BackSolutions.Dtos;
using BackSolutions.Services.interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BackSolutions.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CotizarController : ControllerBase
    {
        private readonly CotizarInterface _cotizarService;

        public CotizarController(CotizarInterface cotizarService)
        {
            _cotizarService = cotizarService;
        }

        [HttpPost]
        public async Task<IActionResult> CrearCotizacion([FromBody] CotizarDto dto)
        {
            // 1. Validar que el cuerpo de la petición no venga vacío
            if (dto == null) 
            {
                return BadRequest(new { Error = "Los datos de la cotización son requeridos." });
            }

            // 2. Validar campos mínimos obligatorios para evitar llamadas innecesarias a la IA
            if (string.IsNullOrWhiteSpace(dto.Nombre) || string.IsNullOrWhiteSpace(dto.Email))
            {
                return BadRequest(new { Error = "El nombre y el correo electrónico son obligatorios." });
            }

            // 3. Invocar al servicio interno (Orquestador) para ejecutar todo el flujo
            bool resultado = await _cotizarService.ProcesarCotizacionAsync(dto);

            // 4. Evaluar el resultado del flujo asíncrono
            if (!resultado)
            {
                return StatusCode(500, new 
                { 
                    Error = "Ocurrió un error al procesar tu solicitud. Por favor, intenta de nuevo más tarde." 
                });
            }

            // 5. Todo salió perfecto: el cliente y la empresa tienen sus correos con el PDF adjunto
            return Ok(new 
            { 
                Mensaje = "¡Cotización procesada con éxito! Revisa tu bandeja de entrada." 
            });
        }
    }
}