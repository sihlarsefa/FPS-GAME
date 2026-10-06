using System;
using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.World
{
    public enum DecalSurface
    {
        Ground = 0,  // zemine yukarıdan, arazi normaline hizalı
        Wall = 1,    // duvara yatay; Position = dış nokta, Dir = duvara doğru (XZ), raycast ile oturtulur
        Roof = 2     // Position.y verilen yükseklik; aşağı doğru izdüşüm (baca isi)
    }

    /// <summary>Planlanan tek çıkartma (saf veri).</summary>
    public struct DecalPlacement
    {
        public DecalKind Kind;
        public DecalSurface Surface;
        public int Variant;
        public Vector3 Position;
        /// <summary>Doku v ekseninin dünya yönü (derece, Y etrafında; 0 = +Z). Yalnız Ground/Roof.</summary>
        public float Yaw;
        public Vector2 Dir;
        public Vector2 Size;
        public float Depth;
        /// <summary>Tür içi sıra (0..1): kademe düşünce yüksek sıra olanlar önce kapanır.</summary>
        public float Rank01;
        public float Intensity;
    }

    /// <summary>Kademe başına çıkartma bütçesi (saf tablo).</summary>
    public readonly struct DecalBudget
    {
        public readonly int[] PerKind;
        public readonly int Total;
        public readonly float DrawDistance;
        public readonly float FadeScale;

        public DecalBudget(int[] perKind, float drawDistance, float fadeScale)
        {
            PerKind = perKind;
            var t = 0;
            for (var i = 0; i < perKind.Length; i++)
                t += perKind[i];
            Total = t;
            DrawDistance = drawDistance;
            FadeScale = fadeScale;
        }
    }

    /// <summary>
    /// Kademe tablosu: 0 Düşük (Decal renderer özelliği kapalı → 0), 1 Orta, 2 Yüksek, 3 Ultra. Tür başına taban sayılar Orta içindir;
    /// kademe çarpanı, harita alanı ve ıslaklık ölçekler; toplam üst sınıra kırpılır.
    /// </summary>
    public static class DecalScatterTiers
    {
        // Sıra DecalKind: Puddle, MudSplat, TireTrack, Leaves, Needles, OilStain, WallGrime, SootStreak, Moss, Crack, Poster
        private static readonly int[] Base = { 44, 36, 52, 90, 0, 22, 120, 48, 40, 64, 14 };
        private static readonly float[] TierMul = { 0f, 1f, 2f, 3.2f };
        private static readonly int[] TierCap = { 0, 460, 980, 1800 };
        private static readonly float[] Distance = { 0f, 45f, 70f, 110f };
        private static readonly float[] Fade = { 0f, 0.75f, 0.8f, 0.85f };

        public static int ClampTier(int tier) => Mathf.Clamp(tier, 0, 3);

        public static float AreaScale(float halfSize) => Mathf.Clamp(halfSize / 512f, 0.6f, 1.8f);

        public static DecalBudget Get(int tier, float halfSize, float wet01)
        {
            tier = ClampTier(tier);
            wet01 = Mathf.Clamp01(wet01);
            var mul = TierMul[tier] * AreaScale(halfSize);
            var counts = new int[DecalLibraryMath.KindCount];
            var total = 0f;
            for (var i = 0; i < counts.Length; i++)
            {
                var m = mul;
                if (i == (int)DecalKind.Puddle) m *= 0.35f + 0.65f * wet01;
                else if (i == (int)DecalKind.MudSplat) m *= 0.7f + 0.5f * wet01;
                else if (i == (int)DecalKind.Leaves) m *= 1f - 0.2f * wet01;
                counts[i] = Mathf.RoundToInt(Base[i] * m);
                total += counts[i];
            }

            var cap = TierCap[tier];
            if (total > cap && total > 0f)
            {
                var k = cap / total;
                for (var i = 0; i < counts.Length; i++)
                    counts[i] = Mathf.FloorToInt(counts[i] * k);
            }
            return new DecalBudget(counts, Distance[tier], Fade[tier]);
        }

        /// <summary>Çıkartma çizim mesafesi: büyük çıkartmalar biraz daha uzaktan görünür.</summary>
        public static float DrawDistance(int tier, float sizeMeters)
        {
            return Distance[ClampTier(tier)] * Mathf.Clamp(sizeMeters / 2.5f, 0.8f, 1.6f);
        }

        public static float FadeScale(int tier) => Fade[ClampTier(tier)];

        /// <summary>Toplam bütçenin (yüksek kademede kurulmuş) aşağı kademeye oranı: etkin tutulacak Rank01 sınırı.</summary>
        public static float ActiveFraction(int builtTier, int currentTier, float halfSize, float wet01)
        {
            var built = Get(builtTier, halfSize, wet01).Total;
            if (built <= 0)
                return 0f;
            return Mathf.Clamp01(Get(currentTier, halfSize, wet01).Total / (float)built);
        }
    }

    /// <summary>Planlayıcı girdisi: dünya geometrisi + örnekleyiciler (Unity sahnesine bağımlı değil).</summary>
    public sealed class DecalScatterInput
    {
        public MapLayout Layout;
        public int Seed;
        /// <summary>0..1: yağmur=1, kar≈0.35, açık=0 (su birikintisi/çamur yoğunluğunu etkiler).</summary>
        public float Wet01;
        public Func<float, float, float> Height;
        public IReadOnlyList<Bounds> Buildings = Array.Empty<Bounds>();
        public IReadOnlyList<Vector3> Trees = Array.Empty<Vector3>();
        public IReadOnlyList<Vector3> Chimneys = Array.Empty<Vector3>();
    }

    /// <summary>
    /// GX8: çıkartma yerleşim planı (saf, deterministik: aynı girdi+tohum → aynı liste). Kural özeti:
    /// su birikintisi = yol/köy yakını alçak düz noktalar (yağmurda çok); lastik izi = yol boyunca (toprak yol öncelikli);
    /// yaprak/iğne = ağaç altı (yüksek rakımda iğne); duvar kiri = bina diplerinde; is = baca üstü + pencere altı akıntı;
    /// çatlak = beton (bina çevresi, asfalt, karakol/FOB); yağ = üs/ocak/çiftlik; yosun = dere kıyısı + orman; afiş = bina duvarı.
    /// </summary>
    public static class DecalScatterPlanner
    {
        private const float StructureMargin = 0.4f;

        private struct Seg
        {
            public Vector2 A, B;
            public float Len;
            public float Width;
            public RoadKind Kind;
        }

        private sealed class SegSet
        {
            public readonly List<Seg> Segs = new List<Seg>();
            public float Total;
        }

        public static List<DecalPlacement> Plan(DecalScatterInput input, int tier)
        {
            var result = new List<DecalPlacement>(256);
            if (input == null || input.Layout == null || input.Height == null)
                return result;

            tier = DecalScatterTiers.ClampTier(tier);
            var layout = input.Layout;
            var budget = DecalScatterTiers.Get(tier, layout.HalfSize, input.Wet01);
            if (budget.Total <= 0)
                return result;

            var all = BuildSegs(layout, null);
            var dirt = BuildSegs(layout, r => r.Kind == RoadKind.Dirt);
            var asphalt = BuildSegs(layout, r => r.Kind == RoadKind.Asphalt);

            var puddles = new List<DecalPlacement>();
            PlanPuddles(input, budget.PerKind[(int)DecalKind.Puddle], all, puddles);
            result.AddRange(puddles);
            var mudN = budget.PerKind[(int)DecalKind.MudSplat];
            var trackN = budget.PerKind[(int)DecalKind.TireTrack];
            var oilN = budget.PerKind[(int)DecalKind.OilStain];
            var crackN = budget.PerKind[(int)DecalKind.Crack];
            var mossN = budget.PerKind[(int)DecalKind.Moss];
            var sootN = budget.PerKind[(int)DecalKind.SootStreak];
            var doorN = Share(mudN, 0.25f);
            var sweepN = Share(trackN, 0.10f);
            var dropN = Share(oilN, 0.40f);
            var sillN = Share(crackN, 0.20f);
            var wallMossN = Share(mossN, 0.25f);
            var eaveN = Share(sootN, 0.25f);
            PlanMud(input, mudN - doorN, dirt.Total > 0 ? dirt : all, puddles, result);
            PlanTracks(input, trackN - sweepN, dirt, all, result);
            PlanLeaves(input, budget.PerKind[(int)DecalKind.Leaves], result);
            PlanOil(input, oilN - dropN, asphalt, result);
            PlanWallGrime(input, budget.PerKind[(int)DecalKind.WallGrime], result);
            PlanSoot(input, sootN - eaveN, result);
            PlanMoss(input, mossN - wallMossN, result);
            PlanCracks(input, crackN - sillN, asphalt, result);
            PlanPosters(input, budget.PerKind[(int)DecalKind.Poster], result);
            PlanDoorWear(input, doorN, result);
            PlanSweepMarks(input, sweepN, result);
            PlanRoadDrops(input, dropN, all, result);
            PlanThresholds(input, sillN, result);
            PlanWallMoss(input, wallMossN, result);
            PlanEaveStains(input, eaveN, result);
            return result;
        }

        // ------------------------------------------------------------------------------------------ ortak

        private static System.Random Rng(int seed, DecalKind kind)
        {
            unchecked
            {
                return new System.Random(seed * 7919 + (int)kind * 104729 + 13);
            }
        }

        private static int Share(int count, float f) => Mathf.Min(count, Mathf.RoundToInt(count * f));

        /// <summary>
        /// Köy merkezine doğru artan yoğunluk ağırlığı (0.25..1). Köy yoksa sabit 0.6. Saf; yerleşim kuralı.
        /// </summary>
        public static float VillageDensity(MapLayout layout, Vector2 p)
        {
            var any = false;
            var best = 0f;
            if (layout != null && layout.Locations != null)
            {
                for (var i = 0; i < layout.Locations.Count; i++)
                {
                    var l = layout.Locations[i];
                    if (l == null || l.Kind != LocationKind.Village || l.Radius <= 0.01f)
                        continue;
                    any = true;
                    var t = 1f - Vector2.Distance(p, l.Center) / (l.Radius * 1.3f);
                    t = DecalLibraryMath.Smoothstep(0f, 1f, Mathf.Clamp01(t));
                    if (t > best)
                        best = t;
                }
            }
            return any ? 0.25f + 0.75f * best : 0.6f;
        }

        /// <summary>Köy merkezine ağırlıklı bina seçimi (reddetme örneklemesi, deterministik). -1: bulunamadı.</summary>
        private static int PickBuilding(DecalScatterInput input, System.Random rng)
        {
            var bs = input.Buildings;
            if (bs == null || bs.Count == 0)
                return -1;
            var fallback = -1;
            for (var t = 0; t < 10; t++)
            {
                var i = rng.Next(bs.Count);
                var b = bs[i];
                if (!UsableBuilding(b))
                    continue;
                if (fallback < 0)
                    fallback = i;
                var w = VillageDensity(input.Layout, new Vector2(b.center.x, b.center.z));
                if (rng.NextDouble() < w)
                    return i;
            }
            return fallback;
        }

        private static float R(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        public static Vector3 GroundNormal(Func<float, float, float> h, float x, float z)
        {
            const float e = 1f;
            var hl = h(x - e, z);
            var hr = h(x + e, z);
            var hd = h(x, z - e);
            var hu = h(x, z + e);
            return new Vector3(hl - hr, 2f * e, hd - hu).normalized;
        }

        public static float SlopeDegrees(Func<float, float, float> h, float x, float z)
        {
            var n = GroundNormal(h, x, z);
            return Mathf.Acos(Mathf.Clamp(n.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        private static SegSet BuildSegs(MapLayout layout, Func<RoadSpec, bool> filter)
        {
            var set = new SegSet();
            if (layout.Roads == null)
                return set;
            for (var r = 0; r < layout.Roads.Count; r++)
            {
                var road = layout.Roads[r];
                if (road == null || road.Points == null || (filter != null && !filter(road)))
                    continue;
                for (var i = 0; i + 1 < road.Points.Count; i++)
                {
                    var len = Vector2.Distance(road.Points[i], road.Points[i + 1]);
                    if (len < 0.01f)
                        continue;
                    set.Segs.Add(new Seg { A = road.Points[i], B = road.Points[i + 1], Len = len, Width = road.Width, Kind = road.Kind });
                    set.Total += len;
                }
            }
            return set;
        }

        private static bool PickRoad(SegSet set, System.Random rng, out Vector2 p, out Vector2 dir, out Seg seg)
        {
            p = default;
            dir = Vector2.up;
            seg = default;
            if (set == null || set.Segs.Count == 0 || set.Total <= 0f)
                return false;
            var t = (float)rng.NextDouble() * set.Total;
            for (var i = 0; i < set.Segs.Count; i++)
            {
                var s = set.Segs[i];
                if (t <= s.Len || i == set.Segs.Count - 1)
                {
                    var f = Mathf.Clamp01(t / s.Len);
                    p = Vector2.Lerp(s.A, s.B, f);
                    dir = (s.B - s.A) / s.Len;
                    seg = s;
                    return true;
                }
                t -= s.Len;
            }
            return false;
        }

        private static float DistToPolyline(IReadOnlyList<Vector2> pts, Vector2 p)
        {
            var best = float.PositiveInfinity;
            if (pts == null)
                return best;
            for (var i = 0; i + 1 < pts.Count; i++)
            {
                var a = pts[i];
                var ab = pts[i + 1] - a;
                var l2 = ab.sqrMagnitude;
                var t = l2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2) : 0f;
                var d = Vector2.Distance(p, a + ab * t);
                if (d < best)
                    best = d;
            }
            return best;
        }

        /// <summary>Zemin çıkartması için uygun mu: harita içi, su üstü, dere/göl/köprü/bina dışı.</summary>
        public static bool GroundFree(DecalScatterInput input, float x, float z, float riverMargin, float structureMargin)
        {
            var layout = input.Layout;
            var lim = layout.HalfSize - 8f;
            if (Mathf.Abs(x) > lim || Mathf.Abs(z) > lim)
                return false;
            if (input.Height(x, z) < layout.WaterLevel + 0.25f)
                return false;

            var p = new Vector2(x, z);
            if (layout.Rivers != null)
            {
                for (var i = 0; i < layout.Rivers.Count; i++)
                {
                    var rv = layout.Rivers[i];
                    if (rv != null && DistToPolyline(rv.Points, p) < rv.Width * 0.5f + riverMargin)
                        return false;
                }
            }
            if (layout.Lakes != null)
            {
                for (var i = 0; i < layout.Lakes.Count; i++)
                {
                    var l = layout.Lakes[i];
                    if (l != null && Vector2.Distance(p, l.Center) < l.Radius + riverMargin + 1f)
                        return false;
                }
            }
            if (layout.Bridges != null)
            {
                for (var i = 0; i < layout.Bridges.Count; i++)
                {
                    var b = layout.Bridges[i];
                    if (b != null && Vector2.Distance(p, b.Center) < b.Length * 0.5f + b.Width)
                        return false;
                }
            }

            var bs = input.Buildings;
            for (var i = 0; i < bs.Count; i++)
            {
                var b = bs[i];
                if (x > b.min.x - structureMargin && x < b.max.x + structureMargin && z > b.min.z - structureMargin && z < b.max.z + structureMargin)
                    return false;
            }
            return true;
        }

        private static DecalPlacement Ground(DecalScatterInput input, DecalKind kind, System.Random rng, float x, float z, float yaw, float w, float h, float depth, float intensity)
        {
            return new DecalPlacement
            {
                Kind = kind,
                Surface = DecalSurface.Ground,
                Variant = rng.Next(DecalLibraryMath.VariantCount),
                Position = new Vector3(x, input.Height(x, z), z),
                Yaw = yaw,
                Size = new Vector2(w, h),
                Depth = depth,
                Intensity = intensity
            };
        }

        private static void Rank(List<DecalPlacement> list, int from)
        {
            var n = list.Count - from;
            for (var i = 0; i < n; i++)
            {
                var p = list[from + i];
                p.Rank01 = n <= 1 ? 0f : i / (float)n;
                list[from + i] = p;
            }
        }

        private static bool IsSettlement(LocationKind k)
        {
            return k == LocationKind.Village || k == LocationKind.Karakol || k == LocationKind.ForwardBase
                   || k == LocationKind.Farm || k == LocationKind.Ruins;
        }

        private static bool IsConcreteSite(LocationKind k)
        {
            return k == LocationKind.Karakol || k == LocationKind.ForwardBase || k == LocationKind.Dam || k == LocationKind.RelayHill;
        }

        private static List<LocationSpec> Locations(MapLayout layout, Func<LocationKind, bool> filter)
        {
            var list = new List<LocationSpec>();
            if (layout.Locations == null)
                return list;
            for (var i = 0; i < layout.Locations.Count; i++)
            {
                var l = layout.Locations[i];
                if (l != null && filter(l.Kind))
                    list.Add(l);
            }
            return list;
        }

        private static Vector2 DiskPoint(System.Random rng, Vector2 c, float radius)
        {
            var a = R(rng, 0f, Mathf.PI * 2f);
            var r = radius * Mathf.Sqrt((float)rng.NextDouble());
            return c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }

        // ------------------------------------------------------------------------------------------ türler

        private static void PlanPuddles(DecalScatterInput input, int count, SegSet roads, List<DecalPlacement> output)
        {
            if (count <= 0)
                return;
            var rng = Rng(input.Seed, DecalKind.Puddle);
            var sites = Locations(input.Layout, IsSettlement);
            var from = output.Count;
            var wet = input.Wet01;
            var attempts = count * 40;
            for (var a = 0; a < attempts && output.Count - from < count; a++)
            {
                Vector2 p;
                var nearRoad = roads.Total > 0f && (sites.Count == 0 || rng.NextDouble() < 0.7);
                if (nearRoad)
                {
                    if (!PickRoad(roads, rng, out var rp, out var dir, out var seg))
                        continue;
                    var side = new Vector2(-dir.y, dir.x);
                    p = rp + side * R(rng, -(seg.Width * 0.5f + 2f), seg.Width * 0.5f + 2f);
                }
                else if (sites.Count > 0)
                {
                    var l = sites[rng.Next(sites.Count)];
                    p = DiskPoint(rng, l.Center, l.Radius * 0.9f);
                }
                else
                {
                    continue;
                }

                if (!GroundFree(input, p.x, p.y, 1f, StructureMargin))
                    continue;
                if (SlopeDegrees(input.Height, p.x, p.y) > 5f)
                    continue;

                // Alçak düz nokta: halka ortalaması merkezden yüksek (çukur) ve halka düz.
                var c = input.Height(p.x, p.y);
                var h1 = input.Height(p.x + 3f, p.y);
                var h2 = input.Height(p.x - 3f, p.y);
                var h3 = input.Height(p.x, p.y + 3f);
                var h4 = input.Height(p.x, p.y - 3f);
                var mean = (h1 + h2 + h3 + h4) * 0.25f;
                var span = Mathf.Max(Mathf.Max(h1, h2), Mathf.Max(h3, h4)) - Mathf.Min(Mathf.Min(h1, h2), Mathf.Min(h3, h4));
                if (c > mean + 0.01f || span > 0.6f)
                    continue;

                var size = R(rng, 1.6f, 4.2f) * (1f + 0.5f * wet);
                output.Add(Ground(input, DecalKind.Puddle, rng, p.x, p.y, R(rng, 0f, 360f), size, size * R(rng, 0.7f, 1f), 1f, 0.6f + 0.4f * wet));
            }
            Rank(output, from);
        }

        private static void PlanMud(DecalScatterInput input, int count, SegSet roads, List<DecalPlacement> puddles, List<DecalPlacement> output)
        {
            if (count <= 0)
                return;
            var rng = Rng(input.Seed, DecalKind.MudSplat);
            var from = output.Count;
            var attempts = count * 30;
            for (var a = 0; a < attempts && output.Count - from < count; a++)
            {
                Vector2 p;
                if (puddles.Count > 0 && rng.NextDouble() < 0.4)
                {
                    var pd = puddles[rng.Next(puddles.Count)];
                    p = DiskPoint(rng, new Vector2(pd.Position.x, pd.Position.z), pd.Size.x * 0.9f);
                }
                else if (PickRoad(roads, rng, out var rp, out var dir, out var seg))
                {
                    var side = new Vector2(-dir.y, dir.x);
                    p = rp + side * R(rng, -(seg.Width * 0.5f + 1f), seg.Width * 0.5f + 1f);
                }
                else
                {
                    break;
                }

                if (!GroundFree(input, p.x, p.y, 1f, StructureMargin) || SlopeDegrees(input.Height, p.x, p.y) > 20f)
                    continue;
                var s = R(rng, 1.2f, 3f);
                output.Add(Ground(input, DecalKind.MudSplat, rng, p.x, p.y, R(rng, 0f, 360f), s, s, 1f, R(rng, 0.6f, 1f)));
            }
            Rank(output, from);
        }

        private static void PlanTracks(DecalScatterInput input, int count, SegSet dirt, SegSet all, List<DecalPlacement> output)
        {
            if (count <= 0)
                return;
            var rng = Rng(input.Seed, DecalKind.TireTrack);
            var from = output.Count;
            var attempts = count * 25;
            for (var a = 0; a < attempts && output.Count - from < count; a++)
            {
                var set = dirt.Total > 0f && (all.Total <= 0f || rng.NextDouble() < 0.75) ? dirt : all;
                if (!PickRoad(set, rng, out var rp, out var dir, out var seg))
                    continue;
                var side = new Vector2(-dir.y, dir.x);
                var p = rp + side * R(rng, -seg.Width * 0.22f, seg.Width * 0.22f);
                if (!GroundFree(input, p.x, p.y, 0.5f, 0f) || SlopeDegrees(input.Height, p.x, p.y) > 18f)
                    continue;
                var yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                output.Add(Ground(input, DecalKind.TireTrack, rng, p.x, p.y, yaw, 1.9f, R(rng, 5f, 9f), 1f, seg.Kind == RoadKind.Dirt ? 1f : 0.6f));
            }
            Rank(output, from);
        }

        private static void PlanLeaves(DecalScatterInput input, int count, List<DecalPlacement> output)
        {
            var trees = input.Trees;
            if (count <= 0 || trees == null || trees.Count == 0)
                return;
            var rng = Rng(input.Seed, DecalKind.Leaves);
            var from = output.Count;
            var layout = input.Layout;
            var range = Mathf.Max(10f, layout.MaxHeight - layout.WaterLevel);
            var attempts = count * 12;
            for (var a = 0; a < attempts && output.Count - from < count; a++)
            {
                var t = trees[rng.Next(trees.Count)];
                var p = DiskPoint(rng, new Vector2(t.x, t.z), 2.5f);
                if (!GroundFree(input, p.x, p.y, 1f, StructureMargin) || SlopeDegrees(input.Height, p.x, p.y) > 35f)
                    continue;
                var e = (input.Height(p.x, p.y) - layout.WaterLevel) / range;
                var needles = rng.NextDouble() < DecalLibraryMath.Smoothstep(0.25f, 0.6f, e);
                var s = R(rng, 3f, 5f);
                output.Add(Ground(input, needles ? DecalKind.Needles : DecalKind.Leaves, rng, p.x, p.y, R(rng, 0f, 360f), s, s, 1f, R(rng, 0.7f, 1f)));
            }
            Rank(output, from);
        }

        private static void PlanOil(DecalScatterInput input, int count, SegSet asphalt, List<DecalPlacement> output)
        {
            if (count <= 0)
                return;
            var rng = Rng(input.Seed, DecalKind.OilStain);
            var sites = Locations(input.Layout, k => k == LocationKind.ForwardBase || k == LocationKind.Quarry || k == LocationKind.Karakol
                                                      || k == LocationKind.Farm || k == LocationKind.Dam);
            var from = output.Count;
            var attempts = count * 30;
            for (var a = 0; a < attempts && output.Count - from < count; a++)
            {
                Vector2 p;
                if (sites.Count > 0 && (asphalt.Total <= 0f || rng.NextDouble() < 0.7))
                {
                    var l = sites[rng.Next(sites.Count)];
                    p = DiskPoint(rng, l.Center, l.Radius * 0.7f);
                }
                else if (PickRoad(asphalt, rng, out var rp, out var dir, out var seg))
                {
                    p = rp + new Vector2(-dir.y, dir.x) * R(rng, -seg.Width * 0.4f, seg.Width * 0.4f);
                }
                else
                {
                    break;
                }

                if (!GroundFree(input, p.x, p.y, 1f, StructureMargin) || SlopeDegrees(input.Height, p.x, p.y) > 8f)
                    continue;
                var s = R(rng, 0.8f, 2.2f);
                output.Add(Ground(input, DecalKind.OilStain, rng, p.x, p.y, R(rng, 0f, 360f), s, s, 1f, R(rng, 0.6f, 1f)));
            }
            Rank(output, from);
        }

        private static bool UsableBuilding(Bounds b)
        {
            return b.size.y >= 1.5f && b.size.x >= 2.5f && b.size.z >= 2.5f && b.size.x <= 90f && b.size.z <= 90f;
        }

        /// <summary>Bina yüzü: 0 -Z, 1 +Z, 2 -X, 3 +X. Dış nokta (offset kadar dışarıda) ve içe bakan yön.</summary>
        private static void Face(Bounds b, int face, float along01, float outward, out Vector2 outer, out Vector2 inward, out float faceLen)
        {
            var c = new Vector2(b.center.x, b.center.z);
            var ext = new Vector2(b.extents.x, b.extents.z);
            switch (face)
            {
                case 0:
                    inward = Vector2.up; faceLen = b.size.x;
                    outer = new Vector2(c.x + (along01 - 0.5f) * b.size.x, c.y - ext.y - outward);
                    break;
                case 1:
                    inward = Vector2.down; faceLen = b.size.x;
                    outer = new Vector2(c.x + (along01 - 0.5f) * b.size.x, c.y + ext.y + outward);
                    break;
                case 2:
                    inward = Vector2.right; faceLen = b.size.z;
                    outer = new Vector2(c.x - ext.x - outward, c.y + (along01 - 0.5f) * b.size.z);
                    break;
                default:
                    inward = Vector2.left; faceLen = b.size.z;
                    outer = new Vector2(c.x + ext.x + outward, c.y + (along01 - 0.5f) * b.size.z);
                    break;
            }
        }

        private static DecalPlacement WallDecal(DecalKind kind, System.Random rng, Bounds b, int face, float along01, float y, float w, float h, float intensity)
        {
            Face(b, face, along01, 1.2f, out var outer, out var inward, out _);
            return new DecalPlacement
            {
                Kind = kind,
                Surface = DecalSurface.Wall,
                Variant = rng.Next(DecalLibraryMath.VariantCount),
                Position = new Vector3(outer.x, y, outer.y),
                Dir = inward,
                Size = new Vector2(w, h),
                Depth = 0.8f,
                Intensity = intensity
            };
        }

        private static void PlanWallGrime(DecalScatterInput input, int count, List<DecalPlacement> output)
        {
            var bs = input.Buildings;
            if (count <= 0 || bs == null || bs.Count == 0)
                return;
            var rng = Rng(input.Seed, DecalKind.WallGrime);
            var from = output.Count;
            var attempts = count * 10;
            for (var a = 0; a < attempts && output.Count - from < count; a++)
            {
                var bi = PickBuilding(input, rng);
                if (bi < 0)
                    continue;
                var b = bs[bi];
                var face = rng.Next(4);
                Face(b, face, 0.5f, 0f, out _, out _, out var len);
                // Rutubet/yosun bandı: yarısı geniş ve alçak (duvar dibi şeridi), yarısı klasik kir.
                var band = rng.NextDouble() < 0.5;
                var w = band ? R(rng, 3.8f, 6f) : R(rng, 2.4f, 3.6f);
                var h = band ? R(rng, 0.8f, 1.1f) : 1.4f;
                var along = len <= w ? 0.5f : R(rng, w * 0.5f / len, 1f - w * 0.5f / len);
                output.Add(WallDecal(DecalKind.WallGrime, rng, b, face, along, b.min.y + (band ? 0.4f : 0.6f), w, h, R(rng, 0.6f, 1f)));
            }
            Rank(output, from);
        }

        private static void PlanSoot(DecalScatterInput input, int count, List<DecalPlacement> output)
        {
            if (count <= 0)
                return;
            var rng = Rng(input.Seed, DecalKind.SootStreak);
            var from = output.Count;

            // Baca isi: çatıya aşağı izdüşüm.
            var chimneys = input.Chimneys;
            for (var i = 0; chimneys != null && i < chimneys.Count && output.Count - from < count; i++)
            {
                var c = chimneys[i];
                var s = R(rng, 2f, 3.2f);
                output.Add(new DecalPlacement
                {
                    Kind = DecalKind.SootStreak,
                    Surface = DecalSurface.Roof,
                    Variant = rng.Next(DecalLibraryMath.VariantCount),
                    Position = c,
                    Yaw = R(rng, 0f, 360f),
                    Size = new Vector2(s, s),
                    Depth = 1.6f,
                    Intensity = R(rng, 0.7f, 1f)
                });
            }

            // Pencere altı akıntısı: bina duvarlarında.
            var bs = input.Buildings;
            var attempts = count * 10;
            for (var a = 0; bs != null && bs.Count > 0 && a < attempts && output.Count - from < count; a++)
            {
                var bi = PickBuilding(input, rng);
                if (bi < 0)
                    continue;
                var b = bs[bi];
                var face = rng.Next(4);
                var y = b.min.y + R(rng, 1.4f, Mathf.Max(1.5f, Mathf.Min(b.size.y - 0.8f, 4.5f)));
                output.Add(WallDecal(DecalKind.SootStreak, rng, b, face, R(rng, 0.15f, 0.85f), y, R(rng, 0.8f, 1.2f), R(rng, 1.4f, 2.2f), R(rng, 0.5f, 1f)));
            }
            Rank(output, from);
        }

        private static void PlanMoss(DecalScatterInput input, int count, List<DecalPlacement> output)
        {
            if (count <= 0)
                return;
            var rng = Rng(input.Seed, DecalKind.Moss);
            var layout = input.Layout;
            var forests = Locations(layout, k => k == LocationKind.Forest || k == LocationKind.Ruins);
            var rivers = layout.Rivers ?? new List<RiverSpec>();
            var from = output.Count;
            var attempts = count * 35;
            for (var a = 0; a < attempts && output.Count - from < count; a++)
            {
                Vector2 p;
                var useRiver = rivers.Count > 0 && (forests.Count == 0 || rng.NextDouble() < 0.6);
                if (useRiver)
                {
                    var rv = rivers[rng.Next(rivers.Count)];
                    if (rv == null || rv.Points == null || rv.Points.Count < 2)
                        continue;
                    var i = rng.Next(rv.Points.Count - 1);
                    var q = Vector2.Lerp(rv.Points[i], rv.Points[i + 1], (float)rng.NextDouble());
                    var d = rv.Points[i + 1] - rv.Points[i];
                    if (d.sqrMagnitude < 1e-6f)
                        continue;
                    d.Normalize();
                    var side = new Vector2(-d.y, d.x) * (rng.NextDouble() < 0.5 ? -1f : 1f);
                    p = q + side * (rv.Width * 0.5f + R(rng, 1.5f, 12f));
                }
                else if (forests.Count > 0)
                {
                    var l = forests[rng.Next(forests.Count)];
                    p = DiskPoint(rng, l.Center, l.Radius);
                }
                else
                {
                    break;
                }

                if (!GroundFree(input, p.x, p.y, 0.5f, StructureMargin) || SlopeDegrees(input.Height, p.x, p.y) > 30f)
                    continue;
                var s = R(rng, 1.5f, 3.5f);
                output.Add(Ground(input, DecalKind.Moss, rng, p.x, p.y, R(rng, 0f, 360f), s, s, 1f, R(rng, 0.6f, 1f)));
            }
            Rank(output, from);
        }

        private static void PlanCracks(DecalScatterInput input, int count, SegSet asphalt, List<DecalPlacement> output)
        {
            if (count <= 0)
                return;
            var rng = Rng(input.Seed, DecalKind.Crack);
            var sites = Locations(input.Layout, IsConcreteSite);
            var bs = input.Buildings;
            var from = output.Count;
            var attempts = count * 30;
            for (var a = 0; a < attempts && output.Count - from < count; a++)
            {
                Vector2 p;
                var roll = rng.NextDouble();
                if (roll < 0.4 && bs != null && bs.Count > 0)
                {
                    var b = bs[rng.Next(bs.Count)];
                    if (!UsableBuilding(b))
                        continue;
                    // Bina çevresinde (zemin betonu), temel dışında 0.8-2 m.
                    var face = rng.Next(4);
                    Face(b, face, (float)rng.NextDouble(), R(rng, 0.8f, 2f), out p, out _, out _);
                }
                else if (roll < 0.75 && asphalt.Total > 0f && PickRoad(asphalt, rng, out var rp, out var dir, out var seg))
                {
                    p = rp + new Vector2(-dir.y, dir.x) * R(rng, -seg.Width * 0.35f, seg.Width * 0.35f);
                }
                else if (sites.Count > 0)
                {
                    var l = sites[rng.Next(sites.Count)];
                    p = DiskPoint(rng, l.Center, l.Radius * 0.5f);
                }
                else
                {
                    continue;
                }

                if (!GroundFree(input, p.x, p.y, 1f, StructureMargin) || SlopeDegrees(input.Height, p.x, p.y) > 10f)
                    continue;
                var s = R(rng, 1.5f, 3.2f);
                output.Add(Ground(input, DecalKind.Crack, rng, p.x, p.y, R(rng, 0f, 360f), s, s, 1f, R(rng, 0.6f, 1f)));
            }
            Rank(output, from);
        }

        private static void PlanPosters(DecalScatterInput input, int count, List<DecalPlacement> output)
        {
            var bs = input.Buildings;
            if (count <= 0 || bs == null || bs.Count == 0)
                return;
            var rng = Rng(input.Seed, DecalKind.Poster);
            var from = output.Count;
            var attempts = count * 10;
            for (var a = 0; a < attempts && output.Count - from < count; a++)
            {
                var bi = PickBuilding(input, rng);
                if (bi < 0)
                    continue;
                var b = bs[bi];
                output.Add(WallDecal(DecalKind.Poster, rng, b, rng.Next(4), R(rng, 0.2f, 0.8f), b.min.y + R(rng, 1.4f, 1.9f), R(rng, 0.5f, 0.7f), R(rng, 0.7f, 1f), 1f));
            }
            Rank(output, from);
        }

        // ------------------------------------------------------------------------------------------ köy zenginliği (G3)

        /// <summary>Bina kapısı önü ground noktası: yüzün ortası, dışarıda <paramref name="outward"/> m.</summary>
        private static Vector2 DoorPoint(Bounds b, int face, float outward, out Vector2 inward)
        {
            Face(b, face, 0.5f, outward, out var outer, out inward, out _);
            return outer;
        }

        /// <summary>Kapı önü aşınma: ayak trafiğinden çıplaklaşmış toprak (MudSplat), köy merkezinde daha çok.</summary>
        private static void PlanDoorWear(DecalScatterInput input, int count, List<DecalPlacement> output)
        {
            PlanAtBuildingFoot(input, DecalKind.MudSplat, count, 1.0f, 1.6f, 1.4f, 2.2f, 0.7f, 1f, output);
        }

        /// <summary>Eşik aşınması: kapı eşiğine bitişik beton/taş çatlak (Crack).</summary>
        private static void PlanThresholds(DecalScatterInput input, int count, List<DecalPlacement> output)
        {
            PlanAtBuildingFoot(input, DecalKind.Crack, count, 0.45f, 0.9f, 0.9f, 1.5f, 0.5f, 0.9f, output);
        }

        /// <summary>Süpürge izi: kapı önünde kısa paralel çizgiler (TireTrack dokusu, küçük).</summary>
        private static void PlanSweepMarks(DecalScatterInput input, int count, List<DecalPlacement> output)
        {
            PlanAtBuildingFoot(input, DecalKind.TireTrack, count, 1.4f, 2.4f, 0.8f, 1.2f, 0.3f, 0.5f, output, true);
        }

        private static void PlanAtBuildingFoot(DecalScatterInput input, DecalKind kind, int count, float outMin, float outMax, float sMin, float sMax,
            float iMin, float iMax, List<DecalPlacement> output, bool sweep = false)
        {
            var bs = input.Buildings;
            if (count <= 0 || bs == null || bs.Count == 0)
                return;
            var rng = Rng(input.Seed, kind);
            // Köy-ekstra türleri ana türle aynı tohumu paylaşmasın.
            rng = new System.Random(rng.Next() ^ 0x5bd1e995 ^ (sweep ? 77 : 0));
            var from = output.Count;
            var attempts = count * 12;
            for (var a = 0; a < attempts && output.Count - from < count; a++)
            {
                var bi = PickBuilding(input, rng);
                if (bi < 0)
                    continue;
                var b = bs[bi];
                var face = rng.Next(4);
                var p = DoorPoint(b, face, R(rng, outMin, outMax), out var inward);
                if (!GroundFree(input, p.x, p.y, 0.5f, 0f) || SlopeDegrees(input.Height, p.x, p.y) > 25f)
                    continue;
                // Aşınma bina yönüne dik uzanır; süpürge izi ise kapıdan dışarı doğru.
                var yaw = Mathf.Atan2(inward.x, inward.y) * Mathf.Rad2Deg + (sweep ? 0f : 90f) + R(rng, -15f, 15f);
                var s = R(rng, sMin, sMax);
                output.Add(Ground(input, kind, rng, p.x, p.y, yaw, s, sweep ? s * 2f : s * R(rng, 0.7f, 1f), 1f, R(rng, iMin, iMax)));
            }
            Rank(output, from);
        }

        /// <summary>Yol kenarı yağ damlaları: yol kenarında (genişliğin ~0,5-0,7'si), köy yakınında yoğun; küçük leke.</summary>
        private static void PlanRoadDrops(DecalScatterInput input, int count, SegSet roads, List<DecalPlacement> output)
        {
            if (count <= 0 || roads.Total <= 0f)
                return;
            var rng = new System.Random(Rng(input.Seed, DecalKind.OilStain).Next() ^ 0x2545f491);
            var from = output.Count;
            var attempts = count * 25;
            for (var a = 0; a < attempts && output.Count - from < count; a++)
            {
                if (!PickRoad(roads, rng, out var rp, out var dir, out var seg))
                    continue;
                if (rng.NextDouble() > VillageDensity(input.Layout, rp))
                    continue;
                var sgn = rng.NextDouble() < 0.5 ? -1f : 1f;
                var p = rp + new Vector2(-dir.y, dir.x) * sgn * seg.Width * R(rng, 0.45f, 0.68f);
                if (!GroundFree(input, p.x, p.y, 0.5f, StructureMargin) || SlopeDegrees(input.Height, p.x, p.y) > 12f)
                    continue;
                var s = R(rng, 0.3f, 0.7f);
                output.Add(Ground(input, DecalKind.OilStain, rng, p.x, p.y, R(rng, 0f, 360f), s, s * R(rng, 0.8f, 1.2f), 1f, R(rng, 0.5f, 0.9f)));
            }
            Rank(output, from);
        }

        /// <summary>Duvar dibi yosun yamaları (Moss, duvara yatay): köy merkezinde yoğun, alçak ve küçük.</summary>
        private static void PlanWallMoss(DecalScatterInput input, int count, List<DecalPlacement> output)
        {
            var bs = input.Buildings;
            if (count <= 0 || bs == null || bs.Count == 0)
                return;
            var rng = new System.Random(Rng(input.Seed, DecalKind.Moss).Next() ^ 0x1b873593);
            var from = output.Count;
            var attempts = count * 10;
            for (var a = 0; a < attempts && output.Count - from < count; a++)
            {
                var bi = PickBuilding(input, rng);
                if (bi < 0)
                    continue;
                var b = bs[bi];
                output.Add(WallDecal(DecalKind.Moss, rng, b, rng.Next(4), R(rng, 0.1f, 0.9f), b.min.y + R(rng, 0.25f, 0.5f), R(rng, 1.0f, 2.0f), R(rng, 0.6f, 1.0f), R(rng, 0.5f, 1f)));
            }
            Rank(output, from);
        }

        /// <summary>Çatı altı kuş lekesi / saçak akıntısı: duvarın üst kenarında küçük, soluk SootStreak.</summary>
        private static void PlanEaveStains(DecalScatterInput input, int count, List<DecalPlacement> output)
        {
            var bs = input.Buildings;
            if (count <= 0 || bs == null || bs.Count == 0)
                return;
            var rng = new System.Random(Rng(input.Seed, DecalKind.SootStreak).Next() ^ 0x68e31da4);
            var from = output.Count;
            var attempts = count * 10;
            for (var a = 0; a < attempts && output.Count - from < count; a++)
            {
                var bi = PickBuilding(input, rng);
                if (bi < 0)
                    continue;
                var b = bs[bi];
                var y = b.max.y - R(rng, 0.45f, 0.8f);
                output.Add(WallDecal(DecalKind.SootStreak, rng, b, rng.Next(4), R(rng, 0.1f, 0.9f), y, R(rng, 0.3f, 0.6f), R(rng, 0.5f, 0.9f), R(rng, 0.35f, 0.7f)));
            }
            Rank(output, from);
        }
    }
}
