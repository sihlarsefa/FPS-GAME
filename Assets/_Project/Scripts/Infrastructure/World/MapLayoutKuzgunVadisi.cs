using System.Collections.Generic;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// "Kuzgun Vadisi" yerleşimi (1024 × 1024 m, x,z ∈ [-512, 512], kuzey = +z). Kenarlarda doğal sınır oluşturan dağ
    /// sırtları, ortada kuzeyden güneye akan Kuzgun Deresi, güneyde baraj göleti, köyler, karakol, ileri üs, taş ocağı,
    /// röle tepesi, ormanlık sırt, ağıl, yıkık köy ve gözetleme noktaları; asfalt ana yol + tüm bölgelere toprak yollar.
    /// Yerleşim tasarlanmıştır (tohumdan bağımsız); tohum yalnızca arazi ayrıntısını, ağaç ve kayaları değiştirir.
    /// </summary>
    public sealed partial class MapLayout
    {
        public const float DensifySpacing = 8f;

        public static MapLayout CreateKuzgunVadisi(int seed)
        {
            var layout = new MapLayout
            {
                HalfSize = 512f,
                MaxHeight = 160f,
                WaterLevel = 18f,
                Seed = seed,
                Name = "Kuzgun Vadisi"
            };

            // ------------------------------------------------------------ Kuzgun Deresi (K → G)
            layout.Rivers.Add(new RiverSpec
            {
                Width = 12f,
                Depth = 1.4f,
                Points = Densify(new[]
                {
                    V(28, 560), V(18, 480), V(-6, 410), V(-16, 340), V(0, 265), V(26, 195), V(24, 125), V(0, 55),
                    V(-12, -15), V(0, -85), V(22, -150), V(30, -215), V(24, -280), V(20, -345), V(24, -405),
                    V(12, -470), V(4, -560)
                }, DensifySpacing)
            });

            // Baraj göleti (barajın hemen kuzeyi).
            layout.Lakes.Add(new LakeSpec { Center = V(22, -283), Radius = 46f, Depth = 2.6f });

            // ------------------------------------------------------------ Bölgeler
            AddLocation(layout, "Kuzgun Köyü", LocationKind.Village, V(-118, 22), 85f, 60f, LootTier.Medium, true);
            AddLocation(layout, "Yamaç Köyü", LocationKind.Village, V(208, 178), 70f, 50f, LootTier.Medium, true);
            AddLocation(layout, "Sınır Karakolu", LocationKind.Karakol, V(140, 392), 55f, 42f, LootTier.High, true);
            AddLocation(layout, "İleri Üs Bölgesi", LocationKind.ForwardBase, V(262, -92), 85f, 68f, LootTier.Military, true);
            AddLocation(layout, "Taş Ocağı", LocationKind.Quarry, V(-282, -150), 70f, 50f, LootTier.Medium, true);
            AddLocation(layout, "Kuzgun Barajı", LocationKind.Dam, V(20, -345), 65f, 30f, LootTier.Medium, true);
            AddLocation(layout, "Röle Tepesi", LocationKind.RelayHill, V(-322, 302), 55f, 34f, LootTier.High, true);
            var forest = AddLocation(layout, "Çam Sırtı", LocationKind.Forest, V(318, 318), 110f, 20f, LootTier.Low, true);
            forest.ClearRadius = 26f;
            AddLocation(layout, "Ağıl", LocationKind.Farm, V(-196, -330), 60f, 46f, LootTier.Low, true);
            AddLocation(layout, "Yıkık Köy", LocationKind.Ruins, V(178, -298), 65f, 48f, LootTier.Medium, true);
            AddLocation(layout, "Kuzey Gözetleme Noktası", LocationKind.Outpost, V(-168, 372), 28f, 17f, LootTier.High, false);
            AddLocation(layout, "Doğu Gözetleme Noktası", LocationKind.Outpost, V(386, 40), 28f, 17f, LootTier.High, false);
            AddLocation(layout, "Batı Gözetleme Noktası", LocationKind.Outpost, V(-386, 82), 28f, 17f, LootTier.High, false);
            AddLocation(layout, "Güney Gözetleme Noktası", LocationKind.Outpost, V(-122, -404), 28f, 17f, LootTier.High, false);

            // ------------------------------------------------------------ Yollar
            // Asfalt ana yol: kuzey boğazından girer, batı kıyısından iner, B1 köprüsüyle doğuya geçer, güney boğazından çıkar.
            AddRoad(layout, "Ana Yol", RoadKind.Asphalt, 8f,
                V(-40, 560), V(-46, 480), V(-62, 410), V(-66, 335), V(-58, 262), V(-50, 190), V(-46, 120), V(-42, 55),
                V(-42, -10), V(-36, -70), V(-14, -112), V(30, -130), V(72, -152), V(98, -205), V(104, -265),
                V(96, -330), V(78, -400), V(58, -470), V(48, -560));

            // Kuzgun Köyü girişi.
            AddRoad(layout, "Kuzgun Köyü Yolu", RoadKind.Dirt, 5.5f,
                V(-43, 30), V(-70, 26), V(-96, 23));

            // Kuzgun Köyü ↔ Yamaç Köyü (B2 köprüsü).
            AddRoad(layout, "Yamaç Yolu", RoadKind.Dirt, 5.5f,
                V(-46, 122), V(-10, 132), V(24, 140), V(70, 150), V(118, 162), V(160, 172), V(190, 176));

            // Sınır Karakolu (B3 köprüsü, kıvrımlı çıkış).
            AddRoad(layout, "Karakol Yolu", RoadKind.Dirt, 5.5f,
                V(-63, 372), V(-30, 372), V(-8, 372), V(30, 360), V(80, 345), V(135, 335), V(185, 345), V(205, 372),
                V(180, 392), V(160, 393));

            // İleri Üs Bölgesi.
            AddRoad(layout, "Üs Yolu", RoadKind.Dirt, 6f,
                V(76, -158), V(120, -172), V(168, -150), V(205, -118), V(232, -100));

            // Yamaç Köyü ↔ İleri Üs (doğu sırtı boyunca).
            AddRoad(layout, "Doğu Yolu", RoadKind.Dirt, 5.5f,
                V(225, 140), V(258, 82), V(268, 22), V(268, -30), V(264, -62));

            // Taş Ocağı.
            AddRoad(layout, "Ocak Yolu", RoadKind.Dirt, 6f,
                V(-40, -40), V(-90, -62), V(-150, -96), V(-205, -128), V(-252, -144));

            // Ağıl (ocak yolundan ayrılır).
            AddRoad(layout, "Ağıl Yolu", RoadKind.Dirt, 5f,
                V(-152, -97), V(-165, -170), V(-178, -245), V(-190, -300));

            // Yıkık Köy.
            AddRoad(layout, "Harabe Yolu", RoadKind.Dirt, 5f,
                V(103, -240), V(135, -262), V(160, -284));

            // Kuzgun Barajı (doğu ayağı).
            AddRoad(layout, "Baraj Yolu", RoadKind.Dirt, 5.5f,
                V(97, -322), V(72, -335), V(50, -343));

            // Röle Tepesi (kıvrımlı dağ yolu).
            AddRoad(layout, "Röle Yolu", RoadKind.Dirt, 5f,
                V(-140, 40), V(-190, 62), V(-238, 98), V(-252, 150), V(-222, 196), V(-262, 222), V(-322, 218),
                V(-362, 240), V(-352, 272), V(-334, 290));

            // Çam Sırtı.
            AddRoad(layout, "Orman Yolu", RoadKind.Dirt, 5f,
                V(238, 192), V(282, 206), V(318, 236), V(296, 266), V(306, 300));

            // Gözetleme noktalarına patikalar.
            AddRoad(layout, "Kuzey Patika", RoadKind.Dirt, 4f,
                V(-65, 340), V(-100, 330), V(-140, 322), V(-165, 335), V(-150, 358), V(-158, 366));
            AddRoad(layout, "Doğu Patika", RoadKind.Dirt, 4f,
                V(268, 10), V(310, 0), V(350, 2), V(372, 18), V(352, 36), V(374, 40));
            AddRoad(layout, "Batı Patika", RoadKind.Dirt, 4f,
                V(-195, 62), V(-250, 40), V(-305, 38), V(-345, 52), V(-330, 78), V(-372, 82));
            AddRoad(layout, "Güney Patika", RoadKind.Dirt, 4f,
                V(-186, -300), V(-170, -350), V(-140, -372), V(-100, -380), V(-110, -398));

            layout.ComputeBridges();
            return layout;
        }

        // ================================================================== Sorgular

        /// <summary>Ada göre bölge (yoksa null).</summary>
        public LocationSpec FindLocation(string name)
        {
            for (var i = 0; i < Locations.Count; i++)
            {
                if (Locations[i] != null && Locations[i].Name == name)
                    return Locations[i];
            }

            return null;
        }

        /// <summary>Türüne göre ilk bölge (yoksa null).</summary>
        public LocationSpec FindLocation(LocationKind kind)
        {
            for (var i = 0; i < Locations.Count; i++)
            {
                if (Locations[i] != null && Locations[i].Kind == kind)
                    return Locations[i];
            }

            return null;
        }

        /// <summary>XZ noktasının en yakın yol merkez çizgisine uzaklığı (yol yoksa +∞).</summary>
        public float DistanceToRoad(Vector2 point, out RoadSpec road)
        {
            road = null;
            var best = float.PositiveInfinity;
            for (var r = 0; r < Roads.Count; r++)
            {
                var d = DistanceToPolyline(Roads[r].Points, point);
                if (d < best)
                {
                    best = d;
                    road = Roads[r];
                }
            }

            return best;
        }

        /// <summary>XZ noktasının en yakın dere merkez çizgisine uzaklığı (dere yoksa +∞).</summary>
        public float DistanceToRiver(Vector2 point)
        {
            var best = float.PositiveInfinity;
            for (var r = 0; r < Rivers.Count; r++)
                best = Mathf.Min(best, DistanceToPolyline(Rivers[r].Points, point));
            return best;
        }

        /// <summary>Çoklu çizgiye uzaklık.</summary>
        public static float DistanceToPolyline(List<Vector2> points, Vector2 p)
        {
            if (points == null || points.Count == 0)
                return float.PositiveInfinity;
            if (points.Count == 1)
                return Vector2.Distance(points[0], p);

            var best = float.PositiveInfinity;
            for (var i = 0; i + 1 < points.Count; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                var d = TerrainNoise.SegmentDistance(p.x, p.y, a.x, a.y, b.x, b.y, out _);
                if (d < best)
                    best = d;
            }

            return best;
        }

        /// <summary>
        /// Yol–dere kesişimlerinden köprü listesini yeniden hesaplar (DeckHeight sıfırlanır; arazi üretimi doldurur).
        /// </summary>
        public void ComputeBridges()
        {
            Bridges.Clear();
            for (var r = 0; r < Roads.Count; r++)
            {
                var road = Roads[r];
                if (road?.Points == null)
                    continue;

                for (var v = 0; v < Rivers.Count; v++)
                {
                    var river = Rivers[v];
                    if (river?.Points == null)
                        continue;

                    for (var i = 0; i + 1 < road.Points.Count; i++)
                    {
                        for (var j = 0; j + 1 < river.Points.Count; j++)
                        {
                            if (!SegmentIntersection(road.Points[i], road.Points[i + 1], river.Points[j], river.Points[j + 1], out var hit))
                                continue;

                            var dir = (road.Points[i + 1] - road.Points[i]).normalized;
                            var riverDir = (river.Points[j + 1] - river.Points[j]).normalized;
                            var sin = Mathf.Abs(dir.x * riverDir.y - dir.y * riverDir.x);
                            var span = (river.Width + 18f) / Mathf.Max(0.5f, sin);
                            Bridges.Add(new BridgeSpec
                            {
                                Name = (road.Name ?? "Yol") + " Köprüsü",
                                Center = hit,
                                Direction = dir,
                                Length = Mathf.Min(span, 60f),
                                Width = road.Width + 1.5f,
                                Kind = road.Kind,
                                DeckHeight = -1f
                            });
                        }
                    }
                }
            }
        }

        // ================================================================== Yardımcılar

        /// <summary>Kontrol noktalarından Catmull-Rom (merkezcil) eğrisiyle ≈spacing aralıklı yoğun çoklu çizgi.</summary>
        public static List<Vector2> Densify(IReadOnlyList<Vector2> control, float spacing)
        {
            var result = new List<Vector2>();
            if (control == null || control.Count == 0)
                return result;
            if (control.Count == 1)
            {
                result.Add(control[0]);
                return result;
            }

            spacing = Mathf.Max(0.5f, spacing);
            result.Add(control[0]);
            for (var i = 0; i + 1 < control.Count; i++)
            {
                var p0 = control[Mathf.Max(0, i - 1)];
                var p1 = control[i];
                var p2 = control[i + 1];
                var p3 = control[Mathf.Min(control.Count - 1, i + 2)];
                var length = Vector2.Distance(p1, p2);
                var steps = Mathf.Max(1, Mathf.CeilToInt(length / spacing));
                for (var s = 1; s <= steps; s++)
                    result.Add(CatmullRom(p0, p1, p2, p3, s / (float)steps));
            }

            return result;
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            // Merkezcil Catmull-Rom (alfa 0.5) — kendini kesme/sivri uç oluşturmaz.
            float t0 = 0f;
            var t1 = t0 + Mathf.Sqrt(Mathf.Max(1e-4f, Vector2.Distance(p0, p1)));
            var t2 = t1 + Mathf.Sqrt(Mathf.Max(1e-4f, Vector2.Distance(p1, p2)));
            var t3 = t2 + Mathf.Sqrt(Mathf.Max(1e-4f, Vector2.Distance(p2, p3)));
            var u = Mathf.Lerp(t1, t2, t);
            var a1 = (t1 - u) / (t1 - t0) * p0 + (u - t0) / (t1 - t0) * p1;
            var a2 = (t2 - u) / (t2 - t1) * p1 + (u - t1) / (t2 - t1) * p2;
            var a3 = (t3 - u) / (t3 - t2) * p2 + (u - t2) / (t3 - t2) * p3;
            var b1 = (t2 - u) / (t2 - t0) * a1 + (u - t0) / (t2 - t0) * a2;
            var b2 = (t3 - u) / (t3 - t1) * a2 + (u - t1) / (t3 - t1) * a3;
            return (t2 - u) / (t2 - t1) * b1 + (u - t1) / (t2 - t1) * b2;
        }

        private static bool SegmentIntersection(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 hit)
        {
            hit = default;
            var r = b - a;
            var s = d - c;
            var denom = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(denom) < 1e-6f)
                return false;

            var ca = c - a;
            var t = (ca.x * s.y - ca.y * s.x) / denom;
            var u = (ca.x * r.y - ca.y * r.x) / denom;
            if (t < 0f || t >= 1f || u < 0f || u >= 1f)
                return false;

            hit = a + r * t;
            return true;
        }

        private static LocationSpec AddLocation(MapLayout layout, string name, LocationKind kind, Vector2 center, float radius,
            float flattenRadius, LootTier tier, bool major)
        {
            var spec = new LocationSpec
            {
                Name = name,
                Kind = kind,
                Center = center,
                Radius = radius,
                FlattenRadius = flattenRadius,
                Tier = tier,
                IsMajor = major
            };
            layout.Locations.Add(spec);
            return spec;
        }

        private static void AddRoad(MapLayout layout, string name, RoadKind kind, float width, params Vector2[] control)
        {
            layout.Roads.Add(new RoadSpec
            {
                Name = name,
                Kind = kind,
                Width = width,
                Points = Densify(control, DensifySpacing)
            });
        }

        private static Vector2 V(float x, float z) => new Vector2(x, z);
    }
}
