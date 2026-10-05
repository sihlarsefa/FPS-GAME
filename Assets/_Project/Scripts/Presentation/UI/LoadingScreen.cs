using System;
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
            if (!UnityEngine.Application.isPlaying)
                return;

            try
            {
                var view = EnsureView();
                if (view != null)
                    view.Show(string.IsNullOrEmpty(message) ? "Yükleniyor" : message);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

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
            AutoHide = true;
            MinimumVisibleSeconds = 0.45f;
        }
    }
}
