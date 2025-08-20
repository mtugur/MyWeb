using Microsoft.Extensions.Options;
using MyWeb.Core.History;
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
        // Connection strings binding
        var conn = new Services.DbConnOptions();
        configuration.GetSection("ConnectionStrings").Bind(conn);
        services.AddSingleton(conn);

        // History writer (queue + background flush)
        services.AddSingleton<IHistoryWriter, HistoryWriterService>();
        services.AddHostedService(sp => (HistoryWriterService)sp.GetRequiredService<IHistoryWriter>());

        return services;
    }
}

