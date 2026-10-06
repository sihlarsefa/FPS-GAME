using System;

namespace Project.Infrastructure.Audio.HdrMix
{
    public enum MixSnapshotKind
    {
        Normal = 0,
        Tinnitus,
        Underwater,
        Indoor,
        AdsFocus,
        Downed,
        Dead
    }

    /// <summary>Bir miks anlık görüntüsünün hedef parametreleri.</summary>
    public struct MixParams
    {
        /// <summary>Dünya sesine uygulanan alçak geçiren kesim (Hz). 22000 = kapalı.</summary>
        public float LowpassHz;
        /// <summary>Ana kazanç (doğrusal).</summary>
        public float MasterGain;
        /// <summary>Ortam yatakları kazancı.</summary>
        public float AmbienceGain;
        /// <summary>Kulak çınlaması kazancı (0 = yok).</summary>
        public float TinnitusGain;
        /// <summary>İç mekân oda yankısı eğilimi (0..1; mixer varsa kullanılır).</summary>
        public float ReverbSend;

        public static MixParams Normal => new MixParams
        {
            LowpassHz = MixSnapshotMath.OpenLowpassHz, MasterGain = 1f, AmbienceGain = 1f, TinnitusGain = 0f, ReverbSend = 0f
        };
    }

    /// <summary>Miks anlık görüntüleri için saf mantık (Unity bağımlılığı yok).</summary>
    public static class MixSnapshotMath
    {
        public const float OpenLowpassHz = 22000f;
        public const float TinnitusMinSeconds = 3f;
        public const float TinnitusMaxSeconds = 6f;
        public const float TinnitusMinSeverity = 0.12f;
        public const float TinnitusRingHz = 3800f;

        /// <summary>Tam şiddette anlık görüntü parametreleri.</summary>
        public static MixParams Target(MixSnapshotKind kind)
        {
            var p = MixParams.Normal;
            switch (kind)
            {
                case MixSnapshotKind.Tinnitus:
                    p.LowpassHz = 650f; p.MasterGain = 0.55f; p.AmbienceGain = 0.25f; p.TinnitusGain = 1f; break;
                case MixSnapshotKind.Underwater:
                    p.LowpassHz = 420f; p.MasterGain = 0.7f; p.AmbienceGain = 0.1f; break;
                case MixSnapshotKind.Indoor:
                    p.LowpassHz = 15000f; p.AmbienceGain = 0.45f; p.ReverbSend = 0.6f; break;
                case MixSnapshotKind.AdsFocus:
                    p.AmbienceGain = 0.8f; break;
                case MixSnapshotKind.Downed:
                    p.LowpassHz = 1400f; p.MasterGain = 0.8f; p.AmbienceGain = 0.5f; break;
                case MixSnapshotKind.Dead:
                    p.LowpassHz = 380f; p.MasterGain = 0.6f; p.AmbienceGain = 0.15f; break;
            }

            return p;
        }

        /// <summary>Normal ile hedef arasında şiddete (0..1) göre karışım. Kesim frekansı logaritmik karışır.</summary>
        public static MixParams Blend(MixParams a, MixParams b, float t)
        {
            t = Clamp01(t);
            return new MixParams
            {
                LowpassHz = (float)Math.Exp(Lerp(Math.Log(Math.Max(20f, a.LowpassHz)), Math.Log(Math.Max(20f, b.LowpassHz)), t)),
                MasterGain = Lerp(a.MasterGain, b.MasterGain, t),
                AmbienceGain = Lerp(a.AmbienceGain, b.AmbienceGain, t),
                TinnitusGain = Lerp(a.TinnitusGain, b.TinnitusGain, t),
                ReverbSend = Lerp(a.ReverbSend, b.ReverbSend, t)
            };
        }

        /// <summary>Anlık görüntüleri birleştirir: kesim en küçük, kazançlar çarpım, çınlama/yankı en büyük.</summary>
        public static MixParams Combine(MixParams a, MixParams b) => new MixParams
        {
            LowpassHz = Math.Min(a.LowpassHz, b.LowpassHz),
            MasterGain = a.MasterGain * b.MasterGain,
            AmbienceGain = a.AmbienceGain * b.AmbienceGain,
            TinnitusGain = Math.Max(a.TinnitusGain, b.TinnitusGain),
            ReverbSend = Math.Max(a.ReverbSend, b.ReverbSend)
        };

        /// <summary>Patlamanın şiddeti (0..1): merkezde 1, etki yarıçapının 1.6 katında 0.</summary>
        public static float TinnitusSeverity(float distance, float radius, bool earProtected = false)
        {
            var r = Math.Max(1f, radius) * 1.6f;
            if (distance >= r)
                return 0f;
            var s = 1f - Math.Max(0f, distance) / r;
            s *= s * (3f - 2f * s) * 0.5f + s * 0.5f;
            if (earProtected)
                s *= 0.4f;
            return Clamp01(s * 1.25f);
        }

        /// <summary>Çınlama süresi (sn); eşik altı şiddette 0.</summary>
        public static float TinnitusDuration(float severity)
        {
            if (severity < TinnitusMinSeverity)
                return 0f;
            return TinnitusMinSeconds + (TinnitusMaxSeconds - TinnitusMinSeconds) * Clamp01(severity);
        }

