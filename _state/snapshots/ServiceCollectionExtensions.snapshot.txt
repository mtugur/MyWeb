using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyWeb.Core.Runtime.Health;          // IRuntimeHealthProvider
using MyWeb.Runtime.Services;             // Watchdog, Sampling, HistoryWriter, RetentionCleaner
using MyWeb.Core.History;                 // IHistoryWriter

namespace MyWeb.Runtime
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Runtime bileşenlerini (Options + Sağlık + Hosted Services) kaydeder.
        /// </summary>
        public static IServiceCollection AddMyWebRuntime(this IServiceCollection services, IConfiguration configuration)
        {
            // ---- Options binding ----
            services.AddOptions<RuntimeOptions>().Bind(configuration.GetSection("Runtime"));
            services.AddOptions<SamplingOptions>().Bind(configuration.GetSection("Sampling"));
            services.AddOptions<HistoryOptions>().Bind(configuration.GetSection("History"));
            services.AddOptions<RetentionOptions>().Bind(configuration.GetSection("History:Retention"));
            services.AddOptions<DbConnOptions>().Bind(configuration.GetSection("ConnectionStrings"));

            // ---- Health provider ----
            services.AddSingleton<IRuntimeHealthProvider, RuntimeHealthProvider>();

            // ---- Hosted services ----

            // PLC bağlantı watchdog
            services.AddSingleton<PlcConnectionWatchdog>();
            services.AddHostedService(sp => sp.GetRequiredService<PlcConnectionWatchdog>());

            // Tag örnekleme
            services.AddSingleton<TagSamplingService>();
            services.AddHostedService(sp => sp.GetRequiredService<TagSamplingService>());

            // Historian yazıcı (IHistoryWriter olarak da expose)
            services.AddSingleton<HistoryWriterService>();
            services.AddSingleton<IHistoryWriter>(sp => sp.GetRequiredService<HistoryWriterService>());
            services.AddHostedService(sp => sp.GetRequiredService<HistoryWriterService>());

            // Retention/temizlik servisi
            services.AddSingleton<RetentionCleanerService>();
            services.AddHostedService(sp => sp.GetRequiredService<RetentionCleanerService>());

            return services;
        }
    }
}
