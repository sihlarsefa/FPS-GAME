using UnityEngine;
using static Project.Infrastructure.Audio.SynthDsp;

namespace Project.Infrastructure.Audio
{
    /// <summary>C11 ses katmanları için prosedürel yedekler (mekanik, gümleme, kuyruklar, çatlama, kovan düşmesi).</summary>
    public static partial class ProceduralAudioSynth
    {
        private static float[] RenderLayers(SoundId id, ref SynthRng rng)
        {
            switch (id)
            {
                case SoundId.ShotMech: return ShotMech(ref rng);
                case SoundId.ShotThump: return ShotThump(ref rng);
                case SoundId.ShotTailOutdoor: return ShotTail(ref rng, 1.4f, 0.1f, 0.5f, 900f);
                case SoundId.ShotTailIndoor: return ShotTail(ref rng, 0.9f, 0.05f, 0.9f, 2400f);
                case SoundId.ShotTailValley: return ShotTail(ref rng, 2.6f, 0.45f, 0.55f, 600f);
                case SoundId.BulletCrack: return BulletCrack(ref rng);
                case SoundId.ShotSuppressed: return ShotSuppressed(ref rng);
                case SoundId.MgBeltRattle: return MgBeltRattle(ref rng);
                case SoundId.BulletImpactDirt: return ImpactDirt(ref rng);
                case SoundId.BulletImpactWood: return ImpactWood(ref rng);
                case SoundId.BulletImpactFlesh: return ImpactFlesh(ref rng);
                case SoundId.BulletImpactWater: return ImpactWater(ref rng);
                case SoundId.BulletImpactSnow: return ImpactSnow(ref rng);
                case SoundId.BulletImpactFoliage: return ImpactFoliage(ref rng);
                case SoundId.ShellDropGrass: return ShellDrop(ref rng, 900f, 0.25f, 0.2f);
                case SoundId.ShellDropConcrete: return ShellDrop(ref rng, 3500f, 0.7f, 0.4f);
                case SoundId.ShellDropMetal: return ShellDrop(ref rng, 5200f, 1f, 0.45f);
                default: return null;
            }
        }

        private static float[] ShotMech(ref SynthRng rng)
        {
            var b = Buffer(0.18f);
            NoiseBurst(b, ref rng, 0f, 0.0005f, 0.008f, 0.8f, FilterKind.Bandpass, 3200f, 1.2f);
            Tone(b, 0.012f, 620f, 380f, 0.02f, 0.0005f, 0.03f, 0.35f);
            NoiseBurst(b, ref rng, 0.06f, 0.0005f, 0.01f, 0.5f, FilterKind.Bandpass, 2200f, 1.4f);
            return Finish(b, 0.5f, 0.03f);
        }

        private static float[] ShotThump(ref SynthRng rng)
        {
            var b = Buffer(0.35f);
            Tone(b, 0f, 90f, 42f, 0.05f, 0.002f, 0.09f, 0.9f);
            NoiseBurst(b, ref rng, 0f, 0.002f, 0.05f, 0.5f, FilterKind.Lowpass, 260f, 0.8f);
            return Finish(b, 0.8f, 0.05f);
        }

        private static float[] ShotTail(ref SynthRng rng, float seconds, float predelay, float density, float lowpass)
        {
            var b = Buffer(seconds);
            NoiseBurst(b, ref rng, predelay, 0.02f, seconds * 0.28f, 0.55f, FilterKind.Lowpass, lowpass, 0.7f);
            var dry = (float[])b.Clone();
            Echo(b, dry, seconds * 0.22f, 0.35f * density, lowpass * 0.8f);
            Echo(b, dry, seconds * 0.45f, 0.22f * density, lowpass * 0.6f);
            return Finish(b, 0.6f, 0.1f);
        }

