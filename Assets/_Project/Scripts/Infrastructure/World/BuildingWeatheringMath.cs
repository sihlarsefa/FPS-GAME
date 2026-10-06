using System;
using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>Duvar yüzeyinin hava etkisi sınıfı (hangi ikincil malzemenin görüneceğini belirler).</summary>
    public enum WeatherWallClass { Other = 0, Plaster, Stone, Concrete }

    /// <summary>Cephe düzleminde dikdörtgen (u: duvar boyunca, y: bina yerel yükseklik).</summary>
    public struct WeatherRect
    {
        public float U0, U1, Y0, Y1;

        public WeatherRect(float u0, float u1, float y0, float y1)
        {
            U0 = Mathf.Min(u0, u1);
            U1 = Mathf.Max(u0, u1);
            Y0 = Mathf.Min(y0, y1);
            Y1 = Mathf.Max(y0, y1);
        }

        public float Width => U1 - U0;
        public float Height => Y1 - Y0;
        public float CenterU => (U0 + U1) * 0.5f;
        public float CenterY => (Y0 + Y1) * 0.5f;

        public bool Overlaps(WeatherRect o, float pad = 0f)
            => U0 - pad < o.U1 && o.U0 < U1 + pad && Y0 - pad < o.Y1 && o.Y0 < Y1 + pad;
    }

    /// <summary>Bir bina için hava etkisi / detay miktarları (0 = kapalı, 1 = tam). Kademe ve stile göre <see cref="BuildingWeatheringMath.ProfileFor"/>.</summary>
    public struct WeatheringProfile
    {
        public float PlasterDamage;
        public float Damp;
        public float Streaks;
        public float Eaves;
        public float TileDetail;
        public float Windows;
        public float Doors;
        public float Chimney;
        public float Antenna;
        public float Laundry;
        public float Interior;
        public float PowerDrop;

        public bool IsNone => PlasterDamage <= 0f && Damp <= 0f && Streaks <= 0f && Eaves <= 0f && TileDetail <= 0f && Windows <= 0f
                              && Doors <= 0f && Chimney <= 0f && Antenna <= 0f && Laundry <= 0f && Interior <= 0f && PowerDrop <= 0f;
    }

    /// <summary>Elektrik şebekesi düğümü (bina girişi ya da direk).</summary>
    public struct GridNode
    {
        public Vector3 Position;
        public bool IsPole;
        /// <summary>Bina düğümlerinde giriş listesindeki dizin, direklerde -1.</summary>
        public int Source;
    }

    /// <summary>İki düğüm arasındaki sarkık kablo açıklığı.</summary>
    public struct GridSpan
    {
        public int A, B;
    }

    /// <summary>
    /// Bina yıpranması / köy detayları için SAF matematik (Unity nesnesi üretmez; EditMode testlenebilir).
    /// BuildingGenerator.Weathering ve BuildingWeathering bu sınıfı kullanır.
    /// </summary>
    public static class BuildingWeatheringMath
    {
        // Özellik açılma kademeleri (0 Düşük … 3 Ultra)
        public const int TierDamp = 0, TierStreaks = 0, TierEaves = 0, TierChimney = 0;
        public const int TierDoors = 1, TierWindows = 1, TierPlaster = 1, TierAntenna = 1, TierPower = 1;
        public const int TierTiles = 2, TierLaundry = 2, TierInterior = 2;

        public const float DefaultPoleSpan = 26f;
        public const float DefaultMaxLink = 48f;

        /// <summary>Yüzey malzemesinin sınıfı.</summary>
        public static WeatherWallClass Classify(MaterialId m)
        {
            switch (m)
            {
                case MaterialId.Plaster:
                case MaterialId.PlasterWarm:
                    return WeatherWallClass.Plaster;
                case MaterialId.Stone:
                case MaterialId.StoneDark:
                case MaterialId.Rock:
                case MaterialId.RockDark:
                    return WeatherWallClass.Stone;
                case MaterialId.Concrete:
                case MaterialId.ConcreteDark:
                case MaterialId.Brick:
                    return WeatherWallClass.Concrete;
                default:
                    return WeatherWallClass.Other;
            }
        }

        /// <summary>Aynı spec her zaman aynı yıpranmayı üretir; ana üretici rastgele akışına dokunmaz.</summary>
        public static int SeedFor(int specSeed, int style)
            => unchecked(specSeed * 7919 + (style + 3) * 104729 ^ 0x5EED1);

        /// <summary>Stil tabanlı yıpranma profili, kademeye göre kısılmış (düşük kademede yalnız ucuz katmanlar).</summary>
        public static WeatheringProfile ProfileFor(BuildingStyle style, int tier)
        {
            tier = Mathf.Clamp(tier, 0, 3);
            WeatheringProfile p;
            switch (style)
            {
                case BuildingStyle.VillageHouse:
                    p = new WeatheringProfile
                    {
                        PlasterDamage = 1f, Damp = 1f, Streaks = 1f, Eaves = 1f, TileDetail = 1f, Windows = 1f, Doors = 1f, Chimney = 1f,
                        Antenna = 0.35f, Laundry = 0.45f, Interior = 1f, PowerDrop = 0.8f
                    };
                    break;
                case BuildingStyle.TwoStoryHouse:
                    p = new WeatheringProfile
                    {
                        PlasterDamage = 0.9f, Damp = 1f, Streaks = 1f, Eaves = 1f, TileDetail = 1f, Windows = 1f, Doors = 1f, Chimney = 1f,
                        Antenna = 0.55f, Laundry = 0.3f, Interior = 1f, PowerDrop = 0.9f
                    };
                    break;
                case BuildingStyle.Shop:
                    p = new WeatheringProfile
                    {
                        PlasterDamage = 0.7f, Damp = 0.9f, Streaks = 0.8f, Eaves = 0.6f, TileDetail = 0.6f, Windows = 1f, Doors = 1f, Chimney = 0.6f,
                        Antenna = 0.4f, Laundry = 0f, Interior = 0.5f, PowerDrop = 1f
                    };
                    break;
                case BuildingStyle.Mosque:
                    p = new WeatheringProfile { PlasterDamage = 0.25f, Damp = 0.5f, Streaks = 0.6f, Doors = 0.6f, Windows = 0.5f };
                    break;
                case BuildingStyle.ShepherdHut:
                    p = new WeatheringProfile { PlasterDamage = 0.5f, Damp = 1f, Streaks = 0.8f, Chimney = 0f, Doors = 0.5f };
                    break;
                case BuildingStyle.Barn:
                case BuildingStyle.Shed:
                    p = new WeatheringProfile { PlasterDamage = 0.5f, Damp = 0.9f, Streaks = 0.8f, Eaves = 0.6f, Doors = 0.4f };
                    break;
                case BuildingStyle.Karakol:
                case BuildingStyle.Barracks:
                    p = new WeatheringProfile { PlasterDamage = 0.3f, Damp = 0.6f, Streaks = 0.7f, Doors = 0.5f, Windows = 0.5f, PowerDrop = 0.5f };
                    break;
                default:
                    p = new WeatheringProfile { Damp = 0.4f, Streaks = 0.4f };
                    break;
            }

            if (tier < TierDoors)
                p.Doors = 0f;
            if (tier < TierWindows)
                p.Windows = 0f;
            if (tier < TierPlaster)
                p.PlasterDamage = 0f;
            if (tier < TierAntenna)
                p.Antenna = 0f;
            if (tier < TierPower)
                p.PowerDrop = 0f;
            if (tier < TierTiles)
                p.TileDetail = 0f;
            if (tier < TierLaundry)
                p.Laundry = 0f;
            if (tier < TierInterior)
                p.Interior = 0f;
            if (tier == 0)
                p.Streaks *= 0.5f;
            return p;
        }

        /// <summary>Cephe alanına ve yoğunluğa göre sıvası dökülmüş yama sayısı.</summary>
        public static int PatchCount(float facadeArea, float density)
        {
            if (density <= 0f || facadeArea <= 1f)
                return 0;
            return Mathf.Clamp(Mathf.RoundToInt(facadeArea / 9f * density * 1.4f), 0, 7);
        }

        /// <summary>
        /// Duvarda sıva dökülmesi yamaları. avoid dikdörtgenleri (pencere/kapı) 0.15 m payla dışarıda tutulur.
        /// Yamaların yarısı taban yakınına (nem) yanlı yerleşir.
        /// </summary>
        public static void PlanPatches(System.Random rng, float length, float yMin, float yMax, IReadOnlyList<WeatherRect> avoid, int count,
            List<WeatherRect> result)
        {
            result.Clear();
            if (count <= 0 || length < 1f || yMax - yMin < 0.5f)
                return;
            for (var i = 0; i < count; i++)
            {
                for (var attempt = 0; attempt < 8; attempt++)
                {
                    var w = Lerp(rng, 0.45f, 1.5f);
                    var h = Lerp(rng, 0.35f, 1.15f);
                    if (w > length - 0.4f)
                        w = length - 0.4f;
                    if (h > yMax - yMin)
                        h = yMax - yMin;
                    var u = Lerp(rng, 0.2f + w * 0.5f, Mathf.Max(0.2f + w * 0.5f, length - 0.2f - w * 0.5f));
                    var lowBias = rng.NextDouble() < 0.5;
                    var yHi = lowBias ? Mathf.Min(yMax, yMin + 1.4f) : yMax;
                    var y = Lerp(rng, yMin + h * 0.5f, Mathf.Max(yMin + h * 0.5f, yHi - h * 0.5f));
                    var r = new WeatherRect(u - w * 0.5f, u + w * 0.5f, y - h * 0.5f, y + h * 0.5f);
                    if (r.U0 < 0.1f || r.U1 > length - 0.1f || r.Y0 < yMin - 0.001f || r.Y1 > yMax + 0.001f)
                        continue;
                    if (Hits(r, avoid, 0.15f) || Hits(r, result, 0.1f))
                        continue;
                    result.Add(r);
                    break;
                }
            }
        }

        /// <summary>Yamayı çekirdek + 2-3 taşkın parçaya böler (kırık kenarlı görünüm). Hepsi r içinde kalır.</summary>
        public static void JaggedPieces(System.Random rng, WeatherRect r, List<WeatherRect> result)
        {
            result.Clear();
            var w = r.Width;
            var h = r.Height;
            result.Add(new WeatherRect(r.U0 + w * 0.12f, r.U1 - w * 0.12f, r.Y0 + h * 0.1f, r.Y1 - h * 0.1f));
            var nibs = 2 + (rng.NextDouble() < 0.5 ? 1 : 0);
            for (var i = 0; i < nibs; i++)
            {
                var nw = w * Lerp(rng, 0.25f, 0.45f);
                var nh = h * Lerp(rng, 0.25f, 0.5f);
                var left = rng.NextDouble() < 0.5;
                var top = rng.NextDouble() < 0.5;
                var u0 = left ? r.U0 : r.U1 - nw;
                var y0 = top ? r.Y1 - nh : r.Y0;
                result.Add(new WeatherRect(u0, u0 + nw, y0, y0 + nh));
            }
        }

        /// <summary>spans listesindeki aralıklardan [a,b] çıkarılır.</summary>
        public static void SubtractSpan(List<Vector2> spans, float a, float b)
        {
            if (b < a)
            {
                var t = a;
                a = b;
                b = t;
            }

            for (var i = spans.Count - 1; i >= 0; i--)
            {
                var s = spans[i];
                if (b <= s.x || a >= s.y)
                    continue;
                spans.RemoveAt(i);
                if (a > s.x + 0.001f)
                    spans.Add(new Vector2(s.x, a));
                if (b < s.y - 0.001f)
                    spans.Add(new Vector2(b, s.y));
            }
        }

        /// <summary>Taban nem bandı: dikdörtgen segmentler, üst kenar düzensiz (nem çizgisi). Segment genişliği 0.35–0.9 m.</summary>
        public static void DampSegments(System.Random rng, float u0, float u1, float minH, float maxH, List<WeatherRect> result)
        {
            if (u1 - u0 < 0.1f)
                return;
            var u = u0;
            while (u < u1 - 0.01f)
            {
                var w = Mathf.Min(Lerp(rng, 0.35f, 0.9f), u1 - u);
                if (u1 - (u + w) < 0.2f)
                    w = u1 - u;
                result.Add(new WeatherRect(u, u + w, 0f, Lerp(rng, minH, maxH)));
                u += w;
            }
        }

        /// <summary>Pencere denizliğinden aşağı inen yağmur izleri: 1–3 ince şerit. Uzunluk 0.4–1.8 m, minY'de kesilir.</summary>
        public static void RainStreaks(System.Random rng, float u0, float u1, float startY, float minY, List<WeatherRect> result)
        {
            var n = 1 + rng.Next(3);
            for (var i = 0; i < n; i++)
            {
                var w = Lerp(rng, 0.025f, 0.07f);
                var u = Lerp(rng, u0 + 0.05f, Mathf.Max(u0 + 0.05f, u1 - 0.05f));
                var len = Lerp(rng, 0.4f, 1.8f);
                var y0 = Mathf.Max(minY, startY - len);
                if (startY - y0 < 0.15f)
                    continue;
                result.Add(new WeatherRect(u - w * 0.5f, u + w * 0.5f, y0, startY));
            }
        }

        /// <summary>Çatı saçağı / parapet altından dökülen izler (avoid dışında, [0.3, length-0.3] içinde).</summary>
        public static void EdgeStreaks(System.Random rng, float length, float topY, float minY, float maxLen, IReadOnlyList<WeatherRect> avoid, int count,
            List<WeatherRect> result)
        {
            for (var i = 0; i < count; i++)
            {
                var w = Lerp(rng, 0.04f, 0.12f);
                var u = Lerp(rng, 0.3f, Mathf.Max(0.3f, length - 0.3f));
                var len = Lerp(rng, 0.5f, Mathf.Max(0.6f, maxLen));
                var y0 = Mathf.Max(minY, topY - len);
                if (topY - y0 < 0.25f)
                    continue;
                var r = new WeatherRect(u - w * 0.5f, u + w * 0.5f, y0, topY);
                if (Hits(r, avoid, 0.08f))
                    continue;
                result.Add(r);
            }
        }

        /// <summary>İki uç arasında sarkık ip/kablo yüksekliği (parabol). t in [0,1]; orta noktada tam sag kadar sarkar.</summary>
        public static float SagY(float y0, float y1, float t, float sag)
        {
            t = Mathf.Clamp01(t);
            return Mathf.Lerp(y0, y1, t) - 4f * sag * t * (1f - t);
        }

        /// <summary>Açıklık uzunluğuna göre sarkma (m): uzun açıklık daha çok sarkar, makul sınırlı.</summary>
        public static float SagFor(float length, float slack = 1f)
            => Mathf.Clamp(length * 0.03f * Mathf.Max(0.1f, slack), 0.04f, 2.4f);

        /// <summary>Kabloyu çizen kiriş parçası sayısı.</summary>
        public static int CableSegments(float length, int tier)
        {
            var per = tier >= 3 ? 1.2f : (tier >= 2 ? 1.8f : 3f);
            return Mathf.Clamp(Mathf.CeilToInt(length / per), 3, 28);
        }

        /// <summary>Kademeye göre direk başına paralel kablo sayısı.</summary>
        public static int CablesPerSpan(int tier) => tier >= 3 ? 3 : (tier >= 1 ? 2 : 1);

        /// <summary>Saçak altı yüksekliği: tipY + (tip - a) * tan (a: bina eksenine yatay uzaklık).</summary>
        public static float EaveUnderY(float tipY, float tip, float a, float tan) => tipY + (tip - a) * tan;

        /// <summary>Saçak kiriş ucu (rafter tail) sayısı; aralık sabit, kenarlardan pay bırakılır.</summary>
        public static int TailCount(float length, float spacing)
        {
            if (length < 0.6f || spacing < 0.1f)
                return 0;
            return Mathf.Max(1, Mathf.FloorToInt((length - 0.4f) / spacing) + 1);
        }

        /// <summary>Kırma çatıda a uzaklığındaki kiremit sırası yarı uzunluğu: ridgeHalf + (hz - a) * (hx - ridgeHalf) / hz.</summary>
        public static float HipCourseHalfLength(float ridgeHalf, float hx, float hz, float a)
        {
            if (hz < 0.01f)
                return ridgeHalf;
            a = Mathf.Clamp(a, 0f, hz);
            return ridgeHalf + (hz - a) * (hx - ridgeHalf) / hz;
        }

        /// <summary>Çatı eğimi boyunca kiremit sırası sayısı (saçaktan 0.3 m başlar, sırttan margin bırakır).</summary>
        public static int CourseCount(float slopeLen, float spacing, float margin = 0.3f)
        {
            if (spacing < 0.05f || slopeLen < 0.6f)
                return 0;
            return Mathf.Max(0, Mathf.FloorToInt((slopeLen - 0.3f - margin) / spacing) + 1);
        }

        /// <summary>Sırt kiremit segmenti sayısı.</summary>
        public static int RidgeTileCount(float ridgeLen, float tileLen)
            => tileLen < 0.05f ? 0 : Mathf.Max(1, Mathf.FloorToInt(ridgeLen / tileLen));

        /// <summary>Giriş noktaları için Prim MST (yatay mesafe, &lt;= maxLink): A-B dizin çiftleri. Uzak kümeler ayrı bileşen kalır.</summary>
        public static void PlanLinks(IReadOnlyList<Vector3> anchors, float maxLink, List<GridSpan> links)
        {
            links.Clear();
            var n = anchors.Count;
            if (n < 2)
                return;
            var visited = new bool[n];
            var dist = new float[n];
            var from = new int[n];
            for (var i = 0; i < n; i++)
            {
                dist[i] = float.MaxValue;
                from[i] = -1;
            }

            for (var seed = 0; seed < n; seed++)
            {
                if (visited[seed])
                    continue;
                dist[seed] = 0f;
                from[seed] = -1;
                while (true)
                {
                    var best = -1;
                    var bestD = float.MaxValue;
                    for (var i = 0; i < n; i++)
                    {
                        if (!visited[i] && dist[i] < bestD)
                        {
                            bestD = dist[i];
                            best = i;
                        }
                    }

                    if (best < 0 || bestD > maxLink)
                        break;
                    visited[best] = true;
                    if (from[best] >= 0)
                        links.Add(new GridSpan { A = from[best], B = best });
                    for (var j = 0; j < n; j++)
                    {
                        if (visited[j])
                            continue;
                        var d = Flat(anchors[best], anchors[j]);
                        if (d < dist[j])
                        {
                            dist[j] = d;
                            from[j] = best;
                        }
                    }
                }
            }
        }

        /// <summary>a-b arasında direk noktaları (yatay). Kısa mesafede (&lt;9 m) direk yok; aksi halde ~span aralıkla, en az bir.</summary>
        public static void PolesAlong(Vector3 a, Vector3 b, float span, List<Vector3> result)
        {
            var flat = Flat(a, b);
            if (flat < 9f || span < 4f)
                return;
            var count = Mathf.Max(1, Mathf.RoundToInt(flat / span));
            for (var i = 1; i <= count; i++)
                result.Add(Vector3.Lerp(a, b, i / (float)(count + 1)));
        }

        /// <summary>
        /// Giriş noktalarından şebeke planı: düğümler (önce binalar, sonra direkler) + sarkık kablo açıklıkları.
        /// Birbirine 3 m'den yakın direkler birleştirilir.
        /// </summary>
        public static void PlanGrid(IReadOnlyList<Vector3> anchors, float maxLink, float poleSpan, List<GridNode> nodes, List<GridSpan> spans)
        {
            nodes.Clear();
            spans.Clear();
            for (var i = 0; i < anchors.Count; i++)
                nodes.Add(new GridNode { Position = anchors[i], IsPole = false, Source = i });
            var links = new List<GridSpan>(8);
            PlanLinks(anchors, maxLink, links);
            var tmp = new List<Vector3>(4);
            for (var l = 0; l < links.Count; l++)
            {
                var a = links[l].A;
                var b = links[l].B;
                tmp.Clear();
                PolesAlong(anchors[a], anchors[b], poleSpan, tmp);
                var prev = a;
                for (var k = 0; k < tmp.Count; k++)
                {
                    var idx = FindPole(nodes, tmp[k], 3f);
                    if (idx < 0)
                    {
                        nodes.Add(new GridNode { Position = tmp[k], IsPole = true, Source = -1 });
                        idx = nodes.Count - 1;
                    }

                    if (idx != prev)
                        spans.Add(new GridSpan { A = prev, B = idx });
                    prev = idx;
                }

                if (prev != b)
                    spans.Add(new GridSpan { A = prev, B = b });
            }
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static int FindPole(List<GridNode> nodes, Vector3 p, float radius)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].IsPole && Flat(nodes[i].Position, p) < radius)
                    return i;
            }

            return -1;
        }

        /// <summary>Rastgele float [a, b].</summary>
        public static float Lerp(System.Random rng, float a, float b) => a + (b - a) * (float)rng.NextDouble();

        private static bool Hits(WeatherRect r, IReadOnlyList<WeatherRect> list, float pad)
        {
            if (list == null)
                return false;
            for (var i = 0; i < list.Count; i++)
            {
                if (r.Overlaps(list[i], pad))
                    return true;
            }

            return false;
        }
    }
}
