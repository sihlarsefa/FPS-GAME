using Project.Core.Domain;
using Project.Infrastructure.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.World
{
    /// <summary>Çim mesh varyantları (3 demet) ve malzeme. Gölgelendirici yoksa URP/Lit'e düşer (bir kez uyarı).</summary>
    public static class GrassAssets
    {
        public const string GrassShaderName = "HAREKAT/Grass";
        public const string VegetationShaderName = "HAREKAT/VegetationWind";

        private static bool _warnedGrass;
        private static bool _warnedVeg;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _warnedGrass = false;
            _warnedVeg = false;
        }

        /// <summary>Bir varyantın Mesh'i (her çağrı yeni). detailScale &lt; 1: daha az bıçak.</summary>
        public static Mesh CreateMesh(int variant, int seed, float detailScale)
        {
            var g = GrassBladeGeometry.Build(variant, seed, detailScale);
            int vc = g.VertexCount;
            var verts = new Vector3[vc];
            var norms = new Vector3[vc];
            var uvs = new Vector2[vc];
            var cols = new Color[vc];
            for (var i = 0; i < vc; i++)
            {
                verts[i] = new Vector3(g.Positions[i * 3], g.Positions[i * 3 + 1], g.Positions[i * 3 + 2]);
                norms[i] = new Vector3(g.Normals[i * 3], g.Normals[i * 3 + 1], g.Normals[i * 3 + 2]).normalized;
                uvs[i] = new Vector2(g.Uvs[i * 2], g.Uvs[i * 2 + 1]);
                cols[i] = new Color(g.Colors[i * 4], g.Colors[i * 4 + 1], g.Colors[i * 4 + 2], g.Colors[i * 4 + 3]);
            }

            var mesh = new Mesh { name = "HK_GrassClump_" + variant };
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.colors = cols;
            mesh.triangles = g.Indices;
            mesh.bounds = new Bounds(new Vector3(0f, g.MaxHeight * 0.5f, 0f),
                new Vector3(g.Radius * 2f, g.MaxHeight, g.Radius * 2f));
            mesh.UploadMeshData(true);
            return mesh;
        }

        /// <summary>Çim malzemesi: HAREKAT/Grass. Gölgelendirici yoksa null (bir kez uyarı) ve çağıran mevcut arazi çim yoluna döner.</summary>
        public static Material CreateGrassMaterial()
        {
            var shader = RenderPipelineInfo.Find(GrassShaderName);
            if (shader == null)
            {
                if (!_warnedGrass)
                {
                    _warnedGrass = true;
                    Debug.LogWarning("[Çim] '" + GrassShaderName + "' bulunamadı (Always Included Shaders'a ekli mi?); mesh çim kapalı, arazi detay çimi kullanılıyor.");
                }

                return null;
            }

            var m = new Material(shader) { name = "HK_Grass" };
            m.SetColor("_BaseColor", Color.white);
            m.SetColor("_ColorBottom", new Color(0.34f, 0.42f, 0.17f, 1f));
            m.SetColor("_ColorTop", new Color(0.86f, 0.92f, 0.52f, 1f));
            m.enableInstancing = true;
            m.renderQueue = (int)RenderQueue.AlphaTest;
            return m;
        }

        private static Texture2D _gradient;

        /// <summary>Boyuna gradyan dokusu (v: 0 taban koyu, 1 uç açık sıcak yeşil). URP/Lit _BaseMap olarak kullanılır; mesh uv.y = boy oranı.</summary>
        private static Texture2D GetGradient()
        {
            if (_gradient != null) return _gradient;
            const int h = 16;
            var t = new Texture2D(2, h, TextureFormat.RGBA32, false, false) { name = "HK_GrassGradient", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var bottom = new Color(0.50f, 0.58f, 0.40f, 1f);
            var top = new Color(1.00f, 1.00f, 0.80f, 1f);
            for (var y = 0; y < h; y++)
            {
                float f = y / (float)(h - 1);
                var c = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, f));
                t.SetPixel(0, y, c);
                t.SetPixel(1, y, c);
            }

            t.Apply(false, false);
            _gradient = t;
            return t;
        }

        /// <summary>
        /// URP/Lit çift yüzlü yedek çim (özel gölgelendirici gerekmez; varsayılan yol). Renk, parça başına MaterialPropertyBlock ile
        /// _BaseColor üzerinden verilir; ucun açılması gradyan dokusuyla sağlanır. Bu malzeme kendi başına sıcak yeşildir.
        /// </summary>
        public static Material CreateFallbackMaterial()
        {
            var shader = RenderPipelineInfo.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = RenderPipelineInfo.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null)
                return null;
            var m = new Material(shader) { name = "HK_GrassFallback" };
            var c = FallbackBaseColor;
            m.SetColor("_BaseColor", c);
            m.SetColor("_Color", c);
            var tex = GetGradient();
            m.SetTexture("_BaseMap", tex);
            m.SetTexture("_MainTex", tex);
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetFloat("_Smoothness", 0.04f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_SpecularHighlights", 0f);
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            m.doubleSidedGI = true;
            m.enableInstancing = true;
            return m;
        }

        /// <summary>Yedek malzemenin taban rengi (sıcak yeşil); parça rengi bununla çarpılan ton olarak hesaplanır.</summary>
        public static readonly Color FallbackBaseColor = new Color(0.46f, 0.60f, 0.20f, 1f);

        /// <summary>
        /// Rüzgârlı ağaç/çalı yaprağı malzemesi (HAREKAT/VegetationWind). Gölgelendirici yoksa null döner (çağıran mevcut yolu kullanır).
        /// trunkSway: gövde sallanması, flutter: dal/yaprak titremesi.
        /// </summary>
        public static Material TryCreateWindFoliage(string name, Texture2D texture, Color tint, float trunkSway, float flutter, float translucency)
        {
            var shader = RenderPipelineInfo.Find(VegetationShaderName);
            if (shader == null)
            {
                if (!_warnedVeg)
                {
                    _warnedVeg = true;
                    Debug.LogWarning("[Rüzgâr] '" + VegetationShaderName + "' bulunamadı; mevcut bitki malzemesi kullanılacak.");
                }

                return null;
            }

            var m = new Material(shader) { name = name };
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Cutoff", VegetationMaterials.AlphaCutoff);
            m.SetFloat("_TrunkSway", trunkSway);
            m.SetFloat("_BranchFlutter", flutter);
            m.SetFloat("_Translucency", translucency);
            m.SetFloat("_Smoothness", 0.08f);
            m.SetFloat("_WindInfluence", 1f);
            if (texture != null)
                m.SetTexture("_BaseMap", texture);
            m.EnableKeyword("_ALPHATEST_ON");
            m.enableInstancing = true;
            m.renderQueue = (int)RenderQueue.AlphaTest;
            return m;
        }
    }
}
