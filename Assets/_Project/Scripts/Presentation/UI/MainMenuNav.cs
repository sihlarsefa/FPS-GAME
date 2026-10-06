using System;
using Project.Infrastructure.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Sol gezinme öğesi: üzerine gelince / seçilince etiket sağa kayar, kırmızı vurgu çubuğu açılır, yatay ışık dolgusu belirir
    /// (hepsi üstel yumuşatmayla) ve hover sesi çalar. Etkin sayfa kalıcı vurgulanır.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuNavItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        private const float SlideDistance = 8f;
        private const float Rate = 26f; // ~0,12 sn yumuşak geçiş

        private RectTransform _label;
        private Text _text;
        private RectTransform _bar;
        private Image _barImage;
        private Image _fill;
        private RectTransform _underline;
        private Image _underlineImage;
        private RectTransform _glow;
        private Image _glowImage;
        private RectTransform _scan;
        private Image _scanImage;
        private float _u;          // alt çizgi çizim ilerlemesi (0..1, 0,18 sn)
        private float _scanT = 1f; // tarama çizgisi ilerlemesi (1 = beklemede)
        private bool _installed;
        private const float UnderlineSeconds = 0.18f;
        private const float ScanSeconds = 0.25f;
        private const float GlowHeight = 22f;
        private float _height;
        private float _h;
        private float _a;
        private bool _hover;
        private bool _selected;
        private bool _active;
        private Color _textColor = UiTheme.Text;
        private Color _activeColor = UiKitTokens.Text;

        /// <summary>Tıklanabilir düğme.</summary>
        public Button Button { get; private set; }

        /// <summary>Etiket metni.</summary>
        public Text Label => _text;

        /// <summary>Bu öğenin sayfası açık mı (kalıcı vurgu).</summary>
        public bool Active
        {
            get => _active;
            set => _active = value;
        }

        /// <summary>Gezinme öğesi kurar (ebeveyn dikey listedir).</summary>
        public static MenuNavItem Create(Transform parent, string label, int fontSize, float height, Color textColor, Action onClick)
        {
            label = (label ?? string.Empty).ToUpper(new System.Globalization.CultureInfo("tr-TR"));
            var root = UiFactory.CreateRect("Nav_" + label, parent);
            UiFactory.LayoutSize(root, -1f, height, 1f);

            var fill = UiFactory.Image(root, null, new Color(1f, 1f, 1f, 0f));
            fill.gameObject.name = "Fill";
            fill.raycastTarget = true;
            UiFactory.Stretch(fill);

            var bar = UiFactory.Image(root, null, UiKitTokens.Accent);
            bar.gameObject.name = "Bar";
            bar.raycastTarget = false;
            UiFactory.Anchor(bar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(0f, height));

            // Kırmızı imza: glow şeridi (en altta), canlı alt çizgi, hover tarama çizgisi.
            var glow = UiFactory.Image(root, UiSprites.VerticalGradient, new Color(UiKitTokens.AccentGlow.r, UiKitTokens.AccentGlow.g, UiKitTokens.AccentGlow.b, 0f));
            glow.gameObject.name = "Glow";
            glow.raycastTarget = false;
            UiFactory.SetRect(glow, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(0f, GlowHeight));

            var underline = UiFactory.Image(root, null, UiKitTokens.Accent);
            underline.gameObject.name = "Underline";
            underline.raycastTarget = false;
            UiFactory.SetRect(underline, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 0f), new Vector2(18f, 2f));

            var scan = UiFactory.Image(root, null, new Color(UiKitTokens.Accent.r, UiKitTokens.Accent.g, UiKitTokens.Accent.b, 0f));
            scan.gameObject.name = "Scanline";
            scan.raycastTarget = false;
            UiFactory.SetRect(scan, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -1f), new Vector2(0f, 0f));

            var text = UiFactory.Label(root, label, fontSize, TextAnchor.MiddleLeft, textColor, FontStyle.Bold);
            text.gameObject.name = "Text";
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.Stretch(text, 18f, 0f, 0f, 0f);

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            button.onClick.AddListener(() =>
            {
                UiWidgets.PlaySound(SoundId.UiClick);
                onClick?.Invoke();
            });

            var item = root.gameObject.AddComponent<MenuNavItem>();
            item._label = (RectTransform)text.transform;
            item._text = text;
            item._bar = bar.rectTransform;
            item._barImage = bar;
            item._fill = fill;
            item._underline = underline.rectTransform;
            item._underlineImage = underline;
            item._glow = glow.rectTransform;
            item._glowImage = glow;
            item._scan = scan.rectTransform;
            item._scanImage = scan;
            item._height = height;
            item._textColor = textColor;
            item.Button = button;
            item.Apply();
            return item;
        }

        private void Start()
        {
            if (_installed)
                return;
            _installed = true;
            MenuRedSignature.TryInstall(transform);
        }

        private void Update()
        {
            var targetH = _hover || _selected ? 1f : 0f;
            var targetA = _active ? 1f : 0f;
            var dt = Time.unscaledDeltaTime;
            _h = MainMenuMotion.Approach(_h, targetH, Rate, dt);
            _a = MainMenuMotion.Approach(_a, targetA, Rate * 0.8f, dt);
            _u = Mathf.MoveTowards(_u, _active ? 1f : 0f, dt / UnderlineSeconds);
            if (_scanT < 1f)
                _scanT = Mathf.Min(1f, _scanT + dt / ScanSeconds);
            Apply();
        }

        private void Apply()
        {
            var v = Mathf.Max(_h, _a);
            if (_label != null)
            {
                var left = 18f + SlideDistance * MainMenuMotion.EaseOutCubic(_h) + 4f * _a;
                _label.offsetMin = new Vector2(left, 0f);
            }

            if (_underline != null)
            {
                // Soldan sağa çizilir; sağ uç satır genişliğine (çapa) göre.
                var e = MainMenuMotion.EaseOutCubic(_u);
                _underline.anchorMax = new Vector2(e, 0f);
                _underline.anchorMin = new Vector2(0f, 0f);
                _underline.offsetMin = new Vector2(18f, 0f);
                _underline.offsetMax = new Vector2(0f, 2f);
                _underlineImage.enabled = _u > 0.001f;
                _glow.anchorMin = new Vector2(0f, 0f);
                _glow.anchorMax = new Vector2(e, 0f);
                _glow.offsetMin = new Vector2(18f, 0f);
                _glow.offsetMax = new Vector2(0f, GlowHeight);
                var ga = UiKitTokens.AccentGlow;
                _glowImage.color = new Color(ga.r, ga.g, ga.b, ga.a * e);
                _glowImage.enabled = _u > 0.001f;
            }

            if (_scan != null)
            {
                var on = _scanT < 1f;
                _scanImage.enabled = on;
                if (on)
                {
                    var y = -_scanT * Mathf.Max(0f, _height - 1f);
                    _scan.offsetMin = new Vector2(0f, y - 1f);
                    _scan.offsetMax = new Vector2(0f, y);
                    var a = 0.9f * Mathf.Sin(_scanT * Mathf.PI);
                    var sc = UiKitTokens.Accent;
                    _scanImage.color = new Color(sc.r, sc.g, sc.b, a);
                }
            }

            if (_bar != null)
                _bar.sizeDelta = new Vector2(2f * _a, _height);

            if (_fill != null)
                _fill.color = new Color(1f, 1f, 1f, UiKitTokens.HoverRow.a * Mathf.Max(_h, _a));

            if (_text != null)
                _text.color = Color.Lerp(_textColor, _activeColor, v);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_hover)
            {
                UiWidgets.PlaySound(SoundId.UiHover);
                _scanT = 0f;
            }
            _hover = true;
        }

        public void OnPointerExit(PointerEventData eventData) => _hover = false;

        public void OnSelect(BaseEventData eventData)
        {
            if (!_selected)
            {
                UiWidgets.PlaySound(SoundId.UiHover);
                _scanT = 0f;
            }
            _selected = true;
        }

        public void OnDeselect(BaseEventData eventData) => _selected = false;
    }

    /// <summary>
    /// Kırmızı imzanın menü kökü bağları: ilk gezinme öğesi çıkınca sol bant köz alanı ve HAREKÂT başlık parıltısı
    /// kendiliğinden kurulur (tekrar çağrılar yok sayılır). Ana menü denetleyicisinde değişiklik gerektirmez.
    /// </summary>
    public static class MenuRedSignature
    {
        private const float BandWidth = 560f;

        public static void TryInstall(Transform from)
        {
            var main = from;
            while (main != null && main.name != "Main")
                main = main.parent;
            if (main == null)
                return;
            var title = main.Find("Title");
            if (title == null)
                return;

            var titleText = title.GetComponent<Text>();
            if (titleText != null)
                TitleGlint.Attach(titleText);

            var root = main.parent as RectTransform;
            if (root == null || root.Find("Embers") != null)
                return;
            var embers = UiFactory.CreateRect("Embers", root);
            UiFactory.SetRect(embers, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(BandWidth, 0f));
            var shade = root.Find("LeftShadeFade") ?? root.Find("LeftShade");
            embers.SetSiblingIndex(shade != null ? shade.GetSiblingIndex() + 1 : main.GetSiblingIndex());
            EmberField.Attach(embers, 14);
        }
    }

    /// <summary>
    /// Sağdaki sayfa alanı: sayfalar kimlikle kaydedilir, <see cref="Show"/> eskisini sola kaydırıp soldurur (0.15 sn),
    /// yenisini sağdan kaydırıp belirtir (0.22 sn, yumuşak eğri). Sayfalar ilk gösterimde tembelce kurulur.
    /// </summary>
    public sealed class MenuPageHost : MonoBehaviour
    {
        private sealed class Page
        {
            public string Id;
            public RectTransform Rect;
            public CanvasGroup Group;
            public Action<RectTransform> Build;
            public Action Shown;
            public Action Hidden;
            public bool Built;
            public float Progress;
            public bool Visible;
            public float Sign = 1f;
            public Vector2 BasePosition;
        }

        private readonly System.Collections.Generic.List<Page> _pages = new System.Collections.Generic.List<Page>(6);
        private string _current;

        /// <summary>Etkin sayfa kimliği (yoksa null).</summary>
        public string Current => _current;

        /// <summary>Sayfayı kaydeder (build ilk gösterimde çağrılır).</summary>
        public void Register(string id, Action<RectTransform> build, Action shown = null, Action hidden = null)
        {
            var rect = UiFactory.CreateRect("Page_" + id, transform);
            var group = UiFactory.EnsureCanvasGroup(rect);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            rect.gameObject.SetActive(false);
            _pages.Add(new Page { Id = id, Rect = rect, Group = group, Build = build, Shown = shown, Hidden = hidden });
        }

        /// <summary>Sayfanın kök RectTransform'u (kayıtlı değilse null).</summary>
        public RectTransform RectOf(string id)
        {
            var p = Find(id);
            return p?.Rect;
        }

        private Page Find(string id)
        {
            for (var i = 0; i < _pages.Count; i++)
                if (_pages[i].Id == id)
                    return _pages[i];
            return null;
        }

        /// <summary>Sayfaya geçer; id null ise hepsi kapanır.</summary>
        public void Show(string id)
        {
            if (id == _current)
                return;

            for (var i = 0; i < _pages.Count; i++)
            {
                var p = _pages[i];
                if (p.Id == _current && p.Visible)
                {
                    p.Visible = false;
                    p.Sign = -1f;
                    p.Hidden?.Invoke();
                }
            }

            _current = id;
            var next = id != null ? Find(id) : null;
            if (next == null)
            {
                _current = null;
                return;
            }

            if (!next.Built)
            {
                next.Built = true;
                try
                {
                    next.Build?.Invoke(next.Rect);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            next.Rect.gameObject.SetActive(true);
            next.Sign = 1f;
            next.Visible = true;
            next.BasePosition = Vector2.zero;
            next.Shown?.Invoke();
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            for (var i = 0; i < _pages.Count; i++)
            {
                var p = _pages[i];
                if (!p.Rect.gameObject.activeSelf)
                    continue;

                p.Progress = MainMenuMotion.AdvancePage(p.Progress, p.Visible, dt);
                p.Group.alpha = MainMenuMotion.PageAlpha(p.Progress);
                p.Rect.anchoredPosition = new Vector2(MainMenuMotion.PageOffset(p.Progress, p.Sign), 0f);
                var interactive = p.Visible && p.Progress > 0.6f;
                p.Group.interactable = interactive;
                p.Group.blocksRaycasts = interactive;

                if (!p.Visible && p.Progress <= 0f)
                    p.Rect.gameObject.SetActive(false);
            }
        }
    }
}
