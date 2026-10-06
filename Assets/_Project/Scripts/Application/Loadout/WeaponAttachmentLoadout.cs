using System.Collections.Generic;
using Project.Core.Domain;
using Project.Core.Domain.Attachments;

namespace Project.Application.Loadouts
{
    /// <summary>
    /// Oyuncunun silah başına aksesuar seçimi (silah id -> takılı set). Saf mantık. Canlı sistem item-id tabanlı (Application/Catalogs/AttachmentCatalog + LoadoutSelection + WeaponRuntimeService);
    /// bu sınıf kural/etki modelidir, UI/ateş/mesh bağlantısı item-id yolundan yapılır.
    /// </summary>
    public sealed class WeaponAttachmentLoadout
    {
        private readonly Dictionary<string, WeaponAttachments> _byWeapon = new Dictionary<string, WeaponAttachments>();

        public bool TryAttach(string weaponId, WeaponCategory category, AttachmentKind kind)
        {
            if (string.IsNullOrEmpty(weaponId)) return false;
            if (!_byWeapon.TryGetValue(weaponId, out var set) || set.Weapon != category)
            {
                if (!AttachmentRules.CanAttach(category, kind)) return false;
                set = new WeaponAttachments(category);
                _byWeapon[weaponId] = set;
            }
            return set.TryAttach(kind);
        }

        public AttachmentKind Get(string weaponId, AttachmentSlot slot) =>
            weaponId != null && _byWeapon.TryGetValue(weaponId, out var s) ? s.Get(slot) : AttachmentKind.None;

        public void Detach(string weaponId, AttachmentSlot slot)
        {
            if (weaponId != null && _byWeapon.TryGetValue(weaponId, out var s)) s.Detach(slot);
        }

        public AttachmentEffects Effects(string weaponId) =>
            weaponId != null && _byWeapon.TryGetValue(weaponId, out var s)
                ? AttachmentEffects.Compute(s)
                : AttachmentEffects.Neutral;
    }
}
