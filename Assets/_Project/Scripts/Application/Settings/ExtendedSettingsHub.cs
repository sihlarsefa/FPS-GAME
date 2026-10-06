using System;

namespace Project.Application.Settings
{
    /// <summary>
    /// Çalışma zamanı paylaşımı: oyun sistemleri (girdi, kamera, ses, HUD) güncel ek ayarları buradan okur.
    /// Ayar paneli Apply'da <see cref="Publish"/> çağırır; tüketiciler <see cref="Changed"/> olayına abone olur.
    /// </summary>
    public static class ExtendedSettingsHub
    {
        private static ExtendedSettings _current;

        public static event Action<ExtendedSettings> Changed;

        /// <summary>Güncel ayarlar; henüz yüklenmediyse varsayılanlar (asla null).</summary>
        public static ExtendedSettings Current => _current ??= ExtendedSettingsStore.NewDefault();

        public static void Publish(ExtendedSettings settings)
        {
            if (settings == null)
                return;
            var clean = settings.Clone();
            clean.Sanitize();
            _current = clean;
            Changed?.Invoke(clean);
        }

        /// <summary>Test için: durumu sıfırlar.</summary>
        public static void ResetForTests()
        {
            _current = null;
            Changed = null;
        }

        /// <summary>Bu ADS durumu için nihai hassasiyet çarpanı (kısayol).</summary>
        public static float ResolveAdsMultiplier(float baseAds, bool fovRelative, float hipFov, float zoom)
        {
            return AdsSensitivityModel.Resolve(Current.Ads, baseAds, fovRelative, hipFov, zoom);
        }
    }
}
