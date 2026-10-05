using System;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
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
        private static MatchConfig _config;
        private static bool _configConsumed;

        /// <summary>Sıradaki/aktif oyun modu (menü harekât veya poligonu seçerken ayarlar).</summary>
        public static GameMode Mode { get; set; } = GameMode.BattleRoyale;

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

            Settings.Changed += ApplyRuntimeSettings;
            _initialized = true;

            InstallSceneHook();
            ApplyRuntimeSettings(Settings.Current);
        }

        /// <summary>
        /// Ayarlardan yeni maç yapılandırması üretir (tim sayısı × 10 asker, zorluk, intikal yöntemi, rastgele tohum)
        /// ve <see cref="Config"/> olarak atar.
        /// </summary>
        public static MatchConfig CreateMatchConfig()
        {
            EnsureInitialized();

            var settings = Settings?.Current ?? new GameSettings();
            var config = new MatchConfig().WithTeams(settings.TeamCount, SettingsService.TeamSize);
            config.Difficulty = settings.Difficulty;
            config.PlayerInsertion = settings.Insertion;
            config.FriendlyFire = false;
            config.RandomSeed = NewSeed();

            _config = config;
            _configConsumed = false;
            return config;
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
            AudioListener.pause = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            ShowLoading(SceneNames.LoadingMessageFor(sceneName));

            try
            {
                if (UnityEngine.Application.CanStreamedLevelBeLoaded(sceneName))
                {
                    _loading = true;
                    var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                    if (op != null)
                        return;

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
        }

        /// <summary>Ayarlardan yeni bir harekât yapılandırması oluşturur ve Kuzgun Vadisi'ni yükler.</summary>
        public static void StartOperation()
        {
            Mode = GameMode.BattleRoyale;
            CreateMatchConfig();
            LoadScene(SceneNames.Operation);
        }

        /// <summary>Atış poligonunu yükler.</summary>
        public static void StartTraining()
        {
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
        }

        /// <summary>Ayarların motor tarafı etkileri: ses seviyeleri, grafik kalitesi, tam ekran.</summary>
        public static void ApplyRuntimeSettings(GameSettings settings)
        {
            if (settings == null)
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

        private static void ShowLoading(string message)
        {
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
            LastResult = null;
            Mode = GameMode.BattleRoyale;
        }
    }
}
