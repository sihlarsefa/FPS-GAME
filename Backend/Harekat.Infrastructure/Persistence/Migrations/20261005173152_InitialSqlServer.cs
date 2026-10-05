using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harekat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Resource = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientVersions",
                columns: table => new
                {
                    Channel = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PatchUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PatchSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ReleaseNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Mandatory = table.Column<bool>(type: "bit", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientVersions", x => x.Channel);
                });

            migrationBuilder.CreateTable(
                name: "Friendships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequesterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddresseeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Friendships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameServers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Host = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Port = table.Column<int>(type: "int", nullable: false),
                    Region = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ServerKeyHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentMatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MaxPlayers = table.Column<int>(type: "int", nullable: false),
                    CurrentPlayers = table.Column<int>(type: "int", nullable: false),
                    LastHeartbeatAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameServers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeaderboardCache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Metric = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SeasonNumber = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaderboardCache", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Region = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    GameServerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ServerEndpoint = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Teams = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetTeamCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SeasonNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SquadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Region = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MaxPingMs = table.Column<int>(type: "int", nullable: false),
                    AverageElo = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EnqueuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchTickets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NewsItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Author = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SteamId = table.Column<decimal>(type: "decimal(20,0)", nullable: true),
                    Stats_Matches = table.Column<int>(type: "int", nullable: false),
                    Stats_Wins = table.Column<int>(type: "int", nullable: false),
                    Stats_Kills = table.Column<int>(type: "int", nullable: false),
                    Stats_Headshots = table.Column<int>(type: "int", nullable: false),
                    Stats_BestPlacement = table.Column<int>(type: "int", nullable: false),
                    Stats_TotalDamage = table.Column<float>(type: "real", nullable: false),
                    Stats_LongestSurvivalSeconds = table.Column<float>(type: "real", nullable: false),
                    Stats_Experience = table.Column<int>(type: "int", nullable: false),
                    Stats_Rank = table.Column<int>(type: "int", nullable: false),
                    EloRating = table.Column<int>(type: "int", nullable: false),
                    SquadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmailVerified = table.Column<bool>(type: "bit", nullable: false),
                    EmailVerificationToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RefreshTokenHash = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    RefreshTokenExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsBanned = table.Column<bool>(type: "bit", nullable: false),
                    BanExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    BanReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsMuted = table.Column<bool>(type: "bit", nullable: false),
                    MuteExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsOnline = table.Column<bool>(type: "bit", nullable: false),
                    Region = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OwnedCosmetics = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EquippedCamo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EquippedBeret = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AchievementProgress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnlockedAchievements = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SeasonXp = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReporterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportedPlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MatchId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SeasonArchives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeasonNumber = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SeasonXp = table.Column<int>(type: "int", nullable: false),
                    Placement = table.Column<int>(type: "int", nullable: false),
                    RewardBadge = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonArchives", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Seasons",
                columns: table => new
                {
                    Number = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seasons", x => x.Number);
                });

            migrationBuilder.CreateTable(
                name: "Squads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LeaderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReadyMemberIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InviteCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Region = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsInQueue = table.Column<bool>(type: "bit", nullable: false),
                    IsInMatch = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Squads", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_RequesterId_AddresseeId",
                table: "Friendships",
                columns: new[] { "RequesterId", "AddresseeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_Host_Status",
                table: "GameServers",
                columns: new[] { "Host", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_LastHeartbeatAt",
                table: "GameServers",
                column: "LastHeartbeatAt");

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_Region",
                table: "GameServers",
                column: "Region");

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardCache_Metric_Season_Score",
                table: "LeaderboardCache",
                columns: new[] { "Metric", "SeasonNumber", "Score" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardCache_PlayerId",
                table: "LeaderboardCache",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_CompletedAt",
                table: "Matches",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_CreatedAt",
                table: "Matches",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_Region_Status",
                table: "Matches",
                columns: new[] { "Region", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_SeasonNumber",
                table: "Matches",
                column: "SeasonNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_Status",
                table: "Matches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MatchTickets_Region_Status",
                table: "MatchTickets",
                columns: new[] { "Region", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchTickets_SquadId",
                table: "MatchTickets",
                column: "SquadId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsItems_IsPublished_Language_PublishedAt",
                table: "NewsItems",
                columns: new[] { "IsPublished", "Language", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Players_EloRating",
                table: "Players",
                column: "EloRating");

            migrationBuilder.CreateIndex(
                name: "IX_Players_Email",
                table: "Players",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_RefreshTokenHash",
                table: "Players",
                column: "RefreshTokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_Players_Region_SeasonXp",
                table: "Players",
                columns: new[] { "Region", "SeasonXp" });

            migrationBuilder.CreateIndex(
                name: "IX_Players_SeasonXp",
                table: "Players",
                column: "SeasonXp");

            migrationBuilder.CreateIndex(
                name: "IX_Players_Stats_Experience",
                table: "Players",
                column: "Stats_Experience");

            migrationBuilder.CreateIndex(
                name: "IX_Players_Stats_Kills",
                table: "Players",
                column: "Stats_Kills");

            migrationBuilder.CreateIndex(
                name: "IX_Players_Stats_Wins",
                table: "Players",
                column: "Stats_Wins");

            migrationBuilder.CreateIndex(
                name: "IX_Players_SteamId",
                table: "Players",
                column: "SteamId",
                unique: true,
                filter: "[SteamId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Players_Username",
                table: "Players",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeasonArchives_Season_Placement",
                table: "SeasonArchives",
                columns: new[] { "SeasonNumber", "Placement" });

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_EndsAt",
                table: "Seasons",
                column: "EndsAt");

            migrationBuilder.CreateIndex(
                name: "IX_Squads_InviteCode",
                table: "Squads",
                column: "InviteCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "ClientVersions");

            migrationBuilder.DropTable(
                name: "Friendships");

            migrationBuilder.DropTable(
                name: "GameServers");

            migrationBuilder.DropTable(
                name: "LeaderboardCache");

            migrationBuilder.DropTable(
                name: "Matches");

            migrationBuilder.DropTable(
                name: "MatchTickets");

            migrationBuilder.DropTable(
                name: "NewsItems");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Reports");

            migrationBuilder.DropTable(
                name: "SeasonArchives");

            migrationBuilder.DropTable(
                name: "Seasons");

            migrationBuilder.DropTable(
                name: "Squads");
        }
    }
}
