using System;

namespace Project.Infrastructure.Audio.Weapons
{
    /// <summary>
    /// Silah sesi katmanlama (saf DSP, UnityEngine yok): mekanik klik + gövde patlaması + kuyruk
    /// (yakın/orta/uzak, açık/kapalı mekan) ve şarjör değişim foley zinciri. Hazır kayıt gelince
    /// WeaponClipLibrary öncelikli kalır (ContentOverrides); bu sınıf prosedürel yedek katmanlardır.
    /// </summary>
    public static class WeaponShotSynth
    {
        private struct Prof { public float BodyHz, BodyTau, BodyAmp, Thump, ThumpTau, Crack; }

        private static Prof ProfOf(ShotClass c)
        {
            switch (c)
            {
                case ShotClass.Pistol: return new Prof { BodyHz = 3200f, BodyTau = 0.018f, BodyAmp = 0.9f, Thump = 140f, ThumpTau = 0.03f, Crack = 0.8f };
                case ShotClass.Smg: return new Prof { BodyHz = 3000f, BodyTau = 0.02f, BodyAmp = 0.9f, Thump = 120f, ThumpTau = 0.035f, Crack = 0.75f };
                case ShotClass.Sniper: return new Prof { BodyHz = 2000f, BodyTau = 0.05f, BodyAmp = 1f, Thump = 60f, ThumpTau = 0.09f, Crack = 1f };
                case ShotClass.Shotgun: return new Prof { BodyHz = 1700f, BodyTau = 0.045f, BodyAmp = 1f, Thump = 70f, ThumpTau = 0.08f, Crack = 0.7f };
                default: return new Prof { BodyHz = 2600f, BodyTau = 0.03f, BodyAmp = 0.95f, Thump = 90f, ThumpTau = 0.05f, Crack = 0.9f };
            }
        }

        /// <summary>Mekanik klik (iğne/gürültü). Son mermide daha tok, metalik ve uzun çınlamalı.</summary>
        public static float[] RenderMech(int variant, bool lastRound)
        {
            var v = Math.Abs(variant) % 4;
            var rng = new SynthRng(0x3EC40000u ^ (uint)(v * 7919) ^ (lastRound ? 0xF00Du : 0u));
            var b = SynthDsp.Buffer(lastRound ? 0.22f : 0.12f);
            var ring = lastRound ? 1250f + v * 90f : 2100f + v * 160f;
            SynthDsp.Click(b, ref rng, 0f, 0.9f, lastRound ? 1800f : 2800f, 0.0015f);
            SynthDsp.Clack(b, ref rng, 0.001f, lastRound ? 0.8f : 0.5f, ring);
            if (lastRound)
            {
                SynthDsp.Ring(b, 0.002f, 0.4f, 0.09f, false, ring, ring * 1.58f, ring * 2.4f);
                SynthDsp.Tone(b, 0f, 220f, 140f, 0.02f, 0.001f, 0.05f, 0.35f);
            }
            SynthDsp.Normalize(b, 0.8f);
            SynthDsp.FadeEdges(b, 0.0003f, 0.01f);
            return b;
        }

        /// <summary>Gövde patlaması: çat + gövde gürültüsü + alçak thump + N-dalgası.</summary>
        public static float[] RenderBody(ShotClass c, int variant)
        {
            var p = ProfOf(c);
            var v = Math.Abs(variant) % 4;
            var rng = new SynthRng(0xB0D10000u ^ ((uint)c * 104729u) ^ (uint)(v * 15485863));
            var b = SynthDsp.Buffer(0.25f);
            SynthDsp.NoiseBurst(b, ref rng, 0f, 0.0003f, 0.004f, p.Crack, FilterKind.Highpass, 2500f);
            SynthDsp.NoiseBurst(b, ref rng, 0f, 0.0004f, p.BodyTau * (1f + v * 0.05f), p.BodyAmp, FilterKind.Lowpass, p.BodyHz);
            SynthDsp.Tone(b, 0f, p.Thump * 1.8f, p.Thump, 0.025f, 0.0005f, p.ThumpTau, 0.8f);
            SynthDsp.NWave(b, 0f, 0.5f, 0.0005f);
            SynthDsp.Normalize(b, 1f);
            SynthDsp.SoftClip(b, 1.4f);
            SynthDsp.FadeEdges(b, 0.0002f, 0.03f);
            return b;
        }

        /// <summary>Kuyruk: mesafe kademesine göre süre/kesim; kapalı mekanda sık yansıma + kısa reverb.</summary>
        public static float[] RenderTail(ShotClass c, TailDistance d, bool indoor, int variant)
        {
            var v = Math.Abs(variant) % 4;
            var rng = new SynthRng(0x7A110000u ^ ((uint)c * 7919u) ^ ((uint)d * 31u) ^ (indoor ? 0x1D00u : 0u) ^ (uint)(v * 104729));
            var sec = WeaponShotRules.TailSeconds(d, indoor, c);
            var cut = WeaponShotRules.TailCutoffHz(d, indoor);
            var b = SynthDsp.Buffer(sec);
            var tau = sec * (indoor ? 0.28f : 0.35f);
            SynthDsp.NoiseSwell(b, ref rng, 0f, sec * 0.9f, 0.6f, FilterKind.Lowpass, cut, cut * 0.5f, 0.8f);
            // Zarf: üstel sönüm.
            var k = SynthDsp.DecayFactor(tau);
            var env = 1f;
            for (var i = 0; i < b.Length; i++) { b[i] *= env; env *= k; }

            var dry = (float[])b.Clone();
            if (indoor)
            {
                // Sert duvar yansımaları (ic mekan reverb varyanti).
                var t0 = 0.011f + v * 0.003f;
                for (var e = 0; e < 7; e++)
                    SynthDsp.Echo(b, dry, t0 * (1f + e * 1.37f), 0.55f * (float)Math.Pow(0.72, e), cut);
            }
            else if (d != TailDistance.Near)
            {
                // Uzaktan tek, yumuşak yankı.
                SynthDsp.Echo(b, dry, 0.18f + v * 0.04f + (d == TailDistance.Far ? 0.2f : 0f), 0.35f, cut * 0.7f);
            }

            SynthDsp.Normalize(b, d == TailDistance.Far ? 0.45f : d == TailDistance.Mid ? 0.6f : 0.75f);
            SynthDsp.FadeEdges(b, 0.002f, Math.Min(0.2f, sec * 0.3f));
            return b;
        }

