using System;
using System.Collections.Generic;

namespace Project.Infrastructure.World
{
    /// <summary>Aşındırma ayarları. Sınırlı yineleme: bütçe damla/adım sayısıyla belirlenir (zamanla değil → belirlenimci).</summary>
    public sealed class TerrainErosionSettings
    {
        /// <summary>Hücre başına damla sayısı (toplam = hücre × bu, MaxDroplets ile sınırlı).</summary>
        public float DropletsPerCell = 0.22f;

        /// <summary>Toplam damla üst sınırı (1 km / 1025 haritada bile &lt; 2 sn).</summary>
        public int MaxDroplets = 90000;

        /// <summary>Damla başına en çok adım.</summary>
        public int MaxSteps = 28;

        public float Inertia = 0.06f;
        public float CapacityFactor = 3.5f;
        public float MinCapacity = 0.01f;
        public float ErodeSpeed = 0.18f;
        public float DepositSpeed = 0.25f;
        public float Evaporate = 0.025f;
        public float Gravity = 4f;

        /// <summary>Tek adımda bir hücreden alınabilecek en çok malzeme (m).</summary>
        public float MaxErodePerStep = 0.12f;

        /// <summary>Isıl (talus) yineleme sayısı; 0 = kapalı.</summary>
        public int ThermalIterations = 5;

        /// <summary>Talus eğimi (derece): üstü kayar.</summary>
        public float TalusDegrees = 40f;

        /// <summary>Hücrenin özgün yüksekliğinden en çok sapma (m): vadi/yerleşim yapısı bozulmaz.</summary>
        public float MaxDelta = 3.5f;

        /// <summary>Genel güç [0,1]; 0 = aşındırma yok.</summary>
        public float Strength = 1f;

        /// <summary>Kalite kademesine göre ayar (0 Düşük .. 3 Ultra); gen-zamanı bütçesi.</summary>
        public static TerrainErosionSettings ForTier(int level)
        {
            var s = new TerrainErosionSettings();
            switch (level)
            {
                case 0: s.DropletsPerCell = 0.08f; s.MaxDroplets = 30000; s.ThermalIterations = 3; break;
                case 1: s.DropletsPerCell = 0.15f; s.MaxDroplets = 60000; s.ThermalIterations = 4; break;
                case 3: s.DropletsPerCell = 0.3f; s.MaxDroplets = 130000; s.ThermalIterations = 6; break;
            }

            return s;
        }
    }

    /// <summary>Aşındırma sonrası türetilen haritalar (model çözünürlüğünde, [z*res+x]).</summary>
    public sealed class TerrainErosionMaps
    {
        /// <summary>Akış/ıslaklık [0,1] (oluklar).</summary>
        public float[] Flow;

        /// <summary>Eğrilik [-1,1]: + dışbükey (sırt), − içbükey (oluk).</summary>
        public float[] Curvature;

        /// <summary>Uçurum dibi döküntü (scree) [0,1].</summary>
        public float[] Scree;

        /// <summary>Uçurum yakınlığı [0,1] (dik yamaç bulanıklaşmış).</summary>
        public float[] Cliff;

        public int Resolution;
        public float CellSize;
        public float OriginX;
        public float OriginZ;

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TerrainModel, TerrainErosionMaps> Table =
            new System.Runtime.CompilerServices.ConditionalWeakTable<TerrainModel, TerrainErosionMaps>();

        /// <summary>Modele haritaları iliştirir (üzerine yazar).</summary>
        public static void Attach(TerrainModel model, TerrainErosionMaps maps)
        {
            if (model == null)
                return;
            Table.Remove(model);
            if (maps != null)
                Table.Add(model, maps);
        }

        /// <summary>Modele iliştirilmiş haritalar (yoksa null: boyama eski davranışta kalır).</summary>
        public static TerrainErosionMaps Of(TerrainModel model)
        {
            return model != null && Table.TryGetValue(model, out var maps) ? maps : null;
        }

        /// <summary>Çift doğrusal örnek (dünya x,z).</summary>
        public float Sample(float[] grid, float x, float z)
        {
            var res = Resolution;
            var fx = Clamp((x - OriginX) / CellSize, 0f, res - 1.001f);
            var fz = Clamp((z - OriginZ) / CellSize, 0f, res - 1.001f);
            var ix = (int)fx;
            var iz = (int)fz;
            var tx = fx - ix;
            var tz = fz - iz;
            var i = iz * res + ix;
            var a = grid[i] + (grid[i + 1] - grid[i]) * tx;
            var b = grid[i + res] + (grid[i + res + 1] - grid[i + res]) * tx;
            return a + (b - a) * tz;
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }

