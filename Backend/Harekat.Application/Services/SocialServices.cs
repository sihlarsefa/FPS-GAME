using Harekat.Application.Abstractions;
using Harekat.Application.Common;
using Harekat.Application.Dtos;
using Harekat.Domain.Catalogs;
using Harekat.Domain.Entities;

namespace Harekat.Application.Services;

public sealed class FriendshipService
{
    private readonly IFriendshipRepository _friends;
    private readonly IPlayerRepository _players;
    private readonly IUnitOfWork _uow;

    public FriendshipService(IFriendshipRepository friends, IPlayerRepository players, IUnitOfWork uow)
    {
        _friends = friends;
        _players = players;
        _uow = uow;
    }

    public async Task<FriendshipDto> SendRequestAsync(Guid fromId, Guid toId, CancellationToken ct = default)
    {
        if (fromId == toId)
            throw new AppException("Kendinize istek gönderemezsiniz.", 400, ErrorCodes.Validation);

        _ = await _players.GetByIdAsync(toId, ct)
            ?? throw new AppException("Oyuncu bulunamadı.", 404, ErrorCodes.NotFound);

        var existing = await _friends.GetBetweenAsync(fromId, toId, ct);
        if (existing is not null)
            throw new AppException("Zaten bir arkadaşlık kaydı var.", 409, ErrorCodes.Conflict);

        var f = new Friendship { RequesterId = fromId, AddresseeId = toId };
        await _friends.AddAsync(f, ct);
        await _uow.SaveChangesAsync(ct);

        var other = await _players.GetByIdAsync(toId, ct)!;
        return new FriendshipDto(f.Id, toId, other!.Username, f.Status.ToString(), other.IsOnline);
    }

    public async Task<FriendshipDto> AcceptAsync(Guid playerId, Guid friendshipId, CancellationToken ct = default)
    {
        var f = await _friends.GetAsync(friendshipId, ct)
                ?? throw new AppException("İstek bulunamadı.", 404, ErrorCodes.NotFound);
        if (f.AddresseeId != playerId)
            throw new AppException("Bu isteği kabul edemezsiniz.", 403, ErrorCodes.Forbidden);

        f.Status = FriendshipStatus.Accepted;
        f.AcceptedAt = DateTimeOffset.UtcNow;
        await _friends.UpdateAsync(f, ct);

        foreach (var pid in new[] { f.RequesterId, f.AddresseeId })
        {
            var p = await _players.GetByIdAsync(pid, ct);
            if (p is null) continue;
            p.AchievementProgress.TryGetValue("friends", out var c);
            p.AchievementProgress["friends"] = c + 1;
            await _players.UpdateAsync(p, ct);
        }

        await _uow.SaveChangesAsync(ct);
        var otherId = f.RequesterId == playerId ? f.AddresseeId : f.RequesterId;
        var other = await _players.GetByIdAsync(otherId, ct);
        return new FriendshipDto(f.Id, otherId, other?.Username ?? "?", f.Status.ToString(), other?.IsOnline ?? false);
    }

    public async Task<IReadOnlyList<FriendshipDto>> ListAsync(Guid playerId, CancellationToken ct = default)
    {
        var list = await _friends.GetForPlayerAsync(playerId, ct);
        var result = new List<FriendshipDto>();
        foreach (var f in list.Where(x => x.Status is FriendshipStatus.Accepted or FriendshipStatus.Pending))
        {
            var otherId = f.RequesterId == playerId ? f.AddresseeId : f.RequesterId;
            var other = await _players.GetByIdAsync(otherId, ct);
            result.Add(new FriendshipDto(f.Id, otherId, other?.Username ?? "?", f.Status.ToString(), other?.IsOnline ?? false));
        }
        return result;
    }
}

public sealed class SeasonService
{
    private readonly ISeasonRepository _seasons;
    private readonly IPlayerRepository _players;
    private readonly IUnitOfWork _uow;

    public SeasonService(ISeasonRepository seasons, IPlayerRepository players, IUnitOfWork uow)
    {
        _seasons = seasons;
        _players = players;
        _uow = uow;
    }

    public async Task<SeasonDto?> GetActiveAsync(CancellationToken ct = default)
    {
        var s = await _seasons.GetActiveAsync(ct);
        return s is null ? null : new SeasonDto(s.Number, s.Name, s.StartsAt, s.EndsAt, s.IsActive);
    }

