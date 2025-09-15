using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyWeb.Core.Hist;                    // DataType, ArchiveMode
using MyWeb.Persistence.Catalog;          // CatalogDbContext + Tag/Archive
using System;
using System.Collections.Concurrent;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MyWeb.Runtime.History
{
    /// <summary>
    /// Arşivli tag'leri periyodik okur (DEMO: rastgele), deadband/change-only uygular
    /// ve V2 şemasındaki hist.Samples'a toplu (SqlBulkCopy) yazar.
    /// </summary>
    public sealed class HistoryWriterService : BackgroundService
    {
        private readonly ILogger<HistoryWriterService> _log;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptions<HistoryWriterOptions> _opt;
        private readonly IConfiguration _cfg;

        // Son değer önbelleği (deadband/change-only için)
        // key: TagId  -> (DataType, lastValue)
        private readonly ConcurrentDictionary<int, (DataType type, object? value)> _last = new();

        public HistoryWriterService(
            ILogger<HistoryWriterService> log,
            IServiceScopeFactory scopeFactory,
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
                _log.LogInformation("HistoryWriter (V2) kapalı (Enabled=false).");
                return;
            }

            var poll = Math.Max(100, o.PollMs);
            _log.LogInformation("HistoryWriter (V2) başladı: Poll={poll}ms, Batch={batch}, Random={rand}",
                poll, o.BatchSize, o.UseRandom);

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await DoWorkAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                catch (Exception ex) { _log.LogError(ex, "HistoryWriter (V2) döngü hatası."); }

                try { await Task.Delay(poll, stoppingToken); } catch { /* cancelled */ }
            }
        }

        private async Task DoWorkAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            var db = sp.GetRequiredService<CatalogDbContext>();
            var o = _opt.Value;

            // Proje seçimi: ProjectKey verilmişse ona göre, yoksa ilk proje
            var projRow = await db.Projects.AsNoTracking()
                .Where(p => string.IsNullOrWhiteSpace(o.ProjectKey) || p.Key == o.ProjectKey)
                .OrderBy(p => p.Id)
                .Select(p => new { p.Id })
                .FirstOrDefaultAsync(ct);

            if (projRow is null)
            {
                _log.LogWarning("HistoryWriter (V2): Proje bulunamadı (ProjectKey={key}).", o.ProjectKey ?? "<null>");
                return;
            }
            int projId = projRow.Id;

            // Arşivli tag'ler (Archive != null)
            var tags = await db.Tags
                .Include(t => t.Archive)
                .AsNoTracking()
                .Where(t => t.ProjectId == projId && t.Archive != null)
                .ToListAsync(ct);

            if (tags.Count == 0)
            {
                _log.LogDebug("HistoryWriter (V2): Arşivli tag yok (ProjectId={pid}).", projId);
                return;
            }

            // DEMO: değer üret + filtrele + batch hazırla
            var now = DateTime.UtcNow;
            var table = CreateV2Table();

            foreach (var t in tags)
            {
                if (table.Rows.Count >= Math.Max(1, o.BatchSize)) break;

                // Değer üretimi: Şimdilik UseRandom true ise rastgele
                var (ok, num, txt, b) = o.UseRandom ? GenerateRandom(t.DataType) : (false, (double?)null, (string?)null, (bool?)null);
                if (!o.UseRandom && !ok) continue; // Gerçekte PLC okumaları geldiğinde doldurulacak.

                // ChangeOnly/Deadband/Always kontrolü
                var arch = t.Archive!;
                if (!ShouldWrite(t.Id, t.DataType, arch.Mode, arch.DeadbandAbs, arch.DeadbandPercent, num, txt, b))
                    continue;

                // V2 satırı ekle
                var row = table.NewRow();
                row["ProjectId"] = projId;
                row["TagId"] = t.Id;
                row["Utc"] = now;
                row["DataType"] = (byte)t.DataType;   // enum -> tinyint
                row["ValueNumeric"] = (object?)num ?? DBNull.Value;
                row["ValueText"] = (object?)txt ?? DBNull.Value;
                row["ValueBool"] = (object?)b ?? DBNull.Value;
                row["Quality"] = (short)0;           // 0 = Good
                row["Source"] = (byte)1;            // 1 = Demo/Runtime
                table.Rows.Add(row);
            }

            if (table.Rows.Count == 0) return;

            // Bulk insert -> hist.Samples (V2)
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

            bulk.ColumnMappings.Add("ProjectId", "ProjectId");
            bulk.ColumnMappings.Add("TagId", "TagId");
            bulk.ColumnMappings.Add("Utc", "Utc");
            bulk.ColumnMappings.Add("DataType", "DataType");
            bulk.ColumnMappings.Add("ValueNumeric", "ValueNumeric");
            bulk.ColumnMappings.Add("ValueText", "ValueText");
            bulk.ColumnMappings.Add("ValueBool", "ValueBool");
            bulk.ColumnMappings.Add("Quality", "Quality");
            bulk.ColumnMappings.Add("Source", "Source");

            await bulk.WriteToServerAsync(table, ct);

            _log.LogInformation("HistoryWriter (V2): {n} örnek yazıldı (ProjectId={pid}).", table.Rows.Count, projId);
        }

        private static DataTable CreateV2Table()
        {
            var dt = new DataTable();
            // V2 kolonları (Id yok; identity)
            dt.Columns.Add("ProjectId", typeof(int));
            dt.Columns.Add("TagId", typeof(int));
            dt.Columns.Add("Utc", typeof(DateTime));
            dt.Columns.Add("DataType", typeof(byte));    // enum -> tinyint
            dt.Columns.Add("ValueNumeric", typeof(double));  // nullable
            dt.Columns.Add("ValueText", typeof(string));  // nullable
            dt.Columns.Add("ValueBool", typeof(bool));    // nullable
            dt.Columns.Add("Quality", typeof(short));
            dt.Columns.Add("Source", typeof(byte));    // nullable ama DataTable'da DBNull.Value ile geçeriz
            return dt;
        }

        private static (bool ok, double? num, string? txt, bool? b) GenerateRandom(DataType dt)
        {
            var rnd = Random.Shared;
            return dt switch
            {
                DataType.Bool => (true, null, null, rnd.Next(0, 2) == 0),
                DataType.Int => (true, rnd.Next(-1000, 1000), null, null),
                DataType.Float => (true, rnd.NextDouble() * 1000.0, null, null),
                DataType.String => (true, null, "S" + rnd.Next(0, 1000), null),
                DataType.Date => (true, null, DateTime.UtcNow.ToString("O"), null),
                _ => (false, null, null, null)
            };
        }

        private bool ShouldWrite(
            int tagId,
            DataType dt,
            ArchiveMode mode,
            double? deadAbs,
            double? deadPct,
            double? num,
            string? txt,
            bool? b)
        {
            object? current = dt switch
            {
                DataType.Bool => b,
                DataType.Int => num,
                DataType.Float => num,
                DataType.String => txt,
                DataType.Date => txt, // ISO 8601
                _ => null
            };

            if (mode == ArchiveMode.Always)
            {
                _last[tagId] = (dt, current);
                return true;
            }

            if (!_last.TryGetValue(tagId, out var last))
            {
                _last[tagId] = (dt, current);
                return true; // ilk değer
            }

            if (mode == ArchiveMode.ChangeOnly)
            {
                bool changed = dt switch
                {
                    DataType.Bool => !Equals(last.value as bool?, b),
                    DataType.Int => !Equals(last.value as double?, num),
                    DataType.Float => !Equals(last.value as double?, num),
                    DataType.String => !Equals(last.value as string, txt),
                    DataType.Date => !Equals(last.value as string, txt),
                    _ => true
                };
                if (changed) _last[tagId] = (dt, current);
                return changed;
            }

            // Deadband (sayısal tipler)
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
