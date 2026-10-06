using System;

namespace Project.Infrastructure.Audio.Music
{
    /// <summary>Saf (Unity'siz) miks matematiği: yan zincir (sidechain) kısma zarfı ve son limiter tavanı.</summary>
    public static class MusicMixMath
    {
        /// <summary>Son limiter tavanı: -1 dBFS.</summary>
        public const float CeilingDb = -1f;
        public static readonly float CeilingLinear = (float)Math.Pow(10.0, CeilingDb / 20.0);

        /// <summary>
        /// Tetik sinyalinden (melodi) zarf takipçisi: hızlı atak, yavaş bırakma. Döngüde dikişsiz olsun diye iki geçiş yapar
        /// (ikinci geçiş ilk geçişin son durumundan başlar). Çıktı 0..1 (tetik tepe değerine göre normalize).
        /// </summary>
        public static float[] FollowEnvelope(float[] trigger, int sampleRate, float attackSec, float releaseSec, bool loop)
        {
            var env = new float[trigger.Length];
            if (trigger.Length == 0 || sampleRate <= 0) return env;
            var aA = 1f - (float)Math.Exp(-1.0 / Math.Max(1e-4, attackSec * sampleRate));
            var aR = 1f - (float)Math.Exp(-1.0 / Math.Max(1e-4, releaseSec * sampleRate));
            float peak = 1e-6f;
            for (var i = 0; i < trigger.Length; i++) { var a = Math.Abs(trigger[i]); if (a > peak) peak = a; }
            var e = 0f;
            for (var pass = 0; pass < (loop ? 2 : 1); pass++)
                for (var i = 0; i < trigger.Length; i++)
                {
                    var x = Math.Min(1f, Math.Abs(trigger[i]) / peak);
                    e += (x > e ? aA : aR) * (x - e);
                    env[i] = e;
                }
            return env;
        }

        /// <summary>Kısma kazancı: 1 - depth * env, [1-depth, 1] aralığında.</summary>
        public static float DuckGain(float env, float depth)
        {
            if (depth < 0f) depth = 0f; else if (depth > 1f) depth = 1f;
            if (env < 0f) env = 0f; else if (env > 1f) env = 1f;
            return 1f - depth * env;
        }

        /// <summary>Yumuşak limiter: eşik altında doğrusal, üstünde tavana yumuşak yaklaşır (asla tavanı aşmaz).</summary>
        public static float SoftLimit(float x, float ceiling)
        {
            var knee = ceiling * 0.8f;
            var a = Math.Abs(x);
            if (a <= knee) return x;
            var over = (a - knee) / (ceiling - knee);
            var y = knee + (ceiling - knee) * (float)Math.Tanh(over);
            return x < 0f ? -y : y;
        }
    }
}
