using System;
using System.Collections.Generic;
using Project.Core.Domain;

namespace Project.Application.Catalogs
{
    /// <summary>Silah eklenti yuvası (her yuvaya tek eklenti takılır).</summary>
    public enum AttachmentSlot
    {
        Sight = 0,
        Muzzle = 1,
        Grip = 2,
        Magazine = 3,
        Stock = 4
    }

    /// <summary>Bir eklentinin sabit tanımı ve silah değiştiricileri (çarpanlar 1 = etkisiz).</summary>
    public sealed class AttachmentDefinition
    {
        public string ItemId { get; set; }
        public string DisplayName { get; set; }
        public AttachmentSlot Slot { get; set; }
        public float MagazineMultiplier { get; set; } = 1f;
        public float RecoilMultiplier { get; set; } = 1f;
        public float AdsSpreadMultiplier { get; set; } = 1f;
        public float HipSpreadMultiplier { get; set; } = 1f;

        /// <summary>Nişan alma büyütmesi; 0 = silahın kendi değeri.</summary>
        public float AdsZoom { get; set; }

        public bool Suppressed { get; set; }

        /// <summary>Yalnız yatay (yaw) geri tepme çarpanı (1 = yok); RecoilMultiplier'ın üstüne biner.</summary>
        public float HorizontalRecoilMultiplier { get; set; } = 1f;

        /// <summary>Yalnız dikey (pitch) geri tepme çarpanı (1 = yok); RecoilMultiplier'ın üstüne biner.</summary>
        public float VerticalRecoilMultiplier { get; set; } = 1f;

        /// <summary>Namlu alevi çarpanı (1 = yok; 0.2 = %80 azalır).</summary>
        public float MuzzleFlashMultiplier { get; set; } = 1f;

        /// <summary>Atış sesi şiddeti çarpanı (1 = yok; 1.1 = %10 daha gürültülü).</summary>
        public float SoundMultiplier { get; set; } = 1f;

        /// <summary>Eklenti ağırlığı (kg): silah ağırlığına eklenir; nişan alma ve koşu→ateş süresini uzatır.</summary>
        public float WeightKg { get; set; }

        /// <summary>Ek nişan alma süresi çarpanı (ağırlık etkisinin üstüne; 1 = yok).</summary>
        public float AdsTimeMultiplier { get; set; } = 1f;

        /// <summary>Şarjör değiştirme süresi çarpanı (1 = yok).</summary>
        public float ReloadMultiplier { get; set; } = 1f;

        /// <summary>Namlu hızı çarpanı (1 = yok); susturucu mermiyi biraz yavaşlatır (daha çok düşüş).</summary>
        public float VelocityMultiplier { get; set; } = 1f;

        /// <summary>Uyumlu silah kategorileri.</summary>
        public WeaponCategory[] Compatible { get; set; } = Array.Empty<WeaponCategory>();

        public bool IsCompatibleWith(WeaponCategory category)
        {
            for (var i = 0; i < Compatible.Length; i++)
            {
                if (Compatible[i] == category)
                    return true;
            }

            return false;
        }
    }

    /// <summary>Tüm silah eklentileri (sabit sıra; ItemCatalog bunlardan eşya üretir).</summary>
    public static class AttachmentCatalog
    {
        public const int SlotCount = 5;

        /// <summary>Genişletme eklentilerinin kimlikleri (ItemIds.cs sahibi başka ajan; kimlikler burada tutulur).</summary>
        public static class Ids
        {
            public const string Scope8x = "att_scope8x";
            public const string AngledGrip = "att_agrip";
            public const string FlashHider = "att_flashhider";
            public const string Compensator = "att_compensator";
        }

        private static readonly List<AttachmentDefinition> _all = new();
        private static readonly Dictionary<string, AttachmentDefinition> _byId = new(StringComparer.Ordinal);

