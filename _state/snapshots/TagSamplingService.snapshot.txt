using MyWeb.Core.History;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyWeb.Runtime;
using MyWeb.Core.Runtime.Health;
using MyWeb.Core.Communication; // ICommunicationChannel, TagValue

namespace MyWeb.Runtime.Services;

/// <summary>
/// S2 Step-2a (final): PLC kanalına bağlanır ve SampledTags listesini periyodik okur.
/// SiemensCommunicationChannel senkron imzalarını (Connect, ReadTagsWithQuality) kullanır.
/// Başarılı okumalarda health işaretlenir. (SQL yazımı Step-2b'de eklenecek)
/// </summary>
public sealed class TagSamplingService : BackgroundService
{
    private readonly ILogger<TagSamplingService> _logger;
    private readonly SamplingOptions _opts;
    private readonly IRuntimeHealthProvider _health;
    private readonly ICommunicationChannel _channel; private readonly IHistoryWriter _history;

    public TagSamplingService(
        ILogger<TagSamplingService> logger,
        IOptions<SamplingOptions> opts, IRuntimeHealthProvider health, ICommunicationChannel channel, IHistoryWriter history)
    {
        _logger = logger;
        _opts = opts.Value;
        _health = health;
        _channel = channel; _history = history;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TagSamplingService started. PollMs={PollMs}, Tags=[{Tags}]",
            _opts.PollMs, _opts.SampledTags == null ? "" : string.Join(", ", _opts.SampledTags));

        // İlk bağlantı (best-effort)
        TryConnect();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!HasTags(_opts.SampledTags))
                {
                    _logger.LogDebug("Sampling tick: no tags configured");
                }
                else
                {
                    var names = _opts.SampledTags!.ToArray();

                    // Kanal kopmuşsa hızlı toparlanmayı dene
                    if (!_channel.IsConnected) TryConnect();

                    // Kaliteli okuma
                    Dictionary<string, TagValue> result = _channel.ReadTagsWithQuality(names);

                    // Sağlık ve kısa log
                    _health.ReportGoodSample();
                    var preview = string.Join(", ", result.Take(5).Select(kv => $"{kv.Key}={kv.Value.Value}({kv.Value.Quality})"));
                    _logger.LogInformation("Sample OK: {Count} tags. {Preview}", result.Count, preview);

                                        // Historian kuyruğuna ekle
                    var utc = DateTime.UtcNow;
                    var rows = result.Select(kv => new SamplePoint {
                        Utc = utc,
                        Tag = kv.Key,
                        Value = kv.Value.Value?.ToString(),
                        Quality = kv.Value.Quality ?? "Good"
                    });
                    _history.Enqueue(rows);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // shutdown
            }
            catch (Exception ex)
            {
                _health.ReportError();
                _logger.LogError(ex, "Sampling tick error");
            }

            try
            {
                await Task.Delay(_opts.PollMs, stoppingToken);
            }
            catch (TaskCanceledException) { /* shutdown */ }
        }

        _logger.LogInformation("TagSamplingService stopped.");
    }

    private void TryConnect()
    {
        try
        {
            _channel.Connect(); // senkron
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PLC connect attempt failed; sampling will retry next tick.");
        }
    }

    private static bool HasTags(string[]? arr) => arr != null && arr.Length > 0;
}


