using System;
using System.Collections.Concurrent;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyWeb.Core.Hist;
using MyWeb.Persistence.Catalog;

namespace MyWeb.Runtime.History
{
    /// <summary>
    /// Arşivli tag'leri periyodik okur (DEMO: rastgele), deadband/change-only uygular ve hist.Samples'a toplu yazar.
    /// </summary>
    public sealed class HistoryWriterService : BackgroundService
    {
        private readonly ILogger<HistoryWriterService> _log;
        private readonly Microsoft.Extensions.DependencyInjection.IServiceScopeFactory _scopeFactory; // tam nitelikli
        private readonly IOptions<HistoryWriterOptions> _opt;
        private readonly IConfiguration _cfg;

        // Son değer önbelleği (deadband/change-only için)
        private readonly ConcurrentDictionary<int, (DataType type, object? value)> _last = new();

        public HistoryWriterService(
            ILogger<HistoryWriterService> log,
            Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopeFactory,
            IOptions<HistoryWriterOptions> opt,
            IConfiguration cfg)
        {
            _log = log;
            _scopeFactory = scopeFactory;
            _opt = opt;
            _cfg = cfg;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var o = _opt.Value;
            if (!o.Enabled)
            {
                _log.LogInformation("HistoryWriter kapalı (Enabled=false).");
                return;
            }

            var poll = Math.Max(100, o.PollMs);
            _log.LogInformation("HistoryWriter başladı: Poll={poll}ms, Batch={batch}, Random={rand}", poll, o.BatchSize, o.UseRandom);

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await DoWorkAsync(stoppingToken); }
                catch (Exception ex) { _log.LogError(ex, "HistoryWriter döngü hatası."); }

