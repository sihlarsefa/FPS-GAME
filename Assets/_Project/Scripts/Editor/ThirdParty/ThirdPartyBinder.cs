using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Project.Infrastructure.Content;
using Project.Infrastructure.Rendering;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>
    /// Fotogrametri (ambientCG CC0) doku setlerini + Poly Haven HDRI'lerini URP/Lit malzemeye,
    /// TerrainLayer'a ve gökyüzü Cubemap'ine çevirip ContentOverrides'a yazar.
    /// Maske kuralı (MaterialLibrary/ProceduralPbr ile aynı): R=metalik, G=AO, B=0.5 (yükseklik yok), A=pürüzsüzlük (=1-roughness).
    /// </summary>
    public static class ThirdPartyBinder
    {
        public const string TextureRoot = "Assets/ThirdParty/Textures";
        public const string HdriRoot = "Assets/ThirdParty/HDRI";
        public const string MaterialRoot = "Assets/ThirdParty/Materials";
        private const string LitShader = "Universal Render Pipeline/Lit";

        private sealed class MatBinding
        {
            public MaterialId Id; public string Set; public float Tiling;
            public MatBinding(MaterialId id, string set, float tiling) { Id = id; Set = set; Tiling = tiling; }
        }

        private sealed class LayerBinding
        {
            public MaterialId Id; public string Set; public float TileMeters;
            public LayerBinding(MaterialId id, string set, float tile) { Id = id; Set = set; TileMeters = tile; }
        }

        private sealed class SkyBinding
        {
            public string SkyId; public string File; public float Exposure;
            public SkyBinding(string skyId, string file, float exposure) { SkyId = skyId; File = file; Exposure = exposure; }
        }

        // Docs/VARLIK_BAGLAMA.md tablosuyla aynı tutulur.
        private static readonly MatBinding[] Materials =
        {
            new MatBinding(MaterialId.Mud, "Ground036", 3f),
            new MatBinding(MaterialId.Dirt, "Ground106", 3f),
            new MatBinding(MaterialId.Grass, "Grass001", 3f),
            new MatBinding(MaterialId.DryGrass, "Grass007", 3f),
            new MatBinding(MaterialId.PineNeedles, "ScatteredLeaves009", 3f),
            new MatBinding(MaterialId.Rock, "Rock058", 2f),
            new MatBinding(MaterialId.RockDark, "Rock035", 2f),
            new MatBinding(MaterialId.Gravel, "Gravel023", 3f),
            new MatBinding(MaterialId.Asphalt, "Asphalt031", 3f),
            new MatBinding(MaterialId.Concrete, "Concrete034", 2f),
            new MatBinding(MaterialId.ConcreteDark, "Concrete036", 2f),
            new MatBinding(MaterialId.Plaster, "Plaster004", 2f),
            new MatBinding(MaterialId.PlasterWarm, "Plaster007", 2f),
            new MatBinding(MaterialId.Stone, "Rock051", 2f),
            new MatBinding(MaterialId.StoneDark, "Rocks011", 2f),
            new MatBinding(MaterialId.RoofTile, "RoofingTiles013A", 2f),
            new MatBinding(MaterialId.RoofMetal, "CorrugatedSteel007A", 2f),
            new MatBinding(MaterialId.Wood, "Planks037A", 2f),
            new MatBinding(MaterialId.WoodDark, "Planks023A", 2f),
            new MatBinding(MaterialId.Rust, "Metal021", 2f),
            new MatBinding(MaterialId.TentCanvas, "Fabric062", 3f),
        };

        // TerrainTextureFactory yalnız Grass/DryGrass/Dirt/Rock/Gravel/Mud/Snow/Asphalt okur; kalanlar ileriki arazi için hazır.
        private static readonly LayerBinding[] Layers =
        {
            new LayerBinding(MaterialId.Grass, "Grass001", 4f),
            new LayerBinding(MaterialId.DryGrass, "Grass007", 4f),
            new LayerBinding(MaterialId.Dirt, "Ground106", 4f),
            new LayerBinding(MaterialId.Mud, "Ground036", 4f),
            // Zemin katmanları 2-4 m döşeme (2K dokuda ≈512 px/m): 6 m yakından pikselleşiyordu.
            new LayerBinding(MaterialId.Rock, "Rock058", 4f),
            new LayerBinding(MaterialId.RockDark, "Rock035", 4f),
            new LayerBinding(MaterialId.Gravel, "Gravel023", 3f),
            new LayerBinding(MaterialId.Asphalt, "Asphalt031", 4f),
            new LayerBinding(MaterialId.PineNeedles, "ScatteredLeaves009", 4f),
        };

        private static readonly SkyBinding[] Skies =
        {
            new SkyBinding(ContentIds.SkyDayClear, "autumn_hilly_field_4k.hdr", 1f),
            new SkyBinding(ContentIds.SkyCloudy, "cannon_4k.exr", 1f),
            new SkyBinding(ContentIds.SkySunset, "belfast_sunset_4k.hdr", 1f),
            new SkyBinding("sunrise", "bloem_field_sunrise_4k.hdr", 1f),
        };

        // ------------------------------------------------------------------ import kuralları

        /// <summary>Dosya adından doku türünü çıkarıp import ayarlarını uygular. Değişiklik olduysa true.</summary>
        public static bool ApplyImportRules(TextureImporter ti, string assetPath)
        {
            if (ti == null || string.IsNullOrEmpty(assetPath)) return false;
            var p = assetPath.Replace('\\', '/');
            var changed = false;

            if (p.StartsWith(HdriRoot, StringComparison.Ordinal))
            {
                changed |= Set(ti.textureShape, v => ti.textureShape = v, TextureImporterShape.TextureCube);
                changed |= Set(ti.sRGBTexture, v => ti.sRGBTexture = v, false);
                changed |= Set(ti.mipmapEnabled, v => ti.mipmapEnabled = v, true);
                changed |= Set(ti.maxTextureSize, v => ti.maxTextureSize = v, 2048);
                changed |= Set(ti.filterMode, v => ti.filterMode = v, FilterMode.Trilinear);
                changed |= Set(ti.wrapMode, v => ti.wrapMode = v, TextureWrapMode.Clamp);
                return changed;
            }

            if (!p.StartsWith(TextureRoot, StringComparison.Ordinal)) return false;
            var name = Path.GetFileNameWithoutExtension(p);
            var kind = KindOf(name);
            changed |= Set(ti.maxTextureSize, v => ti.maxTextureSize = v, 2048);
            changed |= Set(ti.mipmapEnabled, v => ti.mipmapEnabled = v, true);
            changed |= Set(ti.wrapMode, v => ti.wrapMode = v, TextureWrapMode.Repeat);
            changed |= Set(ti.anisoLevel, v => ti.anisoLevel = v, 8);
            // BC7 (yüksek kalite): varsayılan BC1/DXT1 yakın mesafede posterizasyon/bantlanma yapıyordu.
            changed |= Set(ti.textureCompression, v => ti.textureCompression = v, TextureImporterCompression.CompressedHQ);
            switch (kind)
            {
                case "normal":
                    changed |= Set(ti.textureType, v => ti.textureType = v, TextureImporterType.NormalMap);
                    changed |= Set(ti.sRGBTexture, v => ti.sRGBTexture = v, false);
                    break;
                case "color":
                    changed |= Set(ti.textureType, v => ti.textureType = v, TextureImporterType.Default);
                    changed |= Set(ti.sRGBTexture, v => ti.sRGBTexture = v, true);
                    break;
                default: // roughness / AO / height / metalness / packed mask
                    changed |= Set(ti.textureType, v => ti.textureType = v, TextureImporterType.Default);
                    changed |= Set(ti.sRGBTexture, v => ti.sRGBTexture = v, false);
                    break;
            }
            return changed;
        }

        private static string KindOf(string fileName)
        {
            if (fileName.IndexOf("_Normal", StringComparison.OrdinalIgnoreCase) >= 0) return "normal";
            if (fileName.EndsWith("_Color", StringComparison.OrdinalIgnoreCase)) return "color";
            return "linear";
        }

        private static bool Set<T>(T current, Action<T> setter, T value)
        {
            if (EqualityComparer<T>.Default.Equals(current, value)) return false;
            setter(value);
            return true;
        }

        // ------------------------------------------------------------------ BindAll

        public static string BindAll()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[ThirdPartyBinder] başlıyor");
            int ok = 0, fail = 0;
            try
            {
                EnsureFolder(MaterialRoot);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var data = ContentOverridesSetup.EnsureAsset();

                var sets = new HashSet<string>();
                foreach (var m in Materials) sets.Add(m.Set);
                foreach (var l in Layers) sets.Add(l.Set);

                var prepared = new Dictionary<string, SetAssets>();
                foreach (var s in sets)
                {
                    try
                    {
                        var a = PrepareSet(s);
                        if (a != null) { prepared[s] = a; ok++; }
                        else { sb.AppendLine("  ATLANDI (dosya eksik): " + s); fail++; }
                    }
                    catch (Exception e) { sb.AppendLine("  HATA " + s + ": " + e.Message); fail++; }
                }

                var matEntries = new List<MaterialOverrideEntry>(data.materials ?? new MaterialOverrideEntry[0]);
                foreach (var b in Materials)
                {
                    if (!prepared.TryGetValue(b.Set, out var a)) continue;
                    var mat = BuildMaterial(b.Set, a, b.Tiling);
                    UpsertMaterial(matEntries, b.Id, mat);
                    sb.AppendLine("  Materyal " + b.Id + " <- " + b.Set);
                }
                data.materials = matEntries.ToArray();

                var layerEntries = new List<TerrainLayerOverrideEntry>(data.terrainLayers ?? new TerrainLayerOverrideEntry[0]);
                foreach (var l in Layers)
                {
                    if (!prepared.TryGetValue(l.Set, out var a)) continue;
                    var layer = BuildLayer(l.Set, a, l.TileMeters);
                    UpsertLayer(layerEntries, l.Id, layer);
                    sb.AppendLine("  ArazıKatmanı " + l.Id + " <- " + l.Set);
                }
                data.terrainLayers = layerEntries.ToArray();

                var skyEntries = new List<SkyOverrideEntry>(data.skies ?? new SkyOverrideEntry[0]);
                foreach (var s in Skies)
                {
                    var path = HdriRoot + "/" + s.File;
                    if (!File.Exists(path)) { sb.AppendLine("  HDRI yok: " + path); fail++; continue; }
                    ImportCube(path);
                    var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
                    if (cube == null) { sb.AppendLine("  HDRI Cubemap yüklenemedi: " + path); fail++; continue; }
                    UpsertSky(skyEntries, s.SkyId, cube, s.Exposure);
                    sb.AppendLine("  Gökyüzü " + s.SkyId + " <- " + s.File);
                    ok++;
                }
                data.skies = skyEntries.ToArray();

                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                ContentOverrides.InvalidateCache();
            }
            catch (Exception e)
            {
                sb.AppendLine("  ISTISNA: " + e);
                fail++;
            }
            sb.AppendLine($"[ThirdPartyBinder] bitti: {ok} başarılı, {fail} sorun");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ set hazırlığı

        private sealed class SetAssets
        {
            public Texture2D Color, Normal, Mask;
        }

        private static string Find(string set, string suffix)
        {
            var dir = TextureRoot + "/" + set;
            foreach (var ext in new[] { ".jpg", ".png", ".jpeg" })
            {
                var p = dir + "/" + set + "_2K-JPG_" + suffix + ext;
                if (File.Exists(p)) return p;
            }
            return null;
        }

        private static SetAssets PrepareSet(string set)
        {
            var colorP = Find(set, "Color");
            var normalP = Find(set, "NormalGL");
            var roughP = Find(set, "Roughness");
            if (colorP == null || normalP == null || roughP == null) return null;
            var aoP = Find(set, "AmbientOcclusion");
            var metalP = Find(set, "Metalness");

            var maskP = TextureRoot + "/" + set + "/" + set + "_Mask.png";
            var newest = LatestWrite(colorP, normalP, roughP, aoP, metalP);
            if (!File.Exists(maskP) || File.GetLastWriteTimeUtc(maskP) < newest)
                WriteMask(maskP, roughP, aoP, metalP);

            ImportTexture(colorP); ImportTexture(normalP); ImportTexture(maskP);
            var a = new SetAssets
            {
                Color = AssetDatabase.LoadAssetAtPath<Texture2D>(colorP),
                Normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalP),
                Mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskP),
            };
            return a.Color != null && a.Normal != null && a.Mask != null ? a : null;
        }

        private static DateTime LatestWrite(params string[] paths)
        {
            var t = DateTime.MinValue;
            foreach (var p in paths)
                if (!string.IsNullOrEmpty(p) && File.Exists(p))
                {
                    var w = File.GetLastWriteTimeUtc(p);
                    if (w > t) t = w;
                }
            return t;
        }

        private static Texture2D LoadRaw(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!ImageConversion.LoadImage(t, File.ReadAllBytes(path), false))
            {
                UnityEngine.Object.DestroyImmediate(t);
                return null;
            }
            return t;
        }

        /// <summary>R=metalik, G=AO, B=0.5, A=1-roughness; yazılan PNG doğrusal (sRGB kapalı) içe aktarılır.</summary>
        private static void WriteMask(string maskPath, string roughP, string aoP, string metalP)
        {
            var rough = LoadRaw(roughP);
            var ao = LoadRaw(aoP);
            var metal = LoadRaw(metalP);
            try
            {
                var w = rough.width; var h = rough.height;
                var r = rough.GetPixels32();
                var a = ao != null && ao.width == w && ao.height == h ? ao.GetPixels32() : null;
                var m = metal != null && metal.width == w && metal.height == h ? metal.GetPixels32() : null;
                var o = new Color32[r.Length];
                for (var i = 0; i < r.Length; i++)
                {
                    o[i] = new Color32(
                        m != null ? m[i].r : (byte)0,
                        a != null ? a[i].r : (byte)255,
                        128,
                        (byte)(255 - r[i].r));
                }
                var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
                outTex.SetPixels32(o);
                var png = ImageConversion.EncodeToPNG(outTex);
                UnityEngine.Object.DestroyImmediate(outTex);
                File.WriteAllBytes(maskPath, png);
            }
            finally
            {
                if (rough != null) UnityEngine.Object.DestroyImmediate(rough);
                if (ao != null) UnityEngine.Object.DestroyImmediate(ao);
                if (metal != null) UnityEngine.Object.DestroyImmediate(metal);
            }
        }

        private static void ImportTexture(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && ApplyImportRules(ti, path))
                ti.SaveAndReimport();
        }

        private static void ImportCube(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && ApplyImportRules(ti, path))
                ti.SaveAndReimport();
        }

        // ------------------------------------------------------------------ varlık üretimi

        private static Material BuildMaterial(string set, SetAssets a, float tiling)
        {
            var path = MaterialRoot + "/HK_" + set + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find(LitShader);
                if (shader == null) throw new InvalidOperationException(LitShader + " bulunamadı");
                mat = new Material(shader) { name = "HK_" + set };
                AssetDatabase.CreateAsset(mat, path);
            }
            var t = new Vector2(tiling, tiling);
            SetTex(mat, "_BaseMap", a.Color, t);
            SetTex(mat, "_BumpMap", a.Normal, t);
            SetTex(mat, "_MetallicGlossMap", a.Mask, t);
            SetTex(mat, "_OcclusionMap", a.Mask, t);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 1f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 1f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 1f);
            if (mat.HasProperty("_OcclusionStrength")) mat.SetFloat("_OcclusionStrength", 1f);
            if (mat.HasProperty("_SmoothnessTextureChannel")) mat.SetFloat("_SmoothnessTextureChannel", 0f);
            mat.EnableKeyword("_NORMALMAP");
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void SetTex(Material m, string prop, Texture tex, Vector2 scale)
        {
            if (!m.HasProperty(prop)) return;
            m.SetTexture(prop, tex);
            m.SetTextureScale(prop, scale);
        }

        private static TerrainLayer BuildLayer(string set, SetAssets a, float tileMeters)
        {
            var path = MaterialRoot + "/HK_TL_" + set + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer { name = "HK_TL_" + set };
                AssetDatabase.CreateAsset(layer, path);
            }
            layer.diffuseTexture = a.Color;
            layer.normalMapTexture = a.Normal;
            layer.maskMapTexture = a.Mask;
            layer.normalScale = 1f;
            layer.tileSize = new Vector2(tileMeters, tileMeters);
            layer.metallic = 0f;
            layer.smoothness = 0.5f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static void UpsertMaterial(List<MaterialOverrideEntry> list, MaterialId id, Material mat)
        {
            foreach (var e in list)
                if (e != null && e.materialId == id) { e.material = mat; return; }
            list.Add(new MaterialOverrideEntry { materialId = id, material = mat });
        }

        private static void UpsertLayer(List<TerrainLayerOverrideEntry> list, MaterialId id, TerrainLayer layer)
        {
            foreach (var e in list)
                if (e != null && e.materialId == id) { e.layer = layer; return; }
            list.Add(new TerrainLayerOverrideEntry { materialId = id, layer = layer });
        }

        private static void UpsertSky(List<SkyOverrideEntry> list, string id, Cubemap cube, float exposure)
        {
            foreach (var e in list)
                if (e != null && string.Equals(e.skyId, id, StringComparison.OrdinalIgnoreCase)) { e.hdri = cube; e.exposure = exposure; return; }
            list.Add(new SkyOverrideEntry { skyId = id, hdri = cube, exposure = exposure });
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }

    /// <summary>ThirdParty doku/HDRI içe aktarma kuralları (yeni dosyalar için otomatik).</summary>
    public sealed class ThirdPartyTextureImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (assetImporter is TextureImporter ti)
                ThirdPartyBinder.ApplyImportRules(ti, assetPath);
        }
    }
}
