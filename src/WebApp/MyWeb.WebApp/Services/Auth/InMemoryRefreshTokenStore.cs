using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace MyWeb.WebApp.Services.Auth
{
    public sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, DateTimeOffset>> _map
            = new(StringComparer.Ordinal);

        public Task StoreAsync(string userId, string token, DateTimeOffset exp)
        {
            var bucket = _map.GetOrAdd(userId, _ => new ConcurrentDictionary<string, DateTimeOffset>(StringComparer.Ordinal));
            bucket[token] = exp;
            return Task.CompletedTask;
        }

        public Task<bool> ValidateAsync(string userId, string token)
        {
            if (_map.TryGetValue(userId, out var bucket) &&
                bucket.TryGetValue(token, out var exp) &&
                exp > DateTimeOffset.UtcNow)
            {
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task RevokeAsync(string userId, string token)
        {
            if (_map.TryGetValue(userId, out var bucket))
            {
                bucket.TryRemove(token, out _);
            }
            return Task.CompletedTask;
        }

        public Task RevokeAllAsync(string userId)
        {
            _map.TryRemove(userId, out _);
            return Task.CompletedTask;
        }
    }
}
