using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Presentation.Bootstrap;
using Project.Infrastructure.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Ana menü (lobi) arayüzü: solda büyük "HAREKÂT" başlığı ve animasyonlu gezinme (OYNA, TİM, DONANIM, SEZON,
    /// TEKRARLAR, AYARLAR, ÇIKIŞ + diğer <see cref="ExtraButtons"/>), sağda yumuşak geçişli sayfalar (OYNA: mod kartı
    /// atlıkarıncası ve harita seçimi; TİM: rütbe, kariyer ve rol; DONANIM: dönen 3B silah önizlemesi), altta haber bandı,
    /// sağ altta profil kartı ve sürüm. Arkada <see cref="MenuBackdrop"/> (T-70 helipadı, Kirpi, tim) kadraj değiştirir.
    /// Menü müziği çalar, imleç serbesttir. Esc / gamepad B önce açık pencereyi, sonra alt adımı/sayfayı kapatır; en sonda
    /// çıkış onayı açar. Kurulum, kariyer, ayarlar ve onay pencereleri eskisi gibi üstte açılır.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        /// <summary>Menü tuvalinin çizim sırası.</summary>
        public const int SortOrder = 50;

        private const float MusicVolume = 0.7f;
        private const float NavHeight = 54f;
        private const float NavTop = 224f;
        private const string ConvoyLabel = "KONVOY KORUMA";
        private const string HostageLabel = "REHİNE KURTARMA";
        private const string SeasonLabel = "SEZON";
        private const string ReplayLabel = "TEKRARLAR";

        private const string PageHome = "home";
        private const string PagePlay = "play";
        private const string PageTeam = "team";
        private const string PageLoadout = "loadout";

        private Canvas _canvas;
        private Lobby.LobbyShell _lobby;
        private RectTransform _root;
        private RectTransform _main;
        private CanvasGroup _mainGroup;
        private bool _mainVisible = true;
        private bool _introDone;

        private MenuPageHost _pages;
        private MenuPlayPage _playPage;
        private MenuNavItem _navPlay;
        private MenuNavItem _navTeam;
        private MenuNavItem _navLoadout;
        private MenuNavItem _navSettings;
        private MenuNavItem _navExit;
        private readonly List<(MenuNavItem item, string key, string fallback)> _navLabels = new List<(MenuNavItem, string, string)>(8);
        private Button _lastFocus;
        private Text _status;
        private float _statusUntil;
        private Image _vignette;
        private RectTransform _sweep;

        private RectTransform _profileInsignia;
        private MilitaryRank _profileRank = (MilitaryRank)(-1);
        private Text _profileName;
        private Text _profileRankText;
        private Text _profileXp;
        private UiProgressBar _profileBar;
        private Texture2D _playerFace;

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
            Select(_navPlay != null ? _navPlay.Button : null);
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

            AnimateBackground();

            if (_status != null && _statusUntil > 0f && Time.unscaledTime > _statusUntil)
            {
                _statusUntil = 0f;
                _status.text = string.Empty;
            }

            // Kozmetik paneli kendi Esc'ini işler; aynı basış ana menü çıkış sorusunu açmasın.
            if (!_leaving && BackPressed() && !OverlayState.EscapeOwnedByTransient)
                Back();
        }

        private void OnEnable() => Loc.LanguageChanged += OnLanguageChanged;
        private void OnDisable() => Loc.LanguageChanged -= OnLanguageChanged;

        private void OnLanguageChanged(string code)
        {
            for (var i = 0; i < _navLabels.Count; i++)
            {
                var entry = _navLabels[i];
                if (entry.item != null && entry.item.Label != null)
                    entry.item.Label.text = Loc.Get(entry.key, entry.fallback);
            }

            RefreshProfile();
        }

        private void OnDestroy()
        {
            if (_career != null)
                _career.Changed -= OnCareerChanged;
            if (_settings != null)
                _settings.Changed -= OnSettingsChanged;
            if (_playerFace != null)
                Destroy(_playerFace);
        }

        // ------------------------------------------------------------------ Genel eylemler

        /// <summary>Harekât kurulum penceresini açar.</summary>
        public void OpenOperationSetup()
        {
            if (_leaving || IsPanelOpen || _root == null)
                return;

            _lastFocus = _navPlay != null ? _navPlay.Button : null;
            _setup = TryCreate(() => OperationSetupPanel.Create(_root, _settings, OnSetupStart, OnSetupClosed), "Harekât kurulumu");
            if (_setup != null)
                OnPanelOpened(MenuBackdrop.Shot.Setup);
        }

        /// <summary>Çatışma (hızlı maç) modunu başlatır.</summary>
        public void StartSkirmish()
        {
            if (_leaving || IsPanelOpen)
                return;

            if (Lobby.LobbyFlow.TryRun(StartSkirmish)) return; // lobi akışı (TİM TOPLANIYOR → BRİFİNG → geri sayım), bitince bu çağrı yeniden gelir

            BeginLeaving();
            try
            {
                GameSession.StartSkirmish();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            CheckLoadStarted();
        }

        /// <summary>Atış poligonunu yükler.</summary>
        public void StartTraining() => StartTrainingInternal(false);

        /// <summary>Atış poligonunu eğitim adımlarıyla yükler.</summary>
        public void StartTutorialTraining() => StartTrainingInternal(true);

        private void StartTrainingInternal(bool tutorial)
        {
            if (_leaving || IsPanelOpen)
                return;

            BeginLeaving();
            try
            {
                GameSession.StartTraining(tutorial);
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

            _lastFocus = _navTeam != null ? _navTeam.Button : null;
            _careerPanel = TryCreate(() => CareerPanel.Create(_root, _career, OnCareerClosed), "Kariyer");
            if (_careerPanel != null)
                OnPanelOpened(MenuBackdrop.Shot.Career);
        }

        /// <summary>Ayarlar penceresini açar.</summary>
        public void OpenSettings()
        {
            if (_leaving || IsPanelOpen || _root == null)
                return;

            _lastFocus = _navSettings != null ? _navSettings.Button : null;
            _settingsPanel = TryCreate(() => SettingsPanel.Create(_root, _settings, OnSettingsClosed), "Ayarlar");
            if (_settingsPanel != null)
                OnPanelOpened(MenuBackdrop.Shot.Settings);
        }

        /// <summary>Çıkış onayı açar.</summary>
        public void RequestQuit()
        {
            if (_leaving || IsPanelOpen || _root == null)
                return;

            _lastFocus = _navExit != null ? _navExit.Button : null;
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

            Project.Infrastructure.Audio.UiSounds.Play(Project.Infrastructure.Audio.UiSfx.Back);

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

            if (_pages != null && _pages.Current == PagePlay && _playPage != null && _playPage.HandleBack())
                return;

            if (_pages != null && !string.IsNullOrEmpty(_pages.Current) && _pages.Current != PageHome)
            {
                ShowPage(PageHome);
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

            // AAA lobi kabuğu: üst bar + sekmeler + OYNA düğmesi; mevcut menü ANA ÜSSÜ sekmesine taşınır.
            _lobby = Lobby.LobbyShell.Create(_root, () => ShowPage(PagePlay));
            _lobby.Vignette.transform.SetParent(_root, false);
            _lobby.Vignette.transform.SetAsFirstSibling();

            _main = UiFactory.CreateRect("Main", _lobby.GetTabPanel(0));
            UiFactory.Stretch(_main);
            _mainGroup = UiFactory.EnsureCanvasGroup(_main);
            _mainGroup.alpha = 0f;

            BuildPages(_main);
            BuildNav(_main);
            BuildFooter(_main);
            BuildLobbyTabs();
            _pages.Show(PageHome);
            SetNavActive(null);
            RefreshProfile();
        }

        private void BuildBackgroundShades(RectTransform root)
        {
            var vignette = UiFactory.Image(root, UiSprites.Vignette, new Color(0f, 0f, 0f, 0.5f));
            vignette.gameObject.name = "Vignette";
            vignette.raycastTarget = false;
            UiFactory.Stretch(vignette);
            _vignette = vignette;

            // Düz koyu şerit (rgba 16,18,20,0.86) + sağına yumuşak solma.
            var left = UiFactory.Image(root, null, UiKitTokens.Bg);
            left.gameObject.name = "LeftShade";
            left.raycastTarget = false;
            UiFactory.SetRect(left, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(560f, 0f));
            var leftFade = UiFactory.Image(root, UiSprites.HorizontalGradient, UiTheme.WithAlpha(UiKitTokens.Bg, 0.7f));
            leftFade.gameObject.name = "LeftShadeFade";
            leftFade.raycastTarget = false;
            leftFade.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            UiFactory.SetRect(leftFade, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(560f, 0f), new Vector2(1000f, 0f));

            // Soldan sağa yavaşça süzülen soluk ışık şeridi.
            var sweep = UiFactory.Image(root, UiSprites.HorizontalGradient, new Color(1f, 1f, 1f, 0.012f));
            sweep.gameObject.name = "LightSweep";
            sweep.raycastTarget = false;
            UiFactory.Anchor(sweep, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(420f, 1080f));
            sweep.rectTransform.anchorMax = new Vector2(0f, 1f);
            sweep.rectTransform.sizeDelta = new Vector2(420f, 0f);
            _sweep = sweep.rectTransform;

            var bottom = UiFactory.Image(root, UiSprites.VerticalGradient, new Color(0.063f, 0.071f, 0.078f, 0.8f));
            bottom.gameObject.name = "BottomShade";
            bottom.raycastTarget = false;
            UiFactory.SetRect(bottom, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 280f));

            var stripe = UiFactory.Image(root, null, UiKitTokens.Accent);
            stripe.gameObject.name = "TopStripe";
            stripe.raycastTarget = false;
            UiFactory.SetRect(stripe, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), Vector2.zero);
        }

        private void AnimateBackground()
        {
            var t = Time.unscaledTime;
            if (_vignette != null)
            {
                var c = _vignette.color;
                c.a = MainMenuMotion.VignettePulse(t, 0.5f, 0.07f);
                _vignette.color = c;
            }

            if (_sweep != null)
                _sweep.anchoredPosition = new Vector2(Mathf.Lerp(-300f, 1400f, MainMenuMotion.SweepPosition(t, 38f)), 0f);
        }

        private static void BuildTitle(RectTransform parent)
        {
            var title = UiFactory.Label(parent, "HAREKÂT", 68, TextAnchor.LowerLeft, UiKitTokens.Text, FontStyle.Bold);
            title.gameObject.name = "Title";
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.Anchor(title, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, -40f), new Vector2(440f, 84f));
            UiFactory.AddShadow(title, new Color(0f, 0f, 0f, 0.6f), new Vector2(1f, -1f));

            Sprite emb = null;
            try { emb = EmblemArt.GetEmblemSprite(256); } catch (System.Exception) { }
            if (emb != null) { var ei = UiFactory.Image(parent, emb, Color.white); ei.preserveAspect = true; ei.raycastTarget = false; UiFactory.Anchor(ei, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -34f), new Vector2(64f, 64f)); }

            var underline = UiFactory.Image(parent, null, UiKitTokens.Accent);
            underline.gameObject.name = "Underline";
            UiFactory.Anchor(underline, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(100f, -132f), new Vector2(120f, 3f));

            var flag = UiFactory.Image(parent, UiSprites.TurkishFlag, Color.white);
            flag.gameObject.name = "Flag";
            flag.preserveAspect = true;
            UiFactory.Anchor(flag, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(100f, -148f), new Vector2(36f, 24f));

            var subtitle = UiFactory.Label(parent, Loc.Get("menu.subtitle", "Tim Battle Royale — Kuzgun Vadisi"), UiTheme.FontSmall + 2, TextAnchor.MiddleLeft, UiKitTokens.TextDim, FontStyle.Bold);
            subtitle.gameObject.name = "Subtitle";
            subtitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Anchor(subtitle, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(146f, -146f), new Vector2(380f, 28f));
            UiFactory.AddShadow(subtitle, new Color(0f, 0f, 0f, 0.6f), new Vector2(1f, -1f));
        }

        /// <summary>Ana menü uzantı noktası: Online/Platform assembly'leri buraya (etiket, açıcı) ekler; Transform = menü kökü.</summary>
        public static readonly List<(string label, Action<Transform> open)> ExtraButtons = new List<(string label, Action<Transform> open)>();

        // ------------------------------------------------------------------ Sayfalar

        private void BuildPages(RectTransform parent)
        {
            var area = UiFactory.CreateRect("PageArea", parent);
            UiFactory.SetRect(area, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(660f, 270f), new Vector2(-60f, -120f));
            _pages = area.gameObject.AddComponent<MenuPageHost>();

            _pages.Register(PageHome, BuildHome);
            _pages.Register(PagePlay, rect => _playPage = MenuPlayPage.Create(rect, this), () => _playPage?.OnShown());
            _pages.Register(PageTeam, rect => MenuTeamPage.Create(rect, this));
            _pages.Register(PageLoadout, rect => LoadoutPage.Create(rect));
        }

        private void BuildHome(RectTransform page)
        {
            // Koyu bant: başlık + açıklama parlak gökyüzü üstünde kalmasın.
            var band = UiFactory.Image(page, null, UiKitTokens.ScrimBand);
            band.gameObject.name = "ScrimBand";
            band.raycastTarget = false;
            UiFactory.SetRect(band, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -150f), new Vector2(0f, 0f));
            var bandLine = UiFactory.Image(page, null, UiKitTokens.Accent);
            bandLine.raycastTarget = false;
            UiFactory.SetRect(bandLine, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(-24f, -150f), new Vector2(-22f, 0f));

            var big = UiFactory.Label(page, "HAREKÂTA HAZIR MISIN?", 40, TextAnchor.MiddleLeft, UiKitTokens.Text, FontStyle.Bold);
            big.horizontalOverflow = HorizontalWrapMode.Overflow;
            big.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.SetRect(big, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -76f), new Vector2(0f, -14f));
            UiFactory.AddShadow(big, new Color(0f, 0f, 0f, 0.6f), new Vector2(1f, -1f));

            var sub = UiFactory.Label(page, "Timini kur, Kuzgun Vadisi'ne intikal et. Son ayakta kalan tim kazanır.", UiTheme.FontMedium, TextAnchor.MiddleLeft, UiKitTokens.TextDim);
            UiFactory.SetRect(sub, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -132f), new Vector2(0f, -80f));

            // OYNA burada tekrar edilmez (sol menü + sağ alttaki büyük OYNA yeterli). Kartlar sağ sütunda:
            // ekranın ortası lobi komutanına (sahnenin kahramanı) bırakılır.
            MenuHomeVisuals.BuildMotif(page);
            var column = UiFactory.CreateRect("HomeRightColumn", page);
            column.anchorMin = column.anchorMax = new Vector2(1f, 1f);
            column.pivot = new Vector2(1f, 1f);
            column.anchoredPosition = new Vector2(0f, -170f);
            column.sizeDelta = new Vector2(620f, 560f);
            MenuHomeVisuals.BuildStatStrip(column, 0f);
            MenuHomeVisuals.BuildDailyChallenge(column, -112f);
            MenuHomeVisuals.BuildBottomBar(column);
        }

        /// <summary>Vitrin / Görevler / Ayarlar sekmelerini mevcut akışlara bağlayan düğmelerle doldurur.</summary>
        private void BuildLobbyTabs()
        {
            AddTabButtons(1, ("DONANIM", () => ShowPage(PageLoadout)), ("TİM", () => ShowPage(PageTeam)));
            AddTabButtons(2, ("KARİYER", OpenCareer), ("SEZON", () => InvokeExtra(SeasonLabel, null)), ("TEKRARLAR", () => InvokeExtra(ReplayLabel, null)));
            AddTabButtons(3, ("AYARLAR", OpenSettings), ("ÇIKIŞ", RequestQuit));
        }

        private void AddTabButtons(int tab, params (string label, Action action)[] items)
        {
            var column = UiFactory.VerticalList(_lobby.GetTabPanel(tab), 12f);
            UiFactory.Anchor(column, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, -40f), new Vector2(420f, items.Length * 72f));
            var layout = column.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.childControlHeight = true;
                layout.childForceExpandHeight = false;
            }

            foreach (var item in items)
            {
                var a = item.action;
                var b = UiFactory.Button(column, item.label, () => { if (!_leaving && !IsPanelOpen) a(); }, UiButtonStyle.Primary);
                var le = b.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 60f;
                Lobby.LobbyGlowButton.Attach(b);
            }
        }

        private void ShowPage(string id)
        {
            if (_leaving || IsPanelOpen || _pages == null)
                return;

            if (_lobby != null)
                _lobby.SelectTab(0);

            _pages.Show(id);
            UiPageTransitions.Play(_pages.transform as RectTransform, _pages.RectOf(id), id == PageHome);
            SetNavActive(id);
            var backdrop = MenuBackdrop.Current;
            if (backdrop != null)
                backdrop.SetShot(ShotForPage(id));
        }

        private void SetNavActive(string pageId)
        {
            if (_navPlay != null) _navPlay.Active = pageId == PagePlay;
            if (_navTeam != null) _navTeam.Active = pageId == PageTeam;
            if (_navLoadout != null) _navLoadout.Active = pageId == PageLoadout;
        }

        /// <summary>Kayıtlı ekstra düğmeyi etikete göre bulur (yoksa null).</summary>
        private static Action<Transform> FindExtra(string label)
        {
            for (var i = 0; i < ExtraButtons.Count; i++)
            {
                if (ExtraButtons[i].label == label && ExtraButtons[i].open != null)
                    return ExtraButtons[i].open;
            }

            return null;
        }

        private void InvokeExtra(string label, Button focus)
        {
            if (_leaving || IsPanelOpen || _root == null)
                return;
            var open = FindExtra(label);
            if (open == null)
            {
                ShowStatus(label + " bu sürümde kullanılamıyor.");
                return;
            }

            _lastFocus = focus;
            try
            {
                open(_root);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ShowStatus(label + " açılamadı.");
            }
        }

        /// <summary>
        /// Mod kartından oyun başlatır. br: haritayı seçip harekât kurulumunu açar; skirmish: hızlı maç; convoy/hostage:
        /// kayıtlı ekstra mod; range: atış poligonu (tutorial = eğitimli).
        /// </summary>
        public void LaunchMode(string modeId, string mapId, bool tutorial)
        {
            if (_leaving || IsPanelOpen)
                return;

            if (!string.IsNullOrEmpty(mapId))
                GameSession.SelectedMap = mapId;

            switch (modeId)
            {
                case MenuModeCatalog.BattleRoyale:
                    OpenOperationSetup();
                    break;
                case MenuModeCatalog.Skirmish:
                    StartSkirmish();
                    break;
                case MenuModeCatalog.Convoy:
                    InvokeExtra(ConvoyLabel, _navPlay != null ? _navPlay.Button : null);
                    break;
                case MenuModeCatalog.Hostage:
                    InvokeExtra(HostageLabel, _navPlay != null ? _navPlay.Button : null);
                    break;
                case MenuModeCatalog.Range:
                    StartTrainingInternal(tutorial);
                    break;
            }
        }

        // ------------------------------------------------------------------ Gezinme

        private MenuNavItem AddNav(Transform parent, string key, string fallback, Action onClick, Color color)
        {
            var item = MenuNavItem.Create(parent, Loc.Get(key, fallback), 30, NavHeight, color, onClick);
            _navLabels.Add((item, key, fallback));
            return item;
        }

        private void BuildNav(RectTransform parent)
        {
            var extras = new List<(string label, Action<Transform> open)>();
            for (var i = 0; i < ExtraButtons.Count; i++)
            {
                var e = ExtraButtons[i];
                if (string.IsNullOrEmpty(e.label) || e.open == null)
                    continue;
                if (e.label == SeasonLabel || e.label == ReplayLabel || e.label == ConvoyLabel || e.label == HostageLabel)
                    continue;
                extras.Add(e);
            }

            const int mainRows = 7;
            var column = UiFactory.VerticalList(parent, 4f);
            column.gameObject.name = "Nav";
            UiFactory.Anchor(column, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, -NavTop),
                new Vector2(420f, mainRows * NavHeight + (mainRows - 1) * 4f));
            var layout = column.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.childControlHeight = true;
                layout.childForceExpandHeight = false;
            }

            _navPlay = AddNav(column, "menu.nav.play", "OYNA", () => ShowPage(PagePlay), UiKitTokens.Text);
            _navTeam = AddNav(column, "menu.nav.team", "TİM", () => ShowPage(PageTeam), UiKitTokens.Text);
            _navLoadout = AddNav(column, "menu.nav.loadout", "DONANIM", () => ShowPage(PageLoadout), UiKitTokens.Text);
            MenuNavItem season = null, replay = null;
            season = AddNav(column, "menu.nav.season", SeasonLabel, () => InvokeExtra(SeasonLabel, season.Button), UiKitTokens.Text);
            replay = AddNav(column, "menu.nav.replays", ReplayLabel, () => InvokeExtra(ReplayLabel, replay.Button), UiKitTokens.Text);
            _navSettings = AddNav(column, "menu.btn.settings", "AYARLAR", OpenSettings, UiKitTokens.Text);
            _navExit = AddNav(column, "menu.btn.exit", "ÇIKIŞ", RequestQuit, UiTheme.Danger);

            if (extras.Count > 0)
            {
                var small = UiFactory.VerticalList(parent, 0f);
                small.gameObject.name = "NavExtra";
                var top = -NavTop - (mainRows * NavHeight + (mainRows - 1) * 4f) - 26f;
                UiFactory.Anchor(small, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, top), new Vector2(420f, extras.Count * 32f));
                var smallLayout = small.GetComponent<VerticalLayoutGroup>();
                if (smallLayout != null)
                {
                    smallLayout.childControlHeight = true;
                    smallLayout.childForceExpandHeight = false;
                }

                for (var i = 0; i < extras.Count; i++)
                {
                    var extra = extras[i];
                    MenuNavItem item = null;
                    item = MenuNavItem.Create(small, extra.label, 18, 32f, UiKitTokens.TextDim, () => InvokeExtra(extra.label, item.Button));
                }
            }

            _status = UiFactory.Label(parent, string.Empty, UiTheme.FontSmall, TextAnchor.LowerLeft, UiTheme.Danger, FontStyle.Bold);
            _status.gameObject.name = "Status";
            UiFactory.Anchor(_status, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(104f, 76f), new Vector2(900f, 30f));
        }

        private void BuildProfileCard(RectTransform parent)
        {
            var card = UiKitPanel.Card(parent, UiKitTokens.Bg, 8);
            card.gameObject.name = "ProfileCard";
            UiFactory.Anchor(card, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 60f), new Vector2(600f, 156f));
            UiKitTokens.ApplyHudScale(card);

            var border = UiFactory.Image(card, UiSprites.GetRoundedRectOutline(8), UiKitTokens.Border);
            border.raycastTarget = false;
            UiFactory.Stretch(border);

            _playerFace = LobbyCommanderStand.LoadTexture("Lobby/commander_face.jpg");
            if (_playerFace != null)
            {
                var face = UiFactory.RawImage(card, _playerFace);
                face.gameObject.name = "PlayerFace";
                UiFactory.Anchor(face, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-66f, 0f), new Vector2(108f, 108f));
            }

            // Kartın tamamı tıklanabilir: kariyeri açar.
            var cardImage = card.GetComponent<Image>();
            if (cardImage != null)
            {
                cardImage.raycastTarget = true;
                var button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = cardImage;
                button.transition = Selectable.Transition.ColorTint;
                var colors = ColorBlock.defaultColorBlock;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.0f, 1.0f, 1.0f, 1f);
                colors.selectedColor = Color.white;
                colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.08f;
                button.colors = colors;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(() =>
                {
                    UiWidgets.PlaySound(SoundId.UiClick);
                    ShowPage(PageTeam);
                });
            }

            var holder = UiFactory.CreateRect("Insignia", card);
            UiFactory.Anchor(holder, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 14f), new Vector2(150f, 60f));
            _profileInsignia = MenuRankInsignia.Create(holder, MilitaryRank.Er, 56f);

            // Sütun: x 190 .. (genişlik - 134). Dört ayrı satır, hiçbiri üst üste binmez; uzun metin küçülür.
            const float left = 190f, right = -134f;

            _profileName = UiFactory.Label(card, string.Empty, 24, TextAnchor.MiddleLeft, UiKitTokens.Text, FontStyle.Bold);
            FitOneLine(_profileName, 15, 24);
            UiFactory.SetRect(_profileName, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(left, -48f), new Vector2(right, -14f));

            _profileRankText = UiFactory.Label(card, string.Empty, 17, TextAnchor.MiddleLeft, UiKitTokens.TextDim);
            FitOneLine(_profileRankText, 12, 17);
            UiFactory.SetRect(_profileRankText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(left, -76f), new Vector2(right, -50f));

            _profileBar = UiFactory.ProgressBar(card, MenuRankInsignia.Gold, UiTheme.Track);
            _profileBar.TrailEnabled = false;
            UiFactory.SetRect(_profileBar, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(left, 58f), new Vector2(right, 66f));

            _profileXp = UiFactory.Label(card, string.Empty, 15, TextAnchor.MiddleLeft, UiKitTokens.TextDim);
            FitOneLine(_profileXp, 11, 15);
            UiFactory.SetRect(_profileXp, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(left, 32f), new Vector2(right, 54f));

            var caption = UiFactory.Label(card, Loc.Get("menu.career_card", "KARİYER ›"), 14, TextAnchor.MiddleLeft, UiKitTokens.TextMuted, FontStyle.Bold);
            FitOneLine(caption, 11, 14);
            UiFactory.SetRect(caption, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(left, 8f), new Vector2(right, 30f));
        }

        /// <summary>Tek satır: taşarsa yazı küçülür (en küçük boyuta kadar), sonra kırpılır; asla komşu satıra binmez.</summary>
        private static void FitOneLine(Text t, int min, int max)
        {
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = min;
            t.resizeTextMaxSize = max;
        }

        private void BuildLastOperation(RectTransform parent)
        {
            if (!GameSession.LastResult.HasValue)
                return;

            var result = GameSession.LastResult.Value;
            var card = UiFactory.Panel(parent, UiKitTokens.Bg, UiSprites.ChamferRect);
            card.gameObject.name = "LastOperation";
            UiFactory.Anchor(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -276f), new Vector2(620f, 92f));

            var accent = UiFactory.Image(card, null, result.IsWinner ? MenuRankInsignia.Gold : UiTheme.Accent);
            UiFactory.SetRect(accent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 8f), new Vector2(5f, -8f));

            var caption = UiFactory.Label(card, Loc.Get("menu.last_op", "SON HAREKÂT"), UiTheme.FontTiny, TextAnchor.MiddleLeft, UiTheme.TextMuted, FontStyle.Bold);
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
            var ticker = MenuTicker.Create(parent, 40f);
            UiFactory.SetRect(ticker, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(-440f, 40f));

            var barScrim = UiFactory.Image(parent, null, UiKitTokens.ScrimBand);
            barScrim.gameObject.name = "FooterScrim";
            barScrim.raycastTarget = false;
            UiFactory.SetRect(barScrim, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-440f, 0f), new Vector2(0f, 40f));
            barScrim.transform.SetAsFirstSibling();

            var version = UnityEngine.Application.version;
            var footer = UiFactory.Label(parent, "SÜRÜM " + (string.IsNullOrEmpty(version) ? "0.1" : version), UiTheme.FontSmall, TextAnchor.MiddleRight, UiKitTokens.TextDim, FontStyle.Bold);
            footer.gameObject.name = "Version";
            footer.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Anchor(footer, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 8f), new Vector2(380f, 24f));

            var keys = UiFactory.Label(parent, Loc.Get("menu.esc_hint", "ESC  geri / çıkış"), UiTheme.FontTiny, TextAnchor.MiddleLeft, UiTheme.TextMuted);
            keys.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Anchor(keys, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(104f, 48f), new Vector2(400f, 22f));
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

            var lobbyXp = MenuText.FormatThousands(xp) + " TP";
            if (_lobby != null)
            {
                _lobby.SetStatus(RankCatalog.FormatName(rank, name), lobbyXp);
                _lobby.Card.Set(new Lobby.LobbyPlayerInfo
                {
                    RankName = RankCatalog.GetName(rank),
                    PlayerName = name,
                    Level = (int)rank + 1,
                    XpProgress01 = RankCatalog.ProgressToNextRank(xp),
                    Kills = stats.Kills,
                    Deaths = 0,
                    Wins = stats.Wins,
                    Matches = stats.Matches,
                });
            }

            var hasNext = RankCatalog.TryGetNextRank(rank, out var next);
            if (_profileBar != null)
                _profileBar.SetValue(RankCatalog.ProgressToNextRank(xp), true);
            if (_profileXp != null)
            {
                _profileXp.text = hasNext
                    ? MenuText.FormatThousands(xp) + " / " + MenuText.FormatThousands(RankCatalog.RequiredExperience(next)) + " TP  →  " + RankCatalog.GetShortName(next)
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
                backdrop.SetShot(ShotForPage(_pages != null ? _pages.Current : null));
            Select(_lastFocus != null ? _lastFocus : (_navPlay != null ? _navPlay.Button : null));
        }

        private void OnSetupStart(GameSettings settings)
        {
            if (Lobby.LobbyFlow.TryRun(() => OnSetupStart(settings))) return;
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
                // Eski 16 sn'lik prosedürel menü döngüsü kaldırıldı: tek müzik MenuMusicDirector (MainMenuBootstrap).
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

        private static MenuBackdrop.Shot ShotForPage(string id)
        {
            if (id == PagePlay) return MenuBackdrop.Shot.Play;
            if (id == PageTeam) return MenuBackdrop.Shot.Team;
            if (id == PageLoadout) return MenuBackdrop.Shot.Loadout;
            return MenuBackdrop.Shot.Main;
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
