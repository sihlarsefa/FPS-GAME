using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Yüksek ayrıntılı ağaç mesh'leri (alfa kesmeli kart tabanlı yapraklanma). Alt mesh 0 = gövde/dal (katı), 1 = kartlar
    /// (kesmeli malzeme, UV 0..1). LOD 0 en ayrıntılı, LOD 1 orta; LOD 2 için eski düz mesh'ler (<see cref="TreeFactory"/>)
    /// kullanılır. Köşe rengi: R = rüzgâr bükülme ağırlığı (yükseklik), G = rastgele faz, A = ortam kapanması.
    /// </summary>
    public static class TreeMeshes
    {
        public const int MaxLod = 1;

        /// <summary>Kalıcı ağaç prototiplerinin mesh/malzeme sürümü; değişince kalıcı varlıklar yeniden üretilir.</summary>
        public const int MeshVersion = 4;

        /// <summary>Gölgeli köşe AO tabanı (0.72 * 255).</summary>
        public const float AoFloor = 184f;


        // ------------------------------------------------------------------ Varyant parametreleri (saf, tohumla belirlenimci)

        /// <summary>Çam varyantı: yükseklik/dal yoğunluğu/dal uzunluğu çarpanları ve ±%6 ton kayması.</summary>
        public struct PineVariant
        {
            public int Index;
            public float HeightMul;
            public float DensityMul;
            public float LengthMul;
            public float HueShift;
        }

        /// <summary>Meşe varyantı: taç kartlarının seyrekliği (parçalı siluet).</summary>
        public struct OakVariant
        {
            public int Index;
            public int Lobes;
            public int CardsPerLobe;
            public float KeepChance;
            public float ShellMin;
            public float SizeMin;
            public float SizeMax;
            public float DetachedChance;
        }

        public const int PineVariantCount = 3;
        public const int OakVariantCount = 2;
        public const float PineRefHeight = 7f;

        public static int PineVariantIndex(int seed) => ((seed % PineVariantCount) + PineVariantCount) % PineVariantCount;

        public static int OakVariantIndex(int seed) => ((seed % OakVariantCount) + OakVariantCount) % OakVariantCount;

        /// <summary>Çam varyantı 0..2: 0 orta, 1 uzun/sık/serin, 2 kısa/seyrek/sıcak. Efektif yükseklik = 7 m * HeightMul (5,5..8,5 m).</summary>
        public static PineVariant GetPineVariant(int index)
        {
            switch (((index % PineVariantCount) + PineVariantCount) % PineVariantCount)
            {
                case 1: return new PineVariant { Index = 1, HeightMul = 8.5f / PineRefHeight, DensityMul = 1.25f, LengthMul = 0.94f, HueShift = -0.06f };
                case 2: return new PineVariant { Index = 2, HeightMul = 5.5f / PineRefHeight, DensityMul = 0.8f, LengthMul = 1.1f, HueShift = 0.06f };
                default: return new PineVariant { Index = 0, HeightMul = 1f, DensityMul = 1f, LengthMul = 1f, HueShift = 0f };
            }
        }

        public static OakVariant GetOakVariant(int index)
        {
            if (((index % OakVariantCount) + OakVariantCount) % OakVariantCount == 1)
                return new OakVariant { Index = 1, Lobes = 5, CardsPerLobe = 22, KeepChance = 0.68f, ShellMin = 0.6f, SizeMin = 0.28f, SizeMax = 0.7f, DetachedChance = 0.22f };
            return new OakVariant { Index = 0, Lobes = 6, CardsPerLobe = 26, KeepChance = 0.8f, ShellMin = 0.5f, SizeMin = 0.32f, SizeMax = 0.85f, DetachedChance = 0.12f };
        }

        /// <summary>Kuru ağaç dal sayısı (eskisi 7/4; seyrek).</summary>
        public static int DeadBranchCount(int lod) => lod == 0 ? 5 : 3;

        /// <summary>Prototip tohumunu çam türüne göre varyanta sabitler: PineB her zaman 1; PineA 0 veya 2 (tohum bitine göre).</summary>
        public static int PineSeedFor(TreeKind kind, int seed)
        {
            var baseSeed = seed - PineVariantIndex(seed);
            var v = kind == TreeKind.PineB ? 1 : (((seed >> 3) & 1) == 0 ? 0 : 2);
            return baseSeed + v;
        }

        // ------------------------------------------------------------------ Çam

        public static Mesh Pine(float height, int seed, int lod)
        {
            lod = Mathf.Clamp(lod, 0, MaxLod);
            var rng = new System.Random(seed * 61 + 3 + lod);
            var pv = GetPineVariant(PineVariantIndex(seed));
            height *= pv.HeightMul;
            var b = new MeshBuilder(2);
            b.EnableColors();
            var trunkR = height * 0.03f;

            b.CurrentColor = new Color32(0, 0, 255, 255);
            MeshFactory.AddFrustum(b, MeshFactory.TrunkSubmesh, Vector3.zero, trunkR * 1.55f, trunkR, height * 0.07f, lod == 0 ? 8 : 6, false, false);
            MeshFactory.AddFrustum(b, MeshFactory.TrunkSubmesh, new Vector3(0f, height * 0.07f, 0f), trunkR, trunkR * 0.22f, height * 0.9f,
                lod == 0 ? 8 : 6, false, false);

            var whorls = lod == 0 ? 12 : 7;
            for (var k = 0; k < whorls; k++)
            {
                var t = k / (float)(whorls - 1);
                var y = height * Mathf.Lerp(0.15f, 0.9f, t);
                var length = height * Mathf.Lerp(0.3f, 0.07f, Mathf.Pow(t, 0.85f)) * Range(rng, 0.88f, 1.12f) * pv.LengthMul;
                var count = Mathf.Max(2, Mathf.RoundToInt(Mathf.Lerp(lod == 0 ? 7f : 5f, lod == 0 ? 4f : 3f, t) * pv.DensityMul));
                var yaw0 = Range(rng, 0f, Mathf.PI * 2f);
                var droop = Mathf.Lerp(0.34f, 0.08f, t);
                var rise = Mathf.Lerp(0.0f, 0.22f, t);
                var width = length * 0.95f;
                for (var i = 0; i < count; i++)
                {
                    var yaw = yaw0 + (i + Range(rng, -0.18f, 0.18f)) / count * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                    var origin = new Vector3(0f, y + Range(rng, -0.02f, 0.02f) * height, 0f) + dir * trunkR * 0.4f;
                    var bend = (byte)Mathf.Clamp(t * 200f + 40f, 0f, 255f);
                    AddBranchCard(b, origin, dir, length, width, droop, rise, lod == 0 ? 3 : 2, Range(rng, 0.8f, 1f),
                        new Color32(bend, (byte)rng.Next(256), 255, 255));
                }
            }

            // Tepe sürgünü: dikey çapraz kartlar.
            var topY = height * 0.86f;
            for (var i = 0; i < 4; i++)
            {
                var yaw = i / 4f * Mathf.PI * 2f + 0.4f;
                var dir = new Vector3(Mathf.Cos(yaw) * 0.22f, 1f, Mathf.Sin(yaw) * 0.22f).normalized;
                AddBranchCard(b, new Vector3(0f, topY, 0f), dir, height * 0.15f, height * 0.09f, 0.02f, 0f, 2, 1f, new Color32(255, (byte)(i * 60), 255, 255));
            }

            return b.ToMesh("HK_PineTree_L" + lod);
        }

        // ------------------------------------------------------------------ Meşe

        public static Mesh Oak(float height, int seed, int lod)
        {
            lod = Mathf.Clamp(lod, 0, MaxLod);
            var rng = new System.Random(seed * 67 + 11 + lod);
            var ov = GetOakVariant(OakVariantIndex(seed));
            var b = new MeshBuilder(2);
            b.EnableColors();
            b.CurrentColor = new Color32(0, 0, 255, 255);
            var trunkR = height * 0.05f;
            var trunkH = height * 0.5f;
            MeshFactory.AddFrustum(b, MeshFactory.TrunkSubmesh, Vector3.zero, trunkR * 1.4f, trunkR, trunkH * 0.18f, lod == 0 ? 9 : 6, false, false);
            MeshFactory.AddFrustum(b, MeshFactory.TrunkSubmesh, new Vector3(0f, trunkH * 0.18f, 0f), trunkR, trunkR * 0.62f, trunkH * 0.82f,
                lod == 0 ? 9 : 6, false, false);

            var lobes = lod == 0 ? ov.Lobes : Mathf.Min(5, ov.Lobes);
            var crownY = height * 0.68f;
            var branchCount = lod == 0 ? 4 : 3;
            for (var i = 0; i < branchCount; i++)
            {
                var yaw = i / (float)branchCount * 360f + Range(rng, -25f, 25f);
                var tilt = Range(rng, 32f, 52f);
                var rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, -tilt);
                var start = new Vector3(0f, trunkH * Range(rng, 0.7f, 0.92f), 0f);
                MeshFactory.AddFrustum(b, MeshFactory.TrunkSubmesh, start, rotation, trunkR * 0.5f, trunkR * 0.18f, height * Range(rng, 0.26f, 0.34f),
                    lod == 0 ? 6 : 4, false, false);
            }

            var cardsPerLobe = lod == 0 ? ov.CardsPerLobe : 11;
            var cardScale = lod == 0 ? 1f : 1.55f;
            for (var l = 0; l < lobes; l++)
            {
                Vector3 center;
                float radius;
                if (l == 0)
                {
                    center = new Vector3(0f, crownY + height * 0.04f, 0f);
                    radius = height * 0.25f;
                }
                else
                {
                    var angle = (l - 1) / (float)(lobes - 1) * Mathf.PI * 2f + Range(rng, -0.35f, 0.35f);
                    var dist = height * Range(rng, 0.17f, 0.26f);
                    center = new Vector3(Mathf.Cos(angle) * dist, crownY + height * Range(rng, -0.08f, 0.06f), Mathf.Sin(angle) * dist);
                    radius = height * Range(rng, 0.15f, 0.2f);
                }

                for (var c = 0; c < cardsPerLobe; c++)
                {
                    // Tüm rastgele değerler her zaman çekilir (belirlenimci); boşluklar kartı atlayarak açılır.
                    var outward = RandomUnit(rng);
                    var keep = rng.NextDouble();
                    var radial = Range(rng, ov.ShellMin * 0.8f, 1.12f);
                    var sizeK = Range(ov.SizeMin, ov.SizeMax, rng);
                    var detached = rng.NextDouble() < ov.DetachedChance;
                    var big = rng.NextDouble() < 0.12;
                    if (keep > ov.KeepChance && !detached)
                        continue; // taç siluetinde delik
                    if (outward.y < -0.35f)
                        outward.y = -outward.y * 0.4f; // alt yarıküre seyrek
                    if (detached)
                        radial = Range(rng, 1.15f, 1.45f); // taçtan sarkan/fırlayan küçük küme
                    var pos = center + Vector3.Scale(outward, new Vector3(1.05f, 0.82f, 1.05f)) * radius * radial;
                    var size = radius * sizeK * cardScale * (detached ? 0.55f : big ? 1.3f : 1f);
                    var h01 = Mathf.Clamp01(pos.y / height);
                    AddLeafCard(b, pos, outward, size, rng, new Color32((byte)(h01 * 255f), (byte)rng.Next(256), 255, (byte)Mathf.Lerp(AoFloor, 255f, (outward.y + 1f) * 0.5f)));
                }
            }

            return b.ToMesh("HK_OakTree_L" + lod);
        }

        // ------------------------------------------------------------------ Çalı

        public static Mesh Bush(float radius, int seed, int lod)
        {
            lod = Mathf.Clamp(lod, 0, MaxLod);
            var rng = new System.Random(seed * 71 + 17 + lod);
            var b = new MeshBuilder(2);
            b.EnableColors();
            b.CurrentColor = new Color32(0, 0, 255, 255);
            for (var i = 0; i < (lod == 0 ? 4 : 2); i++)
            {
                var rotation = Quaternion.Euler(Range(rng, -22f, 22f), Range(rng, 0f, 360f), Range(rng, -22f, 22f));
                MeshFactory.AddFrustum(b, MeshFactory.TrunkSubmesh, Vector3.zero, rotation, radius * 0.05f, radius * 0.02f, radius * 0.6f, 4, false, false);
            }

            var lobes = 3;
            var cards = lod == 0 ? 15 : 7;
            for (var l = 0; l < lobes; l++)
            {
                var angle = Range(rng, 0f, Mathf.PI * 2f);
                var off = l == 0 ? Vector3.zero : new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius * 0.42f;
                var center = new Vector3(off.x, radius * (l == 0 ? 0.5f : 0.38f), off.z);
                var lobeR = radius * (l == 0 ? 0.62f : 0.42f);
                for (var c = 0; c < cards; c++)
                {
                    var outward = RandomUnit(rng);
                    outward.y = Mathf.Abs(outward.y) * 0.8f + 0.1f;
                    outward.Normalize();
                    var pos = center + Vector3.Scale(outward, new Vector3(1f, 0.75f, 1f)) * lobeR * Range(rng, 0.5f, 1f);
                    var size = lobeR * Range(0.7f, 1f, rng) * (lod == 0 ? 1f : 1.5f);
                    AddLeafCard(b, pos, outward, size, rng, new Color32(40, (byte)rng.Next(256), 255, (byte)Mathf.Lerp(AoFloor, 255f, outward.y)));
                }
            }

            return b.ToMesh("HK_Bush_L" + lod);
        }

        // ------------------------------------------------------------------ Kuru ağaç

        public static Mesh Dead(float height, int seed, int lod)
        {
            lod = Mathf.Clamp(lod, 0, MaxLod);
            var rng = new System.Random(seed * 73 + 13 + lod);
            var b = new MeshBuilder(2);
            b.EnableColors();
            b.CurrentColor = new Color32(0, 0, 255, 255);
            var trunkR = height * 0.045f;
            var lean = Quaternion.Euler(Range(rng, -7f, 7f), 0f, Range(rng, -7f, 7f));
            var sides = lod == 0 ? 7 : 5;
            MeshFactory.AddFrustum(b, MeshFactory.TrunkSubmesh, Vector3.zero, lean, trunkR * 1.5f, trunkR, height * 0.12f, sides, false, false);
            MeshFactory.AddFrustum(b, MeshFactory.TrunkSubmesh, lean * new Vector3(0f, height * 0.12f, 0f), lean, trunkR, trunkR * 0.2f, height * 0.88f, sides, false, false);

            var branches = DeadBranchCount(lod);
            var depth = lod == 0 ? 2 : 1;
            for (var i = 0; i < branches; i++)
            {
                var y = height * Range(rng, 0.3f, 0.88f);
                var yaw = i / (float)branches * 360f + Range(rng, -30f, 30f);
                var tilt = Range(rng, 38f, 70f);
                var rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, -tilt);
                var start = lean * new Vector3(0f, y, 0f);
                var length = height * Range(rng, 0.17f, 0.3f) * Mathf.Lerp(1.15f, 0.6f, y / height);
                AddBranch(b, rng, start, rotation, length, trunkR * 0.36f, depth);
            }

            return b.ToMesh("HK_DeadTree_L" + lod);
        }

        private static void AddBranch(MeshBuilder b, System.Random rng, Vector3 start, Quaternion rotation, float length, float radius, int depth)
        {
            MeshFactory.AddFrustum(b, MeshFactory.FoliageSubmesh, start, rotation, radius, radius * 0.3f, length, depth > 0 ? 5 : 4, false, false);
            if (depth <= 0)
                return;
            var forks = 2;
            for (var f = 0; f < forks; f++)
            {
                var at = Range(rng, 0.45f, 0.85f);
                var p = start + rotation * (Vector3.up * length * at);
                var child = rotation * Quaternion.Euler(Range(rng, -35f, 35f), Range(rng, 0f, 360f), Range(rng, 22f, 42f));
                AddBranch(b, rng, p, child, length * Range(rng, 0.4f, 0.6f), radius * 0.45f, depth - 1);
            }
        }

        // ------------------------------------------------------------------ Kart yardımcıları

        /// <summary>
        /// Sarkık dal kartı: origin'den dir yönünde length uzunluğunda, segments dilimli şerit; ucu droop*length kadar sarkar.
        /// Normal yukarı ağırlıklıdır (yumuşak ışıklandırma). UV: u genişlik, v uzunluk.
        /// </summary>
        public static void AddBranchCard(MeshBuilder b, Vector3 origin, Vector3 dir, float length, float width, float droop, float rise, int segments,
            float lengthScale, Color32 color)
        {
            segments = Mathf.Clamp(segments, 1, 6);
            dir.Normalize();
            var side = Vector3.Cross(Vector3.up, dir);
            if (side.sqrMagnitude < 1e-6f)
                side = Vector3.right;
            side.Normalize();
            length *= lengthScale;
            var normal = (Vector3.up * 0.7f + dir * 0.3f).normalized;
            var first = -1;
            for (var s = 0; s <= segments; s++)
            {
                var t = s / (float)segments;
                var p = origin + dir * (length * t) + Vector3.up * (length * (rise * t - droop * t * t));
                var w = width * 0.5f * lengthScale;
                var c = color;
                c.a = (byte)Mathf.Lerp(150f, 255f, t);
                c.b = (byte)Mathf.Lerp(AoFloor, 255f, t); // B = AO (gölgelendirici sözleşmesi): dal köküne yakın biraz koyu, asla siyah değil
                b.CurrentColor = c;
                var l = b.AddVertex(p - side * w, normal, new Vector2(0f, t));
                b.AddVertex(p + side * w, normal, new Vector2(1f, t));
                if (first < 0)
                    first = l;
            }

            for (var s = 0; s < segments; s++)
            {
                var i = first + s * 2;
                b.AddTriangle(MeshFactory.FoliageSubmesh, i, i + 2, i + 1);
                b.AddTriangle(MeshFactory.FoliageSubmesh, i + 1, i + 2, i + 3);
            }
        }

        /// <summary>Yaprak kümesi kartı: pos merkezli, rastgele dönmüş kare; normal dışa (küre gibi yumuşak aydınlanır).</summary>
        public static void AddLeafCard(MeshBuilder b, Vector3 pos, Vector3 outward, float size, System.Random rng, Color32 color)
        {
            // Kart düzlemi dışa bakan yöne dik değil, rastgele eğik: gerçekçi dağınıklık.
            var facing = (outward + RandomUnit(rng) * 0.6f).normalized;
            var right = Vector3.Cross(facing, Mathf.Abs(facing.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            var up = Vector3.Cross(right, facing).normalized;
            var roll = Range(rng, 0f, Mathf.PI * 2f);
            var r = right * Mathf.Cos(roll) + up * Mathf.Sin(roll);
            var u = -right * Mathf.Sin(roll) + up * Mathf.Cos(roll);
            var h = size * 0.5f;
            var n = (outward * 0.7f + Vector3.up * 0.3f).normalized;
            color.b = (byte)Mathf.Clamp(color.a, (int)AoFloor, 255); // B = AO; gölgeli taraf saf siyaha düşmez
            b.CurrentColor = color;
            var i0 = b.AddVertex(pos - r * h - u * h, n, new Vector2(0f, 0f));
            var i1 = b.AddVertex(pos - r * h + u * h, n, new Vector2(0f, 1f));
            var i2 = b.AddVertex(pos + r * h + u * h, n, new Vector2(1f, 1f));
            var i3 = b.AddVertex(pos + r * h - u * h, n, new Vector2(1f, 0f));
            b.AddTriangle(MeshFactory.FoliageSubmesh, i0, i1, i2);
            b.AddTriangle(MeshFactory.FoliageSubmesh, i0, i2, i3);
        }

        private static Vector3 RandomUnit(System.Random rng)
        {
            var z = Range(rng, -1f, 1f);
            var a = Range(rng, 0f, Mathf.PI * 2f);
            var r = Mathf.Sqrt(1f - z * z);
            return new Vector3(r * Mathf.Cos(a), z, r * Mathf.Sin(a));
        }

        private static float Range(System.Random rng, float min, float max) => TreeFactory.Range(rng, min, max);

        // (min, max, rng) sırası: aşırı yüklemeyi okunur kılmak için.
        private static float Range(float min, float max, System.Random rng) => TreeFactory.Range(rng, min, max);

        /// <summary>Tür ve LOD için üçgen sayısı üst sınırı (bütçe testleri).</summary>
        public static int TriangleBudget(TreeKind kind, int lod)
        {
            switch (kind)
            {
                case TreeKind.PineA:
                case TreeKind.PineB: return lod == 0 ? 1800 : 700;
                case TreeKind.Oak: return lod == 0 ? 1700 : 600;
                case TreeKind.Dead: return lod == 0 ? 900 : 350;
                default: return lod == 0 ? 700 : 300;
            }
        }
    }
}
