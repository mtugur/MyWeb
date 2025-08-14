using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection; // CreateScope, GetRequiredService
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyWeb.Core.Hist;                           // DataType, ArchiveMode
using MyWeb.Persistence.Catalog;                 // CatalogDbContext
using MyWeb.Persistence.Catalog.Entities;        // Tag, TagArchiveConfig
using MyWeb.Runtime.Options;                     // BootstrapOptions

namespace MyWeb.Runtime.Snapshot
{
    /// <summary>
    /// .mywebpkg içinden manifest.json ve config/tags.json okuyup
    /// catalog şemasına idempotent Tag + TagArchiveConfig upsert eder.
    /// </summary>
    public sealed class TagSnapshotService : BackgroundService
    {
        private readonly ILogger<TagSnapshotService> _log;
        private readonly IServiceProvider _sp;
        private readonly IOptions<BootstrapOptions> _bootstrap;

        public TagSnapshotService(
            ILogger<TagSnapshotService> log,
            IServiceProvider sp,
            IOptions<BootstrapOptions> bootstrap)
        {
            _log = log;
            _sp = sp;
            _bootstrap = bootstrap;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) { _log.LogError(ex, "TagSnapshotService çalışırken hata."); }
        }

        private async Task RunOnceAsync(CancellationToken ct)
        {
            var pkgPath = _bootstrap.Value.PackagePath;
            if (string.IsNullOrWhiteSpace(pkgPath) || !File.Exists(pkgPath))
            {
                _log.LogWarning("TagSnapshot: Paket bulunamadı: {path}", pkgPath ?? "<null>");
                return;
            }

            PackageManifest? manifest;
            List<TagJson>? tagsFromPkg;

            using (var zip = ZipFile.OpenRead(pkgPath))
            {
                manifest    = ReadJson<PackageManifest>(zip, "manifest.json");
                tagsFromPkg = ReadJson<List<TagJson>>(zip, "config/tags.json");
            }

            if (manifest == null)
            {
                _log.LogWarning("TagSnapshot: manifest.json okunamadı.");
                return;
            }
            if (tagsFromPkg == null || tagsFromPkg.Count == 0)
            {
                _log.LogInformation("TagSnapshot: tags.json yok/boş; işlem yok.");
                return;
            }

            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

            var projId = await db.Projects.AsNoTracking()
                .Where(p => p.Key == manifest.projectKey)
                .Select(p => p.Id)
                .FirstOrDefaultAsync(ct);

            if (projId == 0)
            {
                _log.LogWarning("TagSnapshot: Project '{key}' bulunamadı; önce Bootstrap manifest yazmalı.", manifest.projectKey);
                return;
            }

            int added = 0, updated = 0, archAdded = 0, archUpdated = 0;

            foreach (var tj in tagsFromPkg)
            {
                if (tj is null || string.IsNullOrWhiteSpace(tj.path)) continue;

                var tag = await db.Tags
                    .Include(t => t.Archive)
                    .FirstOrDefaultAsync(t => t.ProjectId == projId && t.Path == tj.path, ct);

                var isNew = tag == null;
                if (isNew)
                {
                    tag = new Tag
                    {
                        ProjectId = projId,
                        Path      = tj.path,
                        Name      = string.IsNullOrWhiteSpace(tj.name) ? LastSegment(tj.path) : tj.name!,
                        DataType  = MapDataType(tj.dataType),
                        Unit      = tj.unit,
                        Address   = tj.address,
                        DriverRef = null,
                        LongString= (string.Equals(tj.dataType, "String", StringComparison.OrdinalIgnoreCase) && (tj.count ?? 0) > 255)
                    };
                    await db.Tags.AddAsync(tag, ct);
                    added++;
                }
                else
                {
                    tag!.Name     = string.IsNullOrWhiteSpace(tj.name) ? tag.Name : tj.name!;
                    tag.DataType  = MapDataType(tj.dataType);
                    tag.Unit      = tj.unit ?? tag.Unit;
                    tag.Address   = tj.address ?? tag.Address;
                    updated++;
                }

                await db.SaveChangesAsync(ct); // Tag.Id kesinleşsin

                if (tj.archive != null)
                {
                    var a = tag.Archive ?? await db.TagArchiveConfigs.FirstOrDefaultAsync(x => x.TagId == tag.Id, ct);
                    var aNew = a == null;

                    if (aNew)
                    {
                        a = new TagArchiveConfig { TagId = tag.Id };
                        await db.TagArchiveConfigs.AddAsync(a, ct);
                        archAdded++;
                    }

                    a!.Mode           = MapArchiveMode(tj.archive.mode);
                    a.DeadbandAbs     = tj.archive.deadbandAbs;
                    a.DeadbandPercent = tj.archive.deadbandPercent;
                    a.RetentionDays   = tj.archive.retentionDays ?? 365;
                    a.RollupsJson     = (tj.archive.rollups != null && tj.archive.rollups.Count > 0)
                        ? JsonSerializer.Serialize(tj.archive.rollups)
                        : @"[""1m"",""1h""]";

                    if (!aNew) archUpdated++;
                    await db.SaveChangesAsync(ct);
                }
            }

            _log.LogInformation("TagSnapshot: Tags added={added}, updated={updated}; Archive added={aadd}, updated={aupd}.",
                added, updated, archAdded, archUpdated);
        }

        // ---------- Helpers ----------
        private static T? ReadJson<T>(ZipArchive zip, string entryPath)
        {
            var entry = zip.Entries.FirstOrDefault(e =>
                string.Equals(e.FullName.Replace('\\','/').TrimStart('/'), entryPath, StringComparison.OrdinalIgnoreCase));
            if (entry == null) return default;

            using var s = entry.Open();
            return JsonSerializer.Deserialize<T>(s, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        private static string LastSegment(string path)
            => path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? path;

        private static DataType MapDataType(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return DataType.Float;
            var v = s.Trim().ToLowerInvariant();
            return v switch
            {
                "bool" or "bit"                          => DataType.Bool,
                "int" or "dint" or "lint" or "word"      => DataType.Int,
                "float" or "real" or "lreal" or "double" => DataType.Float,
                "string" or "wstring"                    => DataType.String,
                "date" or "datetime"                     => DataType.Date,
                _                                         => DataType.Float
            };
        }

        private static ArchiveMode MapArchiveMode(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return ArchiveMode.ChangeOnly;
            var v = s.Trim().ToLowerInvariant();
            return v switch
            {
                "always"     => ArchiveMode.Always,
                "changeonly" => ArchiveMode.ChangeOnly,
                "deadband"   => ArchiveMode.Deadband,
                _            => ArchiveMode.ChangeOnly
            };
        }

        // ----- Paket JSON DTO'ları -----
        private sealed class PackageManifest
        {
            public string projectKey { get; set; } = "";
            public string projectName { get; set; } = "";
            public string projVersion { get; set; } = "";
            public string min_engine { get; set; } = "";
        }

        private sealed class TagJson
        {
            public string path { get; set; } = "";
            public string? name { get; set; }
            public string dataType { get; set; } = "Float";
            public string? unit { get; set; }
            public string? address { get; set; }
            public int? count { get; set; }
            public ArchiveJson? archive { get; set; }
        }

        private sealed class ArchiveJson
        {
            public string mode { get; set; } = "ChangeOnly";
            public double? deadbandAbs { get; set; }
            public double? deadbandPercent { get; set; }
            public int? retentionDays { get; set; }
            public List<string>? rollups { get; set; }
        }
    }
}
