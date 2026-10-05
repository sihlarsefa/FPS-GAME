namespace Project.Core.Domain
{
    /// <summary>
    /// Bir maçın kuralları. Tim Battle Royale: TeamCount x TeamSize asker, son ayakta kalan tim kazanır.
    /// Varsayılanlar 1 km x 1 km "Kuzgun Vadisi" haritası içindir.
    /// </summary>
    public sealed class MatchConfig
    {
        public int TeamCount { get; set; } = 4;
        public int TeamSize { get; set; } = 10;
        public int MaxPlayers { get; set; } = 40;
        public int BotCount { get; set; } = 39;
        public BotDifficulty Difficulty { get; set; } = BotDifficulty.Normal;
        public InsertionMethod PlayerInsertion { get; set; } = InsertionMethod.Helicopter;
        public bool FriendlyFire { get; set; }
        public string MapName { get; set; } = "Kuzgun Vadisi";
        public float PreMatchDurationSeconds { get; set; } = 6f;
        public float MatchDurationSeconds { get; set; } = 1500f;
        public float ZoneShrinkIntervalSeconds { get; set; } = 90f;
        public float MapHalfSize { get; set; } = 512f;
        public float PlaneAltitude { get; set; } = 120f;
        public float PlaneSpeed { get; set; } = 38f;
        public float InitialZoneRadius { get; set; } = 740f;
        public float ArtilleryCooldownSeconds { get; set; } = 150f;
        public int RandomSeed { get; set; }
        public ZonePhase[] ZonePhases { get; set; } = DefaultZonePhases();

        public MatchConfig()
        {
        }

        public MatchConfig(
            int maxPlayers,
            float preMatchDurationSeconds,
            float matchDurationSeconds,
            float zoneShrinkIntervalSeconds)
        {
            MaxPlayers = maxPlayers;
            BotCount = maxPlayers > 0 ? maxPlayers - 1 : 0;
            PreMatchDurationSeconds = preMatchDurationSeconds;
            MatchDurationSeconds = matchDurationSeconds;
            ZoneShrinkIntervalSeconds = zoneShrinkIntervalSeconds;
        }

        public int TotalCombatants => TeamCount * TeamSize;

        public static MatchConfig Default => new();

        /// <summary>Tim sayısına göre oyuncu/bot sayılarını ayarlar.</summary>
        public MatchConfig WithTeams(int teamCount, int teamSize = 10)
        {
            TeamCount = teamCount < 2 ? 2 : teamCount;
            TeamSize = teamSize < 1 ? 1 : teamSize;
            MaxPlayers = TeamCount * TeamSize;
            BotCount = MaxPlayers - 1;
            return this;
        }

        /// <summary>Toplam ~13 dakikalık harekât alanı daralma planı (bekleme, daralma, yarıçap, saniyelik hasar).</summary>
        public static ZonePhase[] DefaultZonePhases() => new[]
        {
            new ZonePhase(150f, 70f, 420f, 1f),
            new ZonePhase(80f, 55f, 260f, 2f),
            new ZonePhase(65f, 45f, 160f, 3.5f),
            new ZonePhase(55f, 40f, 95f, 5f),
            new ZonePhase(45f, 35f, 50f, 8f),
            new ZonePhase(35f, 30f, 20f, 11f),
            new ZonePhase(25f, 30f, 0f, 16f)
        };
    }
}
