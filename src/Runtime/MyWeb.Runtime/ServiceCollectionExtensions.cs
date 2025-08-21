using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyWeb.Core.Runtime.Health;         // IRuntimeHealthProvider
using MyWeb.Runtime.Services;            // RuntimeHealthProvider + diğer servisler
using MyWeb.Core.History;

namespace MyWeb.Runtime
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Runtime bileşenleri (Options + Sağlık + HostedServices) kaydı.
        /// </summary>
        public static IServiceCollection AddMyWebRuntime(this IServiceCollection services, IConfiguration configuration)
        {
            // Options binding
            services.Configure<RuntimeOptions>(configuration.GetSection("Runtime"));
            services.Configure<SamplingOptions>(configuration.GetSection("Sampling"));
            services.Configure<HistoryOptions>(configuration.GetSection("History"));
            services.Configure<DbConnOptions>(configuration.GetSection("ConnectionStrings"));

            // Sağlık sağlayıcı
            services.AddSingleton<IRuntimeHealthProvider, RuntimeHealthProvider>();

            // Hosted services
            services.AddSingleton<PlcConnectionWatchdog>();
            services.AddHostedService(sp => sp.GetRequiredService<PlcConnectionWatchdog>());

            services.AddSingleton<TagSamplingService>();
            
            services.AddSingleton<HistoryWriterService>();
            services.AddSingleton<IHistoryWriter>(sp => sp.GetRequiredService<HistoryWriterService>());
            services.AddHostedService(sp => sp.GetRequiredService<HistoryWriterService>());
services.AddHostedService(sp => sp.GetRequiredService<TagSamplingService>());

            services.AddHostedService(sp => sp.GetRequiredService<HistoryWriterService>());

            // NOT: IHistoryWriter kaydı, HistoryWriterService arayüzü uygulayana kadar bilinçli olarak eklenmedi.
            return services;
        }
    }
}

