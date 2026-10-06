using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Yükleme ekranı için stilize harita silüeti: <see cref="MapLayout"/> verisinden (göl, dere, yol, yerleşim) prosedürel
    /// çizilir. Piksel işleme saf (<see cref="Render"/>); doku üretimi <see cref="CreateTexture"/> içindedir.
    /// </summary>
    public static class LoadingMapSilhouette
    {
        private static readonly Color32 Land = new Color32(58, 66, 44, 120);
        private static readonly Color32 Edge = new Color32(180, 182, 164, 200);
        private static readonly Color32 Water = new Color32(60, 105, 150, 210);
        private static readonly Color32 Road = new Color32(200, 190, 150, 170);
        private static readonly Color32 Town = new Color32(242, 169, 0, 255);
        private static readonly Color32 TownMinor = new Color32(227, 10, 23, 230);

        /// <summary>size x size piksellik renk dizisi (satır 0 = güney). Düzen null ise yalnız zemin çizilir.</summary>
        public static Color32[] Render(MapLayout layout, int size)
        {
            size = Mathf.Clamp(size, 32, 1024);
            var px = new Color32[size * size];
            var half = layout != null && layout.HalfSize > 1f ? layout.HalfSize : 512f;
            var scale = (size - 1) / (2f * half);

            // Zemin: yuvarlatılmış kare.
            var corner = size * 0.08f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                if (!InsideRounded(x, y, size, corner))
                    continue;
                px[y * size + x] = Land;
            }

            DrawRoundedEdge(px, size, corner);
            if (layout == null)
                return px;

            for (var i = 0; i < layout.Lakes.Count; i++)
            {
                var l = layout.Lakes[i];
                FillCircle(px, size, ToPx(l.Center, half, scale), l.Radius * scale, Water);
            }

            for (var i = 0; i < layout.Rivers.Count; i++)
                DrawPolyline(px, size, layout.Rivers[i].Points, half, scale, Mathf.Max(1.5f, layout.Rivers[i].Width * scale), Water);

            for (var i = 0; i < layout.Roads.Count; i++)
                DrawPolyline(px, size, layout.Roads[i].Points, half, scale, 1.2f, Road);

            for (var i = 0; i < layout.Locations.Count; i++)
            {
                var loc = layout.Locations[i];
                var r = loc.IsMajor ? Mathf.Max(3f, size * 0.016f) : Mathf.Max(2f, size * 0.009f);
                FillCircle(px, size, ToPx(loc.Center, half, scale), r, loc.IsMajor ? Town : TownMinor);
            }

            return px;
        }

        /// <summary>Haritayı doku olarak üretir (hata olursa null).</summary>
        public static Texture2D CreateTexture(string mapId, int size = 320)
        {
            try
            {
                var layout = MapLayout.Create(mapId, 1);
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "LoadingMapSilhouette" };
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                tex.SetPixels32(Render(layout, size));
                tex.Apply(false, true);
                return tex;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        private static Vector2 ToPx(Vector2 world, float half, float scale)
        {
            return new Vector2((world.x + half) * scale, (world.y + half) * scale);
        }

        private static bool InsideRounded(int x, int y, int size, float r)
        {
            var max = size - 1;
            var cx = x < r ? r : x > max - r ? max - r : x;
            var cy = y < r ? r : y > max - r ? max - r : y;
            var dx = x - cx;
            var dy = y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        private static void DrawRoundedEdge(Color32[] px, int size, float corner)
        {
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                if (!InsideRounded(x, y, size, corner))
                    continue;
                if (InsideRounded(x - 2, y, size, corner) && InsideRounded(x + 2, y, size, corner)
                    && InsideRounded(x, y - 2, size, corner) && InsideRounded(x, y + 2, size, corner)
                    && x >= 2 && y >= 2 && x < size - 2 && y < size - 2)
                    continue;
                px[y * size + x] = Edge;
            }
        }

        private static void FillCircle(Color32[] px, int size, Vector2 c, float radius, Color32 color)
        {
            var r = Mathf.Max(1f, radius);
            var x0 = Mathf.Max(0, Mathf.FloorToInt(c.x - r));
            var x1 = Mathf.Min(size - 1, Mathf.CeilToInt(c.x + r));
            var y0 = Mathf.Max(0, Mathf.FloorToInt(c.y - r));
            var y1 = Mathf.Min(size - 1, Mathf.CeilToInt(c.y + r));
            for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
            {
                var dx = x - c.x;
                var dy = y - c.y;
                if (dx * dx + dy * dy <= r * r)
                    px[y * size + x] = color;
            }
        }

        private static void DrawPolyline(Color32[] px, int size, System.Collections.Generic.List<Vector2> pts, float half, float scale, float width, Color32 color)
        {
            if (pts == null)
                return;
            var radius = Mathf.Max(0.8f, width * 0.5f);
            for (var i = 1; i < pts.Count; i++)
            {
                var a = ToPx(pts[i - 1], half, scale);
                var b = ToPx(pts[i], half, scale);
                var steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / Mathf.Max(0.8f, radius * 0.7f)));
                for (var s = 0; s <= steps; s++)
                    FillCircle(px, size, Vector2.Lerp(a, b, s / (float)steps), radius, color);
            }
        }
    }
}
