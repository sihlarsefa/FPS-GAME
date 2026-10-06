using System;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure.DI;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Persistence;
using Project.Infrastructure.Rendering;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Presentation.Bootstrap
{
    /// <summary>
    /// Sahneler arası kalıcı oturum durumu (statik — sahne yüklemelerinden etkilenmez): oyun modu, sıradaki maçın
    /// yapılandırması, son maç sonucu, ayarlar (PlayerPrefs) ve kariyer. Her bootstrap Awake'te <see cref="EnsureInitialized"/>
    /// çağırır; böylece herhangi bir sahne doğrudan oynatıldığında da oturum hazır olur.
    /// </summary>
    public static class GameSession
    {
        private static bool _initialized;
        private static bool _loading;
        private static bool _sceneHookInstalled;
        private static ISettingsStore _store;
        private static bool _stingsHooked;
        private static MatchConfig _config;
        private static bool _configConsumed;

        /// <summary>Maç başlarken (MatchBootstrap Start sonunda) tetiklenir — Online/Platform assembly'leri abone olur.</summary>
        public static event Action<MatchConfig> MatchStarting;

        /// <summary>Maç sonucu kaydedilirken tetiklenir — Online/Platform assembly'leri abone olur.</summary>
        public static event Action<MatchResult> MatchFinished;

        /// <summary>Dedicated sunucuda maç bitince TÜM savaşanların sonuçlarıyla tetiklenir (backend sonucu için).</summary>
        public static event Action<MatchSummary> MatchSummaryReady;

        /// <summary>Bir başarım açıldığında tetiklenir — Steam/backend senkronu için.</summary>
        public static event Action<AchievementDefinition> AchievementUnlocked;

        /// <summary>Başarım servisi (ilerleme + açılan başarımlar kalıcıdır).</summary>
        public static AchievementService Achievements { get; private set; }

        private static void OnAchievementUnlocked(AchievementDefinition def)
        {
            try { AchievementUnlocked?.Invoke(def); }
            catch (Exception e) { Debug.LogException(e); }
        }

        internal static void RaiseMatchStarting(MatchConfig config)
        {
            try { MatchStarting?.Invoke(config); }
            catch (Exception e) { Debug.LogException(e); }
        }

        internal static void RaiseMatchSummary(MatchSummary summary)
        {
            try { MatchSummaryReady?.Invoke(summary); }
            catch (Exception e) { Debug.LogException(e); }
        }

        internal static void RaiseMatchFinished(MatchResult result)
        {
            try { MatchFinished?.Invoke(result); }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>Sıradaki/aktif oyun modu (menü harekât veya poligonu seçerken ayarlar).</summary>
        public static GameMode Mode { get; set; } = GameMode.BattleRoyale;

        /// <summary>Poligon açılınca eğitim başlasın mı (TrainingBootstrap okur ve tüketir).</summary>
        public static bool StartTutorial { get; set; }

        /// <summary>Sıradaki/aktif maç yapılandırması. Atanmamışsa ayarlardan oluşturulur.</summary>
        public static MatchConfig Config
        {
            get => _config ??= CreateMatchConfig();
            set
            {
                _config = value;
                _configConsumed = false;
            }
        }

        /// <summary>Son tamamlanan maçın sonucu (ana menü / kariyer ekranı için).</summary>
        public static MatchResult? LastResult { get; set; }

        /// <summary>Madalya/XP/silah-harita kariyeri (CareerService; maç sonunda RecordMatch, olay beslemeleri AddProgress).</summary>
        public static CareerService Progress { get; private set; }

        /// <summary>Oyuncu ayarları (PlayerPrefs'e kaydedilir).</summary>
        public static SettingsService Settings { get; private set; }

        /// <summary>Kariyer istatistikleri ve rütbe (PlayerPrefs'e kaydedilir).</summary>
        public static CareerStatsService Career { get; private set; }

        /// <summary>Oturum hazır mı?</summary>
        public static bool IsInitialized => _initialized;

        /// <summary>Bir sahne yüklemesi sürüyor mu?</summary>
        public static bool IsLoading => _loading;

        /// <summary>Ayar/kariyer kalıcılık deposu (PlayerPrefs; erişilemezse null).</summary>
        public static ISettingsStore Store => _store;

        /// <summary>
        /// Ayarları ve kariyeri yükler (idempotent). Ayar değişikliklerini anında uygular (ses, kalite, tam ekran).
        /// </summary>
        public static void EnsureInitialized()
        {
            if (_initialized && Settings != null && Career != null)
                return;

            try
            {
                _store = new PlayerPrefsSettingsStore();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GameSession] PlayerPrefs deposu açılamadı, ayarlar yalnızca bellekte tutulacak: " + e.Message);
                _store = null;
            }

            Settings = new SettingsService(_store);
            UiSounds.VolumeProvider = () => Settings != null ? Settings.Current.SfxVolume : 0.8f; // arayüz sesi SFX ayarına bağlı
            try
            {
                Settings.Load();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            Career = new CareerStatsService(_store);
            try
            {
                Career.Load();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            try
            {
                Achievements = AchievementServiceProvider.GetOrCreate(_store);
                Achievements.AchievementUnlocked -= OnAchievementUnlocked;
                Achievements.AchievementUnlocked += OnAchievementUnlocked;
                Project.Presentation.UI.AchievementToastView.Ensure();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            try
            {
                var weaponIds = new System.Collections.Generic.List<string>();
                foreach (var w in Project.Application.Catalogs.WeaponCatalog.All)
                    weaponIds.Add(w.WeaponId);
                Progress = new CareerService(_store, weaponIds, MapCatalog.DisplayNames());
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            HookStings();

            Settings.Changed += ApplyRuntimeSettings;
            _initialized = true;

            if (ServerRuntime.IsDedicatedServer)
                ServerRuntime.ConfigureProcess();

            InstallSceneHook();
            ApplyRuntimeSettings(Settings.Current);
        }

        /// <summary>Maç başı / zafer / yenilgi müzik sting'lerini olaylara bağlar (bir kez).</summary>
        private static void HookStings()
        {
            if (_stingsHooked || UnityEngine.Application.isBatchMode)
                return;
            _stingsHooked = true;
            try
            {
                Project.Infrastructure.Audio.Music.MusicStings.VolumeProvider = MusicScriptGain;
                MatchStarting += _ => Project.Infrastructure.Audio.Music.MusicStings.Play(Project.Infrastructure.Audio.Music.StingKind.MatchStart);
                MatchFinished += r => Project.Infrastructure.Audio.Music.MusicStings.Play(
                    r.IsWinner ? Project.Infrastructure.Audio.Music.StingKind.Victory : Project.Infrastructure.Audio.Music.StingKind.Defeat);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Müzik kaynaklarının (menü teması, sting'ler) betik ses çarpanı: Ana ses × Müzik. Gerçek mikserin Müzik grubu kullanıcı
        /// Müzik seviyesini zaten sürüyorsa (MixerRouting.ApplySettings) yalnız Ana ses uygulanır — çifte kısma olmaz.
        /// Ayarlar yoksa 0,8.
        /// </summary>
        public static float MusicScriptGain()
        {
            var current = Settings != null ? Settings.Current : null;
            if (current == null)
                return 0.8f;

            var mixerDrivesMusic = false;
            try
            {
                mixerDrivesMusic = Project.Infrastructure.Audio.HdrMix.MixerRouting.DrivesVolume(Project.Infrastructure.Audio.HdrMix.MixChannel.Muzik);
            }
            catch (Exception)
            {
                // mikser yoksa betik yolu tek başına sürer
            }

            return (mixerDrivesMusic ? 1f : current.MusicVolume) * current.MasterVolume;
        }

        /// <summary>Maç sonu kariyer kaydı (madalya/XP/silah/harita).</summary>
        public static void RecordCareerMatch(MatchResult result, System.Collections.Generic.IReadOnlyDictionary<string, WeaponMatchStats> weapons, string mapName)
        {
            try { Progress?.RecordMatch(result, weapons, mapName); }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>Adanmış (başsız) sunucu olarak mı çalışıyoruz? Bkz. <see cref="ServerRuntime"/>.</summary>
        public static bool IsDedicatedServer => ServerRuntime.IsDedicatedServer;

        /// <summary>
        /// Ayarlardan yeni maç yapılandırması üretir (tim sayısı × 10 asker, zorluk, intikal yöntemi, rastgele tohum)
        /// ve <see cref="Config"/> olarak atar. Komut satırı argümanları (-teams, -seed, -difficulty, -insertion, -prematch)
        /// ayarların üzerine yazılır (sunucu ve test için).
        /// </summary>
        /// <summary>Menüde seçilen harita kimliği (<see cref="MapCatalog"/>).</summary>
        public static string SelectedMap
        {
            get
            {
                if (!string.IsNullOrEmpty(_mapOverride))
                    return _mapOverride;
                var s = Settings?.Current;
                return MapCatalog.Normalize(s != null ? s.SelectedMap : MapCatalog.Kuzgun);
            }
            set
            {
                var id = MapCatalog.Normalize(value);
                try { Settings?.Modify(s => s.SelectedMap = id); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        private static string _mapOverride;

        /// <summary>Menüde seçilen günün saati (oturum boyunca).</summary>
        public static TimeOfDay SelectedTimeOfDay { get; set; } = TimeOfDay.Gunduz;

        /// <summary>Hava seçimi: 0 = harita varsayılanı, 1 Açık, 2 Yağmur, 3 Kar.</summary>
        public static int SelectedWeatherIndex { get; set; }

        /// <summary>Kalıcı ayara dokunmadan harita seçer (sunucu -map argümanı). Boş değer geçersiz kılmayı kaldırır.</summary>
        public static void SetMapOverride(string mapId)
            => _mapOverride = string.IsNullOrWhiteSpace(mapId) ? null : MapCatalog.Normalize(mapId);

        /// <summary>Harita kimliğine karşılık gelen harekât sahnesi adı (bilinmeyen kimlik: Kuzgun Vadisi).</summary>
        public static string ResolveSceneForMap(string mapId) => SceneNames.OperationSceneFor(mapId);

        public static MatchConfig CreateMatchConfig()
        {
            EnsureInitialized();

            MatchConfig config = null;
            try
            {
                config = Settings?.CreateMatchConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (config == null)
            {
                var settings = Settings?.Current ?? new GameSettings();
                config = new MatchConfig().WithTeams(settings.TeamCount, SettingsService.TeamSize);
                config.Difficulty = settings.Difficulty;
                config.PlayerInsertion = settings.Insertion;
            }

            config.FriendlyFire = false;
            var mapId = MapCatalog.Normalize(SelectedMap);
            config.MapName = MapCatalog.DisplayName(mapId);
            config.MapHalfSize = MapCatalog.HalfSize(mapId);
            config.RandomSeed = NewSeed();
            config.TimeOfDay = SelectedTimeOfDay;
            config.Weather = SelectedWeatherIndex <= 0 ? AtmosphereRules.DefaultWeatherForMap(mapId) : AtmosphereRules.WeatherFromIndex(SelectedWeatherIndex - 1);
            ServerRuntime.ApplyMatchOverrides(config);

            _config = config;
            _configConsumed = false;
            return config;
        }

        /// <summary>
        /// Yapılandırmanın haritasını (ad, yarı boyut, otomatik hava) gerçekten yüklü sahnenin haritasına hizalar. Çatışma modu
        /// her zaman Kuzgun Vadisi'ni kullanır; menüde başka harita seçiliyken ya da sahne doğrudan oynatılınca yapılandırma
        /// yanlış haritanın havasını/boyutunu taşımasın.
        /// </summary>
        public static void AlignConfigToMap(MatchConfig config, string mapId)
        {
            if (config == null || string.IsNullOrEmpty(mapId))
                return;

            var id = MapCatalog.Normalize(mapId);
            if (MapCatalog.IndexOf(config.MapName) >= 0 && MapCatalog.Normalize(config.MapName) == id)
                return;

            config.MapName = MapCatalog.DisplayName(id);
            config.MapHalfSize = MapCatalog.HalfSize(id);
            config.Weather = SelectedWeatherIndex <= 0 ? AtmosphereRules.DefaultWeatherForMap(id) : AtmosphereRules.WeatherFromIndex(SelectedWeatherIndex - 1);
        }

        /// <summary>
        /// Harekât sahnesinin kullanacağı yapılandırma: menünün hazırladığı (henüz kullanılmamış) yapılandırma aynen alınır;
        /// yoksa ya da önceki maçta kullanıldıysa ayarlardan yeni tohumla yenisi üretilir. Aynı maç iki kez oynanmaz.
        /// </summary>
        public static MatchConfig AcquireMatchConfig()
        {
            EnsureInitialized();
            if (_config == null || _configConsumed)
                CreateMatchConfig();

            _configConsumed = true;
            return _config;
        }

        /// <summary>
        /// Sahneyi yükler: zaman ölçeği ve imleç sıfırlanır, yükleme ekranı gösterilir. Sahne Build Settings'te yoksa
        /// editörde doğrudan proje yolundan yüklenir; bulunamazsa hata günlüğe yazılır ve mevcut sahnede kalınır.
        /// </summary>
        public static void LoadScene(string sceneName)
        {
            EnsureInitialized();

            if (string.IsNullOrEmpty(sceneName))
                sceneName = SceneNames.MainMenu;

            if (_loading)
                return;

            Time.timeScale = 1f;
            if (!ServerRuntime.IsDedicatedServer)
                AudioListener.pause = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            PrepareLoadingBriefing(sceneName);
            ShowLoading(SceneNames.LoadingMessageFor(sceneName));

            try
            {
                if (UnityEngine.Application.CanStreamedLevelBeLoaded(sceneName))
                {
                    _loading = true;
                    var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                    if (op != null)
                    {
                        TrackLoading(op);
                        return;
                    }

                    _loading = false;
                    SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                    return;
                }

#if UNITY_EDITOR
                var path = SceneNames.PathOf(sceneName);
                if (System.IO.File.Exists(path))
                {
                    _loading = true;
                    var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                        path, new LoadSceneParameters(LoadSceneMode.Single));
                    if (op != null)
                        return;

                    _loading = false;
                }
#endif
                Debug.LogError("[GameSession] Sahne yüklenemedi (Build Settings'te yok): " + sceneName);
            }
            catch (Exception e)
            {
                _loading = false;
                Debug.LogException(e);
            }

            HideLoading();

            // Sunucu boşta takılı kalmasın: süreç kapanır, sunucu yöneticisi yeniden başlatır / alarm verir.
            if (ServerRuntime.IsDedicatedServer)
            {
                Debug.LogError("[Sunucu] Sahne yüklenemediği için süreç kapatılıyor: " + sceneName);
                ServerRuntime.Quit();
            }
        }

        /// <summary>Ayarlardan yeni bir harekât yapılandırması oluşturur ve Kuzgun Vadisi'ni yükler.</summary>
        public static void StartOperation()
        {
            Mode = GameMode.BattleRoyale;
            CreateMatchConfig();
            LoadScene(SceneNames.OperationSceneFor(SelectedMap));
        }

        /// <summary>Çatışma (hızlı maç): Kuzgun Vadisi sahnesi, 2 tim × 10, yerden doğuş. MatchBootstrap SkirmishBootstrap'a devreder.</summary>
        public static void StartSkirmish()
        {
            ConvoyBootstrap.Reset();
            Mode = GameMode.Skirmish;
            CreateMatchConfig();
            LoadScene(SceneNames.Skirmish);
        }

        /// <summary>Rehine Kurtarma: Kuzgun Vadisi sahnesi; kurulum HostageBootstrap'ta.</summary>
        public static void StartHostageRescue()
        {
            Mode = GameMode.HostageRescue;
            CreateMatchConfig();
            LoadScene(SceneNames.Skirmish);
        }

        /// <summary>Atış poligonunu yükler.</summary>
        public static void StartTraining() => StartTraining(false);

        /// <summary>Poligonu açar; <paramref name="tutorial"/> true ise eğitim adımları başlar.</summary>
        public static void StartTraining(bool tutorial)
        {
            StartTutorial = tutorial;
            Mode = GameMode.Training;
            LoadScene(SceneNames.Training);
        }

        /// <summary>Aynı modu yeni tohumla yeniden başlatır.</summary>
        public static void Restart()
        {
            if (Mode == GameMode.Training)
            {
                StartTraining();
                return;
            }

            if (Mode == GameMode.Skirmish)
            {
                if (ConvoyBootstrap.Active)
                {
                    ConvoyBootstrap.Begin();
                    return;
                }

                StartSkirmish();
                return;
            }

            if (Mode == GameMode.HostageRescue)
            {
                StartHostageRescue();
                return;
            }

            StartOperation();
        }

        /// <summary>Ana menüye döner.</summary>
        public static void ReturnToMainMenu() => LoadScene(SceneNames.MainMenu);

        /// <summary>Maç sonucunu kaydeder: son sonuç + kariyer (XP, rütbe).</summary>
        public static void RecordResult(MatchResult result)
        {
            EnsureInitialized();
            LastResult = result;
            try
            {
                Career?.Record(result);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            try
            {
                Achievements?.RecordMatch(result, Career?.Current != null ? (int)Career.Current.Rank : -1);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            Project.Presentation.UI.SeasonPassPanel.RecordMatch(result); // sezon kartı tecrübesi (null-güvenli)
        }

        /// <summary>Ayarların motor tarafı etkileri: ses seviyeleri, grafik kalitesi, tam ekran.</summary>
        public static void ApplyRuntimeSettings(GameSettings settings)
        {
            // Sunucuda ses/görüntü yok: ayarların motor tarafı etkileri uygulanmaz.
            if (settings == null || ServerRuntime.IsDedicatedServer)
                return;

            try
            {
                GameAudio.MasterVolume = settings.MasterVolume;
                GameAudio.AmbientVolume = settings.AmbientVolume;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            AdvancedDisplay.Apply(settings);

            try
            {
                PostProcessing.ApplyQuality(settings.QualityLevel);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

#if !UNITY_EDITOR && !UNITY_SERVER
            try
            {
                if (Screen.fullScreen != settings.Fullscreen)
                    Screen.fullScreen = settings.Fullscreen;
            }
            catch (Exception)
            {
                // Bazı platformlarda tam ekran değiştirilemez.
            }
#endif
        }

        private static int NewSeed()
        {
            unchecked
            {
                var seed = Environment.TickCount * 397 ^ Guid.NewGuid().GetHashCode();
                seed &= 0x7fffffff;
                return seed == 0 ? 1 : seed;
            }
        }

        private static void InstallSceneHook()
        {
            if (_sceneHookInstalled)
                return;

            _sceneHookInstalled = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single)
                _loading = false;
        }

        private static void PrepareLoadingBriefing(string sceneName)
        {
            if (ServerRuntime.IsDedicatedServer)
                return;
            try
            {
                var map = SceneNames.MapIdForScene(sceneName);
                var mode = sceneName == SceneNames.Training ? GameMode.Training : Mode;
                LoadingScreen.SetContext(map, mode, _config != null ? _config.TeamCount : 0, _config != null ? _config.TeamSize : 0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void TrackLoading(AsyncOperation op)
        {
            if (!ServerRuntime.IsDedicatedServer)
                LoadingScreen.TrackSceneLoad(op);
        }

        private static void ShowLoading(string message)
        {
            if (ServerRuntime.IsDedicatedServer)
            {
                Debug.Log("[Sunucu] " + message);
                return;
            }

            try
            {
                LoadingScreen.Show(message);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        internal static void HideLoading()
        {
            if (ServerRuntime.IsDedicatedServer)
                return;

            try
            {
                LoadingScreen.Hide();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Etki alanı yeniden yüklemesi kapalıyken (Enter Play Mode Options) eski oturum taşınmasın.
            if (Settings != null)
                Settings.Changed -= ApplyRuntimeSettings;

            if (_sceneHookInstalled)
                SceneManager.sceneLoaded -= OnSceneLoaded;

            _initialized = false;
            _loading = false;
            _sceneHookInstalled = false;
            _store = null;
            _config = null;
            _configConsumed = false;
            Settings = null;
            Career = null;
            Achievements = null;
            AchievementServiceProvider.ResetShared();
            LastResult = null;
            Mode = GameMode.BattleRoyale;
            SelectedTimeOfDay = TimeOfDay.Gunduz;
            SelectedWeatherIndex = 0;
            _mapOverride = null;
        }
    }
}
