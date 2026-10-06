using Project.Application.Catalogs;
using UnityEngine;
using static Project.Infrastructure.Audio.SynthDsp;

namespace Project.Infrastructure.Audio.Foley
{
    /// <summary>
    /// Foley adımlarının prosedürel yedek sentezi (44.1 kHz mono). <see cref="Render"/> saf C#'tır (iş parçacığı güvenli);
    /// aynı (adım, kalibre, varyant) her zaman aynı dalgayı üretir. Hazır kayıt yoksa devreye girer.
    /// </summary>
    public static class FoleySynth
    {
        public const int VariantCount = 3;

        /// <summary>Kalibre sınıfına göre mekanik çınlama çarpanı (hafif silah tiz, ağır silah derin).</summary>
        public static float PitchScale(WeaponCaliber c)
        {
            switch (c)
            {
                case WeaponCaliber.Pistol9: return 1.18f;
                case WeaponCaliber.Smg9: return 1.1f;
                case WeaponCaliber.Rifle556: return 1f;
                case WeaponCaliber.Rifle762: return 0.92f;
                case WeaponCaliber.Dmr762: return 0.9f;
                case WeaponCaliber.Sniper762: return 0.84f;
                case WeaponCaliber.Lmg762: return 0.78f;
                default: return 0.86f;
            }
        }

