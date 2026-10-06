using System;
using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.Characters
{
    /// <summary>
    /// Prosedürel asker malzemeleri (HAREKAT/Character/* shader'ları): kumaş, ten, kask boyası, cordura, kauçuk, deri, NVG lensi.
    /// Her malzeme (tür + renk + kamuflaj tohumu) başına bir kez üretilir ve paylaşılır. Shader bulunamazsa tek uyarı verilir
    /// ve null döner; çağıran MaterialLibrary.Lit'e düşer. Humanoid (ContentOverrides) yolu bu sınıfı kullanmaz.
    /// </summary>
    public static class CharacterMaterials
    {
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Shader> Shaders = new Dictionary<string, Shader>();
        private static readonly HashSet<string> Warned = new HashSet<string>();

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        private static readonly int MacroMapId = Shader.PropertyToID("_MacroMap");

        /// <summary>Kumaş shader'ı için shader yolu.</summary>
        /// <summary>
        /// GEÇİCİ GÜVENLİ MOD: Skin ve Fabric özel shader'ları build'de üniformayı pembe-beyaz/parlak beyaz çiziyor
        /// (menu5/bench5 kareleri; kök neden bulunamadı — aday: _BumpMap örnekleme/paketleme hatası). Kök neden Unity'de
        /// doğrulanana kadar bu iki tür URP/Lit kullanır (albedo+normal yine bağlanır; sheen/SSS kaybı kabul edildi).
        /// Özel shader'a dönmek için <see cref="UseCustomClothSkinShaders"/> true yapılır.
        /// </summary>
        public static bool UseCustomClothSkinShaders;

        public static string ShaderPath(CharacterMaterialKind kind)
        {
            switch (kind)
            {
                case CharacterMaterialKind.Skin: return UseCustomClothSkinShaders ? "HAREKAT/Character/Skin" : "Universal Render Pipeline/Lit";
                case CharacterMaterialKind.NvgLens: return "HAREKAT/Character/Nvg";
                case CharacterMaterialKind.Fabric: return UseCustomClothSkinShaders ? "HAREKAT/Character/Fabric" : "Universal Render Pipeline/Lit";
                default: return "HAREKAT/Character/Gear";
            }
        }

        /// <summary>Dijital kamuflaj kumaşı (4 renk + tohum başına paylaşımlı). Başarısız → null.</summary>
        public static Material Camo(Color a, Color b, Color c, Color d, int seed, float wear = 0f)
        {
            wear = WearRules.Quantize(wear);
            var baseKey = "camo|" + ColorUtility.ToHtmlStringRGB(a) + ColorUtility.ToHtmlStringRGB(b) + ColorUtility.ToHtmlStringRGB(c) + ColorUtility.ToHtmlStringRGB(d) + "|" + seed;
            var key = wear > 0f ? baseKey + "|w" + wear.ToString("0.00") : baseKey;
            return GetOrCreate(key, CharacterMaterialKind.Fabric, Color.white, () =>
            {
                var tex = ProceduralTextures.DigitalCamoFabric(a, b, c, d, seed);
                return wear > 0f ? CharacterTextureGen.UniformWearAlbedo(tex, baseKey, wear, seed) : tex;
            }, seed, null, wear);
        }

        /// <summary>Düz renkli malzeme (kumaş, ten, boya, cordura, kauçuk, deri). Başarısız → null.</summary>
        public static Material Solid(CharacterMaterialKind kind, Color color, float smoothnessOverride = -1f, float wear = 0f)
        {
            wear = WearRules.Quantize(wear);
            var key = kind + "|" + ColorUtility.ToHtmlStringRGB(color) + "|" + smoothnessOverride.ToString("0.00");
            if (wear > 0f)
                key += "|w" + wear.ToString("0.00");
            Func<Texture2D> albedo = null;
            var tint = color;
            if (wear > 0f && CharacterTextureGen.GearWearAlbedo(kind, wear) != null)
            {
                albedo = () => CharacterTextureGen.GearWearAlbedo(kind, wear);
                // kazıma tabanı (<1) rengi koyultmasın: taban kadar açılarak telafi.
                if (kind == CharacterMaterialKind.HelmetPaint || kind == CharacterMaterialKind.Cordura)
                {
                    var k = 1f / CharacterTextureGen.GearScratchBase;
                    tint = new Color(Mathf.Clamp01(color.r * k), Mathf.Clamp01(color.g * k), Mathf.Clamp01(color.b * k), color.a);
                }
            }

            return GetOrCreate(key, kind, tint, albedo, 17, smoothnessOverride >= 0f ? (float?)smoothnessOverride : null, wear);
        }

        /// <summary>
        /// Yıpranmış yüz/kafa teni: yıpranma 0 ise paylaşılan düz <see cref="Skin"/>. Aksi halde kan izi/barut isi/ter parlaması
        /// dokulu ten (yalnızca baş kabuğuna bağlanır; baş UV'sine tiling (2,4) offset (0,0.1) ile eşlenir). Başarısız → null.
        /// </summary>
        public static Material SkinFace(Color skin, float wear, int variant)
        {
            wear = WearRules.Quantize(wear);
            if (wear <= 0f)
                return Skin(skin);
            var smooth = CharacterMaterialMath.Defaults(CharacterMaterialKind.Skin).Smoothness + WearRules.SkinSmoothnessBoost(wear);
            var key = "skinface|" + ColorUtility.ToHtmlStringRGB(skin) + "|" + wear.ToString("0.00") + "|" + variant;
            var m = GetOrCreate(key, CharacterMaterialKind.Skin, skin, () => CharacterTextureGen.FaceWearAlbedo(skin, wear, variant), 17, smooth, wear);
            if (m != null && m.HasProperty(BaseMapId))
            {
                m.SetTextureScale(BaseMapId, new Vector2(CharacterTextureGen.FaceUScale, CharacterTextureGen.FaceVScale));
                m.SetTextureOffset(BaseMapId, new Vector2(0f, CharacterTextureGen.FaceVOffset));
            }

            return m;
        }

        /// <summary>Bot/ten/kumaş için hızlı kısayollar.</summary>
        public static Material Skin(Color color) => Solid(CharacterMaterialKind.Skin, color);
        public static Material NvgLens() => Solid(CharacterMaterialKind.NvgLens, new Color(0.03f, 0.05f, 0.04f));

        private static Material GetOrCreate(string key, CharacterMaterialKind kind, Color color, Func<Texture2D> albedo, int seed, float? smoothness, float wear = 0f)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            try
            {
                var shader = FindShader(ShaderPath(kind));
                if (shader == null)
                    return null;

                var m = new Material(shader) { name = "HK_Char_" + key, hideFlags = HideFlags.DontSave };
                Apply(m, kind, color, albedo != null ? albedo() : null, seed, smoothness, wear);
                Cache[key] = m;
                return m;
            }
            catch (Exception e)
            {
                WarnOnce("create|" + kind, "[CharacterMaterials] " + kind + " malzemesi üretilemedi, URP/Lit'e dönülüyor: " + e.Message);
                return null;
            }
        }

        /// <summary>Sert ekipman (kask/cordura/kauçuk/deri) tabanı gün ışığında siluet kalmasın diye hafif açılır; kumaş/ten/lens değişmez. Kanallar 0..1.</summary>
        public static Color GearBoost(CharacterMaterialKind kind, Color c)
        {
            if (kind == CharacterMaterialKind.Fabric || kind == CharacterMaterialKind.Skin || kind == CharacterMaterialKind.NvgLens)
                return c;
            const float k = 1.25f;
            return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);
        }

        /// <summary>Malzemeye tür varsayılanlarını, normal/makro dokuları ve renkleri uygular (yalnızca var olan özellikleri).</summary>
        public static void Apply(Material m, CharacterMaterialKind kind, Color color, Texture2D albedo, int seed, float? smoothness, float wear = 0f)
        {
            var p = CharacterMaterialMath.Defaults(kind);
            if (smoothness.HasValue)
                p.Smoothness = Mathf.Clamp01(smoothness.Value);

            color = GearBoost(kind, color);
            SetColor(m, BaseColorId, color);
            SetColor(m, ColorId, color);
            if (albedo != null && m.HasProperty(BaseMapId))
                m.SetTexture(BaseMapId, albedo);
            // GÜVENLİ MOD: Skin/Fabric'te normal haritası BAĞLANMAZ. Bazı prosedürel gövde kabuklarının tangent'ları
            // bozuk (NaN) üretilebiliyor; _NORMALMAP açıkken URP/Lit aydınlatması NaN'a düşüp bloom'da bembeyaz
            // patlıyor (bench8/menu8 kareleri). Normal haritasız Lit tangent kullanmaz → patlama kökten biter.
            var normalIstenir = UseCustomClothSkinShaders || (kind != CharacterMaterialKind.Skin && kind != CharacterMaterialKind.Fabric);
            if (normalIstenir && m.HasProperty(BumpMapId))
            {
                m.SetTexture(BumpMapId, CharacterTextureGen.Normal(kind));
                m.EnableKeyword("_NORMALMAP");
            }
            if (m.HasProperty(MacroMapId))
                m.SetTexture(MacroMapId, CharacterTextureGen.Macro);

            // Yakın plan: ten pürüzsüz (gözenek/statik yok), kumaş dokuması iri ve çok hafif (ince taneli parazit yok).
            if (kind == CharacterMaterialKind.Skin)
            {
                p.BumpScale = 0.18f; p.DetailTiling = 3f;
            }
            else if (kind == CharacterMaterialKind.Fabric)
            {
                p.BumpScale = 0.35f; p.DetailTiling = 7f;
            }

            if (wear > 0f)
            {
                // Yıpranma (SINIRLI görsel güç, bkz. WearRules.VisualStrength/GearDirt/GearDust/GearEdgeWear): kir/toz hafif artar;
                // sert ekipmanda kenar aşınması (shader _EdgeWear) İNCE kalır.
                p.DirtStrength = WearRules.GearDirt(p.DirtStrength, wear);
                p.Dust = WearRules.GearDust(p.Dust, wear);
                if (kind == CharacterMaterialKind.HelmetPaint || kind == CharacterMaterialKind.Cordura)
                    p.EdgeWear = WearRules.GearEdgeWear(p.EdgeWear, wear);
            }

            SetF(m, "_Smoothness", p.Smoothness);
            SetF(m, "_Metallic", p.Metallic);
            SetF(m, "_BumpScale", p.BumpScale);
            SetF(m, "_DetailTiling", p.DetailTiling);
            SetF(m, "_MacroTiling", p.MacroTiling);
            SetF(m, "_SheenStrength", p.SheenStrength);
            SetF(m, "_SheenPower", p.SheenPower);
            SetF(m, "_WrapAmount", p.Wrap);
            SetF(m, "_SssStrength", p.SssStrength);
            SetF(m, "_DirtStrength", p.DirtStrength);
            SetF(m, "_DirtStart", p.DirtStart);
            SetF(m, "_DirtSpan", p.DirtSpan);
            SetF(m, "_DustStrength", p.Dust);
            SetF(m, "_WetResponse", p.WetResponse);
            SetF(m, "_WetDarken", p.WetDarken);
            SetF(m, "_EdgeWear", p.EdgeWear);
            SetF(m, "_LensEmission", p.LensEmission);

            // Kir ve aşınma renkleri taban renkten türer (kamuflaj üstünde toprak, boyada açık astar).
            SetColor(m, Shader.PropertyToID("_DirtColor"), kind == CharacterMaterialKind.Skin
                ? new Color(0.3f, 0.22f, 0.16f)
                : Color.Lerp(new Color(0.34f, 0.27f, 0.18f), color, 0.3f));
            SetColor(m, Shader.PropertyToID("_WearColor"), Color.Lerp(new Color(0.3f, 0.29f, 0.26f), color, 0.25f));
            // Kumaş parlaması kumaş tonunda kalır (beyaz/pembe kayma yok); mat görünüm için kısık.
            SetColor(m, Shader.PropertyToID("_SheenColor"), Color.Lerp(Color.white, color, kind == CharacterMaterialKind.Skin ? 0.5f : 0.85f) * 0.6f);
            SetColor(m, Shader.PropertyToID("_SssColor"), new Color(0.78f, 0.42f, 0.3f));
        }

        /// <summary>Shader'ı bulur; yoksa tek uyarı + null.</summary>
        public static Shader FindShader(string path)
        {
            if (Shaders.TryGetValue(path, out var s) && s != null)
                return s;

            s = Shader.Find(path);
            if (s == null)
            {
                WarnOnce("shader|" + path, "[CharacterMaterials] Shader bulunamadı: " + path + " — URP/Lit yedeği kullanılıyor.");
                return null;
            }

            Shaders[path] = s;
            return s;
        }

        private static void WarnOnce(string key, string message)
        {
            if (Warned.Add(key))
                Debug.LogWarning(message);
        }

        private static void SetF(Material m, string name, float value)
        {
            if (m.HasProperty(name))
                m.SetFloat(name, value);
        }

        private static void SetColor(Material m, int id, Color c)
        {
            if (m.HasProperty(id))
                m.SetColor(id, c);
        }
    }
}
