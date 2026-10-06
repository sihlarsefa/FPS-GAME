using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Rendering;
using UnityEngine;
using M = Project.Infrastructure.Rendering.MaterialId;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Prosedürel bina üreticisi (HAREKÂT — düşük poligonlu Anadolu / askerî yapılar).
    /// <para>Sözleşme:</para>
    /// <list type="bullet">
    /// <item><b>spec.Position</b>: binanın zemin merkezi (dünya). Y, ayak izi altındaki en yüksek arazi noktası olmalıdır;
    /// temeller 2 m aşağı iner (eğimli zeminde açıkta kalan kısım kaide olarak görünür). Zemin kat döşemesi Position.y + 0.2 m'dir.</item>
    /// <item><b>spec.Yaw</b>: binanın baktığı yön (derece). Bina yerel +Z yönüne bakar; ana kapı +Z (ön) cephededir.</item>
    /// <item><b>Width</b> yerel X, <b>Depth</b> yerel Z boyutudur; her stil kendi makul aralığına sıkıştırır (ör. Hangar ≥ 16 m).</item>
    /// <item>Gerçek kapı (1.4 × 2.3 m) ve pencere boşlukları, ara döşemeler, ≤ 33° rampa merdivenler (çarpışmada düz rampa,
    /// görselde basamak), erişilebilir düz çatılar (parapet/korkuluk), iç bölmeler (kapı boşluklu), siper eşyaları,
    /// kat başına ganimet noktaları (BuildingResult.LootPoints, dünya uzayı).</item>
    /// <item>Tüm parçalar Default katmanında, MaterialLibrary malzemeleriyle; aynı malzemedeki parçalar tek ağda birleştirilir
    /// (bina başına ≈ 8–16 nesne). Eksene hizalı çarpıştırıcılar malzeme nesnesinde, eğik olanlar "Egim" alt nesnelerinde.</item>
    /// <item>Aynı spec (Seed dahil) her zaman aynı binayı üretir. Ruined → kırık duvarlar, çatı yok, moloz.</item>
    /// </list>
    /// </summary>
    public static partial class BuildingGenerator
    {
        /// <summary>Zemin kat döşeme üst yüzünün Position.y üzerindeki yüksekliği (m).</summary>
        public const float GroundFloorOffset = 0.2f;

        private const float Y0 = GroundFloorOffset;
        private const float Fin = 0.05f;
        private const float Yb = Y0 - Fin;
        private const float SlabT = 0.22f;
        private const float StairW = 1.2f;
        private const float Approach = 1.0f;
        private const float Landing = 1.15f;
        private const float BalT = 0.12f;
        private const float AccessDepth = 1.0f;
        private const float HeadRoom = 2.05f;
        private const float InnerDoorW = 1.3f;
        private const float InnerDoorH = 2.2f;
        private const float RailH = 1.0f;
        private const float PartitionT = 0.15f;

        // Özel engel seviyeleri (kat dışı platformlar)
        private const int LevelLoft = 10;
        private const int LevelOffice = 20;
        private const int LevelMezzanine = 21;
        private const int LevelPlatform = 30;

        private static StructureBuilder _builder;
        private static readonly List<Vector2> Poly = new List<Vector2>(8);
        private static readonly List<WallOpening> TmpOpenings = new List<WallOpening>(16);
        private static readonly List<Rect> Rooms = new List<Rect>(8);
        private static readonly List<float> Xs = new List<float>(8);
        private static readonly List<Vector2> Spans = new List<Vector2>(8);
        private static readonly List<Vector3> Points = new List<Vector3>(8);

        private static readonly int[][] HipFaces =
        {
            new[] { 0, 1, 2, 3 }, new[] { 0, 1, 5, 4 }, new[] { 3, 2, 5, 4 }, new[] { 1, 2, 5 }, new[] { 0, 3, 4 }
        };

        private static readonly int[] CoreOrder = { 0, 2, 1, 3, 4, 6, 5, 7 };

        /// <summary>Spec'e göre binayı <paramref name="parent"/> altında üretir. spec null ise varsayılan köy evi.</summary>
        public static BuildingResult Build(BuildingSpec spec, Transform parent)
        {
            spec ??= new BuildingSpec();
            _builder ??= new StructureBuilder();
            _builder.Clear();
            var ctx = new Ctx(spec, _builder);
            try
            {
                Generate(ctx);
            }
            catch (Exception e)
            {
                Debug.LogError("[BuildingGenerator] '" + spec.Name + "' (" + spec.Style + ") üretilirken hata: " + e);
            }

            return Finish(ctx, parent);
        }

        private static void Generate(Ctx c)
        {
            switch (c.Spec.Style)
            {
                case BuildingStyle.VillageHouse: VillageHouse(c); break;
                case BuildingStyle.TwoStoryHouse: TwoStoryHouse(c); break;
                case BuildingStyle.Mosque: Mosque(c); break;
                case BuildingStyle.Shop: Shop(c); break;
                case BuildingStyle.Barracks: Barracks(c); break;
                case BuildingStyle.Karakol: Karakol(c); break;
                case BuildingStyle.WatchTower: WatchTower(c); break;
                case BuildingStyle.Hangar: Hangar(c); break;
                case BuildingStyle.Warehouse: Warehouse(c); break;
                case BuildingStyle.FactoryHall: FactoryHall(c); break;
                case BuildingStyle.Barn: Barn(c); break;
                case BuildingStyle.Bunker: Bunker(c); break;
                case BuildingStyle.RadarStation: RadarStation(c); break;
                case BuildingStyle.Shed: Shed(c); break;
                case BuildingStyle.DamControl: DamControl(c); break;
                case BuildingStyle.ShepherdHut: ShepherdHut(c); break;
                default: VillageHouse(c); break;
            }
        }

        private static BuildingResult Finish(Ctx c, Transform parent)
        {
            var spec = c.Spec;
            var name = string.IsNullOrEmpty(spec.Name) ? c.DefaultName : spec.Name;
            var root = StructureKit.CreateGroup(parent, name, Vector3.zero, Quaternion.identity);
            var t = root.transform;
            t.SetPositionAndRotation(spec.Position, Quaternion.Euler(0f, spec.Yaw, 0f));
            c.B.BuildInto(t);
            StructureKit.MarkStatic(root);
            BuildInteriorJobs(c, t);
            for (var i = 0; i < c.Panes.Count; i++)
            {
                var pane = StructureKit.CreateBox(t, "Cam" + i, c.Panes[i].Key, c.Panes[i].Value, Quaternion.identity, M.Glass);
                Project.Infrastructure.Combat.Destructible.Mark(pane, Project.Application.Services.DestructibleKind.Glass, M.Glass);
            }

            if (c.PowerDrop.HasValue)
            {
                var drop = new GameObject(BuildingWeathering.PowerDropName);
                drop.transform.SetParent(t, false);
                drop.transform.localPosition = c.PowerDrop.Value;
            }

            AddReverbZone(t, spec.Style, c.B.HasGeometry ? c.B.LocalBounds : new Bounds(Vector3.up, Vector3.one * 2f));

            var result = new BuildingResult { Root = root };
            var local = c.B.HasGeometry ? c.B.LocalBounds : new Bounds(Vector3.up, Vector3.one * 2f);
            result.Bounds = TransformBounds(t, local);
            for (var i = 0; i < c.Loot.Count; i++)
                result.LootPoints.Add(t.TransformPoint(c.Loot[i]));
            return result;
        }

        /// <summary>Bina içinde duyulan yankı (iç mekan reverb'ü): bina boyutuna ve türüne göre AudioReverbZone.</summary>
        private static void AddReverbZone(Transform root, BuildingStyle style, Bounds local)
        {
            try
            {
                var preset = AudioReverbPreset.Room;
                switch (style)
                {
                    case BuildingStyle.Hangar:
                    case BuildingStyle.FactoryHall:
                    case BuildingStyle.Warehouse: preset = AudioReverbPreset.Hangar; break;
                    case BuildingStyle.Mosque: preset = AudioReverbPreset.Auditorium; break;
                    case BuildingStyle.Bunker:
                    case BuildingStyle.DamControl: preset = AudioReverbPreset.Stoneroom; break;
                    case BuildingStyle.Barracks:
                    case BuildingStyle.Karakol: preset = AudioReverbPreset.Hallway; break;
                    case BuildingStyle.Barn: preset = AudioReverbPreset.Generic; break;
                    case BuildingStyle.VillageHouse:
                    case BuildingStyle.TwoStoryHouse:
                    case BuildingStyle.ShepherdHut: preset = AudioReverbPreset.Livingroom; break;
                }

                var go = new GameObject("IcMekanYanki");
                go.transform.SetParent(root, false);
                go.transform.localPosition = local.center;
                var zone = go.AddComponent<AudioReverbZone>();
                zone.reverbPreset = preset;
                var minExtent = Mathf.Min(local.size.x, local.size.z);
                var maxExtent = Mathf.Max(local.size.x, local.size.z);
                zone.minDistance = Mathf.Max(1.5f, minExtent * 0.5f - 0.5f);
                zone.maxDistance = Mathf.Max(zone.minDistance + 1f, maxExtent * 0.5f + 1.5f);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BuildingGenerator] Yankı bölgesi eklenemedi: " + e.Message);
            }
        }

        // =====================================================================================================
        //  Bağlam ve tasarım tipleri
        // =====================================================================================================

        private enum RoofKind { Flat, Gable, Hip, Shed, None }

        private enum RoofAccess { None, Interior, Exterior }

        private enum OpeningKind { Door, Window, Big, Slit, Niche, Gap, Notch }

        private enum PropTheme { None, Village, Residential, Shop, Military, Office, Industrial, Farm, Bunker, Hut }

        private enum PropKind
        {
            Crate, AmmoCrates, Table, Sedir, Stove, Wardrobe, Bed, BunkBed, Lockers, Desk, Shelf, Counter, Barrels, Machine,
            HayBales, Console, ServerRack, Sandbags, Trough, StrawBed, Hearth
        }

        private readonly struct Area
        {
            public readonly int Level;
            public readonly Rect Rect;

            public Area(int level, Rect rect)
            {
                Level = level;
                Rect = rect;
            }
        }

        private sealed class Ctx
        {
            public readonly BuildingSpec Spec;
            public readonly StructureBuilder B;
            public readonly System.Random Rng;
            public readonly bool Ruined;
            public readonly List<Vector3> Loot = new List<Vector3>(24);
            public readonly List<Area> Obstacles = new List<Area>(64);
            /// <summary>Kırılabilir cam paneller (yerel merkez, boyut). Finish'te ayrı nesne olarak üretilir.</summary>
            public readonly List<KeyValuePair<Vector3, Vector3>> Panes = new List<KeyValuePair<Vector3, Vector3>>(16);
            public string DefaultName = "Bina";
            /// <summary>Elektrik giriş noktası (yerel). BuildingWeathering.BuildPowerLines 'ElektrikGirisi' çocuğunu okur.</summary>
            public Vector3? PowerDrop;
            /// <summary>Baca konumu (yerel xz) - anten çakışmasını önler.</summary>
            public Vector3? ChimneyPos;
            public int WeatherPass;
            /// <summary>İç mekan işleri (InteriorFurnisher / ışık huzmesi / pervaz): Finish'te kök altında kurulur.</summary>
            public readonly List<InteriorJob> Interiors = new List<InteriorJob>(12);
            public readonly List<ShaftJob> Shafts = new List<ShaftJob>(8);
            public readonly List<SillJob> Sills = new List<SillJob>(8);

            public Ctx(BuildingSpec spec, StructureBuilder builder)
            {
                Spec = spec;
                B = builder;
                Ruined = spec.Ruined;
                Rng = new System.Random(unchecked(spec.Seed * 92821 + ((int)spec.Style + 1) * 7919 + 31));
            }

            public float Range(float a, float b) => a + (b - a) * (float)Rng.NextDouble();

            public bool Chance(float p) => Rng.NextDouble() < p;

            public int Int(int min, int maxExclusive) => maxExclusive <= min ? min : Rng.Next(min, maxExclusive);

            public M Pick(M a, M b) => Rng.NextDouble() < 0.5 ? a : b;

            public M Pick(M a, M b, M c)
            {
                var r = Rng.Next(3);
                return r == 0 ? a : r == 1 ? b : c;
            }

            public void Block(int level, Rect rect)
            {
                if (rect.width > 0.001f && rect.height > 0.001f)
                    Obstacles.Add(new Area(level, rect));
            }

            public bool IsFree(int level, Rect rect, float pad = 0f)
            {
                if (pad != 0f)
                    rect = Expand(rect, pad);
                for (var i = 0; i < Obstacles.Count; i++)
                {
                    var o = Obstacles[i];
                    if (o.Level == level && Overlaps(o.Rect, rect))
                        return false;
                }

                return true;
            }

            public bool LootNear(Vector3 p, float minDistance)
            {
                var sq = minDistance * minDistance;
                for (var i = 0; i < Loot.Count; i++)
                {
                    if ((Loot[i] - p).sqrMagnitude < sq)
                        return true;
                }

                return false;
            }
        }

        private sealed class WindowRow
        {
            public float Width = 0.9f;
            public float Height = 1.1f;
            public float Sill = 1f;
            public float Spacing = 2.6f;
            public float Skip;
            public float Margin = 0.5f;
            public int SideMask = 15;
            public int MinLevel;
            public int MaxLevel = 99;
            public OpeningKind Kind = OpeningKind.Window;
        }

        private sealed class BoxDesign
        {
            public float Width = 8f, Depth = 7f, FloorHeight = 3.1f, Wall = 0.3f;
            public int Floors = 1;
            public M WallMat = M.Plaster, FoundationMat = M.StoneDark, FloorMat = M.Wood, SlabMat = M.Concrete, CeilingMat = M.Plaster,
                RoofMat = M.Concrete, TrimMat = M.Concrete, FrameMat = M.WoodDark, PartitionMat = M.Plaster, StairMat = M.Concrete,
                RailMat = M.MetalDark;
            public bool HasLowerWall;
            public M LowerWallMat = M.StoneDark;
            public float LowerWallHeight = 1.2f;
            public RoofKind Roof = RoofKind.Flat;
            public float RoofThickness = 0.3f, CeilingThickness = 0.15f;
            public bool Ceiling = true;
            public float Parapet = 0.5f;
            public bool Coping = true;
            public RoofAccess RoofAccess = RoofAccess.None;
            public bool ExtStairOpen;
            public float RoofPitch = 25f, Overhang = 0.4f;
            public bool AutoRidge = true, RidgeAlongX = true;
            public readonly List<WindowRow> Rows = new List<WindowRow>(2);
            public bool WindowFrames = true, Lintels, Shutters;
            public float ShutterChance = 0.4f;
            public int FrontDoors = 1, SideDoors;
            public bool BackDoor, CenterDoor;
            public float DoorWidth = StructureKit.DoorWidth, DoorHeight = StructureKit.DoorHeight;
            public float BigWidth, BigHeight, BigSill;
            public bool BigShutterBox;
            public bool DoorSteps = true;
            public float RoomWidth;
            public PropTheme Props = PropTheme.None, PropsUpper = PropTheme.None;
            public float PropArea = 10f;
            public int MaxProps = 4;
            public float LootArea = 14f;
            public int MaxLoot = 3;
            public float RoofLootChance = 0.5f;
            public float RuinAmount = 0.55f;
            public bool Kilim, BeamEnds;
            public Action<Ctx, BoxPlan> OnPlanStart, OnBeforeWindows;

            public WindowRow AddRow(float width, float height, float sill, float spacing)
            {
                var row = new WindowRow { Width = width, Height = height, Sill = sill, Spacing = spacing };
                Rows.Add(row);
                return row;
            }
        }

        private struct Reserve
        {
            public int Level;
            public float A, B;
            public bool Window;
        }

        private struct OpeningMeta
        {
            public OpeningKind Kind;
            public int Level;
        }

        /// <summary>Dış duvar. u = duvar başlangıcından ölçülen mesafe (ön/arka: x + W/2, yanlar: z - Zi0).</summary>
        private sealed class Facade
        {
            public int Side;            // 0 ön (+Z), 1 arka (-Z), 2 sağ (+X), 3 sol (-X)
            public bool AlongX;
            public float Plane;
            public Vector3 Outward;
            public Vector3 Start, End;
            public float Length, Offset;
            public readonly List<WallOpening> Openings = new List<WallOpening>(16);
            public readonly List<OpeningMeta> Meta = new List<OpeningMeta>(16);
            public readonly List<Reserve> Reserved = new List<Reserve>(16);

            public float ToU(float coord) => coord + Offset;

            public float ToCoord(float u) => u - Offset;

            public Vector3 Point(float u, float y) => AlongX ? new Vector3(u - Offset, y, Plane) : new Vector3(Plane, y, u - Offset);

            public void Reserve(int level, float a, float b, bool window = false)
                => Reserved.Add(new Reserve { Level = level, A = Mathf.Min(a, b), B = Mathf.Max(a, b), Window = window });

            public bool IsReserved(int level, float a, float b, bool includeWindows = true)
            {
                for (var i = 0; i < Reserved.Count; i++)
                {
                    var r = Reserved[i];
                    if (!includeWindows && r.Window)
                        continue;
                    if (r.Level >= 0 && level >= 0 && r.Level != level)
                        continue;
                    if (a < r.B && r.A < b)
                        return true;
                }

                return false;
            }

            public void Add(WallOpening opening, OpeningKind kind, int level)
            {
                Openings.Add(opening);
                Meta.Add(new OpeningMeta { Kind = kind, Level = level });
            }
        }

        /// <summary>
        /// U-dönüşlü merdiven çekirdeği (kat k → k+1). O: köşe (u=0, v=0), U: kol yönü, V: duvardan içeri.
        /// Şerit 1 (v ∈ [0, Sw]) duvar dibinde yukarı çıkar, ara sahanlık uzak uçta, şerit 2 geri döner.
        /// </summary>
        private sealed class Core
        {
            public int Level;
            public Vector3 O, U, V;
            public float A, R, L, Sw, Rise, Hc;

            public float Len => A + R + L + BalT;
            public float Wid => 2f * Sw + BalT;

            public Vector3 P(float u, float v, float y)
            {
                var q = O + U * u + V * v;
                q.y = y;
                return q;
            }

            public Rect UV(float u0, float v0, float u1, float v1)
            {
                var a = O + U * u0 + V * v0;
                var b = O + U * u1 + V * v1;
                return MinMax(a.x, a.z, b.x, b.z);
            }

            public Rect Footprint => UV(0f, 0f, Len, Wid);
            public Rect Access => UV(0f, Wid, A, Wid + AccessDepth);
        }

        private struct Partition
        {
            public int Level;
            public float X;
            public float DoorZ;
        }

        private struct ExtStair
        {
            public bool Valid;
            public bool Open;
            public int Side;
            public float UStart, UL0, UL1, Run, Rise, Yaw;
            public Vector3 Bottom;
        }

        private sealed class BoxPlan
        {
            public BoxDesign D;
            public float W, Dp, T, FH;
            public int Floors;
            public float Xi0, Xi1, Zi0, Zi1;
            public bool HasRoof, Flat, RoofWalkable;
            public float RoofRise;
            public readonly Facade[] F = new Facade[4];
            public readonly List<Core> Cores = new List<Core>(3);
            public readonly List<Partition> Partitions = new List<Partition>(8);
            public readonly List<Rect>[] Holes = new List<Rect>[8];
            public readonly List<Vector3> RailGaps = new List<Vector3>(2);
            public ExtStair Ext;

            public float TopY => Y0 + Floors * FH;
            public float RoofT => Flat ? D.RoofThickness : (D.Ceiling ? D.CeilingThickness : 0f);

            public float WallTop
            {
                get
                {
                    if (!HasRoof || !Flat)
                        return TopY;
                    return D.Parapet > 0.01f ? TopY + D.Parapet : TopY - D.RoofThickness;
                }
            }

            public float LevelY(int k) => Y0 + k * FH;
            public float ThickAbove(int k) => k + 1 < Floors ? SlabT : (HasRoof ? RoofT : 0.3f);
            public float CeilingY(int k) => LevelY(k + 1) - ThickAbove(k);
            public Rect Interior => Rect.MinMaxRect(Xi0, Zi0, Xi1, Zi1);

            public List<Rect> HolesAt(int level)
            {
                if (level < 0 || level >= Holes.Length)
                    return null;
                return Holes[level] ??= new List<Rect>(4);
            }
        }

        // =====================================================================================================
        //  Kutu bina planlaması
        // =====================================================================================================

        private static BoxPlan StandardBox(Ctx c, BoxDesign d)
        {
            var p = PlanBox(c, d);
            d.OnBeforeWindows?.Invoke(c, p);
            PlanWindows(c, p);
            BuildShell(c, p);
            return p;
        }

        private static BoxPlan PlanBox(Ctx c, BoxDesign d)
        {
            var p = new BoxPlan { D = d };
            p.W = Mathf.Max(2f, d.Width);
            p.Dp = Mathf.Max(2f, d.Depth);
            p.T = Mathf.Clamp(d.Wall, 0.08f, Mathf.Min(p.W, p.Dp) * 0.15f);
            p.FH = Mathf.Max(2.6f, d.FloorHeight);
            p.Floors = Mathf.Clamp(d.Floors, 1, 4);
            p.Xi0 = -p.W * 0.5f + p.T;
            p.Xi1 = p.W * 0.5f - p.T;
            p.Zi0 = -p.Dp * 0.5f + p.T;
            p.Zi1 = p.Dp * 0.5f - p.T;
            p.HasRoof = !c.Ruined && d.Roof != RoofKind.None;
            p.Flat = p.HasRoof && d.Roof == RoofKind.Flat;
            InitFacades(p);

            d.OnPlanStart?.Invoke(c, p);
            PlanFrontOpenings(c, p);

            for (var k = 0; k + 1 < p.Floors; k++)
            {
                if (!TryPlaceCore(c, p, k))
                {
                    p.Floors = k + 1;
                    break;
                }
            }

            if (p.Flat && d.RoofAccess != RoofAccess.None)
            {
                if (d.RoofAccess == RoofAccess.Exterior && TryPlanExteriorStair(c, p))
                    p.RoofWalkable = true;
                else if (TryPlaceCore(c, p, p.Floors - 1))
                    p.RoofWalkable = true;
            }

            PlanOtherDoors(c, p);
            PlanPartitions(c, p);

            if (c.Ruined)
                PlanRuinHoles(c, p);
            return p;
        }

        private static void InitFacades(BoxPlan p)
        {
            var hw = p.W * 0.5f;
            var hd = p.Dp * 0.5f;
            var t = p.T;
            p.F[0] = new Facade
            {
                Side = 0, AlongX = true, Plane = hd - t * 0.5f, Outward = Vector3.forward,
                Start = new Vector3(-hw, 0f, hd - t * 0.5f), End = new Vector3(hw, 0f, hd - t * 0.5f), Length = p.W, Offset = hw
            };
            p.F[1] = new Facade
            {
                Side = 1, AlongX = true, Plane = -(hd - t * 0.5f), Outward = Vector3.back,
                Start = new Vector3(-hw, 0f, -(hd - t * 0.5f)), End = new Vector3(hw, 0f, -(hd - t * 0.5f)), Length = p.W, Offset = hw
            };
            p.F[2] = new Facade
            {
                Side = 2, AlongX = false, Plane = hw - t * 0.5f, Outward = Vector3.right,
                Start = new Vector3(hw - t * 0.5f, 0f, p.Zi0), End = new Vector3(hw - t * 0.5f, 0f, p.Zi1), Length = p.Zi1 - p.Zi0,
                Offset = -p.Zi0
            };
            p.F[3] = new Facade
            {
                Side = 3, AlongX = false, Plane = -(hw - t * 0.5f), Outward = Vector3.left,
                Start = new Vector3(-(hw - t * 0.5f), 0f, p.Zi0), End = new Vector3(-(hw - t * 0.5f), 0f, p.Zi1), Length = p.Zi1 - p.Zi0,
                Offset = -p.Zi0
            };
        }

        private static void AddOpening(BoxPlan p, int side, int level, float u, float width, float sill, float height, OpeningKind kind)
        {
            var bottom = p.LevelY(level) - Yb + sill;
            p.F[side].Add(new WallOpening(u, width, bottom, height), kind, level);
        }

        /// <summary>Cephe iç yüzüne bitişik, içeri doğru depth derinliğinde dikdörtgen (plan).</summary>
        private static Rect InsideRect(BoxPlan p, Facade f, float u, float along, float depth)
        {
            var coord = f.ToCoord(u);
            switch (f.Side)
            {
                case 0: return MinMax(coord - along * 0.5f, p.Zi1 - depth, coord + along * 0.5f, p.Zi1);
                case 1: return MinMax(coord - along * 0.5f, p.Zi0, coord + along * 0.5f, p.Zi0 + depth);
                case 2: return MinMax(p.Xi1 - depth, coord - along * 0.5f, p.Xi1, coord + along * 0.5f);
                default: return MinMax(p.Xi0, coord - along * 0.5f, p.Xi0 + depth, coord + along * 0.5f);
            }
        }

        private static void PlanFrontOpenings(Ctx c, BoxPlan p)
        {
            var d = p.D;
            var f = p.F[0];
            var preferred = float.NaN;
            if (d.BigWidth > 0.5f)
            {
                var doorSpace = d.FrontDoors > 0 ? d.DoorWidth + 0.8f : 0f;
                var bw = Mathf.Min(d.BigWidth, p.W - 2f * p.T - 0.9f - doorSpace);
                var bh = Mathf.Min(d.BigHeight, p.CeilingY(0) - Y0 - 0.15f - d.BigSill);
                if (bw > 0.9f && bh > 0.5f)
                {
                    float u;
                    if (d.FrontDoors > 0)
                    {
                        var total = bw + 0.7f + d.DoorWidth;
                        var start = (p.W - total) * 0.5f;
                        var bigFirst = c.Chance(0.5f);
                        u = bigFirst ? start + bw * 0.5f : start + total - bw * 0.5f;
                        preferred = bigFirst ? start + bw + 0.7f + d.DoorWidth * 0.5f : start + d.DoorWidth * 0.5f;
                    }
                    else
                    {
                        u = p.W * 0.5f;
                    }

                    AddOpening(p, 0, 0, u, bw, d.BigSill, bh, OpeningKind.Big);
                    f.Reserve(0, u - bw * 0.5f - 0.3f, u + bw * 0.5f + 0.3f);
                    c.Block(0, InsideRect(p, f, u, bw + 0.4f, d.BigSill > 0.3f ? 1.0f : 2.5f));
                }
            }

            for (var i = 0; i < d.FrontDoors; i++)
            {
                float pref;
                if (i == 0 && !float.IsNaN(preferred))
                    pref = preferred;
                else if (d.FrontDoors == 1)
                    pref = d.CenterDoor ? p.W * 0.5f : p.W * (0.5f + c.Range(-0.2f, 0.2f));
                else
                    pref = p.W * (i + 0.5f) / d.FrontDoors + c.Range(-0.5f, 0.5f);
                TryAddDoor(c, p, 0, 0, d.DoorWidth, d.DoorHeight, pref, out _);
            }
        }

        private static bool TryAddDoor(Ctx c, BoxPlan p, int side, int level, float width, float height, float preferredU, out float placedU)
        {
            placedU = float.NaN;
            var f = p.F[side];
            var inset = f.AlongX ? p.T : 0f;
            var minU = inset + 0.4f + width * 0.5f;
            var maxU = f.Length - inset - 0.4f - width * 0.5f;
            if (maxU < minU)
                return false;
            height = Mathf.Min(height, p.CeilingY(level) - p.LevelY(level) - 0.1f);
            if (height < 1.9f)
                return false;

            for (var attempt = 0; attempt < 16; attempt++)
            {
                float u;
                if (attempt == 0)
                    u = float.IsNaN(preferredU) ? (minU + maxU) * 0.5f : preferredU;
                else if (attempt < 7)
                    u = (float.IsNaN(preferredU) ? (minU + maxU) * 0.5f : preferredU) + ((attempt & 1) == 0 ? 1f : -1f) * 0.6f * ((attempt + 1) / 2);
                else
                    u = c.Range(minU, maxU);
                u = Mathf.Clamp(u, minU, maxU);
                if (f.IsReserved(level, u - width * 0.5f - 0.3f, u + width * 0.5f + 0.3f))
                    continue;
                var clear = InsideRect(p, f, u, width + 0.6f, 1.5f);
                if (!c.IsFree(level, clear))
                    continue;

                AddOpening(p, side, level, u, width, 0f, height, OpeningKind.Door);
                f.Reserve(level, u - width * 0.5f - 0.35f, u + width * 0.5f + 0.35f);
                c.Block(level, clear);
                placedU = u;
                return true;
            }

            return false;
        }

        private static void PlanOtherDoors(Ctx c, BoxPlan p)
        {
            var d = p.D;
            if (d.BackDoor)
                TryAddDoor(c, p, 1, 0, d.DoorWidth, d.DoorHeight, float.NaN, out _);
            if (d.SideDoors <= 0)
                return;

            var first = c.Chance(0.5f) ? 2 : 3;
            if (p.Ext.Valid && p.Ext.Side == first)
                first = first == 2 ? 3 : 2;
            var second = first == 2 ? 3 : 2;
            var ok = TryAddDoor(c, p, first, 0, d.DoorWidth, d.DoorHeight, float.NaN, out _);
            if (!ok || d.SideDoors > 1)
                TryAddDoor(c, p, second, 0, d.DoorWidth, d.DoorHeight, float.NaN, out _);
        }

        private static Core CoreCandidate(BoxPlan p, int index, bool flip)
        {
            var corner = index / 2;
            var alongZ = (index & 1) == 0;
            var back = corner < 2;
            var right = (corner % 2 == 0) != flip;
            var x = right ? p.Xi1 : p.Xi0;
            var z = back ? p.Zi0 : p.Zi1;
            var inX = right ? Vector3.left : Vector3.right;
            var inZ = back ? Vector3.forward : Vector3.back;
            return new Core { O = new Vector3(x, 0f, z), U = alongZ ? inZ : inX, V = alongZ ? inX : inZ };
        }

        private static bool TryPlaceCore(Ctx c, BoxPlan p, int level)
        {
            if (level < 0)
                return false;
            var rise = p.FH;
            var thick = p.ThickAbove(level);
            var flip = (level & 1) == 1;
            if (c.Chance(0.35f))
                flip = !flip;
            var interior = p.Interior;

            for (var pass = 0; pass < 2; pass++)
            {
                var angle = pass == 0 ? StructureKit.PreferredStairAngle : StructureKit.MaxStairAngle - 0.5f;
                var tan = Mathf.Tan(angle * Mathf.Deg2Rad);
                var run = rise * 0.5f / tan;
                for (var i = 0; i < CoreOrder.Length; i++)
                {
                    var core = CoreCandidate(p, CoreOrder[i], flip);
                    core.Level = level;
                    core.A = Approach;
                    core.R = run;
                    core.L = Landing;
                    core.Sw = StairW;
                    core.Rise = rise;
                    core.Hc = Mathf.Clamp((rise - thick - HeadRoom) / tan - 0.1f, 0.3f, run);
                    var fp = core.Footprint;
                    var acc = core.Access;
                    if (!Inside(fp, interior) || !Inside(acc, interior))
                        continue;
                    if (!c.IsFree(level, fp) || !c.IsFree(level, acc) || !c.IsFree(level + 1, fp) || !c.IsFree(level + 1, acc))
                        continue;

                    p.Cores.Add(core);
                    c.Block(level, fp);
                    c.Block(level, acc);
                    c.Block(level + 1, fp);
                    c.Block(level + 1, acc);
                    var holes = p.HolesAt(level + 1);
                    holes?.Add(core.UV(core.A, core.Sw, core.A + core.R + core.L, 2f * core.Sw));
                    holes?.Add(core.UV(core.A + core.Hc, 0f, core.A + core.R + core.L, core.Sw));
                    ReserveTouching(p, fp, level);
                    return true;
                }
            }

            return false;
        }

        private static void ReserveTouching(BoxPlan p, Rect r, int level)
        {
            const float e = 0.05f;
            const float pad = 0.3f;
            if (r.yMax >= p.Zi1 - e)
                p.F[0].Reserve(level, p.F[0].ToU(r.xMin) - pad, p.F[0].ToU(r.xMax) + pad);
            if (r.yMin <= p.Zi0 + e)
                p.F[1].Reserve(level, p.F[1].ToU(r.xMin) - pad, p.F[1].ToU(r.xMax) + pad);
            if (r.xMax >= p.Xi1 - e)
                p.F[2].Reserve(level, p.F[2].ToU(r.yMin) - pad, p.F[2].ToU(r.yMax) + pad);
            if (r.xMin <= p.Xi0 + e)
                p.F[3].Reserve(level, p.F[3].ToU(r.yMin) - pad, p.F[3].ToU(r.yMax) + pad);
        }

        private static bool FarEndAtWall(BoxPlan p, Core k)
        {
            var fp = k.Footprint;
            const float e = 0.05f;
            if (k.U.z > 0.5f)
                return fp.yMax >= p.Zi1 - e;
            if (k.U.z < -0.5f)
                return fp.yMin <= p.Zi0 + e;
            if (k.U.x > 0.5f)
                return fp.xMax >= p.Xi1 - e;
            return fp.xMin <= p.Xi0 + e;
        }

        /// <summary>Düz çatıya dış cephe boyunca çıkan merdiven (sahanlıklı) — sığarsa.</summary>
        private static bool TryPlanExteriorStair(Ctx c, BoxPlan p)
        {
            var rise = p.TopY;
            var firstSide = c.Chance(0.5f) ? 2 : 3;
            for (var pass = 0; pass < 2; pass++)
            {
                var angle = pass == 0 ? StructureKit.PreferredStairAngle : StructureKit.MaxStairAngle - 0.5f;
                var run = rise / Mathf.Tan(angle * Mathf.Deg2Rad);
                for (var k = 0; k < 3; k++)
                {
                    var side = k == 0 ? firstSide : k == 1 ? (firstSide == 2 ? 3 : 2) : 1;
                    var f = p.F[side];
                    float uStart, minU;
                    if (side == 1)
                    {
                        uStart = p.W - 0.1f;
                        minU = p.T + 0.05f;
                    }
                    else
                    {
                        uStart = f.Length + p.T - 0.1f;
                        minU = 0.05f;
                    }

                    var ul1 = uStart - run;
                    var ul0 = ul1 - Landing;
                    if (ul0 < minU)
                        continue;
                    if (f.IsReserved(0, ul0 - 0.3f, uStart + 0.3f, false))
                        continue;

                    var outward = f.Outward * (p.T * 0.5f + StairW * 0.5f + 0.02f);
                    var bottom = f.Point(uStart, 0f) + outward;
                    var dir = f.Point(0f, 0f) - f.Point(1f, 0f);
                    p.Ext = new ExtStair
                    {
                        Valid = true, Open = p.D.ExtStairOpen, Side = side, UStart = uStart, UL0 = ul0, UL1 = ul1, Run = run, Rise = rise,
                        Yaw = StructureKit.YawOf(dir), Bottom = bottom
                    };
                    f.Reserve(-1, ul0 - 0.3f, uStart + 0.3f);
                    var mid = (ul0 + ul1) * 0.5f;
                    if (p.D.Parapet > 0.01f)
                        f.Add(new WallOpening(mid, Landing - 0.1f, p.TopY - Yb, p.D.Parapet + 1f), OpeningKind.Gap, -1);
                    else
                        p.RailGaps.Add(new Vector3(side, f.ToCoord(ul0 - 0.05f), f.ToCoord(ul1 + 0.05f)));
                    c.Block(p.Floors, InsideRect(p, f, mid, Landing + 0.4f, 1.2f));
                    return true;
                }
            }

            return false;
        }

        private static void PlanPartitions(Ctx c, BoxPlan p)
        {
            var d = p.D;
            if (d.RoomWidth <= 0.5f || p.Zi1 - p.Zi0 < 2.6f)
                return;
            var wInt = p.Xi1 - p.Xi0;
            var n = Mathf.RoundToInt(wInt / d.RoomWidth);
            if (n < 2)
                return;

            for (var k = 0; k < p.Floors; k++)
            {
                for (var i = 1; i < n; i++)
                {
                    var target = p.Xi0 + wInt * i / n + c.Range(-0.3f, 0.3f);
                    for (var t = 0; t < 9; t++)
                    {
                        var x = target + ((t & 1) == 0 ? 1f : -1f) * 0.45f * ((t + 1) / 2);
                        if (x < p.Xi0 + 2f || x > p.Xi1 - 2f || PartitionNear(p, k, x, 2f))
                            continue;
                        var rect = MinMax(x - PartitionT * 0.5f - 0.05f, p.Zi0, x + PartitionT * 0.5f + 0.05f, p.Zi1);
                        if (!c.IsFree(k, rect))
                            continue;

                        var doorZ = float.NaN;
                        for (var a = 0; a < 8; a++)
                        {
                            var z = a == 0 ? (p.Zi0 + p.Zi1) * 0.5f + c.Range(-1f, 1f) : c.Range(p.Zi0 + 0.8f, p.Zi1 - 0.8f);
                            z = Mathf.Clamp(z, p.Zi0 + 0.75f, p.Zi1 - 0.75f);
                            var clear = MinMax(x - 1f, z - 0.75f, x + 1f, z + 0.75f);
                            if (!c.IsFree(k, clear))
                                continue;
                            doorZ = z;
                            c.Block(k, clear);
                            break;
                        }

                        if (float.IsNaN(doorZ))
                            continue;

                        c.Block(k, rect);
                        p.Partitions.Add(new Partition { Level = k, X = x, DoorZ = doorZ });
                        p.F[0].Reserve(k, p.F[0].ToU(x) - 0.4f, p.F[0].ToU(x) + 0.4f);
                        p.F[1].Reserve(k, p.F[1].ToU(x) - 0.4f, p.F[1].ToU(x) + 0.4f);
                        break;
                    }
                }
            }
        }

        private static bool PartitionNear(BoxPlan p, int level, float x, float distance)
        {
            for (var i = 0; i < p.Partitions.Count; i++)
            {
                var q = p.Partitions[i];
                if (q.Level == level && Mathf.Abs(q.X - x) < distance)
                    return true;
            }

            return false;
        }

        private static void PlanRuinHoles(Ctx c, BoxPlan p)
        {
            for (var k = 1; k < p.Floors; k++)
            {
                if (!c.Chance(0.75f))
                    continue;
                for (var attempt = 0; attempt < 8; attempt++)
                {
                    var sx = c.Range(1.4f, 3f);
                    var sz = c.Range(1.4f, 3f);
                    var x = c.Range(p.Xi0 + sx * 0.5f, p.Xi1 - sx * 0.5f);
                    var z = c.Range(p.Zi0 + sz * 0.5f, p.Zi1 - sz * 0.5f);
                    var r = Centered(x, z, sx, sz);
                    if (!c.IsFree(k, r, 0.2f))
                        continue;
                    p.HolesAt(k)?.Add(r);
                    c.Block(k, r);
                    break;
                }
            }
        }

        private static void PlanWindows(Ctx c, BoxPlan p)
        {
            var d = p.D;
            for (var r = 0; r < d.Rows.Count; r++)
            {
                var row = d.Rows[r];
                var primary = r == 0;
                for (var side = 0; side < 4; side++)
                {
                    if ((row.SideMask & (1 << side)) == 0)
                        continue;
                    var f = p.F[side];
                    var inset = f.AlongX ? p.T : 0f;
                    var margin = inset + row.Margin;
                    var usable = f.Length - 2f * margin;
                    if (usable < row.Width + 0.2f)
                        continue;
                    var n = Mathf.Max(1, Mathf.FloorToInt(usable / Mathf.Max(0.5f, row.Spacing)));
                    for (var k = Mathf.Max(0, row.MinLevel); k < p.Floors && k <= row.MaxLevel; k++)
                    {
                        var maxTop = p.CeilingY(k) - p.LevelY(k) - 0.12f;
                        var h = Mathf.Min(row.Height, maxTop - row.Sill);
                        if (h < 0.15f)
                            continue;
                        for (var i = 0; i < n; i++)
                        {
                            if (row.Skip > 0f && c.Chance(row.Skip))
                                continue;
                            var u0 = margin + usable * (i + 0.5f) / n;
                            var half = row.Width * 0.5f + 0.2f;
                            var placed = float.NaN;
                            for (var s = 0; s < 3; s++)
                            {
                                var u = u0 + (s == 0 ? 0f : s == 1 ? 0.6f : -0.6f);
                                if (u - half < inset || u + half > f.Length - inset)
                                    continue;
                                if (f.IsReserved(k, u - half, u + half, primary))
                                    continue;
                                placed = u;
                                break;
                            }

                            if (float.IsNaN(placed))
                                continue;
                            AddOpening(p, side, k, placed, row.Width, row.Sill, h, row.Kind);
                            if (primary)
                                f.Reserve(k, placed - half, placed + half, true);
                        }
                    }
                }
            }
        }

        // =====================================================================================================
        //  Kabuk geometrisi
        // =====================================================================================================

        private static void BuildShell(Ctx c, BoxPlan p)
        {
            var b = c.B;
            var d = p.D;
            var hw = p.W * 0.5f;
            var hd = p.Dp * 0.5f;

            // Temel (2 m gömülü) + zemin kaplaması
            const float ledge = 0.06f;
            b.Box(new Vector3(0f, (Yb - StructureKit.FoundationDepth) * 0.5f, 0f),
                new Vector3(p.W + 2f * ledge, Yb + StructureKit.FoundationDepth, p.Dp + 2f * ledge), d.FoundationMat);
            b.Box(new Vector3((p.Xi0 + p.Xi1) * 0.5f, Y0 - Fin * 0.5f, (p.Zi0 + p.Zi1) * 0.5f),
                new Vector3(p.Xi1 - p.Xi0, Fin, p.Zi1 - p.Zi0), d.FloorMat);

            BuildWalls(c, p);

            for (var k = 1; k < p.Floors; k++)
                b.Slab(-hw + p.T * 0.5f, -hd + p.T * 0.5f, hw - p.T * 0.5f, hd - p.T * 0.5f, p.LevelY(k), SlabT, d.SlabMat, p.HolesAt(k));

            if (p.HasRoof)
            {
                if (p.Flat)
                {
                    BuildFlatRoof(c, p);
                }
                else
                {
                    if (d.Ceiling)
                        b.Slab(-hw + p.T * 0.5f, -hd + p.T * 0.5f, hw - p.T * 0.5f, hd - p.T * 0.5f, p.TopY, d.CeilingThickness, d.CeilingMat);
                    p.RoofRise = BuildPitchedRoof(c, p);
                }
            }

            for (var i = 0; i < p.Cores.Count; i++)
                BuildCore(c, p, p.Cores[i]);
            if (p.Ext.Valid)
                BuildExteriorStair(c, p);
            BuildPartitions(c, p);
            DecorateOpenings(c, p);
            if (p.RoofWalkable && p.Flat && d.Parapet <= 0.01f)
                BuildRoofRailing(c, p);
            if (d.BeamEnds && p.Flat)
                BuildBeamEnds(c, p);
        }

        private static void BuildWalls(Ctx c, BoxPlan p)
        {
            var d = p.D;
            var b = c.B;
            var ruinRng = c.Ruined ? c.Rng : null;
            var ruin = c.Ruined ? d.RuinAmount : 0f;
            var h = p.WallTop - Yb;
            for (var i = 0; i < 4; i++)
            {
                var f = p.F[i];
                if (d.HasLowerWall && d.LowerWallHeight > 0.2f && d.LowerWallHeight < h - 0.3f)
                {
                    var lh = d.LowerWallHeight;
                    b.Wall(f.Start, f.End, Yb, lh, p.T, d.LowerWallMat, f.Openings, ruinRng, ruin * 0.4f);
                    TmpOpenings.Clear();
                    for (var o = 0; o < f.Openings.Count; o++)
                    {
                        var op = f.Openings[o];
                        TmpOpenings.Add(new WallOpening(op.Center, op.Width, op.Bottom - lh, op.Height));
                    }

                    b.Wall(f.Start, f.End, Yb + lh, h - lh, p.T, d.WallMat, TmpOpenings, ruinRng, ruin);
                }
                else
                {
                    b.Wall(f.Start, f.End, Yb, h, p.T, d.WallMat, f.Openings, ruinRng, ruin);
                }
            }
        }

        private static void BuildFlatRoof(Ctx c, BoxPlan p)
        {
            var d = p.D;
            var b = c.B;
            var holes = p.HolesAt(p.Floors);
            float x0, z0, x1, z1;
            if (d.Parapet > 0.01f)
            {
                x0 = -p.W * 0.5f + p.T * 0.5f;
                z0 = -p.Dp * 0.5f + p.T * 0.5f;
                x1 = -x0;
                z1 = -z0;
            }
            else
            {
                const float eave = 0.08f;
                x0 = -p.W * 0.5f - eave;
                z0 = -p.Dp * 0.5f - eave;
                x1 = -x0;
                z1 = -z0;
            }

            var total = d.RoofThickness;
            if (d.CeilingMat != d.RoofMat && total > 0.2f)
            {
                var ct = Mathf.Min(0.12f, total * 0.4f);
                b.Slab(x0, z0, x1, z1, p.TopY - (total - ct), ct, d.CeilingMat, holes);
                b.Slab(x0, z0, x1, z1, p.TopY, total - ct, d.RoofMat, holes);
            }
            else
            {
                b.Slab(x0, z0, x1, z1, p.TopY, total, d.RoofMat, holes);
            }

            if (d.Parapet > 0.01f && d.Coping)
                BuildCoping(c, p);
        }

        private static void BuildCoping(Ctx c, BoxPlan p)
        {
            var d = p.D;
            var top = p.WallTop;
            const float h = 0.07f;
            var w = p.T + 0.08f;
            var wallH = top - Yb;
            for (var s = 0; s < 4; s++)
            {
                var f = p.F[s];
                var u0 = f.AlongX ? -0.04f : 0.04f;
                var u1 = f.AlongX ? f.Length + 0.04f : f.Length - 0.04f;
                Spans.Clear();
                Spans.Add(new Vector2(u0, u1));
                for (var i = 0; i < f.Openings.Count; i++)
                {
                    var o = f.Openings[i];
                    if (o.Top >= wallH - 0.01f)
                        SubtractSpan(Spans, o.Start, o.End);
                }

                for (var i = 0; i < Spans.Count; i++)
                {
                    var sp = Spans[i];
                    if (sp.y - sp.x > 0.05f)
                        FBox(c, f, (sp.x + sp.y) * 0.5f, top + h * 0.5f, 0f, sp.y - sp.x, h, w, d.TrimMat);
                }
            }
        }

        private static void SubtractSpan(List<Vector2> spans, float a, float b)
        {
            for (var i = spans.Count - 1; i >= 0; i--)
            {
                var s = spans[i];
                if (b <= s.x || a >= s.y)
                    continue;
                spans.RemoveAt(i);
                if (s.x < a)
                    spans.Add(new Vector2(s.x, a));
                if (b < s.y)
                    spans.Add(new Vector2(b, s.y));
            }
        }

        /// <summary>Kırma (Hip), beşik (Gable) ya da tek eğimli (Shed) çatı. Mahya yüksekliğini (TopY üstü) döndürür.</summary>
        private static float BuildPitchedRoof(Ctx c, BoxPlan p)
        {
            var d = p.D;
            var b = c.B;
            var baseY = p.TopY;
            var pitch = Mathf.Clamp(d.RoofPitch, 5f, 45f) * Mathf.Deg2Rad;
            var tan = Mathf.Tan(pitch);
            var oh = Mathf.Max(0f, d.Overhang);
            const float th = 0.14f;

            if (d.Roof == RoofKind.Shed)
            {
                var hd = p.Dp * 0.5f;
                var h = p.Dp * tan;
                var n = Vector3.forward * Mathf.Sin(pitch) + Vector3.up * Mathf.Cos(pitch);
                var down = Vector3.forward * Mathf.Cos(pitch) - Vector3.up * Mathf.Sin(pitch);
                var center = new Vector3(0f, baseY + hd * tan, 0f) + n * (th * 0.5f);
                b.Box(center, new Vector3(p.W + 2f * oh, th, (p.Dp + 2f * oh) / Mathf.Cos(pitch)), Quaternion.LookRotation(down, n), d.RoofMat);
                Poly.Clear();
                Poly.Add(new Vector2(-hd, baseY));
                Poly.Add(new Vector2(hd, baseY));
                Poly.Add(new Vector2(-hd, baseY + h));
                for (var s = -1; s <= 1; s += 2)
                    b.Prism(Poly, new Vector3(s * (p.W * 0.5f - p.T * 0.5f), 0f, 0f), Vector3.forward, Vector3.up, p.T, d.WallMat);
                b.Box(new Vector3(0f, baseY + h * 0.5f, -(hd - p.T * 0.5f)), new Vector3(p.W - 2f * p.T, h, p.T), d.WallMat);
                return h;
            }

            var ridgeX = d.AutoRidge ? p.W >= p.Dp : d.RidgeAlongX;
            var along = ridgeX ? Vector3.right : Vector3.forward;
            var across = ridgeX ? Vector3.forward : Vector3.right;
            var alongLen = ridgeX ? p.W : p.Dp;
            var acrossLen = ridgeX ? p.Dp : p.W;

            if (d.Roof == RoofKind.Hip)
            {
                var hx = alongLen * 0.5f + oh;
                var hz = acrossLen * 0.5f + oh;
                var h = hz * tan;
                var rl = Mathf.Max(0.05f, hx - hz);
                Points.Clear();
                Points.Add(along * -hx + across * -hz + Vector3.up * baseY);
                Points.Add(along * hx + across * -hz + Vector3.up * baseY);
                Points.Add(along * hx + across * hz + Vector3.up * baseY);
                Points.Add(along * -hx + across * hz + Vector3.up * baseY);
                Points.Add(along * -rl + Vector3.up * (baseY + h));
                Points.Add(along * rl + Vector3.up * (baseY + h));
                b.ConvexSolid(Points, HipFaces, d.RoofMat);
                return h;
            }

            // Beşik çatı
            {
                var half = acrossLen * 0.5f;
                var h = half * tan;
                var slopeLen = (half + oh) / Mathf.Cos(pitch);
                var ridgeLen = alongLen + 2f * Mathf.Min(oh, 0.35f);
                for (var s = -1; s <= 1; s += 2)
                {
                    var dir = across * s;
                    var n = dir * Mathf.Sin(pitch) + Vector3.up * Mathf.Cos(pitch);
                    var down = dir * Mathf.Cos(pitch) - Vector3.up * Mathf.Sin(pitch);
                    var dm = (half + oh) * 0.5f;
                    var center = dir * dm + Vector3.up * (baseY + (half - dm) * tan) + n * (th * 0.5f);
                    b.Box(center, new Vector3(ridgeLen, th, slopeLen), Quaternion.LookRotation(down, n), d.RoofMat);
                }

                var capSize = ridgeX ? new Vector3(ridgeLen, 0.1f, 0.3f) : new Vector3(0.3f, 0.1f, ridgeLen);
                b.Box(Vector3.up * (baseY + h + th / Mathf.Cos(pitch)), capSize, d.TrimMat == d.WallMat ? d.RoofMat : d.TrimMat,
                    StructureCollider.None);

                Poly.Clear();
                Poly.Add(new Vector2(-half, baseY));
                Poly.Add(new Vector2(half, baseY));
                Poly.Add(new Vector2(0f, baseY + h));
                for (var e = -1; e <= 1; e += 2)
                {
                    var origin = along * (e * (alongLen * 0.5f - p.T * 0.5f));
                    b.Prism(Poly, origin, across, Vector3.up, p.T, d.WallMat);
                }

                return h;
            }
        }

        private static void BuildCore(Ctx c, BoxPlan p, Core k)
        {
            var b = c.B;
            var d = p.D;
            var y = p.LevelY(k.Level);
            var half = k.Rise * 0.5f;
            var yawU = StructureKit.YawOf(k.U);

            // 1. kol (duvar dibi)
            b.SolidStairs(k.P(k.A, k.Sw * 0.5f, y), yawU, k.Sw, half, k.R, d.StairMat, 0f, true);

            // Ara sahanlık (zeminden dolu)
            var land = k.UV(k.A + k.R, 0f, k.A + k.R + k.L, 2f * k.Sw);
            b.Box(new Vector3(land.center.x, y + half * 0.5f, land.center.y), new Vector3(land.width, half, land.height), d.StairMat);

            // 2. kol (geri döner, altı dolu)
            b.SolidStairs(k.P(k.A + k.R, k.Sw * 1.5f, y + half), yawU + 180f, k.Sw, half, k.R, d.StairMat, half, true);

            // Odaya bakan eğimli merdiven duvarı (korkuluk görevi görür)
            var end = k.A + k.R + k.L + BalT;
            Poly.Clear();
            Poly.Add(new Vector2(k.A, 0f));
            Poly.Add(new Vector2(end, 0f));
            Poly.Add(new Vector2(end, half + 0.95f));
            Poly.Add(new Vector2(k.A + k.R, half + 0.95f));
            Poly.Add(new Vector2(k.A, k.Rise + 0.95f));
            b.Prism(Poly, k.P(0f, 2f * k.Sw + BalT * 0.5f, y), k.U, Vector3.up, BalT, d.PartitionMat);

            // Sahanlık ucundaki duvar
            var endWall = k.UV(k.A + k.R + k.L, 0f, end, 2f * k.Sw);
            b.Box(new Vector3(endWall.center.x, y + (half + 0.95f) * 0.5f, endWall.center.y),
                new Vector3(endWall.width, half + 0.95f, endWall.height), d.PartitionMat);

            // Üst kattaki boşluk korkulukları
            var yu = y + k.Rise;
            var v2 = 2f * k.Sw + BalT * 0.5f;
            b.Railing(k.P(k.A, v2, 0f), k.P(end, v2, 0f), yu, RailH, d.RailMat);
            if (!FarEndAtWall(p, k))
                b.Railing(k.P(end - BalT * 0.5f, 0.05f, 0f), k.P(end - BalT * 0.5f, v2, 0f), yu, RailH, d.RailMat);
            b.Railing(k.P(k.A + k.Hc - 0.03f, 0.05f, 0f), k.P(k.A + k.Hc - 0.03f, k.Sw, 0f), yu, RailH, d.RailMat);
        }

        private static void BuildExteriorStair(Ctx c, BoxPlan p)
        {
            var e = p.Ext;
            var b = c.B;
            var d = p.D;
            var f = p.F[e.Side];
            var inner = p.T * 0.5f;
            var outer = p.T * 0.5f + StairW + 0.02f;
            var a = f.Point(e.UL0, 0f);
            var z = f.Point(e.UL1, 0f);
            var l0 = a + f.Outward * inner;
            var l1 = z + f.Outward * outer;
            var landing = MinMax(l0.x, l0.z, l1.x, l1.z);

            if (!e.Open)
            {
                b.SolidStairs(e.Bottom, e.Yaw, StairW, e.Rise, e.Run, d.StairMat, 0.8f, true);
                b.Box(new Vector3(landing.center.x, (e.Rise - 0.8f) * 0.5f, landing.center.y), new Vector3(landing.width, e.Rise + 0.8f, landing.height),
                    d.StairMat);
                return;
            }

            var right = Quaternion.Euler(0f, e.Yaw, 0f) * Vector3.right;
            var outerIsPos = Vector3.Dot(right, f.Outward) > 0f;
            b.OpenStairs(e.Bottom, e.Yaw, StairW, e.Rise, e.Run, M.MetalPanel, M.MetalDark, !outerIsPos, outerIsPos);
            b.Box(new Vector3(landing.center.x, e.Rise - 0.05f, landing.center.y), new Vector3(landing.width, 0.1f, landing.height), M.MetalPanel);

            // Dış köşe dikmeleri ve sahanlık korkulukları
            var o0 = f.Point(e.UL0 + 0.05f, 0f) + f.Outward * (outer - 0.05f);
            var o1 = f.Point(e.UL1 - 0.05f, 0f) + f.Outward * (outer - 0.05f);
            b.Box(new Vector3(o0.x, (e.Rise - 0.5f) * 0.5f, o0.z), new Vector3(0.1f, e.Rise + 0.5f, 0.1f), M.MetalDark);
            b.Box(new Vector3(o1.x, (e.Rise - 0.5f) * 0.5f, o1.z), new Vector3(0.1f, e.Rise + 0.5f, 0.1f), M.MetalDark);
            b.Railing(f.Point(e.UL0, 0f) + f.Outward * (outer - 0.03f), f.Point(e.UL1, 0f) + f.Outward * (outer - 0.03f), e.Rise, RailH, M.MetalDark);
            b.Railing(f.Point(e.UL0 + 0.03f, 0f) + f.Outward * inner, f.Point(e.UL0 + 0.03f, 0f) + f.Outward * outer, e.Rise, RailH, M.MetalDark);
        }

        private static void BuildPartitions(Ctx c, BoxPlan p)
        {
            var b = c.B;
            var d = p.D;
            var ruinRng = c.Ruined ? c.Rng : null;
            var ruin = c.Ruined ? d.RuinAmount * 0.6f : 0f;
            for (var i = 0; i < p.Partitions.Count; i++)
            {
                var part = p.Partitions[i];
                var baseY = part.Level == 0 ? Yb : p.LevelY(part.Level);
                var top = p.CeilingY(part.Level);
                var h = top - baseY;
                if (h < 1f)
                    continue;
                TmpOpenings.Clear();
                var doorBottom = part.Level == 0 ? Fin : 0f;
                var doorU = part.DoorZ - p.Zi0;
                TmpOpenings.Add(new WallOpening(doorU, InnerDoorW, doorBottom, Mathf.Min(InnerDoorH, h - doorBottom - 0.1f)));
                b.Wall(new Vector3(part.X, 0f, p.Zi0), new Vector3(part.X, 0f, p.Zi1), baseY, h, PartitionT, d.PartitionMat, TmpOpenings, ruinRng, ruin);

                if (c.Ruined)
                    continue;
                var dy = baseY + doorBottom;
                const float ft = 0.06f;
                var ac = PartitionT + 0.02f;
                b.Box(new Vector3(part.X, dy + InnerDoorH * 0.5f, part.DoorZ - InnerDoorW * 0.5f + ft * 0.5f), new Vector3(ac, InnerDoorH, ft),
                    d.FrameMat, StructureCollider.None);
                b.Box(new Vector3(part.X, dy + InnerDoorH * 0.5f, part.DoorZ + InnerDoorW * 0.5f - ft * 0.5f), new Vector3(ac, InnerDoorH, ft),
                    d.FrameMat, StructureCollider.None);
                b.Box(new Vector3(part.X, dy + InnerDoorH - ft * 0.5f, part.DoorZ), new Vector3(ac, ft, InnerDoorW), d.FrameMat, StructureCollider.None);
            }
        }

        /// <summary>Cepheye hizalı kutu: u boyunca along, dikey height, cephe kalınlığı yönünde across; outOff dışa kaydırma.</summary>
        private static void FBox(Ctx c, Facade f, float u, float y, float outOff, float along, float height, float across, M mat,
            bool collider = false)
        {
            var pos = f.Point(u, y) + f.Outward * outOff;
            var size = f.AlongX ? new Vector3(along, height, across) : new Vector3(across, height, along);
            c.B.Box(pos, size, mat, collider ? StructureCollider.Box : StructureCollider.None);
        }

        private static void DecorateOpenings(Ctx c, BoxPlan p)
        {
            var d = p.D;
            for (var s = 0; s < 4; s++)
            {
                var f = p.F[s];
                for (var i = 0; i < f.Openings.Count; i++)
                {
                    var o = f.Openings[i];
                    var m = f.Meta[i];
                    var y0 = Yb + o.Bottom;
                    var y1 = y0 + o.Height;
                    switch (m.Kind)
                    {
                        case OpeningKind.Door:
                            if (!c.Ruined)
                                Frame(c, p, f, o, d.FrameMat);
                            if (m.Level == 0 && d.DoorSteps)
                            {
                                const float stepTop = Y0 * 0.5f;
                                FBox(c, f, o.Center, (stepTop - 0.3f) * 0.5f, p.T * 0.5f + 0.06f + 0.25f, o.Width + 0.5f, stepTop + 0.3f, 0.5f,
                                    d.FoundationMat, true);
                            }

                            break;
                        case OpeningKind.Window:
                            if (c.Ruined)
                                break;
                            if (d.WindowFrames)
                                Frame(c, p, f, o, d.FrameMat);
                            FBox(c, f, o.Center, y0 + 0.025f, 0f, o.Width + 0.16f, 0.05f, p.T + 0.12f, d.TrimMat);
                            // Kırılabilir cam: pencerelerin yarısı camlı (rastgele sayı tüketmez; yerleşim değişmez).
                            if ((i + s + m.Level) % 2 == 0 && o.Width > 0.4f && o.Height > 0.4f)
                            {
                                var gw = o.Width - 0.14f;
                                var gh = o.Height - 0.07f;
                                var gc = f.Point(o.Center, y0 + o.Height * 0.5f);
                                c.Panes.Add(new KeyValuePair<Vector3, Vector3>(gc,
                                    f.AlongX ? new Vector3(gw, gh, 0.03f) : new Vector3(0.03f, gh, gw)));
                            }

                            if (d.Lintels)
                                FBox(c, f, o.Center, y1 + 0.075f, 0f, o.Width + 0.4f, 0.15f, p.T + 0.03f, M.WoodDark);
                            if (d.Shutters && c.Chance(d.ShutterChance))
                            {
                                var sm = c.Pick(M.Wood, M.WoodDark);
                                var off = p.T * 0.5f + 0.03f;
                                FBox(c, f, o.Center - o.Width * 0.75f - 0.03f, y0 + o.Height * 0.5f, off, o.Width * 0.5f, o.Height, 0.04f, sm);
                                FBox(c, f, o.Center + o.Width * 0.75f + 0.03f, y0 + o.Height * 0.5f, off, o.Width * 0.5f, o.Height, 0.04f, sm);
                            }

                            break;
                        case OpeningKind.Big:
                            if (c.Ruined)
                                break;
                            Frame(c, p, f, o, M.MetalDark);
                            if (d.BigShutterBox)
                                FBox(c, f, o.Center, y1 + 0.2f, p.T * 0.5f + 0.12f, o.Width + 0.2f, 0.4f, 0.24f, M.MetalDark);
                            break;
                        case OpeningKind.Niche:
                            Frame(c, p, f, o, d.TrimMat);
                            break;
                    }
                }
            }
        }

        private static void Frame(Ctx c, BoxPlan p, Facade f, WallOpening o, M mat)
        {
            const float ft = 0.07f;
            var y0 = Yb + o.Bottom;
            var across = p.T + 0.02f;
            FBox(c, f, o.Center - o.Width * 0.5f + ft * 0.5f, y0 + o.Height * 0.5f, 0f, ft, o.Height, across, mat);
            FBox(c, f, o.Center + o.Width * 0.5f - ft * 0.5f, y0 + o.Height * 0.5f, 0f, ft, o.Height, across, mat);
            FBox(c, f, o.Center, y0 + o.Height - ft * 0.5f, 0f, o.Width, ft, across, mat);
        }

        private static void BuildRoofRailing(Ctx c, BoxPlan p)
        {
            var b = c.B;
            var y = p.TopY;
            const float inset = 0.12f;
            var hw = p.W * 0.5f - inset;
            var hd = p.Dp * 0.5f - inset;
            for (var s = 0; s < 4; s++)
            {
                var alongX = s < 2;
                float a0, a1, fixedCoord;
                if (alongX)
                {
                    a0 = -hw;
                    a1 = hw;
                    fixedCoord = s == 0 ? hd : -hd;
                }
                else
                {
                    a0 = -hd;
                    a1 = hd;
                    fixedCoord = s == 2 ? hw : -hw;
                }

                Spans.Clear();
                Spans.Add(new Vector2(a0, a1));
                for (var g = 0; g < p.RailGaps.Count; g++)
                {
                    var gap = p.RailGaps[g];
                    if (Mathf.RoundToInt(gap.x) == s)
                        SubtractSpan(Spans, Mathf.Min(gap.y, gap.z), Mathf.Max(gap.y, gap.z));
                }

                for (var i = 0; i < Spans.Count; i++)
                {
                    var sp = Spans[i];
                    if (sp.y - sp.x < 0.2f)
                        continue;
                    var from = alongX ? new Vector3(sp.x, 0f, fixedCoord) : new Vector3(fixedCoord, 0f, sp.x);
                    var to = alongX ? new Vector3(sp.y, 0f, fixedCoord) : new Vector3(fixedCoord, 0f, sp.y);
                    b.Railing(from, to, y, RailH, M.MetalDark);
                }
            }
        }

        /// <summary>Toprak damın altındaki dışa taşan ahşap kiriş uçları (Anadolu evi).</summary>
        private static void BuildBeamEnds(Ctx c, BoxPlan p)
        {
            var y = p.TopY - p.D.RoofThickness * 0.65f;
            for (var s = 0; s < 2; s++)
            {
                var f = p.F[s];
                var n = Mathf.FloorToInt((p.W - 0.6f) / 0.75f);
                for (var i = 0; i <= n; i++)
                {
                    var u = 0.3f + (p.W - 0.6f) * i / Mathf.Max(1, n);
                    FBox(c, f, u, y, p.T * 0.5f + 0.12f, 0.13f, 0.13f, 0.24f, M.WoodDark);
                }
            }
        }

        // =====================================================================================================
        //  Döşeme: eşyalar, ganimet, yıkıntı
        // =====================================================================================================

        private static void Complete(Ctx c, BoxPlan p)
        {
            RuinDebris(c, p);
            Furnish(c, p);
            ApplyWeathering(c, p);
            try
            {
                WarWear(c, p);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BuildingGenerator] Savaş yıpranması atlandı (" + c.Spec.Style + "): " + e.Message);
            }

            if (c.Loot.Count == 0)
                c.Loot.Add(new Vector3((p.Xi0 + p.Xi1) * 0.5f, Y0 + 0.05f, (p.Zi0 + p.Zi1) * 0.5f));
        }

        private static void Furnish(Ctx c, BoxPlan p)
        {
            var d = p.D;
            var lootArea = d.LootArea * (c.Spec.Tier >= LootTier.High ? 0.85f : 1f);
            for (var k = 0; k < p.Floors; k++)
            {
                var y = p.LevelY(k);
                CollectRooms(p, k, Rooms);
                var theme = k == 0 || d.PropsUpper == PropTheme.None ? d.Props : d.PropsUpper;
                for (var r = 0; r < Rooms.Count; r++)
                {
                    var room = Rooms[r];
                    var area = room.width * room.height;
                    if (IsDomestic(theme))
                    {
                        // Ev tipi odalar: tek mobilya sistemi InteriorFurnisher (kilim, mobilya, süs, ampul orada)
                        FurnishDomestic(c, p, k, room, r, y);
                    }
                    else if (theme != PropTheme.None)
                    {
                        var n = Mathf.Clamp(Mathf.RoundToInt(area / Mathf.Max(1f, d.PropArea)), 1, d.MaxProps);
                        if (c.Ruined)
                            n = Mathf.Max(0, n - 1 - c.Int(0, 2));
                        for (var i = 0; i < n; i++)
                            PlaceProp(c, p, k, room, y, theme, i);
                    }

                    var lootN = area < 5f ? (c.Chance(0.6f) ? 1 : 0) : Mathf.Clamp(Mathf.RoundToInt(area / Mathf.Max(2f, lootArea)), 1, d.MaxLoot);
                    PlaceLoot(c, k, room, y, lootN);
                }
            }

            if (p.RoofWalkable && c.Chance(d.RoofLootChance))
                PlaceLoot(c, p.Floors, p.Interior, p.TopY, 1);
        }

        private static void CollectRooms(BoxPlan p, int level, List<Rect> rooms)
        {
            rooms.Clear();
            Xs.Clear();
            for (var i = 0; i < p.Partitions.Count; i++)
            {
                if (p.Partitions[i].Level == level)
                    Xs.Add(p.Partitions[i].X);
            }

            Xs.Sort();
            var x0 = p.Xi0;
            for (var i = 0; i < Xs.Count; i++)
            {
                rooms.Add(Rect.MinMaxRect(x0, p.Zi0, Xs[i] - PartitionT * 0.5f, p.Zi1));
                x0 = Xs[i] + PartitionT * 0.5f;
            }

            rooms.Add(Rect.MinMaxRect(x0, p.Zi0, p.Xi1, p.Zi1));
        }

        private static void PlaceLoot(Ctx c, int level, Rect room, float y, int count)
        {
            if (count <= 0)
                return;
            var inner = Expand(room, -0.45f);
            if (inner.width < 0.1f || inner.height < 0.1f)
                return;
            var placed = 0;
            for (var attempt = 0; attempt < count * 14 && placed < count; attempt++)
            {
                var x = c.Range(inner.xMin, inner.xMax);
                var z = c.Range(inner.yMin, inner.yMax);
                if (!c.IsFree(level, Centered(x, z, 0.6f, 0.6f)))
                    continue;
                var pos = new Vector3(x, y + 0.05f, z);
                if (c.LootNear(pos, 1.5f))
                    continue;
                c.Loot.Add(pos);
                placed++;
            }
        }

        private static readonly PropKind[] ThemeVillage =
            { PropKind.Sedir, PropKind.Sedir, PropKind.Crate, PropKind.Stove, PropKind.Table, PropKind.Wardrobe, PropKind.Bed };
        private static readonly PropKind[] ThemeResidential =
            { PropKind.Table, PropKind.Wardrobe, PropKind.Bed, PropKind.Sedir, PropKind.Shelf, PropKind.Crate, PropKind.Desk };
        private static readonly PropKind[] ThemeShop =
            { PropKind.Shelf, PropKind.Shelf, PropKind.Shelf, PropKind.Crate, PropKind.Barrels, PropKind.Crate };
        private static readonly PropKind[] ThemeMilitary =
        {
            PropKind.BunkBed, PropKind.BunkBed, PropKind.BunkBed, PropKind.Lockers, PropKind.Lockers, PropKind.AmmoCrates, PropKind.AmmoCrates,
            PropKind.Desk, PropKind.Table
        };
        private static readonly PropKind[] ThemeOffice =
            { PropKind.Desk, PropKind.Desk, PropKind.Console, PropKind.ServerRack, PropKind.Lockers, PropKind.Shelf, PropKind.AmmoCrates };
        private static readonly PropKind[] ThemeIndustrial =
        {
            PropKind.Crate, PropKind.Crate, PropKind.Crate, PropKind.Barrels, PropKind.Barrels, PropKind.Shelf, PropKind.Machine, PropKind.Machine,
            PropKind.AmmoCrates
        };
        private static readonly PropKind[] ThemeFarm =
            { PropKind.HayBales, PropKind.HayBales, PropKind.HayBales, PropKind.Trough, PropKind.Trough, PropKind.Crate, PropKind.Barrels };
        private static readonly PropKind[] ThemeBunker =
            { PropKind.AmmoCrates, PropKind.AmmoCrates, PropKind.Sandbags, PropKind.Table, PropKind.Crate, PropKind.Lockers };
        private static readonly PropKind[] ThemeHut = { PropKind.StrawBed, PropKind.Crate };

        private static PropKind PickProp(Ctx c, PropTheme theme, int index)
        {
            switch (theme)
            {
                case PropTheme.Village: return ThemeVillage[c.Int(0, ThemeVillage.Length)];
                case PropTheme.Residential: return ThemeResidential[c.Int(0, ThemeResidential.Length)];
                case PropTheme.Shop: return index == 0 ? PropKind.Counter : ThemeShop[c.Int(0, ThemeShop.Length)];
                case PropTheme.Military: return ThemeMilitary[c.Int(0, ThemeMilitary.Length)];
                case PropTheme.Office: return ThemeOffice[c.Int(0, ThemeOffice.Length)];
                case PropTheme.Industrial: return ThemeIndustrial[c.Int(0, ThemeIndustrial.Length)];
                case PropTheme.Farm: return ThemeFarm[c.Int(0, ThemeFarm.Length)];
                case PropTheme.Bunker: return ThemeBunker[c.Int(0, ThemeBunker.Length)];
                case PropTheme.Hut: return index == 0 ? PropKind.Hearth : ThemeHut[c.Int(0, ThemeHut.Length)];
                default: return PropKind.Crate;
            }
        }

        /// <summary>Eşya ayak izi: w (duvar boyunca, yerel X), depth (yerel Z), h (yükseklik).</summary>
        private static void PropSize(PropKind kind, out float w, out float depth, out float h)
        {
            switch (kind)
            {
                case PropKind.Crate: w = 1.0f; depth = 1.0f; h = 1.6f; break;
                case PropKind.AmmoCrates: w = 1.35f; depth = 0.65f; h = 1.0f; break;
                case PropKind.Table: w = 1.4f; depth = 1.4f; h = 0.8f; break;
                case PropKind.Sedir: w = 2.0f; depth = 0.75f; h = 0.95f; break;
                case PropKind.Stove: w = 0.7f; depth = 0.7f; h = 2.3f; break;
                case PropKind.Wardrobe: w = 1.2f; depth = 0.6f; h = 2.0f; break;
                case PropKind.Bed: w = 2.0f; depth = 1.0f; h = 0.6f; break;
                case PropKind.BunkBed: w = 2.0f; depth = 1.0f; h = 1.75f; break;
                case PropKind.Lockers: w = 1.9f; depth = 0.55f; h = 1.9f; break;
                case PropKind.Desk: w = 1.4f; depth = 1.3f; h = 1.2f; break;
                case PropKind.Shelf: w = 2.0f; depth = 0.55f; h = 2.0f; break;
                case PropKind.Counter: w = 2.7f; depth = 0.8f; h = 1.1f; break;
                case PropKind.Barrels: w = 1.4f; depth = 0.7f; h = 0.95f; break;
                case PropKind.Machine: w = 2.4f; depth = 1.5f; h = 1.6f; break;
                case PropKind.HayBales: w = 2.15f; depth = 0.95f; h = 1.1f; break;
                case PropKind.Console: w = 2.2f; depth = 0.8f; h = 1.4f; break;
                case PropKind.ServerRack: w = 1.4f; depth = 0.8f; h = 2.0f; break;
                case PropKind.Sandbags: w = 2.0f; depth = 0.6f; h = 0.85f; break;
                case PropKind.Trough: w = 2.2f; depth = 0.6f; h = 0.55f; break;
                case PropKind.StrawBed: w = 1.9f; depth = 0.9f; h = 0.35f; break;
                case PropKind.Hearth: w = 1.2f; depth = 0.7f; h = 2.4f; break;
                default: w = 1f; depth = 1f; h = 1f; break;
            }
        }

        private static bool CanStandFree(PropKind kind)
        {
            switch (kind)
            {
                case PropKind.Crate:
                case PropKind.AmmoCrates:
                case PropKind.Table:
                case PropKind.Barrels:
                case PropKind.Machine:
                case PropKind.HayBales:
                case PropKind.Sandbags:
                    return true;
                default:
                    return false;
            }
        }

        private static bool PlaceProp(Ctx c, BoxPlan p, int level, Rect room, float y, PropTheme theme, int index)
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var kind = PickProp(c, theme, attempt == 0 ? index : index + 1);
                PropSize(kind, out var w, out var dep, out var h);
                float yaw;
                Rect rect;
                Vector3 center;
                if (CanStandFree(kind) && room.width > 3.8f && room.height > 3.8f && c.Chance(room.width * room.height > 120f ? 0.65f : 0.3f))
                {
                    yaw = c.Chance(0.5f) ? 0f : 90f;
                    var sx = yaw == 0f ? w : dep;
                    var sz = yaw == 0f ? dep : w;
                    var minX = room.xMin + 1.2f + sx * 0.5f;
                    var maxX = room.xMax - 1.2f - sx * 0.5f;
                    var minZ = room.yMin + 1.2f + sz * 0.5f;
                    var maxZ = room.yMax - 1.2f - sz * 0.5f;
                    if (maxX < minX || maxZ < minZ)
                        continue;
                    var x = c.Range(minX, maxX);
                    var z = c.Range(minZ, maxZ);
                    rect = Centered(x, z, sx, sz);
                    center = new Vector3(x, y, z);
                    if (!c.IsFree(level, rect, 0.6f))
                        continue;
                }
                else
                {
                    var edge = c.Int(0, 4);
                    var along = edge < 2 ? room.width : room.height;
                    if (along < w + 0.3f || (edge < 2 ? room.height : room.width) < dep + 0.8f)
                        continue;
                    const float gap = 0.03f;
                    if (edge < 2)
                    {
                        var x = c.Range(room.xMin + w * 0.5f + 0.1f, room.xMax - w * 0.5f - 0.1f);
                        var z = edge == 0 ? room.yMin + gap + dep * 0.5f : room.yMax - gap - dep * 0.5f;
                        yaw = edge == 0 ? 0f : 180f;
                        rect = Centered(x, z, w, dep);
                        center = new Vector3(x, y, z);
                    }
                    else
                    {
                        var z = c.Range(room.yMin + w * 0.5f + 0.1f, room.yMax - w * 0.5f - 0.1f);
                        var x = edge == 2 ? room.xMin + gap + dep * 0.5f : room.xMax - gap - dep * 0.5f;
                        yaw = edge == 2 ? 90f : 270f;
                        rect = Centered(x, z, dep, w);
                        center = new Vector3(x, y, z);
                    }

                    if (!c.IsFree(level, rect, 0.12f))
                        continue;
                    if (h > 1.8f && BlocksWindow(p, level, rect, edge, room))
                        continue;
                }

                BuildProp(c, kind, center, yaw);
                c.Block(level, rect);
                return true;
            }

            return false;
        }

        private static bool BlocksWindow(BoxPlan p, int level, Rect rect, int edge, Rect room)
        {
            if (p == null)
                return false;
            const float e = 0.05f;
            int side;
            switch (edge)
            {
                case 0: side = room.yMin <= p.Zi0 + e ? 1 : -1; break;
                case 1: side = room.yMax >= p.Zi1 - e ? 0 : -1; break;
                case 2: side = room.xMin <= p.Xi0 + e ? 3 : -1; break;
                default: side = room.xMax >= p.Xi1 - e ? 2 : -1; break;
            }

            if (side < 0)
                return false;
            var f = p.F[side];
            var a = f.AlongX ? f.ToU(rect.xMin) : f.ToU(rect.yMin);
            var b = f.AlongX ? f.ToU(rect.xMax) : f.ToU(rect.yMax);
            for (var i = 0; i < f.Openings.Count; i++)
            {
                var m = f.Meta[i];
                if (m.Level != level || (m.Kind != OpeningKind.Window && m.Kind != OpeningKind.Big && m.Kind != OpeningKind.Slit))
                    continue;
                var o = f.Openings[i];
                if (a < o.End + 0.1f && o.Start - 0.1f < b)
                    return true;
            }

            return false;
        }

        // ------------------------------------------------------------------ Eşya modelleri

        private static void PB(Ctx c, Vector3 o, Quaternion q, float x, float y, float z, float sx, float sy, float sz, M m)
            => c.B.Box(o + q * new Vector3(x, y, z), new Vector3(sx, sy, sz), q, m, StructureCollider.None);

        private static void PC(Ctx c, Vector3 o, Quaternion q, float z, float sx, float sy, float sz, M m)
            => c.B.ColliderBox(o + q * new Vector3(0f, sy * 0.5f, z), new Vector3(sx, sy, sz), q, m);

        private static void PCyl(Ctx c, Vector3 o, Quaternion q, float x, float y, float z, float r, float h, M m)
            => c.B.VerticalCylinder(o + q * new Vector3(x, y, z), r, h, 10, m, StructureCollider.None);

        private static void BuildProp(Ctx c, PropKind kind, Vector3 o, float yaw)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            switch (kind)
            {
                case PropKind.Crate:
                {
                    var m = c.Chance(0.3f) ? M.WoodDark : M.Wood;
                    PB(c, o, q, 0f, 0.45f, 0f, 0.95f, 0.9f, 0.95f, m);
                    PB(c, o, q, 0f, 0.45f, 0f, 0.97f, 0.08f, 0.97f, M.WoodDark);
                    var top = 0.9f;
                    if (c.Chance(0.45f))
                    {
                        PB(c, o, q, c.Range(-0.1f, 0.1f), 1.25f, c.Range(-0.1f, 0.1f), 0.7f, 0.7f, 0.7f, m);
                        top = 1.6f;
                    }

                    PC(c, o, q, 0f, 0.95f, top, 0.95f, M.Wood);
                    break;
                }
                case PropKind.AmmoCrates:
                {
                    var top = c.Chance(0.6f) ? 1.0f : 0.5f;
                    for (var y = 0.25f; y < top; y += 0.5f)
                    {
                        PB(c, o, q, 0f, y, 0f, 1.3f, 0.48f, 0.6f, M.VehicleOlive);
                        PB(c, o, q, -0.45f, y, 0f, 0.06f, 0.5f, 0.62f, M.MetalDark);
                        PB(c, o, q, 0.45f, y, 0f, 0.06f, 0.5f, 0.62f, M.MetalDark);
                        PB(c, o, q, 0f, y + 0.12f, 0.305f, 0.45f, 0.06f, 0.01f, M.Yellow);
                    }

                    PC(c, o, q, 0f, 1.32f, top, 0.62f, M.Wood);
                    break;
                }
                case PropKind.Table:
                {
                    PB(c, o, q, 0f, 0.75f, -0.25f, 1.4f, 0.05f, 0.8f, M.Wood);
                    for (var i = 0; i < 4; i++)
                        PB(c, o, q, (i & 1) == 0 ? -0.62f : 0.62f, 0.36f, -0.25f + ((i & 2) == 0 ? -0.32f : 0.32f), 0.06f, 0.72f, 0.06f, M.WoodDark);
                    PB(c, o, q, 0f, 0.45f, 0.42f, 0.42f, 0.05f, 0.42f, M.Wood);
                    PB(c, o, q, 0f, 0.7f, 0.62f, 0.42f, 0.5f, 0.04f, M.Wood);
                    c.B.ColliderBox(o + q * new Vector3(0f, 0.39f, -0.25f), new Vector3(1.4f, 0.78f, 0.8f), q, M.Wood);
                    break;
                }
                case PropKind.Sedir:
                {
                    var cushion = c.Pick(M.Carpet, M.TentCanvas);
                    PB(c, o, q, 0f, 0.2f, 0f, 2.0f, 0.4f, 0.75f, M.WoodDark);
                    PB(c, o, q, 0f, 0.46f, 0.02f, 1.96f, 0.12f, 0.7f, cushion);
                    PB(c, o, q, -0.5f, 0.72f, -0.29f, 0.95f, 0.42f, 0.16f, cushion);
                    PB(c, o, q, 0.5f, 0.72f, -0.29f, 0.95f, 0.42f, 0.16f, cushion);
                    PC(c, o, q, 0f, 2.0f, 0.55f, 0.75f, M.Wood);
                    break;
                }
                case PropKind.Stove:
                {
                    PCyl(c, o, q, 0f, 0.08f, 0f, 0.27f, 0.62f, M.MetalDark);
                    PB(c, o, q, 0f, 0.715f, 0f, 0.5f, 0.03f, 0.5f, M.MetalDark);
                    PCyl(c, o, q, 0f, 0.73f, -0.05f, 0.06f, 1.5f, M.MetalDark);
                    c.B.Beam(o + q * new Vector3(0f, 2.2f, -0.05f), o + q * new Vector3(0f, 2.2f, -0.4f), 0.12f, 0.12f, M.MetalDark);
                    PC(c, o, q, 0f, 0.6f, 0.75f, 0.6f, M.MetalDark);
                    break;
                }
                case PropKind.Wardrobe:
                {
                    PB(c, o, q, 0f, 1.0f, 0f, 1.2f, 2.0f, 0.6f, M.Wood);
                    PB(c, o, q, 0f, 1.0f, 0.301f, 0.02f, 1.85f, 0.01f, M.WoodDark);
                    PB(c, o, q, -0.08f, 1.0f, 0.31f, 0.03f, 0.15f, 0.03f, M.MetalDark);
                    PB(c, o, q, 0.08f, 1.0f, 0.31f, 0.03f, 0.15f, 0.03f, M.MetalDark);
                    PC(c, o, q, 0f, 1.2f, 2.0f, 0.6f, M.Wood);
                    break;
                }
                case PropKind.Bed:
                {
                    PB(c, o, q, 0f, 0.17f, 0f, 2.0f, 0.34f, 1.0f, M.WoodDark);
                    PB(c, o, q, 0f, 0.42f, 0f, 1.94f, 0.16f, 0.94f, M.White);
                    PB(c, o, q, 0.25f, 0.47f, 0f, 1.4f, 0.1f, 0.96f, c.Pick(M.Carpet, M.TentCanvas));
                    PB(c, o, q, -0.75f, 0.53f, 0f, 0.35f, 0.1f, 0.7f, M.White);
                    PC(c, o, q, 0f, 2.0f, 0.55f, 1.0f, M.Wood);
                    break;
                }
                case PropKind.BunkBed:
                {
                    for (var i = 0; i < 4; i++)
                        PB(c, o, q, (i & 1) == 0 ? -0.97f : 0.97f, 0.875f, (i & 2) == 0 ? -0.45f : 0.45f, 0.05f, 1.75f, 0.05f, M.MetalDark);
                    for (var l = 0; l < 2; l++)
                    {
                        var y = l == 0 ? 0.35f : 1.25f;
                        PB(c, o, q, 0f, y, 0f, 2.0f, 0.06f, 0.95f, M.MetalDark);
                        PB(c, o, q, 0f, y + 0.1f, 0f, 1.92f, 0.14f, 0.88f, M.TentCanvas);
                        PB(c, o, q, 0.3f, y + 0.13f, 0f, 1.2f, 0.1f, 0.9f, M.VehicleOlive);
                    }

                    PC(c, o, q, 0f, 2.0f, 1.75f, 1.0f, M.MetalDark);
                    break;
                }
                case PropKind.Lockers:
                {
                    for (var i = -1; i <= 1; i++)
                    {
                        PB(c, o, q, i * 0.62f, 0.95f, 0f, 0.6f, 1.9f, 0.55f, M.MetalPanel);
                        PB(c, o, q, i * 0.62f, 1.6f, 0.278f, 0.3f, 0.12f, 0.01f, M.MetalDark);
                        PB(c, o, q, i * 0.62f + 0.2f, 1.0f, 0.285f, 0.03f, 0.12f, 0.03f, M.MetalDark);
                    }

                    PC(c, o, q, 0f, 1.9f, 1.9f, 0.55f, M.MetalPanel);
                    break;
                }
                case PropKind.Desk:
                {
                    var top = c.Pick(M.Wood, M.MetalPanel);
                    PB(c, o, q, 0f, 0.76f, -0.3f, 1.4f, 0.05f, 0.7f, top);
                    PB(c, o, q, -0.66f, 0.37f, -0.3f, 0.05f, 0.74f, 0.66f, M.MetalDark);
                    PB(c, o, q, 0.66f, 0.37f, -0.3f, 0.05f, 0.74f, 0.66f, M.MetalDark);
                    PB(c, o, q, -0.3f, 0.79f, -0.3f, 0.3f, 0.01f, 0.22f, M.White);
                    if (c.Chance(0.5f))
                    {
                        PB(c, o, q, 0.35f, 0.87f, -0.4f, 0.35f, 0.18f, 0.22f, M.VehicleOlive);
                    }
                    else
                    {
                        PB(c, o, q, 0.35f, 0.83f, -0.5f, 0.06f, 0.1f, 0.06f, M.MetalDark);
                        PB(c, o, q, 0.35f, 1.04f, -0.5f, 0.5f, 0.32f, 0.04f, M.Black);
                    }

                    PB(c, o, q, 0f, 0.45f, 0.35f, 0.45f, 0.06f, 0.45f, M.MetalDark);
                    PB(c, o, q, 0f, 0.75f, 0.56f, 0.45f, 0.5f, 0.05f, M.Black);
                    c.B.ColliderBox(o + q * new Vector3(0f, 0.395f, -0.3f), new Vector3(1.4f, 0.79f, 0.7f), q, M.MetalDark);
                    break;
                }
                case PropKind.Shelf:
                {
                    for (var i = 0; i < 4; i++)
                        PB(c, o, q, (i & 1) == 0 ? -0.97f : 0.97f, 1.0f, (i & 2) == 0 ? -0.24f : 0.24f, 0.05f, 2.0f, 0.05f, M.MetalDark);
                    for (var l = 0; l < 4; l++)
                    {
                        var y = 0.12f + l * 0.6f;
                        PB(c, o, q, 0f, y, 0f, 2.0f, 0.04f, 0.52f, M.MetalPanel);
                        if (l == 3)
                            continue;
                        var x = -0.85f;
                        while (x < 0.75f)
                        {
                            var s = c.Range(0.22f, 0.45f);
                            if (c.Chance(0.75f))
                                PB(c, o, q, x + s * 0.5f, y + 0.02f + s * 0.5f, 0f, s, s, 0.38f, ShelfItemMat(c));
                            x += s + 0.06f;
                        }
                    }

                    PC(c, o, q, 0f, 2.0f, 2.0f, 0.55f, M.MetalDark);
                    break;
                }
                case PropKind.Counter:
                {
                    PB(c, o, q, 0f, 0.5f, 0f, 2.6f, 1.0f, 0.7f, c.Pick(M.Wood, M.WoodDark, M.PlasterWarm));
                    PB(c, o, q, 0f, 1.025f, 0f, 2.7f, 0.05f, 0.8f, M.Wood);
                    PB(c, o, q, 0.7f, 1.15f, 0f, 0.35f, 0.2f, 0.3f, M.Gray);
                    PC(c, o, q, 0f, 2.6f, 1.05f, 0.75f, M.Wood);
                    break;
                }
                case PropKind.Barrels:
                {
                    var m = c.Pick(M.Rust, M.VehicleOlive, M.MetalDark);
                    for (var i = -1; i <= 1; i += 2)
                    {
                        PCyl(c, o, q, i * 0.36f, 0f, 0f, 0.3f, 0.92f, m);
                        PCyl(c, o, q, i * 0.36f, 0.3f, 0f, 0.31f, 0.04f, M.MetalDark);
                        PCyl(c, o, q, i * 0.36f, 0.62f, 0f, 0.31f, 0.04f, M.MetalDark);
                    }

                    PC(c, o, q, 0f, 1.35f, 0.92f, 0.62f, M.MetalDark);
                    break;
                }
                case PropKind.Machine:
                {
                    PB(c, o, q, 0f, 0.15f, 0f, 2.4f, 0.3f, 1.5f, M.MetalDark);
                    PB(c, o, q, -0.2f, 0.85f, 0f, 1.8f, 1.1f, 1.2f, c.Pick(M.VehicleOlive, M.MetalPanel, M.Gray));
                    c.B.Cylinder(o + q * new Vector3(0.85f, 0.75f, 0f), q * Quaternion.Euler(0f, 0f, 90f), 0.32f, 0.32f, 0.6f, 10, M.MetalDark,
                        StructureCollider.None);
                    PB(c, o, q, -0.9f, 1.1f, 0.63f, 0.4f, 0.5f, 0.06f, M.Gray);
                    PC(c, o, q, 0f, 2.4f, 1.6f, 1.5f, M.MetalDark);
                    break;
                }
                case PropKind.HayBales:
                {
                    PB(c, o, q, -0.55f, 0.275f, 0f, 1.05f, 0.55f, 0.95f, M.Hay);
                    PB(c, o, q, 0.55f, 0.275f, 0f, 1.05f, 0.55f, 0.95f, M.Hay);
                    var top = 0.55f;
                    if (c.Chance(0.6f))
                    {
                        PB(c, o, q, c.Range(-0.3f, 0.3f), 0.825f, 0f, 1.05f, 0.55f, 0.95f, M.Hay);
                        top = 1.1f;
                    }

                    PC(c, o, q, 0f, 2.15f, top, 0.95f, M.Hay);
                    break;
                }
                case PropKind.Console:
                {
                    PB(c, o, q, 0f, 0.4f, 0f, 2.2f, 0.8f, 0.8f, M.MetalPanel);
                    c.B.Box(o + q * new Vector3(0f, 0.9f, -0.1f), new Vector3(2.2f, 0.06f, 0.55f), q * Quaternion.Euler(25f, 0f, 0f), M.Gray,
                        StructureCollider.None);
                    for (var i = -1; i <= 1; i++)
                    {
                        PB(c, o, q, i * 0.7f, 1.0f, -0.3f, 0.06f, 0.12f, 0.06f, M.MetalDark);
                        PB(c, o, q, i * 0.7f, 1.2f, -0.3f, 0.55f, 0.36f, 0.05f, M.Black);
                    }

                    PC(c, o, q, 0f, 2.2f, 1.0f, 0.8f, M.MetalPanel);
                    break;
                }
                case PropKind.ServerRack:
                {
                    for (var i = -1; i <= 1; i += 2)
                    {
                        PB(c, o, q, i * 0.36f, 1.0f, 0f, 0.66f, 2.0f, 0.78f, M.MetalDark);
                        PB(c, o, q, i * 0.36f, 1.0f, 0.392f, 0.56f, 1.8f, 0.01f, M.Black);
                        PB(c, o, q, i * 0.36f - 0.2f, 1.6f, 0.4f, 0.04f, 0.04f, 0.01f, M.Green);
                    }

                    PC(c, o, q, 0f, 1.4f, 2.0f, 0.8f, M.MetalDark);
                    break;
                }
                case PropKind.Sandbags:
                {
                    for (var r = 0; r < 3; r++)
                    {
                        for (var i = -1; i <= 1; i++)
                            PB(c, o, q, i * 0.64f + (r % 2) * 0.08f, 0.15f + r * 0.28f, 0f, 0.6f, 0.28f, 0.55f, M.Sandbag);
                    }

                    PC(c, o, q, 0f, 2.0f, 0.85f, 0.58f, M.Sandbag);
                    break;
                }
                case PropKind.Trough:
                {
                    PB(c, o, q, 0f, 0.08f, 0f, 2.2f, 0.16f, 0.6f, M.Wood);
                    PB(c, o, q, 0f, 0.33f, 0.27f, 2.2f, 0.38f, 0.06f, M.Wood);
                    PB(c, o, q, 0f, 0.33f, -0.27f, 2.2f, 0.38f, 0.06f, M.Wood);
                    PB(c, o, q, -1.07f, 0.33f, 0f, 0.06f, 0.38f, 0.48f, M.Wood);
                    PB(c, o, q, 1.07f, 0.33f, 0f, 0.06f, 0.38f, 0.48f, M.Wood);
                    PB(c, o, q, 0f, 0.3f, 0f, 2.05f, 0.14f, 0.46f, M.Hay);
                    PC(c, o, q, 0f, 2.2f, 0.52f, 0.6f, M.Wood);
                    break;
                }
                case PropKind.StrawBed:
                {
                    PB(c, o, q, 0f, 0.15f, 0f, 1.9f, 0.3f, 0.9f, M.Hay);
                    PB(c, o, q, 0.2f, 0.32f, 0f, 1.2f, 0.05f, 0.88f, M.Carpet);
                    PC(c, o, q, 0f, 1.9f, 0.32f, 0.9f, M.Hay);
                    break;
                }
                case PropKind.Hearth:
                {
                    PB(c, o, q, 0f, 0.5f, -0.05f, 1.2f, 1.0f, 0.6f, M.StoneDark);
                    PB(c, o, q, 0f, 0.4f, 0.251f, 0.6f, 0.5f, 0.01f, M.Black);
                    PB(c, o, q, 0f, 1.3f, -0.15f, 0.9f, 0.6f, 0.4f, M.StoneDark);
                    PB(c, o, q, 0f, 2.0f, -0.25f, 0.4f, 0.8f, 0.3f, M.StoneDark);
                    PC(c, o, q, -0.05f, 1.2f, 1.0f, 0.6f, M.StoneDark);
                    break;
                }
            }
        }

        private static M ShelfItemMat(Ctx c)
        {
            switch (c.Int(0, 7))
            {
                case 0: return M.Wood;
                case 1: return M.WoodDark;
                case 2: return M.Gray;
                case 3: return M.Red;
                case 4: return M.Blue;
                case 5: return M.Yellow;
                default: return M.VehicleOlive;
            }
        }

        private static void RuinDebris(Ctx c, BoxPlan p)
        {
            if (!c.Ruined)
                return;
            var d = p.D;
            var b = c.B;
            var area = (p.Xi1 - p.Xi0) * (p.Zi1 - p.Zi0);
            var piles = 1 + Mathf.RoundToInt(area / 22f);
            var placed = 0;
            for (var attempt = 0; attempt < piles * 6 && placed < piles; attempt++)
            {
                var r = c.Range(0.6f, 1.1f);
                if (p.Xi1 - p.Xi0 < 2f * r + 0.4f || p.Zi1 - p.Zi0 < 2f * r + 0.4f)
                    break;
                var x = c.Range(p.Xi0 + r + 0.2f, p.Xi1 - r - 0.2f);
                var z = c.Range(p.Zi0 + r + 0.2f, p.Zi1 - r - 0.2f);
                var rect = Centered(x, z, 2f * r, 2f * r);
                if (!c.IsFree(0, rect))
                    continue;
                b.Rubble(new Vector3(x, Y0, z), r, c.Int(5, 9), c.Chance(0.6f) ? d.WallMat : M.StoneDark, c.Rng);
                c.Block(0, rect);
                placed++;
            }

            // Düşmüş kirişler
            for (var i = 0; i < 2; i++)
            {
                var x = c.Range(p.Xi0 + 0.5f, p.Xi1 - 0.5f);
                var z = c.Range(p.Zi0 + 0.5f, p.Zi1 - 0.5f);
                var len = c.Range(1.5f, 3f);
                var a = new Vector3(x, Y0 + 0.08f, z);
                var dir = Quaternion.Euler(0f, c.Range(0f, 360f), 0f) * Vector3.forward;
                var bEnd = a + dir * len + Vector3.up * c.Range(0.2f, 1.0f);
                b.Beam(a, bEnd, 0.16f, 0.16f, M.WoodDark);
            }

            // Dışarıdaki moloz
            var outside = 3 + c.Int(0, 3);
            for (var i = 0; i < outside; i++)
            {
                var side = c.Int(0, 4);
                var f = p.F[side];
                var u = c.Range(0.6f, Mathf.Max(0.7f, f.Length - 0.6f));
                if (f.IsReserved(0, u - 1.2f, u + 1.2f, false))
                    continue;
                var pt = f.Point(u, 0f) + f.Outward * (p.T * 0.5f + c.Range(0.6f, 1.4f));
                b.Rubble(pt, c.Range(0.6f, 1.2f), c.Int(5, 10), d.WallMat, c.Rng);
            }

            // Ara döşeme deliklerinin altına düşen parçalar
            for (var k = 1; k < p.Floors; k++)
            {
                var holes = p.HolesAt(k);
                if (holes == null)
                    continue;
                for (var i = 0; i < holes.Count; i++)
                {
                    var h = holes[i];
                    if (IsCoreHole(p, k, h))
                        continue;
                    var r = Mathf.Min(h.width, h.height) * 0.45f;
                    var rect = Centered(h.center.x, h.center.y, 2f * r, 2f * r);
                    if (!c.IsFree(k - 1, rect))
                        continue;
                    b.Rubble(new Vector3(h.center.x, p.LevelY(k - 1), h.center.y), r, c.Int(5, 9), d.SlabMat, c.Rng);
                    c.Block(k - 1, rect);
                }
            }
        }

        private static bool IsCoreHole(BoxPlan p, int level, Rect hole)
        {
            for (var i = 0; i < p.Cores.Count; i++)
            {
                var core = p.Cores[i];
                if (core.Level + 1 == level && Overlaps(core.Footprint, hole))
                    return true;
            }

            return false;
        }

        // =====================================================================================================
        //  Ortak dış ayrıntılar
        // =====================================================================================================

        private static void Chimney(Ctx c, BoxPlan p, M mat)
        {
            if (!p.HasRoof)
                return;
            var b = c.B;
            if (p.Flat)
            {
                for (var attempt = 0; attempt < 6; attempt++)
                {
                    var x = c.Chance(0.5f) ? p.Xi0 + 0.4f : p.Xi1 - 0.4f;
                    var z = c.Range(p.Zi0 + 0.4f, p.Zi1 - 0.4f);
                    var r = Centered(x, z, 0.6f, 0.6f);
                    if (!c.IsFree(p.Floors, r, 0.3f))
                        continue;
                    b.Box(new Vector3(x, p.TopY + 0.5f, z), new Vector3(0.5f, 1.1f, 0.5f), mat);
                    b.Box(new Vector3(x, p.TopY + 1.1f, z), new Vector3(0.66f, 0.08f, 0.66f), mat, StructureCollider.None);
                    c.ChimneyPos = new Vector3(x, p.TopY + 1.14f, z);
                    c.Block(p.Floors, r);
                    return;
                }

                return;
            }

            var cx = c.Range(-p.W * 0.25f, p.W * 0.25f);
            var cz = c.Range(-p.Dp * 0.15f, p.Dp * 0.15f);
            var top = p.TopY + p.RoofRise + 0.6f;
            b.Box(new Vector3(cx, (p.TopY + top) * 0.5f, cz), new Vector3(0.5f, top - p.TopY, 0.5f), mat, StructureCollider.None);
            b.Box(new Vector3(cx, top + 0.04f, cz), new Vector3(0.66f, 0.08f, 0.66f), mat, StructureCollider.None);
            c.ChimneyPos = new Vector3(cx, top + 0.08f, cz);
        }

        /// <summary>Üst kat ön cephesine balkon kapısı ekler (pencerelerden önce çağrılır). Başarılıysa kapı u konumu döner.</summary>
        private static float PlanBalcony(Ctx c, BoxPlan p, int level)
        {
            if (level >= p.Floors || level < 1)
                return float.NaN;
            var pref = p.W * 0.5f + c.Range(-0.15f, 0.15f) * p.W;
            return TryAddDoor(c, p, 0, level, 1.3f, 2.2f, pref, out var u) ? u : float.NaN;
        }

        private static void BuildBalcony(Ctx c, BoxPlan p, int level, float u, float width)
        {
            if (float.IsNaN(u))
                return;
            var b = c.B;
            var d = p.D;
            var f = p.F[0];
            var cx = f.ToCoord(u);
            var half = Mathf.Min(width * 0.5f, p.W * 0.5f - 0.2f);
            cx = Mathf.Clamp(cx, -p.W * 0.5f + half, p.W * 0.5f - half);
            const float depth = 1.3f;
            var z0 = p.Dp * 0.5f;
            var y = p.LevelY(level);
            b.Box(new Vector3(cx, y - 0.09f, z0 + depth * 0.5f), new Vector3(half * 2f, 0.18f, depth), d.SlabMat);
            b.Box(new Vector3(cx, y - 0.2f, z0 + depth * 0.5f), new Vector3(half * 2f - 0.1f, 0.06f, depth - 0.05f), d.TrimMat, StructureCollider.None);
            b.Railing(new Vector3(cx - half + 0.04f, 0f, z0 + depth - 0.04f), new Vector3(cx + half - 0.04f, 0f, z0 + depth - 0.04f), y, RailH, d.RailMat);
            b.Railing(new Vector3(cx - half + 0.04f, 0f, z0), new Vector3(cx - half + 0.04f, 0f, z0 + depth - 0.04f), y, RailH, d.RailMat);
            b.Railing(new Vector3(cx + half - 0.04f, 0f, z0), new Vector3(cx + half - 0.04f, 0f, z0 + depth - 0.04f), y, RailH, d.RailMat);
            if (!c.Ruined && c.Chance(0.35f))
                c.Loot.Add(new Vector3(cx + c.Range(-half + 0.5f, half - 0.5f), y + 0.05f, z0 + depth * 0.5f));
        }

        /// <summary>Yan cepheye klima dış ünitesi / çanak anten (görsel).</summary>
        private static void WallGadgets(Ctx c, BoxPlan p, bool satellite)
        {
            if (c.Ruined)
                return;
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var side = c.Chance(0.5f) ? 2 : 3;
                var f = p.F[side];
                var level = c.Int(0, p.Floors);
                var u = c.Range(0.8f, Mathf.Max(0.9f, f.Length - 0.8f));
                if (f.IsReserved(level, u - 0.6f, u + 0.6f))
                    continue;
                var y = p.LevelY(level) + 2.0f;
                FBox(c, f, u, y, p.T * 0.5f + 0.16f, 0.8f, 0.55f, 0.3f, M.White);
                FBox(c, f, u - 0.1f, y, p.T * 0.5f + 0.315f, 0.4f, 0.4f, 0.01f, M.Gray);
                f.Reserve(level, u - 0.6f, u + 0.6f);
                break;
            }

            if (!satellite || !p.HasRoof)
                return;
            {
                var f = p.F[c.Chance(0.5f) ? 2 : 3];
                var u = c.Range(0.6f, Mathf.Max(0.7f, f.Length - 0.6f));
                var y = p.TopY - 0.6f;
                var basePos = f.Point(u, y) + f.Outward * (p.T * 0.5f + 0.25f);
                c.B.Box(basePos - f.Outward * 0.12f, f.AlongX ? new Vector3(0.06f, 0.06f, 0.26f) : new Vector3(0.26f, 0.06f, 0.06f), M.MetalDark,
                    StructureCollider.None);
                var rot = Quaternion.LookRotation(f.Outward + Vector3.up * 0.6f) * Quaternion.Euler(90f, 0f, 0f);
                c.B.Cylinder(basePos + Vector3.up * 0.1f, rot, 0.38f, 0.38f, 0.04f, 12, M.White, StructureCollider.None);
            }
        }

        // =====================================================================================================
        //  Stiller
        // =====================================================================================================

        private static void VillageHouse(Ctx c)
        {
            var s = c.Spec;
            c.DefaultName = "Köy Evi";
            var stone = c.Chance(0.55f);
            var d = new BoxDesign
            {
                Width = Mathf.Clamp(s.Width, 5.5f, 12f),
                Depth = Mathf.Clamp(s.Depth, 5f, 10f),
                Floors = Mathf.Clamp(s.Floors, 1, 2),
                FloorHeight = Mathf.Clamp(s.FloorHeight, 2.8f, 3.4f),
                Wall = stone ? 0.45f : 0.32f,
                WallMat = stone ? c.Pick(M.StoneDark, M.Stone) : c.Pick(M.Plaster, M.PlasterWarm),
                FoundationMat = M.StoneDark,
                FloorMat = M.Wood,
                SlabMat = M.Wood,
                CeilingMat = M.Wood,
                TrimMat = stone ? M.WoodDark : M.Stone,
                FrameMat = M.WoodDark,
                PartitionMat = stone ? M.Plaster : M.PlasterWarm,
                StairMat = M.StoneDark,
                RailMat = M.WoodDark,
                Lintels = stone,
                Shutters = true,
                ShutterChance = 0.35f,
                BackDoor = c.Chance(0.3f),
                RoomWidth = 4.2f,
                Props = PropTheme.Village,
                Kilim = true,
                LootArea = 13f,
                MaxLoot = 2
            };
            if (c.Chance(0.55f))
            {
                d.Roof = RoofKind.Flat;
                d.RoofMat = M.Dirt;
                d.RoofThickness = 0.38f;
                d.Parapet = 0.4f;
                d.Coping = !stone;
                d.RoofAccess = RoofAccess.Exterior;
                d.BeamEnds = true;
                d.RoofLootChance = 0.5f;
            }
            else
            {
                d.Roof = c.Chance(0.65f) ? RoofKind.Hip : RoofKind.Gable;
                d.RoofMat = M.RoofTile;
                d.RoofPitch = c.Range(22f, 30f);
                d.Overhang = 0.45f;
            }

            d.AddRow(0.8f, 1.0f, 1.0f, 2.8f).Skip = 0.15f;
            var p = StandardBox(c, d);
            Chimney(c, p, stone ? d.WallMat : M.StoneDark);
            if (p.Flat && !c.Ruined && c.Chance(0.5f))
            {
                // Dam silindiri (loğ taşı)
                var x = c.Range(p.Xi0 + 0.8f, p.Xi1 - 0.8f);
                var z = c.Range(p.Zi0 + 0.8f, p.Zi1 - 0.8f);
                if (c.IsFree(p.Floors, Centered(x, z, 0.8f, 0.8f)))
                    c.B.Cylinder(new Vector3(x, p.TopY + 0.22f, z), Quaternion.Euler(0f, 0f, 90f), 0.22f, 0.22f, 0.7f, 10, M.Stone, StructureCollider.None);
            }

            Complete(c, p);
        }

        private static void TwoStoryHouse(Ctx c)
        {
            var s = c.Spec;
            c.DefaultName = "İki Katlı Ev";
            var flat = c.Chance(0.2f);
            var d = new BoxDesign
            {
                Width = Mathf.Clamp(s.Width, 7f, 13f),
                Depth = Mathf.Clamp(s.Depth, 6.5f, 11f),
                Floors = Mathf.Clamp(Mathf.Max(2, s.Floors), 2, 3),
                FloorHeight = Mathf.Clamp(s.FloorHeight, 2.9f, 3.4f),
                Wall = 0.3f,
                WallMat = c.Pick(M.Plaster, M.PlasterWarm, c.Pick(M.Brick, M.Plaster)),
                FoundationMat = M.StoneDark,
                FloorMat = M.Wood,
                SlabMat = M.Concrete,
                CeilingMat = M.Plaster,
                TrimMat = c.Pick(M.Concrete, M.White),
                FrameMat = M.WoodDark,
                PartitionMat = M.Plaster,
                StairMat = M.Concrete,
                RailMat = M.MetalDark,
                Shutters = c.Chance(0.3f),
                ShutterChance = 0.4f,
                BackDoor = c.Chance(0.5f),
                RoomWidth = 4.4f,
                Props = PropTheme.Residential,
                Kilim = true,
                LootArea = 14f,
                MaxLoot = 2
            };
            if (flat)
            {
                d.Roof = RoofKind.Flat;
                d.Parapet = 0.9f;
                d.RoofAccess = RoofAccess.Interior;
                d.RoofLootChance = 0.6f;
            }
            else
            {
                d.Roof = c.Chance(0.6f) ? RoofKind.Hip : RoofKind.Gable;
                d.RoofMat = M.RoofTile;
                d.RoofPitch = c.Range(22f, 28f);
                d.Overhang = 0.45f;
            }

            d.AddRow(1.0f, 1.3f, 0.9f, 2.6f).Skip = 0.1f;
            var balconyU = float.NaN;
            var wantBalcony = c.Chance(0.6f);
            d.OnBeforeWindows = (cc, pp) =>
            {
                if (wantBalcony)
                    balconyU = PlanBalcony(cc, pp, 1);
            };
            var p = StandardBox(c, d);
            BuildBalcony(c, p, 1, balconyU, 3.0f);
            Chimney(c, p, M.Brick);
            WallGadgets(c, p, c.Chance(0.5f));
            Complete(c, p);
        }

        private static void Shop(Ctx c)
        {
            var s = c.Spec;
            c.DefaultName = "Dükkân";
            var d = new BoxDesign
            {
                Width = Mathf.Clamp(s.Width, 5f, 12f),
                Depth = Mathf.Clamp(s.Depth, 5f, 10f),
                Floors = Mathf.Clamp(s.Floors, 1, 2),
                FloorHeight = Mathf.Clamp(s.FloorHeight, 3.0f, 3.6f),
                Wall = 0.25f,
                WallMat = c.Pick(M.Plaster, M.PlasterWarm, c.Pick(M.Concrete, M.Brick)),
                FoundationMat = M.Concrete,
                FloorMat = M.Concrete,
                SlabMat = M.Concrete,
                CeilingMat = M.Plaster,
                TrimMat = M.Concrete,
                FrameMat = M.MetalDark,
                Roof = RoofKind.Flat,
                Parapet = 0.6f,
                BigWidth = 3.2f,
                BigHeight = 1.9f,
                BigSill = 0.55f,
                BigShutterBox = true,
                BackDoor = true,
                RoomWidth = 6f,
                Props = PropTheme.Shop,
                PropsUpper = PropTheme.Residential,
                PropArea = 7f,
                MaxProps = 5,
                LootArea = 11f
            };
            d.AddRow(1.0f, 1.2f, 1.0f, 2.8f).Skip = 0.15f;
            var p = StandardBox(c, d);

            if (!c.Ruined)
            {
                var f = p.F[0];
                var signMat = c.Pick(M.Red, M.Blue, M.Green);
                var signW = Mathf.Min(p.W - 1f, 4.2f);
                var y = Y0 + 2.8f;
                FBox(c, f, p.W * 0.5f, y, p.T * 0.5f + 0.04f, signW + 0.12f, 0.62f, 0.08f, M.White);
                FBox(c, f, p.W * 0.5f, y, p.T * 0.5f + 0.09f, signW, 0.5f, 0.06f, signMat);

                // Tente
                var awnMat = c.Pick(M.TentCanvas, M.Red, M.Green);
                const float len = 1.3f;
                const float tilt = 20f;
                var rot = Quaternion.Euler(tilt, 0f, 0f);
                var center = new Vector3(0f, Y0 + 2.45f - Mathf.Sin(tilt * Mathf.Deg2Rad) * len * 0.5f,
                    p.Dp * 0.5f + Mathf.Cos(tilt * Mathf.Deg2Rad) * len * 0.5f);
                c.B.Box(center, new Vector3(Mathf.Min(p.W - 0.6f, 5f), 0.04f, len), rot, awnMat, StructureCollider.None);
            }

            WallGadgets(c, p, false);
            Complete(c, p);
        }

        private static void Barracks(Ctx c)
        {
            var s = c.Spec;
            c.DefaultName = "Koğuş";
            var gable = c.Chance(0.6f);
            var d = new BoxDesign
            {
                Width = Mathf.Clamp(s.Width, 12f, 36f),
                Depth = Mathf.Clamp(s.Depth, 6.5f, 11f),
                Floors = Mathf.Clamp(s.Floors, 1, 2),
                FloorHeight = Mathf.Clamp(s.FloorHeight, 3.0f, 3.6f),
                Wall = 0.3f,
                WallMat = c.Pick(M.PlasterWarm, M.Concrete, M.Plaster),
                FoundationMat = M.ConcreteDark,
                FloorMat = M.Concrete,
                SlabMat = M.Concrete,
                CeilingMat = M.Plaster,
                TrimMat = M.ConcreteDark,
                FrameMat = M.MetalDark,
                StairMat = M.Concrete,
                RoomWidth = 7f,
                Props = PropTheme.Military,
                PropArea = 9f,
                MaxProps = 6,
                LootArea = 16f,
                MaxLoot = 3,
                SideDoors = 1,
                BackDoor = c.Chance(0.5f)
            };
            d.FrontDoors = d.Width >= 18f ? 2 : 1;
            if (gable)
            {
                d.Roof = RoofKind.Gable;
                d.RoofMat = M.RoofMetal;
                d.RoofPitch = 18f;
                d.Overhang = 0.4f;
            }
            else
            {
                d.Roof = RoofKind.Flat;
                d.Parapet = 0.5f;
            }

            d.AddRow(1.2f, 1.1f, 1.0f, 3.0f);
            var p = StandardBox(c, d);
            DoorCanopies(c, p, 0);
            Complete(c, p);
        }

        private static void DoorCanopies(Ctx c, BoxPlan p, int side)
        {
            if (c.Ruined)
                return;
            var f = p.F[side];
            for (var i = 0; i < f.Openings.Count; i++)
            {
                var m = f.Meta[i];
                if (m.Kind != OpeningKind.Door || m.Level != 0)
                    continue;
                var o = f.Openings[i];
                var y = Yb + o.Top + 0.3f;
                FBox(c, f, o.Center, y, p.T * 0.5f + 0.55f, o.Width + 1.0f, 0.14f, 1.1f, M.Concrete, true);
            }
        }

        private static void Karakol(Ctx c)
        {
            var s = c.Spec;
            c.DefaultName = "Karakol";
            var concrete = c.Chance(0.65f);
            var d = new BoxDesign
            {
                Width = Mathf.Clamp(s.Width, 10f, 20f),
                Depth = Mathf.Clamp(s.Depth, 8f, 15f),
                Floors = Mathf.Clamp(Mathf.Max(2, s.Floors), 2, 3),
                FloorHeight = Mathf.Clamp(s.FloorHeight, 3.0f, 3.6f),
                Wall = 0.45f,
                WallMat = concrete ? M.Concrete : M.Stone,
                FoundationMat = concrete ? M.ConcreteDark : M.StoneDark,
                FloorMat = M.Concrete,
                SlabMat = M.Concrete,
                CeilingMat = M.Concrete,
                RoofMat = M.Concrete,
                TrimMat = M.ConcreteDark,
                FrameMat = M.MetalDark,
                PartitionMat = M.Plaster,
                StairMat = M.Concrete,
                Roof = RoofKind.Flat,
                Parapet = 1.15f,
                RoofAccess = RoofAccess.Interior,
                RoofLootChance = 1f,
                CenterDoor = true,
                BackDoor = true,
                RoomWidth = 4.8f,
                Props = PropTheme.Military,
                PropsUpper = PropTheme.Office,
                LootArea = 13f,
                MaxLoot = 2,
                RuinAmount = 0.45f
            };
            var slit = d.AddRow(0.55f, 0.9f, 1.35f, 2.2f);
            slit.MaxLevel = 0;
            var upper = d.AddRow(0.8f, 1.0f, 1.1f, 2.6f);
            upper.MinLevel = 1;
            d.OnBeforeWindows = (cc, pp) => AddCrenels(pp, 1.6f, 0.6f, 0.45f);
            var p = StandardBox(c, d);
            DoorCanopies(c, p, 0);

            if (!c.Ruined)
            {
                // Giriş yanı kum torbası siperleri
                var f = p.F[0];
                for (var i = 0; i < f.Openings.Count; i++)
                {
                    if (f.Meta[i].Kind != OpeningKind.Door || f.Meta[i].Level != 0)
                        continue;
                    var o = f.Openings[i];
                    for (var sgn = -1; sgn <= 1; sgn += 2)
                    {
                        var u = o.Center + sgn * (o.Width * 0.5f + 1.3f);
                        if (u < 0.8f || u > p.W - 0.8f)
                            continue;
                        FBox(c, f, u, 0.45f, p.T * 0.5f + 1.2f, 1.6f, 0.9f, 0.6f, M.Sandbag, true);
                        FBox(c, f, u, 0.97f, p.T * 0.5f + 1.2f, 1.4f, 0.14f, 0.5f, M.Sandbag);
                    }

                    break;
                }

                if (p.RoofWalkable)
                    RoofNests(c, p, 2);
                Searchlight(c, p);
            }

            Complete(c, p);
        }

        /// <summary>Parapete mazgal çentikleri ekler.</summary>
        private static void AddCrenels(BoxPlan p, float spacing, float width, float depth)
        {
            if (!p.Flat || p.D.Parapet < depth + 0.3f)
                return;
            var bottom = p.TopY - Yb + p.D.Parapet - depth;
            for (var s = 0; s < 4; s++)
            {
                var f = p.F[s];
                var margin = (f.AlongX ? p.T : 0f) + 0.7f;
                var usable = f.Length - 2f * margin;
                var n = Mathf.FloorToInt(usable / spacing);
                if (n < 1)
                    continue;
                for (var i = 0; i < n; i++)
                {
                    var u = margin + usable * (i + 0.5f) / n;
                    var blocked = false;
                    for (var o = 0; o < f.Openings.Count; o++)
                    {
                        var op = f.Openings[o];
                        if (f.Meta[o].Kind == OpeningKind.Gap && u + width > op.Start - 0.3f && u - width < op.End + 0.3f)
                        {
                            blocked = true;
                            break;
                        }
                    }

                    if (!blocked)
                        f.Add(new WallOpening(u, width, bottom, depth + 1f), OpeningKind.Notch, -1);
                }
            }
        }

        /// <summary>Çatı köşelerine U biçimli kum torbası mevzileri.</summary>
        private static void RoofNests(Ctx c, BoxPlan p, int count)
        {
            var level = p.Floors;
            var y = p.TopY;
            var placed = 0;
            var start = c.Int(0, 4);
            for (var i = 0; i < 4 && placed < count; i++)
            {
                var corner = (start + i) % 4;
                var sx = (corner & 1) == 0 ? -1f : 1f;
                var sz = (corner & 2) == 0 ? -1f : 1f;
                const float size = 2.4f;
                var cx = sx > 0f ? p.Xi1 - size * 0.5f : p.Xi0 + size * 0.5f;
                var cz = sz > 0f ? p.Zi1 - size * 0.5f : p.Zi0 + size * 0.5f;
                var rect = Centered(cx, cz, size, size);
                if (!c.IsFree(level, rect))
                    continue;
                // İç kenarlar boyunca torbalar (L), açık uç köşeye bakar
                var innerX = cx - sx * (size * 0.5f - 0.25f);
                var innerZ = cz - sz * (size * 0.5f - 0.25f);
                c.B.Box(new Vector3(innerX, y + 0.4f, cz + sz * 0.3f), new Vector3(0.5f, 0.8f, size - 0.6f), M.Sandbag);
                c.B.Box(new Vector3(cx + sx * 0.3f, y + 0.4f, innerZ), new Vector3(size - 0.6f, 0.8f, 0.5f), M.Sandbag);
                c.Block(level, rect);
                placed++;
            }
        }

        private static void Searchlight(Ctx c, BoxPlan p)
        {
            if (!p.HasRoof)
                return;
            var level = p.Floors;
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var x = c.Chance(0.5f) ? p.Xi0 + 0.4f : p.Xi1 - 0.4f;
                var z = c.Range(p.Zi0 + 1f, p.Zi1 - 1f);
                var r = Centered(x, z, 0.6f, 0.6f);
                if (!c.IsFree(level, r))
                    continue;
                var y = p.TopY;
                c.B.Box(new Vector3(x, y + 0.75f, z), new Vector3(0.1f, 1.5f, 0.1f), M.MetalDark);
                var dir = x > 0f ? Vector3.right : Vector3.left;
                c.B.Cylinder(new Vector3(x, y + 1.65f, z) + dir * 0.05f, Quaternion.FromToRotation(Vector3.up, dir + Vector3.down * 0.2f),
                    0.24f, 0.2f, 0.45f, 10, M.MetalDark, StructureCollider.None);
                c.Block(level, r);
                return;
            }
        }

        private static void DamControl(Ctx c)
        {
            var s = c.Spec;
            c.DefaultName = "Baraj Kontrol Binası";
            var d = new BoxDesign
            {
                Width = Mathf.Clamp(s.Width, 8f, 16f),
                Depth = Mathf.Clamp(s.Depth, 6f, 12f),
                Floors = Mathf.Clamp(Mathf.Max(2, s.Floors), 2, 3),
                FloorHeight = Mathf.Clamp(s.FloorHeight, 3.2f, 3.8f),
                Wall = 0.3f,
                WallMat = c.Pick(M.Concrete, M.Plaster),
                FoundationMat = M.ConcreteDark,
                FloorMat = M.Concrete,
                SlabMat = M.Concrete,
                CeilingMat = M.Concrete,
                RoofMat = M.Concrete,
                TrimMat = M.ConcreteDark,
                FrameMat = M.MetalDark,
                PartitionMat = M.Plaster,
                Roof = RoofKind.Flat,
                Parapet = 0f,
                RoofAccess = RoofAccess.Interior,
                RoofLootChance = 0.8f,
                SideDoors = 1,
                RoomWidth = 5f,
                Props = PropTheme.Office,
                LootArea = 15f,
                MaxLoot = 2
            };
            d.AddRow(1.6f, 1.2f, 0.95f, 2.8f);
            var balconyU = float.NaN;
            d.OnBeforeWindows = (cc, pp) => balconyU = PlanBalcony(cc, pp, 1);
            var p = StandardBox(c, d);
            BuildBalcony(c, p, 1, balconyU, Mathf.Max(3f, p.W - 2f));
            if (p.HasRoof && !c.Ruined)
                RoofMast(c, p, 6f);
            WallGadgets(c, p, false);
            Complete(c, p);
        }

        private static void RoofMast(Ctx c, BoxPlan p, float height)
        {
            var level = p.Floors;
            for (var attempt = 0; attempt < 6; attempt++)
            {
                var x = c.Chance(0.5f) ? p.Xi0 + 0.6f : p.Xi1 - 0.6f;
                var z = c.Chance(0.5f) ? p.Zi0 + 0.6f : p.Zi1 - 0.6f;
                var r = Centered(x, z, 0.8f, 0.8f);
                if (!c.IsFree(level, r))
                    continue;
                var y = p.TopY;
                c.B.VerticalCylinder(new Vector3(x, y, z), 0.07f, height, 6, M.MetalDark, StructureCollider.None);
                c.B.ColliderBox(new Vector3(x, y + height * 0.5f, z), new Vector3(0.16f, height, 0.16f), Quaternion.identity, M.MetalDark);
                for (var i = 1; i <= 2; i++)
                    c.B.Box(new Vector3(x, y + height * (0.55f + i * 0.15f), z), new Vector3(1.1f, 0.04f, 0.04f), M.MetalDark, StructureCollider.None);
                c.B.Box(new Vector3(x, y + 0.1f, z), new Vector3(0.5f, 0.2f, 0.5f), M.Concrete);
                c.Block(level, r);
                return;
            }
        }

        private static void ShepherdHut(Ctx c)
        {
            var s = c.Spec;
            c.DefaultName = "Çoban Kulübesi";
            var d = new BoxDesign
            {
                Width = Mathf.Clamp(s.Width, 3f, 6f),
                Depth = Mathf.Clamp(s.Depth, 3f, 5.5f),
                Floors = 1,
                FloorHeight = 2.8f,
                Wall = 0.45f,
                WallMat = c.Pick(M.StoneDark, M.Stone),
                FoundationMat = M.StoneDark,
                FloorMat = M.Dirt,
                CeilingMat = M.Wood,
                RoofMat = M.Dirt,
                TrimMat = M.WoodDark,
                Roof = RoofKind.Flat,
                RoofThickness = 0.35f,
                Parapet = 0.15f,
                Coping = false,
                BeamEnds = true,
                Lintels = true,
                WindowFrames = false,
                DoorSteps = false,
                Props = PropTheme.Hut,
                MaxProps = 2,
                LootArea = 10f,
                MaxLoot = 1,
                RuinAmount = 0.5f
            };
            var row = d.AddRow(0.5f, 0.5f, 1.2f, 3f);
            row.SideMask = (1 << 2) | (1 << 3);
            row.Skip = 0.4f;
            var p = StandardBox(c, d);

            if (!c.Ruined)
            {
                // Odun yığını (yan duvar dibinde)
                var f = p.F[c.Chance(0.5f) ? 2 : 3];
                var u = f.Length * 0.5f;
                var basePos = f.Point(u, 0f) + f.Outward * (p.T * 0.5f + 0.45f);
                var q = Quaternion.LookRotation(f.AlongX ? Vector3.forward : Vector3.right);
                for (var i = 0; i < 6; i++)
                {
                    var row2 = i / 3;
                    var col = i % 3 - 1;
                    var pos = basePos + q * new Vector3(0f, 0.15f + row2 * 0.26f, col * 0.27f);
                    c.B.Cylinder(pos, q * Quaternion.Euler(0f, 0f, 90f), 0.12f, 0.12f, 0.8f, 7, M.Bark, StructureCollider.None);
                }

                c.B.ColliderBox(basePos + Vector3.up * 0.3f, f.AlongX ? new Vector3(0.9f, 0.6f, 0.8f) : new Vector3(0.8f, 0.6f, 0.9f), Quaternion.identity, M.Wood);
            }

            Complete(c, p);
        }

        private static void Shed(Ctx c)
        {
            var s = c.Spec;
            c.DefaultName = "Baraka";
            var metal = c.Chance(0.5f);
            var d = new BoxDesign
            {
                Width = Mathf.Clamp(s.Width, 2.4f, 5f),
                Depth = Mathf.Clamp(s.Depth, 2.4f, 5f),
                Floors = 1,
                FloorHeight = 2.7f,
                Wall = 0.12f,
                WallMat = metal ? c.Pick(M.MetalPanel, M.Rust) : c.Pick(M.Wood, M.WoodDark),
                FoundationMat = M.Concrete,
                FloorMat = metal ? M.Concrete : M.Wood,
                TrimMat = M.WoodDark,
                FrameMat = metal ? M.MetalDark : M.WoodDark,
                Roof = RoofKind.Shed,
                RoofMat = c.Pick(M.RoofMetal, M.Rust),
                RoofPitch = 12f,
                Overhang = 0.25f,
                Ceiling = false,
                DoorSteps = false,
                Props = PropTheme.Industrial,
                MaxProps = 2,
                LootArea = 8f,
                MaxLoot = 1
            };
            var row = d.AddRow(0.6f, 0.6f, 1.3f, 3f);
            row.SideMask = (1 << 2) | (1 << 3);
            row.Skip = 0.5f;
            var p = StandardBox(c, d);
            Complete(c, p);
        }

        private static void Bunker(Ctx c)
        {
            var s = c.Spec;
            c.DefaultName = "Sığınak";
            var d = new BoxDesign
            {
                Width = Mathf.Clamp(s.Width, 4.5f, 10f),
                Depth = Mathf.Clamp(s.Depth, 4f, 8f),
                Floors = 1,
                FloorHeight = 2.7f,
                Wall = 0.6f,
                WallMat = M.ConcreteDark,
                FoundationMat = M.ConcreteDark,
                FloorMat = M.Concrete,
                CeilingMat = M.ConcreteDark,
                RoofMat = M.ConcreteDark,
                TrimMat = M.ConcreteDark,
                FrameMat = M.MetalDark,
                Roof = RoofKind.Flat,
                RoofThickness = 0.5f,
                Parapet = 0f,
                FrontDoors = 0,
                BackDoor = true,
                WindowFrames = false,
                Props = PropTheme.Bunker,
                MaxProps = 3,
                LootArea = 12f,
                MaxLoot = 2,
                RuinAmount = 0.35f
            };
            var slits = d.AddRow(0.9f, 0.25f, 1.25f, 1.6f);
            slits.Kind = OpeningKind.Slit;
            slits.SideMask = 1 | (1 << 2) | (1 << 3);
            slits.Margin = 0.3f;
            var p = StandardBox(c, d);
            var b = c.B;

            // Toprak setler (ön ve yanlar)
            Poly.Clear();
            Poly.Add(new Vector2(-0.3f, -0.6f));
            Poly.Add(new Vector2(2.4f, -0.6f));
            Poly.Add(new Vector2(-0.3f, 1.05f));
            b.Prism(Poly, new Vector3(0f, 0f, p.Dp * 0.5f), Vector3.forward, Vector3.up, p.W + 1.2f, M.Dirt);
            b.Prism(Poly, new Vector3(p.W * 0.5f, 0f, 0f), Vector3.right, Vector3.up, p.Dp + 1.2f, M.Dirt);
            b.Prism(Poly, new Vector3(-p.W * 0.5f, 0f, 0f), Vector3.left, Vector3.up, p.Dp + 1.2f, M.Dirt);

            // Arka kapı önünde patlama duvarı
            var back = p.F[1];
            for (var i = 0; i < back.Openings.Count; i++)
            {
                if (back.Meta[i].Kind != OpeningKind.Door)
                    continue;
                var o = back.Openings[i];
                FBox(c, back, o.Center, 1.0f - 0.25f, p.T * 0.5f + 1.7f, o.Width + 2.2f, 2.5f, 0.4f, M.ConcreteDark, true);
                break;
            }

            // Çatı kenarında kum torbaları
            if (!c.Ruined)
            {
                var y = p.TopY + 0.2f;
                var hw = p.W * 0.5f;
                var hd = p.Dp * 0.5f;
                b.Box(new Vector3(-hw * 0.45f, y, hd - 0.3f), new Vector3(hw * 0.9f, 0.4f, 0.45f), M.Sandbag);
                b.Box(new Vector3(hw * 0.5f, y, hd - 0.3f), new Vector3(hw * 0.7f, 0.4f, 0.45f), M.Sandbag);
                b.Box(new Vector3(hw - 0.3f, y, 0f), new Vector3(0.45f, 0.4f, hd * 1.2f), M.Sandbag);
                b.Box(new Vector3(-hw + 0.3f, y, -hd * 0.2f), new Vector3(0.45f, 0.4f, hd * 1.1f), M.Sandbag);
            }

            Complete(c, p);
        }

        private static void RadarStation(Ctx c)
        {
            var s = c.Spec;
            c.DefaultName = "Radar İstasyonu";
            var d = new BoxDesign
            {
                Width = Mathf.Clamp(s.Width, 7f, 14f),
                Depth = Mathf.Clamp(s.Depth, 6f, 10f),
                Floors = 1,
                FloorHeight = Mathf.Clamp(s.FloorHeight, 3.2f, 3.6f),
                Wall = 0.25f,
                WallMat = c.Pick(M.Concrete, M.MetalPanel, M.Plaster),
                FoundationMat = M.ConcreteDark,
                FloorMat = M.Concrete,
                CeilingMat = M.Concrete,
                RoofMat = M.Concrete,
                TrimMat = M.ConcreteDark,
                FrameMat = M.MetalDark,
                Roof = RoofKind.Flat,
                Parapet = 0f,
                RoofAccess = RoofAccess.Exterior,
                ExtStairOpen = true,
                RoofLootChance = 1f,
                SideDoors = c.Chance(0.5f) ? 1 : 0,
                RoomWidth = 5.5f,
                Props = PropTheme.Office,
                LootArea = 13f
            };
            d.AddRow(1.2f, 1.0f, 1.0f, 3.0f);
            var p = StandardBox(c, d);
            var b = c.B;

            if (p.HasRoof)
            {
                var level = p.Floors;
                var y = p.TopY;
                var radius = Mathf.Clamp(Mathf.Min(p.W, p.Dp) * 0.22f, 1.5f, 2.4f);
                var start = c.Int(0, 4);
                var radomePlaced = false;
                for (var i = 0; i < 4; i++)
                {
                    var corner = (start + i) % 4;
                    var sx = (corner & 1) == 0 ? -1f : 1f;
                    var sz = (corner & 2) == 0 ? -1f : 1f;
                    var x = sx * (p.W * 0.5f - radius - 0.4f);
                    var z = sz * (p.Dp * 0.5f - radius - 0.4f);
                    var rect = Centered(x, z, 2f * radius + 0.3f, 2f * radius + 0.3f);
                    if (!radomePlaced)
                    {
                        if (!c.IsFree(level, rect))
                            continue;
                        b.VerticalCylinder(new Vector3(x, y, z), radius * 0.6f, 1.2f, 10, M.Concrete);
                        b.SphereSection(new Vector3(x, y + 1.2f + radius * 0.55f, z), radius, -35f, 90f, 20, 8, M.White);
                        c.Block(level, rect);
                        radomePlaced = true;
                        continue;
                    }

                    var mr = Centered(sx * (p.W * 0.5f - 0.7f), sz * (p.Dp * 0.5f - 0.7f), 0.8f, 0.8f);
                    if (!c.IsFree(level, mr))
                        continue;
                    var mx = mr.center.x;
                    var mz = mr.center.y;
                    b.VerticalCylinder(new Vector3(mx, y, mz), 0.08f, 7f, 6, M.MetalDark, StructureCollider.None);
                    b.ColliderBox(new Vector3(mx, y + 3.5f, mz), new Vector3(0.18f, 7f, 0.18f), Quaternion.identity, M.MetalDark);
                    for (var k = 0; k < 3; k++)
                        b.Box(new Vector3(mx, y + 4.4f + k * 0.9f, mz), new Vector3(1.2f - k * 0.3f, 0.05f, 0.05f), M.MetalDark, StructureCollider.None);
                    b.Cylinder(new Vector3(mx, y + 5.6f, mz + 0.25f), Quaternion.Euler(70f, 0f, 0f), 0.45f, 0.45f, 0.05f, 12, M.White,
                        StructureCollider.None);
                    c.Block(level, mr);
                    break;
                }
            }

            Complete(c, p);
        }

        private static void Warehouse(Ctx c)
        {
            var s = c.Spec;
            c.DefaultName = "Depo";
            var d = new BoxDesign
            {
                Width = Mathf.Clamp(s.Width, 10f, 24f),
                Depth = Mathf.Clamp(s.Depth, 10f, 30f),
                Floors = 1,
                FloorHeight = Mathf.Clamp(s.FloorHeight * 1.7f, 5f, 7f),
                Wall = 0.3f,
                WallMat = c.Pick(M.Brick, M.Concrete, M.PlasterWarm),
                FoundationMat = M.ConcreteDark,
                FloorMat = M.Concrete,
                TrimMat = M.ConcreteDark,
                FrameMat = M.MetalDark,
                Roof = RoofKind.Gable,
                RoofMat = c.Pick(M.RoofMetal, M.Rust),
                RoofPitch = 16f,
                Overhang = 0.35f,
                Ceiling = false,
                AutoRidge = false,
                RidgeAlongX = false,
                BigWidth = 3.6f,
                BigHeight = 3.8f,
                BigSill = 0f,
                BackDoor = true,
                SideDoors = c.Chance(0.5f) ? 1 : 0,
                DoorSteps = false,
                Props = PropTheme.Industrial,
                PropArea = 25f,
                MaxProps = 6,
                LootArea = 30f,
                MaxLoot = 5
            };
            var row = d.AddRow(1.6f, 0.8f, d.FloorHeight - 1.5f, 3.5f);
            row.SideMask = (1 << 2) | (1 << 3);

            // Raf sıraları
            var racks = new List<Rect>(6);
            d.OnBeforeWindows = (cc, pp) =>
            {
                var len = Mathf.Clamp(pp.Zi1 - pp.Zi0 - 6.5f, 0f, 18f);
                if (len < 3f)
                    return;
                var z0 = pp.Zi0 + 1.6f;
                for (var x = pp.Xi0 + 2.0f; x + 1.0f <= pp.Xi1 - 2.0f; x += 3.7f)
                {
                    var r = Rect.MinMaxRect(x, z0, x + 1.0f, z0 + len);
                    if (!cc.IsFree(0, r, 0.3f))
                        continue;
                    racks.Add(r);
                    cc.Block(0, r);
                }
            };
            var p = StandardBox(c, d);
            for (var i = 0; i < racks.Count; i++)
                RackRow(c, racks[i]);
            HallTrusses(c, p);
            Complete(c, p);
        }

        private static void RackRow(Ctx c, Rect r)
        {
            var b = c.B;
            var y0 = Y0;
            var n = Mathf.Max(1, Mathf.RoundToInt(r.height / 2.4f));
            for (var i = 0; i <= n; i++)
            {
                var z = Mathf.Lerp(r.yMin + 0.04f, r.yMax - 0.04f, i / (float)n);
                b.Box(new Vector3(r.xMin + 0.04f, y0 + 1.3f, z), new Vector3(0.08f, 2.6f, 0.08f), M.MetalDark, StructureCollider.None);
                b.Box(new Vector3(r.xMax - 0.04f, y0 + 1.3f, z), new Vector3(0.08f, 2.6f, 0.08f), M.MetalDark, StructureCollider.None);
            }

            for (var l = 0; l < 3; l++)
            {
                var y = y0 + 0.15f + l * 1.1f;
                b.Box(new Vector3(r.center.x, y, r.center.y), new Vector3(r.width, 0.05f, r.height), M.Wood, StructureCollider.None);
                var z = r.yMin + 0.1f;
                while (z < r.yMax - 0.6f)
                {
                    var sz = c.Range(0.6f, 1.0f);
                    if (z + sz > r.yMax - 0.05f)
                        break;
                    if (c.Chance(0.7f))
                    {
                        var hgt = c.Range(0.4f, 0.85f);
                        b.Box(new Vector3(r.center.x, y + 0.025f + hgt * 0.5f, z + sz * 0.5f), new Vector3(r.width - 0.15f, hgt, sz - 0.05f),
                            c.Chance(0.3f) ? M.VehicleOlive : M.Wood, StructureCollider.None);
                    }

                    z += sz + 0.08f;
                }
            }

            b.ColliderBox(new Vector3(r.center.x, y0 + 1.3f, r.center.y), new Vector3(r.width, 2.6f, r.height), Quaternion.identity, M.MetalDark);
        }

        /// <summary>Açık salon çatısı altına çelik makaslar (görsel).</summary>
        private static void HallTrusses(Ctx c, BoxPlan p)
        {
            if (!p.HasRoof || p.Flat || p.D.Roof != RoofKind.Gable)
                return;
            var b = c.B;
            var ridgeX = p.D.AutoRidge ? p.W >= p.Dp : p.D.RidgeAlongX;
            var spanLen = ridgeX ? p.Zi1 - p.Zi0 : p.Xi1 - p.Xi0;
            var runLen = ridgeX ? p.Xi1 - p.Xi0 : p.Zi1 - p.Zi0;
            var n = Mathf.Max(1, Mathf.RoundToInt(runLen / 5f));
            var y = p.TopY;
            var rise = p.RoofRise;
            var tan = Mathf.Tan(Mathf.Clamp(p.D.RoofPitch, 5f, 45f) * Mathf.Deg2Rad);
            for (var i = 1; i < n; i++)
            {
                var t = -runLen * 0.5f + runLen * i / n;
                Vector3 P(float acrossCoord, float yy) => ridgeX ? new Vector3(t, yy, acrossCoord) : new Vector3(acrossCoord, yy, t);
                var half = spanLen * 0.5f;
                b.Beam(P(-half, y - 0.1f), P(half, y - 0.1f), 0.12f, 0.18f, M.MetalDark);
                b.Beam(P(-half, y + p.T * tan - 0.05f), P(0f, y + rise - 0.2f), 0.12f, 0.18f, M.MetalDark);
                b.Beam(P(half, y + p.T * tan - 0.05f), P(0f, y + rise - 0.2f), 0.12f, 0.18f, M.MetalDark);
                b.Beam(P(0f, y - 0.1f), P(0f, y + rise - 0.2f), 0.1f, 0.1f, M.MetalDark);
            }
        }

        private static void Hangar(Ctx c)
        {
            var s = c.Spec;
            c.DefaultName = "Hangar";
            var d = new BoxDesign
            {
                Width = Mathf.Clamp(s.Width, 16f, 34f),
                Depth = Mathf.Clamp(s.Depth, 16f, 40f),
                Floors = 1,
                FloorHeight = Mathf.Clamp(Mathf.Max(s.FloorHeight * 2.2f, 6.5f), 6.5f, 9f),
                Wall = 0.2f,
                WallMat = M.MetalPanel,
                HasLowerWall = true,
                LowerWallMat = M.Concrete,
                LowerWallHeight = 1.0f,
                FoundationMat = M.Concrete,
                FloorMat = M.Concrete,
                TrimMat = M.MetalDark,
                FrameMat = M.MetalDark,
                PartitionMat = M.Plaster,
                StairMat = M.MetalPanel,
                Roof = RoofKind.Gable,
                RoofMat = M.RoofMetal,
                RoofPitch = 14f,
                Overhang = 0.3f,
                Ceiling = false,
                AutoRidge = false,
                RidgeAlongX = false,
                FrontDoors = 0,
                SideDoors = 2,
                DoorSteps = false,
                Props = PropTheme.Industrial,
                PropArea = 22f,
                MaxProps = 14,
                LootArea = 45f,
                MaxLoot = 6
            };
            d.BigWidth = Mathf.Min(d.Width - 4f, 24f);
            d.BigHeight = d.FloorHeight - 1.4f;
            var row = d.AddRow(2.4f, 0.9f, d.FloorHeight - 1.7f, 4.5f);
            row.SideMask = (1 << 2) | (1 << 3);

            var office = default(Rect);
            var officeOk = false;
            const float officeH = 3.0f;
            var run = officeH / Mathf.Tan(StructureKit.PreferredStairAngle * Mathf.Deg2Rad);
            d.OnPlanStart = (cc, pp) =>
            {
                var ow = Mathf.Min(6f, (pp.Xi1 - pp.Xi0) * 0.35f);
                var od = Mathf.Min(4.5f, (pp.Zi1 - pp.Zi0) * 0.3f);
                if (od + run + 1.5f > pp.Zi1 - pp.Zi0 - 3f || ow < StairW + 2.5f)
                    return;
                office = Rect.MinMaxRect(pp.Xi0, pp.Zi0, pp.Xi0 + ow, pp.Zi0 + od);
                cc.Block(0, office);
                cc.Block(0, Rect.MinMaxRect(pp.Xi0, office.yMax, pp.Xi0 + StairW + 0.1f, office.yMax + run + 1.1f));
                pp.F[3].Reserve(0, 0f, office.yMax + run + 1.4f - pp.Zi0);
                pp.F[1].Reserve(0, pp.F[1].ToU(office.xMin) - 0.3f, pp.F[1].ToU(office.xMax) + 0.3f);
                officeOk = true;
            };
            var p = StandardBox(c, d);
            var b = c.B;

            // Yan kolonlar
            var zs = p.Zi0 + 3f;
            while (zs < p.Zi1 - 2.5f)
            {
                for (var sgn = -1; sgn <= 1; sgn += 2)
                {
                    var x = sgn < 0 ? p.Xi0 + 0.18f : p.Xi1 - 0.18f;
                    var r = Centered(x, zs, 0.36f, 0.36f);
                    if (!c.IsFree(0, r))
                        continue;
                    b.Box(new Vector3(x, (Y0 + p.TopY) * 0.5f, zs), new Vector3(0.3f, p.TopY - Y0, 0.3f), M.MetalDark);
                    c.Block(0, r);
                }

                zs += 6f;
            }

            HallTrusses(c, p);

            if (officeOk)
            {
                var ox0 = office.xMin;
                var ox1 = office.xMax;
                var oz0 = office.yMin;
                var oz1 = office.yMax;
                var wallH = officeH - 0.2f;
                TmpOpenings.Clear();
                var doorU = Mathf.Clamp((ox1 - ox0) * 0.62f, StairW + 1.0f, ox1 - ox0 - 0.8f);
                TmpOpenings.Add(new WallOpening(doorU, InnerDoorW, 0f, InnerDoorH));
                TmpOpenings.Add(new WallOpening(Mathf.Max(StairW + 0.2f + 0.6f, doorU - 1.9f), 1.1f, 1.0f, 1.0f));
                b.Wall(new Vector3(ox0, 0f, oz1 - 0.075f), new Vector3(ox1, 0f, oz1 - 0.075f), Y0, wallH, 0.15f, d.PartitionMat, TmpOpenings,
                    c.Ruined ? c.Rng : null, c.Ruined ? 0.4f : 0f);
                TmpOpenings.Clear();
                TmpOpenings.Add(new WallOpening((oz1 - oz0) * 0.5f, 1.2f, 1.0f, 1.0f));
                b.Wall(new Vector3(ox1 - 0.075f, 0f, oz0), new Vector3(ox1 - 0.075f, 0f, oz1), Y0, wallH, 0.15f, d.PartitionMat, TmpOpenings,
                    c.Ruined ? c.Rng : null, c.Ruined ? 0.4f : 0f);
                b.Slab(ox0 - p.T * 0.5f, oz0 - p.T * 0.5f, ox1, oz1, Y0 + officeH, 0.2f, M.Concrete);
                b.Railing(new Vector3(ox0 + StairW, 0f, oz1 - 0.05f), new Vector3(ox1, 0f, oz1 - 0.05f), Y0 + officeH, RailH, M.MetalDark);
                b.Railing(new Vector3(ox1 - 0.05f, 0f, oz0), new Vector3(ox1 - 0.05f, 0f, oz1), Y0 + officeH, RailH, M.MetalDark);
                b.OpenStairs(new Vector3(ox0 + StairW * 0.5f, Y0, oz1 + run), 180f, StairW, officeH, run, M.MetalPanel, M.MetalDark, true, false);

                var officeRoom = Rect.MinMaxRect(ox0, oz0, ox1 - 0.15f, oz1 - 0.15f);
                c.Block(LevelOffice, Rect.MinMaxRect(ox0 + doorU - 1f, oz1 - 1.3f, ox0 + doorU + 1f, oz1));
                PlaceProp(c, null, LevelOffice, officeRoom, Y0, PropTheme.Office, 0);
                PlaceLoot(c, LevelOffice, officeRoom, Y0, 1);
                var mezz = Rect.MinMaxRect(ox0, oz0, ox1 - 0.1f, oz1 - 0.1f);
                c.Block(LevelMezzanine, Rect.MinMaxRect(ox0, oz1 - 1.3f, ox0 + StairW + 0.6f, oz1));
                PlaceProp(c, null, LevelMezzanine, mezz, Y0 + officeH, PropTheme.Industrial, 0);
                PlaceLoot(c, LevelMezzanine, mezz, Y0 + officeH, 1);
            }

            Complete(c, p);
        }

        private static void FactoryHall(Ctx c)
        {
            var s = c.Spec;
            c.DefaultName = "Fabrika";
            var d = new BoxDesign
            {
                Width = Mathf.Clamp(s.Width, 16f, 32f),
                Depth = Mathf.Clamp(s.Depth, 18f, 40f),
                Floors = 1,
                FloorHeight = Mathf.Clamp(s.FloorHeight * 2.5f, 7f, 10f),
                Wall = 0.35f,
                WallMat = c.Chance(0.7f) ? M.Brick : M.ConcreteDark,
                FoundationMat = M.ConcreteDark,
                FloorMat = M.Concrete,
                CeilingMat = M.Concrete,
                RoofMat = M.Concrete,
                TrimMat = M.Concrete,
                FrameMat = M.MetalDark,
                Roof = RoofKind.Flat,
                RoofThickness = 0.3f,
                Parapet = 0.6f,
                BigWidth = 4.5f,
                BigHeight = 4.5f,
                BigSill = 0f,
                SideDoors = 1,
                DoorSteps = false,
                Props = PropTheme.Industrial,
                PropArea = 28f,
                MaxProps = 10,
                LootArea = 40f,
                MaxLoot = 6
            };
            var row = d.AddRow(1.8f, Mathf.Min(3.2f, d.FloorHeight - 3f), 1.4f, 4f);
            row.SideMask = 1 | (1 << 2) | (1 << 3);

            var walkY = Mathf.Clamp(d.FloorHeight * 0.5f, 3.6f, 4.5f);
            var run = walkY / Mathf.Tan(StructureKit.PreferredStairAngle * Mathf.Deg2Rad);
            const float walkDepth = 2.4f;
            var walkOk = false;
            d.OnPlanStart = (cc, pp) =>
            {
                if (walkDepth + run + 4f > pp.Zi1 - pp.Zi0)
                    return;
                var z1 = pp.Zi0 + walkDepth;
                cc.Block(0, Rect.MinMaxRect(pp.Xi0, z1, pp.Xi0 + StairW + 0.1f, z1 + run + 1.1f));
                pp.F[3].Reserve(0, 0f, z1 + run + 1.4f - pp.Zi0);
                walkOk = true;
            };
            var p = StandardBox(c, d);
            var b = c.B;

            if (walkOk)
            {
                var z1 = p.Zi0 + walkDepth;
                var y = Y0 + walkY;
                b.Slab(p.Xi0 - p.T * 0.5f, p.Zi0 - p.T * 0.5f, p.Xi1 + p.T * 0.5f, z1, y, 0.2f, M.MetalPanel);
                b.Railing(new Vector3(p.Xi0 + StairW, 0f, z1 - 0.05f), new Vector3(p.Xi1, 0f, z1 - 0.05f), y, RailH, M.MetalDark);
                for (var x = p.Xi0 + 4f; x < p.Xi1 - 1f; x += 4.5f)
                {
                    var r = Centered(x, z1 - 0.15f, 0.3f, 0.3f);
                    b.Box(new Vector3(x, (Y0 + y - 0.2f) * 0.5f, z1 - 0.15f), new Vector3(0.22f, y - 0.2f - Y0, 0.22f), M.MetalDark);
                    c.Block(0, r);
                }

                b.OpenStairs(new Vector3(p.Xi0 + StairW * 0.5f, Y0, z1 + run), 180f, StairW, walkY, run, M.MetalPanel, M.MetalDark, true, false);
                var walk = Rect.MinMaxRect(p.Xi0 + StairW + 0.4f, p.Zi0, p.Xi1, z1 - 0.1f);
                var n = Mathf.Clamp(Mathf.RoundToInt(walk.width / 9f), 1, 3);
                PlaceProp(c, null, LevelPlatform, walk, y, PropTheme.Industrial, 0);
                PlaceLoot(c, LevelPlatform, walk, y, n);
            }

            // Testere dişi çatı ışıklıkları
            if (p.HasRoof)
            {
                const float tooth = 4f;
                const float th = 1.6f;
                var count = Mathf.FloorToInt((p.Dp - 2f) / tooth);
                var width = p.W - 2f * p.T - 0.4f;
                for (var i = 0; i < count; i++)
                {
                    var z0 = -p.Dp * 0.5f + 1f + i * tooth;
                    Poly.Clear();
                    Poly.Add(new Vector2(z0, 0f));
                    Poly.Add(new Vector2(z0 + tooth - 0.1f, 0f));
                    Poly.Add(new Vector2(z0, th));
                    b.Prism(Poly, new Vector3(0f, p.TopY, 0f), Vector3.forward, Vector3.up, width, M.RoofMetal);
                    b.Box(new Vector3(0f, p.TopY + th * 0.5f, z0 - 0.02f), new Vector3(width - 0.3f, th - 0.25f, 0.03f), M.Glass, StructureCollider.None);
                }
            }

            // Tuğla baca
            if (!c.Ruined || c.Chance(0.5f))
            {
                var cx = p.W * 0.5f + 2.2f;
                var cz = -p.Dp * 0.5f + 2.2f;
                var h = c.Ruined ? 9f : c.Range(18f, 24f);
                b.Box(new Vector3(cx, -0.5f, cz), new Vector3(3f, 3f, 3f), M.Concrete);
                b.Cylinder(new Vector3(cx, 1f + h * 0.5f, cz), Quaternion.identity, 1.1f, 0.75f, h, 12, M.Brick);
                if (!c.Ruined)
                    b.Cylinder(new Vector3(cx, 1f + h + 0.2f, cz), Quaternion.identity, 0.85f, 0.85f, 0.4f, 12, M.ConcreteDark, StructureCollider.None);
            }

            Complete(c, p);
        }

        private static void Barn(Ctx c)
        {
            var s = c.Spec;
            c.DefaultName = "Ahır";
            var d = new BoxDesign
            {
                Width = Mathf.Clamp(s.Width, 8f, 18f),
                Depth = Mathf.Clamp(s.Depth, 6f, 12f),
                Floors = 1,
                FloorHeight = Mathf.Clamp(s.FloorHeight * 1.5f, 4.6f, 5.6f),
                Wall = 0.22f,
                WallMat = c.Pick(M.Wood, M.WoodDark),
                HasLowerWall = true,
                LowerWallMat = M.StoneDark,
                LowerWallHeight = 1.2f,
                FoundationMat = M.StoneDark,
                FloorMat = M.Dirt,
                TrimMat = M.WoodDark,
                FrameMat = M.WoodDark,
                RailMat = M.WoodDark,
                StairMat = M.Wood,
                Roof = RoofKind.Gable,
                RoofMat = c.Pick(M.RoofTile, M.Rust),
                RoofPitch = c.Range(24f, 32f),
                Overhang = 0.5f,
                Ceiling = false,
                FrontDoors = 0,
                SideDoors = 1,
                BigWidth = 3.0f,
                BigHeight = 3.2f,
                BigSill = 0f,
                DoorSteps = false,
                WindowFrames = false,
                Props = PropTheme.Farm,
                PropArea = 12f,
                MaxProps = 6,
                LootArea = 22f,
                MaxLoot = 3
            };
            var vent = d.AddRow(0.7f, 0.5f, 3.4f, 3.2f);
            vent.Skip = 0.2f;

            const float loftH = 2.8f;
            var run = loftH / Mathf.Tan(StructureKit.PreferredStairAngle * Mathf.Deg2Rad);
            var loftOk = false;
            var loftEdge = 0f;
            var xb = 0f;
            d.OnPlanStart = (cc, pp) =>
            {
                var dInt = pp.Zi1 - pp.Zi0;
                var ld = Mathf.Min(dInt * 0.42f, 4f);
                xb = pp.Xi0 + 0.1f + 1.0f + run;
                if (ld < 2f || xb + 1.2f > pp.Xi1)
                    return;
                loftEdge = pp.Zi0 + ld;
                cc.Block(0, Rect.MinMaxRect(pp.Xi0, loftEdge, xb + 1.2f, loftEdge + StairW + 0.1f));
                pp.F[3].Reserve(0, loftEdge - 0.4f - pp.Zi0, loftEdge + StairW + 0.4f - pp.Zi0);
                loftOk = true;
            };
            var p = StandardBox(c, d);
            var b = c.B;

            if (loftOk)
            {
                var y = Y0 + loftH;
                b.Slab(-p.W * 0.5f + p.T * 0.5f, -p.Dp * 0.5f + p.T * 0.5f, p.W * 0.5f - p.T * 0.5f, loftEdge, y, 0.15f, M.Wood);
                for (var x = p.Xi0 + 1.6f; x < p.Xi1 - 0.8f; x += 3f)
                {
                    if (x < xb + 1.3f && x > p.Xi0 + 0.2f && x < xb - run - 0.1f)
                        continue;
                    var r = Centered(x, loftEdge - 0.12f, 0.24f, 0.24f);
                    b.Box(new Vector3(x, (Y0 + y - 0.15f) * 0.5f, loftEdge - 0.12f), new Vector3(0.2f, y - 0.15f - Y0, 0.2f), M.WoodDark);
                    c.Block(0, r);
                }

                var top = xb - run;
                b.OpenStairs(new Vector3(xb, Y0, loftEdge + StairW * 0.5f), 270f, StairW, loftH, run, M.Wood, M.WoodDark, true, true);
                var lx0 = p.Xi0 + 0.1f;
                b.Box(new Vector3((lx0 + top) * 0.5f, y - 0.06f, loftEdge + StairW * 0.5f), new Vector3(top - lx0, 0.12f, StairW), M.Wood);
                b.Box(new Vector3(lx0 + 0.1f, (Y0 + y - 0.12f) * 0.5f, loftEdge + StairW - 0.1f), new Vector3(0.14f, y - 0.12f - Y0, 0.14f), M.WoodDark);
                b.Railing(new Vector3(lx0, 0f, loftEdge + StairW - 0.03f), new Vector3(top, 0f, loftEdge + StairW - 0.03f), y, RailH, M.WoodDark);
                b.Railing(new Vector3(top, 0f, loftEdge - 0.05f), new Vector3(p.Xi1, 0f, loftEdge - 0.05f), y, RailH, M.WoodDark);

                var loft = Rect.MinMaxRect(p.Xi0, Mathf.Max(p.Zi0, loftEdge - 2.4f), p.Xi1, loftEdge - 0.1f);
                c.Block(LevelLoft, Rect.MinMaxRect(p.Xi0, loftEdge - 1.2f, top + 0.6f, loftEdge));
                PlaceProp(c, null, LevelLoft, loft, y, PropTheme.Farm, 0);
                PlaceLoot(c, LevelLoft, loft, y, 1);
            }

            HallTrusses(c, p);
            Complete(c, p);
        }

        private static void Mosque(Ctx c)
        {
            var s = c.Spec;
            c.DefaultName = "Cami";
            var size = Mathf.Clamp((s.Width + s.Depth) * 0.5f, 9f, 16f);
            var white = c.Chance(0.6f);
            var d = new BoxDesign
            {
                Width = size,
                Depth = size,
                Floors = 1,
                FloorHeight = Mathf.Clamp(size * 0.5f, 5.4f, 7f),
                Wall = 0.5f,
                WallMat = white ? M.Plaster : M.Stone,
                FoundationMat = M.StoneDark,
                FloorMat = M.Carpet,
                CeilingMat = M.Plaster,
                RoofMat = M.Concrete,
                TrimMat = white ? M.Stone : M.StoneDark,
                FrameMat = M.WoodDark,
                Roof = RoofKind.Flat,
                RoofThickness = 0.35f,
                Parapet = 0.35f,
                CenterDoor = true,
                DoorWidth = 1.6f,
                DoorHeight = 2.6f,
                Props = PropTheme.None,
                LootArea = 22f,
                MaxLoot = 3,
                RuinAmount = 0.5f
            };
            d.AddRow(1.0f, 2.0f, 1.1f, 2.8f);
            d.AddRow(0.7f, 0.9f, 3.9f, 2.8f);
            const float minbarX = 1.75f;
            const float MinbarRise = 2.0f;
            var MinbarRun = MinbarRise / Mathf.Tan(StructureKit.MaxStairAngle * Mathf.Deg2Rad) + 0.05f;
            d.OnPlanStart = (cc, pp) =>
            {
                var back = pp.F[1];
                var u = pp.W * 0.5f;
                AddOpening(pp, 1, 0, u, 1.3f, 0f, 2.7f, OpeningKind.Niche);
                back.Reserve(0, u - 1.2f, u + minbarX + 0.9f);
                cc.Block(0, InsideRect(pp, back, u, 2.0f, 1.3f));
                cc.Block(0, Rect.MinMaxRect(minbarX - 0.6f, pp.Zi0, minbarX + 0.6f, pp.Zi0 + 0.6f + MinbarRun + 0.4f));
            };
            var p = StandardBox(c, d);
            var b = c.B;
            var hw = p.W * 0.5f;
            var hd = p.Dp * 0.5f;

            // Mihrap çıkıntısı ve çerçevesi
            b.Box(new Vector3(0f, 1.25f, -(hd + 0.35f)), new Vector3(1.9f, 3.5f, 0.7f), d.WallMat);
            if (!c.Ruined)
            {
                b.Box(new Vector3(-0.78f, Y0 + 1.45f, p.Zi0 + 0.05f), new Vector3(0.2f, 2.9f, 0.1f), d.TrimMat, StructureCollider.None);
                b.Box(new Vector3(0.78f, Y0 + 1.45f, p.Zi0 + 0.05f), new Vector3(0.2f, 2.9f, 0.1f), d.TrimMat, StructureCollider.None);
                b.Box(new Vector3(0f, Y0 + 3.0f, p.Zi0 + 0.05f), new Vector3(1.76f, 0.3f, 0.1f), d.TrimMat, StructureCollider.None);
            }

            // Minber (≤ 33° rampa)
            {
                const float mw = 0.85f;
                var front = p.Zi0 + 0.6f + MinbarRun;
                b.SolidStairs(new Vector3(minbarX, Y0, front), 180f, mw, MinbarRise, MinbarRun, M.WoodDark, 0f, true);
                b.Box(new Vector3(minbarX, Y0 + MinbarRise * 0.5f, p.Zi0 + 0.3f), new Vector3(mw, MinbarRise, 0.6f), M.WoodDark);
                Poly.Clear();
                Poly.Add(new Vector2(p.Zi0, Y0));
                Poly.Add(new Vector2(front, Y0));
                Poly.Add(new Vector2(front, Y0 + 0.9f));
                Poly.Add(new Vector2(p.Zi0 + 0.6f, Y0 + 2.9f));
                Poly.Add(new Vector2(p.Zi0, Y0 + 2.9f));
                b.Prism(Poly, new Vector3(minbarX - mw * 0.5f - 0.03f, 0f, 0f), Vector3.forward, Vector3.up, 0.05f, M.WoodDark, StructureCollider.None);
                b.Prism(Poly, new Vector3(minbarX + mw * 0.5f + 0.03f, 0f, 0f), Vector3.forward, Vector3.up, 0.05f, M.WoodDark, StructureCollider.None);
                if (!c.Ruined)
                {
                    for (var i = 0; i < 4; i++)
                    {
                        var px = minbarX + ((i & 1) == 0 ? -0.38f : 0.38f);
                        var pz = p.Zi0 + ((i & 2) == 0 ? 0.05f : 0.55f);
                        b.Box(new Vector3(px, Y0 + 2.45f, pz), new Vector3(0.05f, 0.9f, 0.05f), M.WoodDark, StructureCollider.None);
                    }

                    b.Cylinder(new Vector3(minbarX, Y0 + 3.3f, p.Zi0 + 0.3f), Quaternion.identity, 0.55f, 0f, 0.9f, 8, M.MosqueDome,
                        StructureCollider.None);
                }
            }

            // Son cemaat yeri (revak)
            const float pd = 3.0f;
            const float ph = 3.3f;
            b.Box(new Vector3(0f, (Y0 - StructureKit.FoundationDepth) * 0.5f, hd + pd * 0.5f), new Vector3(p.W, Y0 + StructureKit.FoundationDepth, pd),
                M.Stone);
            var cols = size >= 13f ? 6 : 4;
            for (var i = 0; i < cols; i++)
            {
                var x = -hw + 0.45f + i * (p.W - 0.9f) / (cols - 1);
                var z = hd + pd - 0.35f;
                var broken = c.Ruined && c.Chance(0.4f);
                var colH = broken ? c.Range(0.8f, 2.2f) : ph;
                b.Box(new Vector3(x, Y0 + 0.1f, z), new Vector3(0.6f, 0.2f, 0.6f), d.TrimMat);
                b.VerticalCylinder(new Vector3(x, Y0 + 0.2f, z), 0.22f, colH - 0.2f, 12, d.TrimMat == M.StoneDark ? M.Stone : M.White);
                if (!broken)
                    b.Box(new Vector3(x, Y0 + ph - 0.1f, z), new Vector3(0.55f, 0.2f, 0.55f), d.TrimMat, StructureCollider.None);
            }

            if (!c.Ruined)
            {
                b.Box(new Vector3(0f, Y0 + ph + 0.15f, hd + pd * 0.5f + 0.05f), new Vector3(p.W + 0.2f, 0.3f, pd + 0.1f), M.Plaster);
                var span = (p.W - 0.9f) / (cols - 1);
                for (var i = 0; i < cols - 1; i++)
                {
                    var x = -hw + 0.45f + (i + 0.5f) * span;
                    b.Dome(new Vector3(x, Y0 + ph + 0.3f, hd + pd * 0.5f), Mathf.Min(pd * 0.38f, span * 0.42f), 12, M.MosqueDome,
                        StructureCollider.None);
                }
            }

            // Kubbe
            var rd = (size * 0.5f - p.T) * 0.8f;
            if (p.HasRoof)
            {
                b.Cylinder(new Vector3(0f, p.TopY + 0.5f, 0f), Quaternion.Euler(0f, 22.5f, 0f), rd, rd, 1.0f, 8, d.WallMat);
                b.Dome(new Vector3(0f, p.TopY + 1.0f, 0f), rd * 0.97f, 24, M.MosqueDome);
                var top = p.TopY + 1.0f + rd * 0.97f;
                Alem(c, new Vector3(0f, top - 0.05f, 0f));
                for (var i = 0; i < 4; i++)
                {
                    var x = ((i & 1) == 0 ? -1f : 1f) * (hw - p.T * 0.5f);
                    var z = ((i & 2) == 0 ? -1f : 1f) * (hd - p.T * 0.5f);
                    b.Cylinder(new Vector3(x, p.WallTop + 0.07f + 0.45f, z), Quaternion.identity, 0.3f, 0f, 0.9f, 8, d.TrimMat, StructureCollider.None);
                }
            }

            // Minare
            {
                var mx = hw + 1.3f;
                var mz = hd - 1.3f;
                var baseTop = Y0 + 3.0f;
                b.Box(new Vector3(mx, (baseTop - StructureKit.FoundationDepth) * 0.5f, mz), new Vector3(2.1f, baseTop + StructureKit.FoundationDepth, 2.1f),
                    d.TrimMat);
                b.Cylinder(new Vector3(mx, baseTop + 0.3f, mz), Quaternion.Euler(0f, 22.5f, 0f), 1.05f, 0.85f, 0.6f, 8, d.TrimMat);
                var hs = Mathf.Clamp(size, 10f, 15f);
                if (c.Ruined)
                    hs *= c.Range(0.3f, 0.55f);
                var shaftBase = baseTop + 0.6f;
                b.Cylinder(new Vector3(mx, shaftBase + hs * 0.5f, mz), Quaternion.identity, 0.82f, 0.76f, hs, 12, d.WallMat);
                if (!c.Ruined)
                {
                    var y1 = shaftBase + hs;
                    b.Cylinder(new Vector3(mx, y1 - 0.3f, mz), Quaternion.identity, 0.76f, 1.25f, 0.6f, 16, d.TrimMat, StructureCollider.None);
                    b.Cylinder(new Vector3(mx, y1 + 0.125f, mz), Quaternion.identity, 1.3f, 1.3f, 0.25f, 16, d.TrimMat);
                    b.Cylinder(new Vector3(mx, y1 + 0.25f + 0.45f, mz), Quaternion.identity, 1.28f, 1.28f, 0.9f, 16, d.WallMat);
                    b.Cylinder(new Vector3(mx, y1 + 0.25f + 1.3f, mz), Quaternion.identity, 0.6f, 0.58f, 2.6f, 12, d.WallMat);
                    b.Cylinder(new Vector3(mx, y1 + 2.85f + 1.6f, mz), Quaternion.identity, 0.78f, 0f, 3.2f, 12, M.MosqueDome);
                    Alem(c, new Vector3(mx, y1 + 2.85f + 3.2f - 0.05f, mz));
                }
                else
                {
                    b.Rubble(new Vector3(mx - 1.4f, 0f, mz + 0.5f), 1.2f, 8, d.WallMat, c.Rng);
                }
            }

            Complete(c, p);
            if (!c.Ruined || c.Chance(0.5f))
            {
                var x = c.Range(-hw + 1f, hw - 1f);
                c.Loot.Add(new Vector3(x, Y0 + 0.05f, hd + pd * 0.45f));
            }
        }

        private static void Alem(Ctx c, Vector3 basePos)
        {
            c.B.VerticalCylinder(basePos, 0.05f, 0.9f, 6, M.Yellow, StructureCollider.None);
            c.B.SphereSection(basePos + Vector3.up * 0.35f, 0.13f, -90f, 90f, 8, 4, M.Yellow, StructureCollider.None);
            c.B.SphereSection(basePos + Vector3.up * 0.65f, 0.1f, -90f, 90f, 8, 4, M.Yellow, StructureCollider.None);
        }

        private static void WatchTower(Ctx c)
        {
            var s = c.Spec;
            var b = c.B;
            c.DefaultName = "Nöbet Kulesi";
            var size = Mathf.Clamp(Mathf.Min(s.Width, s.Depth), 3.2f, 5f);
            var h = s.Floors >= 2 ? Mathf.Clamp(s.Floors * s.FloorHeight, 5f, 9.5f) : Mathf.Clamp(s.FloorHeight * 1.9f, 5f, 7f);
            var concrete = c.Chance(0.5f);
            var legMat = concrete ? M.Concrete : M.WoodDark;
            var deckMat = concrete ? M.Concrete : M.Wood;
            var frameMat = concrete ? M.MetalDark : M.WoodDark;
            var half = size * 0.5f;
            const float deckT = 0.25f;
            const float leg = 0.34f;
            var li = half - 0.22f;

            // Ayaklar ve temel pabuçları
            for (var i = 0; i < 4; i++)
            {
                var x = ((i & 1) == 0 ? -1f : 1f) * li;
                var z = ((i & 2) == 0 ? -1f : 1f) * li;
                var bottom = -StructureKit.FoundationDepth;
                var top = h - deckT;
                b.Box(new Vector3(x, (bottom + top) * 0.5f, z), new Vector3(leg, top - bottom, leg), legMat);
                b.Box(new Vector3(x, 0.05f, z), new Vector3(0.75f, 0.5f, 0.75f), M.Concrete);
            }

            // Çapraz bağlantılar (görsel)
            var tiers = h > 6.5f ? 3 : 2;
            for (var side = 0; side < 4; side++)
            {
                Vector3 A(float y) => side switch
                {
                    0 => new Vector3(-li, y, -li),
                    1 => new Vector3(li, y, -li),
                    2 => new Vector3(li, y, li),
                    _ => new Vector3(-li, y, li)
                };
                Vector3 B(float y) => side switch
                {
                    0 => new Vector3(li, y, -li),
                    1 => new Vector3(li, y, li),
                    2 => new Vector3(-li, y, li),
                    _ => new Vector3(-li, y, -li)
                };
                for (var t = 0; t < tiers; t++)
                {
                    var y0 = 0.4f + (h - deckT - 0.6f) * t / tiers;
                    var y1 = 0.4f + (h - deckT - 0.6f) * (t + 1) / tiers;
                    b.Beam(A(y0), B(y1), 0.08f, 0.08f, frameMat);
                    b.Beam(A(y1), B(y0), 0.08f, 0.08f, frameMat);
                }

                b.Beam(A(h - deckT - 0.1f), B(h - deckT - 0.1f), 0.12f, 0.16f, frameMat);
            }

            // Platform
            b.Box(new Vector3(0f, h - deckT * 0.5f, 0f), new Vector3(size, deckT, size), deckMat);

            // Dönüşlü açık merdiven kulesi (+Z tarafında)
            var flights = Mathf.Max(2, Mathf.CeilToInt(h / 1.75f));
            var rise = h / flights;
            var run = rise / Mathf.Tan(StructureKit.PreferredStairAngle * Mathf.Deg2Rad);
            const float sw = 1.1f;
            const float land = 1.2f;
            var topNear = (flights & 1) == 0;
            var x0 = topNear ? -land * 0.5f : -(land * 1.5f + run);
            var z0 = half;
            for (var i = 0; i < flights; i++)
            {
                var yb = i * rise;
                if ((i & 1) == 0)
                    b.OpenStairs(new Vector3(x0 + land, yb, z0 + sw * 0.5f), 90f, sw, rise, run, deckMat, frameMat, true, true);
                else
                    b.OpenStairs(new Vector3(x0 + land + run, yb, z0 + sw * 1.5f), 270f, sw, rise, run, deckMat, frameMat, true, true);

                var ly = (i + 1) * rise;
                var far = (i & 1) == 0;
                var lx0 = far ? x0 + land + run : x0;
                var lx1 = lx0 + land;
                b.Box(new Vector3((lx0 + lx1) * 0.5f, ly - 0.05f, z0 + sw), new Vector3(land, 0.1f, 2f * sw), deckMat);
                var endX = far ? lx1 - 0.03f : lx0 + 0.03f;
                b.Railing(new Vector3(endX, 0f, z0), new Vector3(endX, 0f, z0 + 2f * sw), ly, RailH, frameMat);
                b.Railing(new Vector3(lx0, 0f, z0 + 2f * sw - 0.03f), new Vector3(lx1, 0f, z0 + 2f * sw - 0.03f), ly, RailH, frameMat);
                if (i < flights - 1)
                    b.Railing(new Vector3(lx0, 0f, z0 + 0.03f), new Vector3(lx1, 0f, z0 + 0.03f), ly, RailH, frameMat);
            }

            var maxNear = (topNear ? flights : flights - 1) * rise;
            var maxFar = (topNear ? flights - 1 : flights) * rise;
            for (var i = 0; i < 4; i++)
            {
                var nearCol = i < 2;
                var px = nearCol ? x0 + 0.06f : x0 + 2f * land + run - 0.06f;
                var pz = (i & 1) == 0 ? z0 + 0.06f : z0 + 2f * sw - 0.06f;
                var top = nearCol ? maxNear : maxFar;
                if (top < 0.5f)
                    continue;
                b.Box(new Vector3(px, (top - 0.5f) * 0.5f, pz), new Vector3(0.12f, top + 0.5f, 0.12f), frameMat);
            }

            // Kum torbası siper duvarı (merdiven ağzında boşluk)
            const float bagT = 0.45f;
            const float bagH = 1.0f;
            var gap0 = -land * 0.5f - 0.05f;
            var gap1 = land * 0.5f + 0.05f;
            var yBag = h + bagH * 0.5f;
            if (!c.Ruined || c.Chance(0.6f))
                b.Box(new Vector3(0f, yBag, -half + bagT * 0.5f), new Vector3(size, bagH, bagT), M.Sandbag);
            if (!c.Ruined || c.Chance(0.6f))
                b.Box(new Vector3(half - bagT * 0.5f, yBag, 0f), new Vector3(bagT, bagH, size - 2f * bagT), M.Sandbag);
            if (!c.Ruined || c.Chance(0.6f))
                b.Box(new Vector3(-half + bagT * 0.5f, yBag, 0f), new Vector3(bagT, bagH, size - 2f * bagT), M.Sandbag);
            b.Box(new Vector3((-half + gap0) * 0.5f, yBag, half - bagT * 0.5f), new Vector3(gap0 + half, bagH, bagT), M.Sandbag);
            b.Box(new Vector3((half + gap1) * 0.5f, yBag, half - bagT * 0.5f), new Vector3(half - gap1, bagH, bagT), M.Sandbag);

            // Çatı
            if (!c.Ruined)
            {
                var rp = half - bagT * 0.5f;
                for (var i = 0; i < 4; i++)
                {
                    var x = ((i & 1) == 0 ? -1f : 1f) * rp;
                    var z = ((i & 2) == 0 ? -1f : 1f) * rp;
                    b.Box(new Vector3(x, h + bagH + 0.8f, z), new Vector3(0.1f, 1.6f, 0.1f), frameMat);
                }

                b.Box(new Vector3(0f, h + bagH + 1.65f, 0f), new Vector3(size + 0.7f, 0.1f, size + 0.7f), concrete ? M.RoofMetal : M.Wood);
            }

            // Ganimet: platform + alt
            var inner = Rect.MinMaxRect(-half + bagT + 0.2f, -half + bagT + 0.2f, half - bagT - 0.2f, half - bagT - 0.2f);
            PlaceLoot(c, LevelPlatform, inner, h, size >= 4.2f ? 2 : 1);
            if (c.Chance(0.5f))
                PlaceLoot(c, 0, Rect.MinMaxRect(-li + 0.4f, -li + 0.4f, li - 0.4f, li - 0.4f), 0f, 1);
            if (c.Loot.Count == 0)
                c.Loot.Add(new Vector3(0f, h + 0.05f, 0f));
        }

        // =====================================================================================================
        //  Yardımcılar
        // =====================================================================================================

        private static Rect MinMax(float x0, float z0, float x1, float z1)
            => Rect.MinMaxRect(Mathf.Min(x0, x1), Mathf.Min(z0, z1), Mathf.Max(x0, x1), Mathf.Max(z0, z1));

        private static Rect Expand(Rect r, float d) => Rect.MinMaxRect(r.xMin - d, r.yMin - d, r.xMax + d, r.yMax + d);

        private static Rect Centered(float x, float z, float sx, float sz) => new Rect(x - sx * 0.5f, z - sz * 0.5f, sx, sz);

        private static bool Overlaps(Rect a, Rect b)
            => a.xMin < b.xMax - 0.001f && b.xMin < a.xMax - 0.001f && a.yMin < b.yMax - 0.001f && b.yMin < a.yMax - 0.001f;

        private static bool Inside(Rect inner, Rect outer, float eps = 0.01f)
            => inner.xMin >= outer.xMin - eps && inner.xMax <= outer.xMax + eps && inner.yMin >= outer.yMin - eps && inner.yMax <= outer.yMax + eps;

        private static Bounds TransformBounds(Transform t, Bounds local)
        {
            var center = local.center;
            var e = local.extents;
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (var i = 0; i < 8; i++)
            {
                var corner = center + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                var w = t.TransformPoint(corner);
                min = Vector3.Min(min, w);
                max = Vector3.Max(max, w);
            }

            var b = new Bounds();
            b.SetMinMax(min, max);
            return b;
        }
    }
}
