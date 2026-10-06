using UnityEngine;

namespace Project.Presentation.Benchmark
{
    /// <summary>Benchmark parçasının bölge türleri.</summary>
    public enum BenchmarkZone
    {
        Grass = 0,
        Mud = 1,
        Rock = 2,
        Forest = 3,
        Pad = 4
    }

    /// <summary>
    /// AAA Benchmark sahnesinin saf yerleşim ve arazi matematiği (Unity nesnesi üretmez, test edilebilir).
    /// 150×150 m parça, merkez (0,0); yarı boyut <see cref="HalfSize"/>. Çamur, kaya, çim bölgeleri ve küçük orman,
    /// ev/araç/atış hattı alanları için düz zemin yamaları.
    /// </summary>
    public static class BenchmarkLayout
    {
        public const float HalfSize = 75f;
        public const float Size = HalfSize * 2f;
        public const float TerrainHeight = 18f;
        public const float BaseHeight = 2.0f;

        // Katman sırası TerrainLayerKind ile aynı: Grass, DryGrass, Dirt, Rock, Gravel, Mud, Snow, Asphalt.
        public const int LayerCount = 8;
        public const int LGrass = 0, LDry = 1, LDirt = 2, LRock = 3, LGravel = 4, LMud = 5;

        // Bölge merkezleri (x, z, yarıçap).
        public static readonly Vector3 MudZone = new Vector3(18f, -32f, 17f);
        public static readonly Vector3 RockZone = new Vector3(48f, 42f, 22f);
        public static readonly Vector3 ForestZone = new Vector3(-42f, 46f, 26f);

        // Düz yamalar (x, z, yarıçap).
        public static readonly Vector3 SpawnPad = new Vector3(-10f, -54f, 9f);
        public static readonly Vector3 HousePad = new Vector3(30f, 2f, 12f);
        public static readonly Vector3 VehiclePad = new Vector3(-36f, 12f, 9f);
        public static readonly Vector3 RangePad = new Vector3(-10f, -26f, 14f);

        public static Vector3 SpawnPoint => new Vector3(SpawnPad.x, 0f, SpawnPad.y);
        public static Vector3 HousePoint => new Vector3(HousePad.x, 0f, HousePad.y);
        public static Vector3 VehiclePoint => new Vector3(VehiclePad.x, 0f, VehiclePad.y);
        public static Vector3 WallPoint => new Vector3(-10f, 0f, -18f);

