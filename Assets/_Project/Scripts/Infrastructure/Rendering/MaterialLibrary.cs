using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Paylaşılan malzeme kütüphanesi. Get(id) önce editörde üretilmiş GameArtLibrary malzemesini kullanır, yoksa
    /// tariften (MaterialSpec) çalışma zamanında üretir ve önbelleğe alır. URP etkin değilse yerleşik hat
    /// gölgelendiricilerine (Standard, Sprites/Default, Legacy Particles) düşer.
    /// Dönen malzemeler PAYLAŞIMLIDIR: değiştirmeyin; nesne başına renk için MaterialPropertyBlock ya da yeni
    /// malzeme (CreateFromSpec / new Material(...)) kullanın.
    /// </summary>
    public static class MaterialLibrary
    {
        // ---------------------------------------------------------------- Property IDs
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int CullId = Shader.PropertyToID("_Cull");
        private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int SrcBlendAlphaId = Shader.PropertyToID("_SrcBlendAlpha");
        private static readonly int DstBlendAlphaId = Shader.PropertyToID("_DstBlendAlpha");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int PreserveSpecularId = Shader.PropertyToID("_BlendModePreserveSpecular");
        private static readonly int ReceiveShadowsId = Shader.PropertyToID("_ReceiveShadows");
        private static readonly int ColorModeId = Shader.PropertyToID("_ColorMode");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        private static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");
        private static readonly int MetallicGlossMapId = Shader.PropertyToID("_MetallicGlossMap");
        private static readonly int OcclusionMapId = Shader.PropertyToID("_OcclusionMap");
        private static readonly int OcclusionStrengthId = Shader.PropertyToID("_OcclusionStrength");
        private static readonly int MaskMapId = Shader.PropertyToID("_MaskMap");
        private static readonly int DetailAlbedoMapId = Shader.PropertyToID("_DetailAlbedoMap");
        private static readonly int DetailNormalMapId = Shader.PropertyToID("_DetailNormalMap");
        private static readonly int DetailAlbedoScaleId = Shader.PropertyToID("_DetailAlbedoMapScale");
        private static readonly int DetailNormalScaleId = Shader.PropertyToID("_DetailNormalMapScale");
        private static readonly int TriplanarId = Shader.PropertyToID("_Triplanar");

        private const string KeywordTransparent = "_SURFACE_TYPE_TRANSPARENT";
        private const string KeywordEmission = "_EMISSION";
        private const string KeywordNormalMap = "_NORMALMAP";
        private const string KeywordMetallicGloss = "_METALLICSPECGLOSSMAP";
        private const string KeywordOcclusion = "_OCCLUSIONMAP";
        private const string KeywordMaskMap = "_MASKMAP";
        private const string KeywordPremultiply = "_ALPHAPREMULTIPLY_ON";
        private const string KeywordModulate = "_ALPHAMODULATE_ON";
        private const string KeywordAlphaTest = "_ALPHATEST_ON";
        private const string KeywordAlphaBlend = "_ALPHABLEND_ON";

        // ---------------------------------------------------------------- State
        private static readonly int IdCount = ComputeIdCount();
        private static readonly MaterialSpec[] Specs = BuildSpecs();
        private static readonly Material[] ById = new Material[IdCount];

        private static readonly Dictionary<ulong, Material> LitCache = new Dictionary<ulong, Material>();
        private static readonly Dictionary<uint, Material> UnlitCache = new Dictionary<uint, Material>();
        private static readonly Dictionary<ulong, Material> TransparentCache = new Dictionary<ulong, Material>();
        private static readonly Dictionary<TextureKey, Material> TexturedCache = new Dictionary<TextureKey, Material>();
        private static readonly Dictionary<TextureKey, Material> ParticleCache = new Dictionary<TextureKey, Material>();
        private static readonly Dictionary<CamoKey, Material> CamoCache = new Dictionary<CamoKey, Material>();

        private static GameArtLibrary _library;
        private static bool _libraryResolved;
        private static Material _particleAdditive;
        private static Material _particleAlpha;
        private static Material _terrain;
        private static bool _terrainResolved;

        // ---------------------------------------------------------------- Contract API

        /// <summary>Toplamalı parçacık malzemesi (URP Particles/Unlit, yumuşak daire, SrcAlpha+One). Köşe rengiyle renklenir.</summary>
        public static Material ParticleAdditive
        {
            get
            {
                if (_particleAdditive == null)
                    _particleAdditive = Particle(SoftParticleTexture, true);
                return _particleAdditive;
            }
        }

        /// <summary>Alfa karışımlı parçacık malzemesi (URP Particles/Unlit, yumuşak daire). Köşe rengiyle renklenir.</summary>
        public static Material ParticleAlpha
        {
            get
            {
                if (_particleAlpha == null)
                    _particleAlpha = Particle(SoftParticleTexture, false);
                return _particleAlpha;
            }
        }

        /// <summary>Kimliğe göre paylaşılan malzeme (kütüphane varlığı ya da tariften üretilmiş; önbellekli).</summary>
        public static Material Get(MaterialId id)
        {
            var index = (int)id;
            if (index < 0 || index >= IdCount)
                index = (int)MaterialId.Gray;

            var material = ById[index];
            if (material != null)
                return material;

            try
            {
                if (Project.Infrastructure.Content.ContentOverrides.TryGetMaterial((MaterialId)index, out var overridden) && overridden != null)
                {
                    ById[index] = overridden;
                    return overridden;
                }
            }
            catch (Exception)
            {
                // Hazır varlık erişilemedi — prosedürel yol.
            }

            var library = Library;
            if (library != null && library.materials != null && index < library.materials.Length)
                material = library.materials[index];

            // Kütüphane URP için üretildiyse ama URP etkin değilse (pembe görünmesin) tariften yerleşik hat malzemesi üret.
            if (material != null && !IsCompatible(material))
                material = null;

            if (material == null)
                material = CreateFromSpec(Specs[index]);
            else if (Specs[index].Pbr != PbrSurface.None && material.HasProperty(BumpMapId) && material.GetTexture(BumpMapId) == null)
            {
                // Kütüphane varlığına normal/maske yok: varlığı kirletmeden örnek kopyada ekle.
                material = new Material(material) { name = material.name };
                ApplyPbr(material, Specs[index].Pbr, Specs[index].TextureSeed, Specs[index].Tiling, Specs[index].NormalStrength);
            }

            material = TryWetVariant((MaterialId)index, material);
            ById[index] = material;
            return material;
        }

        /// <summary>Çamur/zemin/kaya için POM + ıslaklık varyantı; gölgelendirici yoksa aynı malzeme döner.</summary>
        private static Material TryWetVariant(MaterialId id, Material lit)
        {
            if (lit == null || !UnityEngine.Application.isPlaying)
                return lit;
            try
            {
                Material variant = null;
                switch (id)
                {
                    case MaterialId.Mud:
                        variant = Project.Infrastructure.World.TerrainShaderBinder.CreateWetMud(lit);
                        break;
                    case MaterialId.Dirt:
                    case MaterialId.Gravel:
                        variant = Project.Infrastructure.World.TerrainShaderBinder.CreateParallaxMaterial(lit, Project.Core.Domain.TerrainShadingMath.GroundPreset());
                        break;
                    case MaterialId.Rock:
                    case MaterialId.RockDark:
                        variant = Project.Infrastructure.World.TerrainShaderBinder.CreateParallaxMaterial(lit, Project.Core.Domain.TerrainShadingMath.RockPreset());
                        break;
                }
                return variant ?? lit;
            }
            catch (Exception)
            {
                return lit;
            }
        }

        /// <summary>Düz renkli opak Lit malzeme (renk/pürüzsüzlük/metaliklik başına önbellekli). Alfa yok sayılır.</summary>
        public static Material Lit(Color color, float smoothness = 0.2f, float metallic = 0f)
        {
            Color32 c = color;
            c.a = 255;
            var key = Pack(c) | ((ulong)Quantize(smoothness) << 32) | ((ulong)Quantize(metallic) << 40);
            if (LitCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var spec = new MaterialSpec
            {
                Name = "Lit_" + ColorUtility.ToHtmlStringRGB(color),
                Color = (Color)c,
                Smoothness = Mathf.Clamp01(smoothness),
                Metallic = Mathf.Clamp01(metallic),
                Shading = MaterialShading.Lit
            };
            var material = CreateFromSpec(spec);
            LitCache[key] = material;
            return material;
        }

        /// <summary>Düz renkli Unlit malzeme (önbellekli). Alfa &lt; 1 ise saydam Unlit döner.</summary>
        public static Material Unlit(Color color)
        {
            if (color.a < 0.999f)
                return Transparent(color, true);

            Color32 c = color;
            var key = Pack(c);
            if (UnlitCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var spec = new MaterialSpec
            {
                Name = "Unlit_" + ColorUtility.ToHtmlStringRGB(color),
                Color = (Color)c,
                Smoothness = 0f,
                Shading = MaterialShading.Unlit
            };
            var material = CreateFromSpec(spec);
            UnlitCache[key] = material;
            return material;
        }

        /// <summary>Alfa karışımlı saydam malzeme (Lit ya da Unlit; önbellekli). Çift taraflı değildir.</summary>
        public static Material Transparent(Color color, bool unlit)
        {
            Color32 c = color;
            var key = Pack(c) | (unlit ? 1UL << 32 : 0UL);
            if (TransparentCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var spec = new MaterialSpec
            {
                Name = (unlit ? "TransparentUnlit_" : "TransparentLit_") + ColorUtility.ToHtmlStringRGBA(color),
                Color = (Color)c,
                Smoothness = unlit ? 0f : 0.6f,
                Shading = unlit ? MaterialShading.Unlit : MaterialShading.Lit,
                Transparent = true
            };
            var material = CreateFromSpec(spec);
            TransparentCache[key] = material;
            return material;
        }

        /// <summary>Kimliğin tarifinin değiştirilebilir kopyası. Geçersiz kimlik → Gray tarifi.</summary>
        public static MaterialSpec GetSpec(MaterialId id)
        {
            var index = (int)id;
            if (index < 0 || index >= IdCount)
                index = (int)MaterialId.Gray;
            return Specs[index].Clone();
        }

        /// <summary>Tariften YENİ bir malzeme üretir (önbelleğe almaz). null → gri Lit.</summary>
        public static Material CreateFromSpec(MaterialSpec spec)
        {
            if (spec == null)
                spec = MaterialSpec.LitSpec(MaterialId.Gray, new Color(0.5f, 0.5f, 0.5f));

            var urp = RenderPipelineInfo.IsUrpActive;
            Material material;
            switch (spec.Shading)
            {
                case MaterialShading.Particle:
                    material = urp ? CreateUrpParticle(spec) : CreateBuiltinParticle(spec);
                    break;
                case MaterialShading.Unlit:
                    material = urp ? CreateUrpUnlit(spec) : CreateBuiltinUnlit(spec);
                    break;
                default:
                    material = urp ? CreateUrpLit(spec) : CreateBuiltinLit(spec);
                    break;
            }

            material.name = "HK_" + (string.IsNullOrEmpty(spec.Name) ? spec.Id.ToString() : spec.Name);
            return material;
        }

        /// <summary>C4: ContentOverrides değişince kimlik önbelleğini temizler; override her zaman kütüphane/tariften önce çözülür.</summary>
        public static void InvalidateOverrides() => Array.Clear(ById, 0, ById.Length);

        /// <summary>Editörde üretilmiş kütüphaneyi kullan (null → çalışma zamanı üretimi). Kimlik önbelleği temizlenir.</summary>
        public static void Use(GameArtLibrary library)
        {
            _library = library;
            _libraryResolved = true;
            Array.Clear(ById, 0, ById.Length);
            _particleAdditive = null;
            _particleAlpha = null;
            _terrain = null;
            _terrainResolved = false;
        }

        // ---------------------------------------------------------------- Extra API

        /// <summary>Etkin kütüphane (ilk erişimde Resources'tan yüklenir; yoksa null).</summary>
        public static GameArtLibrary Library
        {
            get
            {
                if (!_libraryResolved)
                {
                    _libraryResolved = true;
                    try
                    {
                        _library = GameArtLibrary.Load();
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[MaterialLibrary] GameArtLibrary yüklenemedi: " + e.Message);
                        _library = null;
                    }
                }

                return _library;
            }
        }

        /// <summary>MaterialId sayısı (tarif tablosu boyu).</summary>
        public static int Count => IdCount;

        /// <summary>Parçacık dokusu: kütüphanedeki softParticle ya da prosedürel yumuşak daire.</summary>
        public static Texture2D SoftParticleTexture
        {
            get
            {
                var library = Library;
                if (library != null && library.softParticle != null)
                    return library.softParticle;
                return ProceduralTextures.SoftCircle;
            }
        }

        /// <summary>Arazi malzemesi (kütüphane ya da URP Terrain/Lit). Gölgelendirici yoksa null (Terrain varsayılanı kullanılır).</summary>
        public static Material TerrainMaterial
        {
            get
            {
                if (_terrain != null)
                    return _terrain;
                if (_terrainResolved)
                    return null;

                _terrainResolved = true;
                var library = Library;
                if (library != null && library.terrainMaterial != null)
                    return _terrain = library.terrainMaterial;

                var shader = RenderPipelineInfo.IsUrpActive
                    ? RenderPipelineInfo.Find(RenderPipelineInfo.UrpTerrainLit)
                    : RenderPipelineInfo.Find(RenderPipelineInfo.BuiltinTerrain);
                if (shader == null)
                    return null;

                _terrain = new Material(shader) { name = "HK_Terrain" };
                return _terrain;
            }
        }

        /// <summary>Dokulu opak Lit malzeme (doku + renk tonu başına önbellekli). texture null → Lit(tint).</summary>
        public static Material Textured(Texture2D texture, Color tint, float smoothness = 0.2f, float metallic = 0f, float tiling = 1f)
        {
            if (texture == null)
                return Lit(tint, smoothness, metallic);

            var key = new TextureKey(texture, Pack(tint), Quantize(smoothness) | (Quantize(metallic) << 8) | (Quantize(tiling / 16f) << 16));
            if (TexturedCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var material = CreateFromSpec(new MaterialSpec
            {
                Name = "Textured_" + texture.name,
                Color = tint,
                Smoothness = Mathf.Clamp01(smoothness),
                Metallic = Mathf.Clamp01(metallic),
                Shading = MaterialShading.Lit,
                Tiling = new Vector2(tiling, tiling)
            });
            ApplyTexture(material, texture, new Vector2(tiling, tiling));
            TexturedCache[key] = material;
            return material;
        }

        /// <summary>Dört renkli dijital kamuflaj Lit malzemesi (renkler + tohum başına önbellekli).</summary>
        public static Material Camo(Color a, Color b, Color c, Color d, int seed, float tiling = 2f)
        {
            var key = new CamoKey(Pack(a), Pack(b), Pack(c), Pack(d), seed, Quantize(tiling / 16f));
            if (CamoCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var spec = MaterialSpec.CamoSpec(MaterialId.CamoWoodland, a, b, c, d, seed);
            spec.Name = "Camo_" + seed;
            spec.Tiling = new Vector2(tiling, tiling);
            var material = CreateFromSpec(spec);
            CamoCache[key] = material;
            return material;
        }

        /// <summary>Verilen dokuyla parçacık malzemesi (toplamalı ya da alfa; doku başına önbellekli). null → yumuşak daire.</summary>
        public static Material Particle(Texture2D texture, bool additive)
        {
            if (texture == null)
                texture = ProceduralTextures.SoftCircle;

            var key = new TextureKey(texture, 0xFFFFFFFFu, additive ? 1 : 0);
            if (ParticleCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var spec = MaterialSpec.ParticleSpec(MaterialId.Smoke, Color.white, additive, MaterialTextureKey.None);
            spec.Name = (additive ? "ParticleAdditive_" : "ParticleAlpha_") + texture.name;
            var material = CreateFromSpec(spec);
            ApplyTexture(material, texture, Vector2.one);
            ParticleCache[key] = material;
            return material;
        }

        // ---------------------------------------------------------------- URP builders

        private static Material CreateUrpLit(MaterialSpec spec)
        {
            var shader = RenderPipelineInfo.FindFirst(RenderPipelineInfo.UrpLit, RenderPipelineInfo.UrpSimpleLit,
                RenderPipelineInfo.UrpUnlit, RenderPipelineInfo.BuiltinStandard);
            var m = new Material(shader);
            SetColor(m, spec.Color);
            SetFloat(m, SmoothnessId, Mathf.Clamp01(spec.Smoothness));
            SetFloat(m, GlossinessId, Mathf.Clamp01(spec.Smoothness));
            SetFloat(m, MetallicId, Mathf.Clamp01(spec.Metallic));
            SetFloat(m, ReceiveShadowsId, 1f);
            SetupUrpSurface(m, spec.Transparent, spec.Additive, spec.DoubleSided, true);
            SetupEmission(m, spec);
            ApplyTexture(m, ProceduralTextures.ForKey(spec.Texture, spec), spec.Tiling);
            ApplyPbr(m, spec.Pbr, spec.TextureSeed, spec.Tiling, spec.NormalStrength);
            ApplyDetail(m, spec);
            m.enableInstancing = !spec.Transparent;
            return m;
        }

        private static Material CreateUrpUnlit(MaterialSpec spec)
        {
            var shader = RenderPipelineInfo.FindFirst(RenderPipelineInfo.UrpUnlit, RenderPipelineInfo.UrpParticlesUnlit,
                RenderPipelineInfo.BuiltinSprites);
            var m = new Material(shader);
            SetColor(m, spec.Color);
            SetupUrpSurface(m, spec.Transparent, spec.Additive, spec.DoubleSided, false);
            ApplyTexture(m, ProceduralTextures.ForKey(spec.Texture, spec), spec.Tiling);
            m.enableInstancing = !spec.Transparent;
            return m;
        }

        private static Material CreateUrpParticle(MaterialSpec spec)
        {
            var shader = RenderPipelineInfo.FindFirst(RenderPipelineInfo.UrpParticlesUnlit, RenderPipelineInfo.UrpUnlit,
                RenderPipelineInfo.BuiltinSprites);
            var m = new Material(shader);
            SetColor(m, spec.Color);
            SetFloat(m, ColorModeId, 0f); // Multiply
            // Parçacıklar her zaman saydam ve çift taraflıdır (çizgi/iz/ağ parçacıkları için).
            SetupUrpSurface(m, true, spec.Additive, true, false);
            var tex = ProceduralTextures.ForKey(spec.Texture, spec);
            ApplyTexture(m, tex, spec.Tiling);
            return m;
        }

        /// <summary>URP Lit/Unlit/Particles yüzey durumu (editördeki BaseShaderGUI.SetupMaterialBlendMode eşdeğeri).</summary>
        private static void SetupUrpSurface(Material m, bool transparent, bool additive, bool doubleSided, bool castsShadows)
        {
            SetFloat(m, SurfaceId, transparent ? 1f : 0f);
            SetFloat(m, BlendId, additive ? 2f : 0f); // BlendMode: Alpha=0, Premultiply=1, Additive=2, Multiply=3
            SetFloat(m, CullId, doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
            SetFloat(m, AlphaClipId, 0f);
            m.DisableKeyword(KeywordAlphaTest);
            m.DisableKeyword(KeywordPremultiply);
            m.DisableKeyword(KeywordModulate);

            if (transparent)
            {
                SetFloat(m, SrcBlendId, (float)BlendMode.SrcAlpha);
                SetFloat(m, DstBlendId, additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
                SetFloat(m, SrcBlendAlphaId, (float)BlendMode.One);
                SetFloat(m, DstBlendAlphaId, additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
                SetFloat(m, ZWriteId, 0f);
                SetFloat(m, PreserveSpecularId, 0f);
                m.EnableKeyword(KeywordTransparent);
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetShaderPassEnabled("ShadowCaster", false);
                m.SetShaderPassEnabled("DepthOnly", false);
            }
            else
            {
                SetFloat(m, SrcBlendId, (float)BlendMode.One);
                SetFloat(m, DstBlendId, (float)BlendMode.Zero);
                SetFloat(m, SrcBlendAlphaId, (float)BlendMode.One);
                SetFloat(m, DstBlendAlphaId, (float)BlendMode.Zero);
                SetFloat(m, ZWriteId, 1f);
                m.DisableKeyword(KeywordTransparent);
                m.SetOverrideTag("RenderType", "Opaque");
                m.renderQueue = (int)RenderQueue.Geometry;
                m.SetShaderPassEnabled("ShadowCaster", castsShadows);
                m.SetShaderPassEnabled("DepthOnly", true);
            }
        }

        // ---------------------------------------------------------------- Built-in fallbacks

        private static Material CreateBuiltinLit(MaterialSpec spec)
        {
            var shader = RenderPipelineInfo.FindFirst(RenderPipelineInfo.BuiltinStandard, RenderPipelineInfo.BuiltinSprites);
            var m = new Material(shader);
            SetColor(m, spec.Color);
            SetFloat(m, GlossinessId, Mathf.Clamp01(spec.Smoothness));
            SetFloat(m, MetallicId, Mathf.Clamp01(spec.Metallic));

            if (spec.Transparent)
            {
                SetFloat(m, ModeId, 2f); // Fade
                SetFloat(m, SrcBlendId, (float)BlendMode.SrcAlpha);
                SetFloat(m, DstBlendId, spec.Additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
                SetFloat(m, ZWriteId, 0f);
                m.EnableKeyword(KeywordAlphaBlend);
                m.DisableKeyword(KeywordAlphaTest);
                m.DisableKeyword(KeywordPremultiply);
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
            }

            SetupEmission(m, spec);
            ApplyTexture(m, ProceduralTextures.ForKey(spec.Texture, spec), spec.Tiling);
            m.enableInstancing = !spec.Transparent;
            return m;
        }

        private static Material CreateBuiltinUnlit(MaterialSpec spec)
        {
            var tex = ProceduralTextures.ForKey(spec.Texture, spec);
            var needsBlend = spec.Transparent || tex != null || spec.DoubleSided;
            var shader = needsBlend
                ? RenderPipelineInfo.FindFirst(RenderPipelineInfo.BuiltinSprites, RenderPipelineInfo.BuiltinUnlitColor)
                : RenderPipelineInfo.FindFirst(RenderPipelineInfo.BuiltinUnlitColor, RenderPipelineInfo.BuiltinSprites);
            var m = new Material(shader);
            SetColor(m, spec.Color);
            ApplyTexture(m, tex, spec.Tiling);
            if (needsBlend)
                m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        private static Material CreateBuiltinParticle(MaterialSpec spec)
        {
            var shader = spec.Additive
                ? RenderPipelineInfo.FindFirst(RenderPipelineInfo.BuiltinParticlesAdditive, RenderPipelineInfo.BuiltinSprites)
                : RenderPipelineInfo.FindFirst(RenderPipelineInfo.BuiltinParticlesAlpha, RenderPipelineInfo.BuiltinSprites);
            var m = new Material(shader);
            SetColor(m, spec.Color);
            if (m.HasProperty(TintColorId))
                m.SetColor(TintColorId, spec.Color * 0.5f); // eski parçacık gölgelendiricileri 2× çarpar
            ApplyTexture(m, ProceduralTextures.ForKey(spec.Texture, spec), spec.Tiling);
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        // ---------------------------------------------------------------- Helpers

        private static void SetupEmission(Material m, MaterialSpec spec)
        {
            if (!m.HasProperty(EmissionColorId))
                return;

            if (spec.Emissive)
            {
                m.SetColor(EmissionColorId, spec.Emission);
                m.EnableKeyword(KeywordEmission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                m.SetColor(EmissionColorId, Color.black);
                m.DisableKeyword(KeywordEmission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }

        private static void SetColor(Material m, Color color)
        {
            if (m.HasProperty(BaseColorId))
                m.SetColor(BaseColorId, color);
            if (m.HasProperty(ColorId))
                m.SetColor(ColorId, color);
        }

        private static void SetFloat(Material m, int id, float value)
        {
            if (m.HasProperty(id))
                m.SetFloat(id, value);
        }

        /// <summary>
        /// Prosedürel normal + maske haritalarını Lit malzemeye bağlar ve anahtar kelimeleri açar:
        /// _NORMALMAP (_BumpMap), _METALLICSPECGLOSSMAP (R metalik, A pürüzsüzlük × _Smoothness), _OCCLUSIONMAP (G),
        /// ve _MaskMap/_MASKMAP özelliği olan gölgelendiriciler için maske. Özellik yoksa sessizce geçer.
        /// </summary>
        private static bool _triplanarWarned;

        /// <summary>
        /// C4: detay albedo/normal, makro varyasyon, texel density, triplanar. Hepsi spec'te varsayılan kapalıysa hiçbir şey yapmaz.
        /// URP/Lit özellik adları: _DetailAlbedoMap, _DetailNormalMap, _DetailAlbedoMapScale, _DetailNormalMapScale, _DETAIL_MULX2.
        /// </summary>
        public static void ApplyDetail(Material m, MaterialSpec spec)
        {
            if (m == null || spec == null)
                return;

            try
            {
                if (spec.TexelDensity > 0f && m.HasProperty(BaseMapId))
                {
                    var tex = m.GetTexture(BaseMapId);
                    var size = tex != null ? tex.width : ProceduralPbr.Resolution;
                    var t = MaterialMath.TilingForTexelDensity(spec.TexelDensity, spec.TileWorldMeters, size);
                    m.SetTextureScale(BaseMapId, new Vector2(t, t));
                    if (m.HasProperty(BumpMapId)) m.SetTextureScale(BumpMapId, new Vector2(t, t));
                    if (m.HasProperty(MaskMapId)) m.SetTextureScale(MaskMapId, new Vector2(t, t));
                }

                var detailAlbedo = spec.DetailAlbedo;
                var detailTiling = spec.DetailTiling;
                var albedoScale = spec.DetailAlbedoScale;
                if (detailAlbedo == null && spec.MacroVariation > 0.0001f)
                {
                    detailAlbedo = ProceduralTextures.ForKey(MaterialTextureKey.Noise, spec);
                    detailTiling = MaterialMath.MacroTiling(spec.MacroScaleMeters, spec.TileWorldMeters);
                    albedoScale = MaterialMath.MacroAlbedoScale(spec.MacroVariation);
                }

                if ((detailAlbedo != null || spec.DetailNormal != null) && m.HasProperty(DetailAlbedoMapId))
                {
                    if (detailAlbedo != null)
                    {
                        m.SetTexture(DetailAlbedoMapId, detailAlbedo);
                        m.SetTextureScale(DetailAlbedoMapId, new Vector2(detailTiling, detailTiling));
                        SetFloat(m, DetailAlbedoScaleId, albedoScale);
                    }
                    if (spec.DetailNormal != null && m.HasProperty(DetailNormalMapId))
                    {
                        m.SetTexture(DetailNormalMapId, spec.DetailNormal);
                        SetFloat(m, DetailNormalScaleId, spec.DetailNormalScale);
                    }
                    m.EnableKeyword("_DETAIL_MULX2");
                }

                if (spec.Triplanar)
                {
                    if (m.HasProperty(TriplanarId))
                        SetFloat(m, TriplanarId, 1f);
                    else if (!_triplanarWarned)
                    {
                        _triplanarWarned = true;
                        Debug.LogWarning("[MaterialLibrary] Triplanar isteniyor ama shader '_Triplanar' özelliğini desteklemiyor; UV kullanılacak.");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MaterialLibrary] ApplyDetail atlandı: " + e.Message);
            }
        }

        public static void ApplyPbr(Material m, PbrSurface surface, int seed, Vector2 tiling, float normalStrength = 1f)
        {
            if (m == null || surface == PbrSurface.None)
                return;

            var set = ProceduralPbr.Get(surface, seed);
            if (set == null || !set.IsValid)
                return;

            if (m.HasProperty(BumpMapId))
            {
                m.SetTexture(BumpMapId, set.Normal);
                m.SetTextureScale(BumpMapId, tiling);
                SetFloat(m, BumpScaleId, normalStrength);
                m.EnableKeyword(KeywordNormalMap);
            }

            if (m.HasProperty(MetallicGlossMapId))
            {
                m.SetTexture(MetallicGlossMapId, set.Mask);
                m.SetTextureScale(MetallicGlossMapId, tiling);
                m.EnableKeyword(KeywordMetallicGloss);
            }

            if (m.HasProperty(OcclusionMapId))
            {
                m.SetTexture(OcclusionMapId, set.Mask);
                m.SetTextureScale(OcclusionMapId, tiling);
                SetFloat(m, OcclusionStrengthId, 1f);
                m.EnableKeyword(KeywordOcclusion);
            }

            if (m.HasProperty(MaskMapId))
            {
                m.SetTexture(MaskMapId, set.Mask);
                m.SetTextureScale(MaskMapId, tiling);
                m.EnableKeyword(KeywordMaskMap);
            }
        }

        private static void ApplyTexture(Material m, Texture texture, Vector2 tiling)
        {
            if (texture == null)
                return;

            if (m.HasProperty(BaseMapId))
            {
                m.SetTexture(BaseMapId, texture);
                m.SetTextureScale(BaseMapId, tiling);
            }

            if (m.HasProperty(MainTexId))
            {
                m.SetTexture(MainTexId, texture);
                m.SetTextureScale(MainTexId, tiling);
            }
        }

        /// <summary>Malzemenin gölgelendiricisi etkin render hattında çalışır mı (URP gölgelendiricisi + yerleşik hat = hayır).</summary>
        private static bool IsCompatible(Material material)
        {
            var shader = material.shader;
            if (shader == null || !shader.isSupported)
                return false;

            if (RenderPipelineInfo.IsUrpActive)
                return true;

            var name = shader.name;
            return name == null || !name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal);
        }

        private static uint Pack(Color32 c) => (uint)(c.r | (c.g << 8) | (c.b << 16) | (c.a << 24));

        private static uint Pack(Color c) => Pack((Color32)c);

        private static uint Quantize(float v01) => (uint)Mathf.Clamp(Mathf.RoundToInt(v01 * 255f), 0, 255);

        private static int ComputeIdCount()
        {
            var max = 0;
            foreach (var value in Enum.GetValues(typeof(MaterialId)))
                max = Math.Max(max, (int)value);
            return max + 1;
        }

        private readonly struct TextureKey : IEquatable<TextureKey>
        {
            private readonly Texture2D _texture;
            private readonly uint _color;
            private readonly uint _extra;

            public TextureKey(Texture2D texture, uint color, uint extra)
            {
                _texture = texture;
                _color = color;
                _extra = extra;
            }

            public TextureKey(Texture2D texture, uint color, int extra) : this(texture, color, (uint)extra) { }

            public bool Equals(TextureKey other) => ReferenceEquals(_texture, other._texture) && _color == other._color && _extra == other._extra;

            public override bool Equals(object obj) => obj is TextureKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    var h = _texture is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_texture);
                    h = h * 397 ^ (int)_color;
                    return h * 397 ^ (int)_extra;
                }
            }
        }

        private readonly struct CamoKey : IEquatable<CamoKey>
        {
            private readonly uint _a, _b, _c, _d;
            private readonly int _seed;
            private readonly uint _tiling;

            public CamoKey(uint a, uint b, uint c, uint d, int seed, uint tiling)
            {
                _a = a; _b = b; _c = c; _d = d; _seed = seed; _tiling = tiling;
            }

            public bool Equals(CamoKey o) => _a == o._a && _b == o._b && _c == o._c && _d == o._d && _seed == o._seed && _tiling == o._tiling;

            public override bool Equals(object obj) => obj is CamoKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    var h = (int)_a;
                    h = h * 397 ^ (int)_b;
                    h = h * 397 ^ (int)_c;
                    h = h * 397 ^ (int)_d;
                    h = h * 397 ^ _seed;
                    return h * 397 ^ (int)_tiling;
                }
            }
        }

        // ---------------------------------------------------------------- Spec table

        private static Color Rgb(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);

        /// <summary>HDR renk: RGB yoğunlukla çarpılır, alfa 1 kalır (bloom için &gt; 1 değerler).</summary>
        private static Color Hdr(float r, float g, float b, float intensity) => new Color(r * intensity, g * intensity, b * intensity, 1f);

        private static MaterialSpec[] BuildSpecs()
        {
            var specs = new MaterialSpec[IdCount];

            void Add(MaterialSpec spec)
            {
                var i = (int)spec.Id;
                if (i >= 0 && i < specs.Length)
                    specs[i] = spec;
            }

            MaterialSpec L(MaterialId id, Color c, float smooth = 0.2f, float metal = 0f, MaterialTextureKey tex = MaterialTextureKey.None, float tiling = 1f)
                => MaterialSpec.LitSpec(id, c, smooth, metal, tex, (int)id * 7919 + 17, tiling);

            const MaterialTextureKey Detail = MaterialTextureKey.SurfaceDetail;

            // Temel renkler
            Add(L(MaterialId.White, Rgb(0.92f, 0.92f, 0.9f)));
            Add(L(MaterialId.Black, Rgb(0.05f, 0.05f, 0.05f)));
            Add(L(MaterialId.Red, Rgb(0.75f, 0.08f, 0.08f)));
            Add(L(MaterialId.Yellow, Rgb(0.95f, 0.78f, 0.15f)));
            Add(L(MaterialId.Blue, Rgb(0.15f, 0.35f, 0.75f)));
            Add(L(MaterialId.Green, Rgb(0.2f, 0.55f, 0.2f)));
            Add(L(MaterialId.Orange, Rgb(0.95f, 0.5f, 0.1f)));
            Add(L(MaterialId.Gray, Rgb(0.5f, 0.5f, 0.5f)));

            // Arazi / doğa
            Add(L(MaterialId.Grass, Rgb(0.29f, 0.36f, 0.17f), 0.12f, 0f, Detail, 2f));
            Add(L(MaterialId.DryGrass, Rgb(0.6f, 0.55f, 0.32f), 0.1f, 0f, Detail, 2f));
            Add(L(MaterialId.Dirt, Rgb(0.42f, 0.33f, 0.24f), 0.1f, 0f, Detail, 2f));
            Add(L(MaterialId.Mud, Rgb(0.27f, 0.21f, 0.15f), 0.25f, 0f, Detail, 2f));
            Add(L(MaterialId.Rock, Rgb(0.5f, 0.48f, 0.45f), 0.18f, 0f, Detail));
            Add(L(MaterialId.RockDark, Rgb(0.3f, 0.29f, 0.28f), 0.2f, 0f, Detail));
            Add(L(MaterialId.Sand, Rgb(0.76f, 0.68f, 0.5f), 0.08f, 0f, Detail, 2f));
            Add(L(MaterialId.Snow, Rgb(0.87f, 0.9f, 0.94f), 0.28f, 0f, Detail));
            Add(L(MaterialId.Gravel, Rgb(0.53f, 0.51f, 0.47f), 0.12f, 0f, MaterialTextureKey.Noise, 4f));
            Add(L(MaterialId.Asphalt, Rgb(0.19f, 0.19f, 0.2f), 0.14f, 0f, Detail, 4f));
            Add(L(MaterialId.Water, Rgb(0.13f, 0.3f, 0.36f, 0.78f), 0.92f).WithTransparent());
            Add(L(MaterialId.Foliage, Rgb(0.25f, 0.38f, 0.16f), 0.15f, 0f, Detail).WithDoubleSided());
            Add(L(MaterialId.FoliageDark, Rgb(0.15f, 0.26f, 0.12f), 0.15f, 0f, Detail).WithDoubleSided());
            Add(L(MaterialId.PineNeedles, Rgb(0.13f, 0.24f, 0.15f), 0.12f, 0f, Detail).WithDoubleSided());
            Add(L(MaterialId.Bark, Rgb(0.3f, 0.22f, 0.15f), 0.1f, 0f, Detail));
            Add(L(MaterialId.DeadWood, Rgb(0.45f, 0.4f, 0.33f), 0.1f, 0f, Detail));

            // Yapı
            Add(L(MaterialId.Concrete, Rgb(0.55f, 0.54f, 0.51f), 0.15f, 0f, Detail));
            Add(L(MaterialId.ConcreteDark, Rgb(0.38f, 0.38f, 0.37f), 0.15f, 0f, Detail));
            Add(L(MaterialId.Plaster, Rgb(0.74f, 0.71f, 0.64f), 0.12f, 0f, Detail));
            Add(L(MaterialId.PlasterWarm, Rgb(0.76f, 0.64f, 0.5f), 0.12f, 0f, Detail));
            Add(L(MaterialId.Stone, Rgb(0.6f, 0.56f, 0.5f), 0.15f, 0f, Detail));
            Add(L(MaterialId.StoneDark, Rgb(0.38f, 0.35f, 0.32f), 0.15f, 0f, Detail));
            Add(L(MaterialId.Brick, Rgb(0.6f, 0.3f, 0.22f), 0.12f, 0f, Detail));
            Add(L(MaterialId.RoofTile, Rgb(0.62f, 0.27f, 0.18f), 0.25f, 0f, Detail));
            Add(L(MaterialId.RoofMetal, Rgb(0.46f, 0.49f, 0.5f), 0.4f, 1.0f, Detail));
            Add(L(MaterialId.Wood, Rgb(0.55f, 0.4f, 0.25f), 0.2f, 0f, Detail));
            Add(L(MaterialId.WoodDark, Rgb(0.32f, 0.22f, 0.14f), 0.2f, 0f, Detail));
            Add(L(MaterialId.Glass, Rgb(0.62f, 0.76f, 0.82f, 0.32f), 0.95f).WithTransparent());
            Add(L(MaterialId.MetalPanel, Rgb(0.5f, 0.52f, 0.5f), 0.4f, 1.0f, Detail));
            Add(L(MaterialId.MetalDark, Rgb(0.5f, 0.5f, 0.52f), 0.35f, 1.0f, Detail));
            Add(L(MaterialId.Rust, Rgb(0.45f, 0.24f, 0.12f), 0.15f, 0.2f, MaterialTextureKey.Noise, 2f));
            Add(L(MaterialId.Hesco, Rgb(0.62f, 0.57f, 0.44f), 0.08f, 0f, Detail, 2f));
            Add(L(MaterialId.Sandbag, Rgb(0.66f, 0.6f, 0.45f), 0.08f, 0f, Detail, 2f));
            Add(L(MaterialId.CamoNet, Rgb(0.3f, 0.34f, 0.2f), 0.05f, 0f, MaterialTextureKey.Noise, 3f).WithDoubleSided());
            Add(L(MaterialId.TentCanvas, Rgb(0.4f, 0.42f, 0.28f), 0.1f, 0f, Detail).WithDoubleSided());
            Add(L(MaterialId.Hay, Rgb(0.8f, 0.68f, 0.35f), 0.08f, 0f, MaterialTextureKey.Noise, 2f));
            var flag = L(MaterialId.TurkishFlag, Color.white, 0.15f).WithDoubleSided();
            flag.Texture = MaterialTextureKey.TurkishFlag;
            Add(flag);
            Add(L(MaterialId.MosqueDome, Rgb(0.55f, 0.6f, 0.62f), 0.45f, 0.3f, Detail));
            Add(L(MaterialId.Carpet, Rgb(0.55f, 0.12f, 0.12f), 0.05f, 0f, MaterialTextureKey.Noise, 3f));

            // Araç
            Add(L(MaterialId.VehicleOlive, Rgb(0.3f, 0.33f, 0.2f), 0.3f, 0.1f, Detail));
            Add(L(MaterialId.VehicleTan, Rgb(0.65f, 0.58f, 0.42f), 0.3f, 0.1f, Detail));
            Add(L(MaterialId.VehicleDark, Rgb(0.16f, 0.17f, 0.15f), 0.3f, 0.15f, Detail));
            Add(L(MaterialId.Tire, Rgb(0.08f, 0.08f, 0.08f), 0.1f));
            Add(L(MaterialId.HeliOlive, Rgb(0.27f, 0.3f, 0.2f), 0.35f, 0.15f, Detail));
            Add(L(MaterialId.Windshield, Rgb(0.2f, 0.26f, 0.27f, 0.45f), 0.95f).WithTransparent());
            Add(L(MaterialId.RotorBlade, Rgb(0.1f, 0.1f, 0.1f), 0.3f).WithDoubleSided());

            // Silah
            Add(L(MaterialId.GunMetal, Rgb(0.5f, 0.5f, 0.52f), 0.62f, 0.9f));
            Add(L(MaterialId.GunPolymer, Rgb(0.08f, 0.08f, 0.08f), 0.25f));
            Add(L(MaterialId.GunWood, Rgb(0.4f, 0.25f, 0.14f), 0.35f, 0f, Detail));
            Add(L(MaterialId.GunTan, Rgb(0.6f, 0.52f, 0.38f), 0.25f));

            // Karakter — TSK dijital kamuflaj paletleri
            Add(MaterialSpec.CamoSpec(MaterialId.CamoWoodland,
                Rgb(0.36f, 0.38f, 0.24f), Rgb(0.36f, 0.27f, 0.18f), Rgb(0.18f, 0.24f, 0.14f), Rgb(0.08f, 0.08f, 0.07f), 101));
            Add(MaterialSpec.CamoSpec(MaterialId.CamoMountain,
                Rgb(0.5f, 0.48f, 0.38f), Rgb(0.35f, 0.33f, 0.26f), Rgb(0.24f, 0.27f, 0.18f), Rgb(0.62f, 0.58f, 0.48f), 202));
            Add(MaterialSpec.CamoSpec(MaterialId.CamoDesert,
                Rgb(0.74f, 0.66f, 0.5f), Rgb(0.62f, 0.52f, 0.36f), Rgb(0.5f, 0.42f, 0.3f), Rgb(0.82f, 0.76f, 0.62f), 303));
            Add(MaterialSpec.CamoSpec(MaterialId.CamoUrban,
                Rgb(0.55f, 0.56f, 0.56f), Rgb(0.35f, 0.36f, 0.37f), Rgb(0.18f, 0.19f, 0.2f), Rgb(0.72f, 0.73f, 0.74f), 404));
            Add(L(MaterialId.Skin, Rgb(0.74f, 0.56f, 0.44f), 0.4f));
            Add(L(MaterialId.SkinDark, Rgb(0.55f, 0.4f, 0.3f), 0.4f));
            Add(L(MaterialId.Gear, Rgb(0.25f, 0.27f, 0.18f), 0.15f, 0f, Detail));
            Add(L(MaterialId.Boots, Rgb(0.12f, 0.1f, 0.08f), 0.3f));
            Add(L(MaterialId.Beret, Rgb(0.42f, 0.07f, 0.1f), 0.08f, 0f, Detail)); // bordo bere
            Add(L(MaterialId.ArmbandBlue, Rgb(0.1f, 0.3f, 0.85f), 0.2f).WithEmission(Rgb(0.02f, 0.06f, 0.18f)));
            Add(L(MaterialId.ArmbandRed, Rgb(0.85f, 0.1f, 0.1f), 0.2f).WithEmission(Rgb(0.18f, 0.02f, 0.02f)));
            Add(L(MaterialId.ArmbandYellow, Rgb(0.95f, 0.8f, 0.1f), 0.2f).WithEmission(Rgb(0.18f, 0.15f, 0.02f)));
            Add(L(MaterialId.ArmbandGreen, Rgb(0.2f, 0.75f, 0.2f), 0.2f).WithEmission(Rgb(0.03f, 0.14f, 0.03f)));

            // Efekt / işaret
            Add(MaterialSpec.UnlitSpec(MaterialId.ZoneWall, Rgb(0.25f, 0.55f, 1f, 0.32f), true, true));
            Add(MaterialSpec.ParticleSpec(MaterialId.Tracer, Hdr(1f, 0.82f, 0.5f, 1.8f), true, MaterialTextureKey.SoftLine));
            Add(MaterialSpec.ParticleSpec(MaterialId.MuzzleFlash, Hdr(1f, 0.78f, 0.42f, 2f), true, MaterialTextureKey.Starburst));
            Add(MaterialSpec.ParticleSpec(MaterialId.Smoke, Rgb(0.78f, 0.78f, 0.75f, 0.65f), false, MaterialTextureKey.SmokePuff));
            Add(MaterialSpec.ParticleSpec(MaterialId.Fire, Hdr(1f, 0.55f, 0.18f, 1.8f), true, MaterialTextureKey.SoftCircle));
            Add(MaterialSpec.ParticleSpec(MaterialId.Blood, Rgb(0.42f, 0.02f, 0.02f, 0.9f), false, MaterialTextureKey.SoftCircle));
            Add(MaterialSpec.ParticleSpec(MaterialId.Spark, Hdr(1f, 0.85f, 0.5f, 2.2f), true, MaterialTextureKey.SoftCircle));
            Add(MaterialSpec.ParticleSpec(MaterialId.BulletHole, Rgb(1f, 1f, 1f, 0.95f), false, MaterialTextureKey.BulletHole));
            Add(MaterialSpec.ParticleSpec(MaterialId.LootHighlight, Rgb(1f, 0.85f, 0.35f, 0.55f), true, MaterialTextureKey.None));
            Add(MaterialSpec.UnlitSpec(MaterialId.AllyMarker, Rgb(0.3f, 0.78f, 1f, 0.9f), true));
            Add(MaterialSpec.UnlitSpec(MaterialId.EnemyMarker, Rgb(1f, 0.25f, 0.2f, 0.9f), true));
            Add(MaterialSpec.UnlitSpec(MaterialId.LandingZone, Rgb(0.25f, 1f, 0.4f, 0.35f), true, true));
            Add(L(MaterialId.Parachute, Rgb(0.36f, 0.4f, 0.26f), 0.12f, 0f, Detail).WithDoubleSided());

            // PBR yüzeyleri: klasik Detail dokusu olanlar prosedürel PBR albedo'ya yükselir (kamuflaj kendi dokusunu korur).
            for (var i = 0; i < specs.Length; i++)
            {
                var spec = specs[i];
                if (spec == null || spec.Shading != MaterialShading.Lit || spec.Transparent || spec.Particle)
                    continue;

                var surface = ProceduralPbr.SurfaceFor(spec.Id);
                if (surface == PbrSurface.None)
                    continue;

                spec.Pbr = surface;
                if (spec.Texture != MaterialTextureKey.DigitalCamo)
                    spec.Texture = MaterialTextureKey.PbrAlbedo;
            }

            // Enum sona eklenip tablo güncellenmezse: gri yedek.
            for (var i = 0; i < specs.Length; i++)
            {
                if (specs[i] == null)
                {
                    var spec = MaterialSpec.LitSpec((MaterialId)i, new Color(0.5f, 0.5f, 0.5f));
                    specs[i] = spec;
                }
            }

            return specs;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Etki alanı yeniden yüklenmeden oynatma: kütüphane yeniden çözülsün; yok edilmiş malzemeler null kontrolüyle yeniden üretilir.
            _libraryResolved = false;
            _library = null;
            Array.Clear(ById, 0, ById.Length);
            _particleAdditive = null;
            _particleAlpha = null;
            _terrain = null;
            _terrainResolved = false;
        }
    }
}
