using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BackSolutions.Externals.Interfaces
{
    public interface EmailInterface
    {
        Task<bool> SendEmail(string to, string subject, string body, byte[]? pdfBytes, string pdfName);
    }
}