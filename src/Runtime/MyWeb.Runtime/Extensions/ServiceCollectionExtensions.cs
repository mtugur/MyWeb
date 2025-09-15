using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyWeb.Runtime.Options;
using MyWeb.Runtime.Packaging;
using MyWeb.Runtime.Snapshot;
using MyWeb.Runtime.History;

namespace MyWeb.Runtime.Extensions
{
    /// <summary>
    /// Runtime DI kayıtları: paket yükleme, bootstrap, snapshot ve history writer.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMyWebRuntime(this IServiceCollection services, IConfiguration configuration)
        {
            // Options
            services.AddOptions();
            services.Configure<BootstrapOptions>(configuration.GetSection("Bootstrap"));
            services.Configure<HistoryWriterOptions>(configuration.GetSection("History"));

            // Paket yükleyici
            services.AddSingleton<IPackageLoader, ZipPackageLoader>();

            // Snapshot bileşeni ve bootstrap (CatalogSnapshotService scoped!)
            services.AddScoped<CatalogSnapshotService>();
            services.AddSingleton<BootstrapRunner>();
            services.AddHostedService<BootstrapHostedService>();

            // Historian writer
            services.AddHostedService<HistoryWriterService>();

            return services;
        }
    }
}