    public async Task ArchiveSeasonAsync(int seasonNumber, CancellationToken ct = default)
    {
        var players = await _players.GetSeasonLeaderboardAsync(seasonNumber, 100, ct);
        var placement = 1;
        foreach (var p in players)
        {
            var badge = placement switch
            {
                1 => "season_gold",
                2 => "season_silver",
                3 => "season_bronze",
                <= 10 => "season_top10",
                _ => "season_participant"
            };
            await _seasons.AddArchiveAsync(new SeasonArchiveEntry
            {
                SeasonNumber = seasonNumber,
                PlayerId = p.Id,
                Username = p.Username,
                SeasonXp = p.SeasonXp,
                Placement = placement++,
                RewardBadge = badge
            }, ct);

            if (!p.OwnedCosmetics.Contains(badge))
                p.OwnedCosmetics.Add(badge);
            p.SeasonXp = 0;
            await _players.UpdateAsync(p, ct);
        }
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SeasonArchiveDto>> GetArchiveAsync(int season, CancellationToken ct = default)
    {
        var entries = await _seasons.GetArchiveAsync(season, ct);
        return entries.Select(e => new SeasonArchiveDto(e.SeasonNumber, e.PlayerId, e.Username, e.SeasonXp, e.Placement, e.RewardBadge)).ToList();
    }
}

public sealed class AchievementService
{
    private readonly IPlayerRepository? _players;
    private readonly IUnitOfWork? _uow;

    public AchievementService() { }

    public AchievementService(IPlayerRepository players, IUnitOfWork uow)
    {
        _players = players;
        _uow = uow;
    }

    public IReadOnlyList<AchievementDto> ListForPlayer(Player player)
    {
        return AchievementCatalog.All.Select(d =>
        {
            var progress = player.AchievementProgress.GetValueOrDefault(d.Metric);
            var unlocked = player.UnlockedAchievements.Contains(d.Id);
            return new AchievementDto(d.Id, d.Title, d.Description, d.Target, progress, unlocked);
        }).ToList();
    }

    public List<string> EvaluateAndUnlock(Player player)
    {
        var newly = new List<string>();
        foreach (var def in AchievementCatalog.All)
        {
            if (player.UnlockedAchievements.Contains(def.Id))
                continue;
            var progress = player.AchievementProgress.GetValueOrDefault(def.Metric);
            if (progress >= def.Target)
            {
                player.UnlockedAchievements.Add(def.Id);
                newly.Add(def.Id);
            }
        }
        return newly;
    }

    /// <summary>
    /// İstemci ilerleme senkronu: metrikleri max ile birleştirir; maç sonucundan türetilebilen
    /// (matches/wins/kills/headshots) sunucu CareerStats değerinin altına düşürülemez.
    /// </summary>
    public async Task<AchievementSyncResult> SyncAsync(Guid playerId, AchievementSyncRequest request, CancellationToken ct = default)
    {
        if (_players is null || _uow is null)
            throw new InvalidOperationException("AchievementService DI ile oluşturulmalı.");

        var player = await _players.GetByIdAsync(playerId, ct)
                     ?? throw new AppException("Oyuncu bulunamadı.", 404, ErrorCodes.NotFound);

        // Sunucu otoriteli metrikler — istemci bunları düşüremez.
        player.AchievementProgress["matches"] = Math.Max(
            player.AchievementProgress.GetValueOrDefault("matches"), player.Stats.Matches);
        player.AchievementProgress["wins"] = Math.Max(
            player.AchievementProgress.GetValueOrDefault("wins"), player.Stats.Wins);
        player.AchievementProgress["kills"] = Math.Max(
            player.AchievementProgress.GetValueOrDefault("kills"), player.Stats.Kills);
        player.AchievementProgress["headshots"] = Math.Max(
            player.AchievementProgress.GetValueOrDefault("headshots"), player.Stats.Headshots);
        player.AchievementProgress["rank"] = (int)player.Stats.Rank;
        player.AchievementProgress["elo"] = player.EloRating;

        if (request.Progress is not null)
        {
            foreach (var (metric, value) in request.Progress)
            {
                if (string.IsNullOrWhiteSpace(metric) || value < 0)
                    continue;
                // matches/wins/kills/headshots: yalnızca yükselt
                var serverFloor = player.AchievementProgress.GetValueOrDefault(metric);
                player.AchievementProgress[metric] = Math.Max(serverFloor, value);
            }
        }

        if (request.UnlockedIds is not null)
        {
            foreach (var id in request.UnlockedIds)
            {
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                // Yalnızca katalogda olan ve hedefi karşılanan (veya UnlockXp=0 tip) id'ler.
                var def = AchievementCatalog.All.FirstOrDefault(a => a.Id == id);
                if (def is null)
                    continue;
                var progress = player.AchievementProgress.GetValueOrDefault(def.Metric);
                if (progress >= def.Target)
                    player.UnlockedAchievements.Add(id);
            }
        }

        var newly = EvaluateAndUnlock(player);
        await _players.UpdateAsync(player, ct);
        await _uow.SaveChangesAsync(ct);
        return new AchievementSyncResult(newly, ListForPlayer(player));
    }
}

public sealed class CosmeticService
{
    private readonly IPlayerRepository _players;
    private readonly IUnitOfWork _uow;

    public CosmeticService(IPlayerRepository players, IUnitOfWork uow)
    {
        _players = players;
        _uow = uow;
    }

