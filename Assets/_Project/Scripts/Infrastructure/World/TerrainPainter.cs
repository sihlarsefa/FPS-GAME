using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Arazi boyama (splat/alphamap): eğim, yükseklik, dere, yol ve bölge maskelerinden <see cref="TerrainLayerKind"/>
    /// ağırlıkları. Kurallar: çim/kuru çim (yükseklik, güneye bakan yamaç, gürültü), toprak lekeleri ve yol kenarları,
    /// eğim &gt; ~30° kaya, dere/göl kıyısında çamur, ~135 m üstünde kar, toprak yollarda çakıl, ana yolda asfalt,
    /// bölgelerde türüne göre zemin (köy toprak, FOB/karakol çakıl, taş ocağı kaya+çakıl).
    /// </summary>
    public static class TerrainPainter
    {
        public const int DefaultAlphamapResolution = 512;

        /// <summary>Kar çizgisi (m).</summary>
        public const float SnowLine = 135f;

        /// <summary>Kaya eğimi eşiği (derece).</summary>
        public const float RockSlope = 30f;

        /// <summary>Alphamap üretir: [z, x, katman] (Unity TerrainData.SetAlphamaps düzeni).</summary>
        public static float[,,] ComputeAlphamaps(TerrainModel model, int resolution)
        {
            resolution = Mathf.Clamp(resolution, 16, 4096);
            var layers = TerrainTextureFactory.LayerCount;
            var maps = new float[resolution, resolution, layers];
            var w = new float[layers];
            var layout = model.Layout;
            var noise = model.Noise;
            var waterLevel = layout.WaterLevel;
            var riverHalf = model.RiverWaterHalfWidth;
            var step = model.Size / (resolution - 1);
            var plan = FieldPlan.Of(model);

            for (var az = 0; az < resolution; az++)
            {
                var z = model.OriginZ + az * step;
                for (var ax = 0; ax < resolution; ax++)
                {
                    var x = model.OriginX + ax * step;
                    ComputeWeights(model, layout, noise, x, z, waterLevel, riverHalf, w, plan);
                    for (var l = 0; l < layers; l++)
                        maps[az, ax, l] = w[l];
                }
            }

            return maps;
        }

        /// <summary>Tek bir noktanın normalleştirilmiş katman ağırlıkları (weights uzunluğu ≥ LayerCount).</summary>
        public static void ComputeWeights(TerrainModel model, float x, float z, float[] weights)
        {
            ComputeWeights(model, model.Layout, model.Noise, x, z, model.Layout.WaterLevel, model.RiverWaterHalfWidth, weights, FieldPlan.Of(model));
        }

        /// <summary>Noktadaki baskın katman.</summary>
        public static TerrainLayerKind DominantLayer(TerrainModel model, float x, float z, float[] scratch)
        {
            ComputeWeights(model, x, z, scratch);
            var best = 0;
            for (var i = 1; i < TerrainTextureFactory.LayerCount; i++)
            {
                if (scratch[i] > scratch[best])
                    best = i;
            }

            return (TerrainLayerKind)best;
        }

        private static void ComputeWeights(TerrainModel model, MapLayout layout, TerrainNoise noise, float x, float z, float waterLevel,
            float riverHalf, float[] w, FieldPlan plan)
        {
            for (var i = 0; i < w.Length; i++)
                w[i] = 0f;

            var h = model.SampleHeight(x, z);
            var normal = model.SampleNormal(x, z);
            var slope = Mathf.Acos(Mathf.Clamp(normal.y, -1f, 1f)) * Mathf.Rad2Deg;
            var riverDistance = model.SampleRiverDistance(x, z);
            var n1 = noise.Fbm(x / 70f + 13.1f, z / 70f - 4.7f, 3);
            var n2 = noise.Fbm(x / 19f - 8.3f, z / 19f + 21.9f, 2);
            var n3 = noise.Fbm(x / 150f - 31.7f, z / 150f + 12.3f, 3);

            // ---------------------------------------------------------------- Çim / kuru çim
            // Vadide çayır lekeleri (n3), yükseldikçe ve güneye bakan yamaçlarda kuru çim; dere kıyısı yeşil.
            var riverWet = 1f - TerrainNoise.SmoothStep(riverHalf + 4f, riverHalf + 45f, riverDistance);
            var dryness = 0.5f * TerrainNoise.SmoothStep(45f, 120f, h)
                          + 0.62f * (n3 * 0.5f + 0.5f)
                          + 0.1f * n1
                          + 0.2f * Mathf.Max(0f, -normal.z) // güneye bakan yamaç
                          - 0.45f * riverWet;
            dryness += TerrainPaintRules.MacroVariation(noise.Fbm(x / 420f + 57.3f, z / 420f - 91.1f, 2));
            // 50-200 m ölçekli ince renk bozulması: düzgün tek tonluluğu kırar.
            dryness += FieldDetailRules.ColorBreakup(noise.Fbm(x / 55f + 3.9f, z / 55f - 17.2f, 2), noise.Fbm(x / 180f - 44.4f, z / 180f + 8.8f, 2));
            var dry = TerrainNoise.SmoothStep(0.3f, 0.5f, dryness);
            w[(int)TerrainLayerKind.Grass] = 1f - dry;
            w[(int)TerrainLayerKind.DryGrass] = dry;

            // ---------------------------------------------------------------- Toprak lekeleri
            var dirt = TerrainNoise.SmoothStep(0.62f, 0.85f, n2 * 0.5f + 0.5f) * 0.55f;
            dirt = Mathf.Max(dirt, TerrainNoise.SmoothStep(20f, 28f, slope) * 0.6f); // dik yamaç geçişi
            Over(w, TerrainLayerKind.Dirt, dirt);

            // ---------------------------------------------------------------- Tarla parselleri + yerleşim çevresi ezilmiş çim
            if (plan != null)
                ApplyFieldGround(plan, x, z, n2, w);

            // ---------------------------------------------------------------- Bölge zemini
            ApplyLocationGround(layout, model, x, z, n2, w);

            // ---------------------------------------------------------------- Kaya
            var rock = TerrainNoise.SmoothStep(RockSlope - 4f, RockSlope + 6f, slope + n2 * 3f);
            rock = Mathf.Max(rock, TerrainNoise.SmoothStep(112f, 138f, h) * 0.4f * (n2 * 0.5f + 0.5f));
            Over(w, TerrainLayerKind.Rock, rock);
            Over(w, TerrainLayerKind.Rock, TerrainPaintRules.CliffRock(slope + n2 * 2f, h));

            // ---------------------------------------------------------------- Aşınma: sırt kayası, uçurum dibi döküntü, oluklarda çamur/çakıl
            var erosion = TerrainErosionMaps.Of(model);
            if (erosion != null)
            {
                var flow = erosion.Sample(erosion.Flow, x, z);
                var curv = erosion.Sample(erosion.Curvature, x, z);
                var scree = erosion.Sample(erosion.Scree, x, z);
                Over(w, TerrainLayerKind.Rock, TerrainPaintRules.RidgeRock(curv, slope));
                Over(w, TerrainLayerKind.Gravel, TerrainPaintRules.ScreeGravel(scree, n2));
                Over(w, TerrainLayerKind.Rock, TerrainPaintRules.ScreeRock(scree, n2));
                Over(w, TerrainLayerKind.Gravel, TerrainPaintRules.GullyGravel(flow, slope, curv));
                Over(w, TerrainLayerKind.Mud, TerrainPaintRules.GullyMud(flow, slope, curv));
            }

            // ---------------------------------------------------------------- Çamur (dere/göl kıyısı)
            var mud = 1f - TerrainNoise.SmoothStep(riverHalf + 0.5f, riverHalf + 5f + 2f * n2, riverDistance);
            mud = Mathf.Max(mud, 1f - TerrainNoise.SmoothStep(waterLevel + 0.2f, waterLevel + 1.6f, h));
            var lakes = layout.Lakes;
            for (var i = 0; i < lakes.Count; i++)
            {
                var lake = lakes[i];
                if (lake == null)
                    continue;
                var dx = x - lake.Center.x;
                var dz = z - lake.Center.y;
                var d = Mathf.Sqrt(dx * dx + dz * dz) / model.LakeShoreScale(i, dx, dz);
                mud = Mathf.Max(mud, 1f - TerrainNoise.SmoothStep(lake.Radius - 2f, lake.Radius + 6f, d));
            }

            Over(w, TerrainLayerKind.Mud, mud * 0.92f);

            // ---------------------------------------------------------------- Kumsal (kıyı haritaları)
            if (layout.BeachHeight > 0.01f)
            {
                var beach = BeachWeight(h, waterLevel, layout.BeachHeight, slope);
                if (beach > 0f)
                {
                    // Katman setinde kum dokusu yok: açık renkli kuru çim + çakıl karışımı kumsal görünümü verir.
                    Over(w, TerrainLayerKind.DryGrass, beach * 0.9f);
                    Over(w, TerrainLayerKind.Gravel, beach * (0.25f + 0.2f * n2));
                }
            }

            // ---------------------------------------------------------------- Kar
            Over(w, TerrainLayerKind.Rock, TerrainPaintRules.SnowExposedRock(h, layout.SnowLine, slope));
            Over(w, TerrainLayerKind.Snow, TerrainPaintRules.SlopeSnow(h, layout.SnowLine, slope, n1));

            // ---------------------------------------------------------------- Yollar
            var edge = model.SampleRoadEdge(x, z);
            if (!float.IsInfinity(edge))
            {
                var kind = model.SampleRoadKind(x, z);
                // Yol kenarı: ezilmiş toprak şeridi.
                // Omuz sınırı gürültüyle oynatılır: düz şerit yerine yumuşak, düzensiz geçiş (+ erozyonlu kenarlık).
                var softEdge = edge + n2 * 0.9f;
                var shoulder = 1f - TerrainNoise.SmoothStep(0.3f, 4.5f, softEdge);
                Over(w, TerrainLayerKind.Dirt, shoulder * 0.7f);
                var core = 1f - TerrainNoise.SmoothStep(-0.8f, 0.6f, edge);
                if (kind == 1)
                {
                    // Asfalt + çakıl banket.
                    Over(w, TerrainLayerKind.Gravel, 1f - TerrainNoise.SmoothStep(0.2f, 1.6f, edge));
                    Over(w, TerrainLayerKind.Asphalt, core);
                }
                else if (kind == 2)
                {
                    Over(w, TerrainLayerKind.Gravel, core * 0.92f);
                    if (plan != null)
                    {
                        // Toprak yol: ortada çim şeridi, iki yanda tekerlek izi.
                        FieldDetailRules.DirtRoadWear(edge, plan.DirtRoadHalfWidth, n2 * 0.5f + 0.5f, out var strip, out var rut);
                        Over(w, TerrainLayerKind.Mud, rut * 0.3f * core);
                        Over(w, TerrainLayerKind.DryGrass, strip * 0.55f * core);
                    }
                }
            }

            Normalize(w);
        }

        private static void ApplyFieldGround(FieldPlan plan, float x, float z, float n2, float[] w)
        {
            var trample = plan.TrampleAt(x, z, n2);
            if (trample > 0f)
            {
                Over(w, TerrainLayerKind.DryGrass, trample * 0.45f * (0.6f + 0.4f * (n2 * 0.5f + 0.5f)));
                Over(w, TerrainLayerKind.Dirt, trample * 0.18f * (1f - (n2 * 0.5f + 0.5f)));
            }

            var parcel = plan.Find(x, z, out var inside);
            if (parcel == null)
                return;
            var m = TerrainNoise.SmoothStep(-0.4f, 1.2f, inside);
            if (m <= 0f)
                return;
            parcel.ToLocal(x, z, out var lx, out _);
            var ridge = FieldDetailRules.RowRidge(lx);
            switch (parcel.Kind)
            {
                case FieldKind.Plowed:
                    Over(w, TerrainLayerKind.Dirt, m * 0.95f);
                    Over(w, TerrainLayerKind.Mud, m * 0.42f * (1f - ridge));
                    break;
                case FieldKind.Stubble:
                    Over(w, TerrainLayerKind.DryGrass, m * 0.9f);
                    Over(w, TerrainLayerKind.Dirt, m * 0.3f * (1f - ridge));
                    break;
                default:
                    Over(w, TerrainLayerKind.Grass, m * 0.5f);
                    break;
            }

            // Tarla başı: parsel kenarında çiğnenmiş toprak şeridi.
            var headland = (1f - TerrainNoise.SmoothStep(0.6f, 2.6f, inside)) * m;
            Over(w, TerrainLayerKind.Dirt, headland * 0.5f);
        }

        /// <summary>Kumsal ağırlığı [0,1]: su seviyesinin hemen üstündeki, dik olmayan şerit (yumuşak kenarlı).</summary>
        public static float BeachWeight(float height, float waterLevel, float beachHeight, float slopeDegrees)
        {
            if (beachHeight <= 0.01f)
                return 0f;
            var above = height - waterLevel;
            var band = 1f - TerrainNoise.SmoothStep(beachHeight * 0.6f, beachHeight, above);
            var wet = TerrainNoise.SmoothStep(-1.5f, -0.2f, above);
            return band * wet * (1f - TerrainNoise.SmoothStep(25f, 38f, slopeDegrees));
        }

        private static void ApplyLocationGround(MapLayout layout, TerrainModel model, float x, float z, float n2, float[] w)
        {
            var locations = layout.Locations;
            for (var i = 0; i < locations.Count; i++)
            {
                var l = locations[i];
                if (l == null || l.FlattenRadius <= 0f)
                    continue;

                var dx = x - l.Center.x;
                var dz = z - l.Center.y;
                var d = Mathf.Sqrt(dx * dx + dz * dz);
                var reach = l.FlattenRadius * 1.15f;
                if (d >= reach)
                    continue;

                var t = 1f - TerrainNoise.SmoothStep(l.FlattenRadius * 0.55f, reach, d + n2 * 6f);
                if (t <= 0f)
                    continue;

                switch (l.Kind)
                {
                    case LocationKind.Village:
                    case LocationKind.Ruins:
                        Over(w, TerrainLayerKind.Dirt, t * 0.6f);
                        break;
                    case LocationKind.Farm:
                        Over(w, TerrainLayerKind.Dirt, t * 0.45f);
                        Over(w, TerrainLayerKind.DryGrass, t * 0.25f);
                        break;
                    case LocationKind.ForwardBase:
                    case LocationKind.Karakol:
                    case LocationKind.RelayHill:
                    case LocationKind.Outpost:
                        Over(w, TerrainLayerKind.Dirt, t * 0.35f);
                        Over(w, TerrainLayerKind.Gravel, t * 0.55f);
                        break;
                    case LocationKind.Quarry:
                        Over(w, TerrainLayerKind.Gravel, t * 0.6f);
                        Over(w, TerrainLayerKind.Rock, t * 0.35f * (n2 * 0.5f + 0.5f));
                        break;
                    case LocationKind.Dam:
                        Over(w, TerrainLayerKind.Gravel, t * 0.5f);
                        break;
                }
            }
        }

        /// <summary>"Üstüne boya": mevcut ağırlıkları (1 - s) ile ölçekler, katmana s ekler.</summary>
        private static void Over(float[] w, TerrainLayerKind layer, float strength)
        {
            strength = Mathf.Clamp01(strength);
            if (strength <= 0f)
                return;
            var keep = 1f - strength;
            for (var i = 0; i < w.Length; i++)
                w[i] *= keep;
            w[(int)layer] += strength;
        }

        private static void Normalize(float[] w)
        {
            var sum = 0f;
            for (var i = 0; i < w.Length; i++)
                sum += w[i];
            if (sum <= 1e-5f)
            {
                for (var i = 0; i < w.Length; i++)
                    w[i] = 0f;
                w[0] = 1f;
                return;
            }

            var inv = 1f / sum;
            for (var i = 0; i < w.Length; i++)
                w[i] *= inv;
        }
    }
}

