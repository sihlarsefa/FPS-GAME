using Harekat.Application.Abstractions;
using Harekat.Application.Common;
using Harekat.Application.Dtos;
using Harekat.Domain.Catalogs;
using Harekat.Domain.Entities;
using Harekat.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Harekat.Application.Services;

public sealed class MatchResultService
{
    private readonly IMatchRepository _matches;
    private readonly IPlayerRepository _players;
    private readonly ISquadRepository _squads;
    private readonly IGameServerRepository _servers;
    private readonly ISeasonRepository _seasons;
    private readonly IUnitOfWork _uow;
    private readonly AchievementService _achievements;
    private readonly ILogger<MatchResultService> _logger;

    public MatchResultService(
        IMatchRepository matches,
        IPlayerRepository players,
        ISquadRepository squads,
        IGameServerRepository servers,
        ISeasonRepository seasons,
        IUnitOfWork uow,
        AchievementService achievements,
        ILogger<MatchResultService> logger)
    {
        _matches = matches;
        _players = players;
        _squads = squads;
        _servers = servers;
        _seasons = seasons;
        _uow = uow;
        _achievements = achievements;
        _logger = logger;
    }

    public async Task<MatchResultResponse> SubmitResultAsync(
        Guid matchId,
        MatchResultRequest request,
        Guid? serverId,
        CancellationToken ct = default)
    {
        var match = await _matches.GetByIdAsync(matchId, ct)
                    ?? throw new AppException("Maç bulunamadı.", 404, ErrorCodes.NotFound);

        if (match.Status == MatchStatus.Completed)
            throw new AppException("Maç sonucu zaten işlenmiş.", 409, ErrorCodes.Conflict);

        if (serverId is not null && match.GameServerId is not null && match.GameServerId != serverId)
            throw new AppException("Sunucu bu maça atanmamış.", 403, ErrorCodes.Forbidden);

        var teamCount = Math.Max(match.Teams.Count, request.Teams.Count);
        var awards = new List<XpAwardDto>();

        foreach (var teamResult in request.Teams)
        {
            var isWin = teamResult.Placement == 1;
            var slot = match.Teams.FirstOrDefault(t => t.SquadId == teamResult.SquadId);
            if (slot is not null)
                slot.Placement = teamResult.Placement;

            foreach (var pr in teamResult.Players)
            {
                var player = await _players.GetByIdAsync(pr.PlayerId, ct);
                if (player is null) continue;

                var xp = RankCatalog.CalculateMatchXp(
                    pr.Kills, pr.Headshots, teamCount, teamResult.Placement, isWin);

                player.Stats.Matches++;
                player.Stats.Kills += pr.Kills;
                player.Stats.Headshots += pr.Headshots;
                player.Stats.TotalDamage += pr.Damage;
                if (isWin) player.Stats.Wins++;
                if (player.Stats.BestPlacement == 0 || teamResult.Placement < player.Stats.BestPlacement)
                    player.Stats.BestPlacement = teamResult.Placement;
                if (pr.SurvivalSeconds > player.Stats.LongestSurvivalSeconds)
                    player.Stats.LongestSurvivalSeconds = pr.SurvivalSeconds;

                player.Stats.Experience += xp;
                player.Stats.Rank = RankCatalog.RankForXp(player.Stats.Experience);
                player.SeasonXp += xp;

                var oldElo = player.EloRating;
                player.EloRating = EloCalculator.Update(player.EloRating, teamResult.Placement, teamCount, pr.Kills);

                // Achievement metrics
                Bump(player, "kills", pr.Kills);
                Bump(player, "headshots", pr.Headshots);
                Bump(player, "matches", 1);
                if (isWin) Bump(player, "wins", 1);
                Bump(player, "damage", (int)pr.Damage);
                if (pr.SurvivalSeconds > 0)
                    player.AchievementProgress["survival"] = Math.Max(
                        player.AchievementProgress.GetValueOrDefault("survival"), (int)pr.SurvivalSeconds);
                if (teamResult.Placement <= 3) Bump(player, "top3", 1);
                Bump(player, "season_xp", xp);
                player.AchievementProgress["rank"] = (int)player.Stats.Rank;
                player.AchievementProgress["elo"] = player.EloRating;
                Bump(player, "region_matches", 1);
                if (isWin && (slot?.BotCount > 0 || match.Teams.Any(t => t.SquadId == Guid.Empty)))
                    Bump(player, "bot_wins", 1);

                if (slot is not null && slot.SquadId != Guid.Empty)
                {
                    var squad = await _squads.GetByIdAsync(slot.SquadId, ct);
                    if (squad is not null && squad.LeaderId == player.Id)
                        Bump(player, "leader_matches", 1);
                }

                var newlyUnlocked = _achievements.EvaluateAndUnlock(player);
                UnlockCosmeticsByXp(player);

                await _players.UpdateAsync(player, ct);
                awards.Add(new XpAwardDto(player.Id, xp, player.Stats.Rank, player.EloRating, newlyUnlocked));
                _logger.LogDebug("XP {Xp} Elo {Old}->{New} for {Player}", xp, oldElo, player.EloRating, player.Username);
            }

            if (teamResult.SquadId != Guid.Empty)
            {
                var squad = await _squads.GetByIdAsync(teamResult.SquadId, ct);
                if (squad is not null)
                {
                    squad.IsInMatch = false;
                    squad.ReadyMemberIds.Clear();
                    await _squads.UpdateAsync(squad, ct);
                }
            }
        }

        match.Status = MatchStatus.Completed;
        match.CompletedAt = DateTimeOffset.UtcNow;

        if (match.GameServerId is Guid gsId)
        {
            var server = await _servers.GetByIdAsync(gsId, ct);
            if (server is not null)
            {
                server.Status = GameServerStatus.Ready;
                server.CurrentMatchId = null;
                server.CurrentPlayers = 0;
                await _servers.UpdateAsync(server, ct);
            }
        }

        await _matches.UpdateAsync(match, ct);
        await _uow.SaveChangesAsync(ct);
        return new MatchResultResponse(match.Id, awards);
    }

