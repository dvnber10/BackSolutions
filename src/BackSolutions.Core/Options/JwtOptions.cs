using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BackSolutions.Core.Options
{
    public class JwtOptions
    {
        public const string SectionName = "Jwt";

        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public string SigningKey { get; set; } = string.Empty;
        public int AccessTokenMinutes { get; set; } = 30;
        public int RefreshTokenDays { get; set; } = 30;
        public bool IsValid(out string? error)
        {
            if (string.IsNullOrWhiteSpace(Issuer)) { error = "Issuer requerido."; return false; }
            if (string.IsNullOrWhiteSpace(Audience)) { error = "Audience requerido."; return false; }
            if (string.IsNullOrWhiteSpace(SigningKey) || SigningKey.Length < 32)
            {
                error = "SigningKey debe tener al menos 32 caracteres.";
                return false;
            }
            if (AccessTokenMinutes is < 1 or > 1440) { error = "AccessTokenMinutes inválido."; return false; }
            if (RefreshTokenDays is < 1 or > 365) { error = "RefreshTokenDays inválido."; return false; }
            error = null;
            return true;
        }
    }


}