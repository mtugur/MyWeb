using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyWeb.Core.Runtime.Health;
using MyWeb.Core.History;
using MyWeb.Runtime;
using MyWeb.Runtime.Services;

namespace MyWeb.Runtime;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Program.cs içinde: builder.Services.AddMyWebRuntime(builder.Configuration);
    /// Runtime (watchdog) + Sampling + History (queue+writer) kayıtları.
    /// </summary>
    public static IServiceCollection AddMyWebRuntime(this IServiceCollection services, IConfiguration configuration)
    {
        // ---- Runtime (watchdog) ----
        services.Configure<RuntimeOptions>(configuration.GetSection("Runtime"));
        services.AddSingleton<IRuntimeHealthProvider, RuntimeHealthProvider>();
        services.AddHostedService<PlcConnectionWatchdog>();

        // ---- Sampling ----
        services.Configure<SamplingOptions>(configuration.GetSection("Sampling"));
        services.AddHostedService<TagSamplingService>();

        // ---- History (queue + writer) ----
        services.Configure<HistoryOptions>(configuration.GetSection("History"));

        // ConnectionStrings -> DbConnOptions
        var conn = new DbConnOptions();
        configuration.GetSection("ConnectionStrings").Bind(conn);
        services.AddSingleton(conn);

        // Queue + background writer
        services.AddSingleton<IHistoryWriter, HistoryWriterService>();
        services.AddHostedService(sp => (HistoryWriterService)sp.GetRequiredService<IHistoryWriter>());

        return services;
    }
}
