using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// HUD görünümlerinin ortak kurulum yardımcıları (UiFactory üzerine ince katman): konumlu görsel/yazı/dikdörtgen,
    /// iç içe tuval (sık değişen öğelerin yeniden toplu çizimini ayırmak için) ve değişmedikçe dokunmayan atayıcılar.
    /// Tüm HUD öğeleri ışın hedefi değildir (oyun girdisini engellemez).
    /// </summary>
    public static class HudBuild
    {
        public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        private static Sprite _banner;

        /// <summary>
        /// Yazı arka bandı: ortası opak, sağ ve sol uçlara doğru yumuşakça solan, üst/alt kenarı hafif yumuşak beyaz
        /// şerit (bildirimler, merkez mesajı). Renk vererek kullanın.
        /// </summary>
        public static Sprite Banner
        {
            get
            {
                if (_banner != null)
                    return _banner;

                const int width = 128;
                const int height = 32;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "HUD_Banner",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave
                };

                var pixels = new Color32[width * height];
                for (var y = 0; y < height; y++)
                {
                    var v = (y + 0.5f) / height;
                    var vertical = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(v, 1f - v) / 0.18f));
                    for (var x = 0; x < width; x++)
                    {
                        var u = (x + 0.5f) / width;
                        var horizontal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(u, 1f - u) / 0.3f));
                        var a = Mathf.Clamp01(horizontal * vertical);
                        pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                _banner = Sprite.Create(texture, new UnityEngine.Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
                _banner.name = "HUD_Banner";
                _banner.hideFlags = HideFlags.DontSave;
                return _banner;
            }
        }

        /// <summary>Konumlu boş dikdörtgen.</summary>
        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rt = UiFactory.CreateRect(name, parent);
            UiFactory.Anchor(rt, anchor, pivot, position, size);
            return rt;
        }

        /// <summary>Ebeveyni dolduran boş dikdörtgen.</summary>
        public static RectTransform Fill(string name, Transform parent)
        {
            return UiFactory.CreateRect(name, parent);
        }

        /// <summary>Konumlu görsel (ışın hedefi kapalı).</summary>
        public static Image Image(string name, Transform parent, Sprite sprite, Color color, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            var image = UiFactory.Image(parent, sprite, color);
            image.gameObject.name = name;
            image.raycastTarget = false;
            UiFactory.Anchor(image, anchor, pivot, position, size);
            return image;
        }

        /// <summary>Merkezde konumlu görsel.</summary>
        public static Image Image(string name, Transform parent, Sprite sprite, Color color, Vector2 position, Vector2 size)
        {
            return Image(name, parent, sprite, color, Center, Center, position, size);
        }

        /// <summary>Ebeveyni dolduran görsel.</summary>
        public static Image FillImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var image = UiFactory.Image(parent, sprite, color);
            image.gameObject.name = name;
            image.raycastTarget = false;
            UiFactory.Stretch(image);
            return image;
        }

        /// <summary>Konumlu, taşmalı (kırpılmayan), gölgeli yazı.</summary>
        public static Text Text(string name, Transform parent, string text, int size, TextAnchor alignment, Color color,
            FontStyle style, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 rectSize, bool shadow = true)
        {
            var label = UiFactory.Label(parent, text, size, alignment, color, style);
            label.gameObject.name = name;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.Anchor(label, anchor, pivot, position, rectSize);
            if (shadow)
                UiFactory.AddShadow(label, UiTheme.TextShadow, new Vector2(1.5f, -1.5f));
            return label;
        }

        /// <summary>Ebeveyni dolduran gölgeli yazı.</summary>
        public static Text FillText(string name, Transform parent, string text, int size, TextAnchor alignment, Color color,
            FontStyle style = FontStyle.Normal, bool shadow = true)
        {
            var label = UiFactory.Label(parent, text, size, alignment, color, style);
            label.gameObject.name = name;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            if (shadow)
                UiFactory.AddShadow(label, UiTheme.TextShadow, new Vector2(1.5f, -1.5f));
            return label;
        }

        /// <summary>
        /// Öğeye iç içe tuval ekler: içindeki değişiklikler yalnızca bu alt tuvalin yeniden toplu çizimini tetikler.
        /// </summary>
        public static Canvas NestedCanvas(Component target)
        {
            if (target == null)
                return null;

            var canvas = target.GetComponent<Canvas>();
            if (canvas == null)
                canvas = target.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = false;
            return canvas;
        }

        /// <summary>GameObject etkinliğini yalnızca farklıysa değiştirir.</summary>
        public static void SetActive(Component target, bool active)
        {
            if (target != null && target.gameObject.activeSelf != active)
                target.gameObject.SetActive(active);
        }

        /// <summary>Grafik saydamlığını yalnızca belirgin farkta değiştirir.</summary>
        public static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null)
                return;

            var color = graphic.color;
            if (Mathf.Abs(color.a - alpha) < 0.002f)
                return;

            color.a = alpha;
            graphic.color = color;
        }

        /// <summary>CanvasGroup saydamlığını yalnızca belirgin farkta değiştirir.</summary>
        public static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group != null && Mathf.Abs(group.alpha - alpha) >= 0.002f)
                group.alpha = alpha;
        }

        /// <summary>Etkileşimsiz, ışın engellemeyen CanvasGroup.</summary>
        public static CanvasGroup PassiveGroup(Component target, float alpha = 1f)
        {
            var group = UiFactory.EnsureCanvasGroup(target);
            if (group == null)
                return null;
            group.alpha = alpha;
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        /// <summary>anchoredPosition'ı yalnızca farklıysa atar.</summary>
        public static void SetPosition(RectTransform rt, Vector2 position)
        {
            if (rt != null && (rt.anchoredPosition - position).sqrMagnitude > 0.0001f)
                rt.anchoredPosition = position;
        }

        /// <summary>Yerel Z dönüşünü (derece) yalnızca farklıysa atar.</summary>
        public static void SetRotation(RectTransform rt, float zDegrees)
        {
            if (rt == null)
                return;

            if (Mathf.Abs(Mathf.DeltaAngle(rt.localEulerAngles.z, zDegrees)) > 0.01f)
                rt.localRotation = Quaternion.Euler(0f, 0f, zDegrees);
        }

        /// <summary>Tekdüze ölçeği yalnızca farklıysa atar.</summary>
        public static void SetScale(RectTransform rt, float scale)
        {
            if (rt != null && Mathf.Abs(rt.localScale.x - scale) > 0.0005f)
                rt.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Doluluk oranını yalnızca farklıysa atar.</summary>
        public static void SetFill(Image image, float fill)
        {
            if (image != null && Mathf.Abs(image.fillAmount - fill) > 0.0005f)
                image.fillAmount = fill;
        }

        /// <summary>Radyal (saat yönünde, tepeden) dolan görsel.</summary>
        public static Image RadialImage(string name, Transform parent, Sprite sprite, Color color, Vector2 position, Vector2 size)
        {
            var image = Image(name, parent, sprite, color, position, size);
            image.type = UnityEngine.UI.Image.Type.Filled;
            image.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            image.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            image.fillClockwise = true;
            image.fillAmount = 0f;
            return image;
        }
    }
}
