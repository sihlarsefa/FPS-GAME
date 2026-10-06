using System;
using System.Text;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Localization;
using Project.Presentation.Bootstrap;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// "HAREKÂTA KATIL" kurulum penceresi: oyuncu adı, tim sayısı (2–6, "4 Tim = 40 Asker"), zorluk (Er / Uzman / Komando →
    /// Easy / Normal / Hard) ve intikal (T-70 helikopteri / Kirpi zırhlı araç). Sağda seçimlere göre güncellenen harekât emri.
    /// <para>"HAREKÂTA BAŞLA" seçimleri <see cref="SettingsService.Apply"/> ile kaydeder ve <c>onStart</c> çağırır (çağıran
    /// sahneyi yükler). "GERİ" kaydetmeden kapatır. Ebeveyni karartır; kapanınca kendini yok eder.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OperationSetupPanel : MonoBehaviour
    {
        private const float WindowWidth = 1240f;
        private const float WindowHeight = 800f;
        private const int ChipCount = SettingsService.MaxTeamCount;

        private static readonly StringBuilder Builder = new StringBuilder(512);

        private SettingsService _settings;
        private Action<GameSettings> _onStart;
        private Action _onClose;
        private GameSettings _working;

        private CanvasGroup _group;
        private float _fade;
        private bool _closed;
        private bool _starting;

        private InputField _nameField;
        private Slider _teamSlider;
        private Text _teamValue;
        private Image[] _chips;
        private Text[] _chipLabels;
        private UiOptionSelector _difficulty;
        private UiOptionSelector _insertion;
        private Text _difficultyInfo;
        private Text _insertionInfo;
        private Text _briefing;
        private Text _mapLabel;
        private Text _status;
        private Button _startButton;
        private Button _backButton;

        /// <summary>Pencere açık mı?</summary>
        public bool IsOpen => !_closed;

        /// <summary>Oyuncu adı alanına yazı yazılıyor mu (Esc menüyü kapatmasın diye)?</summary>
        public bool IsEditingText => _nameField != null && _nameField.isFocused;

        /// <summary>Kurulum seçimlerinin çalışma kopyası.</summary>
        public GameSettings Working => _working;

        /// <summary>
        /// Kurulum penceresini oluşturur (ebeveyni doldurur, arkasını karartır).
        /// </summary>
        /// <param name="parent">Tuval veya tam ekran kök.</param>
        /// <param name="settings">Ayar servisi; null ise <see cref="GameSession.Settings"/>.</param>
        /// <param name="onStart">Seçimler kaydedildikten sonra (harekâtı başlatmak için) çağrılır.</param>
        /// <param name="onClose">Pencere vazgeçilerek kapanınca çağrılır.</param>
        public static OperationSetupPanel Create(Transform parent, SettingsService settings, Action<GameSettings> onStart, Action onClose)
        {
            var root = UiFactory.CreateRect("[Harekât Kurulumu]", parent);
            root.SetAsLastSibling();

            var dim = root.gameObject.AddComponent<Image>();
            dim.color = UiTheme.WithAlpha(UiTheme.Overlay, 0.55f);
            dim.raycastTarget = true;

            var panel = root.gameObject.AddComponent<OperationSetupPanel>();
            panel._settings = ResolveSettings(settings);
            panel._onStart = onStart;
            panel._onClose = onClose;
            panel._working = SettingsService.Sanitize(panel._settings != null ? panel._settings.Current : new GameSettings());
            panel._group = UiFactory.EnsureCanvasGroup(root);
            panel._group.alpha = 0f;
            panel.Build(root);
            panel.RefreshAll();
            return panel;
        }

        /// <summary>Seçimleri kaydeder ve harekâtı başlatma geri çağrısını çalıştırır.</summary>
        public void StartOperation()
        {
            if (_closed || _starting)
                return;

            _starting = true;
            if (_nameField != null)
                _working.PlayerName = _nameField.text;

            var sanitized = SettingsService.Sanitize(_working);
            try
            {
                if (_settings != null)
                {
                    var merged = _settings.Current.Clone();
                    merged.TeamCount = sanitized.TeamCount;
                    merged.Difficulty = sanitized.Difficulty;
                    merged.Insertion = sanitized.Insertion;
                    merged.PlayerName = sanitized.PlayerName;
                    _settings.Apply(merged);
                    sanitized = _settings.Current.Clone();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            _working = sanitized;
            SetInteractable(false);
            ShowStatus(Loc.Get("setup.status.ready", "Harekât emri verildi — tim intikale hazırlanıyor..."), UiTheme.Amber);
            UiWidgets.PlaySound(Infrastructure.Audio.SoundId.UiConfirm);

            var callback = _onStart;
            if (callback == null)
                return;

            try
            {
                callback(sanitized);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                // Yükleme başlatılamadıysa pencere yeniden kullanılabilir olsun.
                _starting = false;
                SetInteractable(true);
                ShowStatus(Loc.Get("setup.status.fail", "Harekât başlatılamadı. Tekrar dene."), UiTheme.Danger);
            }
        }

        /// <summary>Kurulumu kaydetmeden kapatır (yükleme başladıysa bir şey yapmaz).</summary>
        public void Close()
        {
            if (_closed || _starting)
                return;

            _closed = true;
            ClearSelection();
            var callback = _onClose;
            _onClose = null;
            _onStart = null;

            gameObject.SetActive(false);
            UiFactory.DestroySafe(gameObject);

            if (callback == null)
                return;
            try
            {
                callback();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static SettingsService ResolveSettings(SettingsService settings)
        {
            if (settings != null)
                return settings;
            try
            {
                GameSession.EnsureInitialized();
                return GameSession.Settings;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        // ------------------------------------------------------------------ Kurulum

        private void Build(RectTransform root)
        {
            var window = UiFactory.Panel(root, UiTheme.Panel, UiSprites.ChamferRect);
            window.gameObject.name = "Window";
            UiFactory.Anchor(window, UiAnchor.Center, Vector2.zero, new Vector2(WindowWidth, WindowHeight));

            var border = UiFactory.Image(window, UiSprites.GetRoundedRectOutline(UiTheme.CornerRadius), UiTheme.PanelBorder);
            UiFactory.Stretch(border);

            var stripe = UiFactory.Image(window, null, UiTheme.Accent);
            stripe.gameObject.name = "Stripe";
            UiFactory.SetRect(stripe, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f), Vector2.zero);

            var title = UiFactory.Label(window, Loc.Get("setup.title", "HAREKÂT KURULUMU"), UiTheme.FontTitle, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(title, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -96f), new Vector2(-40f, -22f));
            UiFactory.AddShadow(title, UiTheme.TextShadow, new Vector2(2f, -2f));

            var map = UiFactory.Label(window, string.Empty, UiTheme.FontNormal, TextAnchor.MiddleRight, UiTheme.Khaki, FontStyle.Bold);
            map.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(map, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -96f), new Vector2(-40f, -22f));
            _mapLabel = map;
            RefreshMapLabel();

            BuildForm(window);
            BuildBriefing(window);

            // Alt çubuk.
            _status = UiFactory.Label(window, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.TextMuted);
            UiFactory.SetRect(_status, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 26f), new Vector2(-620f, 26f + UiTheme.ButtonHeight));

            var row = UiFactory.HorizontalList(window, 14f, 0, TextAnchor.MiddleRight);
            row.gameObject.name = "Buttons";
            UiFactory.SetRect(row, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-600f, 26f), new Vector2(-40f, 26f + UiTheme.ButtonHeight));
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;

            UiFactory.FlexibleSpacer(row);
            _backButton = UiFactory.Button(row, Loc.Get("setup.btn.back", "GERİ"), Close, UiButtonStyle.Default);
            UiFactory.LayoutSize(_backButton, 200f, UiTheme.ButtonHeight);
            _startButton = UiFactory.Button(row, Loc.Get("setup.btn.start", "HAREKÂTA BAŞLA"), StartOperation, UiButtonStyle.Primary);
            UiFactory.LayoutSize(_startButton, 330f, UiTheme.ButtonHeight);
        }

        private void BuildForm(RectTransform window)
        {
            var form = UiFactory.VerticalList(window, 8f);
            form.gameObject.name = "Form";
            UiFactory.SetRect(form, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(40f, 112f), new Vector2(40f + 640f, -116f));

            // Oyuncu adı.
            SectionLabel(form, Loc.Get("setup.section.player", "OYUNCU ADI"));
            _nameField = CreateNameField(form, _working.PlayerName);

            // Tim sayısı.
            SectionLabel(form, Loc.Get("setup.section.teams", "TİM SAYISI"));
            var teamRow = UiFactory.CreateRect("TeamRow", form);
            UiFactory.LayoutSize(teamRow, -1f, 44f, 1f);
            _teamSlider = UiFactory.Slider(teamRow, SettingsService.MinTeamCount, SettingsService.MaxTeamCount, _working.TeamCount, OnTeamCountChanged, true);
            UiFactory.SetRect(_teamSlider, new Vector2(0f, 0.5f), new Vector2(0.56f, 0.5f), new Vector2(4f, -14f), new Vector2(0f, 14f));
            _teamValue = UiFactory.Label(teamRow, string.Empty, UiTheme.FontMedium, TextAnchor.MiddleRight, UiTheme.Amber, FontStyle.Bold);
            _teamValue.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_teamValue, new Vector2(0.58f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            var chips = UiFactory.HorizontalList(form, 8f, 0, TextAnchor.MiddleLeft);
            chips.gameObject.name = "TeamChips";
            UiFactory.LayoutSize(chips, -1f, 30f, 1f);
            var chipGroup = chips.GetComponent<HorizontalLayoutGroup>();
            chipGroup.childForceExpandWidth = false;
            chipGroup.childForceExpandHeight = false;
            _chips = new Image[ChipCount];
            _chipLabels = new Text[ChipCount];
            for (var i = 0; i < ChipCount; i++)
            {
                var chip = UiFactory.Image(chips, UiSprites.GetRoundedRect(4), UiTheme.TeamPaletteColor(i));
                chip.gameObject.name = "Chip" + i;
                UiFactory.LayoutSize(chip, i == 0 ? 92f : 64f, 28f);
                var label = UiFactory.Label(chip.rectTransform, i == 0 ? Loc.Get("setup.you", "SEN") : (i + 1).ToString(), UiTheme.FontTiny, TextAnchor.MiddleCenter, UiTheme.Text, FontStyle.Bold);
                UiFactory.AddShadow(label, UiTheme.TextShadow, new Vector2(1f, -1f));
                _chips[i] = chip;
                _chipLabels[i] = label;
            }

            // Harita.
            SectionLabel(form, Loc.Get("setup.section.map", "HARİTA"));
            UiWidgets.OptionSelector(form, Loc.Get("setup.map", "Harekât bölgesi"), MapCatalog.DisplayNames(),
                Mathf.Max(0, MapCatalog.IndexOf(GameSession.SelectedMap)), OnMapChanged);

            // Atmosfer.
            SectionLabel(form, Loc.Get("setup.section.atmosphere", "ATMOSFER"));
            UiWidgets.OptionSelector(form, Loc.Get("setup.tod", "Günün saati"), AtmosphereRules.TimeNames, (int)GameSession.SelectedTimeOfDay,
                i => GameSession.SelectedTimeOfDay = AtmosphereRules.TimeFromIndex(i));
            UiWidgets.OptionSelector(form, Loc.Get("setup.weather", "Hava durumu"), new[] { Loc.Get("setup.weather.auto", "Otomatik"), Loc.Get("setup.weather.clear", "Açık"), Loc.Get("setup.weather.rain", "Hafif yağmur"), Loc.Get("setup.weather.snow", "Kar") },
                Mathf.Clamp(GameSession.SelectedWeatherIndex, 0, 3), i => GameSession.SelectedWeatherIndex = i);

            // Zorluk.
            SectionLabel(form, Loc.Get("setup.section.difficulty", "ZORLUK"));
            _difficulty = UiWidgets.OptionSelector(form, Loc.Get("setup.enemy", "Düşman timleri"), MenuText.DifficultyNames, Mathf.Clamp((int)_working.Difficulty, 0, 2), OnDifficultyChanged);
            _difficultyInfo = InfoLabel(form);

            // İntikal.
            SectionLabel(form, Loc.Get("setup.section.insertion", "İNTİKAL"));
            _insertion = UiWidgets.OptionSelector(form, Loc.Get("setup.vehicle", "İntikal aracı"), MenuText.InsertionNames, Mathf.Clamp((int)_working.Insertion, 0, 1), OnInsertionChanged);
            _insertionInfo = InfoLabel(form);
        }

        private void BuildBriefing(RectTransform window)
        {
            var card = UiFactory.Panel(window, UiTheme.PanelDark, UiSprites.ChamferRect);
            card.gameObject.name = "Briefing";
            UiFactory.SetRect(card, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(40f + 640f + 40f, 112f), new Vector2(-40f, -116f));

            var cardBorder = UiFactory.Image(card, UiSprites.GetRoundedRectOutline(UiTheme.CornerRadius), UiTheme.WithAlpha(UiTheme.PanelBorder, 0.7f));
            UiFactory.Stretch(cardBorder);

            var flag = UiFactory.Image(card, UiSprites.TurkishFlag, new Color(1f, 1f, 1f, 0.9f));
            flag.preserveAspect = true;
            UiFactory.Anchor(flag, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -22f), new Vector2(60f, 40f));

            var header = UiFactory.Label(card, Loc.Get("setup.briefing", "HAREKÂT EMRİ"), UiTheme.FontMedium, TextAnchor.MiddleLeft, UiTheme.TextHeader, FontStyle.Bold);
            header.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -66f), new Vector2(-100f, -20f));

            var line = UiFactory.Image(card, null, UiTheme.Accent);
            UiFactory.SetRect(line, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -74f), new Vector2(28f + 120f, -70f));

            _briefing = UiFactory.Label(card, string.Empty, UiTheme.FontSmall, TextAnchor.UpperLeft, UiTheme.TextDim);
            _briefing.lineSpacing = 1.15f;
            _briefing.supportRichText = true;
            UiFactory.SetRect(_briefing, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(28f, 92f), new Vector2(-24f, -92f));

            var keys = UiFactory.Label(card, Loc.Get("setup.keys", "F1 Takip  ·  F2 Mevzi tut  ·  F3 Taarruz  ·  F4 Toplan  ·  V Topçu"), UiTheme.FontTiny, TextAnchor.MiddleLeft, UiTheme.Khaki);
            keys.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(keys, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(28f, 52f), new Vector2(-24f, 80f));

            var hint = UiFactory.Label(card, Loc.Get("setup.hint", "Tim emirleri tuşlarla verilir; harita (M) üzerinde işaretlenen nokta hedef olur."), UiTheme.FontTiny,
                TextAnchor.MiddleLeft, UiTheme.TextMuted);
            UiFactory.SetRect(hint, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(28f, 20f), new Vector2(-24f, 50f));
        }

        private static void SectionLabel(Transform parent, string text)
        {
            var label = UiFactory.Label(parent, text, UiTheme.FontSmall, TextAnchor.LowerLeft, UiTheme.Khaki, FontStyle.Bold);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.LayoutSize(label, -1f, 28f, 1f);
        }

        private static Text InfoLabel(Transform parent)
        {
            var label = UiFactory.Label(parent, string.Empty, UiTheme.FontSmall, TextAnchor.UpperLeft, UiTheme.TextDim);
            UiFactory.LayoutSize(label, -1f, 48f, 1f);
            return label;
        }

        /// <summary>Tema uyumlu tek satırlık ad alanı (en fazla 16 karakter).</summary>
        private InputField CreateNameField(Transform parent, string value)
        {
            var root = UiFactory.CreateRect("NameField", parent);
            UiFactory.LayoutSize(root, -1f, 50f, 1f);

            var background = root.gameObject.AddComponent<Image>();
            background.sprite = UiSprites.GetRoundedRect(4);
            background.type = Image.Type.Sliced;
            background.color = Color.white;
            background.raycastTarget = true;

            var outline = UiFactory.Image(root, UiSprites.GetRoundedRectOutline(4), UiTheme.PanelBorder);
            UiFactory.Stretch(outline);

            var text = UiFactory.Label(root, string.Empty, UiTheme.FontMedium, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold);
            text.gameObject.name = "Text";
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Stretch(text, 16f, 2f, 16f, 2f);

            var placeholder = UiFactory.Label(root, SettingsService.DefaultPlayerName, UiTheme.FontMedium, TextAnchor.MiddleLeft, UiTheme.TextMuted, FontStyle.Italic);
            placeholder.gameObject.name = "Placeholder";
            UiFactory.Stretch(placeholder, 16f, 2f, 16f, 2f);

            var field = root.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholder;
            field.targetGraphic = background;
            field.characterLimit = SettingsService.MaxPlayerNameLength;
            field.lineType = InputField.LineType.SingleLine;
            field.contentType = InputField.ContentType.Standard;
            field.caretColor = UiTheme.Amber;
            field.customCaretColor = true;
            field.caretWidth = 2;
            field.selectionColor = UiTheme.WithAlpha(UiTheme.Accent, 0.45f);
            field.transition = Selectable.Transition.ColorTint;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = UiTheme.ToggleBox;
            colors.highlightedColor = UiTheme.ButtonHover;
            colors.selectedColor = UiTheme.ButtonNormal;
            colors.pressedColor = UiTheme.ButtonPressed;
            colors.disabledColor = UiTheme.ButtonDisabled;
            colors.fadeDuration = 0.08f;
            field.colors = colors;
            field.SetTextWithoutNotify(string.IsNullOrEmpty(value) ? SettingsService.DefaultPlayerName : value);
            field.onValueChanged.AddListener(OnNameChanged);
            field.onEndEdit.AddListener(OnNameEndEdit);
            return field;
        }

        // ------------------------------------------------------------------ Olaylar

        private void OnNameChanged(string value)
        {
            _working.PlayerName = value;
            RefreshBriefing();
        }

        private void OnNameEndEdit(string value)
        {
            var clean = SettingsService.SanitizeName(value);
            if (_nameField != null && _nameField.text != clean)
                _nameField.SetTextWithoutNotify(clean);
            _working.PlayerName = clean;
            RefreshBriefing();
        }

        private void OnTeamCountChanged(float value)
        {
            _working.TeamCount = Mathf.Clamp(Mathf.RoundToInt(value), SettingsService.MinTeamCount, SettingsService.MaxTeamCount);
            RefreshTeams();
            RefreshBriefing();
        }

        private void OnMapChanged(int index)
        {
            GameSession.SelectedMap = MapCatalog.IdAt(index);
            RefreshMapLabel();
        }

        private void RefreshMapLabel()
        {
            if (_mapLabel == null)
                return;
            var id = MapCatalog.Normalize(GameSession.SelectedMap);
            var size = Mathf.RoundToInt(MapCatalog.HalfSize(id) * 2f);
            _mapLabel.text = MapCatalog.DisplayName(id).ToUpper(new System.Globalization.CultureInfo("tr-TR")) + "  ·  " + size + " × " + size + " m";
        }

        private void OnDifficultyChanged(int index)
        {
            _working.Difficulty = (BotDifficulty)Mathf.Clamp(index, 0, 2);
            RefreshInfo();
            RefreshBriefing();
        }

        private void OnInsertionChanged(int index)
        {
            _working.Insertion = (InsertionMethod)Mathf.Clamp(index, 0, 1);
            RefreshInfo();
            RefreshBriefing();
        }

        // ------------------------------------------------------------------ Görünüm

        private void RefreshAll()
        {
            RefreshTeams();
            RefreshInfo();
            RefreshBriefing();
        }

        private void RefreshTeams()
        {
            var count = _working.TeamCount;
            if (_teamValue != null)
                _teamValue.text = MenuText.TeamSummary(count);

            if (_chips == null)
                return;

            for (var i = 0; i < _chips.Length; i++)
            {
                var active = i < count;
                if (_chips[i] != null)
                    _chips[i].color = active ? UiTheme.TeamPaletteColor(i) : UiTheme.WithAlpha(UiTheme.PanelLight, 0.45f);
                if (_chipLabels[i] != null)
                    _chipLabels[i].color = active ? UiTheme.Text : UiTheme.TextMuted;
            }
        }

        private void RefreshInfo()
        {
            if (_difficultyInfo != null)
               _difficultyInfo.text = MenuText.DifficultyDescription(_working.Difficulty);
            if (_insertionInfo != null)
               _insertionInfo.text = MenuText.InsertionDescription(_working.Insertion);
        }

        private void RefreshBriefing()
        {
            if (_briefing == null)
                return;

            var rank = MilitaryRank.Yuzbasi;
            try
            {
                var career = GameSession.Career;
                if (career != null && career.Current != null)
                    rank = career.Current.Rank;
            }
            catch (Exception)
            {
                // Kariyer yoksa varsayılan rütbe.
            }

            var name = SettingsService.SanitizeName(_working.PlayerName);
            var enemies = Mathf.Max(1, _working.TeamCount - 1);
            var accent = UiTheme.ToHex(UiTheme.Text);

            Builder.Clear();
            Builder.Append("<color=").Append(accent).Append("><b>").Append(RankCatalog.FormatName(rank, name)).Append("</b></color>  —  Tim Komutanı\n\n");
            Builder.Append("•  Timin: ").Append(SettingsService.TeamSize).Append(" asker. Sen komutansın, ")
                .Append(SettingsService.TeamSize - 1).Append(" yapay zekâ asker emrinde.\n");
            Builder.Append("•  Karşı kuvvet: ").Append(enemies).Append(" düşman timi (").Append(enemies * SettingsService.TeamSize)
                .Append(" asker), zorluk: ").Append(MenuText.DifficultyName(_working.Difficulty)).Append(".\n");
            Builder.Append("•  İntikal: ").Append(_working.Insertion == InsertionMethod.ArmoredVehicle
                ? Loc.Get("setup.insert.ground", "Kirpi zırhlı araçla kara intikali.")
                : Loc.Get("setup.insert.air", "T-70 helikopteriyle hava intikali.")).Append('\n');
            Builder.Append("•  Harekât alanı (mavi bölge) aşama aşama daralır; dışında kalan asker hasar alır.\n");
            Builder.Append("•  Son ayakta kalan tim harekâtı kazanır.\n");
            Builder.Append("•  Komutan şehit düşerse komuta en kıdemli askere geçer ve tim savaşmaya devam eder.");
            _briefing.text = Builder.ToString();
        }

        private void SetInteractable(bool interactable)
        {
            if (_group != null)
                _group.interactable = interactable;
        }

        private void ShowStatus(string text, Color color)
        {
            if (_status == null)
                return;
            _status.text = text ?? string.Empty;
            _status.color = color;
        }

        private void Start()
        {
            UiFactory.Select(_startButton);
        }

        private void Update()
        {
            if (_group == null || _fade >= 1f)
                return;

            _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / UiTheme.FadeDuration);
            _group.alpha = _fade;
        }

        private void ClearSelection()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != null &&
                eventSystem.currentSelectedGameObject.transform.IsChildOf(transform))
                eventSystem.SetSelectedGameObject(null);
        }

        private void OnDestroy()
        {
            _closed = true;
            _onStart = null;
            _onClose = null;
        }
    }
}
