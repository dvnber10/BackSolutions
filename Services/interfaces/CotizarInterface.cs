using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BackSolutions.Dtos;

namespace BackSolutions.Services.interfaces
{
    public interface CotizarInterface
    {
        Task<bool> ProcesarCotizacionAsync(CotizarDto datos);
    }
}