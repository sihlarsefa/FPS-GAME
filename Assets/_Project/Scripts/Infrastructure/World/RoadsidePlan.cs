using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>Yol boyu örnek: konum (y = yol profili), birim yön (xz) ve baştan mesafe.</summary>
    public struct RoadSample
    {
        public Vector3 Pos;
        public Vector2 Dir;
        public float S;
    }

    public struct RoadsidePost
    {
        public Vector3 Pos;
        public float Yaw;
        public int Km;
    }

    public struct GuardRun
    {
        public Vector3 Start;
        public Vector3 End;
    }

    /// <summary>
    /// Yol kenarı detaylarının saf planlaması (UnityEngine yalnızca vektör için): Catmull-Rom (centripetal) pürüzsüz eksen,
    /// sabit aralıkla yeniden örnekleme, direk / km taşı / korkuluk yerleşimi, tel sarkması. Deterministik, RNG kullanmaz.
    /// </summary>
    public static class RoadsidePlan
    {
        public const float MainRoadWidth = 7f;
        public const float VillageRoadWidth = 4.5f;
        public const float PoleSpacing = 38f;
        public const float KmSpacing = 500f;
        public const float GuardSegment = 4f;
        public const float GuardMinDrop = 2.2f;
        public const int MaxPoles = 140;
        public const int MaxKmStones = 14;
        public const int MaxGuardRuns = 48;

        /// <summary>Asfalt = ana yol (elektrik direkli), toprak = köy yolu.</summary>
        public static bool IsMainRoad(RoadKind kind) => kind == RoadKind.Asphalt;

        /// <summary>Hiyerarşi genişliği: layout genişliği geçerliyse o, değilse tür varsayılanı.</summary>
        public static float WidthFor(RoadKind kind, float specWidth)
            => specWidth > 0.5f ? specWidth : (IsMainRoad(kind) ? MainRoadWidth : VillageRoadWidth);

        // ------------------------------------------------------------------ Eğri

        /// <summary>
        /// Noktalardan geçen centripetal Catmull-Rom eğrisi, ~step aralıkla. İlk/son nokta aynen korunur
        /// (kavşak/köprü sabitleri bozulmaz). Aşım (overshoot) centripetal parametreyle sınırlıdır.
        /// </summary>
        public static List<Vector2> Smooth(IList<Vector2> pts, float step)
        {
            var result = new List<Vector2>();
            if (pts == null || pts.Count == 0)
                return result;
            if (pts.Count < 3 || step <= 0.01f)
            {
                for (var i = 0; i + 1 < pts.Count; i++)
                {
                    var a = pts[i];
                    var b = pts[i + 1];
                    var n = step <= 0.01f ? 1 : Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / step));
                    for (var s = 0; s < n; s++)
                        result.Add(Vector2.Lerp(a, b, s / (float)n));
                }

                result.Add(pts[pts.Count - 1]);
                return result;
            }

            for (var i = 0; i + 1 < pts.Count; i++)
            {
                var p1 = pts[i];
                var p2 = pts[i + 1];
                var p0 = i > 0 ? pts[i - 1] : 2f * p1 - p2;
                var p3 = i + 2 < pts.Count ? pts[i + 2] : 2f * p2 - p1;
                var n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(p1, p2) / step));
                for (var s = 0; s < n; s++)
                    result.Add(CentripetalPoint(p0, p1, p2, p3, s / (float)n));
            }

            result.Add(pts[pts.Count - 1]);
            return result;
        }

        /// <summary>P1→P2 arasında u∈[0,1] için centripetal Catmull-Rom noktası (Barry–Goldman).</summary>
        public static Vector2 CentripetalPoint(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float u)
        {
            const float eps = 1e-3f;
            var t0 = 0f;
            var t1 = t0 + Mathf.Max(eps, Mathf.Sqrt(Vector2.Distance(p0, p1)));
            var t2 = t1 + Mathf.Max(eps, Mathf.Sqrt(Vector2.Distance(p1, p2)));
            var t3 = t2 + Mathf.Max(eps, Mathf.Sqrt(Vector2.Distance(p2, p3)));
            var t = Mathf.Lerp(t1, t2, Mathf.Clamp01(u));
            var a1 = Vector2.LerpUnclamped(p0, p1, (t - t0) / (t1 - t0));
            var a2 = Vector2.LerpUnclamped(p1, p2, (t - t1) / (t2 - t1));
            var a3 = Vector2.LerpUnclamped(p2, p3, (t - t2) / (t3 - t2));
            var b1 = Vector2.LerpUnclamped(a1, a2, (t - t0) / (t2 - t0));
            var b2 = Vector2.LerpUnclamped(a2, a3, (t - t1) / (t3 - t1));
            return Vector2.LerpUnclamped(b1, b2, (t - t1) / (t2 - t1));
        }

        /// <summary>Profili (xz yol ekseni, y yükseklik) sabit mesafede yeniden örnekler; ilk örnek S=0.</summary>
        public static List<RoadSample> Resample(IList<Vector3> profile, float spacing)
        {
            var list = new List<RoadSample>();
            if (profile == null || profile.Count < 2 || spacing <= 0.1f)
                return list;

            var total = 0f;
            var segLen = new float[profile.Count - 1];
            for (var i = 0; i + 1 < profile.Count; i++)
            {
                var d = new Vector2(profile[i + 1].x - profile[i].x, profile[i + 1].z - profile[i].z).magnitude;
                segLen[i] = d;
                total += d;
            }

            if (total < 0.1f)
                return list;

            var seg = 0;
            var segStart = 0f;
            for (var s = 0f; s <= total + 0.001f; s += spacing)
            {
                while (seg < segLen.Length - 1 && s > segStart + segLen[seg])
                {
                    segStart += segLen[seg];
                    seg++;
                }

                var a = profile[seg];
                var b = profile[seg + 1];
                var t = segLen[seg] > 1e-4f ? Mathf.Clamp01((s - segStart) / segLen[seg]) : 0f;
                var dir = new Vector2(b.x - a.x, b.z - a.z);
                dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.up;
                list.Add(new RoadSample { Pos = Vector3.Lerp(a, b, t), Dir = dir, S = s });
            }

            return list;
        }

        /// <summary>Yönün sağ normali (xz).</summary>
        public static Vector2 Right(Vector2 dir) => new Vector2(dir.y, -dir.x);

        /// <summary>Yola dik kayma ile konum (xz), y korunur.</summary>
        public static Vector3 Offset(RoadSample s, float lateral)
        {
            var r = Right(s.Dir) * lateral;
            return new Vector3(s.Pos.x + r.x, s.Pos.y, s.Pos.z + r.y);
        }

        public static float YawOf(Vector2 dir) => Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

        // ------------------------------------------------------------------ Yerleşim

        /// <summary>
        /// Ana yol elektrik direkleri: yolun tek yanında (kenardan +2.6 m), PoleSpacing aralıkla. blocked(x,z) true ise atlanır
        /// (köprü, su, kavşak, yapı). Bütçe maxCount ile sınırlı; aşılırsa aralık otomatik büyütülür.
        /// </summary>
        public static List<RoadsidePost> PlanPoles(IList<RoadSample> samples, float roadWidth, float side,
            int maxCount, Func<float, float, bool> blocked)
        {
            var list = new List<RoadsidePost>();
            if (samples == null || samples.Count < 2 || maxCount <= 0)
                return list;

            var length = samples[samples.Count - 1].S;
            var spacing = PoleSpacing;
            if (length / spacing > maxCount)
                spacing = length / maxCount;

            var lateral = side * (roadWidth * 0.5f + 2.6f);
            var next = spacing * 0.5f;
            for (var i = 0; i < samples.Count && list.Count < maxCount; i++)
            {
                if (samples[i].S < next)
                    continue;
                next += spacing;
                var p = Offset(samples[i], lateral);
                if (blocked != null && blocked(p.x, p.z))
                    continue;
                list.Add(new RoadsidePost { Pos = p, Yaw = YawOf(samples[i].Dir) });
            }

            return list;
        }

        /// <summary>Km taşları: her KmSpacing metrede, yolun sağında (kenardan +1.4 m).</summary>
        public static List<RoadsidePost> PlanKmStones(IList<RoadSample> samples, float roadWidth, int maxCount,
            Func<float, float, bool> blocked)
        {
            var list = new List<RoadsidePost>();
            if (samples == null || samples.Count < 2)
                return list;

            var lateral = roadWidth * 0.5f + 1.4f;
            var km = 1;
            var next = KmSpacing;
            for (var i = 0; i < samples.Count && list.Count < maxCount; i++)
            {
                if (samples[i].S < next)
                    continue;
                next += KmSpacing;
                var p = Offset(samples[i], lateral);
                if (blocked != null && blocked(p.x, p.z))
                {
                    km++;
                    continue;
                }

                list.Add(new RoadsidePost { Pos = p, Yaw = YawOf(samples[i].Dir) + 90f, Km = km++ });
            }

            return list;
        }

        /// <summary>
        /// Korkuluk: yolun herhangi bir yanında arazi yol seviyesinden GuardMinDrop kadar düşüyorsa (uçurum kenarı) o yan boyunca
        /// ardışık örnekler tek koşuya birleştirilir. height(x,z) arazi yüksekliğidir.
        /// </summary>
        public static List<GuardRun> PlanGuardrails(IList<RoadSample> samples, float roadWidth, int maxRuns,
            Func<float, float, float> height, Func<float, float, bool> blocked)
        {
            var runs = new List<GuardRun>();
            if (samples == null || samples.Count < 2 || height == null)
                return runs;

            var lateralEdge = roadWidth * 0.5f + 0.9f;
            for (var sideIdx = 0; sideIdx < 2 && runs.Count < maxRuns; sideIdx++)
            {
                var side = sideIdx == 0 ? 1f : -1f;
                var open = false;
                var start = Vector3.zero;
                var last = Vector3.zero;
                for (var i = 0; i < samples.Count; i++)
                {
                    var s = samples[i];
                    var edge = Offset(s, side * lateralEdge);
                    var outer = Offset(s, side * (lateralEdge + 3.5f));
                    var drop = s.Pos.y - height(outer.x, outer.z);
                    var ok = drop >= GuardMinDrop && (blocked == null || !blocked(edge.x, edge.z));
                    if (ok)
                    {
                        if (!open)
                        {
                            open = true;
                            start = edge;
                        }

                        last = edge;
                    }

                    if ((!ok || i == samples.Count - 1) && open)
                    {
                        open = false;
                        if (Vector3.Distance(start, last) >= GuardSegment && runs.Count < maxRuns)
                            runs.Add(new GuardRun { Start = start, End = last });
                    }
                }
            }

            return runs;
        }

        /// <summary>Tel sarkması: t∈[0,1] boyunca aşağı sapma (parabol, uçlarda 0, ortada sag).</summary>
        public static float WireSag(float t, float sag) => 4f * sag * t * (1f - t);

        /// <summary>İki direk arası açıklığa göre sarkma (m): kısa açıklıkta az.</summary>
        public static float SagFor(float span) => Mathf.Clamp(span * 0.035f, 0.1f, 1.6f);
    }
}
