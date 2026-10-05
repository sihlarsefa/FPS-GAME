using System.Collections.Concurrent;
using System.Text.Json;
using Harekat.Application.Abstractions;
using Harekat.Domain.Entities;

namespace Harekat.Infrastructure.Repositories;

/// <summary>NuGet/EF yoksa veya testlerde kullanılan eşzamanlı bellek + opsiyonel JSON kalıcılık.</summary>
public sealed class InMemoryStore
{
    public ConcurrentDictionary<Guid, Player> Players { get; } = new();
    public ConcurrentDictionary<Guid, Squad> Squads { get; } = new();
    public ConcurrentDictionary<Guid, MatchTicket> Tickets { get; } = new();
    public ConcurrentDictionary<Guid, Match> Matches { get; } = new();
    public ConcurrentDictionary<Guid, GameServer> Servers { get; } = new();
    public ConcurrentDictionary<Guid, Friendship> Friendships { get; } = new();
    public ConcurrentDictionary<int, Season> Seasons { get; } = new();
    public ConcurrentBag<SeasonArchiveEntry> SeasonArchives { get; } = new();
    public ConcurrentBag<PlayerReport> Reports { get; } = new();
    public ConcurrentBag<AuditLogEntry> AuditLogs { get; } = new();
    public ConcurrentDictionary<Guid, NewsItem> NewsItems { get; } = new();
    public ConcurrentDictionary<string, ClientVersion> ClientVersions { get; } = new();

    private readonly string? _path;
    private readonly object _saveLock = new();

    public InMemoryStore(string? jsonPath = null)
    {
        _path = jsonPath;
        if (_path is not null && File.Exists(_path))
            Load();
        SeedSeason();
        SeedClientContent();
    }

