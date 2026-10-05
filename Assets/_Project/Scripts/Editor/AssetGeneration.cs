using System;
using System.IO;
using Project.Infrastructure.Config;
using Project.Infrastructure.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.EditorTools
{
    /// <summary>URP asset / quality kurulumu.</summary>
    public static class UrpSetup
    {
        private const string Folder = "Assets/_Project/Settings/Rendering";

        public static void EnsureAll()
        {
            EnsureFolder(Folder);
            var tiers = new[]
            {
                ("Low", 0.75f, 40f, 1, false, false),
                ("Medium", 0.9f, 60f, 2, true, false),
                ("High", 1f, 90f, 4, true, true),
                ("Ultra", 1f, 120f, 4, true, true)
            };

            UniversalRenderPipelineAsset defaultAsset = null;
            var qualityNames = new string[tiers.Length];
            var pipelines = new RenderPipelineAsset[tiers.Length];

            AssetDatabase.StartAssetEditing();
            try
            {
                for (var i = 0; i < tiers.Length; i++)
                {
                    var (name, scale, shadow, msaa, hdr, soft) = tiers[i];
                    qualityNames[i] = name;
                    var rendererPath = Folder + "/URP_Renderer_" + name + ".asset";
                    var assetPath = Folder + "/URP_" + name + ".asset";

                    var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
                    if (renderer == null)
                    {
                        renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                        AssetDatabase.CreateAsset(renderer, rendererPath);
                    }

                    TryAssignPostProcess(renderer);

                    var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
                    if (pipeline == null)
                    {
                        pipeline = UniversalRenderPipelineAsset.Create(renderer);
                        AssetDatabase.CreateAsset(pipeline, assetPath);
                    }

                    pipeline.renderScale = scale;
                    pipeline.shadowDistance = shadow;
                    pipeline.msaaSampleCount = msaa;
                    pipeline.supportsHDR = hdr;
                    pipeline.supportsSoftShadows = soft;
                    EditorUtility.SetDirty(pipeline);
                    pipelines[i] = pipeline;
                    if (name == "High" || defaultAsset == null)
                        defaultAsset = pipeline;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            GraphicsSettings.defaultRenderPipeline = defaultAsset;
            try
            {
                // Quality level count / names via SerializedObject is more reliable
                var qso = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
                var qs = qso.FindProperty("m_QualitySettings");
                if (qs != null && qs.isArray)
                {
                    while (qs.arraySize < tiers.Length)
                        qs.InsertArrayElementAtIndex(qs.arraySize);
                    while (qs.arraySize > tiers.Length)
                        qs.DeleteArrayElementAtIndex(qs.arraySize - 1);

                    for (var i = 0; i < tiers.Length; i++)
                    {
                        var el = qs.GetArrayElementAtIndex(i);
                        var n = el.FindPropertyRelative("name");
                        if (n != null) n.stringValue = qualityNames[i];
                        var rp = el.FindPropertyRelative("customRenderPipeline");
                        if (rp != null) rp.objectReferenceValue = pipelines[i];
                    }

                    qso.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HAREKÂT] QualitySettings yazılamadı: " + e.Message);
                for (var i = 0; i < pipelines.Length && i < QualitySettings.names.Length; i++)
                    QualitySettings.SetQualityLevel(i, false);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[HAREKÂT] URP Low/Medium/High/Ultra hazır.");
        }

        private static void TryAssignPostProcess(UniversalRendererData renderer)
        {
            try
            {
                var pp = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                if (pp == null)
                    return;
                var so = new SerializedObject(renderer);
                var prop = so.FindProperty("m_PostProcessData") ?? so.FindProperty("postProcessData");
                if (prop != null)
                {
                    prop.objectReferenceValue = pp;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HAREKÂT] PostProcessData atanamadı: " + e.Message);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parts = path.Split('/');
            var cur = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }

    /// <summary>Malzeme / doku / config varlık üretimi.</summary>
    public static class AssetGeneration
    {
        private const string MatFolder = "Assets/_Project/Art/Materials";
        private const string TexFolder = "Assets/_Project/Art/Textures";
        private const string ResFolder = "Assets/_Project/Resources";

        public static void EnsureArtLibrary()
        {
            EnsureFolder(MatFolder);
            EnsureFolder(TexFolder);
            EnsureFolder(ResFolder);

            var materials = new Material[MaterialLibrary.Count];
            AssetDatabase.StartAssetEditing();
            try
            {
                for (var i = 0; i < MaterialLibrary.Count; i++)
                {
                    var id = (MaterialId)i;
                    var spec = MaterialLibrary.GetSpec(id);
                    var matPath = MatFolder + "/HK_" + id + ".mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (mat == null)
                    {
                        mat = MaterialLibrary.CreateFromSpec(spec);
                        AssetDatabase.CreateAsset(mat, matPath);
                    }

                    // Dokuları PNG olarak kaydet (bayrak vb.)
                    if (id == MaterialId.TurkishFlag)
                        SaveTexturePng(ProceduralTextures.TurkishFlag, TexFolder + "/TurkishFlag.png");

                    materials[i] = mat;
                }

                SaveTexturePng(ProceduralTextures.SoftCircle, TexFolder + "/SoftParticle.png");
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            var libPath = ResFolder + "/GameArtLibrary.asset";
            var lib = AssetDatabase.LoadAssetAtPath<GameArtLibrary>(libPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<GameArtLibrary>();
                AssetDatabase.CreateAsset(lib, libPath);
            }

            lib.materials = materials;
            lib.softParticle = AssetDatabase.LoadAssetAtPath<Texture2D>(TexFolder + "/SoftParticle.png");
            EditorUtility.SetDirty(lib);
            MaterialLibrary.Use(lib);

            EnsureMovementConfig();
            AssetDatabase.SaveAssets();
            Debug.Log("[HAREKÂT] GameArtLibrary ve malzemeler hazır (" + materials.Length + ").");
        }

        public static void EnsureMovementConfig()
        {
            EnsureFolder(ResFolder);
            var path = ResFolder + "/PlayerMovementConfig.asset";
            if (AssetDatabase.LoadAssetAtPath<PlayerMovementConfig>(path) != null)
                return;
            var cfg = ScriptableObject.CreateInstance<PlayerMovementConfig>();
            AssetDatabase.CreateAsset(cfg, path);
            Debug.Log("[HAREKÂT] PlayerMovementConfig Resources altına yazıldı.");
        }

        public static void EnsureAppIcon()
        {
            EnsureFolder(TexFolder);
            var path = TexFolder + "/AppIcon.png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                var pixels = new Color32[256 * 256];
                var red = ProceduralTextures.FlagRed;
                for (var y = 0; y < 256; y++)
                for (var x = 0; x < 256; x++)
                    pixels[y * 256 + x] = red;
                // Ay+yıldız basitleştirilmiş beyaz daire
                for (var y = 80; y < 176; y++)
                for (var x = 90; x < 186; x++)
                {
                    var dx = x - 128f;
                    var dy = y - 128f;
                    if (dx * dx + dy * dy < 28 * 28)
                        pixels[y * 256 + x] = new Color32(255, 255, 255, 255);
                }

                tex.SetPixels32(pixels);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (icon != null)
            {
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, new[] { icon });
            }
        }

        private static void SaveTexturePng(Texture2D src, string path)
        {
            if (src == null || File.Exists(path))
                return;
            try
            {
                var readable = src;
                if (!src.isReadable)
                {
                    // ProceduralTextures dokuları okunabilir bırakılır; yine de güvenli kopya
                    readable = UnityEngine.Object.Instantiate(src);
                }

                File.WriteAllBytes(path, readable.EncodeToPNG());
                if (readable != src)
                    UnityEngine.Object.DestroyImmediate(readable);
                AssetDatabase.ImportAsset(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HAREKÂT] Doku kaydedilemedi " + path + ": " + e.Message);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parts = path.Split('/');
            var cur = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