    /// <summary>Kaya çıkıntısı adayı (dik yamaç): konum, normal ve önerilen boyut.</summary>
    public struct OutcropSite
    {
        public float X, Y, Z;
        public float NormalX, NormalY, NormalZ;
        public float Size;
    }

    /// <summary>
    /// Saf (Unity'siz) hidrolik + ısıl aşındırma ve türev haritalar. Belirlenimci (System.Random(seed)); korunan hücreler
    /// (yol/dere/yerleşim/su) <c>protect</c> maskesiyle özgün yüksekliğe geri karıştırılır. Yükseklik dizisi YERİNDE değişir.
    /// </summary>
    public static class TerrainErosion
    {
        /// <summary>Aşındırır; yükseklik dizisini günceller ve türev haritaları döner. protect: 1 = dokunma, 0 = serbest (null = hepsi serbest).</summary>
        public static TerrainErosionMaps Erode(float[] heights, int res, float cell, float originX, float originZ, float[] protect, int seed,
            TerrainErosionSettings s)
        {
            s ??= new TerrainErosionSettings();
            var maps = new TerrainErosionMaps { Resolution = res, CellSize = cell, OriginX = originX, OriginZ = originZ };
            var n = res * res;
            var flow = new float[n];
            if (s.Strength > 0f && res >= 16)
            {
                var original = (float[])heights.Clone();
                Hydraulic(heights, flow, res, cell, seed, s);
                if (s.ThermalIterations > 0)
                    Thermal(heights, res, cell, protect, s);
                ConstrainAndBlend(heights, original, protect, s.MaxDelta, s.Strength);
            }

            maps.Flow = NormalizeFlow(flow, res);
            BuildDerived(heights, res, cell, maps);
            return maps;
        }

        // ================================================================== Hidrolik (damla tabanlı)

        private static void Hydraulic(float[] h, float[] flow, int res, float cell, int seed, TerrainErosionSettings s)
        {
            var rng = new Random(seed * 7919 + 104729);
            var count = (int)Math.Min(s.MaxDroplets, (long)res * res * (double)s.DropletsPerCell);
            var maxCoord = res - 2.001f;
            for (var d = 0; d < count; d++)
            {
                float px = 2f + (float)rng.NextDouble() * (res - 5f);
                float pz = 2f + (float)rng.NextDouble() * (res - 5f);
                float dx = 0f, dz = 0f, speed = 1f, water = 1f, sediment = 0f;

                for (var step = 0; step < s.MaxSteps; step++)
                {
                    var ix = (int)px;
                    var iz = (int)pz;
                    var fx = px - ix;
                    var fz = pz - iz;
                    var idx = iz * res + ix;

                    float hA = h[idx], hB = h[idx + 1], hC = h[idx + res], hD = h[idx + res + 1];
                    var gx = (hB - hA) * (1f - fz) + (hD - hC) * fz;
                    var gz = (hC - hA) * (1f - fx) + (hD - hB) * fx;
                    var height = hA * (1f - fx) * (1f - fz) + hB * fx * (1f - fz) + hC * (1f - fx) * fz + hD * fx * fz;

                    dx = dx * s.Inertia - gx * (1f - s.Inertia);
                    dz = dz * s.Inertia - gz * (1f - s.Inertia);
                    var len = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (len < 1e-5f)
                        break;
                    dx /= len;
                    dz /= len;

                    flow[idx] += water;

                    var nx = px + dx;
                    var nz = pz + dz;
                    if (nx < 1f || nx > maxCoord || nz < 1f || nz > maxCoord)
                        break;

                    var nix = (int)nx;
                    var niz = (int)nz;
                    var nfx = nx - nix;
                    var nfz = nz - niz;
                    var nidx = niz * res + nix;
                    var newHeight = h[nidx] * (1f - nfx) * (1f - nfz) + h[nidx + 1] * nfx * (1f - nfz)
                                    + h[nidx + res] * (1f - nfx) * nfz + h[nidx + res + 1] * nfx * nfz;
                    var dh = newHeight - height;

                    var capacity = Math.Max(-dh, s.MinCapacity) * speed * water * s.CapacityFactor;
                    float delta;
                    if (sediment > capacity || dh > 0f)
                    {
                        // Biriktir: yokuş yukarıysa çukuru doldur, değilse fazlalığın bir kısmını bırak.
                        delta = dh > 0f ? Math.Min(dh, sediment) : (sediment - capacity) * s.DepositSpeed;
                        sediment -= delta;
                        Splat(h, idx, res, fx, fz, delta);
                    }
                    else
                    {
                        delta = Math.Min((capacity - sediment) * s.ErodeSpeed, Math.Min(-dh, s.MaxErodePerStep));
                        sediment += delta;
                        Splat(h, idx, res, fx, fz, -delta);
                    }

                    speed = (float)Math.Sqrt(Math.Max(0f, speed * speed + -dh * s.Gravity));
                    water *= 1f - s.Evaporate;
                    px = nx;
                    pz = nz;
                }
            }
        }

