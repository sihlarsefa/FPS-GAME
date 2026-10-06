using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Rendering.Grading;
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
        // Gölge mesafesi/kademe/çözünürlük TEK KAYNAK: PipelineTiers (50/100/180/300 m; cascade 1/2/4/4; 1024/2048/2048/4096).
        private static readonly float[] LodBiases = { 0.7f, 1f, 1.4f, 2f };

        private static RuntimeGlobalVolume _current;
        private static int _qualityLevel = 2;
        private static bool _qualityApplied;

        // Geri bildirim (hasar / düşük can) — taban değerlere eklenir.
        private static string _mapId = MapCatalog.Kuzgun;
        private static bool _motionBlur;
        private static float _adsBlur01;
        private static bool _deathBlur;
        private static float _deathTimer;

        // Post 2.0: derecelendirme geçişi (0,5 sn) ve oyuncu efekt ayarları.
        private static readonly ScreenEffectsMath.GradeBlender GradeBlend = new ScreenEffectsMath.GradeBlender();
        private static TimeOfDay _timeOfDay = TimeOfDay.Gunduz;
        // GradingPresets verisi (safak/gunduz/altin/mavi/gece/lobi + harita ofseti); gün saati geçişleri 2,5 sn yumuşak.
        private static readonly GradeSpecBlender SpecBlend = new GradeSpecBlender();
        private static bool _lobbyGrade;
        private static bool _effectPrefsLoaded;
        private static bool _dofEnabled = true;
        private static bool _grainEnabled = true;
        private const string PrefDof = "settings.dof";
        private const string PrefGrain = "settings.grain";

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
                // Gölge mesafesi/cascade/split'i PipelineTiers.ApplyRuntime yazar (çifte yazım yok); çözünürlük tablodan.
                urp.mainLightShadowmapResolution = PipelineTiers.Get(level).ShadowResolution;
            }

            // 2b) Katman kırpma mesafeleri + arazi ağaç/ayrıntı (PerformanceProfile; null güvenli).
            try
            {
                for (var i = 0; i < CameraRig.Active.Count; i++)
                {
                    var rig = CameraRig.Active[i];
                    if (rig != null)
                        PerformanceProfile.ApplyToCamera(rig.WorldCamera, level);
                }

                PerformanceProfile.ApplyToTerrains(level);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PostProcessing] Performans profili uygulanamadı: " + e.Message);
            }

            // 3) Kenar yumuşatma: Düşük/Orta FXAA, Yüksek/Ultra SMAA.
            CameraRig.SetAntialiasingLevel(level <= 1 ? 1 : level);

            // Kalite değişimi render hattını değiştirdiyse kamera yığınlarını yeniden kur.
            if (!ReferenceEquals(pipelineBefore, GraphicsSettings.currentRenderPipeline))
                CameraRig.RefreshAll();

            // 3b) SSAO kademesi (yansıma; yoksa sessiz).
            // Tek uygulayıcı: SSAO, renderer özellikleri, hacimsel sis, doku akışı, çimen/rüzgâr, VFX, arazi, yansıma.
            QualityTierApplier.Apply(level, ActiveWorldCamera());

            // 4) Post-processing ayrıntısı.
            var current = Current;
            if (current != null && current.Volume != null && current.Volume.sharedProfile != null)
                Configure(current.Volume.sharedProfile, current.Look, level);
        }

        private static Camera ActiveWorldCamera()
        {
            try
            {
                for (var i = 0; i < CameraRig.Active.Count; i++)
                    if (CameraRig.Active[i] != null && CameraRig.Active[i].WorldCamera != null)
                        return CameraRig.Active[i].WorldCamera;
            }
            catch (Exception) { }
            return Camera.main;
        }

        /// <summary>Kalite seviyesine karşılık gelen gölge mesafesi (m).</summary>
        public static float ShadowDistanceFor(int level) => PipelineTiers.Get(Mathf.Clamp(level, MinQuality, MaxQuality)).ShadowDistance;

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
            bloom.threshold.Override(menu ? 0.85f : 1.2f);
            bloom.intensity.Override(menu ? 0.85f : (_timeOfDay == TimeOfDay.Gece ? 0.5f : (qualityLevel >= 3 ? 0.35f : 0.30f)));
            bloom.scatter.Override(menu ? 0.72f : 0.62f);
            bloom.tint.Override(menu ? new Color(1f, 0.88f, 0.74f) : Color.white);

            // Renk ayarları — oyun: hafif doygunluğu düşük, hafif zeytin tonlu askerî derecelendirme;
            // menü: sıcak gün batımı filtresi, daha yüksek kontrast.
            GetOrAdd<ColorAdjustments>(profile).active = true;
            GetOrAdd<WhiteBalance>(profile);
            GetOrAdd<ShadowsMidtonesHighlights>(profile);
            EnsureGrade();
            ApplyGrade(profile, look);

            // Vinyet (yoğunluk ve renk ApplyFeedback'te: taban + hasar/düşük can).
            var vignette = GetOrAdd<Vignette>(profile);
            vignette.active = true;
            vignette.smoothness.Override(menu ? 0.45f : 0.4f);

            // Hafif lens kusurları: kromatik sapma + film greni (Düşük kalitede kapalı).
            var chroma = GetOrAdd<ChromaticAberration>(profile);
            chroma.active = qualityLevel > 0;
            chroma.intensity.Override(menu ? 0.05f : 0.02f);

            var grain = GetOrAdd<FilmGrain>(profile);
            LoadEffectPrefs();
            grain.active = qualityLevel > 0 && _grainEnabled;
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(menu ? 0.12f : 0.10f);
            grain.response.Override(0.8f);

            // Alan derinliği: ADS ve ölüm kamerasında SetAimBlur/SetDeathBlur ile açılır (Gaussian, yakın bulanıklık).
            var dof = GetOrAdd<DepthOfField>(profile);
            dof.mode.Override(DepthOfFieldMode.Off);
            dof.active = qualityLevel > 0 && _dofEnabled;
            dof.gaussianMaxRadius.Override(qualityLevel >= 3 ? 1.2f : 0.9f);
            dof.highQualitySampling.Override(qualityLevel >= 3);

            // Hareket bulanıklığı (ayar): yalnız Orta+ kalitede ve seçenek açıksa.
            var mb = GetOrAdd<MotionBlur>(profile);
            mb.active = _motionBlur && qualityLevel > 0 && !menu;
            mb.mode.Override(MotionBlurMode.CameraOnly);
            mb.quality.Override(qualityLevel >= 3 ? MotionBlurQuality.High : qualityLevel == 2 ? MotionBlurQuality.Medium : MotionBlurQuality.Low);
            mb.intensity.Override(0.25f);
            mb.clamp.Override(0.05f);

            ApplyFeedback(profile, look);
            ApplyDepthOfField(profile);
        }

        private static Vector4 ToVector(float[] v) => new Vector4(v[0], v[1], v[2], v[3]);

        /// <summary>Harita derecelendirmesini (Kuzgun sıcak-zeytin, Ayaz soğuk-mavi, Mavi Liman turkuaz) uygular.</summary>
        public static void ApplyMapGrade(string mapId)
        {
            _mapId = MapCatalog.Normalize(mapId);
            _timeOfDay = Atmosphere.CurrentTime;
            RetargetGrade();
        }

        /// <summary>Günün saatine göre derecelendirme preseti; 0,5 sn'de yumuşak geçer.</summary>
        public static void SetTimeOfDay(TimeOfDay time)
        {
            _timeOfDay = time;
            RetargetGrade();
        }

        /// <summary>Menü/lobi: "mavi saat + kırmızı vurgu" presetini açar/kapatır (MenuBackdrop çağırır).</summary>
        public static void SetLobbyGrade(bool on)
        {
            if (_lobbyGrade == on) return;
            _lobbyGrade = on;
            RetargetGrade();
        }

        private static GradeSpec SpecTarget() => _lobbyGrade
            ? GradingPresets.For(_mapId, GradeStage.Lobi)
            : GradingPresets.For(_mapId, _timeOfDay);

        private static void RetargetGrade()
        {
            SpecBlend.SetTarget(SpecTarget());
            GradeBlend.SetTarget(ScreenEffectsMath.BuildGrade(MapGradeTable.For(_mapId), _timeOfDay));
            // Profil hemen güncellenmez: Tick kare kare harmanlar; hacim yoksa/ilk kurulumda Configure anında uygular.
            var current = Current;
            if (current != null && current.Volume != null && current.Volume.sharedProfile != null && !GradeBlend.Blending && !SpecBlend.Blending)
                ApplyGrade(current.Volume.sharedProfile, current.Look);
        }

        private static void EnsureGrade()
        {
            if (!SpecBlend.HasTarget)
                SpecBlend.SetTarget(SpecTarget(), true);
            if (!GradeBlend.HasTarget)
                GradeBlend.SetTarget(ScreenEffectsMath.BuildGrade(MapGradeTable.For(_mapId), _timeOfDay), true);
        }

        /// <summary>Her karede RuntimeGlobalVolume tarafından çağrılır: grade harmanı ve ölüm DoF eğrisi.</summary>
        internal static void Tick(float unscaledDeltaTime)
        {
            var current = Current;
            if (current == null || current.Volume == null || current.Volume.sharedProfile == null)
                return;
            var profile = current.Volume.sharedProfile;
            var specMoved = SpecBlend.Advance(unscaledDeltaTime);
            if (GradeBlend.Advance(unscaledDeltaTime) || specMoved)
                ApplyGrade(profile, current.Look);
            if (_deathBlur)
            {
                _deathTimer += Mathf.Max(0f, unscaledDeltaTime);
                ApplyDepthOfField(profile);
            }
        }

        private static void ApplyGrade(VolumeProfile profile, Look look)
        {
            if (profile == null || !GradeBlend.HasTarget)
                return;
            var menu = look == Look.Menu;
            var g = GradeBlend.Current;
            var spec = SpecBlend.HasTarget ? SpecBlend.Current : GradeSpec.Neutral;
            // Menü: lobi presetı kapalıysa eski sıcak menü görünümü; açıksa preset (mavi saat + kırmızı vurgu) uygulanır.
            var legacyMenu = menu && !_lobbyGrade;

            if (profile.TryGet<ColorAdjustments>(out var ca))
            {
                ca.postExposure.Override(legacyMenu ? 0.22f : spec.PostExposure + g[ScreenEffectsMath.GradeExposure] - ScreenEffectsMath.TimeExposure(_timeOfDay));
                ca.contrast.Override(legacyMenu ? 16f : spec.Contrast);
                ca.colorFilter.Override(menu
                    ? (_lobbyGrade ? Color.white : new Color(1f, 0.9f, 0.78f))
                    : new Color(g[ScreenEffectsMath.GradeFilter], g[ScreenEffectsMath.GradeFilter + 1], g[ScreenEffectsMath.GradeFilter + 2]));
                ca.hueShift.Override(0f);
                ca.saturation.Override(Mathf.Clamp(BaseSaturation(look) - _lowHealthFeedback * 45f, -100f, 100f));
            }

            if (profile.TryGet<WhiteBalance>(out var wb) && legacyMenu)
            {
                wb.active = false; wb.temperature.Override(0f); wb.tint.Override(0f);
            }
            if (!legacyMenu)
                GradingWriter.Apply(profile, spec, true);

            if (profile.TryGet<ShadowsMidtonesHighlights>(out var smh))
            {
                smh.active = !legacyMenu;
                var neutral = new Vector4(1f, 1f, 1f, 0f);
                // Harita SMH'si (g) ile preset SMH'si çarpılır; menüde yalnız preset.
                smh.shadows.Override(legacyMenu ? neutral : MulVec(menu ? neutral : GradeVec(g, ScreenEffectsMath.GradeShadows), spec.Shadows));
                smh.midtones.Override(legacyMenu ? neutral : MulVec(menu ? neutral : GradeVec(g, ScreenEffectsMath.GradeMidtones), spec.Midtones));
                smh.highlights.Override(legacyMenu ? neutral : MulVec(menu ? neutral : GradeVec(g, ScreenEffectsMath.GradeHighlights), spec.Highlights));
            }
        }

        private static Vector4 MulVec(Vector4 a, Vector4 b) => new Vector4(a.x * b.x, a.y * b.y, a.z * b.z, Mathf.Clamp(a.w + b.w, -1f, 1f));

        private static Vector4 GradeVec(float[] g, int i) => new Vector4(g[i], g[i + 1], g[i + 2], g[i + 3]);

        // ---------------------------------------------------------------- Oyuncu efekt ayarları (DoF / film greni)

        public static bool DepthOfFieldEnabled { get { LoadEffectPrefs(); return _dofEnabled; } }
        public static bool FilmGrainEnabled { get { LoadEffectPrefs(); return _grainEnabled; } }

        /// <summary>Alan derinliği (ADS/ölüm bulanıklığı) ayarı; PlayerPrefs'te saklanır.</summary>
        public static void SetDepthOfFieldEnabled(bool enabled)
        {
            LoadEffectPrefs();
            _dofEnabled = enabled;
            SaveEffectPref(PrefDof, enabled);
            ReconfigureCurrent();
        }

        /// <summary>Film greni ayarı; PlayerPrefs'te saklanır.</summary>
        public static void SetFilmGrainEnabled(bool enabled)
        {
            LoadEffectPrefs();
            _grainEnabled = enabled;
            SaveEffectPref(PrefGrain, enabled);
            ReconfigureCurrent();
        }

        private static void ReconfigureCurrent()
        {
            var current = Current;
            if (current != null && current.Volume != null && current.Volume.sharedProfile != null)
                Configure(current.Volume.sharedProfile, current.Look, _qualityApplied ? _qualityLevel : DefaultQuality());
        }

        private static void LoadEffectPrefs()
        {
            if (_effectPrefsLoaded)
                return;
            _effectPrefsLoaded = true;
            try
            {
                _dofEnabled = PlayerPrefs.GetInt(PrefDof, 1) != 0;
                _grainEnabled = PlayerPrefs.GetInt(PrefGrain, 1) != 0;
            }
            catch (Exception)
            {
                // PlayerPrefs erişilemezse varsayılan (açık).
            }
        }

        private static void SaveEffectPref(string key, bool value)
        {
            try { PlayerPrefs.SetInt(key, value ? 1 : 0); }
            catch (Exception) { }
        }

        /// <summary>Hareket bulanıklığı ayarı (Ayarlar > Hareket bulanıklığı).</summary>
        public static void SetMotionBlur(bool enabled)
        {
            if (_motionBlur == enabled)
                return;
            _motionBlur = enabled;
            var current = Current;
            if (current != null && current.Volume != null && current.Volume.sharedProfile != null
                && current.Volume.sharedProfile.TryGet<MotionBlur>(out var mb))
                mb.active = enabled && _qualityLevel > 0 && current.Look == Look.Gameplay;
        }

        /// <summary>Nişan (ADS) derinlik bulanıklığı: 0 kapalı, 1 tam nişan. Her karede çağrılabilir.</summary>
        public static void SetAimBlur(float ads01)
        {
            ads01 = Mathf.Clamp01(ads01);
            if (Mathf.Approximately(ads01, _adsBlur01))
                return;
            _adsBlur01 = ads01;
            ApplyDepthOfFieldToCurrent();
        }

        /// <summary>Ölüm kamerası bulanıklığı (yakın bulanıklık + gaussian).</summary>
        public static void SetDeathBlur(bool on)
        {
            if (_deathBlur == on)
                return;
            _deathBlur = on;
            _deathTimer = 0f;
            ApplyDepthOfFieldToCurrent();
        }

        private static void ApplyDepthOfFieldToCurrent()
        {
            var current = Current;
            if (current != null && current.Volume != null && current.Volume.sharedProfile != null)
                ApplyDepthOfField(current.Volume.sharedProfile);
        }

        private static void ApplyDepthOfField(VolumeProfile profile)
        {
            if (!profile.TryGet<DepthOfField>(out var dof))
                return;
            var amount = _deathBlur ? 1f : _adsBlur01;
            if (amount <= 0.001f)
            {
                dof.mode.Override(DepthOfFieldMode.Off);
                return;
            }

            dof.mode.Override(DepthOfFieldMode.Gaussian);
            if (_deathBlur)
            {
                dof.gaussianStart.Override(0.1f);
                dof.gaussianEnd.Override(ScreenEffectsMath.DeathDofEnd(_deathTimer));
                dof.gaussianMaxRadius.Override(Mathf.Clamp(ScreenEffectsMath.DeathDofRadius(_deathTimer), 0.5f, 1.5f));
            }
            else
            {
                dof.gaussianStart.Override(ScreenEffectsMath.AdsDofStart(amount));
                dof.gaussianEnd.Override(ScreenEffectsMath.AdsDofEnd(amount));
            }
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

        private static float BaseSaturation(Look look) => (look == Look.Menu && !_lobbyGrade) ? -6f : (SpecBlend.HasTarget ? SpecBlend.Current.Saturation : MapGradeTable.For(_mapId).Saturation);

        private static float BaseVignette(Look look) => look == Look.Menu ? 0.36f : 0.12f;

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
            _mapId = MapCatalog.Kuzgun;
            _adsBlur01 = 0f;
            _deathBlur = false;
            _deathTimer = 0f;
            _timeOfDay = TimeOfDay.Gunduz;
            _lobbyGrade = false;
            SpecBlend.Reset();
            _effectPrefsLoaded = false;
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
