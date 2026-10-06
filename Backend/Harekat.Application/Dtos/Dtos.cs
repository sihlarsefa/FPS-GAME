using Harekat.Domain.Enums;
using Harekat.Domain.ValueObjects;

namespace Harekat.Application.Dtos;

public sealed record RegisterRequest(string Username, string Email, string Password, string? Region = "tr");
public sealed record LoginRequest(string Username, string Password);
public sealed record SteamAuthRequest(string Ticket, string? PersonaName = null, string? Region = "tr");
public sealed record AuthResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, PlayerDto Player);
public sealed record RefreshRequest(string RefreshToken);
public sealed record VerifyEmailRequest(string Token);

public sealed record PlayerDto(
    Guid Id,
    string Username,
    string Email,
    CareerStats Stats,
    MilitaryRank Rank,
    int EloRating,
    Guid? SquadId,
    bool EmailVerified,
    string Role,
    bool IsOnline,
    string Region,
    IReadOnlyList<string> OwnedCosmetics,
    string EquippedCamo,
    string EquippedBeret,
    IReadOnlyList<string> UnlockedAchievements,
    int SeasonXp);

public sealed record CreateSquadRequest(string Name, string? Region = "tr");
public sealed record InviteSquadRequest(string InviteCode);
public sealed record SquadDto(
    Guid Id,
    string Name,
    Guid LeaderId,
    IReadOnlyList<Guid> MemberIds,
    IReadOnlyList<Guid> ReadyMemberIds,
    string InviteCode,
    string Region,
    bool AllReady,
    int OpenSlots);

public sealed record QueueRequest(string? Region = null, int? MaxPingMs = null);
public sealed record QueueResponse(Guid TicketId, string Status, string Region, DateTimeOffset EnqueuedAt);
public sealed record MatchDto(
    Guid Id,
    string Region,
    string Status,
    string? ServerEndpoint,
    IReadOnlyList<MatchTeamDto> Teams,
    int SeasonNumber);

public sealed record MatchTeamDto(Guid SquadId, string SquadName, IReadOnlyList<Guid> PlayerIds, int BotCount, int Placement);

public sealed record RegisterServerRequest(string Host, int Port, string Region, string ServerKey, int MaxPlayers = 100);
public sealed record ServerHeartbeatRequest(Guid ServerId, string ServerKey, int CurrentPlayers, string Status);
public sealed record ServerDto(Guid Id, string Endpoint, string Region, string Status, int CurrentPlayers, int MaxPlayers);
public sealed record ClaimMatchRequest(string Host, int Port, string ServerKey, string? Region = null, int MaxPlayers = 100);
public sealed record ClaimMatchResponse(MatchDto Match, Guid ServerId, string Endpoint);
public sealed record ReleaseServerRequest(Guid ServerId, string ServerKey);

public sealed record PlayerMatchResultDto(
    Guid PlayerId,
    int Kills,
    int Headshots,
    float Damage,
    float SurvivalSeconds,
    string? TopWeaponId = null);

public sealed record TeamMatchResultDto(Guid SquadId, int Placement, IReadOnlyList<PlayerMatchResultDto> Players);

public sealed record MatchResultRequest(IReadOnlyList<TeamMatchResultDto> Teams);

public sealed record MatchResultResponse(Guid MatchId, IReadOnlyList<XpAwardDto> Awards);

public sealed record XpAwardDto(Guid PlayerId, int XpGained, MilitaryRank NewRank, int NewElo, IReadOnlyList<string> NewAchievements);

public sealed record LeaderboardEntryDto(int Rank, Guid PlayerId, string Username, MilitaryRank MilitaryRank, int Value, int Elo);

public sealed record FriendshipDto(Guid Id, Guid OtherPlayerId, string OtherUsername, string Status, bool OtherOnline);
public sealed record FriendRequestDto(Guid TargetPlayerId);

public sealed record SeasonDto(int Number, string Name, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsActive);
public sealed record SeasonArchiveDto(int SeasonNumber, Guid PlayerId, string Username, int SeasonXp, int Placement, string RewardBadge);

public sealed record AchievementDto(string Id, string Title, string Description, int Target, int Progress, bool Unlocked);
public sealed record AchievementSyncRequest(
    IReadOnlyList<string>? UnlockedIds = null,
    IReadOnlyDictionary<string, int>? Progress = null);
public sealed record AchievementSyncResult(IReadOnlyList<string> NewlyUnlocked, IReadOnlyList<AchievementDto> Achievements);
public sealed record CosmeticDto(string Id, string Name, string Slot, bool Owned, bool Equipped);
public sealed record EquipCosmeticRequest(string CosmeticId);

public sealed record ReportPlayerRequest(Guid ReportedPlayerId, string Reason, string? MatchId = null);
public sealed record BanPlayerRequest(Guid PlayerId, string Reason, int? DurationHours = null);
public sealed record MutePlayerRequest(Guid PlayerId, int DurationHours, string? Reason = null);

public sealed record LobbyChatMessage(Guid SquadId, Guid SenderId, string SenderName, string Message, DateTimeOffset SentAt);
public sealed record ReadyStatusDto(Guid SquadId, Guid PlayerId, bool IsReady, bool AllReady);

// ——— Client / Launcher (F3-8) ———
public sealed record NewsItemDto(
    Guid Id,
    string Title,
    string Body,
    string Language,
    string? Author,
    DateTimeOffset PublishedAt,
    int SortOrder);

public sealed record UpsertNewsRequest(
    string Title,
    string Body,
    string? Language = "tr",
    string? Author = null,
    bool IsPublished = true,
    int SortOrder = 0,
    DateTimeOffset? PublishedAt = null);

public sealed record ClientVersionDto(
    string Channel,
    string Version,
    string PatchUrl,
    string Sha256,
    long PatchSizeBytes,
    string? ReleaseNotes,
    bool Mandatory,
    DateTimeOffset PublishedAt);

public sealed record UpsertClientVersionRequest(
    string Version,
    string PatchUrl,
    string Sha256,
    long? PatchSizeBytes = null,
    string? ReleaseNotes = null,
    bool Mandatory = false,
    string? Channel = "stable");

