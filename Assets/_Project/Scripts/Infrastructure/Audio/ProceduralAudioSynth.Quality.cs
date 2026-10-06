using UnityEngine;
using static Project.Infrastructure.Audio.SynthDsp;

namespace Project.Infrastructure.Audio
{
    /// <summary>
    /// Ses kalitesi geçişi: uzak atış varyantları, yüzeye göre ayak sesleri, kıyafet hışırtısı, kovan ve silah sınıfına
    /// göre şarjör değiştirme. Saf C# (iş parçacığı güvenli); toplam üretim süresi birkaç on ms (paralel + önbellekli).
    /// </summary>
    public static partial class ProceduralAudioSynth
    {
        private static float[] RenderQuality(SoundId id, ref SynthRng rng)
        {
            switch (id)
            {
                case SoundId.ShotDistantMid: return DistantShotMid(ref rng);
                case SoundId.ShotDistantFar: return DistantShotFar(ref rng);
                case SoundId.FootstepGrass: return StepGrass(ref rng);
                case SoundId.FootstepConcrete: return StepConcrete(ref rng);
                case SoundId.FootstepWood: return StepWood(ref rng);
                case SoundId.FootstepMetal: return StepMetal(ref rng);
                case SoundId.FootstepSnow: return StepSnow(ref rng);
                case SoundId.FootstepGravel: return StepGravel(ref rng);
                case SoundId.FootstepMud: return StepMud(ref rng);
                case SoundId.WoodCreak: return WoodCreak(ref rng);
                case SoundId.GearJingle: return GearJingle(ref rng);
                case SoundId.ClothRustle: return ClothRustle(ref rng);
                case SoundId.BreathHeavy: return BreathHeavy(ref rng);
                case SoundId.ShellCasing: return ShellCasing(ref rng);
                case SoundId.ReloadPistol: return ReloadPistol(ref rng);
                case SoundId.ReloadRifle: return ReloadRifle(ref rng);
                case SoundId.ReloadLmg: return ReloadLmg(ref rng);
                case SoundId.ReloadShotgun: return ReloadShotgun(ref rng);
                case SoundId.ReloadSniper: return ReloadSniper(ref rng);
                default: return RenderLayers(id, ref rng);
            }
        }

        // ---- Uzak atış: orta mesafe (yumuşamış çat + gövde + yankı) ----
        private static float[] DistantShotMid(ref SynthRng rng)
        {
            var b = Buffer(1.6f);
            NoiseBurst(b, ref rng, 0f, 0.0015f, 0.012f, 0.8f, FilterKind.Bandpass, 1400f, 0.8f);
            NoiseBurst(b, ref rng, 0f, 0.002f, 0.07f, 0.9f, FilterKind.Lowpass, 900f, 0.8f);
            Tone(b, 0f, 100f, 48f, 0.04f, 0.002f, 0.1f, 0.8f);
            NoiseBurst(b, ref rng, 0.03f, 0.02f, 0.28f, 0.3f, FilterKind.Lowpass, 500f);
            var dry = (float[])b.Clone();
            Echo(b, dry, 0.22f, 0.35f, 700f);
            Echo(b, dry, 0.47f, 0.2f, 550f);
            Echo(b, dry, 0.85f, 0.1f, 420f);
            return Finish(b, 0.85f, 0.3f);
        }

        // ---- Uzak atış: çok uzak (alçak gümleme + dağ yankısı) ----
        private static float[] DistantShotFar(ref SynthRng rng)
        {
            var b = Buffer(2.4f);
            NoiseBurst(b, ref rng, 0f, 0.012f, 0.22f, 0.9f, FilterKind.Lowpass, 380f, 0.8f);
            Tone(b, 0f, 70f, 40f, 0.06f, 0.008f, 0.18f, 0.9f);
            NoiseBurst(b, ref rng, 0.05f, 0.05f, 0.4f, 0.25f, FilterKind.Lowpass, 260f);
            var dry = (float[])b.Clone();
            Echo(b, dry, 0.4f, 0.4f, 330f);
            Echo(b, dry, 0.78f, 0.25f, 280f);
            Echo(b, dry, 1.25f, 0.14f, 240f);
            return Finish(b, 0.8f, 0.5f);
        }