        public static float[] Render(FoleyStep step, WeaponCaliber caliber, int variant)
        {
            if (step == FoleyStep.None)
                return null;
            var seed = 0xF01E7000u ^ ((uint)step * 2654435761u) ^ ((uint)caliber * 40503u) ^ ((uint)(variant + 1) * 97531u);
            var rng = new SynthRng(seed);
            var p = PitchScale(caliber) * (1f + 0.04f * variant);
            float[] b;
            switch (step)
            {
                case FoleyStep.MagRelease:
                    b = Buffer(0.18f);
                    Click(b, ref rng, 0f, 0.9f, 3000f, 0.0016f);
                    Ring(b, 0f, 0.25f, 0.02f, false, 2100f * p, 3300f * p);
                    NoiseBurst(b, ref rng, 0.004f, 0.001f, 0.02f, 0.3f, FilterKind.Bandpass, 900f);
                    return Finish(b, 0.55f);
                case FoleyStep.MagOut:
                    b = Buffer(0.4f);
                    NoiseSwell(b, ref rng, 0f, 0.14f, 0.28f, FilterKind.Bandpass, 1100f * p, 2100f * p, 2f, 0.5f);
                    Clack(b, ref rng, 0.16f, 0.7f, 1700f * p);
                    NoiseBurst(b, ref rng, 0.16f, 0.001f, 0.035f, 0.55f, FilterKind.Lowpass, 520f);
                    return Finish(b, 0.6f);
                case FoleyStep.MagPouch:
                    b = Buffer(0.45f);
                    NoiseSwell(b, ref rng, 0f, 0.18f, 0.35f, FilterKind.Bandpass, 700f, 1400f, 0.8f, 0.7f);
                    NoiseSwell(b, ref rng, 0.18f, 0.14f, 0.28f, FilterKind.Bandpass, 1500f, 800f, 0.8f, 0.7f);
                    Click(b, ref rng, 0.34f, 0.25f, 1800f, 0.004f);   // cırt/toka
                    NoiseBurst(b, ref rng, 0.2f, 0.002f, 0.03f, 0.35f, FilterKind.Lowpass, 400f);
                    return Finish(b, 0.5f);
                case FoleyStep.MagIn:
                    b = Buffer(0.38f);
                    NoiseSwell(b, ref rng, 0f, 0.1f, 0.22f, FilterKind.Bandpass, 1700f * p, 1100f * p, 2f, 0.6f);
                    Clack(b, ref rng, 0.11f, 1f, 2300f * p);
                    NoiseBurst(b, ref rng, 0.11f, 0.0008f, 0.03f, 0.6f, FilterKind.Lowpass, 700f);
                    Tone(b, 0.11f, 150f * p, 100f * p, 0.02f, 0.001f, 0.04f, 0.35f);
                    return Finish(b, 0.7f);
                case FoleyStep.MagSlap:
                    b = Buffer(0.25f);
                    NoiseBurst(b, ref rng, 0f, 0.001f, 0.035f, 0.9f, FilterKind.Lowpass, 650f);   // avuç
                    Tone(b, 0f, 190f * p, 120f * p, 0.03f, 0.001f, 0.05f, 0.5f);
                    Click(b, ref rng, 0.002f, 0.3f, 2600f, 0.0012f);
                    return Finish(b, 0.6f);
                case FoleyStep.Bolt:
                    b = Buffer(0.62f);
                    Click(b, ref rng, 0f, 0.8f, 2500f, 0.002f);
                    NoiseSwell(b, ref rng, 0.01f, 0.12f, 0.35f, FilterKind.Bandpass, 900f * p, 2000f * p, 2f, 0.6f);
                    Clack(b, ref rng, 0.15f, 1f, 1900f * p);
                    NoiseSwell(b, ref rng, 0.27f, 0.1f, 0.3f, FilterKind.Bandpass, 2000f * p, 1000f * p, 2f, 0.6f);
                    Clack(b, ref rng, 0.4f, 1f, 1650f * p);
                    NoiseBurst(b, ref rng, 0.4f, 0.001f, 0.035f, 0.6f, FilterKind.Lowpass, 560f);
                    return Finish(b, 0.75f);
                case FoleyStep.ChargingHandle:
                    b = Buffer(0.5f);
                    NoiseSwell(b, ref rng, 0f, 0.08f, 0.3f, FilterKind.Bandpass, 1000f * p, 1800f * p, 2f, 0.5f);
                    Clack(b, ref rng, 0.1f, 0.9f, 1800f * p);
                    Clack(b, ref rng, 0.27f, 1f, 1500f * p);
                    NoiseBurst(b, ref rng, 0.27f, 0.001f, 0.04f, 0.55f, FilterKind.Lowpass, 500f);
                    return Finish(b, 0.72f);
                case FoleyStep.ShellInsert:
                    b = Buffer(0.3f);
                    NoiseSwell(b, ref rng, 0f, 0.05f, 0.2f, FilterKind.Bandpass, 1500f, 1500f, 1.2f);
                    Click(b, ref rng, 0.06f, 0.7f, 1500f, 0.003f);
                    NoiseBurst(b, ref rng, 0.06f, 0.001f, 0.025f, 0.7f, FilterKind.Lowpass, 700f);
                    Tone(b, 0.06f, 260f, 200f, 0.02f, 0.001f, 0.02f, 0.3f);
                    return Finish(b, 0.6f);
                case FoleyStep.SelectorClick:
                    b = Buffer(0.1f);
                    Click(b, ref rng, 0f, 0.85f, 3500f, 0.001f);
                    Ring(b, 0f, 0.25f, 0.01f, false, 3100f * p);
                    Click(b, ref rng, 0.03f, 0.5f, 3500f, 0.001f);
                    return Finish(b, 0.45f);
                case FoleyStep.DryFire:
                    b = Buffer(0.16f);
                    Click(b, ref rng, 0f, 0.9f, 2500f, 0.002f);
                    Ring(b, 0.0005f, 0.35f, 0.018f, false, 1850f * p, 2720f * p, 4100f * p);
                    NoiseBurst(b, ref rng, 0f, 0.0005f, 0.01f, 0.3f, FilterKind.Lowpass, 600f);
                    return Finish(b, 0.6f);
                case FoleyStep.WeaponEquip:
                    b = Buffer(0.5f);
                    NoiseSwell(b, ref rng, 0f, 0.26f, 0.35f, FilterKind.Bandpass, 1100f, 1700f, 0.7f, 0.5f);
                    Clack(b, ref rng, 0.3f, 0.8f, 2100f * p);
                    NoiseBurst(b, ref rng, 0.3f, 0.002f, 0.04f, 0.4f, FilterKind.Lowpass, 500f);
                    return Finish(b, 0.55f);
                case FoleyStep.WeaponHolster:
                    b = Buffer(0.45f);
                    NoiseSwell(b, ref rng, 0f, 0.3f, 0.35f, FilterKind.Bandpass, 1600f, 900f, 0.7f, 0.5f);
                    NoiseBurst(b, ref rng, 0.3f, 0.003f, 0.05f, 0.55f, FilterKind.Lowpass, 420f);
                    Click(b, ref rng, 0.31f, 0.3f, 1600f, 0.003f);
                    return Finish(b, 0.5f);
                case FoleyStep.AdsRustle:
                    b = Buffer(0.3f);
                    NoiseSwell(b, ref rng, 0f, 0.2f, 0.3f, FilterKind.Bandpass, 900f, 1800f, 0.8f, 0.8f);
                    Click(b, ref rng, 0.15f, 0.12f, 3000f, 0.002f);
                    return Finish(b, 0.32f);
                case FoleyStep.SprintRattle:
                    b = Buffer(0.28f);
                    for (var i = 0; i < 5; i++)
                        Click(b, ref rng, 0.01f + i * rng.Range(0.025f, 0.05f), rng.Range(0.25f, 0.6f), rng.Range(2600f, 4200f), 0.0012f);
                    Ring(b, 0.01f, 0.18f, 0.03f, false, 2400f * p, 3600f * p);
                    NoiseSwell(b, ref rng, 0f, 0.14f, 0.2f, FilterKind.Bandpass, 800f, 1200f, 0.8f, 0.7f);
                    return Finish(b, 0.4f);
                case FoleyStep.PlateCarrier:
                    b = Buffer(0.4f);
                    NoiseBurst(b, ref rng, 0f, 0.002f, 0.05f, 0.7f, FilterKind.Lowpass, 380f);   // plaka gövde
                    Tone(b, 0f, 120f, 80f, 0.04f, 0.002f, 0.08f, 0.5f);
                    NoiseSwell(b, ref rng, 0.02f, 0.2f, 0.3f, FilterKind.Bandpass, 700f, 1500f, 0.8f, 0.8f);
                    Click(b, ref rng, 0.05f, 0.2f, 2000f, 0.003f);
                    return Finish(b, 0.4f);
                case FoleyStep.ProneDown:
                    b = Buffer(0.7f);
                    NoiseSwell(b, ref rng, 0f, 0.3f, 0.35f, FilterKind.Bandpass, 900f, 500f, 0.7f, 0.7f);
                    NoiseBurst(b, ref rng, 0.3f, 0.003f, 0.09f, 0.9f, FilterKind.Lowpass, 300f);   // gövde
                    Tone(b, 0.3f, 90f, 55f, 0.05f, 0.002f, 0.12f, 0.6f);
                    for (var i = 0; i < 4; i++)
                        Click(b, ref rng, 0.32f + i * rng.Range(0.03f, 0.06f), rng.Range(0.2f, 0.45f), rng.Range(2400f, 3800f), 0.0014f);
                    Ring(b, 0.32f, 0.15f, 0.04f, false, 1900f * p, 2900f * p);
                    return Finish(b, 0.65f);
                case FoleyStep.LandBody:
                    b = Buffer(0.5f);
                    NoiseBurst(b, ref rng, 0f, 0.002f, 0.06f, 0.9f, FilterKind.Lowpass, 350f);
                    Tone(b, 0f, 100f, 60f, 0.04f, 0.001f, 0.1f, 0.65f);
                    for (var i = 0; i < 4; i++)
                        Click(b, ref rng, 0.01f + i * rng.Range(0.02f, 0.05f), rng.Range(0.25f, 0.5f), rng.Range(2400f, 4000f), 0.0014f);
                    Ring(b, 0.01f, 0.15f, 0.04f, false, 2000f * p, 3100f * p);
                    NoiseSwell(b, ref rng, 0.04f, 0.15f, 0.2f, FilterKind.Bandpass, 800f, 1300f, 0.8f, 0.7f);
                    return Finish(b, 0.65f);
                default:
                    return null;
            }
        }

        private static float[] Finish(float[] b, float peak)
        {
            Normalize(b, peak);
            FadeEdges(b, 0f, 0.012f);
            return b;
        }

        /// <summary>Ana iş parçacığı: örneklerden klip.</summary>
        public static AudioClip ToClip(FoleyStep step, WeaponCaliber caliber, int variant)
        {
            var s = Render(step, caliber, variant);
            if (s == null || s.Length == 0)
                return null;
            var clip = AudioClip.Create("foley_" + step + "_" + caliber + "_" + variant, s.Length, 1, SampleRate, false);
            clip.SetData(s, 0);
            clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return clip;
        }
    }
}