        private static void Splat(float[] h, int idx, int res, float fx, float fz, float amount)
        {
            h[idx] += amount * (1f - fx) * (1f - fz);
            h[idx + 1] += amount * fx * (1f - fz);
            h[idx + res] += amount * (1f - fx) * fz;
            h[idx + res + 1] += amount * fx * fz;
        }

        // ================================================================== Isıl (talus)

        private static void Thermal(float[] h, int res, float cell, float[] protect, TerrainErosionSettings s)
        {
            var talus = (float)Math.Tan(s.TalusDegrees * Math.PI / 180.0) * cell;
            var delta = new float[h.Length];
            for (var it = 0; it < s.ThermalIterations; it++)
            {
                Array.Clear(delta, 0, delta.Length);
                for (var z = 1; z < res - 1; z++)
                {
                    var row = z * res;
                    for (var x = 1; x < res - 1; x++)
                    {
                        var i = row + x;
                        var free = protect == null ? 1f : 1f - protect[i];
                        if (free <= 0.01f)
                            continue;
                        var hc = h[i];
                        // En düşük komşuya, talus'u aşan farkın bir kısmını taşı.
                        var best = -1;
                        var bestDiff = talus;
                        var diff = hc - h[i - 1]; if (diff > bestDiff) { bestDiff = diff; best = i - 1; }
                        diff = hc - h[i + 1]; if (diff > bestDiff) { bestDiff = diff; best = i + 1; }
                        diff = hc - h[i - res]; if (diff > bestDiff) { bestDiff = diff; best = i - res; }
                        diff = hc - h[i + res]; if (diff > bestDiff) { bestDiff = diff; best = i + res; }
                        if (best < 0)
                            continue;
                        var move = (bestDiff - talus) * 0.25f * free;
                        delta[i] -= move;
                        delta[best] += move;
                    }
                }

                for (var i = 0; i < h.Length; i++)
                    h[i] += delta[i];
            }
        }

        // ================================================================== Sınırla / koru

        private static void ConstrainAndBlend(float[] h, float[] original, float[] protect, float maxDelta, float strength)
        {
            var strong = strength < 1f ? strength : 1f;
            for (var i = 0; i < h.Length; i++)
            {
                var d = h[i] - original[i];
                if (d > maxDelta) d = maxDelta;
                else if (d < -maxDelta) d = -maxDelta;
                var free = protect == null ? 1f : 1f - protect[i];
                if (free < 0f) free = 0f;
                h[i] = original[i] + d * free * strong;
            }
        }

        // ================================================================== Türev haritalar

        /// <summary>Akış sayacını [0,1] ıslaklığa çevirir (log eğrisi + 1 geçiş bulanıklaştırma).</summary>
        public static float[] NormalizeFlow(float[] counts, int res)
        {
            var o = new float[counts.Length];
            for (var i = 0; i < counts.Length; i++)
                o[i] = FlowCurve(counts[i]);
            return Blur(o, res, 1);
        }

        /// <summary>Ziyaret ağırlığı → [0,1]: 1 − exp(−c/6).</summary>
        public static float FlowCurve(float count)
        {
            if (count <= 0f)
                return 0f;
            return 1f - (float)Math.Exp(-count / 6.0);
        }

