using System;
using Project.Online.Backend;
using Project.Online.Bootstrap;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Online.UI
{
    /// <summary>
    /// Online giriş / kayıt penceresi. Başarılı oturumda hub açılır veya
    /// <see cref="Show"/> ile verilen geri çağrı tetiklenir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OnlineLoginPanel : MonoBehaviour
    {
        private const float WindowWidth = 560f;
        private const float WindowHeight = 520f;

        private Action _onClosed;
        private Action _onLoggedIn;
        private bool _closed;
        private bool _busy;
        private bool _registerMode;
        private CanvasGroup _group;
        private float _fade;

        private InputField _username;
        private InputField _email;
        private InputField _password;
        private RectTransform _emailRow;
        private Text _status;
        private Button _submitButton;
        private Button _tabLogin;
        private Button _tabRegister;
        private Button _backButton;

        public bool IsOpen => !_closed && this != null;

        /// <summary>
        /// Giriş penceresini açar. Başarılı girişte önce hub (veya <paramref name="onLoggedIn"/>),
        /// kapanınca <paramref name="onClosed"/>.
        /// </summary>
        public static OnlineLoginPanel Show(Transform parent, Action onClosed = null, Action onLoggedIn = null)
        {
            OnlineServices.Ensure();

            if (OnlineServices.Client.IsLoggedIn && onLoggedIn == null && parent != null)
            {
                OnlineHubPanel.Show(parent, onClosed);
                return null;
            }

            if (OnlineServices.Client.IsLoggedIn && onLoggedIn != null)
            {
                try { onLoggedIn(); }
                catch (Exception e) { Debug.LogException(e); }
                return null;
            }

            var root = OnlineUi.CreateDimRoot("[Online Giriş]", parent, out var group);
            var panel = root.gameObject.AddComponent<OnlineLoginPanel>();
            panel._onClosed = onClosed;
            panel._onLoggedIn = onLoggedIn;
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
            _onLoggedIn = null;

            gameObject.SetActive(false);
            UiFactory.DestroySafe(gameObject);

            if (callback == null)
                return;
            try { callback(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void Build(RectTransform root)
        {
            var window = OnlineUi.CreateWindow(root, WindowWidth, WindowHeight, "ONLİNE GİRİŞ");
            var body = OnlineUi.CreateBody(window, 110f, 110f);

            var tabs = UiFactory.HorizontalList(body, 10f, 0, TextAnchor.MiddleCenter);
            UiFactory.LayoutSize(tabs, -1f, UiTheme.ButtonHeight, 1f);
            var tabLayout = tabs.GetComponent<HorizontalLayoutGroup>();
            if (tabLayout != null)
                tabLayout.childForceExpandWidth = true;

            _tabLogin = UiFactory.Button(tabs, "GİRİŞ", () => SetMode(false), UiButtonStyle.Primary);
            UiFactory.LayoutSize(_tabLogin, -1f, UiTheme.ButtonHeight, 1f);
            _tabRegister = UiFactory.Button(tabs, "KAYIT", () => SetMode(true), UiButtonStyle.Default);
            UiFactory.LayoutSize(_tabRegister, -1f, UiTheme.ButtonHeight, 1f);

            _username = OnlineUi.CreateField(body, "Kullanıcı adı", false);
            _email = OnlineUi.CreateField(body, "E-posta", false);
            _email.contentType = InputField.ContentType.EmailAddress;
            _emailRow = _email.GetComponent<RectTransform>();
            _password = OnlineUi.CreateField(body, "Şifre", true);

            _status = OnlineUi.StatusLabel(body);

            _submitButton = UiFactory.Button(body, "GİRİŞ YAP", OnSubmitClicked, UiButtonStyle.Primary);
            UiFactory.LayoutSize(_submitButton, -1f, UiTheme.ButtonHeight, 1f);

            OnlineUi.CreateFooter(window, out _backButton, Close, "GERİ");
            SetMode(false);
        }

        private void SetMode(bool register)
        {
            _registerMode = register;
            if (_emailRow != null)
                _emailRow.gameObject.SetActive(register);

            if (_submitButton != null)
            {
                var label = UiFactory.GetButtonLabel(_submitButton);
                if (label != null)
                    label.text = register ? "KAYIT OL" : "GİRİŞ YAP";
            }

            RefreshTabStyles();
            OnlineUi.SetStatus(_status, "", UiTheme.TextDim);
        }

        private void RefreshTabStyles()
        {
            // Görsel ipucu: aktif sekme Primary stilinde kalır; tıklanınca yeniden oluşturulmaz.
            UiWidgets.SetInteractable(_tabLogin, true);
            UiWidgets.SetInteractable(_tabRegister, true);
            var loginLabel = UiFactory.GetButtonLabel(_tabLogin);
            var registerLabel = UiFactory.GetButtonLabel(_tabRegister);
            if (loginLabel != null)
                loginLabel.color = _registerMode ? UiTheme.TextMuted : UiTheme.Text;
            if (registerLabel != null)
                registerLabel.color = _registerMode ? UiTheme.Text : UiTheme.TextMuted;
        }

        private void OnSubmitClicked()
        {
            if (_busy || _closed)
                return;
            _ = SubmitAsync();
        }

        private async System.Threading.Tasks.Task SubmitAsync()
        {
            var username = _username != null ? (_username.text ?? "").Trim() : "";
            var password = _password != null ? (_password.text ?? "") : "";
            var email = _email != null ? (_email.text ?? "").Trim() : "";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                OnlineUi.SetStatus(_status, "Kullanıcı adı ve şifre gerekli.", UiTheme.Accent);
                return;
            }

            if (_registerMode && (string.IsNullOrEmpty(email) || email.IndexOf('@') < 0))
            {
                OnlineUi.SetStatus(_status, "Geçerli bir e-posta girin.", UiTheme.Accent);
                return;
            }

            _busy = true;
            SetInteractable(false);
            OnlineUi.SetStatus(_status, _registerMode ? "Kayıt yapılıyor…" : "Giriş yapılıyor…", UiTheme.TextDim);

            try
            {
                var client = OnlineServices.Client;
                if (_registerMode)
                    await client.RegisterAsync(username, email, password, "tr");
                else
                    await client.LoginAsync(username, password);

                try
                {
                    await OnlineServices.Profile.ActivateOnlineAsync();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[OnlineLogin] Profil senkronu: " + e.Message);
                }

                if (_closed)
                    return;

                OnlineUi.SetStatus(_status, "Başarılı.", UiTheme.HealthHigh);
                OpenHubAfterLogin();
            }
            catch (BackendApiException ex)
            {
                if (!_closed)
                    OnlineUi.SetStatus(_status, ex.TurkishMessage, UiTheme.Accent);
            }
            catch (Exception ex)
            {
                if (!_closed)
                    OnlineUi.SetStatus(_status, "Bağlantı hatası: " + ex.Message, UiTheme.Accent);
            }
            finally
            {
                _busy = false;
                if (!_closed)
                    SetInteractable(true);
            }
        }

        private void OpenHubAfterLogin()
        {
            var parent = transform.parent;
            var onClosed = _onClosed;
            var onLoggedIn = _onLoggedIn;
            _onClosed = null;
            _onLoggedIn = null;

            _closed = true;
            ClearSelection();
            gameObject.SetActive(false);
            UiFactory.DestroySafe(gameObject);

            if (onLoggedIn != null)
            {
                try { onLoggedIn(); }
                catch (Exception e) { Debug.LogException(e); }
                return;
            }

            if (parent != null)
            {
                OnlineHubPanel.Show(parent, onClosed);
                return;
            }

            if (onClosed == null)
                return;
            try { onClosed(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void SetInteractable(bool value)
        {
            UiWidgets.SetInteractable(_submitButton, value);
            UiWidgets.SetInteractable(_tabLogin, value);
            UiWidgets.SetInteractable(_tabRegister, value);
            UiWidgets.SetInteractable(_backButton, value);
            if (_username != null) _username.interactable = value;
            if (_email != null) _email.interactable = value;
            if (_password != null) _password.interactable = value;
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
            if (es != null && _username != null)
                es.SetSelectedGameObject(_username.gameObject);
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
