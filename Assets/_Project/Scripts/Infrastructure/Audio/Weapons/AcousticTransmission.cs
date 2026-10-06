using System;

namespace Project.Infrastructure.Audio.Weapons
{
    /// <summary>Ses geçirgenlik engeli malzemesi (duvar, kapı, pencere...).</summary>
    public enum BarrierMaterial { None = 0, OpenDoorway, Window, ThinWood, Door, Metal, Brick, Concrete }

    /// <summary>
    /// Saf engel/geçirgenlik kuralları: kütle yasası gereği yüksek frekans çok, alçak frekans az kesilir.
    /// Silah sesi ve ayak sesi aynı tabloyu kullanır. Küçük bir boşluk (aralık kapı) yalıtımı ciddi düşürür:
    /// %5 açıklık, ne kadar kalın olursa olsun toplam yalıtımı ~13 dB ile sınırlar.
    /// </summary>
    public static class AcousticTransmission
    {
        /// <summary>Geniş bantlı ses iletim kaybı (dB), ~500 Hz - 1 kHz civarı.</summary>
        public static float LossDb(BarrierMaterial m)
        {
            switch (m)
            {
                case BarrierMaterial.OpenDoorway: return 3f;
                case BarrierMaterial.Window: return 22f;
                case BarrierMaterial.ThinWood: return 18f;
                case BarrierMaterial.Door: return 24f;
                case BarrierMaterial.Metal: return 30f;
                case BarrierMaterial.Brick: return 40f;
                case BarrierMaterial.Concrete: return 50f;
                default: return 0f;
            }
        }

        /// <summary>Engelden geçen sesin alçak geçiren kesimi (Hz).</summary>
        public static float CutoffHz(BarrierMaterial m)
        {
            switch (m)
            {
                case BarrierMaterial.OpenDoorway: return 9000f;
                case BarrierMaterial.Window: return 4500f;
                case BarrierMaterial.ThinWood: return 2200f;
                case BarrierMaterial.Door: return 1800f;
                case BarrierMaterial.Metal: return 1500f;
                case BarrierMaterial.Brick: return 900f;
                case BarrierMaterial.Concrete: return 600f;
                default: return 20000f;
            }
        }

        /// <summary>
        /// Engel + sızıntı birlikte kayıp (dB, pozitif). openingFraction 0..1 yüzeyin açık oranı:
        /// toplam geçirgenlik = (1-f)*10^(-TL/10) + f.
        /// </summary>
        public static float CombinedLossDb(BarrierMaterial m, float openingFraction)
        {
            var f = openingFraction < 0f ? 0f : openingFraction > 1f ? 1f : openingFraction;
            var tau = (1f - f) * (float)Math.Pow(10.0, -LossDb(m) / 10.0) + f;
            if (tau >= 1f) return 0f;
            return -10f * (float)Math.Log10(tau);
        }

        /// <summary>Sızıntı varken kesim yükselir: açıklık oranı arttıkça yüksek frekans geri gelir (log karışım).</summary>
        public static float CombinedCutoffHz(BarrierMaterial m, float openingFraction)
        {
            var f = openingFraction < 0f ? 0f : openingFraction > 1f ? 1f : openingFraction;
            // Geçen enerjideki sızıntı payı: yüksek frekanslar açıklıktan tam geçer.
            var loss = LossDb(m);
            var leakShare = f / ((1f - f) * (float)Math.Pow(10.0, -loss / 10.0) + f + 1e-9f);
            var lo = CutoffHz(m);
            var hi = 20000f;
            return (float)Math.Exp(Math.Log(lo) + (Math.Log(hi) - Math.Log(lo)) * Math.Min(1f, leakShare));
        }

        /// <summary>Birden fazla engel ardışık: kayıplar toplanır, kesim en düşük olandır.</summary>
        public static void Chain(BarrierMaterial[] barriers, out float lossDb, out float cutoffHz)
        {
            lossDb = 0f;
            cutoffHz = 20000f;
            if (barriers == null) return;
            for (var i = 0; i < barriers.Length; i++)
            {
                lossDb += LossDb(barriers[i]);
                cutoffHz = Math.Min(cutoffHz, CutoffHz(barriers[i]));
            }
        }

        /// <summary>Üst kat/alt kat: yapı sesi (ahşap/metal döşeme) ayak sesini taşır, beton keser.</summary>
        public static float FloorLossDb(BarrierMaterial floor, bool structureBorne)
        {
            var db = LossDb(floor);
            return structureBorne ? db * 0.55f : db;
        }

        /// <summary>dB kaybı algısal ses oranına çevirir (her -10 dB algıda yarı yüksek; 2^(-dB/10)).</summary>
        public static float PerceivedGain(float lossDb) => (float)Math.Pow(2.0, -lossDb / 10.0);

        /// <summary>dB kaybı genlik (basınç) kazancına çevirir.</summary>
        public static float AmplitudeGain(float lossDb) => (float)Math.Pow(10.0, -lossDb / 20.0);
    }
}
