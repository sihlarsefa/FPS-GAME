using System;
using System.Collections.Generic;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Presentation.Bootstrap
{
    /// <summary>Haritada otomatik ekran görüntüsü için bir kamera bakış noktası.</summary>
    public readonly struct OtoEkranView
    {
        public readonly string Name;
        public readonly Vector3 Position;
        public readonly Vector3 LookAt;

        /// <summary>Neye baktığının kısa açıklaması (günlük için).</summary>
        public readonly string Looks;

        public OtoEkranView(string name, Vector3 position, Vector3 lookAt, string looks = "")
        {
            Name = name;
            Position = position;
            LookAt = lookAt;
            Looks = looks ?? string.Empty;
        }
    }

    /// <summary>
    /// Dünya verisinden (yerleşimler, yollar, su seviyesi, yükseklik örnekleyici) 5 vitrin bakış noktası seçer:
    /// köy merkezi (yerleşim sınırının 25–35 m dışından, +8–12 m yüksekten merkeze), orman kenarı (ağaç hattından 10 m
    /// açıkta, hat boyunca), dağ tepesi (sırttan vadiye), yol (ana yolun 2 m üstünden viraj boyunca), göl (kıyıdan su
    /// üzerinden karşı tepelere). Saf mantık (yükseklik bir delegeyle verilir), EditMode testli.
    /// İsteğe bağlı delegelerle görüntü engel kontrolü yapılır:
    /// <c>viewBlocked</c> bakış yönünde ilk 10 m için 1,5 m yarıçaplı küre taraması (engel → kamera 3 denemeye kadar
    /// her seferinde 4 m yukarı ve geriye kaydırılır), <c>treeNear</c> kamera konumunun 3 m yakınında arazi ağacı
    /// (varsa farklı açı/konum seçilir; ağaç silinmez).
    /// </summary>
    public static class OtoEkranViewpoints
    {
        public const int Count = 5;
        public static readonly string[] Names = { "koy_merkezi", "orman_kenari", "dag_tepesi", "yol", "gol" };

        /// <summary>Köy kamerası yerleşim sınırının bu kadar dışında durur (m, istenen bant 25–35).</summary>
        private const float VillageOutside = 30f;

        /// <summary>Köy kamerası zemin + bu kadar yüksekte (m, istenen bant 8–12).</summary>
        private const float VillageCamHeight = 10f;

        /// <summary>Engelde kamera kaydırma: deneme sayısı ve her denemede yukarı+geri adım (m).</summary>
        private const int NudgeTries = 3;
        private const float NudgeStep = 4f;

        public static List<OtoEkranView> Build(Vector2 mapCenter, float halfSize, float waterLevel,
            IReadOnlyList<NamedLocation> locations, Func<float, float, float> height)
        {
            return Build(mapCenter, halfSize, waterLevel, locations, height, null, null, null);
        }

        public static List<OtoEkranView> Build(Vector2 mapCenter, float halfSize, float waterLevel,
            IReadOnlyList<NamedLocation> locations, Func<float, float, float> height,
            IReadOnlyList<RoadSpec> roads, Func<Vector3, Vector3, bool> viewBlocked, Func<Vector3, bool> treeNear)
        {
            if (height == null)
                height = (x, z) => 0f;
            halfSize = Mathf.Max(50f, halfSize);

            // Ana yerleşim: en büyük "major" bölge (yoksa en büyük bölge, yoksa harita merkezi).
            var village = mapCenter;
            var villageRadius = 30f;
            var villageName = "harita merkezi";
            var best = -1f;
            if (locations != null)
            {
                for (var pass = 0; pass < 2 && best < 0f; pass++)
                    for (var i = 0; i < locations.Count; i++)
                    {
                        var l = locations[i];
                        if (l == null || (pass == 0 && !l.IsMajor))
                            continue;
                        if (l.Radius > best)
                        {
                            best = l.Radius;
                            village = l.Center;
                            villageRadius = Mathf.Max(10f, l.Radius);
                            villageName = string.IsNullOrEmpty(l.Name) ? "yerleşim" : l.Name;
                        }
                    }
            }

            // Izgara taraması: en yüksek nokta, en alçak (su altı) nokta.
            var step = Mathf.Max(8f, halfSize * 2f / 48f);
            var limit = halfSize * 0.9f;
            var high = new Vector2(mapCenter.x, mapCenter.y);
            var highH = float.NegativeInfinity;
            var low = mapCenter;
            var lowH = float.PositiveInfinity;
            for (var x = mapCenter.x - limit; x <= mapCenter.x + limit; x += step)
                for (var z = mapCenter.y - limit; z <= mapCenter.y + limit; z += step)
                {
                    var h = height(x, z);
                    if (h > highH) { highH = h; high = new Vector2(x, z); }
                    if (h < lowH) { lowH = h; low = new Vector2(x, z); }
                }

            var views = new List<OtoEkranView>(Count);
            var vg = height(village.x, village.y);

            // 1) Köy merkezi: yerleşim sınırının 25–35 m dışından, +8–12 m yüksekten, merkeze (binalara) bakış.
            //    Güneybatıdan başlanır; kamera noktasının 3 m yakınında ağaç varsa farklı açı denenir.
            var vLook = new Vector3(village.x, vg + 4f, village.y);
            var vXZ = PickOffsetAngle(village, villageRadius + VillageOutside, 225f, VillageCamHeight,
                mapCenter, halfSize, height, treeNear);
            var vPos = new Vector3(vXZ.x, height(vXZ.x, vXZ.y) + VillageCamHeight, vXZ.y);
            vPos = ClearView(vPos, vLook, viewBlocked);
            views.Add(new OtoEkranView(Names[0], vPos, vLook,
                "yerleşim merkezi (" + villageName + "), binalar karede"));

            // 2) Orman kenarı: ağaç hattından 10 m açıkta (tarlada) durup hat boyunca bakış.
            var forestLoc = FindForestLocation(locations);
            Vector2 fXZ;
            Vector2 fTangent;
            string fDesc;
            if (forestLoc != null)
            {
                var fr = Mathf.Max(20f, forestLoc.Radius);
                var open = mapCenter - forestLoc.Center; // açık alan genelde vadi/merkez yönünde
                var startDeg = open.sqrMagnitude > 1f ? Mathf.Atan2(open.y, open.x) * Mathf.Rad2Deg : 180f;
                fXZ = PickOffsetAngle(forestLoc.Center, fr + 10f, startDeg, 2f, mapCenter, halfSize, height, treeNear);
                var radial = fXZ - forestLoc.Center;
                radial = radial.sqrMagnitude < 1f ? Vector2.right : radial.normalized;
                fTangent = new Vector2(-radial.y, radial.x); // ağaç hattı boyunca
                fDesc = "orman kenarı (" + forestLoc.Name + "), ağaç hattı boyunca";
            }
            else
            {
                fXZ = FindForestEdge(village, halfSize, step, limit, mapCenter, waterLevel, highH, height);
                var toVillage = village - fXZ;
                toVillage = toVillage.sqrMagnitude < 1f ? Vector2.up : toVillage.normalized;
                fTangent = new Vector2(-toVillage.y, toVillage.x);
                fDesc = "orman kenarı (tarama), hat boyunca";
            }

            var fCam = new Vector3(fXZ.x, height(fXZ.x, fXZ.y) + 2f, fXZ.y);
            var fT = fXZ + fTangent * 70f;
            var fLook = new Vector3(fT.x, height(fT.x, fT.y) + 2f, fT.y);
            fCam = ClearView(fCam, fLook, viewBlocked);
            views.Add(new OtoEkranView(Names[1], fCam, fLook, fDesc));

            // 3) Dağ tepesi: en yüksek sırttan vadiye (ana yerleşime doğru) bakış.
            var peak = high;
            if (treeNear != null && treeNear(new Vector3(peak.x, highH + 3f, peak.y)))
                peak = PickOffsetAngle(high, 8f, 0f, 3f, mapCenter, halfSize, height, treeNear);
            var dLook = new Vector3(village.x, vg + 2f, village.y);
            var dPos = new Vector3(peak.x, height(peak.x, peak.y) + 3f, peak.y);
            dPos = ClearView(dPos, dLook, viewBlocked);
            views.Add(new OtoEkranView(Names[2], dPos, dLook, "sırttan vadi boyunca " + villageName + " yönüne"));

            // 4) Yol: ana yolun (en uzun asfalt, yoksa en uzun) üzerinde, 2 m yükseklikten viraj boyunca bakış.
            //    Yol verisi yoksa köy ile ikinci bölge arasındaki hattın orta noktası.
            var road = MainRoad(roads);
            Vector3 rPos;
            Vector3 rLook;
            string rDesc;
            if (road != null)
            {
                RoadCamera(road.Points, 0.4f, 70f, out var rCamXZ, out var rLookXZ);
                if (treeNear != null)
                {
                    var fracs = new[] { 0.4f, 0.48f, 0.56f, 0.32f, 0.64f, 0.24f };
                    for (var i = 0; i < fracs.Length; i++)
                    {
                        RoadCamera(road.Points, fracs[i], 70f, out var cXZ, out var lXZ);
                        if (!treeNear(new Vector3(cXZ.x, height(cXZ.x, cXZ.y) + 2f, cXZ.y)))
                        {
                            rCamXZ = cXZ;
                            rLookXZ = lXZ;
                            break;
                        }
                    }
                }

                rPos = new Vector3(rCamXZ.x, height(rCamXZ.x, rCamXZ.y) + 2f, rCamXZ.y);
                rLook = new Vector3(rLookXZ.x, height(rLookXZ.x, rLookXZ.y) + 1.5f, rLookXZ.y);
                rDesc = "ana yol (" + (string.IsNullOrEmpty(road.Name) ? "yol" : road.Name) + ") virajı boyunca";
            }
            else
            {
                var other = SecondLocation(locations, village, mapCenter, halfSize);
                var mid = Vector2.Lerp(village, other, 0.5f);
                var dir = other - village;
                dir = dir.sqrMagnitude < 1f ? Vector2.right : dir.normalized;
                var from = mid - dir * 14f;
                var to = mid + dir * 70f;
                rPos = new Vector3(from.x, height(from.x, from.y) + 2f, from.y);
                rLook = new Vector3(to.x, height(to.x, to.y) + 1.5f, to.y);
                rDesc = "iki bölge arası hat boyunca";
            }

            rPos = ClearView(rPos, rLook, viewBlocked);
            views.Add(new OtoEkranView(Names[3], rPos, rLook, rDesc));

            // 5) Göl: kıyıdan su üzerinden karşı tepelere bakış. Su yoksa en alçak noktaya doğru.
            var hasWater = lowH < waterLevel - 0.3f;
            var shore = low + new Vector2(-1f, 0f) * 40f;
            if (hasWater)
                shore = FindShore(low, waterLevel, height, treeNear);
            var across = low - shore;
            across = across.sqrMagnitude < 1f ? Vector2.right : across.normalized;
            var far = low + across * 120f; // suyun ötesi: karşı yamaç/tepeler
            var floor = hasWater ? waterLevel : lowH;
            var gPos = new Vector3(shore.x, Mathf.Max(height(shore.x, shore.y), floor) + 2.5f, shore.y);
            var gLook = new Vector3(far.x, Mathf.Max(height(far.x, far.y), floor) + 8f, far.y);
            gPos = ClearView(gPos, gLook, viewBlocked);
            views.Add(new OtoEkranView(Names[4], gPos, gLook, "göl kıyısından su üzerinden karşı tepelere"));

            return views;
        }

        /// <summary>
        /// Bakış yönü engelliyse (küre taraması delegesi true) kamerayı en çok <see cref="NudgeTries"/> kez,
        /// her denemede <see cref="NudgeStep"/> m yukarı ve geriye kaydırır. Delege yoksa konum aynen döner.
        /// </summary>
        private static Vector3 ClearView(Vector3 pos, Vector3 lookAt, Func<Vector3, Vector3, bool> viewBlocked)
        {
            if (viewBlocked == null)
                return pos;

            for (var i = 0; i < NudgeTries && viewBlocked(pos, lookAt); i++)
            {
                var back = pos - lookAt;
                back.y = 0f;
                back = back.sqrMagnitude > 1e-4f ? back.normalized : Vector3.back;
                pos += back * NudgeStep + Vector3.up * NudgeStep;
            }

            return pos;
        }

        /// <summary>
        /// Merkez çevresinde <paramref name="startDeg"/> açısından başlayıp ±30° adımlarla döner; harita içinde kalan
        /// ve kamera noktasının (zemin + <paramref name="camUp"/>) 3 m yakınında ağaç olmayan ilk açıyı seçer.
        /// Hiçbiri temiz değilse harita içindeki ilk aday döner (ağaç silinmez, yalnız konum değişir).
        /// </summary>
        private static Vector2 PickOffsetAngle(Vector2 center, float dist, float startDeg, float camUp,
            Vector2 mapCenter, float halfSize, Func<float, float, float> height, Func<Vector3, bool> treeNear)
        {
            var limit = halfSize * 0.95f;
            var first = center + Dir(startDeg) * dist;
            var hasFirst = false;
            for (var k = 0; k < 12; k++)
            {
                var off = ((k + 1) / 2) * 30f * (k % 2 == 0 ? 1f : -1f);
                var p = center + Dir(startDeg + off) * dist;
                if (Mathf.Abs(p.x - mapCenter.x) > limit || Mathf.Abs(p.y - mapCenter.y) > limit)
                    continue;
                if (!hasFirst)
                {
                    first = p;
                    hasFirst = true;
                }

                if (treeNear == null || !treeNear(new Vector3(p.x, height(p.x, p.y) + camUp, p.y)))
                    return p;
            }

            return first;
        }

        private static Vector2 Dir(float deg)
        {
            var rad = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        /// <summary>Adında orman/çam/koru geçen en büyük bölge (orman kenarı için), yoksa null.</summary>
        private static NamedLocation FindForestLocation(IReadOnlyList<NamedLocation> locations)
        {
            NamedLocation bestLoc = null;
            if (locations == null)
                return null;
            for (var i = 0; i < locations.Count; i++)
            {
                var l = locations[i];
                var n = l != null ? l.Name : null;
                if (string.IsNullOrEmpty(n))
                    continue;
                if (n.IndexOf("orman", StringComparison.OrdinalIgnoreCase) < 0
                    && n.IndexOf("çam", StringComparison.OrdinalIgnoreCase) < 0
                    && n.IndexOf("koru", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (bestLoc == null || l.Radius > bestLoc.Radius)
                    bestLoc = l;
            }

            return bestLoc;
        }

        private static Vector2 FindForestEdge(Vector2 village, float half, float step, float limit, Vector2 center,
            float water, float highH, Func<float, float, float> height)
        {
            var target = half * 0.35f;
            var bestScore = float.PositiveInfinity;
            var bestPoint = village + new Vector2(target, 0f);
            var cap = water + (highH - water) * 0.6f;
            for (var x = center.x - limit; x <= center.x + limit; x += step)
                for (var z = center.y - limit; z <= center.y + limit; z += step)
                {
                    var h = height(x, z);
                    if (h < water + 1.5f || h > cap)
                        continue;
                    var score = Mathf.Abs(Vector2.Distance(new Vector2(x, z), village) - target);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestPoint = new Vector2(x, z);
                    }
                }

            return bestPoint;
        }

        private static Vector2 SecondLocation(IReadOnlyList<NamedLocation> locations, Vector2 village, Vector2 center, float half)
        {
            var best = float.PositiveInfinity;
            var result = village + new Vector2(half * 0.4f, 0f);
            if (locations != null)
                for (var i = 0; i < locations.Count; i++)
                {
                    var l = locations[i];
                    if (l == null)
                        continue;
                    var d = Vector2.Distance(l.Center, village);
                    if (d < 40f || d >= best)
                        continue;
                    best = d;
                    result = l.Center;
                }

            return result;
        }

        /// <summary>En uzun asfalt yol (yoksa en uzun yol); yol verisi yoksa null.</summary>
        private static RoadSpec MainRoad(IReadOnlyList<RoadSpec> roads)
        {
            if (roads == null)
                return null;
            RoadSpec bestRoad = null;
            var bestScore = -1f;
            for (var i = 0; i < roads.Count; i++)
            {
                var r = roads[i];
                if (r == null || r.Points == null || r.Points.Count < 2)
                    continue;
                var score = PolylineLength(r.Points) + (r.Kind == RoadKind.Asphalt ? 100000f : 0f);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestRoad = r;
                }
            }

            return bestRoad;
        }

        private static float PolylineLength(List<Vector2> pts)
        {
            var len = 0f;
            for (var i = 1; i < pts.Count; i++)
                len += Vector2.Distance(pts[i - 1], pts[i]);
            return len;
        }

        /// <summary>
        /// Yol çoklu çizgisinde toplam uzunluğun <paramref name="frac"/> kesrindeki kamera noktası ve oradan yol
        /// boyunca ~<paramref name="ahead"/> m ilerideki bakış noktası (viraj kare içinde kalır).
        /// </summary>
        private static void RoadCamera(List<Vector2> pts, float frac, float ahead, out Vector2 cam, out Vector2 look)
        {
            var total = PolylineLength(pts);
            var target = Mathf.Max(0f, Mathf.Min(total * Mathf.Clamp01(frac), total - ahead));
            var acc = 0f;
            var i0 = 0;
            for (var i = 1; i < pts.Count; i++)
            {
                var seg = Vector2.Distance(pts[i - 1], pts[i]);
                if (acc + seg >= target)
                {
                    i0 = i - 1;
                    break;
                }

                acc += seg;
                i0 = i - 1;
            }

            cam = pts[i0];
            look = pts[pts.Count - 1];
            var d = 0f;
            for (var i = i0 + 1; i < pts.Count; i++)
            {
                d += Vector2.Distance(pts[i - 1], pts[i]);
                if (d >= ahead)
                {
                    look = pts[i];
                    break;
                }
            }
        }

        /// <summary>
        /// Su noktasından dışa doğru ilk kara (kıyı) adayları taranır; kamera noktasının 3 m yakınında ağaç olmayan
        /// ilk aday tercih edilir, hiçbiri temiz değilse ilk kara noktası döner.
        /// </summary>
        private static Vector2 FindShore(Vector2 waterPoint, float water, Func<float, float, float> height,
            Func<Vector3, bool> treeNear)
        {
            var hasFallback = false;
            var fallback = waterPoint + new Vector2(60f, 0f);
            for (var r = 20f; r <= 400f; r += 20f)
                for (var a = 0; a < 8; a++)
                {
                    var ang = a * Mathf.PI * 0.25f;
                    var p = waterPoint + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                    var h = height(p.x, p.y);
                    if (h <= water + 0.5f)
                        continue;
                    if (treeNear == null || !treeNear(new Vector3(p.x, h + 2.5f, p.y)))
                        return p;
                    if (!hasFallback)
                    {
                        hasFallback = true;
                        fallback = p;
                    }
                }

            return fallback;
        }
    }
}
