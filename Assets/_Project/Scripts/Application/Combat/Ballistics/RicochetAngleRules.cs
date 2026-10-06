using Project.Application.Services;
using Project.Core.Domain;

namespace Project.Application.Combat.Ballistics
{
    /// <summary>Sekme açısı kuralları: yüzey ve kalibreye göre kritik sıyırma açısı, çıkış açısı ve hız/hasar kaybı.</summary>
    public static class RicochetAngleRules
    {
        /// <summary>Yüzey başına kritik sıyırma açısı (derece); üstünde mermi sekmez. 0 = sekmez.</summary>
        public static float SurfaceCriticalAngle(PenetrationMaterial m)
        {
            switch (m)
            {
                case PenetrationMaterial.ThinMetal: return 28f;
                case PenetrationMaterial.Concrete: return 24f;
                case PenetrationMaterial.Brick: return 18f;
                case PenetrationMaterial.Solid: return 14f;
                default: return 0f;
            }
        }

        /// <summary>Kalibre çarpanı: tabanca ve saçma daha kolay seker (deforme olur), tüfek mermisi kritik açıyı biraz düşürür.</summary>
        public static float AmmoAngleFactor(AmmoType ammo)
        {
            switch (ammo)
            {
                case AmmoType.Gauge12: return 1.25f;
                case AmmoType.Mm9: return 1.15f;
                case AmmoType.Mm556: return 1.0f;
                case AmmoType.Mm762: return 0.9f;
                default: return 1.0f;
            }
        }

        public static float CriticalAngle(PenetrationMaterial m, AmmoType ammo) =>
            SurfaceCriticalAngle(m) * AmmoAngleFactor(ammo);

        public static bool CanRicochet(PenetrationMaterial m, AmmoType ammo, float grazingAngleDeg) =>
            grazingAngleDeg >= 0f && grazingAngleDeg < CriticalAngle(m, ammo);

        /// <summary>Sekme olasılığı: kritik açıda 0, yüzeye paralelde tavan değer.</summary>
        public static float Chance(PenetrationMaterial m, AmmoType ammo, float grazingAngleDeg)
        {
            var crit = CriticalAngle(m, ammo);
            if (crit <= 0f || grazingAngleDeg < 0f || grazingAngleDeg >= crit) return 0f;
            var t = 1f - grazingAngleDeg / crit;
            return 0.95f * t * t * (3f - 2f * t); // smoothstep
        }

        /// <summary>Sekme sonrası yüzeyden ayrılma açısı: gelen açıdan daha sığ (enerji yutulur).</summary>
        public static float ExitAngle(float grazingAngleDeg) => grazingAngleDeg < 0f ? 0f : grazingAngleDeg * 0.6f;

        /// <summary>Sekme sonrası hız oranı: sığ açıda çok korunur, kritik açıya yaklaştıkça düşer.</summary>
        public static float SpeedKeep(PenetrationMaterial m, AmmoType ammo, float grazingAngleDeg)
        {
            var crit = CriticalAngle(m, ammo);
            if (crit <= 0f) return 0f;
            var t = grazingAngleDeg / crit;
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return 0.75f - 0.35f * t; // 0.75 .. 0.40
        }

        /// <summary>Sekme sonrası hasar oranı (hız oranının karesine yakın, tabanı 0.1).</summary>
        public static float DamageKeep(PenetrationMaterial m, AmmoType ammo, float grazingAngleDeg)
        {
            var s = SpeedKeep(m, ammo, grazingAngleDeg);
            var d = s * s * 0.8f;
            return d < 0.1f ? (s > 0f ? 0.1f : 0f) : d;
        }

        /// <summary>Bir mermi en fazla bu kadar sekebilir.</summary>
        public const int MaxRicochets = 2;
    }
}
