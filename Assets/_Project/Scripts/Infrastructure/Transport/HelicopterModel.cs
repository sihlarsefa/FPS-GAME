using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.Transport
{
    /// <summary>
    /// T-70 (Black Hawk benzeri) düşük poligonlu model verisi. Kök = kızak altı merkezi (zemin teması), +Z burun.
    /// Gövde tek mesh (malzeme başına alt-mesh), ana/kuyruk rotoru ve rotor diski ayrı meshlerdir. Meshler bir kez üretilip
    /// tüm helikopterlerce paylaşılır. Kabin: iki yanda açık sürgülü kapılar, sırt sırta 2×5 dışa bakan koltuk.
    /// </summary>
    internal static class HelicopterModel
    {
        public const float FloorY = 0.78f;
        public const float MainRotorRadius = 7f;
        public const int SeatsPerSide = 5;

        public static readonly Vector3 MainRotorHub = new Vector3(0f, 3.62f, 0.2f);
        public static readonly Vector3 TailRotorHub = new Vector3(0.27f, 3.35f, -9.2f);

        // Gövde malzeme yuvaları
        private const int Paint = 0;
        private const int Metal = 1;
        private const int Glass = 2;
        private const int Dark = 3;
        private const int Suit = 4;
        private const int Flag = 5;
        private const int Black = 6;
        private const int NavRed = 7;
        private const int NavGreen = 8;
        private const int BodySlotCount = 9;

        // Rotor yuvaları
        private const int Blade = 0;
        private const int Hub = 1;
        private const int Tip = 2;

        private static readonly float[] SeatZ = { 1.24f, 0.62f, 0f, -0.62f, -1.24f };

        private static CachedVehicleMesh _body;
        private static CachedVehicleMesh _mainRotor;
        private static CachedVehicleMesh _tailRotor;
        private static CachedVehicleMesh _disc;

        public static CachedVehicleMesh Body => Ensure(ref _body, BuildBody);
        public static CachedVehicleMesh MainRotor => Ensure(ref _mainRotor, BuildMainRotor);
        public static CachedVehicleMesh TailRotor => Ensure(ref _tailRotor, BuildTailRotor);
        public static CachedVehicleMesh RotorDisc => Ensure(ref _disc, BuildDisc);

        public static Material[] BodyMaterials()
        {
            var materials = new Material[BodySlotCount];
            materials[Paint] = MaterialLibrary.Get(MaterialId.HeliOlive);
            materials[Metal] = MaterialLibrary.Get(MaterialId.MetalDark);
            materials[Glass] = MaterialLibrary.Get(MaterialId.Windshield);
            materials[Dark] = MaterialLibrary.Get(MaterialId.VehicleDark);
            materials[Suit] = MaterialLibrary.Get(MaterialId.Gear);
            materials[Flag] = MaterialLibrary.Get(MaterialId.TurkishFlag);
            materials[Black] = MaterialLibrary.Get(MaterialId.Black);
            materials[NavRed] = MaterialLibrary.Unlit(new Color(1f, 0.12f, 0.08f));
            materials[NavGreen] = MaterialLibrary.Unlit(new Color(0.15f, 1f, 0.3f));
            return materials;
        }

        public static Material[] RotorMaterials()
        {
            return new[]
            {
                MaterialLibrary.Get(MaterialId.RotorBlade),
                MaterialLibrary.Get(MaterialId.MetalDark),
                MaterialLibrary.Get(MaterialId.Yellow)
            };
        }

        public static Material[] DiscMaterials()
        {
            return new[] { MaterialLibrary.Transparent(new Color(0.05f, 0.05f, 0.05f, 0.13f), true) };
        }

        /// <summary>Koltuk düzeni: 0-4 sağ sıra (önden arkaya, +X'e bakar), 5-9 sol sıra (-X'e bakar).</summary>
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
                var z = SeatZ[i % SeatsPerSide];
                seats[i] = new Vector3(side * 0.52f, FloorY, z);
                seatYaws[i] = side * 90f;
                views[i] = new Vector3(side * 0.66f, FloorY + 1.16f, z);
                viewEulers[i] = new Vector3(10f, side * 90f, 0f);
                disembark[i] = new Vector3(side * 3.3f, 0f, z * 1.45f);
                disembarkYaws[i] = side * 90f;
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

        private static readonly Vector2[] CabinOutline =
        {
            new Vector2(-1.15f, 0.61f), new Vector2(1.15f, 0.61f), new Vector2(1.15f, 2.3f),
            new Vector2(0.86f, 2.68f), new Vector2(-0.86f, 2.68f), new Vector2(-1.15f, 2.3f)
        };

        private static readonly Vector2[] BulkheadOutline =
        {
            new Vector2(-1.13f, 0.62f), new Vector2(1.13f, 0.62f), new Vector2(1.13f, 2.29f),
            new Vector2(0.85f, 2.66f), new Vector2(-0.85f, 2.66f), new Vector2(-1.13f, 2.29f)
        };

        private static readonly Vector2[] ConeEnd =
        {
            new Vector2(-0.38f, 1.8f), new Vector2(0.38f, 1.8f), new Vector2(0.42f, 2.3f),
            new Vector2(0.3f, 2.52f), new Vector2(-0.3f, 2.52f), new Vector2(-0.42f, 2.3f)
        };

        private static readonly Vector2[] BoomEnd =
        {
            new Vector2(-0.18f, 2.08f), new Vector2(0.18f, 2.08f), new Vector2(0.2f, 2.3f),
            new Vector2(0.14f, 2.42f), new Vector2(-0.14f, 2.42f), new Vector2(-0.2f, 2.3f)
        };

        private static CachedVehicleMesh BuildBody()
        {
            var b = new TransportMeshBuilder(BodySlotCount);

            // --- Kızaklar ve dikmeler
            for (var side = -1; side <= 1; side += 2)
            {
                var x = side * 1.25f;
                b.CylinderBetween(Metal, new Vector3(x, 0.07f, -1.7f), new Vector3(x, 0.07f, 2.3f), 0.07f, 6);
                b.CylinderBetween(Metal, new Vector3(x, 0.07f, 2.3f), new Vector3(x, 0.3f, 2.68f), 0.07f, 6);
                b.CylinderBetween(Metal, new Vector3(x, 0.07f, -1f), new Vector3(side * 0.95f, 0.66f, -1f), 0.055f, 6);
                b.CylinderBetween(Metal, new Vector3(x, 0.07f, 1.6f), new Vector3(side * 0.95f, 0.66f, 1.6f), 0.055f, 6);
            }

            // --- Kabin tabanı (z -2.05..2.45)
            b.Box(Paint, new Vector3(0f, 0.69f, 0.2f), new Vector3(2.3f, 0.16f, 4.5f));
            b.Box(Dark, new Vector3(0f, 0.775f, 0.2f), new Vector3(2.2f, 0.012f, 4.3f));

            // --- Kabin yan duvarları: kapı boşlukları açık (z -1.6..1.7, y taban..2.2)
            for (var side = -1; side <= 1; side += 2)
            {
                var wx = side * 1.12f;
                b.Box(Paint, new Vector3(wx, 1.535f, -1.825f), new Vector3(0.06f, 1.53f, 0.45f));
                b.Box(Paint, new Vector3(wx, 1.535f, 2.075f), new Vector3(0.06f, 1.53f, 0.75f));
                b.Box(Paint, new Vector3(wx, 2.25f, 0.05f), new Vector3(0.06f, 0.1f, 3.3f));

                // Ön dikmedeki küçük pencere
                var gx = side * 1.152f;
                b.Quad(Glass, new Vector3(gx, 1.55f, 1.85f), new Vector3(gx, 1.55f, 2.3f), new Vector3(gx, 2.05f, 2.3f), new Vector3(gx, 2.05f, 1.85f),
                    new Vector3(side, 0f, 0f));

                // Üst pah
                var chamfer = new[]
                {
                    new Vector2(side * 1.15f, 2.3f), new Vector2(side * 0.86f, 2.68f),
                    new Vector2(side * 0.8f, 2.68f), new Vector2(side * 1.09f, 2.3f)
                };
                b.LoftZ(Paint, chamfer, -2.05f, chamfer, 2.45f, true, true);

                // Açık sürgülü kapı (arkaya kaydırılmış) + pencere + raylar
                b.Box(Paint, new Vector3(side * 1.21f, 1.5f, -2.2f), new Vector3(0.05f, 1.42f, 1.6f));
                var dx = side * 1.236f;
                b.Quad(Glass, new Vector3(dx, 1.62f, -2.65f), new Vector3(dx, 1.62f, -2f), new Vector3(dx, 2.05f, -2f), new Vector3(dx, 2.05f, -2.65f),
                    new Vector3(side, 0f, 0f));
                b.CylinderBetween(Metal, new Vector3(side * 1.17f, 2.24f, -3.1f), new Vector3(side * 1.17f, 2.24f, 1.7f), 0.025f, 4);
                b.CylinderBetween(Metal, new Vector3(side * 1.17f, 0.8f, -3.1f), new Vector3(side * 1.17f, 0.8f, 1.7f), 0.025f, 4);

                // Basamak
                b.Box(Metal, new Vector3(side * 1.2f, 0.45f, 0.9f), new Vector3(0.12f, 0.04f, 0.5f));
            }

            // --- Tavan
            b.Box(Paint, new Vector3(0f, 2.65f, 0.2f), new Vector3(1.74f, 0.06f, 4.5f));
            b.Box(NavRed, new Vector3(0f, 2.61f, 0.2f), new Vector3(0.12f, 0.025f, 0.5f));

            // --- Bölmeler (arka ve ön)
            b.LoftZ(Paint, BulkheadOutline, -2.05f, BulkheadOutline, -1.99f, true, true);
            b.LoftZ(Paint, BulkheadOutline, 2.39f, BulkheadOutline, 2.45f, true, true);

            // --- Arka gövde konisi + kuyruk kirişi
            b.LoftZ(Paint, ConeEnd, -4.1f, CabinOutline, -2.05f, false, false);
            b.LoftZ(Paint, BoomEnd, -8.8f, ConeEnd, -4.1f, true, false);

            // --- Kuyruk dikmesi, yatay stabilizatör, seyir ışıkları, bayrak
            b.PrismX(Paint, new[]
            {
                new Vector2(-8.2f, 2.25f), new Vector2(-9.2f, 2.25f), new Vector2(-9.55f, 3.95f), new Vector2(-8.95f, 3.95f)
            }, -0.07f, 0.07f);
            b.Box(Paint, new Vector3(0f, 2.3f, -9.05f), new Vector3(4f, 0.06f, 0.75f));
            b.Box(NavRed, new Vector3(-2.02f, 2.3f, -9.05f), new Vector3(0.05f, 0.07f, 0.14f));
            b.Box(NavGreen, new Vector3(2.02f, 2.3f, -9.05f), new Vector3(0.05f, 0.07f, 0.14f));
            b.Box(Metal, new Vector3(0.12f, 3.35f, -9.2f), new Vector3(0.16f, 0.22f, 0.22f));
            for (var side = -1; side <= 1; side += 2)
            {
                var fx = side * 0.075f;
                b.QuadUv(Flag,
                    new Vector3(fx, 2.76f, -8.65f), new Vector3(fx, 2.76f, -9.2f), new Vector3(fx, 3.13f, -9.2f), new Vector3(fx, 3.13f, -8.65f),
                    new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                    new Vector3(side, 0f, 0f));
            }

            b.CylinderBetween(Dark, new Vector3(0f, 2.42f, -6f), new Vector3(0f, 2.85f, -6.3f), 0.012f, 4);

            // --- Burun (alt, opak) + kokpit camı + çerçeve
            b.LoftZ(Paint,
                new[] { new Vector2(-1.15f, 0.61f), new Vector2(1.15f, 0.61f), new Vector2(1.15f, 1.55f), new Vector2(-1.15f, 1.55f) }, 2.45f,
                new[] { new Vector2(-0.45f, 0.95f), new Vector2(0.45f, 0.95f), new Vector2(0.5f, 1.38f), new Vector2(-0.5f, 1.38f) }, 4.95f,
                false, true);
            b.LoftZ(Glass,
                new[] { new Vector2(-1.13f, 1.55f), new Vector2(1.13f, 1.55f), new Vector2(0.85f, 2.64f), new Vector2(-0.85f, 2.64f) }, 2.45f,
                new[] { new Vector2(-0.62f, 1.44f), new Vector2(0.62f, 1.44f), new Vector2(0.45f, 1.76f), new Vector2(-0.45f, 1.76f) }, 4.35f,
                false, true);
            AddCanopyFrame(b);

            // Pilotlar ve gösterge paneli (camdan görünür)
            for (var side = -1; side <= 1; side += 2)
            {
                var px = side * 0.48f;
                b.Box(Dark, new Vector3(px, 1.75f, 2.75f), new Vector3(0.5f, 0.85f, 0.1f));
                b.Box(Suit, new Vector3(px, 1.6f, 3.05f), new Vector3(0.44f, 0.62f, 0.3f));
                b.Sphere(Dark, new Vector3(px, 2.07f, 3.07f), new Vector3(0.15f, 0.16f, 0.16f), 8, 5);
                b.Box(Black, new Vector3(px, 2.06f, 3.2f), new Vector3(0.2f, 0.08f, 0.06f));
            }

            b.Box(Dark, new Vector3(0f, 1.6f, 3.85f), new Vector3(1.5f, 0.22f, 0.35f));
            b.Sphere(Dark, new Vector3(0f, 0.82f, 4.55f), new Vector3(0.17f, 0.17f, 0.17f), 8, 5);
            for (var side = -1; side <= 1; side += 2)
                b.CylinderBetween(Metal, new Vector3(side * 0.3f, 1.2f, 4.85f), new Vector3(side * 0.3f, 1.2f, 5.4f), 0.015f, 4);

            // --- Motor kaportası, hava girişleri, egzozlar, arka kaporta, direk
            var hump = new[] { new Vector2(-0.75f, 2.66f), new Vector2(0.75f, 2.66f), new Vector2(0.62f, 3.2f), new Vector2(-0.62f, 3.2f) };
            b.LoftZ(Paint, hump, -1.7f, hump, 1.9f, false, false);
            b.LoftZ(Paint, hump, 1.9f,
                new[] { new Vector2(-0.6f, 2.62f), new Vector2(0.6f, 2.62f), new Vector2(0.45f, 2.9f), new Vector2(-0.45f, 2.9f) }, 2.6f,
                false, true);
            b.LoftZ(Paint,
                new[] { new Vector2(-0.28f, 2.45f), new Vector2(0.28f, 2.45f), new Vector2(0.2f, 2.62f), new Vector2(-0.2f, 2.62f) }, -3.7f,
                hump, -1.7f, true, false);
            for (var side = -1; side <= 1; side += 2)
            {
                b.Box(Black, new Vector3(side * 0.7f, 2.93f, 1.2f), new Vector3(0.06f, 0.28f, 0.5f));
                b.CylinderBetween(Metal, new Vector3(side * 0.62f, 2.95f, -0.9f), new Vector3(side * 0.98f, 3.02f, -1.65f), 0.14f, 8);
            }

            b.Box(NavRed, new Vector3(0f, 3.22f, -0.6f), new Vector3(0.1f, 0.05f, 0.1f));
            b.CylinderBetween(Metal, new Vector3(0f, 3.15f, 0.2f), new Vector3(0f, 3.56f, 0.2f), 0.16f, 8);

            // --- Kabin koltukları: sırt sırta iki sıra, dışa bakar
            for (var side = -1; side <= 1; side += 2)
            {
                b.Box(Dark, new Vector3(side * 0.5f, 1.19f, 0f), new Vector3(0.46f, 0.07f, 3.3f));
                b.Box(Dark, new Vector3(side * 0.22f, 1.62f, 0f), new Vector3(0.08f, 0.72f, 3.3f));
                for (var leg = -1; leg <= 1; leg++)
                    b.Box(Metal, new Vector3(side * 0.62f, 0.98f, leg * 1.5f), new Vector3(0.04f, 0.4f, 0.04f));
            }

            b.Box(Metal, new Vector3(0f, 1.6f, 0f), new Vector3(0.36f, 0.8f, 3.34f));

            return Finish(b, "T70_Govde");
        }

        private static void AddCanopyFrame(TransportMeshBuilder b)
        {
            const float r = 0.03f;
            for (var side = -1; side <= 1; side += 2)
            {
                var bottomBack = new Vector3(side * 1.13f, 1.55f, 2.45f);
                var bottomFront = new Vector3(side * 0.62f, 1.44f, 4.35f);
                var topBack = new Vector3(side * 0.85f, 2.64f, 2.45f);
                var topFront = new Vector3(side * 0.45f, 1.76f, 4.35f);
                b.CylinderBetween(Metal, bottomBack, bottomFront, r, 4);
                b.CylinderBetween(Metal, topBack, topFront, r, 4);
                b.CylinderBetween(Metal, bottomFront, topFront, r, 4);

                var midBottom = Vector3.Lerp(bottomBack, bottomFront, 0.5f);
                var midTop = Vector3.Lerp(topBack, topFront, 0.5f);
                b.CylinderBetween(Metal, midBottom, midTop, r, 4);
            }

            b.CylinderBetween(Metal, new Vector3(-0.62f, 1.44f, 4.35f), new Vector3(0.62f, 1.44f, 4.35f), r, 4);
            b.CylinderBetween(Metal, new Vector3(-0.45f, 1.76f, 4.35f), new Vector3(0.45f, 1.76f, 4.35f), r, 4);
            b.CylinderBetween(Metal, new Vector3(0f, 1.44f, 4.35f), new Vector3(0f, 1.76f, 4.35f), r, 4);
            b.CylinderBetween(Metal, new Vector3(-0.65f, 2.2f, 3.4f), new Vector3(0.65f, 2.2f, 3.4f), r, 4);
        }

        private static CachedVehicleMesh BuildMainRotor()
        {
            var b = new TransportMeshBuilder(3);
            b.CylinderBetween(Hub, new Vector3(0f, -0.12f, 0f), new Vector3(0f, 0.1f, 0f), 0.3f, 8);
            b.Sphere(Hub, new Vector3(0f, 0.1f, 0f), new Vector3(0.18f, 0.12f, 0.18f), 8, 4);
            for (var k = 0; k < 4; k++)
            {
                var rotation = Quaternion.Euler(0f, k * 90f + 45f, 0f);
                b.Box(Hub, rotation * new Vector3(0f, 0f, 0.45f), new Vector3(0.2f, 0.12f, 0.6f), rotation);
                b.Box(Blade, rotation * new Vector3(0f, 0.02f, 3.6f), new Vector3(0.5f, 0.05f, 6.8f), rotation);
                b.Box(Tip, rotation * new Vector3(0f, 0.02f, 6.85f), new Vector3(0.52f, 0.056f, 0.3f), rotation);
            }

            return Finish(b, "T70_AnaRotor");
        }

        private static CachedVehicleMesh BuildTailRotor()
        {
            var b = new TransportMeshBuilder(3);
            b.CylinderBetween(Hub, new Vector3(-0.06f, 0f, 0f), new Vector3(0.1f, 0f, 0f), 0.1f, 6);
            for (var k = 0; k < 4; k++)
            {
                var rotation = Quaternion.Euler(k * 90f + 20f, 0f, 0f);
                b.Box(Blade, rotation * new Vector3(0.03f, 0.72f, 0f), new Vector3(0.03f, 1.3f, 0.2f), rotation);
            }

            return Finish(b, "T70_KuyrukRotor");
        }

        private static CachedVehicleMesh BuildDisc()
        {
            var b = new TransportMeshBuilder(1);
            b.Disc(0, new Vector3(0f, 0.02f, 0f), MainRotorRadius, 40, true);
            return Finish(b, "T70_RotorDiski");
        }
    }
}
