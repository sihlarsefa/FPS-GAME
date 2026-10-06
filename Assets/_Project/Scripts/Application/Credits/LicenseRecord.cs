using System;
using System.Collections.Generic;
using System.Text;

namespace Project.Application.Credits
{
    /// <summary>Tek bir üçüncü taraf varlığın lisans kaydı (Resources/Credits.json satırı).</summary>
    [Serializable]
    public sealed class LicenseRecord
    {
        public string id;
        public string name;
        public string author;
        public string source;
        public string license;      // CC0, CC-BY-4.0, CC-BY-NC-4.0, Editorial, Unity-Asset-Store, Mixamo, ...
        public string url;
        public string folder;       // Assets/ThirdParty/... yolu
        public bool commercial = true;
        public bool redistribution = true;
        public string notes;
    }

    /// <summary>Credits.json kök nesnesi.</summary>
    [Serializable]
    public sealed class CreditsFile
    {
        public int version = 1;
        public LicenseRecord[] records = new LicenseRecord[0];
    }

    public enum LicenseSeverity { Ok = 0, Uyari = 1, Hata = 2 }

    public sealed class LicenseFinding
    {
        public LicenseSeverity Severity;
        public string RecordId;
        public string Message;
        public override string ToString() => "[" + (Severity == LicenseSeverity.Hata ? "HATA" : Severity == LicenseSeverity.Uyari ? "UYARI" : "OK") + "] " + RecordId + " - " + Message;
    }

    /// <summary>Saf lisans mantığı: CC-BY kredi satırı, denetim, kayıtsız varlık kontrolü.</summary>
    public static class CreditsLogic
    {
        public static string Normalize(string license) =>
            string.IsNullOrWhiteSpace(license) ? "" : license.Trim().ToUpperInvariant().Replace(' ', '-').Replace('_', '-');

        public static bool IsAttributionRequired(string license)
        {
            var l = Normalize(license);
            return l.StartsWith("CC-BY") && !l.StartsWith("CC-BY-NC");
        }

        public static bool IsNonCommercial(string license) => Normalize(license).Contains("-NC");
        public static bool IsEditorial(string license) => Normalize(license).Contains("EDITORIAL");
        public static bool IsNoDerivatives(string license) => Normalize(license).Contains("-ND");

        /// <summary>Örn. "\"Kirpi\" - Ali Veli (CC-BY-4.0) https://..."; boşsa null.</summary>
        public static string BuildCreditLine(LicenseRecord r)
        {
            if (r == null || string.IsNullOrWhiteSpace(r.name)) return null;
            var sb = new StringBuilder();
            sb.Append('"').Append(r.name.Trim()).Append('"');
            if (!string.IsNullOrWhiteSpace(r.author)) sb.Append(" - ").Append(r.author.Trim());
            if (!string.IsNullOrWhiteSpace(r.license)) sb.Append(" (").Append(r.license.Trim()).Append(')');
            var link = !string.IsNullOrWhiteSpace(r.url) ? r.url : r.source;
            if (!string.IsNullOrWhiteSpace(link)) sb.Append(' ').Append(link.Trim());
            return sb.ToString();
        }

        /// <summary>Ekranda gösterilecek satırlar: CC-BY zorunlu olanlar + isteğe bağlı diğerleri, ada göre sıralı.</summary>
        public static List<string> BuildCreditLines(CreditsFile file, bool attributionOnly = false)
        {
            var lines = new List<string>();
            if (file?.records == null) return lines;
            foreach (var r in file.records)
            {
                if (r == null) continue;
                if (attributionOnly && !IsAttributionRequired(r.license)) continue;
                var line = BuildCreditLine(r);
                if (line != null) lines.Add(line);
            }
            lines.Sort(StringComparer.Ordinal);
            return lines;
        }

        /// <summary>Ship öncesi denetim: NC/Editorial/ND/ticari-olmayan/yeniden dağıtım yok/eksik alan.</summary>
        public static List<LicenseFinding> Audit(CreditsFile file)
        {
            var list = new List<LicenseFinding>();
            if (file?.records == null) return list;
            var seen = new HashSet<string>();
            foreach (var r in file.records)
            {
                if (r == null) continue;
                var id = string.IsNullOrEmpty(r.id) ? (r.name ?? "?") : r.id;
                void Add(LicenseSeverity s, string m) => list.Add(new LicenseFinding { Severity = s, RecordId = id, Message = m });
                if (!seen.Add(id)) Add(LicenseSeverity.Uyari, "yinelenen kayıt kimliği");
                if (string.IsNullOrWhiteSpace(r.license)) { Add(LicenseSeverity.Hata, "lisans boş"); continue; }
                if (IsNonCommercial(r.license)) Add(LicenseSeverity.Hata, "ticari olmayan lisans (NC): Steam'de kullanılamaz");
                if (IsEditorial(r.license)) Add(LicenseSeverity.Hata, "Editorial lisans: ticari oyunda kullanılamaz");
                if (!r.commercial) Add(LicenseSeverity.Hata, "ticari kullanım izni yok");
                if (!r.redistribution) Add(LicenseSeverity.Hata, "yeniden dağıtım izni yok");
                if (IsNoDerivatives(r.license)) Add(LicenseSeverity.Uyari, "ND lisans: değişiklik yasak olabilir");
                if (IsAttributionRequired(r.license) && string.IsNullOrWhiteSpace(r.author)) Add(LicenseSeverity.Hata, "CC-BY için yazar adı zorunlu");
                if (IsAttributionRequired(r.license) && string.IsNullOrWhiteSpace(r.url) && string.IsNullOrWhiteSpace(r.source)) Add(LicenseSeverity.Uyari, "kaynak bağlantısı yok");
            }
            return list;
        }

        public static int CountErrors(List<LicenseFinding> findings)
        {
            var n = 0;
            if (findings != null) foreach (var f in findings) if (f.Severity == LicenseSeverity.Hata) n++;
            return n;
        }

        /// <summary>Klasörü kayıtlı olmayan ThirdParty klasörlerini bulur (kayıt klasörü, alt yolu kapsar).</summary>
        public static List<string> FindUnregisteredFolders(IEnumerable<string> thirdPartyFolders, CreditsFile file)
        {
            var missing = new List<string>();
            if (thirdPartyFolders == null) return missing;
            foreach (var f in thirdPartyFolders)
            {
                if (string.IsNullOrEmpty(f)) continue;
                var p = Trim(f);
                var ok = false;
                if (file?.records != null)
                    foreach (var r in file.records)
                    {
                        if (r == null || string.IsNullOrEmpty(r.folder)) continue;
                        var rf = Trim(r.folder);
                        if (p == rf || p.StartsWith(rf + "/") || rf.StartsWith(p + "/")) { ok = true; break; }
                    }
                if (!ok) missing.Add(f);
            }
            return missing;
        }

        private static string Trim(string s) => s.Replace('\\', '/').TrimEnd('/');
    }
}
