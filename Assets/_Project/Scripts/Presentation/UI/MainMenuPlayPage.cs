using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>Oynanabilir mod tanımı (saf veri).</summary>
    public sealed class MenuModeInfo
    {
        public string Id;
        public string Title;
        public string Subtitle;
        public string Description;
        public string[] Tags;
        public bool NeedsMap;
        public int ArtIndex;
    }

    /// <summary>Mod listesi: BR, Çatışma, Konvoy, Rehine, Poligon (kart atlıkarıncasının veri kaynağı).</summary>
    public static class MenuModeCatalog
    {
        public const string BattleRoyale = "br";
        public const string Skirmish = "skirmish";
        public const string Convoy = "convoy";
        public const string Hostage = "hostage";
        public const string Range = "range";

        private static readonly MenuModeInfo[] All =
        {
            new MenuModeInfo { Id = BattleRoyale, Title = "BATTLE ROYALE", Subtitle = "10 TİM · 100 ASKER", NeedsMap = true, ArtIndex = 0,
                Description = "Helikopter ya da Kirpi ile intikal et, ganimet topla, daralan alanda son ayakta kalan tim ol.",
                Tags = new[] { "10'ar kişilik timler", "Daralan emniyet alanı", "Ganimet" } },
            new MenuModeInfo { Id = Skirmish, Title = "ÇATIŞMA", Subtitle = "10'A 10 HIZLI MAÇ", NeedsMap = true, ArtIndex = 1,
                Description = "Yerden doğuş, 20 saniyede bir takviye. 50 etkisiz hâle getirmeye ilk ulaşan ya da 10 dakika sonunda önde olan kazanır.",
                Tags = new[] { "2 tim", "Sürekli takviye", "~10 dk" } },
            new MenuModeInfo { Id = Convoy, Title = "KONVOY KORUMA", Subtitle = "ESKORT GÖREVİ", NeedsMap = false, ArtIndex = 2,
                Description = "Zırhlı konvoyu ambuskadan geçir. Araçları ayakta tut, rotayı aç ve hedefe ulaştır.",
                Tags = new[] { "Araç hedefi", "Pusu", "Tim görevi" } },
            new MenuModeInfo { Id = Hostage, Title = "REHİNE KURTARMA", Subtitle = "KONTROLLÜ BASKIN", NeedsMap = false, ArtIndex = 3,
                Description = "Rehineleri tespit et, binayı temizle ve sağ salim tahliye noktasına ulaştır.",
                Tags = new[] { "Yakın muharebe", "Sessiz yaklaşma", "Tahliye" } },
            new MenuModeInfo { Id = Range, Title = "POLİGON", Subtitle = "ATIŞ VE EĞİTİM", NeedsMap = false, ArtIndex = 4,
                Description = "Tüm silahlarla sınırsız cephane. Eğitimli modda hareket, atış, siper ve emir adımlarını öğren.",
                Tags = new[] { "Sınırsız cephane", "Eğitim", "Tek kişilik" } }
        };

        public static IReadOnlyList<MenuModeInfo> Modes => All;

        public static int IndexOf(string id)
        {
            for (var i = 0; i < All.Length; i++)
                if (All[i].Id == id)
                    return i;
            return -1;
        }
    }

    /// <summary>Harita bilgisi (kapak yazıları).</summary>
    public static class MenuMapInfo
    {
        public static string Tagline(string mapId)
        {
            var id = MapCatalog.Normalize(mapId);
            if (id == MapCatalog.AyazGecidi) return "Karlı dağ geçidi · soğuk, açık görüş, keskin nişancı hattı";
            if (id == MapCatalog.MaviLiman) return "Liman, konteyner yığınları ve vinçler · yakın ve orta mesafe";
            if (id == MapCatalog.KartalYaylasi) return "Yayla, taş evler ve kayalıklar · geniş ve rüzgârlı";
            return "Çam ormanı, nehir vadisi ve köyler · dengeli, her mesafe";
        }

        public static string SizeText(string mapId)
        {
            var size = Mathf.RoundToInt(MapCatalog.HalfSize(mapId) * 2f);
            return size + " × " + size + " m";
        }
    }

    /// <summary>
    /// Kart bileşeni: üzerine gelince / seçilince yükselir ve büyür (üstel yumuşatma), seçiliyken vurgu şeridi görünür.
    /// Tıklama ve klavye/gamepad seçimi desteklenir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, ISelectHandler
    {
        private RectTransform _rect;
        private RectTransform _lift;
        private Image _glow;
        private Image _accent;
        private Image _dim;
        private float _t;
        private bool _hover;

        public bool Selected { get; set; }
        public Action<MenuCard> Clicked;
        public Action<MenuCard> Hovered;
        public int Index;

        public static MenuCard Create(Transform parent, float width, float height, Texture art, string title, string subtitle, int titleSize = 28)
        {
            var root = UiFactory.CreateRect("Card", parent);
            UiFactory.Anchor(root, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(width, height));
            UiFactory.LayoutSize(root, width, height, 0f, 0f);

            var lift = UiFactory.CreateRect("Lift", root);
            lift.pivot = new Vector2(0.5f, 0f);

            var back = UiFactory.Image(lift, null, UiTheme.WithAlpha(UiKitTokens.Bg, 0.94f));
            back.raycastTarget = true;

            if (art != null)
            {
                var raw = UiFactory.RawImage(lift, art);
                raw.raycastTarget = false;
                UiFactory.SetRect(raw, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f));
                // Yatay/kare sanat portre karta ortadan kırpılarak oturur (bozulma yok).
                if (art.width > 0 && art.height > 0)
                {
                    var crop = (width / height) / ((float)art.width / art.height);
                    if (crop < 1f)
                        raw.uvRect = new Rect((1f - crop) * 0.5f, 0f, crop, 1f);
                }
            }

            var dim = UiFactory.Image(lift, UiSprites.VerticalGradient, new Color(0f, 0f, 0f, 0.85f));
            dim.raycastTarget = false;
            UiFactory.SetRect(dim, new Vector2(0f, 0f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);

            var glow = UiFactory.Image(lift, null, new Color(1f, 1f, 1f, 0f));
            glow.raycastTarget = false;

            var accent = UiFactory.Image(lift, null, UiKitTokens.Accent);
            accent.raycastTarget = false;
            UiFactory.SetRect(accent, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 3f));

            var t = UiFactory.Label(lift, title, titleSize, TextAnchor.LowerLeft, UiTheme.Text, FontStyle.Bold);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.SetRect(t, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 46f), new Vector2(-12f, 108f));
            UiFactory.AddShadow(t, new Color(0f, 0f, 0f, 0.6f), new Vector2(1f, -1f));

            var s = UiFactory.Label(lift, subtitle, UiTheme.FontTiny + 1, TextAnchor.LowerLeft, UiKitTokens.TextDim, FontStyle.Bold);
            s.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.SetRect(s, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 14f), new Vector2(-12f, 44f));

            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = back;
            btn.transition = Selectable.Transition.None;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };

            var card = root.gameObject.AddComponent<MenuCard>();
            card._rect = root;
            card._lift = lift;
            card._glow = glow;
            card._accent = accent;
            card._dim = dim;
            card.Apply(0f);
            return card;
        }

        private void Update()
        {
            var target = Selected ? 1f : (_hover ? 0.55f : 0f);
            _t = MainMenuMotion.Approach(_t, target, 12f, Time.unscaledDeltaTime);
            Apply(_t);
        }

        private void Apply(float t)
        {
            if (_lift == null)
                return;
            var e = MainMenuMotion.EaseOutCubic(t);
            var scale = Mathf.Lerp(0.93f, 1f, e);
            _lift.localScale = new Vector3(scale, scale, 1f);
            _lift.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, 14f, e));
            _glow.color = new Color(1f, 1f, 1f, 0.07f * e);
            var a = _accent.color;
            a.a = Selected ? 1f : 0.25f + 0.5f * e;
            _accent.color = a;
            var d = _dim.color;
            d.a = Mathf.Lerp(0.95f, 0.7f, e);
            _dim.color = d;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_hover)
                UiWidgets.PlaySound(SoundId.UiHover);
            _hover = true;
            Hovered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData) => _hover = false;

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(this);

        public void OnSelect(BaseEventData eventData) => Hovered?.Invoke(this);
    }

    /// <summary>
    /// OYNA sayfası: 1) mod kartı atlıkarıncası (5 mod, prosedürel kapak), 2) harita seçimi (silüet kapaklı 4 harita).
    /// Sol/sağ (A/D, ok, D-pad) kartı değiştirir; Enter/Boşluk onaylar; Esc haritadan moda döner.
    /// </summary>
    public sealed class MenuPlayPage : MonoBehaviour
    {
        private const float CardWidth = 228f;
        private const float CardHeight = 300f;

        private MainMenuController _menu;
        private RectTransform _modeStep;
        private RectTransform _mapStep;
        private CanvasGroup _modeGroup;
        private CanvasGroup _mapGroup;
        private float _modeVis = 1f;
        private float _mapVis;
        private bool _inMap;

        private readonly List<MenuCard> _modeCards = new List<MenuCard>(5);
        private readonly List<MenuCard> _mapCards = new List<MenuCard>(4);
        private readonly List<Texture2D> _textures = new List<Texture2D>(9);
        private int _modeIndex;
        private int _mapIndex;

        private Text _modeTitle;
        private Text _modeDesc;
        private Text _modeTags;
        private Text _mapTitle;
        private Text _mapDesc;
        private Button _startButton;
        private Button _secondaryButton;
        private Text _startLabel;
        private Text _breadcrumb;

        /// <summary>Harita adımı açık mı (Esc önce buraya döner).</summary>
        public bool InMapStep => _inMap;

        public static MenuPlayPage Create(RectTransform page, MainMenuController menu)
        {
            var host = page.gameObject.AddComponent<MenuPlayPage>();
            host._menu = menu;
            host.Build(page);
            return host;
        }

        private void OnDestroy()
        {
            for (var i = 0; i < _textures.Count; i++)
                if (_textures[i] != null)
                    Destroy(_textures[i]);
            _textures.Clear();
        }

        private void Build(RectTransform page)
        {
            var title = UiFactory.Label(page, "OYNA", 38, TextAnchor.UpperLeft, UiTheme.Text, FontStyle.Bold);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(title, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -56f), new Vector2(-300f, 0f));
            UiFactory.AddShadow(title, new Color(0f, 0f, 0f, 0.6f), new Vector2(1f, -1f));

            _breadcrumb = UiFactory.Label(page, "1 / 2   MOD SEÇ", UiTheme.FontSmall, TextAnchor.UpperRight, UiKitTokens.TextDim, FontStyle.Bold);
            UiFactory.SetRect(_breadcrumb, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-400f, -44f), new Vector2(0f, -10f));

            var line = UiFactory.Image(page, null, UiKitTokens.Accent);
            UiFactory.SetRect(line, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, -66f), new Vector2(120f, -63f));

            _modeStep = UiFactory.CreateRect("ModeStep", page);
            UiFactory.SetRect(_modeStep, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, -96f));
            _modeGroup = UiFactory.EnsureCanvasGroup(_modeStep);
            _mapStep = UiFactory.CreateRect("MapStep", page);
            UiFactory.SetRect(_mapStep, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, -96f));
            _mapGroup = UiFactory.EnsureCanvasGroup(_mapStep);
            _mapGroup.alpha = 0f;
            _mapGroup.interactable = false;
            _mapGroup.blocksRaycasts = false;

            BuildModeStep(_modeStep);
            BuildMapStep(_mapStep);
            RefreshMode();
            RefreshMap();
        }

        private void BuildModeStep(RectTransform step)
        {
            var row = UiFactory.HorizontalList(step, 12f, 0, TextAnchor.UpperLeft);
            UiFactory.SetRect(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -CardHeight - 30f), new Vector2(0f, -10f));
            var h = row.GetComponent<HorizontalLayoutGroup>();
            if (h != null)
            {
                h.childControlWidth = false;
                h.childControlHeight = false;
                h.childForceExpandWidth = false;
                h.childForceExpandHeight = false;
                h.childAlignment = TextAnchor.UpperLeft;
            }

            var modes = MenuModeCatalog.Modes;
            for (var i = 0; i < modes.Count; i++)
            {
                Texture tex = null;
                try { tex = MapPreviewArt.GetModeCard(modes[i].ArtIndex); }   // paylaşımlı önbellek: yok edilmez
                catch (System.Exception e) { Debug.LogWarning("[OynaSayfası] mod kartı sanatı: " + e.Message); }
                if (tex == null)
                {
                    var fallback = MainMenuKeyArt.Mode(modes[i].ArtIndex).ToTexture("HK_ModeArt_" + modes[i].Id);
                    _textures.Add(fallback);
                    tex = fallback;
                }
                var card = MenuCard.Create(row, CardWidth, CardHeight, tex, modes[i].Title, modes[i].Subtitle, modes[i].Title.Length > 12 ? 24 : 30);
                card.Index = i;
                card.Hovered = c => SelectMode(c.Index, false);
                card.Clicked = c =>
                {
                    if (_modeIndex == c.Index)
                        Confirm();
                    else
                        SelectMode(c.Index, true);
                };
                _modeCards.Add(card);
            }

            var scrim__modeTitle = UiFactory.Image(step, null, UiKitTokens.ScrimBand);
            scrim__modeTitle.raycastTarget = false;
            UiFactory.SetRect(scrim__modeTitle, new Vector2(0f, 0f), new Vector2(0.66f, 0f), new Vector2(-12f, 12f), new Vector2(0f, 214f));

            _modeTitle = UiFactory.Label(step, string.Empty, 38, TextAnchor.UpperLeft, UiTheme.Text, FontStyle.Bold);
            _modeTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_modeTitle, new Vector2(0f, 0f), new Vector2(0.62f, 0f), new Vector2(4f, 150f), new Vector2(0f, 206f));

            _modeDesc = UiFactory.Label(step, string.Empty, UiTheme.FontNormal, TextAnchor.UpperLeft, UiTheme.TextDim);
            UiFactory.SetRect(_modeDesc, new Vector2(0f, 0f), new Vector2(0.62f, 0f), new Vector2(4f, 62f), new Vector2(0f, 148f));

            _modeTags = UiFactory.Label(step, string.Empty, UiTheme.FontSmall, TextAnchor.UpperLeft, UiKitTokens.TextDim, FontStyle.Bold);
            UiFactory.SetRect(_modeTags, new Vector2(0f, 0f), new Vector2(0.62f, 0f), new Vector2(4f, 20f), new Vector2(0f, 56f));

            _startButton = UiFactory.Button(step, "İLERLE", Confirm, UiButtonStyle.Primary);
            UiFactory.Anchor(_startButton, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-4f, 70f), new Vector2(330f, 74f));
            _startLabel = UiFactory.GetButtonLabel(_startButton);
            if (_startLabel != null)
                _startLabel.fontSize = UiTheme.FontLarge;

            _secondaryButton = UiFactory.Button(step, "EĞİTİMLİ BAŞLAT", () => _menu.LaunchMode(MenuModeCatalog.Range, null, true), UiButtonStyle.Default);
            UiFactory.Anchor(_secondaryButton, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-4f, 150f), new Vector2(330f, 56f));
        }

        private void BuildMapStep(RectTransform step)
        {
            var row = UiFactory.HorizontalList(step, 14f, 0, TextAnchor.UpperLeft);
            UiFactory.SetRect(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -CardHeight - 30f), new Vector2(0f, -10f));
            var h = row.GetComponent<HorizontalLayoutGroup>();
            if (h != null)
            {
                h.childControlWidth = false;
                h.childControlHeight = false;
                h.childForceExpandWidth = false;
                h.childForceExpandHeight = false;
            }

            var names = MapCatalog.DisplayNames();
            for (var i = 0; i < MapCatalog.Count; i++)
            {
                var id = MapCatalog.IdAt(i);
                Texture tex = null;
                try { tex = MapPreviewArt.Get(id, 512); }   // paylaşımlı önbellek: yok edilmez
                catch (System.Exception e) { Debug.LogWarning("[OynaSayfası] harita önizleme: " + e.Message); }
                if (tex == null)
                {
                    var fallback = MainMenuKeyArt.Map(id).ToTexture("HK_MapArt_" + id);
                    _textures.Add(fallback);
                    tex = fallback;
                }
                var card = MenuCard.Create(row, 292f, CardHeight, tex, MenuText.ToUpperTr(names[i]), MenuMapInfo.SizeText(id), 30);
                card.Index = i;
                card.Hovered = c => SelectMap(c.Index, false);
                card.Clicked = c =>
                {
                    if (_mapIndex == c.Index)
                        Confirm();
                    else
                        SelectMap(c.Index, true);
                };
                _mapCards.Add(card);
            }

            var scrim__mapTitle = UiFactory.Image(step, null, UiKitTokens.ScrimBand);
            scrim__mapTitle.raycastTarget = false;
            UiFactory.SetRect(scrim__mapTitle, new Vector2(0f, 0f), new Vector2(0.66f, 0f), new Vector2(-12f, 12f), new Vector2(0f, 214f));

            _mapTitle = UiFactory.Label(step, string.Empty, 38, TextAnchor.UpperLeft, UiTheme.Text, FontStyle.Bold);
            _mapTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_mapTitle, new Vector2(0f, 0f), new Vector2(0.62f, 0f), new Vector2(4f, 150f), new Vector2(0f, 206f));

            _mapDesc = UiFactory.Label(step, string.Empty, UiTheme.FontNormal, TextAnchor.UpperLeft, UiTheme.TextDim);
            UiFactory.SetRect(_mapDesc, new Vector2(0f, 0f), new Vector2(0.62f, 0f), new Vector2(4f, 62f), new Vector2(0f, 148f));

            var back = UiFactory.Button(step, "‹  GERİ", () => ShowStep(false), UiButtonStyle.Default);
            UiFactory.Anchor(back, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(4f, 6f), new Vector2(200f, 54f));

            var start = UiFactory.Button(step, "BAŞLAT", Confirm, UiButtonStyle.Primary);
            UiFactory.Anchor(start, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-4f, 70f), new Vector2(330f, 74f));
            var label = UiFactory.GetButtonLabel(start);
            if (label != null)
                label.fontSize = UiTheme.FontLarge;
        }

        /// <summary>Sayfa gösterilince çağrılır: harita adımını sıfırlar.</summary>
        public void OnShown()
        {
            _mapIndex = Mathf.Max(0, MapCatalog.IndexOf(Project.Presentation.Bootstrap.GameSession.SelectedMap));
            ShowStep(false);
            RefreshMap();
        }

        /// <summary>Geri tuşu: harita adımındaysa moda döner ve true verir.</summary>
        public bool HandleBack()
        {
            if (!_inMap)
                return false;
            ShowStep(false);
            return true;
        }

        private void ShowStep(bool map)
        {
            _inMap = map;
            _mapGroup.interactable = map;
            _mapGroup.blocksRaycasts = map;
            _modeGroup.interactable = !map;
            _modeGroup.blocksRaycasts = !map;
            if (_breadcrumb != null)
                _breadcrumb.text = map ? "2 / 2   HARİTA SEÇ" : "1 / 2   MOD SEÇ";
        }

        private void SelectMode(int index, bool sound)
        {
            index = MainMenuMotion.Wrap(index, MenuModeCatalog.Modes.Count);
            if (index == _modeIndex && !sound)
                return;
            _modeIndex = index;
            if (sound)
                UiWidgets.PlaySound(SoundId.UiClick);
            RefreshMode();
        }

        private void SelectMap(int index, bool sound)
        {
            index = MainMenuMotion.Wrap(index, MapCatalog.Count);
            if (index == _mapIndex && !sound)
                return;
            _mapIndex = index;
            if (sound)
                UiWidgets.PlaySound(SoundId.UiClick);
            RefreshMap();
        }

        private void RefreshMode()
        {
            var mode = MenuModeCatalog.Modes[_modeIndex];
            for (var i = 0; i < _modeCards.Count; i++)
                _modeCards[i].Selected = i == _modeIndex;
            if (_modeTitle != null) _modeTitle.text = mode.Title;
            if (_modeDesc != null) _modeDesc.text = mode.Description;
            if (_modeTags != null) _modeTags.text = string.Join("   ·   ", mode.Tags);
            if (_startLabel != null)
                _startLabel.text = mode.NeedsMap ? "HARİTA SEÇ  ›" : (mode.Id == MenuModeCatalog.Range ? "SERBEST ATIŞ" : "BAŞLAT");
            if (_secondaryButton != null)
                _secondaryButton.gameObject.SetActive(mode.Id == MenuModeCatalog.Range);
        }

        private void RefreshMap()
        {
            var id = MapCatalog.IdAt(_mapIndex);
            for (var i = 0; i < _mapCards.Count; i++)
                _mapCards[i].Selected = i == _mapIndex;
            if (_mapTitle != null) _mapTitle.text = MenuText.ToUpperTr(MapCatalog.DisplayName(id));
            if (_mapDesc != null) _mapDesc.text = MenuMapInfo.Tagline(id) + "\n" + MenuMapInfo.SizeText(id);
        }

        private void Confirm()
        {
            var mode = MenuModeCatalog.Modes[_modeIndex];
            if (mode.NeedsMap && !_inMap)
            {
                ShowStep(true);
                return;
            }

            _menu.LaunchMode(mode.Id, mode.NeedsMap ? MapCatalog.IdAt(_mapIndex) : null, false);
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _modeVis = MainMenuMotion.Approach(_modeVis, _inMap ? 0f : 1f, 14f, dt);
            _mapVis = MainMenuMotion.Approach(_mapVis, _inMap ? 1f : 0f, 14f, dt);
            if (_modeGroup != null)
            {
                _modeGroup.alpha = _modeVis;
                _modeStep.anchoredPosition = new Vector2(-36f * (1f - _modeVis), 0f);
                _mapGroup.alpha = _mapVis;
                _mapStep.anchoredPosition = new Vector2(36f * (1f - _mapVis), 0f);
            }

            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            var dir = 0;
            var confirm = false;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) dir = -1;
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) dir = 1;
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) confirm = true;
            }

            if (gamepad != null)
            {
                if (gamepad.dpad.left.wasPressedThisFrame) dir = -1;
                if (gamepad.dpad.right.wasPressedThisFrame) dir = 1;
                if (gamepad.buttonSouth.wasPressedThisFrame) confirm = true;
            }

            if (_menu != null && (_menu.IsPanelOpen || _menu.IsLeaving))
                return;

            if (dir != 0)
            {
                if (_inMap) SelectMap(_mapIndex + dir, true);
                else SelectMode(_modeIndex + dir, true);
            }

            // Düğmeye odaklıyken Enter zaten tıklar; yalnızca odak yokken onayla.
            if (confirm)
            {
                var es = EventSystem.current;
                var selected = es != null ? es.currentSelectedGameObject : null;
                if (selected == null || selected.GetComponent<MenuNavItem>() != null)
                    Confirm();
            }
        }
    }
}
