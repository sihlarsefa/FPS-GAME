using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Infrastructure.Rendering
{
    /// <summary>Etkin render hattını ve gölgelendirici adlarını çözer (URP yoksa yerleşik hatta düşer).</summary>
    public static class RenderPipelineInfo
    {
        public const string UrpLit = "Universal Render Pipeline/Lit";
        public const string UrpSimpleLit = "Universal Render Pipeline/Simple Lit";
        public const string UrpUnlit = "Universal Render Pipeline/Unlit";
        public const string UrpParticlesUnlit = "Universal Render Pipeline/Particles/Unlit";
        public const string UrpTerrainLit = "Universal Render Pipeline/Terrain/Lit";
        public const string SkyboxProcedural = "Skybox/Procedural";

        public const string BuiltinStandard = "Standard";
        public const string BuiltinUnlitColor = "Unlit/Color";
        public const string BuiltinSprites = "Sprites/Default";
        public const string BuiltinParticlesAdditive = "Legacy Shaders/Particles/Additive";
        public const string BuiltinParticlesAlpha = "Legacy Shaders/Particles/Alpha Blended";
        public const string BuiltinTerrain = "Nature/Terrain/Standard";
        public const string ErrorShader = "Hidden/InternalErrorShader";

        private static readonly Dictionary<string, Shader> Shaders = new Dictionary<string, Shader>();

        /// <summary>Etkin (kalite seviyesine göre) render hattı varlığı; yerleşik hatta null.</summary>
        public static RenderPipelineAsset CurrentAsset => GraphicsSettings.currentRenderPipeline;

        /// <summary>URP etkin mi?</summary>
        public static bool IsUrpActive => GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;

        /// <summary>Etkin URP varlığı (yoksa null).</summary>
        public static UniversalRenderPipelineAsset UrpAsset => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        /// <summary>Ada göre gölgelendirici (önbellekli). Bulunamazsa null.</summary>
        public static Shader Find(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            if (Shaders.TryGetValue(name, out var cached) && cached != null)
                return cached;

            var shader = Shader.Find(name);
            if (shader != null)
                Shaders[name] = shader;
            return shader;
        }

        /// <summary>Sıradaki ilk bulunan gölgelendiriciyi döndürür; hiçbiri yoksa hata gölgelendiricisi (asla null değil).</summary>
        public static Shader FindFirst(string a, string b = null, string c = null, string d = null)
        {
            var s = Find(a);
            if (s != null) return s;
            s = Find(b);
            if (s != null) return s;
            s = Find(c);
            if (s != null) return s;
            s = Find(d);
            if (s != null) return s;

            s = Find(ErrorShader);
            if (s != null) return s;

            // Son çare: yerleşik varsayılan malzemenin gölgelendiricisi.
            var fallback = Find(BuiltinSprites) ?? Find(BuiltinUnlitColor);
            return fallback;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Shaders.Clear();
        }
    }
}