        // ---- Ayak sesleri ----
        private static float[] StepGrass(ref SynthRng rng)
        {
            var b = Buffer(0.28f);
            NoiseSwell(b, ref rng, 0f, 0.1f, 0.5f, FilterKind.Bandpass, 1600f, 900f, 0.6f, 0.4f);
            NoiseBurst(b, ref rng, 0f, 0.004f, 0.03f, 0.5f, FilterKind.Lowpass, 300f);
            Tone(b, 0f, 75f, 55f, 0.02f, 0.003f, 0.03f, 0.35f);
            NoiseSwell(b, ref rng, 0.05f, 0.09f, 0.25f, FilterKind.Bandpass, 2600f, 1800f, 0.6f, 0.5f);
            Grit(b, ref rng, 0.01f, 0.1f, 8, 0.07f);
            return Finish(b, 0.5f);
        }

        private static float[] StepConcrete(ref SynthRng rng)
        {
            var b = Buffer(0.22f);
            Click(b, ref rng, 0f, 0.9f, 1800f, 0.002f);
            NoiseBurst(b, ref rng, 0f, 0.001f, 0.02f, 0.7f, FilterKind.Bandpass, 2200f, 1.1f);
            Ring(b, 0f, 0.12f, 0.02f, false, 3300f, 5100f);
            Tone(b, 0f, 110f, 70f, 0.015f, 0.001f, 0.025f, 0.45f);
            Click(b, ref rng, 0.05f, 0.4f, 2200f, 0.002f);
            Grit(b, ref rng, 0.003f, 0.06f, 5, 0.08f);
            return Finish(b, 0.6f);
        }

        private static float[] StepWood(ref SynthRng rng)
        {
            var b = Buffer(0.3f);
            Tone(b, 0f, 170f, 110f, 0.02f, 0.001f, 0.06f, 0.8f);
            Ring(b, 0f, 0.25f, 0.07f, false, 420f, 690f, 1130f);
            NoiseBurst(b, ref rng, 0f, 0.001f, 0.02f, 0.55f, FilterKind.Bandpass, 1100f, 1f);
            NoiseSwell(b, ref rng, 0.05f, 0.1f, 0.12f, FilterKind.Bandpass, 800f, 500f, 3f, 0.6f);
            Click(b, ref rng, 0.05f, 0.3f, 1500f, 0.003f);
            return Finish(b, 0.65f);
        }

        private static float[] StepMetal(ref SynthRng rng)
        {
            var b = Buffer(0.4f);
            Clack(b, ref rng, 0f, 0.9f, 900f);
            Ring(b, 0f, 0.35f, 0.12f, false, 780f, 1350f, 2150f, 3300f);
            Tone(b, 0f, 120f, 80f, 0.02f, 0.001f, 0.04f, 0.4f);
            Clack(b, ref rng, 0.055f, 0.45f, 1100f);
            return Finish(b, 0.65f, 0.08f);
        }

        // ---- Adım çeşitliliği: kar gıcırtısı, çakıl, çamur vakumu, ahşap esneme, teçhizat şıngırtısı ----
        private static float[] StepSnow(ref SynthRng rng)
        {
            var b = Buffer(0.3f);
            NoiseSwell(b, ref rng, 0f, 0.07f, 0.35f, FilterKind.Bandpass, 3200f, 2200f, 2.5f, 0.55f);
            NoiseBurst(b, ref rng, 0f, 0.003f, 0.025f, 0.35f, FilterKind.Lowpass, 400f);
            Grit(b, ref rng, 0.01f, 0.12f, 14, 0.08f);
            Tone(b, 0.02f, 2400f, 1900f, 0.05f, 0.004f, 0.05f, 0.08f);
            return Finish(b, 0.5f);
        }

