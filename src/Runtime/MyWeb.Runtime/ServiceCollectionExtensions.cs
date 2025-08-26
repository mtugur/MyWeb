using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyWeb.Core.History;                 // IHistoryWriter
using MyWeb.Core.Runtime.Health;          // IRuntimeHealthProvider
using MyWeb.Runtime.History;              // <-- V2 Writer + Options buradan
using MyWeb.Runtime.Services;             // Watchdog, Sampling, RetentionCleaner (bunlar Services altında)

namespace MyWeb.Runtime
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>Runtime bileşenleri (Options + Health + HostedServices) kaydı.</summary>
        public static IServiceCollection AddMyWebRuntime(this IServiceCollection services, IConfiguration configuration)
        {
            // ---- Options binding ----
            services.AddOptions<RuntimeOptions>().Bind(configuration.GetSection("Runtime"));
            services.AddOptions<SamplingOptions>().Bind(configuration.GetSection("Sampling"));

            // V2 Writer Options (HistoryWriterOptions) -> "History" section’dan
            services.AddOptions<HistoryWriterOptions>().Bind(configuration.GetSection("History"));

            // Retention (temizlik) opsiyonları
            services.AddOptions<RetentionOptions>().Bind(configuration.GetSection("History:Retention"));

            // Connection strings (gerekiyorsa diğer servislerde)
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

            // Historian yazıcı: V2 sınıfını kullan (History namespace’inden)
            services.AddSingleton<HistoryWriterService>(); // MyWeb.Runtime.History.HistoryWriterService
            services.AddSingleton<IHistoryWriter, MyWeb.Runtime.Services.NullHistoryWriter>();
            services.AddHostedService(sp => sp.GetRequiredService<HistoryWriterService>());

            // İstersen V2 writer’ı IHistoryWriter olarak da expose edebiliriz
            // (şimdilik ihtiyaç yoksa yoruma da çekebilirsin)
            // services.AddSingleton<IHistoryWriter>(sp => sp.GetRequiredService<HistoryWriterService>());

            // Retention/temizlik servisi
            services.AddSingleton<RetentionCleanerService>();
            services.AddHostedService(sp => sp.GetRequiredService<RetentionCleanerService>());

            return services;
        }
    }
}
