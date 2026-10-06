using Project.Application.Credits;
using UnityEngine;

namespace Project.Infrastructure.Content
{
    /// <summary>Resources/Credits.json okuyucu. Dosya yoksa/bozuksa boş liste (çökmez).</summary>
    public static class CreditsLoader
    {
        public const string ResourcePath = "Credits";

        public static CreditsFile Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new CreditsFile();
            try
            {
                var f = JsonUtility.FromJson<CreditsFile>(json);
                if (f == null) return new CreditsFile();
                if (f.records == null) f.records = new LicenseRecord[0];
                return f;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Credits] JSON okunamadı: " + e.Message);
                return new CreditsFile();
            }
        }

        public static CreditsFile Load()
        {
            var ta = Resources.Load<TextAsset>(ResourcePath);
            return ta == null ? new CreditsFile() : Parse(ta.text);
        }
    }
}
