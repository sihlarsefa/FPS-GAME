using System;
using UnityEngine;

namespace Project.Presentation.UI.Crosshair
{
    /// <summary>Nişangâh ADS (nişan alma) davranışı.</summary>
    public enum CrosshairAdsMode
    {
        /// <summary>ADS'de tamamen gizle (CoD/Battlefield varsayılanı).</summary>
        Gizle = 0,
        /// <summary>ADS'de soluk ve daha dar göster.</summary>
        Daralt = 1,
        /// <summary>ADS'de hiç değiştirme.</summary>
        Goster = 2
    }

    /// <summary>Renk körü kipi (isabet işareti paleti ve zorunlu kontur için).</summary>
    public enum CrosshairColorBlindMode
    {
        Kapali = 0,
        Deuteranopi = 1,
        Protanopi = 2,
        Tritanopi = 3,
        YuksekKontrast = 4
    }

    /// <summary>Anahtar/değer deposu soyutlaması (PlayerPrefs ya da testte bellek).</summary>
    public interface ICrosshairPrefsStore
    {
        bool HasKey(string key);
        float GetFloat(string key, float fallback);
        int GetInt(string key, int fallback);
        void SetFloat(string key, float value);
        void SetInt(string key, int value);
    }

    /// <summary>PlayerPrefs üzerinden depo.</summary>
    public sealed class PlayerPrefsCrosshairStore : ICrosshairPrefsStore
    {
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
    }

    /// <summary>
    /// Tüm nişangâh ve isabet işareti ayarları tek yerde. Değerler <see cref="Sanitize"/> ile sınırlanır;
    /// <see cref="Load"/>/<see cref="Save"/> PlayerPrefs'e ("Crosshair." öneki) yazar/okur.
    /// </summary>
    [Serializable]
    public sealed class CrosshairSettings
    {
        public const string KeyPrefix = "Crosshair.";
        public const int SchemaVersion = 1;

        // --- Nişangâh çizgileri ---
        public float LineLength = 11f;       // 2..40 px
        public float LineThickness = 2f;     // 1..8 px
        public float CenterGap = 0f;         // 0..30 px ek boşluk (hareketsiz taban)
        public float Opacity = 1f;           // 0.1..1
        public bool UseCustomColor;          // false: AdvancedDisplay.CrosshairTint
        public float ColorR = 1f, ColorG = 1f, ColorB = 1f;

        // --- Merkez nokta ---
        public bool CenterDot = true;
        public float DotSize = 2f;           // 1..8 px

        // --- Kontur ---
        public bool Outline = true;
        public float OutlineOpacity = 0.6f;  // 0..1
        public float OutlineThickness = 1f;  // 0..3 px

        // --- Dinamik açılma ---
        public bool DynamicSpread = true;
        public float SpreadMultiplier = 1f;  // 0.25..2
        public float ExpandRate = 28f;       // 4..60 /sn (açılma hızı)
        public float RecoverRate = 12f;      // 2..40 /sn (toparlanma hızı)

        // --- ADS ---
        public CrosshairAdsMode AdsMode = CrosshairAdsMode.Gizle;
        public float AdsOpacity = 0.35f;     // Daralt kipinde 0..1

        // --- İsabet işareti ---
        public bool HitMarkerEnabled = true;
        public float HitMarkerSize = 1f;       // 0.5..2
        public float HitMarkerOpacity = 1f;    // 0.2..1
        public float HitMarkerDuration = 1f;   // 0.5..2 (süre çarpanı)
        public bool HitMarkerShowArmor = true; // zırh emilimini ayrı renkle göster

        // --- Erişilebilirlik ---
        public CrosshairColorBlindMode ColorBlind = CrosshairColorBlindMode.Kapali;

        public Color CustomColor => new Color(ColorR, ColorG, ColorB, 1f);

        public CrosshairSettings Clone() => (CrosshairSettings)MemberwiseClone();

        public static CrosshairSettings Default() => new CrosshairSettings();

