using System;
using Project.Infrastructure.Vfx;

namespace Project.Infrastructure.Audio
{
    /// <summary>
    /// Yüzeye özgü prosedürel darbe sesleri (saf DSP, UnityEngine yok): her yüzey için 4 varyasyon, mesafe kovasına göre
    /// alçak geçiren filtre. Beton: çatlak + moloz, metal: çan + uzun çınlama, ahşap: tok darbe + kıymık,
    /// toprak: boğuk tok ses, su: plip + sıçrama, kar: yumuşak puf. Ayrıca sekme ıslığı (frekans süpürmeli).
    /// </summary>
    public static class ImpactSynth
    {
        public const int RicochetVariations = 3;

        public static float[] Render(SurfaceKind surface, int variant, int distanceBucket)
        {
            var family = ImpactSoundRules.Family(surface);
            var v = Math.Min(Math.Max(variant, 0), ImpactSoundRules.Variations - 1);
            var rng = new SynthRng(0xA11CE000u ^ ((uint)family * 7919u) ^ ((uint)v * 104729u) ^ 0x5Bu);
            float[] b;
            switch (family)
            {
                case SurfaceKind.Metal: b = Metal(ref rng, v); break;
                case SurfaceKind.Wood: b = Wood(ref rng, v); break;
                case SurfaceKind.Dirt: b = Dirt(ref rng, v); break;
                case SurfaceKind.Water: b = Water(ref rng, v); break;
                case SurfaceKind.Snow: b = Snow(ref rng, v); break;
                case SurfaceKind.Foliage: b = Foliage(ref rng, v); break;
                case SurfaceKind.Flesh: b = Flesh(ref rng, v); break;
                default: b = Concrete(ref rng, v); break;
            }

            return Finish(b, ImpactSoundRules.CutoffHz(distanceBucket), 0.7f);
        }

        /// <summary>Sekme vızıltısı: yüksek frekanstan aşağı süpürme (doppler benzeri) + ilk ping.</summary>
        public static float[] RenderRicochet(int variant, int distanceBucket)
        {
            var v = Math.Min(Math.Max(variant, 0), RicochetVariations - 1);
            var rng = new SynthRng(0xB10C0000u ^ ((uint)v * 15485863u));
            var b = SynthDsp.Buffer(0.7f);
            var f0 = 3300f + v * 450f;
            var f1 = 1250f + v * 140f;
            SynthDsp.Click(b, ref rng, 0f, 0.8f, 3200f, 0.0012f);
            SynthDsp.Ring(b, 0f, 0.45f, 0.06f, false, 2400f + v * 180f, 3900f + v * 220f);
            SynthDsp.Tone(b, 0.004f, f0, f1, 0.17f, 0.004f, 0.28f, 0.55f);
            SynthDsp.Tone(b, 0.004f, f0 * 1.51f, f1 * 1.51f, 0.17f, 0.004f, 0.18f, 0.18f);
            SynthDsp.NoiseSwell(b, ref rng, 0.004f, 0.4f, 0.35f, FilterKind.Bandpass, f0 * 1.3f, f1 * 1.3f, 4f);
            return Finish(b, ImpactSoundRules.CutoffHz(distanceBucket), 0.65f);
        }

        private static float[] Concrete(ref SynthRng rng, int v)
        {
            var b = SynthDsp.Buffer(0.45f);
            SynthDsp.Click(b, ref rng, 0f, 1f, 3500f + v * 300f, 0.0015f);
            SynthDsp.NoiseBurst(b, ref rng, 0f, 0.0008f, 0.03f + v * 0.004f, 0.8f, FilterKind.Bandpass, 1800f + v * 250f, 0.9f);
            SynthDsp.Tone(b, 0f, 130f - v * 10f, 70f, 0.02f, 0.001f, 0.035f, 0.45f);
            // Moloz: dağınık küçük tıkırtılar.
            var chips = 5 + v;
            for (var i = 0; i < chips; i++)
                SynthDsp.Click(b, ref rng, rng.Range(0.03f, 0.3f), rng.Range(0.1f, 0.3f), rng.Range(2500f, 6000f), 0.0012f);
            return b;
        }

        private static float[] Metal(ref SynthRng rng, int v)
        {
            var b = SynthDsp.Buffer(0.9f);
            var baseHz = 1900f + v * 330f;
            SynthDsp.Click(b, ref rng, 0f, 1f, 3200f, 0.0014f);
            SynthDsp.Clack(b, ref rng, 0f, 0.7f, baseHz);
            SynthDsp.Ring(b, 0f, 0.55f, 0.28f, false, baseHz, baseHz * 1.62f, baseHz * 2.31f, baseHz * 3.07f);
            SynthDsp.Tone(b, 0f, 520f + v * 60f, 480f, 0.1f, 0.001f, 0.09f, 0.3f);
            return b;
        }

