#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project.Infrastructure.Characters;
using UnityEngine;

namespace Project.Tests.EditMode
{
    /// <summary>
    /// Savaş yıpranması KARARTMA DENGESİ. Saf Color32 dizisi doku üretimi (Texture2D/Unity yerel köprüsü yok; hepsi gerçekten koşar).
    /// Ölçüt: ortalama sRGB parlaklık oranı (0.299R + 0.587G + 0.114B) = yıpranmış / temiz. Kabul tabanları: wear 1.0'da üniforma ≥ 0.62,
    /// yüz ≥ 0.60 (aradaki seviyelerde 1'den tabana doğrusal). Kir/çamur/is YAMA ve ton kaymasıdır; global çarpan yığını değildir.
    /// 2026-10-06 öncesi fırın (ölçüldü): wear 1.0'da üniforma 0.85-0.88, yüz 0.88 — yani siyaha inmiyordu; yeni fırın taban altına inemez.
    /// </summary>
    public sealed class BattleWearBalanceTests
    {
        private const float UniformSpecFloorAtFull = 0.62f;
        private const float FaceSpecFloorAtFull = 0.60f;
        private const int CamoSize = 256;
        private const int FaceSize = 128;
        private static readonly float[] Levels = { 0f, 0.33f, 0.66f, 1f };

        // TSK paletleri (SoldierLook ile aynı değerler): Orman, Dağ, Çöl, Şehir.
        private static readonly Color[][] Palettes =
        {
            new[] { new Color(0.42f, 0.41f, 0.25f), new Color(0.31f, 0.22f, 0.14f), new Color(0.27f, 0.31f, 0.18f), new Color(0.66f, 0.58f, 0.42f) },
            new[] { new Color(0.47f, 0.47f, 0.44f), new Color(0.35f, 0.36f, 0.33f), new Color(0.25f, 0.27f, 0.23f), new Color(0.62f, 0.62f, 0.58f) },
            new[] { new Color(0.74f, 0.66f, 0.5f), new Color(0.62f, 0.52f, 0.36f), new Color(0.5f, 0.42f, 0.3f), new Color(0.82f, 0.76f, 0.62f) },
            new[] { new Color(0.55f, 0.56f, 0.56f), new Color(0.35f, 0.36f, 0.37f), new Color(0.18f, 0.19f, 0.2f), new Color(0.72f, 0.73f, 0.74f) }
        };

        private static readonly string[] PaletteNames = { "orman", "dag", "col", "sehir" };

        private static readonly Color[] Skins =
        {
            new Color(0.85f, 0.66f, 0.52f), // en açık
            new Color(0.66f, 0.48f, 0.35f), // lobi askeri (HK_Char_skinface|A87A59)
            new Color(0.58f, 0.42f, 0.31f)  // en koyu
        };

        private static readonly Dictionary<int, Color32[]> SrcCache = new Dictionary<int, Color32[]>();
        private static readonly Dictionary<long, Color32[]> WornCache = new Dictionary<long, Color32[]>();

        // ------------------------------------------------------------------ Yardımcılar