        /// <summary>Tüm alanları geçerli aralığa çeker (NaN/sonsuz değerler varsayılana döner).</summary>
        public CrosshairSettings Sanitize()
        {
            var d = new CrosshairSettings();
            LineLength = Clamp(LineLength, 2f, 40f, d.LineLength);
            LineThickness = Clamp(LineThickness, 1f, 8f, d.LineThickness);
            CenterGap = Clamp(CenterGap, 0f, 30f, d.CenterGap);
            Opacity = Clamp(Opacity, 0.1f, 1f, d.Opacity);
            ColorR = Clamp(ColorR, 0f, 1f, 1f);
            ColorG = Clamp(ColorG, 0f, 1f, 1f);
            ColorB = Clamp(ColorB, 0f, 1f, 1f);
            DotSize = Clamp(DotSize, 1f, 8f, d.DotSize);
            OutlineOpacity = Clamp(OutlineOpacity, 0f, 1f, d.OutlineOpacity);
            OutlineThickness = Clamp(OutlineThickness, 0f, 3f, d.OutlineThickness);
            SpreadMultiplier = Clamp(SpreadMultiplier, 0.25f, 2f, 1f);
            ExpandRate = Clamp(ExpandRate, 4f, 60f, d.ExpandRate);
            RecoverRate = Clamp(RecoverRate, 2f, 40f, d.RecoverRate);
            AdsOpacity = Clamp(AdsOpacity, 0f, 1f, d.AdsOpacity);
            HitMarkerSize = Clamp(HitMarkerSize, 0.5f, 2f, 1f);
            HitMarkerOpacity = Clamp(HitMarkerOpacity, 0.2f, 1f, 1f);
            HitMarkerDuration = Clamp(HitMarkerDuration, 0.5f, 2f, 1f);
            if (!Enum.IsDefined(typeof(CrosshairAdsMode), AdsMode))
                AdsMode = CrosshairAdsMode.Gizle;
            if (!Enum.IsDefined(typeof(CrosshairColorBlindMode), ColorBlind))
                ColorBlind = CrosshairColorBlindMode.Kapali;
            return this;
        }

        private static float Clamp(float v, float min, float max, float fallback)
        {
            if (float.IsNaN(v) || float.IsInfinity(v))
                return fallback;
            return v < min ? min : v > max ? max : v;
        }

        // ---------------- Kalıcılık ----------------

        private static CrosshairSettings _current;

        /// <summary>Oyun boyunca paylaşılan etkin ayar (ilk erişimde PlayerPrefs'ten yüklenir).</summary>
        public static CrosshairSettings Current
        {
            get
            {
                if (_current == null)
                    _current = SafeLoad(new PlayerPrefsCrosshairStore());
                return _current;
            }
        }

        /// <summary>Etkin ayarı değiştirir, sınırlar ve kalıcılaştırır.</summary>
        public static void Apply(CrosshairSettings settings, ICrosshairPrefsStore store = null)
        {
            _current = (settings ?? Default()).Clone().Sanitize();
            Save(_current, store ?? new PlayerPrefsCrosshairStore());
        }

        /// <summary>Önbelleği atar; sonraki erişimde yeniden yüklenir.</summary>
        public static void InvalidateCache() => _current = null;

        private static CrosshairSettings SafeLoad(ICrosshairPrefsStore store)
        {
            try { return Load(store); }
            catch (Exception) { return Default(); }
        }

