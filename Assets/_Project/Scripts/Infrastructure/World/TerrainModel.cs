using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Arazinin saf matematiksel modeli (Unity nesnesi yok): yerleşimden (MapLayout) yükseklik alanı ve yardımcı maskeler
    /// üretir — dere uzaklığı, yol kenarı uzaklığı/türü, bölge düzleştirme ağırlığı. TerrainGenerator (yükseklik, katman,
    /// ağaç), WorldGenerator (iniş/araç noktaları, kayalar, su) ve mini harita aynı modeli kullanır.
    /// Izgara: Resolution × Resolution köşe, (OriginX, OriginZ)'den CellSize aralıklı; indeks = z * Resolution + x.
    /// Yükseklikler dünya Y'sidir (arazi y = 0'a yerleştirilir).
    /// </summary>
    public sealed class TerrainModel
    {
        public const int DefaultResolution = 513;

        /// <summary>Dere şevi eğimi (dikey/yatay).</summary>
        public const float RiverBankSlope = 0.7f;

        /// <summary>Yol kenarından itibaren şev/geçiş için ızgarada tutulan en büyük mesafe (m).</summary>
        public const float RoadInfluence = 28f;

        /// <summary>Yolların hedef azami eğimi (%15).</summary>
        public const float MaxRoadGrade = 0.15f;

        public MapLayout Layout { get; }
        public int Seed { get; }
        public int Resolution { get; }
        public float Size { get; }
        public float CellSize { get; }
        public float OriginX { get; }
        public float OriginZ { get; }

        /// <summary>Köşe yükseklikleri (dünya Y, m).</summary>
        public float[] Heights { get; }

        /// <summary>En yakın dere merkez çizgisine uzaklık (m).</summary>
        public float[] RiverDistance { get; }

        /// <summary>En yakın yol KENARINA uzaklık (m; yol içinde negatif; etki dışında +∞).</summary>
        public float[] RoadEdge { get; }

        /// <summary>En yakın yolun profil yüksekliği (RoadEdge sonlu ise geçerli).</summary>
        public float[] RoadHeight { get; }

        /// <summary>En yakın yol türü: 0 yok, 1 asfalt, 2 toprak.</summary>
        public byte[] RoadKindMap { get; }

        /// <summary>Bölge/köprü düzleştirme ağırlığı (0..1).</summary>
        public float[] FlattenWeight { get; }

        /// <summary>Dere su yüzeyi yarı genişliği (m) — su şeridi mesh'i için.</summary>
        public float RiverWaterHalfWidth { get; private set; }

        /// <summary>Hesaplanan en yüksek nokta (m).</summary>
        public float MaxTerrainHeight { get; private set; }

        /// <summary>Yolların hesaplanan profil noktaları (yol sırasıyla; x,z = konum, y = yükseklik).</summary>
        public List<Vector3[]> RoadProfiles { get; } = new List<Vector3[]>();

        private readonly TerrainNoise _noise;
        private readonly float _halfSize;

        // Dere segmentleri (düz diziler, hızlı tarama).
        private float[] _segAx, _segAz, _segBx, _segBz;
        private int[] _segRiver;
        private int _segCount;

        private TerrainModel(MapLayout layout, int seed, int resolution)
        {
            Layout = layout;
            Seed = seed;
            Resolution = resolution;
            _halfSize = layout.HalfSize;
            Size = layout.HalfSize * 2f;
            CellSize = Size / (resolution - 1);
            OriginX = -layout.HalfSize;
            OriginZ = -layout.HalfSize;
            var n = resolution * resolution;
            Heights = new float[n];
            RiverDistance = new float[n];
            RoadEdge = new float[n];
            RoadHeight = new float[n];
            RoadKindMap = new byte[n];
            FlattenWeight = new float[n];
            _noise = new TerrainNoise(seed);
        }

        /// <summary>Yerleşimden modeli üretir. layout.Locations[].TargetHeight ve layout.Bridges[].DeckHeight/Length doldurulur.</summary>
        public static TerrainModel Build(MapLayout layout, int seed, int resolution = DefaultResolution)
        {
            if (layout == null)
                layout = MapLayout.CreateKuzgunVadisi(seed);
            resolution = Mathf.Clamp(resolution, 33, 4097);

            var model = new TerrainModel(layout, seed, resolution);
            model.PrepareRivers();
            model.ComputeRiverDistances();
            model.ComputeBaseHeights();
            model.ApplyLocationFlatten();
            model.ApplyBridgePads();
            model.BuildRoads();
            model.CarveRivers();
            model.CarveLakes();
            model.FinalizeHeights();
            return model;
        }

        // ================================================================== Sorgular

        public float WorldX(int ix) => OriginX + ix * CellSize;
        public float WorldZ(int iz) => OriginZ + iz * CellSize;

        /// <summary>Çift doğrusal yükseklik (dünya Y).</summary>
        public float SampleHeight(float x, float z) => SampleGrid(Heights, x, z);

        /// <summary>Herhangi bir köşe ızgarasını çift doğrusal örnekler.</summary>
        public float SampleGrid(float[] grid, float x, float z)
        {
            var fx = Mathf.Clamp((x - OriginX) / CellSize, 0f, Resolution - 1.001f);
            var fz = Mathf.Clamp((z - OriginZ) / CellSize, 0f, Resolution - 1.001f);
            var ix = (int)fx;
            var iz = (int)fz;
            var tx = fx - ix;
            var tz = fz - iz;
            var i = iz * Resolution + ix;
            var a = grid[i];
            var b = grid[i + 1];
            var c = grid[i + Resolution];
            var d = grid[i + Resolution + 1];
            if (float.IsInfinity(a) || float.IsInfinity(b) || float.IsInfinity(c) || float.IsInfinity(d))
                return Mathf.Min(Mathf.Min(a, b), Mathf.Min(c, d));
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        /// <summary>En yakın köşe indeksi.</summary>
        public int NearestIndex(float x, float z)
        {
            var ix = Mathf.Clamp(Mathf.RoundToInt((x - OriginX) / CellSize), 0, Resolution - 1);
            var iz = Mathf.Clamp(Mathf.RoundToInt((z - OriginZ) / CellSize), 0, Resolution - 1);
            return iz * Resolution + ix;
        }

        /// <summary>Yüzey normali (merkezi farklar).</summary>
        public Vector3 SampleNormal(float x, float z)
        {
            var e = CellSize;
            var hl = SampleHeight(x - e, z);
            var hr = SampleHeight(x + e, z);
            var hd = SampleHeight(x, z - e);
            var hu = SampleHeight(x, z + e);
            return new Vector3(hl - hr, 2f * e, hd - hu).normalized;
        }

        /// <summary>Eğim (derece).</summary>
        public float SampleSlope(float x, float z)
        {
            var n = SampleNormal(x, z);
            return Mathf.Acos(Mathf.Clamp(n.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        public float SampleRiverDistance(float x, float z) => SampleGrid(RiverDistance, x, z);

        /// <summary>Yol kenarı uzaklığı (en yakın köşe; etki dışında +∞).</summary>
        public float SampleRoadEdge(float x, float z) => RoadEdge[NearestIndex(x, z)];

        public byte SampleRoadKind(float x, float z) => RoadKindMap[NearestIndex(x, z)];

        public float SampleFlatten(float x, float z) => SampleGrid(FlattenWeight, x, z);

        /// <summary>Noktanın su altında olup olmadığı (dere/göl).</summary>
        public bool IsWater(float x, float z) => SampleHeight(x, z) < Layout.WaterLevel - 0.05f;

        /// <summary>Kenar uzaklığı (harita sınırına, m).</summary>
        public float EdgeDistance(float x, float z) => _halfSize - Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));

        /// <summary>
        /// Ağaç/kaya için serbest alan mı? Yol (kenar payı), dere, göl, köprü, bölge (ClearRadius/Radius) ve harita kenarı hariç.
        /// </summary>
        public bool IsClearOfFeatures(float x, float z, float roadMargin, float riverMargin, float locationScale)
        {
            if (EdgeDistance(x, z) < 4f)
                return false;
            if (SampleRoadEdge(x, z) < roadMargin)
                return false;
            if (SampleRiverDistance(x, z) < RiverWaterHalfWidth + riverMargin)
                return false;

            var p = new Vector2(x, z);
            var lakes = Layout.Lakes;
            for (var i = 0; i < lakes.Count; i++)
            {
                if (lakes[i] != null && Vector2.Distance(p, lakes[i].Center) < lakes[i].Radius + riverMargin)
                    return false;
            }

            var bridges = Layout.Bridges;
            for (var i = 0; i < bridges.Count; i++)
            {
                if (bridges[i] != null && Vector2.Distance(p, bridges[i].Center) < bridges[i].Length * 0.5f + 8f)
                    return false;
            }

            var locations = Layout.Locations;
            for (var i = 0; i < locations.Count; i++)
            {
                var l = locations[i];
                if (l == null)
                    continue;
                var radius = (l.ClearRadius > 0f ? l.ClearRadius : l.Radius) * locationScale;
                if ((p - l.Center).sqrMagnitude < radius * radius)
                    return false;
            }

            return true;
        }

        /// <summary>Gürültü örnekleyici (aynı tohum).</summary>
        public TerrainNoise Noise => _noise;

        // ================================================================== Dere

        private void PrepareRivers()
        {
            var count = 0;
            for (var r = 0; r < Layout.Rivers.Count; r++)
            {
                var pts = Layout.Rivers[r]?.Points;
                if (pts != null && pts.Count > 1)
                    count += pts.Count - 1;
            }

            _segAx = new float[count];
            _segAz = new float[count];
            _segBx = new float[count];
            _segBz = new float[count];
            _segRiver = new int[count];
            _segCount = 0;
            var maxHalf = 0f;
            for (var r = 0; r < Layout.Rivers.Count; r++)
            {
                var river = Layout.Rivers[r];
                var pts = river?.Points;
                if (pts == null || pts.Count < 2)
                    continue;

                var inner = river.Width * 0.3f;
                maxHalf = Mathf.Max(maxHalf, inner + Mathf.Max(0.1f, river.Depth) / RiverBankSlope);
                for (var i = 0; i + 1 < pts.Count; i++)
                {
                    _segAx[_segCount] = pts[i].x;
                    _segAz[_segCount] = pts[i].y;
                    _segBx[_segCount] = pts[i + 1].x;
                    _segBz[_segCount] = pts[i + 1].y;
                    _segRiver[_segCount] = r;
                    _segCount++;
                }
            }

            RiverWaterHalfWidth = maxHalf;
        }

        private void ComputeRiverDistances()
        {
            var res = Resolution;
            for (var iz = 0; iz < res; iz++)
            {
                var z = WorldZ(iz);
                for (var ix = 0; ix < res; ix++)
                {
                    var x = WorldX(ix);
                    var best = float.MaxValue;
                    for (var s = 0; s < _segCount; s++)
                    {
                        // Hızlı ret: segment kutusuna uzaklık mevcut en iyiden büyükse atla.
                        var minX = Mathf.Min(_segAx[s], _segBx[s]);
                        var maxX = Mathf.Max(_segAx[s], _segBx[s]);
                        var minZ = Mathf.Min(_segAz[s], _segBz[s]);
                        var maxZ = Mathf.Max(_segAz[s], _segBz[s]);
                        var dx = x < minX ? minX - x : (x > maxX ? x - maxX : 0f);
                        var dz = z < minZ ? minZ - z : (z > maxZ ? z - maxZ : 0f);
                        if (dx * dx + dz * dz >= best * best)
                            continue;

                        var d = TerrainNoise.SegmentDistance(x, z, _segAx[s], _segAz[s], _segBx[s], _segBz[s], out _);
                        if (d < best)
                            best = d;
                    }

                    RiverDistance[iz * res + ix] = _segCount > 0 ? best : 10000f;
                }
            }
        }

        private void CarveRivers()
        {
            if (_segCount == 0)
                return;

            var w = Layout.WaterLevel;
            var river = Layout.Rivers[_segRiver[0]];
            var bed = w - Mathf.Max(0.1f, river.Depth);
            var inner = river.Width * 0.3f;
            var res = Resolution;
            for (var i = 0; i < Heights.Length; i++)
            {
                var d = RiverDistance[i];
                if (d > 80f)
                    continue;

                var profile = d <= inner ? bed : bed + (d - inner) * RiverBankSlope;
                var original = Heights[i];
                var h = TerrainNoise.SmoothMin(original, profile, 1.2f);
                // Kanal dışındaki kıyı su seviyesinin altına inmesin (su şeridi havada kalmasın); zaten alçak hücreler korunur.
                h = Mathf.Max(h, Mathf.Min(Mathf.Min(profile, w + 1.5f), original));
                Heights[i] = h;
            }
        }

        private void CarveLakes()
        {
            var w = Layout.WaterLevel;
            var res = Resolution;
            for (var l = 0; l < Layout.Lakes.Count; l++)
            {
                var lake = Layout.Lakes[l];
                if (lake == null || lake.Radius <= 1f)
                    continue;

                var bed = w - Mathf.Max(0.2f, lake.Depth);
                var inner = lake.Radius * 0.5f;
                var k = (Mathf.Max(0.2f, lake.Depth) + 2f) / Mathf.Max(1f, lake.Radius - inner);
                var reach = lake.Radius + 40f;
                var lakeIndex = l;
                var rect = reach * MaxLakeShoreScale;
                ForEachInRect(lake.Center.x - rect, lake.Center.y - rect, lake.Center.x + rect, lake.Center.y + rect, (i, x, z) =>
                {
                    // Doğal kıyı: yarıçap açıya göre ±%15 dalgalanır.
                    var d = Vector2.Distance(new Vector2(x, z), lake.Center) / LakeShoreScale(lakeIndex, x - lake.Center.x, z - lake.Center.y);
                    if (d > reach)
                        return;
                    var profile = d <= inner ? bed : bed + (d - inner) * k;
                    var original = Heights[i];
                    var h = TerrainNoise.SmoothMin(original, profile, 1.5f);
                    // Kıyı su seviyesinin altına inmesin — ama zaten daha alçak olan hücreleri (dere yatağı) yükseltme.
                    h = Mathf.Max(h, Mathf.Min(Mathf.Min(profile, w + 1.2f), original));
                    Heights[i] = h;
                });
            }
        }

        /// <summary>Göletin en büyük kıyı ölçeği (yarıçap çarpanı) — su yüzeyi bu kadar geniş tutulmalı.</summary>
        public const float MaxLakeShoreScale = 1.15f;

        /// <summary>Gölet kıyı yarıçap çarpanı (0.85..1.15), merkeze göre yön (dx, dz) için.</summary>
        public float LakeShoreScale(int lakeIndex, float dx, float dz)
        {
            var angle = Mathf.Atan2(dz, dx);
            var n = _noise.Perlin(Mathf.Cos(angle) * 1.4f + lakeIndex * 7.3f + 50f, Mathf.Sin(angle) * 1.4f - lakeIndex * 3.1f + 50f);
            return Mathf.Clamp(1f + 0.16f * n, 2f - MaxLakeShoreScale, MaxLakeShoreScale);
        }

        // ================================================================== Temel yükseklik

        private void ComputeBaseHeights()
        {
            var res = Resolution;
            var w = Layout.WaterLevel;
            var cap = Layout.MaxHeight - 14f;
            const float knee = 100f;

            // Tepeler (bölge türüne göre).
            var peaks = new List<Vector4>(); // x, z, genlik, sigma
            for (var i = 0; i < Layout.Locations.Count; i++)
            {
                var l = Layout.Locations[i];
                if (l == null)
                    continue;
                switch (l.Kind)
                {
                    case LocationKind.RelayHill:
                        peaks.Add(new Vector4(l.Center.x, l.Center.y, 78f, 105f));
                        break;
                    case LocationKind.Karakol:
                        peaks.Add(new Vector4(l.Center.x, l.Center.y, 8f, 70f));
                        break;
                    case LocationKind.Outpost:
                        peaks.Add(new Vector4(l.Center.x, l.Center.y, 6f, 45f));
                        break;
                }
            }

            LocationSpec forest = null;
            for (var i = 0; i < Layout.Locations.Count; i++)
            {
                if (Layout.Locations[i] != null && Layout.Locations[i].Kind == LocationKind.Forest)
                {
                    forest = Layout.Locations[i];
                    break;
                }
            }

            for (var iz = 0; iz < res; iz++)
            {
                var z = WorldZ(iz);
                for (var ix = 0; ix < res; ix++)
                {
                    var x = WorldX(ix);
                    var index = iz * res + ix;
                    var dr = RiverDistance[index];

                    var floor = w + 5.2f + 2.2f * _noise.Fbm(x / 190f + 3.1f, z / 190f - 1.7f, 3);
                    var valleyT = TerrainNoise.SmoothStep(26f, 300f, dr);
                    var hills = Mathf.Pow(valleyT, 1.2f) * (34f + 22f * _noise.Fbm(x / 240f + 11f, z / 240f - 7f, 4));
                    hills += valleyT * 10f * (_noise.Ridged(x / 150f - 4f, z / 150f + 8f, 4) - 0.35f);
                    // İç kesimde yuvarlak tepeler (vadi yamaçlarında siper/örtü).
                    hills += 12f * _noise.Fbm(x / 115f - 21f, z / 115f + 13f, 3) * TerrainNoise.SmoothStep(25f, 110f, dr)
                             * (1f - TerrainNoise.SmoothStep(260f, 380f, Mathf.Max(Mathf.Abs(x), Mathf.Abs(z))));

                    var de = _halfSize - Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                    var edgeT = 1f - TerrainNoise.SmoothStep(18f, 215f, de);
                    var ridge = Mathf.Pow(edgeT, 1.45f) * (58f + 52f * _noise.Ridged(x / 165f + 3f, z / 165f - 5f, 5));
                    ridge *= TerrainNoise.SmoothStep(48f, 150f, dr); // dere boğazları

                    var detail = 1.8f * _noise.Fbm(x / 36f, z / 36f, 3) * TerrainNoise.SmoothStep(12f, 60f, dr);
                    var h = floor + hills + ridge + detail;

                    if (forest != null)
                    {
                        // KB–GD uzanan ormanlık sırt.
                        var axis = new Vector2(0.7071f, -0.7071f);
                        var rel = new Vector2(x, z) - forest.Center;
                        var along = Vector2.Dot(rel, axis);
                        var across = rel.x * axis.y - rel.y * axis.x;
                        var lineFalloff = Mathf.Exp(-(across * across) / (70f * 70f)) * Mathf.Exp(-(along * along) / (190f * 190f));
                        h += 16f * lineFalloff;
                    }

                    // Yumuşak tavan (üst üste binen sırtlar düz plato oluşturmasın).
                    if (h > knee)
                        h = knee + (cap - knee) * Tanh((h - knee) / (cap - knee));

                    for (var p = 0; p < peaks.Count; p++)
                    {
                        var pk = peaks[p];
                        var dx = x - pk.x;
                        var dz = z - pk.y;
                        h += pk.z * Mathf.Exp(-(dx * dx + dz * dz) / (pk.w * pk.w));
                    }

                    Heights[index] = Mathf.Min(h, Layout.MaxHeight - 3f);
                    // Dere koridorunun tabanı su seviyesinin belirgin üstünde kalsın.
                    if (dr < 90f)
                        Heights[index] = Mathf.Max(Heights[index], w + 2.6f);
                }
            }
        }

        // ================================================================== Bölgeler / köprüler

        private void ApplyLocationFlatten()
        {
            var w = Layout.WaterLevel;
            for (var li = 0; li < Layout.Locations.Count; li++)
            {
                var l = Layout.Locations[li];
                if (l == null || l.FlattenRadius <= 0f)
                    continue;

                var flatten = l.FlattenRadius;
                var target = l.TargetHeight >= 0f ? l.TargetHeight : ComputeLocationTarget(l);
                target = Mathf.Clamp(target, w + 2.5f, Layout.MaxHeight - 4f);
                l.TargetHeight = target;

                var blend = Mathf.Max(24f, flatten * 0.7f);
                var reach = flatten + blend;
                var center = l.Center;
                ForEachInRect(center.x - reach, center.y - reach, center.x + reach, center.y + reach, (i, x, z) =>
                {
                    var d = Vector2.Distance(new Vector2(x, z), center);
                    if (d >= reach)
                        return;
                    // Hafif düzensiz kenar (doğal görünüm).
                    var wobble = 4f * _noise.Perlin(x / 23f + li * 3.3f, z / 23f - li * 1.7f);
                    var weight = 1f - TerrainNoise.SmoothStep(flatten, reach, d + wobble);
                    if (weight <= 0f)
                        return;
                    Heights[i] = Mathf.Lerp(Heights[i], target, weight);
                    FlattenWeight[i] = Mathf.Max(FlattenWeight[i], weight);
                });
            }
        }

        private float ComputeLocationTarget(LocationSpec l)
        {
            var w = Layout.WaterLevel;
            if (l.Kind == LocationKind.Dam)
                return w + 7f;

            var radius = Mathf.Max(6f, l.FlattenRadius * 0.75f);
            float sum = 0f, max = float.MinValue;
            var count = 0;
            ForEachInRect(l.Center.x - radius, l.Center.y - radius, l.Center.x + radius, l.Center.y + radius, (i, x, z) =>
            {
                if (Vector2.Distance(new Vector2(x, z), l.Center) > radius)
                    return;
                sum += Heights[i];
                max = Mathf.Max(max, Heights[i]);
                count++;
            });

            if (count == 0)
                return SampleHeight(l.Center.x, l.Center.y);

            var avg = sum / count;
            switch (l.Kind)
            {
                case LocationKind.RelayHill:
                    return max - 2f;
                case LocationKind.Quarry:
                    return avg - 5f;
                case LocationKind.Karakol:
                case LocationKind.Outpost:
                    return Mathf.Lerp(avg, max, 0.25f);
                default:
                    return avg;
            }
        }

        private void ApplyBridgePads()
        {
            var w = Layout.WaterLevel;
            for (var b = 0; b < Layout.Bridges.Count; b++)
            {
                var bridge = Layout.Bridges[b];
                if (bridge == null)
                    continue;

                // Tabliye: çevredeki vadi tabanının ortalaması, su seviyesinin en az 5 m üstü.
                float sum = 0f;
                var count = 0;
                var c = bridge.Center;
                ForEachInRect(c.x - 30f, c.y - 30f, c.x + 30f, c.y + 30f, (i, x, z) =>
                {
                    var d = Vector2.Distance(new Vector2(x, z), c);
                    if (d < 14f || d > 30f)
                        return;
                    sum += Heights[i];
                    count++;
                });
                var deck = count > 0 ? sum / count : w + 6f;
                deck = Mathf.Clamp(deck, w + 5f, w + 9f);
                bridge.DeckHeight = deck;

                // Uzunluk: kıyı şevlerinin tabliye yüksekliğine ulaştığı yerden yerden + 4 m dayanak.
                var river = Layout.Rivers.Count > 0 ? Layout.Rivers[0] : null;
                var inner = river != null ? river.Width * 0.3f : 4f;
                var bed = w - (river != null ? river.Depth : 1.5f);
                var halfSpan = inner + (deck - bed) / RiverBankSlope;
                var sin = 1f;
                if (river != null)
                {
                    var riverDir = RiverDirectionAt(c);
                    sin = Mathf.Max(0.5f, Mathf.Abs(bridge.Direction.x * riverDir.y - bridge.Direction.y * riverDir.x));
                }

                bridge.Length = Mathf.Min(2f * halfSpan / sin + 8f, 70f);

                var flatten = bridge.Length * 0.5f + 4f;
                var reach = flatten + 18f;
                ForEachInRect(c.x - reach, c.y - reach, c.x + reach, c.y + reach, (i, x, z) =>
                {
                    var d = Vector2.Distance(new Vector2(x, z), c);
                    var weight = 1f - TerrainNoise.SmoothStep(flatten, reach, d);
                    if (weight <= 0f)
                        return;
                    Heights[i] = Mathf.Lerp(Heights[i], deck, weight);
                    FlattenWeight[i] = Mathf.Max(FlattenWeight[i], weight);
                });
            }
        }

        private Vector2 RiverDirectionAt(Vector2 p)
        {
            var best = float.MaxValue;
            var dir = Vector2.up;
            for (var s = 0; s < _segCount; s++)
            {
                var d = TerrainNoise.SegmentDistance(p.x, p.y, _segAx[s], _segAz[s], _segBx[s], _segBz[s], out _);
                if (d < best)
                {
                    best = d;
                    dir = new Vector2(_segBx[s] - _segAx[s], _segBz[s] - _segAz[s]).normalized;
                }
            }

            return dir;
        }

        // ================================================================== Yollar

        private void BuildRoads()
        {
            for (var i = 0; i < RoadEdge.Length; i++)
            {
                RoadEdge[i] = float.PositiveInfinity;
                RoadHeight[i] = 0f;
            }

            for (var r = 0; r < Layout.Roads.Count; r++)
            {
                var road = Layout.Roads[r];
                if (road?.Points == null || road.Points.Count < 2)
                {
                    RoadProfiles.Add(new Vector3[0]);
                    continue;
                }

                var profile = ComputeRoadProfile(road);
                RoadProfiles.Add(profile);
                RasterizeRoad(road, profile);
            }

            // Yol yüksekliğini araziye uygula (şev genişliği yükseklik farkıyla büyür → şev ≤ ~32°).
            for (var i = 0; i < Heights.Length; i++)
            {
                var edge = RoadEdge[i];
                if (float.IsInfinity(edge))
                    continue;

                var roadH = RoadHeight[i];
                var delta = Mathf.Abs(Heights[i] - roadH);
                var shoulder = Mathf.Min(RoadInfluence - 1f, 2.5f + 1.6f * delta);
                var weight = 1f - TerrainNoise.SmoothStep(0f, shoulder, edge);
                if (weight > 0f)
                    Heights[i] = Mathf.Lerp(Heights[i], roadH, weight);
            }
        }

        private Vector3[] ComputeRoadProfile(RoadSpec road)
        {
            // 4 m aralıklı örnekler.
            var pts = new List<Vector2>(road.Points.Count * 2);
            for (var i = 0; i + 1 < road.Points.Count; i++)
            {
                var a = road.Points[i];
                var b = road.Points[i + 1];
                var steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 4f));
                for (var s = 0; s < steps; s++)
                    pts.Add(Vector2.Lerp(a, b, s / (float)steps));
            }

            pts.Add(road.Points[road.Points.Count - 1]);
            var n = pts.Count;
            var h = new float[n];
            var pinned = new bool[n];
            var dist = new float[n];
            for (var i = 0; i < n; i++)
            {
                h[i] = SampleHeight(pts[i].x, pts[i].y);
                dist[i] = i == 0 ? 0f : dist[i - 1] + Vector2.Distance(pts[i - 1], pts[i]);
            }

            // Yumuşatma (±24 m hareketli ortalama, iki geçiş).
            var tmp = new float[n];
            for (var pass = 0; pass < 2; pass++)
            {
                for (var i = 0; i < n; i++)
                {
                    float sum = 0f;
                    var count = 0;
                    for (var k = Mathf.Max(0, i - 6); k <= Mathf.Min(n - 1, i + 6); k++)
                    {
                        sum += h[k];
                        count++;
                    }

                    tmp[i] = sum / count;
                }

                System.Array.Copy(tmp, h, n);
            }

            // Sabitler: bölge pedleri, köprüler, başka yola bağlantı noktaları.
            for (var i = 0; i < n; i++)
            {
                var p = pts[i];
                for (var l = 0; l < Layout.Locations.Count; l++)
                {
                    var loc = Layout.Locations[l];
                    if (loc == null || loc.FlattenRadius <= 0f || loc.TargetHeight < 0f)
                        continue;
                    var d = Vector2.Distance(p, loc.Center);
                    var weight = 1f - TerrainNoise.SmoothStep(loc.FlattenRadius * 0.85f, loc.FlattenRadius + 10f, d);
                    if (weight <= 0f)
                        continue;
                    h[i] = Mathf.Lerp(h[i], loc.TargetHeight, weight);
                    if (weight > 0.98f)
                        pinned[i] = true;
                }

                for (var b = 0; b < Layout.Bridges.Count; b++)
                {
                    var bridge = Layout.Bridges[b];
                    if (bridge == null || bridge.DeckHeight < 0f)
                        continue;
                    if (Vector2.Distance(p, bridge.Center) <= bridge.Length * 0.5f + 3f)
                    {
                        h[i] = bridge.DeckHeight;
                        pinned[i] = true;
                    }
                }
            }

            // Uçlar başka bir yolun üstündeyse o yolun yüksekliğine bağla (kavşak).
            PinToExistingRoad(pts, h, pinned, 0);
            PinToExistingRoad(pts, h, pinned, n - 1);

            // Eğim sınırı: sabitler arası gereken eğim %15'i aşarsa o yol için sınır yükseltilir.
            var grade = MaxRoadGrade;
            var lastPin = -1;
            for (var i = 0; i < n; i++)
            {
                if (!pinned[i])
                    continue;
                if (lastPin >= 0 && dist[i] - dist[lastPin] > 0.5f)
                    grade = Mathf.Max(grade, Mathf.Abs(h[i] - h[lastPin]) / (dist[i] - dist[lastPin]) * 1.03f);
                lastPin = i;
            }

            for (var iter = 0; iter < 4; iter++)
            {
                for (var i = 1; i < n; i++)
                {
                    if (pinned[i])
                        continue;
                    var maxStep = grade * (dist[i] - dist[i - 1]);
                    h[i] = Mathf.Clamp(h[i], h[i - 1] - maxStep, h[i - 1] + maxStep);
                }

                for (var i = n - 2; i >= 0; i--)
                {
                    if (pinned[i])
                        continue;
                    var maxStep = grade * (dist[i + 1] - dist[i]);
                    h[i] = Mathf.Clamp(h[i], h[i + 1] - maxStep, h[i + 1] + maxStep);
                }
            }

            var result = new Vector3[n];
            for (var i = 0; i < n; i++)
                result[i] = new Vector3(pts[i].x, h[i], pts[i].y);
            return result;
        }

        private void PinToExistingRoad(List<Vector2> pts, float[] h, bool[] pinned, int index)
        {
            var p = pts[index];
            var cell = NearestIndex(p.x, p.y);
            if (RoadEdge[cell] < 1.5f)
            {
                h[index] = RoadHeight[cell];
                pinned[index] = true;
            }
        }

        private void RasterizeRoad(RoadSpec road, Vector3[] profile)
        {
            var half = Mathf.Max(1f, road.Width * 0.5f);
            var kind = road.Kind == RoadKind.Asphalt ? (byte)1 : (byte)2;
            var reach = half + RoadInfluence;
            for (var s = 0; s + 1 < profile.Length; s++)
            {
                var a = profile[s];
                var b = profile[s + 1];
                var minX = Mathf.Min(a.x, b.x) - reach;
                var maxX = Mathf.Max(a.x, b.x) + reach;
                var minZ = Mathf.Min(a.z, b.z) - reach;
                var maxZ = Mathf.Max(a.z, b.z) + reach;
                ForEachInRect(minX, minZ, maxX, maxZ, (i, x, z) =>
                {
                    var d = TerrainNoise.SegmentDistance(x, z, a.x, a.z, b.x, b.z, out var t);
                    var edge = d - half;
                    if (edge >= RoadInfluence || edge >= RoadEdge[i])
                        return;
                    RoadEdge[i] = edge;
                    RoadHeight[i] = Mathf.Lerp(a.y, b.y, t);
                    RoadKindMap[i] = kind;
                });
            }
        }

        // ================================================================== Son işlem

        private void FinalizeHeights()
        {
            KeepRelayHillHighest();

            var max = 0f;
            var top = Layout.MaxHeight - 1f;
            for (var i = 0; i < Heights.Length; i++)
            {
                var h = Mathf.Clamp(Heights[i], 0.5f, top);
                Heights[i] = h;
                if (h > max)
                    max = h;
            }

            MaxTerrainHeight = max;
        }

        /// <summary>
        /// Röle Tepesi haritanın en yüksek noktası kalsın: tepe çevresi dışında, tepe düzlüğünün 3 m altını aşan sırtlar yumuşakça
        /// bastırılır (tanh dizi; kar çizgisi ve sırt silueti korunur).
        /// </summary>
        private void KeepRelayHillHighest()
        {
            LocationSpec relay = null;
            for (var i = 0; i < Layout.Locations.Count; i++)
            {
                if (Layout.Locations[i] != null && Layout.Locations[i].Kind == LocationKind.RelayHill)
                {
                    relay = Layout.Locations[i];
                    break;
                }
            }

            if (relay == null || relay.TargetHeight <= 0f)
                return;

            const float band = 8f;
            var limit = relay.TargetHeight - 3f;
            var knee = limit - band;
            var protect = Mathf.Max(relay.Radius, relay.FlattenRadius) * 1.5f;
            var inner = protect * 0.6f;
            var res = Resolution;
            for (var iz = 0; iz < res; iz++)
            {
                var dz = WorldZ(iz) - relay.Center.y;
                for (var ix = 0; ix < res; ix++)
                {
                    var i = iz * res + ix;
                    var h = Heights[i];
                    if (h <= knee)
                        continue;

                    var dx = WorldX(ix) - relay.Center.x;
                    var d = Mathf.Sqrt(dx * dx + dz * dz);
                    var t = TerrainNoise.SmoothStep(inner, protect, d);
                    if (t <= 0f)
                        continue;

                    var compressed = knee + band * Tanh((h - knee) / band);
                    Heights[i] = Mathf.Lerp(h, compressed, t);
                }
            }
        }

        private delegate void CellAction(int index, float x, float z);

        private void ForEachInRect(float minX, float minZ, float maxX, float maxZ, CellAction action)
        {
            var x0 = Mathf.Max(0, Mathf.FloorToInt((minX - OriginX) / CellSize));
            var x1 = Mathf.Min(Resolution - 1, Mathf.CeilToInt((maxX - OriginX) / CellSize));
            var z0 = Mathf.Max(0, Mathf.FloorToInt((minZ - OriginZ) / CellSize));
            var z1 = Mathf.Min(Resolution - 1, Mathf.CeilToInt((maxZ - OriginZ) / CellSize));
            for (var iz = z0; iz <= z1; iz++)
            {
                var z = WorldZ(iz);
                var row = iz * Resolution;
                for (var ix = x0; ix <= x1; ix++)
                    action(row + ix, WorldX(ix), z);
            }
        }

        private static float Tanh(float x)
        {
            if (x > 9f)
                return 1f;
            if (x < -9f)
                return -1f;
            var e = Mathf.Exp(2f * x);
            return (e - 1f) / (e + 1f);
        }
    }
}
