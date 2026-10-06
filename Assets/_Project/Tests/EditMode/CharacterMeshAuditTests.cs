#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Characters;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class CharacterMeshAuditTests
    {
        [Test]
        public void EverySoldierMesh_HasFiniteVerticesAndNormals()
        {
            var all = CharacterMeshAudit.BuildAll();
            Assert.Greater(all.Count, 60, "denetlenen ağ sayısı");
            var errors = new System.Text.StringBuilder();
            foreach (var e in all)
            {
                if (e.Verts.Length == 0) { errors.AppendLine(e.Key + ": 0 vertex"); continue; }
                var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                int badV = 0, badN = 0, badUv = 0;
                for (var i = 0; i < e.Verts.Length; i++)
                {
                    var v = e.Verts[i];
                    if (!Finite(v)) { badV++; continue; }
                    min = Vector3.Min(min, v);
                    max = Vector3.Max(max, v);
                    var n = e.Normals[i];
                    if (!Finite(n) || n.sqrMagnitude < 1e-8f) badN++;
                    var u = e.Uvs[i];
                    if (float.IsNaN(u.x) || float.IsNaN(u.y) || float.IsInfinity(u.x) || float.IsInfinity(u.y)) badUv++;
                }

                if (badV + badN + badUv > 0)
                    errors.AppendLine(e.Key + ": badVerts=" + badV + " badNormals=" + badN + " badUv=" + badUv);
                else
                {
                    var size = max - min;
                    var big = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                    if (big <= 0.005f || big >= 3f)
                        errors.AppendLine(e.Key + ": bounds " + size);
                }
            }

            Assert.AreEqual(string.Empty, errors.ToString(), errors.ToString());
        }

        private static bool Finite(Vector3 v)
        {
            return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                     float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        }
    }
}
#endif
