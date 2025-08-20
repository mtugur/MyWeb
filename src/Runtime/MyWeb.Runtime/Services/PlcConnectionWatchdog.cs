using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyWeb.Core.Runtime.Health;

namespace MyWeb.Runtime.Services;

/// <summary>
/// Watchdog iskeleti: Şimdilik sadece kalp atışı ve eşiklere göre status günceller.
/// Step-2'de PLC kanalı ile entegre edilip auto-reconnect eklenecek.
/// </summary>
public sealed class PlcConnectionWatchdog : BackgroundService
{
    private readonly ILogger<PlcConnectionWatchdog> _logger;
    private readonly IRuntimeHealthProvider _health;
    private readonly RuntimeOptions _opts;

    public PlcConnectionWatchdog(
        ILogger<PlcConnectionWatchdog> logger,
        IRuntimeHealthProvider health,
        IOptions<RuntimeOptions> opts)
    {
        _logger = logger;
        _health = health;
        _opts = opts.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PlcConnectionWatchdog started. Heartbeat={Heartbeat}ms", _opts.HeartbeatMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Basit health hesaplaması: son iyi örnek zamanı üzerinden
                var snap = _health.GetSnapshot();
                var now = DateTime.UtcNow;
                var last = snap.LastGoodSampleUtc ?? now;

                var deltaMs = (int)(now - last).TotalMilliseconds;

                if (deltaMs >= _opts.HealthUnhealthyAfterMs)
                {
                    _health.SetStatus(HealthStatus.Unhealthy, $"No good samples for {deltaMs} ms");
                }
                else if (deltaMs >= _opts.HealthDegradedAfterMs)
                {
                    _health.SetStatus(HealthStatus.Degraded, $"Stale samples for {deltaMs} ms");
                }
                else
                {
                    _health.SetStatus(HealthStatus.Healthy, "Fresh");
                }

                // Günlük heartbeat (çok sık değilse info yaz)
                if (deltaMs == 0 || deltaMs % (_opts.HeartbeatMs * 10) == 0)
                {
                    var s = _health.GetSnapshot();
                    _logger.LogDebug("Watchdog heartbeat: {Status} (errors={Errors})",
                        s.Status, s.ConsecutiveErrors);
                }
            }
            catch (Exception ex)
            {
                _health.ReportError();
                _logger.LogError(ex, "Watchdog tick error");
            }

            try
            {
                await Task.Delay(_opts.HeartbeatMs, stoppingToken);
            }
            catch (TaskCanceledException) { /* shutdown */ }
        }

        _logger.LogInformation("PlcConnectionWatchdog stopped.");
    }
}
