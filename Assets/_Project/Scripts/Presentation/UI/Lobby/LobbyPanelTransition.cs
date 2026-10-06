using UnityEngine;

namespace Project.Presentation.UI.Lobby
{
    /// <summary>
    /// Panel açılış/kapanış geçişi: ease'li solma + yatay kayma. <see cref="Show"/>/<see cref="Hide"/> çağrılır;
    /// kapanış bitince nesne pasifleşir. Ölçeksiz zamanla çalışır.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyPanelTransition : MonoBehaviour
    {
        private CanvasGroup _group;
        private RectTransform _rect;
        private Vector2 _home;
        private bool _homeSet;
        private float _t = 1f;
        private bool _entering;
        private bool _running;

        /// <summary>Geçiş sürüyor mu.</summary>
        public bool Running => _running;

        /// <summary>Hedefe geçiş bileşeni ekler (varsa aynısını döndürür).</summary>
        public static LobbyPanelTransition Attach(Component target)
        {
            var t = target.GetComponent<LobbyPanelTransition>();
            return t != null ? t : target.gameObject.AddComponent<LobbyPanelTransition>();
        }

        private void Init()
        {
            if (_rect != null) return;
            _rect = (RectTransform)transform;
            _group = UiFactory.EnsureCanvasGroup(this);
            if (!_homeSet) { _home = _rect.anchoredPosition; _homeSet = true; }
        }

        public void Show()
        {
            Init();
            gameObject.SetActive(true);
            _entering = true; _t = 0f; _running = true;
            Apply();
        }

        public void Hide()
        {
            Init();
            if (!gameObject.activeSelf) return;
            _entering = false; _t = 0f; _running = true;
        }

        private void Update()
        {
            if (!_running) return;
            _t += Time.unscaledDeltaTime / LobbyTheme.PanelDuration(_entering);
            if (_t >= 1f)
            {
                _t = 1f; _running = false;
                Apply();
                if (!_entering) gameObject.SetActive(false);
                return;
            }
            Apply();
        }

        private void Apply()
        {
            LobbyTheme.PanelPose(_t, _entering, out var alpha, out var dx);
            _group.alpha = alpha;
            _group.interactable = _entering && _t >= 1f || !_running && _entering;
            _group.blocksRaycasts = _entering;
            _rect.anchoredPosition = _home + new Vector2(dx, 0f);
        }
    }
}
