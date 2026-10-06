using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.Weapons.Skins
{
    /// <summary>
    /// Silah renderer'larına kaplama uygular. Paylaşımlı malzemeyi DEĞİŞTİRMEZ: (kaynak malzeme, kaplama, kademe) başına
    /// bir kopya önbelleğe alınır. Clear() orijinal paylaşımlı malzemeye döner.
    /// ENTEGRASYON: Presentation lobi VİTRİN'i WeaponSkinCatalog.All'ı listeler, seçilen Id loadout'a yazılır
    /// ve WeaponViewModel oluşturulurken Apply(renderers, skinId) çağrılır (Infrastructure->Presentation referansı yok).
    /// </summary>
    public static class WeaponSkinApplier
    {
        private static readonly Dictionary<(int, string, int), Material> Cache = new Dictionary<(int, string, int), Material>();
        private static readonly Dictionary<int, Material[]> Originals = new Dictionary<int, Material[]>();

        public static int CachedCount => Cache.Count;

        public static bool Apply(IEnumerable<Renderer> renderers, string skinId)
        {
            if (renderers == null || !WeaponSkinCatalog.TryGet(skinId, out var skin)) return false;
            int tier = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, 3);
            bool any = false;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                int rid = r.GetHashCode();
                if (!Originals.TryGetValue(rid, out var orig)) { orig = r.sharedMaterials; Originals[rid] = orig; }
                var next = new Material[orig.Length];
                for (int i = 0; i < orig.Length; i++) next[i] = SkinnedCopy(orig[i], skin, tier);
                r.sharedMaterials = next;
                any = true;
            }
            return any;
        }

        public static void Clear(IEnumerable<Renderer> renderers)
        {
            if (renderers == null) return;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (Originals.TryGetValue(r.GetHashCode(), out var orig)) { r.sharedMaterials = orig; Originals.Remove(r.GetHashCode()); }
            }
        }

        private static Material SkinnedCopy(Material src, in WeaponSkin skin, int tier)
        {
            if (src == null) return null;
            // Optik cam / kauçuk gibi kaplanmaması gerekenler (şeffaf) olduğu gibi kalır.
            if (src.renderQueue >= 3000) return src;
            var key = (src.GetHashCode(), skin.Id, tier);
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(src) { name = src.name + "_" + skin.Id };
            var tex = WeaponSkinTextures.Get(skin, tier);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", Mathf.Min(skin.Metallic, GunMaterials.MetallicCap));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", skin.Smoothness);
            Cache[key] = m;
            return m;
        }
    }
}
