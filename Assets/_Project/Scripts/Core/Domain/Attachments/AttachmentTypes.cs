using System;

namespace Project.Core.Domain.Attachments
{
    /// <summary>Silah üzerindeki takma yuvası.</summary>
    public enum AttachmentSlot
    {
        Optic = 0,
        Muzzle = 1,
        Grip = 2,
        Magazine = 3
    }

    /// <summary>Takılabilir aksesuar türleri.</summary>
    public enum AttachmentKind
    {
        None = 0,
        RedDot = 1,
        Scope4x = 2,
        Scope8x = 3,
        Suppressor = 4,
        ForeGrip = 5,
        ExtendedMag = 6
    }

    /// <summary>Bir aksesuarın saf veri tanımı (Unity bağımsız).</summary>
    public sealed class AttachmentDefinition
    {
        public AttachmentKind Kind { get; }
        public AttachmentSlot Slot { get; }
        public string DisplayName { get; }
        /// <summary>Yakınlaştırma çarpanı (1 = yok). Sadece optik.</summary>
        public float Zoom { get; }
        /// <summary>Atış sesi çarpanı (1 = değişmez, 0.3 = %70 azalma).</summary>
        public float SoundMultiplier { get; }
        /// <summary>Namlu alevi çarpanı.</summary>
        public float FlashMultiplier { get; }
        /// <summary>Geri tepme çarpanı (0.85 = %15 azalma).</summary>
        public float RecoilMultiplier { get; }
        /// <summary>Şarjör kapasitesi çarpanı (1 = değişmez).</summary>
        public float MagazineMultiplier { get; }
        /// <summary>Hangi silah sınıflarına takılabilir.</summary>
        public WeaponCategory[] AllowedCategories { get; }

        public AttachmentDefinition(AttachmentKind kind, AttachmentSlot slot, string name, float zoom,
            float sound, float flash, float recoil, float magazine, params WeaponCategory[] allowed)
        {
            Kind = kind; Slot = slot; DisplayName = name; Zoom = zoom;
            SoundMultiplier = sound; FlashMultiplier = flash; RecoilMultiplier = recoil;
            MagazineMultiplier = magazine; AllowedCategories = allowed;
        }

        public bool AllowsCategory(WeaponCategory c) => Array.IndexOf(AllowedCategories, c) >= 0;
    }

    /// <summary>Sabit aksesuar kataloğu. NOT: Application/Catalogs/AttachmentCatalog.cs ayrı (item-id bazlı) sistemdir; bu saf kural/etki modelidir.</summary>
    public static class AttachmentCatalog
    {
        private static readonly WeaponCategory[] Firearms =
        {
            WeaponCategory.Smg, WeaponCategory.AssaultRifle, WeaponCategory.Dmr,
            WeaponCategory.Lmg, WeaponCategory.Sniper, WeaponCategory.Shotgun
        };

        private static readonly AttachmentDefinition[] All =
        {
            new AttachmentDefinition(AttachmentKind.RedDot, AttachmentSlot.Optic, "Red Dot", 1.25f, 1f, 1f, 1f, 1f,
                WeaponCategory.Pistol, WeaponCategory.Smg, WeaponCategory.AssaultRifle, WeaponCategory.Dmr,
                WeaponCategory.Lmg, WeaponCategory.Shotgun),
            new AttachmentDefinition(AttachmentKind.Scope4x, AttachmentSlot.Optic, "4x Dürbün", 4f, 1f, 1f, 1f, 1f,
                WeaponCategory.AssaultRifle, WeaponCategory.Dmr, WeaponCategory.Lmg, WeaponCategory.Sniper),
            new AttachmentDefinition(AttachmentKind.Scope8x, AttachmentSlot.Optic, "8x Dürbün", 8f, 1f, 1f, 1f, 1f,
                WeaponCategory.Dmr, WeaponCategory.Sniper),
            new AttachmentDefinition(AttachmentKind.Suppressor, AttachmentSlot.Muzzle, "Susturucu", 1f, 0.3f, 0.2f, 1f, 1f,
                WeaponCategory.Pistol, WeaponCategory.Smg, WeaponCategory.AssaultRifle, WeaponCategory.Dmr,
                WeaponCategory.Sniper),
            new AttachmentDefinition(AttachmentKind.ForeGrip, AttachmentSlot.Grip, "Ön Kabza", 1f, 1f, 1f, 0.85f, 1f,
                WeaponCategory.Smg, WeaponCategory.AssaultRifle, WeaponCategory.Dmr, WeaponCategory.Lmg),
            new AttachmentDefinition(AttachmentKind.ExtendedMag, AttachmentSlot.Magazine, "Uzatılmış Şarjör", 1f, 1f, 1f, 1f, 1.5f,
                Firearms[0], Firearms[1], Firearms[2], Firearms[3], WeaponCategory.Pistol)
        };

        public static AttachmentDefinition Get(AttachmentKind kind)
        {
            for (var i = 0; i < All.Length; i++)
                if (All[i].Kind == kind) return All[i];
            return null;
        }

        public static AttachmentDefinition[] Definitions() => (AttachmentDefinition[])All.Clone();
    }
}
