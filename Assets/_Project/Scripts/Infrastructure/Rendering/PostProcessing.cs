using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// URP post-processing ve kalite ayarları. EnsureGlobalVolume sahnede tek bir global Volume kurar; profili çalışma
    /// zamanında üretilir (varlık gerekmez): ACES ton eşleme, Bloom, hafif doygunluğu düşük "askerî" renk derecelendirmesi,
    /// vinyet. Menü görünümü daha sıcak ve sinematiktir. ApplyQuality(0..3) kalite seviyesini, gölge mesafesini, kenar
    /// yumuşatmayı ve efekt kalitesini ayarlar. URP etkin değilse hacim yine kurulur ama etkisizdir (hata vermez).
    /// </summary>
    public static class PostProcessing
    {
        public enum Look { Gameplay, Menu }

        public const int MinQuality = 0;
        public const int MaxQuality = 3;

        /// <summary>Kalite seviyesine göre gölge mesafesi (m): Düşük, Orta, Yüksek, Ultra.</summary>
        private static readonly float[] ShadowDistances = { 60f, 110f, 170f, 260f };
        private static readonly int[] ShadowCascades = { 1, 2, 3, 4 };
        private static readonly int[] ShadowResolutions = { 1024, 2048, 2048, 4096 };
        private static readonly float[] LodBiases = { 0.7f, 1f, 1.4f, 2f };

        private static RuntimeGlobalVolume _current;
        private static int _qualityLevel = 2;
        private static bool _qualityApplied;

        // Geri bildirim (hasar / düşük can) — taban değerlere eklenir.
        private static float _damageFeedback;
        private static float _lowHealthFeedback;

#if UNITY_EDITOR
        // Editörde oynatma sırasında proje ayarı / varlık değişiklikleri diske kalıcı yazılabilir: ilk değişiklikten
        // önce değerler saklanır, oynatmadan çıkarken (Application.quitting) geri yüklenir.
        private sealed class AssetSnapshot
        {
            public UniversalRenderPipelineAsset Asset;
            public float ShadowDistance;
            public int Cascades;
            public int ShadowResolution;
        }

        private sealed class QualitySnapshot
        {
            public int Level;
            public float ShadowDistance;
            public float LodBias;
            public AnisotropicFiltering Anisotropic;
        }

        private static readonly List<AssetSnapshot> AssetSnapshots = new List<AssetSnapshot>();
        private static readonly List<QualitySnapshot> QualitySnapshots = new List<QualitySnapshot>();
        private static int _originalQualityLevel = -1;
        private static bool _restoreHooked;
#endif

        /// <summary>Sahnedeki etkin global hacim (yoksa null).</summary>
        public static RuntimeGlobalVolume Current => _current != null ? _current : null;

        /// <summary>Son uygulanan kalite seviyesi (0..3).</summary>
        public static int QualityLevel => _qualityLevel;

        /// <summary>
        /// Sahnede global post-processing hacmi olmasını sağlar ve görünümü uygular. Varsa yeniden kullanır (görünüm
        /// farklıysa günceller). Hacim sahneye aittir: sahne kapanınca profiliyle birlikte yok edilir.
        /// </summary>
        public static GameObject EnsureGlobalVolume(Look look)
        {
            var current = Current;
            if (current == null)
            {
                // Sahneye kaydedilmiş (editör kurulumu) bir hacim varsa onu sahiplen.
                current = UnityEngine.Object.FindAnyObjectByType<RuntimeGlobalVolume>();
            }

            if (current != null)
            {
                _current = current;
                if (current.Look != look || current.OwnedProfile == null || current.Volume == null || current.Volume.sharedProfile == null)
                {
                    current.Look = look;
                    Rebuild(current);
                }

                return current.gameObject;
            }

            // Pasif oluştur: RuntimeGlobalVolume.Awake profil hazırken çalışsın (çift kurulum olmasın).
            var go = new GameObject("HK_PostProcessVolume");
            go.SetActive(false);
            go.layer = GameLayers.Default;

            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;

            var owner = go.AddComponent<RuntimeGlobalVolume>();
            owner.Look = look;
            Rebuild(owner);

            go.SetActive(true);
            return go;
        }

        /// <summary>
        /// Kalite seviyesi (0 Düşük … 3 Ultra): varsa QualitySettings seviyesini seçer, gölge mesafesi/kademeleri,
        /// kenar yumuşatma (FXAA/SMAA) ve post-processing ayrıntısını ayarlar. Tekrar çağrılabilir.
        /// </summary>
        public static void ApplyQuality(int level)
        {
            level = Mathf.Clamp(level, MinQuality, MaxQuality);
            _qualityLevel = level;
            _qualityApplied = true;

            // 1) Unity kalite seviyesi (editör kurulumu 4 seviye üretir; farklı sayıda ise orantılı eşle).
            var pipelineBefore = GraphicsSettings.currentRenderPipeline;
            RememberOriginalQualityLevel();
            try
            {
                var names = QualitySettings.names;
                var count = names != null ? names.Length : 0;
                if (count > 1)
                {
                    var index = count == MaxQuality + 1
                        ? level
                        : Mathf.Clamp(Mathf.RoundToInt(level / (float)MaxQuality * (count - 1)), 0, count - 1);
                    if (QualitySettings.GetQualityLevel() != index)
                        QualitySettings.SetQualityLevel(index, true);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PostProcessing] Kalite seviyesi uygulanamadı: " + e.Message);
            }

            // 2) Gölgeler ve ayrıntı.
            var shadowDistance = ShadowDistanceFor(level);
            RememberQualityLevelValues();
            QualitySettings.shadowDistance = shadowDistance; // yerleşik hat
            QualitySettings.lodBias = LodBiases[level];
            QualitySettings.anisotropicFiltering = level >= 2 ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Enable;

            var urp = RenderPipelineInfo.UrpAsset;
            if (urp != null)
            {
                RememberAssetDefaults(urp);
                urp.shadowDistance = shadowDistance;
                try
                {
                    urp.shadowCascadeCount = ShadowCascades[level];
                }
                catch (ArgumentException)
                {
                    // Geçersiz kademe sayısı — varlık değeri korunur.
                }

                urp.mainLightShadowmapResolution = ShadowResolutions[level];
            }

            // 3) Kenar yumuşatma: Düşük/Orta FXAA, Yüksek/Ultra SMAA.
            CameraRig.SetAntialiasingLevel(level <= 1 ? 1 : level);

            // Kalite değişimi render hattını değiştirdiyse kamera yığınlarını yeniden kur.
            if (!ReferenceEquals(pipelineBefore, GraphicsSettings.currentRenderPipeline))
                CameraRig.RefreshAll();

            // 4) Post-processing ayrıntısı.
            var current = Current;
            if (current != null && current.Volume != null && current.Volume.sharedProfile != null)
                Configure(current.Volume.sharedProfile, current.Look, level);
        }

        /// <summary>Kalite seviyesine karşılık gelen gölge mesafesi (m).</summary>
        public static float ShadowDistanceFor(int level) => ShadowDistances[Mathf.Clamp(level, MinQuality, MaxQuality)];

        /// <summary>
        /// Savaş geri bildirimi: damage01 (son hasarın şiddeti, zamanla sönümlendirmek çağıranın işidir) kırmızımsı
        /// vinyet; lowHealth01 (1 = ölmek üzere) doygunluk kaybı + koyu vinyet. Her karede çağrılabilir (bellek ayırmaz).
        /// </summary>
        public static void SetCombatFeedback(float damage01, float lowHealth01)
        {
            damage01 = Mathf.Clamp01(damage01);
            lowHealth01 = Mathf.Clamp01(lowHealth01);
            if (Mathf.Approximately(damage01, _damageFeedback) && Mathf.Approximately(lowHealth01, _lowHealthFeedback))
                return;

            _damageFeedback = damage01;
            _lowHealthFeedback = lowHealth01;

            var current = Current;
            if (current == null || current.Volume == null)
                return;

            var profile = current.Volume.sharedProfile;
            if (profile == null)
                return;

            ApplyFeedback(profile, current.Look);
        }

        /// <summary>Hasar/düşük can geri bildirimini sıfırlar (ölüm ekranı, sahne geçişi).</summary>
        public static void ClearCombatFeedback() => SetCombatFeedback(0f, 0f);

        /// <summary>
        /// Görünüm için YENİ bir profil üretir (çağıran yok etmekten sorumludur; editör kurulumu varlık olarak
        /// kaydedebilir — o durumda bileşenleri profile alt varlık olarak ekleyin).
        /// </summary>
        public static VolumeProfile CreateProfile(Look look, int qualityLevel)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "HK_PostProfile_" + look;
            Configure(profile, look, Mathf.Clamp(qualityLevel, MinQuality, MaxQuality));
            return profile;
        }

        /// <summary>Var olan bir profili görünüm/kalite değerleriyle doldurur (eksik bileşenleri ekler).</summary>
        public static void Configure(VolumeProfile profile, Look look, int qualityLevel)
        {
            if (profile == null)
                return;

            qualityLevel = Mathf.Clamp(qualityLevel, MinQuality, MaxQuality);
            var menu = look == Look.Menu;

            // Ton eşleme — ACES (film benzeri, parlak namlu alevlerini yumuşak sıkıştırır).
            var tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);

            // Bloom — yalnız HDR parlaklar (namlu alevi, iz mermisi, ateş). Düşük kalitede kapalı.
            var bloom = GetOrAdd<Bloom>(profile);
            bloom.active = qualityLevel > 0;
            bloom.threshold.Override(menu ? 0.85f : 1.05f);
            bloom.intensity.Override(menu ? 0.85f : (qualityLevel >= 3 ? 0.6f : 0.5f));
            bloom.scatter.Override(menu ? 0.72f : 0.62f);
            bloom.tint.Override(menu ? new Color(1f, 0.88f, 0.74f) : Color.white);

            // Renk ayarları — oyun: hafif doygunluğu düşük, hafif zeytin tonlu askerî derecelendirme;
            // menü: sıcak gün batımı filtresi, daha yüksek kontrast.
            var colorAdjustments = GetOrAdd<ColorAdjustments>(profile);
            colorAdjustments.active = true;
            colorAdjustments.postExposure.Override(menu ? 0.22f : 0.12f);
            colorAdjustments.contrast.Override(menu ? 16f : 12f);
            colorAdjustments.colorFilter.Override(menu ? new Color(1f, 0.9f, 0.78f) : new Color(0.97f, 0.98f, 0.93f));
            colorAdjustments.hueShift.Override(0f);
            colorAdjustments.saturation.Override(BaseSaturation(look));

            // Vinyet (yoğunluk ve renk ApplyFeedback'te: taban + hasar/düşük can).
            var vignette = GetOrAdd<Vignette>(profile);
            vignette.active = true;
            vignette.smoothness.Override(menu ? 0.45f : 0.4f);

            ApplyFeedback(profile, look);
        }

        // ---------------------------------------------------------------- RuntimeGlobalVolume hooks

        /// <summary>Sahibin profilini (yeniden) üretip hacme bağlar ve onu güncel hacim yapar.</summary>
        internal static void Rebuild(RuntimeGlobalVolume owner)
        {
            if (owner == null)
                return;

            var volume = owner.GetComponent<Volume>();
            if (volume == null)
                return;

            volume.isGlobal = true;

            var profile = owner.OwnedProfile;
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.hideFlags = HideFlags.DontSave;
            }

            profile.name = "HK_PostProfile_" + owner.Look;
            Configure(profile, owner.Look, _qualityApplied ? _qualityLevel : DefaultQuality());
            MarkDontSave(profile);

            owner.SetOwnedProfile(profile);
            volume.sharedProfile = profile;
            _current = owner;
        }

        internal static void ClearCurrent()
        {
            _current = null;
        }

        // ---------------------------------------------------------------- Helpers

        private static float BaseSaturation(Look look) => look == Look.Menu ? -6f : -18f;

        private static float BaseVignette(Look look) => look == Look.Menu ? 0.36f : 0.26f;

        private static void ApplyFeedback(VolumeProfile profile, Look look)
        {
            if (profile.TryGet<Vignette>(out var vignette))
            {
                var intensity = BaseVignette(look) + _damageFeedback * 0.22f + _lowHealthFeedback * 0.18f;
                vignette.intensity.Override(Mathf.Clamp01(intensity));
                var tint = Color.Lerp(Color.black, new Color(0.45f, 0f, 0f), Mathf.Max(_damageFeedback, _lowHealthFeedback * 0.6f));
                vignette.color.Override(tint);
            }

            if (profile.TryGet<ColorAdjustments>(out var colorAdjustments))
                colorAdjustments.saturation.Override(Mathf.Clamp(BaseSaturation(look) - _lowHealthFeedback * 45f, -100f, 100f));
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var component) && component != null)
                return component;

            component = profile.Add<T>(false);
            return component;
        }

        private static void MarkDontSave(VolumeProfile profile)
        {
            if (profile.components == null)
                return;

            for (var i = 0; i < profile.components.Count; i++)
            {
                var component = profile.components[i];
                if (component != null)
                    component.hideFlags = HideFlags.DontSave;
            }
        }

        /// <summary>Kalite henüz uygulanmadıysa: mevcut Unity kalite seviyesinden tahmin (yoksa Yüksek).</summary>
        private static int DefaultQuality()
        {
            var names = QualitySettings.names;
            var count = names != null ? names.Length : 0;
            if (count <= 1)
                return 2;

            var index = QualitySettings.GetQualityLevel();
            return Mathf.Clamp(Mathf.RoundToInt(index / (float)(count - 1) * MaxQuality), MinQuality, MaxQuality);
        }

        private static void RememberAssetDefaults(UniversalRenderPipelineAsset asset)
        {
#if UNITY_EDITOR
            if (asset == null || !UnityEngine.Application.isPlaying)
                return;

            for (var i = 0; i < AssetSnapshots.Count; i++)
            {
                if (ReferenceEquals(AssetSnapshots[i].Asset, asset))
                    return;
            }

            AssetSnapshots.Add(new AssetSnapshot
            {
                Asset = asset,
                ShadowDistance = asset.shadowDistance,
                Cascades = asset.shadowCascadeCount,
                ShadowResolution = asset.mainLightShadowmapResolution
            });
            HookRestore();
#endif
        }

        private static void RememberOriginalQualityLevel()
        {
#if UNITY_EDITOR
            if (_originalQualityLevel >= 0 || !UnityEngine.Application.isPlaying)
                return;

            _originalQualityLevel = QualitySettings.GetQualityLevel();
            HookRestore();
#endif
        }

        private static void RememberQualityLevelValues()
        {
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
                return;

            var level = QualitySettings.GetQualityLevel();
            for (var i = 0; i < QualitySnapshots.Count; i++)
            {
                if (QualitySnapshots[i].Level == level)
                    return;
            }

            QualitySnapshots.Add(new QualitySnapshot
            {
                Level = level,
                ShadowDistance = QualitySettings.shadowDistance,
                LodBias = QualitySettings.lodBias,
                Anisotropic = QualitySettings.anisotropicFiltering
            });
            HookRestore();
#endif
        }

