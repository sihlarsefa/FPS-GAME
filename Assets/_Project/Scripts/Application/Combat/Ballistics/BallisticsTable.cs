using System;
using Project.Application.Services;
using Project.Core.Domain;

namespace Project.Application.Combat.Ballistics
{
    /// <summary>
    /// Mermi tipine göre hava direnci tablosu. Temel katsayı BallisticsMath.DragPerMeter'dan gelir;
    /// bunun üstüne ses altı/geçiş (transonik) hız bölgesi için hıza bağlı bir çarpan eklenir.
    /// </summary>
    public static class BallisticsTable
    {
        /// <summary>Deniz seviyesi, 15 C ses hızı (m/sn).</summary>
        public const float SpeedOfSound = 340f;

        /// <summary>Transonik bölgenin (Mach 0.9..1.2) tepe sürükleme çarpanı.</summary>
        public const float TransonicPeakFactor = 1.6f;

        /// <summary>Ses altı sürükleme çarpanı (Mach &lt; 0.9): mermi gövde sürüklemesi düşer.</summary>
        public const float SubsonicFactor = 0.8f;

        /// <summary>Hıza bağlı sürükleme çarpanı (0.8 .. 1.6).</summary>
        public static float DragFactor(float speed)
        {
            var mach = speed / SpeedOfSound;
            if (mach < 0.9f) return SubsonicFactor;
            if (mach <= 1.05f) // yükselen kol
                return SubsonicFactor + (TransonicPeakFactor - SubsonicFactor) * ((mach - 0.9f) / 0.15f);
            if (mach <= 1.3f) // düşen kol: 1.6 -> 1.0
                return TransonicPeakFactor + (1f - TransonicPeakFactor) * ((mach - 1.05f) / 0.25f);
            return 1f;
        }

        /// <summary>Verilen hızda metre başına üstel kayıp katsayısı.</summary>
        public static float DragPerMeter(AmmoType ammo, float speed) =>
            BallisticsMath.DragPerMeter(ammo) * DragFactor(speed);

        /// <summary>
        /// Sayısal entegrasyonla menzildeki hız ve uçuş süresi (sabit adım). Saf; Unity gerektirmez.
        /// </summary>
        public static void Integrate(float muzzleVelocity, AmmoType ammo, float range, out float speed, out float time)
        {
            speed = muzzleVelocity > 0f ? muzzleVelocity : 0f;
            time = 0f;
            if (muzzleVelocity <= 1f || range <= 0f)
                return;

            const float step = 2f;
            var x = 0f;
            while (x < range && speed > 1f)
            {
                var dx = Math.Min(step, range - x);
                time += dx / speed;
                speed *= (float)Math.Exp(-DragPerMeter(ammo, speed) * dx);
                x += dx;
            }
        }

        public static float SpeedAt(float muzzleVelocity, AmmoType ammo, float range)
        {
            Integrate(muzzleVelocity, ammo, range, out var s, out _);
            return s;
        }

        public static float TimeOfFlight(float muzzleVelocity, AmmoType ammo, float range)
        {
            Integrate(muzzleVelocity, ammo, range, out _, out var t);
            return t;
        }

        /// <summary>Namlu hattına göre düşüş (m), 0.5 g t².</summary>
        public static float Drop(float muzzleVelocity, AmmoType ammo, float range)
        {
            var t = TimeOfFlight(muzzleVelocity, ammo, range);
            return 0.5f * BallisticsMath.Gravity * t * t;
        }
    }
}
