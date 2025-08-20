using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyWeb.Core.Runtime.Health;
using MyWeb.Runtime;
using MyWeb.Runtime.Services;

namespace MyWeb.Runtime;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Runtime + Sampling servis kayıtları.
    /// Program.cs içinde: builder.Services.AddMyWebRuntime(builder.Configuration);
    /// </summary>
    public static IServiceCollection AddMyWebRuntime(this IServiceCollection services, IConfiguration configuration)
    {
        // Runtime (watchdog) ayarları
        services.Configure<RuntimeOptions>(configuration.GetSection("Runtime"));
        services.AddSingleton<IRuntimeHealthProvider, RuntimeHealthProvider>();
        services.AddHostedService<PlcConnectionWatchdog>();

        // Sampling ayarları
        services.Configure<SamplingOptions>(configuration.GetSection("Sampling"));
        services.AddHostedService<TagSamplingService>();

        return services;
    }
}
