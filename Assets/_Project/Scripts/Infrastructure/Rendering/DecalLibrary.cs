using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// GX8: kodla üretilen çıkartma dokuları + URP Decal malzemeleri (tür x varyant, tembel ve önbellekli).
    /// Malzeme gölgelendiricisi URP "Shader Graphs/Decal" (URP paketiyle gelir). Bulunamazsa tek uyarı + null döner;
    /// çağıran (DecalScatter) mevcut yola (VfxMaterials.CreateDecal ile düz quad) düşer.
    /// </summary>
    public static class DecalLibrary
    {
        public const string DecalShaderName = "Shader Graphs/Decal";

        private sealed class Entry
        {
            public Texture2D Albedo;
            public Texture2D Normal;
            public Texture2D Mask;
            public Material Material;
        }

        private static readonly Dictionary<int, Entry> Cache = new Dictionary<int, Entry>();
        private static Shader _shader;
        private static bool _shaderTried;

        private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int NormalMapId = Shader.PropertyToID("_NormalMap");
        private static readonly int MaskMapId = Shader.PropertyToID("_MaskMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int NormalBlendId = Shader.PropertyToID("_NormalBlend");
        private static readonly int AffectsBaseId = Shader.PropertyToID("_AffectBaseColor");
        private static readonly int AffectsMaskId = Shader.PropertyToID("_AffectMaskmap");

        public static int VariantCount => DecalLibraryMath.VariantCount;

        private static int Key(DecalKind kind, int variant)
        {
            return (int)kind * 16 + Mathf.Abs(variant) % DecalLibraryMath.VariantCount;
        }

        private static Entry GetEntry(DecalKind kind, int variant)
        {
            var key = Key(kind, variant);
            if (Cache.TryGetValue(key, out var e) && e != null && e.Albedo != null)
                return e;

            var seed = 1000 + (int)kind * 97 + (key & 15) * 13;
            var size = DecalLibraryMath.TextureSize(kind);
            DecalLibraryMath.BuildMaps(kind, seed, size, out var albedo, out var normal, out var mask);
            e = new Entry
            {
                Albedo = MakeTexture(kind + "_Albedo_" + (key & 15), size, albedo, false),
                Normal = normal != null ? MakeTexture(kind + "_Normal_" + (key & 15), size, normal, true) : null,
                Mask = MakeTexture(kind + "_Mask_" + (key & 15), size, mask, true)
            };
            Cache[key] = e;
            return e;
        }

        private static Texture2D MakeTexture(string name, int size, Color32[] pixels, bool linear)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, linear)
            {
                name = "Decal_" + name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4,
                hideFlags = HideFlags.DontSave
            };
            tex.SetPixels32(pixels);
            tex.Apply(true, false);
            return tex;
        }

        /// <summary>Albedo dokusu (RGBA; alfa = kapsama).</summary>
        public static Texture2D GetAlbedo(DecalKind kind, int variant)
        {
            return GetEntry(kind, variant).Albedo;
        }

        /// <summary>URP Decal malzemesi (DecalProjector için). Gölgelendirici yoksa null.</summary>
        public static Material GetMaterial(DecalKind kind, int variant)
        {
            var e = GetEntry(kind, variant);
            if (e.Material != null)
                return e.Material;

            var shader = FindShader();
            if (shader == null)
                return null;

            var m = new Material(shader) { name = "Decal_" + kind + "_" + (Key(kind, variant) & 15), hideFlags = HideFlags.DontSave };
            SetTex(m, BaseColorMapId, e.Albedo);
            SetTex(m, BaseMapId, e.Albedo);
            SetTex(m, MaskMapId, e.Mask);
            if (e.Normal != null)
            {
                SetTex(m, NormalMapId, e.Normal);
                if (m.HasProperty(NormalBlendId))
                    m.SetFloat(NormalBlendId, 1f);
            }
            if (m.HasProperty(BaseColorId))
                m.SetColor(BaseColorId, Color.white);
            if (m.HasProperty(AffectsBaseId))
                m.SetFloat(AffectsBaseId, 1f);
            if (m.HasProperty(AffectsMaskId))
                m.SetFloat(AffectsMaskId, 1f);
            m.enableInstancing = true;
            e.Material = m;
            return m;
        }

        /// <summary>Quad yedeği için tek sorgu: gölgelendirici mevcut mu.</summary>
        public static bool DecalShaderAvailable => FindShader() != null;

        private static void SetTex(Material m, int id, Texture t)
        {
            if (t != null && m.HasProperty(id))
                m.SetTexture(id, t);
        }

        private static Shader FindShader()
        {
            if (_shaderTried)
                return _shader;
            _shaderTried = true;
            _shader = Shader.Find(DecalShaderName);
            if (_shader == null)
                Debug.LogWarning("[DecalLibrary] '" + DecalShaderName + "' bulunamadı; çıkartmalar düz quad yedeğiyle çizilecek.");
            return _shader;
        }

        /// <summary>Önbelleği (doku + malzeme) boşaltır (sahne/harita değişimi).</summary>
        public static void Clear()
        {
            foreach (var kv in Cache)
            {
                var e = kv.Value;
                if (e == null)
                    continue;
                Kill(e.Material);
                Kill(e.Albedo);
                Kill(e.Normal);
                Kill(e.Mask);
            }
            Cache.Clear();
            _shaderTried = false;
            _shader = null;
        }

        private static void Kill(Object o)
        {
            if (o == null)
                return;
            if (UnityEngine.Application.isPlaying)
                Object.Destroy(o);
            else
                Object.DestroyImmediate(o);
        }
    }
}
