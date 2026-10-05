using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Harita işaretleri için prosedürel simge fabrikası (mini harita, tam harita ve lejant ortak kullanır).
    /// Her simge, merkez çapalı/pivotlu bir kapsayıcı RectTransform'dur; koyu kontur için arkada biraz daha büyük koyu
    /// bir kopya bulunur (3B sahne/harita dokusu üzerinde okunurluk). Renk değiştirmek için dönen <see cref="Image"/>
    /// kullanılır. Işın hedefi kapalıdır.
    /// </summary>
    internal static class MapIcons
    {
        /// <summary>Simge konturu (yarı saydam siyah).</summary>
        public static readonly Color OutlineColor = new Color(0f, 0f, 0f, 0.8f);

        /// <summary>Boş kapsayıcı (merkez çapa/pivot, verilen boyut).</summary>
        public static RectTransform Container(Transform parent, string name, float size)
        {
            var rt = UiFactory.CreateRect(name, parent);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        /// <summary>Kapsayıcının içinde ortalanmış görsel (boyut px).</summary>
        public static Image Shape(Transform parent, string name, Sprite sprite, Color color, float width, float height)
        {
            var image = UiFactory.Image(parent, sprite, color);
            image.gameObject.name = name;
            var rt = image.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = Vector2.zero;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Konturlu yön oku (yukarı bakar; kapsayıcıyı döndürün).</summary>
        public static RectTransform Arrow(Transform parent, string name, float size, Color color, out Image fill)
        {
            var root = Container(parent, name, size);
            Shape(root, "Outline", UiSprites.Triangle, OutlineColor, size + 5f, size + 5f).rectTransform.anchoredPosition = new Vector2(0f, -0.5f);
            fill = Shape(root, "Fill", UiSprites.Triangle, color, size, size);
            // Okun arka kısmını belirginleştiren çentik (koyu küçük üçgen).
            var notch = Shape(root, "Notch", UiSprites.Triangle, OutlineColor, size * 0.42f, size * 0.32f);
            notch.rectTransform.anchoredPosition = new Vector2(0f, -size * 0.36f);
            return root;
        }

        /// <summary>Konturlu nokta (tim arkadaşı).</summary>
        public static RectTransform Dot(Transform parent, string name, float size, Color color, out Image fill)
        {
            var root = Container(parent, name, size);
            Shape(root, "Outline", UiSprites.Circle, OutlineColor, size + 4f, size + 4f);
            fill = Shape(root, "Fill", UiSprites.Circle, color, size, size);
            return root;
        }

        /// <summary>Konturlu baklava (iniş bölgesi / araç).</summary>
        public static RectTransform Diamond(Transform parent, string name, float size, Color color, out Image fill)
        {
            var root = Container(parent, name, size);
            Shape(root, "Outline", UiSprites.Diamond, OutlineColor, size + 5f, size + 5f);
            fill = Shape(root, "Fill", UiSprites.Diamond, color, size, size);
            Shape(root, "Core", UiSprites.Diamond, OutlineColor, size * 0.38f, size * 0.38f);
            return root;
        }

        /// <summary>İşaret iğnesi (baş daire + aşağı bakan uç). İğnenin ucu kapsayıcının merkezindedir (hedef noktası).</summary>
        public static RectTransform Pin(Transform parent, string name, float size, Color color, out Image fill)
        {
            var root = Container(parent, name, size);
            var tipOutline = Shape(root, "TipOutline", UiSprites.Triangle, OutlineColor, size * 0.62f + 4f, size * 0.62f + 4f);
            tipOutline.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            tipOutline.rectTransform.anchoredPosition = new Vector2(0f, size * 0.31f);
            var headOutline = Shape(root, "HeadOutline", UiSprites.Circle, OutlineColor, size * 0.7f + 4f, size * 0.7f + 4f);
            headOutline.rectTransform.anchoredPosition = new Vector2(0f, size * 0.69f);
            var tip = Shape(root, "Tip", UiSprites.Triangle, color, size * 0.62f, size * 0.62f);
            tip.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            tip.rectTransform.anchoredPosition = new Vector2(0f, size * 0.31f);
            fill = Shape(root, "Head", UiSprites.Circle, color, size * 0.7f, size * 0.7f);
            fill.rectTransform.anchoredPosition = new Vector2(0f, size * 0.69f);
            var core = Shape(root, "Core", UiSprites.Circle, OutlineColor, size * 0.26f, size * 0.26f);
            core.rectTransform.anchoredPosition = new Vector2(0f, size * 0.69f);
            return root;
        }

        /// <summary>Çapraz (X) hedef işareti (topçu atış merkezi).</summary>
        public static RectTransform Cross(Transform parent, string name, float size, Color color, out Image fill)
        {
            var root = Container(parent, name, size);
            for (var i = 0; i < 2; i++)
            {
                var angle = i == 0 ? 45f : -45f;
                var outline = Shape(root, "Outline" + i, UiSprites.White, OutlineColor, size * 0.24f + 3f, size + 3f);
                outline.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            fill = null;
            for (var i = 0; i < 2; i++)
            {
                var angle = i == 0 ? 45f : -45f;
                var bar = Shape(root, "Bar" + i, UiSprites.White, color, size * 0.24f, size);
                bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
                fill ??= bar;
            }

            return root;
        }

        /// <summary>Taarruz hedefi: halka içinde aşağı bakan şerit (chevron).</summary>
        public static RectTransform Target(Transform parent, string name, float size, Color color, out Image fill)
        {
            var root = Container(parent, name, size);
            Shape(root, "Outline", UiSprites.Ring, OutlineColor, size + 4f, size + 4f);
            fill = Shape(root, "Ring", UiSprites.Ring, color, size, size);
            var chevron = Shape(root, "Chevron", UiSprites.Chevron, color, size * 0.55f, size * 0.55f);
            chevron.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            return root;
        }

        /// <summary>Kısa etiket rozeti (koyu zemin + yazı), simgenin altına/üstüne konur.</summary>
        public static Text Badge(Transform parent, string name, string text, int fontSize, Color color, Vector2 offset, float width = 80f)
        {
            var label = UiFactory.Label(parent, text, fontSize, TextAnchor.MiddleCenter, color, FontStyle.Bold);
            label.gameObject.name = name;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            var rt = label.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, fontSize + 6f);
            rt.anchoredPosition = offset;
            UiFactory.AddOutline(label, OutlineColor, 1.2f);
            return label;
        }

        /// <summary>Simgenin tüm görsellerinin rengini (kontur hariç) değiştirir — tahsis yapmaz.</summary>
        public static void Tint(RectTransform icon, Color color)
        {
            if (icon == null)
                return;

            for (var i = 0; i < icon.childCount; i++)
            {
                var child = icon.GetChild(i);
                if (!child.TryGetComponent<Image>(out var image))
                    continue;
                if (image.color == OutlineColor || image.color.Equals(OutlineColor))
                    continue;
                if (image.color != color)
                    image.color = color;
            }
        }
    }
}
