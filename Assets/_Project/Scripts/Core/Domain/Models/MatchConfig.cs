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
        public float ArtilleryCooldownSeconds { get; set; } = 180f;

        // ---- İkmal sandığı (T-70 hava ikmali) — maç akışı: Docs/MAC_AKISI.md
        /// <summary>İlk ikmal duyurusu (InMatch başlangıcından sonra, sn). Erken temas ve ölü zaman kırıcı.</summary>
        public float AirdropFirstSeconds { get; set; } = 90f;
        /// <summary>İki ikmal duyurusu arası (sn).</summary>
        public float AirdropIntervalSeconds { get; set; } = 110f;
        /// <summary>Maç boyunca en fazla kaç ikmal (0 = kapalı).</summary>
        public int AirdropCount { get; set; } = 4;
        /// <summary>Duyurudan yere inişe (paraşüt) süre; oyuncular bu sürede yetişebilir ama sandık hemen açılmaz.</summary>
        public float AirdropDescentSeconds { get; set; } = 40f;
        /// <summary>Yere inişten sandığın açılmasına kadar süre (anında üçüncü tarafı engeller).</summary>
        public float AirdropOpenDelaySeconds { get; set; } = 8f;
        /// <summary>Sonraki güvenli çember bu yarıçaptan küçükse ikmal atılmaz (son çember anında kalabalık olmasın).</summary>
        public float AirdropMinZoneRadius { get; set; } = 60f;
        public int RandomSeed { get; set; }
        public TimeOfDay TimeOfDay { get; set; } = TimeOfDay.Gunduz;
        public WeatherKind Weather { get; set; } = WeatherKind.Acik;
        /// <summary>Maç içi dinamik hava (tohumdan Açık→bulut→yağmur→açılma, şimşek). Yalnız başlangıç havası Açık ise çalışır.</summary>
        public bool DynamicWeather { get; set; } = true;
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

        /// <summary>
        /// 1 km harita, 6x10 oyuncu için ~11,7 dakikalık harekât alanı planı (bekleme, daralma, yarıçap, saniyelik hasar).
        /// Kenar hızı hep koşu hızının altında; hasar artar; ilk bekleme kısa (ölü zaman &lt; 90 sn). Ayrıntı: Docs/MAC_AKISI.md.
        /// </summary>
        public static ZonePhase[] DefaultZonePhases() => new[]
        {
            new ZonePhase(120f, 70f, 400f, 1f),
            new ZonePhase(75f, 55f, 270f, 2f),
            new ZonePhase(60f, 45f, 170f, 3.5f),
            new ZonePhase(50f, 40f, 100f, 5f),
            new ZonePhase(40f, 35f, 55f, 8f),
            new ZonePhase(30f, 30f, 22f, 12f),
            new ZonePhase(20f, 30f, 0f, 18f)
        };
    }
}
