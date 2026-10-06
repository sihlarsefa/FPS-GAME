using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Arazi renk haritası saf matematiği (test edilebilir). Çıktı: RGB çarpan (nötr = 1) + AO [0,1].
    /// Doku kodlaması: rgb = tint * 0.5 (nötr = 0.5), a = ao. Shader: tint = rgb * 2.
    /// </summary>
    public static class TerrainColorMapMath
    {
        public const float TintMin = 0.55f;
        public const float TintMax = 1.45f;
        public const float AoMin = 0.55f;

        /// <summary>Tek nokta rengi/AO. macro, patch ≈ [-1,1] gürültü; curvature + dışbükey; flow/scree/cliff [0,1]; edge/roadKind yol bilgisi.</summary>
        public static void Evaluate(float macro, float macro2, float patch, float curvature, float flow, float scree, float cliff,
            float roadEdge, int roadKind, float rutNoise, out float r, out float g, out float b, out float ao)
        {
            Evaluate(macro, macro2, patch, curvature, flow, scree, cliff, roadEdge, roadKind, rutNoise, 0f, 0f, 0f, 0f,
                out r, out g, out b, out ao);
        }

        /// <summary>Makro yama (saman/nemli çukur) + gölge toprağı benekleri dahil; straw/moist [-1,1], canopy/freckle [0,1].</summary>
        public static void Evaluate(float macro, float macro2, float patch, float curvature, float flow, float scree, float cliff,
            float roadEdge, int roadKind, float rutNoise, float straw, float moist, float canopy, float freckle,
            out float r, out float g, out float b, out float ao)
        {
            var bright = 1f + 0.16f * macro + 0.07f * patch;
            r = bright * (1f + 0.055f * macro2);
            g = bright * (1f + 0.012f * macro2);
            b = bright * (1f - 0.06f * macro2);

            // Islak oluk: koyu.
            var wet = 1f - 0.3f * flow;
            // Dışbükey sırt: toz/aşınma ile hafif açık; içbükey: koyu.
            var curvBright = curvature >= 0f ? 1f + 0.07f * curvature : 1f + 0.12f * curvature;
            var screeBright = 1f + 0.12f * scree;
            var k = wet * curvBright * screeBright;
            r *= k * (1f + 0.02f * scree);
            g *= k;
            b *= k * (1f - 0.02f * scree);

            ao = 1f - 0.32f * Mathf.Max(0f, -curvature) - 0.12f * cliff - 0.1f * flow;

            // Yol: kenar yumuşatma ve lastik izi koyulaşması.
            // Büyük ölçekli yama/ton (yol ve kaya/uçurum korunur).
            var onRoad = !float.IsInfinity(roadEdge) && !float.IsNaN(roadEdge) && roadKind > 0 && roadEdge < 3f;
            var protect = onRoad ? 0f : TerrainMacroVariation.Protection(cliff, scree);
            TerrainMacroVariation.Evaluate(straw, moist, canopy, freckle, protect, out var mb, out var mr, out var mg, out var mbl);
            r *= mb * mr; g *= mb * mg; b *= mb * mbl;

            var road = RoadDarkening(roadEdge, roadKind, rutNoise);
            r *= road; g *= road; b *= road * 1.01f;

            r = Mathf.Clamp(r, TintMin, TintMax);
            g = Mathf.Clamp(g, TintMin, TintMax);
            b = Mathf.Clamp(b, TintMin, TintMax);
            ao = Mathf.Clamp(ao, AoMin, 1f);
        }

        /// <summary>
        /// Yol koyulaşması çarpanı: toprak/çakıl yolda kenarın içinde lastik izi şeridi (kırık, gürültülü) + omuzda hafif ezilme;
        /// asfaltta çok hafif yağ lekesi. Yol dışında 1.
        /// </summary>
        public static float RoadDarkening(float edge, int kind, float rutNoise)
        {
            if (float.IsInfinity(edge) || float.IsNaN(edge) || kind <= 0)
                return 1f;
            var broken = Mathf.Clamp01(0.55f + 0.6f * rutNoise);
            if (kind == 2)
            {
                var rut = TerrainErosion.SmoothStep(-2.4f, -1.3f, edge) * (1f - TerrainErosion.SmoothStep(-0.9f, -0.1f, edge));
                var shoulder = (1f - TerrainErosion.SmoothStep(0.4f, 3.5f, edge)) * TerrainErosion.SmoothStep(-0.2f, 0.4f, edge);
                return 1f - 0.24f * rut * broken - 0.1f * shoulder;
            }

            var oil = TerrainErosion.SmoothStep(-2.0f, -1.0f, edge) * (1f - TerrainErosion.SmoothStep(-0.8f, 0f, edge));
            var verge = (1f - TerrainErosion.SmoothStep(0.2f, 2.5f, edge)) * TerrainErosion.SmoothStep(-0.2f, 0.3f, edge);
            return 1f - 0.07f * oil * broken - 0.08f * verge;
        }

        /// <summary>Kademe başına renk haritası gücü (tint, ao); 0 Düşük .. 3 Ultra.</summary>
        public static Vector2 TierStrength(int level)
        {
            switch (Mathf.Clamp(level, 0, 3))
            {
                case 0: return new Vector2(0.6f, 0f);
                case 1: return new Vector2(0.85f, 0.6f);
                default: return new Vector2(1f, 1f);
            }
        }

        /// <summary>[TintMin,TintMax] çarpanını [0,1] doku kanalına kodlar.</summary>
        public static float Encode(float tint) => Mathf.Clamp01(tint * 0.5f);
    }

    /// <summary>
    /// Arazi renk haritası: aşındırma türevleri + makro renk kaymasından pişirilen tek doku (200 m+ tekrarı kırar, oluk/sırt
    /// kararması, uçurum AO, yol lastik izi). Küresel olarak yayınlanır: _HarekatTerrainColorMap (RGBA), _HarekatTerrainColorMapRect
    /// (originX, originZ, 1/size, 0), _HarekatTerrainColorMapParams (tintGücü, aoGücü, solmaBaşı, solmaSonu).
    /// </summary>
    public static class TerrainColorMap
    {
        public const string MapProperty = "_HarekatTerrainColorMap";
        public const string RectProperty = "_HarekatTerrainColorMapRect";
        public const string ParamsProperty = "_HarekatTerrainColorMapParams";

        private static readonly int MapId = Shader.PropertyToID(MapProperty);
        private static readonly int RectId = Shader.PropertyToID(RectProperty);
        private static readonly int ParamsId = Shader.PropertyToID(ParamsProperty);

        /// <summary>Son pişirilen doku (editör varlık olarak kaydedebilir).</summary>
        public static Texture2D Current { get; private set; }

        private static Vector4 _rect;
        private static int _tier = 2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
            _tier = 2;
        }

        /// <summary>Dokuyu pişirir ve yayınlar (görsel olmayan modda çağrılmaz).</summary>
        public static Texture2D Bake(TerrainModel model, TerrainErosionMaps maps, int size)
        {
            if (model == null || maps == null)
                return null;
            size = Mathf.Clamp(size, 64, 2048);
            var noise = model.Noise;
            var pixels = new Color32[size * size];
            var step = model.Size / (size - 1);
            for (var py = 0; py < size; py++)
            {
                var z = model.OriginZ + py * step;
                for (var px = 0; px < size; px++)
                {
                    var x = model.OriginX + px * step;
                    var macro = noise.Fbm(x / 380f + 211.3f, z / 380f - 77.1f, 3);
                    var macro2 = noise.Fbm(x / 520f - 33.9f, z / 520f + 140.2f, 2);
                    var patch = noise.Fbm(x / 55f + 9.1f, z / 55f + 61.7f, 2);
                    var rutNoise = noise.Fbm(x / 7f + 3.3f, z / 7f - 19.4f, 2);
                    var straw = noise.Fbm(x / 110f + 301.7f, z / 110f - 12.9f, 3);
                    var moist = noise.Fbm(x / 70f - 88.4f, z / 70f + 245.3f, 3);
                    var canopy = Mathf.Clamp01(noise.Fbm(x / 28f + 17.7f, z / 28f + 53.1f, 2) * 0.5f + 0.5f);
                    var freckle = Mathf.Clamp01(noise.Fbm(x / 2.2f + 5.5f, z / 2.2f - 41.3f, 2) * 0.5f + 0.5f);
                    var curv = maps.Sample(maps.Curvature, x, z);
                    var flow = maps.Sample(maps.Flow, x, z);
                    var scree = maps.Sample(maps.Scree, x, z);
                    var cliff = maps.Sample(maps.Cliff, x, z);
                    var edge = model.SampleRoadEdge(x, z);
                    var kind = float.IsInfinity(edge) ? 0 : model.SampleRoadKind(x, z);
                    TerrainColorMapMath.Evaluate(macro, macro2, patch, curv, flow, scree, cliff, edge, kind, rutNoise,
                        straw, moist, canopy, freckle, out var r, out var g, out var b, out var ao);
                    pixels[py * size + px] = new Color32(
                        (byte)(TerrainColorMapMath.Encode(r) * 255f + 0.5f),
                        (byte)(TerrainColorMapMath.Encode(g) * 255f + 0.5f),
                        (byte)(TerrainColorMapMath.Encode(b) * 255f + 0.5f),
                        (byte)(ao * 255f + 0.5f));
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "HK_TerrainColorMap",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4
            };
            tex.SetPixels32(pixels);
            tex.Apply(true, false);
            Current = tex;
            _rect = new Vector4(model.OriginX, model.OriginZ, 1f / model.Size, 0f);
            Publish();
            return tex;
        }

        /// <summary>Küresel değişkenleri yazar (doku yoksa nötr beyaz/kapalı).</summary>
        public static void Publish()
        {
            var strength = TerrainColorMapMath.TierStrength(_tier);
            if (Current == null)
            {
                Shader.SetGlobalVector(ParamsId, new Vector4(0f, 0f, 0f, 0f));
                return;
            }

            Shader.SetGlobalTexture(MapId, Current);
            Shader.SetGlobalVector(RectId, _rect);
            Shader.SetGlobalVector(ParamsId, new Vector4(strength.x, strength.y, 120f, 260f));
        }

        /// <summary>Kalite kademesi (QualityTierApplier): tint/AO gücünü ayarlar.</summary>
        public static void SetTier(int level)
        {
            _tier = Mathf.Clamp(level, 0, 3);
            Publish();
        }
    }
}
