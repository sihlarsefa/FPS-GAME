using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.Transport
{
    /// <summary>
    /// BMC Kirpi (MRAP) düşük poligonlu model verisi. Kök = aks ortası zemin teması, +Z ön. V-gövde, iri tekerlekler,
    /// zırhlı küçük pencereler, kule halkası + makineli tüfek, arka menteşeli kapı. Gövde duvarları ince kutulardan
    /// kurulur (içeriden de görünür). Meshler bir kez üretilip tüm araçlarca paylaşılır; boya rengi malzeme dizisiyle değişir.
    /// Arka bölmede iki yan sıra hâlinde 2×5 içe bakan koltuk; görüş noktaları pencerelerde, dışarı bakar.
    /// </summary>
    internal static class KirpiModel
    {
        public const float FloorY = 1.25f;
        public const float WheelRadius = 0.56f;
        public const float WheelTrack = 1.08f;
        public const float FrontAxleZ = 1.85f;
        public const float RearAxleZ = -1.75f;
        public const float Wheelbase = FrontAxleZ - RearAxleZ;
        public const float DoorOpenAngle = -105f;
        public const int SeatsPerSide = 5;

        public static readonly Vector3 DoorHinge = new Vector3(0.5f, FloorY, RearZ);
        public static readonly Vector3 TurretPivot = new Vector3(0f, RoofY + 0.15f, -0.9f);

        private const float RearZ = -3.3f;
        private const float CabFrontZ = 0.9f;
        private const float WindshieldBottomZ = 1.5f;
        private const float SideX = 1.25f;
        private const float Wall = 0.07f;
        private const float ChamferY = 2.6f;
        private const float RoofY = 2.88f;
        private const float RoofHalfWidth = 1.02f;
        private const float WindowBottom = 2.12f;
        private const float WindowTop = 2.5f;
        private const float WindowHalfWidth = 0.18f;

        // Malzeme yuvaları (gövde, tekerlek, kapı, kule ortak dizi kullanır)
        public const int Paint = 0;
        public const int Metal = 1;
        public const int Glass = 2;
        public const int Dark = 3;
        public const int Tire = 4;
        public const int Suit = 5;
        public const int Flag = 6;
        public const int Black = 7;
        public const int Light = 8;
        public const int RedLight = 9;
        public const int Gun = 10;
        public const int SlotCount = 11;

        /// <summary>Arkadan öne koltuk Z konumları (pencereler de bunlara hizalıdır).</summary>
        private static readonly float[] SeatZ = { -2.65f, -2.05f, -1.45f, -0.85f, -0.25f };

        private static readonly float[] DisembarkX = { 0f, 1.25f, -1.25f, 2.5f, -2.5f };

        public static readonly Vector3[] WheelPositions =
        {
            new Vector3(-WheelTrack, WheelRadius, FrontAxleZ),
            new Vector3(WheelTrack, WheelRadius, FrontAxleZ),
            new Vector3(-WheelTrack, WheelRadius, RearAxleZ),
            new Vector3(WheelTrack, WheelRadius, RearAxleZ)
        };

        private static CachedVehicleMesh _body;
        private static CachedVehicleMesh _wheel;
        private static CachedVehicleMesh _door;
        private static CachedVehicleMesh _turret;

        public static CachedVehicleMesh Body => Ensure(ref _body, BuildBody);
        public static CachedVehicleMesh Wheel => Ensure(ref _wheel, BuildWheel);
        public static CachedVehicleMesh Door => Ensure(ref _door, BuildDoor);
        public static CachedVehicleMesh Turret => Ensure(ref _turret, BuildTurret);

        /// <summary>Malzeme dizisi; <paramref name="tan"/> çöl (VehicleTan) boyası, aksi hâlde zeytin yeşili.</summary>
        public static Material[] Materials(bool tan)
        {
            var materials = new Material[SlotCount];
            materials[Paint] = MaterialLibrary.Get(tan ? MaterialId.VehicleTan : MaterialId.VehicleOlive);
            materials[Metal] = MaterialLibrary.Get(MaterialId.MetalDark);
            materials[Glass] = MaterialLibrary.Get(MaterialId.Windshield);
            materials[Dark] = MaterialLibrary.Get(MaterialId.VehicleDark);
            materials[Tire] = MaterialLibrary.Get(MaterialId.Tire);
            materials[Suit] = MaterialLibrary.Get(MaterialId.Gear);
            materials[Flag] = MaterialLibrary.Get(MaterialId.TurkishFlag);
            materials[Black] = MaterialLibrary.Get(MaterialId.Black);
            materials[Light] = MaterialLibrary.Unlit(new Color(1f, 0.93f, 0.72f));
            materials[RedLight] = MaterialLibrary.Unlit(new Color(0.85f, 0.08f, 0.05f));
            materials[Gun] = MaterialLibrary.Get(MaterialId.GunMetal);
            return materials;
        }

        /// <summary>Koltuk düzeni: 0-4 sağ sıra (arkadan öne, -X'e/içe bakar), 5-9 sol sıra (+X'e bakar). İniş: arka kapının arkası.</summary>
        public static void GetSeatLayout(out Vector3[] seats, out float[] seatYaws, out Vector3[] views, out Vector3[] viewEulers,
            out Vector3[] disembark, out float[] disembarkYaws)
        {
            var count = SeatsPerSide * 2;
            seats = new Vector3[count];
            seatYaws = new float[count];
            views = new Vector3[count];
            viewEulers = new Vector3[count];
            disembark = new Vector3[count];
            disembarkYaws = new float[count];

            for (var i = 0; i < count; i++)
            {
                var side = i < SeatsPerSide ? 1f : -1f;
                var k = i % SeatsPerSide;
                var z = SeatZ[k];
                seats[i] = new Vector3(side * 0.86f, FloorY, z);
                seatYaws[i] = side > 0f ? -90f : 90f;

                // Pencere arkasından dışarı (ve hafif ileri) bakan göz noktası.
                views[i] = new Vector3(side * 1.07f, FloorY + 1.17f, z);
                viewEulers[i] = new Vector3(4f, side * 75f, 0f);

                var local = new Vector3(DisembarkX[k] * (i < SeatsPerSide ? 1f : -1f), 0f, i < SeatsPerSide ? -4.9f : -6.4f);
                if (k == 0 && i >= SeatsPerSide)
                    local.x = 0.6f;
                disembark[i] = local;
                disembarkYaws[i] = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            }
        }

        // ------------------------------------------------------------------ build

        private static CachedVehicleMesh Ensure(ref CachedVehicleMesh cache, System.Func<CachedVehicleMesh> build)
        {
            if (cache == null || !cache.IsValid)
                cache = build();
            return cache;
        }

        private static CachedVehicleMesh Finish(TransportMeshBuilder builder, string name)
        {
            var mesh = builder.Build(name, out var slots);
            return new CachedVehicleMesh { Mesh = mesh, Slots = slots };
        }

        private static CachedVehicleMesh BuildBody()
        {
            var b = new TransportMeshBuilder(SlotCount);

            BuildHull(b);
            for (var side = -1; side <= 1; side += 2)
                BuildSide(b, side);
            BuildRear(b);
            BuildCab(b);
            BuildRoofFittings(b);
            BuildInterior(b);

            return Finish(b, "Kirpi_Govde");
        }

        private static void BuildHull(TransportMeshBuilder b)
        {
            // V-gövde (mayına karşı) — dışbükey beşgen kesit.
            var vMain = new[]
            {
                new Vector2(-0.85f, 1.2f), new Vector2(-0.85f, 0.86f), new Vector2(0f, 0.42f), new Vector2(0.85f, 0.86f), new Vector2(0.85f, 1.2f)
            };
            var vFront = new[]
            {
                new Vector2(-0.75f, 1.2f), new Vector2(-0.75f, 0.96f), new Vector2(0f, 0.78f), new Vector2(0.75f, 0.96f), new Vector2(0.75f, 1.2f)
            };
            b.LoftZ(Paint, vMain, -3.15f, vMain, 2.9f, true, false);
            b.LoftZ(Paint, vMain, 2.9f, vFront, 3.32f, false, true);

            // Tampon, koruma barları, ızgara, farlar
            b.Box(Metal, new Vector3(0f, 1.0f, 3.43f), new Vector3(2.3f, 0.22f, 0.18f));
            for (var side = -1; side <= 1; side += 2)
            {
                b.CylinderBetween(Metal, new Vector3(side * 0.6f, 1.05f, 3.5f), new Vector3(side * 0.6f, 1.78f, 3.44f), 0.035f, 6);
                b.Box(Light, new Vector3(side * 0.85f, 1.48f, 3.39f), new Vector3(0.24f, 0.14f, 0.05f));
                b.Box(Metal, new Vector3(side * 0.85f, 1.48f, 3.37f), new Vector3(0.3f, 0.2f, 0.04f));

                // Ön çamurluk dudağı (tekerlek üstü)
                b.Box(Paint, new Vector3(side * 1.26f, 1.21f, FrontAxleZ), new Vector3(0.16f, 0.06f, 1.3f));
                b.Box(Paint, new Vector3(side * 1.26f, 1.08f, FrontAxleZ + 0.68f), new Vector3(0.16f, 0.3f, 0.06f));

                // Arka tekerlek koruması (gövde altı) ve çamurluk paneli
                b.Box(Dark, new Vector3(side * 1.18f, 1.17f, RearAxleZ), new Vector3(0.18f, 0.06f, 1.3f));
                b.Box(Black, new Vector3(side * 1.2f, 0.75f, RearAxleZ - 0.66f), new Vector3(0.36f, 0.5f, 0.03f));
            }

            b.CylinderBetween(Metal, new Vector3(-0.6f, 1.78f, 3.44f), new Vector3(0.6f, 1.78f, 3.44f), 0.035f, 6);
            b.Box(Black, new Vector3(0f, 1.42f, 3.39f), new Vector3(1.1f, 0.3f, 0.05f));
            for (var i = 0; i < 5; i++)
                b.Box(Dark, new Vector3(-0.44f + i * 0.22f, 1.42f, 3.415f), new Vector3(0.05f, 0.3f, 0.02f));

            // Kaput (ön motor bölmesi)
            b.PrismX(Paint, new[]
            {
                new Vector2(WindshieldBottomZ, 1.2f), new Vector2(3.3f, 1.2f), new Vector2(3.42f, 1.6f), new Vector2(3.2f, 1.92f),
                new Vector2(WindshieldBottomZ, 2.0f)
            }, -1.18f, 1.18f);
            b.Box(Dark, new Vector3(0f, 1.97f, 2.5f), new Vector3(1.4f, 0.04f, 0.9f));

            // Taban (iç zemin) — kabin + arka bölme
            b.Box(Dark, new Vector3(0f, 1.215f, (RearZ + CabFrontZ) * 0.5f + 0.02f), new Vector3(2.36f, 0.07f, CabFrontZ - RearZ - 0.08f));

            // Arka basamak
            b.Box(Metal, new Vector3(0f, 0.95f, RearZ - 0.16f), new Vector3(1.1f, 0.05f, 0.3f));
            b.Box(Metal, new Vector3(0f, 1.08f, RearZ - 0.03f), new Vector3(1.1f, 0.22f, 0.04f));
        }

        private static void BuildSide(TransportMeshBuilder b, int side)
        {
            var outer = side * SideX;
            var x = side * (SideX - Wall * 0.5f);

            // Alt bant (pencere altı) ve üst bant
            var length = CabFrontZ - RearZ;
            var centerZ = (CabFrontZ + RearZ) * 0.5f;
            b.Box(Paint, new Vector3(x, (1.2f + WindowBottom) * 0.5f, centerZ), new Vector3(Wall, WindowBottom - 1.2f, length));
            b.Box(Paint, new Vector3(x, (WindowTop + ChamferY) * 0.5f, centerZ), new Vector3(Wall, ChamferY - WindowTop, length));

            // Pencere aralıkları: 5 yolcu penceresi + sürücü/komutan kapı penceresi.
            var windowStarts = new float[SeatsPerSide + 1];
            var windowEnds = new float[SeatsPerSide + 1];
            for (var i = 0; i < SeatsPerSide; i++)
            {
                windowStarts[i] = SeatZ[i] - WindowHalfWidth;
                windowEnds[i] = SeatZ[i] + WindowHalfWidth;
            }

            windowStarts[SeatsPerSide] = 0.28f;
            windowEnds[SeatsPerSide] = 0.8f;

            var cursor = RearZ;
            var bandY = (WindowBottom + WindowTop) * 0.5f;
            var bandH = WindowTop - WindowBottom;
            for (var i = 0; i <= SeatsPerSide; i++)
            {
                if (windowStarts[i] > cursor + 0.001f)
                    b.Box(Paint, new Vector3(x, bandY, (cursor + windowStarts[i]) * 0.5f), new Vector3(Wall, bandH, windowStarts[i] - cursor));

                AddWindow(b, side, outer, windowStarts[i], windowEnds[i]);
                cursor = windowEnds[i];
            }

            if (CabFrontZ > cursor + 0.001f)
                b.Box(Paint, new Vector3(x, bandY, (cursor + CabFrontZ) * 0.5f), new Vector3(Wall, bandH, CabFrontZ - cursor));

            // Üst pah (MRAP eğik omuz)
            var chamfer = new[]
            {
                new Vector2(side * SideX, ChamferY), new Vector2(side * RoofHalfWidth, RoofY),
                new Vector2(side * (RoofHalfWidth - 0.07f), RoofY), new Vector2(side * (SideX - 0.07f), ChamferY)
            };
            b.LoftZ(Paint, chamfer, RearZ, chamfer, CabFrontZ, true, true);

            // Kabin yan paneli (ön cam yanı, yamuk)
            var cabProfile = new[]
            {
                new Vector2(CabFrontZ, 1.2f), new Vector2(WindshieldBottomZ, 1.2f), new Vector2(WindshieldBottomZ, 2.0f), new Vector2(CabFrontZ, ChamferY)
            };
            b.PrismX(Paint, cabProfile, side > 0 ? SideX - Wall : -SideX, side > 0 ? SideX : -SideX + Wall);

            // Kapı çizgileri, kapı kolu, menteşeler (sürücü kapısı)
            b.Box(Dark, new Vector3(side * (SideX + 0.004f), 1.75f, 0.2f), new Vector3(0.01f, 1.0f, 0.03f));
            b.Box(Dark, new Vector3(side * (SideX + 0.004f), 1.75f, 0.88f), new Vector3(0.01f, 1.0f, 0.03f));
            b.Box(Metal, new Vector3(side * (SideX + 0.03f), 1.72f, 0.3f), new Vector3(0.04f, 0.05f, 0.18f));

            // Türk bayrağı (kapı üstünde; gönder tarafı öne)
            var fx = side * (SideX + 0.006f);
            b.QuadUv(Flag,
                new Vector3(fx, 1.42f, 0.78f), new Vector3(fx, 1.42f, 0.33f), new Vector3(fx, 1.72f, 0.33f), new Vector3(fx, 1.72f, 0.78f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector3(side, 0f, 0f));

            // Yan saklama kutusu ve bidon (arka bölme altı)
            b.Box(Paint, new Vector3(side * (SideX + 0.08f), 1.42f, -2.95f), new Vector3(0.16f, 0.36f, 0.5f));
            b.Box(Dark, new Vector3(side * (SideX + 0.09f), 1.42f, -0.85f), new Vector3(0.18f, 0.4f, 0.32f));

            // Ayna
            b.CylinderBetween(Metal, new Vector3(side * SideX, 2.08f, 1.1f), new Vector3(side * 1.47f, 2.16f, 1.22f), 0.018f, 4);
            b.Box(Dark, new Vector3(side * 1.5f, 2.16f, 1.22f), new Vector3(0.05f, 0.26f, 0.17f));
        }

        private static void AddWindow(TransportMeshBuilder b, int side, float outer, float z0, float z1)
        {
            var normal = new Vector3(side, 0f, 0f);
            var xo = outer + side * 0.004f;
            var xi = outer - side * (Wall + 0.004f);
            b.Quad(Glass, new Vector3(xo, WindowBottom, z0), new Vector3(xo, WindowBottom, z1), new Vector3(xo, WindowTop, z1), new Vector3(xo, WindowTop, z0), normal);
            b.Quad(Glass, new Vector3(xi, WindowBottom, z0), new Vector3(xi, WindowBottom, z1), new Vector3(xi, WindowTop, z1), new Vector3(xi, WindowTop, z0), -normal);

            // Kalın zırhlı cam çerçevesi (dışa taşan koyu şerit)
            var fx = outer + side * 0.015f;
            var width = z1 - z0;
            var cz = (z0 + z1) * 0.5f;
            b.Box(Dark, new Vector3(fx, WindowTop + 0.02f, cz), new Vector3(0.03f, 0.04f, width + 0.08f));
            b.Box(Dark, new Vector3(fx, WindowBottom - 0.02f, cz), new Vector3(0.03f, 0.04f, width + 0.08f));
            b.Box(Dark, new Vector3(fx, (WindowBottom + WindowTop) * 0.5f, z0 - 0.02f), new Vector3(0.03f, WindowTop - WindowBottom, 0.04f));
            b.Box(Dark, new Vector3(fx, (WindowBottom + WindowTop) * 0.5f, z1 + 0.02f), new Vector3(0.03f, WindowTop - WindowBottom, 0.04f));
        }

        private static void BuildRear(TransportMeshBuilder b)
        {
            var z = RearZ + Wall * 0.5f;
            const float doorHalf = 0.5f;
            const float pieceWidth = SideX - doorHalf;
            b.Box(Paint, new Vector3(-(doorHalf + pieceWidth * 0.5f), (1.2f + ChamferY) * 0.5f, z), new Vector3(pieceWidth, ChamferY - 1.2f, Wall));
            b.Box(Paint, new Vector3(doorHalf + pieceWidth * 0.5f, (1.2f + ChamferY) * 0.5f, z), new Vector3(pieceWidth, ChamferY - 1.2f, Wall));
            b.Box(Paint, new Vector3(0f, 1.225f, z), new Vector3(doorHalf * 2f, 0.05f, Wall));

            var top = new[]
            {
                new Vector2(-SideX, ChamferY), new Vector2(SideX, ChamferY), new Vector2(RoofHalfWidth, RoofY), new Vector2(-RoofHalfWidth, RoofY)
            };
            b.LoftZ(Paint, top, RearZ, top, RearZ + Wall, true, true);

            // Kapı çerçevesi, stop lambaları, bayrak, yedek lastik askısı
            b.Box(Dark, new Vector3(-doorHalf - 0.02f, (FloorY + ChamferY) * 0.5f, RearZ - 0.01f), new Vector3(0.04f, ChamferY - FloorY, 0.03f));
            b.Box(Dark, new Vector3(doorHalf + 0.02f, (FloorY + ChamferY) * 0.5f, RearZ - 0.01f), new Vector3(0.04f, ChamferY - FloorY, 0.03f));
            for (var side = -1; side <= 1; side += 2)
            {
                b.Box(RedLight, new Vector3(side * 1.08f, 1.4f, RearZ - 0.015f), new Vector3(0.16f, 0.1f, 0.03f));
                b.Box(Light, new Vector3(side * 1.08f, 1.28f, RearZ - 0.015f), new Vector3(0.16f, 0.06f, 0.03f));
            }

            var fz = RearZ - 0.006f;
            b.QuadUv(Flag,
                new Vector3(-0.62f, 2.12f, fz), new Vector3(-1.04f, 2.12f, fz), new Vector3(-1.04f, 2.4f, fz), new Vector3(-0.62f, 2.4f, fz),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector3(0f, 0f, -1f));

            b.Cylinder(Tire, new Vector3(0.88f, 1.95f, RearZ - 0.2f), Quaternion.Euler(90f, 0f, 0f), 0.36f, 0.36f, 0.26f, 12);
            b.Cylinder(Dark, new Vector3(0.88f, 1.95f, RearZ - 0.2f), Quaternion.Euler(90f, 0f, 0f), 0.2f, 0.2f, 0.28f, 8);
        }

        private static void BuildCab(TransportMeshBuilder b)
        {
            // Ön cam (iki yüzlü) + dikmeler
            var bl = new Vector3(-1.16f, 2.0f, WindshieldBottomZ);
            var br = new Vector3(1.16f, 2.0f, WindshieldBottomZ);
            var tr = new Vector3(RoofHalfWidth - 0.02f, RoofY - 0.03f, CabFrontZ + 0.02f);
            var tl = new Vector3(-RoofHalfWidth + 0.02f, RoofY - 0.03f, CabFrontZ + 0.02f);
            var outward = new Vector3(0f, 0.6f, 0.8f);
            b.Quad(Glass, bl, br, tr, tl, outward);
            b.Quad(Glass, bl, br, tr, tl, -outward);

            b.CylinderBetween(Dark, bl, tl, 0.06f, 6);
            b.CylinderBetween(Dark, br, tr, 0.06f, 6);
            b.CylinderBetween(Dark, new Vector3(0f, 2.0f, WindshieldBottomZ), new Vector3(0f, RoofY - 0.03f, CabFrontZ + 0.02f), 0.035f, 6);
            b.CylinderBetween(Dark, bl, br, 0.04f, 6);
            b.CylinderBetween(Dark, tl, tr, 0.04f, 6);

            // Gösterge paneli, direksiyon, sürücü ve komutan
            b.Box(Dark, new Vector3(0f, 1.82f, 1.32f), new Vector3(2.2f, 0.34f, 0.3f));
            b.CylinderBetween(Black, new Vector3(-0.55f, 1.86f, 1.1f), new Vector3(-0.55f, 2.02f, 0.9f), 0.025f, 4);
            b.Cylinder(Black, new Vector3(-0.55f, 2.03f, 0.88f), Quaternion.Euler(-55f, 0f, 0f), 0.19f, 0.19f, 0.03f, 10);

            for (var side = -1; side <= 1; side += 2)
            {
                var px = side * 0.55f;
                b.Box(Dark, new Vector3(px, 1.5f, 0.38f), new Vector3(0.48f, 0.1f, 0.48f));
                b.Box(Dark, new Vector3(px, 2.0f, 0.1f), new Vector3(0.48f, 0.9f, 0.1f));
                b.Box(Suit, new Vector3(px, 1.98f, 0.32f), new Vector3(0.42f, 0.6f, 0.3f));
                b.Box(Suit, new Vector3(px, 1.62f, 0.62f), new Vector3(0.38f, 0.16f, 0.5f));
                b.Sphere(Dark, new Vector3(px, 2.45f, 0.36f), new Vector3(0.14f, 0.15f, 0.15f), 8, 5);
                b.Box(Black, new Vector3(px, 2.42f, 0.5f), new Vector3(0.18f, 0.07f, 0.04f));
            }

            // Egzoz bacası (sağ ön köşe)
            b.CylinderBetween(Black, new Vector3(1.3f, 1.6f, 1.25f), new Vector3(1.3f, 2.55f, 1.25f), 0.06f, 8);
            b.Box(Metal, new Vector3(1.28f, 1.6f, 1.25f), new Vector3(0.1f, 0.06f, 0.16f));
        }

        private static void BuildRoofFittings(TransportMeshBuilder b)
        {
            var length = CabFrontZ - RearZ;
            b.Box(Paint, new Vector3(0f, RoofY - 0.035f, (CabFrontZ + RearZ) * 0.5f), new Vector3(RoofHalfWidth * 2f, 0.07f, length));

            // Kule halkası ve kapaklar
            b.Cylinder(Metal, new Vector3(0f, RoofY + 0.07f, TurretPivot.z), Quaternion.identity, 0.64f, 0.6f, 0.14f, 16);
            b.Box(Paint, new Vector3(0f, RoofY + 0.02f, -2.4f), new Vector3(0.7f, 0.05f, 0.7f));
            b.Box(Metal, new Vector3(0.3f, RoofY + 0.06f, -2.4f), new Vector3(0.05f, 0.04f, 0.5f));
            b.Box(Paint, new Vector3(-0.5f, RoofY + 0.02f, 0.35f), new Vector3(0.6f, 0.05f, 0.6f));

            // Anten ve tavan rayı
            for (var side = -1; side <= 1; side += 2)
            {
                b.CylinderBetween(Dark, new Vector3(side * 0.92f, RoofY, -3.1f), new Vector3(side * 0.92f, RoofY + 2.0f, -3.15f), 0.012f, 4);
                b.Box(Dark, new Vector3(side * 0.92f, RoofY + 0.06f, -3.1f), new Vector3(0.1f, 0.12f, 0.1f));
                b.CylinderBetween(Metal, new Vector3(side * 0.88f, RoofY + 0.06f, -2.9f), new Vector3(side * 0.88f, RoofY + 0.06f, -1.6f), 0.02f, 4);
            }
        }

        private static void BuildInterior(TransportMeshBuilder b)
        {
            const float benchStart = -2.98f;
            const float benchEnd = 0.08f;
            var benchLength = benchEnd - benchStart;
            var benchCenter = (benchStart + benchEnd) * 0.5f;
            for (var side = -1; side <= 1; side += 2)
            {
                // Oturak (zeminden 0.45 m), bel desteği (pencere altında kalır), ayaklar
                b.Box(Dark, new Vector3(side * 0.86f, FloorY + 0.42f, benchCenter), new Vector3(0.44f, 0.06f, benchLength));
                b.Box(Dark, new Vector3(side * 1.13f, 1.9f, benchCenter), new Vector3(0.06f, 0.36f, benchLength));
                for (var leg = 0; leg < 3; leg++)
                {
                    var z = Mathf.Lerp(benchStart + 0.1f, benchEnd - 0.1f, leg * 0.5f);
                    b.Box(Metal, new Vector3(side * 0.7f, FloorY + 0.2f, z), new Vector3(0.04f, 0.4f, 0.04f));
                }

                // Tavan tutunma barı
                b.CylinderBetween(Metal, new Vector3(side * 0.62f, RoofY - 0.16f, benchStart), new Vector3(side * 0.62f, RoofY - 0.16f, benchEnd), 0.02f, 4);
            }

            // İç tavan kaplaması ve tavan lambası
            b.Box(Dark, new Vector3(0f, RoofY - 0.085f, benchCenter), new Vector3(RoofHalfWidth * 2f - 0.05f, 0.03f, benchLength));
            b.Box(Light, new Vector3(0f, RoofY - 0.11f, -1.45f), new Vector3(0.3f, 0.02f, 0.12f));

            // Telsiz ve teçhizat rafı (kabin arkası)
            b.Box(Dark, new Vector3(0f, 1.55f, 0.15f), new Vector3(0.4f, 0.6f, 0.3f));
            b.Box(Black, new Vector3(0f, 1.75f, 0.31f), new Vector3(0.28f, 0.16f, 0.02f));
        }

        private static CachedVehicleMesh BuildWheel()
        {
            var b = new TransportMeshBuilder(SlotCount);
            var axis = Quaternion.Euler(0f, 0f, 90f);
            b.Cylinder(Tire, Vector3.zero, axis, WheelRadius, WheelRadius, 0.42f, 14);
            b.Cylinder(Tire, Vector3.zero, axis, WheelRadius - 0.05f, WheelRadius - 0.05f, 0.46f, 14, false);
            b.Cylinder(Dark, Vector3.zero, axis, 0.3f, 0.3f, 0.44f, 10);
            b.Cylinder(Metal, Vector3.zero, axis, 0.1f, 0.1f, 0.52f, 8);

            // Bijon somunları (dönüş görünsün)
            for (var face = -1; face <= 1; face += 2)
            {
                for (var k = 0; k < 6; k++)
                {
                    var angle = k / 6f * Mathf.PI * 2f;
                    var p = new Vector3(face * 0.225f, Mathf.Cos(angle) * 0.19f, Mathf.Sin(angle) * 0.19f);
                    b.Box(Metal, p, new Vector3(0.03f, 0.045f, 0.045f));
                }
            }

            // Diş blokları
            for (var k = 0; k < 10; k++)
            {
                var rotation = Quaternion.Euler(k * 36f, 0f, 0f);
                b.Box(Tire, rotation * new Vector3(0f, WheelRadius + 0.005f, 0f), new Vector3(0.38f, 0.03f, 0.1f), rotation);
            }

            return Finish(b, "Kirpi_Teker");
        }

        /// <summary>Arka kapı (pivot = menteşe, sağ kenar). Yerelde -X'e uzanır; kapalıyken arka duvarla aynı düzlemde.</summary>
        private static CachedVehicleMesh BuildDoor()
        {
            var b = new TransportMeshBuilder(SlotCount);
            const float width = 0.98f;
            const float height = ChamferY - FloorY - 0.05f;
            b.Box(Paint, new Vector3(-0.5f, 0.03f + height * 0.5f, Wall * 0.5f), new Vector3(width, height, Wall));

            // Küçük zırhlı pencere (iki yüz)
            var wx0 = -0.66f;
            var wx1 = -0.34f;
            const float wy0 = 0.86f;
            const float wy1 = 1.14f;
            b.Quad(Glass, new Vector3(wx0, wy0, -0.004f), new Vector3(wx1, wy0, -0.004f), new Vector3(wx1, wy1, -0.004f), new Vector3(wx0, wy1, -0.004f), Vector3.back);
            b.Quad(Glass, new Vector3(wx0, wy0, Wall + 0.004f), new Vector3(wx1, wy0, Wall + 0.004f), new Vector3(wx1, wy1, Wall + 0.004f), new Vector3(wx0, wy1, Wall + 0.004f), Vector3.forward);
            b.Box(Dark, new Vector3(-0.5f, wy1 + 0.02f, -0.01f), new Vector3(0.38f, 0.04f, 0.03f));
            b.Box(Dark, new Vector3(-0.5f, wy0 - 0.02f, -0.01f), new Vector3(0.38f, 0.04f, 0.03f));

            // Kol, menteşeler, takviye
            b.Box(Metal, new Vector3(-0.86f, 0.62f, -0.04f), new Vector3(0.04f, 0.22f, 0.05f));
            b.Box(Metal, new Vector3(-0.86f, 0.62f, Wall + 0.04f), new Vector3(0.04f, 0.22f, 0.05f));
            b.CylinderBetween(Metal, new Vector3(0f, 0.2f, 0f), new Vector3(0f, 0.36f, 0f), 0.04f, 6);
            b.CylinderBetween(Metal, new Vector3(0f, 1.02f, 0f), new Vector3(0f, 1.18f, 0f), 0.04f, 6);
            b.Box(Dark, new Vector3(-0.5f, 0.35f, -0.01f), new Vector3(0.9f, 0.05f, 0.03f));

            return Finish(b, "Kirpi_ArkaKapi");
        }

        /// <summary>Kule: pivot halkanın üstü; makineli tüfek + kalkan.</summary>
        private static CachedVehicleMesh BuildTurret()
        {
            var b = new TransportMeshBuilder(SlotCount);
            b.CylinderBetween(Metal, new Vector3(0f, 0f, 0.05f), new Vector3(0f, 0.42f, 0.05f), 0.045f, 6);
            b.Box(Metal, new Vector3(0f, 0.02f, 0f), new Vector3(0.2f, 0.04f, 0.2f));

            // Silah
            b.Box(Gun, new Vector3(0f, 0.5f, 0.12f), new Vector3(0.12f, 0.15f, 0.62f));
            b.Box(Gun, new Vector3(0f, 0.47f, -0.28f), new Vector3(0.1f, 0.14f, 0.22f));
            b.CylinderBetween(Gun, new Vector3(0f, 0.53f, 0.42f), new Vector3(0f, 0.53f, 1.22f), 0.022f, 6);
            b.CylinderBetween(Gun, new Vector3(0f, 0.53f, 1.12f), new Vector3(0f, 0.53f, 1.26f), 0.035f, 6);
            b.CylinderBetween(Gun, new Vector3(0f, 0.53f, 0.42f), new Vector3(0f, 0.53f, 0.7f), 0.04f, 6);
            b.Box(Dark, new Vector3(0.13f, 0.44f, 0.1f), new Vector3(0.12f, 0.16f, 0.24f));
            b.CylinderBetween(Black, new Vector3(-0.07f, 0.44f, -0.36f), new Vector3(-0.07f, 0.36f, -0.36f), 0.02f, 4);
            b.CylinderBetween(Black, new Vector3(0.07f, 0.44f, -0.36f), new Vector3(0.07f, 0.36f, -0.36f), 0.02f, 4);

            // Kalkan: iki yan plaka + üst plaka (namlu boşluğu ortada)
            b.Box(Paint, new Vector3(-0.27f, 0.5f, 0.42f), new Vector3(0.44f, 0.46f, 0.04f));
            b.Box(Paint, new Vector3(0.27f, 0.5f, 0.42f), new Vector3(0.44f, 0.46f, 0.04f));
            b.Box(Paint, new Vector3(0f, 0.68f, 0.42f), new Vector3(0.12f, 0.1f, 0.04f));
            b.Box(Paint, new Vector3(-0.5f, 0.5f, 0.32f), new Vector3(0.04f, 0.46f, 0.22f), Quaternion.Euler(0f, -25f, 0f));
            b.Box(Paint, new Vector3(0.5f, 0.5f, 0.32f), new Vector3(0.04f, 0.46f, 0.22f), Quaternion.Euler(0f, 25f, 0f));

            return Finish(b, "Kirpi_Kule");
        }
    }
}