        private static float[] StepGravel(ref SynthRng rng)
        {
            var b = Buffer(0.3f);
            NoiseBurst(b, ref rng, 0f, 0.002f, 0.03f, 0.6f, FilterKind.Bandpass, 2400f, 0.8f);
            Grit(b, ref rng, 0f, 0.16f, 22, 0.16f);
            NoiseSwell(b, ref rng, 0.01f, 0.1f, 0.3f, FilterKind.Bandpass, 1800f, 1200f, 0.8f, 0.5f);
            Tone(b, 0f, 110f, 70f, 0.02f, 0.002f, 0.04f, 0.3f);
            return Finish(b, 0.6f);
        }

        private static float[] StepMud(ref SynthRng rng)
        {
            var b = Buffer(0.38f);
            NoiseBurst(b, ref rng, 0f, 0.006f, 0.05f, 0.6f, FilterKind.Lowpass, 380f, 0.8f);
            Tone(b, 0f, 95f, 45f, 0.04f, 0.004f, 0.07f, 0.55f);
            // Vakum: ayak çekilirken kısa, perdesi yükselen sürtünme
            Tone(b, 0.14f, 140f, 320f, 0.09f, 0.01f, 0.06f, 0.2f);
            NoiseSwell(b, ref rng, 0.12f, 0.1f, 0.25f, FilterKind.Bandpass, 500f, 900f, 2f, 0.4f);
            return Finish(b, 0.55f);
        }

        private static float[] WoodCreak(ref SynthRng rng)
        {
            var b = Buffer(0.5f);
            Tone(b, 0f, 240f, 380f, 0.25f, 0.04f, 0.2f, 0.28f);
            Tone(b, 0.02f, 480f, 700f, 0.25f, 0.05f, 0.18f, 0.12f);
            NoiseSwell(b, ref rng, 0f, 0.2f, 0.3f, FilterKind.Bandpass, 900f, 600f, 6f, 0.3f);
            return Finish(b, 0.45f);
        }

        private static float[] GearJingle(ref SynthRng rng)
        {
            var b = Buffer(0.3f);
            Ring(b, 0f, 0.18f, 0.06f, false, 2100f, 3300f, 4700f);
            Click(b, ref rng, 0.03f, 0.25f, 3000f, 0.002f);
            Click(b, ref rng, 0.07f, 0.18f, 2500f, 0.002f);
            return Finish(b, 0.4f);
        }

        // ---- Kıyafet / teçhizat hışırtısı ----
        private static float[] ClothRustle(ref SynthRng rng)
        {
            var b = Buffer(0.22f);
            NoiseSwell(b, ref rng, 0f, 0.2f, 0.4f, FilterKind.Bandpass, 2200f, 3200f, 0.5f, 0.7f);
            NoiseSwell(b, ref rng, 0.02f, 0.14f, 0.2f, FilterKind.Bandpass, 800f, 600f, 0.6f, 0.5f);
            Grit(b, ref rng, 0.02f, 0.15f, 5, 0.1f);
            // teçhizat: hafif tokalaşma
            Ring(b, 0.06f, 0.05f, 0.025f, false, 2600f, 3900f);
            return Finish(b, 0.28f, 0.06f);
        }

        // ---- Soluk (tükenmiş nefes): nefes alma (yükselen bantgeçiren) + verme (alçalan) ----
        private static float[] BreathHeavy(ref SynthRng rng)
        {
            var b = Buffer(0.9f);
            NoiseSwell(b, ref rng, 0f, 0.38f, 0.55f, FilterKind.Bandpass, 900f, 1700f, 0.9f, 0.1f);
            NoiseSwell(b, ref rng, 0.42f, 0.42f, 0.45f, FilterKind.Bandpass, 1500f, 700f, 0.8f, 0.1f);
            NoiseSwell(b, ref rng, 0.05f, 0.3f, 0.2f, FilterKind.Lowpass, 400f, 300f, 0.7f, 0.2f);
            return Finish(b, 0.5f, 0.05f);
        }

