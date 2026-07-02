using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BackSolutions.Settings
{
    public class EmailSettings
    {
        public string SmtpServer { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 587;
        public string SmtpUser { get; set; } = string.Empty;
        public string SmtpPassword { get; set; } = string.Empty;
    }
}