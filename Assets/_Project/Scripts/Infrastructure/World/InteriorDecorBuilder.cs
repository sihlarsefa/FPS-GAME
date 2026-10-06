using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Yaşanmış ev süsü: duvar halısı, çerçeve, yansımasız koyu ayna, tavan ampulü (gece yanar), köşe örümcek ağı, pencere pervazı saksısı.
    /// Hiçbirinin çarpıştırıcısı yok (NavMesh etkilenmez). Oda başına InteriorDecorPlan.RoomTriBudget üçgen sınırı.
    /// ENTEGRASYON: BuildingGenerator pencere açıklığı hook'u: InteriorDecorBuilder.WindowSill(parent, sillCenter, along, width, inward, seed) çağırmalı (pencere konumu burada bilinmiyor);
    /// duvar süsü pencere önüne denk gelebilir (pencere listesi InteriorFurnisher.Furnish'e verilmiyor).
    /// </summary>
    public static class InteriorDecorBuilder
    {
        private static Mesh _webQuad;
        private static Material _webMat;
        private static readonly List<DecorItem> Items = new List<DecorItem>(16);
        private static readonly MaterialId[] Pictures = { MaterialId.Sand, MaterialId.DryGrass, MaterialId.Stone, MaterialId.Blue, MaterialId.Green };

        public static void Decorate(Transform parent, Rect room, float floorY, float ceilH, RoomKind kind, int seed,
            IList<Rect> doorZones, List<FurniturePlacement> placed)
        {
            if (parent == null || room.width < 1.8f || room.height < 1.8f)
                return;
            var rng = new System.Random(seed * 31 + 7);
            Items.Clear();
            InteriorDecorPlan.Plan(room, kind, ceilH, rng, doorZones, placed, Items);
            var g = StructureKit.CreateGroup(parent, "Suslemeler", Vector3.zero, Quaternion.identity);
            for (var i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                switch (it.Kind)
                {
                    case DecorKind.Cobweb: Web(g.transform, room, floorY, it); break;
                    default: Hanging(g.transform, room, floorY, it, rng); break;
                }
            }

            Items.Clear();
            Bulb(g.transform, room, floorY, ceilH);
            if (kind == RoomKind.LivingRoom || kind == RoomKind.Kitchen)
                TablePot(g.transform, placed, floorY, rng);
        }

        private static float YawOf(int wall) => wall == 0 ? 0f : wall == 1 ? 270f : wall == 2 ? 180f : 90f;

        private static Vector3 WallPoint(Rect room, int wall, float u, float y, float floorY, float off)
        {
            switch (wall)
            {
                case 0: return new Vector3(room.xMin + u, floorY + y, room.yMin + off);
                case 2: return new Vector3(room.xMin + u, floorY + y, room.yMax - off);
                case 1: return new Vector3(room.xMax - off, floorY + y, room.yMin + u);
                default: return new Vector3(room.xMin + off, floorY + y, room.yMin + u);
            }
        }

        private static void Hanging(Transform parent, Rect room, float floorY, DecorItem it, System.Random rng)
        {
            var root = StructureKit.CreateGroup(parent, it.Kind.ToString(), WallPoint(room, it.Wall, it.U, it.Y, floorY, 0.02f), Quaternion.Euler(0f, YawOf(it.Wall), 0f)).transform;
            var w = it.Width;
            var h = it.Height;
            switch (it.Kind)
            {
                case DecorKind.WallRug:
                    Box(root, "Hali", 0f, 0f, 0.012f, w, h, 0.024f, MaterialId.Red);
                    Box(root, "HaliOrta", 0f, 0f, 0.026f, w - 0.22f, h - 0.22f, 0.006f, MaterialId.Carpet);
                    Box(root, "HaliMotif", 0f, 0f, 0.031f, w * 0.4f, h * 0.45f, 0.004f, MaterialId.Yellow);
                    break;
                case DecorKind.Mirror:
                    Box(root, "Cerceve", 0f, 0f, 0.015f, w, h, 0.03f, MaterialId.WoodDark);
                    // Yansımasız koyu cam: gerçek yansıma yok, mat siyah levha
                    Box(root, "Cam", 0f, 0f, 0.032f, w - 0.1f, h - 0.1f, 0.006f, MaterialId.Black);
                    break;
                default:
                    Box(root, "Cerceve", 0f, 0f, 0.015f, w, h, 0.03f, MaterialId.Wood);
                    Box(root, "Resim", 0f, 0f, 0.032f, w - 0.08f, h - 0.08f, 0.006f, Pictures[rng.Next(Pictures.Length)]);
                    break;
            }
        }

        private static void Box(Transform t, string n, float x, float y, float z, float sx, float sy, float sz, MaterialId m)
        {
            var go = StructureKit.CreateBox(t, n, new Vector3(x, y, z), new Vector3(sx, sy, sz), Quaternion.identity, m, false);
            go.isStatic = true;
        }

        private static void Bulb(Transform parent, Rect room, float floorY, float ceilH)
        {
            var pos = new Vector3(room.center.x, floorY + ceilH, room.center.y);
            var root = StructureKit.CreateGroup(parent, "Ampul", pos, Quaternion.identity);
            var t = root.transform;
            StructureKit.CreateCylinder(t, "Kablo", new Vector3(0f, -0.12f, 0f), 0.006f, 0.24f, MaterialId.Black, false);
            StructureKit.CreateCylinder(t, "Duy", new Vector3(0f, -0.25f, 0f), 0.025f, 0.05f, MaterialId.MetalDark, false);
            StructureKit.CreateCylinder(t, "Cam", new Vector3(0f, -0.31f, 0f), 0.035f, 0.08f, MaterialId.Yellow, false);
            var lightGo = StructureKit.CreateGroup(t, "Isik", new Vector3(0f, -0.35f, 0f), Quaternion.identity);
            InteriorBulb.Attach(lightGo);
        }

        // Masa/pencere kenarı olmayan odalar için: masanın köşesine saksı
        private static void TablePot(Transform parent, List<FurniturePlacement> placed, float floorY, System.Random rng)
        {
            for (var i = 0; i < placed.Count; i++)
            {
                if (placed[i].Kind != FurnitureKind.Table)
                    continue;
                var c = placed[i].Footprint;
                Pot(parent, new Vector3(c.xMax - 0.15f, floorY + 0.755f, c.yMax - 0.15f), rng);
                return;
            }
        }

        /// <summary>Pencere pervazı saksısı (BuildingGenerator pencere hook'u). Tüm vektörler parent yerel uzayında; sill = pervaz üst yüzü merkezi.</summary>
        public static GameObject WindowSill(Transform parent, Vector3 sill, Vector3 along, float width, Vector3 inward, int seed)
        {
            if (parent == null || width < 0.5f)
                return null;
            var rng = new System.Random(seed);
            var root = StructureKit.CreateGroup(parent, "Pervaz", Vector3.zero, Quaternion.identity);
            var a = along.normalized;
            var n = rng.Next(1, 3);
            for (var i = 0; i < n; i++)
            {
                var u = n == 1 ? (float)(rng.NextDouble() - 0.5) * width * 0.5f : (i == 0 ? -1f : 1f) * width * 0.28f;
                Pot(root.transform, sill + a * u + inward.normalized * 0.09f, rng);
            }

            return root;
        }

        private static void Pot(Transform parent, Vector3 basePos, System.Random rng)
        {
            StructureKit.CreateCylinder(parent, "Saksi", basePos + new Vector3(0f, 0.06f, 0f), 0.06f, 0.12f, MaterialId.Rust, false);
            StructureKit.CreateCylinder(parent, "Toprak", basePos + new Vector3(0f, 0.125f, 0f), 0.05f, 0.01f, MaterialId.Dirt, false);
            var h = 0.12f + (float)rng.NextDouble() * 0.1f;
            StructureKit.CreateBox(parent, "Yaprak", basePos + new Vector3(0f, 0.13f + h * 0.5f, 0f), new Vector3(0.1f, h, 0.1f), Quaternion.Euler(0f, rng.Next(0, 90), 0f), MaterialId.Foliage, false);
        }

        private static void Web(Transform parent, Rect room, float floorY, DecorItem it)
        {
            // Köşe it.Wall: 0 (xMin,yMin) 1 (xMax,yMin) 2 (xMax,yMax) 3 (xMin,yMax). NearEnd: iki duvardan hangisi.
            var cx = it.Wall == 1 || it.Wall == 2 ? room.xMax : room.xMin;
            var cz = it.Wall >= 2 ? room.yMax : room.yMin;
            var sx = cx == room.xMin ? 1f : -1f;
            var sz = cz == room.yMin ? 1f : -1f;
            Vector3 a, inward;
            if (it.NearEnd)
            {
                a = new Vector3(sx, 0f, 0f);       // z duvarı boyunca
                inward = new Vector3(0f, 0f, sz);
            }
            else
            {
                a = new Vector3(0f, 0f, sz);       // x duvarı boyunca
                inward = new Vector3(sx, 0f, 0f);
            }

            var go = new GameObject("Orumcek");
            go.layer = GameLayers.Default;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(cx, floorY + it.Y - 0.01f, cz) + inward * 0.012f;
            go.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(a, Vector3.up), Vector3.up);
            go.transform.localScale = new Vector3(it.Width, it.Height, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = WebQuad();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = WebMat();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private static Mesh WebQuad()
        {
            if (_webQuad != null)
                return _webQuad;
            _webQuad = new Mesh { name = "OrumcekKart" };
            _webQuad.vertices = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, -1, 0), new Vector3(0, -1, 0) };
            _webQuad.uv = new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) };
            _webQuad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _webQuad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            _webQuad.RecalculateBounds();
            return _webQuad;
        }

        private static Material WebMat()
        {
            if (_webMat != null)
                return _webMat;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "OrumcekAlfa", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (var y = 0; y < n; y++)
            {
                for (var x = 0; x < n; x++)
                {
                    // Köşe (0,n-1) merkezli yelpaze: ışınlar + eş merkezli yaylar
                    var dx = x / (float)(n - 1);
                    var dy = (n - 1 - y) / (float)(n - 1);
                    var rad = Mathf.Sqrt(dx * dx + dy * dy);
                    var ang = Mathf.Atan2(dy, dx) / (Mathf.PI * 0.5f);
                    var spoke = Mathf.Abs(Mathf.Repeat(ang * 5f, 1f) - 0.5f) < 0.04f + rad * 0.02f ? 1f : 0f;
                    var ring = Mathf.Abs(Mathf.Repeat(rad * 7f, 1f) - 0.5f) < 0.06f ? 1f : 0f;
                    var inQuad = rad < 1f ? 1f : 0f;
                    var a = Mathf.Max(spoke, ring * (ang > 0.02f && ang < 0.98f ? 1f : 0f)) * inQuad * (1f - rad * 0.55f);
                    px[y * n + x] = new Color32(235, 235, 230, (byte)Mathf.Clamp(a * 150f, 0f, 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            _webMat = new Material(sh) { name = "IcOrumcek", mainTexture = tex, renderQueue = 3000 };
            if (_webMat.HasProperty("_BaseMap"))
                _webMat.SetTexture("_BaseMap", tex);
            if (_webMat.HasProperty("_BaseColor"))
                _webMat.SetColor("_BaseColor", Color.white);
            if (_webMat.HasProperty("_Surface"))
            {
                _webMat.SetFloat("_Surface", 1f);
                _webMat.SetFloat("_Blend", 0f);
                _webMat.SetFloat("_SrcBlend", 5f);
                _webMat.SetFloat("_DstBlend", 10f);
                _webMat.SetFloat("_ZWrite", 0f);
                _webMat.SetFloat("_Cull", 0f);
                _webMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _webMat.SetOverrideTag("RenderType", "Transparent");
            }

            return _webMat;
        }
    }
}
