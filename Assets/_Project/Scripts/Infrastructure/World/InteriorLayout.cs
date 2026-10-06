using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.World
{
    public enum RoomKind { LivingRoom, Bedroom, Kitchen, Storage }

    public enum FurnitureKind { Bed, Wardrobe, Table, Chair, KitchenShelf, Rug, Stove, Crate, Sedir }

    /// <summary>Oda içi yerleşim kaydı. Rect = zemin ayak izi (XZ, bina yerel uzayı), Yaw = ön yüzün bakışı.</summary>
    public struct FurniturePlacement
    {
        public FurnitureKind Kind;
        public Rect Footprint;
        public float Yaw;
        public bool Blocks;

        public Vector3 Center(float y) => new Vector3(Footprint.center.x, y, Footprint.center.y);
    }

    /// <summary>Saf mantık: oda türü seçimi ve mobilya yerleştirme (duvara yaslı, kapı önü açık, çakışmasız, deterministik).</summary>
    public static class InteriorLayout
    {
        public const float WallGap = 0.06f;
        public const float DoorClearance = 0.95f;
        public const float MinWalkGap = 0.55f;

        public static Vector2 Size(FurnitureKind k)
        {
            switch (k)
            {
                case FurnitureKind.Bed: return new Vector2(1.0f, 2.0f);
                case FurnitureKind.Wardrobe: return new Vector2(1.1f, 0.55f);
                case FurnitureKind.Table: return new Vector2(1.3f, 0.85f);
                case FurnitureKind.Chair: return new Vector2(0.42f, 0.42f);
                case FurnitureKind.KitchenShelf: return new Vector2(1.4f, 0.45f);
                case FurnitureKind.Rug: return new Vector2(2.0f, 1.4f);
                case FurnitureKind.Stove: return new Vector2(0.6f, 0.6f);
                case FurnitureKind.Sedir: return new Vector2(1.9f, 0.6f);
                default: return new Vector2(0.7f, 0.5f);
            }
        }

        public static float Height(FurnitureKind k)
        {
            switch (k)
            {
                case FurnitureKind.Bed: return 0.5f;
                case FurnitureKind.Wardrobe: return 1.9f;
                case FurnitureKind.Table: return 0.75f;
                case FurnitureKind.Chair: return 0.9f;
                case FurnitureKind.KitchenShelf: return 1.6f;
                case FurnitureKind.Rug: return 0.015f;
                case FurnitureKind.Stove: return 0.85f;
                case FurnitureKind.Sedir: return 0.45f;
                default: return 0.5f;
            }
        }

        /// <summary>Oda türü: küçük odalar depo, mutfak/yatak/oturma sırayla; üst kat yatak ağırlıklı.</summary>
        public static RoomKind Classify(float width, float depth, int roomIndex, int level)
        {
            var area = width * depth;
            if (area < 7f)
                return RoomKind.Storage;
            if (level > 0)
                return roomIndex % 2 == 0 ? RoomKind.Bedroom : RoomKind.LivingRoom;
            switch (roomIndex % 3)
            {
                case 0: return RoomKind.LivingRoom;
                case 1: return RoomKind.Kitchen;
                default: return RoomKind.Bedroom;
            }
        }

        public static FurnitureKind[] PlanKinds(RoomKind k)
        {
            switch (k)
            {
                case RoomKind.Bedroom: return new[] { FurnitureKind.Bed, FurnitureKind.Wardrobe, FurnitureKind.Rug, FurnitureKind.Crate };
                case RoomKind.Kitchen: return new[] { FurnitureKind.Stove, FurnitureKind.KitchenShelf, FurnitureKind.Table, FurnitureKind.Chair, FurnitureKind.Chair };
                case RoomKind.LivingRoom: return new[] { FurnitureKind.Rug, FurnitureKind.Sedir, FurnitureKind.Stove, FurnitureKind.Table, FurnitureKind.Chair, FurnitureKind.Chair, FurnitureKind.Wardrobe };
                default: return new[] { FurnitureKind.Crate, FurnitureKind.Crate, FurnitureKind.KitchenShelf };
            }
        }

        /// <summary>Odayı mobilyalar. doorZones = kapı/merdiven önleri (bina yerel XZ). Dönen sayı eklenen parça.</summary>
        public static int Plan(Rect room, RoomKind kind, System.Random rng, IList<Rect> doorZones, List<FurniturePlacement> output)
        {
            var start = output.Count;
            if (room.width < 1.8f || room.height < 1.8f)
                return 0;
            var kinds = PlanKinds(kind);
            Rect tableRect = default;
            var haveTable = false;
            for (var i = 0; i < kinds.Length; i++)
            {
                var k = kinds[i];
                var fp = default(FurniturePlacement);
                bool ok;
                if (k == FurnitureKind.Rug)
                    ok = TryRug(room, rng, doorZones, out fp);
                else if (k == FurnitureKind.Table)
                {
                    ok = room.width >= 3f && room.height >= 2.6f && TryCentral(room, k, rng, output, start, doorZones, out fp);
                    if (ok)
                    {
                        tableRect = fp.Footprint;
                        haveTable = true;
                    }
                }
                else if (k == FurnitureKind.Chair)
                    ok = haveTable && TryChair(tableRect, rng, room, output, start, doorZones, out fp);
                else
                    ok = TryWall(room, k, rng, output, start, doorZones, out fp);
                if (ok)
                    output.Add(fp);
            }

            return output.Count - start;
        }

        private static bool TryRug(Rect room, System.Random rng, IList<Rect> doors, out FurniturePlacement fp)
        {
            var s = Size(FurnitureKind.Rug);
            var w = Mathf.Min(s.x, room.width * 0.55f);
            var d = Mathf.Min(s.y, room.height * 0.5f);
            fp = default;
            if (w < 1f || d < 0.7f)
                return false;
            var r = new Rect(room.center.x - w * 0.5f, room.center.y - d * 0.5f, w, d);
            fp = new FurniturePlacement { Kind = FurnitureKind.Rug, Footprint = r, Yaw = 0f, Blocks = false };
            return true;
        }

        private static bool TryCentral(Rect room, FurnitureKind k, System.Random rng, List<FurniturePlacement> list, int from, IList<Rect> doors, out FurniturePlacement fp)
        {
            var s = Size(k);
            fp = default;
            for (var a = 0; a < 12; a++)
            {
                var jx = ((float)rng.NextDouble() - 0.5f) * Mathf.Max(0f, room.width - s.x - 2f) * 0.5f;
                var jz = ((float)rng.NextDouble() - 0.5f) * Mathf.Max(0f, room.height - s.y - 2f) * 0.5f;
                var r = Centered(room.center.x + jx, room.center.y + jz, s.x, s.y);
                if (Fits(r, room, list, from, doors, 0.8f))
                {
                    fp = new FurniturePlacement { Kind = k, Footprint = r, Yaw = rng.NextDouble() < 0.5 ? 0f : 90f, Blocks = true };
                    if (fp.Yaw == 90f)
                    {
                        r = Centered(r.center.x, r.center.y, s.y, s.x);
                        if (!Fits(r, room, list, from, doors, 0.8f))
                            continue;
                        fp.Footprint = r;
                    }

                    return true;
                }
            }

            return false;
        }

        private static bool TryChair(Rect table, System.Random rng, Rect room, List<FurniturePlacement> list, int from, IList<Rect> doors, out FurniturePlacement fp)
        {
            var s = Size(FurnitureKind.Chair);
            fp = default;
            // Masanın 4 kenarından birine (uzun kenarlar önce)
            var order = rng.Next(4);
            for (var i = 0; i < 4; i++)
            {
                var side = (order + i) % 4;
                Vector2 c;
                float yaw;
                switch (side)
                {
                    case 0: c = new Vector2(table.center.x, table.yMin - s.y * 0.5f - 0.05f); yaw = 0f; break; // masanın -Z'si, masaya bakar (+Z)
                    case 1: c = new Vector2(table.center.x, table.yMax + s.y * 0.5f + 0.05f); yaw = 180f; break;
                    case 2: c = new Vector2(table.xMin - s.x * 0.5f - 0.05f, table.center.y); yaw = 90f; break;
                    default: c = new Vector2(table.xMax + s.x * 0.5f + 0.05f, table.center.y); yaw = 270f; break;
                }

                var r = Centered(c.x, c.y, s.x, s.y);
                if (!Fits(r, room, list, from, doors, 0.1f))
                    continue;
                fp = new FurniturePlacement { Kind = FurnitureKind.Chair, Footprint = r, Yaw = yaw, Blocks = true };
                return true;
            }

            return false;
        }

        private static bool TryWall(Rect room, FurnitureKind k, System.Random rng, List<FurniturePlacement> list, int from, IList<Rect> doors, out FurniturePlacement fp)
        {
            var s = Size(k);
            fp = default;
            var wall0 = rng.Next(4);
            for (var wi = 0; wi < 4; wi++)
            {
                var wall = (wall0 + wi) % 4;
                var alongX = wall == 0 || wall == 2; // 0: zMin, 2: zMax kuzey/güney
                var runLen = alongX ? room.width : room.height;
                var w = s.x;
                var d = s.y;
                if (runLen < w + 0.4f)
                    continue;
                for (var a = 0; a < 8; a++)
                {
                    var u = Mathf.Lerp(w * 0.5f + 0.15f, runLen - w * 0.5f - 0.15f, (float)rng.NextDouble());
                    Rect r;
                    float yaw;
                    switch (wall)
                    {
                        case 0: r = Centered(room.xMin + u, room.yMin + WallGap + d * 0.5f, w, d); yaw = 0f; break;
                        case 2: r = Centered(room.xMin + u, room.yMax - WallGap - d * 0.5f, w, d); yaw = 180f; break;
                        case 1: r = Centered(room.xMax - WallGap - d * 0.5f, room.yMin + u, d, w); yaw = 270f; break;
                        default: r = Centered(room.xMin + WallGap + d * 0.5f, room.yMin + u, d, w); yaw = 90f; break;
                    }

                    if (!Fits(r, room, list, from, doors, MinWalkGap * 0.5f))
                        continue;
                    fp = new FurniturePlacement { Kind = k, Footprint = r, Yaw = yaw, Blocks = true };
                    return true;
                }
            }

            return false;
        }

        public static Rect Centered(float cx, float cz, float w, float d) => new Rect(cx - w * 0.5f, cz - d * 0.5f, w, d);

        /// <summary>Oda içinde, kapı önü dışında, diğer engelleyicilerle (boşluk dahil) çakışmıyor mu.</summary>
        public static bool Fits(Rect r, Rect room, List<FurniturePlacement> list, int from, IList<Rect> doors, float gap)
        {
            if (r.xMin < room.xMin || r.xMax > room.xMax || r.yMin < room.yMin || r.yMax > room.yMax)
                return false;
            if (doors != null)
            {
                for (var i = 0; i < doors.Count; i++)
                {
                    if (Overlap(r, Grow(doors[i], DoorClearance)))
                        return false;
                }
            }

            for (var i = from; i < list.Count; i++)
            {
                if (!list[i].Blocks)
                    continue;
                if (Overlap(r, Grow(list[i].Footprint, gap)))
                    return false;
            }

            return true;
        }

        private static Rect Grow(Rect r, float g) => new Rect(r.xMin - g, r.yMin - g, r.width + 2f * g, r.height + 2f * g);

        private static bool Overlap(Rect a, Rect b) => a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;

        // ------------------------------------------------------------------ Işık huzmesi matematiği

        /// <summary>Güneş (ışık yönü, aşağı bakan) pencereden içeri giriyor mu: içeri yön ile ışığın yatay bileşeni uyumlu ve yeterince eğik.</summary>
        public static bool SunThroughWindow(Vector3 sunLightDir, Vector3 inward)
        {
            if (sunLightDir.y > -0.15f)
                return false;
            var flat = new Vector3(sunLightDir.x, 0f, sunLightDir.z);
            var inw = new Vector3(inward.x, 0f, inward.z);
            if (flat.sqrMagnitude < 1e-4f || inw.sqrMagnitude < 1e-4f)
                return false;
            return Vector3.Dot(flat.normalized, inw.normalized) > 0.25f;
        }

        /// <summary>Verilen noktadan ışık yönünde zemin düzlemine (y=floorY) izdüşüm. Işık yukarı bakıyorsa false.</summary>
        public static bool ProjectToFloor(Vector3 p, Vector3 lightDir, float floorY, out Vector3 hit)
        {
            hit = p;
            if (lightDir.y > -1e-3f)
                return false;
            var t = (floorY - p.y) / lightDir.y;
            if (t < 0f)
                return false;
            hit = p + lightDir * t;
            return true;
        }
    }
}
