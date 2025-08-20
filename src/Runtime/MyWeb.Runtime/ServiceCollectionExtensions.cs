using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyWeb.Core.History;
using MyWeb.Runtime.Services;

namespace MyWeb.Runtime
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Runtime bileşenlerini (Options + HostedServices) kaydeder.
        /// </summary>
        public static IServiceCollection AddMyWebRuntime(this IServiceCollection services, IConfiguration configuration)
        {
            // Options binding
            services.Configure<RuntimeOptions>(configuration.GetSection("Runtime"));
            services.Configure<SamplingOptions>(configuration.GetSection("Sampling"));
            services.Configure<HistoryOptions>(configuration.GetSection("History"));
            services.Configure<DbConnOptions>(configuration.GetSection("ConnectionStrings"));

            // Hosted services (BackgroundService)
            services.AddSingleton<PlcConnectionWatchdog>();
            services.AddHostedService(sp => sp.GetRequiredService<PlcConnectionWatchdog>());

            services.AddSingleton<TagSamplingService>();
            services.AddHostedService(sp => sp.GetRequiredService<TagSamplingService>());

            services.AddSingleton<HistoryWriterService>();
            services.AddHostedService(sp => sp.GetRequiredService<HistoryWriterService>());

            // IHistoryWriter olarak da aynı instance'ı sun (adapter pattern)
            services.AddSingleton<IHistoryWriter>(sp => sp.GetRequiredService<HistoryWriterService>());

            return services;
        }
    }
}