namespace Project.Infrastructure.World
{
    /// <summary>Arazi boyama saf kuralları (test edilebilir) ve kalite kademesi başına basemap/ayrıntı tablosu.</summary>
    public static partial class TerrainPaintRules
    {
        /// <summary>Makro (yüzlerce metrelik) çim/kuru çim varyasyonu gücü; 0 = kapalı.</summary>
        public static float MacroStrength = 0.1f;

        /// <summary>fBm değerinden [-MacroStrength, MacroStrength] aralığında makro kaydırma.</summary>
        public static float MacroVariation(float fbm)
        {
            return Mathf.Clamp(fbm, -1f, 1f) * MacroStrength;
        }

        /// <summary>Eğim (derece) → kaya ağırlığı [0,1]; RockSlope çevresinde yumuşak geçiş.</summary>
        public static float SlopeRock(float slopeDegrees)
        {
            return TerrainNoise.SmoothStep(TerrainPainter.RockSlope - 4f, TerrainPainter.RockSlope + 6f, slopeDegrees);
        }

        /// <summary>Yüksekliğe göre kar ağırlığı [0,1].</summary>
        public static float HeightSnow(float height, float snowLine)
        {
            return TerrainNoise.SmoothStep(snowLine - 7f, snowLine + 6f, height);
        }

