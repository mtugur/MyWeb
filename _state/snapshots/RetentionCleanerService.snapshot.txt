using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MyWeb.Runtime.Services
{
    public sealed class RetentionOptions
    {
        public bool Enabled { get; set; } = true;
        public int SweepMinutes { get; set; } = 10;
        public int DeleteChunk { get; set; } = 5000;
        public int DefaultRetentionDays { get; set; } = 30;
    }

    public sealed class RetentionCleanerService : BackgroundService
    {
        private readonly ILogger<RetentionCleanerService> _log;
        private readonly RetentionOptions _opt;
        private readonly DbConnOptions _db;

        public RetentionCleanerService(
            ILogger<RetentionCleanerService> log,
            IOptions<HistoryOptions> histOpt,           // History:Retention altından okuyoruz
            IOptions<RetentionOptions> retOpt,          // fallback
            IOptions<DbConnOptions> db)
        {
            _log = log;
            _db = db.Value;

            // HistoryOptions içindeki Retention bölümünü de destekle
            _opt = retOpt?.Value ?? new RetentionOptions();
            if (histOpt?.Value?.Retention is not null)
            {
                var r = histOpt.Value.Retention!;
                _opt.Enabled = r.Enabled;
                _opt.SweepMinutes = r.SweepMinutes;
                _opt.DeleteChunk = r.DeleteChunk;
                _opt.DefaultRetentionDays = r.DefaultRetentionDays;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_opt.Enabled)
            {
                _log.LogInformation("RetentionCleaner kapalı (Enabled=false).");
                return;
            }

            var period = TimeSpan.FromMinutes(Math.Max(1, _opt.SweepMinutes));
            _log.LogInformation("RetentionCleaner başladı. Periyot={minutes}dk, Chunk={chunk}",
                _opt.SweepMinutes, _opt.DeleteChunk);

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await SweepOnceAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                catch (Exception ex) { _log.LogError(ex, "Retention sweep hatası"); }

                try { await Task.Delay(period, stoppingToken); }
                catch { /* cancelled */ }
            }
        }

        private async Task SweepOnceAsync(CancellationToken ct)
        {
            // 1) Politikalı tag'ler ve süreleri
            var map = await LoadPolicyMapAsync(ct); // Tag -> RetentionDays

            // 2) Politikası olmayanlar için default süreyi uygulamak istiyorsan, 
            //    distinct tag listesini çekip `map` içinde olmayanlara ekleyebilirsin (opsiyonel).
            //    Şimdilik sadece tanımlı politikalara göre temizleyelim.

            int totalDeleted = 0;
            using var conn = new SqlConnection(_db.HistorianDb);
            await conn.OpenAsync(ct);

            foreach (var grp in map.GroupBy(kv => kv.Value)) // aynı gün için toplu çalış
            {
                int days = grp.Key;
                var tags = grp.Select(kv => kv.Key).ToList();
                var cutoff = DateTime.UtcNow.AddDays(-days);

                // IN listesi yerine parametreli TVP de yapılabilir; tag sayısı düşükken IN pratik
                int deleted;
                do
                {
                    using var cmd = new SqlCommand();
                    cmd.Connection = conn;
                    cmd.CommandType = CommandType.Text;

                    // küçük gruplar halinde yapalım (SQL param limiti vs. için)
                    var thisBatch = tags.Take(100).ToList(); // 100 tag'lik dilimler
                    string inList = string.Join(",", thisBatch.Select((t, i) => $"@p{i}"));
                    cmd.CommandText = $@"
DELETE TOP ({_opt.DeleteChunk})
FROM [hist].[Samples]
WHERE [Utc] < @cutoff AND [Tag] IN ({inList});";

                    cmd.Parameters.Add(new SqlParameter("@cutoff", SqlDbType.DateTime2) { Value = cutoff });
                    for (int i = 0; i < thisBatch.Count; i++)
                        cmd.Parameters.Add(new SqlParameter($"@p{i}", SqlDbType.NVarChar, 128) { Value = thisBatch[i] });

                    deleted = await cmd.ExecuteNonQueryAsync(ct);
                    totalDeleted += deleted;

                    if (deleted < _opt.DeleteChunk)
                        break; // bu dilimde temizlenecek kalmadı
                }
                while (true);
            }

            if (totalDeleted > 0)
                _log.LogInformation("RetentionCleaner: {n} satır silindi.", totalDeleted);
            else
                _log.LogDebug("RetentionCleaner: silinecek satır yok.");
        }

        private async Task<Dictionary<string, int>> LoadPolicyMapAsync(CancellationToken ct)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using var conn = new SqlConnection(_db.HistorianDb);
            await conn.OpenAsync(ct);

            const string sql = @"
SELECT apt.Tag, ap.RetentionDays
FROM catalog.ArchivePolicyTags apt
JOIN catalog.ArchivePolicies   ap ON ap.Id = apt.PolicyId;";

            using var cmd = new SqlCommand(sql, conn);
            using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
            {
                var tag = rd.GetString(0);
                var days = rd.GetInt32(1);
                result[tag] = days;
            }
            return result;
        }
    }

    // HistoryOptions içine Retention bölümü eklemek için küçük DTO
    public sealed class HistoryOptions
    {
        public bool Enabled { get; set; } = true;
        public int WriteIntervalSeconds { get; set; } = 1;
        public int BatchSize { get; set; } = 100;
        public int MaxQueue { get; set; } = 10000;

        public RetentionOptions? Retention { get; set; }
    }
}
