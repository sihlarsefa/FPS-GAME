using Harekat.Application.Abstractions;
using Harekat.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Harekat.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<PlayerService>();
        services.AddScoped<SquadService>();
        services.AddScoped<MatchmakingService>();
        services.AddScoped<MatchResultService>();
        services.AddScoped<GameServerService>();
        services.AddScoped<LeaderboardService>();
        services.AddScoped<FriendshipService>();
        services.AddScoped<SeasonService>();
        services.AddScoped<AchievementService>();
        services.AddScoped<CosmeticService>();
        services.AddScoped<ModerationService>();
        return services;
    }
}
