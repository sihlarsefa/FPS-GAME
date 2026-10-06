using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Harita kimliği POI yapıları (Ayaz Geçidi / Mavi Liman / Kartal Yaylası). <see cref="MapLayout"/> kimlik lokasyonlarını
    /// ekler; bu sınıf onları adlarına göre prosedürel kurar (donmuş göl + iskele, kayak istasyonu kalıntısı, tipi bölgesi,
    /// konteyner labirenti + vinçler, balık hali + tekneler, dalgakıran, obsidyen kayalıkları, telesiyej, şenlik alanı...).
    /// Her POI loot seviyelidir (LootSpawnPointData), NavMesh dostudur (koridorlar >= 3 m, dik/ince yapılar kenarda) ve
    /// tohum + ada göre deterministiktir. Kuzgun Vadisi'nde hiçbir şey yapmaz.
    /// </summary>
    public static class MapIdentityProps
    {
        /// <summary>Tipi (kar fırtınası) bölgesi yarıçapı (m) — WeatherSystem presetinin etki alanı.</summary>
        public const float BlizzardRadius = 130f;

        // ------------------------------------------------------------------ Giriş

        public static void BuildIfApplicable(MapLayout layout, Terrain terrain, Transform parent, int seed,
            List<LootSpawnPointData> lootOut, List<Bounds> structuresOut)
        {
            if (layout == null || terrain == null)
                return;
            var isAyaz = layout.Name == MapCatalog.AyazGecidiName;
            var isMavi = layout.Name == MapCatalog.MaviLimanName;
            var isKartal = layout.Name == MapCatalog.KartalYaylasiName;
            if (!isAyaz && !isMavi && !isKartal)
                return;

            var root = new GameObject("[Harita Kimliği]");
            if (parent != null)
                root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            if (isAyaz)
                Safe("Donmuş Göl", () => BuildFrozenLake(new Ctx(layout, terrain, root.transform, seed, "Donmuş Göl", lootOut, structuresOut)));

            for (var i = 0; i < layout.Locations.Count; i++)
            {
                var loc = layout.Locations[i];
                if (loc == null)
                    continue;
                var name = loc.Name;
                var c = new Ctx(layout, terrain, root.transform, seed, name, lootOut, structuresOut);
                var l = loc;
                Safe(name, () => Dispatch(c, l));
            }
        }

        private static void Dispatch(Ctx c, LocationSpec loc)
        {
            switch (loc.Name)
            {
                case MapLayout.AyazKayakKalintiName: BuildSkiRuins(c, loc); break;
                case MapLayout.AyazBarinakName: BuildMountainShelters(c, loc); break;
                case MapLayout.AyazTipiName: BuildBlizzardPost(c, loc); break;
                case MapLayout.AyazSiperName: BuildSnowTrenches(c, loc); break;
                case MapLayout.LimanKonteynerName: BuildContainerYard(c, loc); break;
                case MapLayout.LimanBalikHaliName: BuildFishMarket(c, loc); break;
                case MapLayout.LimanDalgakiranName: BuildBreakwater(c, loc); break;
                case MaviLimanProps.LighthouseName: BuildLighthouseExtras(c, loc); break;
                case MapLayout.KartalObsidyenName: BuildObsidian(c, loc); break;
                case MapLayout.KartalYukariMeraName:
                case MapLayout.KartalKuzeyMeraName: BuildPasture(c, loc); break;
                case MapLayout.KartalTeleAltName: BuildLiftStation(c, loc, true); break;
                case MapLayout.KartalTeleUstName: BuildLiftStation(c, loc, false); break;
                case MapLayout.KartalSenlikName: BuildFestival(c, loc); break;
            }
        }

        private static void Safe(string what, Action action)
        {
            try { action(); }
            catch (Exception e) { Debug.LogWarning("[MapIdentityProps] " + what + " üretilemedi: " + e.Message); }
        }

        /// <summary>Tipi bölgesi (merkez XZ, yarıçap) — Ayaz'da var; diğer haritalarda false. WeatherSystem ENTEGRASYON kancası.</summary>
        public static bool TryGetBlizzardZone(MapLayout layout, out Vector2 center, out float radius)
        {
            center = Vector2.zero;
            radius = 0f;
            if (layout == null || layout.Locations == null)
                return false;
            for (var i = 0; i < layout.Locations.Count; i++)
            {
                var l = layout.Locations[i];
                if (l != null && l.Name == MapLayout.AyazTipiName)
                {
                    center = l.Center;
                    radius = BlizzardRadius;
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------ Saf mantık (test edilebilir)

        /// <summary>a'dan b'ye eşit aralıklı noktalar (uçlar dahil, en az 2).</summary>
        public static List<Vector2> LinePoints(Vector2 a, Vector2 b, float spacing)
        {
            var len = Vector2.Distance(a, b);
            var n = Mathf.Max(1, Mathf.CeilToInt(len / Mathf.Max(0.5f, spacing)));
            var list = new List<Vector2>(n + 1);
            for (var i = 0; i <= n; i++)
                list.Add(Vector2.Lerp(a, b, i / (float)n));
            return list;
        }

        /// <summary>
        /// Konteyner yığın haritası [col,row] = kat sayısı (0..3). Her 3. satır/sütun açık koridordur (labirent iskeleti);
        /// kalan hücreler tohumla doldurulur. Ayrıca (rampCol, rampRow) tek katlı rampa hedefi döner.
        /// </summary>
        public static int[,] ContainerMaze(int cols, int rows, int seed, out int rampCol, out int rampRow)
        {
            var rng = new System.Random(seed * 31 + 7);
            var map = new int[Math.Max(1, cols), Math.Max(1, rows)];
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    if (r % 3 == 2 || c % 3 == 2)
                        continue;
                    var roll = rng.NextDouble();
                    map[c, r] = roll < 0.18 ? 0 : roll < 0.55 ? 1 : roll < 0.85 ? 2 : 3;
                }
            }

            rampCol = -1;
            rampRow = -1;
            // Rampa: ilk koridor satırının hemen üstündeki dolu olmayan hücre (r%3==0) tek kata sabitlenir.
            for (var c = 0; c < cols && rampCol < 0; c++)
            {
                if (c % 3 == 2 || rows < 4)
                    continue;
                map[c, 3 % rows] = 1;
                rampCol = c;
                rampRow = 3 % rows;
            }

            return map;
        }

        /// <summary>Doluluk oranı (koridor olmayan açık hücre / tüm hücre) — labirent açıklığı denetimi.</summary>
        public static float OpenRatio(int[,] maze)
        {
            if (maze == null)
                return 1f;
            var open = 0;
            var total = maze.GetLength(0) * maze.GetLength(1);
            for (var c = 0; c < maze.GetLength(0); c++)
                for (var r = 0; r < maze.GetLength(1); r++)
                    if (maze[c, r] == 0)
                        open++;
            return total == 0 ? 1f : open / (float)total;
        }

        /// <summary>from'dan dir yönünde zemin ilk kez minHeight üstüne çıktığı mesafe (kıyıya çıkış); yoksa -1.</summary>
        public static float FindLandDistance(Func<float, float, float> height, Vector2 from, Vector2 dir, float minHeight,
            float maxDistance, float step = 2f)
        {
            if (height == null || dir.sqrMagnitude < 1e-6f)
                return -1f;
            dir.Normalize();
            for (var d = 0f; d <= maxDistance; d += step)
            {
                var p = from + dir * d;
                if (height(p.x, p.y) > minHeight)
                    return d;
            }

            return -1f;
        }

        /// <summary>İsimden kararlı (platformdan bağımsız) tohum.</summary>
        public static int StableHash(string s)
        {
            unchecked
            {
                var h = (int)2166136261u;
                if (s != null)
                    for (var i = 0; i < s.Length; i++)
                        h = (h ^ s[i]) * 16777619;
                return h;
            }
        }

        // ------------------------------------------------------------------ Bağlam ve yardımcılar

        private sealed class Ctx
        {
            public readonly MapLayout Layout;
            public readonly Terrain Terrain;
            public readonly Transform Folder;
            public readonly System.Random Rng;
            public readonly List<LootSpawnPointData> Loot;
            public readonly List<Bounds> Structs;
            public readonly float Water;

            public Ctx(MapLayout layout, Terrain terrain, Transform parent, int seed, string name,
                List<LootSpawnPointData> loot, List<Bounds> structs)
            {
                Layout = layout;
                Terrain = terrain;
                Rng = new System.Random(seed ^ StableHash(name) ^ 0x1D3A);
                Loot = loot;
                Structs = structs;
                Water = layout.WaterLevel;
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                Folder = go.transform;
            }

            public float R() => (float)Rng.NextDouble();
            public float R(float a, float b) => a + (b - a) * (float)Rng.NextDouble();

            public Vector3 Ground(float x, float z)
                => new Vector3(x, Terrain.SampleHeight(new Vector3(x, 0f, z)) + Terrain.transform.position.y, z);

            public Vector3 GroundV(Vector2 p) => Ground(p.x, p.y);

            public void AddLoot(Vector3 p, LootTier tier) => Loot?.Add(new LootSpawnPointData(p, tier));
            public void AddBounds(Bounds b) => Structs?.Add(b);
        }

        private static GameObject Box(Ctx c, Transform parent, string name, Vector3 center, Vector3 size, float yaw, MaterialId mat,
            bool collider = true)
        {
            var go = StructureKit.CreateBox(parent, name, center, size, Quaternion.Euler(0f, yaw, 0f), mat, collider);
            go.isStatic = true;
            return go;
        }

        /// <summary>Zemine oturan kutu: taban yüksekliği zeminde, h kadar yukarı.</summary>
        private static GameObject GroundBox(Ctx c, Transform parent, string name, Vector2 xz, Vector3 size, float yaw, MaterialId mat,
            bool collider = true)
        {
            var g = c.Ground(xz.x, xz.y);
            return Box(c, parent, name, new Vector3(xz.x, g.y + size.y * 0.5f - 0.05f, xz.y), size, yaw, mat, collider);
        }

        private static GameObject Cyl(Transform parent, string name, Vector3 center, float radius, float height, MaterialId mat,
            bool collider = true)
        {
            var go = StructureKit.CreateCylinder(parent, name, center, radius, height, mat, collider);
            go.isStatic = true;
            return go;
        }

        private static Transform Sub(Ctx c, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(c.Folder, false);
            return go.transform;
        }

        private static Vector2 Dir(float degrees)
        {
            var r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        private static float YawOfDir(Vector2 d) => Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;

        private static BuildingResult Place(Ctx c, Transform parent, string name, BuildingStyle style, Vector3 pos, float yaw,
            float w, float d, int floors, LootTier tier, bool ruined)
        {
            var spec = new BuildingSpec
            {
                Name = name,
                Style = style,
                Position = pos,
                Yaw = yaw,
                Width = w,
                Depth = d,
                Floors = floors,
                FloorHeight = 3.2f,
                Ruined = ruined,
                Tier = tier,
                Seed = c.Rng.Next()
            };
            BuildingResult res;
            try { res = BuildingGenerator.Build(spec, parent); }
            catch (Exception e)
            {
                Debug.LogWarning("[MapIdentityProps] Bina üretilemedi: " + name + " — " + e.Message);
                return null;
            }

            if (res == null)
                return null;
            if (res.Bounds.size.sqrMagnitude > 0.01f)
                c.AddBounds(res.Bounds);
            if (res.LootPoints != null && res.LootPoints.Count > 0)
            {
                for (var i = 0; i < res.LootPoints.Count; i++)
                    c.AddLoot(res.LootPoints[i], tier);
            }
            else
            {
                c.AddLoot(pos + Vector3.up, tier);
            }

            return res;
        }

        /// <summary>Kar seti / kar yığını: basık gövde (çarpıştırıcılı siper veya dekor).</summary>
        private static void SnowBerm(Ctx c, Transform parent, Vector2 xz, float yaw, float len, float h, bool collider = true)
            => GroundBox(c, parent, "KarSiperi", xz, new Vector3(len, h, 1.5f), yaw, MaterialId.Snow, collider);

        private static void Pole(Transform parent, Vector3 basePos, float h, MaterialId mat)
            => Cyl(parent, "Direk", basePos + Vector3.up * (h * 0.5f), 0.12f, h, mat);

        // ================================================================== AYAZ GEÇİDİ

        /// <summary>Donmuş göl: buz levhası adası + 4 köprü (biri kıyı iskelesi), buz blokları, balıkçı çadırları, loot.</summary>
        private static void BuildFrozenLake(Ctx c)
        {
            if (c.Layout.Lakes.Count == 0)
                return;
            var lake = c.Layout.Lakes[0];
            var w = c.Water;
            var ice = Sub(c, "Buz Üstü");
            var iceTopDelta = 0.1f;
            var cx = lake.Center.x;
            var cz = lake.Center.y;

            // Merkez ada.
            Box(c, ice, "BuzAda", new Vector3(cx, w + iceTopDelta - 0.45f, cz), new Vector3(24f, 0.9f, 24f), 12f, MaterialId.Snow);
            c.AddBounds(new Bounds(new Vector3(cx, w, cz), new Vector3(26f, 3f, 26f)));

            // Kıyı iskelesi yönü: haritanın merkezine doğru.
            var toCenter = (-lake.Center).sqrMagnitude < 1f ? Vector2.left : (-lake.Center).normalized;
            var baseAngle = Mathf.Atan2(toCenter.y, toCenter.x) * Mathf.Rad2Deg;
            var reach = lake.Radius * 0.78f;
            for (var k = 0; k < 4; k++)
            {
                var d = Dir(baseAngle + k * 90f);
                var yaw = YawOfDir(d);
                var wood = k == 0;
                var start = 12f;
                var end = reach;
                if (wood)
                {
                    var land = FindLandDistance((x, z) => c.Ground(x, z).y, lake.Center, d, w + 0.9f, lake.Radius * 1.8f);
                    end = land > 0f ? land + 2f : lake.Radius + 6f;
                }

                var len = Mathf.Max(6f, end - start);
                var mid = lake.Center + d * (start + len * 0.5f);
                var top = wood ? 0.25f : iceTopDelta;
                Box(c, ice, wood ? "İskele" : "BuzKöprü" + k, new Vector3(mid.x, w + top - 0.2f, mid.y),
                    new Vector3(3.4f, 0.4f, len), yaw, wood ? MaterialId.WoodDark : MaterialId.Snow);
                if (wood)
                {
                    for (var i = 0; i <= (int)(len / 5f); i++)
                    {
                        var p = lake.Center + d * (start + i * 5f);
                        var side = new Vector2(-d.y, d.x) * 1.8f;
                        Cyl(ice, "Kazık", new Vector3(p.x + side.x, w + 0.3f, p.y + side.y), 0.16f, 1.2f, MaterialId.WoodDark);
                        Cyl(ice, "Kazık", new Vector3(p.x - side.x, w + 0.3f, p.y - side.y), 0.16f, 1.2f, MaterialId.WoodDark);
                    }

                    // Kıyıdaki balıkçı barakası.
                    var shed = lake.Center + d * (end + 5f) + new Vector2(-d.y, d.x) * 7f;
                    Place(c, ice, "Buz Balıkçısı Barakası", BuildingStyle.Shed, c.GroundV(shed), yaw + 90f, 5f, 4f, 1,
                        LootTier.Medium, false);
                    PropFactory.Barrel(ice, c.GroundV(shed + d * 4f), c.R(0f, 360f), c.Rng);
                    PropFactory.Woodpile(ice, c.GroundV(shed - d * 3f), yaw, c.Rng);
                }
                else
                {
                    // Uç levha: iki blok + orta seviye loot.
                    var tip = lake.Center + d * reach;
                    Box(c, ice, "BuzLevha" + k, new Vector3(tip.x, w + iceTopDelta - 0.45f, tip.y), new Vector3(9f, 0.9f, 9f),
                        yaw + 20f, MaterialId.Snow);
                    for (var b = 0; b < 2; b++)
                    {
                        var o = tip + Dir(c.R(0f, 360f)) * c.R(1.5f, 3f);
                        Box(c, ice, "BuzBlok", new Vector3(o.x, w + 0.8f, o.y), new Vector3(2.2f, 1.5f, 1.8f), c.R(0f, 180f), MaterialId.Snow);
                    }

                    c.AddLoot(new Vector3(tip.x, w + 0.7f, tip.y), LootTier.Medium);
                }
            }

            // Ada siperleri: buz bloklar halkası + çadırlar + açık koridorlar (>= 3 m).
            for (var i = 0; i < 8; i++)
            {
                var a = baseAngle + 45f * i + 22f;
                var o = lake.Center + Dir(a) * c.R(6.5f, 9f);
                Box(c, ice, "BuzSiper", new Vector3(o.x, w + 0.85f, o.y),
                    new Vector3(2f + c.R() * 2f, 1.5f + c.R() * 0.6f, 1.5f + c.R() * 1.2f), a + 90f, MaterialId.Snow);
            }

            for (var t = 0; t < 2; t++)
            {
                var o = lake.Center + Dir(baseAngle + 90f + t * 180f) * 3.5f;
                var tent = PropFactory.Tent(ice, new Vector3(o.x, w + iceTopDelta, o.y), baseAngle + t * 180f, c.Rng);
                if (tent != null)
                    tent.transform.localScale = Vector3.one * 0.8f;
            }

            for (var h = 0; h < 5; h++)
            {
                var o = lake.Center + Dir(c.R(0f, 360f)) * c.R(2f, 10f);
                Cyl(ice, "BuzDelik", new Vector3(o.x, w + iceTopDelta + 0.02f, o.y), 0.5f, 0.02f, MaterialId.Black, false);
            }

            c.AddLoot(new Vector3(cx, w + 0.8f, cz), LootTier.High);
            c.AddLoot(new Vector3(cx + 3f, w + 0.8f, cz - 2f), LootTier.High);
        }

        private static void BuildSkiRuins(Ctx c, LocationSpec loc)
        {
            var f = c.Folder;
            var center = c.GroundV(loc.Center);
            Place(c, f, "Lift İstasyonu", BuildingStyle.Barn, center, 20f, 16f, 10f, 1, LootTier.Medium, true);
            Place(c, f, "Bilet Kulübesi", BuildingStyle.Shed, c.Ground(loc.Center.x + 16f, loc.Center.y - 10f), 70f, 5f, 4f, 1,
                LootTier.Medium, true);

            // Telesiyej direkleri: biri devrilmiş, kablo kalıntısı, düşmüş sandalyeler.
            var dir = (new Vector2(-300f, 265f) - loc.Center).normalized;
            var yaw = YawOfDir(dir);
            for (var i = 0; i < 4; i++)
            {
                var p = loc.Center + dir * (24f + i * 28f);
                var g = c.GroundV(p);
                if (i == 2)
                {
                    Box(c, f, "DevrikDirek", g + new Vector3(0f, 1.0f, 0f), new Vector3(0.5f, 0.5f, 9f), yaw + 25f, MaterialId.Rust);
                    Box(c, f, "DevrikKol", g + new Vector3(1.5f, 0.6f, 0f), new Vector3(3.2f, 0.3f, 0.3f), yaw + 25f, MaterialId.Rust, false);
                    continue;
                }

                var side = new Vector2(-dir.y, dir.x) * 0.7f;
                Cyl(f, "Direk", g + new Vector3(side.x, 4.5f, side.y), 0.22f, 9f, MaterialId.MetalDark);
                Cyl(f, "Direk", g + new Vector3(-side.x, 4.5f, -side.y), 0.22f, 9f, MaterialId.MetalDark);
                Box(c, f, "Kol", g + Vector3.up * 9f, new Vector3(4f, 0.25f, 0.3f), yaw + 90f, MaterialId.MetalDark, false);
            }

            for (var i = 0; i < 3; i++)
            {
                var p = loc.Center + dir * (60f + i * 6f) + new Vector2(-dir.y, dir.x) * c.R(-4f, 4f);
                GroundBox(c, f, "DüşenSandalye", p, new Vector3(1.4f, 0.5f, 0.7f), c.R(0f, 180f), MaterialId.Red);
            }

            for (var i = 0; i < 4; i++)
            {
                var p = loc.Center + Dir(c.R(0f, 360f)) * c.R(10f, 28f);
                GroundBox(c, f, "KarYığını", p, new Vector3(c.R(4f, 7f), c.R(1f, 1.8f), c.R(3f, 5f)), c.R(0f, 180f), MaterialId.Snow, false);
            }

            PropFactory.Wreck(f, c.Ground(loc.Center.x - 12f, loc.Center.y + 12f), 40f, c.Rng);
            PropFactory.Container(f, c.Ground(loc.Center.x + 6f, loc.Center.y + 20f), 100f, c.Rng);
            c.AddLoot(center + new Vector3(-3f, 1f, 2f), LootTier.Medium);
            c.AddLoot(c.Ground(loc.Center.x + 7f, loc.Center.y + 20f) + Vector3.up, LootTier.Medium);
        }

        private static void BuildMountainShelters(Ctx c, LocationSpec loc)
        {
            var f = c.Folder;
            for (var i = 0; i < 3; i++)
            {
                var a = i * 120f + 20f;
                var p = loc.Center + Dir(a) * 14f;
                Place(c, f, "Dağ Barınağı " + (i + 1), BuildingStyle.ShepherdHut, c.GroundV(p), -a + 90f, 5f, 4f, 1, LootTier.Low, false);
                SnowBerm(c, f, loc.Center + Dir(a) * 21f, -a, 6f, 1.2f);
            }

            // Ortak ateş çukuru, odun, taş duvar parçaları.
            var fire = c.GroundV(loc.Center);
            for (var i = 0; i < 6; i++)
            {
                var o = Dir(i * 60f) * 1.3f;
                Box(c, f, "AteşTaşı", fire + new Vector3(o.x, 0.2f, o.y), new Vector3(0.5f, 0.4f, 0.5f), i * 60f, MaterialId.StoneDark, false);
            }

            Cyl(f, "Ateş", fire + Vector3.up * 0.3f, 0.45f, 0.4f, MaterialId.Fire, false);
            PropFactory.Woodpile(f, c.Ground(loc.Center.x + 4f, loc.Center.y - 4f), 30f, c.Rng);
            for (var i = 0; i < 4; i++)
                GroundBox(c, f, "TaşDuvar", loc.Center + Dir(i * 90f + 45f) * 26f, new Vector3(4.5f, 1f, 0.6f), i * 90f + 45f, MaterialId.Stone);
            c.AddLoot(fire + new Vector3(2f, 0.8f, 1f), LootTier.Low);
        }

        private static void BuildBlizzardPost(Ctx c, LocationSpec loc)
        {
            var f = c.Folder;
            var center = c.GroundV(loc.Center);
            Place(c, f, "Meteoroloji Kulübesi", BuildingStyle.Shed, center, 0f, 6f, 5f, 1, LootTier.High, false);
            PropFactory.Antenna(f, c.Ground(loc.Center.x + 6f, loc.Center.y + 3f), 0f, c.Rng);
            PropFactory.Antenna(f, c.Ground(loc.Center.x - 6f, loc.Center.y + 4f), 40f, c.Rng);
            // Rüzgâr ölçer direği.
            var an = c.Ground(loc.Center.x + 2f, loc.Center.y - 8f);
            Cyl(f, "Rüzgârölçer", an + Vector3.up * 4f, 0.1f, 8f, MaterialId.MetalPanel);
            Box(c, f, "Kollar", an + Vector3.up * 8.1f, new Vector3(1.6f, 0.08f, 0.08f), 0f, MaterialId.Red, false);

            // Tipi sınırı: kırmızı-beyaz işaret direkleri halkası + rüzgâra karşı kar siperleri.
            const int markers = 14;
            for (var i = 0; i < markers; i++)
            {
                var o = loc.Center + Dir(i * 360f / markers) * (loc.Radius - 2f);
                var g = c.GroundV(o);
                Pole(f, g, 4.5f, i % 2 == 0 ? MaterialId.Red : MaterialId.White);
                Box(c, f, "Flama", g + new Vector3(0.4f, 4.2f, 0f), new Vector3(0.8f, 0.5f, 0.04f), i * 25f, MaterialId.Orange, false);
            }

            for (var i = 0; i < 5; i++)
                SnowBerm(c, f, loc.Center + Dir(150f + i * 15f) * 15f, 90f + 150f + i * 15f, 5.5f, 1.4f);
            c.AddLoot(center + new Vector3(0f, 0.8f, 2f), LootTier.High);
            c.AddLoot(c.Ground(loc.Center.x - 5f, loc.Center.y - 9f) + Vector3.up, LootTier.High);
        }

        private static void BuildSnowTrenches(Ctx c, LocationSpec loc)
        {
            var f = c.Folder;
            for (var row = 0; row < 2; row++)
            {
                for (var i = 0; i < 6; i++)
                {
                    var p = loc.Center + new Vector2(-15f + i * 6f + row * 3f, row * 9f - 4f);
                    SnowBerm(c, f, p, 0f, 5f, 1.3f);
                    if (i % 2 == 0)
                        PropFactory.Sandbags(f, c.Ground(p.x, p.y + 1.2f), 0f, c.Rng);
                }
            }

            for (var i = 0; i < 4; i++)
            {
                var o = loc.Center + new Vector2(-9f + i * 6f, 2f);
                Cyl(f, "Siper Çukuru", c.GroundV(o) + Vector3.up * 0.02f, 0.9f, 0.04f, MaterialId.Black, false);
            }

            PropFactory.AmmoCrate(f, c.Ground(loc.Center.x, loc.Center.y + 5f), 15f, c.Rng);
            PropFactory.AmmoCrate(f, c.Ground(loc.Center.x + 3f, loc.Center.y + 5f), -10f, c.Rng);
            c.AddLoot(c.Ground(loc.Center.x, loc.Center.y + 5f) + Vector3.up, LootTier.High);
            for (var i = 0; i < 3; i++)
                c.AddLoot(c.Ground(loc.Center.x - 8f + i * 8f, loc.Center.y - 2f) + Vector3.up, LootTier.Medium);
        }

        // ================================================================== MAVİ LİMAN

        private static readonly MaterialId[] ContainerPalette =
        {
            MaterialId.Red, MaterialId.Blue, MaterialId.Orange, MaterialId.Green, MaterialId.MetalPanel, MaterialId.Rust,
            MaterialId.Yellow, MaterialId.Gray
        };

        private static void BuildContainerYard(Ctx c, LocationSpec loc)
        {
            const int cols = 6;
            const int rows = 9;
            const float pitchX = 8.4f;
            const float pitchZ = 5.8f;
            var f = c.Folder;
            var maze = ContainerMaze(cols, rows, c.Rng.Next(1, 100000), out var rampCol, out var rampRow);
            var origin = loc.Center - new Vector2((cols - 1) * pitchX * 0.5f, (rows - 1) * pitchZ * 0.5f);
            var baseY = c.Ground(loc.Center.x, loc.Center.y).y;
            for (var r = 0; r < rows; r++)
            {
                for (var col = 0; col < cols; col++)
                {
                    var levels = maze[col, r];
                    var cell = origin + new Vector2(col * pitchX, r * pitchZ);
                    if (levels == 0)
                    {
                        if (c.R() < 0.18f)
                            PropFactory.Barrel(f, new Vector3(cell.x, baseY, cell.y), c.R(0f, 360f), c.Rng);
                        continue;
                    }

                    var mat = ContainerPalette[c.Rng.Next(ContainerPalette.Length)];
                    for (var lv = 0; lv < levels; lv++)
                    {
                        var jitter = lv == 0 ? 0f : c.R(-0.25f, 0.25f);
                        Box(c, f, "Konteyner", new Vector3(cell.x + jitter, baseY + lv * 2.6f + 1.3f, cell.y), new Vector3(6f, 2.5f, 2.4f),
                            0f, lv == 0 ? mat : ContainerPalette[c.Rng.Next(ContainerPalette.Length)]);
                        Box(c, f, "Kapı", new Vector3(cell.x + 3.05f, baseY + lv * 2.6f + 1.2f, cell.y), new Vector3(0.08f, 2.2f, 2.2f), 0f,
                            MaterialId.MetalDark, false);
                    }
                }
            }

            c.AddBounds(new Bounds(new Vector3(loc.Center.x, baseY + 3f, loc.Center.y), new Vector3(cols * pitchX, 8f, rows * pitchZ)));

            // Rampa: koridordan tek katlı yığının tepesine.
            if (rampCol >= 0)
            {
                var cell = origin + new Vector2(rampCol * pitchX, rampRow * pitchZ);
                var edge = cell.y - 1.2f;
                var run = 6f;
                var ang = Mathf.Atan2(2.6f, run) * Mathf.Rad2Deg;
                var len = Mathf.Sqrt(run * run + 2.6f * 2.6f);
                var ramp = StructureKit.CreateBox(f, "Rampa", new Vector3(cell.x, baseY + 1.3f - 0.1f, edge - run * 0.5f),
                    new Vector3(2.2f, 0.25f, len), Quaternion.Euler(-ang, 0f, 0f), MaterialId.WoodDark);
                ramp.isStatic = true;
                // Yığın üstü loot (riskli tepe) + koridor kavşağı lootları.
                c.AddLoot(new Vector3(cell.x, baseY + 3.2f, cell.y), LootTier.Military);
            }

            for (var r = 2; r < rows; r += 3)
            {
                for (var col = 2; col < cols; col += 3)
                {
                    var cell = origin + new Vector2(col * pitchX, r * pitchZ);
                    c.AddLoot(new Vector3(cell.x, baseY + 0.7f, cell.y), LootTier.High);
                }
            }

            // Rıhtım vinçleri (rayda) — yığının iki yanında, yürüyüş koridorunu tıkamaz.
            for (var k = 0; k < 2; k++)
            {
                var side = k == 0 ? -1f : 1f;
                var cp = new Vector2(loc.Center.x + side * (cols * pitchX * 0.5f + 9f), loc.Center.y + (k == 0 ? 8f : -8f));
                BuildGantryCrane(c, f, c.GroundV(cp), side < 0f ? 0f : 180f);
            }

            Place(c, f, "Gümrük Ofisi", BuildingStyle.Shed, c.Ground(loc.Center.x, loc.Center.y - (rows * pitchZ * 0.5f) - 8f), 0f, 7f, 5f, 1,
                LootTier.Military, false);
        }

        private static void BuildGantryCrane(Ctx c, Transform parent, Vector3 g, float yaw)
        {
            var root = StructureKit.CreateGroup(parent, "Rıhtım Vinci", g, Quaternion.Euler(0f, yaw, 0f));
            const float h = 17f;
            for (var sx = -1; sx <= 1; sx += 2)
            {
                for (var sz = -1; sz <= 1; sz += 2)
                    StructureKit.CreateBox(root.transform, "Ayak", new Vector3(sx * 6f, h * 0.5f, sz * 4f), new Vector3(0.9f, h, 0.9f),
                        Quaternion.identity, MaterialId.Yellow);
            }

            StructureKit.CreateBox(root.transform, "Kiriş", new Vector3(0f, h, 0f), new Vector3(13f, 1.1f, 9f), Quaternion.identity,
                MaterialId.Yellow, false);
            StructureKit.CreateBox(root.transform, "Bom", new Vector3(14f, h + 0.3f, 0f), new Vector3(18f, 0.7f, 1.2f), Quaternion.identity,
                MaterialId.Gray, false);
            StructureKit.CreateBox(root.transform, "Kabin", new Vector3(-2f, h - 1.6f, 4.8f), new Vector3(2.2f, 2f, 2.2f), Quaternion.identity,
                MaterialId.Orange, false);
            StructureKit.CreateBox(root.transform, "Halat", new Vector3(10f, h - 4.5f, 0f), new Vector3(0.1f, 8f, 0.1f), Quaternion.identity,
                MaterialId.MetalDark, false);
            StructureKit.MarkStatic(root);
            c.AddBounds(new Bounds(g + Vector3.up * (h * 0.5f), new Vector3(14f, h, 10f)));
        }

        private static Vector2 NearestLakeDir(MapLayout layout, Vector2 from)
        {
            var best = float.MaxValue;
            var dir = Vector2.right;
            for (var i = 0; i < layout.Lakes.Count; i++)
            {
                var v = layout.Lakes[i].Center - from;
                if (v.sqrMagnitude < best && v.sqrMagnitude > 1f)
                {
                    best = v.sqrMagnitude;
                    dir = v.normalized;
                }
            }

            return dir;
        }

        private static void BuildFishMarket(Ctx c, LocationSpec loc)
        {
            var f = c.Folder;
            var w = c.Water;
            var center = c.GroundV(loc.Center);
            var dir = NearestLakeDir(c.Layout, loc.Center);
            var side = new Vector2(-dir.y, dir.x);
            var yaw = YawOfDir(dir);

            Place(c, f, "Balık Hali", BuildingStyle.Barn, center - new Vector3(dir.x, 0f, dir.y) * 6f, yaw + 90f, 16f, 9f, 1, LootTier.Medium, false);
            Place(c, f, "Soğuk Hava Deposu", BuildingStyle.Shed, c.GroundV(loc.Center - dir * 8f + side * 14f), yaw, 7f, 5f, 1, LootTier.Medium, false);

            // Balık kasaları ve ağ direkleri.
            for (var i = 0; i < 6; i++)
            {
                var p = loc.Center + dir * c.R(6f, 12f) + side * (i * 1.4f - 4f);
                GroundBox(c, f, "BalıkKasası", p, new Vector3(1.1f, 0.5f + (i % 2) * 0.5f, 0.7f), yaw, i % 2 == 0 ? MaterialId.Blue : MaterialId.White);
            }

            for (var i = 0; i < 3; i++)
            {
                var p = loc.Center + side * (-14f + i * 3.4f) + dir * 12f;
                var g = c.GroundV(p);
                Box(c, f, "AğDireği", g + Vector3.up * 1.5f, new Vector3(0.14f, 3f, 0.14f), 0f, MaterialId.Wood);
                Box(c, f, "Ağ", g + new Vector3(0f, 1.9f, 0f), new Vector3(3f, 1.4f, 0.03f), yaw + 90f, MaterialId.CamoNet, false);
            }

            // İskele + bağlı balıkçı tekneleri.
            var shore = MaviLimanProps.FindShoreDistance((x, z) => c.Ground(x, z).y, loc.Center, dir, w, 260f);
            var start = shore > 0f ? loc.Center + dir * Mathf.Max(0f, shore - 10f) : loc.Center + dir * (loc.Radius * 0.8f);
            const float length = 38f;
            var deckY = w + 1.0f;
            var mid = start + dir * (length * 0.5f);
            Box(c, f, "Rıhtım", new Vector3(mid.x, deckY - 0.25f, mid.y), new Vector3(4f, 0.5f, length), yaw, MaterialId.WoodDark);
            for (var i = 0; i < 6; i++)
            {
                var p = start + dir * (3f + i * 6f);
                Cyl(f, "Kazık", new Vector3(p.x + side.x * 2.2f, w - 1.2f, p.y + side.y * 2.2f), 0.3f, 4.6f, MaterialId.WoodDark);
                Cyl(f, "Kazık", new Vector3(p.x - side.x * 2.2f, w - 1.2f, p.y - side.y * 2.2f), 0.3f, 4.6f, MaterialId.WoodDark);
            }

            c.AddBounds(new Bounds(new Vector3(mid.x, deckY, mid.y), new Vector3(8f, 3f, length)));
            for (var i = 0; i < 4; i++)
            {
                var s = i % 2 == 0 ? 1f : -1f;
                var p = start + dir * (8f + (i / 2) * 15f) + side * s * 4.6f;
                BuildBoat(c, f, new Vector3(p.x, w, p.y), yaw, ContainerPalette[(i * 3 + 1) % ContainerPalette.Length]);
                c.AddLoot(new Vector3(p.x, w + 1.4f, p.y), i == 3 ? LootTier.High : LootTier.Medium);
            }

            c.AddLoot(center + Vector3.up * 0.8f, LootTier.Medium);
        }

        private static void BuildBoat(Ctx c, Transform parent, Vector3 pos, float yaw, MaterialId hull)
        {
            var root = StructureKit.CreateGroup(parent, "Balıkçı Teknesi", pos, Quaternion.Euler(0f, yaw, 0f));
            var t = root.transform;
            StructureKit.CreateBox(t, "Gövde", new Vector3(0f, 0.2f, 0f), new Vector3(2.4f, 1.1f, 6.2f), Quaternion.identity, hull);
            StructureKit.CreateBox(t, "Burun", new Vector3(0f, 0.2f, 3.3f), new Vector3(1.7f, 1.1f, 1.7f), Quaternion.Euler(0f, 45f, 0f), hull);
            StructureKit.CreateBox(t, "Kabin", new Vector3(0f, 1.5f, -0.9f), new Vector3(1.8f, 1.5f, 2.2f), Quaternion.identity, MaterialId.White);
            StructureKit.CreateBox(t, "Cam", new Vector3(0f, 1.8f, 0.22f), new Vector3(1.5f, 0.6f, 0.05f), Quaternion.identity, MaterialId.Glass, false);
            StructureKit.CreateCylinder(t, "Direk", new Vector3(0f, 2.8f, 1.5f), 0.06f, 3.6f, MaterialId.MetalDark, false);
            StructureKit.MarkStatic(root);
        }

        private static void BuildBreakwater(Ctx c, LocationSpec loc)
        {
            var f = c.Folder;
            var w = c.Water;
            var dir = NearestLakeDir(c.Layout, loc.Center);
            var side = new Vector2(-dir.y, dir.x);
            var shore = MaviLimanProps.FindShoreDistance((x, z) => c.Ground(x, z).y, loc.Center, dir, w, 260f);
            var start = loc.Center + dir * (shore > 0f ? Mathf.Max(0f, shore - 3f) : 28f);
            const float length = 90f;
            const float seg = 6f;
            var yaw = YawOfDir(dir);
            for (var i = 0; i < (int)(length / seg); i++)
            {
                var p = start + dir * (i * seg + seg * 0.5f) + side * (i % 2 == 0 ? 0.4f : -0.4f);
                Box(c, f, "DalgakıranBlok", new Vector3(p.x, w - 0.7f, p.y), new Vector3(5.6f, 4f, seg + 0.4f), yaw + c.R(-5f, 5f), MaterialId.Stone);
                if (i % 2 == 0)
                {
                    var rp = p + side * 4.2f * (i % 4 == 0 ? 1f : -1f);
                    RockFactory.CreateRock(f, new Vector3(rp.x, w + 0.1f, rp.y), Quaternion.Euler(0f, c.R(0f, 360f), 0f),
                        new Vector3(2.4f, 1.8f, 2.4f), i, true).isStatic = true;
                }
            }

            var tip = start + dir * length;
            Box(c, f, "DalgakıranUç", new Vector3(tip.x, w - 0.7f, tip.y), new Vector3(9f, 4f, 9f), yaw, MaterialId.Concrete);
            Cyl(f, "Fener Direği", new Vector3(tip.x, w + 3.6f, tip.y), 0.18f, 5.4f, MaterialId.MetalDark);
            Cyl(f, "Yeşil Işık", new Vector3(tip.x, w + 6.5f, tip.y), 0.4f, 0.6f, MaterialId.Green, false);
            c.AddBounds(new Bounds(new Vector3((start.x + tip.x) * 0.5f, w, (start.y + tip.y) * 0.5f), new Vector3(10f, 4f, length)));
            c.AddLoot(new Vector3(tip.x, w + 1.7f, tip.y), LootTier.High);
            var m = start + dir * (length * 0.5f);
            c.AddLoot(new Vector3(m.x, w + 1.7f, m.y), LootTier.High);
        }

        private static void BuildLighthouseExtras(Ctx c, LocationSpec loc)
        {
            var f = c.Folder;
            var p = loc.Center + new Vector2(-14f, -8f);
            Place(c, f, "Fener Bekçisi Evi", BuildingStyle.VillageHouse, c.GroundV(p), 15f, 8f, 6f, 1, LootTier.High, false);
            for (var i = 0; i < 6; i++)
                GroundBox(c, f, "FenerDuvarı", loc.Center + Dir(200f + i * 20f) * 16f, new Vector3(4.2f, 1f, 0.6f), 200f + i * 20f + 90f, MaterialId.Stone);
            Cyl(f, "Sis Düdüğü", c.Ground(loc.Center.x + 14f, loc.Center.y - 6f) + Vector3.up * 1.1f, 0.6f, 2.2f, MaterialId.Red);
            c.AddLoot(c.GroundV(p) + new Vector3(2f, 1f, 1f), LootTier.High);
        }

        // ================================================================== KARTAL YAYLASI

        private static void BuildObsidian(Ctx c, LocationSpec loc)
        {
            var f = c.Folder;
            var placed = new List<Vector2>();
            // Dış halka: yüksek obsidyen kuleler; iç: alçak parçalar. Aralık >= 9 m (koridorlar >= 4 m).
            for (var ring = 0; ring < 2; ring++)
            {
                var count = ring == 0 ? 9 : 5;
                var radius = ring == 0 ? 33f : 17f;
                for (var i = 0; i < count; i++)
                {
                    var a = i * 360f / count + ring * 20f + c.R(-8f, 8f);
                    var p = loc.Center + Dir(a) * (radius + c.R(-3f, 3f));
                    var ok = true;
                    for (var q = 0; q < placed.Count && ok; q++)
                        ok = (placed[q] - p).sqrMagnitude > 81f;
                    if (!ok)
                        continue;
                    placed.Add(p);
                    var g = c.GroundV(p);
                    var tall = ring == 0 ? c.R(5f, 9f) : c.R(2.5f, 4.5f);
                    var wd = c.R(1.8f, 3.2f);
                    RockFactory.CreateRock(f, g + Vector3.up * (tall * 0.3f), Quaternion.Euler(c.R(-6f, 6f), c.R(0f, 360f), c.R(-6f, 6f)),
                        new Vector3(wd, tall, wd), ring * 7 + i, true).isStatic = true;
                    // Parlak siyah kristal dikenler.
                    for (var s = 0; s < 3; s++)
                    {
                        var o = p + Dir(c.R(0f, 360f)) * c.R(1f, 2.2f);
                        var sp = StructureKit.CreateBox(f, "ObsidyenKristal", c.GroundV(o) + Vector3.up * 1.2f,
                            new Vector3(0.5f, c.R(2f, 3.4f), 0.5f), Quaternion.Euler(c.R(-14f, 14f), c.R(0f, 180f), c.R(-14f, 14f)),
                            MaterialId.Black);
                        sp.isStatic = true;
                    }
                }
            }

            Place(c, f, "Maden Kulübesi", BuildingStyle.Shed, c.GroundV(loc.Center), 30f, 6f, 5f, 1, LootTier.High, false);
            PropFactory.AmmoCrate(f, c.Ground(loc.Center.x + 4f, loc.Center.y + 1f), 0f, c.Rng);
            PropFactory.Barrel(f, c.Ground(loc.Center.x - 4f, loc.Center.y + 2f), 0f, c.Rng);
            c.AddLoot(c.Ground(loc.Center.x + 4f, loc.Center.y + 1f) + Vector3.up, LootTier.Military);
            c.AddLoot(c.Ground(loc.Center.x - 12f, loc.Center.y - 9f) + Vector3.up, LootTier.High);
        }

        private static void BuildPasture(Ctx c, LocationSpec loc)
        {
            var f = c.Folder;
            var yawBase = c.R(0f, 360f);
            Place(c, f, "Çoban Evi", BuildingStyle.ShepherdHut, c.GroundV(loc.Center + Dir(yawBase) * 9f), yawBase, 5f, 5f, 1, LootTier.Low, false);
            Place(c, f, "Mera Ahırı", BuildingStyle.Barn, c.GroundV(loc.Center + Dir(yawBase + 140f) * 14f), yawBase + 140f, 12f, 8f, 1,
                LootTier.Medium, false);
            Place(c, f, "Yazlık Kulübe", BuildingStyle.ShepherdHut, c.GroundV(loc.Center + Dir(yawBase + 250f) * 12f), yawBase + 250f, 4f, 4f, 1,
                LootTier.Low, false);

            // Koyun ağılı: 12 parçalı çit halkası (kapı boşluğu) ve içinde sürü.
            var pen = loc.Center + Dir(yawBase + 60f) * 24f;
            for (var i = 1; i < 13; i++)
            {
                var a = i * 30f;
                GroundBox(c, f, "AğılÇiti", pen + Dir(a) * 7f, new Vector3(3.8f, 1f, 0.12f), a + 90f, MaterialId.WoodDark);
            }

            for (var i = 0; i < 9; i++)
            {
                var s = pen + Dir(c.R(0f, 360f)) * c.R(0.5f, 5.5f);
                var g = c.GroundV(s);
                var ya = c.R(0f, 360f);
                Box(c, f, "Koyun", g + Vector3.up * 0.55f, new Vector3(0.7f, 0.6f, 1.1f), ya, MaterialId.White, false);
                var hd = Dir(90f - ya) * 0.65f;
                Box(c, f, "KoyunBaş", g + new Vector3(hd.x, 0.65f, hd.y), new Vector3(0.28f, 0.28f, 0.34f), ya, MaterialId.Black, false);
            }

            for (var i = 0; i < 3; i++)
                PropFactory.HayBale(f, c.Ground(loc.Center.x - 6f + i * 2.5f, loc.Center.y - 6f), c.R(0f, 90f), c.Rng);
            GroundBox(c, f, "Suluk", loc.Center + Dir(yawBase + 200f) * 5f, new Vector3(3f, 0.7f, 0.8f), yawBase, MaterialId.Wood);
            c.AddLoot(c.GroundV(loc.Center) + Vector3.up, loc.Tier);
        }

        private static void BuildLiftStation(Ctx c, LocationSpec loc, bool lower)
        {
            var f = c.Folder;
            var a = c.Layout.Locations.Find(l => l.Name == MapLayout.KartalTeleAltName);
            var b = c.Layout.Locations.Find(l => l.Name == MapLayout.KartalTeleUstName);
            if (a == null || b == null)
                return;
            var dir = (b.Center - a.Center).normalized;
            var yaw = YawOfDir(dir);
            var center = c.GroundV(loc.Center);
            Place(c, f, lower ? "Alt İstasyon" : "Üst İstasyon", BuildingStyle.Shed, center, yaw, 9f, 6f, 1, LootTier.Medium, false);
            // Tahrik tekeri (dik eksen) + korkuluk.
            var wheel = Cyl(f, "TahrikTeker", center + new Vector3(dir.x * 6f, 2.2f, dir.y * 6f * (lower ? -1f : 1f)), 1.6f, 0.3f, MaterialId.MetalDark, false);
            wheel.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            c.AddLoot(center + new Vector3(0f, 0.9f, 1.5f), LootTier.Medium);

            if (!lower)
                return;

            // Hat direkleri (yalnızca alt istasyon bir kez üretir): alt -> üst, 38 m aralık, kablo + sandalyeler.
            var pts = LinePoints(a.Center + dir * 20f, b.Center - dir * 20f, 38f);
            var tops = new List<Vector3>(pts.Count);
            var side = new Vector2(-dir.y, dir.x);
            for (var i = 0; i < pts.Count; i++)
            {
                var g = c.GroundV(pts[i]);
                var h = 11f;
                Cyl(f, "HatDireği", g + new Vector3(side.x * 0.9f, h * 0.5f, side.y * 0.9f), 0.2f, h, MaterialId.MetalDark);
                Cyl(f, "HatDireği", g + new Vector3(-side.x * 0.9f, h * 0.5f, -side.y * 0.9f), 0.2f, h, MaterialId.MetalDark);
                Box(c, f, "HatKolu", g + Vector3.up * h, new Vector3(4.2f, 0.25f, 0.35f), yaw + 90f, MaterialId.MetalDark, false);
                tops.Add(g + Vector3.up * (h + 0.3f));
            }

            for (var i = 0; i + 1 < tops.Count; i++)
            {
                var from = tops[i];
                var to = tops[i + 1];
                var span = to - from;
                for (var s = -1; s <= 1; s += 2)
                {
                    var off = new Vector3(side.x, 0f, side.y) * (2.1f * s);
                    var mid = (from + to) * 0.5f + off - Vector3.up * 0.4f;
                    var cable = StructureKit.CreateBox(f, "Kablo", mid, new Vector3(0.05f, 0.05f, span.magnitude),
                        Quaternion.LookRotation(span.normalized, Vector3.up), MaterialId.MetalDark, false);
                    cable.isStatic = true;
                }

                // Sandalyeler: aralık başına bir tane, kabloda asılı.
                var p = Vector3.Lerp(from, to, 0.5f) + new Vector3(side.x, 0f, side.y) * 2.1f;
                var chair = StructureKit.CreateBox(f, "Sandalye", p - Vector3.up * 2.4f, new Vector3(1.4f, 0.1f, 0.6f),
                    Quaternion.Euler(0f, yaw, 0f), MaterialId.Red, false);
                chair.isStatic = true;
                StructureKit.CreateBox(f, "SandalyeSırt", p - Vector3.up * 2f, new Vector3(1.4f, 0.7f, 0.08f), Quaternion.Euler(0f, yaw, 0f),
                    MaterialId.Red, false).isStatic = true;
                StructureKit.CreateBox(f, "SandalyeAskı", p - Vector3.up * 1.2f, new Vector3(0.06f, 2.4f, 0.06f), Quaternion.identity,
                    MaterialId.MetalDark, false).isStatic = true;
            }
        }

        private static void BuildFestival(Ctx c, LocationSpec loc)
        {
            var f = c.Folder;
            var center = c.GroundV(loc.Center);
            var toMap = (-loc.Center).normalized;
            var yaw = YawOfDir(toMap);

            // Sahne: kalın tahta zemin + arka perde + bayrak.
            var sp = loc.Center + toMap * 10f;
            var sg = c.GroundV(sp);
            Box(c, f, "Sahne", sg + Vector3.up * 0.4f, new Vector3(10f, 0.8f, 7f), yaw, MaterialId.Wood);
            Box(c, f, "SahneArka", sg + new Vector3(toMap.x * 3.2f, 2.6f, toMap.y * 3.2f), new Vector3(10f, 3.6f, 0.2f), yaw, MaterialId.WoodDark);
            Box(c, f, "Pankart", sg + new Vector3(toMap.x * 3.0f, 3.2f, toMap.y * 3.0f), new Vector3(6f, 2f, 0.05f), yaw, MaterialId.TurkishFlag, false);
            c.AddBounds(new Bounds(sg + Vector3.up * 2f, new Vector3(11f, 5f, 8f)));

            // Ortada şenlik ateşi (taş halka, çarpışmasız alev).
            for (var i = 0; i < 8; i++)
                Box(c, f, "AteşTaşı", center + new Vector3(Dir(i * 45f).x * 1.6f, 0.25f, Dir(i * 45f).y * 1.6f), new Vector3(0.6f, 0.5f, 0.6f),
                    i * 45f, MaterialId.StoneDark, false);
            Cyl(f, "Şenlik Ateşi", center + Vector3.up * 0.4f, 0.7f, 0.6f, MaterialId.Fire, false);

            // Bayraklı direkler halkası ve renkli flamalar.
            var flag = new[] { MaterialId.Red, MaterialId.Yellow, MaterialId.Blue, MaterialId.Orange };
            for (var i = 0; i < 10; i++)
            {
                var g = c.GroundV(loc.Center + Dir(i * 36f) * (loc.Radius - 6f));
                Pole(f, g, 5f, MaterialId.Wood);
                Box(c, f, "Flama", g + new Vector3(0.45f, 4.6f, 0f), new Vector3(0.9f, 0.5f, 0.04f), i * 36f, flag[i % flag.Length], false);
            }

            // Uzun masalar + banklar (aralarında >= 3 m).
            for (var t = 0; t < 4; t++)
            {
                var p = loc.Center - toMap * 8f + new Vector2(-toMap.y, toMap.x) * (-9f + t * 6f);
                GroundBox(c, f, "Masa", p, new Vector3(1f, 0.9f, 5f), yaw, MaterialId.Wood);
                GroundBox(c, f, "Bank", p + new Vector2(-toMap.y, toMap.x) * 1.1f, new Vector3(0.4f, 0.5f, 5f), yaw, MaterialId.WoodDark);
            }

            // Çadırlar ve saman balyaları.
            for (var t = 0; t < 3; t++)
                PropFactory.Tent(f, c.GroundV(loc.Center + Dir(yaw + 120f + t * 40f) * 26f), yaw + 120f + t * 40f, c.Rng);
            for (var i = 0; i < 4; i++)
                PropFactory.HayBale(f, c.GroundV(loc.Center + Dir(c.R(0f, 360f)) * c.R(8f, 14f)), c.R(0f, 360f), c.Rng);

            c.AddLoot(sg + Vector3.up * 1.2f, LootTier.High);
            for (var i = 0; i < 4; i++)
                c.AddLoot(c.GroundV(loc.Center + Dir(i * 90f + 30f) * 11f) + Vector3.up * 0.9f, LootTier.Medium);
        }
    }
}
