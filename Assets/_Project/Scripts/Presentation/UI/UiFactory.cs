using System;
using Project.Infrastructure.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project.Presentation.UI
{
    /// <summary>Hazır yerleşim noktaları (çapa = pivot).</summary>
    public enum UiAnchor
    {
        /// <summary>Sol üst köşe.</summary>
        TopLeft,
        /// <summary>Üst orta.</summary>
        Top,
        /// <summary>Sağ üst köşe.</summary>
        TopRight,
        /// <summary>Sol orta.</summary>
        Left,
        /// <summary>Merkez.</summary>
        Center,
        /// <summary>Sağ orta.</summary>
        Right,
        /// <summary>Sol alt köşe.</summary>
        BottomLeft,
        /// <summary>Alt orta.</summary>
        Bottom,
        /// <summary>Sağ alt köşe.</summary>
        BottomRight
    }

    /// <summary>Düğme görünüm türleri.</summary>
    public enum UiButtonStyle
    {
        /// <summary>Zeytin yeşili standart düğme.</summary>
        Default,
        /// <summary>Türk kırmızısı birincil eylem düğmesi ("HAREKÂTA BAŞLA").</summary>
        Primary,
        /// <summary>Saydam zeminli, yalnızca üzerine gelince belirginleşen düğme.</summary>
        Ghost,
        /// <summary>Tehlikeli eylem (çıkış, teslim ol) — koyu kırmızı.</summary>
        Danger
    }

    /// <summary>
    /// UGUI arayüz fabrikası: tuval, panel, yazı (legacy <see cref="Text"/>, LegacyRuntime.ttf), düğme, kaydırıcı,
    /// onay kutusu, görsel, yerleşim yardımcıları ve olay sistemi kurulumu. Tüm öğeler UI katmanında oluşturulur ve
    /// <see cref="UiTheme"/> değerlerini kullanır. Boyutlar 1920×1080 referans çözünürlüğündedir.
    /// <para>Varsayılan yerleşimler: Panel/Label/liste ebeveyni doldurur (Stretch); Button/Slider/Toggle/Image/RawImage
    /// ebeveyn merkezinde sabit boyutludur. Konumlandırmak için <see cref="Anchor(Component, UiAnchor, Vector2, Vector2)"/>,
    /// <see cref="Stretch(Component)"/> veya <see cref="SetRect(Component, Vector2, Vector2, Vector2, Vector2)"/> kullanın.</para>
    /// </summary>
    public static class UiFactory
    {
        /// <summary>CanvasScaler referans çözünürlüğü (1920×1080).</summary>
        public static readonly Vector2 ReferenceResolution = new Vector2(UiTheme.ReferenceWidth, UiTheme.ReferenceHeight);

        /// <summary>Genişlik/yükseklik eşleme oranı (0.5).</summary>
        public const float MatchWidthOrHeight = 0.5f;

        /// <summary>Arayüz seslerini (üzerine gelme/tıklama) genel olarak açıp kapatır.</summary>
        public static bool SoundsEnabled { get; set; } = true;

        /// <summary>Ortak yazı tipi (<see cref="UiTheme.Font"/>).</summary>
        public static Font DefaultFont => UiTheme.Font;

        // ================================================================== Tuval / olay sistemi

        /// <summary>
        /// Ekran-kaplamalı (ScreenSpaceOverlay) yeni bir tuval oluşturur: CanvasScaler (1920×1080, eşleme 0.5) ve
        /// GraphicRaycaster ekler, sahnede olay sistemi olduğundan emin olur (<see cref="EnsureEventSystem"/>).
        /// </summary>
        /// <param name="name">GameObject adı.</param>
        /// <param name="sortOrder">Çizim sırası (büyük olan üstte). Öneri: HUD 10, harita 20, menü 50, yükleme 100.</param>
        public static Canvas CreateCanvas(string name, int sortOrder)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "Canvas" : name);
            go.layer = UiTheme.UiLayer;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            canvas.pixelPerfect = false;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = MatchWidthOrHeight;
            scaler.referencePixelsPerUnit = 100f;

            go.AddComponent<GraphicRaycaster>();

            EnsureEventSystem();
            return canvas;
        }

        /// <summary>
        /// Sahnede bir <see cref="EventSystem"/> ve Input System UI modülü olmasını sağlar. Eski
        /// <see cref="StandaloneInputModule"/> varsa devre dışı bırakıp kaldırır (eski Input API'si kapalıdır),
        /// <see cref="InputSystemUIInputModule"/> ekler ve varsayılan eylemleri atar. Mevcut olanı döndürür.
        /// </summary>
        public static EventSystem EnsureEventSystem()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                eventSystem = Object.FindAnyObjectByType<EventSystem>();

            if (eventSystem == null)
            {
                var go = new GameObject("EventSystem");
                eventSystem = go.AddComponent<EventSystem>();
            }

            var legacy = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacy != null)
            {
                legacy.enabled = false;
                DestroySafe(legacy);
            }

            var module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module == null)
                module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

            // Oyun sırasında modül OnEnable'da varsayılan eylemleri kendisi atar; atanmadıysa (ör. nesne kapalıyken
            // eklendiyse) burada atanır. Düzenleyici modunda atanmaz: çalışma zamanı varlığı sahneye kaydedilemez.
            if (module.actionsAsset == null && UnityEngine.Application.isPlaying)
                module.AssignDefaultActions();

            return eventSystem;
        }

        /// <summary>Olay sistemindeki seçimi temizler (menü kapanırken; gizli düğmenin Enter/WASD ile tetiklenmemesi için).</summary>
        public static void ClearSelection()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem != null && !eventSystem.alreadySelecting)
                eventSystem.SetSelectedGameObject(null);
        }

        /// <summary>
        /// Verilen öğeyi olay sisteminde seçer (gamepad/klavye ile menü gezinmesinin başlangıç noktası). Null güvenli.
        /// </summary>
        public static void Select(Selectable selectable)
        {
            if (selectable == null || !selectable.isActiveAndEnabled)
                return;
            var eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.alreadySelecting)
                return;
            eventSystem.SetSelectedGameObject(selectable.gameObject);
        }

        /// <summary>
        /// İmleci arayüz kullanımı için serbest bırakır (<paramref name="free"/> = true: görünür, kilitsiz) ya da oyun
        /// için kilitler (gizli, ekran ortasına kilitli).
        /// </summary>
        public static void SetCursorFree(bool free)
        {
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        // ================================================================== Temel öğeler

        /// <summary>
        /// Ebeveyne bağlı boş bir RectTransform oluşturur (UI katmanı, yerel ölçek 1). Ebeveyni doldurur.
        /// </summary>
        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "Rect" : name, typeof(RectTransform));
            go.layer = UiTheme.UiLayer;
            var rt = (RectTransform)go.transform;
            if (parent != null)
                rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            Stretch(rt);
            return rt;
        }

        /// <summary>
        /// Düz renkli panel (Image, ışın hedefi açık — arkasına tıklamayı engeller). Ebeveyni doldurur.
        /// </summary>
        public static RectTransform Panel(Transform parent, Color color)
        {
            return Panel(parent, color, null);
        }

        /// <summary>
        /// Sprite'lı panel. Sprite 9-dilim kenarlıysa (ör. <see cref="UiSprites.RoundedRect"/>,
        /// <see cref="UiSprites.ChamferRect"/>) Sliced olarak çizilir. Ebeveyni doldurur.
        /// </summary>
        public static RectTransform Panel(Transform parent, Color color, Sprite sprite)
        {
            var rt = CreateRect("Panel", parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = UiSprites.HasBorder(sprite) ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.color = color;
            image.raycastTarget = true;
            return rt;
        }

        /// <summary>
        /// Yazı (legacy Text, LegacyRuntime.ttf). Işın hedefi kapalı, zengin metin açık, yatayda kaydırmalı.
        /// Ebeveyni doldurur.
        /// </summary>
        public static Text Label(Transform parent, string text, int size, TextAnchor anchor, Color color, FontStyle style = FontStyle.Normal)
        {
            var rt = CreateRect("Label", parent);
            var label = rt.gameObject.AddComponent<Text>();
            label.font = DefaultFont;
            label.text = text ?? string.Empty;
            label.fontSize = Mathf.Max(1, size);
            label.fontStyle = style;
            label.alignment = anchor;
            label.color = color;
            label.supportRichText = true;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.lineSpacing = 1f;
            return label;
        }

        /// <summary>
        /// Yazıyı yalnızca farklıysa atar (aynı metinde mesh yeniden kurulmaz). HUD gibi sık güncellenen yerlerde
        /// <see cref="UiWidgets.Number(int)"/> ile birlikte kullanın. Değiştiyse true döner.
        /// </summary>
        public static bool SetText(Text label, string text)
        {
            if (label == null)
                return false;
            text ??= string.Empty;
            if (string.Equals(label.text, text, StringComparison.Ordinal))
                return false;
            label.text = text;
            return true;
        }

        /// <summary>Grafik rengini yalnızca farklıysa atar (gereksiz yeniden çizimi önler).</summary>
        public static void SetColor(Graphic graphic, Color color)
        {
            if (graphic != null && graphic.color != color)
                graphic.color = color;
        }

        /// <summary>Tema metin rengiyle, normal boyutta yazı (kısa yol).</summary>
        public static Text Label(Transform parent, string text, int size, TextAnchor anchor)
        {
            return Label(parent, text, size, anchor, UiTheme.Text);
        }

        /// <summary>
        /// Düğme: 9-dilim pahlı zemin, tema renk geçişleri (normal/üzerine gelme/basılı/devre dışı), ortalanmış büyük harf
        /// yazı, üzerine gelince sol kenarda kırmızı vurgu şeridi ve UiHover/UiClick sesleri (<see cref="GameAudio"/>).
        /// 300×56 boyutunda, ebeveyn merkezinde; LayoutElement ile listelerde doğru yükseklik alır.
        /// </summary>
        public static Button Button(Transform parent, string label, Action onClick)
        {
            return Button(parent, label, onClick, UiButtonStyle.Default);
        }

        /// <summary>Belirli görünüm türünde düğme (bkz. <see cref="Button(Transform, string, Action)"/>).</summary>
        public static Button Button(Transform parent, string label, Action onClick, UiButtonStyle style)
        {
            var rt = CreateRect("Button", parent);
            rt.gameObject.name = "Button_" + Sanitize(label);
            Anchor(rt, UiAnchor.Center, Vector2.zero, new Vector2(UiTheme.ButtonWidth, UiTheme.ButtonHeight));

            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = UiSprites.ChamferRect;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.color = Color.white;
            image.raycastTarget = true;

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = ButtonColors(style);
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };

            // Sol vurgu şeridi (üzerine gelince görünür).
            var strip = CreateRect("Accent", rt);
            SetRect(strip, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 6f), new Vector2(UiTheme.AccentStripWidth, -6f));
            var stripImage = strip.gameObject.AddComponent<Image>();
            stripImage.color = style == UiButtonStyle.Primary ? UiTheme.Text : UiTheme.Accent;
            stripImage.raycastTarget = false;

            var text = Label(rt, label, UiTheme.FontMedium, TextAnchor.MiddleCenter, UiTheme.Text, FontStyle.Bold);
            text.gameObject.name = "Text";
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(text, 12f, 0f, 12f, 0f);
            AddShadow(text, UiTheme.TextShadow, new Vector2(1f, -1f));

            var feedback = rt.gameObject.AddComponent<UiButtonFeedback>();
            feedback.Bind(button, stripImage, text);

            button.onClick.AddListener(() =>
            {
                UiButtonFeedback.PlaySound(SoundId.UiClick);
                onClick?.Invoke();
            });

            var layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = UiTheme.ButtonHeight;
            layout.preferredHeight = UiTheme.ButtonHeight;
            layout.preferredWidth = UiTheme.ButtonWidth;
            return button;
        }

        /// <summary>Düğmenin yazı bileşenini döndürür (yoksa null).</summary>
        public static Text GetButtonLabel(Button button)
        {
            if (button == null)
                return null;
            var t = button.transform.Find("Text");
            return t != null ? t.GetComponent<Text>() : button.GetComponentInChildren<Text>(true);
        }

        /// <summary>Düğme yazısını değiştirir.</summary>
        public static void SetButtonLabel(Button button, string label)
        {
            var text = GetButtonLabel(button);
            if (text != null)
                text.text = label ?? string.Empty;
        }

        /// <summary>Düğme stiline göre ColorBlock (Image rengi beyaz kalır, renk geçişle verilir).</summary>
        public static ColorBlock ButtonColors(UiButtonStyle style)
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            switch (style)
            {
                case UiButtonStyle.Primary:
                    colors.normalColor = UiTheme.Accent;
                    colors.highlightedColor = UiTheme.AccentLight;
                    colors.pressedColor = UiTheme.AccentDark;
                    colors.selectedColor = UiTheme.AccentLight;
                    colors.disabledColor = UiTheme.ButtonDisabled;
                    break;
                case UiButtonStyle.Ghost:
                    colors.normalColor = new Color(1f, 1f, 1f, 0f);
                    colors.highlightedColor = UiTheme.WithAlpha(UiTheme.ButtonHover, 0.85f);
                    colors.pressedColor = UiTheme.ButtonPressed;
                    colors.selectedColor = UiTheme.WithAlpha(UiTheme.ButtonHover, 0.85f);
                    colors.disabledColor = new Color(1f, 1f, 1f, 0f);
                    break;
                case UiButtonStyle.Danger:
                    colors.normalColor = UiTheme.Hex(0x5A, 0x14, 0x14, 0xF2);
                    colors.highlightedColor = UiTheme.Hex(0x8A, 0x1C, 0x1C);
                    colors.pressedColor = UiTheme.Hex(0x3E, 0x0E, 0x0E);
                    colors.selectedColor = UiTheme.Hex(0x8A, 0x1C, 0x1C);
                    colors.disabledColor = UiTheme.ButtonDisabled;
                    break;
                default:
                    colors.normalColor = UiTheme.ButtonNormal;
                    colors.highlightedColor = UiTheme.ButtonHover;
                    colors.pressedColor = UiTheme.ButtonPressed;
                    colors.selectedColor = UiTheme.ButtonHover;
                    colors.disabledColor = UiTheme.ButtonDisabled;
                    break;
            }

            return colors;
        }

        /// <summary>
        /// Yatay kaydırıcı: koyu yuva, kırmızı dolgu, beyaz daire tutamak. Değer bildirimsiz atanır; yalnızca kullanıcı
        /// değişiklikleri <paramref name="onChanged"/> çağırır. 320×28, ebeveyn merkezinde.
        /// </summary>
        public static Slider Slider(Transform parent, float min, float max, float value, Action<float> onChanged)
        {
            if (max < min)
            {
                var tmp = min;
                min = max;
                max = tmp;
            }

            var rt = CreateRect("Slider", parent);
            Anchor(rt, UiAnchor.Center, Vector2.zero, new Vector2(320f, 28f));

            // Tüm satır yüksekliğinde tıklanabilir alan (ince yuvayı tam isabetle tıklamak gerekmesin).
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            var background = CreateRect("Background", rt);
            SetRect(background, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -4f), new Vector2(0f, 4f));
            var bgImage = background.gameObject.AddComponent<Image>();
            bgImage.sprite = UiSprites.GetRoundedRect(3);
            bgImage.type = UnityEngine.UI.Image.Type.Sliced;
            bgImage.color = UiTheme.Track;
            bgImage.raycastTarget = true;

            var fillArea = CreateRect("Fill Area", rt);
            SetRect(fillArea, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -4f), new Vector2(0f, 4f));
            var fill = CreateRect("Fill", fillArea);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = UiSprites.GetRoundedRect(3);
            fillImage.type = UnityEngine.UI.Image.Type.Sliced;
            fillImage.color = UiTheme.SliderFill;
            fillImage.raycastTarget = false;

            var handleArea = CreateRect("Handle Slide Area", rt);
            Stretch(handleArea, 10f, 0f, 10f, 0f);
            var handle = CreateRect("Handle", handleArea);
            handle.sizeDelta = new Vector2(22f, 0f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = UiSprites.Circle;
            handleImage.preserveAspect = true;
            handleImage.color = Color.white;
            handleImage.raycastTarget = true;

            var slider = rt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.transition = Selectable.Transition.ColorTint;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = UiTheme.SliderHandle;
            colors.highlightedColor = UiTheme.Amber;
            colors.pressedColor = UiTheme.Accent;
            colors.selectedColor = UiTheme.Amber;
            colors.disabledColor = UiTheme.TextMuted;
            colors.fadeDuration = 0.08f;
            slider.colors = colors;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(Mathf.Clamp(value, min, max));
            if (onChanged != null)
                slider.onValueChanged.AddListener(v => onChanged(v));

            var feedback = rt.gameObject.AddComponent<UiButtonFeedback>();
            feedback.Bind(slider, null, null);

            var layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 120f;
            layout.preferredWidth = 320f;
            layout.minHeight = 28f;
            layout.preferredHeight = 28f;
            return slider;
        }

        /// <summary>Tam sayı adımlı kaydırıcı (bkz. <see cref="Slider(Transform, float, float, float, Action{float})"/>).</summary>
        public static Slider Slider(Transform parent, float min, float max, float value, Action<float> onChanged, bool wholeNumbers)
        {
            var slider = Slider(parent, min, max, value, onChanged);
            slider.wholeNumbers = wholeNumbers;
            if (wholeNumbers)
                slider.SetValueWithoutNotify(Mathf.Round(Mathf.Clamp(value, slider.minValue, slider.maxValue)));
            return slider;
        }

        /// <summary>
        /// Onay kutusu: solda 28 px kutu (kırmızı işaret), sağda yazı. Değer bildirimsiz atanır; kullanıcı
        /// değişikliğinde tıklama sesi ve <paramref name="onChanged"/>. 320×40, ebeveyn merkezinde.
        /// </summary>
        public static Toggle Toggle(Transform parent, string label, bool value, Action<bool> onChanged)
        {
            var rt = CreateRect("Toggle_" + Sanitize(label), parent);
            Anchor(rt, UiAnchor.Center, Vector2.zero, new Vector2(320f, UiTheme.RowHeight));

            // Tüm satır tıklanabilir olsun diye saydam ışın hedefi.
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            var box = CreateRect("Background", rt);
            Anchor(box, UiAnchor.Left, new Vector2(2f, 0f), new Vector2(28f, 28f));
            var boxImage = box.gameObject.AddComponent<Image>();
            boxImage.sprite = UiSprites.GetRoundedRect(4);
            boxImage.type = UnityEngine.UI.Image.Type.Sliced;
            boxImage.color = Color.white;
            boxImage.raycastTarget = true;

            var border = CreateRect("Border", box);
            var borderImage = border.gameObject.AddComponent<Image>();
            borderImage.sprite = UiSprites.GetRoundedRectOutline(4);
            borderImage.type = UnityEngine.UI.Image.Type.Sliced;
            borderImage.color = UiTheme.PanelBorder;
            borderImage.raycastTarget = false;

            var check = CreateRect("Checkmark", box);
            Stretch(check, 6f);
            var checkImage = check.gameObject.AddComponent<Image>();
            checkImage.sprite = UiSprites.GetRoundedRect(2);
            checkImage.type = UnityEngine.UI.Image.Type.Sliced;
            checkImage.color = UiTheme.Accent;
            checkImage.raycastTarget = false;

            var text = Label(rt, label, UiTheme.FontNormal, TextAnchor.MiddleLeft, UiTheme.Text);
            text.gameObject.name = "Label";
            Stretch(text, 42f, 0f, 0f, 0f);

            var toggle = rt.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = boxImage;
            toggle.graphic = checkImage;
            toggle.toggleTransition = UnityEngine.UI.Toggle.ToggleTransition.Fade;
            toggle.transition = Selectable.Transition.ColorTint;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = UiTheme.ToggleBox;
            colors.highlightedColor = UiTheme.ButtonHover;
            colors.pressedColor = UiTheme.ButtonPressed;
            colors.selectedColor = UiTheme.ButtonHover;
            colors.disabledColor = UiTheme.ButtonDisabled;
            colors.fadeDuration = 0.08f;
            toggle.colors = colors;
            toggle.SetIsOnWithoutNotify(value);
            checkImage.canvasRenderer.SetAlpha(value ? 1f : 0f);
            toggle.onValueChanged.AddListener(v =>
            {
                UiButtonFeedback.PlaySound(SoundId.UiClick);
                onChanged?.Invoke(v);
            });

            var feedback = rt.gameObject.AddComponent<UiButtonFeedback>();
            feedback.Bind(toggle, null, text);

            var layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = UiTheme.RowHeight;
            layout.preferredHeight = UiTheme.RowHeight;
            layout.preferredWidth = 320f;
            return toggle;
        }

        /// <summary>
        /// Görsel (Image). Sprite 9-dilim kenarlıysa Sliced çizilir. Işın hedefi kapalı. 100×100, ebeveyn merkezinde.
        /// <paramref name="sprite"/> null ise düz renkli dikdörtgen olur.
        /// </summary>
        public static Image Image(Transform parent, Sprite sprite, Color color)
        {
            var rt = CreateRect("Image", parent);
            Anchor(rt, UiAnchor.Center, Vector2.zero, new Vector2(100f, 100f));
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = UiSprites.HasBorder(sprite) ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Ham doku görseli (RawImage; mini harita, render dokuları). Işın hedefi kapalı. 100×100, ebeveyn merkezinde.</summary>
        public static RawImage RawImage(Transform parent, Texture texture)
        {
            var rt = CreateRect("RawImage", parent);
            Anchor(rt, UiAnchor.Center, Vector2.zero, new Vector2(100f, 100f));
            var raw = rt.gameObject.AddComponent<RawImage>();
            raw.texture = texture;
            raw.color = Color.white;
            raw.raycastTarget = false;
            return raw;
        }

        /// <summary>
        /// İlerleme çubuğu (doluluk görseli; isteğe bağlı gecikmeli "hasar izi" ve bölmeler). 240×14, ebeveyn merkezinde.
        /// </summary>
        public static UiProgressBar ProgressBar(Transform parent, Color fillColor, Color backgroundColor)
        {
            return UiProgressBar.Create(parent, fillColor, backgroundColor);
        }

        /// <summary>Tema renkleriyle ilerleme çubuğu (kırık beyaz dolgu, koyu yuva).</summary>
        public static UiProgressBar ProgressBar(Transform parent)
        {
            return UiProgressBar.Create(parent, UiTheme.HealthHigh, UiTheme.Track);
        }

        /// <summary>Yatay ayraç çizgisi (listelerde 2 px yükseklik). Ebeveyn genişliğini doldurur.</summary>
        public static Image Divider(Transform parent, Color color, float thickness = 2f)
        {
            var rt = CreateRect("Divider", parent);
            SetRect(rt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -thickness * 0.5f), new Vector2(0f, thickness * 0.5f));
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = thickness;
            layout.preferredHeight = thickness;
            layout.flexibleWidth = 1f;
            return image;
        }

        // ================================================================== Listeler

        /// <summary>
        /// Dikey liste (VerticalLayoutGroup): çocukların genişliğini kontrol eder ve genişletir, yüksekliklerini tercih
        /// edilen değerden alır (LayoutElement / Text). Ebeveyni doldurur.
        /// </summary>
        public static RectTransform VerticalList(Transform parent, float spacing = UiTheme.Spacing, int padding = 0, TextAnchor childAlignment = TextAnchor.UpperLeft)
        {
            var rt = CreateRect("VerticalList", parent);
            var group = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.padding = new RectOffset(padding, padding, padding, padding);
            group.childAlignment = childAlignment;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            group.childScaleWidth = false;
            group.childScaleHeight = false;
            return rt;
        }

        /// <summary>
        /// Yatay liste (HorizontalLayoutGroup): çocukların yüksekliğini kontrol eder ve genişletir, genişliklerini tercih
        /// edilen değerden alır. Ebeveyni doldurur.
        /// </summary>
        public static RectTransform HorizontalList(Transform parent, float spacing = UiTheme.Spacing, int padding = 0, TextAnchor childAlignment = TextAnchor.MiddleLeft)
        {
            var rt = CreateRect("HorizontalList", parent);
            var group = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = spacing;
            group.padding = new RectOffset(padding, padding, padding, padding);
            group.childAlignment = childAlignment;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = true;
            group.childScaleWidth = false;
            group.childScaleHeight = false;
            return rt;
        }

        /// <summary>
        /// Listeyi içeriğine göre boyutlandırır (ContentSizeFitter, tercih edilen boyut). Dikey listede yükseklik,
        /// yatay listede genişlik; <paramref name="both"/> ise ikisi de.
        /// </summary>
        public static ContentSizeFitter FitContent(Component list, bool vertical = true, bool both = false)
        {
            if (list == null)
                return null;
            var fitter = list.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = list.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = vertical || both ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            fitter.horizontalFit = !vertical || both ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            return fitter;
        }

        /// <summary>Listeye esnek olmayan sabit boşluk ekler.</summary>
        public static LayoutElement Spacer(Transform parent, float size)
        {
            var rt = CreateRect("Spacer", parent);
            var layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = size;
            layout.preferredHeight = size;
            layout.minWidth = size;
            layout.preferredWidth = size;
            return layout;
        }

        /// <summary>Listede kalan boşluğu dolduran esnek boşluk ekler.</summary>
        public static LayoutElement FlexibleSpacer(Transform parent)
        {
            var rt = CreateRect("FlexSpacer", parent);
            var layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.flexibleHeight = 1f;
            layout.flexibleWidth = 1f;
            return layout;
        }

        /// <summary>
        /// Bir öğenin liste içindeki boyutunu belirler (LayoutElement ekler/günceller). Negatif değerler "ayarlanmadı"
        /// anlamına gelir. Tercih edilen boyut aynı zamanda en küçük boyut olarak atanır.
        /// </summary>
        public static LayoutElement LayoutSize(Component target, float preferredWidth, float preferredHeight, float flexibleWidth = -1f, float flexibleHeight = -1f)
        {
            if (target == null)
                return null;
            var layout = target.GetComponent<LayoutElement>();
            if (layout == null)
                layout = target.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = preferredWidth;
            layout.preferredHeight = preferredHeight;
            layout.minWidth = preferredWidth;
            layout.minHeight = preferredHeight;
            layout.flexibleWidth = flexibleWidth;
            layout.flexibleHeight = flexibleHeight;
            return layout;
        }

        // ================================================================== Yerleşim yardımcıları

        /// <summary>Öğeyi ebeveynini tamamen dolduracak şekilde gerer (çapalar 0..1, kenar boşluğu 0).</summary>
        public static RectTransform Stretch(Component target)
        {
            var rt = AsRect(target);
            if (rt == null)
                return null;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Öğeyi ebeveyni içinde her kenardan <paramref name="padding"/> px boşlukla gerer.</summary>
        public static RectTransform Stretch(Component target, float padding)
        {
            return Stretch(target, padding, padding, padding, padding);
        }

        /// <summary>Öğeyi ebeveyni içinde kenar boşluklarıyla (sol, üst, sağ, alt — px) gerer.</summary>
        public static RectTransform Stretch(Component target, float left, float top, float right, float bottom)
        {
            var rt = Stretch(target);
            if (rt == null)
                return null;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>
        /// Öğeyi hazır bir noktaya sabitler (çapa = pivot = nokta). <paramref name="position"/> çapaya göre ofsettir
        /// (ör. TopLeft + (20, -20) sol üstten 20 px içeride), <paramref name="size"/> px boyuttur.
        /// </summary>
        public static RectTransform Anchor(Component target, UiAnchor anchor, Vector2 position, Vector2 size)
        {
            var point = AnchorPoint(anchor);
            return Anchor(target, point, point, position, size);
        }

        /// <summary>Öğeyi verilen normalize çapa noktasına (0..1) ve pivota göre konumlandırır, boyutunu atar.</summary>
        public static RectTransform Anchor(Component target, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rt = AsRect(target);
            if (rt == null)
                return null;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return rt;
        }

        /// <summary>Çapa ve kenar ofsetlerini doğrudan atar (offsetMin = sol-alt, offsetMax = sağ-üst; px).</summary>
        public static RectTransform SetRect(Component target, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = AsRect(target);
            if (rt == null)
                return null;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        /// <summary>
        /// Ebeveynin sol üst köşesine göre piksel dikdörtgeni atar (x sağa, y aşağı pozitif): web/IMGUI benzeri yerleşim.
        /// </summary>
        public static RectTransform SetRect(Component target, float x, float y, float width, float height)
        {
            return Anchor(target, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, height));
        }

        /// <summary>Yalnızca boyutu değiştirir (çapalar aynıysa px boyut).</summary>
        public static RectTransform SetSize(Component target, float width, float height)
        {
            var rt = AsRect(target);
            if (rt == null)
                return null;
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        /// <summary>Hazır yerleşim noktasının normalize koordinatı (ör. TopRight → (1,1)).</summary>
        public static Vector2 AnchorPoint(UiAnchor anchor)
        {
            switch (anchor)
            {
                case UiAnchor.TopLeft: return new Vector2(0f, 1f);
                case UiAnchor.Top: return new Vector2(0.5f, 1f);
                case UiAnchor.TopRight: return new Vector2(1f, 1f);
                case UiAnchor.Left: return new Vector2(0f, 0.5f);
                case UiAnchor.Right: return new Vector2(1f, 0.5f);
                case UiAnchor.BottomLeft: return new Vector2(0f, 0f);
                case UiAnchor.Bottom: return new Vector2(0.5f, 0f);
                case UiAnchor.BottomRight: return new Vector2(1f, 0f);
                default: return new Vector2(0.5f, 0.5f);
            }
        }

        // ================================================================== Efektler / görünürlük

        /// <summary>Grafiğe gölge efekti ekler (HUD yazılarının 3B sahne üzerinde okunması için).</summary>
        public static Shadow AddShadow(Graphic graphic, Color color, Vector2 distance)
        {
            if (graphic == null)
                return null;
            var shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
            return shadow;
        }

        /// <summary>Grafiğe kontur efekti ekler.</summary>
        public static Outline AddOutline(Graphic graphic, Color color, float thickness = 1f)
        {
            if (graphic == null)
                return null;
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(thickness, -thickness);
            outline.useGraphicAlpha = true;
            return outline;
        }

        /// <summary>Öğeye CanvasGroup ekler/döndürür (toplu saydamlık, etkileşim/ışın kontrolü).</summary>
        public static CanvasGroup EnsureCanvasGroup(Component target)
        {
            if (target == null)
                return null;
            var group = target.GetComponent<CanvasGroup>();
            if (group == null)
                group = target.gameObject.AddComponent<CanvasGroup>();
            return group;
        }

        /// <summary>
        /// Öğeyi gösterir/gizler: CanvasGroup alfa + etkileşim + ışın engelleme. GameObject'i kapatmaz (bileşenler
        /// çalışmaya devam eder). Animasyonlu geçiş için <see cref="UiFader"/> kullanın.
        /// </summary>
        public static void SetVisible(Component target, bool visible)
        {
            var group = EnsureCanvasGroup(target);
            if (group == null)
                return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        /// <summary>Bir çocuğu yok eder (oyun sırasında Destroy, düzenleyicide DestroyImmediate).</summary>
        public static void DestroySafe(Object obj)
        {
            if (obj == null)
                return;
            if (UnityEngine.Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }

        /// <summary>Bir dönüşümün tüm çocuklarını yok eder (liste yeniden doldurma).</summary>
        public static void ClearChildren(Transform parent)
        {
            if (parent == null)
                return;
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (UnityEngine.Application.isPlaying)
                {
                    // Yerleşim gruplarının aynı karede yok edilen çocukları saymaması için önce ayır.
                    child.gameObject.SetActive(false);
                    child.SetParent(null, false);
                    Object.Destroy(child.gameObject);
                }
                else
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        // ================================================================== İç

        private static RectTransform AsRect(Component target)
        {
            if (target == null)
                return null;
            if (target is RectTransform rt)
                return rt;
            return target.transform as RectTransform;
        }

        private static string Sanitize(string label)
        {
            if (string.IsNullOrEmpty(label))
                return "Unnamed";
            return label.Length <= 24 ? label : label.Substring(0, 24);
        }
    }
}