        static AttachmentCatalog()
        {
            const WeaponCategory P = WeaponCategory.Pistol;
            const WeaponCategory S = WeaponCategory.Smg;
            const WeaponCategory A = WeaponCategory.AssaultRifle;
            const WeaponCategory D = WeaponCategory.Dmr;
            const WeaponCategory L = WeaponCategory.Lmg;
            const WeaponCategory N = WeaponCategory.Sniper;
            const WeaponCategory G = WeaponCategory.Shotgun;

            Add(new AttachmentDefinition
            {
                ItemId = ItemIds.RedDot, DisplayName = "Kırmızı Nokta", Slot = AttachmentSlot.Sight,
                AdsZoom = 1.5f, AdsSpreadMultiplier = 0.9f, WeightKg = 0.1f, Compatible = new[] { P, S, A, D, L, G }
            });
            Add(new AttachmentDefinition
            {
                ItemId = ItemIds.Scope2x, DisplayName = "2x Dürbün", Slot = AttachmentSlot.Sight,
                AdsZoom = 2f, AdsSpreadMultiplier = 0.85f, WeightKg = 0.3f, AdsTimeMultiplier = 1.08f, Compatible = new[] { S, A, D, L }
            });
            Add(new AttachmentDefinition
            {
                ItemId = ItemIds.Scope4x, DisplayName = "4x Dürbün", Slot = AttachmentSlot.Sight,
                AdsZoom = 4f, AdsSpreadMultiplier = 0.8f, HipSpreadMultiplier = 1.1f, WeightKg = 0.5f, AdsTimeMultiplier = 1.18f, Compatible = new[] { A, D, L, N }
            });
            Add(new AttachmentDefinition
            {
                ItemId = ItemIds.Suppressor, DisplayName = "Susturucu", Slot = AttachmentSlot.Muzzle,
                RecoilMultiplier = 0.9f, Suppressed = true, WeightKg = 0.4f, VelocityMultiplier = 0.94f, Compatible = new[] { P, S, A, D, N }
            });
            Add(new AttachmentDefinition
            {
                ItemId = ItemIds.ExtMag, DisplayName = "Uzatılmış Şarjör", Slot = AttachmentSlot.Magazine,
                MagazineMultiplier = 1.4f, WeightKg = 0.3f, ReloadMultiplier = 1.12f, Compatible = new[] { P, S, A, D, L }
            });
            Add(new AttachmentDefinition
            {
                ItemId = ItemIds.VerticalGrip, DisplayName = "Dikey Tutamak", Slot = AttachmentSlot.Grip,
                RecoilMultiplier = 0.85f, HipSpreadMultiplier = 0.9f, WeightKg = 0.25f, AdsTimeMultiplier = 1.05f, Compatible = new[] { S, A, L, G }
            });
            Add(new AttachmentDefinition
            {
                ItemId = Ids.Scope8x, DisplayName = "8x Dürbün", Slot = AttachmentSlot.Sight,
                AdsZoom = 8f, AdsSpreadMultiplier = 0.7f, HipSpreadMultiplier = 1.2f, WeightKg = 0.7f, AdsTimeMultiplier = 1.35f, Compatible = new[] { D, N }
            });
            Add(new AttachmentDefinition
            {
                ItemId = Ids.AngledGrip, DisplayName = "Açılı Kabza", Slot = AttachmentSlot.Grip,
                HorizontalRecoilMultiplier = 0.9f, AdsTimeMultiplier = 1f / 1.1f, WeightKg = 0.2f, Compatible = new[] { S, A, L, G }
            });
            Add(new AttachmentDefinition
            {
                ItemId = Ids.FlashHider, DisplayName = "Alev Gizleyici", Slot = AttachmentSlot.Muzzle,
                MuzzleFlashMultiplier = 0.2f, WeightKg = 0.15f, Compatible = new[] { S, A, D, L, N }
            });
            Add(new AttachmentDefinition
            {
                ItemId = Ids.Compensator, DisplayName = "Kompansatör", Slot = AttachmentSlot.Muzzle,
                HorizontalRecoilMultiplier = 0.8f, SoundMultiplier = 1.1f, WeightKg = 0.2f, Compatible = new[] { P, S, A, D, L }
            });
            Add(new AttachmentDefinition
            {
                ItemId = ItemIds.SniperStock, DisplayName = "Nişancı Dipçiği", Slot = AttachmentSlot.Stock,
                RecoilMultiplier = 0.88f, AdsSpreadMultiplier = 0.9f, WeightKg = 0.45f, Compatible = new[] { S, A, D, N, G }
            });
        }

        public static IReadOnlyList<AttachmentDefinition> All => _all;

        public static AttachmentDefinition Get(string itemId) =>
            itemId != null && _byId.TryGetValue(itemId, out var d) ? d : null;

        public static bool IsAttachment(string itemId) => itemId != null && _byId.ContainsKey(itemId);

        public static bool IsCompatible(string itemId, WeaponCategory category)
        {
            var d = Get(itemId);
            return d != null && d.IsCompatibleWith(category);
        }

        /// <summary>Katalogun dışında kalan yeni etkiler için ek etiketler (LoadoutPage; "+" iyi, "-" kötü). Saf mantık.</summary>
        public static string ExtraEffectTags(AttachmentDefinition a)
        {
            if (a == null)
                return string.Empty;
            var tags = new List<string>(3);
            if (a.AdsZoom >= 6f) tags.Add("+" + a.AdsZoom.ToString("0") + "x");
            if (a.VerticalRecoilMultiplier < 0.995f) tags.Add("+DİKEY");
            if (a.HorizontalRecoilMultiplier < 0.995f) tags.Add("+YATAY");
            if (a.MuzzleFlashMultiplier < 0.995f) tags.Add("+ALEV");
            if (a.SoundMultiplier > 1.005f) tags.Add("-SES");
            return string.Join(" ", tags);
        }

        private static void Add(AttachmentDefinition d)
        {
            _byId[d.ItemId] = d;
            _all.Add(d);
        }
    }
}
