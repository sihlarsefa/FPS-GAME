using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Arazi ağacı yerleşimi: yoğunluk alanı (Çam Sırtı ormanı, kenar sırtları, vadi meşelikleri, dere kıyısı çalılıkları,
    /// harabe çevresinde kuru ağaçlar) × seyreltilmiş ızgara örneklemesi. Yollara, dereye, göle, köprülere, bölgelere ve
    /// harita kenarına ağaç konmaz. Belirlenimci (tohum).
    /// </summary>
    public static class TreeScatter
    {
        public const int DefaultTargetCount = 3500;

        /// <summary>Örnekleme ızgara aralığı (m) — ağaçlar arası en az ~1.6 m.</summary>
        public const float CandidateSpacing = 4f;

        private struct Candidate
        {
            public float X, Z, Density;
            public TreeKind Kind;
        }

        /// <summary>Ağaç örneklerini üretir (TerrainData.treeInstances için; konumlar 0..1 normalize).</summary>
        public static TreeInstance[] Scatter(TerrainModel model, Vector3 terrainSize, int seed, int targetCount = DefaultTargetCount)
        {
            if (model == null || targetCount <= 0)
                return new TreeInstance[0];

            var rng = new System.Random(seed * 7349 + 1931);
            var layout = model.Layout;
            var noise = model.Noise;
            var half = layout.HalfSize;
            var cells = Mathf.Max(1, Mathf.FloorToInt(half * 2f / CandidateSpacing));
            var candidates = new List<Candidate>(cells * cells / 6);
            var forest = layout.FindLocation(LocationKind.Forest);
            var ruins = layout.FindLocation(LocationKind.Ruins);
            var total = 0f;

            for (var cz = 0; cz < cells; cz++)
            {
                for (var cx = 0; cx < cells; cx++)
                {
                    var x = -half + (cx + 0.15f + 0.7f * (float)rng.NextDouble()) * CandidateSpacing;
                    var z = -half + (cz + 0.15f + 0.7f * (float)rng.NextDouble()) * CandidateSpacing;
                    var kindRoll = (float)rng.NextDouble();
                    var density = Density(model, noise, forest, ruins, x, z, kindRoll, out var kind);
                    if (density <= 0f)
                        continue;

                    candidates.Add(new Candidate { X = x, Z = z, Density = density, Kind = kind });
                    total += density;
                }
            }

            if (candidates.Count == 0 || total <= 0f)
                return new TreeInstance[0];

            var scale = targetCount / total;
            var result = new List<TreeInstance>(targetCount + 64);
            var size = terrainSize;
            for (var i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                var p = Mathf.Min(1f, c.Density * scale);
                if ((float)rng.NextDouble() >= p)
                    continue;
                if (result.Count >= targetCount * 1.08f)
                    break;

                var h = model.SampleHeight(c.X, c.Z);
                var heightScale = Range(rng, 0.78f, 1.25f);
                var widthScale = heightScale * Range(rng, 0.88f, 1.12f);
                if (c.Kind == TreeKind.Bush)
                {
                    heightScale = Range(rng, 0.7f, 1.4f);
                    widthScale = heightScale * Range(rng, 0.9f, 1.3f);
                }

                var color = TintFor(c.Kind, rng);
                result.Add(new TreeInstance
                {
                    prototypeIndex = (int)c.Kind,
                    position = new Vector3((c.X + half) / size.x, Mathf.Clamp01(h / Mathf.Max(1f, size.y)), (c.Z + half) / size.z),
                    heightScale = heightScale,
                    widthScale = widthScale,
                    rotation = Range(rng, 0f, Mathf.PI * 2f),
                    color = color,
                    lightmapColor = Color.white
                });
            }

            return result.ToArray();
        }

        /// <summary>Kalite kademesine (0 Düşük … 3 Ultra) göre hedef ağaç sayısı (performans bütçesi).</summary>
        public static int TargetCountForTier(int tier)
        {
            switch (Mathf.Clamp(tier, 0, 3))
            {
                case 0: return 1400;
                case 1: return 2400;
                case 2: return 3200;
                default: return DefaultTargetCount;
            }
        }

        /// <summary>Örnek rengi: tür başına ton/parlaklık çeşitliliği (meşe sarıya, çam maviye, kuru ağaç griye kayar).</summary>
        internal static Color TintFor(TreeKind kind, System.Random rng)
        {
            var v = Range(rng, 0.82f, 1.02f);
            switch (kind)
            {
                case TreeKind.Oak:
                    var warm = Range(rng, 0f, 0.14f);
                    return new Color(v, v * (1f - warm * 0.15f), v * (1f - warm), 1f);
                case TreeKind.PineA:
                case TreeKind.PineB:
                    var cool = Range(rng, 0f, 0.08f);
                    return new Color(v * (1f - cool), v, v * (1f - cool * 0.3f), 1f);
                case TreeKind.Dead:
                    return new Color(v, v * 0.97f, v * 0.94f, 1f);
                default:
                    var dry = Range(rng, 0f, 0.18f);
                    return new Color(v, v * (1f - dry * 0.2f), v * (1f - dry), 1f);
            }
        }

        /// <summary>
        /// Verilen sınır kutularıyla (XZ, margin kadar genişletilmiş) çakışan ağaçları araziden kaldırır. Kaldırılan sayıyı döner.
        /// </summary>
        public static int RemoveTreesInBounds(Terrain terrain, IReadOnlyList<Bounds> bounds, float margin)
        {
            if (terrain == null || terrain.terrainData == null || bounds == null || bounds.Count == 0)
                return 0;

            var data = terrain.terrainData;
            var instances = data.treeInstances;
            if (instances == null || instances.Length == 0)
                return 0;

            var size = data.size;
            var origin = terrain.transform.position;
            var kept = new List<TreeInstance>(instances.Length);
            var removed = 0;
            for (var i = 0; i < instances.Length; i++)
            {
                var t = instances[i];
                var wx = origin.x + t.position.x * size.x;
                var wz = origin.z + t.position.z * size.z;
                var blocked = false;
                for (var b = 0; b < bounds.Count; b++)
                {
                    var box = bounds[b];
                    if (box.size.x <= 0.01f && box.size.z <= 0.01f)
                        continue;
                    if (wx >= box.min.x - margin && wx <= box.max.x + margin && wz >= box.min.z - margin && wz <= box.max.z + margin)
                    {
                        blocked = true;
                        break;
                    }
                }

                if (blocked)
                    removed++;
                else
                    kept.Add(t);
            }

            if (removed > 0)
            {
                data.SetTreeInstances(kept.ToArray(), false);
                WorldAssetPersistence.MarkDirty(data);
            }

            return removed;
        }

        // ================================================================== Yoğunluk

        private static float Density(TerrainModel model, TerrainNoise noise, LocationSpec forest, LocationSpec ruins, float x, float z,
            float kindRoll, out TreeKind kind)
        {
            kind = TreeKind.PineA;
            if (!model.IsClearOfFeatures(x, z, 3.5f, 2.5f, 1f))
                return 0f;

            var edge = model.EdgeDistance(x, z);
            if (edge < 6f)
                return 0f;

            var h = model.SampleHeight(x, z);
            if (h < model.Layout.WaterLevel + 0.8f)
                return 0f;

            var slope = model.SampleSlope(x, z);
            if (slope > 41f)
                return 0f;

            var flatten = model.SampleFlatten(x, z);
            if (flatten > 0.6f)
                return 0f;

            var riverDistance = model.SampleRiverDistance(x, z);
            var patch = noise.Fbm(x / 85f + 41.3f, z / 85f - 17.9f, 3) * 0.5f + 0.5f;     // korular
            var fine = noise.Fbm(x / 23f - 5.1f, z / 23f + 9.4f, 2) * 0.5f + 0.5f;         // açıklıklar
            var slopeFactor = 1f - TerrainNoise.SmoothStep(32f, 41f, slope);

            // ---------------------------------------------------------------- Çam Sırtı ormanı
            if (forest != null)
            {
                var dx = x - forest.Center.x;
                var dz = z - forest.Center.y;
                var d = Mathf.Sqrt(dx * dx + dz * dz);
                var forestT = 1f - TerrainNoise.SmoothStep(forest.Radius * 0.75f, forest.Radius * 1.45f, d + (fine - 0.5f) * 30f);
                if (forestT > 0.05f)
                {
                    var forestEdge = TreeScatterRules.EdgeWeight(forestT);
                    kind = kindRoll < TreeScatterRules.BushChance(forestEdge) * 0.7f ? TreeKind.Bush
                        : (kindRoll < 0.93f ? (kindRoll < 0.5f ? TreeKind.PineA : TreeKind.PineB) : TreeKind.Dead);
                    // Orman içinde birkaç küçük açıklık.
                    var clearing = TerrainNoise.SmoothStep(0.72f, 0.82f, fine);
                    return 1.35f * forestT * slopeFactor * (1f - clearing * 0.85f);
                }
            }

            // ---------------------------------------------------------------- Kar çizgisi üstü: seyrek kuru ağaç
            var snowLine = model.Layout.SnowLine;
            if (h > snowLine - 4f)
            {
                kind = TreeKind.Dead;
                return h > snowLine + 6f ? 0f : 0.015f * slopeFactor;
            }

            // ---------------------------------------------------------------- Harabe çevresi: kuru ağaçlar
            if (ruins != null)
            {
                var dx = x - ruins.Center.x;
                var dz = z - ruins.Center.y;
                var d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < ruins.Radius * 2f && kindRoll < 0.5f)
                {
                    kind = kindRoll < 0.25f ? TreeKind.Dead : TreeKind.Bush;
                    return 0.05f * slopeFactor;
                }
            }

            // ---------------------------------------------------------------- Dere kıyısı (çalı + meşe)
            var riverHalf = model.RiverWaterHalfWidth;
            if (riverDistance < riverHalf + 26f)
            {
                var bank = TerrainNoise.SmoothStep(riverHalf + 3f, riverHalf + 7f, riverDistance)
                           * (1f - TerrainNoise.SmoothStep(riverHalf + 16f, riverHalf + 26f, riverDistance));
                kind = kindRoll < 0.55f ? TreeKind.Bush : (kindRoll < 0.93f ? TreeKind.Oak : TreeKind.Dead);
                return 0.22f * bank * slopeFactor * (0.4f + patch);
            }

            // ---------------------------------------------------------------- Sırtlar / vadi: kümeli koru + açıklık + kenar sırası
            var mask = TreeScatterRules.StandMask(patch, fine);
            var edgeW = TreeScatterRules.EdgeWeight(mask);
            var roadLine = TreeScatterRules.RoadLineBoost(model.SampleRoadEdge(x, z));
            var ridgeT = TerrainNoise.SmoothStep(52f, 78f, h);
            kind = TreeScatterRules.PickKind(kindRoll, h, Mathf.Max(edgeW, roadLine > 1.05f ? 0.6f : 0f));
            var stand = (0.016f + 0.2f * mask) * (0.5f + fine) * (1f + 0.9f * edgeW);
            var ridgeBoost = 1f + 0.5f * ridgeT;
            return stand * ridgeBoost * roadLine * slopeFactor;
        }

        // ================================================================== Çeşitlilik (VegetationVariety)

        /// <summary>Kademeye göre çeşitlilik ağacı/çalı hedef sayısı (0 Düşük … 3 Ultra).</summary>
        public static int VarietyCountForTier(int tier)
        {
            switch (Mathf.Clamp(tier, 0, 3))
            {
                case 0: return 120;
                case 1: return 280;
                case 2: return 450;
                default: return 650;
            }
        }

        /// <summary>
        /// Biyom kuralı (saf): kavak su kenari/vadi, bodur mese kuru yamac, calilar orman kenari, ince cam orman ici.
        /// Agirlik 0 ise o noktaya tur konmaz. forestT: 0..1 orman ici degeri; dryness01: 0 nemli .. 1 kuru.
        /// </summary>
        public static float VarietyWeight(float riverDistance, float riverHalf, float heightAboveWater, float slope, float forestT,
            float dryness01, float roll, out VarietyKind kind)
        {
            kind = VarietyKind.ShrubRound;
            var nearWater = riverDistance < riverHalf + 30f || heightAboveWater < 6f;
            if (nearWater && forestT < 0.5f)
            {
                if (roll < 0.7f)
                {
                    kind = VarietyKind.Poplar;
                    return 1f;
                }

                kind = VarietyKind.ShrubRound;
                return 0.6f;
            }

            if (forestT > 0.55f)
            {
                kind = VarietyKind.PineSlim;
                return roll < 0.2f ? 0.7f : 0f;
            }

            if (forestT > 0.05f)
            {
                kind = roll < 0.65f ? VarietyKind.ShrubRound : VarietyKind.ShrubSparse;
                return 1.2f;
            }

            if (slope >= 12f && slope <= 36f && dryness01 > 0.35f)
            {
                if (roll < 0.6f)
                {
                    kind = VarietyKind.DwarfOak;
                    return 0.9f * dryness01;
                }

                kind = VarietyKind.ShrubSparse;
                return 0.5f * dryness01;
            }

            return 0f;
        }

        /// <summary>
        /// Çeşitlilik türlerini (5 prototip) araziye ekler: prototip indeksi = mevcut sayı + VarietyKind. Kalıcı (editör varlığı)
        /// arazi verisine dokunmaz. Eklenen örnek sayısını döner.
        /// </summary>
        public static int AddVariety(Terrain terrain, TerrainModel model, int seed, int tier, Transform holderParent)
        {
            if (terrain == null || model == null || terrain.terrainData == null)
                return 0;
            var data = terrain.terrainData;
            if (WorldAssetPersistence.ShouldPersist(data))
                return 0;
            var target = VarietyCountForTier(tier);
            if (target <= 0)
                return 0;

            var layout = model.Layout;
            var noise = model.Noise;
            var half = layout.HalfSize;
            var forest = layout.FindLocation(LocationKind.Forest);
            var rng = new System.Random(seed * 9173 + 4409);
            const float spacing = 6f;
            var cells = Mathf.Max(1, Mathf.FloorToInt(half * 2f / spacing));
            var cand = new List<Candidate>(2048);
            var kinds = new List<VarietyKind>(2048);
            var total = 0f;
            var riverHalf = model.RiverWaterHalfWidth;
            for (var cz = 0; cz < cells; cz++)
            {
                for (var cx = 0; cx < cells; cx++)
                {
                    var x = -half + (cx + 0.15f + 0.7f * (float)rng.NextDouble()) * spacing;
                    var z = -half + (cz + 0.15f + 0.7f * (float)rng.NextDouble()) * spacing;
                    var roll = (float)rng.NextDouble();
                    if (!model.IsClearOfFeatures(x, z, 3.5f, 2.5f, 1f) || model.EdgeDistance(x, z) < 6f)
                        continue;
                    var h = model.SampleHeight(x, z);
                    if (h < layout.WaterLevel + 0.8f || h > layout.SnowLine - 6f || model.SampleFlatten(x, z) > 0.6f)
                        continue;
                    var slope = model.SampleSlope(x, z);
                    if (slope > 38f)
                        continue;
                    var forestT = 0f;
                    if (forest != null)
                    {
                        var dx = x - forest.Center.x;
                        var dz = z - forest.Center.y;
                        var d = Mathf.Sqrt(dx * dx + dz * dz);
                        forestT = 1f - TerrainNoise.SmoothStep(forest.Radius * 0.75f, forest.Radius * 1.45f, d);
                    }

                    var dry = noise.Fbm(x / 70f + 13.7f, z / 70f - 3.3f, 2) * 0.5f + 0.5f;
                    var w = VarietyWeight(model.SampleRiverDistance(x, z), riverHalf, h - layout.WaterLevel, slope, forestT, dry, roll, out var kind);
                    if (w <= 0f)
                        continue;
                    cand.Add(new Candidate { X = x, Z = z, Density = w });
                    kinds.Add(kind);
                    total += w;
                }
            }

            if (cand.Count == 0 || total <= 0f)
                return 0;

            var baseProtos = data.treePrototypes ?? new TreePrototype[0];
            var baseCount = baseProtos.Length;
            var holder = new GameObject("[Çeşitlilik Prototipleri]");
            if (holderParent != null)
                holder.transform.SetParent(holderParent, false);
            holder.transform.position = new Vector3(0f, -2000f, 0f);
            var protos = new TreePrototype[baseCount + VegetationVariety.KindCount];
            System.Array.Copy(baseProtos, protos, baseCount);
            for (var k = 0; k < VegetationVariety.KindCount; k++)
            {
                var kind = (VarietyKind)k;
                var go = VegetationVariety.CreatePrototype(kind, holder.transform, seed + 31 * (k + 1), tier);
                protos[baseCount + k] = new TreePrototype { prefab = go, bendFactor = VegetationVariety.WindFor(kind, 2).Bend };
            }

            data.treePrototypes = protos;
            data.RefreshPrototypes();

            var size = data.size;
            var scale = target / total;
            var list = new List<TreeInstance>(data.treeInstances ?? new TreeInstance[0]);
            var added = 0;
            for (var i = 0; i < cand.Count && added < target; i++)
            {
                var c = cand[i];
                if ((float)rng.NextDouble() >= Mathf.Min(1f, c.Density * scale))
                    continue;
                var kind = kinds[i];
                var hs = Range(rng, 0.8f, 1.25f);
                var tint = TintFor(kind == VarietyKind.DwarfOak ? TreeKind.Oak : kind == VarietyKind.PineSlim ? TreeKind.PineA : TreeKind.Bush, rng);
                list.Add(new TreeInstance
                {
                    prototypeIndex = baseCount + (int)kind,
                    position = new Vector3((c.X + half) / size.x, Mathf.Clamp01(model.SampleHeight(c.X, c.Z) / Mathf.Max(1f, size.y)), (c.Z + half) / size.z),
                    heightScale = hs,
                    widthScale = hs * Range(rng, 0.9f, 1.15f),
                    rotation = Range(rng, 0f, Mathf.PI * 2f),
                    color = tint,
                    lightmapColor = Color.white
                });
                added++;
            }

            data.SetTreeInstances(list.ToArray(), false);
            return added;
        }

        /// <summary>Orman maskesi (0..1) — kütük/prop yerleşimi için dış kullanım.</summary>
        public static float StandMaskAt(TerrainModel model, float x, float z)
        {
            var noise = model.Noise;
            var patch = noise.Fbm(x / 85f + 41.3f, z / 85f - 17.9f, 3) * 0.5f + 0.5f;
            var fine = noise.Fbm(x / 23f - 5.1f, z / 23f + 9.4f, 2) * 0.5f + 0.5f;
            return TreeScatterRules.StandMask(patch, fine);
        }

        public struct LogSpot
        {
            public Vector3 Position;
            public float Yaw;
            public float Length;
        }

        /// <summary>
        /// Orman içi düşmüş kütük noktaları (~36 m hücre). ENTEGRASYON: prop üretimi (PropFactory/MicroPoi "kütük") bu listeyi tüketir.
        /// Belirlenimci; serbest alan + eğim kontrolü yapılır.
        /// </summary>
        public static System.Collections.Generic.List<LogSpot> ScatterLogSpots(TerrainModel model, int seed, int maxCount = 160)
        {
            var list = new System.Collections.Generic.List<LogSpot>();
            if (model == null)
                return list;
            var rng = new System.Random(seed * 4421 + 977);
            var half = model.Layout.HalfSize;
            const float cell = 36f;
            var n = Mathf.Max(1, Mathf.FloorToInt(half * 2f / cell));
            for (var cz = 0; cz < n && list.Count < maxCount; cz++)
            {
                for (var cx = 0; cx < n && list.Count < maxCount; cx++)
                {
                    var x = -half + (cx + (float)rng.NextDouble()) * cell;
                    var z = -half + (cz + (float)rng.NextDouble()) * cell;
                    var roll = (float)rng.NextDouble();
                    var yaw = (float)rng.NextDouble() * 360f;
                    var len = Range(rng, 2.6f, 5.2f);
                    if (!model.IsClearOfFeatures(x, z, 5f, 4f, 1f) || model.SampleSlope(x, z) > 24f)
                        continue;
                    if (model.SampleHeight(x, z) < model.Layout.WaterLevel + 1.2f)
                        continue;
                    if (!TreeScatterRules.AcceptsLog(StandMaskAt(model, x, z), roll))
                        continue;
                    list.Add(new LogSpot { Position = new Vector3(x, model.SampleHeight(x, z), z), Yaw = yaw, Length = len });
                }
            }

            return list;
        }

        private static float Range(System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }
    }
}