        /// <summary>Eğrilik + uçurum + scree haritalarını üretir (maps.Flow dolu olmalı).</summary>
        public static void BuildDerived(float[] h, int res, float cell, TerrainErosionMaps maps)
        {
            var n = res * res;
            var curv = new float[n];
            var cliffRaw = new float[n];
            var slopeDeg = new float[n];
            var k = Math.Max(1, (int)Math.Round(4.0 / Math.Max(0.5, cell))); // ~4 m'lik ofset
            for (var z = 0; z < res; z++)
            {
                for (var x = 0; x < res; x++)
                {
                    var i = z * res + x;
                    var xl = x - 1 < 0 ? 0 : x - 1;
                    var xr = x + 1 >= res ? res - 1 : x + 1;
                    var zd = z - 1 < 0 ? 0 : z - 1;
                    var zu = z + 1 >= res ? res - 1 : z + 1;
                    var gx = (h[z * res + xr] - h[z * res + xl]) / ((xr - xl) * cell);
                    var gz = (h[zu * res + x] - h[zd * res + x]) / ((zu - zd) * cell);
                    var s = (float)(Math.Atan(Math.Sqrt(gx * gx + gz * gz)) * 180.0 / Math.PI);
                    slopeDeg[i] = s;
                    cliffRaw[i] = CliffWeight(s);

                    var x2l = x - k < 0 ? 0 : x - k;
                    var x2r = x + k >= res ? res - 1 : x + k;
                    var z2d = z - k < 0 ? 0 : z - k;
                    var z2u = z + k >= res ? res - 1 : z + k;
                    var lap = h[z * res + x2l] + h[z * res + x2r] + h[z2d * res + x] + h[z2u * res + x] - 4f * h[i];
                    var step = k * cell;
                    curv[i] = CurvatureNormalize(-lap / (step * step));
                }
            }

            maps.Curvature = Blur(curv, res, 1);
            var cliffBlur = Blur(cliffRaw, res, Math.Max(2, (int)Math.Round(10.0 / Math.Max(0.5, cell))));
            maps.Cliff = cliffBlur;
            var scree = new float[n];
            for (var i = 0; i < n; i++)
                scree[i] = ScreeWeight(cliffBlur[i], cliffRaw[i], slopeDeg[i]);
            maps.Scree = Blur(scree, res, 1);
        }

        /// <summary>Eğim (derece) → uçurum [0,1].</summary>
        public static float CliffWeight(float slopeDegrees)
        {
            return SmoothStep(34f, 46f, slopeDegrees);
        }

        /// <summary>Uçurum dibi döküntü: yakında uçurum var, kendisi uçurum değil, eğimi 8-32° arası.</summary>
        public static float ScreeWeight(float cliffNearby, float cliffHere, float slopeDegrees)
        {
            var nearby = SmoothStep(0.12f, 0.5f, cliffNearby);
            var notCliff = 1f - cliffHere;
            var band = SmoothStep(6f, 12f, slopeDegrees) * (1f - SmoothStep(30f, 38f, slopeDegrees));
            return nearby * notCliff * band;
        }

        /// <summary>Ham laplacian (1/m) → [-1,1].</summary>
        public static float CurvatureNormalize(float lapPerMeter2)
        {
            var v = lapPerMeter2 * 14f;
            return v < -1f ? -1f : (v > 1f ? 1f : v);
        }

        /// <summary>Yarıçaplı kutu bulanıklığı (ayrılabilir, kenar sıkıştırmalı).</summary>
        public static float[] Blur(float[] src, int res, int radius)
        {
            if (radius <= 0)
                return (float[])src.Clone();
            var tmp = new float[src.Length];
            var dst = new float[src.Length];
            var inv = 1f / (2 * radius + 1);
            for (var z = 0; z < res; z++)
            {
                var row = z * res;
                for (var x = 0; x < res; x++)
                {
                    var sum = 0f;
                    for (var o = -radius; o <= radius; o++)
                    {
                        var xx = x + o; xx = xx < 0 ? 0 : (xx >= res ? res - 1 : xx);
                        sum += src[row + xx];
                    }

                    tmp[row + x] = sum * inv;
                }
            }

            for (var x = 0; x < res; x++)
            {
                for (var z = 0; z < res; z++)
                {
                    var sum = 0f;
                    for (var o = -radius; o <= radius; o++)
                    {
                        var zz = z + o; zz = zz < 0 ? 0 : (zz >= res ? res - 1 : zz);
                        sum += tmp[zz * res + x];
                    }

                    dst[z * res + x] = sum * inv;
                }
            }

            return dst;
        }