        public static CrosshairSettings Load(ICrosshairPrefsStore store)
        {
            var s = Default();
            if (store == null || !store.HasKey(KeyPrefix + "Version"))
                return s;
            s.LineLength = store.GetFloat(KeyPrefix + "LineLength", s.LineLength);
            s.LineThickness = store.GetFloat(KeyPrefix + "LineThickness", s.LineThickness);
            s.CenterGap = store.GetFloat(KeyPrefix + "CenterGap", s.CenterGap);
            s.Opacity = store.GetFloat(KeyPrefix + "Opacity", s.Opacity);
            s.UseCustomColor = store.GetInt(KeyPrefix + "UseCustomColor", 0) != 0;
            s.ColorR = store.GetFloat(KeyPrefix + "ColorR", s.ColorR);
            s.ColorG = store.GetFloat(KeyPrefix + "ColorG", s.ColorG);
            s.ColorB = store.GetFloat(KeyPrefix + "ColorB", s.ColorB);
            s.CenterDot = store.GetInt(KeyPrefix + "CenterDot", 1) != 0;
            s.DotSize = store.GetFloat(KeyPrefix + "DotSize", s.DotSize);
            s.Outline = store.GetInt(KeyPrefix + "Outline", 1) != 0;
            s.OutlineOpacity = store.GetFloat(KeyPrefix + "OutlineOpacity", s.OutlineOpacity);
            s.OutlineThickness = store.GetFloat(KeyPrefix + "OutlineThickness", s.OutlineThickness);
            s.DynamicSpread = store.GetInt(KeyPrefix + "DynamicSpread", 1) != 0;
            s.SpreadMultiplier = store.GetFloat(KeyPrefix + "SpreadMultiplier", s.SpreadMultiplier);
            s.ExpandRate = store.GetFloat(KeyPrefix + "ExpandRate", s.ExpandRate);
            s.RecoverRate = store.GetFloat(KeyPrefix + "RecoverRate", s.RecoverRate);
            s.AdsMode = (CrosshairAdsMode)store.GetInt(KeyPrefix + "AdsMode", (int)s.AdsMode);
            s.AdsOpacity = store.GetFloat(KeyPrefix + "AdsOpacity", s.AdsOpacity);
            s.HitMarkerEnabled = store.GetInt(KeyPrefix + "HitMarkerEnabled", 1) != 0;
            s.HitMarkerSize = store.GetFloat(KeyPrefix + "HitMarkerSize", s.HitMarkerSize);
            s.HitMarkerOpacity = store.GetFloat(KeyPrefix + "HitMarkerOpacity", s.HitMarkerOpacity);
            s.HitMarkerDuration = store.GetFloat(KeyPrefix + "HitMarkerDuration", s.HitMarkerDuration);
            s.HitMarkerShowArmor = store.GetInt(KeyPrefix + "HitMarkerShowArmor", 1) != 0;
            s.ColorBlind = (CrosshairColorBlindMode)store.GetInt(KeyPrefix + "ColorBlind", 0);
            return s.Sanitize();
        }

        public static void Save(CrosshairSettings s, ICrosshairPrefsStore store)
        {
            if (s == null || store == null)
                return;
            store.SetInt(KeyPrefix + "Version", SchemaVersion);
            store.SetFloat(KeyPrefix + "LineLength", s.LineLength);
            store.SetFloat(KeyPrefix + "LineThickness", s.LineThickness);
            store.SetFloat(KeyPrefix + "CenterGap", s.CenterGap);
            store.SetFloat(KeyPrefix + "Opacity", s.Opacity);
            store.SetInt(KeyPrefix + "UseCustomColor", s.UseCustomColor ? 1 : 0);
            store.SetFloat(KeyPrefix + "ColorR", s.ColorR);
            store.SetFloat(KeyPrefix + "ColorG", s.ColorG);
            store.SetFloat(KeyPrefix + "ColorB", s.ColorB);
            store.SetInt(KeyPrefix + "CenterDot", s.CenterDot ? 1 : 0);
            store.SetFloat(KeyPrefix + "DotSize", s.DotSize);
            store.SetInt(KeyPrefix + "Outline", s.Outline ? 1 : 0);
            store.SetFloat(KeyPrefix + "OutlineOpacity", s.OutlineOpacity);
            store.SetFloat(KeyPrefix + "OutlineThickness", s.OutlineThickness);
            store.SetInt(KeyPrefix + "DynamicSpread", s.DynamicSpread ? 1 : 0);
            store.SetFloat(KeyPrefix + "SpreadMultiplier", s.SpreadMultiplier);
            store.SetFloat(KeyPrefix + "ExpandRate", s.ExpandRate);
            store.SetFloat(KeyPrefix + "RecoverRate", s.RecoverRate);
            store.SetInt(KeyPrefix + "AdsMode", (int)s.AdsMode);
            store.SetFloat(KeyPrefix + "AdsOpacity", s.AdsOpacity);
            store.SetInt(KeyPrefix + "HitMarkerEnabled", s.HitMarkerEnabled ? 1 : 0);
            store.SetFloat(KeyPrefix + "HitMarkerSize", s.HitMarkerSize);
            store.SetFloat(KeyPrefix + "HitMarkerOpacity", s.HitMarkerOpacity);
            store.SetFloat(KeyPrefix + "HitMarkerDuration", s.HitMarkerDuration);
            store.SetInt(KeyPrefix + "HitMarkerShowArmor", s.HitMarkerShowArmor ? 1 : 0);
            store.SetInt(KeyPrefix + "ColorBlind", (int)s.ColorBlind);
        }
    }
}
