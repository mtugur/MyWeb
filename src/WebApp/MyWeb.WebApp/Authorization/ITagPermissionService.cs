using System.Security.Claims;
using System.Threading.Tasks;

namespace MyWeb.WebApp.Authorization
{
    public interface ITagPermissionService
    {
        Task<bool> CanReadTagAsync(string? userId, ClaimsPrincipal user, int tagId);
    }

    /// <summary>
    /// MVP/stub: Admin her şeyi okuyabilir; diğerleri şimdilik izinli.
    /// Stage-2.5’te DB tabanlı gerçek denetimle değiştirilecek.
    /// </summary>
    public sealed class PermissiveTagPermissionService : ITagPermissionService
    {
        public Task<bool> CanReadTagAsync(string? userId, ClaimsPrincipal user, int tagId)
            => Task.FromResult(true);
    }
}
