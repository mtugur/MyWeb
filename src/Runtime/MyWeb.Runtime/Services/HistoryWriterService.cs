using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyWeb.Core.History;
using MyWeb.Runtime; // DbConnOptions & HistoryOptions burada

namespace MyWeb.Runtime.Services
{
    public sealed class HistoryWriterService : BackgroundService, IHistoryWriter
    {
        private readonly ILogger<HistoryWriterService> _log;
        private readonly HistoryOptions _opts;
        private readonly DbConnOptions _db;
        private readonly ConcurrentQueue<SamplePoint> _queue = new();

        public HistoryWriterService(
            ILogger<HistoryWriterService> log,
            IOptions<HistoryOptions> opts,
            IOptions<DbConnOptions> db)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _opts = opts?.Value ?? throw new ArgumentNullException(nameof(opts));
            _db = db?.Value ?? throw new ArgumentNullException(nameof(db));
        }

        // IHistoryWriter
        public void Enqueue(SamplePoint item)
        {
            if (!_opts.Enabled || item == null) return;
            if (_queue.Count >= _opts.MaxQueue) return; // backpressure
            _queue.Enqueue(item);
        }

        // IHistoryWriter
        public void Enqueue(IEnumerable<SamplePoint> items)
        {
            if (!_opts.Enabled || items == null) return;
            foreach (var it in items)
            {
                if (_queue.Count >= _opts.MaxQueue) break;
                if (it != null) _queue.Enqueue(it);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_opts.Enabled)
            {
                _log.LogInformation("HistoryWriter disabled; exiting.");
                return;
            }

            _log.LogInformation(
                "HistoryWriter started. Interval={Interval}s, BatchSize={Batch}, MaxQueue={Max}",
                _opts.WriteIntervalSeconds, _opts.BatchSize, _opts.MaxQueue);

            await EnsureSchemaAsync(stoppingToken);
            _log.LogInformation("History schema ensured (hist.Samples).");

            var delayMs = Math.Max(250, _opts.WriteIntervalSeconds * 1000);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await FlushOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    _log.LogError(ex, "HistoryWriter flush error");
                }

                try { await Task.Delay(delayMs, stoppingToken); }
                catch (TaskCanceledException) { /* shutdown */ }
            }
        }

        private async Task EnsureSchemaAsync(CancellationToken ct)
        {
            using var conn = new SqlConnection(_db.HistorianDb);
            await conn.OpenAsync(ct);

            const string sql = @"
IF SCHEMA_ID(N'hist') IS NULL EXEC('CREATE SCHEMA [hist]');
IF OBJECT_ID(N'[hist].[Samples]') IS NULL
BEGIN
    CREATE TABLE [hist].[Samples](
        [Id] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [Utc] DATETIME2(7) NOT NULL,
        [Tag] NVARCHAR(128) NOT NULL,
        [Value] NVARCHAR(MAX) NULL,
        [Quality] NVARCHAR(16) NOT NULL
    );
    CREATE INDEX IX_Samples_Utc ON [hist].[Samples]([Utc]);
    CREATE INDEX IX_Samples_Tag_Utc ON [hist].[Samples]([Tag],[Utc]);
END;";
            using var cmd = new SqlCommand(sql, conn) { CommandType = CommandType.Text };
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private async Task FlushOnceAsync(CancellationToken ct)
        {
            if (_queue.IsEmpty) return;

            var take = Math.Max(1, _opts.BatchSize);
            var list = new List<SamplePoint>(take);
            while (list.Count < take && _queue.TryDequeue(out var sp))
            {
                if (sp != null) list.Add(sp);
            }
            if (list.Count == 0) return;

            using var conn = new SqlConnection(_db.HistorianDb);
            await conn.OpenAsync(ct);

            // Parametreli multi-row INSERT (param sayısı limitine takılmamak için chunk)
            int total = 0;
            foreach (var chunk in Chunk(list, 200))
            {
                using var cmd = new SqlCommand { Connection = conn };

                var values = new List<string>(chunk.Count);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var row = chunk[i];
                    string pU = $"@u{i}", pT = $"@t{i}", pV = $"@v{i}", pQ = $"@q{i}";
                    values.Add($"({pU},{pT},{pV},{pQ})");

                    cmd.Parameters.AddWithValue(pU, row.Utc);
                    cmd.Parameters.AddWithValue(pT, (object)row.Tag ?? DBNull.Value);
                    cmd.Parameters.AddWithValue(pV, (object?)row.Value ?? DBNull.Value);
                    cmd.Parameters.AddWithValue(pQ, (object)row.Quality ?? DBNull.Value);
                }

                cmd.CommandText =
                    $"INSERT INTO [hist].[Samples]([Utc],[Tag],[Value],[Quality]) VALUES {string.Join(",", values)};";
                total += await cmd.ExecuteNonQueryAsync(ct);
            }

            // >>> Görünür log (Information) <<<
            _log.LogInformation("History flush OK: {Count} rows", total);
        }

        private static IEnumerable<List<T>> Chunk<T>(IReadOnlyList<T> source, int size)
        {
            for (int i = 0; i < source.Count; i += size)
                yield return source.Skip(i).Take(Math.Min(size, source.Count - i)).ToList();
        }
    }
}
