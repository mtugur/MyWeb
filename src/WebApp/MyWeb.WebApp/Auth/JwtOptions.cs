using System;

namespace MyWeb.WebApp.Auth
{
    public sealed class JwtOptions
    {
        public string Issuer { get; set; } = "MyWeb";
        public string Audience { get; set; } = "MyWebAPI";
        public string Key { get; set; } = "CHANGE-ME-TO-A-STRONG-256BIT-KEY-AT-LEAST-32-CHARS";
        public int AccessTokenMinutes { get; set; } = 30;
        public int RefreshTokenDays { get; set; } = 7;
    }
}
