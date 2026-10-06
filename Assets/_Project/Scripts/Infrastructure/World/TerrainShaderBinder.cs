using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// S4: HAREKAT arazi gölgelendiricisini (HAREKAT/Terrain/Lit) araziye bağlar, küresel parametreleri (yamaç triplanar, makro,
    /// detay normal, ıslaklık, POM kalitesi) yazar ve Islak Çamur/POM zemin malzemesi üretir. Gölgelendirici bulunamazsa tek uyarı
    /// verip mevcut URP Terrain/Lit yoluna dokunmaz (altın kural: eksik varlık hiçbir şeyi bozmaz).
    /// </summary>
    public static class TerrainShaderBinder
    {
        public const string TerrainShaderName = "HAREKAT/Terrain/Lit";
        public const string ParallaxShaderName = "HAREKAT/Surface/ParallaxLit";

        private static readonly int Params0 = Shader.PropertyToID("_HarekatTerrainParams0");
        private static readonly int Params1 = Shader.PropertyToID("_HarekatTerrainParams1");
        private static readonly int Params2 = Shader.PropertyToID("_HarekatTerrainParams2");
        private static readonly int Params3 = Shader.PropertyToID("_HarekatTerrainParams3");
        private static readonly int WetnessId = Shader.PropertyToID("_HarekatWetness");
        private static readonly int PomQualityId = Shader.PropertyToID("_HarekatPomQuality");
        private static readonly int CliffAlbedoId = Shader.PropertyToID("_HarekatCliffAlbedo");
        private static readonly int CliffNormalId = Shader.PropertyToID("_HarekatCliffNormal");
        private static readonly int DetailNormalId = Shader.PropertyToID("_HarekatDetailNormal");

        private static bool _warnedTerrain;
        private static bool _warnedParallax;
        private static float _wetness;
        private static float _wetnessScale = 1f;
        private static Material _terrainMaterial;
        private static Material _previousTemplate;
        private static Terrain _boundTerrain;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _warnedTerrain = false;
            _warnedParallax = false;
            _wetness = 0f;
            _wetnessScale = 1f;
            _terrainMaterial = null;
            _previousTemplate = null;
            _boundTerrain = null;
        }

        /// <summary>Küresel ıslaklık 0-1 (kalite ölçeğiyle çarpılmış değer gölgelendiriciye gider).</summary>
        public static float Wetness => _wetness;

        public static Shader FindTerrainShader() => Shader.Find(TerrainShaderName);

        /// <summary>
        /// Arazi malzeme şablonunu kalite kademesine göre ayarlar. Kademe 0 veya gölgelendirici yoksa mevcut şablon korunur
        /// (önceden değiştirildiyse geri yüklenir). true = HAREKAT gölgelendirici etkin.
        /// TerrainGenerator ve QualityTierApplier çağırır.
        /// </summary>
        public static bool Install(Terrain terrain, int qualityTier)
        {
            var tier = TerrainShadingMath.GetTier(qualityTier);
            ApplyGlobals(tier, terrain);
            _wetnessScale = tier.WetnessScale;
            Shader.SetGlobalFloat(WetnessId, _wetness * _wetnessScale);

            if (terrain == null)
                return false;

            if (!tier.UseHarekatTerrain)
            {
                Restore(terrain);
                return false;
            }

            var shader = FindTerrainShader();
            if (shader == null || !shader.isSupported)
            {
                if (!_warnedTerrain)
                {
                    _warnedTerrain = true;
                    Debug.LogWarning("[TerrainShaderBinder] " + TerrainShaderName + " bulunamadı; URP Terrain/Lit korunuyor.");
                }

                Restore(terrain);
                return false;
            }

            if (_terrainMaterial == null || _terrainMaterial.shader != shader)
                _terrainMaterial = new Material(shader) { name = "HK_TerrainHarekat" };

            if (terrain.materialTemplate != _terrainMaterial)
            {
                _previousTemplate = terrain.materialTemplate;
                terrain.materialTemplate = _terrainMaterial;
            }

            _boundTerrain = terrain;
            return true;
        }

        /// <summary>Önceki malzeme şablonunu geri yükler.</summary>
        public static void Restore(Terrain terrain)
        {
            if (terrain == null || _terrainMaterial == null || terrain.materialTemplate != _terrainMaterial)
                return;
            terrain.materialTemplate = _previousTemplate;
            _boundTerrain = null;
        }

        /// <summary>Küresel parametreleri yazar (Terrain null olabilir: yalnız sayısal değerler).</summary>
        public static void ApplyGlobals(TerrainShadingTier tier, Terrain terrain)
        {
            var cliffTile = 12f;
            Texture cliffAlbedo = null, cliffNormal = null, detailNormal = null;
            var detailTile = 2.5f;
            var layers = terrain != null && terrain.terrainData != null ? terrain.terrainData.terrainLayers : null;
            if (layers != null)
            {
                for (var i = 0; i < layers.Length; i++)
                {
                    var layer = layers[i];
                    if (layer == null)
                        continue;
                    if (cliffAlbedo == null && layer.name.Contains("Rock"))
                    {
                        cliffAlbedo = layer.diffuseTexture;
                        cliffNormal = layer.normalMapTexture;
                        cliffTile = Mathf.Max(2f, layer.tileSize.x);
                    }
                    else if (detailNormal == null && layer.name.Contains("Dirt"))
                    {
                        detailNormal = layer.normalMapTexture;
                    }
                }
            }

            var cliff = tier.CliffStrength;
            if (cliffAlbedo == null || cliffNormal == null)
                cliff = 0f;
            var detail = detailNormal != null ? tier.DetailStrength : 0f;

            Shader.SetGlobalTexture(CliffAlbedoId, cliffAlbedo != null ? cliffAlbedo : Texture2D.whiteTexture);
            Shader.SetGlobalTexture(CliffNormalId, cliffNormal != null ? cliffNormal : Texture2D.normalTexture);
            Shader.SetGlobalTexture(DetailNormalId, detailNormal != null ? detailNormal : Texture2D.normalTexture);

            Shader.SetGlobalVector(Params0, new Vector4(cliff, TerrainShadingMath.NormalYThreshold(tier.CliffAngleDeg > 0f ? tier.CliffAngleDeg : 35f), 0.12f, cliffTile));
            Shader.SetGlobalVector(Params1, new Vector4(tier.MacroStrength, 137f, 40f, 160f));
            Shader.SetGlobalVector(Params2, new Vector4(detail, detailTile, tier.DetailDistance, 0.3f));
            Shader.SetGlobalVector(Params3, new Vector4(6f, 0.35f, 0.6f, 0.92f));
            Shader.SetGlobalFloat(PomQualityId, tier.PomQuality);
        }

        /// <summary>Yağmur yoğunluğuna (0-1) göre küresel ıslaklığı ilerletir (her karede/periyodik çağrılabilir).</summary>
        public static void Tick(float rain01, float deltaTime)
        {
            _wetness = TerrainShadingMath.StepWetness(_wetness, rain01, deltaTime);
            Shader.SetGlobalFloat(WetnessId, _wetness * _wetnessScale);
        }

        /// <summary>Islaklığı doğrudan ayarlar (test/hava ön ayarı için).</summary>
        public static void SetWetness(float wetness01)
        {
            _wetness = Mathf.Clamp01(wetness01);
            Shader.SetGlobalFloat(WetnessId, _wetness * _wetnessScale);
        }

        /// <summary>
        /// Lit malzemeden POM + ıslaklık malzemesi üretir (Lit uyumlu özellikler kopyalanır). Gölgelendirici yoksa null (uyarı bir kez).
        /// MaterialLibrary çamur/zemin/kaya malzemelerini bununla takas eder (null ise mevcut malzeme kalır).
        /// </summary>
        public static Material CreateParallaxMaterial(Material litSource, SurfaceWetPreset preset, Texture ormhMap = null, Texture heightMap = null)
        {
            var shader = Shader.Find(ParallaxShaderName);
            if (shader == null || !shader.isSupported)
            {
                if (!_warnedParallax)
                {
                    _warnedParallax = true;
                    Debug.LogWarning("[TerrainShaderBinder] " + ParallaxShaderName + " bulunamadı; mevcut zemin malzemesi korunuyor.");
                }

                return null;
            }

            var m = new Material(shader) { name = litSource != null ? litSource.name + "_POM" : "HK_ParallaxLit" };
            if (litSource != null)
            {
                CopyColor(litSource, m, "_BaseColor");
                CopyTexture(litSource, m, "_BaseMap");
                CopyTexture(litSource, m, "_BumpMap");
                CopyFloat(litSource, m, "_Smoothness");
                CopyFloat(litSource, m, "_Metallic");
                CopyFloat(litSource, m, "_BumpScale");
                if (litSource.IsKeywordEnabled("_NORMALMAP"))
                    m.EnableKeyword("_NORMALMAP");
            }

            m.SetFloat("_Parallax", preset.ParallaxDepth);
            m.SetFloat("_HarekatWetResponse", preset.WetResponse);
            m.SetFloat("_HarekatBaseWet", preset.BaseWet);
            m.SetFloat("_HarekatPuddleLevel", preset.PuddleLevel);
            m.SetFloat("_HarekatWetDarken", preset.WetDarken);
            m.SetFloat("_HarekatWetSmooth", preset.WetSmooth);
            m.SetFloat("_HarekatPuddleScale", preset.PuddleScale);

            if (ormhMap != null)
            {
                m.SetTexture("_MaskMap", ormhMap);
                m.EnableKeyword("_MASKMAP");
            }

            if (heightMap != null)
                m.SetTexture("_ParallaxMap", heightMap);
            if (ormhMap != null || heightMap != null)
                m.EnableKeyword("_PARALLAXMAP");
            return m;
        }

        /// <summary>Islak çamur ön ayarıyla malzeme (kısayol).</summary>
        public static Material CreateWetMud(Material litSource, Texture ormhMap = null, Texture heightMap = null)
            => CreateParallaxMaterial(litSource, TerrainShadingMath.MudPreset(), ormhMap, heightMap);

        private static void CopyColor(Material from, Material to, string name)
        {
            if (from.HasProperty(name) && to.HasProperty(name))
                to.SetColor(name, from.GetColor(name));
        }

        private static void CopyFloat(Material from, Material to, string name)
        {
            if (from.HasProperty(name) && to.HasProperty(name))
                to.SetFloat(name, from.GetFloat(name));
        }

        private static void CopyTexture(Material from, Material to, string name)
        {
            if (from.HasProperty(name) && to.HasProperty(name))
            {
                to.SetTexture(name, from.GetTexture(name));
                to.SetTextureScale(name, from.GetTextureScale(name));
                to.SetTextureOffset(name, from.GetTextureOffset(name));
            }
        }
    }
}
