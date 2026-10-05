using System;
using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Arayüz için prosedürel sprite'lar (harici varlık yok). Mümkünse <see cref="ProceduralTextures"/> (Rendering)
    /// dokularını kullanır; o modül yoksa/başarısızsa kendi üretecine düşer. Tüm sprite'lar önbelleğe alınır,
    /// paylaşımlıdır — değiştirmeyin / yok etmeyin. Sprite'lar 100 piksel/birim ile üretilir; böylece
    /// 1920×1080 referanslı tuvalde 9-dilim kenarlar piksel ölçüsünde görünür.
    /// </summary>
    public static class UiSprites
    {
        private const float PixelsPerUnit = 100f;

        private static Sprite _white;
        private static Sprite _circle;
        private static Sprite _softCircle;
        private static Sprite _ring;
        private static Sprite _thinRing;
        private static Sprite _triangle;
        private static Sprite _diamond;
        private static Sprite _crosshairLine;
        private static Sprite _softLine;
        private static Sprite _verticalGradient;
        private static Sprite _horizontalGradient;
        private static Sprite _vignette;
        private static Sprite _turkishFlag;
        private static Sprite _star;
        private static Sprite _chevron;

        private static readonly Dictionary<int, Sprite> RoundedCache = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> RoundedOutlineCache = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> ChamferCache = new Dictionary<int, Sprite>();

        // ------------------------------------------------------------------ Temel şekiller

        /// <summary>Düz beyaz kare (4×4). Filled/Tiled görüntü türleri için gerekli (null sprite dolgu yapmaz).</summary>
        public static Sprite White => Valid(_white) ? _white : (_white = MakeSprite(FromRendering(() => ProceduralTextures.WhitePixel) ?? BuildWhite(), Vector4.zero));

        /// <summary>Kenar yumuşatmalı dolu beyaz daire (harita işaretleri, tutamaklar, nokta nişangâh).</summary>
        public static Sprite Circle => Valid(_circle) ? _circle : (_circle = MakeSprite(FromRendering(() => ProceduralTextures.Circle) ?? BuildCircle(128), Vector4.zero));

        /// <summary>Merkezden dışa yumuşak solan beyaz daire (parlama, ses/isabet göstergesi).</summary>
        public static Sprite SoftCircle => Valid(_softCircle) ? _softCircle : (_softCircle = MakeSprite(FromRendering(() => ProceduralTextures.SoftCircle) ?? BuildSoftCircle(64), Vector4.zero));

        /// <summary>Beyaz halka (bölge çemberi, nişangâh halkası, seçim çerçevesi).</summary>
        public static Sprite Ring => Valid(_ring) ? _ring : (_ring = MakeSprite(FromRendering(() => ProceduralTextures.Ring) ?? BuildRing(128, 0.92f, 0.14f), Vector4.zero));

        /// <summary>İnce beyaz halka (büyük ölçekli harita çemberleri için; 256 px, ~%3 kalınlık).</summary>
        public static Sprite ThinRing => Valid(_thinRing) ? _thinRing : (_thinRing = MakeSprite(BuildRing(256, 0.97f, 0.035f), Vector4.zero));

        /// <summary>Yukarı bakan beyaz üçgen (yön/hasar okları, pusula işaretçisi). Döndürerek kullanın.</summary>
        public static Sprite Triangle => Valid(_triangle) ? _triangle : (_triangle = MakeSprite(FromRendering(() => ProceduralTextures.Triangle) ?? BuildTriangle(128), Vector4.zero));

        /// <summary>Beyaz eşkenar dörtgen (baklava) — işaretçiler, isabet işareti uçları, rütbe süsleri.</summary>
        public static Sprite Diamond => Valid(_diamond) ? _diamond : (_diamond = MakeSprite(BuildDiamond(64), Vector4.zero));

        /// <summary>
        /// Nişangâh çizgisi: dikey, uçları hafif yumuşak beyaz çubuk (8×32). Yatay için 90° döndürün.
        /// 9-dilim kenarı sayesinde her uzunlukta keskin kalır.
        /// </summary>
        public static Sprite CrosshairLine => Valid(_crosshairLine) ? _crosshairLine : (_crosshairLine = MakeSprite(BuildCrosshairLine(8, 32), new Vector4(0f, 4f, 0f, 4f)));

        /// <summary>Kenarları yumuşak yatay çizgi (ayraçlar, hız çizgileri). Rendering modülünden gelir.</summary>
        public static Sprite SoftLine => Valid(_softLine) ? _softLine : (_softLine = MakeSprite(FromRendering(() => ProceduralTextures.SoftLine) ?? BuildCrosshairLine(32, 8), Vector4.zero));

        /// <summary>Dikey geçiş: altta opak beyaz, üstte saydam (başlık şeritleri, alt gölgeler).</summary>
        public static Sprite VerticalGradient => Valid(_verticalGradient) ? _verticalGradient : (_verticalGradient = MakeSprite(BuildGradient(4, 64, true), Vector4.zero));

        /// <summary>Yatay geçiş: solda opak beyaz, sağda saydam (öldürme akışı/satır vurguları).</summary>
        public static Sprite HorizontalGradient => Valid(_horizontalGradient) ? _horizontalGradient : (_horizontalGradient = MakeSprite(BuildGradient(64, 4, false), Vector4.zero));

        /// <summary>Kenar karartması: merkez saydam, kenarlar opak beyaz (hasar/düşük can/dürbün kenarı). Renk vererek kullanın.</summary>
        public static Sprite Vignette => Valid(_vignette) ? _vignette : (_vignette = MakeSprite(BuildVignette(128), Vector4.zero));

        /// <summary>Türk bayrağı (ay-yıldız). Rendering modülünden; yoksa kendi üretilir.</summary>
        public static Sprite TurkishFlag => Valid(_turkishFlag) ? _turkishFlag : (_turkishFlag = MakeSprite(FromRendering(() => ProceduralTextures.TurkishFlag) ?? BuildFlag(384, 256), Vector4.zero));

        /// <summary>Beş köşeli beyaz yıldız (rütbe/kariyer süsleri, MVP işareti).</summary>
        public static Sprite Star => Valid(_star) ? _star : (_star = MakeSprite(BuildStar(96), Vector4.zero));

        /// <summary>Yukarı bakan beyaz şerit (V biçimli rütbe işareti, liste genişletme oku).</summary>
        public static Sprite Chevron => Valid(_chevron) ? _chevron : (_chevron = MakeSprite(BuildChevron(64), Vector4.zero));

        // ------------------------------------------------------------------ 9-dilim paneller

        /// <summary>Varsayılan yuvarlatılmış dikdörtgen (9-dilim, <see cref="UiTheme.CornerRadius"/> yarıçap). Image.Type.Sliced ile kullanın.</summary>
        public static Sprite RoundedRect => GetRoundedRect(UiTheme.CornerRadius);

        /// <summary>Varsayılan yuvarlatılmış dikdörtgen çerçeve (2 px kenar, 9-dilim).</summary>
        public static Sprite RoundedRectOutline => GetRoundedRectOutline(UiTheme.CornerRadius);

        /// <summary>Köşeleri pahlı (kesik) taktik panel (9-dilim, 8 px pah) — askerî görünüm.</summary>
        public static Sprite ChamferRect => GetChamferRect(8);

        /// <summary>
        /// Verilen köşe yarıçapında (px, 1..64) yuvarlatılmış dikdörtgen 9-dilim sprite döndürür (önbellekli).
        /// </summary>
        public static Sprite GetRoundedRect(int radius)
        {
            radius = Mathf.Clamp(radius, 1, 64);
            if (RoundedCache.TryGetValue(radius, out var cached) && Valid(cached))
                return cached;

            var size = radius * 2 + 4;
            var sprite = MakeSprite(BuildRoundedRect(size, radius, -1f), new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
            RoundedCache[radius] = sprite;
            return sprite;
        }

        /// <summary>Verilen köşe yarıçapında, 2 px kalınlıkta yuvarlatılmış çerçeve 9-dilim sprite (önbellekli).</summary>
        public static Sprite GetRoundedRectOutline(int radius)
        {
            radius = Mathf.Clamp(radius, 2, 64);
            if (RoundedOutlineCache.TryGetValue(radius, out var cached) && Valid(cached))
                return cached;

            var size = radius * 2 + 4;
            var sprite = MakeSprite(BuildRoundedRect(size, radius, 2f), new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
            RoundedOutlineCache[radius] = sprite;
            return sprite;
        }

        /// <summary>Verilen pah boyunda (px, 1..48) köşeleri kesik dikdörtgen 9-dilim sprite (önbellekli).</summary>
        public static Sprite GetChamferRect(int chamfer)
        {
            chamfer = Mathf.Clamp(chamfer, 1, 48);
            if (ChamferCache.TryGetValue(chamfer, out var cached) && Valid(cached))
                return cached;

            var size = chamfer * 2 + 4;
            var sprite = MakeSprite(BuildChamfer(size, chamfer), new Vector4(chamfer + 1, chamfer + 1, chamfer + 1, chamfer + 1));
            ChamferCache[chamfer] = sprite;
            return sprite;
        }

        /// <summary>Sprite'ın 9-dilim kenarı varsa true (Image.Type.Sliced seçimi için).</summary>
        public static bool HasBorder(Sprite sprite) => sprite != null && sprite.border.sqrMagnitude > 0f;

        // ------------------------------------------------------------------ İç: oluşturma

        private static bool Valid(UnityEngine.Object o) => o != null;

        /// <summary>Rendering modülünden doku alır; istisna veya null olursa null döner (kendi üretecine düşmek için).</summary>
        private static Texture2D FromRendering(Func<Texture2D> getter)
        {
            try
            {
                return getter();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UiSprites] ProceduralTextures kullanılamadı, yerel üreteç kullanılıyor: " + e.Message);
                return null;
            }
        }

        private static Sprite MakeSprite(Texture2D texture, Vector4 border)
        {
            if (texture == null)
                texture = BuildWhite();

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                PixelsPerUnit, 0, SpriteMeshType.FullRect, border);
            sprite.name = "UI_" + texture.name;
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }

        private static Texture2D CreateTexture(string name, int width, int height, Color32[] pixels)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontUnloadUnusedAsset
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static byte Alpha(float a) => (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255);

        /// <summary>İşaretli mesafeden (içeride negatif, piksel) kenar yumuşatmalı kapsama.</summary>
        private static float Coverage(float signedDistance) => Mathf.Clamp01(0.5f - signedDistance);

        private static Texture2D BuildWhite()
        {
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            return CreateTexture("UI_White", 4, 4, pixels);
        }

        private static Texture2D BuildCircle(int size)
        {
            var pixels = new Color32[size * size];
            var c = size * 0.5f;
            var r = c - 1f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - c;
                    var dy = y + 0.5f - c;
                    var d = Mathf.Sqrt(dx * dx + dy * dy) - r;
                    pixels[y * size + x] = new Color32(255, 255, 255, Alpha(Coverage(d)));
                }
            }

            return CreateTexture("UI_Circle", size, size, pixels);
        }

        private static Texture2D BuildSoftCircle(int size)
        {
            var pixels = new Color32[size * size];
            var c = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x + 0.5f - c) / c;
                    var dy = (y + 0.5f - c) / c;
                    var t = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color32(255, 255, 255, Alpha(t * t * (3f - 2f * t)));
                }
            }

            return CreateTexture("UI_SoftCircle", size, size, pixels);
        }

        private static Texture2D BuildRing(int size, float outer01, float thickness01)
        {
            var pixels = new Color32[size * size];
            var c = size * 0.5f;
            var outer = c * outer01;
            var inner = Mathf.Max(0f, outer - c * thickness01);
            var mid = (outer + inner) * 0.5f;
            var half = (outer - inner) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - c;
                    var dy = y + 0.5f - c;
                    var d = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - mid) - half;
                    pixels[y * size + x] = new Color32(255, 255, 255, Alpha(Coverage(d)));
                }
            }

            return CreateTexture("UI_Ring", size, size, pixels);
        }

        private static Texture2D BuildTriangle(int size)
        {
            var pixels = new Color32[size * size];
            var margin = size * 0.08f;
            var top = new Vector2(size * 0.5f, size - margin);
            var left = new Vector2(margin, margin);
            var right = new Vector2(size - margin, margin);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    var d = Mathf.Max(EdgeDistance(p, top, right), Mathf.Max(EdgeDistance(p, right, left), EdgeDistance(p, left, top)));
                    pixels[y * size + x] = new Color32(255, 255, 255, Alpha(Coverage(d)));
                }
            }

            return CreateTexture("UI_Triangle", size, size, pixels);
        }

        private static Texture2D BuildDiamond(int size)
        {
            var pixels = new Color32[size * size];
            var c = size * 0.5f;
            var r = c - 1.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Abs(x + 0.5f - c);
                    var dy = Mathf.Abs(y + 0.5f - c);
                    // L1 mesafesi → piksel cinsinden kenar mesafesi (45° kenar normali: /√2)
                    var d = (dx + dy - r) * 0.70710678f;
                    pixels[y * size + x] = new Color32(255, 255, 255, Alpha(Coverage(d)));
                }
            }

            return CreateTexture("UI_Diamond", size, size, pixels);
        }

        private static Texture2D BuildCrosshairLine(int width, int height)
        {
            var pixels = new Color32[width * height];
            var vertical = height >= width;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    float across, along, acrossSize, alongSize;
                    if (vertical)
                    {
                        across = x + 0.5f;
                        acrossSize = width;
                        along = y + 0.5f;
                        alongSize = height;
                    }
                    else
                    {
                        across = y + 0.5f;
                        acrossSize = height;
                        along = x + 0.5f;
                        alongSize = width;
                    }

                    // Enine: her kenarda ~1 px saydam pay + 1 px yumuşama, ortası tam opak. Boyuna: uçlarda 1 px yumuşama.
                    var dAcross = Mathf.Abs(across - acrossSize * 0.5f) - acrossSize * 0.5f + 1.5f;
                    var dAlong = Mathf.Abs(along - alongSize * 0.5f) - alongSize * 0.5f + 1f;
                    var a = Coverage(Mathf.Max(dAcross, dAlong));
                    pixels[y * width + x] = new Color32(255, 255, 255, Alpha(a));
                }
            }

            return CreateTexture(vertical ? "UI_CrosshairLine" : "UI_SoftLine", width, height, pixels);
        }

        private static Texture2D BuildGradient(int width, int height, bool vertical)
        {
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var t = vertical ? 1f - (y + 0.5f) / height : 1f - (x + 0.5f) / width;
                    pixels[y * width + x] = new Color32(255, 255, 255, Alpha(t));
                }
            }

            return CreateTexture(vertical ? "UI_GradientV" : "UI_GradientH", width, height, pixels);
        }

        private static Texture2D BuildVignette(int size)
        {
            var pixels = new Color32[size * size];
            var c = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x + 0.5f - c) / c;
                    var dy = (y + 0.5f - c) / c;
                    var d = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f;
                    var t = Mathf.Clamp01((d - 0.35f) / 0.65f);
                    pixels[y * size + x] = new Color32(255, 255, 255, Alpha(t * t));
                }
            }

            return CreateTexture("UI_Vignette", size, size, pixels);
        }

        private static Texture2D BuildRoundedRect(int size, int radius, float outlineWidth)
        {
            var pixels = new Color32[size * size];
            var half = size * 0.5f;
            // Düz kenarlar doku sınırına tam oturur (9-dilimde yarı saydam saçak kalmaz); köşeler kenar yumuşatmalı.
            var inner = half - radius;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var qx = Mathf.Abs(x + 0.5f - half) - inner;
                    var qy = Mathf.Abs(y + 0.5f - half) - inner;
                    var ox = Mathf.Max(qx, 0f);
                    var oy = Mathf.Max(qy, 0f);
                    var d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    if (outlineWidth > 0f)
                        d = Mathf.Abs(d + outlineWidth * 0.5f) - outlineWidth * 0.5f;
                    pixels[y * size + x] = new Color32(255, 255, 255, Alpha(Coverage(d)));
                }
            }

            return CreateTexture(outlineWidth > 0f ? "UI_RoundedOutline_" + radius : "UI_Rounded_" + radius, size, size, pixels);
        }

        private static Texture2D BuildChamfer(int size, int chamfer)
        {
            var pixels = new Color32[size * size];
            var half = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var ax = Mathf.Abs(x + 0.5f - half);
                    var ay = Mathf.Abs(y + 0.5f - half);
                    var dBox = Mathf.Max(ax, ay) - half;
                    var dCut = (ax + ay - (2f * half - chamfer)) * 0.70710678f;
                    pixels[y * size + x] = new Color32(255, 255, 255, Alpha(Coverage(Mathf.Max(dBox, dCut))));
                }
            }

            return CreateTexture("UI_Chamfer_" + chamfer, size, size, pixels);
        }

        private static Texture2D BuildStar(int size)
        {
            var pixels = new Color32[size * size];
            var c = new Vector2(size * 0.5f, size * 0.5f);
            var outer = size * 0.48f;
            var verts = StarVertices(c, outer, outer * 0.4f, 90f);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    var a = Supersample(p, verts);
                    pixels[y * size + x] = new Color32(255, 255, 255, Alpha(a));
                }
            }

            return CreateTexture("UI_Star", size, size, pixels);
        }

        private static Texture2D BuildChevron(int size)
        {
            var pixels = new Color32[size * size];
            var thickness = size * 0.16f;
            var apex = new Vector2(size * 0.5f, size * 0.78f);
            var leftEnd = new Vector2(size * 0.1f, size * 0.32f);
            var rightEnd = new Vector2(size * 0.9f, size * 0.32f);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    var d = Mathf.Min(SegmentDistance(p, leftEnd, apex), SegmentDistance(p, apex, rightEnd)) - thickness * 0.5f;
                    pixels[y * size + x] = new Color32(255, 255, 255, Alpha(Coverage(d)));
                }
            }

            return CreateTexture("UI_Chevron", size, size, pixels);
        }

        private static Texture2D BuildFlag(int width, int height)
        {
            // Türk Bayrağı Kanunu oranları (G = yükseklik): hilal dış çember merkezi gönderden 0.5G, çap 0.5G;
            // iç çember 0.0625G sağda, çap 0.4G; iç çember merkezi ile yıldız çemberi arası 1/3 G, yıldız çemberi çapı 0.25G.
            var pixels = new Color32[width * height];
            var g = (float)height;
            var red = new Color32(0xE3, 0x0A, 0x17, 0xFF);
            var outerC = new Vector2(0.5f * g, 0.5f * g);
            var outerR = 0.25f * g;
            var innerC = new Vector2(0.5f * g + 0.0625f * g, 0.5f * g);
            var innerR = 0.2f * g;
            var starC = new Vector2((0.5f + 0.0625f + 1f / 3f + 0.125f) * g, 0.5f * g);
            var starVerts = StarVertices(starC, 0.125f * g, 0.125f * g * 0.382f, 180f);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    var dOuter = Vector2.Distance(p, outerC) - outerR;
                    var dInner = innerR - Vector2.Distance(p, innerC);
                    var crescent = Coverage(Mathf.Max(dOuter, dInner));
                    var star = 0f;
                    if (Mathf.Abs(p.x - starC.x) < 0.14f * g && Mathf.Abs(p.y - starC.y) < 0.14f * g)
                        star = Supersample(p, starVerts);
                    var w = Mathf.Max(crescent, star);
                    pixels[y * width + x] = Color32.Lerp(red, new Color32(255, 255, 255, 255), w);
                }
            }

            return CreateTexture("UI_TurkishFlag", width, height, pixels);
        }

        private static Vector2[] StarVertices(Vector2 center, float outerRadius, float innerRadius, float startAngleDegrees)
        {
            var verts = new Vector2[10];
            for (var i = 0; i < 10; i++)
            {
                var r = (i & 1) == 0 ? outerRadius : innerRadius;
                var a = (startAngleDegrees + i * 36f) * Mathf.Deg2Rad;
                verts[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }

            return verts;
        }

        /// <summary>4×4 alt örnekleme ile çokgen kapsaması (konkav çokgenler için, yalnızca üretim sırasında).</summary>
        private static float Supersample(Vector2 pixelCenter, Vector2[] polygon)
        {
            var hits = 0;
            for (var sy = 0; sy < 4; sy++)
            {
                for (var sx = 0; sx < 4; sx++)
                {
                    var s = new Vector2(pixelCenter.x - 0.5f + (sx + 0.5f) * 0.25f, pixelCenter.y - 0.5f + (sy + 0.5f) * 0.25f);
                    if (PointInPolygon(s, polygon))
                        hits++;
                }
            }

            return hits / 16f;
        }

        private static bool PointInPolygon(Vector2 p, Vector2[] poly)
        {
            var inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }

            return inside;
        }

        private static float EdgeDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            // Saat yönündeki (y yukarı) kenar için: sağ taraf iç bölgedir → içeride negatif, dışarıda pozitif.
            var e = b - a;
            var n = new Vector2(e.y, -e.x).normalized;
            return -Vector2.Dot(p - a, n);
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-5f));
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