    public void Save()
    {
        if (string.IsNullOrWhiteSpace(_path)) return;
        lock (_saveLock)
        {
            var dto = new PersistDto
            {
                Players = Players.Values.ToList(),
                Squads = Squads.Values.ToList(),
                Tickets = Tickets.Values.ToList(),
                Matches = Matches.Values.ToList(),
                Servers = Servers.Values.ToList(),
                Friendships = Friendships.Values.ToList(),
                Seasons = Seasons.Values.ToList(),
                SeasonArchives = SeasonArchives.ToList(),
                Reports = Reports.ToList(),
                AuditLogs = AuditLogs.ToList(),
                NewsItems = NewsItems.Values.ToList(),
                ClientVersions = ClientVersions.Values.ToList()
            };
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private void Load()
    {
        try
        {
            var json = File.ReadAllText(_path!);
            var dto = JsonSerializer.Deserialize<PersistDto>(json);
            if (dto is null) return;
            foreach (var p in dto.Players) Players[p.Id] = p;
            foreach (var s in dto.Squads) Squads[s.Id] = s;
            foreach (var t in dto.Tickets) Tickets[t.Id] = t;
            foreach (var m in dto.Matches) Matches[m.Id] = m;
            foreach (var s in dto.Servers) Servers[s.Id] = s;
            foreach (var f in dto.Friendships) Friendships[f.Id] = f;
            foreach (var s in dto.Seasons) Seasons[s.Number] = s;
            foreach (var a in dto.SeasonArchives) SeasonArchives.Add(a);
            foreach (var r in dto.Reports) Reports.Add(r);
            foreach (var a in dto.AuditLogs) AuditLogs.Add(a);
            foreach (var n in dto.NewsItems) NewsItems[n.Id] = n;
            foreach (var v in dto.ClientVersions) ClientVersions[v.Channel] = v;
        }
        catch
        {
            // corrupt file — start fresh
        }
    }

    private void SeedSeason()
    {
        if (!Seasons.IsEmpty) return;
        var season = new Season
        {
            Number = 1,
            Name = "Sezon 1 — Kuzgun Vadisi",
            StartsAt = DateTimeOffset.UtcNow.AddDays(-7),
            EndsAt = DateTimeOffset.UtcNow.AddDays(83)
        };
        Seasons[1] = season;
    }

    private void SeedClientContent()
    {
        if (ClientVersions.IsEmpty)
        {
            ClientVersions["stable"] = new ClientVersion
            {
                Channel = "stable",
                Version = "0.1.0",
                PatchUrl = "https://cdn.harekat.example/patches/0.1.0.zip",
                Sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                PatchSizeBytes = 0,
                ReleaseNotes = "İlk açık beta sürümü.",
                Mandatory = false,
                PublishedAt = DateTimeOffset.UtcNow
            };
        }

        if (NewsItems.IsEmpty)
        {
            var tr = new NewsItem
            {
                Title = "HAREKÂT açık beta başladı",
                Body = "Kuzgun Vadisi haritası ve 60 kişilik tim savaşları şimdi canlı. Launcher üzerinden güncelleyin ve OYNA'ya basın.",
                Language = "tr",
                Author = "HAREKÂT",
                IsPublished = true,
                SortOrder = 0,
                PublishedAt = DateTimeOffset.UtcNow
            };
            var en = new NewsItem
            {
                Title = "HAREKÂT open beta is live",
                Body = "Kuzgun Vadisi and 60-player squad battles are online. Update via the launcher and hit PLAY.",
                Language = "en",
                Author = "HAREKÂT",
                IsPublished = true,
                SortOrder = 0,
                PublishedAt = DateTimeOffset.UtcNow
            };
            NewsItems[tr.Id] = tr;
            NewsItems[en.Id] = en;
        }
    }

    private sealed class PersistDto
    {
        public List<Player> Players { get; set; } = [];
        public List<Squad> Squads { get; set; } = [];
        public List<MatchTicket> Tickets { get; set; } = [];
        public List<Match> Matches { get; set; } = [];
        public List<GameServer> Servers { get; set; } = [];
        public List<Friendship> Friendships { get; set; } = [];
        public List<Season> Seasons { get; set; } = [];
        public List<SeasonArchiveEntry> SeasonArchives { get; set; } = [];
        public List<PlayerReport> Reports { get; set; } = [];
        public List<AuditLogEntry> AuditLogs { get; set; } = [];
        public List<NewsItem> NewsItems { get; set; } = [];
        public List<ClientVersion> ClientVersions { get; set; } = [];
    }
}

public sealed class MemoryUnitOfWork : IUnitOfWork
{
    private readonly InMemoryStore _store;
    public MemoryUnitOfWork(InMemoryStore store) => _store = store;
    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        _store.Save();
        return Task.CompletedTask;
    }
}

public sealed class MemoryPlayerRepository : IPlayerRepository
{
    private readonly InMemoryStore _s;
    public MemoryPlayerRepository(InMemoryStore s) => _s = s;

    public Task<Player?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_s.Players.TryGetValue(id, out var p) ? Clone(p) : null);

    public Task<Player?> GetByUsernameAsync(string username, CancellationToken ct = default) =>
        Task.FromResult(_s.Players.Values.FirstOrDefault(p =>
            string.Equals(p.Username, username, StringComparison.OrdinalIgnoreCase)) is { } p ? Clone(p) : null);

    public Task<Player?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        Task.FromResult(_s.Players.Values.FirstOrDefault(p =>
            string.Equals(p.Email, email, StringComparison.OrdinalIgnoreCase)) is { } p ? Clone(p) : null);

    public Task<Player?> GetBySteamIdAsync(ulong steamId, CancellationToken ct = default) =>
        Task.FromResult(_s.Players.Values.FirstOrDefault(p => p.SteamId == steamId) is { } p ? Clone(p) : null);

    public Task<Player?> GetByRefreshTokenHashAsync(string hash, CancellationToken ct = default) =>
        Task.FromResult(_s.Players.Values.FirstOrDefault(p => p.RefreshTokenHash == hash) is { } p ? Clone(p) : null);

