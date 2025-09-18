using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyWeb.Infrastructure.Data.Identity;   // IdentityDb, Permission*, ApplicationRole/User
using MyWeb.Persistence.Catalog;           // CatalogDbContext

namespace MyWeb.WebApp.Authorization
{
    /// <summary>
    /// Tag/Project/Area (Zone) bazlı okuma izni kontrolü.
    /// - User + Role bazlı PermissionSet'lerin birleşimini alır.
    /// - Permission.ScopeType:
    ///     Tag     -> ScopeId = Tag.Id (string)
    ///     Project -> ScopeId = Project.Id (string)
    ///     Area    -> ScopeId = Tag.Path prefix (örn: "Plant1/AreaA")
    /// </summary>
    public sealed class DbTagPermissionService : ITagPermissionService
    {
        private readonly IdentityDb _auth;
        private readonly CatalogDbContext _catalog;

        public DbTagPermissionService(IdentityDb auth, CatalogDbContext catalog)
        {
            _auth = auth;
            _catalog = catalog;
        }

        public async Task<bool> CanReadTagAsync(string? userId, ClaimsPrincipal user, int tagId)
        {
            // 0) Admin ise geç
            if (user.IsInRole("Admin")) return true;
            if (string.IsNullOrWhiteSpace(userId)) return false;

            // 1) Tag bilgisi
            var tag = await _catalog.Tags
                .AsNoTracking()
                .Where(t => t.Id == tagId)
                .Select(t => new { t.Id, t.ProjectId, t.Path })
                .FirstOrDefaultAsync();

            if (tag is null) return false;

            // 2) Kullanıcıya ve rollerine bağlı PermissionSetId'leri topla
            //    RoleId'leri ClaimsPrincipal'daki Role isimlerinden çözüyoruz.
            var roleNames = user.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .Distinct()
                .ToArray();

            var normRoleNames = roleNames.Select(r => r.ToUpperInvariant()).ToArray();
            var roleIds = await _auth.Roles
                .Where(r => normRoleNames.Contains(r.NormalizedName))
                .Select(r => r.Id)
                .ToListAsync();

            var userSetIds = await _auth.UserPermissionSets
                .Where(x => x.UserId == userId)
                .Select(x => x.PermissionSetId)
                .ToListAsync();

            var roleSetIds = await _auth.RolePermissionSets
                .Where(x => roleIds.Contains(x.RoleId))
                .Select(x => x.PermissionSetId)
                .ToListAsync();

            var allSetIds = userSetIds.Concat(roleSetIds).Distinct().ToArray();
            if (allSetIds.Length == 0) return false;

            // 3) Etkili Permission'ları çek
            var perms = await _auth.Permissions
                .Where(p => allSetIds.Contains(p.PermissionSetId))
                .Select(p => new
                {
                    p.ScopeType,
                    p.ScopeId,
                    p.Access
                })
                .ToListAsync();

            // 4) Değerlendirme (Read/Write/Admin -> okuma izni sayılır)
            bool AllowsRead(string access)
                => access == PermissionAccess.Read.ToString()
                || access == PermissionAccess.Write.ToString()
                || access == PermissionAccess.Admin.ToString();

            // Tag
            if (perms.Any(p =>
                    p.ScopeType == PermissionScopeType.Tag &&
                    p.ScopeId == tag.Id.ToString() &&
                    AllowsRead(p.Access.ToString())))
                return true;

            // Project
            if (perms.Any(p =>
                    p.ScopeType == PermissionScopeType.Project &&
                    p.ScopeId == tag.ProjectId.ToString() &&
                    AllowsRead(p.Access.ToString())))
                return true;

            // Area (Zone): Path prefix eşleşmesi (örn: "Plant1/AreaA")
            if (!string.IsNullOrWhiteSpace(tag.Path))
            {
                var tpath = tag.Path!;
                if (perms.Any(p =>
                        p.ScopeType == PermissionScopeType.Area &&
                        !string.IsNullOrWhiteSpace(p.ScopeId) &&
                        tpath.StartsWith(p.ScopeId, StringComparison.OrdinalIgnoreCase) &&
                        AllowsRead(p.Access.ToString())))
                    return true;
            }

            return false;
        }
    }
}
