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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var jsonOptions = new JsonSerializerOptions();

        modelBuilder.Entity<Player>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.RefreshTokenHash);
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
            e.Property(x => x.Teams).HasConversion(
                v => JsonSerializer.Serialize(v, jsonOptions),
                v => JsonSerializer.Deserialize<List<MatchTeamSlot>>(v, jsonOptions) ?? new());
        });

        modelBuilder.Entity<GameServer>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Region);
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
        });

        modelBuilder.Entity<SeasonArchiveEntry>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<PlayerReport>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<AuditLogEntry>(e => e.HasKey(x => x.Id));
    }
}
