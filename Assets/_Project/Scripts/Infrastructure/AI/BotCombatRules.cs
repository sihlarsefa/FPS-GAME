using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.AI
{
    /// <summary>Siperde atış döngüsü evresi.</summary>
    public enum CoverPeekPhase
    {
        Hidden = 0,
        Peek = 1
    }

    /// <summary>
    /// Disiplinli asker kuralları — saf, Physics'siz, GC'siz (EditMode'da test edilir):
    /// siper skoru, siperden çıkıp-ateş-edip-saklanma döngüsü, geri çekilme, baskıda kanat, işitme menzili,
    /// sis altında yaralı kaldırma, bomba ile siperden çıkarma.
    /// </summary>
    public static class BotCombatRules
    {
        // ------------------------------------------------------------------ siper skoru

        /// <summary>
        /// Siper adayı skoru (DÜŞÜK = iyi). travel: bota yürüme mesafesi; toThreat: aday → ana tehdit; selfToThreat: bot → tehdit;
        /// preferredRange: silahın tercih ettiği çatışma mesafesi; hiddenFromSecondary: ikinci tehdit yönünden de gizli mi
        /// (hasSecondary=false ise sayılmaz); allyDistance: en yakın dost uzaklığı (kopmamak için), high: yüksek siper (&gt;=1.4 m).
        /// </summary>
        public static float CoverScore(float travel, float toThreat, float selfToThreat, float preferredRange,
            bool hasSecondary, bool hiddenFromSecondary, float allyDistance, bool high, float suppression01)
        {
            var score = travel;

            // Tehdide yaklaştıran siper pahalı.
            score += Mathf.Max(0f, selfToThreat - toThreat) * 1.5f;

            // Silahın etkili menzilinin çok dışındaki siper (çok uzak ya da dibinde) değersiz: menzil sapması.
            var range = Mathf.Max(8f, preferredRange);
            if (toThreat > range * 1.6f)
                score += (toThreat - range * 1.6f) * 0.25f;
            else if (toThreat < range * 0.35f)
                score += (range * 0.35f - toThreat) * 0.5f;

            // İkinci tehdit yönünden açıkta kalmak ağır ceza.
            if (hasSecondary && !hiddenFromSecondary)
                score += 14f;

            // Tim bütünlüğü: dosttan uzak siper (tek başına kalma) cezası.
            score += Mathf.Clamp(allyDistance - 18f, 0f, 40f) * 0.3f;

            // Baskı altında yürüme mesafesi daha pahalı (yakın siper tercih edilir); yüksek siper bonusu.
            score += travel * Mathf.Clamp01(suppression01) * 0.8f;
            if (high)
                score -= 2.5f;

            return score;
        }

        // ------------------------------------------------------------------ siperden çık-ateş et-saklan

        /// <summary>Saklanma süresi (sn): baskı yükseldikçe uzar, beceri kısaltır.</summary>
        public static float HiddenSeconds(float suppression01, float skill01, float rng01)
        {
            var baseSeconds = Mathf.Lerp(0.6f, 1.7f, Mathf.Clamp01(rng01));
            baseSeconds *= Mathf.Lerp(1.25f, 0.8f, Mathf.Clamp01(skill01));
            return baseSeconds * (1f + Mathf.Clamp01(suppression01) * 1.4f);
        }

        /// <summary>Başı çıkarıp ateş etme süresi (sn): baskı kısaltır (kör, çabuk saklanma).</summary>
        public static float PeekSeconds(float suppression01, float skill01, float rng01)
        {
            var baseSeconds = Mathf.Lerp(1.3f, 2.6f, Mathf.Clamp01(rng01)) * Mathf.Lerp(0.85f, 1.15f, Mathf.Clamp01(skill01));
            return Mathf.Max(0.6f, baseSeconds * (1f - Mathf.Clamp01(suppression01) * 0.5f));
        }

        /// <summary>Saklanma bitince siperden çıkma isteği (olasılık 0..1): baskı ve düşük can düşürür, beceri artırır.</summary>
        public static float PeekWillingness(float suppression01, float skill01, float health01)
        {
            var s = Mathf.Clamp01(suppression01);
            var p = 1f - s * Mathf.Lerp(0.9f, 0.5f, Mathf.Clamp01(skill01));
            p -= (1f - Mathf.Clamp01(health01)) * 0.3f;
            return Mathf.Clamp(p, 0.08f, 1f);
        }

        /// <summary>Evre ilerlet: süre doldu mu?</summary>
        public static bool PhaseElapsed(float now, float phaseEnd)
        {
            return now >= phaseEnd;
        }

        // ------------------------------------------------------------------ geri çekilme / kanat

        /// <summary>
        /// Sayıca üstün/sağlıksız: geri çekil. enemies = görünen düşman, allies = yakın dost (kendisi hariç).
        /// </summary>
        public static bool ShouldFallBack(float health01, int enemies, int allies, float suppression01, float ammo01, float aggression01)
        {
            if (enemies <= 0)
                return false;

            var outnumbered = enemies >= allies + 2 || (enemies >= 2 && enemies >= (allies + 1) * 2);
            var weak = health01 < 0.35f || (health01 < 0.5f && ammo01 < 0.2f);
            var pinnedHard = suppression01 > 0.8f && enemies >= 2 && health01 < 0.7f;

            if (weak && enemies >= 1 && allies == 0)
                return true;

            if (!outnumbered && !pinnedHard)
                return false;

            // Saldırgan askerler biraz daha direnir.
            var courage = Mathf.Lerp(0f, 0.15f, Mathf.Clamp01(aggression01));
            return health01 < 0.8f + courage || suppression01 > 0.5f || enemies >= allies + 3;
        }

        /// <summary>Bastırılmış (yerinde kalmış) asker kanat dolaşsın mı: uzun süre sıkıştı, sağlıklı, az düşman, orta mesafe.</summary>
        public static bool ShouldFlankWhenPinned(float pinnedSeconds, float suppression01, float health01, float distance, int enemies, float roll01, float aggression01)
        {
            if (pinnedSeconds < 3.5f || health01 < 0.5f || enemies > 2)
                return false;
            if (distance < 14f || distance > 90f)
                return false;
            if (suppression01 > 0.9f)
                return false; // tamamen ezilmiş: önce siper
            return roll01 < 0.3f + aggression01 * 0.45f;
        }

        /// <summary>Kanat açısı baskıda daha geniş (45–75°) — hat dışına çık.</summary>
        public static float PinnedFlankAngle(float jitter01)
        {
            return Mathf.Lerp(45f, 75f, Mathf.Clamp01(jitter01));
        }

        /// <summary>Baskı sürekli yüksekse siper noktasına sürünerek git (yakınsa); aksi halde koş.</summary>
        public static bool ShouldCrawlToCover(float suppression01, float coverDistance)
        {
            return suppression01 > 0.6f && coverDistance > 0.6f && coverDistance <= 6f;
        }

        // ------------------------------------------------------------------ bomba ile siperden çıkarma

        /// <summary>
        /// Siperdeki hedefe bomba isteği (olasılık): hedef ne kadar süredir gizli, kendimiz sıkıştık mı, bombacı mıyız.
        /// </summary>
        public static float FlushGrenadeChance(bool isGrenadier, float aggression01, float secondsHidden, float pinnedSeconds)
        {
            var chance = isGrenadier ? 0.3f : 0.1f;
            chance += aggression01 * 0.15f;
            chance += Mathf.Clamp(secondsHidden - 2f, 0f, 6f) * 0.05f;
            chance += Mathf.Clamp(pinnedSeconds, 0f, 8f) * 0.04f;
            return Mathf.Clamp01(chance);
        }

        // ------------------------------------------------------------------ RC2 v3: kapatıcı ateş

        /// <summary>Kapatıcı (baskı) ateşi yapabilen silah: LMG/AR (ve SMG yakında).</summary>
        public static bool IsSuppressiveWeapon(WeaponCategory category)
        {
            return category == WeaponCategory.Lmg || category == WeaponCategory.AssaultRifle;
        }

        /// <summary>Siper kenarına kapatıcı seri boyu: 3..5 mermi.</summary>
        public static int SuppressiveBurstRounds(float rng01)
        {
            return 3 + Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(rng01) * 3f), 0, 2);
        }

        /// <summary>Kapatıcı ateş penceresi: 4..8 sn.</summary>
        public static float SuppressWindowSeconds(float rng01)
        {
            return Mathf.Lerp(4f, 8f, Mathf.Clamp01(rng01));
        }

        /// <summary>
        /// Hedef siper kenarına kapatıcı ateş: LMG/AR, yeterli mermi, kanat hareketi var (ya da yok ama LMG), menzil içinde.
        /// </summary>
        public static bool ShouldSuppressCoverEdge(WeaponCategory category, float ammo01, bool targetInCover, bool flankerMoving, float distance)
        {
            if (!targetInCover || !IsSuppressiveWeapon(category) || ammo01 < 0.3f || distance > 120f || distance < 8f)
                return false;
            return flankerMoving || category == WeaponCategory.Lmg;
        }

        // ------------------------------------------------------------------ RC2 v3: bomba v2

        public const float FragMinRange = 12f;
        public const float FragMaxRange = 30f;

        /// <summary>Bomba mesafe aralığı 12-30 m.</summary>
        public static bool FragRangeOk(float distance)
        {
            return distance >= FragMinRange && distance <= FragMaxRange;
        }

        /// <summary>Pişirme süresi: hedef alçak siper arkasında sabitse 1.5 sn, aksi halde 0.</summary>
        public static float FragCookSeconds(bool targetStatic, bool lowCover, float fuseSeconds)
        {
            if (!targetStatic || !lowCover)
                return 0f;
            return Mathf.Min(1.5f, Mathf.Max(0f, fuseSeconds - 1.2f));
        }

        /// <summary>Patlama yarıçapında dost varsa asla atma (emniyet payı 2 m).</summary>
        public static bool FragSafeFromFriendlies(float nearestAllyToBlast, float blastRadius)
        {
            return nearestAllyToBlast > blastRadius + 2f;
        }

        // ------------------------------------------------------------------ RC2 v3: kapı / bina girişi

        public enum DoorEntryPhase { Approach = 0, Pause = 1, Peek = 2, Enter = 3 }

        /// <summary>Kapı girişi evre süresi (sn): Pause 0.6-1.0, Peek 0.8-1.4; diğerleri 0.</summary>
        public static float DoorPhaseSeconds(DoorEntryPhase phase, float rng01)
        {
            var r = Mathf.Clamp01(rng01);
            switch (phase)
            {
                case DoorEntryPhase.Pause: return Mathf.Lerp(0.6f, 1f, r);
                case DoorEntryPhase.Peek: return Mathf.Lerp(0.8f, 1.4f, r);
                default: return 0f;
            }
        }

        /// <summary>Sonraki evre; Enter sonda kalır.</summary>
        public static DoorEntryPhase NextDoorPhase(DoorEntryPhase phase)
        {
            return phase == DoorEntryPhase.Enter ? phase : (DoorEntryPhase)((int)phase + 1);
        }

        /// <summary>Kapıda durup bakma: kapıya 2.5 m kala ve kapı kilitli değilken.</summary>
        public static bool ShouldPauseAtDoor(float distanceToDoor, bool locked, bool underFire)
        {
            return !locked && !underFire && distanceToDoor <= 2.5f;
        }

        // ------------------------------------------------------------------ RC2 v3: yaralı

        /// <summary>Bacak yarası: topallayarak sipere git (hız çarpanı 0.55); yaralı değilse 1.</summary>
        public static float LimpSpeedScale(bool legWounded)
        {
            return legWounded ? 0.55f : 1f;
        }

        /// <summary>Bandaj: güvenli (ateş yok, görünür düşman yok), eşya var, yaralı/kanama.</summary>
        public static bool ShouldBandageNow(bool legWounded, float health01, bool hasBandage, float secondsSinceThreat, int visibleEnemies)
        {
            if (!hasBandage || visibleEnemies > 0 || secondsSinceThreat < 4f)
                return false;
            return legWounded || health01 < 0.7f;
        }

        // ------------------------------------------------------------------ RC2 v3: son adam

        /// <summary>Tek kalan asker: saldırganlık düşer (x0.35), siperde kaplumbağa.</summary>
        public static float LastManAggression(float aggression01, int aliveTeammates)
        {
            return aliveTeammates <= 0 ? aggression01 * 0.35f : aggression01;
        }

        /// <summary>Son adam komutansa topçu çağır (tehdit var, hazır).</summary>
        public static bool LastManShouldCallArtillery(int aliveTeammates, bool isCommander, bool artilleryReady, bool hasThreat)
        {
            return aliveTeammates <= 0 && isCommander && artilleryReady && hasThreat;
        }

        // ------------------------------------------------------------------ sis altında kaldırma

        /// <summary>
        /// Ateş altında yaralı kaldırmaya izin: sis tehdit hattını kesiyor, kendi canı yeterli, baskı düşük, kurban yakın.
        /// </summary>
        public static bool CanReviveUnderSmoke(bool smokeBlocksThreat, float health01, float suppression01, float victimDistance, int visibleEnemies)
        {
            if (!smokeBlocksThreat)
                return false;
            if (health01 < 0.45f || suppression01 > 0.55f || victimDistance > 28f)
                return false;
            return visibleEnemies <= 1;
        }

        /// <summary>Yaralıyı örtmek için sis atılsın mı: tehdit var, hat açık, kurban yakın, sis eli var.</summary>
        public static bool ShouldSmokeForRevive(bool hasThreat, bool smokeBlocksThreat, bool hasSmoke, float victimDistance)
        {
            return hasThreat && !smokeBlocksThreat && hasSmoke && victimDistance <= 30f;
        }

        // ------------------------------------------------------------------ işitme

        /// <summary>
        /// Silah sesi işitme menzili (m): taban (profil) × şiddet × engel çarpanı. Her engel %35 keser (en az %25 kalır).
        /// </summary>
        public static float GunfireAudibleRange(float hearingDistance, float loudness, int blockers)
        {
            return hearingDistance * Mathf.Max(0.1f, loudness) * OcclusionFactor(blockers);
        }

        /// <summary>Engel sayısına göre ses çarpanı (0 engel = 1; 1 = 0.65; 2 = 0.42; 3+ = 0.27).</summary>
        public static float OcclusionFactor(int blockers)
        {
            var f = 1f;
            for (var i = 0; i < Mathf.Clamp(blockers, 0, 3); i++)
                f *= 0.65f;
            return Mathf.Max(0.25f, f);
        }

        /// <summary>
        /// Ayak sesi menzili (m): hız ve duruşa göre. Depar 28, koşu 22, yürüyüş 12, çömelik 5, yatan 2; durağan 0.
        /// </summary>
        public static float FootstepRange(float speed, bool crouching, bool prone)
        {
            if (speed < 0.6f)
                return 0f;
            if (prone)
                return 2f;
            if (crouching)
                return 5f;
            if (speed > 5f)
                return 28f;
            if (speed > 3f)
                return 22f;
            return 12f;
        }

        /// <summary>Duyulan konumun belirsizliği (m): mesafe ve engellemeyle büyür.</summary>
        public static float HeardPositionError(float distance, int blockers)
        {
            return distance * (0.08f + 0.05f * Mathf.Clamp(blockers, 0, 3));
        }
    }
}
