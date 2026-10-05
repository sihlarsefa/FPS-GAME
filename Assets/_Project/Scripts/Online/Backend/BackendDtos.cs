using System;
using System.Collections.Generic;

namespace Project.Online.Backend
{
    /// <summary>
    /// Backend/ClientSdk DTO'larıyla birebir aynı şekil (Harekat.ClientSdk).
    /// Unity JsonUtility / manuel eşleme için alan adları wire camelCase ile uyumludur.
    /// </summary>
    public sealed class AuthResult
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public DateTimeOffset ExpiresAt { get; set; }
        public PlayerProfile Player { get; set; }
    }

    public sealed class PlayerProfile
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = "";
        public string Email { get; set; } = "";
        public CareerStatsDto Stats { get; set; }
        public int Rank { get; set; }
        public int EloRating { get; set; }
        public Guid? SquadId { get; set; }
    }

    public sealed class CareerStatsDto
    {
        public int Matches { get; set; }
        public int Wins { get; set; }
        public int Kills { get; set; }
        public int Headshots { get; set; }
        public int BestPlacement { get; set; }
        public float TotalDamage { get; set; }
        public float LongestSurvivalSeconds { get; set; }
        public int Experience { get; set; }
        public int Rank { get; set; }
    }

    public sealed class SquadInfo
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public Guid LeaderId { get; set; }
        public List<Guid> MemberIds { get; set; } = new List<Guid>();
        public List<Guid> ReadyMemberIds { get; set; } = new List<Guid>();
        public string InviteCode { get; set; } = "";
        public string Region { get; set; } = "tr";
        public int OpenSlots { get; set; }
        public bool AllReady { get; set; }
    }

    public sealed class QueueInfo
    {
        public Guid TicketId { get; set; }
        public string Status { get; set; } = "";
        public string Region { get; set; } = "";
        public DateTimeOffset EnqueuedAt { get; set; }
    }

    public sealed class LeaderboardRow
    {
        public int Rank { get; set; }
        public Guid PlayerId { get; set; }
        public string Username { get; set; } = "";
        public int MilitaryRank { get; set; }
        public int Value { get; set; }
        public int Elo { get; set; }
    }

    /// <summary>Backend AchievementDto ile aynı.</summary>
    public sealed class AchievementDto
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public int Target { get; set; }
        public int Progress { get; set; }
        public bool Unlocked { get; set; }
    }

    public sealed class ReadyStatusDto
    {
        public Guid SquadId { get; set; }
        public Guid PlayerId { get; set; }
        public bool IsReady { get; set; }
        public bool AllReady { get; set; }
    }

    public sealed class BackendApiException : Exception
    {
        public int StatusCode { get; }
        public string ResponseBody { get; }
        public string TurkishMessage { get; }

        public BackendApiException(int statusCode, string body, string turkishMessage)
            : base(turkishMessage ?? ("API " + statusCode))
        {
            StatusCode = statusCode;
            ResponseBody = body ?? "";
            TurkishMessage = turkishMessage ?? Message;
        }
    }
}