                try { await Task.Delay(poll, stoppingToken); } catch { /* cancelled */ }
            }
        }

        private async Task DoWorkAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();

            // GetService kullan: extension gerektirmez
            var sp = scope.ServiceProvider;
            var db = sp.GetService(typeof(CatalogDbContext)) as CatalogDbContext
                     ?? throw new InvalidOperationException("CatalogDbContext bulunamadı (DI).");
            var o  = _opt.Value;

            // Proje seçimi: sadece Id'yi çek (anonymous projection)
            var projRow = await db.Projects.AsNoTracking()
                .Where(p => string.IsNullOrWhiteSpace(o.ProjectKey) || p.Key == o.ProjectKey)
                .OrderBy(p => p.Id)
                .Select(p => new { p.Id })
                .FirstOrDefaultAsync(ct);

            if (projRow == null)
            {
                _log.LogWarning("HistoryWriter: Proje bulunamadı (ProjectKey={key}).", o.ProjectKey ?? "<null>");
                return;
            }

            int projId = projRow.Id;

            // Arşivli tag'ler
            var tags = await db.Tags.Include(t => t.Archive)
                .Where(t => t.ProjectId == projId && t.Archive != null)
                .AsNoTracking()
                .ToListAsync(ct);

            if (tags.Count == 0)
            {
                _log.LogDebug("HistoryWriter: Arşivli tag yok (ProjectId={pid}).", projId);
                return;
            }

            // DEMO değer üret + filtrele + batch
            var now = DateTime.UtcNow;
            var table = SampleWriteRow.CreateTable();

            foreach (var t in tags)
            {
                if (table.Rows.Count >= o.BatchSize) break;

                var (ok, num, txt, b) = o.UseRandom ? GenerateRandom(t.DataType) : (false, (double?)null, (string?)null, (bool?)null);
                if (!o.UseRandom && !ok) continue; // PLC bağlanınca gelecek

                if (!ShouldWrite(t.Id, t.DataType, t.Archive!.Mode, t.Archive.DeadbandAbs, t.Archive.DeadbandPercent, num, txt, b))
                    continue;

                new SampleWriteRow {
                    ProjectId = projId, TagId = t.Id, Utc = now, DataType = t.DataType,
                    ValueNumeric = num, ValueText = txt, ValueBool = b, Quality = 0, Source = 1
                }.AddTo(table);
            }

            if (table.Rows.Count == 0) return;

            // Bulk insert
            var connStr = _cfg.GetConnectionString("HistorianDb")
                         ?? throw new InvalidOperationException("HistorianDb connection string yok.");
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(ct);

            using var bulk = new SqlBulkCopy(conn)
            {
                DestinationTableName = "hist.Samples",
                BatchSize = table.Rows.Count,
                BulkCopyTimeout = 60
            };
            bulk.ColumnMappings.Add("ProjectId",    "ProjectId");
            bulk.ColumnMappings.Add("TagId",        "TagId");
            bulk.ColumnMappings.Add("Utc",          "Utc");
            bulk.ColumnMappings.Add("DataType",     "DataType");
            bulk.ColumnMappings.Add("ValueNumeric", "ValueNumeric");
            bulk.ColumnMappings.Add("ValueText",    "ValueText");
            bulk.ColumnMappings.Add("ValueBool",    "ValueBool");
            bulk.ColumnMappings.Add("Quality",      "Quality");
            bulk.ColumnMappings.Add("Source",       "Source");

            await bulk.WriteToServerAsync(table, ct);
            _log.LogDebug("HistoryWriter: {n} örnek yazıldı (ProjectId={pid}).", table.Rows.Count, projId);

        }

        private static (bool ok, double? num, string? txt, bool? b) GenerateRandom(DataType dt)
        {
            var rnd = Random.Shared;
            return dt switch
            {
                DataType.Bool   => (true, null, null, rnd.Next(0, 2) == 0),
                DataType.Int    => (true, rnd.Next(-1000, 1000), null, null),
                DataType.Float  => (true, rnd.NextDouble() * 1000.0, null, null),
                DataType.String => (true, null, "S" + rnd.Next(0, 1000), null),
                DataType.Date   => (true, null, DateTime.UtcNow.ToString("O"), null),
                _               => (false, null, null, null)
            };
        }

        private bool ShouldWrite(int tagId, DataType dt, Core.Hist.ArchiveMode mode, double? deadAbs, double? deadPct, double? num, string? txt, bool? b)
        {
            object? current = dt switch
            {
                DataType.Bool   => b,
                DataType.Int    => num,
                DataType.Float  => num,
                DataType.String => txt,
                DataType.Date   => txt, // ISO 8601
                _ => null
            };

            if (mode == Core.Hist.ArchiveMode.Always)
            {
                _last[tagId] = (dt, current);
                return true;
            }

            if (!_last.TryGetValue(tagId, out var last))
            {
                _last[tagId] = (dt, current);
                return true; // ilk değer
            }

            if (mode == Core.Hist.ArchiveMode.ChangeOnly)
            {
                bool changed = dt switch
                {
                    DataType.Bool   => !Equals(last.value as bool?, b),
                    DataType.Int    => !Equals(last.value as double?, num),
                    DataType.Float  => !Equals(last.value as double?, num),
                    DataType.String => !Equals(last.value as string, txt),
                    DataType.Date   => !Equals(last.value as string, txt),
                    _ => true
                };
                if (changed) _last[tagId] = (dt, current);
                return changed;
            }

            // Deadband (sayısal)
            if (dt is DataType.Int or DataType.Float && num.HasValue)
            {
                var lastNum = last.value as double?;
                if (!lastNum.HasValue)
                {
                    _last[tagId] = (dt, num);
                    return true;
                }

                double diff = Math.Abs(num.Value - lastNum.Value);
                double limA = deadAbs ?? 0.0;
                double limP = (deadPct ?? 0.0) * Math.Abs(lastNum.Value);
                double threshold = Math.Max(limA, limP);

                if (diff >= threshold)
                {
                    _last[tagId] = (dt, num);
                    return true;
                }
                return false;
            }

            // Sayısal değilse ChangeOnly gibi
            bool changedText = !Equals(last.value, current);
            if (changedText) _last[tagId] = (dt, current);
            return changedText;
        }
    }
}
