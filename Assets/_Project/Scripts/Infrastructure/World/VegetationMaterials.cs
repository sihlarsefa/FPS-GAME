using Project.Infrastructure.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Bitki malzemeleri: alfa kesmeli çift yüzlü kart malzemeleri (çam iğnesi, meşe/çalı yaprağı), yosun ve LOD2 düz yaprak.
    /// Her çağrı YENİ malzeme üretir (prototip kalıcılaştırma kendi kopyasını varlığa yazar).
    /// </summary>
    public static class VegetationMaterials
    {
        public const float AlphaCutoff = 0.42f;

        /// <summary>Alfa kesmeli, çift yüzlü, gölge düşüren kart malzemesi.</summary>
        public static Material CreateCutout(string name, Texture2D texture, Color tint, float smoothness = 0.08f, float translucency = 0.9f)
        {
            try
            {
                var wind = Project.Infrastructure.World.GrassAssets.TryCreateWindFoliage(name, texture, tint, 0.5f, 0.5f, translucency);
                if (wind != null)
                    return wind;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[HAREKÂT] Rüzgar yaprak malzemesi atlandı: " + e.Message);
            }

            var shader = RenderPipelineInfo.FindFirst(RenderPipelineInfo.UrpLit, RenderPipelineInfo.UrpSimpleLit,
                RenderPipelineInfo.BuiltinStandard);
            var m = new Material(shader) { name = name };
            SetColor(m, tint);
            SetFloat(m, "_Smoothness", smoothness);
            SetFloat(m, "_Glossiness", smoothness);
            SetFloat(m, "_Metallic", 0f);
            SetFloat(m, "_Cull", (float)CullMode.Off);
            SetFloat(m, "_AlphaClip", 1f);
            SetFloat(m, "_Cutoff", AlphaCutoff);
            SetFloat(m, "_Surface", 0f);
            SetFloat(m, "_ZWrite", 1f);
            SetFloat(m, "_ReceiveShadows", 1f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            if (texture != null)
            {
                if (m.HasProperty("_BaseMap"))
                    m.SetTexture("_BaseMap", texture);
                if (m.HasProperty("_MainTex"))
                    m.SetTexture("_MainTex", texture);
            }

            m.enableInstancing = true;
            return m;
        }

        public static Material CreateNeedles(int seed, int variant = 0) =>
            CreateCutout("HK_Veg_PineNeedles", VegetationTextures.CreateNeedleCard(seed), VegetationTextures.PineTint(variant), 0.08f, 1f);

        public static Material CreateLeaves(int seed, bool dark, int variant = 0) =>
            CreateCutout(dark ? "HK_Veg_BushLeaves" : "HK_Veg_OakLeaves", VegetationTextures.CreateLeafCluster(seed),
                dark ? new Color(0.95f, 1f, 0.88f) : VegetationTextures.OakTint(variant));

        /// <summary>Kaya üstü yosun (hafif pürüzlü, koyu yeşil).</summary>
        public static Material CreateMoss()
        {
            var spec = MaterialSpec.LitSpec(MaterialId.Foliage, new Color(0.22f, 0.28f, 0.14f), 0.1f, 0f, MaterialTextureKey.SurfaceDetail, 4711, 1f);
            var m = MaterialLibrary.CreateFromSpec(spec);
            m.name = "HK_Veg_Moss";
            return m;
        }

        private static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color"))
                m.SetColor("_Color", c);
        }

        private static void SetFloat(Material m, string prop, float v)
        {
            if (m.HasProperty(prop))
                m.SetFloat(prop, v);
        }
    }

    /// <summary>Bitki/kaya ayarları: tür kimliği eşlemesi, kaya boyut sınıfı, rüzgâr parametreleri, kademe başına billboard mesafesi.</summary>
    public static class VegetationTuning
    {
        public struct Wind
        {
            public float Bend, Speed, Size, Amount;
        }

        /// <summary>TreeKind → ContentIds tür kimliği (VegetationOverride.speciesId).</summary>
        public static string SpeciesId(TreeKind kind)
        {
            switch (kind)
            {
                case TreeKind.PineA:
                case TreeKind.PineB: return Project.Infrastructure.Content.ContentIds.VegPine;
                case TreeKind.Oak: return Project.Infrastructure.Content.ContentIds.VegOak;
                case TreeKind.Dead: return Project.Infrastructure.Content.ContentIds.VegDead;
                default: return Project.Infrastructure.Content.ContentIds.VegBush;
            }
        }

        /// <summary>Kaya ölçeği (ana boyut) → boyut sınıfı: small / medium / large.</summary>
        public static string RockSizeClass(float size)
        {
            if (size < 1.0f) return Project.Infrastructure.Content.ContentIds.RockSmall;
            if (size < 1.8f) return Project.Infrastructure.Content.ContentIds.RockMedium;
            return Project.Infrastructure.Content.ContentIds.RockLarge;
        }

        /// <summary>Tür başına rüzgâr: çam hafif, meşe orta, çalı fazla, kuru ağaç sabit. Kademe 0'da hareket kapanır.</summary>
        public static Wind WindFor(TreeKind kind, int tier)
        {
            if (tier <= 0 || kind == TreeKind.Dead)
                return new Wind { Bend = 0f, Speed = 0f, Size = 0.3f, Amount = 0f };
            switch (kind)
            {
                case TreeKind.PineA:
                case TreeKind.PineB: return new Wind { Bend = 0.25f, Speed = 0.35f, Size = 0.4f, Amount = 0.35f };
                case TreeKind.Oak: return new Wind { Bend = 0.4f, Speed = 0.45f, Size = 0.5f, Amount = 0.5f };
                default: return new Wind { Bend = 0.55f, Speed = 0.6f, Size = 0.6f, Amount = 0.7f };
            }
        }

        /// <summary>Kademe başına ağaç billboard başlangıç mesafesi (m); PerformanceProfile ile aynı kaynak, alt sınır 60 m.</summary>
        public static float BillboardDistance(int tier)
        {
            return Mathf.Max(60f, Project.Infrastructure.Rendering.PerformanceProfile.TreeBillboardStart(tier));
        }

        /// <summary>Prefab'da LODGroup yoksa true (uyarı + prosedürel LOD gerekir).</summary>
        public static bool NeedsProceduralLod(GameObject prefab)
        {
            return prefab != null && prefab.GetComponentInChildren<LODGroup>(true) == null;
        }

        /// <summary>
        /// Override prefab'ı terrain prototipi olarak hazırlar. LODGroup yoksa uyarı verir ve bir kopyaya iki kademeli LODGroup ekler
        /// (tam ayrıntı → son kademe; altında terrain billboard'u). Kalıcı prefab varlığı değiştirilmez.
        /// </summary>
        public static GameObject PrepareOverride(GameObject prefab, Transform holder, string label)
        {
            if (prefab == null)
                return null;
            if (!NeedsProceduralLod(prefab))
                return prefab;
            Debug.LogWarning("[Bitki] '" + label + "' override prefab'ında LODGroup yok; prosedürel LOD kopyası ekleniyor.");
            var copy = Object.Instantiate(prefab, holder);
            copy.name = prefab.name + "_LOD";
            var renderers = copy.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return prefab;
            var group = copy.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(0.06f, renderers) });
            group.RecalculateBounds();
            return copy;
        }
    }
}
