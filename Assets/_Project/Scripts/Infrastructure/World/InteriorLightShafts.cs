using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Pencere ışık huzmesi: pencere dikdörtgeninden güneş yönünde zemine uzanan ucuz transparan yüzler + zeminde ışık lekesi.
    /// Fiziksel ışık/gölge yok (iki mesh, toplamsal malzeme). Güneş pencereden girmiyorsa kurulmaz.
    /// ENTEGRASYON: BuildingGenerator pencere açıklığı hook'u: InteriorLightShafts.TryBuild(parent, center, along, w, h, inward, sunDir, floorY).
    /// </summary>
    public static class InteriorLightShafts
    {
        private static Material _beam;
        private static Material _patch;

        /// <summary>Tüm vektörler parent yerel uzayında. center = pencere merkezi, along = duvar boyunca birim, inward = içeri yön.</summary>
        public static GameObject TryBuild(Transform parent, Vector3 center, Vector3 along, float width, float height,
            Vector3 inward, Vector3 sunLightDir, float floorY)
        {
            if (parent == null || width < 0.4f || height < 0.4f || !InteriorLayout.SunThroughWindow(sunLightDir, inward))
                return null;
            var up = Vector3.up;
            var hw = along.normalized * (width * 0.5f - 0.04f);
            var hh = up * (height * 0.5f - 0.04f);
            var c = center + inward.normalized * 0.05f;
            var bl = c - hw - hh;
            var br = c + hw - hh;
            var tl = c - hw + hh;
            var tr = c + hw + hh;
            if (!InteriorLayout.ProjectToFloor(bl, sunLightDir, floorY + 0.01f, out var fbl) ||
                !InteriorLayout.ProjectToFloor(br, sunLightDir, floorY + 0.01f, out var fbr) ||
                !InteriorLayout.ProjectToFloor(tl, sunLightDir, floorY + 0.01f, out var ftl) ||
                !InteriorLayout.ProjectToFloor(tr, sunLightDir, floorY + 0.01f, out var ftr))
                return null;
            // Çok uzun huzme (alçak güneş) kırpılır
            if ((ftl - tl).magnitude > 9f)
                return null;

            var root = StructureKit.CreateGroup(parent, "IsikHuzmesi", Vector3.zero, Quaternion.identity);
            MakeMesh(root.transform, "Leke", new[] { fbl, fbr, ftr, ftl }, PatchMat());
            // Yan yüzler (huzme hacmi): sol ve sağ
            MakeMesh(root.transform, "HuzmeSol", new[] { bl, tl, ftl, fbl }, BeamMat());
            MakeMesh(root.transform, "HuzmeSag", new[] { br, tr, ftr, fbr }, BeamMat());
            MakeMesh(root.transform, "HuzmeUst", new[] { tl, tr, ftr, ftl }, BeamMat());
            root.isStatic = false;
            return root;
        }

        private static void MakeMesh(Transform parent, string name, Vector3[] q, Material mat)
        {
            var go = new GameObject(name);
            go.layer = GameLayers.Default;
            go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = name };
            mesh.vertices = q;
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private static Material BeamMat() => _beam != null ? _beam : _beam = Make("IcHuzme", new Color(1f, 0.93f, 0.75f, 0.045f));

        private static Material PatchMat() => _patch != null ? _patch : _patch = Make("IcLeke", new Color(1f, 0.92f, 0.7f, 0.2f));

        private static Material Make(string name, Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            var m = new Material(sh) { name = name, color = c, renderQueue = 3000 };
            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 2f); // toplamsal
                m.SetFloat("_SrcBlend", 5f);
                m.SetFloat("_DstBlend", 1f);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_Cull", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
            }

            return m;
        }
    }
}
