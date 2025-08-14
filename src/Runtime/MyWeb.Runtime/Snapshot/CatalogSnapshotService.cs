using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyWeb.Persistence.Catalog;
using MyWeb.Persistence.Catalog.Entities;

namespace MyWeb.Runtime.Snapshot
{
    /// <summary>
    /// Paket manifestine göre Project ve ProjectVersion kayıtlarını uygular.
    /// (Basit v1: Tag/Config upsert yok – gerekirse sonra ekleriz.)
    /// </summary>
    public class CatalogSnapshotService
    {
        private readonly ILogger<CatalogSnapshotService> _logger;
        private readonly CatalogDbContext _db;

        public CatalogSnapshotService(ILogger<CatalogSnapshotService> logger, CatalogDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        // BootstrapRunner bu imzayı çağırıyor
        public async Task<int> ApplyPackageAsync(object pkgObj, CancellationToken ct)
        {
            int affected = 0;

            if (pkgObj is null)
            {
                _logger.LogWarning("Snapshot: paket null geldi, işlem yok.");
                return affected;
            }

            // reflection ile güvenli okuma (model isimleri değişse bile)
            static string? GetStr(object owner, params string[] names)
            {
                var t = owner.GetType();
                foreach (var n in names)
                {
                    var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (p != null)
                    {
                        var v = p.GetValue(owner);
                        if (v is string s) return s;
                    }
                }
                return null;
            }

            // Manifest erişimi
            var manifestProp = pkgObj.GetType().GetProperty("Manifest");
            if (manifestProp == null)
            {
                _logger.LogWarning("Snapshot: paket Manifest içermiyor, işlem yok.");
                return affected;
            }

            var manifest = manifestProp.GetValue(pkgObj)!;

            var projectKey = GetStr(manifest, "ProjectKey", "projectKey");
            var projectName = GetStr(manifest, "ProjectName", "projectName") ?? projectKey ?? "Project";
            var projVersion = GetStr(manifest, "ProjVersion", "projVersion") ?? "1.0.0";
            var minEngine = GetStr(manifest, "MinEngine", "min_engine") ?? ">=1.0";

            var pkgHash = GetStr(pkgObj, "PackageHash", "packageHash") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(projectKey))
            {
                _logger.LogWarning("Snapshot: ProjectKey boş, işlem yok.");
                return affected;
            }

            // Project upsert
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Key == projectKey, ct);
            if (project == null)
            {
                project = new Project
                {
                    Key = projectKey,
                    Name = projectName!,
                    Version = projVersion!,
                    MinEngine = minEngine!,
                    CreatedUtc = DateTime.UtcNow
                };
                _db.Projects.Add(project);
                await _db.SaveChangesAsync(ct);
                affected++;
                _logger.LogInformation("Snapshot: Project eklendi: {Key}", projectKey);
            }
            else
            {
                // Sadece görünen ad/sürüm değiştiyse güncelle (opsiyonel)
                bool changed = false;
                if (project.Name != projectName) { project.Name = projectName!; changed = true; }
                if (project.Version != projVersion) { project.Version = projVersion!; changed = true; }
                if (project.MinEngine != minEngine) { project.MinEngine = minEngine!; changed = true; }

                if (changed)
                {
                    await _db.SaveChangesAsync(ct);
                    affected++;
                    _logger.LogInformation("Snapshot: Project güncellendi: {Key}", projectKey);
                }
            }

            // ProjectVersion varsa-atla; yoksa ekle
            var versionExists = await _db.ProjectVersions.AnyAsync(v =>
                    v.ProjectId == project.Id &&
                    v.Version == projVersion &&
                    v.Hash == pkgHash, ct);

            if (!versionExists)
            {
                _db.ProjectVersions.Add(new ProjectVersion
                {
                    ProjectId = project.Id,
                    Version = projVersion!,
                    Hash = pkgHash,
                    AppliedUtc = DateTime.UtcNow
                });
                await _db.SaveChangesAsync(ct);
                affected++;
                _logger.LogInformation("Snapshot: ProjectVersion eklendi: {Key} {Version}", projectKey, projVersion);
            }

            return affected;
        }
    }
}
