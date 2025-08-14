using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyWeb.Runtime.Options;
using MyWeb.Runtime.Packaging;

namespace MyWeb.Runtime.Snapshot
{
    /// <summary>
    /// Uygulama açılışında paketi okuyup Catalog snapshot uygular.
    /// Scoped servislere erişim için IServiceScopeFactory kullanır.
    /// </summary>
    public class BootstrapRunner
    {
        private readonly ILogger<BootstrapRunner> _logger;
        private readonly IPackageLoader _loader;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptions<BootstrapOptions> _options;

        public BootstrapRunner(
            ILogger<BootstrapRunner> logger,
            IPackageLoader loader,
            IServiceScopeFactory scopeFactory,
            IOptions<BootstrapOptions> options)
        {
            _logger = logger;
            _loader = loader;
            _scopeFactory = scopeFactory;
            _options = options;
        }

        public async Task TryRunAsync(CancellationToken ct)
        {
            var pkgPath = _options.Value.PackagePath;
            if (string.IsNullOrWhiteSpace(pkgPath))
                return;

            _logger.LogInformation("Bootstrap: paket okunuyor: {Path}", pkgPath);

            var pkg = await _loader.LoadAsync(pkgPath, ct);

            using var scope = _scopeFactory.CreateScope();
            var snapshot = scope.ServiceProvider.GetRequiredService<CatalogSnapshotService>();
            var affected = await snapshot.ApplyPackageAsync(pkg, ct);

            _logger.LogInformation("Bootstrap: snapshot tamam. Değişiklik sayısı: {Count}", affected);
        }
    }
}
