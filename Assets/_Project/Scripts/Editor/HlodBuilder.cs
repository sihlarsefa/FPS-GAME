using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.EditorTools
{
    /// <summary>
    /// S6: Seçili yerleşim kökü için HLOD proxy üretir: malzeme başına tek birleşik mesh, küçük parçalar atılır.
    /// Çıktı: kök altında "HLOD_Proxy" + HlodProxy bileşeni (çalışma anında mesafe/histerezis ile takas).
    /// </summary>
    public static class HlodBuilder
    {
        private const string OutDir = "Assets/_Project/Generated/Hlod";
        public const float MinPartExtent = 1.5f;     // m: bundan küçük parça proxy'ye girmez
        public const int TriBudget = 60000;          // yerleşim başına proxy üçgen üst sınırı

        [MenuItem("HAREKÂT/Optimizasyon/HLOD Oluştur (Seçili Yerleşim)", priority = 100)]
        public static void BuildSelectedMenu()
        {
            var sel = Selection.gameObjects;
            if (sel == null || sel.Length == 0)
            {
                EditorUtility.DisplayDialog("HLOD", "Hiyerarşide bir yerleşim kökü seçin.", "Tamam");
                return;
            }
            var made = 0;
            foreach (var go in sel)
                if (Build(go, MinPartExtent, TriBudget)) made++;
            Debug.Log("[HLOD] " + made + "/" + sel.Length + " yerleşim için proxy üretildi.");
        }

        [MenuItem("HAREKÂT/Optimizasyon/HLOD Kaldır (Seçili)", priority = 101)]
        public static void RemoveSelectedMenu()
        {
            foreach (var go in Selection.gameObjects)
                Remove(go);
        }

        public static void Remove(GameObject root)
        {
            if (root == null) return;
            var hp = root.GetComponent<HlodProxy>();
            if (hp != null)
            {
                hp.enabled = false;   // OnDisable kaynak renderer'ları geri açar
                Object.DestroyImmediate(hp);
            }
            var t = root.transform.Find("HLOD_Proxy");
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        public static bool Build(GameObject root, float minExtent, int triBudget)
        {
            if (root == null) return false;
            Remove(root);

            var sources = new List<MeshRenderer>();
            var parts = new List<MeshRenderer>();
            var extents = new List<float>();
            var tris = new List<int>();
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                if (mr.GetComponentInParent<LODGroup>() != null) continue;   // kendi LOD'u olan (ağaç vb.) hariç
                sources.Add(mr);
                var s = mr.bounds.size;
                parts.Add(mr);
                extents.Add(Mathf.Max(s.x, Mathf.Max(s.y, s.z)));
                tris.Add((int)(mf.sharedMesh.triangles.Length / 3));
            }
            if (parts.Count == 0)
            {
                Debug.LogWarning("[HLOD] " + root.name + ": okunabilir MeshRenderer yok (Read/Write açık mı?).");
                return false;
            }

            var keep = HlodMath.SelectParts(extents.ToArray(), tris.ToArray(), minExtent, triBudget);
            var byMat = new Dictionary<Material, List<CombineInstance>>();
            var toRoot = root.transform.worldToLocalMatrix;
            var kept = 0;
            for (var i = 0; i < parts.Count; i++)
            {
                if (!keep[i]) continue;
                var mr = parts[i];
                var mesh = mr.GetComponent<MeshFilter>().sharedMesh;
                var mats = mr.sharedMaterials;
                for (var sm = 0; sm < mesh.subMeshCount && sm < mats.Length; sm++)
                {
                    if (mats[sm] == null) continue;
                    if (!byMat.TryGetValue(mats[sm], out var list))
                        byMat[mats[sm]] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = sm, transform = toRoot * mr.transform.localToWorldMatrix });
                }
                kept++;
            }
            if (byMat.Count == 0) return false;

            System.IO.Directory.CreateDirectory(OutDir);
            var holder = new GameObject("HLOD_Proxy");
            holder.transform.SetParent(root.transform, false);
            var proxies = new List<Renderer>();
            var idx = 0;
            foreach (var kv in byMat)
            {
                long verts = 0;
                foreach (var ci in kv.Value) verts += ci.mesh.vertexCount;
                var combined = new Mesh
                {
                    name = root.name + "_HLOD_" + idx,
                    indexFormat = HlodMath.FitsUInt16(verts) ? IndexFormat.UInt16 : IndexFormat.UInt32
                };
                combined.CombineMeshes(kv.Value.ToArray(), true, true);
                combined.RecalculateBounds();
                var path = AssetDatabase.GenerateUniqueAssetPath(OutDir + "/" + Sanitize(root.name) + "_HLOD_" + idx + ".asset");
                AssetDatabase.CreateAsset(combined, path);

                var child = new GameObject("HLOD_" + idx + "_" + kv.Key.name);
                child.transform.SetParent(holder.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = combined;
                var r = child.AddComponent<MeshRenderer>();
                r.sharedMaterial = kv.Key;
                r.shadowCastingMode = ShadowCastingMode.Off;   // uzakta gölge maliyeti yok
                r.enabled = false;
                GameObjectUtility.SetStaticEditorFlags(child, StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic);
                proxies.Add(r);
                idx++;
            }

            var hp = root.GetComponent<HlodProxy>() ?? root.AddComponent<HlodProxy>();
            hp.Configure(sources.ToArray(), proxies.ToArray(), root.transform.position);
            EditorUtility.SetDirty(hp);
            AssetDatabase.SaveAssets();
            Debug.Log("[HLOD] " + root.name + ": " + kept + "/" + parts.Count + " parça, " + proxies.Count + " malzeme proxy'si.");
            return true;
        }

        private static string Sanitize(string s)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }
    }
}
