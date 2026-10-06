using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.Audio.Foley
{
    /// <summary>
    /// Resources'taki hazır silah kayıtlarını (Sonniss vb.) otomatik keşfeder:
    /// <c>Resources/Audio/Weapons/&lt;weaponId&gt;/&lt;layer&gt;_&lt;n&gt;.wav</c>, bulunamazsa
    /// <c>Resources/Audio/Weapons/_class/&lt;kalibre&gt;/&lt;layer&gt;_&lt;n&gt;.wav</c>; o da yoksa çağıran prosedürel yola düşer.
    /// Katman adları <see cref="FoleyNaming"/>. Aynı klip arka arkaya seçilmez.
    /// </summary>
    public static class WeaponClipLibrary
    {
        private static readonly Dictionary<string, Dictionary<string, List<AudioClip>>> Folders =
            new Dictionary<string, Dictionary<string, List<AudioClip>>>(StringComparer.Ordinal);

        private static readonly Dictionary<string, AudioClip> LastPick = new Dictionary<string, AudioClip>(StringComparer.Ordinal);

        /// <summary>Klasör önbelleğini temizler (kayıtlar sonradan eklendiyse).</summary>
        public static void ClearCache()
        {
            Folders.Clear();
            LastPick.Clear();
        }

        /// <summary>Silah klasörü, yoksa kalibre klasörü. Bulunursa true.</summary>
        public static bool TryPick(string weaponId, string caliberFolder, string layer, out AudioClip clip)
        {
            clip = null;
            if (string.IsNullOrEmpty(layer))
                return false;
            if (!string.IsNullOrEmpty(weaponId) && TryPickFrom(FoleyNaming.WeaponFolder(weaponId), layer, out clip))
                return true;
            return !string.IsNullOrEmpty(caliberFolder) && TryPickFrom(FoleyNaming.ClassFolderPath(caliberFolder), layer, out clip);
        }

        public static bool HasLayer(string weaponId, string caliberFolder, string layer) =>
            TryPick(weaponId, caliberFolder, layer, out _);

        private static bool TryPickFrom(string folder, string layer, out AudioClip clip)
        {
            clip = null;
            var map = Load(folder);
            if (map == null || !map.TryGetValue(layer, out var list) || list.Count == 0)
                return false;

            var key = folder + "|" + layer;
            LastPick.TryGetValue(key, out var last);
            for (var attempt = 0; attempt < 3; attempt++)
            {
                clip = list[UnityEngine.Random.Range(0, list.Count)];
                if (list.Count == 1 || clip != last)
                    break;
            }

            LastPick[key] = clip;
            return clip != null;
        }

        private static Dictionary<string, List<AudioClip>> Load(string folder)
        {
            if (Folders.TryGetValue(folder, out var cached))
                return cached;

            Dictionary<string, List<AudioClip>> map = null;
            try
            {
                var clips = Resources.LoadAll<AudioClip>(folder);
                if (clips != null && clips.Length > 0)
                {
                    map = new Dictionary<string, List<AudioClip>>(StringComparer.Ordinal);
                    for (var i = 0; i < clips.Length; i++)
                    {
                        if (clips[i] == null)
                            continue;
                        var layer = FoleyNaming.LayerOfClipName(clips[i].name);
                        if (layer == null)
                            continue;
                        if (!map.TryGetValue(layer, out var l))
                            map[layer] = l = new List<AudioClip>(4);
                        l.Add(clips[i]);
                    }
                }
            }
            catch (Exception)
            {
                map = null;   // Resources erişilemedi — prosedürel yol.
            }

            Folders[folder] = map;
            return map;
        }
    }

    /// <summary>Klip kaynağı: yok (prosedürel), silah klasörü ya da kalibre sınıfı klasörü.</summary>
    public enum ClipSource { None = 0, Weapon, Class }

    /// <summary>
    /// Ses kapsam raporu (saf, Unity/AssetDatabase yok): Resources'a göreli dosya yollarından
    /// silah/kalibre başına hangi ateş katmanının gerçek kayıtla dolu olduğunu çıkarır.
    /// </summary>
    public static class ClipCoverageReport
    {
        /// <summary>Rapordaki ateş katmanları (sıra: bang/mech/thump/tail/distant/suppressed).</summary>
        public static readonly string[] ReportLayers = { "bang", "mech", "thump", "tail_outdoor", "distant", "supp" };

        /// <summary>Klasör (Resources yolu, uzantısız) -> katman kümesi. Yollar "/" ile, Resources köküne göre; .meta atlanır.</summary>
        public static Dictionary<string, HashSet<string>> IndexFolders(IEnumerable<string> resourceRelativeFiles)
        {
            var map = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            if (resourceRelativeFiles == null)
                return map;
            foreach (var raw in resourceRelativeFiles)
            {
                if (string.IsNullOrEmpty(raw))
                    continue;
                var path = raw.Replace('\\', '/');
                if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;
                var slash = path.LastIndexOf('/');
                if (slash <= 0)
                    continue;
                var folder = path.Substring(0, slash);
                var file = path.Substring(slash + 1);
                var dot = file.LastIndexOf('.');
                if (dot > 0)
                    file = file.Substring(0, dot);
                var layer = FoleyNaming.LayerOfClipName(file);
                if (layer == null)
                    continue;
                if (!map.TryGetValue(folder, out var set))
                    map[folder] = set = new HashSet<string>(StringComparer.Ordinal);
                set.Add(layer);
            }

            return map;
        }

        public static ClipSource SourceOf(Dictionary<string, HashSet<string>> index, string weaponId, string caliberFolder, string layer)
        {
            if (index == null || string.IsNullOrEmpty(layer))
                return ClipSource.None;
            if (!string.IsNullOrEmpty(weaponId) && index.TryGetValue(FoleyNaming.WeaponFolder(weaponId), out var w) && w.Contains(layer))
                return ClipSource.Weapon;
            if (!string.IsNullOrEmpty(caliberFolder) && index.TryGetValue(FoleyNaming.ClassFolderPath(caliberFolder), out var c) && c.Contains(layer))
                return ClipSource.Class;
            return ClipSource.None;
        }

        /// <summary>Tek satır: weaponId | bang | mech | thump | tail | distant | supp | kaynak.</summary>
        public static string FormatRow(Dictionary<string, HashSet<string>> index, string weaponId, string caliberFolder)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(weaponId).Append(" | ");
            var real = 0;
            for (var i = 0; i < ReportLayers.Length; i++)
            {
                var src = SourceOf(index, weaponId, caliberFolder, ReportLayers[i]);
                if (src != ClipSource.None)
                    real++;
                sb.Append(src == ClipSource.Weapon ? "silah" : src == ClipSource.Class ? "sınıf" : "-").Append(" | ");
            }

            sb.Append(real == 0 ? "prosedürel" : real == ReportLayers.Length ? "gerçek" : "gerçek+prosedürel");
            return sb.ToString();
        }

        /// <summary>Hiçbir silahta gerçek kaydı olmayan katmanlar (boş kategoriler).</summary>
        public static List<string> EmptyLayers(Dictionary<string, HashSet<string>> index, IEnumerable<KeyValuePair<string, string>> weaponsAndCalibers)
        {
            var empty = new List<string>();
            for (var i = 0; i < ReportLayers.Length; i++)
            {
                var any = false;
                if (weaponsAndCalibers != null)
                    foreach (var kv in weaponsAndCalibers)
                        if (SourceOf(index, kv.Key, kv.Value, ReportLayers[i]) != ClipSource.None) { any = true; break; }
                if (!any)
                    empty.Add(ReportLayers[i]);
            }

            return empty;
        }
    }
}
