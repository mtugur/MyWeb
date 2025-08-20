using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyWeb.Core.History;

namespace MyWeb.Runtime.Services
{
    /// <summary>
    /// Örnekleri (SamplePoint) RAM kuyruğunda toplayıp belirli aralıklarla SQL Server'a yazar.
    /// - Tablo yoksa oluşturur (hist.Samples)
    /// - Parametrik çok-satırlı INSERT kullanır (SqlBulkCopy problemlerini baypas eder)
    /// </summary>
    public sealed class HistoryWriterService : BackgroundService, IHistoryWriter {
        private readonly ILogger<HistoryWriterService> _logger;
        private readonly HistoryOptions _opts;
        private readonly string _connString;

        // RAM kuyruğu
        private readonly ConcurrentQueue<SamplePoint> _queue = new();
        private int _approxCount = 0;

        public HistoryWriterService(
            ILogger<HistoryWriterService> logger,
            IOptions<HistoryOptions> opts,
            IOptions<DbConnOptions> db)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _opts = (opts ?? throw new ArgumentNullException(nameof(opts))).Value ?? new HistoryOptions();
            var dbo = (db ?? throw new ArgumentNullException(nameof(db))).Value ?? throw new ArgumentNullException(nameof(db.Value));
            _connString = dbo.HistorianDb ?? throw new ArgumentNullException(nameof(dbo.HistorianDb));
        }

        /// <summary>Sampling servisinin çağırdığı toplu enqueue.</summary>
        public void Enqueue(IEnumerable<SamplePoint> points)
        {
            if (points == null) return;

            foreach (var p in points)
            {
                // Kuyruğu sınırlı tut
                if (_approxCount >= _opts.MaxQueue)
                    break;

                _queue.Enqueue(p);
                Interlocked.Increment(ref _approxCount);
            }
        }    public void Enqueue(SamplePoint item)
    {
        if (item == null) return;
        _queue.Enqueue(item);
    }

    public void Enqueue(IEnumerable<SamplePoint> items)
    {
        if (items == null) return;
        foreach (var it in items) { if (it != null) _queue.Enqueue(it); }
    }

(CancellationToken stoppingToken)
        {
            _logger.LogInformation("HistoryWriter started. Interval={Interval}s, BatchSize={Batch}, MaxQueue={Max}",
                _opts.WriteIntervalSeconds, _opts.BatchSize, _opts.MaxQueue);

            try
            {
                await EnsureSchemaAsync(stoppingToken);
                _logger.LogInformation("History schema ensured (hist.Samples).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EnsureSchema failed.");
                throw;
            }

            var delayMs = Math.Max(250, _opts.WriteIntervalSeconds * 1000);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await FlushOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "HistoryWriter flush error");
                }

                try
                {
                    await Task.Delay(delayMs, stoppingToken);
                }
                catch (TaskCanceledException) { /* shutting down */ }
            }
        }

        private async Task EnsureSchemaAsync(CancellationToken ct)
        {
            using var cn = new SqlConnection(_connString);
            await cn.OpenAsync(ct);

            string sql = @"
IF SCHEMA_ID(N'hist') IS NULL EXEC('CREATE SCHEMA [hist];');

IF OBJECT_ID(N'[hist].[Samples]') IS NULL
BEGIN
    CREATE TABLE [hist].[Samples](
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_hist_Samples] PRIMARY KEY,
        [Utc] DATETIME2(7) NOT NULL,
        [Tag] NVARCHAR(128) NOT NULL,
        [Value] NVARCHAR(MAX) NULL,
        [Quality] NVARCHAR(32) NULL
    );
END;";

            using var cmd = new SqlCommand(sql, cn);
            cmd.CommandType = CommandType.Text;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private async Task FlushOnceAsync(CancellationToken ct)
        {
            // Kuyruk boşsa çık
            if (_approxCount == 0) return;

            // Batch oluştur
            var list = new List<SamplePoint>(_opts.BatchSize);
            while (list.Count < _opts.BatchSize && _queue.TryDequeue(out var item))
            {
                list.Add(item);
                Interlocked.Decrement(ref _approxCount);
            }
            if (list.Count == 0) return;

            using var cn = new SqlConnection(_connString);
            await cn.OpenAsync(ct);

            // Parametrik çok-satırlı INSERT
            var sb = new StringBuilder();
            sb.Append("INSERT INTO [hist].[Samples] ([Utc],[Tag],[Value],[Quality]) VALUES ");

            using var cmd = cn.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandTimeout = 30;

            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(',');

                string pUtc = $"@Utc{i}";
                string pTag = $"@Tag{i}";
                string pVal = $"@Val{i}";
                string pQlt = $"@Qlt{i}";

                sb.Append($"({pUtc},{pTag},{pVal},{pQlt})");

                var row = list[i];

                var parUtc = cmd.CreateParameter(); parUtc.ParameterName = pUtc; parUtc.SqlDbType = SqlDbType.DateTime2; parUtc.Value = row.Utc;
                var parTag = cmd.CreateParameter(); parTag.ParameterName = pTag; parTag.SqlDbType = SqlDbType.NVarChar; parTag.Size = 128; parTag.Value = (object?)row.Tag ?? DBNull.Value;
                var parVal = cmd.CreateParameter(); parVal.ParameterName = pVal; parVal.SqlDbType = SqlDbType.NVarChar; parVal.Size = -1; parVal.Value = (object?)row.Value ?? DBNull.Value;
                var parQlt = cmd.CreateParameter(); parQlt.ParameterName = pQlt; parQlt.SqlDbType = SqlDbType.NVarChar; parQlt.Size = 32; parQlt.Value = (object?)row.Quality ?? DBNull.Value;

                cmd.Parameters.Add(parUtc);
                cmd.Parameters.Add(parTag);
                cmd.Parameters.Add(parVal);
                cmd.Parameters.Add(parQlt);
            }

            cmd.CommandText = sb.ToString();
            var affected = await cmd.ExecuteNonQueryAsync(ct);

            _logger.LogInformation("History flush OK: wrote {Count} rows.", affected);
        }
    }
}

