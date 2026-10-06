using UnityEngine;

namespace Project.Infrastructure.AI
{
    /// <summary>
    /// Tim taktiği için saf (durumsuz, Physics'siz, GC'siz) yardımcılar: kanat dolaşma, bastırma ateşi,
    /// sıçramalı ilerleme (bounding overwatch), bomba değerlendirmesi ve iyileşmek için geri çekilme kararı.
    /// </summary>
    public static class BotSquadTactics
    {
        public const float FlankMinAngle = 30f;
        public const float FlankMaxAngle = 60f;
        public const float BoundPeriodSeconds = 4f;
        public const float SuppressWindowSeconds = 3.5f;
        public const float RetreatHealthFraction = 0.35f;
        public const float GrenadeCoverMaxDistance = 25f;

        /// <summary>Ateş timi: 0 = bastırma (yerinde ateş), 1 = manevra (kanat/sıçrama). Formasyon sırasına göre.</summary>
        public static int FireTeam(int formationIndex)
        {
            return formationIndex <= 0 ? 0 : (formationIndex & 1);
        }

        /// <summary>Kanat açısı (30–60°): üye sırasına göre deterministik, 0..1 jitter ile.</summary>
        public static float FlankAngle(int formationIndex, float jitter01)
        {
            var t = Mathf.Clamp01(((formationIndex * 7) % 10) / 9f * 0.7f + jitter01 * 0.3f);
            return Mathf.Lerp(FlankMinAngle, FlankMaxAngle, t);
        }

        /// <summary>Kanat noktası: hedefin etrafında, bizim hattımızdan ±angle dönmüş yönde, distance uzaklıkta.</summary>
        public static Vector3 FlankPoint(Vector3 self, Vector3 enemy, float angleDegrees, float side, float distance)
        {
            var fromEnemy = self - enemy;
            fromEnemy.y = 0f;
            if (fromEnemy.sqrMagnitude < 0.01f)
                fromEnemy = Vector3.back;

            fromEnemy.Normalize();
            var rotated = Quaternion.Euler(0f, (side >= 0f ? 1f : -1f) * angleDegrees, 0f) * fromEnemy;
            return enemy + rotated * Mathf.Max(6f, distance);
        }

        /// <summary>Kanat dolaşma uygun mu: sağlıklı, orta mesafe, manevra timi, saldırgan.</summary>
        public static bool ShouldFlank(int formationIndex, float distance, float health01, float aggression01, float roll01)
        {
            if (FireTeam(formationIndex) != 1)
                return false;
            if (health01 < 0.6f || distance < 18f || distance > 95f)
                return false;
            return roll01 < 0.25f + aggression01 * 0.5f;
        }

        /// <summary>
        /// Bastırma ateşi: hedef son kez yakın zamanda görüldü, bilinen konum var, mermi yeterli.
        /// <paramref name="window"/> varsayılanı eski sabit pencere; bot beyni RC2 v3 4-8 sn penceresini (BotCombatRules.SuppressWindowSeconds) geçirir.
        /// </summary>
        public static bool ShouldSuppress(float secondsSinceSeen, bool hasLastKnown, float ammo01, float health01,
            float window = SuppressWindowSeconds)
        {
            return hasLastKnown && secondsSinceSeen > 0.3f && secondsSinceSeen < window &&
                   ammo01 > 0.25f && health01 > RetreatHealthFraction;
        }

        /// <summary>Sıçramalı ilerleme: bu üye şu an hareket etme sırasında mı (diğer ekip ateş korur).</summary>
        public static bool MovesThisPhase(int formationIndex, float now)
        {
            var phase = (int)(now / BoundPeriodSeconds) & 1;
            return FireTeam(formationIndex) == phase;
        }

        /// <summary>Bir sonraki sıçrama evresine kalan süre (sn) — tim üyeleri evre sınırında yeniden karar verir.</summary>
        public static float SecondsToNextPhase(float now)
        {
            var t = now % BoundPeriodSeconds;
            return Mathf.Max(0.2f, BoundPeriodSeconds - t);
        }

        /// <summary>
        /// Çatışmada sıçrama uygun mu: orta mesafe, sağlıklı, baskı düşük, en az bir yakın dost (ateş korumak için).
        /// </summary>
        public static bool ShouldBound(float distance, float health01, float suppression01, int nearbyAllies)
        {
            return nearbyAllies >= 1 && distance > 20f && distance < 75f && health01 > 0.6f && suppression01 < 0.4f;
        }

        /// <summary>Sıçrama hedefi: düşmana doğru boundLength kadar, hatta dik lateral kaydırmayla (hat dışına çıkmak için).</summary>
        public static Vector3 BoundPoint(Vector3 self, Vector3 enemy, float boundLength, float lateral)
        {
            var toEnemy = enemy - self;
            toEnemy.y = 0f;
            if (toEnemy.sqrMagnitude < 0.01f)
                return self;

            var forward = toEnemy.normalized;
            var right = new Vector3(forward.z, 0f, -forward.x);
            return self + forward * boundLength + right * lateral;
        }

        /// <summary>
        /// Bomba: siperdeki (çömelmiş/yatan ya da sabit) düşman aralığın içinde (varsayılan 9–25 m; bot beyni RC2 v3 12–30 m
        /// <see cref="BotCombatRules.FragMinRange"/>/<see cref="BotCombatRules.FragMaxRange"/> geçirir) ve müttefik patlama yarıçapının dışında.
        /// </summary>
        public static bool GrenadeWorthwhile(float distance, bool targetInCover, float nearestAllyToTarget, float blastRadius,
            float minDistance = 9f, float maxDistance = GrenadeCoverMaxDistance)
        {
            if (!targetInCover || distance < minDistance || distance > maxDistance)
                return false;
            return nearestAllyToTarget > blastRadius + 2.5f;
        }

        /// <summary>İyileşmek için geri çekil: can çok düşük, ateş altında ve iyileştirme eşyası var.</summary>
        public static bool ShouldRetreatToHeal(float health01, bool underFire, bool hasHealItem)
        {
            return hasHealItem && underFire && health01 < RetreatHealthFraction;
        }

        /// <summary>Hizalama toleransı içinde mi (bastırma atışı için yön farkı, derece).</summary>
        public static bool AimedAt(float yawOffDegrees, float distance)
        {
            var tolerance = Mathf.Max(3f, Mathf.Atan2(1.2f, Mathf.Max(1f, distance)) * Mathf.Rad2Deg);
            return Mathf.Abs(yawOffDegrees) <= tolerance;
        }
    }
}