    public async Task<IReadOnlyList<CosmeticDto>> ListAsync(Guid playerId, CancellationToken ct = default)
    {
        var player = await _players.GetByIdAsync(playerId, ct)
                     ?? throw new AppException("Oyuncu bulunamadı.", 404, ErrorCodes.NotFound);

        return CosmeticCatalog.All.Select(c => new CosmeticDto(
            c.Id,
            c.Name,
            c.Slot,
            player.OwnedCosmetics.Contains(c.Id),
            IsEquipped(player, c)
        )).ToList();
    }

    public async Task<PlayerDto> EquipAsync(Guid playerId, string cosmeticId, CancellationToken ct = default)
    {
        var player = await _players.GetByIdAsync(playerId, ct)
                     ?? throw new AppException("Oyuncu bulunamadı.", 404, ErrorCodes.NotFound);

        if (!player.OwnedCosmetics.Contains(cosmeticId))
            throw new AppException("Bu kozmetiğe sahip değilsiniz.", 403, ErrorCodes.Forbidden);

        var item = CosmeticCatalog.All.FirstOrDefault(c => c.Id == cosmeticId)
                   ?? throw new AppException("Kozmetik bulunamadı.", 404, ErrorCodes.NotFound);

        switch (item.Slot)
        {
            case "camo":
                player.EquippedCamo = item.Id;
                break;
            case "beret":
                player.EquippedBeret = item.Id;
                break;
            default:
                // armband / weapon_skin / victory_pose / emblem_frame — sahiplik yeterli; kuşanım istemci.
                break;
        }

        await _players.UpdateAsync(player, ct);
        await _uow.SaveChangesAsync(ct);
        return Mapper.ToDto(player);
    }

    private static bool IsEquipped(Player player, CosmeticCatalog.Item c) => c.Slot switch
    {
        "camo" => player.EquippedCamo == c.Id,
        "beret" => player.EquippedBeret == c.Id,
        _ => false
    };
}

public sealed class ModerationService
{
    private readonly IModerationRepository _moderation;
    private readonly IPlayerRepository _players;
    private readonly IUnitOfWork _uow;

    public ModerationService(IModerationRepository moderation, IPlayerRepository players, IUnitOfWork uow)
    {
        _moderation = moderation;
        _players = players;
        _uow = uow;
    }

    public async Task ReportAsync(Guid reporterId, ReportPlayerRequest request, CancellationToken ct = default)
    {
        if (reporterId == request.ReportedPlayerId)
            throw new AppException("Kendinizi raporlayamazsınız.", 400, ErrorCodes.Validation);

        await _moderation.AddReportAsync(new PlayerReport
        {
            ReporterId = reporterId,
            ReportedPlayerId = request.ReportedPlayerId,
            Reason = request.Reason,
            MatchId = request.MatchId
        }, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task BanAsync(Guid actorId, BanPlayerRequest request, CancellationToken ct = default)
    {
        await EnsureModeratorAsync(actorId, ct);
        var player = await _players.GetByIdAsync(request.PlayerId, ct)
                     ?? throw new AppException("Oyuncu bulunamadı.", 404, ErrorCodes.NotFound);

        player.IsBanned = true;
        player.BanReason = request.Reason;
        player.BanExpiresAt = request.DurationHours is int h
            ? DateTimeOffset.UtcNow.AddHours(h)
            : null;

        await _players.UpdateAsync(player, ct);
        await _moderation.AddAuditAsync(new AuditLogEntry
        {
            ActorId = actorId,
            Action = "moderation.ban",
            Resource = $"player:{player.Id}",
            Details = request.Reason
        }, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task MuteAsync(Guid actorId, MutePlayerRequest request, CancellationToken ct = default)
    {
        await EnsureModeratorAsync(actorId, ct);
        var player = await _players.GetByIdAsync(request.PlayerId, ct)
                     ?? throw new AppException("Oyuncu bulunamadı.", 404, ErrorCodes.NotFound);

        player.IsMuted = true;
        player.MuteExpiresAt = DateTimeOffset.UtcNow.AddHours(request.DurationHours);
        await _players.UpdateAsync(player, ct);
        await _moderation.AddAuditAsync(new AuditLogEntry
        {
            ActorId = actorId,
            Action = "moderation.mute",
            Resource = $"player:{player.Id}",
            Details = request.Reason
        }, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<PlayerReport>> GetOpenReportsAsync(CancellationToken ct = default) =>
        _moderation.GetOpenReportsAsync(ct);

    private async Task EnsureModeratorAsync(Guid actorId, CancellationToken ct)
    {
        var actor = await _players.GetByIdAsync(actorId, ct)
                    ?? throw new AppException("Yetkisiz.", 401, ErrorCodes.Unauthorized);
        if (actor.Role is not ("Moderator" or "Admin"))
            throw new AppException("Yönetici yetkisi gerekli.", 403, ErrorCodes.Forbidden);
    }
}
