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
    /// Runtime servisleri ve Watchdog kaydı.
    /// Program.cs içinde: builder.Services.AddMyWebRuntime(builder.Configuration);
    /// </summary>
    public static IServiceCollection AddMyWebRuntime(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RuntimeOptions>(configuration.GetSection("Runtime"));
        services.AddSingleton<IRuntimeHealthProvider, RuntimeHealthProvider>();
        services.AddHostedService<PlcConnectionWatchdog>(); // Step-2’de reconnect eklenecek
        return services;
    }
}
