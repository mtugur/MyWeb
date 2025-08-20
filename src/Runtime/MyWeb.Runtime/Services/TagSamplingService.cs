using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MyWeb.Runtime.Services;

/// <summary>
/// S2 Step-1: Örnekleme servisinin iskeleti.
/// Şimdilik sadece periyodik döngü ve yapılandırmayı loglar.
/// Step-2'de gerçek PLC okuma + SQL yazma eklenecek.
/// </summary>
public sealed class TagSamplingService : BackgroundService
{
    private readonly ILogger<TagSamplingService> _logger;
    private readonly SamplingOptions _opts;

    public TagSamplingService(ILogger<TagSamplingService> logger, IOptions<SamplingOptions> opts)
    {
        _logger = logger;
        _opts = opts.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TagSamplingService started. PollMs={PollMs}, Tags=[{Tags}]",
            _opts.PollMs, _opts.SampledTags == null ? "" : string.Join(", ", _opts.SampledTags));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Şu anlık sadece niyet/log (dry run). Step-2'de burada PLC'den okuma + SQL yazma olacak.
                if (_opts.SampledTags != null && _opts.SampledTags.Length > 0)
                {
                    _logger.LogDebug("Sampling tick: {Count} tag planned ({Preview})",
                        _opts.SampledTags.Length,
                        string.Join(", ", _opts.SampledTags.Take(5)));
                }
                else
                {
                    _logger.LogDebug("Sampling tick: no tags configured");
                }
            }
            catch (Exception ex)
            {
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