        /// <summary>
        /// Süpersonik çatırtı: üç katman — keskin transient (HP gürültü), parlak bant (N-dalga "snap" gövdesi),
        /// kısa düşük "tok" vuruş + hafif kuyruk. Karşılaştırmalı perde kayması Acoustics'in pitch'iyle gelir.
        /// </summary>
        private static float[] BulletCrack(ref SynthRng rng)
        {
            var b = Buffer(0.16f);
            NoiseBurst(b, ref rng, 0f, 0.0002f, 0.0045f, 1f, FilterKind.Highpass, 3200f, 0.9f);
            NoiseBurst(b, ref rng, 0f, 0.0003f, 0.011f, 0.55f, FilterKind.Bandpass, 4600f, 1.1f);
            NoiseBurst(b, ref rng, 0.0006f, 0.0004f, 0.02f, 0.3f, FilterKind.Bandpass, 2400f, 0.8f);
            Tone(b, 0f, 1400f, 520f, 0.006f, 0.0002f, 0.012f, 0.35f);
            NoiseBurst(b, ref rng, 0.004f, 0.002f, 0.045f, 0.12f, FilterKind.Lowpass, 1800f, 0.7f);
            return Finish(b, 0.85f, 0.03f);
        }

        /// <summary>
        /// Susturuculu atış: filtrelenmiş (alçak geçiren) hafif patlama, "pfft" gürültü + mekanik vurgusu
        /// (sürgü/tetik klik) ve kısa kuyruk. Süpersonik çat YOK.
        /// </summary>
        private static float[] ShotSuppressed(ref SynthRng rng)
        {
            var b = Buffer(0.5f);
            NoiseBurst(b, ref rng, 0f, 0.0008f, 0.03f, 0.7f, FilterKind.Lowpass, 1500f, 0.8f);
            NoiseBurst(b, ref rng, 0f, 0.001f, 0.012f, 0.4f, FilterKind.Bandpass, 900f, 1.2f);
            Tone(b, 0f, 120f, 60f, 0.03f, 0.002f, 0.05f, 0.45f);
            NoiseBurst(b, ref rng, 0.01f, 0.004f, 0.1f, 0.22f, FilterKind.Lowpass, 700f, 0.7f);
            // Mekanik vurgusu: susturucu bastırınca sürgü/tetik daha öne çıkar.
            NoiseBurst(b, ref rng, 0.004f, 0.0004f, 0.009f, 0.55f, FilterKind.Bandpass, 3000f, 1.3f);
            Tone(b, 0.016f, 700f, 420f, 0.02f, 0.0005f, 0.03f, 0.3f);
            NoiseBurst(b, ref rng, 0.055f, 0.0005f, 0.01f, 0.35f, FilterKind.Bandpass, 2100f, 1.4f);
            return Finish(b, 0.7f, 0.08f);
        }

        /// <summary>MG3 palet/kayış takırtısı: metal halka şıngırtıları + kayış sürtünmesi (kısa, atışa bindirilir).</summary>
        private static float[] MgBeltRattle(ref SynthRng rng)
        {
            var b = Buffer(0.22f);
            for (var i = 0; i < 5; i++)
            {
                var t = 0.004f + i * 0.026f + rng.Range(0f, 0.008f);
                var f = 2600f + rng.Range(0f, 1800f);
                Click(b, ref rng, t, 0.5f - i * 0.05f, 2800f, 0.0012f);
                Ring(b, t, 0.28f, 0.03f, false, f, f * 1.52f, f * 2.3f);
            }

            NoiseBurst(b, ref rng, 0f, 0.003f, 0.09f, 0.25f, FilterKind.Bandpass, 1700f, 0.9f);
            return Finish(b, 0.55f, 0.04f);
        }

        private static float[] ImpactDirt(ref SynthRng rng)
        {
            var b = Buffer(0.3f);
            NoiseBurst(b, ref rng, 0f, 0.0008f, 0.035f, 0.9f, FilterKind.Lowpass, 700f, 0.8f);
            Tone(b, 0f, 110f, 55f, 0.02f, 0.001f, 0.04f, 0.5f);
            Grit(b, ref rng, 0.01f, 0.2f, 14, 0.3f);
            return Finish(b, 0.65f);
        }

