namespace Project.Core.Domain
{
    public readonly struct MatchResult
    {
        public bool IsWinner { get; }
        public int Placement { get; }
        public int TotalPlayers { get; }
        public int Kills { get; }
        public int Headshots { get; }
        public float DamageDealt { get; }
        public float SurvivalSeconds { get; }
        public float Accuracy { get; }
        public string KillerName { get; }

        /// <summary>Timin sıralaması (#1 = kazanan tim).</summary>
        public int TeamPlacement { get; }
        public int TeamCount { get; }
        public string TeamName { get; }
        public int TeamKills { get; }

        public MatchResult(bool isWinner, int placement, int totalPlayers, int kills, int headshots,
            float damageDealt, float survivalSeconds, float accuracy, string killerName)
            : this(isWinner, placement, totalPlayers, kills, headshots, damageDealt, survivalSeconds, accuracy, killerName,
                isWinner ? 1 : placement, 0, null, kills)
        {
        }

        public MatchResult(bool isWinner, int placement, int totalPlayers, int kills, int headshots,
            float damageDealt, float survivalSeconds, float accuracy, string killerName,
            int teamPlacement, int teamCount, string teamName, int teamKills)
        {
            IsWinner = isWinner;
            Placement = placement;
            TotalPlayers = totalPlayers;
            Kills = kills;
            Headshots = headshots;
            DamageDealt = damageDealt;
            SurvivalSeconds = survivalSeconds;
            Accuracy = accuracy;
            KillerName = killerName;
            TeamPlacement = teamPlacement;
            TeamCount = teamCount;
            TeamName = teamName;
            TeamKills = teamKills;
        }
    }
}
