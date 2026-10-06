using System;
using System.Collections.Generic;

namespace Project.Infrastructure.Audio.Weapons
{
    /// <summary>
    /// Ateş eden/dinleyen noktadan atılan 5 ışının isabet mesafeleri (m). Isabet yoksa <see cref="EnclosureProbe.MaxRange"/>.
    /// Işınlar yerel eksenlerde: ön, arka, sol, sağ, yukarı. Raycast Presentation/MonoBehaviour tarafında yapılır.
    /// </summary>
    public struct EnclosureProbe
    {
        public const float MaxRange = 120f;
        public float Forward, Back, Left, Right, Up;
        /// <summary>Ortalama yüzey sönümleme katsayısı (0.02 cam .. 0.6 halı/ahşap panel), tipik beton 0.15.</summary>
        public float Absorption;

        public static EnclosureProbe Open()
            => new EnclosureProbe { Forward = MaxRange, Back = MaxRange, Left = MaxRange, Right = MaxRange, Up = MaxRange, Absorption = 0.2f };
    }

    public enum EnclosureKind { Open = 0, Semi, Indoor, Canyon, Tunnel }

    public readonly struct EnclosureReport
    {
        public readonly EnclosureKind Kind;
        public readonly float Openness;      // 0 kapalı .. 1 açık
        public readonly float Volume;        // yaklaşık m3 (kapalı için anlamlı)
        public readonly float Rt60;          // sn (Sabine)
        public readonly float MeanWallDistance;

        public EnclosureReport(EnclosureKind kind, float openness, float volume, float rt60, float meanWall)
        {
            Kind = kind;
            Openness = openness;
            Volume = volume;
            Rt60 = rt60;
            MeanWallDistance = meanWall;
        }
    }

    /// <summary>Erken yansıma noktası (tap): atıştan sonra gecikme, kazanç, alçak geçiren kesim.</summary>
    public readonly struct ReflectionTap
    {
        public readonly float DelaySeconds;
        public readonly float Gain;
        public readonly float CutoffHz;
        /// <summary>0..4: ışın indeksi (ön, arka, sol, sağ, yukarı) — yönlü sahneleme için.</summary>
        public readonly int Direction;

        public ReflectionTap(float delay, float gain, float cutoff, int dir)
        {
            DelaySeconds = delay;
            Gain = gain;
            CutoffHz = cutoff;
            Direction = dir;
        }
    }

    /// <summary>Kuyruk katman ağırlıkları (açık / kapalı / vadi) toplam 1.</summary>
    public readonly struct TailBlend
    {
        public readonly float Outdoor;
        public readonly float Indoor;
        public readonly float Valley;
        public readonly float DecayScale;

        public TailBlend(float outdoor, float indoor, float valley, float decayScale)
        {
            Outdoor = outdoor;
            Indoor = indoor;
            Valley = valley;
            DecayScale = decayScale;
        }
    }

    /// <summary>
    /// Ortam sınıflandırması ve erken yansıma planı (saf). CoD MW (2019) silah yansıma sistemi gibi: çevreye ışın atılır,
    /// isabet noktalarından geciken yansımalar çalınır. Burada 5 ışından Sabine RT60, tap listesi ve kuyruk karışımı üretilir.
    /// </summary>
    public static class EnclosureAnalyzer
    {
        public const float EyeHeight = 1.6f;

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        /// <summary>Isabet yapan (MaxRange'den küçük) ışın sayısı.</summary>
        public static int HitCount(in EnclosureProbe p)
        {
            var n = 0;
            if (p.Forward < EnclosureProbe.MaxRange) n++;
            if (p.Back < EnclosureProbe.MaxRange) n++;
            if (p.Left < EnclosureProbe.MaxRange) n++;
            if (p.Right < EnclosureProbe.MaxRange) n++;
            if (p.Up < EnclosureProbe.MaxRange) n++;
            return n;
        }

        public static EnclosureReport Analyze(in EnclosureProbe p)
        {
            var maxR = EnclosureProbe.MaxRange;
            var f = Clamp(p.Forward, 0.5f, maxR);
            var b = Clamp(p.Back, 0.5f, maxR);
            var l = Clamp(p.Left, 0.5f, maxR);
            var r = Clamp(p.Right, 0.5f, maxR);
            var u = Clamp(p.Up, 0.5f, maxR);

            var mean = (f + b + l + r) * 0.25f;
            // Açıklık: yatay ışınların uzaklığı ve tavanın yokluğu.
            var horizOpen = (Math.Min(f, 60f) + Math.Min(b, 60f) + Math.Min(l, 60f) + Math.Min(r, 60f)) / 240f;
            var ceilOpen = Clamp((u - 3f) / 20f, 0f, 1f);
            var openness = Clamp(0.65f * horizOpen + 0.35f * ceilOpen, 0f, 1f);

            var width = Math.Min(l + r, 80f);
            var length = Math.Min(f + b, 80f);
            var height = Math.Min(u + EyeHeight, 25f);
            var volume = width * length * height;
            var surface = 2f * (width * length + width * height + length * height);
            var abs = Clamp(p.Absorption <= 0f ? 0.15f : p.Absorption, 0.02f, 0.8f);
            var rt = surface > 1f ? 0.161f * volume / (surface * abs) : 0.2f;
            rt = Clamp(rt, 0.15f, 4f);

            var ceilingClosed = u < 8f;
            var wallsNear = 0;
            if (f < 15f) wallsNear++;
            if (b < 15f) wallsNear++;
            if (l < 15f) wallsNear++;
            if (r < 15f) wallsNear++;

            EnclosureKind kind;
            if (ceilingClosed && wallsNear >= 3)
                kind = (Math.Min(l, r) < 4f && Math.Max(f, b) > 25f) ? EnclosureKind.Tunnel : EnclosureKind.Indoor;
            else if (!ceilingClosed && l < 60f && r < 60f && l > 8f && r > 8f && Math.Max(l, r) < 90f && Math.Min(l, r) < 45f
                     && (f > 40f || b > 40f))
                kind = EnclosureKind.Canyon;
            else if (ceilingClosed || wallsNear >= 2)
                kind = EnclosureKind.Semi;
            else
                kind = EnclosureKind.Open;

            return new EnclosureReport(kind, openness, volume, rt, mean);
        }

