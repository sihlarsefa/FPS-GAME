using Project.Application.Catalogs;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.Config
{
    /// <summary>
    /// ESKİ prototip silah ayar varlığı (Settings/AssaultRifleConfig.asset). Asıl silah verisi
    /// <see cref="WeaponCatalog"/>'dadır; <see cref="useCatalogDefinition"/> açıkken ve kimlik katalogda varsa katalog tanımı
    /// döner, yoksa alanlardan bir tanım üretilir. Mevcut alan adları asset uyumu için korunur.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponConfig", menuName = "Project/Weapon Config")]
    public sealed class WeaponConfig : ScriptableObject
    {
        public string weaponId = "ar_mk1";
        public WeaponCategory category = WeaponCategory.AssaultRifle;
        public float damage = 28f;
        public int magazineSize = 30;
        public float fireIntervalSeconds = 0.1f;
        public float reloadDurationSeconds = 2.2f;
        public float range = 200f;
        public float headshotMultiplier = 2f;
        public float recoilKick = 0.03f;
        public Vector3 viewModelScale = new(0.08f, 0.12f, 0.45f);
        public Material viewModelMaterial;

        [Header("Katalog")]
        [Tooltip("weaponId WeaponCatalog'da varsa katalog tanımını kullan.")]
        public bool useCatalogDefinition = true;

        [Header("Ek (katalog dışı tanım için)")]
        public string displayName = "";
        public AmmoType ammoType = AmmoType.Mm556;
        public float recoilVertical = 0.6f;
        public float recoilHorizontal = 0.35f;
        public float hipSpread = 3f;
        public float adsSpread = 0.4f;
        public float adsZoom = 1.3f;
        public bool automatic = true;

        public WeaponDefinitionData ToDefinition()
        {
            if (useCatalogDefinition && WeaponCatalog.TryGet(weaponId, out var catalogDefinition) && catalogDefinition != null)
                return catalogDefinition;

            var definition = new WeaponDefinitionData(
                string.IsNullOrEmpty(weaponId) ? "weapon" : weaponId,
                category,
                Mathf.Max(0f, damage),
                Mathf.Max(1, magazineSize),
                Mathf.Max(0.01f, fireIntervalSeconds),
                Mathf.Max(0.1f, reloadDurationSeconds),
                Mathf.Max(1f, range),
                Mathf.Max(1f, headshotMultiplier))
            {
                AmmoType = ammoType,
                RecoilVertical = Mathf.Max(0f, recoilVertical),
                RecoilHorizontal = Mathf.Max(0f, recoilHorizontal),
                HipSpread = Mathf.Max(0f, hipSpread),
                AdsSpread = Mathf.Max(0f, adsSpread),
                AdsZoom = Mathf.Max(1f, adsZoom),
                FireModes = automatic ? new[] { FireMode.Single, FireMode.Auto } : new[] { FireMode.Single }
            };

            if (!string.IsNullOrEmpty(displayName))
                definition.DisplayName = displayName;

            return definition;
        }
    }
}
