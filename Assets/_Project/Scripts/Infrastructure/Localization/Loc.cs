using System;
using System.Collections.Generic;
using Project.Application.Localization;
using UnityEngine;

namespace Project.Infrastructure.Localization
{
    /// <summary>
    /// Çalışma zamanı yerelleştirme. Resources/Localization/strings_&lt;dil&gt;.json (+ ui_&lt;dil&gt;.json eki) tembel yüklenir.
    /// Eksik anahtar bir kez loglanır ve verilen Türkçe yedek metin döner.
    /// </summary>
    public static class Loc
    {
        private static LocalizationTable _table;
        private static string _language = LocalizationTable.DefaultLanguage;
        private static readonly HashSet<string> Missing = new();

        public static string Language => _language;

        /// <summary>Dil değiştiğinde (yeni kod) tetiklenir.</summary>
        public static event Action<string> LanguageChanged;

        public static bool IsRtl => _language == "ar";

        public static void SetLanguage(string code)
        {
            var n = LocalizationTable.NormalizeLanguage(code);
            if (n == _language && _table != null)
                return;
            _language = n;
            _table = null;
            Missing.Clear();
            try { LanguageChanged?.Invoke(_language); }
            catch (Exception e) { Debug.LogException(e); }
        }

        public static string Get(string key, string fallback = null)
        {
            EnsureLoaded();
            if (_table != null && _table.TryGet(key, out var v) && !string.IsNullOrEmpty(v))
                return v;
            if (Missing.Add(key ?? string.Empty))
                Debug.LogWarning("[Loc] Eksik anahtar (" + _language + "): " + key);
            return fallback ?? key ?? string.Empty;
        }

        /// <summary>Sessiz arama (eksik anahtar loglamaz).</summary>
        public static bool TryGet(string key, out string value)
        {
            EnsureLoaded();
            value = null;
            return _table != null && _table.TryGet(key, out value) && !string.IsNullOrEmpty(value);
        }

        /// <summary>NameProfile görünen adlarını name.&lt;id&gt; / name.k.&lt;id&gt; anahtarlarına bağlar; anahtar yoksa yerleşik tablo kullanılır.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InstallNameResolver()
        {
            try
            {
                Project.Application.Catalogs.NameProfile.Resolver = key => TryGet(key, out var v) ? v : null;
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        public static string Format(string key, string fallback, params object[] args)
        {
            var t = Get(key, fallback);
            try { return string.Format(t, args); }
            catch (FormatException) { return t; }
        }

        private static void EnsureLoaded()
        {
            if (_table != null)
                return;
            _table = new LocalizationTable();
            try
            {
                Load("Localization/strings_" + _language);
                Load("Localization/ui_" + _language);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        private static void Load(string path)
        {
            var asset = Resources.Load<TextAsset>(path);
            if (asset != null && !_table.Merge(asset.text))
                Debug.LogWarning("[Loc] Bozuk JSON: " + path);
        }
    }
}
