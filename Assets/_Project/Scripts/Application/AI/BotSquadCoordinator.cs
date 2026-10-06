using System;

namespace Project.Application.AI
{
    public enum SquadRole
    {
        Anchor = 0,      // yerinde tutar, ana bastırma
        Pointman = 1,    // öne çıkar, ilk temas
        Flanker = 2,     // yan rota
        Suppressor = 3,  // ağır silah, baskı
        Support = 4      // yedek, iyileştirme/bomba
    }

    /// <summary>
    /// Takım koordinasyonu (saf mantık): rol dağıtımı, odak ateşi (hedef yığılmasını önleme), telsiz konuşma kısıtı
    /// ve "ateş-hareket" tetikleyicileri. Squad/Insurgency'deki bot grubu davranışı: biri bastırır, diğeri hareket eder,
    /// aynı hedefe hepsi ateş etmez (yeni tehdide bir kişi bakar).
    /// </summary>
    public static class BotSquadCoordinator
    {
        public const int MaxShootersPerTarget = 3;
        public const float CalloutCooldownSeconds = 2.5f;

        /// <summary>
        /// Rol ata: sıra (0 = lider) ve ağır silah/keskin nişancı bayrağı. suppressorSlots: bastırıcı üst sınırı.
        /// Kural: ağır silah → Suppressor; sıra 1 → Pointman; çift sıralar → Flanker; tek → Support; lider = Anchor.
        /// </summary>
        public static SquadRole AssignRole(int formationIndex, bool hasHeavyWeapon, int existingSuppressors, int suppressorSlots)
        {
            if (formationIndex <= 0) return SquadRole.Anchor;
            if (hasHeavyWeapon && existingSuppressors < suppressorSlots) return SquadRole.Suppressor;
            if (formationIndex == 1) return SquadRole.Pointman;
            return (formationIndex & 1) == 0 ? SquadRole.Flanker : SquadRole.Support;
        }

        /// <summary>
        /// Hedef puanı (YÜKSEK = iyi): tehdit (yakın/ateş eden) + görünürlük − yığılma cezası.
        /// </summary>
        public static float TargetScore(float distance, bool isShootingAtUs, bool isVisibleNow, int alliesAlreadyTargeting,
            float targetHealth01, bool isRocketOrHeavy)
        {
            var s = 100f - Math.Min(distance, 100f);
            if (isShootingAtUs) s += 35f;
            if (isVisibleNow) s += 20f;
            if (isRocketOrHeavy) s += 25f;
            s += (1f - Clamp01(targetHealth01)) * 15f;        // yaralıyı bitir
            s -= Math.Max(0, alliesAlreadyTargeting) * 22f;    // odak dağılsın
            return s;
        }

        /// <summary>Bu hedefe ateş etmeye hakkım var mı (yığılma sınırı). Yakın tehdit/doğrudan ateş altındaysa sınır yok.</summary>
        public static bool MayEngageTarget(int alliesAlreadyTargeting, bool targetingMeDirectly, float distance)
        {
            if (targetingMeDirectly || distance < 12f) return true;
            return alliesAlreadyTargeting < MaxShootersPerTarget;
        }

        /// <summary>Telsiz konuşması yapılabilir mi (aynı kategori için soğuma; öncelikli çağrı (düşman gördü) kısaltılmış).</summary>
        public static bool MayCallout(float now, float lastCalloutTime, bool urgent, int alliesTalkingNow)
        {
            if (alliesTalkingNow >= 2 && !urgent) return false;
            var cd = urgent ? CalloutCooldownSeconds * 0.4f : CalloutCooldownSeconds;
            return now - lastCalloutTime >= cd;
        }

        /// <summary>Rolüne göre taktik tercih ağırlıkları (0..1): saldır / kanat / bastır / siperde kal.</summary>
        public static void RoleBias(SquadRole role, out float push, out float flank, out float suppress, out float hold)
        {
            switch (role)
            {
                case SquadRole.Pointman: push = 0.8f; flank = 0.1f; suppress = 0.2f; hold = 0.2f; break;
                case SquadRole.Flanker: push = 0.3f; flank = 0.85f; suppress = 0.1f; hold = 0.1f; break;
                case SquadRole.Suppressor: push = 0.05f; flank = 0f; suppress = 0.95f; hold = 0.6f; break;
                case SquadRole.Support: push = 0.3f; flank = 0.2f; suppress = 0.4f; hold = 0.4f; break;
                default: push = 0.2f; flank = 0.05f; suppress = 0.6f; hold = 0.8f; break;
            }
        }

        /// <summary>
        /// Ateş-hareket: ben hareket edebilir miyim? Yeterli sayıda dost bastırma ateşi veriyor olmalı.
        /// movers: şu an hareket eden dost, suppressors: şu an bastıran dost.
        /// </summary>
        public static bool MayMove(int movers, int suppressors, int squadSize)
        {
            if (squadSize <= 1) return true;
            var maxMovers = Math.Max(1, squadSize / 2);
            return suppressors >= 1 && movers < maxMovers;
        }

        /// <summary>Dost kaybı sonrası morali: kayıp oranına göre saldırganlık çarpanı (0.5..1).</summary>
        public static float MoraleMultiplier(int alive, int startSize, float secondsSinceLastLoss)
        {
            if (startSize <= 0) return 1f;
            var lossRatio = 1f - Clamp01(alive / (float)startSize);
            var recovery = Clamp01(secondsSinceLastLoss / 25f);
            var m = 1f - lossRatio * 0.6f * (1f - recovery * 0.7f);
            return Math.Max(0.5f, Math.Min(1f, m));
        }

        /// <summary>Dost ateşi güvenliği: ateş hattında dost, hedefe mesafenin içinde ve hatta (açı) yakın mı.</summary>
        public static bool LineOfFireBlockedByAlly(float shooterToTarget, float shooterToAlly, float angleBetweenDegrees)
        {
            if (shooterToAlly >= shooterToTarget) return false;
            var tolerance = Math.Max(3f, (float)Math.Atan2(0.9f, Math.Max(1f, shooterToAlly)) * 57.29578f);
            return Math.Abs(angleBetweenDegrees) <= tolerance;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
