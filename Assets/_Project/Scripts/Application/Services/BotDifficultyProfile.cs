using Project.Core.Domain;

namespace Project.Application.Services
{
    /// <summary>
    /// Zorluğa göre bot yetenekleri. For() her çağrıda yeni (değiştirilebilir) bir örnek döndürür.
    /// Kolay: nişan hatası 6°, tepki 0.9 sn, görüş 160 m · Normal: 3.5°, 0.55 sn, 220 m · Zor: 1.8°, 0.3 sn, 280 m.
    /// </summary>
    public sealed class BotDifficultyProfile
    {
        public BotDifficulty Difficulty { get; set; } = BotDifficulty.Normal;
        public float AimErrorDegrees { get; set; }
        public float ReactionSeconds { get; set; }
        public float ViewDistance { get; set; }
        public float FieldOfViewDegrees { get; set; }
        public float TurnSpeedDegreesPerSecond { get; set; }
        public float HearingDistance { get; set; }
        public float HeadshotChance { get; set; }
        public int BurstMin { get; set; }
        public int BurstMax { get; set; }
        public float BurstPauseSeconds { get; set; }
        public float AggressionChance { get; set; }

        /// <summary>Hareket halindeki hedefe ateşte nişan hatası çarpanı.</summary>
        public float MovingTargetErrorMultiplier { get; set; }

        /// <summary>Saniyede karar yenileme aralığı (sn) — AI kademelendirme için.</summary>
        public float DecisionIntervalSeconds { get; set; }

        public static BotDifficultyProfile For(BotDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BotDifficulty.Easy:
                    return new BotDifficultyProfile
                    {
                        Difficulty = BotDifficulty.Easy,
                        AimErrorDegrees = 6f,
                        ReactionSeconds = 0.9f,
                        ViewDistance = 160f,
                        FieldOfViewDegrees = 110f,
                        TurnSpeedDegreesPerSecond = 140f,
                        HearingDistance = 60f,
                        HeadshotChance = 0.06f,
                        BurstMin = 2,
                        BurstMax = 4,
                        BurstPauseSeconds = 0.65f,
                        AggressionChance = 0.3f,
                        MovingTargetErrorMultiplier = 1.8f,
                        DecisionIntervalSeconds = 0.5f
                    };
                case BotDifficulty.Hard:
                    return new BotDifficultyProfile
                    {
                        Difficulty = BotDifficulty.Hard,
                        AimErrorDegrees = 1.8f,
                        ReactionSeconds = 0.3f,
                        ViewDistance = 280f,
                        FieldOfViewDegrees = 140f,
                        TurnSpeedDegreesPerSecond = 320f,
                        HearingDistance = 110f,
                        HeadshotChance = 0.25f,
                        BurstMin = 3,
                        BurstMax = 6,
                        BurstPauseSeconds = 0.3f,
                        AggressionChance = 0.7f,
                        MovingTargetErrorMultiplier = 1.25f,
                        DecisionIntervalSeconds = 0.25f
                    };
                default:
                    return new BotDifficultyProfile
                    {
                        Difficulty = BotDifficulty.Normal,
                        AimErrorDegrees = 3.5f,
                        ReactionSeconds = 0.55f,
                        ViewDistance = 220f,
                        FieldOfViewDegrees = 120f,
                        TurnSpeedDegreesPerSecond = 220f,
                        HearingDistance = 85f,
                        HeadshotChance = 0.14f,
                        BurstMin = 3,
                        BurstMax = 5,
                        BurstPauseSeconds = 0.45f,
                        AggressionChance = 0.5f,
                        MovingTargetErrorMultiplier = 1.5f,
                        DecisionIntervalSeconds = 0.35f
                    };
            }
        }

        public BotDifficultyProfile Clone() => (BotDifficultyProfile)MemberwiseClone();
    }
}
