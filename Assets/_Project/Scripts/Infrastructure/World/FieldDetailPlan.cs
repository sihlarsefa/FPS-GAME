using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>Tarla parseli türü.</summary>
    public enum FieldKind
    {
        /// <summary>Sürülmüş tarla: sıra sıra karık/sırt dokusu.</summary>
        Plowed = 0,
        /// <summary>Hasat sonrası anız: kuru zemin + kısa anız ayrıntısı.</summary>
        Stubble = 1,
        /// <summary>Nadas: çayır, çiçek lekeleri.</summary>
        Fallow = 2
    }

    /// <summary>Dikdörtgen tarla parseli (yerel eksenler: ex = (cos, sin), ez = (-sin, cos); sıralar ez boyunca uzanır).</summary>
    public sealed class FieldParcel
    {
        public Vector2 Center;
        public float HalfX;
        public float HalfZ;
        public float Yaw;
        public FieldKind Kind;
        /// <summary>Taş duvar kenarları (bit: 1 +x, 2 -x, 4 +z, 8 -z).</summary>
        public int WallMask;
        /// <summary>Çalı çiti kenarları (aynı bit düzeni).</summary>
        public int HedgeMask;
        public float Radius;
        public float Cos = 1f;
        public float Sin;

        public void Init()
        {
            Cos = Mathf.Cos(Yaw);
            Sin = Mathf.Sin(Yaw);
            Radius = Mathf.Sqrt(HalfX * HalfX + HalfZ * HalfZ);
        }

        public void ToLocal(float x, float z, out float lx, out float lz)
        {
            var dx = x - Center.x;
            var dz = z - Center.y;
            lx = dx * Cos + dz * Sin;
            lz = -dx * Sin + dz * Cos;
        }

        public Vector2 ToWorld(float lx, float lz)
        {
            return new Vector2(Center.x + lx * Cos - lz * Sin, Center.y + lx * Sin + lz * Cos);
        }

        /// <summary>Parsel içi mesafe (m): kenara olan en küçük uzaklık; dışarıda negatif.</summary>
        public float InsideDistance(float x, float z)
        {
            ToLocal(x, z, out var lx, out var lz);
            return Mathf.Min(HalfX - Mathf.Abs(lx), HalfZ - Mathf.Abs(lz));
        }
    }

    /// <summary>Yerleşim çevresi ezilmiş çim dairesi.</summary>
    public struct TrampleCircle
    {
        public Vector2 Center;
        public float InnerRadius;
        public float Width;
    }

    /// <summary>
    /// Saha ayrıntı planı: köy/çiftlik/harabe çevresinde seed'e bağlı deterministik tarla parselleri, kenar duvar/çit bayrakları
    /// ve yerleşim ezilmiş-çim daireleri. Saf mantık: boyama, ayrıntı ve prop üretimi aynı planı paylaşır (model başına önbellek).
    /// </summary>
    public sealed class FieldPlan
    {
        public const float RowSpacing = 6f;

        private static readonly ConditionalWeakTable<TerrainModel, FieldPlan> Cache = new ConditionalWeakTable<TerrainModel, FieldPlan>();

        public readonly List<FieldParcel> Parcels = new List<FieldParcel>();
        public readonly List<TrampleCircle> Trample = new List<TrampleCircle>();

        /// <summary>Toprak yolların en dar yarı genişliği (m); yol ortası çim şeridi/iz bandı buna göre.</summary>
        public float DirtRoadHalfWidth = 2f;

        public static FieldPlan Of(TerrainModel model)
        {
            if (model == null)
                return null;
            return Cache.GetValue(model, Build);
        }

        public static FieldPlan Build(TerrainModel model)
        {
            var plan = new FieldPlan();
            var layout = model.Layout;
            var rng = new System.Random(model.Seed * 6151 + 2749);
            var half = float.MaxValue;
            for (var r = 0; r < layout.Roads.Count; r++)
            {
                var road = layout.Roads[r];
                if (road != null && road.Kind == RoadKind.Dirt)
                    half = Mathf.Min(half, Mathf.Max(1f, road.Width * 0.5f));
            }

            if (half < float.MaxValue)
                plan.DirtRoadHalfWidth = half;

            for (var i = 0; i < layout.Locations.Count; i++)
            {
                var loc = layout.Locations[i];
                if (loc == null || loc.FlattenRadius <= 0f)
                    continue;
                if (loc.Kind == LocationKind.Village || loc.Kind == LocationKind.Farm || loc.Kind == LocationKind.Ruins
                    || loc.Kind == LocationKind.Karakol || loc.Kind == LocationKind.ForwardBase)
                    plan.Trample.Add(new TrampleCircle { Center = loc.Center, InnerRadius = loc.FlattenRadius * 1.1f, Width = 26f });
            }

            for (var i = 0; i < layout.Locations.Count; i++)
            {
                var loc = layout.Locations[i];
                if (loc == null || loc.FlattenRadius <= 0f)
                    continue;
                int target;
                switch (loc.Kind)
                {
                    case LocationKind.Village: target = 7; break;
                    case LocationKind.Farm: target = 8; break;
                    case LocationKind.Ruins: target = 3; break;
                    default: continue;
                }

                plan.PlaceAround(model, loc, target, rng);
            }

            return plan;
        }

        private void PlaceAround(TerrainModel model, LocationSpec loc, int target, System.Random rng)
        {
            var layout = model.Layout;
            var inner = Mathf.Max(loc.FlattenRadius * 1.2f, loc.Radius) + 8f;
            var placed = 0;
            for (var attempt = 0; attempt < target * 14 && placed < target; attempt++)
            {
                var angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                var hx = 13f + (float)rng.NextDouble() * 17f;
                var hz = 10f + (float)rng.NextDouble() * 14f;
                var dist = inner + Mathf.Max(hx, hz) * 0.8f + (float)rng.NextDouble() * 85f;
                var cx = loc.Center.x + Mathf.Cos(angle) * dist;
                var cz = loc.Center.y + Mathf.Sin(angle) * dist;
                var yaw = angle + Mathf.PI * 0.5f + ((float)rng.NextDouble() - 0.5f) * 0.9f;
                var parcel = new FieldParcel { Center = new Vector2(cx, cz), HalfX = hx, HalfZ = hz, Yaw = yaw };
                parcel.Init();

                if (!Suitable(model, parcel, layout))
                    continue;
                var overlaps = false;
                for (var p = 0; p < Parcels.Count && !overlaps; p++)
                {
                    var o = Parcels[p];
                    overlaps = Vector2.Distance(o.Center, parcel.Center) < (o.Radius + parcel.Radius) * 0.82f;
                }

                if (overlaps)
                    continue;

                var k = (float)rng.NextDouble();
                parcel.Kind = k < 0.45f ? FieldKind.Plowed : k < 0.75f ? FieldKind.Stubble : FieldKind.Fallow;
                for (var side = 0; side < 4; side++)
                {
                    var s = (float)rng.NextDouble();
                    if (s < 0.42f)
                        parcel.WallMask |= 1 << side;
                    else if (s < 0.72f)
                        parcel.HedgeMask |= 1 << side;
                }

                Parcels.Add(parcel);
                placed++;
            }
        }

        private static bool Suitable(TerrainModel model, FieldParcel p, MapLayout layout)
        {
            var water = layout.WaterLevel;
            var minH = float.MaxValue;
            var maxH = float.MinValue;
            for (var iz = -1; iz <= 1; iz++)
            {
                for (var ix = -1; ix <= 1; ix++)
                {
                    var w = p.ToWorld(ix * p.HalfX, iz * p.HalfZ);
                    if (!model.IsClearOfFeatures(w.x, w.y, 6f, 10f, 1.05f))
                        return false;
                    if (model.EdgeDistance(w.x, w.y) < 20f)
                        return false;
                    var h = model.SampleHeight(w.x, w.y);
                    if (h < water + 1.5f || model.SampleSlope(w.x, w.y) > 11f)
                        return false;
                    if (h > layout.SnowLine - 25f)
                        return false;
                    minH = Mathf.Min(minH, h);
                    maxH = Mathf.Max(maxH, h);
                    for (var l = 0; l < layout.Locations.Count; l++)
                    {
                        var loc = layout.Locations[l];
                        if (loc == null || loc.FlattenRadius <= 0f)
                            continue;
                        var reach = Mathf.Max(loc.FlattenRadius * 1.2f, loc.Radius) + 3f;
                        if (Vector2.Distance(w, loc.Center) < reach)
                            return false;
                    }
                }
            }

            return maxH - minH < 3.5f;
        }

        /// <summary>Noktayı içeren en baskın parsel ve içeri mesafe; yoksa null.</summary>
        public FieldParcel Find(float x, float z, out float insideDistance)
        {
            insideDistance = -1f;
            FieldParcel best = null;
            for (var i = 0; i < Parcels.Count; i++)
            {
                var p = Parcels[i];
                var dx = x - p.Center.x;
                var dz = z - p.Center.y;
                if (dx * dx + dz * dz > p.Radius * p.Radius)
                    continue;
                var d = p.InsideDistance(x, z);
                if (d > insideDistance)
                {
                    insideDistance = d;
                    best = p;
                }
            }

            return insideDistance > -0.5f ? best : null;
        }

        /// <summary>Ezilmiş çim gücü [0,1]: yerleşim iç yarıçapından dışa doğru sönümlenir.</summary>
        public float TrampleAt(float x, float z, float noise = 0f)
        {
            var best = 0f;
            for (var i = 0; i < Trample.Count; i++)
            {
                var c = Trample[i];
                var dx = x - c.Center.x;
                var dz = z - c.Center.y;
                var reach = c.InnerRadius + c.Width + 8f;
                var sq = dx * dx + dz * dz;
                if (sq > reach * reach)
                    continue;
                var d = Mathf.Sqrt(sq) + noise * 5f;
                best = Mathf.Max(best, 1f - TerrainNoise.SmoothStep(c.InnerRadius, c.InnerRadius + c.Width, d));
            }

            return best;
        }

        /// <summary>Nokta (kenar payıyla) herhangi bir parselin içinde mi? (ağaç temizliği için)</summary>
        public bool InsideAny(float x, float z, float margin)
        {
            return Find(x, z, out var d) != null && d > -margin;
        }
    }

    /// <summary>Saf saha ayrıntı kuralları (test edilebilir).</summary>
    public static class FieldDetailRules
    {
        /// <summary>Sıra deseni [0,1]: 1 = sırt (kuru toprak), 0 = karık (nemli, koyu).</summary>
        public static float RowRidge(float localX, float spacing = FieldPlan.RowSpacing)
        {
            var s = 0.5f + 0.5f * Mathf.Sin(localX / Mathf.Max(0.5f, spacing) * Mathf.PI * 2f);
            return TerrainNoise.SmoothStep(0.3f, 0.7f, s);
        }

        /// <summary>Toprak yol şeridi: ortada çim şeridi ve iki yanında tekerlek izi bandı. edge: yol kenarına işaretli mesafe (iç negatif).</summary>
        public static void DirtRoadWear(float edge, float halfWidth, float noise01, out float grassStrip, out float rut)
        {
            var depth = -edge; // merkezden kenara: halfWidth → 0
            var centerReach = halfWidth * 0.5f;
            grassStrip = TerrainNoise.SmoothStep(centerReach * 0.8f, centerReach * 1.2f + 0.2f, depth) * Mathf.Clamp01((noise01 - 0.25f) * 2f);
            var rutBand = TerrainNoise.SmoothStep(halfWidth * 0.3f, halfWidth * 0.45f, depth)
                          * (1f - TerrainNoise.SmoothStep(halfWidth * 0.55f, halfWidth * 0.75f, depth));
            rut = rutBand * (0.55f + 0.45f * noise01);
        }

        /// <summary>Boyada 50-200 m ölçekli ince renk bozulması (kuruluk eklentisi). a: ~55 m fBm, b: ~180 m fBm, [-1,1].</summary>
        public static float ColorBreakup(float fbm55, float fbm180)
        {
            return Mathf.Clamp(fbm55, -1f, 1f) * 0.1f + Mathf.Clamp(fbm180, -1f, 1f) * 0.12f;
        }

        /// <summary>
        /// Ayrıntı yoğunluklarını (6 tür: çimen, kuru, 3 çiçek, anız) tarla/ezilme/yol durumuna göre değiştirir.
        /// fieldInside: parsel içi yumuşak ağırlık [0,1]; ridge: sıra sırtı [0,1]; trample [0,1]; roadEdge: yol kenarı mesafesi (m).
        /// </summary>
        public static void ModulateDensity(float[] density, float fieldInside, FieldKind kind, float ridge, float trample, float roadEdge, float cluster01)
        {
            if (density == null || density.Length < VegetationPainter.TotalKindCount)
                return;
            const int stubble = (int)DetailKind.Stubble;

            // Çiçek yalnızca yoldan uzak çayırda; ezilmiş çimde yok.
            var offPath = TerrainNoise.SmoothStep(3f, 9f, float.IsInfinity(roadEdge) ? 99f : roadEdge);
            for (var k = (int)DetailKind.FlowerWhite; k <= (int)DetailKind.FlowerViolet; k++)
                density[k] *= offPath * (1f - trample);

            var grassKeep = 1f - 0.85f * trample;
            density[(int)DetailKind.Grass] *= grassKeep;
            density[(int)DetailKind.DryGrass] *= grassKeep;

            if (fieldInside <= 0f)
                return;

            switch (kind)
            {
                case FieldKind.Plowed:
                    for (var k = 0; k < stubble; k++)
                        density[k] *= 1f - fieldInside;
                    density[stubble] = 0f;
                    break;
                case FieldKind.Stubble:
                    for (var k = 0; k < stubble; k++)
                        density[k] *= 1f - fieldInside;
                    density[stubble] = Mathf.Clamp01(fieldInside * (0.55f + 0.45f * ridge));
                    break;
                default:
                    // Nadas: çiçek lekeleri.
                    var fl = Mathf.Clamp01(fieldInside * TerrainNoise.SmoothStep(0.42f, 0.7f, cluster01) * 0.85f);
                    var fk = cluster01 < 0.55f ? DetailKind.FlowerWhite : cluster01 < 0.62f ? DetailKind.FlowerYellow : DetailKind.FlowerViolet;
                    density[(int)fk] = Mathf.Max(density[(int)fk], fl);
                    break;
            }
        }
    }
}
