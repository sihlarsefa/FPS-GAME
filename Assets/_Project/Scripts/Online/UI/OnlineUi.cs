using System;
using UnityEngine;
using UnityEngine.UI;
using Project.Presentation.UI;

namespace Project.Online.UI
{
    /// <summary>Online panelleri için ortak InputField / pencere yardımcıları.</summary>
    internal static class OnlineUi
    {
        public static RectTransform CreateDimRoot(string name, Transform parent, out CanvasGroup group)
        {
            var root = UiFactory.CreateRect(name, parent);
            root.SetAsLastSibling();
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = UiTheme.WithAlpha(UiTheme.Overlay, 0.55f);
            dim.raycastTarget = true;
            group = UiFactory.EnsureCanvasGroup(root);
            group.alpha = 0f;
            return root;
        }

        public static RectTransform CreateWindow(Transform parent, float width, float height, string title)
        {
            var window = UiFactory.Panel(parent, UiTheme.Panel, UiSprites.ChamferRect);
            window.gameObject.name = "Window";
            UiFactory.Anchor(window, UiAnchor.Center, Vector2.zero, new Vector2(width, height));

            var border = UiFactory.Image(window, UiSprites.GetRoundedRectOutline(UiTheme.CornerRadius), UiTheme.PanelBorder);
            UiFactory.Stretch(border);

            var stripe = UiFactory.Image(window, null, UiTheme.Accent);
            UiFactory.SetRect(stripe, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f), Vector2.zero);

            var titleLabel = UiFactory.Label(window, title, UiTheme.FontTitle, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold);
            titleLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(titleLabel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(36f, -88f), new Vector2(-36f, -24f));
            UiFactory.AddShadow(titleLabel, UiTheme.TextShadow, new Vector2(2f, -2f));
            return window;
        }

        public static RectTransform CreateBody(Transform window, float top = 110f, float bottom = 100f)
        {
            var body = UiFactory.VerticalList(window, 12f, 0, TextAnchor.UpperCenter);
            body.gameObject.name = "Body";
            UiFactory.SetRect(body, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(40f, bottom), new Vector2(-40f, -top));
            var layout = body.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
            }

            return body;
        }

        public static InputField CreateField(Transform parent, string placeholder, bool password, float height = 48f)
        {
            var root = UiFactory.CreateRect("Field", parent);
            UiFactory.LayoutSize(root, -1f, height, 1f);

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
            UiFactory.Stretch(text, 14f, 2f, 14f, 2f);

            var ph = UiFactory.Label(root, placeholder, UiTheme.FontMedium, TextAnchor.MiddleLeft, UiTheme.TextMuted, FontStyle.Italic);
            ph.gameObject.name = "Placeholder";
            UiFactory.Stretch(ph, 14f, 2f, 14f, 2f);

            var field = root.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = ph;
            field.targetGraphic = background;
            field.lineType = InputField.LineType.SingleLine;
            field.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
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
            return field;
        }

        public static Text StatusLabel(Transform parent)
        {
            var label = UiFactory.Label(parent, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.TextDim);
            UiFactory.LayoutSize(label, -1f, 28f, 1f);
            return label;
        }

        public static void SetStatus(Text label, string message, Color color)
        {
            if (label == null)
                return;
            label.text = message ?? "";
            label.color = color;
        }

        public static RectTransform CreateFooter(Transform window, out Button backButton, Action onBack, string backLabel = "GERİ")
        {
            var row = UiFactory.HorizontalList(window, 14f, 0, TextAnchor.MiddleRight);
            row.gameObject.name = "Footer";
            UiFactory.SetRect(row, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 24f), new Vector2(-40f, 24f + UiTheme.ButtonHeight));
            var h = row.GetComponent<HorizontalLayoutGroup>();
            if (h != null)
                h.childForceExpandWidth = false;

            UiFactory.FlexibleSpacer(row);
            backButton = UiFactory.Button(row, backLabel, onBack, UiButtonStyle.Default);
            UiFactory.LayoutSize(backButton, 180f, UiTheme.ButtonHeight);
            return row;
        }
    }
}