        public static float Smooth(float a, float b, float x)
        {
            if (b <= a)
                return x >= b ? 1f : 0f;
            var t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Noktanın bir bölgeye (x,z,r) uzaklığına göre 0..1 üyelik (merkezde 1, kenarda 0).</summary>
        public static float Membership(Vector3 zone, float x, float z, float feather = 0.35f)
        {
            var dx = x - zone.x;
            var dz = z - zone.y;
            var d = Mathf.Sqrt(dx * dx + dz * dz);
            return 1f - Smooth(zone.z * (1f - feather), zone.z, d);
        }

        /// <summary>Düz yama ağırlığı (1 = tamamen düz).</summary>
        public static float PadWeight(float x, float z)
        {
            var w = Membership(SpawnPad, x, z, 0.5f);
            w = Mathf.Max(w, Membership(HousePad, x, z, 0.5f));
            w = Mathf.Max(w, Membership(VehiclePad, x, z, 0.5f));
            w = Mathf.Max(w, Membership(RangePad, x, z, 0.5f));
            return w;
        }

        /// <summary>Arazi yüksekliği (m, yerel Y). Hafif dalga, kaya bölgesinde tepe, yamalarda düz.</summary>
        public static float HeightAt(float x, float z)
        {
            var wave = Mathf.PerlinNoise((x + 200f) * 0.045f, (z + 200f) * 0.045f) - 0.5f;
            var fine = Mathf.PerlinNoise((x + 91f) * 0.18f, (z + 57f) * 0.18f) - 0.5f;
            var h = BaseHeight + wave * 3.2f + fine * 0.35f;

            // Kaya bölgesinde yükselen tepe (dik yamaç = kaya katmanı).
            var rock = Membership(RockZone, x, z, 0.8f);
            h += rock * rock * 8.5f;

            // Çamur çukuru: hafif çanak.
            var mud = Membership(MudZone, x, z, 0.6f);
            h -= mud * 0.6f;

            var pad = PadWeight(x, z);
            return Mathf.Lerp(h, BaseHeight, pad);
        }

        /// <summary>Verilen noktadaki baskın bölge (tohum yerleşimi için).</summary>
        public static BenchmarkZone ZoneAt(float x, float z)
        {
            if (PadWeight(x, z) > 0.5f)
                return BenchmarkZone.Pad;
            var forest = Membership(ForestZone, x, z);
            var rock = Membership(RockZone, x, z);
            var mud = Membership(MudZone, x, z);
            if (forest >= rock && forest >= mud && forest > 0.3f)
                return BenchmarkZone.Forest;
            if (rock >= mud && rock > 0.3f)
                return BenchmarkZone.Rock;
            if (mud > 0.3f)
                return BenchmarkZone.Mud;
            return BenchmarkZone.Grass;
        }

        /// <summary>Katman ağırlıkları (toplam 1). weights en az <see cref="LayerCount"/> uzunlukta olmalı.</summary>
        public static void Weights(float x, float z, float slopeDegrees, float[] weights)
        {
            if (weights == null || weights.Length < LayerCount)
                return;
            for (var i = 0; i < LayerCount; i++)
                weights[i] = 0f;

            var patch = Mathf.PerlinNoise((x + 33f) * 0.09f, (z - 17f) * 0.09f);
            var mud = Membership(MudZone, x, z, 0.5f);
            var rock = Membership(RockZone, x, z, 0.6f);
            var slopeRock = Smooth(26f, 38f, slopeDegrees);
            var pad = PadWeight(x, z);

            var wMud = mud * (0.75f + 0.25f * patch);
            var wRock = Mathf.Clamp01(rock * 0.85f + slopeRock);
            var wGravel = pad * 0.35f + rock * (1f - wRock) * 0.35f + mud * 0.18f;
            var wDirt = pad * 0.45f + mud * 0.25f * (1f - patch);
            var baseGrass = Mathf.Clamp01(1f - wMud - wRock);

            weights[LMud] = wMud;
            weights[LRock] = wRock;
            weights[LGravel] = wGravel;
            weights[LDirt] = wDirt;
            weights[LGrass] = baseGrass * (0.45f + 0.55f * patch) * (1f - pad * 0.7f);
            weights[LDry] = baseGrass * (0.55f - 0.55f * patch) * (1f - pad * 0.7f);

            var sum = 0f;
            for (var i = 0; i < LayerCount; i++)
                sum += weights[i];
            if (sum <= 1e-5f)
            {
                weights[LGrass] = 1f;
                return;
            }

            var inv = 1f / sum;
            for (var i = 0; i < LayerCount; i++)
                weights[i] *= inv;
        }

        /// <summary>Orman bölgesinde ağaç yoğunluğu (0..1); evin/aracın/atış alanının yakınında 0.</summary>
        public static float TreeDensity(float x, float z)
        {
            if (PadWeight(x, z) > 0.05f)
                return 0f;
            var forest = Membership(ForestZone, x, z, 0.45f);
            var sparse = 0.06f * Membership(new Vector3(0f, 0f, HalfSize * 1.2f), x, z, 0.8f);
            var rockEdge = Membership(RockZone, x, z, 0.6f) * 0.05f;
            return Mathf.Clamp01(forest + sparse + rockEdge);
        }

        /// <summary>Dünya x,z → 0..1 normalize arazi koordinatı.</summary>
        public static Vector2 ToNormalized(float x, float z) => new Vector2((x + HalfSize) / Size, (z + HalfSize) / Size);
    }
}
