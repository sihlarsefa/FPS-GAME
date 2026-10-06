using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Arazi mikro-detay planlayıcısı: TerrainModel'den yol kenarı aşınma (çamur), çukur birikintisi, kaya çatlağı ve
    /// toprak-çim geçiş (yaprak/çamur) çıkartma yerleşimlerini üretir. Saf veri döner; çizim DecalScatter tarafında.
    /// Aynı tohum aynı sonucu verir.
    /// </summary>
    public static class TerrainMicroDetailPlanner
    {
        public static List<DecalPlacement> Plan(TerrainModel model, int tier, float wet01 = 0.3f)
        {
            var result = new List<DecalPlacement>();
            if (model == null) return result;
            var budget = TerrainMicroDetailMath.Budget(tier);
            if (budget[0] + budget[1] + budget[2] + budget[3] == 0) return result;
            var rng = new System.Random(model.Seed * 7919 + 13);
            var res = model.Resolution;
            var water = model.Layout.WaterLevel;

            // 1) Yol kenarı aşınma şeritleri (toprak yol kenarında çamur)
            PlanRoadWear(model, rng, budget[0], result);

            // 2) Çukur analizi -> su birikintileri (ıslaklıkla ölçeklenir)
            var puddleCount = Mathf.RoundToInt(budget[1] * Mathf.Lerp(0.5f, 1.2f, Mathf.Clamp01(wet01)));
            var pits = TerrainMicroDetailMath.FindPits(model.Heights, res, model.CellSize, model.OriginX, model.OriginZ,
                0.12f, water + 0.4f, 2, 2, puddleCount);
            foreach (var p in pits)
            {
                if (model.SampleSlope(p.X, p.Z) > 12f) continue;
                var size = Mathf.Clamp(p.Radius * 1.4f + p.Depth * 2f, 1.2f, 4.5f);
                result.Add(Make(model, DecalKind.Puddle, p.X, p.Z, (float)rng.NextDouble() * 360f, size, size * 0.8f,
                    Mathf.Clamp01(p.Depth * 3f), rng, 0f));
            }

            // 3) Kaya yarığı/çatlağı (dik yamaçlar)
            Scatter(model, rng, budget[2], 60, (x, z) =>
            {
                var n = model.Noise.Perlin(x * 0.05f, z * 0.05f);
                return TerrainMicroDetailMath.RockCrackScore(model.SampleSlope(x, z), n);
            }, DecalKind.Crack, 1.5f, 3.2f, result);

            // 4) Toprak-çim geçiş bandı: ağırlık, düşük eğim + gürültü ile (yaprak kırıntısı)
            Scatter(model, rng, budget[3], 60, (x, z) =>
            {
                if (model.SampleSlope(x, z) > 25f) return 0f;
                var w = 0.5f + 0.5f * model.Noise.Perlin(x * 0.02f + 31f, z * 0.02f - 17f);
                return TerrainMicroDetailMath.GrassSoilBand(w) > 0.8f ? 1f : 0f;
            }, DecalKind.Leaves, 1.6f, 3.0f, result);
            return result;
        }

        private static void PlanRoadWear(TerrainModel m, System.Random rng, int count, List<DecalPlacement> outList)
        {
            if (count <= 0) return;
            var placed = 0;
            for (var tries = 0; tries < count * 40 && placed < count; tries++)
            {
                var i = rng.Next(m.Resolution * m.Resolution);
                if (m.RoadKindMap[i] != 2) continue; // yalnız toprak yol
                var w = TerrainMicroDetailMath.RoadWear(m.RoadEdge[i], 2.5f);
                if (w < 0.35f || (float)rng.NextDouble() > w) continue;
                var x = m.OriginX + (i % m.Resolution) * m.CellSize;
                var z = m.OriginZ + (i / m.Resolution) * m.CellSize;
                outList.Add(Make(m, DecalKind.MudSplat, x, z, (float)rng.NextDouble() * 360f,
                    1.5f + (float)rng.NextDouble() * 1.5f, 1.5f + (float)rng.NextDouble() * 1.5f, w, rng, (float)placed / count));
                placed++;
            }
        }

        private static void Scatter(TerrainModel m, System.Random rng, int count, int perTry, System.Func<float, float, float> score,
            DecalKind kind, float minSize, float maxSize, List<DecalPlacement> outList)
        {
            var placed = 0;
            var half = m.Size * 0.5f - 8f;
            for (var tries = 0; tries < count * perTry && placed < count; tries++)
            {
                var x = ((float)rng.NextDouble() * 2f - 1f) * half;
                var z = ((float)rng.NextDouble() * 2f - 1f) * half;
                var s = score(x, z);
                if (s <= 0.05f || (float)rng.NextDouble() > s) continue;
                if (m.SampleHeight(x, z) < m.Layout.WaterLevel + 0.3f) continue;
                var size = minSize + (float)rng.NextDouble() * (maxSize - minSize);
                outList.Add(Make(m, kind, x, z, (float)rng.NextDouble() * 360f, size, size, s, rng, (float)placed / count));
                placed++;
            }
        }

        private static DecalPlacement Make(TerrainModel m, DecalKind kind, float x, float z, float yaw, float w, float h,
            float intensity, System.Random rng, float rank)
        {
            return new DecalPlacement
            {
                Kind = kind,
                Surface = DecalSurface.Ground,
                Variant = rng.Next(4),
                Position = new Vector3(x, m.SampleHeight(x, z), z),
                Yaw = yaw,
                Size = new Vector2(w, h),
                Depth = 0.6f,
                Rank01 = rank,
                Intensity = Mathf.Clamp01(intensity)
            };
        }
    }
}