        // ================================================================== Korunan alan maskesi

        /// <summary>
        /// Korunan alan değeri [0,1]: yerleşim düzlüğü, yol (kenara 18 m), dere/göl ve su çizgisinin hemen üstü, harita kenarı.
        /// Düzenlemeler yerleşim/yol/dere yerleşimini bozmasın diye tüm hücreler bu maskeyle özgün yüksekliğe karıştırılır.
        /// </summary>
        public static float[] BuildProtectMask(TerrainModel model)
        {
            var res = model.Resolution;
            var mask = new float[res * res];
            var water = model.Layout.WaterLevel;
            var riverHalf = model.RiverWaterHalfWidth;
            for (var z = 0; z < res; z++)
            {
                for (var x = 0; x < res; x++)
                {
                    var i = z * res + x;
                    mask[i] = ProtectValue(model.FlattenWeight[i], model.RoadEdge[i], model.RiverDistance[i], riverHalf,
                        model.Heights[i], water, Math.Min(Math.Min(x, z), Math.Min(res - 1 - x, res - 1 - z)));
                }
            }

            return Blur(mask, res, 1);
        }

        /// <summary>Tek hücre koruma değeri (saf, test edilebilir).</summary>
        public static float ProtectValue(float flatten, float roadEdge, float riverDistance, float riverHalf, float height, float waterLevel,
            int edgeCells)
        {
            var p = Math.Min(1f, flatten * 3f);
            if (!float.IsInfinity(roadEdge) && !float.IsNaN(roadEdge))
                p = Math.Max(p, 1f - SmoothStep(8f, 20f, roadEdge));
            p = Math.Max(p, 1f - SmoothStep(riverHalf + 3f, riverHalf + 16f, riverDistance));
            p = Math.Max(p, 1f - SmoothStep(waterLevel + 1.5f, waterLevel + 5f, height));
            p = Math.Max(p, 1f - SmoothStep(2f, 6f, edgeCells));
            return p < 0f ? 0f : (p > 1f ? 1f : p);
        }

        // ================================================================== Kaya çıkıntısı adayları

        /// <summary>
        /// Dik yamaçlardaki (uçurum) kaya çıkıntısı adaylarını belirlenimci olarak seçer. Yerleştirme çağıran tarafın işi
        /// (RockScatter/WorldGenerator bu listeyi okuyabilir). Korunan alanlar atlanır.
        /// </summary>
        public static List<OutcropSite> CollectOutcropSites(TerrainModel model, TerrainErosionMaps maps, float[] protect, int seed, int max)
        {
            var list = new List<OutcropSite>();
            if (model == null || maps == null || max <= 0)
                return list;
            var rng = new Random(seed * 31337 + 911);
            var res = model.Resolution;
            var attempts = max * 60;
            for (var a = 0; a < attempts && list.Count < max; a++)
            {
                var ix = 4 + rng.Next(res - 8);
                var iz = 4 + rng.Next(res - 8);
                var i = iz * res + ix;
                if (protect != null && protect[i] > 0.05f)
                    continue;
                var x = model.WorldX(ix);
                var z = model.WorldZ(iz);
                var n = model.SampleNormal(x, z);
                var slope = (float)(Math.Acos(Math.Max(-1f, Math.Min(1f, n.y))) * 180.0 / Math.PI);
                if (slope < 38f || slope > 70f)
                    continue;
                if (rng.NextDouble() > 0.35 + 0.65 * maps.Cliff[i])
                    continue;
                var size = 1.2f + (float)rng.NextDouble() * 2.2f;
                list.Add(new OutcropSite
                {
                    X = x, Y = model.SampleHeight(x, z), Z = z,
                    NormalX = n.x, NormalY = n.y, NormalZ = n.z,
                    Size = size
                });
            }

            return list;
        }

        public static float SmoothStep(float a, float b, float x)
        {
            if (b <= a)
                return x >= b ? 1f : 0f;
            var t = (x - a) / (b - a);
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return t * t * (3f - 2f * t);
        }
    }
}
