using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.Weapons
{
    /// <summary>Silah yüzey türü (HAREKAT/Weapon/GunLit _GunKind ile aynı sıra).</summary>
    public enum GunKind
    {
        ParkerizedSteel = 0,
        AnodizedAluminum = 1,
        Polymer = 2,
        Wood = 3,
        Paint = 4,
        Rubber = 5
    }

    /// <summary>
    /// Silah malzemeleri fabrikası: HAREKAT/Weapon/GunLit (kenar aşınma, mikro doku, yağ, karbon) ve HAREKAT/Weapon/OpticGlass.
    /// Gölgelendirici bulunamazsa tek uyarı + MaterialLibrary.Lit yedeği. Aynı parametreler aynı malzemeyi paylaşır.
    /// </summary>
    public static class GunMaterials
    {
        public const string GunShaderName = "HAREKAT/Weapon/GunLit";
        public const string GlassShaderName = "HAREKAT/Weapon/OpticGlass";

        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        private static bool _warned;

        /// <summary>Metal üst sınırı: tam metal difüz ışığı sıfırlar, yansıma probu yoksa silah siyah kalır.</summary>
        public const float MetallicCap = 0.55f;
        /// <summary>Dünya/TP silahı için asgari albedo parlaklığı (sRGB gri); siluet 30-100 m okunur.</summary>
        public const float MinAlbedoLuma = 0.05f;
        public const float MaxAlbedoLuma = 0.62f;

        /// <summary>Saf parametre tablosu (testler okur). Sıra: key, kind, albedo, smoothness, metallic.</summary>
        public struct Param
        {
            public string Key; public GunKind Kind; public Color Albedo; public float Smoothness; public float Metallic;
            public Param(string key, GunKind kind, Color albedo, float smoothness, float metallic)
            { Key = key; Kind = kind; Albedo = albedo; Smoothness = smoothness; Metallic = metallic; }
        }

        public static readonly Param[] Table =
        {
            new Param("steel", GunKind.ParkerizedSteel, new Color(0.075f, 0.075f, 0.078f), 0.42f, 0.45f),
            new Param("anod", GunKind.AnodizedAluminum, new Color(0.07f, 0.07f, 0.072f), 0.42f, 0.45f),
            new Param("poly", GunKind.Polymer, new Color(0.09f, 0.09f, 0.09f), 0.38f, 0f),
            new Param("tan", GunKind.Paint, new Color(0.62f, 0.52f, 0.37f), 0.3f, 0f),
            new Param("olive", GunKind.Paint, new Color(0.30f, 0.34f, 0.22f), 0.26f, 0f),
            new Param("wood", GunKind.Wood, new Color(0.42f, 0.26f, 0.14f), 0.38f, 0f),
            new Param("woodd", GunKind.Wood, new Color(0.27f, 0.16f, 0.09f), 0.34f, 0f),
            new Param("rubber", GunKind.Rubber, new Color(0.07f, 0.07f, 0.07f), 0.1f, 0f),
        };

        public static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        public static Param Get(string key)
        {
            for (int i = 0; i < Table.Length; i++)
                if (Table[i].Key == key) return Table[i];
            return Table[0];
        }

        public static Material ParkerizedSteel(float wear = 0.55f) =>
            Make(Get("steel"), wear, new Color(0.30f, 0.30f, 0.30f), 0.5f, 0.45f);

        public static Material DarkAnodized(float wear = 0.45f) =>
            Make(Get("anod"), wear, new Color(0.30f, 0.30f, 0.30f), 0.5f, 0.45f);

        public static Material Polymer(float wear = 0.4f) =>
            Make(Get("poly"), wear, new Color(0.16f, 0.16f, 0.16f), 0f, 0.4f);

        public static Material TanPaint(float wear = 0.55f) =>
            Make(Get("tan"), wear, new Color(0.2f, 0.2f, 0.2f), 0.5f, 0.45f);

        public static Material OlivePaint(float wear = 0.6f) =>
            Make(Get("olive"), wear, new Color(0.2f, 0.2f, 0.2f), 0.5f, 0.45f);

        public static Material WoodGrain(float wear = 0.35f) =>
            Make(Get("wood"), wear, new Color(0.5f, 0.34f, 0.2f), 0f, 0.2f);

        public static Material DarkWoodGrain(float wear = 0.35f) =>
            Make(Get("woodd"), wear, new Color(0.38f, 0.25f, 0.14f), 0f, 0.2f);

        public static Material RubberGrip(float wear = 0.2f) =>
            Make(Get("rubber"), wear, new Color(0.12f, 0.12f, 0.12f), 0f, 0.14f);

        /// <summary>Optik cam: koyu taban, mavi/amber kaplama tonu.</summary>
        public static Material OpticGlass(bool dark)
        {
            var key = dark ? "glassd" : "glass";
            if (Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var shader = Shader.Find(GlassShaderName);
            Material m;
            if (shader == null)
            {
                Warn(GlassShaderName);
                m = MaterialLibrary.Lit(dark ? new Color(0.015f, 0.025f, 0.035f) : new Color(0.05f, 0.09f, 0.13f), 0.96f, 0.4f);
            }
            else
            {
                m = new Material(shader) { name = "OpticGlass_" + key, hideFlags = HideFlags.DontSave, enableInstancing = true };
                m.SetColor("_BaseColor", dark ? new Color(0.012f, 0.02f, 0.03f) : new Color(0.03f, 0.05f, 0.07f));
                m.SetFloat("_Smoothness", 0.97f);
                m.SetFloat("_Metallic", 0.35f);
                m.SetColor("_CoatTint", dark ? new Color(0.12f, 0.2f, 0.55f) : new Color(0.15f, 0.32f, 0.8f));
                m.SetColor("_CoatTint2", dark ? new Color(0.55f, 0.32f, 0.1f) : new Color(0.85f, 0.5f, 0.12f));
            }

            Cache[key] = m;
            return m;
        }

        /// <summary>MaterialLibrary silah kimliklerini (GunMetal/GunPolymer/GunWood/GunTan) yeni malzemelere eşler; diğerleri null.</summary>
        public static Material ForId(MaterialId id)
        {
            switch (id)
            {
                case MaterialId.GunMetal: return ParkerizedSteel();
                case MaterialId.GunPolymer: return Polymer();
                case MaterialId.GunWood: return WoodGrain();
                case MaterialId.GunTan: return TanPaint();
                default: return null;
            }
        }

        private static Material Make(Param p, float wear, Color wearColor, float wearMetal, float wearSmooth)
        {
            string key = p.Key; GunKind kind = p.Kind; Color baseColor = p.Albedo; float smoothness = p.Smoothness; float metallic = Mathf.Min(p.Metallic, MetallicCap);
            var cacheKey = key + "_" + Mathf.RoundToInt(wear * 100f);
            if (Cache.TryGetValue(cacheKey, out var cached) && cached != null)
                return cached;

            var shader = Shader.Find(GunShaderName);
            Material m;
            if (shader == null)
            {
                Warn(GunShaderName);
                m = MaterialLibrary.Lit(baseColor, smoothness, metallic);
            }
            else
            {
                m = new Material(shader) { name = "Gun_" + cacheKey, hideFlags = HideFlags.DontSave, enableInstancing = true };
                m.SetColor("_BaseColor", baseColor);
                m.SetFloat("_Smoothness", smoothness);
                m.SetFloat("_Metallic", metallic);
                m.SetFloat("_GunKind", (float)kind);
                m.SetFloat("_WearAmount", Mathf.Clamp01(wear));
                m.SetColor("_WearColor", wearColor);
                m.SetFloat("_WearMetallic", wearMetal);
                m.SetFloat("_WearSmoothness", wearSmooth);
                m.SetFloat("_SheenAmount", kind == GunKind.ParkerizedSteel || kind == GunKind.AnodizedAluminum ? 0.55f : 0.3f);
            }

            Cache[cacheKey] = m;
            return m;
        }

        private static void Warn(string name)
        {
            if (_warned)
                return;
            _warned = true;
            Debug.LogWarning("[GunMaterials] Gölgelendirici bulunamadı: " + name + " — MaterialLibrary.Lit yedeği kullanılıyor.");
        }

        /// <summary>Test/yeniden kurulum için önbelleği boşaltır (malzemeler yok edilmez).</summary>
        public static void ClearCache()
        {
            Cache.Clear();
            _warned = false;
        }
    }
}
