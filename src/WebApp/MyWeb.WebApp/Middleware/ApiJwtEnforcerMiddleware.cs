using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace MyWeb.WebApp.Middleware
{
    /// <summary>
    /// UI isteklerinde cookie ile gelen authenticated kullanıcıyı kabul eder.
    /// API için:
    ///   - Whitelist path'ler: /api/auth/login, /api/auth/refresh, /api/diagnostics/*, /swagger,
    ///                          (UI için read-only) /api/catalog/tags*, /api/hist/projects
    ///   - OPTIONS preflight serbest
    ///   - Aksi halde Bearer header zorunlu
    /// </summary>
    public sealed class ApiJwtEnforcerMiddleware
    {
        private readonly RequestDelegate _next;

        public ApiJwtEnforcerMiddleware(RequestDelegate next) => _next = next;

        public async Task Invoke(HttpContext ctx)
        {
            var path = ctx.Request.Path.Value ?? string.Empty;
            var isApi = path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);

            // 0) UI cookie ile authenticated ise geç
            if (ctx.User?.Identity?.IsAuthenticated == true)
            {
                await _next(ctx);
                return;
            }

            if (!isApi)
            {
                await _next(ctx);
                return;
            }

            // 1) Whitelist
            if (IsWhitelisted(path))
            {
                await _next(ctx);
                return;
            }

            // 2) CORS preflight
            if (HttpMethods.IsOptions(ctx.Request.Method))
            {
                ctx.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }

            // 3) Bearer kontrolü
            var hasBearer = ctx.Request.Headers.TryGetValue("Authorization", out var auth) &&
                            auth.Count > 0 &&
                            auth[0].StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

            if (hasBearer)
            {
                await _next(ctx);
                return;
            }

            // 4) Reddet
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            ctx.Response.Headers["WWW-Authenticate"] = "Bearer";
        }

        private static bool IsWhitelisted(string path)
        {
            // Swagger
            if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)) return true;

            // Auth (login/refresh)
            if (path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase)) return true;
            if (path.Equals("/api/auth/refresh", StringComparison.OrdinalIgnoreCase)) return true;

            // Diagnostics
            if (path.StartsWith("/api/diagnostics/", StringComparison.OrdinalIgnoreCase)) return true;

            // UI'daki Trend sayfasının çağırdığı "tag listesi" ve "project listesi" uçları (read-only demoda serbest)
            if (path.StartsWith("/api/catalog/tags", StringComparison.OrdinalIgnoreCase)) return true;
            if (path.Equals("/api/hist/projects", StringComparison.OrdinalIgnoreCase)) return true;

            // logout için Bearer aranabilir; cookie yoksa 401 makul. İstersen whitelist’e ekleyebilirsin.
            return false;
        }
    }
}
