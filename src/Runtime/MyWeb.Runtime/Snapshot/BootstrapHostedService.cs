using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyWeb.Runtime.Snapshot
{
    /// <summary>
    /// BootstrapRunner'ı uygulama başlangıcında bir defa çalıştırır.
    /// </summary>
    public sealed class BootstrapHostedService : IHostedService
    {
        private readonly BootstrapRunner _runner;
        private readonly ILogger<BootstrapHostedService> _log;

        public BootstrapHostedService(BootstrapRunner runner, ILogger<BootstrapHostedService> log)
        {
            _runner = runner;
            _log = log;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try { await _runner.TryRunAsync(cancellationToken); }
            catch (Exception ex) { _log.LogError(ex, "Bootstrap çalışırken hata."); }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
