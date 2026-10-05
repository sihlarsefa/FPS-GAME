using Harekat.Telemetry.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Harekat.Telemetry.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTelemetryApplication(this IServiceCollection services)
    {
        services.AddSingleton<EventIngestionService>();
        services.AddSingleton<SuspicionAnalysisService>();
        services.AddSingleton<HeatmapService>();
        services.AddSingleton<WeaponBalanceService>();
        services.AddSingleton<RiskScoreQueryService>();
        services.AddSingleton<ReplayService>();
        return services;
    }
}
