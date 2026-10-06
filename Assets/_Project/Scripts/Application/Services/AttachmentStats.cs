using System;
using System.Collections.Generic;
using Project.Application.Catalogs;

namespace Project.Application.Services
{
    /// <summary>Takılı eklentilerin birleşik silah değiştiricileri (saf veri).</summary>
    public readonly struct AttachmentModifiers
    {
        public static readonly AttachmentModifiers None = new(1f, 1f, 1f, 1f, 0f, false);

        public float MagazineMultiplier { get; }
        public float RecoilMultiplier { get; }
        public float AdsSpreadMultiplier { get; }
        public float HipSpreadMultiplier { get; }

        /// <summary>0 = silahın kendi büyütmesi.</summary>
        public float AdsZoom { get; }

        public bool Suppressed { get; }

        /// <summary>Takılı eklentilerin toplam ağırlığı (kg).</summary>
        public float WeightKg { get; }

        /// <summary>Eklentilerin ek nişan alma süresi çarpanı (ağırlık etkisinin üstüne).</summary>
        public float AdsTimeMultiplier { get; }

        public float ReloadMultiplier { get; }
        public float VelocityMultiplier { get; }

        public AttachmentModifiers(float magazine, float recoil, float adsSpread, float hipSpread, float adsZoom, bool suppressed,
            float weightKg, float adsTime, float reload, float velocity)
            : this(magazine, recoil, adsSpread, hipSpread, adsZoom, suppressed)
        {
            WeightKg = weightKg;
            AdsTimeMultiplier = adsTime;
            ReloadMultiplier = reload;
            VelocityMultiplier = velocity;
        }

        public AttachmentModifiers(float magazine, float recoil, float adsSpread, float hipSpread, float adsZoom, bool suppressed)
        {
            WeightKg = 0f;
            AdsTimeMultiplier = 1f;
            ReloadMultiplier = 1f;
            VelocityMultiplier = 1f;
            MagazineMultiplier = magazine;
            RecoilMultiplier = recoil;
            AdsSpreadMultiplier = adsSpread;
            HipSpreadMultiplier = hipSpread;
            AdsZoom = adsZoom;
            Suppressed = suppressed;
        }
    }

    /// <summary>Eklenti kimliklerinden değiştirici hesabı ve uygulama yardımcıları.</summary>
    public static class AttachmentStats
    {
        /// <summary>Kimlik listesinden birleşik değiştirici (null/bilinmeyen kimlikler yok sayılır).</summary>
        public static AttachmentModifiers Compute(IReadOnlyList<string> itemIds)
        {
            if (itemIds == null)
                return AttachmentModifiers.None;

            float mag = 1f, recoil = 1f, ads = 1f, hip = 1f, zoom = 0f, weight = 0f, adsTime = 1f, reload = 1f, velocity = 1f;
            var suppressed = false;
            for (var i = 0; i < itemIds.Count; i++)
            {
                var d = AttachmentCatalog.Get(itemIds[i]);
                if (d == null)
                    continue;

                mag *= d.MagazineMultiplier;
                recoil *= d.RecoilMultiplier;
                ads *= d.AdsSpreadMultiplier;
                hip *= d.HipSpreadMultiplier;
                if (d.AdsZoom > zoom)
                    zoom = d.AdsZoom;
                suppressed |= d.Suppressed;
                weight += d.WeightKg;
                adsTime *= d.AdsTimeMultiplier;
                reload *= d.ReloadMultiplier;
                velocity *= d.VelocityMultiplier;
            }

            return new AttachmentModifiers(mag, recoil, ads, hip, zoom, suppressed, weight, adsTime, reload, velocity);
        }

        public static int ApplyMagazine(int baseSize, in AttachmentModifiers m)
        {
            if (baseSize <= 0)
                return 0;

            return Math.Max(baseSize, (int)Math.Round(baseSize * m.MagazineMultiplier));
        }

        public static float ApplyZoom(float baseZoom, in AttachmentModifiers m) => m.AdsZoom > 0f ? m.AdsZoom : baseZoom;

        /// <summary>Dürbün (≥3x) takılı mı?</summary>
        public static bool HasScope(in AttachmentModifiers m) => m.AdsZoom >= 3f;
    }
}
