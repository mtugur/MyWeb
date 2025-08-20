using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyWeb.Runtime;
using MyWeb.Core.Runtime.Health;
using MyWeb.Core.Communication; // ICommunicationChannel için

namespace MyWeb.Runtime.Services;

/// <summary>
/// S2 Step-2a: PLC kanalına bağlanıp SampledTags listesini periyodik okur.
/// Şimdilik sonuçları loglar ve health'e iyi/kötü örnek bildirir.
/// Step-2b: SQL yazımı eklenecek (mevcut tablo/kolonlara göre).
/// </summary>
public sealed class TagSamplingService : BackgroundService
{
    private readonly ILogger<TagSamplingService> _logger;
    private readonly SamplingOptions _opts;
    private readonly IRuntimeHealthProvider _health;
    private readonly ICommunicationChannel _channel;

    public TagSamplingService(
        ILogger<TagSamplingService> logger,
        IOptions<SamplingOptions> opts,
        IRuntimeHealthProvider health,
        ICommunicationChannel channel)
    {
        _logger = logger;
        _opts = opts.Value;
        _health = health;
        _channel = channel;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TagSamplingService started. PollMs={PollMs}, Tags=[{Tags}]",
            _opts.PollMs, _opts.SampledTags == null ? "" : string.Join(", ", _opts.SampledTags));

        // Kanal bağlı değilse bağlanmayı dene (non-fatal)
        try
        {
            await _channel.ConnectAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Initial PLC connect attempt failed. Sampling will still try reads.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_opts.SampledTags != null && _opts.SampledTags.Length > 0)
                {
                    // 1) Tagleri oku (ReadMany mevcut yapınıza göre isimler DB1000 altında)
                    var names = _opts.SampledTags.ToArray();
                    var result = await _channel.ReadManyAsync(names, stoppingToken);

                    // 2) Başarılıysa health işaretle + kısa log
                    _health.ReportGoodSample();

                    // Log’u çok uzatmamak için ilk 5 tag önizleme
                    var preview = string.Join(", ", result.Take(5).Select(kv => $"{kv.Key}={kv.Value}"));
                    _logger.LogDebug("Sample OK: {Count} tags. {Preview}", result.Count, preview);

                    // TODO (Step-2b): result koleksiyonunu SQL'e yaz (UtcNow, Tag, Value, Quality)
                }
                else
                {
                    _logger.LogDebug("Sampling tick: no tags configured");
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
}
