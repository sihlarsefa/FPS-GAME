using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>GX8: dünya çıkartma (decal) türleri. Hepsi kodla üretilir (DecalLibraryMath), dosya/varlık gerektirmez.</summary>
    public enum DecalKind
    {
        Puddle = 0,     // su birikintisi (yüksek pürüzsüzlük)
        MudSplat = 1,   // çamur sıçraması
        TireTrack = 2,  // çift şeritli lastik izi (v ekseni yol yönü)
        Leaves = 3,     // dökülmüş yapraklar
        Needles = 4,    // çam iğneleri
        OilStain = 5,   // yağ lekesi
        WallGrime = 6,  // duvar dibi kir/yosun (v=0 alt)
        SootStreak = 7, // is / pencere altı akıntı (v=1 üst)
        Moss = 8,       // yosun yaması
        Crack = 9,      // beton/asfalt çatlağı
        Poster = 10     // yıpranmış afiş (grafiti/slogan yok; soyut düzen)
    }

    /// <summary>Tek texel örneği (0..1). Height: normal haritası için işaretli yükseklik (-1 çukur … +1 kabarık).</summary>
    public struct DecalPixel
    {
        public float R, G, B, A, Height, Smooth;
    }

    /// <summary>
    /// GX8: çıkartma dokularının saf matematiği (Unity doku nesnesi yok → EditMode testli). Her tür için
    /// <see cref="Sample"/> (u,v ∈ [0,1]) renk + alfa + yükseklik + pürüzsüzlük döner; <see cref="BuildMaps"/> hepsini
    /// albedo(RGBA) / normal / maske (R metalik, G AO, B 255, A pürüzsüzlük) dizilerine doldurur. Aynı tohum → aynı doku.
    /// </summary>
    public static class DecalLibraryMath
    {
        public const int KindCount = 11;
        public const int VariantCount = 3;

        /// <summary>Tür başına doku kenarı (px). Büyük, ayrıntılı türler 256; küçükler 128.</summary>
        public static int TextureSize(DecalKind kind)
        {
            switch (kind)
            {
                case DecalKind.Puddle:
                case DecalKind.WallGrime:
                case DecalKind.SootStreak:
                case DecalKind.Poster:
                case DecalKind.TireTrack:
                    return 256;
                default:
                    return 128;
            }
        }

        /// <summary>Türün normal haritası var mı (yalnız yüzey kabartması anlamlı olanlar).</summary>
        public static bool HasNormal(DecalKind kind)
        {
            return kind == DecalKind.TireTrack || kind == DecalKind.Crack || kind == DecalKind.MudSplat
                   || kind == DecalKind.Moss || kind == DecalKind.Leaves;
        }

        /// <summary>Normal kabartma gücü (yükseklik farkı çarpanı).</summary>
        public static float NormalStrength(DecalKind kind)
        {
            switch (kind)
            {
                case DecalKind.Crack: return 5f;
                case DecalKind.TireTrack: return 4f;
                case DecalKind.Moss: return 2.5f;
                case DecalKind.Leaves: return 2f;
                default: return 2.5f;
            }
        }

        // ------------------------------------------------------------------------------------------ gürültü

        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                var h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(seed * 83492791);
                h ^= h >> 13;
                h *= 1274126177u;
                h ^= h >> 16;
                h *= 2246822519u;
                h ^= h >> 13;
                return (h & 0xFFFFFFu) / 16777215f;
            }
        }

        public static float Smoothstep(float e0, float e1, float x)
        {
            if (e1 == e0)
                return x < e0 ? 0f : 1f;
            var t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Düzgün değer gürültüsü, 0..1.</summary>
        public static float Value(float x, float y, int seed)
        {
            var ix = Mathf.FloorToInt(x);
            var iy = Mathf.FloorToInt(y);
            var fx = x - ix;
            var fy = y - iy;
            var sx = fx * fx * (3f - 2f * fx);
            var sy = fy * fy * (3f - 2f * fy);
            var a = Hash(ix, iy, seed);
            var b = Hash(ix + 1, iy, seed);
            var c = Hash(ix, iy + 1, seed);
            var d = Hash(ix + 1, iy + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
        }

        /// <summary>Fraktal gürültü, 0..1 (normalize edilmiş).</summary>
        public static float Fbm(float x, float y, int seed, int octaves)
        {
            var sum = 0f;
            var amp = 0.5f;
            var norm = 0f;
            for (var i = 0; i < octaves; i++)
            {
                sum += amp * Value(x, y, seed + i * 31);
                norm += amp;
                x = x * 2.03f + 17.1f;
                y = y * 2.03f + 9.7f;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        private static float BorderFade(float u, float v, float w)
        {
            return Smoothstep(0f, w, Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)));
        }

        private static float RadialFade(float rad, float start)
        {
            return 1f - Smoothstep(start, 1f, rad);
        }

        /// <summary>
        /// Karelaj üzerinde döndürülmüş elips (yaprak/iğne/damla) alanı. Dönen: kapsama 0..1 (merkezde 1), id: hücre rastgelesi,
        /// local: elips küçük ekseni boyunca konum (-1..1; orta damar için).
        /// </summary>
        private static float Blobs(float u, float v, int cells, int seed, float prob, float rMin, float rMax, float aspect, out float id, out float local)
        {
            var fx = u * cells;
            var fy = v * cells;
            var cx = Mathf.FloorToInt(fx);
            var cy = Mathf.FloorToInt(fy);
            var best = 0f;
            id = 0f;
            local = 0f;
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var gx = cx + dx;
                    var gy = cy + dy;
                    if (Hash(gx, gy, seed) > prob)
                        continue;
                    var jx = gx + 0.2f + 0.6f * Hash(gx, gy, seed + 11);
                    var jy = gy + 0.2f + 0.6f * Hash(gx, gy, seed + 17);
                    var r = Mathf.Lerp(rMin, rMax, Hash(gx, gy, seed + 23));
                    var ang = Hash(gx, gy, seed + 37) * Mathf.PI;
                    var px = fx - jx;
                    var py = fy - jy;
                    var c = Mathf.Cos(ang);
                    var s = Mathf.Sin(ang);
                    var lx = (px * c + py * s) / (r * aspect);
                    var ly = (-px * s + py * c) / r;
                    var d = lx * lx + ly * ly;
                    if (d >= 1f)
                        continue;
                    var cov = 1f - d;
                    if (cov > best)
                    {
                        best = cov;
                        id = Hash(gx, gy, seed + 51);
                        local = ly;
                    }
                }
            }
            return best;
        }

        // ------------------------------------------------------------------------------------------ türler

        /// <summary>u,v ∈ [0,1] için tür örneği. Dış kenarlarda alfa 0'a iner (dikiş görünmez).</summary>
        public static DecalPixel Sample(DecalKind kind, float u, float v, int seed)
        {
            switch (kind)
            {
                case DecalKind.Puddle: return Puddle(u, v, seed);
                case DecalKind.MudSplat: return MudSplat(u, v, seed);
                case DecalKind.TireTrack: return TireTrack(u, v, seed);
                case DecalKind.Leaves: return Leaves(u, v, seed, false);
                case DecalKind.Needles: return Leaves(u, v, seed, true);
                case DecalKind.OilStain: return OilStain(u, v, seed);
                case DecalKind.WallGrime: return WallGrime(u, v, seed);
                case DecalKind.SootStreak: return SootStreak(u, v, seed);
                case DecalKind.Moss: return Moss(u, v, seed);
                case DecalKind.Crack: return Crack(u, v, seed);
                case DecalKind.Poster: return Poster(u, v, seed);
                default: return default;
            }
        }

        private static DecalPixel Puddle(float u, float v, int seed)
        {
            var x = u * 2f - 1f;
            var y = v * 2f - 1f;
            var rad = Mathf.Sqrt(x * x + y * y);
            var ang = Mathf.Atan2(y, x);
            var n = Fbm(Mathf.Cos(ang) * 1.4f + 5f, Mathf.Sin(ang) * 1.4f + 5f, seed, 3);
            var edgeR = 0.58f + 0.28f * n;
            var body = 1f - Smoothstep(edgeR - 0.12f, edgeR, rad);
            var rim = 1f - Smoothstep(edgeR, edgeR + 0.1f, rad);
            var fade = RadialFade(rad, 0.9f);
            var wet = Mathf.Max(0f, rim - body);
            var p = new DecalPixel();
            p.R = Mathf.Lerp(0.12f, 0.05f, body);
            p.G = Mathf.Lerp(0.09f, 0.06f, body);
            p.B = Mathf.Lerp(0.06f, 0.06f, body);
            p.A = (body * 0.88f + wet * 0.35f) * fade;
            p.Smooth = Mathf.Clamp01(body * 0.96f + wet * 0.3f);
            p.Height = -body * 0.15f;
            return p;
        }

        private static DecalPixel MudSplat(float u, float v, int seed)
        {
            var x = u * 2f - 1f;
            var y = v * 2f - 1f;
            var rad = Mathf.Sqrt(x * x + y * y);
            var n = Fbm(u * 5f, v * 5f, seed, 3);
            var main = 1f - Smoothstep(0.18f + 0.18f * n, 0.4f + 0.2f * n, rad);
            float id, local;
            var drops = Blobs(u, v, 7, seed + 5, 0.55f, 0.18f, 0.42f, 1f, out id, out local);
            var dropMask = Smoothstep(0.1f, 0.35f, drops) * (1f - Smoothstep(0.35f, 0.95f, rad));
            var cov = Mathf.Max(main, dropMask);
            var p = new DecalPixel();
            var tone = 0.7f + 0.3f * Fbm(u * 9f, v * 9f, seed + 3, 2);
            p.R = 0.26f * tone;
            p.G = 0.19f * tone;
            p.B = 0.12f * tone;
            p.A = cov * 0.9f * RadialFade(rad, 0.92f);
            p.Smooth = 0.35f;
            p.Height = cov * (0.3f + 0.4f * n);
            return p;
        }

        private static DecalPixel TireTrack(float u, float v, int seed)
        {
            var band = 0f;
            var pat = 0f;
            var nearest = 0.27f;
            for (var i = 0; i < 2; i++)
            {
                var c = i == 0 ? 0.27f : 0.73f;
                var dx = Mathf.Abs(u - c);
                var b = 1f - Smoothstep(0.065f, 0.095f, dx);
                if (b > band)
                {
                    band = b;
                    nearest = c;
                }
            }
            var side = u - nearest;
            // Tırtıl (chevron) deseni: yola çapraz yönlü dalga.
            pat = 0.5f + 0.5f * Mathf.Sin((v * 46f + Mathf.Abs(side) * 70f) * Mathf.PI);
            pat = Smoothstep(0.25f, 0.75f, pat);
            var wear = Smoothstep(0.22f, 0.65f, Fbm(u * 3f + seed * 0.37f, v * 7f, seed, 3));
            var endFade = Smoothstep(0f, 0.12f, v) * Smoothstep(0f, 0.12f, 1f - v);
            var inner = 0.35f + 0.45f * pat;
            var p = new DecalPixel();
            p.R = 0.075f;
            p.G = 0.058f;
            p.B = 0.042f;
            p.A = band * inner * wear * endFade * 0.85f;
            p.Smooth = 0.2f;
            p.Height = -band * pat;
            return p;
        }

        private static DecalPixel Leaves(float u, float v, int seed, bool needles)
        {
            float id, local;
            float cov;
            if (needles)
                cov = Blobs(u, v, 14, seed + 7, 0.8f, 0.2f, 0.4f, 7f, out id, out local);
            else
                cov = Blobs(u, v, 5, seed + 3, 0.7f, 0.22f, 0.42f, 2.2f, out id, out local);
            var edge = needles ? Smoothstep(0f, 0.08f, cov) : Smoothstep(0f, 0.12f, cov);
            var p = new DecalPixel();
            if (needles)
            {
                var k = 0.75f + 0.5f * id;
                p.R = 0.40f * k;
                p.G = 0.27f * k;
                p.B = 0.12f * k;
            }
            else
            {
                // Sonbahar paleti: yeşil-kahve-turuncu-pas.
                var pick = Mathf.FloorToInt(id * 5f) % 5;
                float r, g, b;
                switch (pick)
                {
                    case 0: r = 0.45f; g = 0.30f; b = 0.10f; break;
                    case 1: r = 0.55f; g = 0.36f; b = 0.12f; break;
                    case 2: r = 0.36f; g = 0.21f; b = 0.08f; break;
                    case 3: r = 0.30f; g = 0.28f; b = 0.10f; break;
                    default: r = 0.58f; g = 0.24f; b = 0.08f; break;
                }
                var vein = 1f - 0.35f * (1f - Smoothstep(0.02f, 0.1f, Mathf.Abs(local)));
                p.R = r * vein;
                p.G = g * vein;
                p.B = b * vein;
            }
            p.A = edge * 0.95f * BorderFade(u, v, 0.05f);
            p.Smooth = 0.25f;
            p.Height = cov * 0.4f;
            return p;
        }

        private static DecalPixel OilStain(float u, float v, int seed)
        {
            var x = u * 2f - 1f;
            var y = v * 2f - 1f;
            var rad = Mathf.Sqrt(x * x + y * y);
            var n = Fbm(x * 2.2f + 3f, y * 2.2f + 3f, seed, 4);
            var field = 1f - rad / (0.45f + 0.5f * n);
            var body = Smoothstep(0f, 0.25f, field);
            var rim = Smoothstep(0f, 0.08f, field) * (1f - Smoothstep(0.08f, 0.35f, field));
            var irid = Mathf.Sin(field * 18f + n * 6f) * 0.5f + 0.5f;
            var p = new DecalPixel();
            p.R = 0.025f + rim * 0.05f * irid;
            p.G = 0.025f + rim * 0.035f * (1f - irid);
            p.B = 0.03f + rim * 0.06f * irid;
            p.A = (0.25f + 0.55f * body) * Smoothstep(0f, 0.05f, field) * RadialFade(rad, 0.92f);
            p.Smooth = 0.4f + 0.55f * body;
            p.Height = 0f;
            return p;
        }

        private static DecalPixel WallGrime(float u, float v, int seed)
        {
            // v=0 duvar dibi. Dibe doğru yoğun, üste doğru dikey akıntılarla incelir.
            var dirt = Mathf.Pow(1f - v, 2.2f);
            var streak = Fbm(u * 14f + 3f, v * 2.4f, seed, 3);
            var a = Mathf.Clamp01(dirt * 1.35f + (streak - 0.5f) * 0.9f - 0.08f);
            a *= Smoothstep(0f, 0.15f, Mathf.Min(u, 1f - u));
            var algae = Smoothstep(0.5f, 0.8f, Fbm(u * 6f, v * 4f, seed + 9, 3)) * dirt;
            var p = new DecalPixel();
            p.R = Mathf.Lerp(0.11f, 0.12f, algae);
            p.G = Mathf.Lerp(0.09f, 0.15f, algae);
            p.B = Mathf.Lerp(0.07f, 0.07f, algae);
            p.A = a * 0.85f;
            p.Smooth = 0.2f;
            p.Height = 0f;
            return p;
        }

        private static DecalPixel SootStreak(float u, float v, int seed)
        {
            // v=1 üst (baca/pencere); aşağı doğru dikey is akıntıları.
            var streak = Fbm(u * 22f + 5f, v * 1.6f, seed, 3);
            var top = Mathf.Pow(v, 1.5f);
            var width = Smoothstep(0f, 0.3f, u) * Smoothstep(0f, 0.3f, 1f - u);
            var a = Mathf.Clamp01((streak - 0.33f) * 2.4f) * top * width;
            var p = new DecalPixel();
            p.R = 0.035f;
            p.G = 0.033f;
            p.B = 0.032f;
            p.A = a * 0.8f * Smoothstep(0f, 0.04f, 1f - v + 0.04f);
            p.Smooth = 0.15f;
            p.Height = 0f;
            return p;
        }

        private static DecalPixel Moss(float u, float v, int seed)
        {
            var x = u * 2f - 1f;
            var y = v * 2f - 1f;
            var rad = Mathf.Sqrt(x * x + y * y);
            var f = Fbm(u * 3.2f + 2f, v * 3.2f + 2f, seed, 4);
            var radial = 1f - Smoothstep(0.35f, 1f, rad);
            var m = Smoothstep(0.46f, 0.6f, f + radial * 0.45f - 0.2f);
            var detail = Fbm(u * 30f, v * 30f, seed + 4, 2);
            var p = new DecalPixel();
            p.R = Mathf.Lerp(0.10f, 0.20f, detail);
            p.G = Mathf.Lerp(0.20f, 0.32f, detail);
            p.B = Mathf.Lerp(0.06f, 0.09f, detail);
            p.A = m * 0.9f * RadialFade(rad, 0.85f);
            p.Smooth = 0.25f;
            p.Height = m * detail * 0.5f;
            return p;
        }

        private static DecalPixel Crack(float u, float v, int seed)
        {
            var wx = u + 0.1f * (Fbm(u * 4f, v * 4f, seed + 2, 3) - 0.5f);
            var wy = v + 0.1f * (Fbm(u * 4f + 9f, v * 4f + 4f, seed + 5, 3) - 0.5f);
            var ridge = Mathf.Abs(Fbm(wx * 3.5f, wy * 3.5f, seed, 4) - 0.5f);
            var main = 1f - Smoothstep(0.004f, 0.026f, ridge);
            var ridge2 = Mathf.Abs(Fbm(wx * 9f + 7f, wy * 9f + 3f, seed + 13, 3) - 0.5f);
            var branch = (1f - Smoothstep(0.003f, 0.016f, ridge2)) * 0.6f;
            var fringe = (1f - Smoothstep(0.02f, 0.07f, ridge)) * 0.15f;
            var line = Mathf.Max(Mathf.Max(main, branch), fringe);
            var x = u * 2f - 1f;
            var y = v * 2f - 1f;
            var rad = Mathf.Sqrt(x * x + y * y);
            var p = new DecalPixel();
            p.R = 0.035f;
            p.G = 0.033f;
            p.B = 0.032f;
            p.A = line * 0.9f * RadialFade(rad, 0.55f);
            p.Smooth = 0.3f;
            p.Height = -Mathf.Max(main, branch);
            return p;
        }

        private static DecalPixel Poster(float u, float v, int seed)
        {
            // Soyut yıpranmış afiş: başlık bandı, geometrik görsel, metin satırları; grafiti/slogan yok.
            var torn = (Fbm(u * 18f, v * 18f, seed, 2) - 0.5f) * 0.05f;
            var dr = Mathf.Min(Mathf.Min(u - 0.12f, 0.88f - u), Mathf.Min(v - 0.06f, 0.94f - v)) + torn;
            var inside = Smoothstep(0f, 0.01f, dr);
            // Sağ alt köşe kıvrılıp kopmuş.
            if (u > 0.78f && v < 0.18f && (u - 0.78f) + (0.18f - v) > 0.15f)
                inside = 0f;

            float pr, pg, pb;
            switch (Mathf.Abs(seed) % 3)
            {
                case 0: pr = 0.82f; pg = 0.80f; pb = 0.72f; break;
                case 1: pr = 0.85f; pg = 0.78f; pb = 0.55f; break;
                default: pr = 0.60f; pg = 0.66f; pb = 0.72f; break;
            }

            var ink = 0f;
            float ir = 0.12f, ig = 0.12f, ib = 0.14f;
            if (v > 0.78f && v < 0.9f && u > 0.18f && u < 0.82f)
                ink = 0.9f;
            if (v > 0.38f && v < 0.74f && u > 0.2f && u < 0.8f)
            {
                var cx = u - 0.5f;
                var cy = (v - 0.56f) * 1.2f;
                var rr = Mathf.Sqrt(cx * cx + cy * cy);
                var shape = Mathf.Abs(seed) % 2 == 0
                    ? 1f - Smoothstep(0.17f, 0.18f, rr) - (1f - Smoothstep(0.1f, 0.11f, rr)) * 0.7f
                    : (Mathf.Abs(cx) + Mathf.Abs(cy) < 0.2f ? 1f : 0f);
                if (shape > 0.02f)
                {
                    ink = Mathf.Max(ink, shape * 0.85f);
                    ir = 0.55f; ig = 0.16f; ib = 0.12f;
                }
            }
            if (v > 0.12f && v < 0.34f && u > 0.2f && u < 0.8f)
            {
                var row = Mathf.FloorToInt((v - 0.12f) / 0.04f);
                var inRow = ((v - 0.12f) % 0.04f) < 0.016f;
                var len = 0.35f + 0.4f * Hash(row, 3, seed);
                if (inRow && (u - 0.2f) < len * 0.6f)
                    ink = Mathf.Max(ink, 0.7f);
            }

            var wear = Smoothstep(0.55f, 0.8f, Fbm(u * 5f, v * 5f, seed + 21, 3));
            var wrinkle = 0.85f + 0.15f * Fbm(u * 24f, v * 6f, seed + 33, 2);
            var r = Mathf.Lerp(pr, ir, ink);
            var g = Mathf.Lerp(pg, ig, ink);
            var b = Mathf.Lerp(pb, ib, ink);
            r = Mathf.Lerp(r, 0.2f, wear * 0.55f) * wrinkle;
            g = Mathf.Lerp(g, 0.17f, wear * 0.55f) * wrinkle;
            b = Mathf.Lerp(b, 0.12f, wear * 0.55f) * wrinkle;
            var p = new DecalPixel();
            p.R = r;
            p.G = g;
            p.B = b;
            p.A = inside * 0.95f;
            p.Smooth = 0.35f;
            p.Height = 0f;
            return p;
        }

        // ------------------------------------------------------------------------------------------ dizi üretimi

        /// <summary>
        /// Türün dokularını doldurur: albedo (RGBA, alfa = kapsama), normal (RGB teğet uzayı, A 255; HasNormal değilse null),
        /// maske (R metalik 0, G AO 255, B 255, A pürüzsüzlük). Çıktı dizileri size*size uzunlukta.
        /// </summary>
        public static void BuildMaps(DecalKind kind, int seed, int size, out Color32[] albedo, out Color32[] normal, out Color32[] mask)
        {
            size = Mathf.Max(4, size);
            var n = size * size;
            albedo = new Color32[n];
            mask = new Color32[n];
            var heights = HasNormal(kind) ? new float[n] : null;
            for (var y = 0; y < size; y++)
            {
                var v = (y + 0.5f) / size;
                for (var x = 0; x < size; x++)
                {
                    var u = (x + 0.5f) / size;
                    var p = Sample(kind, u, v, seed);
                    var i = y * size + x;
                    albedo[i] = new Color32(To8(p.R), To8(p.G), To8(p.B), To8(p.A));
                    mask[i] = new Color32(0, 255, 255, To8(p.Smooth));
                    if (heights != null)
                        heights[i] = p.Height;
                }
            }

            normal = null;
            if (heights == null)
                return;

            normal = new Color32[n];
            var k = NormalStrength(kind);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var hl = heights[y * size + Mathf.Max(x - 1, 0)];
                    var hr = heights[y * size + Mathf.Min(x + 1, size - 1)];
                    var hd = heights[Mathf.Max(y - 1, 0) * size + x];
                    var hu = heights[Mathf.Min(y + 1, size - 1) * size + x];
                    var nx = (hl - hr) * k;
                    var ny = (hd - hu) * k;
                    var nz = 1f;
                    var inv = 1f / Mathf.Sqrt(nx * nx + ny * ny + nz * nz);
                    normal[y * size + x] = new Color32(To8(nx * inv * 0.5f + 0.5f), To8(ny * inv * 0.5f + 0.5f), To8(nz * inv * 0.5f + 0.5f), 255);
                }
            }
        }

        private static byte To8(float f)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(f * 255f), 0, 255);
        }
    }
}
