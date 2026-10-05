using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Low-poly kaya üretimi: kaya mesh'i (düz gölgeli, basık tabanlı ikosfer) ve çarpıştırıcılı kaya nesneleri.
    /// Mesh'ler varyant başına paylaşılır (<see cref="VariantCount"/> farklı şekil).
    /// </summary>
    public static class RockFactory
    {
        public const int VariantCount = 8;

        /// <summary>Paylaşılan kaya mesh'i (variant 0..VariantCount-1).</summary>
        public static Mesh GetVariant(int variant)
        {
            variant = ((variant % VariantCount) + VariantCount) % VariantCount;
            return MeshFactory.Rock(1000 + variant * 101, 0.2f + (variant % 3) * 0.04f);
        }

        /// <summary>
        /// Kaya nesnesi oluşturur (MeshFilter/MeshRenderer + MeshCollider, Default katman). Pivot kaya merkezi; mesh yarıçapı ~1 m,
        /// tabanı yaklaşık -0.35 m'de — zemine oturtmak için y = zemin + 0.25 * scale.y önerilir.
        /// </summary>
        public static GameObject CreateRock(Transform parent, Vector3 position, Quaternion rotation, Vector3 scale, int variant, bool dark = false)
        {
            var go = new GameObject("HK_Rock");
            if (parent != null)
                go.transform.SetParent(parent, false);
            go.layer = GameLayers.Default;
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;

            var mesh = GetVariant(variant);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialLibrary.Get(dark ? MaterialId.RockDark : MaterialId.Rock);
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            return go;
        }

        internal static Mesh BuildRockMesh(int seed, float roughness)
        {
            var rng = new System.Random(seed * 53 + 19);
            Icosphere.Get(1, out var unit, out var triangles);
            var stretch = new Vector3(TreeFactory.Range(rng, 0.95f, 1.25f), TreeFactory.Range(rng, 0.62f, 0.8f), TreeFactory.Range(rng, 0.8f, 1.05f));
            var points = new Vector3[unit.Length];
            for (var i = 0; i < unit.Length; i++)
            {
                var v = unit[i] * (1f + TreeFactory.Range(rng, -roughness, roughness));
                v = Vector3.Scale(v, stretch);
                if (v.y < -0.35f)
                    v.y = -0.35f + (v.y + 0.35f) * 0.25f;
                points[i] = v;
            }

            var b = new MeshBuilder();
            for (var t = 0; t + 2 < triangles.Length; t += 3)
                b.AddFlatTriangle(0, points[triangles[t]], points[triangles[t + 1]], points[triangles[t + 2]], 0.5f);
            return b.ToMesh("HK_Rock");
        }
    }
}
