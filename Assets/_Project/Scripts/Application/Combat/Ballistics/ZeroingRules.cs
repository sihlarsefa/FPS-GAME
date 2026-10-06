using System;
using Project.Core.Domain;

namespace Project.Application.Combat.Ballistics
{
    /// <summary>
    /// Zeroing (gez ayarı): 100/200/300 m. Nişan hattı namlu ekseninin <see cref="SightHeight"/> kadar üstündedir;
    /// seçilen menzilde mermi nişan hattını keser. Saf matematik, küçük açı yaklaşımı.
    /// </summary>
    public static class ZeroingRules
    {
        public const float SightHeight = 0.045f;
        public static readonly float[] Steps = { 100f, 200f, 300f };

        public static float ClampToStep(float requested)
        {
            var best = Steps[0];
            var bd = Math.Abs(requested - best);
            for (var i = 1; i < Steps.Length; i++)
            {
                var d = Math.Abs(requested - Steps[i]);
                if (d < bd) { bd = d; best = Steps[i]; }
            }
            return best;
        }

        /// <summary>Sonraki gez adımı (300'den sonra 100'e döner).</summary>
        public static float NextStep(float current)
        {
            var c = ClampToStep(current);
            for (var i = 0; i < Steps.Length; i++)
                if (Math.Abs(Steps[i] - c) < 0.01f) return Steps[(i + 1) % Steps.Length];
            return Steps[0];
        }

        /// <summary>Namlu eksenine uygulanan yükseliş (radyan): zeroRange'de nişan hattını kesecek şekilde.</summary>
        public static float ElevationRadians(float muzzleVelocity, AmmoType ammo, float zeroRange)
        {
            if (zeroRange <= 0f) return 0f;
            var drop = BallisticsTable.Drop(muzzleVelocity, ammo, zeroRange);
            return (float)Math.Atan((SightHeight + drop) / zeroRange);
        }

        /// <summary>İsabet noktasının nişan hattına göre dikey sapması (m): + yukarı, - aşağı.</summary>
        public static float ImpactOffset(float muzzleVelocity, AmmoType ammo, float zeroRange, float targetRange)
        {
            if (targetRange <= 0f) return 0f;
            var elev = ElevationRadians(muzzleVelocity, ammo, zeroRange);
            var rise = targetRange * (float)Math.Tan(elev);
            return rise - BallisticsTable.Drop(muzzleVelocity, ammo, targetRange) - SightHeight;
        }

        /// <summary>Atış yönünü dikey eksende yükseltir (yatay yön birimi, y bileşeni değişir); birim vektör döner.</summary>
        public static void ApplyElevation(float dirX, float dirY, float dirZ, float elevationRad,
            out float ox, out float oy, out float oz)
        {
            var horiz = (float)Math.Sqrt(dirX * dirX + dirZ * dirZ);
            var pitch = (float)Math.Atan2(dirY, horiz) + elevationRad;
            var cp = (float)Math.Cos(pitch);
            var sp = (float)Math.Sin(pitch);
            if (horiz < 1e-6f) { ox = 0f; oy = sp; oz = cp; return; }
            ox = dirX / horiz * cp;
            oz = dirZ / horiz * cp;
            oy = sp;
        }
    }
}