        // ---- Boş kovan düşmesi ----
        private static float[] ShellCasing(ref SynthRng rng)
        {
            var b = Buffer(0.45f);
            Click(b, ref rng, 0f, 0.6f, 3500f, 0.001f);
            Ring(b, 0f, 0.6f, 0.05f, false, 4300f, 6200f, 7900f);
            Click(b, ref rng, 0.075f, 0.35f, 3500f, 0.001f);
            Ring(b, 0.075f, 0.32f, 0.04f, false, 4400f, 6300f, 8100f);
            Click(b, ref rng, 0.13f, 0.18f, 3500f, 0.001f);
            Ring(b, 0.13f, 0.16f, 0.03f, false, 4350f, 6400f);
            Ring(b, 0.17f, 0.06f, 0.02f, false, 4500f, 6500f);
            return Finish(b, 0.4f, 0.05f);
        }

        // ---- Silah sınıfına göre şarjör değiştirme ----
        private static float[] ReloadPistol(ref SynthRng rng)
        {
            var b = Buffer(1.0f);
            Click(b, ref rng, 0f, 0.8f, 2500f, 0.002f);
            Ring(b, 0f, 0.2f, 0.02f, false, 1700f, 2600f);
            NoiseSwell(b, ref rng, 0.02f, 0.12f, 0.2f, FilterKind.Bandpass, 1300f, 2200f, 2f, 0.5f);
            NoiseBurst(b, ref rng, 0.2f, 0.001f, 0.025f, 0.6f, FilterKind.Lowpass, 500f);
            NoiseSwell(b, ref rng, 0.5f, 0.07f, 0.22f, FilterKind.Bandpass, 1800f, 1100f, 2f, 0.6f);
            Clack(b, ref rng, 0.58f, 1f, 2600f);
            Click(b, ref rng, 0.78f, 0.7f, 3000f);
            Clack(b, ref rng, 0.82f, 0.7f, 2100f);
            return Finish(b, 0.7f);
        }

        private static float[] ReloadRifle(ref SynthRng rng)
        {
            var b = Buffer(1.8f);
            Click(b, ref rng, 0f, 0.8f, 2000f, 0.002f);
            Ring(b, 0f, 0.2f, 0.02f, false, 1400f, 2300f);
            NoiseSwell(b, ref rng, 0.03f, 0.16f, 0.25f, FilterKind.Bandpass, 1200f, 2200f, 2f, 0.5f);
            Tone(b, 0.22f, 180f, 120f, 0.02f, 0.001f, 0.03f, 0.4f);
            NoiseBurst(b, ref rng, 0.22f, 0.001f, 0.03f, 0.6f, FilterKind.Lowpass, 500f);
            NoiseSwell(b, ref rng, 0.7f, 0.1f, 0.25f, FilterKind.Bandpass, 1800f, 1100f, 2f, 0.6f);
            Clack(b, ref rng, 0.8f, 1f, 2400f);
            NoiseBurst(b, ref rng, 0.8f, 0.001f, 0.03f, 0.5f, FilterKind.Lowpass, 800f);
            // şarjör kolu çekme
            NoiseSwell(b, ref rng, 1.2f, 0.1f, 0.3f, FilterKind.Bandpass, 900f, 2000f, 2f, 0.6f);
            Clack(b, ref rng, 1.31f, 0.9f, 2000f);
            Clack(b, ref rng, 1.42f, 0.7f, 1700f);
            return Finish(b, 0.7f);
        }

