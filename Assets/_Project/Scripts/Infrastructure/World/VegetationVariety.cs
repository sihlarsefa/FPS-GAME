using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>Ek bitki örtüsü türleri (TreeKind'e dokunmadan; kendi prototip dizisi).</summary>
    public enum VarietyKind
    {
        /// <summary>Çam varyantı: uzun, dar, seyrek katlı.</summary>
        PineSlim = 0,
        /// <summary>Kavak: ince, sütun gibi taç.</summary>
        Poplar = 1,
        /// <summary>Bodur meşe: alçak, geniş, kıvrık.</summary>
        DwarfOak = 2,
        /// <summary>Çalı A: yuvarlak yoğun.</summary>
        ShrubRound = 3,
        /// <summary>Çalı B: dağınık, dikenli (kuru).</summary>
        ShrubSparse = 4
    }

    /// <summary>Saf tür tanımı (Unity nesnesi gerektirmez; test edilebilir).</summary>
    public struct VarietySpec
    {
        public float Height, TrunkRadius, CrownRadius, CrownBase01;
        public float Sway, Flutter, BarkNormal;
        public bool Conifer;
    }

    /// <summary>
    /// Bitki çeşitliliği: 3 ağaç türü + 2 çalı. Rüzgâr: gövde sallanması / dal titremesi (HAREKAT/VegetationWind _TrunkSway/_BranchFlutter)
    /// ve köşe rengi R kanalında yüksekliğe bağlı bükme ağırlığı (TreeFactory.WindBend deseni). Kabuk: prosedürel normal haritası.
    /// Kaya yosunu: <see cref="RockFactory.GetSplitVariant"/> + <see cref="VegetationMaterials.CreateMoss"/> (varsayılan); burada yosun oranı kuralı.
    /// </summary>
    public static class VegetationVariety
    {
        public const int KindCount = 5;

        public static VarietySpec Spec(VarietyKind kind)
        {
            switch (kind)
            {
                case VarietyKind.PineSlim:
                    return new VarietySpec { Height = 13f, TrunkRadius = 0.24f, CrownRadius = 1.9f, CrownBase01 = 0.18f, Sway = 0.3f, Flutter = 0.35f, BarkNormal = 1.0f, Conifer = true };
                case VarietyKind.Poplar:
                    return new VarietySpec { Height = 15f, TrunkRadius = 0.22f, CrownRadius = 1.5f, CrownBase01 = 0.25f, Sway = 0.6f, Flutter = 0.8f, BarkNormal = 0.8f };
                case VarietyKind.DwarfOak:
                    return new VarietySpec { Height = 4.2f, TrunkRadius = 0.26f, CrownRadius = 2.4f, CrownBase01 = 0.35f, Sway = 0.35f, Flutter = 0.5f, BarkNormal = 1.3f };
                case VarietyKind.ShrubRound:
                    return new VarietySpec { Height = 1.2f, TrunkRadius = 0f, CrownRadius = 0.9f, CrownBase01 = 0.1f, Sway = 0.5f, Flutter = 0.7f, BarkNormal = 0.5f };
                default:
                    return new VarietySpec { Height = 1.0f, TrunkRadius = 0f, CrownRadius = 0.8f, CrownBase01 = 0.05f, Sway = 0.45f, Flutter = 0.9f, BarkNormal = 0.5f };
            }
        }

        /// <summary>Tür kimliği (ContentOverrides / CSV).</summary>
        public static string SpeciesId(VarietyKind kind)
        {
            switch (kind)
            {
                case VarietyKind.PineSlim: return Project.Infrastructure.Content.ContentIds.VegPineSlim;
                case VarietyKind.Poplar: return Project.Infrastructure.Content.ContentIds.VegPoplar;
                case VarietyKind.DwarfOak: return Project.Infrastructure.Content.ContentIds.VegDwarfOak;
                case VarietyKind.ShrubRound: return Project.Infrastructure.Content.ContentIds.VegShrubRound;
                default: return Project.Infrastructure.Content.ContentIds.VegShrubSparse;
            }
        }

        /// <summary>Kademeye göre rüzgâr: kademe 0'da hareket kapalı; yüksek kademelerde tam genlik.</summary>
        public static VegetationTuning.Wind WindFor(VarietyKind kind, int tier)
        {
            var s = Spec(kind);
            if (tier <= 0)
                return new VegetationTuning.Wind { Bend = 0f, Speed = 0f, Size = 0.3f, Amount = 0f };
            var scale = tier == 1 ? 0.6f : 1f;
            return new VegetationTuning.Wind { Bend = s.Sway * scale, Speed = 0.3f + s.Flutter * 0.35f, Size = 0.4f + s.Flutter * 0.2f, Amount = s.Sway * scale };
        }

        /// <summary>Köşe bükme ağırlığı (0..1): yükseklik oranıyla kuadratik artar; taban sabit.</summary>
        public static float BendWeight(float height01) => Mathf.Clamp01(height01) * Mathf.Clamp01(height01);

        /// <summary>Kabuk normal haritası şiddeti: kademe 0'da kapalı, 1'de yarım, üstünde tam.</summary>
        public static float BarkBumpScale(VarietyKind kind, int tier)
        {
            if (tier <= 0)
                return 0f;
            return Spec(kind).BarkNormal * (tier == 1 ? 0.5f : 1f);
        }

        /// <summary>Kayada yosun kaplama oranı (0..1): nem ve gölge arttıkça artar, eğim arttıkça azalır.</summary>
        public static float MossCoverage(float moisture01, float shade01, float slopeDeg)
        {
            var wet = Mathf.Clamp01(moisture01) * 0.65f + Mathf.Clamp01(shade01) * 0.35f;
            var slope = 1f - Mathf.Clamp01((slopeDeg - 20f) / 40f);
            return Mathf.Clamp01(wet * slope);
        }

        /// <summary>Kademeye göre taç ayrıntısı: ikosfer alt bölüm sayısı.</summary>
        public static int CrownSubdivisions(int tier) => tier >= 2 ? 1 : 0;

        // ------------------------------------------------------------------ Mesh / malzeme

        public static Mesh BuildMesh(VarietyKind kind, int seed, int tier = 2)
        {
            var s = Spec(kind);
            var rng = new System.Random(seed * 47 + 23 + (int)kind * 101);
            var b = new MeshBuilder(2);
            b.EnableColors();
            var sub = CrownSubdivisions(tier);
            var h = s.Height;

            if (s.TrunkRadius > 0f)
            {
                var trunkH = h * (s.Conifer ? 0.55f : s.CrownBase01 + 0.3f);
                b.CurrentColor = BendColor(0.1f);
                MeshFactory.AddFrustum(b, MeshFactory.TrunkSubmesh, Vector3.zero, s.TrunkRadius, s.TrunkRadius * 0.5f, trunkH, 7, false, false);
            }

            switch (kind)
            {
                case VarietyKind.PineSlim:
                    for (var k = 0; k < 6; k++)
                    {
                        var t = k / 5f;
                        var baseY = h * Mathf.Lerp(s.CrownBase01, 0.8f, t);
                        var coneH = h * 0.22f;
                        var r = s.CrownRadius * Mathf.Lerp(1f, 0.3f, t) * TreeFactory.Range(rng, 0.9f, 1.1f);
                        AddCone(b, baseY, coneH, r, rng, BendColor(baseY / h));
                    }

                    break;
                case VarietyKind.Poplar:
                    for (var k = 0; k < 4; k++)
                    {
                        var y = h * Mathf.Lerp(s.CrownBase01 + 0.15f, 0.82f, k / 3f);
                        b.CurrentColor = BendColor(y / h);
                        var rad = new Vector3(s.CrownRadius, h * 0.16f, s.CrownRadius) * Mathf.Lerp(1f, 0.6f, k / 3f);
                        TreeFactory.AddBlob(b, MeshFactory.FoliageSubmesh, new Vector3(0f, y, 0f), rad, sub, 0.15f, rng);
                    }

                    break;
                case VarietyKind.DwarfOak:
                    for (var i = 0; i < 2; i++)
                    {
                        var rot = Quaternion.Euler(0f, i * 150f + TreeFactory.Range(rng, -20f, 20f), -TreeFactory.Range(rng, 40f, 55f));
                        b.CurrentColor = BendColor(0.45f);
                        MeshFactory.AddFrustum(b, MeshFactory.TrunkSubmesh, new Vector3(0f, h * 0.3f, 0f), rot, s.TrunkRadius * 0.5f, s.TrunkRadius * 0.2f, h * 0.4f, 5, false, false);
                    }

                    for (var i = 0; i < 3; i++)
                    {
                        var a = i / 3f * Mathf.PI * 2f + TreeFactory.Range(rng, -0.4f, 0.4f);
                        var off = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * s.CrownRadius * 0.45f;
                        b.CurrentColor = BendColor(0.85f);
                        TreeFactory.AddBlob(b, MeshFactory.FoliageSubmesh, new Vector3(off.x, h * 0.72f, off.z), new Vector3(1.1f, 0.6f, 1.1f) * (s.CrownRadius * 0.6f), sub, 0.18f, rng);
                    }

                    break;
                default:
                    var blobs = kind == VarietyKind.ShrubRound ? 3 : 2;
                    for (var i = 0; i < blobs; i++)
                    {
                        var a = TreeFactory.Range(rng, 0f, Mathf.PI * 2f);
                        var off = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (i == 0 ? 0f : s.CrownRadius * 0.5f);
                        var sq = kind == VarietyKind.ShrubRound ? 0.7f : 0.5f;
                        b.CurrentColor = BendColor(0.8f);
                        TreeFactory.AddBlob(b, MeshFactory.FoliageSubmesh, new Vector3(off.x, h * 0.4f, off.z), new Vector3(1f, sq, 1f) * (s.CrownRadius * (i == 0 ? 0.8f : 0.55f)), sub, 0.2f, rng);
                    }

                    break;
            }

            return b.ToMesh("HK_Variety_" + kind);
        }

        private static Color32 BendColor(float height01)
        {
            var w = (byte)Mathf.RoundToInt(BendWeight(height01) * 255f);
            return new Color32(w, 255, 255, 255);
        }

        private static void AddCone(MeshBuilder b, float baseY, float coneH, float radius, System.Random rng, Color32 color)
        {
            b.CurrentColor = color;
            const int seg = 6;
            var rot = TreeFactory.Range(rng, 0f, Mathf.PI * 2f);
            var apex = new Vector3(0f, baseY + coneH, 0f);
            var under = new Vector3(0f, baseY + coneH * 0.2f, 0f);
            for (var i = 0; i < seg; i++)
            {
                var a0 = rot + i / (float)seg * Mathf.PI * 2f;
                var a1 = rot + (i + 1) / (float)seg * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius + new Vector3(0f, baseY, 0f);
                var p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius + new Vector3(0f, baseY, 0f);
                b.AddFlatTriangle(MeshFactory.FoliageSubmesh, p0, apex, p1, 0.5f);
                b.AddFlatTriangle(MeshFactory.FoliageSubmesh, p0, p1, under, 0.5f);
            }
        }

        /// <summary>Kabuk normal haritası (boyuna oluklu, gürültülü). Yükseklik → Sobel ile normal.</summary>
        public static Color32[] BarkNormalPixels(int size, int seed, float strength)
        {
            var h = new float[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var u = x / (float)size;
                    var v = y / (float)size;
                    var groove = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 2f * 6f + Mathf.Sin(v * 9f + seed) * 0.8f));
                    h[y * size + x] = groove * 0.7f + Mathf.PerlinNoise(u * 8f + seed * 0.37f, v * 3f) * 0.3f;
                }

            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var l = h[y * size + (x + size - 1) % size];
                    var r = h[y * size + (x + 1) % size];
                    var d = h[((y + size - 1) % size) * size + x];
                    var up = h[((y + 1) % size) * size + x];
                    var n = new Vector3((l - r) * strength, (d - up) * strength, 1f).normalized;
                    px[y * size + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255f), (byte)((n.y * 0.5f + 0.5f) * 255f), (byte)((n.z * 0.5f + 0.5f) * 255f), 255);
                }

            return px;
        }

        /// <summary>Normal haritalı kabuk malzemesi (yeni örnek; MaterialLibrary'ye dokunmaz). Kademe 0'da normal haritası yok.</summary>
        public static Material CreateBark(VarietyKind kind, int seed, int tier)
        {
            var m = new Material(MaterialLibrary.Get(MaterialId.Bark)) { name = "HK_Veg_Bark_" + kind };
            var bump = BarkBumpScale(kind, tier);
            if (bump <= 0f)
                return m;
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, true, true) { name = "HK_Veg_BarkNormal", wrapMode = TextureWrapMode.Repeat };
            tex.SetPixels32(BarkNormalPixels(64, seed, 2.5f));
            tex.Apply(true, true);
            if (m.HasProperty("_BumpMap"))
                m.SetTexture("_BumpMap", tex);
            if (m.HasProperty("_BumpScale"))
                m.SetFloat("_BumpScale", bump);
            m.EnableKeyword("_NORMALMAP");
            return m;
        }

        /// <summary>Tek prototip (LOD yok: kademe düşükse sade taç). ContentOverrides kancası korunur.</summary>
        public static GameObject CreatePrototype(VarietyKind kind, Transform parent, int seed, int tier = 2)
        {
            tier = Mathf.Clamp(tier, 0, PipelineTiers.Count - 1);
            try
            {
                if (Project.Infrastructure.Content.ContentOverrides.TryGetVegetation(SpeciesId(kind), out var prefab) && prefab != null)
                {
                    var over = VegetationTuning.PrepareOverride(prefab, parent, kind.ToString());
                    if (over != null)
                        return over;
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("[Bitki] Çeşitlilik override okunamadı (" + kind + "): " + e.Message);
            }

            var s = Spec(kind);
            var leaf = kind == VarietyKind.PineSlim
                ? VegetationMaterials.CreateNeedles(seed, seed & 1)
                : VegetationMaterials.CreateLeaves(seed, kind == VarietyKind.ShrubRound || kind == VarietyKind.ShrubSparse, seed & 1);
            leaf.name += "_" + kind + "_" + seed;
            var go = new GameObject("HK_Variety_" + kind) { layer = GameLayers.Default };
            if (parent != null)
                go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = BuildMesh(kind, seed, tier);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[] { CreateBark(kind, seed, tier), leaf };
            r.receiveShadows = true;
            if (s.TrunkRadius > 0f)
            {
                var c = go.AddComponent<CapsuleCollider>();
                c.direction = 1;
                c.radius = s.TrunkRadius;
                c.height = s.Height * 0.6f;
                c.center = new Vector3(0f, c.height * 0.5f, 0f);
            }

            return go;
        }
    }
}