        private static float[] ImpactWood(ref SynthRng rng)
        {
            var b = Buffer(0.3f);
            Click(b, ref rng, 0f, 0.9f, 1800f, 0.002f);
            Tone(b, 0f, 520f, 300f, 0.015f, 0.0005f, 0.045f, 0.6f);
            Ring(b, 0f, 0.3f, 0.06f, false, 420f, 730f);
            NoiseBurst(b, ref rng, 0.005f, 0.002f, 0.04f, 0.4f, FilterKind.Lowpass, 1400f, 0.7f);
            return Finish(b, 0.7f);
        }

        private static float[] ImpactFlesh(ref SynthRng rng)
        {
            var b = Buffer(0.25f);
            NoiseBurst(b, ref rng, 0f, 0.001f, 0.03f, 1f, FilterKind.Lowpass, 520f, 0.9f);
            Tone(b, 0f, 160f, 70f, 0.02f, 0.001f, 0.035f, 0.6f);
            NoiseBurst(b, ref rng, 0.008f, 0.004f, 0.05f, 0.25f, FilterKind.Bandpass, 900f, 1f);
            return Finish(b, 0.6f);
        }

        private static float[] ImpactWater(ref SynthRng rng)
        {
            var b = Buffer(0.55f);
            NoiseBurst(b, ref rng, 0f, 0.004f, 0.06f, 0.9f, FilterKind.Bandpass, 1400f, 0.7f);
            NoiseBurst(b, ref rng, 0.01f, 0.01f, 0.14f, 0.4f, FilterKind.Lowpass, 2600f, 0.6f);
            Tone(b, 0.03f, 300f, 900f, 0.06f, 0.004f, 0.07f, 0.3f);
            Tone(b, 0.09f, 260f, 800f, 0.05f, 0.004f, 0.06f, 0.2f);
            return Finish(b, 0.6f, 0.15f);
        }

        private static float[] ImpactSnow(ref SynthRng rng)
        {
            var b = Buffer(0.3f);
            NoiseBurst(b, ref rng, 0f, 0.003f, 0.045f, 0.8f, FilterKind.Lowpass, 850f, 0.7f);
            NoiseBurst(b, ref rng, 0f, 0.002f, 0.02f, 0.25f, FilterKind.Bandpass, 2500f, 0.8f);
            Tone(b, 0f, 90f, 50f, 0.02f, 0.002f, 0.04f, 0.3f);
            return Finish(b, 0.5f);
        }

        private static float[] ImpactFoliage(ref SynthRng rng)
        {
            var b = Buffer(0.3f);
            NoiseBurst(b, ref rng, 0f, 0.004f, 0.07f, 0.7f, FilterKind.Bandpass, 3200f, 0.6f);
            NoiseBurst(b, ref rng, 0.01f, 0.006f, 0.09f, 0.4f, FilterKind.Bandpass, 2000f, 0.6f);
            Click(b, ref rng, 0.02f, 0.2f, 2500f, 0.002f);
            return Finish(b, 0.45f);
        }

        private static float[] ShellDrop(ref SynthRng rng, float ringHz, float ring, float amp)
        {
            var b = Buffer(0.4f);
            Click(b, ref rng, 0f, amp, Mathf.Min(ringHz, 4000f), 0.001f);
            Ring(b, 0f, ring * 0.5f, 0.04f, false, ringHz, ringHz * 1.45f);
            Click(b, ref rng, 0.08f, amp * 0.5f, Mathf.Min(ringHz, 4000f), 0.001f);
            Ring(b, 0.08f, ring * 0.25f, 0.03f, false, ringHz * 1.02f, ringHz * 1.5f);
            return Finish(b, 0.4f, 0.05f);
        }
    }
}
