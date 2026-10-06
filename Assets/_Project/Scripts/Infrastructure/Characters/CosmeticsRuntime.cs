using System;
using System.Collections.Generic;
using Project.Application.Services;
using Project.Infrastructure.Persistence;
using UnityEngine;

namespace Project.Infrastructure.Characters
{
    /// <summary>
    /// Kozmetik servisine erişim ve görünüm eşlemesi (yalnızca görsel). Yerel oyuncunun <see cref="SoldierLook"/> değerine
    /// kamuflaj/bere/kolluk uygular; silah kaplaması rengini <see cref="Project.Infrastructure.Weapons.WeaponModelFactory"/> kancasına verir.
    /// </summary>
    public static class CosmeticsRuntime
    {
        private static CosmeticsService _service;

        private static readonly Dictionary<string, Color[]> Camo = new Dictionary<string, Color[]>
        {
            { "camo_forest", new[] { new Color(0.16f, 0.27f, 0.12f), new Color(0.3f, 0.22f, 0.12f), new Color(0.08f, 0.14f, 0.07f), new Color(0.04f, 0.05f, 0.04f) } },
            { "camo_mountain", new[] { new Color(0.5f, 0.5f, 0.47f), new Color(0.32f, 0.34f, 0.3f), new Color(0.22f, 0.25f, 0.2f), new Color(0.66f, 0.66f, 0.62f) } },
            { "camo_desert", new[] { new Color(0.76f, 0.68f, 0.5f), new Color(0.62f, 0.52f, 0.36f), new Color(0.5f, 0.42f, 0.3f), new Color(0.84f, 0.78f, 0.64f) } },
            { "camo_snow", new[] { new Color(0.9f, 0.92f, 0.94f), new Color(0.7f, 0.74f, 0.78f), new Color(0.5f, 0.54f, 0.58f), new Color(0.97f, 0.97f, 0.98f) } },
            { "camo_urban", new[] { new Color(0.55f, 0.56f, 0.56f), new Color(0.35f, 0.36f, 0.37f), new Color(0.18f, 0.19f, 0.2f), new Color(0.72f, 0.73f, 0.74f) } },
            { "camo_night", new[] { new Color(0.1f, 0.12f, 0.16f), new Color(0.06f, 0.08f, 0.12f), new Color(0.04f, 0.05f, 0.08f), new Color(0.15f, 0.17f, 0.2f) } },
            { "camo_coast", new[] { new Color(0.42f, 0.5f, 0.5f), new Color(0.6f, 0.56f, 0.44f), new Color(0.28f, 0.38f, 0.4f), new Color(0.82f, 0.8f, 0.7f) } }
        };

        private static readonly Dictionary<string, Color> Beret = new Dictionary<string, Color>
        {
            { "beret_green", new Color(0.2f, 0.32f, 0.16f) },
            { "beret_maroon", new Color(0.45f, 0.06f, 0.1f) },
            { "beret_black", new Color(0.07f, 0.07f, 0.08f) },
            { "beret_navy", new Color(0.08f, 0.14f, 0.3f) },
            { "beret_gold", new Color(0.8f, 0.62f, 0.15f) },
            { "beret_steel", new Color(0.5f, 0.54f, 0.58f) },
            { "beret_frost", new Color(0.75f, 0.88f, 0.95f) }
        };

        private static readonly Dictionary<string, Color> Armband = new Dictionary<string, Color>
        {
            { "armband_kartal", new Color(0.55f, 0.35f, 0.12f) },
            { "armband_bozkurt", new Color(0.55f, 0.58f, 0.62f) },
            { "armband_simsek", new Color(0.95f, 0.85f, 0.1f) },
            { "armband_yildirim", new Color(0.2f, 0.45f, 0.95f) },
            { "armband_kilic", new Color(0.85f, 0.85f, 0.88f) },
            { "armband_kaplan", new Color(0.95f, 0.5f, 0.08f) },
            { "armband_pars", new Color(0.7f, 0.45f, 0.15f) },
            { "armband_atmaca", new Color(0.3f, 0.3f, 0.34f) },
            { "armband_season1", new Color(0.85f, 0.1f, 0.15f) }
        };

        /// <summary>Silah kaplaması: kaplama kimliği -> (silah kimliği, ton).</summary>
        private static readonly Dictionary<string, (string weapon, Color tint)> Skins = new Dictionary<string, (string, Color)>
        {
            { "skin_mpt76_olive", ("ar_mpt76", new Color(0.35f, 0.4f, 0.22f)) },
            { "skin_mpt76_desert", ("ar_mpt76", new Color(0.72f, 0.6f, 0.4f)) },
            { "skin_jng90_snow", ("sr_jng90", new Color(0.88f, 0.9f, 0.92f)) },
            { "skin_jng90_night", ("sr_jng90", new Color(0.08f, 0.1f, 0.16f)) },
            { "skin_pmt76_steel", ("lmg_pmt76", new Color(0.55f, 0.58f, 0.62f)) },
            { "skin_g3_woodland", ("ar_g3a7", new Color(0.24f, 0.32f, 0.16f)) },
            { "skin_sar9_black", ("pistol_sar9", new Color(0.04f, 0.04f, 0.05f)) },
            { "skin_escort_copper", ("sg_escort", new Color(0.72f, 0.42f, 0.22f)) },
            { "skin_knt76_ridge", ("dmr_knt76", new Color(0.42f, 0.44f, 0.4f)) },
            { "skin_mpt55_coast", ("ar_mpt55", new Color(0.4f, 0.55f, 0.55f)) }
        };

