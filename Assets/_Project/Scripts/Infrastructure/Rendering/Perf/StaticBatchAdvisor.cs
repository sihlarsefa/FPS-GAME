using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.Rendering.Perf
{
    /// <summary>Statik prop köklerindeki uygun mesh'leri StaticBatchingUtility.Combine ile birleştirir (çalışma zamanı; kök başına bir kez).</summary>
    public static class StaticBatchAdvisor
    {
        /// <summary>Kök altındaki statik batch adaylarını sayar.</summary>
        public static int CountCandidates(GameObject root)
        {
            return Collect(root).Count;
        }

        /// <summary>Adayları birleştirir; birleştirilen nesne sayısını döndürür. Düşük kademede (0) bellek için atlanır.</summary>
        public static int Apply(GameObject root, int tier)
        {
            if (root == null || tier <= 0) return 0;
            var c = Collect(root);
            if (c.Count < 2) return 0;
            StaticBatchingUtility.Combine(c.ToArray(), root);
            return c.Count;
        }

        private static List<GameObject> Collect(GameObject root)
        {
            var res = new List<GameObject>();
            if (root == null) return res;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                var go = mf.gameObject;
                var mr = go.GetComponent<MeshRenderer>();
                int verts = mf.sharedMesh != null && mf.sharedMesh.isReadable ? mf.sharedMesh.vertexCount : 0;
                if (PerfMath.IsStaticBatchCandidate(go.isStatic, mr != null && mr.isPartOfStaticBatch, mr != null, verts))
                    res.Add(go);
            }
                        // ENTEGRASYON: world/prop yerleştirme kodu icinde prop kökü kurulduktan sonra StaticBatchAdvisor.Apply(kök, PipelineTiers kademesi) çağrılmalı.
            return res;
        }
    }
}
