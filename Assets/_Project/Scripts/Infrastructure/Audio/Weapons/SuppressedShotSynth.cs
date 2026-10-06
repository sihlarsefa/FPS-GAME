using System;

namespace Project.Infrastructure.Audio.Weapons
{
    /// <summary>
    /// Susturucu/balistik çatlama/ortama duyarlı prosedürel atış (yedek katmanlar). Hazır kayıtlar yine öncelikli.
    /// Saf DSP (UnityEngine yok): <see cref="SuppressorRules"/> karışımı ve <see cref="EnclosureAnalyzer"/> kuyruk
    /// karışımıyla çalışır; <see cref="WeaponShotSynth"/> bozulmadan kalır.
    /// </summary>
    public static class SuppressedShotSynth
    {
        /// <summary>Susturulmuş gövde: patlama yerine kısık "tok" ses (düşük kesim, gaz ıslığı, düşük thump).</summary>
        public static float[] RenderBody(ShotClass c, MuzzleDevice device, int variant)
        {
            var v = Math.Abs(variant) % 4;
            var supp = SuppressorRules.IsSuppressor(device);
            var rng = new SynthRng(0x5B0D0000u ^ ((uint)c * 104729u) ^ ((uint)device * 7919u) ^ (uint)(v * 15485863));
            var b = SynthDsp.Buffer(supp ? 0.3f : 0.25f);
            if (supp)
            {
                var cut = device == MuzzleDevice.IntegralSuppressor ? 1500f : 1900f;
                SynthDsp.NoiseBurst(b, ref rng, 0f, 0.0008f, 0.02f * (1f + v * 0.06f), 0.9f, FilterKind.Lowpass, cut);
                // Gaz ıslığı (bölmelerden sızan): bant geçiren gürültü, patlamadan hemen sonra.
                SynthDsp.NoiseBurst(b, ref rng, 0.004f, 0.004f, 0.045f, 0.35f, FilterKind.Bandpass, 3400f + v * 150f, 1.4f);
                SynthDsp.Tone(b, 0f, 150f, 85f, 0.03f, 0.0008f, 0.045f, 0.8f);
                SynthDsp.Clack(b, ref rng, 0.006f, 0.25f, 1700f + v * 90f);
            }
            else
            {
                return WeaponShotSynth.RenderBody(c, variant);
            }
            SynthDsp.Normalize(b, 0.9f);
            SynthDsp.SoftClip(b, 1.2f);
            SynthDsp.FadeEdges(b, 0.0002f, 0.04f);
            return b;
        }

        /// <summary>Balistik çatlama: çok kısa N-dalgası + yüksek frekans çatırtı. Susturucudan etkilenmez.</summary>
        public static float[] RenderCrack(ShotClass c, int variant)
        {
            var v = Math.Abs(variant) % 4;
            var rng = new SynthRng(0xC4AC0000u ^ ((uint)c * 31337u) ^ (uint)(v * 104729));
            var b = SynthDsp.Buffer(0.06f);
            SynthDsp.NWave(b, 0f, 1f, c == ShotClass.Sniper ? 0.0007f : 0.0005f);
            SynthDsp.NoiseBurst(b, ref rng, 0f, 0.0001f, 0.006f, 0.8f, FilterKind.Highpass, 3500f + v * 300f);
            SynthDsp.Normalize(b, 0.95f);
            SynthDsp.FadeEdges(b, 0.00005f, 0.012f);
            return b;
        }

        /// <summary>Tek yansıma tap'ini karışıma ekler (alçak geçirilmiş, geciken kopya).</summary>
        public static void AddTaps(float[] dst, float[] src, System.Collections.Generic.List<ReflectionTap> taps, float gain)
        {
            if (taps == null) return;
            for (var t = 0; t < taps.Count; t++)
            {
                var tap = taps[t];
                var offset = SynthDsp.Samples(tap.DelaySeconds) - 1;
                var lp = (float[])src.Clone();
                SynthDsp.Filter(lp, FilterKind.Lowpass, tap.CutoffHz);
                for (var i = 0; i < lp.Length; i++)
                {
                    var j = i + offset;
                    if (j >= dst.Length) break;
                    dst[j] += lp[i] * tap.Gain * gain;
                }
            }
        }

        /// <summary>
        /// Tam atış: susturucu karışımı + çatlama + ortam kuyruğu (açık/kapalı/vadi karışımı) + erken yansıma tap'leri.
        /// distanceMeters dinleyiciye uzaklıktır (gövde/çatlama gecikmesi ve kuyruk kademesi buna bağlı).
        /// </summary>
        public static float[] RenderShot(ShotClass c, MuzzleDevice device, AmmoSpeed ammo, float distanceMeters,
            in EnclosureProbe probe, bool lastRound, int variant, int roundsSinceNew = 0)
        {
            var mix = SuppressorRules.Mix(c, device, ammo, roundsSinceNew);
            var rep = EnclosureAnalyzer.Analyze(probe);
            var blend = EnclosureAnalyzer.Blend(rep);
            var d = WeaponShotRules.Bucket(distanceMeters);

            var body = RenderBody(c, device, variant);
            var mech = WeaponShotSynth.RenderMech(variant, lastRound);
            var tailOut = WeaponShotSynth.RenderTail(c, d, false, variant);
            var tailIn = WeaponShotSynth.RenderTail(c, d, true, variant);
            var crack = mix.CrackGain > 0f ? RenderCrack(c, variant) : null;

            var delay = SynthDsp.Samples(d == TailDistance.Far ? 0.04f : 0.012f);
            var len = Math.Max(Math.Max(mech.Length, body.Length + delay), Math.Max(tailOut.Length, tailIn.Length) + delay);
            len += SynthDsp.Samples(0.35f); // tap'ler için pay
            var o = new float[len];

            var farDuck = d == TailDistance.Far ? 0.55f : 1f;
            Mix(o, mech, 0, mix.MechGain * (d == TailDistance.Near ? 0.5f : 0.25f));
            Mix(o, body, delay, mix.BodyGain * farDuck);
            if (crack != null) Mix(o, crack, 0, mix.CrackGain * 0.7f);
            var tailGain = 0.8f * mix.TailGain;
            if (blend.Outdoor + blend.Valley > 0f) Mix(o, tailOut, delay, tailGain * (blend.Outdoor + blend.Valley * 0.6f));
            if (blend.Indoor > 0f) Mix(o, tailIn, delay, tailGain * blend.Indoor);
            AddTaps(o, body, EnclosureAnalyzer.Taps(probe, 4), 0.45f * mix.BodyGain);
            SynthDsp.Normalize(o, 0.95f);
            return o;
        }

        private static void Mix(float[] dst, float[] src, int offset, float gain)
        {
            if (src == null || !(gain > 0f)) return;
            for (var i = 0; i < src.Length; i++)
            {
                var j = i + offset;
                if (j >= dst.Length) break;
                dst[j] += src[i] * gain;
            }
        }
    }
}
