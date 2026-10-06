using System;
using System.Collections.Generic;

namespace Project.Core.Domain
{
    /// <summary>Tek savaşanın maç sonu özeti (oyuncu ya da bot).</summary>
    public readonly struct CombatantResult
    {
        public PlayerId Id { get; }
        public string Name { get; }
        public int Team { get; }
        public string TeamName { get; }
        public int TeamPlacement { get; }
        public int Placement { get; }
        public int Kills { get; }
        public int Headshots { get; }
        public float DamageDealt { get; }
        public float SurvivalSeconds { get; }
        public bool IsWinner { get; }

        public CombatantResult(PlayerId id, string name, int team, string teamName, int teamPlacement, int placement,
            int kills, int headshots, float damageDealt, float survivalSeconds, bool isWinner)
        {
            Id = id;
            Name = name;
            Team = team;
            TeamName = teamName;
            TeamPlacement = teamPlacement;
            Placement = placement;
            Kills = kills;
            Headshots = headshots;
            DamageDealt = damageDealt;
            SurvivalSeconds = survivalSeconds;
            IsWinner = isWinner;
        }
    }

    /// <summary>Bir timin maç sonu özeti.</summary>
    public sealed class TeamSummary
    {
        public int Team { get; }
        public string TeamName { get; }
        public int Placement { get; }
        public int Kills { get; }
        public IReadOnlyList<CombatantResult> Members { get; }

        public TeamSummary(int team, string teamName, int placement, int kills, IReadOnlyList<CombatantResult> members)
        {
            Team = team;
            TeamName = teamName;
            Placement = placement;
            Kills = kills;
            Members = members;
        }
    }

    /// <summary>Maçtaki TÜM savaşanların sonuçları (sunucunun backend'e gerçek tim/oyuncu sonucu göndermesi için).</summary>
    public sealed class MatchSummary
    {
        public IReadOnlyList<CombatantResult> Combatants { get; }
        public int WinnerTeam { get; }
        public float DurationSeconds { get; }
        public bool TimedOut { get; }

        public MatchSummary(IReadOnlyList<CombatantResult> combatants, int winnerTeam, float durationSeconds, bool timedOut)
        {
            Combatants = combatants ?? Array.Empty<CombatantResult>();
            WinnerTeam = winnerTeam;
            DurationSeconds = durationSeconds;
            TimedOut = timedOut;
        }

        /// <summary>
        /// Savaşanları tim dizinine göre gruplar; timler sıralamaya (1 = kazanan) göre, eşitlikte tim dizinine göre dizilir.
        /// Takımsız (Team &lt; 0) savaşanlar kendi tek kişilik takımı olur. Üyeler bireysel sıraya göre dizilir.
        /// </summary>
        public IReadOnlyList<TeamSummary> GroupByTeam()
        {
            var groups = new Dictionary<int, List<CombatantResult>>();
            var order = new List<int>();
            var soloKey = int.MinValue;
            for (var i = 0; i < Combatants.Count; i++)
            {
                var c = Combatants[i];
                var key = c.Team >= 0 ? c.Team : soloKey++;
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<CombatantResult>();
                    groups.Add(key, list);
                    order.Add(key);
                }

                list.Add(c);
            }

            var result = new List<TeamSummary>(order.Count);
            for (var g = 0; g < order.Count; g++)
            {
                var key = order[g];
                var members = groups[key];
                members.Sort((a, b) => a.Placement != b.Placement ? a.Placement.CompareTo(b.Placement) : a.Id.Value.CompareTo(b.Id.Value));

                var kills = 0;
                for (var m = 0; m < members.Count; m++)
                    kills += members[m].Kills;

                var first = members[0];
                var placement = first.TeamPlacement > 0 ? first.TeamPlacement : first.Placement;
                result.Add(new TeamSummary(key >= 0 ? key : -1, first.TeamName ?? string.Empty, placement, kills, members));
            }

            result.Sort((a, b) => a.Placement != b.Placement ? a.Placement.CompareTo(b.Placement) : a.Team.CompareTo(b.Team));
            return result;
        }
    }
}