        /// <summary>Tek atış: mekanik + gövde + kuyruk karışımı (mekanik biraz önde).</summary>
        public static float[] RenderShot(ShotClass c, float distanceMeters, bool indoor, bool lastRound, int variant)
        {
            var d = WeaponShotRules.Bucket(distanceMeters);
            var mech = RenderMech(variant, lastRound);
            var body = RenderBody(c, variant);
            var tail = RenderTail(c, d, indoor, variant);
            var delay = SynthDsp.Samples(d == TailDistance.Far ? 0.04f : 0.012f);
            var len = Math.Max(Math.Max(mech.Length, body.Length + delay), tail.Length + delay);
            var o = new float[len];
            Add(o, mech, 0, d == TailDistance.Near ? 0.5f : 0.25f);
            Add(o, body, delay, d == TailDistance.Far ? 0.55f : 1f);
            Add(o, tail, delay, 0.8f);
            SynthDsp.Normalize(o, 0.95f);
            return o;
        }

        /// <summary>Şarjör değişimi: zincir adımlarını ortak zaman çizelgesinde sentezler (prosedürel yedek).</summary>
        public static float[] RenderReloadChain(bool tactical, float speed = 1f)
        {
            var chain = WeaponShotRules.ReloadChain(tactical, speed);
            var o = new float[SynthDsp.Samples(WeaponShotRules.ChainDuration(chain))];
            for (var i = 0; i < chain.Length; i++)
                Add(o, RenderBeat(chain[i].Layer, i), SynthDsp.Samples(chain[i].Time) - 1 < 0 ? 0 : SynthDsp.Samples(chain[i].Time) - 1, chain[i].Gain);
            SynthDsp.Normalize(o, 0.9f);
            return o;
        }

        public static float[] RenderBeat(string layer, int variant)
        {
            var rng = new SynthRng(0x5E100000u ^ (uint)(layer == null ? 0 : layer.GetHashCode() & 0xFFFF) ^ (uint)(variant * 7919));
            var b = SynthDsp.Buffer(0.2f);
            switch (layer)
            {
                case "magrelease":
                    SynthDsp.Click(b, ref rng, 0f, 0.9f, 2400f, 0.0015f);
                    SynthDsp.Clack(b, ref rng, 0f, 0.5f, 1900f);
                    break;
                case "magout":
                    SynthDsp.NoiseBurst(b, ref rng, 0f, 0.002f, 0.05f, 0.5f, FilterKind.Bandpass, 900f, 1.2f);
                    SynthDsp.Clack(b, ref rng, 0.004f, 0.6f, 1400f);
                    break;
                case "pouch":
                    SynthDsp.NoiseSwell(b, ref rng, 0f, 0.15f, 0.4f, FilterKind.Bandpass, 700f, 1500f, 0.9f);
                    break;
                case "magin":
                    SynthDsp.Clack(b, ref rng, 0f, 0.9f, 1100f);
                    SynthDsp.Tone(b, 0f, 260f, 150f, 0.02f, 0.001f, 0.06f, 0.5f);
                    SynthDsp.Click(b, ref rng, 0.03f, 0.6f, 2200f, 0.0015f);
                    break;
                case "slap":
                    SynthDsp.NoiseBurst(b, ref rng, 0f, 0.001f, 0.025f, 0.7f, FilterKind.Bandpass, 500f, 0.9f);
                    SynthDsp.Tone(b, 0f, 180f, 110f, 0.02f, 0.001f, 0.04f, 0.5f);
                    break;
                case "chargehandle":
                    SynthDsp.NoiseSwell(b, ref rng, 0f, 0.12f, 0.35f, FilterKind.Bandpass, 800f, 2400f, 1.5f);
                    SynthDsp.Clack(b, ref rng, 0.1f, 0.5f, 1700f);
                    break;
                default: // bolt
                    SynthDsp.Clack(b, ref rng, 0f, 0.9f, 2300f);
                    SynthDsp.Ring(b, 0f, 0.4f, 0.05f, false, 2300f, 3600f);
                    break;
            }
            SynthDsp.Normalize(b, 0.8f);
            SynthDsp.FadeEdges(b, 0.0003f, 0.02f);
            return b;
        }

        private static void Add(float[] dst, float[] src, int offset, float gain)
        {
            for (var i = 0; i < src.Length; i++)
            {
                var j = i + offset;
                if (j >= dst.Length) break;
                dst[j] += src[i] * gain;
            }
        }
    }
}