#if UNITY_EDITOR
        private static void HookRestore()
        {
            if (_restoreHooked)
                return;

            _restoreHooked = true;
            UnityEngine.Application.quitting += RestoreEditorState;
        }

        private static void RestoreEditorState()
        {
            for (var i = 0; i < AssetSnapshots.Count; i++)
            {
                var snapshot = AssetSnapshots[i];
                if (snapshot.Asset == null)
                    continue;

                snapshot.Asset.shadowDistance = snapshot.ShadowDistance;
                snapshot.Asset.mainLightShadowmapResolution = snapshot.ShadowResolution;
                try
                {
                    snapshot.Asset.shadowCascadeCount = snapshot.Cascades;
                }
                catch (ArgumentException)
                {
                }
            }

            AssetSnapshots.Clear();

            try
            {
                for (var i = 0; i < QualitySnapshots.Count; i++)
                {
                    var snapshot = QualitySnapshots[i];
                    QualitySettings.SetQualityLevel(snapshot.Level, false);
                    QualitySettings.shadowDistance = snapshot.ShadowDistance;
                    QualitySettings.lodBias = snapshot.LodBias;
                    QualitySettings.anisotropicFiltering = snapshot.Anisotropic;
                }

                if (_originalQualityLevel >= 0 && QualitySettings.GetQualityLevel() != _originalQualityLevel)
                    QualitySettings.SetQualityLevel(_originalQualityLevel, false);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PostProcessing] Kalite ayarları geri yüklenemedi: " + e.Message);
            }

            QualitySnapshots.Clear();
            _originalQualityLevel = -1;
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _current = null;
            _qualityApplied = false;
            _qualityLevel = 2;
            _damageFeedback = 0f;
            _lowHealthFeedback = 0f;
#if UNITY_EDITOR
            // Önceki oturum geri yüklenmeden bittiyse (çıkış olayı kaçtıysa) şimdi geri yükle.
            if (AssetSnapshots.Count > 0 || QualitySnapshots.Count > 0 || _originalQualityLevel >= 0)
                RestoreEditorState();
#endif
        }
    }
}
