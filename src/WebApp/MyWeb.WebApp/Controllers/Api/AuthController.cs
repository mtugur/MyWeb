using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using MyWeb.WebApp.Services.Auth;

namespace MyWeb.WebApp.Controllers.Api
{
    [ApiController]
    [Route("api/auth")]
    public sealed class AuthController : ControllerBase
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IConfiguration _cfg;
        private readonly IRefreshTokenStore _refreshStore;
        private readonly ILogger<AuthController> _log;

        public AuthController(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            IConfiguration cfg,
            IRefreshTokenStore refreshStore,
            ILogger<AuthController> log)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _cfg = cfg;
            _refreshStore = refreshStore;
            _log = log;
        }

        public sealed record LoginDto(
            [property: JsonPropertyName("email")] string Email,
            [property: JsonPropertyName("password")] string Password
        );

        public sealed record TokenPair(string AccessToken, string RefreshToken, DateTimeOffset AccessExpiresUtc, DateTimeOffset RefreshExpiresUtc);

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user is null) return Unauthorized();

            var pass = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);
            if (!pass.Succeeded) return Unauthorized();

            // Cookie sign-in (web UI) – mevcut akışın korunduğunu varsayıyoruz
            await _signInManager.SignInAsync(user, isPersistent: true);

            var pair = await IssueTokensAsync(user);
            return Ok(new { accessToken = pair.AccessToken, refreshToken = pair.RefreshToken, accessExpiresUtc = pair.AccessExpiresUtc, refreshExpiresUtc = pair.RefreshExpiresUtc });
        }

        public sealed record RefreshDto([property: JsonPropertyName("refreshToken")] string RefreshToken);

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> Refresh([FromBody] RefreshDto dto)
        {
            // Access token Cookie’da olabilir; refresh token body’de gelir
            var principal = HttpContext.User;
            string? email = principal?.FindFirstValue(ClaimTypes.Email);
            string? userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            // Eğer cookie yoksa, refresh token içindeki sub’ı çözmek yerine store’a sadece userId bağlamı ile gideceğiz.
            // Bu nedenle refresh token’ı kiminle verdiğimizi, login sırasında store’a userId ile yazmıştık.
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
            {
                // Cookie yoksa (örn. Postman), refresh token’ı mevcut kullanıcıyı bulmak için email’e ihtiyaç duymadan da doğrulayacağız:
                // Bu demo akışında refresh token, userId ile storelandı; userId’yi çözemiyorsak kullanıcıyı refresh token’dan türetemiyoruz → 401
                return Unauthorized();
            }

            var ok = await _refreshStore.ValidateAsync(userId, dto.RefreshToken);
            if (!ok) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null) return Unauthorized();

            var pair = await IssueTokensAsync(user); // rotate
            // Eski refresh token’ı iptal et
            await _refreshStore.RevokeAsync(userId, dto.RefreshToken);

            return Ok(new { accessToken = pair.AccessToken, refreshToken = pair.RefreshToken, accessExpiresUtc = pair.AccessExpiresUtc, refreshExpiresUtc = pair.RefreshExpiresUtc });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
                await _refreshStore.RevokeAllAsync(userId);

            await _signInManager.SignOutAsync();
            return Ok();
        }

        // ---- helpers ----
        private async Task<TokenPair> IssueTokensAsync(IdentityUser user)
        {
            var issuer = _cfg["Jwt:Issuer"] ?? "MyWeb";
            var audience = _cfg["Jwt:Audience"] ?? "MyWebAPI";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var minutes = int.TryParse(_cfg["Jwt:AccessTokenMinutes"], out var m) ? m : 45;
            var refreshDays = int.TryParse(_cfg["Jwt:RefreshTokenDays"], out var d) ? d : 7;

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id),
                new(ClaimTypes.Email, user.Email ?? "")
            };

            // Role claim – minimum
            var roles = await _userManager.GetRolesAsync(user);
            foreach (var r in roles)
                claims.Add(new Claim(ClaimTypes.Role, r));

            var now = DateTimeOffset.UtcNow;
            var accessExp = now.AddMinutes(minutes);

            var jwt = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: now.UtcDateTime,
                expires: accessExp.UtcDateTime,
                signingCredentials: creds);

            var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);

            // Refresh token (rastgele)
            var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var refreshExp = now.AddDays(refreshDays);
            await _refreshStore.StoreAsync(user.Id, refreshToken, refreshExp);

            return new TokenPair(accessToken, refreshToken, accessExp, refreshExp);
        }
    }

    internal static class ClaimsPrincipalExt
    {
        public static string? FindFirstValue(this ClaimsPrincipal p, string type)
            => p?.FindFirst(type)?.Value;
    }
}
