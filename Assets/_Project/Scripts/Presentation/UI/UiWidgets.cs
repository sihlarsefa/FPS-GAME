using System;
using System.Collections.Generic;
using Project.Infrastructure.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Bileşik arayüz parçaları (ayar satırları, başlıklar, kaydırılabilir liste, tuş ipucu). Hepsi
    /// <see cref="UiFactory"/> temelleri ve <see cref="UiTheme"/> ile kurulur; listelerde doğru boyut almak için
    /// LayoutElement taşır.
    /// </summary>
    public static class UiWidgets
    {
        /// <summary>
        /// Bölüm başlığı: solda kırmızı vurgu şeridi, büyük harfli kalın kehribar yazı. 48 px yükseklik. Yazıyı döndürür.
        /// </summary>
        public static Text Header(Transform parent, string title, int size = UiTheme.FontLarge)
        {
            var row = UiFactory.CreateRect("Header", parent);
            UiFactory.LayoutSize(row, -1f, size + 18f, 1f);

            var strip = UiFactory.Image(row, null, UiTheme.Accent);
            strip.gameObject.name = "Accent";
            UiFactory.SetRect(strip, new Vector2(0f, 0.15f), new Vector2(0f, 0.85f), Vector2.zero, new Vector2(6f, 0f));

            var text = UiFactory.Label(row, title, size, TextAnchor.MiddleLeft, UiTheme.TextHeader, FontStyle.Bold);
            UiFactory.Stretch(text, 18f, 0f, 0f, 0f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }

        /// <summary>
        /// Etiketli ayar kaydırıcısı satırı: solda etiket, ortada kaydırıcı, sağda biçimlendirilmiş değer.
        /// <paramref name="format"/> null ise tam sayılarda "0", diğerlerinde "0.00" kullanılır. Kaydırıcıyı döndürür.
        /// </summary>
        public static Slider LabeledSlider(Transform parent, string label, float min, float max, float value, Action<float> onChanged,
            Func<float, string> format = null, bool wholeNumbers = false)
        {
            var row = UiFactory.CreateRect("Row_" + label, parent);
            UiFactory.LayoutSize(row, -1f, UiTheme.RowHeight, 1f);

            var text = UiFactory.Label(row, label, UiTheme.FontNormal, TextAnchor.MiddleLeft, UiTheme.Text);
            UiFactory.SetRect(text, new Vector2(0f, 0f), new Vector2(0.42f, 1f), Vector2.zero, Vector2.zero);

            var valueText = UiFactory.Label(row, string.Empty, UiTheme.FontNormal, TextAnchor.MiddleRight, UiTheme.Amber, FontStyle.Bold);
            UiFactory.SetRect(valueText, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-90f, 0f), Vector2.zero);

            Func<float, string> fmt = format ?? (wholeNumbers ? (Func<float, string>)(v => v.ToString("0")) : v => v.ToString("0.00"));
            var slider = UiFactory.Slider(row, min, max, value, v =>
            {
                valueText.text = fmt(v);
                onChanged?.Invoke(v);
            }, wholeNumbers);
            UiFactory.SetRect(slider, new Vector2(0.44f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -14f), new Vector2(-104f, 14f));
            valueText.text = fmt(slider.value);
            return slider;
        }

        /// <summary>Etiketli onay kutusu satırı (listelerde tam genişlik). Onay kutusunu döndürür.</summary>
        public static Toggle LabeledToggle(Transform parent, string label, bool value, Action<bool> onChanged)
        {
            var toggle = UiFactory.Toggle(parent, label, value, onChanged);
            UiFactory.LayoutSize(toggle, -1f, UiTheme.RowHeight, 1f);
            return toggle;
        }

        /// <summary>
        /// Seçenek döngüleyici satırı: "Etiket   ◀ Değer ▶". Sol/sağ ok düğmeleri ve klavye/gamepad sol-sağ ile değişir.
        /// </summary>
        public static UiOptionSelector OptionSelector(Transform parent, string label, IReadOnlyList<string> options, int index, Action<int> onChanged)
        {
            return UiOptionSelector.Create(parent, label, options, index, onChanged);
        }

        /// <summary>
        /// Anahtar-değer satırı (istatistik/maç sonu ekranları): solda soluk anahtar, sağda kalın değer.
        /// Satırı döndürür; değer yazısı <paramref name="valueText"/> ile güncellenebilir.
        /// </summary>
        public static RectTransform KeyValueRow(Transform parent, string key, string value, out Text valueText, int size = UiTheme.FontNormal)
        {
            var row = UiFactory.CreateRect("KV_" + key, parent);
            UiFactory.LayoutSize(row, -1f, size + 12f, 1f);

            var k = UiFactory.Label(row, key, size, TextAnchor.MiddleLeft, UiTheme.TextDim);
            k.horizontalOverflow = HorizontalWrapMode.Overflow;
            valueText = UiFactory.Label(row, value, size, TextAnchor.MiddleRight, UiTheme.Text, FontStyle.Bold);
            valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
            return row;
        }

        /// <summary>
        /// Tuş ipucu: koyu kutu içinde tuş harfi + yanında eylem yazısı (ör. "[F] Al"). Ebeveyn merkezinde, içeriğe göre
        /// genişler. Kökü döndürür; eylem yazısı <paramref name="actionText"/>.
        /// </summary>
        public static RectTransform KeyHint(Transform parent, string key, string action, out Text actionText, int size = UiTheme.FontNormal)
        {
            var row = UiFactory.HorizontalList(parent, 10f, 0, TextAnchor.MiddleCenter);
            row.gameObject.name = "KeyHint_" + key;
            UiFactory.Anchor(row, UiAnchor.Center, Vector2.zero, new Vector2(300f, size + 16f));
            UiFactory.FitContent(row, false);
            var group = row.GetComponent<HorizontalLayoutGroup>();
            group.childForceExpandHeight = true;

            var keyBox = UiFactory.Panel(row, UiTheme.Hex(0xEC, 0xEB, 0xE0, 0xF0), UiSprites.GetRoundedRect(4));
            keyBox.gameObject.name = "Key";
            keyBox.GetComponent<Image>().raycastTarget = false;
            var keyWidth = Mathf.Max(size + 12f, (key?.Length ?? 1) * size * 0.62f + 16f);
            UiFactory.LayoutSize(keyBox, keyWidth, size + 16f);
            var keyText = UiFactory.Label(keyBox, key, size, TextAnchor.MiddleCenter, UiTheme.Background, FontStyle.Bold);
            keyText.horizontalOverflow = HorizontalWrapMode.Overflow;

            actionText = UiFactory.Label(row, action, size, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold);
            actionText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.AddShadow(actionText, UiTheme.TextShadow, new Vector2(1f, -1f));
            return row;
        }

        /// <summary>
        /// Dikey kaydırılabilir liste: maskeli görüntü alanı, içerik (VerticalLayoutGroup + ContentSizeFitter) ve ince
        /// kaydırma çubuğu. Ebeveyni doldurur. Öğeleri <paramref name="content"/> altına ekleyin.
        /// </summary>
        public static ScrollRect ScrollList(Transform parent, out RectTransform content, float spacing = UiTheme.Spacing, int padding = 0)
        {
            var root = UiFactory.CreateRect("ScrollList", parent);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            var viewport = UiFactory.CreateRect("Viewport", root);
            UiFactory.Stretch(viewport, 0f, 0f, 14f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var vpImage = viewport.gameObject.AddComponent<Image>();
            vpImage.color = new Color(0f, 0f, 0f, 0f);
            vpImage.raycastTarget = true;

            content = UiFactory.VerticalList(viewport, spacing, padding, TextAnchor.UpperLeft);
            content.gameObject.name = "Content";
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);
            UiFactory.FitContent(content, true);

            var bar = UiFactory.CreateRect("Scrollbar", root);
            UiFactory.SetRect(bar, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-8f, 0f), Vector2.zero);
            var barImage = bar.gameObject.AddComponent<Image>();
            barImage.sprite = UiSprites.GetRoundedRect(3);
            barImage.type = Image.Type.Sliced;
            barImage.color = UiTheme.Track;
            barImage.raycastTarget = true;
            var slidingArea = UiFactory.CreateRect("Sliding Area", bar);
            var handle = UiFactory.CreateRect("Handle", slidingArea);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = UiSprites.GetRoundedRect(3);
            handleImage.type = Image.Type.Sliced;
            handleImage.color = Color.white;
            handleImage.raycastTarget = true;
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = UiTheme.PanelBorder;
            colors.highlightedColor = UiTheme.Khaki;
            colors.pressedColor = UiTheme.Amber;
            colors.selectedColor = UiTheme.Khaki;
            colors.disabledColor = UiTheme.ButtonDisabled;
            scrollbar.colors = colors;
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };

            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 40f;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scroll.verticalScrollbarSpacing = 4f;
            return scroll;
        }

        /// <summary>
        /// Seçilebilir öğenin etkileşimini değiştirir ve görsel geri bildirimini (yazı rengi, vurgu şeridi) günceller.
        /// </summary>
        public static void SetInteractable(Selectable selectable, bool interactable)
        {
            if (selectable == null)
                return;
            selectable.interactable = interactable;
            var feedback = selectable.GetComponent<UiButtonFeedback>();
            if (feedback != null)
                feedback.Refresh();
        }

        /// <summary>Arayüz sesi çalar (üzerine gelme sesi hız sınırlıdır). Ses sistemi yoksa sessizce geçer.</summary>
        public static void PlaySound(SoundId id) => UiButtonFeedback.PlaySound(id);

        private const int NumberCacheMin = -100;
        private const int NumberCacheMax = 1000;
        private static string[] _numberCache;

        /// <summary>
        /// Tam sayıyı metne çevirir; -100..1000 arası önbellekten döner (HUD'da kare başına çöp üretmez).
        /// </summary>
        public static string Number(int value)
        {
            if (value < NumberCacheMin || value > NumberCacheMax)
                return value.ToString();
            if (_numberCache == null)
                _numberCache = new string[NumberCacheMax - NumberCacheMin + 1];
            var index = value - NumberCacheMin;
            return _numberCache[index] ??= value.ToString();
        }

        private static string[] _clockCache;

        /// <summary>
        /// Saniyeyi "dd:ss" biçimine çevirir (negatifler 0). 0..3599 sn önbellekten döner (çöp üretmez); daha uzunu "s:dd:ss".
        /// </summary>
        public static string Clock(float seconds)
        {
            var total = float.IsNaN(seconds) ? 0 : Mathf.Max(0, Mathf.CeilToInt(seconds));
            if (total >= 3600)
                return (total / 3600) + ":" + ((total / 60) % 60).ToString("00") + ":" + (total % 60).ToString("00");
            if (_clockCache == null)
                _clockCache = new string[3600];
            return _clockCache[total] ??= (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }
    }

    /// <summary>
    /// Seçilebilir öğeler için geri bildirim: üzerine gelince UiHover sesi ve seçme (fare ile gezinme = seçim; gamepad
    /// ile tutarlı vurgu), vurgu şeridi görünürlüğü, devre dışıyken soluk yazı. <see cref="UiFactory"/> ekler.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        private const float HoverSoundInterval = 0.06f;
        private static float _lastHoverSoundTime = -10f;
        private static bool _audioFailureLogged;

        private Selectable _selectable;
        private Graphic _accent;
        private Text _label;
        private Color _labelColor = UiTheme.Text;
        private bool _hovered;
        private bool _selected;

        /// <summary>Bağlı seçilebilir öğe.</summary>
        public Selectable Target => _selectable;

        /// <summary>Üzerine gelince seçimi bu öğeye taşı (varsayılan açık).</summary>
        public bool SelectOnHover { get; set; } = true;

        /// <summary>Üzerine gelme sesi çalınsın mı (varsayılan açık).</summary>
        public bool HoverSound { get; set; } = true;

        /// <summary>Fare imleci şu an öğenin üzerinde mi.</summary>
        public bool IsHovered => _hovered;

        /// <summary>Geri bildirimi bir öğeye bağlar. <paramref name="accent"/> ve <paramref name="label"/> isteğe bağlıdır.</summary>
        public void Bind(Selectable selectable, Graphic accent, Text label)
        {
            _selectable = selectable;
            _accent = accent;
            _label = label;
            if (_label != null)
                _labelColor = _label.color;
            Refresh();
        }

        /// <summary>Etkileşim/vurgu durumuna göre görselleri günceller.</summary>
        public void Refresh()
        {
            var interactable = IsInteractable();
            if (_accent != null)
                _accent.enabled = interactable && (_hovered || _selected);
            if (_label != null)
                _label.color = interactable ? _labelColor : UiTheme.WithAlpha(UiTheme.TextMuted, _labelColor.a);
        }

        /// <summary>Etkin yazı rengini değiştirir (devre dışı rengi otomatik).</summary>
        public void SetLabelColor(Color color)
        {
            _labelColor = color;
            Refresh();
        }

        /// <summary>Fare girişi: ses + (isteğe bağlı) seçim.</summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            if (IsInteractable())
            {
                if (HoverSound)
                    PlaySound(SoundId.UiHover);
                if (SelectOnHover && _selectable != null && EventSystem.current != null && !EventSystem.current.alreadySelecting)
                    _selectable.Select();
            }

            Refresh();
        }

        /// <summary>Fare çıkışı.</summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            Refresh();
        }

        /// <summary>Seçim (klavye/gamepad gezinmesi): fare üzerinde değilse ses çalar.</summary>
        public void OnSelect(BaseEventData eventData)
        {
            _selected = true;
            if (!_hovered && HoverSound && IsInteractable())
                PlaySound(SoundId.UiHover);
            Refresh();
        }

        /// <summary>Seçim kaldırıldı.</summary>
        public void OnDeselect(BaseEventData eventData)
        {
            _selected = false;
            Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnDisable()
        {
            _hovered = false;
            _selected = false;
            if (_accent != null)
                _accent.enabled = false;
        }

        private bool IsInteractable() => _selectable == null || _selectable.IsInteractable();

        /// <summary>
        /// Arayüz sesi çalar (<see cref="GameAudio.Play2D"/>). Üzerine gelme sesi 60 ms'de bir ile sınırlıdır.
        /// <see cref="UiFactory.SoundsEnabled"/> kapalıysa veya ses sistemi hata verirse sessizce geçer.
        /// </summary>
        public static void PlaySound(SoundId id)
        {
            if (!UiFactory.SoundsEnabled || id == SoundId.None)
                return;

            var volume = 0.8f;
            if (id == SoundId.UiHover)
            {
                var now = Time.unscaledTime;
                if (now - _lastHoverSoundTime < HoverSoundInterval)
                    return;
                _lastHoverSoundTime = now;
                volume = 0.45f;
            }

            try
            {
                GameAudio.Play2D(id, volume);
            }
            catch (Exception e)
            {
                if (!_audioFailureLogged)
                {
                    _audioFailureLogged = true;
                    Debug.LogWarning("[UI] Arayüz sesi çalınamadı: " + e.Message);
                }
            }
        }
    }

    /// <summary>
    /// İlerleme çubuğu: koyu yuva, doluluk görseli (Image.Type.Filled) ve isteğe bağlı gecikmeli "hasar izi"
    /// (değer düşünce iz bir süre bekleyip yavaşça iner), bölme çizgileri ve ortalanmış yazı. Değer değişmedikçe
    /// kare başına iş yapmaz (iz animasyonu bitince bileşen kendini kapatır).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiProgressBar : MonoBehaviour
    {
        private Image _background;
        private Image _trail;
        private Image _fill;
        private Text _label;
        private RectTransform _segmentRoot;
        private readonly List<Image> _segments = new List<Image>();

        private float _value = 1f;
        private float _trailValue = 1f;
        private float _trailWait;
        private bool _vertical;

        /// <summary>Yuva (arka plan) görseli.</summary>
        public Image Background => _background;

        /// <summary>Doluluk görseli (Filled).</summary>
        public Image Fill => _fill;

        /// <summary>Gecikmeli iz görseli (Filled, dolgunun arkasında).</summary>
        public Image Trail => _trail;

        /// <summary>Çubuk yazısı (ilk <see cref="SetLabel"/> çağrısında oluşturulur; o zamana dek null).</summary>
        public Text Label => _label;

        /// <summary>İz efekti açık mı (varsayılan açık).</summary>
        public bool TrailEnabled { get; set; } = true;

        /// <summary>Değer düştükten sonra izin inmeye başlamadan önce beklediği süre (sn, ölçeksiz).</summary>
        public float TrailDelay { get; set; } = 0.35f;

        /// <summary>İzin iniş hızı (doluluk/sn).</summary>
        public float TrailSpeed { get; set; } = 0.8f;

        /// <summary>Geçerli değer (0..1). Atama <see cref="SetValue"/> ile aynıdır (iz animasyonlu).</summary>
        public float Value
        {
            get => _value;
            set => SetValue(value, false);
        }

        /// <summary>Dolgu rengi.</summary>
        public Color FillColor
        {
            get => _fill != null ? _fill.color : Color.white;
            set
            {
                if (_fill != null && _fill.color != value)
                    _fill.color = value;
            }
        }

        /// <summary>Yuva rengi.</summary>
        public Color BackgroundColor
        {
            get => _background != null ? _background.color : Color.black;
            set
            {
                if (_background != null)
                    _background.color = value;
            }
        }

        /// <summary>İz rengi (varsayılan yarı saydam kırmızı-turuncu).</summary>
        public Color TrailColor
        {
            get => _trail != null ? _trail.color : Color.white;
            set
            {
                if (_trail != null)
                    _trail.color = value;
            }
        }

        /// <summary>Dikey mi (alttan yukarı dolar). Varsayılan yatay (soldan sağa).</summary>
        public bool Vertical
        {
            get => _vertical;
            set
            {
                _vertical = value;
                ApplyFillMethod(_fill);
                ApplyFillMethod(_trail);
                LayoutSegments();
            }
        }

        /// <summary>
        /// Yeni ilerleme çubuğu oluşturur (240×14, ebeveyn merkezinde; LayoutElement ile listelerde 14 px yükseklik).
        /// </summary>
        public static UiProgressBar Create(Transform parent, Color fillColor, Color backgroundColor)
        {
            var rt = UiFactory.CreateRect("ProgressBar", parent);
            UiFactory.Anchor(rt, UiAnchor.Center, Vector2.zero, new Vector2(240f, 14f));

            var bar = rt.gameObject.AddComponent<UiProgressBar>();

            bar._background = rt.gameObject.AddComponent<Image>();
            bar._background.sprite = UiSprites.White;
            bar._background.color = backgroundColor;
            bar._background.raycastTarget = false;

            bar._trail = CreateFillImage("Trail", rt, UiTheme.WithAlpha(UiTheme.Hex(0xFF, 0x7A, 0x45), 0.85f));
            bar._fill = CreateFillImage("Fill", rt, fillColor);

            var layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 240f;
            layout.preferredHeight = 14f;
            layout.minHeight = 6f;

            bar.SetValue(1f, true);
            bar.enabled = false;
            return bar;
        }

        /// <summary>
        /// Değeri ayarlar (0..1'e kırpılır). Değer düşerse iz gecikmeli iner; yükselirse iz hemen yetişir.
        /// <paramref name="instant"/> ise iz de anında ayarlanır. Değişiklik yoksa hiçbir şey yapmaz.
        /// </summary>
        public void SetValue(float value, bool instant = false)
        {
            value = float.IsNaN(value) ? 0f : Mathf.Clamp01(value);
            if (Mathf.Approximately(value, _value) && !instant)
                return;

            var previous = _value;
            _value = value;
            if (_fill != null)
                _fill.fillAmount = value;

            if (instant || !TrailEnabled || value >= _trailValue)
            {
                _trailValue = value;
                _trailWait = 0f;
                if (_trail != null)
                    _trail.fillAmount = value;
                enabled = false;
                return;
            }

            if (value < previous)
                _trailWait = TrailDelay;
            enabled = true;
        }

        /// <summary>
        /// Çubuk üstüne ortalanmış yazı koyar (ör. "75/100"). İlk çağrıda yazı oluşturulur; null/boş gizler.
        /// </summary>
        public void SetLabel(string text, int size = UiTheme.FontTiny)
        {
            if (_label == null)
            {
                if (string.IsNullOrEmpty(text))
                    return;
                _label = UiFactory.Label(transform, text, size, TextAnchor.MiddleCenter, UiTheme.Text, FontStyle.Bold);
                _label.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiFactory.AddShadow(_label, UiTheme.TextShadow, new Vector2(1f, -1f));
            }

            var show = !string.IsNullOrEmpty(text);
            if (_label.enabled != show)
                _label.enabled = show;
            if (show && !string.Equals(_label.text, text, StringComparison.Ordinal))
                _label.text = text;
            if (_label.fontSize != size)
                _label.fontSize = size;
        }

        /// <summary>
        /// Çubuğu <paramref name="count"/> eşit bölmeye ayıran ince çizgiler çizer (ör. 4 → 3 çizgi). 0/1 kaldırır.
        /// Çizgi nesneleri yeniden kullanılır.
        /// </summary>
        public void SetSegments(int count, Color lineColor, float lineWidth = 2f)
        {
            var lines = Mathf.Max(0, count - 1);
            if (lines > 0 && _segmentRoot == null)
            {
                _segmentRoot = UiFactory.CreateRect("Segments", transform);
                _segmentRoot.SetAsLastSibling();
                if (_label != null)
                    _label.transform.SetAsLastSibling();
            }

            for (var i = 0; i < lines; i++)
            {
                if (i >= _segments.Count)
                {
                    var image = UiFactory.Image(_segmentRoot, null, lineColor);
                    image.gameObject.name = "Segment" + i;
                    _segments.Add(image);
                }

                var seg = _segments[i];
                seg.color = lineColor;
                seg.gameObject.SetActive(true);
                seg.rectTransform.sizeDelta = _vertical ? new Vector2(0f, lineWidth) : new Vector2(lineWidth, 0f);
            }

            for (var i = lines; i < _segments.Count; i++)
                _segments[i].gameObject.SetActive(false);

            _segmentCount = lines + 1;
            LayoutSegments();
        }

        private int _segmentCount;

        private void LayoutSegments()
        {
            if (_segmentRoot == null || _segmentCount <= 1)
                return;
            for (var i = 0; i < _segmentCount - 1 && i < _segments.Count; i++)
            {
                var t = (i + 1) / (float)_segmentCount;
                var rt = _segments[i].rectTransform;
                var size = rt.sizeDelta;
                var width = Mathf.Max(size.x, size.y);
                if (_vertical)
                {
                    rt.anchorMin = new Vector2(0f, t);
                    rt.anchorMax = new Vector2(1f, t);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(0f, width);
                }
                else
                {
                    rt.anchorMin = new Vector2(t, 0f);
                    rt.anchorMax = new Vector2(t, 1f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(width, 0f);
                }

                rt.anchoredPosition = Vector2.zero;
            }
        }

        private void Update()
        {
            if (_trail == null || _trailValue <= _value)
            {
                enabled = false;
                return;
            }

            var dt = Time.unscaledDeltaTime;
            if (_trailWait > 0f)
            {
                _trailWait -= dt;
                return;
            }

            _trailValue = Mathf.MoveTowards(_trailValue, _value, TrailSpeed * dt);
            _trail.fillAmount = _trailValue;
            if (_trailValue <= _value)
                enabled = false;
        }

        private static Image CreateFillImage(string name, Transform parent, Color color)
        {
            var rt = UiFactory.CreateRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = UiSprites.White;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = 1f;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private void ApplyFillMethod(Image image)
        {
            if (image == null)
                return;
            if (_vertical)
            {
                image.fillMethod = Image.FillMethod.Vertical;
                image.fillOrigin = (int)Image.OriginVertical.Bottom;
            }
            else
            {
                image.fillMethod = Image.FillMethod.Horizontal;
                image.fillOrigin = (int)Image.OriginHorizontal.Left;
            }
        }
    }

    /// <summary>
    /// CanvasGroup tabanlı yumuşak göster/gizle (ölçeksiz zaman — oyun duraklatılmışken de çalışır). Geçiş bitince
    /// bileşen kendini kapatır; kare başına maliyet yalnızca geçiş sırasında.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiFader : MonoBehaviour
    {
        private CanvasGroup _group;
        private float _target = 1f;
        private float _speed = 1f;
        private bool _deactivateWhenHidden;

        /// <summary>Kontrol edilen CanvasGroup.</summary>
        public CanvasGroup Group
        {
            get
            {
                if (_group == null)
                    _group = UiFactory.EnsureCanvasGroup(this);
                return _group;
            }
        }

        /// <summary>Hedef durum görünür mü (geçiş sürüyor olabilir).</summary>
        public bool IsVisible => _target > 0.5f;

        /// <summary>Geçiş sürüyor mu.</summary>
        public bool IsAnimating => enabled && !Mathf.Approximately(Group.alpha, _target);

        /// <summary>Hedefe ulaşıldığında çağrılır (parametre: görünür mü).</summary>
        public event Action<bool> Completed;

        /// <summary>
        /// Hedef nesneye bir solma bileşeni ekler (veya mevcut olanı döndürür) ve başlangıç görünürlüğünü anında uygular.
        /// </summary>
        public static UiFader Attach(Component target, bool startVisible = true)
        {
            if (target == null)
                return null;
            var fader = target.GetComponent<UiFader>();
            if (fader == null)
                fader = target.gameObject.AddComponent<UiFader>();
            fader.SetVisibleInstant(startVisible);
            return fader;
        }

        /// <summary>Görünür hâle getirir (gerekirse GameObject'i etkinleştirir) ve <paramref name="duration"/> sn'de belirir.</summary>
        public void Show(float duration = UiTheme.FadeDuration)
        {
            _deactivateWhenHidden = false;
            if (!gameObject.activeSelf)
            {
                Group.alpha = 0f;
                gameObject.SetActive(true);
            }

            FadeTo(1f, duration);
        }

        /// <summary>
        /// <paramref name="duration"/> sn'de gizler; <paramref name="deactivate"/> ise sonunda GameObject'i kapatır.
        /// </summary>
        public void Hide(float duration = UiTheme.FadeDuration, bool deactivate = false)
        {
            _deactivateWhenHidden = deactivate;
            ReleaseSelection();
            if (!gameObject.activeInHierarchy)
            {
                SetVisibleInstant(false);
                if (deactivate && gameObject.activeSelf)
                    gameObject.SetActive(false);
                return;
            }

            FadeTo(0f, duration);
        }

        /// <summary>Olay sistemindeki seçili nesne bu panelin içindeyse seçimi temizler.</summary>
        private void ReleaseSelection()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.alreadySelecting)
                return;
            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(transform))
                eventSystem.SetSelectedGameObject(null);
        }

        /// <summary>Görünürlüğü anında uygular (geçişsiz). Gizlenirken GameObject açık kalır.</summary>
        public void SetVisibleInstant(bool visible)
        {
            if (!visible)
                ReleaseSelection();
            _target = visible ? 1f : 0f;
            var group = Group;
            group.alpha = _target;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            enabled = false;
        }

        private void FadeTo(float target, float duration)
        {
            _target = target;
            var group = Group;
            var visible = target > 0.5f;
            group.interactable = visible;
            group.blocksRaycasts = visible;

            if (duration <= 0.0001f)
            {
                group.alpha = target;
                Finish();
                return;
            }

            _speed = 1f / duration;
            enabled = true;
        }

        private void Update()
        {
            var group = Group;
            group.alpha = Mathf.MoveTowards(group.alpha, _target, _speed * Time.unscaledDeltaTime);
            if (Mathf.Approximately(group.alpha, _target))
            {
                group.alpha = _target;
                Finish();
            }
        }

        private void Finish()
        {
            enabled = false;
            var visible = _target > 0.5f;
            if (!visible && _deactivateWhenHidden)
                gameObject.SetActive(false);
            Completed?.Invoke(visible);
        }
    }

    /// <summary>
    /// Seçenek döngüleyici ("Kalite   ◀ Yüksek ▶"): ok düğmeleri, klavye/gamepad sol-sağ ile değer değiştirir.
    /// Ayar menüleri için açılır liste yerine kullanılır.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiOptionSelector : MonoBehaviour, IMoveHandler
    {
        private readonly List<string> _options = new List<string>();
        private Action<int> _onChanged;
        private int _index;
        private Text _label;
        private Text _valueText;
        private Button _previous;
        private Button _next;
        private Selectable _selectable;

        /// <summary>Son seçenekten sonra başa dönülsün mü (varsayılan açık).</summary>
        public bool Loop { get; set; } = true;

        /// <summary>Etiket yazısı.</summary>
        public Text Label => _label;

        /// <summary>Seçili değer yazısı.</summary>
        public Text ValueText => _valueText;

        /// <summary>Satırın seçilebilir bileşeni (gamepad gezinmesi için).</summary>
        public Selectable Selectable => _selectable;

        /// <summary>Seçenek sayısı.</summary>
        public int Count => _options.Count;

        /// <summary>Seçili seçenek metni (yoksa boş).</summary>
        public string CurrentOption => _index >= 0 && _index < _options.Count ? _options[_index] : string.Empty;

        /// <summary>Seçili dizin. Atama bildirim yapmaz (geri çağrı tetiklenmez).</summary>
        public int Index
        {
            get => _index;
            set => SetIndex(value, false);
        }

        /// <summary>
        /// Yeni seçenek satırı oluşturur (tam genişlik, <see cref="UiTheme.RowHeight"/> yükseklik).
        /// </summary>
        public static UiOptionSelector Create(Transform parent, string label, IReadOnlyList<string> options, int index, Action<int> onChanged)
        {
            var row = UiFactory.CreateRect("Option_" + label, parent);
            UiFactory.Anchor(row, UiAnchor.Center, Vector2.zero, new Vector2(560f, UiTheme.RowHeight));
            UiFactory.LayoutSize(row, -1f, UiTheme.RowHeight, 1f);

            var bg = row.gameObject.AddComponent<Image>();
            bg.sprite = UiSprites.GetRoundedRect(4);
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;
            bg.raycastTarget = true;

            var selector = row.gameObject.AddComponent<UiOptionSelector>();
            var selectable = row.gameObject.AddComponent<Selectable>();
            selectable.targetGraphic = bg;
            selectable.transition = Selectable.Transition.ColorTint;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = UiTheme.WithAlpha(UiTheme.ButtonHover, 0.6f);
            colors.selectedColor = UiTheme.WithAlpha(UiTheme.ButtonHover, 0.6f);
            colors.pressedColor = UiTheme.WithAlpha(UiTheme.ButtonPressed, 0.8f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0f);
            colors.fadeDuration = 0.08f;
            selectable.colors = colors;
            selectable.navigation = new Navigation { mode = Navigation.Mode.Vertical };
            selector._selectable = selectable;

            selector._label = UiFactory.Label(row, label, UiTheme.FontNormal, TextAnchor.MiddleLeft, UiTheme.Text);
            UiFactory.SetRect(selector._label, new Vector2(0f, 0f), new Vector2(0.44f, 1f), new Vector2(8f, 0f), Vector2.zero);

            selector._previous = ArrowButton(row, false, selector.Previous);
            UiFactory.SetRect(selector._previous, new Vector2(0.44f, 0.5f), new Vector2(0.44f, 0.5f), new Vector2(0f, -16f), new Vector2(32f, 16f));

            selector._next = ArrowButton(row, true, selector.Next);
            UiFactory.SetRect(selector._next, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-36f, -16f), new Vector2(-4f, 16f));

            selector._valueText = UiFactory.Label(row, string.Empty, UiTheme.FontNormal, TextAnchor.MiddleCenter, UiTheme.Amber, FontStyle.Bold);
            UiFactory.SetRect(selector._valueText, new Vector2(0.44f, 0f), new Vector2(1f, 1f), new Vector2(36f, 0f), new Vector2(-40f, 0f));
            selector._valueText.horizontalOverflow = HorizontalWrapMode.Overflow;

            var feedback = row.gameObject.AddComponent<UiButtonFeedback>();
            feedback.Bind(selectable, null, selector._label);

            selector.SetOptions(options, index);
            selector._onChanged = onChanged;
            return selector;
        }

        /// <summary>Seçenekleri değiştirir ve dizini (bildirimsiz) ayarlar.</summary>
        public void SetOptions(IReadOnlyList<string> options, int index)
        {
            _options.Clear();
            if (options != null)
            {
                for (var i = 0; i < options.Count; i++)
                    _options.Add(options[i] ?? string.Empty);
            }

            _index = -1;
            SetIndex(index, false);
        }

        /// <summary>Dizini ayarlar; <paramref name="notify"/> ise değiştiğinde geri çağrıyı tetikler.</summary>
        public void SetIndex(int index, bool notify)
        {
            if (_options.Count == 0)
            {
                _index = 0;
                UpdateVisuals();
                return;
            }

            index = Mathf.Clamp(index, 0, _options.Count - 1);
            var changed = index != _index;
            _index = index;
            UpdateVisuals();
            if (changed && notify)
                _onChanged?.Invoke(_index);
        }

        /// <summary>Sonraki seçeneğe geçer (tıklama sesiyle, bildirimli).</summary>
        public void Next() => Step(1);

        /// <summary>Önceki seçeneğe geçer (tıklama sesiyle, bildirimli).</summary>
        public void Previous() => Step(-1);

        /// <summary>Klavye/gamepad sol-sağ: seçeneği değiştirir.</summary>
        public void OnMove(AxisEventData eventData)
        {
            if (eventData == null || (_selectable != null && !_selectable.IsInteractable()))
                return;
            if (eventData.moveDir == MoveDirection.Left)
            {
                Previous();
                eventData.Use();
            }
            else if (eventData.moveDir == MoveDirection.Right)
            {
                Next();
                eventData.Use();
            }
        }

        private void Step(int delta)
        {
            if (_options.Count <= 1)
                return;
            var target = _index + delta;
            if (Loop)
                target = (target % _options.Count + _options.Count) % _options.Count;
            else
                target = Mathf.Clamp(target, 0, _options.Count - 1);
            if (target == _index)
                return;
            UiButtonFeedback.PlaySound(SoundId.UiClick);
            SetIndex(target, true);
        }

        private void UpdateVisuals()
        {
            if (_valueText != null)
                _valueText.text = CurrentOption;
            var canStep = _options.Count > 1;
            if (_previous != null)
                _previous.interactable = canStep && (Loop || _index > 0);
            if (_next != null)
                _next.interactable = canStep && (Loop || _index < _options.Count - 1);
        }

        private static Button ArrowButton(Transform parent, bool right, Action onClick)
        {
            var rt = UiFactory.CreateRect(right ? "Next" : "Previous", parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = UiSprites.GetChamferRect(4);
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            image.raycastTarget = true;

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = UiFactory.ButtonColors(UiButtonStyle.Default);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => onClick?.Invoke());

            var arrow = UiFactory.Image(rt, UiSprites.Triangle, UiTheme.Text);
            arrow.gameObject.name = "Arrow";
            UiFactory.Anchor(arrow, UiAnchor.Center, Vector2.zero, new Vector2(14f, 14f));
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, right ? -90f : 90f);

            var feedback = rt.gameObject.AddComponent<UiButtonFeedback>();
            feedback.SelectOnHover = false;
            feedback.Bind(button, null, null);
            return button;
        }
    }
}
