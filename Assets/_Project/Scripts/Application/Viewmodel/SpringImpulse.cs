using System;

namespace Project.Application.Viewmodel
{
    /// <summary>Yay darbe matematiği: istenen TEPE yer değiştirmesi için gereken başlangıç hızı.</summary>
    public static class SpringImpulse
    {
        /// <summary>
        /// Dengeden v0 hızıyla fırlatılan sönümlü yayın (x'' = -k x - 2 zeta w x') ilk tepe değeri:
        /// peak = v0/w * exp(-zeta/sqrt(1-zeta^2) * atan(sqrt(1-zeta^2)/zeta)); zeta>=1 için v0/w * exp(-1) (kritik).
        /// </summary>
        public static float PeakFactor(float stiffness, float zeta)
        {
            if (stiffness <= 0f) return 0f;
            var w = Math.Sqrt(stiffness);
            if (zeta <= 0.0001) return (float)(1.0 / w);
            if (zeta >= 1.0) return (float)(Math.Exp(-1.0) / w);
            var s = Math.Sqrt(1.0 - zeta * zeta);
            return (float)(Math.Exp(-zeta / s * Math.Atan(s / zeta)) / w);
        }

        /// <summary>İstenen tepe için başlangıç hızı.</summary>
        public static float VelocityForPeak(float peak, float stiffness, float zeta)
        {
            var f = PeakFactor(stiffness, zeta);
            return f <= 1e-9f ? 0f : peak / f;
        }
    }
}
