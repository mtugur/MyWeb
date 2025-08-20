using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyWeb.Runtime;
using MyWeb.Core.Runtime.Health;
using MyWeb.Core.Communication; // ICommunicationChannel

namespace MyWeb.Runtime.Services;

/// <summary>
/// S2 Step-2a (uyarlanmış): PLC kanalına bağlanıp SampledTags listesini periyodik okur.
/// Kanalın async/sync imzası fark etmeksizin reflection ile çağırır.
/// Şimdilik sonuçları loglar ve health'e iyi/kötü örnek bildirir.
/// Step-2b: SQL yazımı eklenecek.
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

        // Kanal bağlantısı (best-effort)
        try
        {
            await ConnectBestEffortAsync(stoppingToken);
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
                    var names = _opts.SampledTags.ToArray();
                    var result = await ReadManyBestEffortAsync(names, stoppingToken);

                    _health.ReportGoodSample();

                    var preview = string.Join(", ", result.Take(5).Select(kv => $"{kv.Key}={kv.Value}"));
                    _logger.LogDebug("Sample OK: {Count} tags. {Preview}", result.Count, preview);

                    // TODO (Step-2b): result -> SQL
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

    // ---- Helpers: sync/async köprüleri ----

    private async Task ConnectBestEffortAsync(CancellationToken ct)
    {
        var t = _channel.GetType();

        // 1) ConnectAsync(CancellationToken)
        var mi = t.GetMethod("ConnectAsync", new[] { typeof(CancellationToken) });
        if (mi != null)
        {
            var task = (Task)mi.Invoke(_channel, new object[] { ct })!;
            await task.ConfigureAwait(false);
            return;
        }

        // 2) Connect() veya Connect(CancellationToken)
        var miNoArg = t.GetMethod("Connect", Type.EmptyTypes);
        if (miNoArg != null)
        {
            await Task.Run(() => miNoArg.Invoke(_channel, null), ct).ConfigureAwait(false);
            return;
        }

        var miCt = t.GetMethod("Connect", new[] { typeof(CancellationToken) });
        if (miCt != null)
        {
            await Task.Run(() => miCt.Invoke(_channel, new object[] { ct }), ct).ConfigureAwait(false);
            return;
        }

        // Hiçbiri yoksa uyarı
        _logger.LogWarning("No Connect/ConnectAsync method found on ICommunicationChannel implementation {Type}.", t.FullName);
    }

    private async Task<IDictionary<string, object?>> ReadManyBestEffortAsync(IEnumerable<string> names, CancellationToken ct)
    {
        var t = _channel.GetType();
        // 1) ReadManyAsync(IEnumerable<string>, CancellationToken)
        var miAsync2 = t.GetMethod("ReadManyAsync", new[] { typeof(IEnumerable<string>), typeof(CancellationToken) });
        if (miAsync2 != null)
        {
            var taskObj = (Task)miAsync2.Invoke(_channel, new object[] { names, ct })!;
            await taskObj.ConfigureAwait(false);
            // Task<T> Result erişimi
            var resultProp = taskObj.GetType().GetProperty("Result");
            return (IDictionary<string, object?>)resultProp!.GetValue(taskObj)!;
        }

        // 2) ReadManyAsync(IEnumerable<string>)
        var miAsync1 = t.GetMethod("ReadManyAsync", new[] { typeof(IEnumerable<string>) });
        if (miAsync1 != null)
        {
            var taskObj = (Task)miAsync1.Invoke(_channel, new object[] { names })!;
            await taskObj.ConfigureAwait(false);
            var resultProp = taskObj.GetType().GetProperty("Result");
            return (IDictionary<string, object?>)resultProp!.GetValue(taskObj)!;
        }

        // 3) ReadMany(IEnumerable<string>, CancellationToken)
        var miSync2 = t.GetMethod("ReadMany", new[] { typeof(IEnumerable<string>), typeof(CancellationToken) });
        if (miSync2 != null)
        {
            return await Task.Run(() => (IDictionary<string, object?>)miSync2.Invoke(_channel, new object[] { names, ct })!, ct)
                             .ConfigureAwait(false);
        }

        // 4) ReadMany(IEnumerable<string>)
        var miSync1 = t.GetMethod("ReadMany", new[] { typeof(IEnumerable<string>) });
        if (miSync1 != null)
        {
            return await Task.Run(() => (IDictionary<string, object?>)miSync1.Invoke(_channel, new object[] { names })!, ct)
                             .ConfigureAwait(false);
        }

        // 5) Hiçbiri yoksa hata
        throw new MissingMethodException($"ReadMany/ReadManyAsync not found on {_channel.GetType().FullName}");
    }
}
