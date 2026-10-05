using System.ComponentModel.DataAnnotations;
using Harekat.DiscordBot.Configuration;
using Harekat.DiscordBot.Domain;
using Harekat.DiscordBot.Services;
using Microsoft.Extensions.Options;

namespace Harekat.DiscordBot.Webhooks;

public sealed class MatchResultWebhookRequest
{
    [Required]
    public string MatchId { get; set; } = "";

    [Required]
    public string WinningSquadName { get; set; } = "";

    public List<string> WinningMembers { get; set; } = [];

    [Range(0, 10_000)]
    public int WinningTeamKills { get; set; }

    [Range(1, 64)]
    public int TeamCount { get; set; } = 1;

    public string? MapName { get; set; }

    /// <summary>Opsiyonel: kazanan üyelerin XP güncellemesi.</summary>
    public List<PlayerXpGainDto>? XpGains { get; set; }
}

public sealed class PlayerXpGainDto
{
    [Required]
    public string PlayerId { get; set; } = "";

    [Range(0, 1_000_000)]
    public int Xp { get; set; }
}

public sealed class RankPromotionWebhookRequest
{
    [Required]
    public string PlayerId { get; set; } = "";

    [Range(0, 1_000_000)]
    public int XpGain { get; set; }
}

public static class WebhookEndpoints
{
    public static IEndpointRouteBuilder MapHarekatWebhooks(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/webhooks")
            .WithTags("Webhooks");

        group.MapPost("/match-result", HandleMatchResultAsync);
        group.MapPost("/rank-promotion", HandleRankPromotionAsync);
        group.MapGet("/health", () => Results.Ok(new { status = "ok", service = "harekat-discord-bot" }));
        group.MapGet("/metrics", (PerformanceMetrics metrics) => Results.Ok(metrics.Snapshot()));

        return app;
    }

    private static async Task<IResult> HandleMatchResultAsync(
        MatchResultWebhookRequest body,
        HttpRequest request,
        IMatchAnnouncementService announcements,
        IProfileService profiles,
        IRankSyncService rankSync,
        IAnnouncementSink sink,
        IOptions<DiscordBotOptions> options,
        CancellationToken ct)
    {
        if (!IsAuthorized(request, options.Value))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(body.MatchId) || string.IsNullOrWhiteSpace(body.WinningSquadName))
            return Results.BadRequest(new { error = "MatchId ve WinningSquadName zorunlu." });

        if (body.TeamCount < 1)
            return Results.BadRequest(new { error = "TeamCount en az 1 olmalı." });

        var announcement = new MatchResultAnnouncement
        {
            MatchId = body.MatchId.Trim(),
            WinningSquadName = body.WinningSquadName.Trim(),
            WinningMembers = body.WinningMembers.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m.Trim()).ToArray(),
            WinningTeamKills = body.WinningTeamKills,
            TeamCount = body.TeamCount,
            MapName = body.MapName
        };

        await announcements.AnnounceWinnerAsync(announcement, ct).ConfigureAwait(false);

        var promotions = new List<object>();
        if (body.XpGains is { Count: > 0 })
        {
            foreach (var gain in body.XpGains)
            {
                var result = await profiles.ApplyXpAndPromoteAsync(gain.PlayerId, gain.Xp, ct).ConfigureAwait(false);
                if (result is null)
                    continue;

                if (result.Promoted)
                {
                    var msg = rankSync.FormatPromotionMessage(result);
                    await sink.PublishAsync("rank-promo", msg, ct).ConfigureAwait(false);
                }

                promotions.Add(new
                {
                    result.DisplayName,
                    previous = result.PreviousRank.ToString(),
                    next = result.NewRank.ToString(),
                    result.Promoted
                });
            }
        }

        return Results.Ok(new { announced = true, promotions });
    }

    private static async Task<IResult> HandleRankPromotionAsync(
        RankPromotionWebhookRequest body,
        HttpRequest request,
        IProfileService profiles,
        IRankSyncService rankSync,
        IAnnouncementSink sink,
        IOptions<DiscordBotOptions> options,
        CancellationToken ct)
    {
        if (!IsAuthorized(request, options.Value))
            return Results.Unauthorized();

        var result = await profiles.ApplyXpAndPromoteAsync(body.PlayerId, body.XpGain, ct).ConfigureAwait(false);
        if (result is null)
            return Results.NotFound(new { error = "Oyuncu bulunamadı." });

        if (result.Promoted)
            await sink.PublishAsync("rank-promo", rankSync.FormatPromotionMessage(result), ct).ConfigureAwait(false);

        var guildId = options.Value.GuildId;
        var player = await profiles.GetByNameAsync(result.DisplayName, ct).ConfigureAwait(false);
        if (result.Promoted && guildId is > 0 && player?.DiscordUserId is ulong discordId)
            await rankSync.SyncPlayerAsync(guildId.Value, discordId, result.NewRank, ct).ConfigureAwait(false);

        return Results.Ok(new
        {
            result.DisplayName,
            previous = result.PreviousRank.ToString(),
            next = result.NewRank.ToString(),
            result.Experience,
            result.Promoted
        });
    }

    internal static bool IsAuthorized(HttpRequest request, DiscordBotOptions options)
    {
        var expected = options.WebhookApiKey;
        if (string.IsNullOrWhiteSpace(expected))
            return true;

        if (!request.Headers.TryGetValue("X-Api-Key", out var provided))
            return false;

        return string.Equals(provided.ToString(), expected, StringComparison.Ordinal);
    }
}