        private static float[] Wood(ref SynthRng rng, int v)
        {
            var b = SynthDsp.Buffer(0.4f);
            SynthDsp.Tone(b, 0f, 240f + v * 25f, 110f, 0.025f, 0.001f, 0.05f, 0.7f);
            SynthDsp.NoiseBurst(b, ref rng, 0f, 0.001f, 0.03f, 0.8f, FilterKind.Lowpass, 1300f + v * 150f);
            SynthDsp.Click(b, ref rng, 0f, 0.5f, 2200f, 0.002f);
            // Kıymık: kısa yüksek tıkırtılar.
            var splinters = 3 + v;
            for (var i = 0; i < splinters; i++)
                SynthDsp.Click(b, ref rng, rng.Range(0.02f, 0.18f), rng.Range(0.12f, 0.3f), 4200f, 0.0009f);
            return b;
        }

        private static float[] Dirt(ref SynthRng rng, int v)
        {
            var b = SynthDsp.Buffer(0.35f);
            SynthDsp.NoiseBurst(b, ref rng, 0f, 0.002f, 0.05f + v * 0.008f, 1f, FilterKind.Lowpass, 520f + v * 70f);
            SynthDsp.Tone(b, 0f, 95f, 52f, 0.03f, 0.002f, 0.06f, 0.55f);
            SynthDsp.NoiseBurst(b, ref rng, 0.02f, 0.004f, 0.08f, 0.25f, FilterKind.Lowpass, 1800f);
            return b;
        }

        private static float[] Water(ref SynthRng rng, int v)
        {
            var b = SynthDsp.Buffer(0.6f);
            // Plip: yükselen kısa damla tonu.
            SynthDsp.Tone(b, 0f, 520f + v * 80f, 1500f + v * 120f, 0.03f, 0.002f, 0.05f, 0.6f);
            SynthDsp.NoiseSwell(b, ref rng, 0.01f, 0.25f, 0.5f, FilterKind.Highpass, 3500f, 2200f);
            var drops = 3 + v;
            for (var i = 0; i < drops; i++)
            {
                var f = rng.Range(900f, 2400f);
                SynthDsp.Tone(b, rng.Range(0.08f, 0.45f), f, f * 1.6f, 0.02f, 0.001f, 0.025f, rng.Range(0.1f, 0.25f));
            }

            return b;
        }

        private static float[] Snow(ref SynthRng rng, int v)
        {
            var b = SynthDsp.Buffer(0.35f);
            SynthDsp.NoiseBurst(b, ref rng, 0f, 0.006f, 0.05f + v * 0.006f, 0.9f, FilterKind.Lowpass, 1700f + v * 200f);
            SynthDsp.Tone(b, 0f, 80f, 55f, 0.03f, 0.004f, 0.05f, 0.25f);
            for (var i = 0; i < 4; i++)
                SynthDsp.NoiseBurst(b, ref rng, rng.Range(0.02f, 0.18f), 0.002f, 0.012f, rng.Range(0.1f, 0.22f), FilterKind.Lowpass, 3000f);
            return b;
        }

        private static float[] Foliage(ref SynthRng rng, int v)
        {
            var b = SynthDsp.Buffer(0.35f);
            SynthDsp.NoiseSwell(b, ref rng, 0f, 0.22f, 0.7f, FilterKind.Bandpass, 2600f + v * 300f, 1800f, 1.2f);
            SynthDsp.NoiseBurst(b, ref rng, 0f, 0.002f, 0.025f, 0.5f, FilterKind.Lowpass, 1200f);
            return b;
        }

        private static float[] Flesh(ref SynthRng rng, int v)
        {
            var b = SynthDsp.Buffer(0.3f);
            SynthDsp.Tone(b, 0f, 120f - v * 8f, 65f, 0.02f, 0.001f, 0.04f, 0.7f);
            SynthDsp.NoiseBurst(b, ref rng, 0f, 0.002f, 0.04f, 0.7f, FilterKind.Lowpass, 800f);
            return b;
        }

        private static float[] Finish(float[] b, float cutoffHz, float peak)
        {
            if (cutoffHz > 0f)
                SynthDsp.Filter(b, FilterKind.Lowpass, cutoffHz);
            SynthDsp.Normalize(b, Math.Min(peak, LoudnessMath.PeakCeilingLinear));
            SynthDsp.FadeEdges(b, 0f, 0.015f);
            return b;
        }
    }
}
