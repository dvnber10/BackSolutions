using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BackSolutions.Core.Options
{
    public class SmtpOptions
    {
        public const string SectionName = "Smtp";

        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromName { get; set; } = "BackSolutions";
        public bool UseStartTls { get; set; } = true;

        public bool IsValid(out string? error)
        {
            if (string.IsNullOrWhiteSpace(Host)) { error = "Host requerido."; return false; }
            if (Port is < 1 or > 65535) { error = "Port inválido."; return false; }
            if (string.IsNullOrWhiteSpace(User)) { error = "User requerido."; return false; }
            if (string.IsNullOrWhiteSpace(Password)) { error = "Password requerido."; return false; }
            error = null;
            return true;
        }
    }
}