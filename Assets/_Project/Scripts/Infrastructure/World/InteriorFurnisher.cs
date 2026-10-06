using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;
using UnityEngine.AI;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Oda mobilyalama: yatak, dolap, masa+sandalye, mutfak rafı, kilim, borulu soba, sandık. PropFactory tarzı kutu parçalar,
    /// çarpıştırıcılı + NavMeshObstacle (carve). Ganimet çapalarını InteriorAnchors'a kaydeder.
    /// BuildingGenerator.Furnish (BuildingGeneratorInterior) oda başına bu sınıfı çağırır; ev tipi (köy/konut) odalarda tek mobilya sistemi budur.
    /// </summary>
    public static class InteriorFurnisher
    {
        private static readonly List<FurniturePlacement> Scratch = new List<FurniturePlacement>(16);

        /// <summary>Oda planı: Furnish ile bire bir aynı yerleşim (aynı tohum). BuildingGenerator bunu engel/ganimet kaydı için üretim anında çağırır.</summary>
        public static int PlanRoom(Rect room, RoomKind kind, int seed, IList<Rect> doorZones, int maxPieces, List<FurniturePlacement> output)
        {
            output.Clear();
            InteriorLayout.Plan(room, kind, new System.Random(seed), doorZones, output);
            if (maxPieces >= 0 && output.Count > maxPieces)
            {
                // Zemin kilimi sayılmaz: engelleyici parçalar öncelikli kalır
                for (var i = output.Count - 1; i >= 0 && output.Count > maxPieces; i--)
                {
                    if (output[i].Kind == FurnitureKind.Rug)
                        output.RemoveAt(i);
                }

                if (output.Count > maxPieces)
                    output.RemoveRange(maxPieces, output.Count - maxPieces);
            }

            return output.Count;
        }

        /// <param name="maxPieces">Kalite kademesine bağlı oda başına parça sınırı (PlanRoom ile aynı olmalı).</param>
        /// <param name="tipChance">Sandalye/masa devrik olasılığı (savaş yorgunu).</param>
        /// <param name="windowZones">Duvar süsünün kaçınacağı pencere önleri (mobilya yerleşimini etkilemez).</param>
        public static GameObject Furnish(Transform parent, Rect room, float floorY, float ceilingH, RoomKind kind, int seed,
            IList<Rect> doorZones, bool registerAnchors = true, int maxPieces = -1, float tipChance = 0f, IList<Rect> windowZones = null)
        {
            var rng = new System.Random(seed);
            var tipRng = new System.Random(seed ^ 0x5bd1e995);
            PlanRoom(room, kind, seed, doorZones, maxPieces, Scratch);
            var group = StructureKit.CreateGroup(parent, "Ic_" + kind, Vector3.zero, Quaternion.identity);
            for (var i = 0; i < Scratch.Count; i++)
            {
                var p = Scratch[i];
                var s = InteriorLayout.Size(p.Kind);
                var tipped = tipChance > 0f && (p.Kind == FurnitureKind.Chair || p.Kind == FurnitureKind.Table) && tipRng.NextDouble() < tipChance;
                var pos = p.Center(floorY);
                var rot = Quaternion.Euler(0f, p.Yaw, 0f);
                if (tipped)
                {
                    if (p.Kind == FurnitureKind.Table)
                    {
                        pos.y += 0.78f;
                        rot *= Quaternion.Euler(0f, 0f, 180f);
                    }
                    else
                    {
                        pos.y += s.x * 0.5f;
                        rot *= Quaternion.Euler(0f, 0f, tipRng.NextDouble() < 0.5 ? 90f : -90f);
                    }
                }

                var root = StructureKit.CreateGroup(group.transform, p.Kind.ToString() + (tipped ? "_Devrik" : string.Empty), pos, rot);
                Build(root.transform, p.Kind, s.x, s.y, ceilingH - 0.0f, rng);
                if (p.Blocks)
                    AddObstacle(root, s, InteriorLayout.Height(p.Kind));
                if (registerAnchors && !tipped)
                    RegisterAnchor(root.transform, p.Kind, kind, floorY);
            }

            var zones = doorZones;
            if (windowZones != null && windowZones.Count > 0)
            {
                var merged = new List<Rect>(windowZones);
                if (doorZones != null)
                    merged.AddRange(doorZones);
                zones = merged;
            }

            InteriorDecorBuilder.Decorate(group.transform, room, floorY, ceilingH, kind, seed, zones, Scratch);
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
