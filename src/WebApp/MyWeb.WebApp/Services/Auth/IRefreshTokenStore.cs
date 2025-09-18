using System;
using System.Threading.Tasks;

namespace MyWeb.WebApp.Services.Auth
{
    public interface IRefreshTokenStore
    {
        Task StoreAsync(string userId, string refreshToken, DateTimeOffset expiresUtc);
        Task<bool> ValidateAsync(string userId, string refreshToken);
        Task RevokeAsync(string userId, string refreshToken);
        Task RevokeAllAsync(string userId);
    }
}
