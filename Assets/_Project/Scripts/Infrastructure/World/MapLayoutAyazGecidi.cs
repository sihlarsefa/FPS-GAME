using System.Collections.Generic;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// "Ayaz Geçidi" yerleşimi (1000 x 1000 m, x,z in [-500, 500], kuzey = +z): karlı dağ geçidi. Tasarım verisi
    /// Design/Maps/v2/AyazGecidi/layout.json dosyasından aktarılmıştır (tohumdan bağımsız; tohum yalnızca arazi
    /// ayrıntısını, ağaç ve kayaları değiştirir). Kar çizgisi düşüktür (<see cref="AyazGecidiSnowLine"/>).
    /// </summary>
    public sealed partial class MapLayout
    {
        /// <summary>Ayaz Geçidi kar çizgisi (m) — Kuzgun Vadisi'nden belirgin biçimde düşük.</summary>
        public const float AyazGecidiSnowLine = 72f;

        /// <summary>Harita kimliğine göre yerleşim (bilinmeyen/boş → Kuzgun Vadisi).</summary>
        public static MapLayout Create(string mapId, int seed)
        {
            var id = MapCatalog.Normalize(mapId);
            if (id == MapCatalog.AyazGecidi)
                return CreateAyazGecidi(seed);
            if (id == MapCatalog.KartalYaylasi)
                return CreateKartalYaylasi(seed);
            return id == MapCatalog.MaviLiman ? CreateMaviLiman(seed) : CreateKuzgunVadisi(seed);
        }

        public static MapLayout CreateAyazGecidi(int seed)
        {
            var layout = new MapLayout
            {
                HalfSize = 500f,
                MaxHeight = 220f,
                WaterLevel = 12f,
                Seed = seed,
                Name = "Ayaz Geçidi",
                SnowLine = AyazGecidiSnowLine,
                FrozenLakes = true
            };
            layout.Lakes.Add(new LakeSpec { Center = V(345, -350), Radius = 60f, Depth = 3.2f });

            {
                var spec = AddLocation(layout, "Geçit Köyü", LocationKind.Village, V(-140, 10), 65f, 44f, LootTier.Medium, true);
                spec.TargetHeight = 76f;
                spec.ClearRadius = 65f;
            }
            {
                var spec = AddLocation(layout, "Kayak Evi", LocationKind.Village, V(-300, 265), 52f, 35f, LootTier.Medium, true);
                spec.TargetHeight = 100f;
                spec.ClearRadius = 52f;
            }
            {
                var spec = AddLocation(layout, "Radar Üssü", LocationKind.RelayHill, V(275, 305), 55f, 37f, LootTier.High, true);
                spec.TargetHeight = 118f;
                spec.ClearRadius = 55f;
            }
            {
                var spec = AddLocation(layout, "Ayaz Karakolu", LocationKind.Karakol, V(70, 360), 48f, 33f, LootTier.High, true);
                spec.TargetHeight = 106f;
                spec.ClearRadius = 48f;
            }
            {
                var spec = AddLocation(layout, "Dağ İkmal Üssü", LocationKind.ForwardBase, V(235, -175), 66f, 45f, LootTier.Military, true);
                spec.TargetHeight = 69f;
                spec.ClearRadius = 66f;
            }
            {
                var spec = AddLocation(layout, "Karaçam Sırtı", LocationKind.Forest, V(-320, -120), 65f, 18f, LootTier.Low, true);
                spec.TargetHeight = 70f;
                spec.ClearRadius = 22f;
            }
            {
                var spec = AddLocation(layout, "Eski Taş Ocağı", LocationKind.Quarry, V(300, 55), 58f, 39f, LootTier.Medium, true);
                spec.TargetHeight = 86f;
                spec.ClearRadius = 58f;
            }
            {
                var spec = AddLocation(layout, "Yayla Ağılı", LocationKind.Farm, V(-210, -330), 52f, 35f, LootTier.Low, true);
                spec.TargetHeight = 61f;
                spec.ClearRadius = 52f;
            }
            {
                var spec = AddLocation(layout, "Donuk Gözetleme", LocationKind.Outpost, V(-55, 200), 30f, 20f, LootTier.High, false);
                spec.TargetHeight = 94f;
                spec.ClearRadius = 30f;
            }
            {
                var spec = AddLocation(layout, "Güney Sığınağı", LocationKind.Ruins, V(35, -345), 45f, 31f, LootTier.Medium, true);
                spec.TargetHeight = 59f;
                spec.ClearRadius = 45f;
            }

            AddDensifiedRoad(layout, "Geçit Yolu", RoadKind.Asphalt, 8f, new[]
            {
                0f, -490f, 0f, -482.368f, 0f, -474.737f, 0f, -467.105f, 0f, -459.474f, 0f, -451.842f,
                0f, -444.211f, 0f, -436.579f, 0f, -428.947f, 0f, -421.316f, 0f, -413.684f, 0f, -406.053f,
                0f, -398.421f, 0f, -390.789f, 0f, -383.158f, 0f, -375.526f, 0f, -367.895f, 0f, -360.263f,
                0f, -352.632f, 0f, -345f, -1.818f, -337.5f, -3.636f, -330f, -5.455f, -322.5f, -7.273f, -315f,
                -9.091f, -307.5f, -10.909f, -300f, -12.727f, -292.5f, -14.545f, -285f, -16.364f, -277.5f, -18.182f, -270f,
                -20f, -262.5f, -21.818f, -255f, -23.636f, -247.5f, -25.455f, -240f, -27.273f, -232.5f, -29.091f, -225f,
                -30.909f, -217.5f, -32.727f, -210f, -34.545f, -202.5f, -36.364f, -195f, -38.182f, -187.5f, -40f, -180f,
                -40.217f, -172.174f, -40.435f, -164.348f, -40.652f, -156.522f, -40.87f, -148.696f, -41.087f, -140.87f, -41.304f, -133.043f,
                -41.522f, -125.217f, -41.739f, -117.391f, -41.957f, -109.565f, -42.174f, -101.739f, -42.391f, -93.913f, -42.609f, -86.087f,
                -42.826f, -78.261f, -43.043f, -70.435f, -43.261f, -62.609f, -43.478f, -54.783f, -43.696f, -46.957f, -43.913f, -39.13f,
                -44.13f, -31.304f, -44.348f, -23.478f, -44.565f, -15.652f, -44.783f, -7.826f, -45f, 0f, -42.917f, 7.708f,
                -40.833f, 15.417f, -38.75f, 23.125f, -36.667f, 30.833f, -34.583f, 38.542f, -32.5f, 46.25f, -30.417f, 53.958f,
                -28.333f, 61.667f, -26.25f, 69.375f, -24.167f, 77.083f, -22.083f, 84.792f, -20f, 92.5f, -17.917f, 100.208f,
                -15.833f, 107.917f, -13.75f, 115.625f, -11.667f, 123.333f, -9.583f, 131.042f, -7.5f, 138.75f, -5.417f, 146.458f,
                -3.333f, 154.167f, -1.25f, 161.875f, 0.833f, 169.583f, 2.917f, 177.292f, 5f, 185f, 7.708f, 192.292f,
                10.417f, 199.583f, 13.125f, 206.875f, 15.833f, 214.167f, 18.542f, 221.458f, 21.25f, 228.75f, 23.958f, 236.042f,
                26.667f, 243.333f, 29.375f, 250.625f, 32.083f, 257.917f, 34.792f, 265.208f, 37.5f, 272.5f, 40.208f, 279.792f,
                42.917f, 287.083f, 45.625f, 294.375f, 48.333f, 301.667f, 51.042f, 308.958f, 53.75f, 316.25f, 56.458f, 323.542f,
                59.167f, 330.833f, 61.875f, 338.125f, 64.583f, 345.417f, 67.292f, 352.708f, 70f, 360f, 70f, 367.647f,
                70f, 375.294f, 70f, 382.941f, 70f, 390.588f, 70f, 398.235f, 70f, 405.882f, 70f, 413.529f,
                70f, 421.176f, 70f, 428.824f, 70f, 436.471f, 70f, 444.118f, 70f, 451.765f, 70f, 459.412f,
                70f, 467.059f, 70f, 474.706f, 70f, 482.353f, 70f, 490f
            });

            AddDensifiedRoad(layout, "Kayak Evi Yolu", RoadKind.Dirt, 6f, new[]
            {
                5f, 185f, -2.632f, 186.842f, -10.263f, 188.684f, -17.895f, 190.526f, -25.526f, 192.368f, -33.158f, 194.211f,
                -40.789f, 196.053f, -48.421f, 197.895f, -56.053f, 199.737f, -63.684f, 201.579f, -71.316f, 203.421f, -78.947f, 205.263f,
                -86.579f, 207.105f, -94.211f, 208.947f, -101.842f, 210.789f, -109.474f, 212.632f, -117.105f, 214.474f, -124.737f, 216.316f,
                -132.368f, 218.158f, -140f, 220f, -147.619f, 222.143f, -155.238f, 224.286f, -162.857f, 226.429f, -170.476f, 228.571f,
                -178.095f, 230.714f, -185.714f, 232.857f, -193.333f, 235f, -200.952f, 237.143f, -208.571f, 239.286f, -216.19f, 241.429f,
                -223.81f, 243.571f, -231.429f, 245.714f, -239.048f, 247.857f, -246.667f, 250f, -254.286f, 252.143f, -261.905f, 254.286f,
                -269.524f, 256.429f, -277.143f, 258.571f, -284.762f, 260.714f, -292.381f, 262.857f, -300f, 265f
            });

            AddDensifiedRoad(layout, "Radar Servisi", RoadKind.Dirt, 6f, new[]
            {
                70f, 360f, 77.5f, 360.833f, 85f, 361.667f, 92.5f, 362.5f, 100f, 363.333f, 107.5f, 364.167f,
                115f, 365f, 122.5f, 365.833f, 130f, 366.667f, 137.5f, 367.5f, 145f, 368.333f, 152.5f, 369.167f,
                160f, 370f, 166.765f, 366.176f, 173.529f, 362.353f, 180.294f, 358.529f, 187.059f, 354.706f, 193.824f, 350.882f,
                200.588f, 347.059f, 207.353f, 343.235f, 214.118f, 339.412f, 220.882f, 335.588f, 227.647f, 331.765f, 234.412f, 327.941f,
                241.176f, 324.118f, 247.941f, 320.294f, 254.706f, 316.471f, 261.471f, 312.647f, 268.235f, 308.824f, 275f, 305f
            });

            AddDensifiedRoad(layout, "İkmal Yolu", RoadKind.Dirt, 7f, new[]
            {
                -40f, -180f, -32.222f, -181.667f, -24.444f, -183.333f, -16.667f, -185f, -8.889f, -186.667f, -1.111f, -188.333f,
                6.667f, -190f, 14.444f, -191.667f, 22.222f, -193.333f, 30f, -195f, 37.778f, -196.667f, 45.556f, -198.333f,
                53.333f, -200f, 61.111f, -201.667f, 68.889f, -203.333f, 76.667f, -205f, 84.444f, -206.667f, 92.222f, -208.333f,
                100f, -210f, 107.5f, -208.056f, 115f, -206.111f, 122.5f, -204.167f, 130f, -202.222f, 137.5f, -200.278f,
                145f, -198.333f, 152.5f, -196.389f, 160f, -194.444f, 167.5f, -192.5f, 175f, -190.556f, 182.5f, -188.611f,
                190f, -186.667f, 197.5f, -184.722f, 205f, -182.778f, 212.5f, -180.833f, 220f, -178.889f, 227.5f, -176.944f,
                235f, -175f, 237.167f, -167.333f, 239.333f, -159.667f, 241.5f, -152f, 243.667f, -144.333f, 245.833f, -136.667f,
                248f, -129f, 250.167f, -121.333f, 252.333f, -113.667f, 254.5f, -106f, 256.667f, -98.333f, 258.833f, -90.667f,
                261f, -83f, 263.167f, -75.333f, 265.333f, -67.667f, 267.5f, -60f, 269.667f, -52.333f, 271.833f, -44.667f,
                274f, -37f, 276.167f, -29.333f, 278.333f, -21.667f, 280.5f, -14f, 282.667f, -6.333f, 284.833f, 1.333f,
                287f, 9f, 289.167f, 16.667f, 291.333f, 24.333f, 293.5f, 32f, 295.667f, 39.667f, 297.833f, 47.333f,
                300f, 55f, 299.219f, 62.813f, 298.438f, 70.625f, 297.656f, 78.438f, 296.875f, 86.25f, 296.094f, 94.063f,
                295.313f, 101.875f, 294.531f, 109.688f, 293.75f, 117.5f, 292.969f, 125.313f, 292.188f, 133.125f, 291.406f, 140.938f,
                290.625f, 148.75f, 289.844f, 156.563f, 289.063f, 164.375f, 288.281f, 172.188f, 287.5f, 180f, 286.719f, 187.813f,
                285.938f, 195.625f, 285.156f, 203.438f, 284.375f, 211.25f, 283.594f, 219.063f, 282.813f, 226.875f, 282.031f, 234.688f,
                281.25f, 242.5f, 280.469f, 250.313f, 279.688f, 258.125f, 278.906f, 265.938f, 278.125f, 273.75f, 277.344f, 281.563f,
                276.563f, 289.375f, 275.781f, 297.188f, 275f, 305f
            });

            AddDensifiedRoad(layout, "Köy Bağlantısı", RoadKind.Dirt, 5f, new[]
            {
                -45f, 0f, -52.917f, 0.833f, -60.833f, 1.667f, -68.75f, 2.5f, -76.667f, 3.333f, -84.583f, 4.167f,
                -92.5f, 5f, -100.417f, 5.833f, -108.333f, 6.667f, -116.25f, 7.5f, -124.167f, 8.333f, -132.083f, 9.167f,
                -140f, 10f, -147.143f, 6.429f, -154.286f, 2.857f, -161.429f, -0.714f, -168.571f, -4.286f, -175.714f, -7.857f,
                -182.857f, -11.429f, -190f, -15f, -197.143f, -18.571f, -204.286f, -22.143f, -211.429f, -25.714f, -218.571f, -29.286f,
                -225.714f, -32.857f, -232.857f, -36.429f, -240f, -40f, -245.333f, -45.333f, -250.667f, -50.667f, -256f, -56f,
                -261.333f, -61.333f, -266.667f, -66.667f, -272f, -72f, -277.333f, -77.333f, -282.667f, -82.667f, -288f, -88f,
                -293.333f, -93.333f, -298.667f, -98.667f, -304f, -104f, -309.333f, -109.333f, -314.667f, -114.667f, -320f, -120f,
                -316.333f, -127f, -312.667f, -134f, -309f, -141f, -305.333f, -148f, -301.667f, -155f, -298f, -162f,
                -294.333f, -169f, -290.667f, -176f, -287f, -183f, -283.333f, -190f, -279.667f, -197f, -276f, -204f,
                -272.333f, -211f, -268.667f, -218f, -265f, -225f, -261.333f, -232f, -257.667f, -239f, -254f, -246f,
                -250.333f, -253f, -246.667f, -260f, -243f, -267f, -239.333f, -274f, -235.667f, -281f, -232f, -288f,
                -228.333f, -295f, -224.667f, -302f, -221f, -309f, -217.333f, -316f, -213.667f, -323f, -210f, -330f,
                -202.222f, -330.556f, -194.444f, -331.111f, -186.667f, -331.667f, -178.889f, -332.222f, -171.111f, -332.778f, -163.333f, -333.333f,
                -155.556f, -333.889f, -147.778f, -334.444f, -140f, -335f, -132.222f, -335.556f, -124.444f, -336.111f, -116.667f, -336.667f,
                -108.889f, -337.222f, -101.111f, -337.778f, -93.333f, -338.333f, -85.556f, -338.889f, -77.778f, -339.444f, -70f, -340f,
                -62.222f, -340.556f, -54.444f, -341.111f, -46.667f, -341.667f, -38.889f, -342.222f, -31.111f, -342.778f, -23.333f, -343.333f,
                -15.556f, -343.889f, -7.778f, -344.444f, 0f, -345f
            });

            AddDensifiedRoad(layout, "Gözetleme Yolu", RoadKind.Dirt, 4f, new[]
            {
                5f, 185f, -2.5f, 186.875f, -10f, 188.75f, -17.5f, 190.625f, -25f, 192.5f, -32.5f, 194.375f,
                -40f, 196.25f, -47.5f, 198.125f, -55f, 200f
            });

            AddAyazIdentity(layout);
            layout.ComputeBridges();
            return layout;
        }

        /// <summary>Hazır (zaten sıklaştırılmış) x,z çiftlerinden yol ekler.</summary>
        private static void AddDensifiedRoad(MapLayout layout, string name, RoadKind kind, float width, float[] xz)
        {
            var points = new List<Vector2>(xz.Length / 2);
            for (var i = 0; i + 1 < xz.Length; i += 2)
                points.Add(new Vector2(xz[i], xz[i + 1]));
            layout.Roads.Add(new RoadSpec { Name = name, Kind = kind, Width = width, Points = points });
        }
    }
}
