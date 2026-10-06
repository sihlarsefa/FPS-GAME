using System.Collections.Generic;
using UnityEngine;
using M = Project.Infrastructure.Rendering.MaterialId;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// BuildingGenerator iç mekan katmanı: TEK sistem. Ev tipi (köy/konut) odalar InteriorFurnisher'a (mobilya + süs + ampul), pencereler
    /// InteriorLightShafts / InteriorDecorBuilder.WindowSill'e bağlanır; savaş yorgunu görünüm (kurşun deliği, is, cam kırığı, enkaz,
    /// yırtık perde, asker eşyası) burada, InteriorWearPlan sayılarıyla ve kalite kademesine göre çizilir. Kendi rastgele akışını kullanır:
    /// bina yerleşimi/ganimet akışı değişmez.
    /// </summary>
    public static partial class BuildingGenerator
    {
        private struct InteriorJob
        {
            public Rect Room;
            public float FloorY, CeilH;
            public RoomKind Kind;
            public int Seed, MaxPieces;
            public float TipChance;
            public List<Rect> Windows;
            public List<Rect> Doors;
        }

        private struct ShaftJob
        {
            public Vector3 Center, Along, Inward, Sun;
            public float Width, Height, FloorY;
        }

        private struct SillJob
        {
            public Vector3 Sill, Along, Inward;
            public float Width;
            public int Seed;
        }

        private static readonly List<FurniturePlacement> PlanBuf = new List<FurniturePlacement>(12);
        private static readonly M[] CurtainColors = { M.TentCanvas, M.Red, M.White, M.TentCanvas, M.Blue };

        private static bool IsDomestic(PropTheme theme) => theme == PropTheme.Village || theme == PropTheme.Residential;

        // =====================================================================================================
        //  Ev tipi odalar: InteriorFurnisher'a devret (engelleri hemen kaydet, çizimi Finish'te yap)
        // =====================================================================================================

        private static void FurnishDomestic(Ctx c, BoxPlan p, int level, Rect room, int roomIndex, float y)
        {
            if (room.width < 1.8f || room.height < 1.8f)
                return;
            var tier = InteriorWearPlan.Tier();
            var rng = new System.Random(InteriorWearPlan.SeedFor(c.Spec.Seed, level, roomIndex) + 11);
            if (c.Ruined && rng.NextDouble() < InteriorWearPlan.RuinedDropChance)
                return;
            var job = new InteriorJob
            {
                Room = room,
                FloorY = y,
                CeilH = p.CeilingY(level) - p.LevelY(level),
                Kind = InteriorLayout.Classify(room.width, room.height, roomIndex, level),
                Seed = InteriorWearPlan.SeedFor(c.Spec.Seed, level, roomIndex),
                MaxPieces = InteriorWearPlan.MaxPiecesPerRoom(tier),
                TipChance = InteriorWearPlan.TipChance(tier, c.Ruined),
                Windows = new List<Rect>(2),
                Doors = new List<Rect>(4)
            };
            if (c.Ruined)
                job.MaxPieces = Mathf.Max(2, job.MaxPieces * 2 / 3);
            OpeningZones(p, level, room, job.Doors, job.Windows);
            InteriorFurnisher.PlanRoom(room, job.Kind, job.Seed, job.Doors, job.MaxPieces, PlanBuf);
            for (var i = 0; i < PlanBuf.Count; i++)
            {
                if (PlanBuf[i].Blocks)
                    c.Block(level, Expand(PlanBuf[i].Footprint, 0.04f));
            }

            PlanBuf.Clear();
            c.Interiors.Add(job);
        }

        /// <summary>Kapı önleri, ara duvar kapıları, merdiven/delik ve pencere önleri (bina yerel XZ) — odayla kesişenler.</summary>
        private static void OpeningZones(BoxPlan p, int level, Rect room, List<Rect> doors, List<Rect> windows)
        {
            for (var s = 0; s < 4; s++)
            {
                var f = p.F[s];
                for (var i = 0; i < f.Openings.Count; i++)
                {
                    var m = f.Meta[i];
                    if (m.Level != level)
                        continue;
                    var o = f.Openings[i];
                    if (m.Kind == OpeningKind.Window)
                    {
                        var z = OpeningRect(f, o, p.T, 0.35f, 0.1f);
                        if (Overlaps(z, room))
                            windows.Add(z);
                    }
                    else if (m.Kind == OpeningKind.Door || m.Kind == OpeningKind.Big)
                    {
                        var z = OpeningRect(f, o, p.T, 1.1f, 0.15f);
                        if (Overlaps(z, room))
                            doors.Add(z);
                    }
                }
            }

            for (var i = 0; i < p.Partitions.Count; i++)
            {
                var pt = p.Partitions[i];
                if (pt.Level != level)
                    continue;
                var z = new Rect(pt.X - 0.6f, pt.DoorZ - 0.75f, 1.2f, 1.5f);
                if (Overlaps(z, room))
                    doors.Add(z);
            }

            var holes = p.HolesAt(level);
            if (holes != null)
            {
                for (var i = 0; i < holes.Count; i++)
                {
                    var z = Expand(holes[i], 0.4f);
                    if (Overlaps(z, room))
                        doors.Add(z);
                }
            }

            for (var i = 0; i < p.Cores.Count; i++)
            {
                var core = p.Cores[i];
                if (core.Level != level && core.Level + 1 != level)
                    continue;
                var z = Expand(core.Footprint, 0.4f);
                if (Overlaps(z, room))
                    doors.Add(z);
            }
        }

        /// <summary>Açıklığın iç yüzünden odaya doğru depth derinliğinde dikdörtgen.</summary>
        private static Rect OpeningRect(Facade f, WallOpening o, float wallT, float depth, float pad)
        {
            var pt = f.Point(o.Center, 0f) - f.Outward * (wallT * 0.5f + depth * 0.5f);
            var w = o.Width + pad * 2f;
            return f.AlongX ? Centered(pt.x, pt.z, w, depth) : Centered(pt.x, pt.z, depth, w);
        }

        private static void BuildInteriorJobs(Ctx c, Transform t)
        {
            for (var i = 0; i < c.Interiors.Count; i++)
            {
                var j = c.Interiors[i];
                try
                {
                    InteriorFurnisher.Furnish(t, j.Room, j.FloorY, j.CeilH, j.Kind, j.Seed, j.Doors, true, j.MaxPieces, j.TipChance, j.Windows);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[BuildingGenerator] İç mekan odası atlandı: " + e.Message);
                }
            }

            for (var i = 0; i < c.Shafts.Count; i++)
            {
                var s = c.Shafts[i];
                InteriorLightShafts.TryBuild(t, s.Center, s.Along, s.Width, s.Height, s.Inward, s.Sun, s.FloorY);
            }

            for (var i = 0; i < c.Sills.Count; i++)
            {
                var s = c.Sills[i];
                InteriorDecorBuilder.WindowSill(t, s.Sill, s.Along, s.Width, s.Inward, s.Seed);
            }
        }

        // =====================================================================================================
        //  Savaş yorgunu katman (tüm binalar; yoğunluk kalite kademesine göre)
        // =====================================================================================================

        private static bool FloorSpotFree(Ctx c, BoxPlan p, int level, float x, float z, float r)
        {
            var rect = Centered(x, z, r * 2f, r * 2f);
            if (!c.IsFree(level, rect))
                return false;
            var holes = p.HolesAt(level);
            if (holes != null)
            {
                for (var i = 0; i < holes.Count; i++)
                {
                    if (Overlaps(holes[i], rect))
                        return false;
                }
            }

            for (var i = 0; i < p.Cores.Count; i++)
            {
                var core = p.Cores[i];
                if ((core.Level == level || core.Level + 1 == level) && Overlaps(core.Footprint, rect))
                    return false;
            }

            return true;
        }

        private static void WarWear(Ctx c, BoxPlan p)
        {
            var tier = InteriorWearPlan.Tier();
            var ruined = c.Ruined;
            var rng = new System.Random(unchecked(c.Spec.Seed * 668265263 + 40503 + c.WeatherPass * 31));
            c.WeatherPass++;
            var domestic = IsDomestic(p.D.Props) || IsDomestic(p.D.PropsUpper);
            var sun = SunLocal(c);
            var shafts = 0;
            var maxShafts = InteriorWearPlan.MaxShafts(tier);

            for (var k = 0; k < p.Floors; k++)
            {
                var floorY = p.LevelY(k);
                // Pencereler: cam kırığı, yırtık perde, is, ışık huzmesi, pervaz saksısı
                for (var s = 0; s < 4; s++)
                {
                    var f = p.F[s];
                    for (var i = 0; i < f.Openings.Count; i++)
                    {
                        var m = f.Meta[i];
                        if (m.Level != k || m.Kind != OpeningKind.Window)
                            continue;
                        WindowWear(c, p, f, f.Openings[i], k, floorY, tier, ruined, domestic, rng, sun, ref shafts, maxShafts);
                    }

                    WallWear(c, p, f, k, floorY, tier, ruined, rng);
                }

                // Odalar: enkaz + asker eşyası
                CollectRooms(p, k, Rooms);
                for (var r = 0; r < Rooms.Count; r++)
                    RoomWear(c, p, k, Rooms[r], floorY, tier, ruined, rng);
            }
        }

        private static Vector3 SunLocal(Ctx c)
        {
            var sunLight = RenderSettings.sun;
            var dir = sunLight != null ? sunLight.transform.forward : new Vector3(0.4f, -0.75f, 0.5f).normalized;
            return Quaternion.Inverse(Quaternion.Euler(0f, c.Spec.Yaw, 0f)) * dir;
        }

        private static void WindowWear(Ctx c, BoxPlan p, Facade f, WallOpening o, int level, float floorY, int tier, bool ruined, bool domestic,
            System.Random rng, Vector3 sun, ref int shafts, int maxShafts)
        {
            var inward = -f.Outward;
            var y0 = Yb + o.Bottom;
            var y1 = y0 + o.Height;
            var along = f.AlongX ? Vector3.right : Vector3.forward;

            // Cam kırıkları: pencere altında zeminde
            var n = InteriorWearPlan.Shards(tier, ruined);
            for (var i = 0; i < n; i++)
            {
                var near = InteriorWearPlan.ShardNearWindow(rng.NextDouble());
                var du = ((float)rng.NextDouble() - 0.5f) * (near ? o.Width * 0.8f : o.Width + 1.2f);
                var dist = p.T * 0.5f + 0.12f + (float)rng.NextDouble() * (near ? 0.45f : 2.0f);
                var pos = f.Point(o.Center + du, floorY + 0.004f) + inward * dist;
                if (!FloorSpotFree(c, p, level, pos.x, pos.z, 0.05f))
                    continue;
                var size = new Vector3(0.04f + (float)rng.NextDouble() * 0.09f, 0.004f, 0.03f + (float)rng.NextDouble() * 0.07f);
                c.B.Box(pos, size, Quaternion.Euler(0f, (float)rng.NextDouble() * 180f, 0f), M.Glass, StructureCollider.None);
            }

            // Pencere üstü is lekesi
            if (rng.NextDouble() < (ruined ? 0.8 : 0.35 + 0.1 * tier))
                FBox(c, f, o.Center + ((float)rng.NextDouble() - 0.5f) * 0.2f, y1 + 0.28f, -p.T * 0.5f + 0.006f, o.Width * 0.7f, 0.45f, 0.012f, M.Black);

            // Yırtık perde
            if (o.Width >= 0.6f && rng.NextDouble() < (domestic ? InteriorWearPlan.CurtainChance(tier) : InteriorWearPlan.CurtainChance(tier) * 0.25f))
            {
                var cm = CurtainColors[rng.Next(CurtainColors.Length)];
                var drop = o.Height * (ruined ? 0.45f : 0.7f);
                FBox(c, f, o.Center, y1 + 0.02f, -p.T * 0.5f + 0.03f, o.Width + 0.2f, 0.03f, 0.03f, M.MetalDark);
                var strips = rng.NextDouble() < 0.35 ? 1 : 2;
                for (var q = 0; q < strips; q++)
                {
                    var h = drop * (0.45f + 0.55f * (float)rng.NextDouble());
                    var w = o.Width * (0.2f + 0.12f * (float)rng.NextDouble());
                    var u = strips == 1 ? o.Center + ((float)rng.NextDouble() - 0.5f) * o.Width * 0.5f : o.Center + (q == 0 ? -1f : 1f) * (o.Width * 0.5f - w * 0.5f - 0.03f);
                    FBox(c, f, u, y1 - h * 0.5f, -p.T * 0.5f + 0.03f, w, h, 0.015f, cm);
                    // yırtık uç: kısa, daha dar parça
                    var th = h * 0.25f;
                    FBox(c, f, u + (q == 0 ? 0.03f : -0.03f), y1 - h - th * 0.5f + 0.02f, -p.T * 0.5f + 0.03f, w * 0.5f, th, 0.012f, cm);
                }
            }

            // Pervaz saksısı (yıkıkta yok)
            if (domestic && !ruined && o.Width >= 0.5f && rng.NextDouble() < 0.5)
            {
                c.Sills.Add(new SillJob
                {
                    Sill = f.Point(o.Center, y0 + 0.05f),
                    Along = along,
                    Inward = inward,
                    Width = o.Width,
                    Seed = InteriorWearPlan.SeedFor(c.Spec.Seed, level, (int)(o.Center * 10f))
                });
            }

            // Işık huzmesi
            if (shafts < maxShafts && o.Width >= 0.6f && o.Height >= 0.6f && InteriorLayout.SunThroughWindow(sun, inward))
            {
                c.Shafts.Add(new ShaftJob
                {
                    Center = f.Point(o.Center, y0 + o.Height * 0.5f),
                    Along = along,
                    Inward = inward,
                    Sun = sun,
                    Width = o.Width,
                    Height = o.Height,
                    FloorY = floorY
                });
                shafts++;
            }
        }

        /// <summary>Duvarın iç yüzünde kurşun delikleri ve is lekeleri (açıklıklardan kaçınır).</summary>
        private static void WallWear(Ctx c, BoxPlan p, Facade f, int level, float floorY, int tier, bool ruined, System.Random rng)
        {
            WSpans.Clear();
            WSpans.Add(new Vector2(p.T * 0.5f + 0.25f, f.Length - p.T * 0.5f - 0.25f));
            for (var i = 0; i < f.Openings.Count; i++)
            {
                var o = f.Openings[i];
                var oy0 = Yb + o.Bottom;
                if (oy0 < floorY + p.FH && oy0 + o.Height > floorY)
                    BuildingWeatheringMath.SubtractSpan(WSpans, o.Start - 0.3f, o.End + 0.3f);
            }

            var total = 0f;
            for (var i = 0; i < WSpans.Count; i++)
                total += Mathf.Max(0f, WSpans[i].y - WSpans[i].x);
            if (total < 0.5f)
                return;
            var holes = InteriorWearPlan.BulletHoles(total, tier, ruined);
            var minY = floorY + 0.5f;
            var maxY = floorY + Mathf.Max(0.8f, p.FH - 0.7f);
            for (var h = 0; h < holes; h++)
            {
                var u = PickSpanU(rng, total);
                var y = minY + (float)rng.NextDouble() * (maxY - minY);
                // Kümelenme: çoğu kez 2-3 delik birbirine yakın (seri atış)
                var burst = rng.NextDouble() < 0.4 ? 2 : 1;
                for (var b = 0; b < burst; b++)
                {
                    var uu = u + b * (InteriorWearPlan.HoleSpacing + (float)rng.NextDouble() * 0.12f);
                    var yy = y + ((float)rng.NextDouble() - 0.5f) * 0.25f;
                    FBox(c, f, uu, yy, -p.T * 0.5f + 0.004f, 0.13f, 0.13f, 0.008f, M.StoneDark);
                    FBox(c, f, uu, yy, -p.T * 0.5f + 0.009f, 0.055f, 0.055f, 0.018f, M.Black);
                }
            }

            // Is lekeleri
            var soot = Mathf.RoundToInt(total * 0.08f * InteriorWearPlan.Density(tier) * (ruined ? 1.6f : 1f));
            for (var i = 0; i < soot; i++)
            {
                var u = PickSpanU(rng, total);
                var w = 0.4f + (float)rng.NextDouble() * 0.7f;
                var hgt = 0.3f + (float)rng.NextDouble() * 0.6f;
                FBox(c, f, u, floorY + 0.3f + (float)rng.NextDouble() * (p.FH - 1.2f), -p.T * 0.5f + 0.0025f, w, hgt, 0.005f, M.Black);
            }
        }

        private static float PickSpanU(System.Random rng, float total)
        {
            var t = (float)rng.NextDouble() * total;
            for (var i = 0; i < WSpans.Count; i++)
            {
                var len = Mathf.Max(0f, WSpans[i].y - WSpans[i].x);
                if (t <= len || i == WSpans.Count - 1)
                    return WSpans[i].x + Mathf.Min(t, len);
                t -= len;
            }

            return WSpans[0].x;
        }

        /// <summary>Oda zemininde enkaz parçaları ve terk edilmiş asker eşyaları (çarpıştırıcısız).</summary>
        private static void RoomWear(Ctx c, BoxPlan p, int level, Rect room, float floorY, int tier, bool ruined, System.Random rng)
        {
            var area = room.width * room.height;
            var cap = InteriorWearPlan.MaxPiecesPerRoom(tier) * 3;
            var debris = Mathf.Min(cap, InteriorWearPlan.Debris(area, tier, ruined));
            var wallMat = p.D.WallMat;
            for (var i = 0; i < debris; i++)
            {
                var pt = InteriorWearPlan.RandomIn(room, rng, 0.35f);
                if (!FloorSpotFree(c, p, level, pt.x, pt.y, 0.2f))
                    continue;
                var s = 0.08f + (float)rng.NextDouble() * 0.22f;
                var mat = rng.NextDouble() < 0.5 ? wallMat : (rng.NextDouble() < 0.5 ? M.StoneDark : M.Dirt);
                var rot = Quaternion.Euler((float)rng.NextDouble() * 18f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 18f);
                c.B.Box(new Vector3(pt.x, floorY + s * 0.4f, pt.y), new Vector3(s * 1.3f, s * 0.8f, s), rot, mat, StructureCollider.None);
            }

            var items = InteriorWearPlan.SoldierItems(area, tier, ruined);
            for (var i = 0; i < items; i++)
            {
                var pt = InteriorWearPlan.RandomIn(room, rng, 0.5f);
                if (!FloorSpotFree(c, p, level, pt.x, pt.y, 0.25f))
                    continue;
                SoldierItem(c, InteriorWearPlan.ItemKind(rng.Next(0, 4)), new Vector3(pt.x, floorY, pt.y), rng);
            }
        }

        private static void SoldierItem(Ctx c, int kind, Vector3 at, System.Random rng)
        {
            var yaw = (float)rng.NextDouble() * 360f;
            var q = Quaternion.Euler(0f, yaw, 0f);
            switch (kind)
            {
                case 0: // boş şarjör
                    c.B.Box(at + new Vector3(0f, 0.02f, 0f), new Vector3(0.04f, 0.04f, 0.16f), q, M.MetalDark, StructureCollider.None);
                    c.B.Box(at + q * new Vector3(0f, 0.022f, 0.02f), new Vector3(0.042f, 0.03f, 0.05f), q, M.VehicleOlive, StructureCollider.None);
                    break;
                case 1: // telsiz
                    c.B.Box(at + new Vector3(0f, 0.05f, 0f), new Vector3(0.1f, 0.1f, 0.22f), q, M.VehicleOlive, StructureCollider.None);
                    c.B.Box(at + q * new Vector3(0.03f, 0.1f, 0.08f), new Vector3(0.012f, 0.014f, 0.3f), q * Quaternion.Euler(0f, 0f, 8f), M.Black, StructureCollider.None);
                    break;
                case 2: // matara
                    c.B.Box(at + new Vector3(0f, 0.045f, 0f), new Vector3(0.13f, 0.09f, 0.2f), q, M.VehicleOlive, StructureCollider.None);
                    c.B.Box(at + q * new Vector3(0f, 0.095f, 0.09f), new Vector3(0.05f, 0.03f, 0.04f), q, M.MetalDark, StructureCollider.None);
                    break;
                default: // kovanlar
                    for (var i = 0; i < 4; i++)
                    {
                        var o = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.35f, 0.006f, ((float)rng.NextDouble() - 0.5f) * 0.35f);
                        c.B.Box(at + o, new Vector3(0.012f, 0.012f, 0.045f), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), M.Yellow, StructureCollider.None);
                    }

                    break;
            }
        }
    }
}
