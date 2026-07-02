using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BackSolutions.Externals.Interfaces
{
    public interface InteligenceInterface
    {
        Task<string> GenerateResponse(string prompt);
    }
}