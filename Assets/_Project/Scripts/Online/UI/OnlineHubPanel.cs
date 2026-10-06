using System;
using Project.Online.Bootstrap;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Online.UI
{
    /// <summary>
    /// Giriş sonrası online hub: Tim, Eşleştirme, Sıralama, Çıkış.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OnlineHubPanel : MonoBehaviour
    {
        private const float WindowWidth = 520f;
        private const float WindowHeight = 620f;

        private Action _onClosed;
        private bool _closed;
        private CanvasGroup _group;
        private float _fade;
        private Text _welcome;
        private Button _firstButton;

        public bool IsOpen => !_closed && this != null;

        public static OnlineHubPanel Show(Transform parent, Action onClosed = null)
        {
            OnlineServices.Ensure();

            var root = OnlineUi.CreateDimRoot("[Online Hub]", parent, out var group);
            var panel = root.gameObject.AddComponent<OnlineHubPanel>();
            panel._onClosed = onClosed;
            panel._group = group;
            panel.Build(root);
            return panel;
        }

        public void Close()
        {
            if (_closed)
                return;

            _closed = true;
            ClearSelection();
            var callback = _onClosed;
            _onClosed = null;

            gameObject.SetActive(false);
            UiFactory.DestroySafe(gameObject);

            if (callback == null)
                return;
            try { callback(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void Build(RectTransform root)
        {
            var window = OnlineUi.CreateWindow(root, WindowWidth, WindowHeight, "ONLİNE");
            var body = OnlineUi.CreateBody(window, 110f, 100f);

            var name = OnlineServices.Client.CurrentPlayer != null
                ? OnlineServices.Client.CurrentPlayer.Username
                : "Komutan";
            _welcome = UiFactory.Label(body, "Hoş geldin, " + name, UiTheme.FontMedium, TextAnchor.MiddleLeft, UiTheme.Text);
            UiFactory.LayoutSize(_welcome, -1f, 36f, 1f);

            _firstButton = AddNavButton(body, "TİM", OpenSquad);
            AddNavButton(body, "EŞLEŞTİRME", OpenMatchmaking);
            AddNavButton(body, "SUNUCU / KATIL", OpenServer);
            AddNavButton(body, "SIRALAMA", OpenLeaderboard);
            AddNavButton(body, "ÇIKIŞ YAP", Logout, UiButtonStyle.Danger);

            OnlineUi.CreateFooter(window, out _, Close, "GERİ");
        }

        private Button AddNavButton(Transform parent, string label, Action action, UiButtonStyle style = UiButtonStyle.Primary)
        {
            var btn = UiFactory.Button(parent, label, action, style);
            UiFactory.LayoutSize(btn, -1f, UiTheme.ButtonHeight, 1f);
            return btn;
        }

        private void OpenSquad()
        {
            OpenChild((parent, onClosed) => OnlineSquadPanel.Show(parent, onClosed));
        }

        private void OpenMatchmaking()
        {
            OpenChild((parent, onClosed) => OnlineMatchmakingPanel.Show(parent, onClosed));
        }

        private void OpenServer()
        {
            OpenChild((parent, onClosed) => OnlineServerPanel.Show(parent, onClosed));
        }

        private void OpenLeaderboard()
        {
            OpenChild((parent, onClosed) => OnlineLeaderboardPanel.Show(parent, onClosed));
        }

        private void OpenChild(Action<Transform, Action> showChild)
        {
            if (_closed)
                return;

            var parent = transform.parent;
            var onClosed = _onClosed;
            _onClosed = null;
            _closed = true;
            ClearSelection();

            gameObject.SetActive(false);
            UiFactory.DestroySafe(gameObject);

            if (parent == null)
            {
                try { onClosed?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
                return;
            }

            showChild(parent, () =>
            {
                if (parent != null)
                    Show(parent, onClosed);
                else
                {
                    try { onClosed?.Invoke(); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            });
        }

        private void Logout()
        {
            if (_closed)
                return;

            try
            {
                OnlineServices.Profile.DeactivateOnline();
                OnlineServices.Client.LogoutLocal();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OnlineHub] Çıkış: " + e.Message);
            }

            var parent = transform.parent;
            var onClosed = _onClosed;
            _onClosed = null;
            _closed = true;
            ClearSelection();
            gameObject.SetActive(false);
            UiFactory.DestroySafe(gameObject);

            if (parent != null)
                OnlineLoginPanel.Show(parent, onClosed);
            else if (onClosed != null)
            {
                try { onClosed(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        private void Update()
        {
            if (_group == null || _fade >= 1f)
                return;
            _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / UiTheme.FadeDuration);
            _group.alpha = _fade;
        }

        private void Start()
        {
            var es = EventSystem.current;
            if (es != null && _firstButton != null)
                es.SetSelectedGameObject(_firstButton.gameObject);
        }

        private void ClearSelection()
        {
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null &&
                es.currentSelectedGameObject.transform.IsChildOf(transform))
                es.SetSelectedGameObject(null);
        }
    }
}
