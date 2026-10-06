using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>Malzemenin taban dokusu (ProceduralTextures.ForKey ile çözülür).</summary>
    public enum MaterialTextureKey
    {
        None = 0,
        Noise,
        SurfaceDetail,
        DigitalCamo,
        TurkishFlag,
        SoftCircle,
        Circle,
        Ring,
        SoftLine,
        SmokePuff,
        BulletHole,
        Starburst,
        WhitePixel,
        /// <summary>Prosedürel PBR albedo (Pbr yüzeyine göre; normal + maske haritaları da eklenir).</summary>
        PbrAlbedo
    }

    /// <summary>Gölgelendirme türü.</summary>
    public enum MaterialShading
    {
        /// <summary>URP Lit (PBR, ışık + gölge alır).</summary>
        Lit = 0,
        /// <summary>URP Unlit (ışıktan etkilenmez).</summary>
        Unlit,
        /// <summary>URP Particles/Unlit (köşe rengi × doku × renk; parçacık, çizgi, iz).</summary>
        Particle
    }

    /// <summary>
    /// Bir malzemenin üretim tarifi. MaterialLibrary her MaterialId için bir tarif tutar; editör kurulumu aynı tariften
    /// kalıcı malzeme varlıkları üretir (GameArtLibrary). Tarif değiştirilebilir bir kopyadır.
    /// </summary>
    public sealed class MaterialSpec
    {
        public MaterialId Id;
        public string Name;

        /// <summary>Taban rengi (sRGB). Alfa yalnızca Transparent/Particle için anlamlıdır.</summary>
        public Color Color = Color.white;
        public float Smoothness = 0.2f;
        public float Metallic;

        public MaterialShading Shading = MaterialShading.Lit;

        /// <summary>Saydam yüzey (_Surface=1, ZWrite kapalı, kuyruk 3000).</summary>
        public bool Transparent;

        /// <summary>Saydam modda toplamalı karışım (SrcAlpha, One). Değilse alfa karışımı.</summary>
        public bool Additive;

        /// <summary>Çift taraflı (_Cull=0) — yaprak, kumaş, bayrak, bölge duvarı.</summary>
        public bool DoubleSided;

        /// <summary>Işıma (HDR). Emission.maxColorComponent &gt; 0 ise _EMISSION açılır.</summary>
        public Color Emission = Color.black;

        public MaterialTextureKey Texture = MaterialTextureKey.None;
        public Vector2 Tiling = Vector2.one;
        public int TextureSeed;

        /// <summary>Prosedürel PBR yüzeyi: None dışındaysa Lit malzemeye normal (_NORMALMAP) ve maske (metalik/AO/pürüzsüzlük) haritası eklenir.</summary>
        public PbrSurface Pbr = PbrSurface.None;

        /// <summary>Normal harita gücü (_BumpScale).</summary>
        public float NormalStrength = 1f;

        // ---- C4: detay / triplanar / makro / texel density (hepsi varsayılan kapalı; override yoksa davranış değişmez)

        /// <summary>Detay albedo çarpanı (_DetailAlbedoMapScale); detay dokusu DetailAlbedo ya da prosedürel gürültüden gelir.</summary>
        public Texture2D DetailAlbedo;
        /// <summary>Detay normal haritası (_DetailNormalMap).</summary>
        public Texture2D DetailNormal;
        /// <summary>Detay karo sayısı (_DetailAlbedoMap ölçeği). 0 → detay kapalı.</summary>
        public float DetailTiling;
        public float DetailAlbedoScale = 1f;
        public float DetailNormalScale = 1f;
        /// <summary>Triplanar projeksiyon (yalnızca shader destekliyorsa; yoksa bir kez uyarı, UV'ye düşer).</summary>
        public bool Triplanar;
        /// <summary>Makro varyasyon gücü 0..1 (düşük frekanslı lekeler; detay albedo yuvasından uygulanır). 0 → kapalı.</summary>
        public float MacroVariation;
        /// <summary>Makro lekelerin dünya boyutu (metre).</summary>
        public float MacroScaleMeters = 12f;
        /// <summary>Hedef texel yoğunluğu (texel/metre). 0 → Tiling olduğu gibi.</summary>
        public float TexelDensity;
        /// <summary>Texel yoğunluğu hesabında bir karonun kapladığı dünya boyutu (metre).</summary>
        public float TileWorldMeters = 1f;

        public bool HasDetail => DetailTiling > 0.0001f && (DetailAlbedo != null || DetailNormal != null);

        /// <summary>DigitalCamo dokusu için 4 renk (a zemin, b/c lekeler, d koyu benekler).</summary>
        public Color CamoA = Color.gray, CamoB = Color.gray, CamoC = Color.gray, CamoD = Color.black;

        /// <summary>Kolaylık: Unlit gölgelendirme mi?</summary>
        public bool Unlit
        {
            get => Shading == MaterialShading.Unlit;
            set => Shading = value ? MaterialShading.Unlit : (Shading == MaterialShading.Unlit ? MaterialShading.Lit : Shading);
        }

        /// <summary>Kolaylık: Parçacık gölgelendiricisi mi? (Particle her zaman saydamdır.)</summary>
        public bool Particle
        {
            get => Shading == MaterialShading.Particle;
            set => Shading = value ? MaterialShading.Particle : (Shading == MaterialShading.Particle ? MaterialShading.Lit : Shading);
        }

        public bool Emissive => Emission.maxColorComponent > 0.0001f;

        public MaterialSpec Clone() => (MaterialSpec)MemberwiseClone();

        // ---------------------------------------------------------------- Fabrika yardımcıları

        public static MaterialSpec LitSpec(MaterialId id, Color color, float smoothness = 0.2f, float metallic = 0f,
            MaterialTextureKey texture = MaterialTextureKey.None, int seed = 0, float tiling = 1f)
        {
            return new MaterialSpec
            {
                Id = id, Name = id.ToString(), Color = color, Smoothness = smoothness, Metallic = metallic,
                Shading = MaterialShading.Lit, Texture = texture, TextureSeed = seed, Tiling = new Vector2(tiling, tiling)
            };
        }

        public static MaterialSpec UnlitSpec(MaterialId id, Color color, bool transparent = false, bool doubleSided = false)
        {
            return new MaterialSpec
            {
                Id = id, Name = id.ToString(), Color = color, Smoothness = 0f, Shading = MaterialShading.Unlit,
                Transparent = transparent || color.a < 0.999f, DoubleSided = doubleSided
            };
        }

        public static MaterialSpec ParticleSpec(MaterialId id, Color color, bool additive, MaterialTextureKey texture = MaterialTextureKey.SoftCircle)
        {
            return new MaterialSpec
            {
                Id = id, Name = id.ToString(), Color = color, Smoothness = 0f, Shading = MaterialShading.Particle,
                Transparent = true, Additive = additive, Texture = texture
            };
        }

        public static MaterialSpec CamoSpec(MaterialId id, Color a, Color b, Color c, Color d, int seed)
        {
            return new MaterialSpec
            {
                Id = id, Name = id.ToString(), Color = Color.white, Smoothness = 0.12f, Shading = MaterialShading.Lit,
                Texture = MaterialTextureKey.DigitalCamo, TextureSeed = seed, Tiling = new Vector2(2f, 2f),
                CamoA = a, CamoB = b, CamoC = c, CamoD = d, Pbr = PbrSurface.CamoFabric
            };
        }

        public MaterialSpec WithTransparent(bool doubleSided = false)
        {
            Transparent = true;
            DoubleSided |= doubleSided;
            return this;
        }

        public MaterialSpec WithDoubleSided()
        {
            DoubleSided = true;
            return this;
        }

        public MaterialSpec WithEmission(Color emission)
        {
            Emission = emission;
            return this;
        }
    }
}
