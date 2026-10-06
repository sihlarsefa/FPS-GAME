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
        /// <summary>Tabela türü (RoadsideSignSet indeksi) veya enkaz varyantı.</summary>
        public int Kind;
    }

    public struct GuardRun
    {
        public Vector3 Start;
        public Vector3 End;
        /// <summary>Koşu boyunca ardışık kenar noktaları (eğimi izlemek için); boşsa Start→End düz.</summary>
        public List<Vector3> Points;
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
        public const float SignSpacing = 260f;
        public const int MaxSigns = 24;
        public const float WreckSpacing = 700f;
        public const int MaxWrecks = 6;

        /// <summary>Kalite kademesi (0..3) için Max* ölçeği: Düşük %40, Orta %65, Yüksek %85, Ultra %100.</summary>
        public static float TierScale(int tier)
        {
            switch (Mathf.Clamp(tier, 0, 3)) { case 0: return 0.40f; case 1: return 0.65f; case 2: return 0.85f; default: return 1f; }
        }

        private static int Scaled(int max, int tier) => Mathf.Max(1, Mathf.RoundToInt(max * TierScale(tier)));

        public static int PolesFor(int tier) => Scaled(MaxPoles, tier);
        public static int KmStonesFor(int tier) => Scaled(MaxKmStones, tier);
        public static int GuardRunsFor(int tier) => Scaled(MaxGuardRuns, tier);
        public static int SignsFor(int tier) => Scaled(MaxSigns, tier);
        public static int WrecksFor(int tier) => Scaled(MaxWrecks, tier);
        public static int TotalFor(int tier) => PolesFor(tier) + KmStonesFor(tier) + GuardRunsFor(tier) + SignsFor(tier) + WrecksFor(tier);

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
                var pts = new List<Vector3>();
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
                            pts = new List<Vector3>();
                        }

                        pts.Add(edge);
                        last = edge;
                    }

                    if ((!ok || i == samples.Count - 1) && open)
                    {
                        open = false;
                        if (Vector3.Distance(start, last) >= GuardSegment && runs.Count < maxRuns)
                            runs.Add(new GuardRun { Start = start, End = last, Points = pts });
                    }
                }
            }

            return runs;
        }

        /// <summary>Tel sarkması: t∈[0,1] boyunca aşağı sapma (parabol, uçlarda 0, ortada sag).</summary>
        public static float WireSag(float t, float sag) => 4f * sag * t * (1f - t);

        /// <summary>
        /// Gerçek katener (cosh) sarkması: t∈[0,1] boyunca aşağı sapma; uçlarda 0, ortada tam sag. Parametre a,
        /// a*(cosh(span/2a)-1)=sag denkleminden ikiye bölmeyle bulunur. Küçük sarkmada parabole yakınsar.
        /// </summary>
        public static float CatenaryDrop(float t, float span, float sag)
        {
            if (sag <= 1e-4f || span <= 1e-3f)
                return 0f;
            var lo = span * 0.02f;
            var hi = 1e6f;
            for (var i = 0; i < 60; i++)
            {
                var mid = Mathf.Sqrt(lo * hi);
                var v = mid * ((float)Math.Cosh(span / (2f * mid)) - 1f);
                if (v > sag)
                    lo = mid;
                else
                    hi = mid;
            }

            var a = Mathf.Sqrt(lo * hi);
            var x = (Mathf.Clamp01(t) - 0.5f) * span;
            return a * ((float)Math.Cosh(span / (2f * a)) - (float)Math.Cosh(x / a));
        }

        /// <summary>Korkuluk iki noktası arasındaki eğim açısı (derece, yukarı +).</summary>
        public static float PitchDegrees(Vector3 a, Vector3 b)
        {
            var h = new Vector2(b.x - a.x, b.z - a.z).magnitude;
            return Mathf.Atan2(b.y - a.y, Mathf.Max(1e-4f, h)) * Mathf.Rad2Deg;
        }

        /// <summary>Korkuluk koşusunu eğimi izleyen segmentlere böler (Points varsa onları, yoksa Start→End'i maxSeg aralıkla).</summary>
        public static List<Vector3> RunPath(GuardRun run, float maxSeg)
        {
            if (run.Points != null && run.Points.Count >= 2)
                return run.Points;
            var list = new List<Vector3>();
            var len = Vector3.Distance(run.Start, run.End);
            var n = Mathf.Max(1, Mathf.CeilToInt(len / Mathf.Max(0.5f, maxSeg)));
            for (var i = 0; i <= n; i++)
                list.Add(Vector3.Lerp(run.Start, run.End, i / (float)n));
            return list;
        }

        /// <summary>
        /// Trafik/askeri tabelalar: SignSpacing aralıkla, yan değiştirerek (okunabilirlik iki yönden). Tür, kindCount üzerinden
        /// döner; ilk tabela her yolda 0 (bölge adı). Yaz = tabela ön yüzünün normali.
        /// </summary>
        public static List<RoadsidePost> PlanSigns(IList<RoadSample> samples, float roadWidth, int kindCount, int seed,
            int maxCount, Func<float, float, bool> blocked)
        {
            var list = new List<RoadsidePost>();
            if (samples == null || samples.Count < 2 || maxCount <= 0 || kindCount <= 0)
                return list;

            var next = SignSpacing * 0.35f;
            var idx = 0;
            for (var i = 0; i < samples.Count && list.Count < maxCount; i++)
            {
                if (samples[i].S < next)
                    continue;
                next += SignSpacing;
                var side = (idx & 1) == 0 ? 1f : -1f;
                var p = Offset(samples[i], side * (roadWidth * 0.5f + 2.3f));
                var kind = idx == 0 ? 0 : (idx + Mathf.Abs(seed)) % kindCount;
                idx++;
                if (blocked != null && blocked(p.x, p.z))
                    continue;
                var facing = side > 0f ? -samples[i].Dir : samples[i].Dir;
                list.Add(new RoadsidePost { Pos = p, Yaw = YawOf(facing), Kind = kind });
            }

            return list;
        }

        /// <summary>
        /// Yanmış araç kasaları: WreckSpacing aralıkla, ilki yolun ~%40'ında; yoldan 3.5 m dışta, deterministik eğik duruş.
        /// Kısa yollarda (&lt;300 m) yok.
        /// </summary>
        public static List<RoadsidePost> PlanWrecks(IList<RoadSample> samples, float roadWidth, int maxCount,
            Func<float, float, bool> blocked)
        {
            var list = new List<RoadsidePost>();
            if (samples == null || samples.Count < 2 || maxCount <= 0)
                return list;
            var length = samples[samples.Count - 1].S;
            if (length < 300f)
                return list;

            var next = length * 0.4f;
            var idx = 0;
            for (var i = 0; i < samples.Count && list.Count < maxCount; i++)
            {
                if (samples[i].S < next)
                    continue;
                next += WreckSpacing;
                var side = (idx & 1) == 0 ? -1f : 1f;
                var p = Offset(samples[i], side * (roadWidth * 0.5f + 3.5f));
                var yaw = YawOf(samples[i].Dir) + 25f + 17f * idx;
                idx++;
                if (blocked != null && blocked(p.x, p.z))
                    continue;
                list.Add(new RoadsidePost { Pos = p, Yaw = yaw, Kind = idx });
            }

            return list;
        }

        /// <summary>İki direk arası açıklığa göre sarkma (m): kısa açıklıkta az.</summary>
        public static float SagFor(float span) => Mathf.Clamp(span * 0.035f, 0.1f, 1.6f);
    }
}
