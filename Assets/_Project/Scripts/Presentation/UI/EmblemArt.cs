using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Yazılım rasterleştirici (CPU, kenar yumuşatmalı): çokgen/halka/kapsül/ışıma çizimi. Üst-sol orijin (y aşağı), Color tamponu
    /// (alfa korunur). Saf; Unity nesnesi gerektirmez. Çokgen dolgusu 4 alt tarama çizgisi + yatay kesin kapsama ile yapılır.
    /// </summary>
    public sealed class ArtSurface
    {
        public readonly int Width;
        public readonly int Height;
        public readonly Color[] Pixels;

        private readonly float[] _cover;
        private readonly List<float> _xs = new List<float>(16);
        private const int SubRows = 4;

        public ArtSurface(int width, int height)
        {
            Width = Math.Max(4, width);
            Height = Math.Max(4, height);
            Pixels = new Color[Width * Height];
            _cover = new float[Width + 2];
        }

        // ------------------------------------------------------------------ Piksel

        /// <summary>"Over" karışımı (düz alfa).</summary>
        public void Over(int x, int y, Color s, float cov = 1f)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return;
            var sa = s.a * cov;
            if (sa <= 0.0005f)
                return;
            var i = y * Width + x;
            var d = Pixels[i];
            var oa = sa + d.a * (1f - sa);
            if (oa <= 0f)
                return;
            var k = d.a * (1f - sa);
            Pixels[i] = new Color((s.r * sa + d.r * k) / oa, (s.g * sa + d.g * k) / oa, (s.b * sa + d.b * k) / oa, oa);
        }

        /// <summary>Toplamsal (ışıma) karışımı.</summary>
        public void Add(int x, int y, Color s, float amount)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height || amount <= 0f)
                return;
            var i = y * Width + x;
            var d = Pixels[i];
            Pixels[i] = new Color(Mathf.Min(1f, d.r + s.r * amount), Mathf.Min(1f, d.g + s.g * amount), Mathf.Min(1f, d.b + s.b * amount), Mathf.Max(d.a, Mathf.Clamp01(amount * s.a)));
        }

        public void Clear(Color c)
        {
            for (var i = 0; i < Pixels.Length; i++)
                Pixels[i] = c;
        }

        public void VerticalGradient(Color top, Color bottom, int y0 = 0, int y1 = -1)
        {
            if (y1 < 0)
                y1 = Height - 1;
            y0 = Mathf.Clamp(y0, 0, Height - 1);
            y1 = Mathf.Clamp(y1, y0, Height - 1);
            for (var y = y0; y <= y1; y++)
            {
                var t = y1 == y0 ? 0f : (y - y0) / (float)(y1 - y0);
                var c = Color.Lerp(top, bottom, t);
                for (var x = 0; x < Width; x++)
                    Pixels[y * Width + x] = c;
            }
        }

        // ------------------------------------------------------------------ Şekiller

        /// <summary>Çok konturlu (çift-tek kuralı) dolgu; shade(x, y) her piksel için renk verir.</summary>
        public void FillPaths(IList<Vector2[]> paths, Func<int, int, Color> shade)
        {
            if (paths == null || paths.Count == 0)
                return;
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            for (var p = 0; p < paths.Count; p++)
            {
                var pts = paths[p];
                for (var i = 0; i < pts.Length; i++)
                {
                    if (pts[i].y < minY) minY = pts[i].y;
                    if (pts[i].y > maxY) maxY = pts[i].y;
                }
            }

            var y0 = Mathf.Max(0, Mathf.FloorToInt(minY));
            var y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(maxY));
            for (var y = y0; y <= y1; y++)
            {
                var lo = Width;
                var hi = -1;
                for (var s = 0; s < SubRows; s++)
                {
                    var sy = y + (s + 0.5f) / SubRows;
                    _xs.Clear();
                    for (var p = 0; p < paths.Count; p++)
                    {
                        var pts = paths[p];
                        var n = pts.Length;
                        for (var i = 0; i < n; i++)
                        {
                            var a = pts[i];
                            var b = pts[(i + 1) % n];
                            if ((a.y <= sy) == (b.y <= sy))
                                continue;
                            _xs.Add(a.x + (sy - a.y) / (b.y - a.y) * (b.x - a.x));
                        }
                    }

                    if (_xs.Count < 2)
                        continue;
                    _xs.Sort();
                    for (var k = 0; k + 1 < _xs.Count; k += 2)
                    {
                        var xa = Mathf.Max(0f, _xs[k]);
                        var xb = Mathf.Min(Width, _xs[k + 1]);
                        if (xb <= xa)
                            continue;
                        var ia = (int)xa;
                        var ib = (int)xb;
                        if (ia < lo) lo = ia;
                        if (ib > hi) hi = Mathf.Min(ib, Width - 1);
                        if (ia == ib)
                        {
                            _cover[ia] += (xb - xa) / SubRows;
                            continue;
                        }

                        _cover[ia] += (ia + 1 - xa) / SubRows;
                        for (var i = ia + 1; i < ib; i++)
                            _cover[i] += 1f / SubRows;
                        if (ib < Width)
                            _cover[ib] += (xb - ib) / SubRows;
                    }
                }

                if (hi < lo)
                    continue;
                for (var x = lo; x <= hi; x++)
                {
                    var cv = _cover[x];
                    _cover[x] = 0f;
                    if (cv > 0.001f)
                        Over(x, y, shade(x, y), Mathf.Min(1f, cv));
                }

                if (hi + 1 < _cover.Length)
                    _cover[hi + 1] = 0f;
                for (var x = lo; x <= hi + 1 && x < _cover.Length; x++)
                    _cover[x] = 0f;
            }
        }

        public void Fill(Vector2[] poly, Func<int, int, Color> shade) => FillPaths(new[] { poly }, shade);

        public void Fill(Vector2[] poly, Color c) => FillPaths(new[] { poly }, (x, y) => c);

        public void FillCircle(float cx, float cy, float r, Color c) => Fill(CirclePoly(cx, cy, r), c);

        public void FillCircle(float cx, float cy, float r, Func<int, int, Color> shade) => Fill(CirclePoly(cx, cy, r), shade);

        public void Ring(float cx, float cy, float rOuter, float rInner, Func<int, int, Color> shade)
            => FillPaths(new[] { CirclePoly(cx, cy, rOuter), CirclePoly(cx, cy, rInner) }, shade);

        public void Ring(float cx, float cy, float rOuter, float rInner, Color c) => Ring(cx, cy, rOuter, rInner, (x, y) => c);

        /// <summary>Yuvarlak uçlu kalın çizgi (tek çokgen).</summary>
        public void Line(float x0, float y0, float x1, float y1, float thickness, Color c) => Fill(CapsulePoly(x0, y0, x1, y1, thickness * 0.5f), c);

        /// <summary>Düz uçlu çizgi (iz/ışın için).</summary>
        public void Beam(float x0, float y0, float x1, float y1, float w0, float w1, Color c)
        {
            var dx = x1 - x0;
            var dy = y1 - y0;
            var len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 0.001f)
                return;
            var nx = -dy / len;
            var ny = dx / len;
            Fill(new[]
            {
                new Vector2(x0 + nx * w0 * 0.5f, y0 + ny * w0 * 0.5f), new Vector2(x1 + nx * w1 * 0.5f, y1 + ny * w1 * 0.5f),
                new Vector2(x1 - nx * w1 * 0.5f, y1 - ny * w1 * 0.5f), new Vector2(x0 - nx * w0 * 0.5f, y0 - ny * w0 * 0.5f)
            }, c);
        }

        /// <summary>Yarıçap boyunca yumuşak düşen toplamsal ışıma.</summary>
        public void Glow(float cx, float cy, float r, Color c, float intensity, float squashY = 1f)
        {
            var x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r));
            var x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(cx + r));
            var ry = r * squashY;
            var y0 = Mathf.Max(0, Mathf.FloorToInt(cy - ry));
            var y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(cy + ry));
            for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
            {
                var dx = (x + 0.5f - cx) / r;
                var dy = (y + 0.5f - cy) / ry;
                var d2 = dx * dx + dy * dy;
                if (d2 >= 1f)
                    continue;
                var f = 1f - d2;
                Add(x, y, c, f * f * intensity);
            }
        }

        // ------------------------------------------------------------------ Geometri yardımcıları

        public static Vector2[] CirclePoly(float cx, float cy, float r)
        {
            var n = Mathf.Clamp(Mathf.CeilToInt(r * 1.6f), 12, 160);
            var pts = new Vector2[n];
            for (var i = 0; i < n; i++)
            {
                var a = i * Mathf.PI * 2f / n;
                pts[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }

            return pts;
        }

        public static Vector2[] EllipsePoly(float cx, float cy, float rx, float ry, float rotRad = 0f)
        {
            var n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(rx, ry) * 1.6f), 10, 120);
            var pts = new Vector2[n];
            var cr = Mathf.Cos(rotRad);
            var sr = Mathf.Sin(rotRad);
            for (var i = 0; i < n; i++)
            {
                var a = i * Mathf.PI * 2f / n;
                var ex = Mathf.Cos(a) * rx;
                var ey = Mathf.Sin(a) * ry;
                pts[i] = new Vector2(cx + ex * cr - ey * sr, cy + ex * sr + ey * cr);
            }

            return pts;
        }

        public static Vector2[] CapsulePoly(float x0, float y0, float x1, float y1, float r)
        {
            var ang = Mathf.Atan2(y1 - y0, x1 - x0);
            const int cap = 8;
            var pts = new List<Vector2>(cap * 2 + 2);
            for (var i = 0; i <= cap; i++)
            {
                var a = ang - Mathf.PI * 0.5f + Mathf.PI * i / cap;
                pts.Add(new Vector2(x1 + Mathf.Cos(a) * r, y1 + Mathf.Sin(a) * r));
            }

            for (var i = 0; i <= cap; i++)
            {
                var a = ang + Mathf.PI * 0.5f + Mathf.PI * i / cap;
                pts.Add(new Vector2(x0 + Mathf.Cos(a) * r, y0 + Mathf.Sin(a) * r));
            }

            return pts.ToArray();
        }

        public static Vector2[] RoundRectPoly(float x, float y, float w, float h, float r)
        {
            r = Mathf.Min(r, Mathf.Min(w, h) * 0.5f);
            const int seg = 6;
            var pts = new List<Vector2>(seg * 4 + 4);
            void Corner(float cx, float cy, float a0)
            {
                for (var i = 0; i <= seg; i++)
                {
                    var a = a0 + Mathf.PI * 0.5f * i / seg;
                    pts.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
                }
            }

            Corner(x + w - r, y + h - r, 0f);
            Corner(x + r, y + h - r, Mathf.PI * 0.5f);
            Corner(x + r, y + r, Mathf.PI);
            Corner(x + w - r, y + r, Mathf.PI * 1.5f);
            return pts.ToArray();
        }

        /// <summary>5 köşeli yıldız (üst köşe yukarı).</summary>
        public static Vector2[] StarPoly(float cx, float cy, float rOuter, float rotRad = 0f, float innerRatio = 0.4f)
        {
            var pts = new Vector2[10];
            for (var i = 0; i < 10; i++)
            {
                var a = -Mathf.PI * 0.5f + rotRad + i * Mathf.PI / 5f;
                var r = (i & 1) == 0 ? rOuter : rOuter * innerRatio;
                pts[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }

            return pts;
        }

        /// <summary>
        /// Hilal çokgeni: dış daire (R) eksi iç daire (r, merkezi +d kaydırılmış); ağzı sağa (+x) bakar. Kesişmiyorsa dış daire döner.
        /// </summary>
        public static Vector2[] CrescentPoly(float cx, float cy, float outerR, float innerR, float offset)
        {
            if (offset <= 0.0001f)
                return CirclePoly(cx, cy, outerR);
            var x0 = (offset * offset + outerR * outerR - innerR * innerR) / (2f * offset);
            var yy = outerR * outerR - x0 * x0;
            if (yy <= 0f)
                return CirclePoly(cx, cy, outerR);
            var y0 = Mathf.Sqrt(yy);
            var theta = Mathf.Atan2(y0, x0);
            var phi = Mathf.Atan2(y0, x0 - offset);
            var n = Mathf.Clamp(Mathf.CeilToInt(outerR * 1.2f), 16, 120);
            var pts = new List<Vector2>(n * 2 + 2);
            // Dış yay: üstteki kesişimden alta, sol taraftan geçerek (y aşağı ekranda yön önemsiz).
            var a0 = theta;
            var a1 = Mathf.PI * 2f - theta;
            for (var i = 0; i <= n; i++)
            {
                var a = Mathf.Lerp(a0, a1, i / (float)n);
                pts.Add(new Vector2(cx + Mathf.Cos(a) * outerR, cy - Mathf.Sin(a) * outerR));
            }

            // İç yay: alt kesişimden üste, iç dairenin sol tarafından geçerek.
            var b0 = Mathf.PI * 2f - phi;
            var b1 = phi;
            for (var i = 0; i <= n; i++)
            {
                var a = Mathf.Lerp(b0, b1, i / (float)n);
                pts.Add(new Vector2(cx + offset + Mathf.Cos(a) * innerR, cy - Mathf.Sin(a) * innerR));
            }

            return pts.ToArray();
        }

        public static Vector2[] Mirror(IList<Vector2> rightHalf, float axisX)
        {
            var pts = new List<Vector2>(rightHalf.Count * 2);
            for (var i = 0; i < rightHalf.Count; i++)
                pts.Add(rightHalf[i]);
            for (var i = rightHalf.Count - 1; i >= 0; i--)
                pts.Add(new Vector2(2f * axisX - rightHalf[i].x, rightHalf[i].y));
            return pts.ToArray();
        }

        // ------------------------------------------------------------------ Çıkış

        public Color32[] ToColor32(bool flipY = true)
        {
            var o = new Color32[Pixels.Length];
            for (var y = 0; y < Height; y++)
            {
                var sy = flipY ? Height - 1 - y : y;
                for (var x = 0; x < Width; x++)
                {
                    var c = Pixels[sy * Width + x];
                    o[y * Width + x] = new Color32(B(c.r), B(c.g), B(c.b), B(c.a));
                }
            }

            return o;
        }

        private static byte B(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        public Texture2D ToTexture(string name, bool mipmaps = true)
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, mipmaps, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 2
            };
            tex.SetPixels32(ToColor32());
            tex.Apply(mipmaps, false);
            return tex;
        }
    }

    /// <summary>Rütbe işareti türü (vektör apolet).</summary>
    public enum RankMark
    {
        Star,
        SmallStar,
        Chevron,
        Bar,
        Wreath
    }

    /// <summary>Rütbe işareti metali/ipliği.</summary>
    public enum RankMetal
    {
        Gold,
        Silver,
        Red,
        Muted
    }

    /// <summary>Tek apolet sembolü.</summary>
    public struct RankSymbol : IEquatable<RankSymbol>
    {
        public RankMark Mark;
        public RankMetal Metal;

        public RankSymbol(RankMark mark, RankMetal metal)
        {
            Mark = mark;
            Metal = metal;
        }

        public bool Equals(RankSymbol other) => Mark == other.Mark && Metal == other.Metal;
        public override bool Equals(object obj) => obj is RankSymbol o && Equals(o);
        public override int GetHashCode() => ((int)Mark << 4) ^ (int)Metal;
    }

    /// <summary>
    /// HAREKÂT amblemi (stilize kartal siluetinin göğsünde hilal-yıldız; kırmızı/antrasit/beyaz, 512 px) ve rütbe apoletleri
    /// (MilitaryRank başına vektör şevron/yıldız/çubuk/palamut, metalik gradyan, 128 px yükseklik). Özgün stilizasyondur; hiçbir resmî TSK
    /// arması/işareti kopyalanmaz. Çekirdek çizim (<see cref="RenderEmblem"/>, <see cref="RenderApolet"/>) saf ve deterministiktir;
    /// doku sarmalayıcıları bellekte önbelleklenir.
    /// </summary>
    public static class EmblemArt
    {
        public const int EmblemSize = 512;
        public const int ApoletHeight = 128;
        public const float ApoletAspect = 2.6f;

        public static readonly Color Red = new Color(0.831f, 0.227f, 0.180f, 1f);          // #D43A2E
        public static readonly Color RedDark = new Color(0.45f, 0.09f, 0.08f, 1f);
        public static readonly Color RedLight = new Color(1f, 0.42f, 0.34f, 1f);
        public static readonly Color Charcoal = new Color(0.075f, 0.078f, 0.086f, 1f);
        public static readonly Color CharcoalLift = new Color(0.14f, 0.145f, 0.16f, 1f);
        public static readonly Color Bone = new Color(0.94f, 0.93f, 0.9f, 1f);

        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

        // ================================================================== Genel API

        /// <summary>Amblem dokusu (şeffaf zemin, dairesel). Başlık köşesi + yükleme için.</summary>
        public static Texture2D GetEmblem(int size = EmblemSize)
        {
            size = Mathf.Clamp(size, 64, 1024);
            var key = "emblem_" + size;
            if (Cache.TryGetValue(key, out var t) && t != null)
                return t;
            t = RenderEmblem(size).ToTexture("HK_Emblem_" + size);
            Cache[key] = t;
            return t;
        }

        public static Sprite GetEmblemSprite(int size = EmblemSize)
        {
            var tex = GetEmblem(size);
            var key = "emblem_" + tex.width;
            if (SpriteCache.TryGetValue(key, out var s) && s != null)
                return s;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            s.name = "HK_EmblemSprite";
            SpriteCache[key] = s;
            return s;
        }

        /// <summary>Rütbe apolet dokusu (şeffaf zemin).</summary>
        public static Texture2D GetApolet(MilitaryRank rank, int height = ApoletHeight)
        {
            height = Mathf.Clamp(height, 32, 512);
            var key = "apolet_" + (int)rank + "_" + height;
            if (Cache.TryGetValue(key, out var t) && t != null)
                return t;
            t = RenderApolet(rank, height).ToTexture("HK_Apolet_" + rank);
            Cache[key] = t;
            return t;
        }

        public static Sprite GetApoletSprite(MilitaryRank rank, int height = ApoletHeight)
        {
            var tex = GetApolet(rank, height);
            var key = "apolet_" + (int)rank + "_" + tex.height;
            if (SpriteCache.TryGetValue(key, out var s) && s != null)
                return s;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            s.name = "HK_ApoletSprite_" + rank;
            SpriteCache[key] = s;
            return s;
        }

        public static void ReleaseMemory()
        {
            foreach (var kv in SpriteCache)
                if (kv.Value != null)
                    UnityEngine.Object.Destroy(kv.Value);
            SpriteCache.Clear();
            foreach (var kv in Cache)
                if (kv.Value != null)
                    UnityEngine.Object.Destroy(kv.Value);
            Cache.Clear();
        }

        // ================================================================== Amblem

        /// <summary>Amblemin saf çizimi: kare, şeffaf zemin, ortada dairesel rozet.</summary>
        public static ArtSurface RenderEmblem(int size = EmblemSize)
        {
            var s = new ArtSurface(size, size);
            var half = size * 0.5f;
            var cx = half;
            var cy = half;

            // Rozet zemini: antrasit radyal + kırmızı çerçeve halkaları.
            var rDisc = half * 0.985f;
            s.FillCircle(cx, cy, rDisc, (x, y) =>
            {
                var d = Mathf.Clamp01(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / rDisc);
                return Color.Lerp(CharcoalLift, Charcoal, Mathf.SmoothStep(0f, 1f, d));
            });
            // Kırmızı ışıma (kartalın arkası).
            s.Glow(cx, cy - half * 0.05f, half * 0.78f, Red, 0.30f);

            s.Ring(cx, cy, rDisc, rDisc * 0.935f, (x, y) =>
            {
                var t = Mathf.Clamp01((y - (cy - rDisc)) / (rDisc * 2f));
                return Color.Lerp(RedLight, RedDark, Mathf.SmoothStep(0f, 1f, t));
            });
            s.Ring(cx, cy, rDisc * 0.905f, rDisc * 0.893f, new Color(Bone.r, Bone.g, Bone.b, 0.55f));

            // Kartal: birim koordinat (x sağa, y yukarı) → piksel.
            var sc = half * 0.86f;
            var ox = cx;
            var oy = cy - 0.155f * sc;
            Vector2 P(float x, float y) => new Vector2(ox + x * sc, oy - y * sc);
            Vector2[] Pts(IList<Vector2> u)
            {
                var o = new Vector2[u.Count];
                for (var i = 0; i < u.Count; i++)
                    o[i] = P(u[i].x, u[i].y);
                return o;
            }

            // Gölge
            Func<int, int, Color> shadow = (x, y) => new Color(0f, 0f, 0f, 0.45f);
            var wing = WingPoly();
            var wingL = new List<Vector2>(wing.Count);
            for (var i = 0; i < wing.Count; i++)
                wingL.Add(new Vector2(-wing[i].x, wing[i].y));
            var tail = TailPoly();
            var body = BodyPoly();
            var head = HeadPoly();

            var off = size * 0.012f;
            foreach (var poly in new[] { Pts(wing), Pts(wingL), Pts(tail), Pts(body), Pts(head) })
            {
                var sh = new Vector2[poly.Length];
                for (var i = 0; i < poly.Length; i++)
                    sh[i] = poly[i] + new Vector2(off * 0.4f, off);
                s.Fill(sh, shadow);
            }

            // Kartal gövdesi: yukarıdan aşağıya kemik beyazı → soğuk gri; sol/sağ hafif kenar kararması.
            var yTop = P(0, 0.56f).y;
            var yBot = P(0, -0.84f).y;
            Func<int, int, Color> plumage = (x, y) =>
            {
                var t = Mathf.Clamp01((y - yTop) / (yBot - yTop));
                var c = Color.Lerp(Bone, new Color(0.62f, 0.64f, 0.68f, 1f), t * 0.9f);
                var edge = Mathf.Clamp01(Mathf.Abs(x - ox) / (sc * 0.98f));
                return Color.Lerp(c, new Color(c.r * 0.8f, c.g * 0.8f, c.b * 0.82f, 1f), edge * edge * 0.6f);
            };

            s.Fill(Pts(tail), (x, y) => Color.Lerp(plumage(x, y), Charcoal, 0.18f));
            s.Fill(Pts(wing), plumage);
            s.Fill(Pts(wingL), plumage);
            s.Fill(Pts(body), plumage);
            s.Fill(Pts(head), plumage);

            // Tüy ayrımları (koyu ince çizgiler).
            var featherLine = new Color(0.08f, 0.09f, 0.1f, 0.55f);
            var pivot = new Vector2(0.14f, 0.12f);
            var tips = WingFeatherTips();
            for (var side = -1; side <= 1; side += 2)
            for (var i = 0; i < tips.Count; i++)
            {
                var tip = tips[i];
                var a = new Vector2(pivot.x + (tip.x - pivot.x) * 0.38f, pivot.y + (tip.y - pivot.y) * 0.38f);
                var pa = P(side * a.x, a.y);
                var pb = P(side * (pivot.x + (tip.x - pivot.x) * 0.93f), pivot.y + (tip.y - pivot.y) * 0.93f);
                s.Line(pa.x, pa.y, pb.x, pb.y, Mathf.Max(1.4f, size * 0.0042f), featherLine);
            }

            // Kuyruk tüy çizgileri
            for (var i = -2; i <= 2; i++)
            {
                var a = P(i * 0.028f, -0.34f);
                var b = P(i * 0.062f, -0.74f);
                s.Line(a.x, a.y, b.x, b.y, Mathf.Max(1.2f, size * 0.0034f), new Color(0.08f, 0.09f, 0.1f, 0.45f));
            }

            // Göz + gaga ucu vurgusu
            var eye = P(-0.058f, 0.465f);
            s.FillCircle(eye.x, eye.y, Mathf.Max(2f, sc * 0.021f), Red);
            s.FillCircle(eye.x, eye.y, Mathf.Max(1f, sc * 0.009f), Charcoal);

            // Göğüs kalkanı: antrasit + kırmızı kenarlık; içinde beyaz hilal ve kırmızı yıldız.
            var shield = ShieldPoly();
            var shieldPx = Pts(shield);
            var shieldIn = new List<Vector2>(shield.Count);
            for (var i = 0; i < shield.Count; i++)
                shieldIn.Add(new Vector2(shield[i].x * 0.86f, 0.0f + (shield[i].y - 0.0f) * 0.9f + 0.002f));
            s.Fill(shieldPx, (x, y) => Red);
            s.Fill(Pts(shieldIn), (x, y) =>
            {
                var t = Mathf.Clamp01((y - P(0, 0.28f).y) / (P(0, -0.30f).y - P(0, 0.28f).y));
                return Color.Lerp(CharcoalLift, Charcoal, t);
            });

            var cc = P(-0.018f, 0.0f);
            var crescent = ArtSurface.CrescentPoly(cc.x, cc.y, sc * 0.125f, sc * 0.098f, sc * 0.048f);
            s.Fill(crescent, (x, y) =>
            {
                var t = Mathf.Clamp01((y - (cc.y - sc * 0.125f)) / (sc * 0.25f));
                return Color.Lerp(Color.white, new Color(0.78f, 0.8f, 0.84f, 1f), t);
            });
            var sp = P(0.058f, 0.0f);
            s.Fill(ArtSurface.StarPoly(sp.x, sp.y, sc * 0.052f, 0.02f), (x, y) => Color.Lerp(RedLight, Red, Mathf.Clamp01((y - (sp.y - sc * 0.05f)) / (sc * 0.1f))));

            // Alt kırmızı çentik (rozetin altı) — hafif parlak şerit.
            var gloss = new Vector2[]
            {
                new Vector2(cx - rDisc * 0.62f, cy - rDisc * 0.66f), new Vector2(cx + rDisc * 0.62f, cy - rDisc * 0.66f),
                new Vector2(cx + rDisc * 0.40f, cy - rDisc * 0.86f), new Vector2(cx - rDisc * 0.40f, cy - rDisc * 0.86f)
            };
            s.Glow(cx, cy - half * 0.72f, half * 0.55f, Bone, 0.05f, 0.35f);
            _ = gloss;
            return s;
        }

        // ---- Kartal geometrisi (birim uzay; sağ kanat/yarı) ----

        private static List<Vector2> WingFeatherTips()
        {
            var pivot = new Vector2(0.14f, 0.12f);
            var tips = new List<Vector2>();
            for (var i = 0; i < 6; i++)
            {
                var ang = (20f - i * 17f) * Mathf.Deg2Rad;
                var len = 0.86f - i * 0.055f;
                tips.Add(new Vector2(pivot.x + Mathf.Cos(ang) * len, pivot.y + Mathf.Sin(ang) * len));
            }

            return tips;
        }

        private static List<Vector2> WingPoly()
        {
            var pivot = new Vector2(0.14f, 0.12f);
            var tips = WingFeatherTips();
            var pts = new List<Vector2>
            {
                new Vector2(0.12f, 0.30f), new Vector2(0.28f, 0.40f), new Vector2(0.50f, 0.49f), new Vector2(0.74f, 0.50f), new Vector2(0.90f, 0.46f)
            };
            for (var i = 0; i < tips.Count; i++)
            {
                pts.Add(tips[i]);
                if (i + 1 < tips.Count)
                {
                    var m = (tips[i] + tips[i + 1]) * 0.5f;
                    var dir = (m - pivot).normalized;
                    var dist = (m - pivot).magnitude * 0.82f;
                    pts.Add(pivot + dir * dist);
                }
            }

            pts.Add(new Vector2(0.20f, -0.20f));
            pts.Add(new Vector2(0.12f, -0.12f));
            return pts;
        }

        private static List<Vector2> TailPoly()
        {
            var r = new List<Vector2>
            {
                new Vector2(0f, -0.84f), new Vector2(0.06f, -0.73f), new Vector2(0.11f, -0.81f), new Vector2(0.155f, -0.70f),
                new Vector2(0.215f, -0.76f), new Vector2(0.16f, -0.26f), new Vector2(0f, -0.26f)
            };
            return new List<Vector2>(ArtSurface.Mirror(r.GetRange(0, r.Count - 1), 0f));
        }

        private static List<Vector2> BodyPoly()
        {
            var r = new List<Vector2>
            {
                new Vector2(0.09f, 0.36f), new Vector2(0.17f, 0.30f), new Vector2(0.21f, 0.06f), new Vector2(0.17f, -0.20f), new Vector2(0.09f, -0.36f), new Vector2(0f, -0.40f)
            };
            return new List<Vector2>(ArtSurface.Mirror(r, 0f));
        }

        private static List<Vector2> HeadPoly()
        {
            return new List<Vector2>
            {
                new Vector2(-0.085f, 0.30f), new Vector2(0.085f, 0.30f), new Vector2(0.095f, 0.42f), new Vector2(0.035f, 0.525f), new Vector2(-0.05f, 0.535f),
                new Vector2(-0.115f, 0.495f), new Vector2(-0.225f, 0.425f), new Vector2(-0.178f, 0.388f), new Vector2(-0.115f, 0.41f), new Vector2(-0.105f, 0.36f)
            };
        }

        private static List<Vector2> ShieldPoly()
        {
            var r = new List<Vector2> { new Vector2(0f, 0.30f), new Vector2(0.17f, 0.30f), new Vector2(0.17f, 0.04f) };
            for (var i = 1; i <= 8; i++)
            {
                var t = i / 8f;
                // 0.17,0.04 -> (0.10,-0.18) -> (0,-0.30)
                var a = Vector2.Lerp(new Vector2(0.17f, 0.04f), new Vector2(0.11f, -0.20f), t);
                var b = Vector2.Lerp(new Vector2(0.11f, -0.20f), new Vector2(0f, -0.31f), t);
                r.Add(Vector2.Lerp(a, b, t));
            }

            return new List<Vector2>(ArtSurface.Mirror(r.GetRange(0, r.Count - 1), 0f));
        }

        // ================================================================== Rütbe apoleti

        /// <summary>Rütbe → apolet sembolleri (soldan sağa). Saf; testlenebilir.</summary>
        public static RankSymbol[] GetRankSymbols(MilitaryRank rank)
        {
            var list = new List<RankSymbol>();
            var value = (int)rank;
            switch (RankCatalog.GetCategoryKind(rank))
            {
                case RankCategory.Officer:
                    switch (rank)
                    {
                        case MilitaryRank.Astegmen:
                            list.Add(new RankSymbol(RankMark.SmallStar, RankMetal.Silver));
                            break;
                        case MilitaryRank.Tegmen:
                            list.Add(new RankSymbol(RankMark.Star, RankMetal.Gold));
                            break;
                        case MilitaryRank.Ustegmen:
                            for (var i = 0; i < 2; i++)
                                list.Add(new RankSymbol(RankMark.Star, RankMetal.Gold));
                            break;
                        case MilitaryRank.Yuzbasi:
                            for (var i = 0; i < 3; i++)
                                list.Add(new RankSymbol(RankMark.Star, RankMetal.Gold));
                            break;
                        default:
                        {
                            var stars = Mathf.Clamp(value - (int)MilitaryRank.Yuzbasi, 1, 3);
                            list.Add(new RankSymbol(RankMark.Wreath, RankMetal.Gold));
                            for (var i = 0; i < stars; i++)
                                list.Add(new RankSymbol(RankMark.Star, RankMetal.Gold));
                            break;
                        }
                    }

                    break;
                case RankCategory.NonCommissioned:
                {
                    var tier = value - (int)MilitaryRank.AstsubayCavus;
                    if (tier >= 3)
                        list.Add(new RankSymbol(RankMark.Star, RankMetal.Gold));
                    for (var i = 0; i < tier % 3 + 1; i++)
                        list.Add(new RankSymbol(RankMark.Bar, RankMetal.Gold));
                    break;
                }
                case RankCategory.Specialist:
                {
                    var chevrons = rank == MilitaryRank.UzmanCavus ? 2 : 1;
                    for (var i = 0; i < chevrons; i++)
                        list.Add(new RankSymbol(RankMark.Chevron, RankMetal.Gold));
                    list.Add(new RankSymbol(RankMark.Bar, RankMetal.Gold));
                    break;
                }
                default:
                    switch (rank)
                    {
                        case MilitaryRank.Onbasi:
                            list.Add(new RankSymbol(RankMark.Chevron, RankMetal.Red));
                            break;
                        case MilitaryRank.Cavus:
                            list.Add(new RankSymbol(RankMark.Chevron, RankMetal.Red));
                            list.Add(new RankSymbol(RankMark.Chevron, RankMetal.Red));
                            break;
                        case MilitaryRank.SozlesmeliEr:
                            list.Add(new RankSymbol(RankMark.Bar, RankMetal.Red));
                            break;
                        default:
                            list.Add(new RankSymbol(RankMark.Bar, RankMetal.Muted));
                            break;
                    }

                    break;
            }

            return list.ToArray();
        }

        public static int ApoletWidth(int height) => Mathf.RoundToInt(height * ApoletAspect);

        private static Color MetalBase(RankMetal m)
        {
            switch (m)
            {
                case RankMetal.Gold: return new Color(0.89f, 0.70f, 0.25f, 1f);
                case RankMetal.Silver: return new Color(0.78f, 0.81f, 0.84f, 1f);
                case RankMetal.Red: return new Color(0.80f, 0.14f, 0.15f, 1f);
                default: return new Color(0.46f, 0.49f, 0.46f, 1f);
            }
        }

        /// <summary>Metalik gradyan: üst parlak → taban → alt koyu, çapraz parlama şeridi.</summary>
        private static Func<int, int, Color> Metal(RankMetal metal, float yTop, float yBot, float xMid)
        {
            var b = MetalBase(metal);
            var hi = Color.Lerp(b, Color.white, metal == RankMetal.Red ? 0.38f : 0.62f);
            var dk = new Color(b.r * 0.46f, b.g * 0.46f, b.b * 0.46f, 1f);
            return (x, y) =>
            {
                var t = Mathf.Clamp01((y - yTop) / Mathf.Max(1f, yBot - yTop));
                var c = t < 0.4f ? Color.Lerp(hi, b, t / 0.4f) : Color.Lerp(b, dk, (t - 0.4f) / 0.6f);
                var band = Mathf.Abs((x - xMid) * 0.35f + (y - (yTop + yBot) * 0.5f) * 0.25f);
                var sheen = Mathf.Clamp01(1f - band / 6f) * 0.22f;
                return Color.Lerp(c, Color.white, sheen);
            };
        }

        private static float MarkWidth(RankMark m, float h)
        {
            switch (m)
            {
                case RankMark.Star: return h * 0.40f;
                case RankMark.SmallStar: return h * 0.30f;
                case RankMark.Chevron: return h * 0.42f;
                case RankMark.Bar: return h * 0.085f;
                default: return h * 0.15f;
            }
        }

        /// <summary>Apoletin saf çizimi (şeffaf zemin; genişlik = yükseklik × 2,6).</summary>
        public static ArtSurface RenderApolet(MilitaryRank rank, int height = ApoletHeight)
        {
            height = Mathf.Max(32, height);
            var w = ApoletWidth(height);
            var s = new ArtSurface(w, height);
            float W = w, H = height;
            var kind = RankCatalog.GetCategoryKind(rank);
            var metalEdge = kind == RankCategory.Enlisted ? RankMetal.Muted : RankMetal.Gold;

            Vector2[] Board(float m)
            {
                var tipX = W - m;
                return new[]
                {
                    new Vector2(m, m + H * 0.05f), new Vector2(W * 0.82f, m), new Vector2(tipX, H * 0.5f - H * 0.08f),
                    new Vector2(tipX, H * 0.5f + H * 0.08f), new Vector2(W * 0.82f, H - m), new Vector2(m, H - m - H * 0.05f)
                };
            }

            var mOuter = H * 0.03f;
            var mPipe = H * 0.075f;
            var mInner = H * 0.095f;

            // Gölge
            var sh = Board(mOuter);
            for (var i = 0; i < sh.Length; i++)
                sh[i] += new Vector2(0f, H * 0.012f);
            s.Fill(sh, new Color(0f, 0f, 0f, 0.4f));

            // Metal pervaz (iki kontur = halka).
            s.FillPaths(new[] { Board(mOuter), Board(mPipe) }, Metal(metalEdge, 0f, H, W * 0.4f));
            // Kumaş: zeytin-antrasit gradyan + çapraz dokuma çizgileri.
            var fabricTop = new Color(0.25f, 0.29f, 0.19f, 1f);
            var fabricBot = new Color(0.11f, 0.135f, 0.095f, 1f);
            s.Fill(Board(mPipe), (x, y) =>
            {
                var t = Mathf.Clamp01(y / H);
                var c = Color.Lerp(fabricTop, fabricBot, t);
                var weave = ((x + y) & 3) == 0 ? 0.045f : 0f;
                var vig = Mathf.Clamp01(1f - Mathf.Abs(x / W - 0.45f) * 1.2f);
                var k = 0.82f + vig * 0.22f;
                return new Color(Mathf.Clamp01(c.r * k + weave), Mathf.Clamp01(c.g * k + weave), Mathf.Clamp01(c.b * k + weave), 1f);
            });
            // İç ince çizgi
            s.FillPaths(new[] { Board(mInner), Board(mInner + Mathf.Max(1f, H * 0.012f)) }, (x, y) => new Color(0f, 0f, 0f, 0.35f));

            // Düğme (sol).
            var bx = W * 0.115f;
            var by = H * 0.5f;
            var br = H * 0.115f;
            s.FillCircle(bx + 0f, by + H * 0.012f, br + 1f, new Color(0f, 0f, 0f, 0.5f));
            s.FillCircle(bx, by, br, Metal(metalEdge, by - br, by + br, bx));
            s.Ring(bx, by, br * 0.62f, br * 0.5f, new Color(0f, 0f, 0f, 0.35f));
            s.FillCircle(bx - br * 0.25f, by - br * 0.3f, br * 0.2f, new Color(1f, 1f, 1f, 0.5f));

            // Semboller: [0.24W, 0.90W] arasında ortalı.
            var symbols = GetRankSymbols(rank);
            var gap = H * 0.07f;
            var total = 0f;
            for (var i = 0; i < symbols.Length; i++)
                total += MarkWidth(symbols[i].Mark, H) + (i > 0 ? gap * (symbols[i].Mark == RankMark.Bar ? 1.4f : 1f) : 0f);
            var x0 = W * 0.57f - total * 0.5f;
            var yMid = H * 0.5f;
            for (var i = 0; i < symbols.Length; i++)
            {
                var sym = symbols[i];
                var mw = MarkWidth(sym.Mark, H);
                if (i > 0)
                    x0 += gap * (sym.Mark == RankMark.Bar ? 1.4f : 1f);
                DrawMark(s, sym, x0, mw, yMid, H);
                x0 += mw;
            }

            return s;
        }

        private static void DrawMark(ArtSurface s, RankSymbol sym, float x0, float mw, float yMid, float H)
        {
            var cx = x0 + mw * 0.5f;
            var shadowOff = new Vector2(H * 0.012f, H * 0.02f);
            var shadow = new Color(0f, 0f, 0f, 0.55f);

            void Shadowed(Vector2[] poly, float yTop, float yBot)
            {
                var sh = new Vector2[poly.Length];
                for (var i = 0; i < poly.Length; i++)
                    sh[i] = poly[i] + shadowOff;
                s.Fill(sh, shadow);
                s.Fill(poly, Metal(sym.Metal, yTop, yBot, cx));
            }

            switch (sym.Mark)
            {
                case RankMark.Star:
                case RankMark.SmallStar:
                {
                    var r = sym.Mark == RankMark.Star ? H * 0.205f : H * 0.15f;
                    var poly = ArtSurface.StarPoly(cx, yMid + r * 0.06f, r, 0f, 0.42f);
                    Shadowed(poly, yMid - r, yMid + r);
                    break;
                }
                case RankMark.Chevron:
                {
                    var bh = H * 0.34f;
                    var t = H * 0.12f;
                    var hw = mw * 0.5f;
                    var poly = new[]
                    {
                        new Vector2(cx - hw, yMid + bh * 0.5f), new Vector2(cx, yMid - bh * 0.5f), new Vector2(cx + hw, yMid + bh * 0.5f),
                        new Vector2(cx + hw, yMid + bh * 0.5f - t), new Vector2(cx, yMid - bh * 0.5f + t * 1.15f), new Vector2(cx - hw, yMid + bh * 0.5f - t)
                    };
                    Shadowed(poly, yMid - bh * 0.5f, yMid + bh * 0.5f);
                    break;
                }
                case RankMark.Bar:
                {
                    var bh = H * 0.56f;
                    var poly = ArtSurface.RoundRectPoly(x0, yMid - bh * 0.5f, mw, bh, mw * 0.4f);
                    Shadowed(poly, yMid - bh * 0.5f, yMid + bh * 0.5f);
                    break;
                }
                default:
                {
                    // Palamut: dikey sap + iki yana 4 çift meşe yaprağı.
                    var bh = H * 0.58f;
                    var top = yMid - bh * 0.5f;
                    var stem = ArtSurface.RoundRectPoly(cx - mw * 0.08f, top, mw * 0.16f, bh, mw * 0.08f);
                    Shadowed(stem, top, top + bh);
                    for (var i = 0; i < 4; i++)
                    {
                        var ly = top + bh * (0.14f + i * 0.24f);
                        for (var side = -1; side <= 1; side += 2)
                        {
                            var leaf = ArtSurface.EllipsePoly(cx + side * mw * 0.3f, ly, mw * 0.34f, mw * 0.15f, side * -0.5f);
                            Shadowed(leaf, ly - mw * 0.2f, ly + mw * 0.2f);
                        }
                    }

                    break;
                }
            }
        }
    }
}
