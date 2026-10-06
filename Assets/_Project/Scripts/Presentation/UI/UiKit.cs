using System;
using System.Collections.Generic;
using Project.Infrastructure.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>Kozmetik / ödül nadirlik kademesi (parıltı rengini belirler).</summary>
    public enum UiRarity { Common, Uncommon, Rare, Epic, Legendary }

    /// <summary>Modern düğme türleri (UiKit).</summary>
    public enum UiKitButtonKind { Default, Primary, Ghost, Danger }

    /// <summary>
    /// Modern menü sayfaları için ortak tasarım jetonları: koyu antrasit zemin, kum/haki vurgu, kırmızı tehlike,
    /// aralık ölçeği ve yazı boyutları. Yalnızca görünüm; mantık içermez.
    /// </summary>
    public static class UiKitTokens
    {
        public static readonly Color Scrim = new Color(0.02f, 0.025f, 0.03f, 0.78f);
        /// <summary>Arka plan paneli rgba(16,18,20,0.86).</summary>
        public static readonly Color Bg = UiTheme.Hex(0x10, 0x12, 0x14, 0xDB);
        public static readonly Color Surface = UiTheme.Hex(0x16, 0x18, 0x1B, 0xEB);
        public static readonly Color SurfaceRaised = UiTheme.Hex(0x22, 0x25, 0x28, 0xFF);
        public static readonly Color SurfaceHover = UiTheme.Hex(0x2E, 0x31, 0x35, 0xFF);
        public static readonly Color Border = UiTheme.Hex(0x3A, 0x3E, 0x42, 0xFF);
        /// <summary>Satır üzerine gelme rgba(255,255,255,0.06).</summary>
        public static readonly Color HoverRow = new Color(1f, 1f, 1f, 0.06f);
        /// <summary>Koyu yazı bandı (gökyüzü üstünde okunabilirlik) ve alt çubuk.</summary>
        public static readonly Color ScrimBand = new Color(0.063f, 0.071f, 0.078f, 0.72f);
        /// <summary>Ana vurgu kırmızısı #D43A2E (OYNA düğmesi, aktif gezinme çubuğu).</summary>
        public static readonly Color Accent = UiTheme.Hex(0xD4, 0x3A, 0x2E, 0xFF);
        public static readonly Color AccentHover = UiTheme.Hex(0xE8, 0x4B, 0x3E, 0xFF);
        public static readonly Color AccentDown = UiTheme.Hex(0x9F, 0x2A, 0x21, 0xFF);
        /// <summary>Koyu kırmızı (#7A1F17): alt çizgi dibi, ember soğuma ucu.</summary>
        public static readonly Color AccentDeep = UiTheme.Hex(0x7A, 0x1F, 0x17, 0xFF);
        /// <summary>Kırmızı parıltı rgba(212,58,46,0.25): glow şeridi, panel köşe ışıması.</summary>
        public static readonly Color AccentGlow = new Color(212f / 255f, 58f / 255f, 46f / 255f, 0.25f);
        /// <summary>Köz turuncusu (ember sıcak ucu).</summary>
        public static readonly Color Ember = UiTheme.Hex(0xFF, 0x8A, 0x3C, 0xFF);
        public static readonly Color Sand = UiTheme.Hex(0xD2, 0xBF, 0x8A, 0xFF);
        public static readonly Color SandDark = UiTheme.Hex(0x8F, 0x80, 0x5A, 0xFF);
        public static readonly Color Khaki = UiTheme.Hex(0x7E, 0x85, 0x5F, 0xFF);
        public static readonly Color Danger = UiTheme.Hex(0xD8, 0x3A, 0x3A, 0xFF);
        public static readonly Color DangerDark = UiTheme.Hex(0x7A, 0x1E, 0x22, 0xFF);
        public static readonly Color Success = UiTheme.Hex(0x6F, 0xBF, 0x62, 0xFF);
        public static readonly Color Text = UiTheme.Hex(0xF2, 0xF2, 0xF0, 0xFF);
        public static readonly Color TextDim = UiTheme.Hex(0x9A, 0xA0, 0xA3, 0xFF);
        public static readonly Color TextMuted = UiTheme.Hex(0x8A, 0x90, 0x94, 0xFF);

        public const float SpaceXs = 4f;
        public const float SpaceSm = 8f;
        public const float SpaceMd = 16f;
        public const float SpaceLg = 24f;
        public const float SpaceXl = 40f;

        public const int FontCaption = 15;
        public const int FontBody = 20;
        public const int FontLabel = 22;
        public const int FontHeading = 30;
        public const int FontTitle = 44;

        public const int Radius = 10;
        public const float Transition = 0.1f;

        /// <summary>Nadirlik rengi.</summary>
        public static Color RarityColor(UiRarity r)
        {
            switch (r)
            {
                case UiRarity.Uncommon: return UiTheme.Hex(0x6F, 0xBF, 0x62);
                case UiRarity.Rare: return UiTheme.Hex(0x4A, 0x9B, 0xE8);
                case UiRarity.Epic: return UiTheme.Hex(0xB0, 0x6C, 0xE0);
                case UiRarity.Legendary: return UiTheme.Hex(0xF2, 0xA9, 0x00);
                default: return UiTheme.Hex(0x9A, 0x9F, 0xA8);
            }
        }

        /// <summary>Nadirlik adı (Türkçe).</summary>
        public static string RarityName(UiRarity r)
        {
            switch (r)
            {
                case UiRarity.Uncommon: return "SIRADIŞI";
                case UiRarity.Rare: return "NADİR";
                case UiRarity.Epic: return "EFSANEVİ ADAY";
                case UiRarity.Legendary: return "EFSANE";
                default: return "SIRADAN";
            }
        }

        /// <summary>Kozmetiğin açılma yöntemi/tecrübe eşiğinden nadirlik türetir.</summary>
        public static UiRarity RarityOf(string unlockMethod, int unlockXp)
        {
            if (unlockMethod == "default") return UiRarity.Common;
            if (unlockMethod == "career_xp")
                return unlockXp < 2000 ? UiRarity.Uncommon : unlockXp < 6000 ? UiRarity.Rare : UiRarity.Epic;
            return UiRarity.Legendary;
        }

        /// <summary>HUD ölçeğini (Ayarlar) pencereye uygular; taşmayı önlemek için en fazla 1.0'a yakın tutar.</summary>
        public static void ApplyHudScale(RectTransform window)
        {
            if (window == null) return;
            var s = Mathf.Clamp(AdvancedDisplay.HudScale, 0.8f, 1.2f);
            window.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>Panel kurucusu: yuvarlatılmış 9-dilim, yumuşak gölge, üst başlık şeridi.</summary>
    public static class UiKitPanel
    {
        private static Sprite _shadow;

        /// <summary>Prosedürel yumuşak gölge sprite'ı (9-dilim, kenarlara doğru saydamlaşır).</summary>
        public static Sprite SoftShadow
        {
            get
            {
                if (_shadow != null) return _shadow;
                const int size = 64;
                const int border = 28;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "UiKitShadow", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[size * size];
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Min(x, size - 1 - x) / (float)border;
                    var dy = Mathf.Min(y, size - 1 - y) / (float)border;
                    var d = Mathf.Clamp01(Mathf.Min(dx, dy));
                    var a = d * d * (3f - 2f * d);
                    px[y * size + x] = new Color32(0, 0, 0, (byte)(a * 255f));
                }
                tex.SetPixels32(px);
                tex.Apply(false, false);
                _shadow = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
                _shadow.hideFlags = HideFlags.DontUnloadUnusedAsset;
                return _shadow;
            }
        }

        /// <summary>Düz yuvarlatılmış kart.</summary>
        public static RectTransform Card(Transform parent, Color color, int radius = UiKitTokens.Radius)
        {
            return UiFactory.Panel(parent, color, UiSprites.GetRoundedRect(radius));
        }

        /// <summary>Gölge ekler (hedefin arkasında, dışarı taşan yumuşak halka).</summary>
        public static void AddSoftShadow(RectTransform target, float spread = 28f, float alpha = 0.55f, float offsetY = -8f)
        {
            var sh = UiFactory.Image(target, SoftShadow, new Color(0f, 0f, 0f, alpha));
            sh.gameObject.name = "Shadow";
            sh.raycastTarget = false;
            sh.type = Image.Type.Sliced;
            UiFactory.SetRect(sh, Vector2.zero, Vector2.one, new Vector2(-spread, -spread + offsetY), new Vector2(spread, spread + offsetY));
            sh.transform.SetAsFirstSibling();
        }

        /// <summary>
        /// Panel köşelerine 2 px L-biçimli kırmızı ayraçlar (ince, düşük opaklık). Tek sefer kurulur (aynı ada sahip çocuk varsa atlar).
        /// </summary>
        public static void AddCornerBrackets(RectTransform panel, float arm = 16f, float thickness = 2f, float inset = 6f, float alpha = 0.55f)
        {
            if (panel == null || panel.Find("Brackets") != null)
                return;
            var holder = UiFactory.CreateRect("Brackets", panel);
            UiFactory.Stretch(holder);
            var c = UiTheme.WithAlpha(UiKitTokens.Accent, alpha);
            for (var i = 0; i < 4; i++)
            {
                var right = (i & 1) == 1;
                var top = (i & 2) == 2;
                var anchor = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
                var sx = right ? -1f : 1f;
                var sy = top ? -1f : 1f;
                var h = UiFactory.Image(holder, null, c);
                h.gameObject.name = "BrH" + i;
                UiFactory.Anchor(h, anchor, anchor, new Vector2(inset * sx, inset * sy), new Vector2(arm, thickness));
                h.rectTransform.pivot = anchor;
                var v = UiFactory.Image(holder, null, c);
                v.gameObject.name = "BrV" + i;
                UiFactory.Anchor(v, anchor, anchor, new Vector2(inset * sx, inset * sy), new Vector2(thickness, arm));
                v.rectTransform.pivot = anchor;
            }
        }

        /// <summary>Pencere: karartma zemin + ortalanmış gölgeli kart + başlık şeridi + gövde alanı.</summary>
        public sealed class WindowParts
        {
            public RectTransform Window;
            public RectTransform Header;
            public RectTransform Body;
            public Text Title;
            public Text Subtitle;
        }

        public static WindowParts Window(Transform root, Vector2 size, string title, string subtitle = null)
        {
            var parts = new WindowParts();
            var window = Card(root, UiKitTokens.Bg, 14);
            window.gameObject.name = "Window";
            UiFactory.Anchor(window, UiAnchor.Center, Vector2.zero, size);
            AddSoftShadow(window, 36f, 0.6f, -10f);
            var outline = UiFactory.Image(window, UiSprites.GetRoundedRectOutline(14), UiKitTokens.Border);
            outline.raycastTarget = false;
            UiFactory.Stretch(outline);
            UiKitPanel.AddCornerBrackets(window);
            UiKitTokens.ApplyHudScale(window);
            parts.Window = window;

            var header = UiFactory.CreateRect("Header", window);
            UiFactory.SetRect(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -88f), Vector2.zero);
            var strip = UiFactory.Image(header, UiSprites.GetRoundedRect(14), UiKitTokens.Surface);
            strip.raycastTarget = false;
            UiFactory.Stretch(strip);
            var accent = UiFactory.Image(header, null, UiKitTokens.Accent);
            accent.raycastTarget = false;
            UiFactory.SetRect(accent, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 0f), new Vector2(-24f, 2f));
            parts.Header = header;

            parts.Title = UiFactory.Label(header, title, UiKitTokens.FontHeading + 4, TextAnchor.MiddleLeft, UiKitTokens.Text, FontStyle.Bold);
            parts.Title.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(parts.Title, new Vector2(0f, 0.38f), new Vector2(1f, 1f), new Vector2(32f, 0f), new Vector2(-32f, -6f));
            parts.Subtitle = UiFactory.Label(header, subtitle ?? string.Empty, UiKitTokens.FontCaption + 1, TextAnchor.MiddleLeft, UiKitTokens.TextDim);
            parts.Subtitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(parts.Subtitle, new Vector2(0f, 0f), new Vector2(1f, 0.4f), new Vector2(32f, 4f), new Vector2(-32f, 0f));

            var body = UiFactory.CreateRect("Body", window);
            UiFactory.SetRect(body, Vector2.zero, Vector2.one, new Vector2(24f, 24f), new Vector2(-24f, -100f));
            parts.Body = body;
            return parts;
        }

        /// <summary>Tam ekran karartma kökü (tıklamayı engeller).</summary>
        public static RectTransform Scrim(Transform parent, string name)
        {
            var root = UiFactory.CreateRect(name, parent);
            root.SetAsLastSibling();
            UiFactory.Stretch(root);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = UiKitTokens.Scrim;
            dim.raycastTarget = true;
            return root;
        }
    }

    /// <summary>Düğme: varsayılan/üzerine gelme/basılı/devre dışı geçişleri 0.1 sn, tıklama sesi bağlantısı.</summary>
    public sealed class UiKitButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        /// <summary>Tıklama sesi bağlantısı (varsayılan: UiClick). Null yapılırsa ses çalınmaz.</summary>
        public static Action<UiKitButtonKind> ClickSound = _ => UiSounds.Play(UiSfx.Press);

        private Image _bg;
        private Text _label;
        private Button _button;
        private UiKitButtonKind _kind;
        private bool _hover, _down;
        private Color _current;

        public static Button Create(Transform parent, string label, Action onClick, UiKitButtonKind kind = UiKitButtonKind.Default, float width = 220f, float height = 52f)
        {
            var rt = UiFactory.CreateRect("Btn_" + label, parent);
            UiFactory.Anchor(rt, UiAnchor.Center, Vector2.zero, new Vector2(width, height));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UiSprites.GetRoundedRect(UiKitTokens.Radius);
            img.type = Image.Type.Sliced;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;
            var text = UiFactory.Label(rt, label, UiKitTokens.FontLabel, TextAnchor.MiddleCenter, UiKitTokens.Text, FontStyle.Bold);
            text.gameObject.name = "Text";
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Stretch(text, 10f, 0f, 10f, 0f);
            var kb = rt.gameObject.AddComponent<UiKitButton>();
            kb._bg = img; kb._label = text; kb._button = btn; kb._kind = kind;
            kb._current = kb.Target();
            img.color = kb._current;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.preferredHeight = height; le.minHeight = height;
            btn.onClick.AddListener(() =>
            {
                try { ClickSound?.Invoke(kind); } catch (Exception) { /* ses hatası arayüzü bozmasın */ }
                onClick?.Invoke();
            });
            return btn;
        }

        private Color Target()
        {
            var enabled = _button == null || _button.interactable;
            if (!enabled) return _kind == UiKitButtonKind.Ghost ? new Color(1f, 1f, 1f, 0f) : UiTheme.Hex(0x22, 0x25, 0x2A, 0xB0);
            switch (_kind)
            {
                case UiKitButtonKind.Primary:
                    return _down ? UiKitTokens.AccentDown : _hover ? UiKitTokens.AccentHover : UiKitTokens.Accent;
                case UiKitButtonKind.Danger:
                    return _down ? UiTheme.Darken(UiKitTokens.DangerDark, 0.2f) : _hover ? UiKitTokens.Danger : UiKitTokens.DangerDark;
                case UiKitButtonKind.Ghost:
                    return _down ? UiKitTokens.SurfaceRaised : _hover ? UiKitTokens.SurfaceHover : new Color(1f, 1f, 1f, 0f);
                default:
                    return _down ? UiKitTokens.Surface : _hover ? UiKitTokens.SurfaceHover : UiKitTokens.SurfaceRaised;
            }
        }

        private Color LabelColor()
        {
            if (_button != null && !_button.interactable) return UiKitTokens.TextMuted;
            return UiKitTokens.Text;
        }

        private void Update()
        {
            if (_bg == null) return;
            var t = Target();
            var k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / (UiKitTokens.Transition * 0.4f));
            _current = Color.Lerp(_current, t, k);
            _bg.color = _current;
            if (_label != null) _label.color = LabelColor();
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _hover = true;
            if (_button == null || _button.interactable) UiSounds.Play(UiSfx.Hover);
        }
        public void OnPointerExit(PointerEventData e) { _hover = false; _down = false; }
        public void OnPointerDown(PointerEventData e) => _down = true;
        public void OnPointerUp(PointerEventData e) => _down = false;

        public static void SetLabel(Button b, string text)
        {
            var t = b != null ? b.GetComponentInChildren<Text>(true) : null;
            if (t != null) t.text = text;
        }
    }

    /// <summary>Toggle / Slider / Dropdown görünümünü yeni palete çeker (davranış değişmez).</summary>
    public static class UiKitRestyle
    {
        public static void Toggle(Toggle t)
        {
            if (t == null) return;
            foreach (var g in t.GetComponentsInChildren<Image>(true))
            {
                if (g.gameObject == t.gameObject) continue;
                if (t.graphic == g) g.color = UiKitTokens.Sand;
                else g.color = UiKitTokens.SurfaceRaised;
            }
        }

        public static void Slider(Slider s)
        {
            if (s == null) return;
            if (s.fillRect != null) { var f = s.fillRect.GetComponent<Image>(); if (f != null) f.color = UiKitTokens.Sand; }
            if (s.handleRect != null) { var h = s.handleRect.GetComponent<Image>(); if (h != null) h.color = UiKitTokens.Text; }
            var bg = s.transform.Find("Background");
            if (bg != null) { var i = bg.GetComponent<Image>(); if (i != null) i.color = UiKitTokens.SurfaceRaised; }
        }

        public static void Dropdown(Dropdown d)
        {
            if (d == null) return;
            var img = d.GetComponent<Image>();
            if (img != null) img.color = UiKitTokens.SurfaceRaised;
            if (d.captionText != null) d.captionText.color = UiKitTokens.Text;
        }

        /// <summary>Alt ağaçtaki tüm Toggle/Slider/Dropdown'ları yeniden boyar.</summary>
        public static void All(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Toggle>(true)) Toggle(t);
            foreach (var s in root.GetComponentsInChildren<Slider>(true)) Slider(s);
            foreach (var d in root.GetComponentsInChildren<Dropdown>(true)) Dropdown(d);
        }
    }

    /// <summary>Sekme çubuğu (dikey sol menü ya da yatay). Seçilen sekme kum rengi vurgu şeridi alır.</summary>
    public sealed class UiKitTabBar : MonoBehaviour
    {
        private readonly List<Image> _bgs = new List<Image>();
        private readonly List<Text> _labels = new List<Text>();
        private readonly List<Image> _bars = new List<Image>();
        private Action<int> _onSelect;
        private int _index = -1;

        public int Index => _index;

        public static UiKitTabBar Create(Transform parent, IList<string> names, bool vertical, Action<int> onSelect, float tabWidth, float tabHeight)
        {
            var list = vertical ? UiFactory.VerticalList(parent, 6f, 0, TextAnchor.UpperLeft) : UiFactory.HorizontalList(parent, 6f, 0, TextAnchor.MiddleLeft);
            list.gameObject.name = "TabBar";
            var bar = list.gameObject.AddComponent<UiKitTabBar>();
            bar._onSelect = onSelect;
            for (var i = 0; i < names.Count; i++)
            {
                var idx = i;
                var rt = UiFactory.CreateRect("Tab_" + names[i], list);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = UiSprites.GetRoundedRect(8);
                img.type = Image.Type.Sliced;
                img.color = new Color(1f, 1f, 1f, 0f);
                var btn = rt.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = img;
                btn.onClick.AddListener(() =>
                {
                    UiSounds.Play(UiSfx.Tab);
                    bar.Select(idx);
                });
                var le = rt.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = tabWidth; le.preferredHeight = tabHeight; le.minHeight = tabHeight;
                var accent = UiFactory.Image(rt, null, UiKitTokens.Accent);
                accent.raycastTarget = false;
                UiFactory.SetRect(accent, vertical ? new Vector2(0f, 0f) : new Vector2(0.1f, 0f), vertical ? new Vector2(0f, 1f) : new Vector2(0.9f, 0f),
                    Vector2.zero, vertical ? new Vector2(2f, 0f) : new Vector2(0f, 2f));
                accent.enabled = false;
                var label = UiFactory.Label(rt, names[i], UiKitTokens.FontLabel, vertical ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, UiKitTokens.TextDim, FontStyle.Bold);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiFactory.Stretch(label, 22f, 0f, 8f, 0f);
                bar._bgs.Add(img); bar._labels.Add(label); bar._bars.Add(accent);
            }
            return bar;
        }

        public void Select(int index, bool notify = true)
        {
            if (index < 0 || index >= _bgs.Count) return;
            _index = index;
            for (var i = 0; i < _bgs.Count; i++)
            {
                var on = i == index;
                _bgs[i].color = on ? UiKitTokens.SurfaceRaised : new Color(1f, 1f, 1f, 0f);
                _labels[i].color = on ? UiKitTokens.Text : UiKitTokens.TextDim;
                _bars[i].enabled = on;
            }
            if (notify) _onSelect?.Invoke(index);
        }
    }

    /// <summary>İpucu balonu: üzerine gelince imleci izleyen küçük kart (tek örnek, tuvale eklenir).</summary>
    public sealed class UiKitTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static RectTransform _tip;
        private static Text _tipText;
        private static RectTransform _tipCanvas;
        private string _text;

        /// <summary>Hedefe ipucu metni bağlar.</summary>
        public static void Attach(Component target, string text)
        {
            if (target == null || string.IsNullOrEmpty(text)) return;
            var t = target.GetComponent<UiKitTooltip>() ?? target.gameObject.AddComponent<UiKitTooltip>();
            t._text = text;
        }

        public void OnPointerEnter(PointerEventData e) => Show(_text, transform);
        public void OnPointerExit(PointerEventData e) => Hide();
        private void OnDisable() => Hide();

        private static void Show(string text, Transform from)
        {
            var canvas = from != null ? from.GetComponentInParent<Canvas>() : null;
            if (canvas == null || string.IsNullOrEmpty(text)) return;
            canvas = canvas.rootCanvas;
            if (_tip == null || _tipCanvas != canvas.transform)
            {
                _tipCanvas = canvas.transform as RectTransform;
                _tip = UiKitPanel.Card(canvas.transform, UiTheme.Hex(0x0E, 0x10, 0x13, 0xF5), 8);
                _tip.gameObject.name = "[Tooltip]";
                _tip.GetComponent<Image>().raycastTarget = false;
                _tip.pivot = new Vector2(0f, 1f);
                _tip.anchorMin = _tip.anchorMax = new Vector2(0f, 0f);
                _tip.sizeDelta = new Vector2(380f, 64f);
                _tipText = UiFactory.Label(_tip, "", UiKitTokens.FontBody - 2, TextAnchor.MiddleLeft, UiKitTokens.Text);
                UiFactory.Stretch(_tipText, 12f, 6f, 12f, 6f);
                _tip.gameObject.AddComponent<UiKitTooltipFollow>();
            }
            _tipText.text = text;
            _tip.SetAsLastSibling();
            _tip.gameObject.SetActive(true);
        }

        private static void Hide()
        {
            if (_tip != null) _tip.gameObject.SetActive(false);
        }

        private sealed class UiKitTooltipFollow : MonoBehaviour
        {
            private void LateUpdate()
            {
                var m = Mouse.current;
                if (m == null || _tipCanvas == null) return;
                var canvas = _tipCanvas.GetComponent<Canvas>();
                var scale = canvas != null ? canvas.scaleFactor : 1f;
                var p = m.position.ReadValue() / Mathf.Max(0.01f, scale) + new Vector2(18f, -18f);
                var rect = _tipCanvas.rect;
                p.x = Mathf.Min(p.x, rect.width - _tip.sizeDelta.x - 8f);
                p.y = Mathf.Max(p.y, _tip.sizeDelta.y + 8f);
                _tip.anchoredPosition = p;
            }
        }
    }

    /// <summary>Kaydırma listesi kenarlarında silinen (solan) gradyanlar: kaydırılabilir içerik olduğunu belli eder.</summary>
    public sealed class UiKitScrollFade : MonoBehaviour
    {
        private ScrollRect _scroll;
        private Image _top, _bottom, _left, _right;

        /// <summary>Dikey kaydırma listesi + üst/alt solma. İçeriği <paramref name="content"/> altına ekleyin.</summary>
        public static ScrollRect VerticalList(Transform parent, out RectTransform content, Color fadeColor, float spacing = 8f, int padding = 0)
        {
            var scroll = UiWidgets.ScrollList(parent, out content, spacing, padding);
            var fade = scroll.gameObject.AddComponent<UiKitScrollFade>();
            fade._scroll = scroll;
            fade._top = fade.Edge(scroll.transform, true, true, fadeColor);
            fade._bottom = fade.Edge(scroll.transform, true, false, fadeColor);
            return scroll;
        }

        /// <summary>Mevcut yatay ScrollRect'e sol/sağ solma ekler.</summary>
        public static void AttachHorizontal(ScrollRect scroll, Color fadeColor)
        {
            var fade = scroll.gameObject.AddComponent<UiKitScrollFade>();
            fade._scroll = scroll;
            fade._left = fade.Edge(scroll.transform, false, true, fadeColor);
            fade._right = fade.Edge(scroll.transform, false, false, fadeColor);
        }

        private Image Edge(Transform parent, bool vertical, bool startSide, Color color)
        {
            var img = UiFactory.Image(parent, vertical ? UiSprites.VerticalGradient : UiSprites.HorizontalGradient, color);
            img.raycastTarget = false;
            img.gameObject.name = "Fade";
            const float size = 36f;
            if (vertical)
            {
                // VerticalGradient: altta opak, üstte saydam; üst kenar için ters çevir.
                img.rectTransform.localScale = new Vector3(1f, startSide ? -1f : 1f, 1f);
                UiFactory.SetRect(img, startSide ? new Vector2(0f, 1f) : new Vector2(0f, 0f), startSide ? new Vector2(1f, 1f) : new Vector2(1f, 0f),
                    startSide ? new Vector2(0f, -size) : Vector2.zero, startSide ? Vector2.zero : new Vector2(-14f, size));
            }
            else
            {
                img.rectTransform.localScale = new Vector3(startSide ? 1f : -1f, 1f, 1f);
                UiFactory.SetRect(img, startSide ? new Vector2(0f, 0f) : new Vector2(1f, 0f), startSide ? new Vector2(0f, 1f) : new Vector2(1f, 1f),
                    startSide ? Vector2.zero : new Vector2(-size, 0f), startSide ? new Vector2(size, 0f) : Vector2.zero);
            }
            return img;
        }

        private void LateUpdate()
        {
            if (_scroll == null || _scroll.content == null || _scroll.viewport == null) return;
            if (_top != null)
            {
                var range = _scroll.content.rect.height - _scroll.viewport.rect.height;
                var v = range <= 1f ? 1f : _scroll.verticalNormalizedPosition;
                SetA(_top, range <= 1f ? 0f : 1f - v);
                SetA(_bottom, range <= 1f ? 0f : v);
            }
            if (_left != null)
            {
                var range = _scroll.content.rect.width - _scroll.viewport.rect.width;
                var h = range <= 1f ? 0f : _scroll.horizontalNormalizedPosition;
                SetA(_left, range <= 1f ? 0f : h);
                SetA(_right, range <= 1f ? 0f : 1f - h);
            }
        }

        private static void SetA(Image img, float a)
        {
            if (img == null) return;
            var c = img.color; c.a = Mathf.Clamp01(a) * 0.95f;
            if (!Mathf.Approximately(img.color.a, c.a)) img.color = c;
        }
    }

    /// <summary>Kısa ölçek "pop" + solma canlandırması (ödül talep animasyonu vb.).</summary>
    public sealed class UiKitPop : MonoBehaviour
    {
        private float _t;
        private float _dur = 0.45f;

        public static void Play(RectTransform target, float duration = 0.45f)
        {
            if (target == null) return;
            var p = target.GetComponent<UiKitPop>() ?? target.gameObject.AddComponent<UiKitPop>();
            p._t = 0f; p._dur = Mathf.Max(0.05f, duration);
            p.enabled = true;
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            var k = Mathf.Clamp01(_t / _dur);
            // Aşım yapan ease (back-out) -> 1
            var c1 = 2.2f; var x = k - 1f;
            var s = 1f + (c1 + 1f) * x * x * x + c1 * x * x;
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.7f, 1f, s);
            if (k >= 1f) { transform.localScale = Vector3.one; enabled = false; }
        }
    }

    /// <summary>Kart zeminine nadirlik parıltısı (kenar çizgisi + yumuşak dış ışık; nefes alır).</summary>
    public sealed class UiKitGlow : MonoBehaviour
    {
        private Image _halo;
        private Color _color;
        private bool _pulse;

        public static void Attach(RectTransform card, Color color, bool pulse)
        {
            var halo = UiFactory.Image(card, UiKitPanel.SoftShadow, color);
            halo.raycastTarget = false;
            halo.type = Image.Type.Sliced;
            halo.gameObject.name = "Glow";
            UiFactory.SetRect(halo, Vector2.zero, Vector2.one, new Vector2(-16f, -16f), new Vector2(16f, 16f));
            halo.transform.SetAsFirstSibling();
            var g = card.gameObject.AddComponent<UiKitGlow>();
            g._halo = halo; g._color = color; g._pulse = pulse;
            halo.color = UiTheme.WithAlpha(color, 0.35f);
            g.enabled = pulse;
        }

        private void Update()
        {
            if (!_pulse || _halo == null) return;
            _halo.color = UiTheme.WithAlpha(_color, 0.3f + 0.2f * Mathf.Sin(Time.unscaledTime * 2.4f));
        }
    }

    /// <summary>
    /// Havuzlu köz alanı: verilen RectTransform içinde 10-16 küçük kırmızı-turuncu köz yukarı süzülür ve titreşir.
    /// UGUI Image'lar bir kez kurulur, kare başına yalnızca değer ataması yapılır (tahsis yok); zamanlama unscaled dt ile.
    /// </summary>
    public sealed class EmberField : MonoBehaviour
    {
        private RectTransform _rect;
        private RectTransform[] _rts;
        private Image[] _imgs;
        private float[] _x, _y, _speed, _sway, _swayFreq, _phase, _flickFreq, _size;
        private uint _seed = 2463534242u;
        private float _time;

        /// <summary>Alanı kurar (count 10-16 aralığına sıkıştırılır). Aynı hedefte ikinci çağrı mevcut bileşeni döndürür.</summary>
        public static EmberField Attach(RectTransform area, int count = 14)
        {
            if (area == null)
                return null;
            var existing = area.GetComponent<EmberField>();
            if (existing != null)
                return existing;
            var f = area.gameObject.AddComponent<EmberField>();
            f.Build(Mathf.Clamp(count, 10, 16));
            return f;
        }

        private float Rand()
        {
            _seed ^= _seed << 13;
            _seed ^= _seed >> 17;
            _seed ^= _seed << 5;
            return (_seed & 0xFFFFFF) / (float)0x1000000;
        }

        private void Build(int n)
        {
            _rect = (RectTransform)transform;
            _rts = new RectTransform[n];
            _imgs = new Image[n];
            _x = new float[n]; _y = new float[n]; _speed = new float[n]; _sway = new float[n];
            _swayFreq = new float[n]; _phase = new float[n]; _flickFreq = new float[n]; _size = new float[n];
            var sprite = UiSprites.SoftCircle;
            for (var i = 0; i < n; i++)
            {
                var img = UiFactory.Image(_rect, sprite, new Color(1f, 0.5f, 0.2f, 0f));
                img.gameObject.name = "Ember" + i;
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
                _rts[i] = rt;
                _imgs[i] = img;
                Respawn(i, true);
            }
        }

        private void Respawn(int i, bool scatter)
        {
            _x[i] = Rand();
            _y[i] = scatter ? Rand() : -Rand() * 0.08f;
            _speed[i] = 0.025f + Rand() * 0.05f;      // alan yüksekliği / sn
            _sway[i] = 4f + Rand() * 10f;
            _swayFreq[i] = 0.6f + Rand() * 1.4f;
            _phase[i] = Rand() * 6.2831853f;
            _flickFreq[i] = 5f + Rand() * 9f;
            _size[i] = 2.5f + Rand() * 3.5f;
        }

        private void Update()
        {
            var dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            _time += dt;
            var r = _rect.rect;
            var w = r.width;
            var h = r.height;
            for (var i = 0; i < _rts.Length; i++)
            {
                _y[i] += _speed[i] * dt;
                if (_y[i] > 1.05f)
                    Respawn(i, false);

                var y = _y[i];
                var px = _x[i] * w + Mathf.Sin(_time * _swayFreq[i] + _phase[i]) * _sway[i];
                _rts[i].anchoredPosition = new Vector2(px, y * h);
                var s = _size[i] * (1f - 0.35f * Mathf.Clamp01(y));
                _rts[i].sizeDelta = new Vector2(s, s);

                var flick = 0.55f + 0.45f * Mathf.Sin(_time * _flickFreq[i] + _phase[i]);
                var env = Mathf.Clamp01(y * 8f) * Mathf.Clamp01((1.05f - y) * 3f);
                var heat = Mathf.Clamp01(y * 1.2f);
                var c = Color.Lerp(UiKitTokens.Ember, UiKitTokens.Accent, heat);
                c.a = 0.85f * flick * env;
                _imgs[i].color = c;
            }
        }
    }

    /// <summary>
    /// Başlık üzerinde 8 sn'de bir kayan kırmızı parıltı: metnin kopyası dar bir RectMask2D içinde, maske kaydıkça
    /// kopya ters yönde kaydırılır (kopya sabit kalır, yalnızca maskenin içi görünür). Kare hızından bağımsız.
    /// </summary>
    public sealed class TitleGlint : MonoBehaviour
    {
        public const float Period = 8f;
        public const float SweepSeconds = 0.7f;
        private const float BandWidth = 70f;

        private Text _source;
        private Text _copy;
        private RectTransform _mask;
        private RectTransform _copyRect;
        private RectTransform _self;
        private float _clock;

        /// <summary>Başlık Text'ine parıltı ekler (tekrar çağrı yok sayılır).</summary>
        public static TitleGlint Attach(Text title)
        {
            if (title == null)
                return null;
            var existing = title.GetComponent<TitleGlint>();
            if (existing != null)
                return existing;
            var g = title.gameObject.AddComponent<TitleGlint>();
            g.Build(title);
            return g;
        }

        private void Build(Text title)
        {
            _source = title;
            _self = (RectTransform)title.transform;
            _clock = Period - 1.5f; // ilk parıltı menü açıldıktan kısa süre sonra
            _mask = UiFactory.CreateRect("GlintMask", _self);
            _mask.gameObject.AddComponent<RectMask2D>();
            _mask.anchorMin = _mask.anchorMax = Vector2.zero;
            _mask.pivot = Vector2.zero;
            var copy = UiFactory.Label(_mask, title.text, title.fontSize, title.alignment, new Color(1f, 0.42f, 0.34f, 0f), title.fontStyle);
            copy.gameObject.name = "GlintText";
            copy.horizontalOverflow = title.horizontalOverflow;
            copy.verticalOverflow = title.verticalOverflow;
            copy.raycastTarget = false;
            _copy = copy;
            _copyRect = copy.rectTransform;
            _copyRect.anchorMin = _copyRect.anchorMax = Vector2.zero;
            _copyRect.pivot = Vector2.zero;
            _mask.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_source == null || _mask == null)
                return;
            _clock += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (_clock < Period)
                return;
            var t = (_clock - Period) / SweepSeconds;
            if (t >= 1f)
            {
                _clock = 0f;
                _mask.gameObject.SetActive(false);
                return;
            }

            if (!_mask.gameObject.activeSelf)
                _mask.gameObject.SetActive(true);
            if (!ReferenceEquals(_copy.text, _source.text))
                _copy.text = _source.text;

            var size = _self.rect.size;
            var x = Mathf.Lerp(-BandWidth, size.x * 0.75f, MainMenuMotion.EaseOutCubic(t));
            _mask.anchoredPosition = new Vector2(x, 0f);
            _mask.sizeDelta = new Vector2(BandWidth, size.y);
            _copyRect.sizeDelta = size;
            _copyRect.anchoredPosition = new Vector2(-x, 0f);
            var a = Mathf.Sin(t * Mathf.PI);
            var c = _copy.color;
            c.a = a;
            _copy.color = c;
        }
    }
}
