namespace Project.Core.Domain
{
    /// <summary>Bot karar mantığına giden algı özeti (saf veri; Unity'den bağımsız test edilebilir).</summary>
    public struct BotSenses
    {
        public float HealthNormalized;
        public bool HasWeapon;
        public bool HasAmmo;
        public bool IsReloading;
        public bool HasHealItem;
        public bool HasBoostItem;
        public bool CanSeeEnemy;
        public float EnemyDistance;
        public float SecondsSinceEnemySeen;
        public float SecondsSinceDamaged;
        public bool HasLastKnownEnemyPosition;
        public bool HeardGunfireRecently;
        public bool IsOutsideZone;
        public bool IsOutsideNextZone;
        public float SecondsUntilZoneShrinks;
        public bool ZoneIsShrinking;
        public bool KnowsUsefulLoot;
        public float NearestUsefulLootDistance;
        public bool NeedsLoot;

        // Tim (squad) bilgisi
        public bool IsSquadMember;
        public bool LeaderAlive;
        public float DistanceToLeader;
        public bool HasSquadOrder;
        public SquadOrder Order;
        public float DistanceToOrderTarget;
        public bool AllyNeedsHelp;
    }
}
