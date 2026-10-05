using Harekat.Domain.Entities;
using Harekat.Domain.ValueObjects;

namespace Harekat.Application.Abstractions;

public interface IPlayerRepository
{
    Task<Player?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Player?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<Player?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<Player?> GetBySteamIdAsync(ulong steamId, CancellationToken ct = default);
    Task<Player?> GetByRefreshTokenHashAsync(string hash, CancellationToken ct = default);
    Task<IReadOnlyList<Player>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<IReadOnlyList<Player>> GetLeaderboardAsync(string metric, int take, CancellationToken ct = default);
    Task<IReadOnlyList<Player>> GetSeasonLeaderboardAsync(int season, int take, CancellationToken ct = default);
    Task AddAsync(Player player, CancellationToken ct = default);
    Task UpdateAsync(Player player, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}

public interface ISquadRepository
{
    Task<Squad?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Squad?> GetByInviteCodeAsync(string code, CancellationToken ct = default);
    Task<Squad?> GetByMemberIdAsync(Guid playerId, CancellationToken ct = default);
    Task AddAsync(Squad squad, CancellationToken ct = default);
    Task UpdateAsync(Squad squad, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IMatchTicketRepository
{
    Task<MatchTicket?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<MatchTicket?> GetActiveBySquadIdAsync(Guid squadId, CancellationToken ct = default);
    Task<IReadOnlyList<MatchTicket>> GetQueuedByRegionAsync(string region, CancellationToken ct = default);
    Task AddAsync(MatchTicket ticket, CancellationToken ct = default);
    Task UpdateAsync(MatchTicket ticket, CancellationToken ct = default);
}

public interface IMatchRepository
{
    Task<Match?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Match match, CancellationToken ct = default);
    Task UpdateAsync(Match match, CancellationToken ct = default);
    Task<IReadOnlyList<Match>> GetPendingAllocationAsync(string? region = null, CancellationToken ct = default);
    Task<int> CountQueuedTicketsAsync(CancellationToken ct = default);
}

public interface IGameServerRepository
{
    Task<GameServer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<GameServer>> GetReadyByRegionAsync(string region, CancellationToken ct = default);
    Task AddAsync(GameServer server, CancellationToken ct = default);
    Task UpdateAsync(GameServer server, CancellationToken ct = default);
    Task<IReadOnlyList<GameServer>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<GameServer>> GetByHostAsync(string host, CancellationToken ct = default);
}

public interface IFriendshipRepository
{
    Task<Friendship?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Friendship?> GetBetweenAsync(Guid a, Guid b, CancellationToken ct = default);
    Task<IReadOnlyList<Friendship>> GetForPlayerAsync(Guid playerId, CancellationToken ct = default);
    Task AddAsync(Friendship friendship, CancellationToken ct = default);
    Task UpdateAsync(Friendship friendship, CancellationToken ct = default);
}

public interface ISeasonRepository
{
    Task<Season?> GetActiveAsync(CancellationToken ct = default);
    Task<Season?> GetByNumberAsync(int number, CancellationToken ct = default);
    Task<IReadOnlyList<Season>> GetAllAsync(CancellationToken ct = default);
    Task AddArchiveAsync(SeasonArchiveEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<SeasonArchiveEntry>> GetArchiveAsync(int season, CancellationToken ct = default);
}

public interface IModerationRepository
{
    Task AddReportAsync(PlayerReport report, CancellationToken ct = default);
    Task<IReadOnlyList<PlayerReport>> GetOpenReportsAsync(CancellationToken ct = default);
    Task UpdateReportAsync(PlayerReport report, CancellationToken ct = default);
    Task AddAuditAsync(AuditLogEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLogEntry>> GetAuditAsync(int take, CancellationToken ct = default);
}

public interface INewsRepository
{
    Task<IReadOnlyList<NewsItem>> ListPublishedAsync(string? language, int take, CancellationToken ct = default);
    Task<IReadOnlyList<NewsItem>> ListAllAsync(int take, CancellationToken ct = default);
    Task<NewsItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(NewsItem item, CancellationToken ct = default);
    Task UpdateAsync(NewsItem item, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IClientVersionRepository
{
    Task<ClientVersion?> GetAsync(string channel, CancellationToken ct = default);
    Task UpsertAsync(ClientVersion version, CancellationToken ct = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface IJwtTokenService
{
    (string AccessToken, DateTimeOffset ExpiresAt) CreateAccessToken(Player player);
    string CreateRefreshToken();
    string HashRefreshToken(string refreshToken);
}

public interface IEmailService
{
    Task SendVerificationAsync(string email, string token, CancellationToken ct = default);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);
}
