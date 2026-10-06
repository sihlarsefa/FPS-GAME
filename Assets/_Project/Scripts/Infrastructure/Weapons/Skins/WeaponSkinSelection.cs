using UnityEngine;

namespace Project.Infrastructure.Weapons.Skins
{
    /// <summary>
    /// Silah başına seçili kaplamanın kalıcılığı (PlayerPrefs "harekat.skin.&lt;silah&gt;"). Mevcut kozmetik servisi
    /// ayrı bir katalog kullandığından (CosmeticsService) bu katalog için ayrı anahtar kullanılır. Boş/bilinmeyen = kaplamasız.
    /// </summary>
    public static class WeaponSkinSelection
    {
        public const string KeyPrefix = "harekat.skin.";

        public static string KeyFor(string weaponId) => KeyPrefix + (string.IsNullOrEmpty(weaponId) ? "genel" : weaponId);

        /// <summary>Katalogdaki sıra (0..All-1); yoksa -1 (kaplamasız).</summary>
        public static int IndexOf(string skinId)
        {
            if (string.IsNullOrEmpty(skinId)) return -1;
            for (int i = 0; i < WeaponSkinCatalog.All.Length; i++)
                if (WeaponSkinCatalog.All[i].Id == skinId) return i;
            return -1;
        }

        public static string Get(string weaponId)
        {
            try
            {
                var id = PlayerPrefs.GetString(KeyFor(weaponId), string.Empty);
                return IndexOf(id) >= 0 ? id : WeaponSkinCatalog.DefaultId;
            }
            catch (System.Exception) { return WeaponSkinCatalog.DefaultId; }
        }

        public static void Set(string weaponId, string skinId)
        {
            try
            {
                PlayerPrefs.SetString(KeyFor(weaponId), IndexOf(skinId) >= 0 ? skinId : WeaponSkinCatalog.DefaultId);
                PlayerPrefs.Save();
            }
            catch (System.Exception) { /* görsel tercih; sessiz */ }
        }
    }
}
