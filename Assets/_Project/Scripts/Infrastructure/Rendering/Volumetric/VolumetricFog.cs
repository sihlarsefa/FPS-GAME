using System;
using System.Collections;
using System.Reflection;
using Project.Core.Domain;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Hacimsel sis + ışık huzmesi (god ray) giriş noktası. Çeyrek çözünürlük ışın yürütme, güneş gölge haritası örnekleme,
    /// mavi gürültü + zamansal karışım, çift yönlü üst örnekleme (bkz. VolumetricFogFeature / Shaders/Volumetric).
    /// Altın kural: shader/URP özelliği yoksa tek uyarı + sessizce kapalı; oyun bozulmaz.
    /// </summary>
    public static class VolumetricFog
    {
        /// <summary>Canlı ayarlar (render bunu okur). Doğrudan değiştirilebilir; ön ayar için ApplyPreset.</summary>
        public static VolumetricFogSettings Settings { get; } = new VolumetricFogSettings();

        /// <summary>Kalite kademesi: 0 Düşük (kapalı), 1 Orta, 2 Yüksek, 3 Ultra.</summary>
        public static int Tier { get; private set; }
        public static bool UserEnabled { get; private set; } = true;
        public static VolumetricTierConfig TierConfig => VolumetricFogMath.ForTier(Tier, UserEnabled);

        /// <summary>0 kapalı, 1 yalnız ham ışın yürütme (zamansal yok), 2 yalnız gölge terimi (hata ayıklama).</summary>
        public static int DebugMode { get; set; }

        public static string CurrentPresetLabel { get; private set; } = "Gündüz / Açık";
        public static bool Installed { get; private set; }

        /// <summary>Hacimsel sis şu an çiziliyor mu (kademe + kullanıcı + ayar + yoğunluk).</summary>
        public static bool IsActive => TierConfig.Enabled && Settings.Enabled && Settings.Density > 0f;

        /// <summary>
        /// Yerleşik (RenderSettings) sis yoğunluğuna uygulanacak çarpan: hacimsel sis açıkken çifte sis olmaması için düşürülür.
        /// </summary>
        public static float BuiltInFogScale => IsActive ? 0.35f : 1f;

        private static bool _warnedPipeline;
        private static bool _quitHooked;
        private static VolumetricFogParams _from, _to;
        private static float _blendT = 1f, _blendDur;
        private static int _lastTickFrame = -1;

        /// <summary>
        /// Kademeyi ayarlar ve renderer özelliğini etkin URP renderer'ına ekler (yinelenmez). true = özellik kayıtlı.
        /// QualityTierApplier çağırır.
        /// </summary>
        public static bool Install(int tier, bool userEnabled = true)
        {
            Tier = Mathf.Clamp(tier, 0, PipelineTiers.Count - 1);
            UserEnabled = userEnabled;
            Installed = TierConfig.Enabled && RegisterFeature();
            if (!TierConfig.Enabled)
                SetFeatureActive(false);
            return Installed;
        }

        /// <summary>Kamera ile kurulum: yalnız temel (Base) kamerada anlamlıdır; kameraya derinlik dokusu gereksinimi pass'ten istenir.</summary>
        public static bool Install(Camera camera, int tier, bool userEnabled = true)
        {
            if (camera == null)
                return Install(tier, userEnabled);
            return Install(tier, userEnabled);
        }

        public static void SetUserEnabled(bool enabled) => Install(Tier, enabled);

        /// <summary>Hava + gün saati ön ayarı uygular. blendSeconds &gt; 0 ise yumuşak geçiş (Tick ile).</summary>
        public static void ApplyPreset(TimeOfDay time, VolumetricWeather weather, float blendSeconds = 0f)
        {
            var target = VolumetricFogPresets.Get(time, weather);
            CurrentPresetLabel = AtmosphereRules.Name(time) + " / " + VolumetricFogPresets.Name(weather);
            if (blendSeconds <= 0.01f)
            {
                _blendT = 1f;
                Settings.Apply(target);
                return;
            }
            _from = Settings.ToParams();
            _to = target;
            _blendT = 0f;
            _blendDur = blendSeconds;
        }

        /// <summary>Oyunun WeatherKind değerinden ön ayar (Kar = Sisli).</summary>
        public static void ApplyPreset(TimeOfDay time, WeatherKind weather, float blendSeconds = 0f)
            => ApplyPreset(time, VolumetricFogPresets.FromWeather(weather), blendSeconds);

        /// <summary>Geçişi ilerletir (kare başına bir kez; özellik her kamerada çağırır, ikinci çağrı yok sayılır).</summary>
        public static void Tick()
        {
            var f = Time.frameCount;
            if (f == _lastTickFrame)
                return;
            _lastTickFrame = f;
            if (_blendT >= 1f)
                return;
            _blendT = Mathf.Min(1f, _blendT + Time.unscaledDeltaTime / Mathf.Max(0.01f, _blendDur));
            var smooth = _blendT * _blendT * (3f - 2f * _blendT);
            Settings.Apply(VolumetricFogParams.Lerp(_from, _to, smooth));
        }

        public static void Uninstall()
        {
            Installed = false;
            try
            {
                var lists = RendererFeatureLists();
                foreach (var features in lists)
                {
                    for (var i = features.Count - 1; i >= 0; i--)
                        if (features[i] is VolumetricFogFeature f && f.hideFlags.HasFlag(HideFlags.DontSave))
                        {
                            features.RemoveAt(i);
                            UnityEngine.Object.Destroy(f);
                        }
                }
                MarkDirty();
            }
            catch (Exception) { /* sessizce */ }
        }

        private static bool RegisterFeature()
        {
            try
            {
                var any = false;
                var lists = RendererFeatureLists();
                if (lists.Count == 0)
                {
                    Warn("URP renderer listesi bulunamadı");
                    return false;
                }
                var added = false;
                foreach (var features in lists)
                {
                    var found = false;
                    foreach (var f in features)
                        if (f is VolumetricFogFeature) { found = true; f.SetActive(true); break; }
                    if (!found)
                    {
                        var feature = ScriptableObject.CreateInstance<VolumetricFogFeature>();
                        feature.name = "HAREKAT Volumetric Fog (runtime)";
                        feature.hideFlags = HideFlags.DontSave; // varlığa kaydedilmez
                        features.Add(feature);
                        added = true;
                    }
                    any = true;
                }
                if (added)
                    MarkDirty();
                HookQuit();
                return any;
            }
            catch (Exception e)
            {
                Warn("kayıt başarısız: " + e.Message);
                return false;
            }
        }

        private static void SetFeatureActive(bool on)
        {
            try
            {
                foreach (var features in RendererFeatureLists())
                    foreach (var f in features)
                        if (f is VolumetricFogFeature) f.SetActive(on);
            }
            catch (Exception) { /* sessizce */ }
        }

        private static System.Collections.Generic.List<System.Collections.Generic.IList<ScriptableRendererFeature>> RendererFeatureLists()
        {
            var result = new System.Collections.Generic.List<System.Collections.Generic.IList<ScriptableRendererFeature>>();
            var asset = GraphicsSettings.currentRenderPipeline;
            if (asset == null)
                return result;
            var data = asset.GetType().GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(asset) as IEnumerable;
            if (data == null)
                return result;
            foreach (var d in data)
            {
                if (d == null) continue;
                if (d.GetType().GetProperty("rendererFeatures")?.GetValue(d) is System.Collections.Generic.IList<ScriptableRendererFeature> list)
                    result.Add(list);
            }
            return result;
        }

        private static void MarkDirty()
        {
            var asset = GraphicsSettings.currentRenderPipeline;
            if (asset == null) return;
            var data = asset.GetType().GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(asset) as IEnumerable;
            if (data == null) return;
            foreach (var d in data)
                d?.GetType().GetMethod("SetDirty", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null)?.Invoke(d, null);
        }

        private static void HookQuit()
        {
            if (_quitHooked) return;
            _quitHooked = true;
            UnityEngine.Application.quitting += Uninstall;
        }

        internal static void Warn(string msg)
        {
            if (_warnedPipeline) return;
            _warnedPipeline = true;
            Debug.LogWarning("[HAREKAT] Hacimsel sis kapalı: " + msg);
        }
    }
}
