using System;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Presentation.Bootstrap;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Ana menü arayüzü (MainMenuBootstrap ekler; sahneye elle de konabilir). Solda "HAREKÂT" başlığı ve
    /// "Tim Battle Royale — Kuzgun Vadisi" alt başlığı, düğmeler: HAREKÂTA KATIL (kurulum penceresi), ATIŞ POLİGONU,
    /// KARİYER, AYARLAR, ÇIKIŞ (onaylı). Sağ altta rütbe kartı (apolet, ad, TP çubuğu — tıklanınca kariyer) ve son
    /// harekât özeti. Menü müziği çalar, imleç serbesttir. Esc / gamepad B en üstteki pencereyi kapatır; pencere yoksa
    /// çıkış onayı açar. Açık pencereye göre <see cref="MenuBackdrop"/> kamerası kadraj değiştirir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        /// <summary>Menü tuvalinin çizim sırası.</summary>
        public const int SortOrder = 50;

        private const float MusicVolume = 0.7f;
        private const float ButtonHeight = 66f;

        private const string HintJoin = "Timini kur ve Kuzgun Vadisi'ne intikal et. Son ayakta kalan tim kazanır.";
        private const string HintTraining = "Tüm silahlarla sınırsız cephane — hedeflere atış yaparak ısın.";
        private const string HintCareer = "Rütben, tecrübe puanın ve harekât istatistiklerin.";
        private const string HintSettings = "Fare hassasiyeti, görüş alanı, grafik kalitesi ve ses.";
        private const string HintExit = "Karargâhtan ayrıl ve oyunu kapat.";

        private Canvas _canvas;
        private RectTransform _root;
        private RectTransform _main;
        private CanvasGroup _mainGroup;
        private bool _mainVisible = true;
        private bool _introDone;

        private Button _joinButton;
        private Button _trainingButton;
        private Button _careerButton;
        private Button _settingsButton;
        private Button _exitButton;
        private Button _lastFocus;
        private Text _hint;
        private Text _status;
        private float _statusUntil;

        private RectTransform _profileInsignia;
        private MilitaryRank _profileRank = (MilitaryRank)(-1);
        private Text _profileName;
        private Text _profileRankText;
        private Text _profileXp;
        private UiProgressBar _profileBar;

        private OperationSetupPanel _setup;
        private CareerPanel _careerPanel;
        private SettingsPanel _settingsPanel;
        private MenuDialog _dialog;

        private CareerStatsService _career;
        private SettingsService _settings;
        private bool _leaving;
        private bool _built;

        /// <summary>Bir sahne yüklemesi başlatıldı mı (menü kilitli)?</summary>
        public bool IsLeaving => _leaving;

        /// <summary>Üstte bir pencere (kurulum, kariyer, ayarlar, onay) açık mı?</summary>
        public bool IsPanelOpen => _setup != null || _careerPanel != null || _settingsPanel != null || _dialog != null;

        /// <summary>Menü tuvali.</summary>
        public Canvas Canvas => _canvas;

        // ------------------------------------------------------------------ Yaşam döngüsü

        private void Awake()
        {
            try
            {
                GameSession.EnsureInitialized();
                _settings = GameSession.Settings;
                _career = GameSession.Career;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            Build();
            if (_career != null)
                _career.Changed += OnCareerChanged;
            if (_settings != null)
                _settings.Changed += OnSettingsChanged;
        }

        private void Start()
        {
            UiFactory.SetCursorFree(true);
            StartMusic();
            Select(_joinButton);
        }

        private void Update()
        {
            if (_mainGroup != null)
            {
                // Açılışta yavaş belirir; pencere açılınca menü sütunu solar (pencereyle üst üste binmesin).
                var target = _mainVisible ? 1f : 0f;
                var alpha = _mainGroup.alpha;
                if (!Mathf.Approximately(alpha, target))
                {
                    var duration = _introDone ? 0.22f : 0.6f;
                    _mainGroup.alpha = Mathf.MoveTowards(alpha, target, Time.unscaledDeltaTime / duration);
                }
                else if (target >= 1f)
                {
                    _introDone = true;
                }
            }

            if (_status != null && _statusUntil > 0f && Time.unscaledTime > _statusUntil)
            {
                _statusUntil = 0f;
                _status.text = string.Empty;
            }

            if (!_leaving && BackPressed())
                Back();
        }

        private void OnDestroy()
        {
            if (_career != null)
                _career.Changed -= OnCareerChanged;
            if (_settings != null)
                _settings.Changed -= OnSettingsChanged;
        }

        // ------------------------------------------------------------------ Genel eylemler

        /// <summary>Harekât kurulum penceresini açar.</summary>
        public void OpenOperationSetup()
        {
            if (_leaving || IsPanelOpen || _root == null)
                return;

            _lastFocus = _joinButton;
            _setup = TryCreate(() => OperationSetupPanel.Create(_root, _settings, OnSetupStart, OnSetupClosed), "Harekât kurulumu");
            if (_setup != null)
                OnPanelOpened(MenuBackdrop.Shot.Setup);
        }

        /// <summary>Atış poligonunu yükler.</summary>
        public void StartTraining()
        {
            if (_leaving || IsPanelOpen)
                return;

            BeginLeaving();
            try
            {
                GameSession.StartTraining();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            CheckLoadStarted();
        }

        /// <summary>Kariyer penceresini açar.</summary>
        public void OpenCareer()
        {
            if (_leaving || IsPanelOpen || _root == null)
                return;

            _lastFocus = _careerButton;
            _careerPanel = TryCreate(() => CareerPanel.Create(_root, _career, OnCareerClosed), "Kariyer");
            if (_careerPanel != null)
                OnPanelOpened(MenuBackdrop.Shot.Career);
        }

        /// <summary>Ayarlar penceresini açar.</summary>
        public void OpenSettings()
        {
            if (_leaving || IsPanelOpen || _root == null)
                return;

            _lastFocus = _settingsButton;
            _settingsPanel = TryCreate(() => SettingsPanel.Create(_root, _settings, OnSettingsClosed), "Ayarlar");
            if (_settingsPanel != null)
                OnPanelOpened(MenuBackdrop.Shot.Settings);
        }

        /// <summary>Çıkış onayı açar.</summary>
        public void RequestQuit()
        {
            if (_leaving || IsPanelOpen || _root == null)
                return;

            _lastFocus = _exitButton;
            _dialog = TryCreate(() => MenuDialog.Show(_root, "Oyundan çık", "Karargâhtan ayrılıp oyunu kapatmak istediğine emin misin?", "ÇIKIŞ",
                QuitApplication, "VAZGEÇ", OnDialogCancelled, true), "Çıkış onayı");
            if (_dialog != null)
                SetMainInteractable(false);
        }

        /// <summary>Esc / geri: en üstteki pencereyi kapatır; pencere yoksa çıkış onayı açar.</summary>
        public void Back()
        {
            if (_leaving)
                return;

            if (_dialog != null)
            {
                _dialog.Cancel();
                return;
            }

            if (_settingsPanel != null)
            {
                _settingsPanel.Close();
                return;
            }

            if (_careerPanel != null)
            {
                _careerPanel.Back();
                return;
            }

            if (_setup != null)
            {
                if (!_setup.IsEditingText)
                    _setup.Close();
                return;
            }

            RequestQuit();
        }

        // ------------------------------------------------------------------ Kurulum

        private void Build()
        {
            if (_built)
                return;
            _built = true;

            _canvas = UiFactory.CreateCanvas("[Ana Menü Arayüzü]", SortOrder);
            _canvas.transform.SetParent(transform, false);
            _root = (RectTransform)_canvas.transform;

            BuildBackgroundShades(_root);

            _main = UiFactory.CreateRect("Main", _root);
            _mainGroup = UiFactory.EnsureCanvasGroup(_main);
            _mainGroup.alpha = 0f;

            BuildTitle(_main);
            BuildButtons(_main);
            BuildProfileCard(_main);
            BuildLastOperation(_main);
            BuildFooter(_main);
            RefreshProfile();
        }

        private static void BuildBackgroundShades(RectTransform root)
        {
            var vignette = UiFactory.Image(root, UiSprites.Vignette, new Color(0f, 0f, 0f, 0.5f));
            vignette.gameObject.name = "Vignette";
            UiFactory.Stretch(vignette);

            var left = UiFactory.Image(root, UiSprites.HorizontalGradient, new Color(0.02f, 0.03f, 0.02f, 0.9f));
            left.gameObject.name = "LeftShade";
            UiFactory.SetRect(left, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(1150f, 0f));

            var bottom = UiFactory.Image(root, UiSprites.VerticalGradient, new Color(0f, 0f, 0f, 0.65f));
            bottom.gameObject.name = "BottomShade";
            UiFactory.SetRect(bottom, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 280f));

            var stripe = UiFactory.Image(root, null, UiTheme.Accent);
            stripe.gameObject.name = "TopStripe";
            UiFactory.SetRect(stripe, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -5f), Vector2.zero);
        }

        private static void BuildTitle(RectTransform parent)
        {
            var title = UiFactory.Label(parent, "HAREKÂT", 132, TextAnchor.LowerLeft, UiTheme.Text, FontStyle.Bold);
            title.gameObject.name = "Title";
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.Anchor(title, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(104f, -60f), new Vector2(900f, 150f));
            UiFactory.AddShadow(title, new Color(0f, 0f, 0f, 0.8f), new Vector2(4f, -4f));

            var underline = UiFactory.Image(parent, null, UiTheme.Accent);
            underline.gameObject.name = "Underline";
            UiFactory.Anchor(underline, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(112f, -222f), new Vector2(300f, 8f));

            var flag = UiFactory.Image(parent, UiSprites.TurkishFlag, Color.white);
            flag.gameObject.name = "Flag";
            flag.preserveAspect = true;
            UiFactory.Anchor(flag, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(112f, -244f), new Vector2(57f, 38f));

            var subtitle = UiFactory.Label(parent, "Tim Battle Royale — Kuzgun Vadisi", UiTheme.FontLarge, TextAnchor.MiddleLeft, UiTheme.Khaki, FontStyle.Bold);
            subtitle.gameObject.name = "Subtitle";
            subtitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Anchor(subtitle, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(184f, -240f), new Vector2(900f, 46f));
            UiFactory.AddShadow(subtitle, UiTheme.TextShadow, new Vector2(2f, -2f));
        }

        private void BuildButtons(RectTransform parent)
        {
            var column = UiFactory.VerticalList(parent, 14f);
            column.gameObject.name = "Buttons";
            UiFactory.Anchor(column, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(110f, 150f), new Vector2(470f, 5f * ButtonHeight + 4f * 14f));

            _joinButton = MenuButton(column, "HAREKÂTA KATIL", OpenOperationSetup, UiButtonStyle.Primary, HintJoin);
            _trainingButton = MenuButton(column, "ATIŞ POLİGONU", StartTraining, UiButtonStyle.Default, HintTraining);
            _careerButton = MenuButton(column, "KARİYER", OpenCareer, UiButtonStyle.Default, HintCareer);
            _settingsButton = MenuButton(column, "AYARLAR", OpenSettings, UiButtonStyle.Default, HintSettings);
            _exitButton = MenuButton(column, "ÇIKIŞ", RequestQuit, UiButtonStyle.Danger, HintExit);

            _hint = UiFactory.Label(parent, HintJoin, UiTheme.FontSmall, TextAnchor.UpperLeft, UiTheme.TextDim);
            _hint.gameObject.name = "Hint";
            UiFactory.Anchor(_hint, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(112f, 150f - (5f * ButtonHeight + 4f * 14f) - 18f),
                new Vector2(560f, 60f));

            _status = UiFactory.Label(parent, string.Empty, UiTheme.FontSmall, TextAnchor.UpperLeft, UiTheme.Danger, FontStyle.Bold);
            _status.gameObject.name = "Status";
            UiFactory.Anchor(_status, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(112f, 150f - (5f * ButtonHeight + 4f * 14f) - 80f),
                new Vector2(700f, 34f));
        }

        private Button MenuButton(Transform parent, string label, Action onClick, UiButtonStyle style, string hint)
        {
            var button = UiFactory.Button(parent, label, onClick, style);
            UiFactory.LayoutSize(button, -1f, ButtonHeight, 1f);

            var text = UiFactory.GetButtonLabel(button);
            if (text != null)
            {
                text.alignment = TextAnchor.MiddleLeft;
                text.fontSize = UiTheme.FontLarge - 4;
                UiFactory.Stretch(text, 30f, 0f, 12f, 0f);
            }

            var chevron = UiFactory.Image(button.transform, UiSprites.Chevron, UiTheme.WithAlpha(UiTheme.Text, 0.55f));
            chevron.gameObject.name = "Chevron";
            chevron.rectTransform.localEulerAngles = new Vector3(0f, 0f, -90f);
            UiFactory.Anchor(chevron, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-30f, 0f), new Vector2(20f, 20f));

            MenuButtonHint.Attach(button, hint, SetHint);
            return button;
        }

        private void BuildProfileCard(RectTransform parent)
        {
            var card = UiFactory.Panel(parent, UiTheme.PanelDark, UiSprites.ChamferRect);
            card.gameObject.name = "ProfileCard";
            UiFactory.Anchor(card, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 74f), new Vector2(560f, 156f));

            var border = UiFactory.Image(card, UiSprites.GetRoundedRectOutline(UiTheme.CornerRadius), UiTheme.WithAlpha(MenuRankInsignia.Gold, 0.5f));
            UiFactory.Stretch(border);

            // Kartın tamamı tıklanabilir: kariyeri açar.
            var cardImage = card.GetComponent<Image>();
            if (cardImage != null)
            {
                cardImage.raycastTarget = true;
                var button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = cardImage;
                button.transition = Selectable.Transition.ColorTint;
                var colors = ColorBlock.defaultColorBlock;
                colors.normalColor = new Color(0.82f, 0.82f, 0.82f, 1f);
                colors.highlightedColor = Color.white;
                colors.selectedColor = new Color(0.95f, 0.95f, 0.92f, 1f);
                colors.pressedColor = new Color(0.68f, 0.68f, 0.68f, 1f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.08f;
                button.colors = colors;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(() =>
                {
                    UiWidgets.PlaySound(SoundId.UiClick);
                    OpenCareer();
                });
                MenuButtonHint.Attach(button, HintCareer, SetHint);
            }

            var holder = UiFactory.CreateRect("Insignia", card);
            UiFactory.Anchor(holder, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 14f), new Vector2(150f, 60f));
            _profileInsignia = MenuRankInsignia.Create(holder, MilitaryRank.Er, 56f);

            _profileName = UiFactory.Label(card, string.Empty, UiTheme.FontMedium, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold);
            _profileName.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_profileName, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(190f, -54f), new Vector2(-20f, -16f));

            _profileRankText = UiFactory.Label(card, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.Khaki);
            _profileRankText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_profileRankText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(190f, -84f), new Vector2(-20f, -56f));

            _profileBar = UiFactory.ProgressBar(card, MenuRankInsignia.Gold, UiTheme.Track);
            _profileBar.TrailEnabled = false;
            UiFactory.SetRect(_profileBar, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(190f, 40f), new Vector2(-20f, 52f));

            _profileXp = UiFactory.Label(card, string.Empty, UiTheme.FontTiny, TextAnchor.MiddleLeft, UiTheme.TextDim);
            _profileXp.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_profileXp, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(190f, 12f), new Vector2(-20f, 36f));

            var caption = UiFactory.Label(card, "KARİYER ›", UiTheme.FontTiny, TextAnchor.MiddleRight, UiTheme.TextMuted, FontStyle.Bold);
            UiFactory.SetRect(caption, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(190f, 12f), new Vector2(-20f, 36f));
        }

        private void BuildLastOperation(RectTransform parent)
        {
            if (!GameSession.LastResult.HasValue)
                return;

            var result = GameSession.LastResult.Value;
            var card = UiFactory.Panel(parent, UiTheme.WithAlpha(UiTheme.PanelDark, 0.85f), UiSprites.ChamferRect);
            card.gameObject.name = "LastOperation";
            UiFactory.Anchor(card, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 242f), new Vector2(560f, 92f));

            var accent = UiFactory.Image(card, null, result.IsWinner ? MenuRankInsignia.Gold : UiTheme.Accent);
            UiFactory.SetRect(accent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 8f), new Vector2(5f, -8f));

            var caption = UiFactory.Label(card, "SON HAREKÂT", UiTheme.FontTiny, TextAnchor.MiddleLeft, UiTheme.TextMuted, FontStyle.Bold);
            UiFactory.SetRect(caption, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -36f), new Vector2(-20f, -10f));

            var headline = result.IsWinner ? "ZAFER" : "TİM SIRALAMASI";
            var placement = result.TeamPlacement > 0 ? result.TeamPlacement : result.Placement;
            var total = result.TeamCount > 0 ? result.TeamCount : result.TotalPlayers;
            var body = headline + "  " + MenuText.FormatPlacement(result.IsWinner ? 1 : placement, total) +
                       "   ·   " + result.Kills + " etkisiz   ·   " + MenuText.FormatDuration(result.SurvivalSeconds);
            var text = UiFactory.Label(card, body, UiTheme.FontNormal, TextAnchor.MiddleLeft, result.IsWinner ? MenuRankInsignia.Gold : UiTheme.Text, FontStyle.Bold);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(text, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 10f), new Vector2(-20f, -34f));
        }

        private static void BuildFooter(RectTransform parent)
        {
            var version = UnityEngine.Application.version;
            var footer = UiFactory.Label(parent,
                "Mavi / Kırmızı kuvvetler harekât tatbikatı" + (string.IsNullOrEmpty(version) ? string.Empty : "   ·   sürüm " + version),
                UiTheme.FontTiny, TextAnchor.MiddleLeft, UiTheme.TextMuted);
            footer.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Anchor(footer, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(112f, 30f), new Vector2(900f, 24f));

            var keys = UiFactory.Label(parent, "ESC  geri / çıkış", UiTheme.FontTiny, TextAnchor.MiddleLeft, UiTheme.TextMuted);
            keys.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Anchor(keys, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(112f, 56f), new Vector2(400f, 24f));
        }

        // ------------------------------------------------------------------ Profil

        private void RefreshProfile()
        {
            var stats = _career != null && _career.Current != null ? _career.Current : new CareerStats();
            var xp = Mathf.Max(0, stats.Experience);
            var rank = RankCatalog.RankForExperience(xp);

            if (_profileInsignia != null && rank != _profileRank)
                MenuRankInsignia.Rebuild(_profileInsignia, rank);
            _profileRank = rank;

            var name = SettingsService.DefaultPlayerName;
            if (_settings != null && _settings.Current != null)
                name = SettingsService.SanitizeName(_settings.Current.PlayerName);

            if (_profileName != null)
                _profileName.text = RankCatalog.FormatName(rank, name);
            if (_profileRankText != null)
                _profileRankText.text = RankCatalog.GetName(rank) + "  ·  " + RankCatalog.GetCategory(rank);

            var hasNext = RankCatalog.TryGetNextRank(rank, out var next);
            if (_profileBar != null)
                _profileBar.SetValue(RankCatalog.ProgressToNextRank(xp), true);
            if (_profileXp != null)
            {
                _profileXp.text = hasNext
                    ? MenuText.FormatThousands(xp) + " / " + MenuText.FormatThousands(RankCatalog.RequiredExperience(next)) + " TP  ·  " + RankCatalog.GetShortName(next)
                    : MenuText.FormatThousands(xp) + " TP  ·  en yüksek rütbe";
            }
        }

        private void OnCareerChanged(CareerStats stats) => RefreshProfile();

        private void OnSettingsChanged(GameSettings settings) => RefreshProfile();

        // ------------------------------------------------------------------ Pencere geri çağrıları

        private void OnPanelOpened(MenuBackdrop.Shot shot)
        {
            SetMainInteractable(false);
            var backdrop = MenuBackdrop.Current;
            if (backdrop != null)
                backdrop.SetShot(shot);
        }

        private void OnPanelClosed()
        {
            if (_leaving)
                return;

            SetMainInteractable(true);
            var backdrop = MenuBackdrop.Current;
            if (backdrop != null)
                backdrop.SetShot(MenuBackdrop.Shot.Main);
            Select(_lastFocus != null ? _lastFocus : _joinButton);
        }

        private void OnSetupStart(GameSettings settings)
        {
            BeginLeaving();
            try
            {
                GameSession.StartOperation();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            CheckLoadStarted();
        }

        private void OnSetupClosed()
        {
            _setup = null;
            OnPanelClosed();
        }

        private void OnCareerClosed()
        {
            _careerPanel = null;
            OnPanelClosed();
        }

        private void OnSettingsClosed()
        {
            _settingsPanel = null;
            OnPanelClosed();
        }

        private void OnDialogCancelled()
        {
            _dialog = null;
            OnPanelClosed();
        }

        // ------------------------------------------------------------------ Sahne geçişi / çıkış

        private void BeginLeaving()
        {
            _leaving = true;
            SetMainInteractable(false);
            try
            {
                GameAudio.SetAmbience(SoundId.None, 0f);
            }
            catch (Exception)
            {
                // Ses sistemi yoksa önemli değil.
            }
        }

        /// <summary>Yükleme başlamadıysa (sahne Build Settings'te yoksa) menüyü yeniden kullanılabilir yapar.</summary>
        private void CheckLoadStarted()
        {
            if (GameSession.IsLoading)
                return;

            // Eşzamanlı yükleme yolunda sahne zaten değişiyor olabilir; bu durumda bu nesne birazdan yok olur.
            _leaving = false;
            if (_setup != null)
            {
                var setup = _setup;
                _setup = null;
                setup.gameObject.SetActive(false);
                UiFactory.DestroySafe(setup.gameObject);
            }

            OnPanelClosed();
            StartMusic();
            ShowStatus("Harekât sahnesi yüklenemedi — Build Settings'i kontrol et.");
        }

        private void QuitApplication()
        {
            _dialog = null;
            _leaving = true;
            SetMainInteractable(false);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ Yardımcılar

        private static T TryCreate<T>(Func<T> create, string context) where T : class
        {
            try
            {
                return create();
            }
            catch (Exception e)
            {
                Debug.LogError("[MainMenu] " + context + " açılamadı: " + e.Message);
                Debug.LogException(e);
                return null;
            }
        }

        private void StartMusic()
        {
            // Başsız (batch / dedicated server) çalışmada ses yok.
            if (UnityEngine.Application.isBatchMode)
                return;

            try
            {
                if (!GameAudio.IsInitialized)
                    GameAudio.Initialize();
                GameAudio.SetAmbience(SoundId.MenuMusic, MusicVolume);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MainMenu] Menü müziği başlatılamadı: " + e.Message);
            }
        }

        private void SetMainInteractable(bool interactable)
        {
            _mainVisible = interactable;
            if (_mainGroup != null)
            {
                _mainGroup.interactable = interactable;
                _mainGroup.blocksRaycasts = interactable;
            }
        }

        private void SetHint(string hint)
        {
            if (_hint != null && !string.Equals(_hint.text, hint, StringComparison.Ordinal))
                _hint.text = hint ?? string.Empty;
        }

        private void ShowStatus(string message)
        {
            if (_status == null)
                return;
            _status.text = message ?? string.Empty;
            _statusUntil = Time.unscaledTime + 6f;
        }

        private static void Select(Selectable selectable)
        {
            if (selectable == null || !selectable.isActiveAndEnabled)
                return;
            var eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.alreadySelecting)
                return;
            eventSystem.SetSelectedGameObject(selectable.gameObject);
        }

        private static bool BackPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                return true;

            var gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonEast.wasPressedThisFrame;
        }
    }
}