        // Düşük, Orta, Yüksek, Ultra
        private static readonly float[] Basemap = { 160f, 260f, 380f, 520f };

        /// <summary>Kademe başına basemap mesafesi (m): daha uzakta düşük çözünürlüklü tek doku.</summary>
        public static float BasemapDistance(int level)
        {
            return Basemap[Mathf.Clamp(level, 0, 3)];
        }

        /// <summary>Kademe başına çimen/çiçek mesafesi (m); ayarlardaki çarpan uygulanır.</summary>
        public static float DetailDistance(int level, float userScale = 1f)
        {
            return Rendering.PerformanceProfile.DetailObjectDistance(level) * Mathf.Clamp(userScale, 0f, 2f);
        }

        /// <summary>Kademe başına çimen/çiçek yoğunluğu [0,1]; ayarlardaki çarpan uygulanır.</summary>
        public static float DetailDensity(int level, float userScale = 1f)
        {
            return Mathf.Clamp01(Rendering.PerformanceProfile.DetailDensityScale(level) * Mathf.Clamp(userScale, 0f, 2f));
        }

        /// <summary>Tüm etkin arazilere kademeyi uygular (null güvenli).</summary>
        public static void ApplyTier(Terrain terrain, int level, float detailScale = 1f)
        {
            if (terrain == null)
                return;
            terrain.basemapDistance = BasemapDistance(level);
            terrain.detailObjectDistance = DetailDistance(level, detailScale);
            terrain.detailObjectDensity = DetailDensity(level, detailScale);
        }
    }
}
