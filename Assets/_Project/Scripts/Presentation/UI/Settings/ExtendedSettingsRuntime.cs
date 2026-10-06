using Project.Application.Settings;
using Project.Core.Interfaces;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Persistence;
using UnityEngine;

namespace Project.Presentation.UI.Settings
{
    /// <summary>
    /// Ek ayarların çalışma zamanı köprüsü: açılışta kaydı yükler/yayınlar, odak dışı ses kısmayı uygular ve
    /// girdi/kamera sistemlerinin çağırabileceği saf-mantık kısayolları sunar.
    /// </summary>
    public static class ExtendedSettingsRuntime
    {
        private static readonly MouseDeltaProcessor Processor = new MouseDeltaProcessor();
        private static FocusDucker _ducker;
        private static bool _loaded;

        /// <summary>Kayıtlı ek ayarları bir kez yükleyip yayınlar (mağaza null ise PlayerPrefs mağazası).</summary>
        public static void EnsureLoaded(ISettingsStore store = null)
        {
            if (_loaded)
                return;
            _loaded = true;
            try
            {
                var settings = ExtendedSettingsStore.Load(store ?? new PlayerPrefsSettingsStore());
                ExtendedSettingsHub.Publish(settings);
                Apply(settings);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>Ayar uygulanınca: işlemciyi günceller, odak-dışı ses kısmayı kurar.</summary>
        public static void Apply(ExtendedSettings settings)
        {
            if (settings == null)
                return;
            Processor.Settings = settings.Mouse.Clone();
            Processor.Reset();
            if (!UnityEngine.Application.isPlaying)
                return;
            if (_ducker == null)
            {
                var go = new GameObject("[AyarOdakKisma]");
                Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideInHierarchy;
                _ducker = go.AddComponent<FocusDucker>();
            }
            _ducker.Factor = settings.Audio.UnfocusedVolume;
        }

        /// <summary>
        /// Fare deltasını ayarlara göre işler (ivme/yumuşatma/Y oranı). Varsayılan ayarlarda girdi aynen döner.
        /// ENTEGRASYON: UnityInputReader.cs içinde look okumasından sonra bu çağrı yapılmalı.
        /// </summary>
        public static Vector2 ProcessLook(Vector2 rawDelta, float dt)
        {
            Processor.Settings = ExtendedSettingsHub.Current.Mouse;
            Processor.Process(rawDelta.x, rawDelta.y, dt, out var x, out var y);
            return new Vector2(x, y);
        }

        /// <summary>ADS için nihai çarpan: kameranın ZoomSensitivityScaling alanıyla çifte sayılmaması için çağıran tarafı belirler.</summary>
        public static float AdsMultiplier(float baseAds, bool fovRelative, float hipFov, float zoom)
        {
            return ExtendedSettingsHub.ResolveAdsMultiplier(baseAds, fovRelative, hipFov, zoom);
        }

        /// <summary>Verilen dikey dünya FOV'si ve zoom için ADS FOV'si (bağımsız ya da etkilenen kip).</summary>
        public static float AdsFov(float hipFov, float zoom)
        {
            var d = ExtendedSettingsHub.Current.Display;
            return FovMath.AdsFov(hipFov, zoom, d.AdsFovIndependent, d.AdsFovDegrees);
        }

        private sealed class FocusDucker : MonoBehaviour
        {
            public float Factor = 0.3f;

            private void OnApplicationFocus(bool focused)
            {
                try
                {
                    AudioListener.volume = focused ? GameAudio.MasterVolume : GameAudio.MasterVolume * Mathf.Clamp01(Factor);
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}
