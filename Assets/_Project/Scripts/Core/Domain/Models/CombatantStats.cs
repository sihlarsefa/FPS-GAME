namespace Project.Core.Domain
{
    public sealed class CombatantStats
    {
        public int Kills { get; set; }
        public int Headshots { get; set; }
        public float DamageDealt { get; set; }
        public int ShotsFired { get; set; }
        public int ShotsHit { get; set; }
        public float SurvivalSeconds { get; set; }
        public int Placement { get; set; }

        public float Accuracy => ShotsFired > 0 ? (float)ShotsHit / ShotsFired : 0f;
    }
}
