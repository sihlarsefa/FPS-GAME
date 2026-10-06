using System;
using System.Collections.Generic;
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
            var tiers = new (string, int)[PipelineTiers.Count];
            for (var k = 0; k < tiers.Length; k++)
                tiers[k] = (PipelineTiers.Get(k).Name, k);

            UniversalRenderPipelineAsset defaultAsset = null;
            var qualityNames = new string[tiers.Length];
            var pipelines = new RenderPipelineAsset[tiers.Length];

            AssetDatabase.StartAssetEditing();
            try
            {
                for (var i = 0; i < tiers.Length; i++)
                {
                    var name = tiers[i].Item1;
                    var tier = PipelineTiers.Get(i);
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
                    // Görüntü kalitesi: SSAO (Orta+) ve Decal (Orta+; kurşun izi/kan) renderer özellikleri.
                    TryEnsureRendererFeature(renderer, "UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion", "HK_SSAO", tier.Ssao);
                    TryEnsureRendererFeature(renderer, "UnityEngine.Rendering.Universal.DecalRendererFeature", "HK_Decal", tier.Decals);
                    TrySetRendererInt(renderer, "m_RenderingMode", PipelineTiers.RenderingModeForwardPlus);

                    var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
                    if (pipeline == null)
                    {
                        pipeline = UniversalRenderPipelineAsset.Create(renderer);
                        AssetDatabase.CreateAsset(pipeline, assetPath);
                    }

                    pipeline.renderScale = tier.RenderScale;
                    pipeline.shadowDistance = tier.ShadowDistance;
                    pipeline.msaaSampleCount = PipelineTiers.EffectiveMsaa(tier);
                    pipeline.supportsHDR = tier.Hdr;
                    // URP 17+: bazı alanlar salt okunur; SerializedObject ile yazılır, yoksa uyarı.
                    var pipelineSo = new SerializedObject(pipeline);
                    SetBool(pipelineSo, "m_SoftShadowsSupported", tier.SoftShadows);
                    SetInt(pipelineSo, "m_ShadowCascadeCount", tier.ShadowCascades);
                    SetInt(pipelineSo, "m_MainLightShadowmapResolution", tier.ShadowResolution);
                    SetInt(pipelineSo, "m_SoftShadowQuality", tier.SoftShadowQuality);
                    SetVec3(pipelineSo, "m_Cascade4Split", tier.CascadeSplits);
                    SetBool(pipelineSo, "m_UseSRPBatcher", true);
                    // SSR-lite pürüzsüzlük alfası (_WRITE_SMOOTHNESS) varyantı build'de atılmasın.
                    SetBool(pipelineSo, "m_PrefilterWriteSmoothness", false);
                    // URP 17.3+ reads the named upscaler, not the legacy enum.
                    var upscaler = pipelineSo.FindProperty("m_SelectedUpscalerName");
                    if (upscaler != null)
                        upscaler.stringValue = tier.UseStp ? "Spatial-Temporal Post-Processing" : "Bilinear";
                    else
                        SetInt(pipelineSo, "m_UpscalingFilter", tier.UseStp ? PipelineTiers.UpscalingStp : 0);
                    SetInt(pipelineSo, "m_GPUResidentDrawerMode", tier.GpuResidentDrawer ? PipelineTiers.GpuResidentDrawerInstanced : 0);
                    SetBool(pipelineSo, "m_GPUResidentDrawerEnableOcclusionCullingInCameras", tier.GpuOcclusion);
                    SetInt(pipelineSo, "m_LightProbeSystem", PipelineTiers.LightProbeSystemProbeVolumes);
                    pipelineSo.ApplyModifiedPropertiesWithoutUndo();
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
            // GPU Resident Drawer, DOTS-instancing varyantları olmadan player'da URP/Lit nesnelerini ÇİZMEZ (editörde
            // kendini kapattığı için hata yalnız build'de görünür: asker bacak/kol/yüzü kayboluyordu).
            var anyGrd = false;
            for (var g = 0; g < PipelineTiers.Count; g++)
                anyGrd |= PipelineTiers.Get(g).GpuResidentDrawer;
            const int brgKeepAll = (int)UnityEditor.Rendering.BatchRendererGroupStrippingMode.KeepAll;
            var graphicsSo = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            var brgStripping = graphicsSo.FindProperty("m_BrgStripping");
            if (anyGrd && brgStripping != null && brgStripping.intValue != brgKeepAll)
            {
                brgStripping.intValue = brgKeepAll;
                graphicsSo.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[AssetGeneration] BatchRendererGroup Variants = Keep All (GPU Resident Drawer için zorunlu).");
            }

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

        private static void SetInt(SerializedObject so, string name, int value)
        {
            var prop = so.FindProperty(name);
            if (prop != null)
                prop.intValue = value;
            else
                Debug.LogWarning("[HAREKÂT] URP alanı yok (sürüm farkı?): " + name);
        }

        private static void SetBool(SerializedObject so, string name, bool value)
        {
            var prop = so.FindProperty(name);
            if (prop != null)
                prop.boolValue = value;
            else
                Debug.LogWarning("[HAREKÂT] URP alanı yok (sürüm farkı?): " + name);
        }

        private static void SetVec3(SerializedObject so, string name, Vector3 value)
        {
            var prop = so.FindProperty(name);
            if (prop != null)
                prop.vector3Value = value;
            else
                Debug.LogWarning("[HAREKÂT] URP alanı yok (sürüm farkı?): " + name);
        }

        private static void TrySetRendererInt(UniversalRendererData renderer, string name, int value)
        {
            var so = new SerializedObject(renderer);
            SetInt(so, name, value);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
        }

        /// <summary>Renderer özelliğini (tür adıyla, yansıma) ekler/etkinleştirir; tür yoksa sessizce geçer.</summary>
        private static void TryEnsureRendererFeature(UniversalRendererData renderer, string typeName, string featureName, bool enabled)
        {
            try
            {
                var type = typeof(UniversalRendererData).Assembly.GetType(typeName);
                if (type == null)
                    return;
                var so = new SerializedObject(renderer);
                var list = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                if (list == null || map == null)
                    return;
                for (var i = 0; i < list.arraySize; i++)
                {
                    var existing = list.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererFeature;
                    if (existing != null && existing.GetType() == type)
                    {
                        existing.SetActive(enabled);
                        EditorUtility.SetDirty(existing);
                        return;
                    }
                }

                var feature = ScriptableObject.CreateInstance(type) as ScriptableRendererFeature;
                if (feature == null)
                    return;
                feature.name = featureName;
                AssetDatabase.AddObjectToAsset(feature, renderer);
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId))
                    return;
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
                feature.SetActive(enabled);
                EditorUtility.SetDirty(renderer);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HAREKÂT] Renderer özelliği eklenemedi (" + featureName + "): " + e.Message);
            }
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
            var pbrJobs = new List<KeyValuePair<Material, MaterialSpec>>();
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
                    if (spec != null && mat != null && spec.Pbr != PbrSurface.None)
                        pbrJobs.Add(new KeyValuePair<Material, MaterialSpec>(mat, spec));
                }

                SaveTexturePng(ProceduralTextures.SoftCircle, TexFolder + "/SoftParticle.png");
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            for (var j = 0; j < pbrJobs.Count; j++)
                PersistPbr(pbrJobs[j].Key, pbrJobs[j].Value);
            if (pbrJobs.Count > 0)
                AssetDatabase.SaveAssets();

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

        /// <summary>ProceduralPbr normal/maske dokularını PNG olarak kaydeder ve .mat dosyasına atar (build'de runtime üretim gerekmez).</summary>
        private static void PersistPbr(Material mat, MaterialSpec spec)
        {
            try
            {
                var set = ProceduralPbr.Get(spec.Pbr, spec.TextureSeed);
                if (set == null || !set.IsValid)
                    return;
                var key = spec.Pbr + "_" + spec.TextureSeed;
                var nPath = TexFolder + "/HK_Pbr_" + key + "_N.png";
                var mPath = TexFolder + "/HK_Pbr_" + key + "_M.png";
                var n = SavePbrPng(set.Normal, nPath, true);
                var m = SavePbrPng(set.Mask, mPath, false);
                if (n == null || m == null)
                    return;
                var changed = false;
                if (mat.HasProperty("_BumpMap") && mat.GetTexture("_BumpMap") != n)
                {
                    mat.SetTexture("_BumpMap", n);
                    mat.SetTextureScale("_BumpMap", spec.Tiling);
                    mat.EnableKeyword("_NORMALMAP");
                    changed = true;
                }
                if (mat.HasProperty("_MetallicGlossMap") && mat.GetTexture("_MetallicGlossMap") != m)
                {
                    mat.SetTexture("_MetallicGlossMap", m);
                    mat.SetTextureScale("_MetallicGlossMap", spec.Tiling);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                    changed = true;
                }
                if (mat.HasProperty("_OcclusionMap") && mat.GetTexture("_OcclusionMap") != m)
                {
                    mat.SetTexture("_OcclusionMap", m);
                    mat.EnableKeyword("_OCCLUSIONMAP");
                    changed = true;
                }
                if (mat.HasProperty("_MaskMap") && mat.GetTexture("_MaskMap") != m)
                {
                    mat.SetTexture("_MaskMap", m);
                    changed = true;
                }
                if (changed)
                    EditorUtility.SetDirty(mat);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HAREKÂT] PBR doku kaydedilemedi " + mat.name + ": " + e.Message);
            }
        }

        private static Texture2D SavePbrPng(Texture2D src, string path, bool normalMap)
        {
            if (src == null)
                return null;
            if (!File.Exists(path))
            {
                File.WriteAllBytes(path, src.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                if (AssetImporter.GetAtPath(path) is TextureImporter imp)
                {
                    imp.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    imp.sRGBTexture = false;
                    imp.mipmapEnabled = true;
                    imp.wrapMode = TextureWrapMode.Repeat;
                    imp.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