        /// <summary>Kuyruk karışımı: kapalı mekan -> indoor, vadi/orman duvarı -> valley, aksi hâlde outdoor.</summary>
        public static TailBlend Blend(in EnclosureReport rep)
        {
            float o, i, v;
            switch (rep.Kind)
            {
                case EnclosureKind.Indoor: o = 0.05f; i = 0.95f; v = 0f; break;
                case EnclosureKind.Tunnel: o = 0f; i = 0.8f; v = 0.2f; break;
                case EnclosureKind.Canyon: o = 0.25f; i = 0f; v = 0.75f; break;
                case EnclosureKind.Semi: o = 0.45f; i = 0.45f; v = 0.1f; break;
                default: i = 0f; v = 0.2f * (1f - rep.Openness); o = 1f - v; break;
            }
            var s = o + i + v;
            if (s <= 0f) { o = 1f; s = 1f; }
            var decay = Clamp(rep.Rt60 / 1.0f, 0.5f, 3f);
            if (rep.Kind == EnclosureKind.Open) decay = 1f;
            return new TailBlend(o / s, i / s, v / s, decay);
        }

        /// <summary>
        /// Erken yansıma tap'leri: ışın başına tek yansıma, yol = 2d (kaynak dinleyiciye yakın), gecikme 2d/c.
        /// Kazanç = (1-sönümleme) / (1 + 2d/6). 60 m üzeri ışınlar (gökyüzü/açık) atılır. En güçlü <paramref name="maxTaps"/> tap döner.
        /// </summary>
        public static List<ReflectionTap> Taps(in EnclosureProbe p, int maxTaps = 4, float tempCelsius = 20f)
        {
            var list = new List<ReflectionTap>(5);
            var abs = Clamp(p.Absorption <= 0f ? 0.15f : p.Absorption, 0.02f, 0.8f);
            var c = ShotPropagationRules.SpeedOfSound(tempCelsius);
            AddTap(list, p.Forward, 0, abs, c);
            AddTap(list, p.Back, 1, abs, c);
            AddTap(list, p.Left, 2, abs, c);
            AddTap(list, p.Right, 3, abs, c);
            AddTap(list, p.Up, 4, abs, c);
            list.Sort((x, y) => y.Gain.CompareTo(x.Gain));
            if (maxTaps < 0) maxTaps = 0;
            if (list.Count > maxTaps) list.RemoveRange(maxTaps, list.Count - maxTaps);
            list.Sort((x, y) => x.DelaySeconds.CompareTo(y.DelaySeconds));
            return list;
        }

        private static void AddTap(List<ReflectionTap> list, float dist, int dir, float absorb, float c)
        {
            if (!(dist < 60f) || !(dist > 0.3f)) return;
            var path = 2f * dist;
            var gain = (1f - absorb) / (1f + path / 6f);
            var cutoff = 9000f * (float)Math.Pow(1f - absorb, 2.0) * (1f / (1f + dist / 40f));
            if (cutoff < 1200f) cutoff = 1200f;
            list.Add(new ReflectionTap(path / c, gain, cutoff, dir));
        }

        /// <summary>
        /// Uzak yansıtıcı yankısı (slapback): açık alanda 25..250 m'deki büyük duvar/yamaç; yoksa gecikme 0.
        /// Döner: gecikme sn; gain 0..0.5 (uzaklıkla azalır).
        /// </summary>
        public static float SlapbackDelay(in EnclosureProbe p, out float gain)
        {
            gain = 0f;
            var rep = Analyze(p);
            if (rep.Kind == EnclosureKind.Indoor || rep.Kind == EnclosureKind.Tunnel) return 0f;
            var best = float.MaxValue;
            var cand = new[] { p.Forward, p.Back, p.Left, p.Right };
            for (var i = 0; i < cand.Length; i++)
                if (cand[i] >= 25f && cand[i] < EnclosureProbe.MaxRange && cand[i] < best) best = cand[i];
            if (best == float.MaxValue) return 0f;
            gain = Clamp(0.5f * (1f - best / EnclosureProbe.MaxRange) * (1f - Clamp(p.Absorption, 0f, 0.8f)), 0.05f, 0.5f);
            return ShotPropagationRules.EchoDelay(best);
        }
    }
}
