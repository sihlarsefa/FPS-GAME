using System;

namespace Project.Core.Domain
{
    /// <summary>Shader'a giden rüzgâr durumu (saf).</summary>
    public struct WindState
    {
        public float DirX, DirZ;
        /// <summary>Genel güç 0..1.5.</summary>
        public float Strength;
        /// <summary>Esinti (gust) 0..1.</summary>
        public float Gust;
        /// <summary>Hız çarpanı 0..1.</summary>
        public float Speed;
        /// <summary>Yaprak titremesi 0..1.</summary>
        public float Turbulence;
        public bool Enabled;
    }

    /// <summary>Hava durumundan rüzgâr parametreleri (saf). WeatherKind: 0 Açık, 1 Yağmur, 2 Kar.</summary>
    public static class WindRules
    {
        /// <summary>Düşük kademede rüzgâr hareketi kapalı; Orta titremesiz; Yüksek/Ultra tam.</summary>
        public static WindState ForWeather(int weather, int tier, float baseAngleDeg)
        {
            var s = new WindState();
            float strength, gust, speed, turb;
            switch (weather)
            {
                case 1: strength = 0.8f; gust = 0.6f; speed = 0.6f; turb = 0.55f; break;
                case 2: strength = 0.5f; gust = 0.35f; speed = 0.4f; turb = 0.3f; break;
                default: strength = 0.35f; gust = 0.25f; speed = 0.3f; turb = 0.25f; break;
            }

            tier = GrassRules.Clamp(tier, 0, 3);
            if (tier <= 0)
            {
                strength = 0f; gust = 0f; speed = 0f; turb = 0f;
            }
            else if (tier == 1)
            {
                turb = 0f; gust *= 0.5f;
            }

            DirFromAngle(baseAngleDeg, out s.DirX, out s.DirZ);
            s.Strength = strength; s.Gust = gust; s.Speed = speed; s.Turbulence = turb;
            s.Enabled = tier > 0 && strength > 0f;
            return s;
        }

        /// <summary>Derece → birim yön (x, z). 0 derece = +z, saat yönü.</summary>
        public static void DirFromAngle(float deg, out float x, out float z)
        {
            double r = deg * Math.PI / 180.0;
            x = (float)Math.Sin(r);
            z = (float)Math.Cos(r);
        }

        /// <summary>Rüzgâr yönü yavaş salınım (derece): taban ± 25.</summary>
        public static float DriftAngle(float baseDeg, float time) => baseDeg + 25f * (float)Math.Sin(time * 0.05);

        /// <summary>Hedefe yumuşak yaklaşma (hava değişiminde rüzgârın aniden değişmemesi için).</summary>
        public static float Approach(float current, float target, float ratePerSec, float dt)
        {
            float k = 1f - (float)Math.Exp(-Math.Max(0f, ratePerSec) * Math.Max(0f, dt));
            return current + (target - current) * k;
        }

        public static WindState Approach(WindState c, WindState t, float rate, float dt)
        {
            c.DirX = t.DirX; c.DirZ = t.DirZ; // yön zaten yavaş süzülüyor
            c.Strength = Approach(c.Strength, t.Strength, rate, dt);
            c.Gust = Approach(c.Gust, t.Gust, rate, dt);
            c.Speed = Approach(c.Speed, t.Speed, rate, dt);
            c.Turbulence = Approach(c.Turbulence, t.Turbulence, rate, dt);
            c.Enabled = t.Enabled || c.Strength > 0.01f;
            return c;
        }
    }
}