        private static float[] ReloadLmg(ref SynthRng rng)
        {
            var b = Buffer(2.6f);
            // kapak aç
            Clack(b, ref rng, 0f, 1f, 1200f);
            NoiseSwell(b, ref rng, 0.05f, 0.25f, 0.3f, FilterKind.Bandpass, 700f, 1400f, 1.5f, 0.5f);
            // boş kutu çıkar, dolu kutu gir
            NoiseBurst(b, ref rng, 0.5f, 0.002f, 0.05f, 0.8f, FilterKind.Lowpass, 400f);
            Tone(b, 0.5f, 120f, 70f, 0.03f, 0.002f, 0.06f, 0.6f);
            NoiseSwell(b, ref rng, 0.95f, 0.2f, 0.25f, FilterKind.Bandpass, 1500f, 900f, 1.5f, 0.6f);
            NoiseBurst(b, ref rng, 1.15f, 0.002f, 0.06f, 0.9f, FilterKind.Lowpass, 350f);
            Clack(b, ref rng, 1.17f, 0.7f, 1500f);
            // kemer yerleştir (tıkırtı dizisi)
            for (var i = 0; i < 6; i++)
                Click(b, ref rng, 1.5f + i * 0.045f, 0.35f, 2800f, 0.0015f);
            // kapak kapat + sürgü
            Clack(b, ref rng, 1.95f, 1f, 1300f);
            NoiseSwell(b, ref rng, 2.1f, 0.12f, 0.3f, FilterKind.Bandpass, 900f, 2000f, 2f, 0.6f);
            Clack(b, ref rng, 2.25f, 0.9f, 1900f);
            return Finish(b, 0.72f);
        }

        private static float[] ReloadShotgun(ref SynthRng rng)
        {
            var b = Buffer(2.0f);
            for (var i = 0; i < 3; i++)
            {
                var t = 0.05f + i * 0.34f;
                NoiseSwell(b, ref rng, t, 0.05f, 0.2f, FilterKind.Bandpass, 1500f, 1500f, 1.2f);
                Click(b, ref rng, t + 0.06f, 0.7f, 1500f, 0.003f);
                NoiseBurst(b, ref rng, t + 0.06f, 0.001f, 0.025f, 0.7f, FilterKind.Lowpass, 700f);
                Tone(b, t + 0.06f, 260f, 200f, 0.02f, 0.001f, 0.02f, 0.3f);
            }

            // pompa
            NoiseSwell(b, ref rng, 1.2f, 0.14f, 0.35f, FilterKind.Bandpass, 800f, 1800f, 1.5f, 0.6f);
            Clack(b, ref rng, 1.35f, 1f, 1500f);
            NoiseSwell(b, ref rng, 1.5f, 0.1f, 0.3f, FilterKind.Bandpass, 1800f, 900f, 1.5f, 0.6f);
            Clack(b, ref rng, 1.62f, 1f, 1300f);
            return Finish(b, 0.72f);
        }

        private static float[] ReloadSniper(ref SynthRng rng)
        {
            var b = Buffer(2.4f);
            // sürgü aç
            NoiseSwell(b, ref rng, 0f, 0.12f, 0.3f, FilterKind.Bandpass, 900f, 2000f, 2f, 0.6f);
            Clack(b, ref rng, 0.13f, 0.9f, 2000f);
            // şarjör çıkar/tak
            Click(b, ref rng, 0.55f, 0.7f, 2000f, 0.002f);
            NoiseBurst(b, ref rng, 0.6f, 0.001f, 0.03f, 0.6f, FilterKind.Lowpass, 500f);
            NoiseSwell(b, ref rng, 1.0f, 0.1f, 0.25f, FilterKind.Bandpass, 1800f, 1100f, 2f, 0.6f);
            Clack(b, ref rng, 1.11f, 1f, 2300f);
            // sürgü kapat
            NoiseSwell(b, ref rng, 1.55f, 0.1f, 0.3f, FilterKind.Bandpass, 2000f, 1000f, 2f, 0.6f);
            Clack(b, ref rng, 1.68f, 1f, 1800f);
            Clack(b, ref rng, 1.8f, 0.8f, 1600f);
            NoiseBurst(b, ref rng, 1.8f, 0.001f, 0.03f, 0.6f, FilterKind.Lowpass, 600f);
            return Finish(b, 0.7f);
        }
    }
}
