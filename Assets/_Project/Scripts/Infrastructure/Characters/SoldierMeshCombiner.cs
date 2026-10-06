using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Characters
{
    /// <summary>Birleştirme adayı (saf veri): parça adı + ebeveyn/malzeme kimliği + gölge ayarı.</summary>
    public struct CombineCandidate
    {
        public string Name;
        public int ParentId;
        public int MaterialId;
        public bool CastShadows;
    }

    /// <summary>Saf gruplama kuralları (Unity nesnesi yok, test edilebilir).</summary>
    public static class SoldierMeshCombinerRules
    {
        /// <summary>Bu eşiğin altındaki askerlerde birleştirme yapılmaz.</summary>
        public const int MinPartCount = 30;

        /// <summary>Çalışma zamanında değişebilen / ayrı kalması gereken parça adları (tam eşleşme).</summary>
        private static readonly HashSet<string> ExcludedNames = new HashSet<string>
        {
            "Skull",        // SetWear özel malzeme (yüz derisi)
            "ChestShape",   // görünürlük probu (isVisible)
            "Armband",      // takım rengi
            "Eyelid", "Eye", "Iris", "Brow", "LipLine", "Stubble", "Mustache" // yüz/göz katmanları
        };

        private static readonly string[] ExcludedPrefixes =
        {
            "Balaclava", "Shemagh",             // yüz örtüsü
            "Helmet", "Cap", "Beret", "Chin", "Nvg", "Rail", "EarCup", "HeadsetCup", // kask/başlık (devrilebilir)
            "Pack", "Bedroll", "Vest", "Plate", "Strap", "Mag", // yelek/çanta varyantları
            "Rank", "Pip", "Chevron", "Weapon"
        };

        public static bool IsExcluded(string name)
        {
            if (string.IsNullOrEmpty(name))
                return true;
            if (ExcludedNames.Contains(name))
                return true;
            for (var i = 0; i < ExcludedPrefixes.Length; i++)
            {
                if (name.StartsWith(ExcludedPrefixes[i], System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Aday listesini (ebeveyn, malzeme, gölge) gruplarına böler. Yalnız dışlanmayan adaylar; en az 2 üyeli gruplar döner.
        /// Toplam aday sayısı <see cref="MinPartCount"/> değerini aşmıyorsa boş döner. Her grup aday indekslerinden oluşur.
        /// </summary>
        public static List<List<int>> Plan(IReadOnlyList<CombineCandidate> candidates)
        {
            var result = new List<List<int>>();
            if (candidates == null || candidates.Count <= MinPartCount)
                return result;

            var map = new Dictionary<(int, int, bool), List<int>>();
            for (var i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (IsExcluded(c.Name))
                    continue;
                var key = (c.ParentId, c.MaterialId, c.CastShadows);
                if (!map.TryGetValue(key, out var list))
                {
                    list = new List<int>(4);
                    map[key] = list;
                    result.Add(list);
                }

                list.Add(i);
            }

            result.RemoveAll(g => g.Count < 2);
            return result;
        }
    }

    /// <summary>
    /// Asker kurulumundan sonra STATİK dişli parçaları (aynı kemik + aynı malzeme örneği + aynı gölge modu) tek mesh'e birleştirir.
    /// Kemik/poz çalışmaya devam eder: birleşik renderer aynı kemiğe bağlanır. SetWear malzeme örneği değiştirdiğinden birleşik
    /// renderer'ın malzemesi de değişir (renderer kayıt listesine eklenir).
    /// </summary>
    public static class SoldierMeshCombiner
    {
        /// <summary>Tam geri dönüş bayrağı: false → hiçbir birleştirme yapılmaz.</summary>
        public static bool Enabled = true;

        public struct Result
        {
            public int Before;
            public int After;
            public List<Mesh> Meshes;
            public List<GameObject> Created;
            public List<GameObject> Removed;
        }

        /// <summary>
        /// <paramref name="parts"/> içindeki MeshRenderer'ları birleştirir. Dönen Result.Removed GameObject'leri çağıran yok eder
        /// (kayıt listesinden çıkarmak için), Result.Created yeni renderer'lı nesnelerdir.
        /// </summary>
        public static bool TryCombine(IList<MeshRenderer> parts, int layer, out Result result)
        {
            result = default;
            if (!Enabled || parts == null)
                return false;

            var candidates = new List<CombineCandidate>(parts.Count);
            var valid = new List<MeshRenderer>(parts.Count);
            var parentIds = new Dictionary<Transform, int>();
            var matIds = new Dictionary<Material, int>();
            for (var i = 0; i < parts.Count; i++)
            {
                var r = parts[i];
                if (r == null || r.transform.parent == null || !r.TryGetComponent<MeshFilter>(out var f) ||
                    f.sharedMesh == null || !f.sharedMesh.isReadable || r.sharedMaterial == null || !r.gameObject.activeSelf)
                    continue;
                var p = r.transform.parent;
                if (!parentIds.TryGetValue(p, out var pid)) { pid = parentIds.Count; parentIds[p] = pid; }
                var mat = r.sharedMaterial;
                if (!matIds.TryGetValue(mat, out var mid)) { mid = matIds.Count; matIds[mat] = mid; }
                candidates.Add(new CombineCandidate
                {
                    Name = r.name, ParentId = pid, MaterialId = mid, CastShadows = r.shadowCastingMode != ShadowCastingMode.Off
                });
                valid.Add(r);
            }

            var groups = SoldierMeshCombinerRules.Plan(candidates);
            if (groups.Count == 0)
                return false;

            result = new Result
            {
                Before = parts.Count,
                Meshes = new List<Mesh>(groups.Count),
                Created = new List<GameObject>(groups.Count),
                Removed = new List<GameObject>(parts.Count)
            };

            for (var g = 0; g < groups.Count; g++)
            {
                var first = valid[groups[g][0]];
                var parent = first.transform.parent;
                var ci = new CombineInstance[groups[g].Count];
                var verts = 0;
                for (var k = 0; k < ci.Length; k++)
                {
                    var r = valid[groups[g][k]];
                    var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                    ci[k].mesh = mesh;
                    ci[k].transform = parent.worldToLocalMatrix * r.transform.localToWorldMatrix;
                    verts += mesh.vertexCount;
                    result.Removed.Add(r.gameObject);
                }

                var combined = new Mesh { name = "soldierCombined_" + first.sharedMaterial.name };
                if (verts > 65000)
                    combined.indexFormat = IndexFormat.UInt32;
                combined.CombineMeshes(ci, true, true);
                combined.RecalculateBounds();

                var go = new GameObject("Combined_" + first.sharedMaterial.name) { layer = layer };
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = combined;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = first.sharedMaterial;
                mr.shadowCastingMode = first.shadowCastingMode;
                mr.receiveShadows = first.receiveShadows;
                mr.lightProbeUsage = first.lightProbeUsage;
                mr.reflectionProbeUsage = first.reflectionProbeUsage;
                mr.motionVectorGenerationMode = first.motionVectorGenerationMode;
                mr.allowOcclusionWhenDynamic = first.allowOcclusionWhenDynamic;
                result.Meshes.Add(combined);
                result.Created.Add(go);
            }

            result.After = parts.Count - result.Removed.Count + result.Created.Count;
            return true;
        }
    }
}
