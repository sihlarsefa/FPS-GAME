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

            for (var az = 0; az < resolution; az++)
            {
                var z = model.OriginZ + az * step;
                for (var ax = 0; ax < resolution; ax++)
                {
                    var x = model.OriginX + ax * step;
                    ComputeWeights(model, layout, noise, x, z, waterLevel, riverHalf, w);
                    for (var l = 0; l < layers; l++)
                        maps[az, ax, l] = w[l];
                }
            }

            return maps;
        }

        /// <summary>Tek bir noktanın normalleştirilmiş katman ağırlıkları (weights uzunluğu ≥ LayerCount).</summary>
        public static void ComputeWeights(TerrainModel model, float x, float z, float[] weights)
        {
            ComputeWeights(model, model.Layout, model.Noise, x, z, model.Layout.WaterLevel, model.RiverWaterHalfWidth, weights);
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
            float riverHalf, float[] w)
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
            var dry = TerrainNoise.SmoothStep(0.3f, 0.5f, dryness);
            w[(int)TerrainLayerKind.Grass] = 1f - dry;
            w[(int)TerrainLayerKind.DryGrass] = dry;

            // ---------------------------------------------------------------- Toprak lekeleri
            var dirt = TerrainNoise.SmoothStep(0.62f, 0.85f, n2 * 0.5f + 0.5f) * 0.55f;
            dirt = Mathf.Max(dirt, TerrainNoise.SmoothStep(20f, 28f, slope) * 0.6f); // dik yamaç geçişi
            Over(w, TerrainLayerKind.Dirt, dirt);

            // ---------------------------------------------------------------- Bölge zemini
            ApplyLocationGround(layout, model, x, z, n2, w);

            // ---------------------------------------------------------------- Kaya
            var rock = TerrainNoise.SmoothStep(RockSlope - 4f, RockSlope + 6f, slope + n2 * 3f);
            rock = Mathf.Max(rock, TerrainNoise.SmoothStep(112f, 138f, h) * 0.4f * (n2 * 0.5f + 0.5f));
            Over(w, TerrainLayerKind.Rock, rock);

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

            // ---------------------------------------------------------------- Kar
            var snow = TerrainNoise.SmoothStep(SnowLine - 7f, SnowLine + 6f, h + n1 * 6f) * (1f - TerrainNoise.SmoothStep(40f, 52f, slope));
            Over(w, TerrainLayerKind.Snow, snow);

            // ---------------------------------------------------------------- Yollar
            var edge = model.SampleRoadEdge(x, z);
            if (!float.IsInfinity(edge))
            {
                var kind = model.SampleRoadKind(x, z);
                // Yol kenarı: ezilmiş toprak şeridi.
                var shoulder = 1f - TerrainNoise.SmoothStep(0.5f, 3.5f, edge);
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
                }
            }

            Normalize(w);
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
