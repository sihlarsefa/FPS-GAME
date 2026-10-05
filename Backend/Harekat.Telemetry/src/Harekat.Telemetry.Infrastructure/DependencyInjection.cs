using Harekat.Telemetry.Application.Abstractions;
using Harekat.Telemetry.Infrastructure.Heatmap;
using Harekat.Telemetry.Infrastructure.Metrics;
using Harekat.Telemetry.Infrastructure.Replay;
using Harekat.Telemetry.Infrastructure.Rules;
using Harekat.Telemetry.Infrastructure.Storage;
using Harekat.Telemetry.Infrastructure.Streaming;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Harekat.Telemetry.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTelemetryInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RuleEngineOptions>(configuration.GetSection("RuleEngine"));

        services.AddSingleton<IEventStore, InMemoryEventStore>();
        services.AddSingleton<ISuspicionReportStore, InMemorySuspicionReportStore>();
        services.AddSingleton<IRiskScoreStore, InMemoryRiskScoreStore>();
        services.AddSingleton<IReviewQueue, InMemoryReviewQueue>();
        services.AddSingleton<IReplayStore, CompressedReplayStore>();
        services.AddSingleton<IHeatmapImageRenderer, HeatmapPngRenderer>();
        services.AddSingleton<IPerformanceMetrics, InMemoryPerformanceMetrics>();

        var clientErrorProvider = configuration["Storage:Provider"] ?? "InMemory";
        var hasSql = !string.IsNullOrWhiteSpace(configuration.GetConnectionString("SqlServer"))
                     || !string.IsNullOrWhiteSpace(configuration["Storage:SqlServer"]);
        if (string.Equals(clientErrorProvider, "SqlServer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(clientErrorProvider, "MSSQL", StringComparison.OrdinalIgnoreCase)
            || (hasSql && string.Equals(clientErrorProvider, "Auto", StringComparison.OrdinalIgnoreCase)))
        {
            services.AddSingleton<IClientErrorStore, SqlServerClientErrorStore>();
        }
        else
        {
            services.AddSingleton<IClientErrorStore, InMemoryClientErrorStore>();
        }

        var streamBackend = configuration["Streaming:Backend"] ?? "InMemory";
        services.AddSingleton<IStreamBackend>(sp => streamBackend.ToLowerInvariant() switch
        {
            "redis" or "redisstreams" => new RedisStreamsBackend(),
            "kafka" => new KafkaStreamBackend(),
            _ => new InMemoryStreamBackend()
        });
        services.AddSingleton<IEventStreamPublisher, EventStreamPublisher>();

        services.AddSingleton<JsonHotReloadRuleProvider>();
        services.AddSingleton<IRuleProvider>(sp => sp.GetRequiredService<JsonHotReloadRuleProvider>());
        services.AddHostedService(sp => sp.GetRequiredService<JsonHotReloadRuleProvider>());

        return services;
    }
}
