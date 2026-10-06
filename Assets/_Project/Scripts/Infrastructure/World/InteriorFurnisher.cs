using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;
using UnityEngine.AI;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Oda mobilyalama: yatak, dolap, masa+sandalye, mutfak rafı, kilim, borulu soba, sandık. PropFactory tarzı kutu parçalar,
    /// çarpıştırıcılı + NavMeshObstacle (carve). Ganimet çapalarını InteriorAnchors'a kaydeder.
    /// ENTEGRASYON: BuildingGenerator.Furnish: oda başına InteriorFurnisher.Furnish(parent, room, floorY, ceilH, kind, seed, doorZones) çağrısı (public hook).
    /// </summary>
    public static class InteriorFurnisher
    {
        private static readonly List<FurniturePlacement> Scratch = new List<FurniturePlacement>(16);

        public static GameObject Furnish(Transform parent, Rect room, float floorY, float ceilingH, RoomKind kind, int seed,
            IList<Rect> doorZones, bool registerAnchors = true)
        {
            var rng = new System.Random(seed);
            Scratch.Clear();
            InteriorLayout.Plan(room, kind, rng, doorZones, Scratch);
            var group = StructureKit.CreateGroup(parent, "Ic_" + kind, Vector3.zero, Quaternion.identity);
            for (var i = 0; i < Scratch.Count; i++)
            {
                var p = Scratch[i];
                var root = StructureKit.CreateGroup(group.transform, p.Kind.ToString(), p.Center(floorY), Quaternion.Euler(0f, p.Yaw, 0f));
                var s = InteriorLayout.Size(p.Kind);
                Build(root.transform, p.Kind, s.x, s.y, ceilingH - 0.0f, rng);
                if (p.Blocks)
                    AddObstacle(root, s, InteriorLayout.Height(p.Kind));
                if (registerAnchors)
                    RegisterAnchor(root.transform, p.Kind, kind, floorY);
            }

            InteriorDecorBuilder.Decorate(group.transform, room, floorY, ceilingH, kind, seed, doorZones, Scratch);
            Scratch.Clear();
            return group;
        }

        private static void AddObstacle(GameObject root, Vector2 size, float h)
        {
            var o = root.AddComponent<NavMeshObstacle>();
            o.shape = NavMeshObstacleShape.Box;
            o.center = new Vector3(0f, h * 0.5f, 0f);
            o.size = new Vector3(size.x, h, size.y);
            o.carving = true;
            o.carveOnlyStationary = true;
        }

        private static void RegisterAnchor(Transform root, FurnitureKind k, RoomKind room, float floorY)
        {
            InteriorAnchorKind ak;
            float h;
            switch (k)
            {
                case FurnitureKind.Table: ak = InteriorAnchorKind.Table; h = 0.78f; break;
                case FurnitureKind.KitchenShelf: ak = InteriorAnchorKind.Shelf; h = 0.95f; break;
                case FurnitureKind.Wardrobe: ak = InteriorAnchorKind.Wardrobe; h = 0.05f; break;
                case FurnitureKind.Bed: ak = InteriorAnchorKind.Bed; h = 0.6f; break;
                case FurnitureKind.Crate: ak = InteriorAnchorKind.Crate; h = 0.55f; break;
                default: return;
            }

            var local = new Vector3(0f, h, k == FurnitureKind.Wardrobe ? InteriorLayout.Size(k).y * 0.5f + 0.35f : 0f);
            var pos = root.TransformPoint(local);
            if (k == FurnitureKind.Wardrobe)
                pos.y = floorY + 0.05f;
            InteriorAnchors.Register(pos, ak, room, InteriorAnchors.TierFor(ak));
        }

        private static void Box(Transform t, string n, float x, float y, float z, float sx, float sy, float sz, MaterialId m, bool col = true)
        {
            StructureKit.CreateBox(t, n, new Vector3(x, y, z), new Vector3(sx, sy, sz), Quaternion.identity, m, col);
        }

        // Yerel: X genişlik, Z derinlik (arka duvar -Z), Y yukarı; (0,0,0) = zemin ayak izi merkezi.
        private static void Build(Transform t, FurnitureKind k, float w, float d, float ceilH, System.Random rng)
        {
            switch (k)
            {
                case FurnitureKind.Bed:
                    Box(t, "Cerceve", 0f, 0.22f, 0f, w, 0.26f, d, MaterialId.WoodDark);
                    Box(t, "Sunger", 0f, 0.43f, 0.05f, w - 0.1f, 0.16f, d - 0.2f, MaterialId.TentCanvas, false);
                    Box(t, "Yastik", 0f, 0.55f, -d * 0.5f + 0.3f, w - 0.45f, 0.1f, 0.35f, MaterialId.White, false);
                    Box(t, "Bashk", 0f, 0.6f, -d * 0.5f + 0.03f, w, 0.8f, 0.06f, MaterialId.Wood, false);
                    Box(t, "Yorgan", 0f, 0.53f, 0.3f, w - 0.06f, 0.08f, d * 0.58f, MaterialId.Red, false);
                    Box(t, "YorganSerit", 0f, 0.575f, 0.3f, w - 0.4f, 0.01f, d * 0.58f - 0.06f, MaterialId.Yellow, false);
                    break;
                case FurnitureKind.Wardrobe:
                    Box(t, "Govde", 0f, 0.95f, 0f, w, 1.9f, d, MaterialId.Wood);
                    Box(t, "KapiCizgi", 0f, 0.95f, d * 0.5f + 0.005f, 0.02f, 1.7f, 0.01f, MaterialId.WoodDark, false);
                    Box(t, "KolSol", -0.08f, 1.0f, d * 0.5f + 0.02f, 0.03f, 0.14f, 0.03f, MaterialId.MetalDark, false);
                    Box(t, "KolSag", 0.08f, 1.0f, d * 0.5f + 0.02f, 0.03f, 0.14f, 0.03f, MaterialId.MetalDark, false);
                    break;
                case FurnitureKind.Table:
                    Box(t, "Tabla", 0f, 0.73f, 0f, w, 0.05f, d, MaterialId.Wood);
                    for (var sx = -1; sx <= 1; sx += 2)
                        for (var sz = -1; sz <= 1; sz += 2)
                            Box(t, "Ayak", sx * (w * 0.5f - 0.06f), 0.355f, sz * (d * 0.5f - 0.06f), 0.07f, 0.71f, 0.07f, MaterialId.WoodDark, false);
                    Box(t, "Ortu", 0f, 0.762f, 0f, w * 0.6f, 0.006f, d * 0.6f, MaterialId.White, false);
                    StructureKit.CreateCylinder(t, "Cay", new Vector3(-w * 0.2f, 0.79f, 0f), 0.04f, 0.05f, MaterialId.Glass, false);
                    StructureKit.CreateCylinder(t, "Demlik", new Vector3(w * 0.15f, 0.82f, 0.05f), 0.07f, 0.11f, MaterialId.MetalPanel, false);
                    break;
                case FurnitureKind.Chair:
                    Box(t, "Oturak", 0f, 0.45f, 0f, w, 0.04f, d, MaterialId.Wood, false);
                    Box(t, "Sirt", 0f, 0.72f, -d * 0.5f + 0.02f, w, 0.5f, 0.04f, MaterialId.WoodDark, false);
                    for (var sx = -1; sx <= 1; sx += 2)
                        for (var sz = -1; sz <= 1; sz += 2)
                            Box(t, "Ayak", sx * (w * 0.5f - 0.03f), 0.215f, sz * (d * 0.5f - 0.03f), 0.04f, 0.43f, 0.04f, MaterialId.WoodDark, false);
                    var cc = t.gameObject.AddComponent<BoxCollider>();
                    cc.center = new Vector3(0f, 0.45f, 0f);
                    cc.size = new Vector3(w, 0.9f, d);
                    break;
                case FurnitureKind.KitchenShelf:
                    Box(t, "Govde", 0f, 0.45f, 0f, w, 0.9f, d, MaterialId.Wood);
                    Box(t, "Tezgah", 0f, 0.92f, 0f, w + 0.04f, 0.04f, d + 0.04f, MaterialId.WoodDark, false);
                    Box(t, "UstRaf", 0f, 1.5f, -d * 0.25f, w, 0.04f, d * 0.5f, MaterialId.Wood, false);
                    Box(t, "Kavanoz", -0.3f, 1.58f, -d * 0.25f, 0.12f, 0.14f, 0.12f, MaterialId.Glass, false);
                    Box(t, "Kavanoz2", -0.1f, 1.58f, -d * 0.25f, 0.12f, 0.14f, 0.12f, MaterialId.Orange, false);
                    Box(t, "Tabaklar", 0.3f, 1.6f, -d * 0.25f, 0.24f, 0.18f, 0.03f, MaterialId.White, false);
                    Box(t, "OrtaRaf", 0f, 1.2f, -d * 0.25f, w, 0.035f, d * 0.5f, MaterialId.Wood, false);
                    StructureKit.CreateCylinder(t, "Tencere", new Vector3(-0.4f, 1.0f, 0f), 0.11f, 0.15f, MaterialId.MetalPanel, false);
                    StructureKit.CreateCylinder(t, "TencereKucuk", new Vector3(0.05f, 0.99f, 0.02f), 0.08f, 0.12f, MaterialId.Rust, false);
                    StructureKit.CreateCylinder(t, "Tava", new Vector3(0.45f, 0.95f, 0f), 0.13f, 0.03f, MaterialId.MetalDark, false);
                    Box(t, "AsiliTencere", 0.55f, 1.35f, -d * 0.5f + 0.02f, 0.14f, 0.18f, 0.05f, MaterialId.MetalPanel, false);
                    break;
                case FurnitureKind.Rug:
                    var rug = StructureKit.CreateBox(t, "Kilim", new Vector3(0f, 0.01f, 0f), new Vector3(w, 0.015f, d), Quaternion.identity, MaterialId.Carpet, false);
                    rug.isStatic = false;
                    Box(t, "KilimKenar", 0f, 0.012f, 0f, w - 0.25f, 0.016f, d - 0.25f, MaterialId.Red, false);
                    break;
                case FurnitureKind.Stove:
                    Box(t, "Govde", 0f, 0.4f, 0f, w, 0.8f, d, MaterialId.MetalDark);
                    Box(t, "Kapak", 0f, 0.35f, d * 0.5f + 0.01f, 0.3f, 0.25f, 0.02f, MaterialId.Rust, false);
                    Box(t, "Ocak", 0f, 0.815f, 0f, w, 0.03f, d, MaterialId.MetalDark, false);
                    StructureKit.CreateCylinder(t, "OcakTencere", new Vector3(0.1f, 0.91f, 0.08f), 0.1f, 0.15f, MaterialId.MetalPanel, false);
                    Box(t, "Odun", -0.45f, 0.1f, 0.05f, 0.25f, 0.2f, 0.4f, MaterialId.DeadWood, false);
                    var pipeH = Mathf.Max(0.8f, ceilH - 0.8f - 0.05f);
                    StructureKit.CreateCylinder(t, "Boru", new Vector3(0f, 0.8f + pipeH * 0.5f, -d * 0.2f), 0.06f, pipeH, MaterialId.Rust, false);
                    break;
                case FurnitureKind.Sedir:
                    Box(t, "Govde", 0f, 0.2f, 0f, w, 0.4f, d, MaterialId.WoodDark);
                    Box(t, "Minder", 0f, 0.43f, 0.03f, w - 0.08f, 0.1f, d - 0.12f, MaterialId.Carpet, false);
                    Box(t, "SirtMinder", 0f, 0.7f, -d * 0.5f + 0.06f, w - 0.1f, 0.4f, 0.1f, MaterialId.Red, false);
                    Box(t, "YastikSol", -w * 0.35f, 0.56f, 0f, 0.35f, 0.2f, 0.12f, MaterialId.Yellow, false);
                    break;
                default:
                    Box(t, "Sandik", 0f, 0.25f, 0f, w, 0.5f, d, MaterialId.Wood);
                    Box(t, "Bant", 0f, 0.25f, 0f, w + 0.02f, 0.08f, d + 0.02f, MaterialId.MetalDark, false);
                    Box(t, "Kapak", 0f, 0.51f, 0f, w + 0.03f, 0.03f, d + 0.03f, MaterialId.WoodDark, false);
                    Box(t, "Kilit", 0f, 0.42f, d * 0.5f + 0.02f, 0.06f, 0.08f, 0.02f, MaterialId.Yellow, false);
                    break;
            }
        }
    }
}
