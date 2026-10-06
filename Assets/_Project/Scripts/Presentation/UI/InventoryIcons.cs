using Project.Application.Catalogs;
using Project.Core.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Envanter simgeleri: kategori başına prosedürel çizim (dikdörtgen/daire/üçgen parçalarından), silahlar için
    /// kategori siluetleri. Simge 0..1 birim karede tanımlanır, kapsayıcı boyutuna otomatik ölçeklenir; renk
    /// <see cref="Tint"/> ile toplu değişir. Işın hedefi kapalıdır.
    /// </summary>
    public sealed class InventoryIcon
    {
        public RectTransform Root;
        public readonly System.Collections.Generic.List<Image> Parts = new System.Collections.Generic.List<Image>(10);
        private readonly System.Collections.Generic.List<float> _shade = new System.Collections.Generic.List<float>(10);

        internal void Add(Image image, float shade)
        {
            Parts.Add(image);
            _shade.Add(shade);
        }

        /// <summary>Tüm parçaları verilen renge boyar (her parçanın kendi açık/koyu payı korunur).</summary>
        public void Tint(Color color)
        {
            for (var i = 0; i < Parts.Count; i++)
            {
                var c = _shade[i] >= 0f ? Color.Lerp(color, Color.white, _shade[i]) : Color.Lerp(color, Color.black, -_shade[i]);
                c.a = color.a;
                if (Parts[i] != null)
                    Parts[i].color = c;
            }
        }
    }

    public static class InventoryIcons
    {
        /// <summary>Kategoriye (ve silahsa silah kategorisine) uygun simgeyi <paramref name="parent"/> altında kurar.</summary>
        public static InventoryIcon Create(Transform parent, ItemCategory category, string itemId, WeaponCategory weapon = WeaponCategory.None)
        {
            var icon = new InventoryIcon { Root = UiFactory.CreateRect("Icon", parent) };
            UiFactory.Stretch(icon.Root);

            switch (category)
            {
                case ItemCategory.Weapon: Weapon(icon, weapon); break;
                case ItemCategory.Ammunition: Ammo(icon); break;
                case ItemCategory.Armor: Vest(icon); break;
                case ItemCategory.Helmet: Helmet(icon); break;
                case ItemCategory.Backpack: Backpack(icon); break;
                case ItemCategory.Medical: Medical(icon, itemId); break;
                case ItemCategory.Boost: Boost(icon, itemId); break;
                case ItemCategory.Throwable: Grenade(icon, itemId); break;
                case ItemCategory.Attachment: Attachment(icon, itemId); break;
                case ItemCategory.Equipment: Goggles(icon); break;
                default: Part(icon, Box, 0.25f, 0.25f, 0.75f, 0.75f, 0f, 0f, UiSprites.RoundedRect); break;
            }

            return icon;
        }

        private const float Box = 0f;

        /// <summary>Normalleştirilmiş (0..1) dikdörtgen parça; <paramref name="rotation"/> derece (merkez etrafında).</summary>
        private static Image Part(InventoryIcon icon, float unused, float x0, float y0, float x1, float y1, float rotation, float shade, Sprite sprite = null)
        {
            var image = UiFactory.Image(icon.Root, sprite != null ? sprite : UiSprites.White, Color.white);
            image.raycastTarget = false;
            var rt = image.rectTransform;
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localRotation = Quaternion.Euler(0f, 0f, rotation);
            icon.Add(image, shade);
            return image;
        }

        private static Image Rect(InventoryIcon i, float x0, float y0, float x1, float y1, float shade = 0f, float rot = 0f)
            => Part(i, Box, x0, y0, x1, y1, rot, shade);

        private static Image Round(InventoryIcon i, float x0, float y0, float x1, float y1, float shade = 0f)
            => Part(i, Box, x0, y0, x1, y1, 0f, shade, UiSprites.Circle);

        private static Image Tri(InventoryIcon i, float x0, float y0, float x1, float y1, float shade = 0f, float rot = 0f)
            => Part(i, Box, x0, y0, x1, y1, rot, shade, UiSprites.Triangle);

        private static Image Soft(InventoryIcon i, float x0, float y0, float x1, float y1, float shade = 0f)
            => Part(i, Box, x0, y0, x1, y1, 0f, shade, UiSprites.GetRoundedRect(4));

        // ------------------------------------------------------------------ Silahlar (yandan siluet, namlu sağa)

        private static void Weapon(InventoryIcon i, WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.Pistol:
                    Rect(i, 0.28f, 0.52f, 0.86f, 0.68f);            // sürgü
                    Rect(i, 0.28f, 0.40f, 0.56f, 0.52f, -0.15f);   // gövde
                    Rect(i, 0.30f, 0.14f, 0.45f, 0.42f, -0.1f, -12f); // kabza
                    Rect(i, 0.50f, 0.38f, 0.58f, 0.42f, 0.2f);     // tetik koruması
                    break;
                case WeaponCategory.Smg:
                    Rect(i, 0.20f, 0.52f, 0.88f, 0.68f);
                    Rect(i, 0.40f, 0.30f, 0.52f, 0.52f, -0.15f);
                    Rect(i, 0.56f, 0.12f, 0.64f, 0.52f, -0.2f);   // şarjör
                    Rect(i, 0.26f, 0.30f, 0.38f, 0.54f, -0.1f, -10f);
                    Rect(i, 0.08f, 0.54f, 0.22f, 0.64f, -0.1f);   // dipçik
                    break;
                case WeaponCategory.Shotgun:
                    Rect(i, 0.28f, 0.58f, 0.94f, 0.70f);
                    Rect(i, 0.28f, 0.46f, 0.80f, 0.58f, -0.1f);
                    Rect(i, 0.52f, 0.46f, 0.76f, 0.54f, 0.2f);    // el koruma
                    Rect(i, 0.05f, 0.38f, 0.30f, 0.62f, -0.2f, -8f);
                    break;
                case WeaponCategory.Sniper:
                    Rect(i, 0.20f, 0.50f, 0.96f, 0.58f);
                    Rect(i, 0.28f, 0.42f, 0.60f, 0.52f, -0.1f);
                    Rect(i, 0.34f, 0.60f, 0.62f, 0.72f, 0.15f);   // dürbün
                    Round(i, 0.60f, 0.58f, 0.70f, 0.74f, 0.25f);
                    Rect(i, 0.04f, 0.38f, 0.28f, 0.56f, -0.2f, -6f);
                    Rect(i, 0.84f, 0.30f, 0.88f, 0.50f, 0.1f, 12f); // bipod
                    break;
                case WeaponCategory.Lmg:
                    Rect(i, 0.22f, 0.54f, 0.94f, 0.66f);
                    Rect(i, 0.28f, 0.40f, 0.70f, 0.54f, -0.1f);
                    Soft(i, 0.44f, 0.18f, 0.60f, 0.40f, -0.2f);   // kutu şarjör
                    Rect(i, 0.06f, 0.42f, 0.28f, 0.60f, -0.2f, -6f);
                    Rect(i, 0.82f, 0.34f, 0.86f, 0.54f, 0.1f, 14f);
                    break;
                case WeaponCategory.Melee:
                    Tri(i, 0.40f, 0.28f, 0.70f, 0.94f, 0.3f, 0f);
                    Rect(i, 0.46f, 0.12f, 0.58f, 0.30f, -0.2f);
                    break;
                default: // Taarruz tüfeği / DMR
                    Rect(i, 0.24f, 0.54f, 0.94f, 0.64f);
                    Rect(i, 0.30f, 0.40f, 0.68f, 0.54f, -0.1f);
                    Rect(i, 0.46f, 0.18f, 0.56f, 0.42f, -0.2f, 8f); // şarjör
                    Rect(i, 0.32f, 0.22f, 0.42f, 0.42f, -0.1f, -10f);
                    Rect(i, 0.06f, 0.40f, 0.30f, 0.62f, -0.2f, -4f);
                    if (category == WeaponCategory.Dmr)
                        Rect(i, 0.40f, 0.64f, 0.64f, 0.72f, 0.2f);
                    break;
            }
        }

        // ------------------------------------------------------------------ Diğer kategoriler

        private static void Ammo(InventoryIcon i)
        {
            for (var k = 0; k < 3; k++)
            {
                var x = 0.16f + k * 0.26f;
                Rect(i, x, 0.14f, x + 0.20f, 0.58f, 0f);                // kovan
                Round(i, x, 0.50f, x + 0.20f, 0.86f, 0.35f);            // kurşun ucu
                Rect(i, x, 0.14f, x + 0.20f, 0.22f, -0.35f);            // kapsül
            }
        }

        private static void Vest(InventoryIcon i)
        {
            Soft(i, 0.22f, 0.12f, 0.78f, 0.80f);
            Rect(i, 0.12f, 0.58f, 0.30f, 0.88f, -0.1f, -12f);          // omuz
            Rect(i, 0.70f, 0.58f, 0.88f, 0.88f, -0.1f, 12f);
            Rect(i, 0.32f, 0.28f, 0.68f, 0.56f, -0.25f);               // plaka
            Rect(i, 0.28f, 0.16f, 0.72f, 0.22f, -0.35f);
        }

        private static void Helmet(InventoryIcon i)
        {
            Round(i, 0.14f, 0.26f, 0.86f, 0.92f);
            Rect(i, 0.10f, 0.20f, 0.90f, 0.40f, -0.15f);               // siper kenarı
            Rect(i, 0.20f, 0.30f, 0.80f, 0.36f, 0.3f);                 // kayış
            Rect(i, 0.44f, 0.60f, 0.56f, 0.86f, 0.25f);                // alın şeridi
        }

        private static void Backpack(InventoryIcon i)
        {
            Soft(i, 0.24f, 0.10f, 0.76f, 0.86f);
            Soft(i, 0.30f, 0.14f, 0.70f, 0.40f, -0.25f);               // ön cep
            Rect(i, 0.28f, 0.50f, 0.72f, 0.56f, 0.25f);                // kapak kayışı
            Rect(i, 0.12f, 0.20f, 0.20f, 0.70f, -0.35f);               // askılar
            Rect(i, 0.80f, 0.20f, 0.88f, 0.70f, -0.35f);
        }

        private static void Medical(InventoryIcon i, string itemId)
        {
            Soft(i, 0.14f, 0.20f, 0.86f, 0.80f, 0.55f);
            Rect(i, 0.40f, 0.30f, 0.60f, 0.70f, 0f);
            Rect(i, 0.28f, 0.42f, 0.72f, 0.58f, 0f);
            if (string.Equals(itemId, ItemIds.Bandage, System.StringComparison.Ordinal))
                Rect(i, 0.10f, 0.44f, 0.90f, 0.56f, -0.2f, 35f);       // sargı
        }

        private static void Boost(InventoryIcon i, string itemId)
        {
            if (string.Equals(itemId, ItemIds.Painkiller, System.StringComparison.Ordinal))
            {
                Soft(i, 0.30f, 0.12f, 0.70f, 0.74f);                    // şişe
                Rect(i, 0.38f, 0.74f, 0.62f, 0.88f, -0.3f);             // kapak
                Rect(i, 0.34f, 0.34f, 0.66f, 0.58f, 0.6f);              // etiket
                return;
            }

            Soft(i, 0.30f, 0.10f, 0.70f, 0.86f);                        // teneke
            Rect(i, 0.30f, 0.44f, 0.70f, 0.56f, -0.3f);                 // bant
            Tri(i, 0.40f, 0.30f, 0.60f, 0.70f, 0.7f, 0f);               // şimşek benzeri
        }

        private static void Grenade(InventoryIcon i, string itemId)
        {
            var smoke = string.Equals(itemId, ItemIds.SmokeGrenade, System.StringComparison.Ordinal);
            if (smoke)
                Soft(i, 0.32f, 0.12f, 0.68f, 0.74f);
            else
                Round(i, 0.22f, 0.10f, 0.78f, 0.74f);
            Rect(i, 0.40f, 0.72f, 0.60f, 0.84f, -0.3f);                 // kapak
            Rect(i, 0.58f, 0.78f, 0.84f, 0.84f, 0.2f, 12f);             // kol
            Round(i, 0.74f, 0.66f, 0.92f, 0.84f, 0.3f);                 // pim halkası
            if (!smoke)
            {
                Rect(i, 0.30f, 0.34f, 0.70f, 0.38f, -0.3f);
                Rect(i, 0.30f, 0.50f, 0.70f, 0.54f, -0.3f);
            }
        }

        private static void Attachment(InventoryIcon i, string itemId)
        {
            if (string.Equals(itemId, ItemIds.Suppressor, System.StringComparison.Ordinal))
            {
                Soft(i, 0.14f, 0.40f, 0.86f, 0.62f);
                Rect(i, 0.30f, 0.44f, 0.34f, 0.58f, -0.4f);
                Rect(i, 0.46f, 0.44f, 0.50f, 0.58f, -0.4f);
                Rect(i, 0.62f, 0.44f, 0.66f, 0.58f, -0.4f);
            }
            else if (string.Equals(itemId, ItemIds.ExtMag, System.StringComparison.Ordinal))
            {
                Rect(i, 0.36f, 0.10f, 0.64f, 0.86f, 0f, -8f);
                Rect(i, 0.36f, 0.10f, 0.64f, 0.20f, -0.4f, -8f);
            }
            else if (string.Equals(itemId, ItemIds.VerticalGrip, System.StringComparison.Ordinal))
            {
                Rect(i, 0.40f, 0.12f, 0.60f, 0.72f);
                Rect(i, 0.28f, 0.70f, 0.72f, 0.84f, -0.3f);
            }
            else if (string.Equals(itemId, ItemIds.SniperStock, System.StringComparison.Ordinal))
            {
                Rect(i, 0.12f, 0.36f, 0.88f, 0.64f, 0f, -6f);
                Rect(i, 0.12f, 0.30f, 0.24f, 0.70f, -0.4f);
            }
            else
            {
                // Dürbün / kırmızı nokta.
                Soft(i, 0.16f, 0.42f, 0.84f, 0.68f);
                Round(i, 0.08f, 0.36f, 0.30f, 0.74f, 0.2f);
                Round(i, 0.70f, 0.36f, 0.92f, 0.74f, 0.2f);
                Rect(i, 0.42f, 0.24f, 0.58f, 0.42f, -0.3f);
            }
        }

        private static void Goggles(InventoryIcon i)
        {
            Rect(i, 0.08f, 0.40f, 0.92f, 0.52f, -0.4f);                 // kayış
            Soft(i, 0.20f, 0.30f, 0.46f, 0.74f);
            Soft(i, 0.54f, 0.30f, 0.80f, 0.74f);
            Round(i, 0.24f, 0.38f, 0.42f, 0.62f, 0.6f);
            Round(i, 0.58f, 0.38f, 0.76f, 0.62f, 0.6f);
        }
    }
}
