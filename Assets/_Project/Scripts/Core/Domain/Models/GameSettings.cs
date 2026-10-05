namespace Project.Core.Domain
{
    /// <summary>Oyuncu tercihleri. Kalıcılık ISettingsStore üzerinden yapılır.</summary>
    public sealed class GameSettings
    {
        public float MouseSensitivity { get; set; } = 0.12f;
        public float AdsSensitivityMultiplier { get; set; } = 0.8f;
        public float FieldOfView { get; set; } = 80f;
        public float MasterVolume { get; set; } = 0.8f;
        public float AmbientVolume { get; set; } = 0.6f;
        public bool InvertY { get; set; }
        public int QualityLevel { get; set; } = 2;
        public bool Fullscreen { get; set; } = true;
        public bool ShowFps { get; set; }

        /// <summary>Maçtaki tim sayısı (her tim 10 kişi). Varsayılan 4 tim = 40 asker.</summary>
        public int TeamCount { get; set; } = 4;

        public BotDifficulty Difficulty { get; set; } = BotDifficulty.Normal;
        public InsertionMethod Insertion { get; set; } = InsertionMethod.Helicopter;
        public string PlayerName { get; set; } = "Komutan";

        /// <summary>Eski alan (tim sayısından türetilir).</summary>
        public int BotCount { get; set; } = 39;

        public GameSettings Clone() => (GameSettings)MemberwiseClone();
    }
}
