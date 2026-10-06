using System;
using Project.Infrastructure.Audio.Weapons;

namespace Project.Infrastructure.Audio.Foley
{
    /// <summary>Bir ayak sesinin dinleyicideki hesaplanmış sonucu.</summary>
    public readonly struct FootstepHearing
    {
        public readonly bool Audible;
        public readonly float Volume;
        public readonly float CutoffHz;
        public readonly float SpreadDeg;
        public readonly float LevelAtListenerDb;
        public readonly float MaskMarginDb;

        public FootstepHearing(bool audible, float volume, float cutoff, float spread, float level, float margin)
        {
            Audible = audible;
            Volume = volume;
            CutoffHz = cutoff;
            SpreadDeg = spread;
            LevelAtListenerDb = level;
            MaskMarginDb = margin;
        }
    }

    /// <summary>
    /// Uzaktan duyulabilirlik modeli (saf): mesafe + hava soğurması + engel (duvar) + düşey kat farkı (yapı sesi)
    /// + maskeleme (ortam, rüzgar, kendi/komşu silah sesi). Ayak sesi yalnızca eşiğin üzerindeyse duyulur;
    /// engelli/uzaktaysa yön belirsizliği (spread) büyür: duvar arkası ayak sesi "bir yerden" gelir.
    /// </summary>
    public static class FootstepAudibility
    {
        public const float AbsorptionDbPerMeter = 0.02f;
        public const float DetectMarginDb = 12f;

        /// <summary>
        /// Dinleyicide seviye ve duyulabilirlik. verticalMeters: kaynak-dinleyici yükseklik farkı (kat farkı ~3 m).
        /// structureBorne: yüzeyin yapı sesi payı (<see cref="SurfaceProfile.StructureBorne"/>).
        /// maskingDb: o anki sesli olay (silah vb.) seviyesi (yoksa 0): maskeleme en yüksek ortam/olaydan alınır.
        /// </summary>
        public static FootstepHearing Evaluate(float levelAt1mDb, float distance, BarrierMaterial barrier, float openingFraction,
            float verticalMeters, float structureBorne, float ambientDb, float maskingDb, float surfaceBrightnessHz)
        {
            var d = distance < 1f ? 1f : distance;
            var level = levelAt1mDb - 20f * (float)Math.Log10(d) - AbsorptionDbPerMeter * d;
            var cutoff = Math.Min(surfaceBrightnessHz, AirCutoffHz(d));

            if (barrier != BarrierMaterial.None)
            {
                level -= AcousticTransmission.CombinedLossDb(barrier, openingFraction);
                cutoff = Math.Min(cutoff, AcousticTransmission.CombinedCutoffHz(barrier, openingFraction));
            }

            var floors = Math.Abs(verticalMeters) / 3f;
            if (floors > 0.3f)
            {
                // Kat arası: beton döşeme 50 dB keser; ahşap/metal yapı sesi payı kadar geçirir.
                var sb = structureBorne < 0f ? 0f : structureBorne > 1f ? 1f : structureBorne;
                level -= floors * AcousticTransmission.FloorLossDb(BarrierMaterial.Concrete, sb > 0.5f) * (1f - 0.6f * sb);
                cutoff = Math.Min(cutoff, 250f + 450f * sb);
            }

            var floor = Math.Max(ambientDb, maskingDb) + DetectMarginDb;
            var margin = level - floor;
            var audible = margin > 0f;
            var volume = audible ? FootstepRules.SourceVolume(level + 10f) : 0f;
            var spread = 4f + (barrier != BarrierMaterial.None ? 18f : 0f) + (floors > 0.3f ? 40f : 0f) + 18f * Math.Min(1f, d / 60f);
            if (spread > 90f) spread = 90f;
            return new FootstepHearing(audible, volume, cutoff < 200f ? 200f : cutoff, spread, level, margin);
        }

        /// <summary>Hava soğurması kesimi: 15 m'e kadar açık, sonra 2 kHz'e düşen mesafe eğrisi.</summary>
        public static float AirCutoffHz(float distance)
        {
            if (distance <= 15f) return 20000f;
            var t = (float)Math.Log10(distance / 15f) / (float)Math.Log10(90f / 15f);
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return 20000f * (float)Math.Pow(2000f / 20000f, t);
        }

        /// <summary>Atış anı maskeleme: silah sesi seviyesi dinleyicide X dB ise yaklaşık 0.4 sn ayak sesi maskelenir.</summary>
        public static float GunMaskingDb(float gunLevelAtListenerDb, float secondsSinceShot)
        {
            if (secondsSinceShot < 0f || secondsSinceShot > 0.8f) return 0f;
            var fade = 1f - secondsSinceShot / 0.8f;
            return (gunLevelAtListenerDb - 20f) * fade;
        }
    }
}
