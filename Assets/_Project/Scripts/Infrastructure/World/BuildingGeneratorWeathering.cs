using System;
using System.Collections.Generic;
using UnityEngine;
using M = Project.Infrastructure.Rendering.MaterialId;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// BuildingGenerator'ın yıpranma / köy detayı katmanı (Anadolu köy evi gerçekçiliği). Tüm parçalar görsel (çarpıştırıcısız) olduğundan
    /// NavMesh, Destructible camlar ve gövde çarpıştırıcıları aynen kalır. Kendi rastgele akışını kullanır: bina yerleşimi/ganimet değişmez.
    /// Kademe: <see cref="BuildingWeathering.Tier"/>; matematik: <see cref="BuildingWeatheringMath"/>.
    /// </summary>
    public static partial class BuildingGenerator
    {
        // Katman derinlikleri (duvar yüzeyinden dışarı, m): z-çakışmasını önlemek için ayrı ön yüzler.
        private const float DepthDamp = 0.005f;      // ön yüz 0.010
        private const float DepthStreak = 0.0075f;   // ön yüz 0.0125
        private const float DepthRim = 0.011f;       // ön yüz 0.017
        private const float DepthReveal = 0.016f;    // ön yüz 0.026

        private static readonly List<WeatherRect> WAvoid = new List<WeatherRect>(24);
        private static readonly List<WeatherRect> WPatch = new List<WeatherRect>(8);
        private static readonly List<WeatherRect> WPiece = new List<WeatherRect>(4);
        private static readonly List<WeatherRect> WTmp = new List<WeatherRect>(24);
        private static readonly List<Vector2> WSpans = new List<Vector2>(6);
        private static readonly M[] LaundryColors = { M.White, M.Red, M.Blue, M.Yellow, M.Orange, M.Green, M.TentCanvas, M.White };

        private static void ApplyWeathering(Ctx c, BoxPlan p)
        {
            if (c.Ruined)
                return;
            var prof = BuildingWeatheringMath.ProfileFor(c.Spec.Style, BuildingWeathering.Tier);
            if (prof.IsNone)
                return;
            var rng = new System.Random(BuildingWeatheringMath.SeedFor(c.Spec.Seed, (int)c.Spec.Style) + c.WeatherPass * 7717);
            c.WeatherPass++;
            try
            {
                WeatherWalls(c, p, prof, rng);
                WeatherOpenings(c, p, prof, rng);
                WeatherRoof(c, p, prof, rng);
                WeatherChimneyAndAntenna(c, p, prof, rng);
                WeatherLaundry(c, p, prof, rng);
                WeatherInterior(c, p, prof, rng);
                WeatherPowerDrop(c, p, prof, rng);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BuildingGenerator] Yıpranma katmanı atlandı (" + c.Spec.Style + "): " + e.Message);
            }
        }

        // ---------------------------------------------------------------------------------------------------
        //  Duvar: dökülmüş sıva yamaları, taban nemi, yağmur izleri
        // ---------------------------------------------------------------------------------------------------

        private static void WeatherWalls(Ctx c, BoxPlan p, WeatheringProfile prof, System.Random rng)
        {
            var cls = BuildingWeatheringMath.Classify(p.D.WallMat);
            var coat = cls == WeatherWallClass.Plaster || cls == WeatherWallClass.Concrete;
            if (!coat)
                return;
            var yMin = Y0 + 0.1f;
            var yMax = p.WallTop - 0.2f;
            if (yMax - yMin < 0.8f)
                return;
            var half = p.T * 0.5f;
            var dampMat = cls == WeatherWallClass.Plaster ? M.Mud : M.ConcreteDark;
            for (var s = 0; s < 4; s++)
            {
                var f = p.F[s];
                WAvoid.Clear();
                for (var i = 0; i < f.Openings.Count; i++)
                {
                    var o = f.Openings[i];
                    WAvoid.Add(new WeatherRect(o.Start - 0.05f, o.End + 0.05f, Yb + o.Bottom - 0.05f, Yb + o.Top + 0.05f));
                }

                // Dökülmüş sıva: alttan taş/tuğla görünür
                if (cls == WeatherWallClass.Plaster && prof.PlasterDamage > 0f)
                {
                    var n = BuildingWeatheringMath.PatchCount(f.Length * (yMax - yMin), prof.PlasterDamage);
                    BuildingWeatheringMath.PlanPatches(rng, f.Length, yMin, yMax, WAvoid, n, WPatch);
                    for (var k = 0; k < WPatch.Count; k++)
                    {
                        BuildingWeatheringMath.JaggedPieces(rng, WPatch[k], WPiece);
                        var reveal = rng.NextDouble() < 0.5 ? M.Brick : (rng.NextDouble() < 0.5 ? M.Stone : M.StoneDark);
                        for (var q = 0; q < WPiece.Count; q++)
                        {
                            var r = WPiece[q];
                            FBox(c, f, r.CenterU, r.CenterY, half + DepthRim, r.Width + 0.08f, r.Height + 0.08f, 0.012f, M.Concrete);
                            FBox(c, f, r.CenterU, r.CenterY, half + DepthReveal, r.Width, r.Height, 0.02f, reveal);
                        }
                    }
                }

                // Yükselen nem: taban koyulaşması (kapı aralıkları hariç)
                if (prof.Damp > 0f)
                {
                    WSpans.Clear();
                    WSpans.Add(new Vector2(0.12f, f.Length - 0.12f));
                    for (var i = 0; i < f.Openings.Count; i++)
                    {
                        var o = f.Openings[i];
                        if (o.Bottom < 0.4f)
                            BuildingWeatheringMath.SubtractSpan(WSpans, o.Start - 0.05f, o.End + 0.05f);
                    }

                    for (var sp = 0; sp < WSpans.Count; sp++)
                    {
                        WTmp.Clear();
                        BuildingWeatheringMath.DampSegments(rng, WSpans[sp].x, WSpans[sp].y, 0.12f + 0.1f * prof.Damp, 0.3f + 0.4f * prof.Damp, WTmp);
                        for (var k = 0; k < WTmp.Count; k++)
                        {
                            var r = WTmp[k];
                            FBox(c, f, r.CenterU, Y0 + r.Y1 * 0.5f, half + DepthDamp, r.Width, r.Y1, 0.01f, dampMat);
                        }
                    }
                }

                // Pencere altı ve saçak altı yağmur izleri
                if (prof.Streaks > 0f)
                {
                    WTmp.Clear();
                    for (var i = 0; i < f.Openings.Count; i++)
                    {
                        if (f.Meta[i].Kind != OpeningKind.Window)
                            continue;
                        var o = f.Openings[i];
                        if (rng.NextDouble() > 0.45 + 0.4 * prof.Streaks)
                            continue;
                        var sill = Yb + o.Bottom;
                        BuildingWeatheringMath.RainStreaks(rng, o.Start, o.End, sill, Mathf.Max(Y0 + 0.3f, sill - 1.8f), WTmp);
                    }

                    var edges = Mathf.RoundToInt(f.Length / 3.5f * prof.Streaks);
                    BuildingWeatheringMath.EdgeStreaks(rng, f.Length, p.WallTop - 0.02f, Y0 + 0.6f, 1.6f, WAvoid, edges, WTmp);
                    for (var k = 0; k < WTmp.Count; k++)
                    {
                        var r = WTmp[k];
                        FBox(c, f, r.CenterU, r.CenterY, half + DepthStreak, r.Width, r.Height, 0.01f, M.ConcreteDark);
                    }
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------
        //  Pencere (ahşap çerçeve + denizlik + iç karanlık) ve kapı (söve, basamak, aralık kanat)
        // ---------------------------------------------------------------------------------------------------

        private static void WeatherOpenings(Ctx c, BoxPlan p, WeatheringProfile prof, System.Random rng)
        {
            if (prof.Windows <= 0f && prof.Doors <= 0f)
                return;
            var t = p.T;
            for (var s = 0; s < 4; s++)
            {
                var f = p.F[s];
                for (var i = 0; i < f.Openings.Count; i++)
                {
                    var o = f.Openings[i];
                    var m = f.Meta[i];
                    var y0 = Yb + o.Bottom;
                    if (m.Kind == OpeningKind.Window && prof.Windows > 0f && o.Width > 0.4f && o.Height > 0.4f)
                    {
                        // Orta denizlik + dikme (cam arkası, gömük) ve çıkıntılı taş denizlik
                        FBox(c, f, o.Center, y0 + o.Height * 0.5f, -t * 0.1f, 0.035f, o.Height - 0.12f, 0.04f, M.WoodDark);
                        FBox(c, f, o.Center, y0 + o.Height * 0.58f, -t * 0.1f, o.Width - 0.12f, 0.035f, 0.04f, M.WoodDark);
                        FBox(c, f, o.Center, y0 - 0.035f, t * 0.5f + 0.07f, o.Width + 0.22f, 0.06f, 0.16f, M.Stone);
                        // İç karanlık: kapalı iç panjur ya da yarım perde (iki taraftan da okunur)
                        var r = rng.NextDouble();
                        if (r < 0.18)
                            FBox(c, f, o.Center, y0 + o.Height * 0.5f, -t * 0.5f + 0.05f, o.Width - 0.1f, o.Height - 0.08f, 0.03f, M.WoodDark);
                        else if (r < 0.5)
                            FBox(c, f, o.Center, y0 + o.Height * 0.725f - 0.04f, -t * 0.5f + 0.05f, o.Width * 0.85f, o.Height * 0.55f, 0.02f,
                                rng.NextDouble() < 0.5 ? M.TentCanvas : M.Carpet);
                    }
                    else if (m.Kind == OpeningKind.Door && prof.Doors > 0f && m.Level == 0)
                    {
                        FBox(c, f, o.Center, y0 + o.Height + 0.09f, 0f, o.Width + 0.5f, 0.18f, t + 0.1f, M.WoodDark);
                        FBox(c, f, o.Center, y0 + 0.02f, 0f, o.Width, 0.04f, t + 0.1f, M.Stone);
                        if (p.D.DoorSteps)
                            FBox(c, f, o.Center, -0.07f, t * 0.5f + 0.76f, o.Width + 0.9f, 0.2f, 0.4f, p.D.FoundationMat);
                        if (rng.NextDouble() < 0.35)
                            AjarDoorLeaf(c, p, f, o, y0, rng.NextDouble() < 0.5);
                    }
                }
            }
        }

        private static void AjarDoorLeaf(Ctx c, BoxPlan p, Facade f, WallOpening o, float y0, bool hingeAtStart)
        {
            var dirU = f.AlongX ? Vector3.right : Vector3.forward;
            var sgn = hingeAtStart ? 1f : -1f;
            var hingeU = hingeAtStart ? o.Start + 0.05f : o.End - 0.05f;
            var len = o.Width - 0.1f;
            var h = o.Height - 0.08f;
            var ang = 70f * Mathf.Deg2Rad;
            var leaf = dirU * (sgn * Mathf.Cos(ang)) - f.Outward * Mathf.Sin(ang);
            var hinge = f.Point(hingeU, y0 + 0.04f + h * 0.5f) - f.Outward * (p.T * 0.5f - 0.06f);
            c.B.Box(hinge + leaf * (len * 0.5f), new Vector3(0.045f, h, len), Quaternion.LookRotation(leaf, Vector3.up), M.Wood, StructureCollider.None);
        }

        // ---------------------------------------------------------------------------------------------------
        //  Çatı: ahşap saçak, kiremit sıraları, sırt kiremitleri; düz damda su olukları
        // ---------------------------------------------------------------------------------------------------

        private static void WeatherRoof(Ctx c, BoxPlan p, WeatheringProfile prof, System.Random rng)
        {
            var d = p.D;
            if (!p.HasRoof)
                return;
            if (p.Flat)
            {
                FlatRoofSpout(c, p, prof, rng);
                return;
            }

            if (d.Roof != RoofKind.Gable && d.Roof != RoofKind.Hip)
                return;
            var b = c.B;
            var baseY = p.TopY;
            var pitch = Mathf.Clamp(d.RoofPitch, 5f, 45f) * Mathf.Deg2Rad;
            var tan = Mathf.Tan(pitch);
            var cos = Mathf.Cos(pitch);
            var oh = Mathf.Max(0f, d.Overhang);
            const float th = 0.14f;
            var ridgeX = d.AutoRidge ? p.W >= p.Dp : d.RidgeAlongX;
            var along = ridgeX ? Vector3.right : Vector3.forward;
            var across = ridgeX ? Vector3.forward : Vector3.right;
            var alongLen = ridgeX ? p.W : p.Dp;
            var acrossLen = ridgeX ? p.Dp : p.W;
            var half = acrossLen * 0.5f;
            var hip = d.Roof == RoofKind.Hip;
            var hx = alongLen * 0.5f + oh;
            var hz = half + oh;
            var rl = Mathf.Max(0.05f, hx - hz);
            var ridgeLen = hip ? 2f * rl : alongLen + 2f * Mathf.Min(oh, 0.35f);
            var h = hip ? hz * tan : half * tan;

            // Ahşap kiriş uçları + saçak tahtası
            if (prof.Eaves > 0f && oh >= 0.2f)
            {
                for (var s = -1; s <= 1; s += 2)
                {
                    var dir = across * s;
                    var n = BuildingWeatheringMath.TailCount(alongLen, 0.75f);
                    for (var i = 0; i < n; i++)
                    {
                        var u = n == 1 ? 0f : -alongLen * 0.5f + 0.3f + i * ((alongLen - 0.6f) / (n - 1));
                        var a0 = half - 0.12f;
                        var a1 = half + oh - 0.04f;
                        var y0 = hip ? baseY - 0.05f : baseY + (half - a0) * tan - 0.05f;
                        var y1 = hip ? baseY - 0.05f : baseY + (half - a1) * tan - 0.05f;
                        b.Beam(along * u + dir * a0 + Vector3.up * y0, along * u + dir * a1 + Vector3.up * y1, 0.07f, 0.09f, M.WoodDark);
                    }

                    var tipY = hip ? baseY + 0.02f : baseY - oh * tan + 0.02f;
                    var tipPos = dir * (hip ? hz - 0.02f : half + oh - 0.02f) + Vector3.up * tipY;
                    b.Box(tipPos, ridgeX ? new Vector3(ridgeLen, 0.14f, 0.04f) : new Vector3(0.04f, 0.14f, ridgeLen), M.WoodDark, StructureCollider.None);
                }

                if (hip)
                {
                    for (var e = -1; e <= 1; e += 2)
                    {
                        var dir = along * e;
                        var n = BuildingWeatheringMath.TailCount(acrossLen, 0.75f);
                        for (var i = 0; i < n; i++)
                        {
                            var u = n == 1 ? 0f : -acrossLen * 0.5f + 0.3f + i * ((acrossLen - 0.6f) / (n - 1));
                            b.Beam(across * u + dir * (hx - oh - 0.12f) + Vector3.up * (baseY - 0.05f),
                                across * u + dir * (hx - 0.04f) + Vector3.up * (baseY - 0.05f), 0.07f, 0.09f, M.WoodDark);
                        }
                    }
                }
            }

            // Kiremit sıraları ve sırt kiremitleri
            if (prof.TileDetail > 0f && d.RoofMat == M.RoofTile)
            {
                var slopeLen = (hip ? hz : half + oh) / cos;
                var courses = BuildingWeatheringMath.CourseCount(slopeLen, 0.3f);
                for (var s = -1; s <= 1; s += 2)
                {
                    var dir = across * s;
                    var nrm = dir * Mathf.Sin(pitch) + Vector3.up * cos;
                    var down = dir * cos - Vector3.up * Mathf.Sin(pitch);
                    var rot = Quaternion.LookRotation(down, nrm);
                    for (var i = 0; i < courses; i++)
                    {
                        var sl = 0.3f + i * 0.3f;
                        float a;
                        float y;
                        float len;
                        if (hip)
                        {
                            var e = sl * cos;
                            a = hz - e;
                            y = baseY + e * tan;
                            len = 2f * (BuildingWeatheringMath.HipCourseHalfLength(rl, hx, hz, e) - 0.06f);
                        }
                        else
                        {
                            a = half + oh - sl * cos;
                            y = baseY + (half - a) * tan + th;
                            len = ridgeLen * 0.99f;
                        }

                        if (len < 0.3f)
                            continue;
                        var mat = rng.NextDouble() < 0.12 ? M.Brick : M.RoofTile;
                        b.Box(dir * a + Vector3.up * y + nrm * 0.012f, new Vector3(len, 0.024f, 0.06f), rot, mat, StructureCollider.None);
                    }
                }

                var ridgeY = baseY + h + (hip ? 0.07f : th / cos + 0.09f);
                var tiles = BuildingWeatheringMath.RidgeTileCount(ridgeLen, 0.42f);
                for (var i = 0; i < tiles; i++)
                {
                    var u = -ridgeLen * 0.5f + (i + 0.5f) * (ridgeLen / tiles);
                    var size = ridgeX ? new Vector3(0.38f, 0.09f, 0.34f) : new Vector3(0.34f, 0.09f, 0.38f);
                    b.Box(along * u + Vector3.up * ridgeY, size, i % 5 == 3 ? M.Brick : M.RoofTile, StructureCollider.None);
                }
            }
        }

        private static void FlatRoofSpout(Ctx c, BoxPlan p, WeatheringProfile prof, System.Random rng)
        {
            if (prof.Eaves <= 0f || p.D.Parapet <= 0.01f)
                return;
            var f = p.F[rng.Next(4)];
            var u = 0.8f + (float)rng.NextDouble() * Mathf.Max(0.1f, f.Length - 1.6f);
            for (var i = 0; i < f.Openings.Count; i++)
            {
                var o = f.Openings[i];
                if (u > o.Start - 0.5f && u < o.End + 0.5f)
                    return;
            }

            var half = p.T * 0.5f;
            FBox(c, f, u, p.TopY + 0.08f, half + 0.14f, 0.16f, 0.1f, 0.3f, M.MetalDark);
            FBox(c, f, u, p.TopY - 0.7f, half + DepthStreak, 0.14f, 1.4f, 0.01f, M.ConcreteDark);
        }

        // ---------------------------------------------------------------------------------------------------
        //  Baca külahı ve TV anteni
        // ---------------------------------------------------------------------------------------------------

        private static void WeatherChimneyAndAntenna(Ctx c, BoxPlan p, WeatheringProfile prof, System.Random rng)
        {
            var b = c.B;
            if (prof.Chimney > 0f && c.ChimneyPos.HasValue)
            {
                var cp = c.ChimneyPos.Value;
                b.VerticalCylinder(cp + Vector3.up * 0.02f, 0.14f, 0.3f, 8, M.Brick, StructureCollider.None);
                b.VerticalCylinder(cp + Vector3.up * 0.32f, 0.17f, 0.04f, 8, M.ConcreteDark, StructureCollider.None);
                b.VerticalCylinder(cp + Vector3.up * 0.36f, 0.1f, 0.02f, 8, M.Black, StructureCollider.None);
            }

            if (prof.Antenna <= 0f || !p.HasRoof || rng.NextDouble() > prof.Antenna)
                return;
            Vector3 bp;
            if (p.Flat)
            {
                var found = false;
                bp = default;
                for (var attempt = 0; attempt < 5 && !found; attempt++)
                {
                    var x = Mathf.Lerp(p.Xi0 + 0.5f, p.Xi1 - 0.5f, (float)rng.NextDouble());
                    var z = Mathf.Lerp(p.Zi0 + 0.5f, p.Zi1 - 0.5f, (float)rng.NextDouble());
                    var r = Centered(x, z, 0.8f, 0.8f);
                    if (!c.IsFree(p.Floors, r, 0.2f))
                        continue;
                    c.Block(p.Floors, r);
                    bp = new Vector3(x, p.TopY, z);
                    found = true;
                }

                if (!found)
                    return;
            }
            else
            {
                if (p.D.Roof != RoofKind.Gable && p.D.Roof != RoofKind.Hip)
                    return;
                var ridgeX = p.D.AutoRidge ? p.W >= p.Dp : p.D.RidgeAlongX;
                var alongLen = ridgeX ? p.W : p.Dp;
                var acrossLen = ridgeX ? p.Dp : p.W;
                var ridgeHalf = p.D.Roof == RoofKind.Hip ? Mathf.Max(0.05f, alongLen * 0.5f - acrossLen * 0.5f) : alongLen * 0.5f;
                var u = Mathf.Lerp(-ridgeHalf * 0.7f, ridgeHalf * 0.7f, (float)rng.NextDouble());
                var along = ridgeX ? Vector3.right : Vector3.forward;
                bp = along * u + Vector3.up * (p.TopY + (p.RoofRise > 0f ? p.RoofRise : 0.5f) + 0.12f);
                if (c.ChimneyPos.HasValue)
                {
                    var dx = c.ChimneyPos.Value.x - bp.x;
                    var dz = c.ChimneyPos.Value.z - bp.z;
                    if (dx * dx + dz * dz < 1f)
                        return;
                }
            }

            b.VerticalCylinder(bp, 0.015f, 1.7f, 6, M.MetalDark, StructureCollider.None);
            b.Box(bp + Vector3.up * 1.4f, new Vector3(0.9f, 0.012f, 0.012f), M.Gray, StructureCollider.None);
            var lens = new[] { 0.7f, 0.6f, 0.52f, 0.44f };
            for (var i = 0; i < lens.Length; i++)
                b.Box(bp + Vector3.up * 1.4f + Vector3.right * (-0.38f + i * 0.25f), new Vector3(0.012f, 0.012f, lens[i]), M.Gray, StructureCollider.None);
        }

        // ---------------------------------------------------------------------------------------------------
        //  Çamaşır ipi
        // ---------------------------------------------------------------------------------------------------

        private static void WeatherLaundry(Ctx c, BoxPlan p, WeatheringProfile prof, System.Random rng)
        {
            if (prof.Laundry <= 0f || rng.NextDouble() > prof.Laundry)
                return;
            var f = p.F[rng.NextDouble() < 0.5 ? 2 : (rng.NextDouble() < 0.5 ? 3 : 1)];
            if (f.Length < 3f)
                return;
            var u = -1f;
            for (var attempt = 0; attempt < 4 && u < 0f; attempt++)
            {
                var cand = 0.8f + (float)rng.NextDouble() * Mathf.Max(0.1f, f.Length - 1.6f);
                var ok = true;
                for (var i = 0; i < f.Openings.Count && ok; i++)
                {
                    var o = f.Openings[i];
                    if (f.Meta[i].Level == 0 && cand > o.Start - 0.5f && cand < o.End + 0.5f)
                        ok = false;
                }

                if (ok)
                    u = cand;
            }

            if (u < 0f)
                return;
            var y = Y0 + 2.25f;
            var span = 2.5f + (float)rng.NextDouble() * 1.1f;
            var wall = f.Point(u, y) + f.Outward * (p.T * 0.5f + 0.04f);
            var post = wall + f.Outward * span;
            var b = c.B;
            b.Box(wall, new Vector3(0.06f, 0.06f, 0.06f), M.MetalDark, StructureCollider.None);
            b.VerticalCylinder(new Vector3(post.x, -0.1f, post.z), 0.04f, y + 0.2f, 6, M.WoodDark, StructureCollider.None);
            var sag = BuildingWeatheringMath.SagFor(span, 1.2f);
            const int seg = 6;
            var prev = wall;
            for (var i = 1; i <= seg; i++)
            {
                var t = i / (float)seg;
                var pt = Vector3.Lerp(wall, post, t);
                pt.y = BuildingWeatheringMath.SagY(y, y, t, sag);
                b.Beam(prev, pt, 0.012f, 0.012f, M.Gray);
                prev = pt;
            }

            var n = 2 + rng.Next(4);
            var dirH = f.Outward;
            for (var i = 0; i < n; i++)
            {
                var t = 0.18f + 0.66f * (i + (float)rng.NextDouble() * 0.5f) / n;
                var pt = Vector3.Lerp(wall, post, t);
                var ly = BuildingWeatheringMath.SagY(y, y, t, sag);
                var w = 0.35f + (float)rng.NextDouble() * 0.25f;
                var hh = 0.45f + (float)rng.NextDouble() * 0.3f;
                b.Box(new Vector3(pt.x, ly - hh * 0.5f - 0.01f, pt.z), new Vector3(0.012f, hh, w), Quaternion.LookRotation(dirH, Vector3.up),
                    LaundryColors[rng.Next(LaundryColors.Length)], StructureCollider.None);
            }
        }

        // ---------------------------------------------------------------------------------------------------
        //  İç mekan: süpürgelik, tavan kirişleri, döşeme tahta araları
        // ---------------------------------------------------------------------------------------------------

        private static void WeatherInterior(Ctx c, BoxPlan p, WeatheringProfile prof, System.Random rng)
        {
            if (prof.Interior <= 0f)
                return;
            var t = p.T;
            for (var k = 0; k < p.Floors; k++)
            {
                var floorY = p.LevelY(k);
                for (var s = 0; s < 4; s++)
                {
                    var f = p.F[s];
                    WSpans.Clear();
                    WSpans.Add(new Vector2(t * 0.5f + 0.05f, f.Length - t * 0.5f - 0.05f));
                    for (var i = 0; i < f.Openings.Count; i++)
                    {
                        var o = f.Openings[i];
                        var oy0 = Yb + o.Bottom;
                        if (oy0 <= floorY + 0.2f && oy0 + o.Height > floorY)
                            BuildingWeatheringMath.SubtractSpan(WSpans, o.Start - 0.02f, o.End + 0.02f);
                    }

                    for (var sp = 0; sp < WSpans.Count; sp++)
                    {
                        var w = WSpans[sp].y - WSpans[sp].x;
                        if (w < 0.2f)
                            continue;
                        FBox(c, f, (WSpans[sp].x + WSpans[sp].y) * 0.5f, floorY + 0.07f, -t * 0.5f + 0.0125f, w, 0.12f, 0.025f, M.WoodDark);
                    }
                }
            }

            if (prof.Interior < 0.9f)
                return;
            // Tavan kirişleri (zemin kat, merdiven çekirdeğinden kaçınır)
            if (p.FH >= 2.6f)
            {
                var yb = p.CeilingY(0) - 0.075f;
                var spanZ = p.Zi1 - p.Zi0;
                var count = Mathf.FloorToInt(spanZ / 1.5f);
                for (var i = 0; i < count; i++)
                {
                    var z = p.Zi0 + (i + 0.5f) * (spanZ / count);
                    var rect = Rect.MinMaxRect(p.Xi0, z - 0.1f, p.Xi1, z + 0.1f);
                    var hit = false;
                    for (var q = 0; q < p.Cores.Count && !hit; q++)
                        hit = Overlaps(p.Cores[q].Footprint, rect);
                    if (hit)
                        continue;
                    c.B.Box(new Vector3((p.Xi0 + p.Xi1) * 0.5f, yb, z), new Vector3(p.Xi1 - p.Xi0 - 0.1f, 0.15f, 0.14f), M.WoodDark, StructureCollider.None);
                }
            }

            // Döşeme tahta araları (zemin kat ahşap döşeme)
            if (p.D.FloorMat == M.Wood)
            {
                var spanX = p.Xi1 - p.Xi0;
                var n = Mathf.FloorToInt(spanX / 0.9f);
                for (var i = 1; i < n; i++)
                {
                    var x = p.Xi0 + i * (spanX / n);
                    c.B.Box(new Vector3(x, Y0 + 0.002f, (p.Zi0 + p.Zi1) * 0.5f), new Vector3(0.012f, 0.004f, p.Zi1 - p.Zi0), M.WoodDark, StructureCollider.None);
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------
        //  Elektrik girişi: saçak altı konsol + yalıtkan; BuildingWeathering.BuildPowerLines bu noktaya kablo çeker
        // ---------------------------------------------------------------------------------------------------

        private static void WeatherPowerDrop(Ctx c, BoxPlan p, WeatheringProfile prof, System.Random rng)
        {
            if (prof.PowerDrop <= 0f || rng.NextDouble() > prof.PowerDrop)
                return;
            var f = p.F[rng.NextDouble() < 0.5 ? 2 : 3];
            var u = -1f;
            foreach (var cand in new[] { 0.45f, f.Length - 0.45f })
            {
                var ok = true;
                for (var i = 0; i < f.Openings.Count && ok; i++)
                    ok = !(cand > f.Openings[i].Start - 0.4f && cand < f.Openings[i].End + 0.4f);
                if (ok)
                {
                    u = cand;
                    break;
                }
            }

            if (u < 0f)
                return;
            var y = p.TopY - 0.35f;
            var wall = f.Point(u, y) + f.Outward * (p.T * 0.5f);
            var tip = wall + f.Outward * 0.3f;
            c.B.Beam(wall, tip, 0.04f, 0.04f, M.MetalDark);
            c.B.VerticalCylinder(tip + Vector3.up * 0.0f, 0.03f, 0.08f, 6, M.Gray, StructureCollider.None);
            c.PowerDrop = tip + Vector3.up * 0.1f;
        }
    }
}