        public static CosmeticsService Service
        {
            get
            {
                if (_service != null)
                    return _service;
                try
                {
                    // Menü ve oyun aynı PlayerPrefs deposunu paylaşır; kariyer tecrübesi depodan okunur.
                    var store = new PlayerPrefsSettingsStore();
                    _service = new CosmeticsService(store, LoadDefinitions());
                    _service.SyncUnlocks(CurrentXp());
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Kozmetik] Servis kurulamadı: " + e.Message);
                }

                return _service;
            }
        }

        /// <summary>Kayıtlı kariyer tecrübe puanı.</summary>
        public static int CurrentXp()
        {
            try { return new PlayerPrefsSettingsStore().GetInt(CareerStatsService.Keys.Experience, 0); }
            catch (Exception) { return 0; }
        }

        [Serializable]
        private sealed class Root { public List<CosmeticDefinition> items; }

        public static List<CosmeticDefinition> LoadDefinitions()
        {
            try
            {
                var asset = Resources.Load<TextAsset>("Progression/cosmetics");
                var root = asset != null ? JsonUtility.FromJson<Root>(asset.text) : null;
                return root?.items ?? new List<CosmeticDefinition>();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return new List<CosmeticDefinition>();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _service = null;

        /// <summary>Kuşanılmış kamuflaj/bere/kolluğu verilen görünüme uygular (null-güvenli).</summary>
        public static void ApplyToLook(SoldierLook look)
        {
            if (look == null)
                return;
            try
            {
                var s = Service;
                if (s == null)
                    return;

                if (Camo.TryGetValue(s.GetEquipped(CosmeticsService.SlotCamo) ?? "", out var c))
                {
                    look.CamoA = c[0]; look.CamoB = c[1]; look.CamoC = c[2]; look.CamoD = c[3];
                    look.CamoSeed += 17;
                }

                if (Beret.TryGetValue(s.GetEquipped(CosmeticsService.SlotBeret) ?? "", out var b))
                {
                    look.HasBeretColor = true;
                    look.BeretColor = b;
                }

                if (Armband.TryGetValue(s.GetEquipped(CosmeticsService.SlotArmband) ?? "", out var a))
                    look.Armband = a;

                // Yeni kozmetikler (camo kataloğu, yüz boyası, kask örtüsü, gili): SoldierLook.ApplyCosmetic.
                var camoId = s.GetEquipped(CosmeticsService.SlotCamo);
                if (!string.IsNullOrEmpty(camoId) && !Camo.ContainsKey(camoId))
                    look.ApplyCosmetic(camoId);
                look.ApplyCosmetic(s.GetEquipped(CosmeticsService.SlotFacePaint));
                look.ApplyCosmetic(s.GetEquipped(CosmeticsService.SlotHelmetCover));
                look.ApplyCosmetic(s.GetEquipped(CosmeticsService.SlotGhillie));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Kozmetik] Görünüm uygulanamadı: " + e.Message);
            }
        }

        /// <summary>Silah için kuşanılmış kaplama tonu (yoksa false).</summary>
        public static bool TryGetWeaponTint(string weaponId, out Color tint)
        {
            tint = Color.white;
            try
            {
                var s = Service;
                var id = s?.GetEquipped(CosmeticsService.SlotWeaponSkin);
                if (id != null && Skins.TryGetValue(id, out var v) && v.weapon == weaponId)
                {
                    tint = v.tint;
                    return true;
                }

                if (id != null && CamoCatalog.WrapTint(id, out var wrap))
                {
                    tint = wrap;
                    return true;
                }
            }
            catch (Exception) { /* görsel; sessiz */ }

            return false;
        }

        /// <summary>Panelde önizleme rengi (yoksa gri).</summary>
        public static Color PreviewColor(CosmeticDefinition d)
        {
            if (d == null) return Color.gray;
            if (Camo.TryGetValue(d.id, out var c)) return c[0];
            if (Beret.TryGetValue(d.id, out var b)) return b;
            if (Armband.TryGetValue(d.id, out var a)) return a;
            if (Skins.TryGetValue(d.id, out var s)) return s.tint;
            return new Color(0.45f, 0.5f, 0.38f);
        }

        /// <summary>Kaplama kimliğinin bağlı olduğu silah (panel etiketi için).</summary>
        public static string WeaponOfSkin(string skinId) => skinId != null && Skins.TryGetValue(skinId, out var v) ? v.weapon : null;
    }
}
