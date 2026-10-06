using System.Collections.Generic;
using System.IO;
using System.Text;
using Project.Application.Catalogs;
using Project.Infrastructure.Audio.Foley;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>Gerçek ses kapsam denetimi: silah katmanları + ortam sesleri. Çıktı: konsol tablosu ve Docs/SES_KAPSAM.md.</summary>
    public static class ClipAudit
    {
        private const string ResourcesDir = "Assets/_Project/Resources";

        [MenuItem("HAREKAT/Ses/Kapsam Denetimi")]
        public static void Run() => Debug.Log(Audit(true));

        public static string Audit(bool writeDoc)
        {
            var files = new List<string>();
            var root = Path.GetFullPath(ResourcesDir);
            foreach (var sub in new[] { "Audio/Weapons", "Audio/Ambience" })
            {
                var dir = Path.Combine(root, sub);
                if (!Directory.Exists(dir))
                    continue;
                foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                    files.Add(f.Substring(root.Length + 1).Replace('\\', '/'));
            }

            var index = ClipCoverageReport.IndexFolders(files);
            var pairs = new List<KeyValuePair<string, string>>();
            var table = new StringBuilder();
            table.AppendLine("weaponId | bang | mech | thump | tail | distant | supp | kaynak");
            foreach (var def in WeaponCatalog.All)
            {
                if (def == null || string.IsNullOrEmpty(def.WeaponId))
                    continue;
                var cal = WeaponSoundProfiles.Get(def.WeaponId, def.Category).CaliberFolder;
                pairs.Add(new KeyValuePair<string, string>(def.WeaponId, cal));
                table.AppendLine(ClipCoverageReport.FormatRow(index, def.WeaponId, cal));
            }

            var empty = ClipCoverageReport.EmptyLayers(index, pairs);
            var amb = new StringBuilder();
            foreach (var kv in index)
                if (kv.Key.StartsWith("Audio/Ambience/", System.StringComparison.Ordinal))
                    amb.AppendLine("- " + kv.Key.Substring("Audio/Ambience/".Length) + ": " + string.Join(", ", kv.Value));

            var sb = new StringBuilder();
            sb.AppendLine("# SES KAPSAMI (otomatik: HAREKAT/Ses/Kapsam Denetimi)");
            sb.AppendLine();
            sb.AppendLine("Silah katmanı: silah = Weapons/<id>, sınıf = Weapons/_class/<kalibre>, - = prosedürel yedek.");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.Append(table);
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine(empty.Count == 0
                ? "Boş kategori yok."
                : "UYARI: hiçbir silahta gerçek kaydı olmayan katmanlar: " + string.Join(", ", empty));
            sb.AppendLine();
            sb.AppendLine("## Ortam (Ambience)");
            sb.Append(amb.Length == 0 ? "Klip yok.\n" : amb.ToString());
            var text = sb.ToString();

            if (writeDoc)
            {
                var docs = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Docs", "SES_KAPSAM.md"));
                Directory.CreateDirectory(Path.GetDirectoryName(docs));
                File.WriteAllText(docs, text);
            }

            return text;
        }
    }
}
