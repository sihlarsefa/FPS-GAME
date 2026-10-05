using Project.Core.Domain;

namespace Project.Application.Services
{
    /// <summary>
    /// Saf bot karar mantığı (öncelik sırası): düşman görünüyor+silahlı → Engage; silahsız ve düşman yakın (&lt;4 m) →
    /// Engage (yumruk), uzaksa Flee/Loot; bölge dışı → MoveToZone; can &lt; %60 ve iyileştirme var ve 4 sn düşman yok → Heal;
    /// yakın zamanda düşman/silah sesi ve silahlı → Investigate; yağma ihtiyacı ve bilinen yağma → Loot;
    /// sonraki bölge dışı → MoveToZone; aksi halde Roam.
    /// <para>
    /// Tim (squad) kuralları — IsSquadMember: bot bir tim liderine bağlı takipçidir (lider botun kendisi için false).
    /// Görünen düşman (silahlı) her zaman önceliklidir; bölge dışındaysa bölgeye kaçış emirlerden önce gelir.
    /// Emir varsa: HoldPosition → Hold; Attack → Assault (hedefe taarruz); Follow/Regroup → lidere uzaklık &gt; 8 m ise
    /// Follow (yaklaşınca 4 m'ye kadar sürdürür), değilse Idle (Follow emrinde çok yakın yağmayı alabilir).
    /// Emir yoksa takipçi lidere bağlı kalır (30 m tasma), tasma içinde silah sesini araştırır / yakın yağmayı alır.
    /// </para>
    /// </summary>
    public static class BotDecisionService
    {
        public const float MeleeRange = 4f;
        public const float HealHealthThreshold = 0.6f;
        public const float HealContinueThreshold = 0.85f;
        public const float HealSafeSeconds = 4f;
        public const float InvestigateMemorySeconds = 10f;
        public const float FleeMemorySeconds = 3f;

        public const float FollowStartDistance = 8f;
        public const float FollowStopDistance = 4f;
        public const float FollowLootRadius = 10f;

        public const float LeashDistance = 30f;
        public const float LeashReturnDistance = 6f;
        public const float LeashFollowDistance = 10f;
        public const float LeashLootRadius = 20f;

        public static BotState Decide(in BotSenses senses, BotState current)
        {
            var armed = senses.HasWeapon && (senses.HasAmmo || senses.IsReloading);

            // 1) Görünen düşman.
            if (senses.CanSeeEnemy)
            {
                if (armed)
                    return BotState.Engage;

                if (senses.EnemyDistance < MeleeRange)
                    return BotState.Engage;

                return senses.KnowsUsefulLoot ? BotState.Loot : BotState.Flee;
            }

            // 2) Bölge dışı — her emirden önce.
            if (senses.IsOutsideZone)
                return BotState.MoveToZone;

            // Silahsız kaçış birkaç saniye sürer (düşman gözden kaybolunca hemen geri dönmesin).
            if (current == BotState.Flee && !armed && senses.SecondsSinceEnemySeen < FleeMemorySeconds)
                return BotState.Flee;

            // 3) İyileşme (yaklaşınca biraz daha devam eder — histerezis).
            if (senses.HasHealItem && senses.SecondsSinceEnemySeen >= HealSafeSeconds)
            {
                if (senses.HealthNormalized < HealHealthThreshold)
                    return BotState.Heal;

                if (current == BotState.Heal && senses.HealthNormalized < HealContinueThreshold)
                    return BotState.Heal;
            }

            var recentThreat = senses.HeardGunfireRecently ||
                               (senses.HasLastKnownEnemyPosition && senses.SecondsSinceEnemySeen < InvestigateMemorySeconds);

            // 4) Tim emirleri / lidere bağlılık.
            if (senses.IsSquadMember)
            {
                if (senses.HasSquadOrder)
                {
                    switch (senses.Order)
                    {
                        case SquadOrder.HoldPosition:
                            return BotState.Hold;

                        case SquadOrder.Attack:
                            return BotState.Assault;

                        case SquadOrder.Follow:
                        case SquadOrder.Regroup:
                            if (!senses.LeaderAlive)
                                break;

                            if (senses.DistanceToLeader > FollowStartDistance ||
                                (current == BotState.Follow && senses.DistanceToLeader > FollowStopDistance))
                                return BotState.Follow;

                            if (senses.Order == SquadOrder.Follow && senses.NeedsLoot && senses.KnowsUsefulLoot &&
                                senses.NearestUsefulLootDistance <= FollowLootRadius)
                                return BotState.Loot;

                            return BotState.Idle;
                    }
                }
                else if (senses.LeaderAlive)
                {
                    if (senses.DistanceToLeader > LeashDistance ||
                        (current == BotState.Follow && senses.DistanceToLeader > LeashReturnDistance))
                        return BotState.Follow;

                    if (armed && (recentThreat || senses.AllyNeedsHelp))
                        return BotState.Investigate;

                    if (senses.NeedsLoot && senses.KnowsUsefulLoot && senses.NearestUsefulLootDistance <= LeashLootRadius)
                        return BotState.Loot;

                    return senses.DistanceToLeader > LeashFollowDistance ? BotState.Follow : BotState.Idle;
                }
            }

            // 5) Bağımsız bot (ya da lideri ölmüş takipçi).
            if (armed && (recentThreat || (senses.IsSquadMember && senses.AllyNeedsHelp)))
                return BotState.Investigate;

            if (senses.NeedsLoot && senses.KnowsUsefulLoot)
                return BotState.Loot;

            if (senses.IsOutsideNextZone)
                return BotState.MoveToZone;

            return BotState.Roam;
        }

        /// <summary>Durum adı (hata ayıklama / HUD). Türkçe.</summary>
        public static string GetStateName(BotState state)
        {
            switch (state)
            {
                case BotState.Idle: return "Beklemede";
                case BotState.Loot: return "Malzeme topluyor";
                case BotState.Roam: return "Keşifte";
                case BotState.MoveToZone: return "Bölgeye intikal";
                case BotState.Engage: return "Çatışmada";
                case BotState.Investigate: return "Araştırıyor";
                case BotState.Heal: return "Tedavi";
                case BotState.Flee: return "Geri çekiliyor";
                case BotState.Follow: return "Takipte";
                case BotState.Hold: return "Mevzide";
                case BotState.Assault: return "Taarruzda";
                default: return state.ToString();
            }
        }
    }
}
