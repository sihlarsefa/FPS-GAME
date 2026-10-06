using System;
using Project.Core.Domain;

namespace Project.Application.Combat.Ballistics
{
    /// <summary>Ses hızı gecikmesi: süpersonik merminin şok dalgası (crack) ile namlu sesinin (thump) varış farkı.</summary>
    public static class SupersonicSoundRules
    {
        /// <summary>Şok dalgası duyulabilir yakın geçiş yarıçapı (m).</summary>
        public const float CrackMaxMissDistance = 12f;

        public static bool IsSupersonic(float speed) => speed > BallisticsTable.SpeedOfSound;

        /// <summary>Namlu sesinin dinleyiciye varış süresi (sn) — mesafe / ses hızı.</summary>
        public static float ThumpDelay(float distance) =>
            distance <= 0f ? 0f : distance / BallisticsTable.SpeedOfSound;

        /// <summary>Merminin dinleyici hizasına varış süresi (sn); crack bu anda duyulur.</summary>
        public static float CrackTime(float muzzleVelocity, AmmoType ammo, float distance) =>
            BallisticsTable.TimeOfFlight(muzzleVelocity, ammo, distance);

        /// <summary>Mermi bu mesafede hâlâ süpersonik ve yakın geçişse crack var mı?</summary>
        public static bool HasCrack(float muzzleVelocity, AmmoType ammo, float distance, float missDistance)
        {
            if (missDistance > CrackMaxMissDistance) return false;
            return IsSupersonic(BallisticsTable.SpeedAt(muzzleVelocity, ammo, distance));
        }

        /// <summary>
        /// Thump varışı ile crack varışı arasındaki fark (sn). Süpersonik mermide >= 0 (önce crack, sonra thump);
        /// ses altı mermide crack yoktur, 0 döner (ses ve mermi birlikte ya da mermi sonra gelir).
        /// </summary>
        public static float CrackThumpGap(float muzzleVelocity, AmmoType ammo, float distance)
        {
            var gap = ThumpDelay(distance) - CrackTime(muzzleVelocity, ammo, distance);
            return gap > 0f ? gap : 0f;
        }
    }
}
