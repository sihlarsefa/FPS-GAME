using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Project.Application.Credits;
using Project.Infrastructure.Content;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>Ship öncesi lisans denetimi + C7 doğrulayıcı kancası (kayıtsız ThirdParty klasörü = HATA).</summary>
    public static class CreditsAudit
    {
        private const string ReportPath = "Library/CreditsAuditReport.txt";
        private const string ThirdPartyRoot = "Assets/ThirdParty";

        [MenuItem("HAREKÂT/İçerik/Lisans Denetimi (Credits)", priority = 52)]
        public static void AuditMenu()
        {
            var report = Run(out var errors);
            try { File.WriteAllText(ReportPath, report); } catch (Exception) { /* yoksay */ }
            if (errors > 0) Debug.LogError("[Credits] Denetim " + errors + " HATA:\n" + report);
            else Debug.Log("[Credits] Denetim temiz:\n" + report);
        }

        public static string Run(out int errors)
        {
            var file = CreditsLoader.Load();
            var findings = CreditsLogic.Audit(file);
            var sb = new StringBuilder();
            sb.AppendLine("HAREKÂT Lisans Denetimi - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " (" + file.records.Length + " kayıt)");
            foreach (var f in findings) sb.AppendLine(f.ToString());
            errors = CreditsLogic.CountErrors(findings);
            foreach (var m in UnregisteredFolders(file))
            {
                sb.AppendLine("[HATA] " + m + " - ThirdParty klasörü Credits.json'da kayıtsız");
                errors++;
            }
            return sb.ToString();
        }

        /// <summary>C7 doğrulayıcı için tek satır özet; HATA sayısını döndürür.</summary>
        public static int AppendTo(StringBuilder sb)
        {
            var r = Run(out var errors);
            sb.AppendLine("--- Lisans denetimi ---");
            sb.Append(r);
            return errors;
        }

        private static List<string> UnregisteredFolders(CreditsFile file)
        {
            var dirs = new List<string>();
            try
            {
                if (Directory.Exists(ThirdPartyRoot))
                    foreach (var d in Directory.GetDirectories(ThirdPartyRoot))
                        if (Directory.GetFileSystemEntries(d).Length > 0) dirs.Add(d.Replace('\\', '/'));
            }
            catch (Exception) { /* yoksay */ }
            return CreditsLogic.FindUnregisteredFolders(dirs, file);
        }
    }
}
