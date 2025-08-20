using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyWeb.Core.History;

namespace MyWeb.Runtime.Services;

/// <summary>
/// Sampling verilerini bellek içi kuyrukta biriktirir ve periyodik olarak SQL'e yazar.
/// Şema: hist.Samples(Utc datetime2, Tag nvarchar(128), Value nvarchar(max), Quality nvarchar(16))
/// </summary>
public sealed class HistoryWriterService : BackgroundService, IHistoryWriter
{
    private readonly ILogger<HistoryWriterService> _logger;
    private readonly HistoryOptions _opts;
    private readonly string _connString;
    private readonly ConcurrentQueue<SamplePoint> _queue = new();
    private int _approxCount = 0;

    public HistoryWriterService(
        ILogger<HistoryWriterService> logger,
        IOptions<HistoryOptions> options,
        IOptions<DbConnOptions> conn) // DbConnOptions'ı aşağıda ekliyoruz
    {
        _logger = logger;
        _opts = options.Value;
        _connString = conn.Value.HistorianDb;
    }

    public void Enqueue(IEnumerable<SamplePoint> items)
    {
        foreach (var it in items)
        {
            if (_approxCount >= _opts.MaxQueue) { _queue.TryDequeue(out _); Interlocked.Decrement(ref _approxCount); }
            _queue.Enqueue(it);
            Interlocked.Increment(ref _approxCount);
        }
    }

    public void Enqueue(SamplePoint item)
    {
        if (_approxCount >= _opts.MaxQueue) { _queue.TryDequeue(out _); Interlocked.Decrement(ref _approxCount); }
        _queue.Enqueue(item);
        Interlocked.Increment(ref _approxCount);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opts.Enabled)
        {
            _logger.LogWarning("HistoryWriter disabled via config.");
            return;
        }

        _logger.LogInformation("HistoryWriter started. Interval={Interval}s, BatchSize={Batch}, MaxQueue={Max}",
            _opts.WriteIntervalSeconds, _opts.BatchSize, _opts.MaxQueue);

        await EnsureSchemaAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await FlushOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HistoryWriter flush error");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(_opts.WriteIntervalSeconds), stoppingToken); }
            catch (TaskCanceledException) { /* shutdown */ }
        }

        _logger.LogInformation("HistoryWriter stopped.");
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        var sql = @"
IF SCHEMA_ID(N'hist') IS NULL EXEC(N'CREATE SCHEMA [hist]');
IF OBJECT_ID(N'[hist].[Samples]') IS NULL
BEGIN
    CREATE TABLE [hist].[Samples](
        [Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Utc] DATETIME2(3) NOT NULL,
        [Tag] NVARCHAR(128) NOT NULL,
        [Value] NVARCHAR(MAX) NULL,
        [Quality] NVARCHAR(16) NOT NULL
    );
    CREATE INDEX IX_Samples_Utc ON [hist].[Samples]([Utc]);
    CREATE INDEX IX_Samples_TagUtc ON [hist].[Samples]([Tag], [Utc]);
END
";
        using var cn = new SqlConnection(_connString);
        await cn.OpenAsync(ct);
        using var cmd = new SqlCommand(sql, cn);
        await cmd.ExecuteNonQueryAsync(ct);
        _logger.LogInformation("History schema ensured (hist.Samples).");
    }

    private async Task FlushOnceAsync(CancellationToken ct)
    {
        if (_approxCount == 0) return;

        var list = new List<SamplePoint>(_opts.BatchSize);
        while (list.Count < _opts.BatchSize && _queue.TryDequeue(out var it))
        {
            list.Add(it);
            Interlocked.Decrement(ref _approxCount);
        }
        if (list.Count == 0) return;

        // DataTable -> SqlBulkCopy
        var table = new DataTable();
        table.Columns.Add("Utc", typeof(DateTime));
        table.Columns.Add("Tag", typeof(string));
        table.Columns.Add("Value", typeof(string));
        table.Columns.Add("Quality", typeof(string));

        foreach (var s in list)
            table.Rows.Add(s.Utc, s.Tag, s.Value, s.Quality);

        using var cn = new SqlConnection(_connString);
        await cn.OpenAsync(ct);

        using var bulk = new SqlBulkCopy(cn, SqlBulkCopyOptions.CheckConstraints, null)
        {
            DestinationTableName = "[hist].[Samples]",
            BulkCopyTimeout = 30
        };
        bulk.ColumnMappings.Clear();
        // Map source ordinals -> destination column names (Id is identity; we skip it)
        bulk.ColumnMappings.Add(0, "Utc");
        bulk.ColumnMappings.Add(1, "Tag");
        bulk.ColumnMappings.Add(2, "Value");
        bulk.ColumnMappings.Add(3, "Quality");

        await bulk.WriteToServerAsync(table, ct);

        _logger.LogInformation("History flush OK: wrote {Count} rows.", list.Count);
    }
}

/// <summary>Connection strings bind için küçük bir sınıf.</summary>
public sealed class DbConnOptions
{
    public string CatalogDb { get; set; } = string.Empty;
    public string HistorianDb { get; set; } = string.Empty;
}

