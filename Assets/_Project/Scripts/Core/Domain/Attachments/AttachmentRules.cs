using System;

namespace Project.Core.Domain.Attachments
{
    /// <summary>Takma kuralları: hangi silaha hangi aksesuar takılabilir.</summary>
    public static class AttachmentRules
    {
        public static bool CanAttach(WeaponCategory weapon, AttachmentKind kind)
        {
            if (weapon == WeaponCategory.None || weapon == WeaponCategory.Melee) return false;
            var def = AttachmentCatalog.Get(kind);
            return def != null && def.AllowsCategory(weapon);
        }

        public static AttachmentSlot SlotOf(AttachmentKind kind)
        {
            var def = AttachmentCatalog.Get(kind);
            if (def == null) throw new ArgumentException("Bilinmeyen aksesuar: " + kind);
            return def.Slot;
        }
    }

    /// <summary>Bir silahın takılı aksesuarları (slot başına en fazla bir).</summary>
    public sealed class WeaponAttachments
    {
        private readonly AttachmentKind[] _slots = new AttachmentKind[4];

        public WeaponCategory Weapon { get; }

        public WeaponAttachments(WeaponCategory weapon) { Weapon = weapon; }

        public AttachmentKind Get(AttachmentSlot slot) => _slots[(int)slot];

        /// <summary>Kurallara uyuyorsa takar (aynı slottakini değiştirir). Uymuyorsa false.</summary>
        public bool TryAttach(AttachmentKind kind)
        {
            if (!AttachmentRules.CanAttach(Weapon, kind)) return false;
            _slots[(int)AttachmentRules.SlotOf(kind)] = kind;
            return true;
        }

        public void Detach(AttachmentSlot slot) => _slots[(int)slot] = AttachmentKind.None;

        public bool Has(AttachmentKind kind) => kind != AttachmentKind.None && _slots[(int)AttachmentRules.SlotOf(kind)] == kind;
    }

    /// <summary>Takılı aksesuarların toplam etkisi.</summary>
    public readonly struct AttachmentEffects
    {
        public readonly float Zoom;
        public readonly float SoundMultiplier;
        public readonly float FlashMultiplier;
        public readonly float RecoilMultiplier;
        public readonly float MagazineMultiplier;

        public AttachmentEffects(float zoom, float sound, float flash, float recoil, float mag)
        {
            Zoom = zoom; SoundMultiplier = sound; FlashMultiplier = flash; RecoilMultiplier = recoil; MagazineMultiplier = mag;
        }

        public static AttachmentEffects Neutral => new AttachmentEffects(1f, 1f, 1f, 1f, 1f);

        public static AttachmentEffects Compute(WeaponAttachments set)
        {
            if (set == null) return Neutral;
            float zoom = 1f, snd = 1f, fl = 1f, rec = 1f, mag = 1f;
            for (var s = 0; s < 4; s++)
            {
                var def = AttachmentCatalog.Get(set.Get((AttachmentSlot)s));
                if (def == null) continue;
                zoom *= def.Zoom; snd *= def.SoundMultiplier; fl *= def.FlashMultiplier;
                rec *= def.RecoilMultiplier; mag *= def.MagazineMultiplier;
            }
            return new AttachmentEffects(zoom, snd, fl, rec, mag);
        }

        /// <summary>Uzatılmış şarjör sonrası kapasite (yukarı yuvarlanır).</summary>
        public int ApplyMagazine(int baseCapacity) => (int)Math.Ceiling(baseCapacity * MagazineMultiplier - 1e-4f);
    }
}