    private static void Bump(Player player, string key, int delta)
    {
        player.AchievementProgress.TryGetValue(key, out var cur);
        player.AchievementProgress[key] = cur + delta;
    }

    private static void UnlockCosmeticsByXp(Player player)
    {
        foreach (var item in CosmeticCatalog.All)
        {
            if (player.Stats.Experience >= item.UnlockXp && !player.OwnedCosmetics.Contains(item.Id))
                player.OwnedCosmetics.Add(item.Id);
        }
        player.AchievementProgress["cosmetics"] = player.OwnedCosmetics.Count;
    }
}

public static class EloCalculator
{
    /// <summary>Basit placement + kill tabanlı Elo güncellemesi (TrueSkill benzeri basitleştirilmiş).</summary>
    public static int Update(int currentElo, int placement, int teamCount, int kills)
    {
        var expected = 1.0 / (1.0 + Math.Pow(10, (1500 - currentElo) / 400.0));
        var score = 1.0 - (placement - 1) / (double)Math.Max(1, teamCount - 1);
        var killBonus = Math.Min(kills, 10) * 2;
        var delta = (int)Math.Round(32 * (score - expected) + killBonus);
        return Math.Clamp(currentElo + delta, 100, 3000);
    }
}

public sealed class GameServerService
{
    private readonly IGameServerRepository _servers;
    private readonly IPasswordHasher _hasher;
    private readonly IUnitOfWork _uow;

    public GameServerService(IGameServerRepository servers, IPasswordHasher hasher, IUnitOfWork uow)
    {
        _servers = servers;
        _hasher = hasher;
        _uow = uow;
    }

    public async Task<ServerDto> RegisterAsync(RegisterServerRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ServerKey) || request.ServerKey.Length < 16)
            throw new AppException("Server key en az 16 karakter olmalı.", 400, ErrorCodes.Validation);

        var server = new GameServer
        {
            Host = request.Host,
            Port = request.Port,
            Region = request.Region.ToLowerInvariant(),
            ServerKeyHash = _hasher.Hash(request.ServerKey),
            MaxPlayers = request.MaxPlayers,
            Status = GameServerStatus.Ready
        };
        await _servers.AddAsync(server, ct);
        await _uow.SaveChangesAsync(ct);
        return Mapper.ToDto(server);
    }

    public async Task<ServerDto> HeartbeatAsync(ServerHeartbeatRequest request, CancellationToken ct = default)
    {
        var server = await _servers.GetByIdAsync(request.ServerId, ct)
                     ?? throw new AppException("Sunucu bulunamadı.", 404, ErrorCodes.NotFound);

        if (!_hasher.Verify(request.ServerKey, server.ServerKeyHash))
            throw new AppException("Geçersiz server key.", 401, ErrorCodes.Unauthorized);

        server.LastHeartbeatAt = DateTimeOffset.UtcNow;
        server.CurrentPlayers = request.CurrentPlayers;
        if (Enum.TryParse<GameServerStatus>(request.Status, true, out var status))
            server.Status = status;

        await _servers.UpdateAsync(server, ct);
        await _uow.SaveChangesAsync(ct);
        return Mapper.ToDto(server);
    }

    public async Task<GameServer?> AuthenticateServerAsync(Guid serverId, string serverKey, CancellationToken ct = default)
    {
        var server = await _servers.GetByIdAsync(serverId, ct);
        if (server is null) return null;
        return _hasher.Verify(serverKey, server.ServerKeyHash) ? server : null;
    }

    public async Task<IReadOnlyList<ServerDto>> ListAsync(CancellationToken ct = default)
    {
        var all = await _servers.GetAllAsync(ct);
        return all.Select(Mapper.ToDto).ToList();
    }
}

public sealed class LeaderboardService
{
    private readonly IPlayerRepository _players;
    private readonly ISeasonRepository _seasons;

    public LeaderboardService(IPlayerRepository players, ISeasonRepository seasons)
    {
        _players = players;
        _seasons = seasons;
    }

    public async Task<IReadOnlyList<LeaderboardEntryDto>> GetAsync(string metric = "experience", int take = 50, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 100);
        var players = await _players.GetLeaderboardAsync(metric.ToLowerInvariant(), take, ct);
        return players.Select((p, i) => new LeaderboardEntryDto(
            i + 1,
            p.Id,
            p.Username,
            p.Stats.Rank,
            metric switch
            {
                "kills" => p.Stats.Kills,
                "wins" => p.Stats.Wins,
                "elo" => p.EloRating,
                "headshots" => p.Stats.Headshots,
                "season" => p.SeasonXp,
                _ => p.Stats.Experience
            },
            p.EloRating)).ToList();
    }

    public async Task<IReadOnlyList<LeaderboardEntryDto>> GetSeasonAsync(int? seasonNumber, int take = 50, CancellationToken ct = default)
    {
        var season = seasonNumber.HasValue
            ? await _seasons.GetByNumberAsync(seasonNumber.Value, ct)
            : await _seasons.GetActiveAsync(ct);
        if (season is null)
            return [];

        var players = await _players.GetSeasonLeaderboardAsync(season.Number, take, ct);
        return players.Select((p, i) => new LeaderboardEntryDto(
            i + 1, p.Id, p.Username, p.Stats.Rank, p.SeasonXp, p.EloRating)).ToList();
    }
}
