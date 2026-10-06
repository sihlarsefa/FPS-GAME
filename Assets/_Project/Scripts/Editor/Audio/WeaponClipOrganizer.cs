using System.Collections.Generic;
using System.IO;
using Project.Application.Catalogs;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>
    /// Kalibre sınıfı klip setlerinden (<c>Resources/Audio/Weapons/_class/&lt;kalibre&gt;</c>) silaha özel klasörler üretir
    /// (<c>Resources/Audio/Weapons/&lt;weaponId&gt;</c>). Aynı kalibredeki silahlar birbirinden ayrışsın diye her silah
    /// kimliğinden türetilen kaydırmayla katman başına klip alt kümesi seçer. Var olan silah klasörüne dokunmaz (idempotent).
    /// </summary>
    public static class WeaponClipOrganizer
    {
        private const string Root = "Assets/_Project/Resources/Audio/Weapons";

        [MenuItem("HAREKAT/Ses/Silah Klasörlerini Düzenle")]
        public static void Run()
        {
            var copied = 0;
            var weapons = 0;
            foreach (var def in WeaponCatalog.All)
            {
                if (def == null || string.IsNullOrEmpty(def.WeaponId))
                    continue;
                var profile = WeaponSoundProfiles.Get(def.WeaponId, def.Category);
                var n = Organize(def.WeaponId, profile.CaliberFolder);
                if (n > 0)
                    weapons++;
                copied += n;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Project.Infrastructure.Audio.Foley.WeaponClipLibrary.ClearCache();
            Debug.Log("[WeaponClipOrganizer] " + weapons + " silah, " + copied + " klip kopyalandı.");
        }

        /// <summary>Silah için klasör oluşturur; kopyalanan klip sayısını döndürür (klasör varsa/sınıf boşsa 0).</summary>
        public static int Organize(string weaponId, string caliberFolder)
        {
            var src = Root + "/_class/" + caliberFolder;
            var dst = Root + "/" + weaponId;
            if (!AssetDatabase.IsValidFolder(src) || AssetDatabase.IsValidFolder(dst))
                return 0;

            var byLayer = new SortedDictionary<string, List<string>>(System.StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { src }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var layer = Project.Infrastructure.Audio.Foley.FoleyNaming.LayerOfClipName(Path.GetFileNameWithoutExtension(path));
                if (layer == null)
                    continue;
                if (!byLayer.TryGetValue(layer, out var list))
                    byLayer[layer] = list = new List<string>();
                list.Add(path);
            }

            if (byLayer.Count == 0)
                return 0;

            AssetDatabase.CreateFolder(Root, weaponId);
            var hash = StableHash(weaponId);
            var count = 0;
            foreach (var kv in byLayer)
            {
                var clips = kv.Value;
                clips.Sort(System.StringComparer.Ordinal);
                // Çok klipli katmanda (bang/tail/distant...) silaha özel alt küme; tek klipli katman olduğu gibi.
                var take = clips.Count <= 1 ? clips.Count : Mathf.Max(1, (clips.Count + 1) / 2);
                var start = (int)(hash % (uint)clips.Count);
                for (var i = 0; i < take; i++)
                {
                    var from = clips[(start + i) % clips.Count];
                    var to = dst + "/" + kv.Key + "_" + (i + 1) + Path.GetExtension(from);
                    if (AssetDatabase.CopyAsset(from, to))
                        count++;
                }
            }

            return count;
        }

        private static uint StableHash(string s)
        {
            var h = 2166136261u;
            for (var i = 0; i < s.Length; i++)
                h = (h ^ s[i]) * 16777619u;
            return h;
        }
    }
}
