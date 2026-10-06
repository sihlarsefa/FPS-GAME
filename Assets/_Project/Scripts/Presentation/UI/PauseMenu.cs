using System;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Presentation.Bootstrap;
using Project.Infrastructure.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Oyun içi duraklatma menüsü (Esc). Açıkken <see cref="Time.timeScale"/> 0'dır ve imleç serbesttir; kapanınca önceki
    /// zaman ölçeği ve imleç durumu geri yüklenir. "DEVAM ET", "AYARLAR" (ayar penceresi) ve "ANA MENÜ" (onaylı) düğmeleri,
    /// sağda kontrol kılavuzu.
    /// <para>Esc tuşunu bu bileşen okumaz: oyun arayüzü denetleyicisi (GameplayUiController) Esc ile
    /// <see cref="Open"/>/<see cref="Close"/> çağırır. Kendi tuvali vardır (çizim sırası 60 — HUD ve haritanın üstünde).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseMenu : MonoBehaviour
    {
        /// <summary>Tuval çizim sırası.</summary>
        public const int SortOrder = 60;

        private static readonly string[,] Controls =
        {
            { "W A S D", "Hareket" },
            { "SHIFT", "Koş" },
            { "SPACE", "Zıpla" },
            { "CTRL / C", "Çömel" },
            { "Z", "Yüzüstü yat" },
            { "Q / E", "Yana eğil" },
            { "SOL TIK", "Ateş" },
            { "SAĞ TIK", "Nişan al" },
            { "R", "Şarjör değiştir" },
            { "B", "Atış modu" },
            { "F", "Etkileşim / araca bin" },
            { "1–4", "Silah seç" },
            { "H / J", "İyileş / takviye" },
            { "G / T", "El bombası / sis" },
            { "TAB", "Envanter" },
            { "M", "Harita" },
            { "F1–F4", "Tim emirleri" },
            { "V", "Topçu desteği" }
        };

        private Action _onResume;
        private Action _onMainMenu;
        private SettingsService _settings;

        private Canvas _canvas;
        private GraphicRaycaster _raycaster;
        private RectTransform _root;
        private RectTransform _content;
        private CanvasGroup _contentGroup;
        private Button _resumeButton;
        private SettingsPanel _settingsPanel;
        private MenuDialog _dialog;

        private float _previousTimeScale = 1f;
        private CursorLockMode _previousLock = CursorLockMode.Locked;
        private bool _previousCursorVisible;
        private float _fade;
        private bool _leaving;

        /// <summary>Menü açık mı?</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Menü açıldığında.</summary>
        public event Action Opened;

        /// <summary>Menü kapandığında.</summary>
        public event Action Closed;

        /// <summary>Ayarlar penceresinden ayarlar uygulandığında (oyuncu kontrolcüsüne aktarılması için).</summary>
        public event Action<GameSettings> SettingsApplied;

        /// <summary>
        /// Duraklatma menüsünü oluşturur (kapalı başlar).
        /// </summary>
        /// <param name="onResume">"DEVAM ET" — null ise menü yalnızca kendini kapatır.</param>
        /// <param name="onMainMenu">"ANA MENÜ" onaylanınca — null ise <see cref="GameSession.ReturnToMainMenu"/>.</param>
        /// <param name="settings">Ayar servisi — null ise <see cref="GameSession.Settings"/>.</param>
        public static PauseMenu Create(Action onResume, Action onMainMenu, SettingsService settings)
        {
            var canvas = UiFactory.CreateCanvas("[Duraklatma Menüsü]", SortOrder);
            var menu = canvas.gameObject.AddComponent<PauseMenu>();
            menu._onResume = onResume;
            menu._onMainMenu = onMainMenu;
            menu._settings = settings;
            menu._canvas = canvas;
            menu._raycaster = canvas.GetComponent<GraphicRaycaster>();
            menu.Build((RectTransform)canvas.transform);
            menu.SetCanvasVisible(false);
            return menu;
        }

        /// <summary>Menüyü açar: zamanı durdurur, imleci serbest bırakır.</summary>
        public void Open()
        {
            if (IsOpen || _leaving)
                return;

            IsOpen = true;
            _previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;

            _previousLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            ShowMain(true);
            SetCanvasVisible(true);
            _fade = 0f;
            if (_contentGroup != null)
                _contentGroup.alpha = 0f;
            SelectDefault();

            UiButtonFeedback.PlaySound(Infrastructure.Audio.SoundId.UiConfirm);
            Raise(Opened, "Opened");
        }

        /// <summary>Menüyü kapatır: açık ayar/onay pencerelerini kapatır, zaman ölçeğini ve imleci geri yükler.</summary>
        public void Close()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            CloseChildren();
            SetCanvasVisible(false);
            ClearSelection();

            Time.timeScale = _previousTimeScale > 0f ? _previousTimeScale : 1f;
            Cursor.lockState = _previousLock;
            Cursor.visible = _previousCursorVisible;

            Raise(Closed, "Closed");
        }

        /// <summary>
        /// Esc / geri tuşu için: açık onay penceresini ya da ayar penceresini kapatır ve true döner. Alt pencere yoksa
        /// false döner — çağıran bu durumda menüyü kapatabilir (<see cref="Close"/>).
        /// </summary>
        public bool HandleBack()
        {
            if (!IsOpen)
                return false;

            if (_dialog != null)
            {
                _dialog.Cancel();
                return true;
            }

            if (_settingsPanel != null)
            {
                _settingsPanel.Close();
                return true;
            }

            return false;
        }

        /// <summary>Ayar veya onay penceresi açık mı?</summary>
        public bool HasSubPanel => _dialog != null || _settingsPanel != null;

        /// <summary>Açıksa kapatır, kapalıysa açar.</summary>
        public void Toggle()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }

        private void Build(RectTransform root)
        {
            _root = root;

            var dim = UiFactory.Panel(root, new Color(0f, 0f, 0f, 0.55f));
            dim.gameObject.name = "Dim";

            var vignette = UiFactory.Image(root, UiSprites.HorizontalGradient, new Color(0.03f, 0.04f, 0.02f, 0.92f));
            vignette.gameObject.name = "LeftShade";
            UiFactory.SetRect(vignette, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(1100f, 0f));

            _content = UiFactory.CreateRect("Content", root);
            _contentGroup = UiFactory.EnsureCanvasGroup(_content);

            // Sol sütun: başlık + düğmeler.
            var column = UiFactory.CreateRect("Column", _content);
            UiFactory.Anchor(column, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(140f, 0f), new Vector2(520f, 640f));

            var title = UiFactory.Label(column, Loc.Get("pause.title", "DURAKLATILDI"), UiTheme.FontTitle + 8, TextAnchor.LowerLeft, UiTheme.Text, FontStyle.Bold);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(title, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -90f), new Vector2(0f, 0f));
            UiFactory.AddShadow(title, UiTheme.TextShadow, new Vector2(2f, -2f));

            var underline = UiFactory.Image(column, null, UiTheme.Accent);
            UiFactory.SetRect(underline, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(2f, -104f), new Vector2(200f, -98f));

            var subtitle = UiFactory.Label(column, SubtitleText(), UiTheme.FontNormal, TextAnchor.UpperLeft, UiTheme.Khaki);
            UiFactory.SetRect(subtitle, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -150f), new Vector2(0f, -116f));

            var list = UiFactory.VerticalList(column, 14f);
            UiFactory.SetRect(list, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -560f), new Vector2(-60f, -190f));

            _resumeButton = UiFactory.Button(list, Loc.Get("pause.btn.resume", "DEVAM ET"), OnResumeClicked, UiButtonStyle.Primary);
            UiFactory.LayoutSize(_resumeButton, -1f, 64f, 1f);
            _settingsButton = UiFactory.Button(list, Loc.Get("pause.btn.settings", "AYARLAR"), OpenSettings);
            UiFactory.LayoutSize(_settingsButton, -1f, 64f, 1f);
            _menuButton = UiFactory.Button(list, Loc.Get("pause.btn.main_menu", "ANA MENÜ"), ConfirmMainMenu, UiButtonStyle.Danger);
            UiFactory.LayoutSize(_menuButton, -1f, 64f, 1f);

            var hint = UiFactory.Label(column, Loc.Get("pause.esc_hint", "ESC — devam et"), UiTheme.FontSmall, TextAnchor.LowerLeft, UiTheme.TextMuted);
            UiFactory.SetRect(hint, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 30f));

            BuildControls(_content);
        }

        private void BuildControls(RectTransform parent)
        {
            var card = UiFactory.Panel(parent, UiTheme.PanelDark, UiSprites.ChamferRect);
            card.gameObject.name = "Controls";
            UiFactory.Anchor(card, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-120f, 0f), new Vector2(620f, 760f));

            var border = UiFactory.Image(card, UiSprites.GetRoundedRectOutline(UiTheme.CornerRadius), UiTheme.WithAlpha(UiTheme.PanelBorder, 0.7f));
            UiFactory.Stretch(border);

            var list = UiFactory.VerticalList(card, 4f, 0);
            UiFactory.Stretch(list, 32f, 26f, 32f, 26f);

            UiWidgets.Header(list, "KONTROLLER", UiTheme.FontMedium);
            UiFactory.Spacer(list, 6f);

            var rows = Controls.GetLength(0);
            for (var i = 0; i < rows; i++)
            {
                var row = UiFactory.CreateRect("Row", list);
                UiFactory.LayoutSize(row, -1f, 32f, 1f);

                var key = UiFactory.Label(row, Controls[i, 0], UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.Amber, FontStyle.Bold);
                UiFactory.SetRect(key, new Vector2(0f, 0f), new Vector2(0.36f, 1f), Vector2.zero, Vector2.zero);
                key.horizontalOverflow = HorizontalWrapMode.Overflow;

                var action = UiFactory.Label(row, Controls[i, 1], UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.TextDim);
                UiFactory.SetRect(action, new Vector2(0.38f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
                action.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
        }

        private static string SubtitleText()
        {
            return GameSession.Mode == GameMode.Training ? "Atış Poligonu — eğitim" : "Harekât sürüyor — Kuzgun Vadisi";
        }

        private void Update()
        {
            if (!IsOpen)
                return;

            // Başka bir sistem zamanı yeniden başlatmadıkça menü açıkken oyun durur; imleç serbest kalır.
            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible)
                Cursor.visible = true;

            if (_contentGroup != null && _fade < 1f)
            {
                _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / UiTheme.FadeDuration);
                _contentGroup.alpha = _fade;
            }
        }

        private void OnResumeClicked()
        {
            if (_onResume != null)
            {
                try
                {
                    _onResume();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            // Geri çağrı menüyü kapatmadıysa (ya da yoksa) kendimiz kapatırız.
            if (IsOpen)
                Close();
        }

        private void OpenSettings()
        {
            if (_settingsPanel != null)
                return;

            ShowMain(false);
            _settingsPanel = SettingsPanel.Create(_root, _settings, OnSettingsClosed);
            if (_settingsPanel != null)
                _settingsPanel.Applied += OnSettingsApplied;
            else
                ShowMain(true);
        }

        private void OnSettingsApplied(GameSettings settings)
        {
            Raise(SettingsApplied, settings);
        }

        private void OnSettingsClosed()
        {
            if (_settingsPanel != null)
                _settingsPanel.Applied -= OnSettingsApplied;
            _settingsPanel = null;

            if (IsOpen)
            {
                ShowMain(true);
                SelectDefault();
            }
        }

        private void ConfirmMainMenu()
        {
            if (_dialog != null)
                return;

            var message = GameSession.Mode == GameMode.Training
                ? "Atış poligonundan ayrılıp karargâha dönülecek."
                : "Harekâttan çekilirsen bu maçtaki ilerlemen kaydedilmez. Karargâha dönmek istediğine emin misin?";
            _dialog = MenuDialog.Show(_root, "Ana menüye dön", message, "ANA MENÜ", GoToMainMenu, "VAZGEÇ", OnDialogCancelled, true);
            SetMainInteractable(false);
        }

        private void OnDialogCancelled()
        {
            _dialog = null;
            SetMainInteractable(true);
            SelectDefault();
        }

        private void SetMainInteractable(bool interactable)
        {
            if (_contentGroup != null)
                _contentGroup.interactable = interactable;
        }

        private void GoToMainMenu()
        {
            _dialog = null;
            if (_leaving)
                return;

            _leaving = true;
            Time.timeScale = 1f;
            _previousTimeScale = 1f;

            try
            {
                if (_onMainMenu != null)
                    _onMainMenu();
                else
                    GameSession.ReturnToMainMenu();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _leaving = false;
            }
        }

        private void ShowMain(bool visible)
        {
            if (_content != null)
                UiFactory.SetVisible(_content, visible);
        }

        private void CloseChildren()
        {
            if (_dialog != null)
            {
                _dialog.Dismiss();
                _dialog = null;
                SetMainInteractable(true);
            }

            if (_settingsPanel != null)
            {
                var panel = _settingsPanel;
                panel.Applied -= OnSettingsApplied;
                _settingsPanel = null;
                panel.Close();
            }
        }

        private void SetCanvasVisible(bool visible)
        {
            if (_canvas != null)
                _canvas.enabled = visible;
            if (_raycaster != null)
                _raycaster.enabled = visible;
        }

        private void SelectDefault()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem != null && _resumeButton != null && _resumeButton.isActiveAndEnabled)
                eventSystem.SetSelectedGameObject(_resumeButton.gameObject);
        }

        private void ClearSelection()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != null &&
                eventSystem.currentSelectedGameObject.transform.IsChildOf(transform))
                eventSystem.SetSelectedGameObject(null);
        }

        private static void Raise(Action action, string context)
        {
            if (action == null)
                return;
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogError("[PauseMenu] " + context + " olayı hata verdi: " + e.Message);
                Debug.LogException(e);
            }
        }

        private static void Raise(Action<GameSettings> action, GameSettings settings)
        {
            if (action == null)
                return;
            try
            {
                action(settings);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private UnityEngine.UI.Button _settingsButton;
        private UnityEngine.UI.Button _menuButton;

        private void OnEnable() => Loc.LanguageChanged += OnLanguageChanged;
        private void OnDisable() => Loc.LanguageChanged -= OnLanguageChanged;

        private void OnLanguageChanged(string code)
        {
            UiFactory.SetButtonLabel(_resumeButton, Loc.Get("pause.btn.resume", "DEVAM ET"));
            UiFactory.SetButtonLabel(_settingsButton, Loc.Get("pause.btn.settings", "AYARLAR"));
            UiFactory.SetButtonLabel(_menuButton, Loc.Get("pause.btn.main_menu", "ANA MENÜ"));
        }

        private void OnDestroy()
        {
            // Sahne değişirken açık kalmışsa oyun donuk kalmasın.
            if (IsOpen && !_leaving)
                Time.timeScale = _previousTimeScale > 0f ? _previousTimeScale : 1f;
            IsOpen = false;
            Opened = null;
            Closed = null;
            SettingsApplied = null;
        }
    }
}