        /// <summary>Çınlama zarfı: ilk %55 tam, sonra yumuşak sönüm. elapsed/total ∈ [0,1].</summary>
        public static float TinnitusEnvelope(float elapsed, float total)
        {
            if (total <= 0f || elapsed >= total || elapsed < 0f)
                return 0f;
            var u = elapsed / total;
            if (u < 0.55f)
                return 1f;
            var k = 1f - (u - 0.55f) / 0.45f;
            return k * k;
        }

        /// <summary>Üstel yumuşatma: hedefe doğru, yükselirken/alçalırken farklı zaman sabitleriyle.</summary>
        public static float SmoothTo(float current, float target, float dt, float tauUp, float tauDown)
        {
            var tau = target > current ? tauUp : tauDown;
            if (tau <= 0f)
                return target;
            var k = 1f - (float)Math.Exp(-Math.Max(0f, dt) / tau);
            return current + (target - current) * k;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
        private static double Lerp(double a, double b, float t) => a + (b - a) * t;
    }

    /// <summary>Etkin durumlardan anlık hedef miksi hesaplar (saf; Unity yok).</summary>
    public sealed class MixSnapshotState
    {
        private float _tinnitusElapsed;
        private float _tinnitusTotal;
        private float _tinnitusSeverity;
        private MixParams _current = MixParams.Normal;

        public bool Underwater;
        public bool Indoor;
        public bool Ads;
        public bool Downed;
        public bool Dead;

        public MixParams Current => _current;
        public bool TinnitusActive => _tinnitusTotal > 0f && _tinnitusElapsed < _tinnitusTotal;
        public float TinnitusRemaining => TinnitusActive ? _tinnitusTotal - _tinnitusElapsed : 0f;
        public float TinnitusSeconds => _tinnitusTotal;

        /// <summary>Patlama çınlaması başlatır (daha uzun/şiddetli olan kazanır). Süre 0 ise yok sayılır.</summary>
        public float TriggerTinnitus(float severity)
        {
            var dur = MixSnapshotMath.TinnitusDuration(severity);
            if (dur <= 0f)
                return 0f;
            if (!TinnitusActive || dur > TinnitusRemaining)
            {
                _tinnitusTotal = dur;
                _tinnitusElapsed = 0f;
                _tinnitusSeverity = severity;
            }

            return dur;
        }

        /// <summary>Anlık hedef (yumuşatmasız).</summary>
        public MixParams ComputeTarget()
        {
            var p = MixParams.Normal;
            if (Indoor)
                p = MixSnapshotMath.Combine(p, MixSnapshotMath.Target(MixSnapshotKind.Indoor));
            if (Ads)
                p = MixSnapshotMath.Combine(p, MixSnapshotMath.Target(MixSnapshotKind.AdsFocus));
            if (Downed)
                p = MixSnapshotMath.Combine(p, MixSnapshotMath.Target(MixSnapshotKind.Downed));
            if (Underwater)
                p = MixSnapshotMath.Combine(p, MixSnapshotMath.Target(MixSnapshotKind.Underwater));
            if (Dead)
                p = MixSnapshotMath.Combine(p, MixSnapshotMath.Target(MixSnapshotKind.Dead));
            if (TinnitusActive)
            {
                var env = MixSnapshotMath.TinnitusEnvelope(_tinnitusElapsed, _tinnitusTotal) * (0.5f + 0.5f * _tinnitusSeverity);
                p = MixSnapshotMath.Combine(p, MixSnapshotMath.Blend(MixParams.Normal, MixSnapshotMath.Target(MixSnapshotKind.Tinnitus), env));
            }

            return p;
        }

        /// <summary>Zamanı ilerletir ve yumuşatılmış mevcut miksi döndürür.</summary>
        public MixParams Tick(float dt)
        {
            if (TinnitusActive)
                _tinnitusElapsed += Math.Max(0f, dt);

            var t = ComputeTarget();
            // Kesim frekansı logaritmik uzayda yumuşar: kapanış hızlı (vuruş), açılış yavaş (kulak toparlanır).
            var lp = (float)Math.Exp(MixSnapshotMath.SmoothTo(
                (float)Math.Log(_current.LowpassHz), (float)Math.Log(t.LowpassHz), dt, 0.04f, TinnitusActive ? 0.9f : 0.35f));
            // Log-uzay yumuşatma 22000'e asimptotik yaklaşır; neredeyse açıksa tam aç (takılı boğukluk olmasın).
            if (t.LowpassHz >= MixSnapshotMath.OpenLowpassHz - 1f && lp > 21000f)
                lp = MixSnapshotMath.OpenLowpassHz;
            _current = new MixParams
            {
                LowpassHz = lp,
                MasterGain = MixSnapshotMath.SmoothTo(_current.MasterGain, t.MasterGain, dt, 0.03f, 0.6f),
                AmbienceGain = MixSnapshotMath.SmoothTo(_current.AmbienceGain, t.AmbienceGain, dt, 0.08f, 0.8f),
                TinnitusGain = MixSnapshotMath.SmoothTo(_current.TinnitusGain, t.TinnitusGain, dt, 0.05f, 0.7f),
                ReverbSend = MixSnapshotMath.SmoothTo(_current.ReverbSend, t.ReverbSend, dt, 0.3f, 0.5f)
            };
            return _current;
        }

        public void ResetAll()
        {
            Underwater = Indoor = Ads = Downed = Dead = false;
            _tinnitusElapsed = _tinnitusTotal = _tinnitusSeverity = 0f;
            _current = MixParams.Normal;
        }
    }
}
