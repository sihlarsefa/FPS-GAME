using Project.Core.Domain;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>S6: Occlusion culling bake yardımcısı + doku akışı içe aktarma ayarı.</summary>
    public static class OcclusionBakeTool
    {
        [MenuItem("HAREKÂT/Optimizasyon/Occlusion Bake", priority = 110)]
        public static void Bake()
        {
            MarkStatic();
            StaticOcclusionCulling.smallestOccluder = HlodMath.OcclusionSmallestOccluder;
            StaticOcclusionCulling.smallestHole = HlodMath.OcclusionSmallestHole;
            StaticOcclusionCulling.backfaceThreshold = HlodMath.OcclusionBackfaceThreshold;
            Debug.Log("[Occlusion] Bake başlıyor (occluder " + HlodMath.OcclusionSmallestOccluder + " m, hole "
                + HlodMath.OcclusionSmallestHole + " m, backface " + HlodMath.OcclusionBackfaceThreshold + ").");
            StaticOcclusionCulling.GenerateInBackground();
        }

        [MenuItem("HAREKÂT/Optimizasyon/Occlusion Temizle", priority = 111)]
        public static void Clear() => StaticOcclusionCulling.Clear();

        /// <summary>Büyük statik yapıları OccluderStatic, tüm statikleri OccludeeStatic yapar (bot/oyuncu/dinamikler hariç).</summary>
        public static void MarkStatic()
        {
            var n = 0;
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                var go = mr.gameObject;
                if (go.GetComponentInParent<Rigidbody>() != null || go.GetComponentInParent<CharacterController>() != null) continue;
                if (go.name.StartsWith("HLOD_")) continue;
                var flags = GameObjectUtility.GetStaticEditorFlags(go) | StaticEditorFlags.OccludeeStatic;
                var s = mr.bounds.size;
                if (Mathf.Max(s.x, Mathf.Max(s.y, s.z)) >= HlodMath.OcclusionSmallestOccluder)
                    flags |= StaticEditorFlags.OccluderStatic;
                GameObjectUtility.SetStaticEditorFlags(go, flags);
                n++;
            }
            Debug.Log("[Occlusion] " + n + " renderer occluder/occludee işaretlendi.");
        }

        [MenuItem("HAREKÂT/Optimizasyon/Doku Akışını Aç (Streaming Mipmaps)", priority = 120)]
        public static void EnableTextureStreaming()
        {
            var changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null || imp.streamingMipmaps || !imp.mipmapEnabled) continue;
                if (imp.textureType == TextureImporterType.Sprite || imp.textureType == TextureImporterType.GUI) continue;
                imp.streamingMipmaps = true;
                imp.SaveAndReimport();
                changed++;
            }
            Debug.Log("[Streaming] " + changed + " dokuda Streaming Mipmaps açıldı.");
        }
    }
}
