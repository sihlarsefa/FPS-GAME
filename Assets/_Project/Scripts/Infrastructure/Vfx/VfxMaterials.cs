using Project.Infrastructure.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Vfx
{
    /// <summary>
    /// Efekt malzemeleri. Taban olarak <see cref="MaterialLibrary.ParticleAdditive"/> / <see cref="MaterialLibrary.ParticleAlpha"/>
    /// kopyalanır ve efekt dokusu atanır (SRP Batcher uyumlu, paylaşılan malzemeler). Kütüphane malzemesi yoksa
    /// URP "Particles/Unlit" gölgelendiricisi ile çalışma zamanında kurulur.
    /// </summary>
    internal static class VfxMaterials
    {
        private const string ParticleShaderName = "Universal Render Pipeline/Particles/Unlit";

        // Çıkartmalar opak geometriden sonra, duman/parçacıklardan önce çizilir (şeffaf aralığın başı).
        private const int ScorchQueue = (int)RenderQueue.Transparent - 50;
        private const int DecalQueue = ScorchQueue + 1;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int SrcBlendAlphaId = Shader.PropertyToID("_SrcBlendAlpha");
        private static readonly int DstBlendAlphaId = Shader.PropertyToID("_DstBlendAlpha");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int CullId = Shader.PropertyToID("_Cull");

        private static Material _additiveGlow;
        private static Material _alphaPuff;
        private static Material _alphaSoft;
        private static Material _alphaChunk;
        private static Material _alphaRing;
        private static Material _tracer;
        private static Material _decalConcrete;
        private static Material _decalWood;
        private static Material _decalDirt;
        private static Material _decalMetal;
        private static Material _scorch;
        private static Material _decalBlood;

        /// <summary>Katkılı yumuşak parıltı (alev, namlu alevi, kıvılcım).</summary>
        public static Material AdditiveGlow => _additiveGlow != null ? _additiveGlow : _additiveGlow = Create("VFX_AdditiveGlow", true, VfxTextures.SoftDot, Color.white, 0);

        /// <summary>Alfa karışımlı bulut (duman, toz, sis).</summary>
        public static Material AlphaPuff => _alphaPuff != null ? _alphaPuff : _alphaPuff = Create("VFX_AlphaPuff", false, VfxTextures.Puff, Color.white, 0);

        /// <summary>Alfa karışımlı yumuşak nokta (kan, su sisi).</summary>
        public static Material AlphaSoft => _alphaSoft != null ? _alphaSoft : _alphaSoft = Create("VFX_AlphaSoft", false, VfxTextures.SoftDot, Color.white, 0);

        /// <summary>Alfa karışımlı parça (toprak, kıymık, enkaz, yaprak, damla).</summary>
        public static Material AlphaChunk => _alphaChunk != null ? _alphaChunk : _alphaChunk = Create("VFX_AlphaChunk", false, VfxTextures.Chunk, Color.white, 0);

        /// <summary>Alfa karışımlı halka (su halkası, şok dalgası).</summary>
        public static Material AlphaRing => _alphaRing != null ? _alphaRing : _alphaRing = Create("VFX_AlphaRing", false, VfxTextures.Ring, Color.white, 0);

        /// <summary>Mermi izi (katkılı ışın).</summary>
        public static Material Tracer => _tracer != null ? _tracer : _tracer = CreateTracer();

        public static Material DecalConcrete => _decalConcrete != null ? _decalConcrete : _decalConcrete = Create("VFX_HoleConcrete", false, VfxTextures.HoleGeneric, new Color(0.78f, 0.76f, 0.72f, 1f), DecalQueue);
        public static Material DecalWood => _decalWood != null ? _decalWood : _decalWood = Create("VFX_HoleWood", false, VfxTextures.HoleGeneric, new Color(0.72f, 0.52f, 0.3f, 1f), DecalQueue);
        public static Material DecalDirt => _decalDirt != null ? _decalDirt : _decalDirt = Create("VFX_HoleDirt", false, VfxTextures.HoleGeneric, new Color(0.32f, 0.25f, 0.18f, 0.9f), DecalQueue);
        public static Material DecalMetal => _decalMetal != null ? _decalMetal : _decalMetal = Create("VFX_HoleMetal", false, VfxTextures.HoleMetal, new Color(0.85f, 0.85f, 0.88f, 1f), DecalQueue);
        public static Material Scorch => _scorch != null ? _scorch : _scorch = Create("VFX_Scorch", false, VfxTextures.Scorch, new Color(1f, 1f, 1f, 0.85f), ScorchQueue);

        /// <summary>Duvara/zemine sıçrayan kan lekesi.</summary>
        public static Material DecalBlood => _decalBlood != null ? _decalBlood : _decalBlood = Create("VFX_BloodSplat", false, VfxTextures.Splat, new Color(0.3f, 0.015f, 0.015f, 0.85f), DecalQueue);

        /// <summary>Mermi izi HDR parlaklığı (bloom dostu): malzeme rengi 1'in üzerine çıkar, gece daha parlak.</summary>
        public static void SetTracerBrightness(float hdr)
        {
            var material = Tracer;
            if (material == null || Mathf.Abs(hdr - _tracerHdr) < 0.02f)
                return;

            _tracerHdr = hdr;
            var color = new Color(hdr, hdr, hdr, 1f);
            if (material.HasProperty(BaseColorId))
                material.SetColor(BaseColorId, color);
            if (material.HasProperty(ColorId))
                material.SetColor(ColorId, color);
        }

        private static float _tracerHdr = 1f;

        private static Material CreateTracer()
        {
            var material = Create("VFX_Tracer", true, VfxTextures.Beam, Color.white, 0);
            if (material != null)
                DisableFading(material);
            return material;
        }

        /// <summary>Çıkartma kuyruğunda, verilen dokulu malzeme (mermi deliği çeşitleri için).</summary>
        public static Material CreateDecal(string name, Texture texture)
        {
            return Create(name, false, texture, Color.white, DecalQueue);
        }

        public static Material DecalFor(SurfaceKind surface)
        {
            switch (surface)
            {
                case SurfaceKind.Metal:
                    return DecalMetal;
                case SurfaceKind.Wood:
                    return DecalWood;
                case SurfaceKind.Dirt:
                    return DecalDirt;
                default:
                    return DecalConcrete;
            }
        }

        /// <summary>Statik önbellekleri sıfırlar (alan yeniden yüklemesi kapalıyken oynatma oturumları arasında).</summary>
        public static void ResetCache()
        {
            _additiveGlow = null;
            _alphaPuff = null;
            _alphaSoft = null;
            _alphaChunk = null;
            _alphaRing = null;
            _tracer = null;
            _tracerHdr = 1f;
            _decalConcrete = null;
            _decalWood = null;
            _decalDirt = null;
            _decalMetal = null;
            _scorch = null;
            _decalBlood = null;
        }

        private static Material Create(string name, bool additive, Texture texture, Color color, int renderQueue)
        {
            Material source = null;
            try
            {
                source = additive ? MaterialLibrary.ParticleAdditive : MaterialLibrary.ParticleAlpha;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[GameVfx] MaterialLibrary parçacık malzemesi alınamadı: {exception.Message}");
            }

            Material material;
            if (source != null)
            {
                material = new Material(source);
            }
            else
            {
                material = CreateFallback(additive);
                if (material == null)
                    return null;
            }

            material.name = name;
            material.hideFlags = HideFlags.DontSave;

            if (texture != null)
            {
                if (material.HasProperty(BaseMapId))
                    material.SetTexture(BaseMapId, texture);
                if (material.HasProperty(MainTexId))
                    material.SetTexture(MainTexId, texture);
            }

            if (material.HasProperty(BaseColorId))
                material.SetColor(BaseColorId, color);
            if (material.HasProperty(ColorId))
                material.SetColor(ColorId, color);

            if (material.HasProperty(CullId))
                material.SetFloat(CullId, (float)CullMode.Off);

            if (renderQueue > 0)
            {
                // Çıkartmalar yüzeye 1 cm uzaklıkta: yumuşak parçacık / kamera solması onları görünmez yapar.
                DisableFading(material);
                material.renderQueue = renderQueue;
            }

            return material;
        }

        private static readonly int SoftParticlesEnabledId = Shader.PropertyToID("_SoftParticlesEnabled");
        private static readonly int CameraFadingEnabledId = Shader.PropertyToID("_CameraFadingEnabled");

        private static void DisableFading(Material material)
        {
            material.DisableKeyword("_SOFTPARTICLES_ON");
            material.DisableKeyword("_FADING_ON");
            material.DisableKeyword("_DISTORTION_ON");
            if (material.HasProperty(SoftParticlesEnabledId))
                material.SetFloat(SoftParticlesEnabledId, 0f);
            if (material.HasProperty(CameraFadingEnabledId))
                material.SetFloat(CameraFadingEnabledId, 0f);
        }

        private static Material CreateFallback(bool additive)
        {
            var shader = Shader.Find(ParticleShaderName);
            if (shader != null)
            {
                var material = new Material(shader);
                ConfigureUrpParticle(material, additive);
                return material;
            }

            // Son çare: her derlemede bulunan sprite gölgelendiricisi (yalnızca alfa karışımı).
            shader = Shader.Find("Sprites/Default");
            if (shader != null)
                return new Material(shader);

            Debug.LogWarning("[GameVfx] Parçacık gölgelendiricisi bulunamadı; efektler görünmeyecek.");
            return null;
        }

        private static void ConfigureUrpParticle(Material material, bool additive)
        {
            material.SetFloat(SurfaceId, 1f);
            material.SetFloat(BlendId, additive ? 2f : 0f);
            material.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
            material.SetFloat(DstBlendId, additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat(SrcBlendAlphaId, (float)BlendMode.One);
            material.SetFloat(DstBlendAlphaId, additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat(ZWriteId, 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetShaderPassEnabled("DepthOnly", false);
            material.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