    public Task<IReadOnlyList<Player>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var set = ids.ToHashSet();
        IReadOnlyList<Player> list = _s.Players.Values.Where(p => set.Contains(p.Id)).Select(Clone).ToList();
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<Player>> GetLeaderboardAsync(string metric, int take, CancellationToken ct = default)
    {
        var q = _s.Players.Values.AsEnumerable();
        q = metric switch
        {
            "kills" => q.OrderByDescending(p => p.Stats.Kills),
            "wins" => q.OrderByDescending(p => p.Stats.Wins),
            "elo" => q.OrderByDescending(p => p.EloRating),
            "headshots" => q.OrderByDescending(p => p.Stats.Headshots),
            "season" => q.OrderByDescending(p => p.SeasonXp),
            _ => q.OrderByDescending(p => p.Stats.Experience)
        };
        IReadOnlyList<Player> list = q.Take(take).Select(Clone).ToList();
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<Player>> GetSeasonLeaderboardAsync(int season, int take, CancellationToken ct = default)
    {
        IReadOnlyList<Player> list = _s.Players.Values.OrderByDescending(p => p.SeasonXp).Take(take).Select(Clone).ToList();
        return Task.FromResult(list);
    }

    public Task AddAsync(Player player, CancellationToken ct = default)
    {
        _s.Players[player.Id] = Clone(player);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Player player, CancellationToken ct = default)
    {
        _s.Players[player.Id] = Clone(player);
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(CancellationToken ct = default) => Task.FromResult(_s.Players.Count);

    private static Player Clone(Player p) =>
        JsonSerializer.Deserialize<Player>(JsonSerializer.Serialize(p))!;
}

public sealed class MemorySquadRepository : ISquadRepository
{
    private readonly InMemoryStore _s;
    public MemorySquadRepository(InMemoryStore s) => _s = s;

    public Task<Squad?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_s.Squads.TryGetValue(id, out var sq) ? Clone(sq) : null);

    public Task<Squad?> GetByInviteCodeAsync(string code, CancellationToken ct = default) =>
        Task.FromResult(_s.Squads.Values.FirstOrDefault(s => s.InviteCode == code) is { } sq ? Clone(sq) : null);

    public Task<Squad?> GetByMemberIdAsync(Guid playerId, CancellationToken ct = default) =>
        Task.FromResult(_s.Squads.Values.FirstOrDefault(s => s.MemberIds.Contains(playerId)) is { } sq ? Clone(sq) : null);

    public Task AddAsync(Squad squad, CancellationToken ct = default)
    {
        _s.Squads[squad.Id] = Clone(squad);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Squad squad, CancellationToken ct = default)
    {
        _s.Squads[squad.Id] = Clone(squad);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _s.Squads.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    private static Squad Clone(Squad s) => JsonSerializer.Deserialize<Squad>(JsonSerializer.Serialize(s))!;
}

public sealed class MemoryMatchTicketRepository : IMatchTicketRepository
{
    private readonly InMemoryStore _s;
    public MemoryMatchTicketRepository(InMemoryStore s) => _s = s;

    public Task<MatchTicket?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_s.Tickets.TryGetValue(id, out var t) ? t : null);

    public Task<MatchTicket?> GetActiveBySquadIdAsync(Guid squadId, CancellationToken ct = default) =>
        Task.FromResult(_s.Tickets.Values.FirstOrDefault(t => t.SquadId == squadId && t.Status == MatchTicketStatus.Queued));

    public Task<IReadOnlyList<MatchTicket>> GetQueuedByRegionAsync(string region, CancellationToken ct = default)
    {
        IReadOnlyList<MatchTicket> list = _s.Tickets.Values
            .Where(t => t.Region == region && t.Status == MatchTicketStatus.Queued)
            .OrderBy(t => t.EnqueuedAt).ToList();
        return Task.FromResult(list);
    }

    public Task AddAsync(MatchTicket ticket, CancellationToken ct = default)
    {
        _s.Tickets[ticket.Id] = ticket;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(MatchTicket ticket, CancellationToken ct = default)
    {
        _s.Tickets[ticket.Id] = ticket;
        return Task.CompletedTask;
    }
}

public sealed class MemoryMatchRepository : IMatchRepository
{
    private readonly InMemoryStore _s;
    public MemoryMatchRepository(InMemoryStore s) => _s = s;

    public Task<Match?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_s.Matches.TryGetValue(id, out var m) ? m : null);

    public Task AddAsync(Match match, CancellationToken ct = default)
    {
        _s.Matches[match.Id] = match;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Match match, CancellationToken ct = default)
    {
        _s.Matches[match.Id] = match;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Match>> GetPendingAllocationAsync(string? region = null, CancellationToken ct = default)
    {
        IReadOnlyList<Match> list = _s.Matches.Values
            .Where(m => m.Status == MatchStatus.Allocating)
            .Where(m => string.IsNullOrWhiteSpace(region) || m.Region == region)
            .OrderBy(m => m.CreatedAt)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<int> CountQueuedTicketsAsync(CancellationToken ct = default) =>
        Task.FromResult(_s.Tickets.Values.Count(t => t.Status == MatchTicketStatus.Queued));
}

public sealed class MemoryGameServerRepository : IGameServerRepository
{
    private readonly InMemoryStore _s;
    public MemoryGameServerRepository(InMemoryStore s) => _s = s;

    public Task<GameServer?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_s.Servers.TryGetValue(id, out var g) ? g : null);

    public Task<IReadOnlyList<GameServer>> GetReadyByRegionAsync(string region, CancellationToken ct = default)
    {
        IReadOnlyList<GameServer> list = _s.Servers.Values
            .Where(s => s.Region == region && s.Status == GameServerStatus.Ready).ToList();
        return Task.FromResult(list);
    }

    public Task AddAsync(GameServer server, CancellationToken ct = default)
    {
        _s.Servers[server.Id] = server;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(GameServer server, CancellationToken ct = default)
    {
        _s.Servers[server.Id] = server;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<GameServer>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<GameServer> list = _s.Servers.Values.ToList();
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<GameServer>> GetByHostAsync(string host, CancellationToken ct = default)
    {
        IReadOnlyList<GameServer> list = _s.Servers.Values.Where(s => s.Host == host).ToList();
        return Task.FromResult(list);
    }
}

public sealed class MemoryFriendshipRepository : IFriendshipRepository
{
    private readonly InMemoryStore _s;
    public MemoryFriendshipRepository(InMemoryStore s) => _s = s;

    public Task<Friendship?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_s.Friendships.TryGetValue(id, out var f) ? f : null);

    public Task<Friendship?> GetBetweenAsync(Guid a, Guid b, CancellationToken ct = default) =>
        Task.FromResult(_s.Friendships.Values.FirstOrDefault(f =>
            (f.RequesterId == a && f.AddresseeId == b) || (f.RequesterId == b && f.AddresseeId == a)));

    public Task<IReadOnlyList<Friendship>> GetForPlayerAsync(Guid playerId, CancellationToken ct = default)
    {
        IReadOnlyList<Friendship> list = _s.Friendships.Values
            .Where(f => f.RequesterId == playerId || f.AddresseeId == playerId).ToList();
        return Task.FromResult(list);
    }

    public Task AddAsync(Friendship friendship, CancellationToken ct = default)
    {
        _s.Friendships[friendship.Id] = friendship;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Friendship friendship, CancellationToken ct = default)
    {
        _s.Friendships[friendship.Id] = friendship;
        return Task.CompletedTask;
    }
}

public sealed class MemorySeasonRepository : ISeasonRepository
{
    private readonly InMemoryStore _s;
    public MemorySeasonRepository(InMemoryStore s) => _s = s;

    public Task<Season?> GetActiveAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return Task.FromResult(_s.Seasons.Values.FirstOrDefault(s => s.StartsAt <= now && s.EndsAt > now));
    }

    public Task<Season?> GetByNumberAsync(int number, CancellationToken ct = default) =>
        Task.FromResult(_s.Seasons.TryGetValue(number, out var s) ? s : null);

    public Task<IReadOnlyList<Season>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Season> list = _s.Seasons.Values.OrderByDescending(s => s.Number).ToList();
        return Task.FromResult(list);
    }

    public Task AddArchiveAsync(SeasonArchiveEntry entry, CancellationToken ct = default)
    {
        _s.SeasonArchives.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SeasonArchiveEntry>> GetArchiveAsync(int season, CancellationToken ct = default)
    {
        IReadOnlyList<SeasonArchiveEntry> list = _s.SeasonArchives
            .Where(a => a.SeasonNumber == season).OrderBy(a => a.Placement).ToList();
        return Task.FromResult(list);
    }
}

public sealed class MemoryModerationRepository : IModerationRepository
{
    private readonly InMemoryStore _s;
    public MemoryModerationRepository(InMemoryStore s) => _s = s;

    public Task AddReportAsync(PlayerReport report, CancellationToken ct = default)
    {
        _s.Reports.Add(report);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PlayerReport>> GetOpenReportsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<PlayerReport> list = _s.Reports.Where(r => r.Status == ReportStatus.Open)
            .OrderByDescending(r => r.CreatedAt).ToList();
        return Task.FromResult(list);
    }

    public Task UpdateReportAsync(PlayerReport report, CancellationToken ct = default) => Task.CompletedTask;

    public Task AddAuditAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        _s.AuditLogs.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditLogEntry>> GetAuditAsync(int take, CancellationToken ct = default)
    {
        IReadOnlyList<AuditLogEntry> list = _s.AuditLogs.OrderByDescending(a => a.CreatedAt).Take(take).ToList();
        return Task.FromResult(list);
    }
}

public sealed class MemoryNewsRepository : INewsRepository
{
    private readonly InMemoryStore _s;
    public MemoryNewsRepository(InMemoryStore s) => _s = s;

    public Task<IReadOnlyList<NewsItem>> ListPublishedAsync(string? language, int take, CancellationToken ct = default)
    {
        var q = _s.NewsItems.Values.Where(n => n.IsPublished);
        if (!string.IsNullOrWhiteSpace(language))
            q = q.Where(n => n.Language == language);
        IReadOnlyList<NewsItem> list = q.OrderByDescending(n => n.SortOrder)
            .ThenByDescending(n => n.PublishedAt)
            .Take(take)
            .Select(Clone)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<NewsItem>> ListAllAsync(int take, CancellationToken ct = default)
    {
        IReadOnlyList<NewsItem> list = _s.NewsItems.Values
            .OrderByDescending(n => n.PublishedAt)
            .Take(take)
            .Select(Clone)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<NewsItem?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_s.NewsItems.TryGetValue(id, out var n) ? Clone(n) : null);

    public Task AddAsync(NewsItem item, CancellationToken ct = default)
    {
        _s.NewsItems[item.Id] = Clone(item);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(NewsItem item, CancellationToken ct = default)
    {
        _s.NewsItems[item.Id] = Clone(item);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _s.NewsItems.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    private static NewsItem Clone(NewsItem n) => new()
    {
        Id = n.Id,
        Title = n.Title,
        Body = n.Body,
        Language = n.Language,
        Author = n.Author,
        IsPublished = n.IsPublished,
        SortOrder = n.SortOrder,
        PublishedAt = n.PublishedAt,
        UpdatedAt = n.UpdatedAt
    };
}

public sealed class MemoryClientVersionRepository : IClientVersionRepository
{
    private readonly InMemoryStore _s;
    public MemoryClientVersionRepository(InMemoryStore s) => _s = s;

    public Task<ClientVersion?> GetAsync(string channel, CancellationToken ct = default) =>
        Task.FromResult(_s.ClientVersions.TryGetValue(channel, out var v) ? Clone(v) : null);

    public Task UpsertAsync(ClientVersion version, CancellationToken ct = default)
    {
        _s.ClientVersions[version.Channel] = Clone(version);
        return Task.CompletedTask;
    }

    private static ClientVersion Clone(ClientVersion v) => new()
    {
        Channel = v.Channel,
        Version = v.Version,
        PatchUrl = v.PatchUrl,
        Sha256 = v.Sha256,
        PatchSizeBytes = v.PatchSizeBytes,
        ReleaseNotes = v.ReleaseNotes,
        Mandatory = v.Mandatory,
        PublishedAt = v.PublishedAt
    };
}
