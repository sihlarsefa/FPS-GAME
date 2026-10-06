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

            var mesh = GetSplitVariant(variant);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { MaterialLibrary.Get(dark ? MaterialId.RockDark : MaterialId.Rock), VegetationMaterials.CreateMoss() };
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            return go;
        }

        private static readonly System.Collections.Generic.Dictionary<int, Mesh> SplitCache = new System.Collections.Generic.Dictionary<int, Mesh>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SplitCache.Clear();
        }

        /// <summary>Paylaşılan kaya mesh'i, 2 alt mesh: 0 kaya, 1 yosunlu üst yüzler (malzeme: kaya + <see cref="VegetationMaterials.CreateMoss"/>).</summary>
        public static Mesh GetSplitVariant(int variant)
        {
            variant = ((variant % VariantCount) + VariantCount) % VariantCount;
            if (SplitCache.TryGetValue(variant, out var mesh) && mesh != null)
                return mesh;
            mesh = BuildRock(1000 + variant * 101, 0.2f + (variant % 3) * 0.04f, true);
            SplitCache[variant] = mesh;
            return mesh;
        }

        /// <summary>Üst yüzeyde yosun mu? normalY: yüz normalinin y bileşeni, centerY: yüz merkezi (kaya uzayı), patch01: 0..1 yama gürültüsü.</summary>
        public static bool IsMossFace(float normalY, float centerY, float patch01)
        {
            if (normalY < 0.55f || centerY < -0.12f)
                return false;
            // Yatay yüzeyler neredeyse tam, eğimlilerde yama yama.
            var coverage = Mathf.InverseLerp(0.55f, 0.9f, normalY);
            return patch01 < 0.25f + coverage * 0.7f;
        }

        internal static Mesh BuildRockMesh(int seed, float roughness)
        {
            return BuildRock(seed, roughness, false);
        }

        internal static Mesh BuildRock(int seed, float roughness, bool split)
        {
            var rng = new System.Random(seed * 53 + 19);
            Icosphere.Get(2, out var unit, out var triangles); // 320 yüz: faset kayalar
            var stretch = new Vector3(TreeFactory.Range(rng, 0.95f, 1.25f), TreeFactory.Range(rng, 0.62f, 0.8f), TreeFactory.Range(rng, 0.8f, 1.05f));

            // Düşük frekanslı çıkıntılar (yönlü) + kırık düzlemler (açısal yüzler).
            var bumpDir = new Vector3[6];
            var bumpAmp = new float[6];
            for (var i = 0; i < bumpDir.Length; i++)
            {
                bumpDir[i] = RandomDirection(rng);
                bumpAmp[i] = TreeFactory.Range(rng, -0.16f, 0.2f);
            }

            var cutN = new Vector3[5];
            var cutD = new float[5];
            for (var i = 0; i < cutN.Length; i++)
            {
                cutN[i] = RandomDirection(rng);
                cutD[i] = TreeFactory.Range(rng, 0.74f, 0.92f);
            }

            var points = new Vector3[unit.Length];
            for (var i = 0; i < unit.Length; i++)
            {
                var dir = unit[i];
                var radius = 1f;
                for (var k = 0; k < bumpDir.Length; k++)
                    radius += bumpAmp[k] * Mathf.Pow(Mathf.Max(0f, Vector3.Dot(dir, bumpDir[k])), 3f);
                radius *= 1f + TreeFactory.Range(rng, -roughness, roughness) * 0.6f;
                var v = dir * radius;
                for (var k = 0; k < cutN.Length; k++)
                {
                    var over = Vector3.Dot(v, cutN[k]) - cutD[k];
                    if (over > 0f)
                        v -= cutN[k] * over;
                }

                v = Vector3.Scale(v, stretch);
                if (v.y < -0.35f)
                    v.y = -0.35f + (v.y + 0.35f) * 0.25f;
                points[i] = v;
            }

            var b = new MeshBuilder(split ? 2 : 1);
            for (var t = 0; t + 2 < triangles.Length; t += 3)
            {
                var p0 = points[triangles[t]];
                var p1 = points[triangles[t + 1]];
                var p2 = points[triangles[t + 2]];
                var sub = 0;
                if (split)
                {
                    var n = Vector3.Cross(p1 - p0, p2 - p0);
                    if (n.sqrMagnitude > 1e-12f)
                    {
                        n.Normalize();
                        var center = (p0 + p1 + p2) / 3f;
                        var patch = Mathf.Abs(Mathf.Sin(center.x * 7.1f + seed) * Mathf.Cos(center.z * 6.3f - seed * 0.3f) + Mathf.Sin(center.y * 9.7f)) / 1.6f;
                        if (IsMossFace(n.y, center.y, Mathf.Clamp01(patch)))
                            sub = 1;
                    }
                }

                b.AddFlatTriangle(sub, p0, p1, p2, 0.5f);
            }

            return b.ToMesh("HK_Rock" + (split ? "_Moss" : string.Empty));
        }

        private static Vector3 RandomDirection(System.Random rng)
        {
            var z = TreeFactory.Range(rng, -1f, 1f);
            var a = TreeFactory.Range(rng, 0f, Mathf.PI * 2f);
            var r = Mathf.Sqrt(1f - z * z);
            return new Vector3(r * Mathf.Cos(a), z, r * Mathf.Sin(a));
        }
    }
}
