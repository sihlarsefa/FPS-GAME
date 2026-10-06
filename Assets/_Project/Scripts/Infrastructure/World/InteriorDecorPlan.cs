using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.World
{
    public enum DecorKind { Frame, Mirror, WallRug, Cobweb }

    /// <summary>Duvar süsü kaydı. Wall: 0 zMin, 1 xMax, 2 zMax, 3 xMin (InteriorLayout yaw'ları ile aynı). U duvar boyunca konum, Y yükseklik.</summary>
    public struct DecorItem
    {
        public DecorKind Kind;
        public int Wall;
        public float U;
        public float Y;
        public float Width;
        public float Height;
        public bool NearEnd;
    }

    /// <summary>Saf mantık: oda süsü yerleşimi (çerçeve, ayna, duvar halısı, örümcek ağı) + çıplak ampul gece kuralı + üçgen bütçesi.</summary>
    public static class InteriorDecorPlan
    {
        /// <summary>Oda başına üçgen tavanı (ev başına ~4 oda = +2k).</summary>
        public const int RoomTriBudget = 480;
        public const float NightSunY = -0.1f;
        public const float BulbRange = 16f;

        public static int TriCost(DecorKind k)
        {
            switch (k)
            {
                case DecorKind.Frame: return 36;
                case DecorKind.Mirror: return 36;
                case DecorKind.WallRug: return 48;
                default: return 4;
            }
        }

        /// <summary>Güneş ışık yönü Y bileşeni ufka yakın/yukarı ise gece; ampul oyuncuya yakınsa yanar.</summary>
        public static bool BulbOn(float sunForwardY, float distToViewer) => sunForwardY > NightSunY && distToViewer < BulbRange;

        public static Rect WallFootprint(Rect room, int wall, float u, float w)
        {
            const float t = 0.2f;
            switch (wall)
            {
                case 0: return InteriorLayout.Centered(room.xMin + u, room.yMin + t * 0.5f, w, t);
                case 2: return InteriorLayout.Centered(room.xMin + u, room.yMax - t * 0.5f, w, t);
                case 1: return InteriorLayout.Centered(room.xMax - t * 0.5f, room.yMin + u, t, w);
                default: return InteriorLayout.Centered(room.xMin + t * 0.5f, room.yMin + u, t, w);
            }
        }

        public static int Plan(Rect room, RoomKind kind, float ceilH, System.Random rng, IList<Rect> doors, List<FurniturePlacement> placed, List<DecorItem> output)
        {
            var start = output.Count;
            if (room.width < 1.8f || room.height < 1.8f || ceilH < 2.2f)
                return 0;
            var budget = RoomTriBudget;
            int hangings;
            switch (kind)
            {
                case RoomKind.LivingRoom: hangings = 3; break;
                case RoomKind.Bedroom: hangings = 2; break;
                case RoomKind.Kitchen: hangings = 1; break;
                default: hangings = 0; break;
            }

            var order = new[] { DecorKind.WallRug, DecorKind.Mirror, DecorKind.Frame };
            var placedN = 0;
            for (var oi = 0; oi < order.Length && placedN < hangings; oi++)
            {
                var dk = order[oi];
                if (kind != RoomKind.LivingRoom && dk == DecorKind.WallRug)
                    continue;
                if (kind == RoomKind.Kitchen && dk == DecorKind.Mirror)
                    continue;
                if (TryHang(room, dk, ceilH, rng, doors, placed, output, ref budget))
                    placedN++;
            }

            if (kind == RoomKind.LivingRoom && placedN < hangings)
                TryHang(room, DecorKind.Frame, ceilH, rng, doors, placed, output, ref budget);

            // Köşe örümcek ağı: 1-2 köşe (her köşe iki duvar kartı)
            var webs = rng.Next(1, 3);
            var c0 = rng.Next(4);
            for (var i = 0; i < webs && budget >= TriCost(DecorKind.Cobweb) * 2; i++)
            {
                var corner = (c0 + i * 2 + rng.Next(2)) % 4;
                output.Add(new DecorItem { Kind = DecorKind.Cobweb, Wall = corner, NearEnd = false, Width = 0.55f, Height = 0.55f, Y = ceilH });
                output.Add(new DecorItem { Kind = DecorKind.Cobweb, Wall = corner, NearEnd = true, Width = 0.55f, Height = 0.55f, Y = ceilH });
                budget -= TriCost(DecorKind.Cobweb) * 2;
            }

            return output.Count - start;
        }

        private static bool TryHang(Rect room, DecorKind dk, float ceilH, System.Random rng, IList<Rect> doors, List<FurniturePlacement> placed,
            List<DecorItem> output, ref int budget)
        {
            if (budget < TriCost(dk))
                return false;
            float w, h, y;
            switch (dk)
            {
                case DecorKind.WallRug: w = 1.3f; h = 0.85f; y = 1.45f; break;
                case DecorKind.Mirror: w = 0.5f; h = 0.75f; y = 1.5f; break;
                default: w = 0.55f; h = 0.42f; y = 1.6f; break;
            }

            if (y + h * 0.5f > ceilH - 0.15f)
                return false;
            var wall0 = rng.Next(4);
            for (var wi = 0; wi < 4; wi++)
            {
                var wall = (wall0 + wi) % 4;
                var run = wall == 0 || wall == 2 ? room.width : room.height;
                if (run < w + 0.8f)
                    continue;
                for (var a = 0; a < 6; a++)
                {
                    var u = Mathf.Lerp(w * 0.5f + 0.35f, run - w * 0.5f - 0.35f, (float)rng.NextDouble());
                    var fp = WallFootprint(room, wall, u, w);
                    if (Hits(fp, doors, placed, output, room))
                        continue;
                    output.Add(new DecorItem { Kind = dk, Wall = wall, U = u, Y = y, Width = w, Height = h });
                    budget -= TriCost(dk);
                    return true;
                }
            }

            return false;
        }

        // Kapı önü, yüksek mobilya (dolap, raf, boru) veya önceki süs ile çakışma
        private static bool Hits(Rect fp, IList<Rect> doors, List<FurniturePlacement> placed, List<DecorItem> items, Rect room)
        {
            if (doors != null)
            {
                for (var i = 0; i < doors.Count; i++)
                {
                    var d = doors[i];
                    var g = InteriorLayout.DoorClearance * 0.5f;
                    if (fp.xMin < d.xMax + g && fp.xMax > d.xMin - g && fp.yMin < d.yMax + g && fp.yMax > d.yMin - g)
                        return true;
                }
            }

            for (var i = 0; i < placed.Count; i++)
            {
                if (!placed[i].Blocks || InteriorLayout.Height(placed[i].Kind) < 0.8f && placed[i].Kind != FurnitureKind.Stove)
                    continue;
                var f = placed[i].Footprint;
                if (fp.xMin < f.xMax && fp.xMax > f.xMin && fp.yMin < f.yMax && fp.yMax > f.yMin)
                    return true;
            }

            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Kind == DecorKind.Cobweb)
                    continue;
                var o = WallFootprint(room, items[i].Wall, items[i].U, items[i].Width + 0.3f);
                if (fp.xMin < o.xMax && fp.xMax > o.xMin && fp.yMin < o.yMax && fp.yMax > o.yMin)
                    return true;
            }

            return false;
        }
    }
}
