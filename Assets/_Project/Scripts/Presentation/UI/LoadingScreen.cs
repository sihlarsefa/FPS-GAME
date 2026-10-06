using System;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Sahneler arası yükleme örtüsü (DontDestroyOnLoad tuval, en üstte çizilir). <see cref="Show"/> ilk çağrıda tuvali
    /// kurar ve yumuşakça belirir; <see cref="Hide"/> solarak gizler.
    /// <para>Otomatik gizleme: bir sahne yüklemesi bittikten (GameSession.IsLoading false) ve son
    /// <see cref="Show"/>/<see cref="SetProgress"/> çağrısından bu yana birkaç kare geçtikten sonra örtü kendiliğinden kalkar —
    /// böylece yeni sahnenin bootstrap'ı Hide çağırmayı unutsa da ekran kilitli kalmaz. Uzun süren, birden çok kareye yayılan
    /// kurulumlar <see cref="SetProgress"/> ile örtüyü açık tutabilir veya <see cref="AutoHide"/> kapatılabilir.</para>
    /// </summary>
    public static class LoadingScreen
    {
        /// <summary>Tuval çizim sırası (her şeyin üstünde).</summary>
        public const int SortOrder = 500;

        private static LoadingScreenView _view;

        /// <summary>Örtü görünür mü (solma sürüyor olabilir)?</summary>
        public static bool IsVisible => _view != null && _view.IsShowing;

        /// <summary>Sahne yüklemesi bitince kendiliğinden gizlensin mi (varsayılan açık).</summary>
        public static bool AutoHide { get; set; } = true;

        /// <summary>Örtünün en az görünür kalacağı süre (sn, ölçeksiz) — yanıp sönmeyi önler.</summary>
        public static float MinimumVisibleSeconds { get; set; } = 0.45f;

        /// <summary>Örtüyü gösterir ya da iletisini günceller. Oynatma dışında (editör) yok sayılır.</summary>
        public static void Show(string message)
        {
            // Editörde oynatma dışında ve başsız (batch / dedicated server) çalışmada örtü kurulmaz.
            if (!UnityEngine.Application.isPlaying || UnityEngine.Application.isBatchMode)
                return;

            try
            {
                if (!IsVisible)
                {
                    Progress.Reset();
                    _sceneOp = null;
                }

                var view = EnsureView();
                if (view != null)
                    view.Show(string.IsNullOrEmpty(message) ? "Yükleniyor" : message);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static readonly LoadingProgressModel Progress = new LoadingProgressModel();
        private static string _mapId = MapCatalog.Kuzgun;
        private static GameMode _mode = GameMode.BattleRoyale;
        private static int _teamCount;
        private static int _teamSize;
        private static UnityEngine.AsyncOperation _sceneOp;

        /// <summary>Brifing bağlamı: harita (kimlik ya da ad), mod, tim sayısı/boyu (0 = bilinmiyor). Her an çağrılabilir, güvenlidir.</summary>
        public static void SetContext(string mapIdOrName, GameMode mode, int teamCount, int teamSize)
        {
            _mapId = MapCatalog.Normalize(mapIdOrName);
            _mode = mode;
            _teamCount = teamCount;
            _teamSize = teamSize;
            if (_view != null)
                _view.SetBriefing(_mapId, _mode, _teamCount, _teamSize);
        }

        /// <summary>Sahne yüklemesini izler (AsyncOperation.progress → "sahne" aşaması). Örtü kapalıysa yok sayılır.</summary>
        public static void TrackSceneLoad(UnityEngine.AsyncOperation op)
        {
            _sceneOp = op;
        }

        /// <summary>
        /// Gerçek yükleme aşamasını bildirir (aşama içi ilerleme 0..1). Toplam ilerleme yalnız artar; örtü kapalı/yoksa
        /// yok sayılır — bootstrap'ten koşulsuz çağrılabilir.
        /// </summary>
        public static void Report(LoadPhase phase, float fraction01)
        {
            _sceneOp = null;
            var total = Progress.Report(phase, fraction01);
            if (_view != null && _view.IsShowing)
                _view.Report(total, LoadingProgressModel.Label(Progress.Phase));
        }

        internal static float PollSceneProgress()
        {
            if (_sceneOp == null)
                return -1f;
            // Unity sahne yüklemesi 0.9'da bekler; 0..0.9 → 0..1.
            var f = Mathf.Clamp01(_sceneOp.progress / 0.9f);
            return Progress.Report(LoadPhase.SceneLoad, f);
        }

        internal static string CurrentPhaseLabel => LoadingProgressModel.Label(Progress.Phase);

        /// <summary>İletiyi değiştirir (örtü kapalıysa bir şey yapmaz).</summary>
        public static void SetMessage(string message)
        {
            if (_view != null && _view.IsShowing)
                _view.SetMessage(string.IsNullOrEmpty(message) ? "Yükleniyor" : message);
        }

        /// <summary>
        /// Belirli ilerleme (0..1) gösterir; negatif değer belirsiz (kayan) çubuğa döner. Her çağrı otomatik gizlemeyi erteler.
        /// </summary>
        public static void SetProgress(float progress01)
        {
            if (_view != null && _view.IsShowing)
                _view.SetProgress(progress01);
        }

        /// <summary>Örtüyü solarak gizler.</summary>
        public static void Hide()
        {
            if (_view != null)
                _view.Hide(false);
        }

        /// <summary>Örtüyü anında gizler (solma yok).</summary>
        public static void HideImmediate()
        {
            if (_view != null)
                _view.Hide(true);
        }

        private static LoadingScreenView EnsureView()
        {
            if (_view != null)
                return _view;

            _view = LoadingScreenView.Create(SortOrder);
            if (_view != null)
                _view.SetBriefing(_mapId, _mode, _teamCount, _teamSize);
            return _view;
        }

        internal static void NotifyDestroyed(LoadingScreenView view)
        {
            if (_view == view)
                _view = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _view = null;
            _sceneOp = null;
            Progress.Reset();
            _mapId = MapCatalog.Kuzgun;
            _mode = GameMode.BattleRoyale;
            _teamCount = 0;
            _teamSize = 0;
            AutoHide = true;
            MinimumVisibleSeconds = 0.45f;
        }
    }
}
