using Harekat.DiscordBot.Domain;

namespace Harekat.DiscordBot.Services;

/// <summary>Bellek içi veri deposu — backend yokken demo ve test için.</summary>
public sealed class InMemoryGameDataStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, PlayerProfile> _players = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SquadInfo> _squads = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GameServerStatus> _servers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SquadRecruitment> _recruitments = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ModerationLogEntry> _moderationLog = [];
    private readonly Dictionary<string, Tournament> _tournaments = new(StringComparer.OrdinalIgnoreCase);
    private SeasonInfo _currentSeason;

    public InMemoryGameDataStore()
    {
        _currentSeason = new SeasonInfo
        {
            SeasonId = "S1",
            Name = "Sezon 1 — Kuzgun Vadisi",
            StartsAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EndsAt = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero),
            IsActive = true
        };
    }

    public void SeedDemoData()
    {
        lock (_gate)
        {
            if (_players.Count > 0)
                return;

            UpsertPlayerUnlocked(new PlayerProfile
            {
                PlayerId = "p-komutan",
                DisplayName = "KomutanYilmaz",
                Stats = new CareerStats
                {
                    Matches = 120, Wins = 34, Kills = 890, Headshots = 210,
                    BestPlacement = 1, TotalDamage = 125_000, LongestSurvivalSeconds = 1800,
                    Experience = 58_000, Rank = MilitaryRank.Yuzbasi
                },
                SeasonXp = 12_400, SeasonKills = 210, WeeklyKills = 48, SquadId = "sq-bozkurt"
            });

            UpsertPlayerUnlocked(new PlayerProfile
            {
                PlayerId = "p-keskin",
                DisplayName = "KeskinNisanci",
                Stats = new CareerStats
                {
                    Matches = 95, Wins = 22, Kills = 1120, Headshots = 480,
                    BestPlacement = 1, TotalDamage = 98_000, LongestSurvivalSeconds = 1650,
                    Experience = 47_000, Rank = MilitaryRank.Ustegmen
                },
                SeasonXp = 15_800, SeasonKills = 340, WeeklyKills = 72, SquadId = "sq-bozkurt"
            });

            UpsertPlayerUnlocked(new PlayerProfile
            {
                PlayerId = "p-timci",
                DisplayName = "TimciAli",
                Stats = new CareerStats
                {
                    Matches = 60, Wins = 10, Kills = 320, Headshots = 55,
                    BestPlacement = 2, TotalDamage = 40_000, LongestSurvivalSeconds = 1200,
                    Experience = 8_000, Rank = MilitaryRank.AstsubayCavus
                },
                SeasonXp = 4_200, SeasonKills = 90, WeeklyKills = 19, SquadId = "sq-kartal"
            });

            UpsertPlayerUnlocked(new PlayerProfile
            {
                PlayerId = "p-yeni",
                DisplayName = "AcemiEr",
                Stats = new CareerStats
                {
                    Matches = 5, Wins = 0, Kills = 3, Headshots = 0,
                    BestPlacement = 8, TotalDamage = 450, LongestSurvivalSeconds = 300,
                    Experience = 120, Rank = MilitaryRank.Er
                },
                SeasonXp = 120, SeasonKills = 3, WeeklyKills = 3
            });

            UpsertSquadUnlocked(new SquadInfo
            {
                SquadId = "sq-bozkurt",
                Name = "Bozkurtlar",
                LeaderName = "KomutanYilmaz",
                TotalKills = 2010,
                WeeklyKills = 120,
                Wins = 56,
                SeasonScore = 28_200
            });
            _squads["sq-bozkurt"].MemberNames.AddRange(["KomutanYilmaz", "KeskinNisanci", "SeriAtis", "Savunma", "Keşif"]);

            UpsertSquadUnlocked(new SquadInfo
            {
                SquadId = "sq-kartal",
                Name = "Kartal Tim",
                LeaderName = "TimciAli",
                TotalKills = 980,
                WeeklyKills = 87,
                Wins = 18,
                SeasonScore = 9_400
            });
            _squads["sq-kartal"].MemberNames.AddRange(["TimciAli", "Piyade1", "Piyade2"]);

            UpsertSquadUnlocked(new SquadInfo
            {
                SquadId = "sq-aslan",
                Name = "Aslanlar",
                LeaderName = "AslanReis",
                TotalKills = 1500,
                WeeklyKills = 95,
                Wins = 30,
                SeasonScore = 16_000
            });
            _squads["sq-aslan"].MemberNames.AddRange(
                ["AslanReis", "M1", "M2", "M3", "M4", "M5", "M6", "M7", "M8"]);

            UpsertServerUnlocked(new GameServerStatus
            {
                ServerId = "eu-ist-01",
                Region = "İstanbul",
                Name = "HAREKÂT İstanbul-1",
                PlayerCount = 42,
                MaxPlayers = 60,
                IsOnline = true,
                PingMs = 18
            });

            UpsertServerUnlocked(new GameServerStatus
            {
                ServerId = "eu-fra-01",
                Region = "Frankfurt",
                Name = "HAREKÂT Frankfurt-1",
                PlayerCount = 28,
                MaxPlayers = 60,
                IsOnline = true,
                PingMs = 45
            });

            UpsertServerUnlocked(new GameServerStatus
            {
                ServerId = "eu-ams-01",
                Region = "Amsterdam",
                Name = "HAREKÂT Amsterdam-1",
                PlayerCount = 0,
                MaxPlayers = 60,
                IsOnline = false,
                PingMs = 0
            });
        }
    }

    public SeasonInfo GetCurrentSeason()
    {
        lock (_gate) return CloneSeason(_currentSeason);
    }

    public void SetCurrentSeason(SeasonInfo season)
    {
        ArgumentNullException.ThrowIfNull(season);
        lock (_gate) _currentSeason = CloneSeason(season);
    }

    public IReadOnlyList<PlayerProfile> GetPlayers()
    {
        lock (_gate) return _players.Values.Select(ClonePlayer).ToArray();
    }

    public PlayerProfile? FindPlayerByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_gate)
        {
            var match = _players.Values.FirstOrDefault(p =>
                string.Equals(p.DisplayName, name, StringComparison.OrdinalIgnoreCase));
            return match is null ? null : ClonePlayer(match);
        }
    }

    public PlayerProfile? FindPlayerById(string playerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);
        lock (_gate)
            return _players.TryGetValue(playerId, out var p) ? ClonePlayer(p) : null;
    }

    public PlayerProfile? FindPlayerByDiscordId(ulong discordUserId)
    {
        lock (_gate)
        {
            var match = _players.Values.FirstOrDefault(p => p.DiscordUserId == discordUserId);
            return match is null ? null : ClonePlayer(match);
        }
    }

    public void UpsertPlayer(PlayerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        lock (_gate) UpsertPlayerUnlocked(profile);
    }

    public IReadOnlyList<SquadInfo> GetSquads()
    {
        lock (_gate) return _squads.Values.Select(CloneSquad).ToArray();
    }

    public SquadInfo? FindSquadByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_gate)
        {
            var match = _squads.Values.FirstOrDefault(s =>
                string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
            return match is null ? null : CloneSquad(match);
        }
    }

    public void UpsertSquad(SquadInfo squad)
    {
        ArgumentNullException.ThrowIfNull(squad);
        lock (_gate) UpsertSquadUnlocked(squad);
    }

    public IReadOnlyList<GameServerStatus> GetServers()
    {
        lock (_gate) return _servers.Values.Select(CloneServer).ToArray();
    }

    public void UpsertServer(GameServerStatus server)
    {
        ArgumentNullException.ThrowIfNull(server);
        lock (_gate) UpsertServerUnlocked(server);
    }

    public SquadRecruitment AddRecruitment(SquadRecruitment recruitment)
    {
        ArgumentNullException.ThrowIfNull(recruitment);
        lock (_gate)
        {
            _recruitments[recruitment.RecruitmentId] = CloneRecruitment(recruitment);
            return CloneRecruitment(_recruitments[recruitment.RecruitmentId]);
        }
    }

    public SquadRecruitment? GetRecruitment(string recruitmentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recruitmentId);
        lock (_gate)
            return _recruitments.TryGetValue(recruitmentId, out var r) ? CloneRecruitment(r) : null;
    }

    public IReadOnlyList<SquadRecruitment> GetOpenRecruitments()
    {
        lock (_gate)
            return _recruitments.Values.Where(r => !r.IsClosed && !r.IsFull)
                .Select(CloneRecruitment).ToArray();
    }

    public bool TryJoinRecruitment(string recruitmentId, ulong discordUserId, out SquadRecruitment? updated, out string? error)
    {
        lock (_gate)
        {
            updated = null;
            error = null;
            if (!_recruitments.TryGetValue(recruitmentId, out var r))
            {
                error = "İlan bulunamadı.";
                return false;
            }

            if (r.IsClosed)
            {
                error = "İlan kapatılmış.";
                return false;
            }

            if (r.IsFull)
            {
                error = "Tim dolu (10/10).";
                return false;
            }

            if (r.MemberDiscordIds.Contains(discordUserId))
            {
                error = "Zaten timdesin.";
                return false;
            }

            r.MemberDiscordIds.Add(discordUserId);
            if (r.IsFull)
                r.IsClosed = true;

            updated = CloneRecruitment(r);
            return true;
        }
    }

    public void AddModerationLog(ModerationLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate) _moderationLog.Add(entry);
    }

    public IReadOnlyList<ModerationLogEntry> GetModerationLog(int take = 50)
    {
        lock (_gate)
            return _moderationLog.OrderByDescending(e => e.CreatedAt).Take(take).ToArray();
    }

    public void UpsertTournament(Tournament tournament)
    {
        ArgumentNullException.ThrowIfNull(tournament);
        lock (_gate) _tournaments[tournament.TournamentId] = CloneTournament(tournament);
    }

    public Tournament? GetTournament(string tournamentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tournamentId);
        lock (_gate)
            return _tournaments.TryGetValue(tournamentId, out var t) ? CloneTournament(t) : null;
    }

    public IReadOnlyList<Tournament> GetTournaments()
    {
        lock (_gate) return _tournaments.Values.Select(CloneTournament).ToArray();
    }

    private void UpsertPlayerUnlocked(PlayerProfile profile) =>
        _players[profile.PlayerId] = ClonePlayer(profile);

    private void UpsertSquadUnlocked(SquadInfo squad) =>
        _squads[squad.SquadId] = CloneSquad(squad);

    private void UpsertServerUnlocked(GameServerStatus server) =>
        _servers[server.ServerId] = CloneServer(server);

    private static SeasonInfo CloneSeason(SeasonInfo s) => new()
    {
        SeasonId = s.SeasonId,
        Name = s.Name,
        StartsAt = s.StartsAt,
        EndsAt = s.EndsAt,
        IsActive = s.IsActive
    };

    private static PlayerProfile ClonePlayer(PlayerProfile p) => new()
    {
        PlayerId = p.PlayerId,
        DisplayName = p.DisplayName,
        DiscordUserId = p.DiscordUserId,
        SquadId = p.SquadId,
        SeasonXp = p.SeasonXp,
        SeasonKills = p.SeasonKills,
        WeeklyKills = p.WeeklyKills,
        CreatedAt = p.CreatedAt,
        Stats = new CareerStats
        {
            Matches = p.Stats.Matches,
            Wins = p.Stats.Wins,
            Kills = p.Stats.Kills,
            Headshots = p.Stats.Headshots,
            BestPlacement = p.Stats.BestPlacement,
            TotalDamage = p.Stats.TotalDamage,
            LongestSurvivalSeconds = p.Stats.LongestSurvivalSeconds,
            Experience = p.Stats.Experience,
            Rank = p.Stats.Rank
        }
    };

    private static SquadInfo CloneSquad(SquadInfo s)
    {
        var clone = new SquadInfo
        {
            SquadId = s.SquadId,
            Name = s.Name,
            LeaderName = s.LeaderName,
            TotalKills = s.TotalKills,
            WeeklyKills = s.WeeklyKills,
            Wins = s.Wins,
            SeasonScore = s.SeasonScore
        };
        clone.MemberNames.AddRange(s.MemberNames);
        return clone;
    }

    private static GameServerStatus CloneServer(GameServerStatus s) => new()
    {
        ServerId = s.ServerId,
        Region = s.Region,
        Name = s.Name,
        PlayerCount = s.PlayerCount,
        MaxPlayers = s.MaxPlayers,
        IsOnline = s.IsOnline,
        PingMs = s.PingMs,
        LastHeartbeat = s.LastHeartbeat
    };

    private static SquadRecruitment CloneRecruitment(SquadRecruitment r)
    {
        var clone = new SquadRecruitment
        {
            RecruitmentId = r.RecruitmentId,
            CreatorDiscordId = r.CreatorDiscordId,
            CreatorDisplayName = r.CreatorDisplayName,
            SquadName = r.SquadName,
            Description = r.Description,
            CreatedAt = r.CreatedAt,
            IsClosed = r.IsClosed
        };
        clone.MemberDiscordIds.AddRange(r.MemberDiscordIds);
        return clone;
    }

    private static Tournament CloneTournament(Tournament t)
    {
        var clone = new Tournament
        {
            TournamentId = t.TournamentId,
            Name = t.Name,
            Status = t.Status,
            WinnerTeam = t.WinnerTeam,
            CreatedAt = t.CreatedAt
        };
        clone.TeamNames.AddRange(t.TeamNames);
        foreach (var m in t.Matches)
        {
            clone.Matches.Add(new TournamentMatch
            {
                MatchId = m.MatchId,
                Round = m.Round,
                SlotIndex = m.SlotIndex,
                TeamA = m.TeamA,
                TeamB = m.TeamB,
                Winner = m.Winner,
                Status = m.Status,
                NextMatchId = m.NextMatchId,
                AdvancesAsTeamA = m.AdvancesAsTeamA
            });
        }

        return clone;
    }
}