        private static float H(int x, int y, int seed)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
            }
        }

        private static float VNoise(float x, float y, int seed)
        {
            var x0 = Mathf.FloorToInt(x);
            var y0 = Mathf.FloorToInt(y);
            var tx = x - x0;
            var ty = y - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            return Mathf.Lerp(Mathf.Lerp(H(x0, y0, seed), H(x0 + 1, y0, seed), tx), Mathf.Lerp(H(x0, y0 + 1, seed), H(x0 + 1, y0 + 1, seed), tx), ty);
        }

        /// <summary>Üretimdeki DigitalCamoFabric gibi: 40×40 hücre (iri bloklar), 4 renk (zemin + iki leke + küçük benek), 2×2 piksel dokuma ±%3.</summary>
        private static Color32[] Camo(Color[] pal, int size, int seed)
        {
            const int cells = 40;
            var cell = new Color32[cells * cells];
            for (var cy = 0; cy < cells; cy++)
            {
                for (var cx = 0; cx < cells; cx++)
                {
                    var fx = cx / (float)cells * 5f;
                    var fy = cy / (float)cells * 5f;
                    var n1 = 0.67f * VNoise(fx * 1.3f, fy * 1.3f, seed) + 0.33f * VNoise(fx * 2.6f, fy * 2.6f, seed + 1013);
                    var n2 = VNoise(fx * 2.1f + 9.1f, fy * 2.1f + 4.3f, seed * 31 + 7);
                    int index;
                    if (n2 > 0.70f) index = 3;
                    else if (n1 < 0.42f) index = 1;
                    else if (n1 > 0.58f) index = 2;
                    else index = 0;
                    cell[cy * cells + cx] = pal[index];
                }
            }

            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var col = cell[(y * cells / size) * cells + x * cells / size];
                    var w = (((x >> 1) + (y >> 1)) & 1) == 0 ? 1.03f : 0.97f;
                    px[y * size + x] = new Color32(B(col.r / 255f * w), B(col.g / 255f * w), B(col.b / 255f * w), 255);
                }
            }

            return px;
        }

        private static byte B(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        private static Color32[] Src(int palette, int seed = 101)
        {
            var key = palette * 1000 + seed;
            if (!SrcCache.TryGetValue(key, out var src))
                SrcCache[key] = src = Camo(Palettes[palette], CamoSize, seed);
            return src;
        }

        private static Color32[] Worn(int palette, float level, int seed = 101)
        {
            var key = ((long)palette << 40) ^ ((long)Mathf.RoundToInt(level * 100f) << 20) ^ (uint)seed;
            if (!WornCache.TryGetValue(key, out var worn))
                WornCache[key] = worn = CharacterTextureGen.UniformWearPixels(Src(palette, seed), level, seed);
            return worn;
        }

        private static double Lum(Color32 c) => (0.299 * c.r + 0.587 * c.g + 0.114 * c.b) / 255.0;

        private static double MeanLum(Color32[] a)
        {
            double s = 0;
            for (var i = 0; i < a.Length; i++) s += Lum(a[i]);
            return s / a.Length;
        }

        private static double LumSd(Color32[] a)
        {
            double s = 0, s2 = 0;
            for (var i = 0; i < a.Length; i++)
            {
                var l = Lum(a[i]);
                s += l;
                s2 += l * l;
            }

            var m = s / a.Length;
            return Math.Sqrt(Math.Max(0.0, s2 / a.Length - m * m));
        }

        private static int MaxChannelDelta(Color32 a, Color32 b) => Math.Max(Math.Abs(a.r - b.r), Math.Max(Math.Abs(a.g - b.g), Math.Abs(a.b - b.b)));

        /// <summary>Kabul tabanı: wear 0 → 1, wear 1 → atFull, aradaki seviyelerde doğrusal.</summary>
        private static double SpecFloor(float level, float atFull) => Mathf.Lerp(1f, atFull, level);

        // ------------------------------------------------------------------ Üniforma

        [Test]
        public void Uniform_MeanLuminanceRatio_StaysAboveFloor_AllPalettesAllLevels()
        {
            for (var p = 0; p < Palettes.Length; p++)
            {
                var baseLum = MeanLum(Src(p));
                foreach (var lv in Levels)
                {
                    var ratio = MeanLum(Worn(p, lv)) / baseLum;
                    var spec = SpecFloor(lv, UniformSpecFloorAtFull);
                    Assert.GreaterOrEqual(ratio, spec - 1e-4, PaletteNames[p] + " wear " + lv + ": oran " + ratio.ToString("F3") + " < kabul tabanı " + spec.ToString("F3"));
                    var design = WearRules.LuminanceFloor(WearRules.VisualStrength(lv), WearRules.UniformLuminanceFloor);
                    Assert.GreaterOrEqual(ratio, design - 0.003, PaletteNames[p] + " wear " + lv + ": oran " + ratio.ToString("F3") + " < tasarım tabanı " + design.ToString("F3"));
                }
            }
        }

        [Test]
        public void Uniform_MeanLuminanceRatio_HoldsAcrossSeedsAtFullWear()
        {
            for (var p = 0; p < Palettes.Length; p++)
            {
                for (var seed = 3; seed < 3 + 8; seed++)
                {
                    var src = Src(p, seed * 37 + 5);
                    var ratio = MeanLum(Worn(p, 1f, seed * 37 + 5)) / MeanLum(src);
                    Assert.GreaterOrEqual(ratio, UniformSpecFloorAtFull, PaletteNames[p] + " tohum " + seed + ": " + ratio.ToString("F3"));
                    Assert.GreaterOrEqual(ratio, WearRules.UniformLuminanceFloor - 0.003, PaletteNames[p] + " tohum " + seed + " tasarım tabanı: " + ratio.ToString("F3"));
                }
            }
        }

        [Test]
        public void Uniform_WearZero_IsAnExactCopy()
        {
            var src = Src(0);
            var same = CharacterTextureGen.UniformWearPixels(src, 0f, 101);
            Assert.IsFalse(ReferenceEquals(src, same), "kopya yeni dizi olmalı");
            Assert.AreEqual(src.Length, same.Length);
            for (var i = 0; i < src.Length; i++)
                Assert.IsTrue(src[i].r == same[i].r && src[i].g == same[i].g && src[i].b == same[i].b && src[i].a == same[i].a, "piksel " + i);
        }

        [Test]
        public void Uniform_WearOne_DiffersFromWearZero_ButOnlyInPatches()
        {
            for (var p = 0; p < Palettes.Length; p++)
            {
                var src = Src(p);
                var worn = Worn(p, 1f);
                long sum = 0;
                var changed = 0;
                for (var i = 0; i < src.Length; i++)
                {
                    var d = MaxChannelDelta(src[i], worn[i]);
                    sum += d;
                    if (d >= 6) changed++;
                }

                var frac = changed / (double)src.Length;
                var meanAbs = sum / (double)src.Length;
                Assert.GreaterOrEqual(frac, 0.08, PaletteNames[p] + ": etki yok/çok zayıf, değişen piksel oranı " + frac.ToString("F3"));
                Assert.LessOrEqual(frac, 0.55, PaletteNames[p] + ": yama değil global etki, değişen piksel oranı " + frac.ToString("F3"));
                Assert.GreaterOrEqual(meanAbs, 1.0, PaletteNames[p] + ": ortalama kanal farkı " + meanAbs.ToString("F2"));
            }
        }

        [Test]
        public void Uniform_LevelsAreMonotonic_HigherWearChangesMore()
        {
            for (var p = 0; p < Palettes.Length; p++)
            {
                var src = Src(p);
                double prevLum = 1.0;
                double prevDelta = -1.0;
                foreach (var lv in Levels)
                {
                    var worn = Worn(p, lv);
                    var ratio = MeanLum(worn) / MeanLum(src);
                    double delta = 0;
                    for (var i = 0; i < src.Length; i++) delta += MaxChannelDelta(src[i], worn[i]);
                    delta /= src.Length;
                    Assert.LessOrEqual(ratio, prevLum + 0.002, PaletteNames[p] + " wear " + lv + ": parlaklık oranı arttı");
                    Assert.GreaterOrEqual(delta, prevDelta - 0.05, PaletteNames[p] + " wear " + lv + ": değişim azaldı");
                    prevLum = ratio;
                    prevDelta = delta;
                }
            }
        }

        [Test]
        public void Uniform_CamoPattern_StaysReadableAtFullWear()
        {
            for (var p = 0; p < Palettes.Length; p++)
            {
                var retention = LumSd(Worn(p, 1f)) / LumSd(Src(p));
                Assert.GreaterOrEqual(retention, 0.75, PaletteNames[p] + ": desen kontrastı korunumu " + retention.ToString("F3"));
            }
        }

        [Test]
        public void Uniform_DirtDoesNotTurnNearBlack()
        {
            // Çamur beneği dahil hiçbir yıpranmış piksel siyaha inmez: parlaklık, kaynak daha koyu değilse en az 0.12 (çamur rengi 0.16) kalır.
            for (var p = 0; p < Palettes.Length; p++)
            {
                var src = Src(p);
                var worn = Worn(p, 1f);
                var bad = 0;
                var minRatio = 9.0;
                for (var i = 0; i < src.Length; i++)
                {
                    var floor = Math.Min(Lum(src[i]), 0.12) - 0.005;
                    if (Lum(worn[i]) < floor) bad++;
                    if (Lum(src[i]) > 0.05) minRatio = Math.Min(minRatio, Lum(worn[i]) / Lum(src[i]));
                }

                Assert.AreEqual(0, bad, PaletteNames[p] + ": " + bad + " piksel siyaha yakın");
                Assert.GreaterOrEqual(minRatio, 0.38, PaletteNames[p] + ": en koyulaşan piksel oranı " + minRatio.ToString("F3") + " (eski fırın 0.29-0.35)");
            }
        }

        [Test]
        public void Uniform_PatchCoverage_IsCapped()
        {
            // Alan eşiği: kapsama sınırı sentetik alanlarda da aşılmaz (kir ≤%35, toz ≤%30).
            var field = new float[128 * 128];
            for (var y = 0; y < 128; y++)
                for (var x = 0; x < 128; x++)
                    field[y * 128 + x] = VNoise(x / 16f, y / 16f, 77);
            foreach (var cap in new[] { CharacterTextureGen.UniformDirtMaxCoverage, CharacterTextureGen.UniformDustMaxCoverage, 0.10f, 0.50f })
            {
                var thr = CharacterTextureGen.FieldThreshold(field, cap);
                var above = 0;
                for (var i = 0; i < field.Length; i++) if (field[i] >= thr) above++;
                var frac = above / (double)field.Length;
                Assert.LessOrEqual(frac, cap + 1e-6, "kapsama " + frac.ToString("F4") + " > sınır " + cap);
                Assert.GreaterOrEqual(frac, cap - 0.08, "kapsama " + frac.ToString("F4") + " sınırdan çok uzak " + cap);
            }

            Assert.AreEqual(0.35f, CharacterTextureGen.UniformDirtMaxCoverage, 1e-6f);
            Assert.AreEqual(0.35f, CharacterTextureGen.UniformDirtMaxBlend, 1e-6f);
            Assert.AreEqual(0.32f, CharacterTextureGen.DirtColor.r, 1e-6f);
            Assert.AreEqual(0.26f, CharacterTextureGen.DirtColor.g, 1e-6f);
            Assert.AreEqual(0.18f, CharacterTextureGen.DirtColor.b, 1e-6f);
        }

        [Test]
        public void Uniform_IsDeterministic()
        {
            var src = Src(2);
            var a = CharacterTextureGen.UniformWearPixels(src, 0.66f, 5);
            var b = CharacterTextureGen.UniformWearPixels(src, 0.66f, 5);
            for (var i = 0; i < a.Length; i++)
                Assert.IsTrue(a[i].r == b[i].r && a[i].g == b[i].g && a[i].b == b[i].b, "piksel " + i);
        }

        [Test]
        public void Uniform_LuminanceGuard_BoundsExoticBrightCamo()
        {
            // Çok açık, tek tonlu bir kamuflajda bile koruma geçişi tabanın altına indirmez.
            var src = new Color32[CamoSize * CamoSize];
            for (var i = 0; i < src.Length; i++) src[i] = new Color32(250, 245, 235, 255);
            var worn = CharacterTextureGen.UniformWearPixels(src, 1f, 9);
            var ratio = MeanLum(worn) / MeanLum(src);
            Assert.GreaterOrEqual(ratio, WearRules.UniformLuminanceFloor - 0.003, "oran " + ratio.ToString("F3"));
            Assert.GreaterOrEqual(ratio, UniformSpecFloorAtFull);
        }

        // ------------------------------------------------------------------ Hero 0.85 → 1.0 kovası, 0.66 kovası görünümü

        [Test]
        public void Hero085_QuantizesToTopBucket_ButBakesLikeSixtySixBucket()
        {
            Assert.AreEqual(1f, WearRules.Quantize(0.85f), "Quantize API'si değişmedi: 0.85 → 1.0 kovası");
            Assert.AreEqual(3, WearRules.LevelIndex(0.85f));
            var top = WearRules.Quantize(0.85f);
            for (var p = 0; p < Palettes.Length; p++)
            {
                var baseLum = MeanLum(Src(p));
                var rTop = MeanLum(Worn(p, top)) / baseLum;
                var rMid = MeanLum(Worn(p, 0.66f)) / baseLum;
                Assert.LessOrEqual(Math.Abs(rTop - rMid), 0.03, PaletteNames[p] + ": 1.0 kovası 0.66 kovasından ayrıştı (" + rTop.ToString("F3") + " vs " + rMid.ToString("F3") + ")");
                Assert.GreaterOrEqual(rTop, 0.90, PaletteNames[p] + ": hero kovası " + rTop.ToString("F3") + " (eski fırın 0.85-0.88 idi)");
            }
        }

        [Test]
        public void VisualStrength_IsBoundedMonotonicAndSaturates()
        {
            Assert.AreEqual(0f, WearRules.VisualStrength(0f), 1e-6f);
            Assert.AreEqual(1f, WearRules.VisualStrength(WearRules.VisualSaturation), 1e-5f);
            Assert.AreEqual(1f, WearRules.VisualStrength(1f), 1e-6f);
            Assert.AreEqual(0f, WearRules.VisualStrength(-3f), 1e-6f);
            var prev = -1f;
            for (var w = 0f; w <= 1.0001f; w += 0.01f)
            {
                var v = WearRules.VisualStrength(w);
                Assert.That(v, Is.InRange(0f, 1f));
                Assert.GreaterOrEqual(v, prev - 1e-6f);
                prev = v;
            }

            // hero: 1.0 kovası görsel olarak 0.66 kovasından en çok %7 güçlü
            Assert.LessOrEqual(WearRules.VisualStrength(1f) - WearRules.VisualStrength(0.66f), 0.07f);
        }

        [Test]
        public void LuminanceFloor_InterpolatesFromOneToFloor()
        {
            Assert.AreEqual(1f, WearRules.LuminanceFloor(0f, 0.8f), 1e-6f);
            Assert.AreEqual(0.8f, WearRules.LuminanceFloor(1f, 0.8f), 1e-6f);
            Assert.AreEqual(0.9f, WearRules.LuminanceFloor(0.5f, 0.8f), 1e-6f);
            Assert.GreaterOrEqual(WearRules.UniformLuminanceFloor, UniformSpecFloorAtFull);
            Assert.GreaterOrEqual(WearRules.FaceLuminanceFloor, FaceSpecFloorAtFull);
        }

        // ------------------------------------------------------------------ Yüz

        private static Color32[] FaceTex(Color skin, float level, int variant) => CharacterTextureGen.FaceWearPixels(FaceSize, level, variant, skin);

        /// <summary>Yüzün ön kısmında (front &gt; 0.5) ten × çarpan dokusunun ortalama parlaklık oranı (ten parlaklığına göre) ve kanal başına oranlar.</summary>
        private static double FaceRatio(Color32[] tex, Color skin, out double rR, out double rG, out double rB, out int n)
        {
            double sk = 0.299 * skin.r + 0.587 * skin.g + 0.114 * skin.b;
            double sum = 0, sr = 0, sg = 0, sb = 0;
            n = 0;
            for (var py = 0; py < FaceSize; py++)
            {
                for (var px = 0; px < FaceSize; px++)
                {
                    CharacterTextureGen.FaceCoords((px + 0.5f) / FaceSize, (py + 0.5f) / FaceSize, out _, out _, out var front);
                    if (front <= 0.5f) continue;
                    var t = tex[py * FaceSize + px];
                    var r = skin.r * t.r / 255.0; var g = skin.g * t.g / 255.0; var b = skin.b * t.b / 255.0;
                    sum += 0.299 * r + 0.587 * g + 0.114 * b;
                    sr += r; sg += g; sb += b;
                    n++;
                }
            }

            rR = sr / n / skin.r; rG = sg / n / skin.g; rB = sb / n / skin.b;
            return sum / n / sk;
        }

        [Test]
        public void Face_MeanLuminanceRatio_StaysAboveFloor_AllLevelsSkinsVariants()
        {
            foreach (var skin in Skins)
            {
                foreach (var lv in Levels)
                {
                    for (var variant = 0; variant < WearRules.Variants; variant++)
                    {
                        var ratio = FaceRatio(FaceTex(skin, lv, variant), skin, out _, out _, out _, out _);
                        var spec = SpecFloor(lv, FaceSpecFloorAtFull);
                        Assert.GreaterOrEqual(ratio, spec - 1e-4, "ten " + skin + " wear " + lv + " v" + variant + ": oran " + ratio.ToString("F3") + " < kabul tabanı " + spec.ToString("F3"));
                        if (lv >= 1f)
                            Assert.GreaterOrEqual(ratio, WearRules.FaceLuminanceFloor, "ten " + skin + " v" + variant + ": oran " + ratio.ToString("F3") + " < tasarım hedefi");
                    }
                }
            }
        }

        [Test]
        public void Face_WearZero_IsCleanWhite_AndWearOneDiffersFromIt()
        {
            foreach (var skin in Skins)
            {
                var clean = FaceTex(skin, 0f, 0);
                foreach (var c in clean)
                    Assert.IsTrue(c.r == 255 && c.g == 255 && c.b == 255, "wear 0 saf beyaz çarpan olmalı");

                for (var variant = 0; variant < WearRules.Variants; variant++)
                {
                    var worn = FaceTex(skin, 1f, variant);
                    var strong = 0;
                    var any = 0;
                    for (var i = 0; i < worn.Length; i++)
                    {
                        var d = Math.Max(255 - worn[i].r, Math.Max(255 - worn[i].g, 255 - worn[i].b));
                        if (d > 0) any++;
                        if (d >= 40) strong++; // kan/is lekesi (taban düşüşü yalnız ~10)
                    }

                    Assert.Greater(strong, 12, "v" + variant + ": kan/is etkisi yok (" + strong + ")");
                    Assert.Greater(any, worn.Length / 4, "v" + variant + ": taban etkisi yok");
                }
            }
        }

        [Test]
        public void Face_Soot_CoverageAndStrengthAreBounded()
        {
            foreach (var lv in new[] { 0.33f, 0.66f, 1f })
            {
                for (var variant = 0; variant < WearRules.Variants; variant++)
                {
                    var front = 0;
                    var covered = 0;
                    float maxSoot = 0f;
                    for (var py = 0; py < FaceSize; py++)
                    {
                        for (var px = 0; px < FaceSize; px++)
                        {
                            CharacterTextureGen.FaceCoords((px + 0.5f) / FaceSize, (py + 0.5f) / FaceSize, out var x, out var y, out var fr);
                            if (fr <= 0.5f) continue;
                            front++;
                            CharacterTextureGen.FaceWearSample(x, y, lv, variant, out _, out var soot, out _);
                            if (soot > maxSoot) maxSoot = soot;
                            if (soot * fr > 0.02f) covered++;
                        }
                    }

                    var coverage = covered / (float)front;
                    Assert.LessOrEqual(coverage, 0.30f, "wear " + lv + " v" + variant + ": is kapsaması " + coverage.ToString("F3"));
                    Assert.LessOrEqual(maxSoot, 0.5f + 1e-4f, "wear " + lv + " v" + variant + ": is gücü " + maxSoot.ToString("F3"));
                }
            }
        }

        [Test]
        public void Face_Blood_StreaksStayThin()
        {
            for (var variant = 0; variant < WearRules.Variants; variant++)
            {
                var front = 0;
                var covered = 0;
                for (var py = 0; py < FaceSize; py++)
                {
                    for (var px = 0; px < FaceSize; px++)
                    {
                        CharacterTextureGen.FaceCoords((px + 0.5f) / FaceSize, (py + 0.5f) / FaceSize, out var x, out var y, out var fr);
                        if (fr <= 0.5f) continue;
                        front++;
                        CharacterTextureGen.FaceWearSample(x, y, 1f, variant, out var blood, out _, out _);
                        if (blood * fr > 0.05f) covered++;
                    }
                }

                Assert.LessOrEqual(covered / (float)front, 0.03f, "v" + variant + ": kan kapsaması " + (covered / (float)front).ToString("F4"));

                // genişlik: yatay taramada kan > 0.15 koşusu en çok 6 mm
                var maxRun = 0f;
                for (var y = 0.08f; y < 0.14f; y += 0.0005f)
                {
                    var run = 0f;
                    for (var x = -0.07f; x <= 0.07f; x += 0.0002f)
                    {
                        CharacterTextureGen.FaceWearSample(x, y, 1f, variant, out var blood, out _, out _);
                        if (blood > 0.15f) { run += 0.0002f; if (run > maxRun) maxRun = run; }
                        else run = 0f;
                    }
                }

                Assert.LessOrEqual(maxRun, 0.006f, "v" + variant + ": kan izi genişliği " + (maxRun * 1000f).ToString("F1") + " mm");
                var peak = 0f;
                for (var y = 0.05f; y < 0.14f; y += 0.0005f)
                    for (var x = -0.07f; x <= 0.07f; x += 0.0005f)
                    {
                        CharacterTextureGen.FaceWearSample(x, y, 1f, variant, out var blood, out _, out _);
                        if (blood > peak) peak = blood;
                    }

                Assert.LessOrEqual(peak, CharacterTextureGen.BloodMaxStrength + 1e-4f, "v" + variant + ": kan gücü " + peak.ToString("F3"));
            }
        }

        [Test]
        public void Face_SkinStaysSkinToned_AtFullWear()
        {
            foreach (var skin in Skins)
            {
                for (var variant = 0; variant < WearRules.Variants; variant++)
                {
                    FaceRatio(FaceTex(skin, 1f, variant), skin, out var rR, out var rG, out var rB, out _);
                    Assert.GreaterOrEqual(Math.Min(rR, Math.Min(rG, rB)), 0.85, "ten " + skin + " v" + variant + ": kanal oranları " + rR.ToString("F3") + "/" + rG.ToString("F3") + "/" + rB.ToString("F3"));
                    Assert.LessOrEqual(Math.Max(rR, Math.Max(rG, rB)) - Math.Min(rR, Math.Min(rG, rB)), 0.08, "ten " + skin + " v" + variant + ": ton kayması");
                }
            }
        }

        // ------------------------------------------------------------------ Gear (ince kalır)

        [Test]
        public void Gear_Scratches_StaySubtle()
        {
            var px = CharacterTextureGen.GearWearPixels(128, 1f, 29, true, false);
            var baseV = CharacterTextureGen.GearScratchBase * 255f;
            var lighter = 0;
            double mean = 0;
            foreach (var c in px)
            {
                Assert.GreaterOrEqual(c.r, baseV - 1.5f, "çizik tabanın altına karartmamalı");
                if (c.r > baseV + 3f) lighter++;
                mean += c.r;
            }

            mean /= px.Length;
            Assert.LessOrEqual(lighter / (double)px.Length, 0.10, "çizik/sıyrık kapsaması fazla");
            Assert.LessOrEqual(mean, baseV + 0.03 * 255.0, "çizik ortalaması tabandan çok açık: " + mean.ToString("F1"));
            Assert.Greater(lighter, 10, "çizik hiç yok");
        }

        [Test]
        public void Gear_MudSpecks_StaySparseAndPartial()
        {
            var px = CharacterTextureGen.GearWearPixels(128, 1f, 29, false, true);
            var dark = 0;
            double mean = 0;
            foreach (var c in px)
            {
                if (c.r < 200) dark++;
                mean += c.r / 255.0;
            }

            mean /= px.Length;
            Assert.LessOrEqual(dark / (double)px.Length, 0.06, "çamur beneği kapsaması fazla");
            Assert.GreaterOrEqual(mean, 0.97, "çamur beneği ortalamayı fazla karartıyor: " + mean.ToString("F3"));
            Assert.Greater(dark, 5, "çamur beneği hiç yok");
        }

        [Test]
        public void GearShaderScalars_AreBoundedAndEdgeWearStaysSubtle()
        {
            // wear 0 → taban değişmez
            Assert.AreEqual(0.45f, WearRules.GearEdgeWear(0.45f, 0f), 1e-6f);
            Assert.AreEqual(0.4f, WearRules.GearDirt(0.4f, 0f), 1e-6f);
            Assert.AreEqual(0.2f, WearRules.GearDust(0.2f, 0f), 1e-6f);
            // tam güç: cordura (taban 0) kenar aşınması ince; kask (taban 0.45) eski 0.95'ten düşük
            Assert.LessOrEqual(WearRules.GearEdgeWear(0f, 1f), 0.35f);
            Assert.LessOrEqual(WearRules.GearEdgeWear(0.45f, 1f), 0.70f);
            Assert.GreaterOrEqual(WearRules.GearEdgeWear(0f, 1f), 0.1f);
            // kir/toz artışı sınırlı (+0.20 / +0.15), 0..1 içinde
            Assert.AreEqual(0.65f, WearRules.GearDirt(0.45f, 1f), 1e-5f);
            Assert.AreEqual(0.35f, WearRules.GearDust(0.2f, 1f), 1e-5f);
            Assert.LessOrEqual(WearRules.GearDirt(0.85f, 1f), 1f);
            // monoton
            Assert.GreaterOrEqual(WearRules.GearEdgeWear(0.2f, 1f), WearRules.GearEdgeWear(0.2f, 0.33f));
            Assert.GreaterOrEqual(WearRules.GearDirt(0.2f, 1f), WearRules.GearDirt(0.2f, 0.33f));
        }
    }
}
#endif
