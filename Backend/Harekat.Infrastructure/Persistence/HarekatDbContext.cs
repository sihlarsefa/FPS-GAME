using System.Text.Json;
using Harekat.Domain.Entities;
using Harekat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Harekat.Infrastructure.Persistence;

public sealed class HarekatDbContext : DbContext
{
    public HarekatDbContext(DbContextOptions<HarekatDbContext> options) : base(options) { }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<Squad> Squads => Set<Squad>();
    public DbSet<MatchTicket> MatchTickets => Set<MatchTicket>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<GameServer> GameServers => Set<GameServer>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<SeasonArchiveEntry> SeasonArchives => Set<SeasonArchiveEntry>();
    public DbSet<PlayerReport> Reports => Set<PlayerReport>();
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();
    public DbSet<NewsItem> NewsItems => Set<NewsItem>();
    public DbSet<ClientVersion> ClientVersions => Set<ClientVersion>();
    public DbSet<LeaderboardCacheEntry> LeaderboardCache => Set<LeaderboardCacheEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var jsonOptions = new JsonSerializerOptions();

        modelBuilder.Entity<Player>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.SteamId).IsUnique();
            e.HasIndex(x => x.RefreshTokenHash);
            e.HasIndex(x => x.SeasonXp).HasDatabaseName("IX_Players_SeasonXp");
            e.HasIndex(x => x.EloRating).HasDatabaseName("IX_Players_EloRating");
            e.HasIndex(x => new { x.Region, x.SeasonXp }).HasDatabaseName("IX_Players_Region_SeasonXp");
            e.OwnsOne(x => x.Stats, s =>
            {
                s.Property(p => p.Matches);
                s.Property(p => p.Wins);
                s.Property(p => p.Kills);
                s.Property(p => p.Headshots);
                s.Property(p => p.BestPlacement);
                s.Property(p => p.TotalDamage);
                s.Property(p => p.LongestSurvivalSeconds);
                s.Property(p => p.Experience);
                s.Property(p => p.Rank);
                s.HasIndex(p => p.Experience).HasDatabaseName("IX_Players_Stats_Experience");
                s.HasIndex(p => p.Kills).HasDatabaseName("IX_Players_Stats_Kills");
                s.HasIndex(p => p.Wins).HasDatabaseName("IX_Players_Stats_Wins");
            });
            e.Property(x => x.OwnedCosmetics).HasConversion(
                v => JsonSerializer.Serialize(v, jsonOptions),
                v => JsonSerializer.Deserialize<List<string>>(v, jsonOptions) ?? new());
            e.Property(x => x.AchievementProgress).HasConversion(
                v => JsonSerializer.Serialize(v, jsonOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, int>>(v, jsonOptions) ?? new());
            e.Property(x => x.UnlockedAchievements).HasConversion(
                v => JsonSerializer.Serialize(v, jsonOptions),
                v => JsonSerializer.Deserialize<HashSet<string>>(v, jsonOptions) ?? new());
        });

        modelBuilder.Entity<Squad>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.InviteCode).IsUnique();
            e.Property(x => x.MemberIds).HasConversion(
                v => JsonSerializer.Serialize(v, jsonOptions),
                v => JsonSerializer.Deserialize<List<Guid>>(v, jsonOptions) ?? new());
            e.Property(x => x.ReadyMemberIds).HasConversion(
                v => JsonSerializer.Serialize(v, jsonOptions),
                v => JsonSerializer.Deserialize<HashSet<Guid>>(v, jsonOptions) ?? new());
        });

        modelBuilder.Entity<MatchTicket>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SquadId);
            e.HasIndex(x => new { x.Region, x.Status });
            e.Ignore(x => x.EloTolerance);
        });

        modelBuilder.Entity<Match>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Status).HasDatabaseName("IX_Matches_Status");
            e.HasIndex(x => new { x.Region, x.Status }).HasDatabaseName("IX_Matches_Region_Status");
            e.HasIndex(x => x.SeasonNumber).HasDatabaseName("IX_Matches_SeasonNumber");
            e.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_Matches_CreatedAt");
            e.HasIndex(x => x.CompletedAt).HasDatabaseName("IX_Matches_CompletedAt");
            e.Property(x => x.Teams).HasConversion(
                v => JsonSerializer.Serialize(v, jsonOptions),
                v => JsonSerializer.Deserialize<List<MatchTeamSlot>>(v, jsonOptions) ?? new());
        });

        modelBuilder.Entity<GameServer>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Region);
            e.HasIndex(x => new { x.Host, x.Status }).HasDatabaseName("IX_GameServers_Host_Status");
            e.HasIndex(x => x.LastHeartbeatAt).HasDatabaseName("IX_GameServers_LastHeartbeatAt");
            e.Ignore(x => x.Endpoint);
        });

        modelBuilder.Entity<Friendship>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RequesterId, x.AddresseeId }).IsUnique();
        });

        modelBuilder.Entity<Season>(e =>
        {
            e.HasKey(x => x.Number);
            // Season number is domain-assigned (1, 2, …), not SQL IDENTITY.
            e.Property(x => x.Number).ValueGeneratedNever();
            e.HasIndex(x => x.EndsAt).HasDatabaseName("IX_Seasons_EndsAt");
        });

        modelBuilder.Entity<SeasonArchiveEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SeasonNumber, x.Placement }).HasDatabaseName("IX_SeasonArchives_Season_Placement");
        });
        modelBuilder.Entity<PlayerReport>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<AuditLogEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_AuditLogs_CreatedAt");
        });

        // Sıralama önbelleği — XP/sezon leaderboard için denormalize tablo.
        modelBuilder.Entity<LeaderboardCacheEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Metric, x.SeasonNumber, x.Score }).IsDescending(false, false, true)
                .HasDatabaseName("IX_LeaderboardCache_Metric_Season_Score");
            e.HasIndex(x => x.PlayerId).HasDatabaseName("IX_LeaderboardCache_PlayerId");
        });

        modelBuilder.Entity<NewsItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.IsPublished, x.Language, x.PublishedAt });
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Language).HasMaxLength(8);
            e.Property(x => x.Author).HasMaxLength(100);
        });

        modelBuilder.Entity<ClientVersion>(e =>
        {
            e.HasKey(x => x.Channel);
            e.Property(x => x.Channel).HasMaxLength(32);
            e.Property(x => x.Version).HasMaxLength(32);
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.Property(x => x.PatchUrl).HasMaxLength(1024);
        });
    }
}
