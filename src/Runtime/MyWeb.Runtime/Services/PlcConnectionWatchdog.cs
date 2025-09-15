using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyWeb.Core.Runtime.Health;

namespace MyWeb.Runtime.Services;

/// <summary>
/// Step-2: PLC erişilebilirliğini TCP/102 üzerinden yoklar (timeout'lu).
/// Başarılı denemede GoodSample, başarısızda Error raporlar.
/// Health eşikleri Step-1'deki gibi LastGoodSample süresine göre belirlenir.
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
        _logger.LogInformation("PlcConnectionWatchdog v2 started. Probe={Ip}:{Port}, Timeout={Timeout}ms, Heartbeat={Heartbeat}ms",
            _opts.PlcIp, _opts.PlcProbePort, _opts.ProbeTimeoutMs, _opts.HeartbeatMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 1) TCP probe
                bool ok = await ProbeAsync(_opts.PlcIp, _opts.PlcProbePort, _opts.ProbeTimeoutMs, stoppingToken);
                if (ok)
                {
                    _health.ReportGoodSample();
                }
                else
                {
                    _health.ReportError();
                }

                // 2) Health sınıflandırması (LastGoodSample yaşına göre)
                var snap = _health.GetSnapshot();
                var now = DateTime.UtcNow;
                var last = snap.LastGoodSampleUtc ?? DateTime.MinValue;
                var ageMs = last == DateTime.MinValue ? int.MaxValue : (int)(now - last).TotalMilliseconds;

                if (ageMs >= _opts.HealthUnhealthyAfterMs)
                {
                    _health.SetStatus(HealthStatus.Unhealthy, $"No good samples for {ageMs} ms");
                }
                else if (ageMs >= _opts.HealthDegradedAfterMs)
                {
                    _health.SetStatus(HealthStatus.Degraded, $"Stale samples for {ageMs} ms");
                }
                else
                {
                    _health.SetStatus(HealthStatus.Healthy, "Fresh");
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

    private static async Task<bool> ProbeAsync(string host, int port, int timeoutMs, CancellationToken ct)
    {
        using (var client = new TcpClient())
        {
            var connectTask = client.ConnectAsync(host, port);
            var delayTask = Task.Delay(timeoutMs, ct);

            var winner = await Task.WhenAny(connectTask, delayTask);
            if (winner == delayTask)
            {
                // timeout
                return false;
            }

            // ConnectAsync tamamlandı ama gerçekten bağlanmış mı?
            return client.Connected;
        }
    }
}
