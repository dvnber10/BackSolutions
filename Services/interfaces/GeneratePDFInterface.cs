using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace BackSolutions.Services.interfaces
{
    public interface GeneratePDFInterface
    {
        Task<byte[]> CrearPdfPropuestaAsync(string nombreCliente, string tipoServicio, JsonElement datosIa);
    }
}