using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Ayaz Geçidi ve Mavi Liman POI detay geçişi: konteyner dizilimleri, vinç, balıkçı teknesi iskeleti, kar birikintileri,
    /// devrilmiş araç bariyerleri ve çatışma izleri (mermi deliği + yanık leke). Prosedürel (StructureKit/PropFactory);
    /// kalite kademesine göre yoğunluk ölçeklenir. Diğer haritalarda hiçbir şey yapmaz.
    /// // ENTEGRASYON: hazır asset gelince PoiDetailProps.ContentOverrides ile (ad -> prefab) kalemler değiştirilebilir.
    /// </summary>
    public static class PoiDetailProps
    {
        /// <summary>ContentOverrides kancası: anahtar (ör. "Vinc", "TekneIskeleti", "KarBirikintisi") -> hazır prefab.</summary>
        public static readonly Dictionary<string, GameObject> ContentOverrides = new Dictionary<string, GameObject>();

        public static void BuildIfApplicable(MapLayout layout, Terrain terrain, Transform parent, int seed,
            List<LootSpawnPointData> lootOut, List<Bounds> structuresOut)
        {
            if (layout == null || terrain == null)
                return;
            var ayaz = layout.Name == MapCatalog.AyazGecidiName;
            var liman = layout.Name == MapCatalog.MaviLimanName;
            if (!ayaz && !liman)
                return;

            var tier = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, 3);
            var root = new GameObject(ayaz ? "[Ayaz POI Detay]" : "[Liman POI Detay]");
            if (parent != null)
                root.transform.SetParent(parent, false);
            var rng = new System.Random(seed ^ 0x51D7);

            for (var i = 0; i < layout.Locations.Count; i++)
            {
                var loc = layout.Locations[i];
                if (loc == null)
                    continue;
                try
                {
                    if (ayaz)
                    {
                        if (loc.Name == MapLayout.AyazSiperName) BuildTrenchDetail(loc, terrain, root.transform, rng, tier, structuresOut);
                        else if (loc.Name == MapLayout.AyazKayakKalintiName) BuildSkiDetail(loc, terrain, root.transform, rng, tier, structuresOut);
                        else if (loc.Name == MapLayout.AyazBarinakName || loc.Name == MapLayout.AyazTipiName)
                            BuildSnowDetail(loc, terrain, root.transform, rng, tier, structuresOut);
                    }
                    else
                    {
                        if (loc.Name == MapLayout.LimanKonteynerName) BuildContainerYard(loc, terrain, root.transform, rng, tier, lootOut, structuresOut);
                        else if (loc.Name == MapLayout.LimanBalikHaliName) BuildFishingDetail(loc, terrain, root.transform, rng, tier, structuresOut);
                        else if (loc.Name == MapLayout.LimanDalgakiranName) BuildBreakwaterDetail(loc, terrain, root.transform, rng, tier);
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning("[PoiDetailProps] " + loc.Name + " üretilemedi: " + e.Message);
                }
            }
        }

        private static Vector3 Ground(Terrain terrain, Vector2 p)
            => new Vector3(p.x, terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y, p.y);

        // ------------------------------------------------------------------ Liman

        private static void BuildContainerYard(LocationSpec loc, Terrain terrain, Transform parent, System.Random rng, int tier,
            List<LootSpawnPointData> lootOut, List<Bounds> structuresOut)
        {
            var folder = new GameObject("Konteyner Dizilimi");
            folder.transform.SetParent(parent, false);
            var yaw = rng.Next(0, 4) * 90f;
            var rows = Mathf.Max(2, PoiDetailPlan.Scaled(4, tier));
            var slots = PoiDetailPlan.ContainerGrid(loc.Center, yaw, rows, 3, rng.Next());
            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if ((s.Pos - loc.Center).magnitude > loc.Radius * 0.9f)
                    continue;
                var g = Ground(terrain, s.Pos);
                PropFactory.Container(folder.transform, g, s.Yaw, rng);
                if (s.Level > 0)
                    PropFactory.Container(folder.transform, g + Vector3.up * 2.6f, s.Yaw, rng);
                structuresOut?.Add(new Bounds(g + Vector3.up * (1.3f + s.Level * 1.3f), new Vector3(2.6f, 2.6f * (1 + s.Level), 6.2f)));
            }

            BuildGantryCrane(folder.transform, Ground(terrain, loc.Center + new Vector2(loc.Radius * 0.7f, 0f)), yaw, structuresOut);
            BuildBattleMarks(folder.transform, terrain, loc.Center, loc.Radius * 0.7f, rng, tier, PoiDetailPlan.Scaled(6, tier));
            BuildBarrier(folder.transform, terrain, loc.Center + new Vector2(-loc.Radius * 0.6f, loc.Radius * 0.2f), yaw + 90f, rng);
            BuildPalletStacks(folder.transform, terrain, loc.Center + new Vector2(-loc.Radius * 0.2f, -loc.Radius * 0.55f), rng, tier, structuresOut);

            var lp = Ground(terrain, loc.Center + new Vector2(2f, 2f)) + Vector3.up * 0.8f;
            lootOut?.Add(new LootSpawnPointData(lp, LootTier.High));
        }

        /// <summary>Portal vinç: dört ayak + üst kiriş + kabin + çıkıntılı kol.</summary>
        private static void BuildGantryCrane(Transform parent, Vector3 pos, float yaw, List<Bounds> structuresOut)
        {
            if (ContentOverrides.TryGetValue("Vinc", out var prefab) && prefab != null)
            {
                UnityEngine.Object.Instantiate(prefab, pos, Quaternion.Euler(0f, yaw, 0f), parent);
                return;
            }

            var root = StructureKit.CreateGroup(parent, "Liman Vinci", pos, Quaternion.Euler(0f, yaw, 0f));
            const float h = 14f;
            foreach (var sx in new[] { -5f, 5f })
                foreach (var sz in new[] { -4f, 4f })
                    StructureKit.CreateBox(root.transform, "Ayak", new Vector3(sx, h * 0.5f, sz), new Vector3(0.6f, h, 0.6f), Quaternion.identity, MaterialId.Orange);
            StructureKit.CreateBox(root.transform, "KirişA", new Vector3(0f, h, -4f), new Vector3(11f, 0.9f, 0.9f), Quaternion.identity, MaterialId.Orange);
            StructureKit.CreateBox(root.transform, "KirişB", new Vector3(0f, h, 4f), new Vector3(11f, 0.9f, 0.9f), Quaternion.identity, MaterialId.Orange);
            StructureKit.CreateBox(root.transform, "Kol", new Vector3(0f, h + 0.6f, 0f), new Vector3(1.2f, 0.9f, 22f), Quaternion.identity, MaterialId.MetalDark);
            StructureKit.CreateBox(root.transform, "Kabin", new Vector3(0f, h - 1.4f, 3f), new Vector3(2f, 2f, 2f), Quaternion.identity, MaterialId.MetalPanel);
            StructureKit.MarkStatic(root);
            structuresOut?.Add(new Bounds(pos + Vector3.up * h * 0.5f, new Vector3(11f, h, 22f)));
        }

        private static void BuildFishingDetail(LocationSpec loc, Terrain terrain, Transform parent, System.Random rng, int tier, List<Bounds> structuresOut)
        {
            var folder = new GameObject("Balık Hali Detay");
            folder.transform.SetParent(parent, false);
            var p = Ground(terrain, loc.Center + new Vector2(loc.Radius * 0.55f, -loc.Radius * 0.3f));
            BuildBoatSkeleton(folder.transform, p, rng.Next(0, 360), structuresOut);
            BuildRopeCoils(folder.transform, terrain, loc.Center + new Vector2(-loc.Radius * 0.35f, loc.Radius * 0.3f), rng, tier);
            BuildBattleMarks(folder.transform, terrain, loc.Center, loc.Radius * 0.6f, rng, tier, PoiDetailPlan.Scaled(4, tier));
        }

        /// <summary>Karaya oturmuş balıkçı teknesi iskeleti: omurga + kaburgalar + yırtık borda.</summary>
        private static void BuildBoatSkeleton(Transform parent, Vector3 pos, float yaw, List<Bounds> structuresOut)
        {
            if (ContentOverrides.TryGetValue("TekneIskeleti", out var prefab) && prefab != null)
            {
                UnityEngine.Object.Instantiate(prefab, pos, Quaternion.Euler(0f, yaw, 0f), parent);
                return;
            }

            var root = StructureKit.CreateGroup(parent, "Tekne İskeleti", pos, Quaternion.Euler(0f, yaw, 0f));
            StructureKit.CreateBox(root.transform, "Omurga", new Vector3(0f, 0.35f, 0f), new Vector3(0.3f, 0.3f, 9f), Quaternion.identity, MaterialId.WoodDark);
            for (var i = 0; i < 9; i++)
            {
                var z = -3.6f + i * 0.9f;
                var w = 1.6f * Mathf.Sin((i + 0.5f) / 9f * Mathf.PI) + 0.3f;
                StructureKit.CreateBox(root.transform, "KaburgaL" + i, new Vector3(-w * 0.5f, 0.9f, z), new Vector3(0.12f, 1.2f, 0.14f), Quaternion.Euler(0f, 0f, -22f), MaterialId.Wood, false);
                StructureKit.CreateBox(root.transform, "KaburgaR" + i, new Vector3(w * 0.5f, 0.9f, z), new Vector3(0.12f, 1.2f, 0.14f), Quaternion.Euler(0f, 0f, 22f), MaterialId.Wood, false);
            }

            // Sağ bordadan kalan birkaç tahta.
            StructureKit.CreateBox(root.transform, "Tahta1", new Vector3(0.9f, 0.7f, -1.5f), new Vector3(0.08f, 0.35f, 3f), Quaternion.Euler(0f, 0f, 22f), MaterialId.WoodDark, false);
            StructureKit.CreateBox(root.transform, "Tahta2", new Vector3(0.95f, 1.1f, 1.2f), new Vector3(0.08f, 0.35f, 2.4f), Quaternion.Euler(0f, 0f, 22f), MaterialId.WoodDark, false);
            StructureKit.MarkStatic(root);
            structuresOut?.Add(new Bounds(pos + Vector3.up * 0.8f, new Vector3(3.2f, 1.6f, 9f)));
        }

        private static void BuildBreakwaterDetail(LocationSpec loc, Terrain terrain, Transform parent, System.Random rng, int tier)
        {
            var folder = new GameObject("Dalgakıran Detay");
            folder.transform.SetParent(parent, false);
            BuildBattleMarks(folder.transform, terrain, loc.Center, loc.Radius * 0.8f, rng, tier, PoiDetailPlan.Scaled(3, tier));
            BuildBarrier(folder.transform, terrain, loc.Center + new Vector2(loc.Radius * 0.3f, 0f), rng.Next(0, 360), rng);
        }

        // ------------------------------------------------------------------ Ayaz

        private static void BuildTrenchDetail(LocationSpec loc, Terrain terrain, Transform parent, System.Random rng, int tier, List<Bounds> structuresOut)
        {
            var folder = new GameObject("Siper Hattı Detay");
            folder.transform.SetParent(parent, false);
            BuildSnowDrifts(folder.transform, terrain, loc.Center, loc.Radius, rng, tier, PoiDetailPlan.Scaled(10, tier));
            BuildBattleMarks(folder.transform, terrain, loc.Center, loc.Radius * 0.7f, rng, tier, PoiDetailPlan.Scaled(8, tier));
            BuildBarrier(folder.transform, terrain, loc.Center + new Vector2(loc.Radius * 0.4f, loc.Radius * 0.1f), rng.Next(0, 360), rng);
            BuildBarrier(folder.transform, terrain, loc.Center + new Vector2(-loc.Radius * 0.5f, -loc.Radius * 0.2f), rng.Next(0, 360), rng);
        }

        private static void BuildSkiDetail(LocationSpec loc, Terrain terrain, Transform parent, System.Random rng, int tier, List<Bounds> structuresOut)
        {
            var folder = new GameObject("Kayak Kalıntısı Detay");
            folder.transform.SetParent(parent, false);
            BuildSnowDrifts(folder.transform, terrain, loc.Center, loc.Radius, rng, tier, PoiDetailPlan.Scaled(8, tier));
            BuildBattleMarks(folder.transform, terrain, loc.Center, loc.Radius * 0.6f, rng, tier, PoiDetailPlan.Scaled(5, tier));
            BuildBarrier(folder.transform, terrain, loc.Center + new Vector2(0f, -loc.Radius * 0.4f), rng.Next(0, 360), rng);
            BuildBuriedFence(folder.transform, terrain, loc.Center + new Vector2(loc.Radius * 0.35f, loc.Radius * 0.35f), rng.Next(0, 360), rng, structuresOut);
        }

        private static void BuildSnowDetail(LocationSpec loc, Terrain terrain, Transform parent, System.Random rng, int tier, List<Bounds> structuresOut)
        {
            var folder = new GameObject("Kar Detay");
            folder.transform.SetParent(parent, false);
            BuildSnowDrifts(folder.transform, terrain, loc.Center, loc.Radius, rng, tier, PoiDetailPlan.Scaled(7, tier));
            BuildBattleMarks(folder.transform, terrain, loc.Center, loc.Radius * 0.5f, rng, tier, PoiDetailPlan.Scaled(3, tier));
            BuildAbandonedHut(folder.transform, terrain, loc.Center + new Vector2(-loc.Radius * 0.5f, loc.Radius * 0.45f), rng.Next(0, 360), structuresOut);
        }

        /// <summary>Kar birikintileri: yassı beyaz silindirler (çarpışmasız, süs).</summary>
        private static void BuildSnowDrifts(Transform parent, Terrain terrain, Vector2 center, float radius, System.Random rng, int tier, int count)
        {
            var slots = PoiDetailPlan.ScatterInDisc(center, radius * 0.9f, count, rng.Next(), 3.5f, 1.2f, 3.2f);
            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                var g = Ground(terrain, s.Pos);
                var go = StructureKit.CreateCylinder(parent, "KarBirikintisi", g + Vector3.up * 0.1f, s.Scale, 0.45f, MaterialId.Snow, false);
                go.transform.rotation = Quaternion.Euler(0f, s.Yaw, 0f);
                go.transform.localScale = new Vector3(s.Scale * 2f, 0.45f, s.Scale * 1.3f);
                StructureKit.MarkStatic(go);
            }
        }

        // ------------------------------------------------------------------ Yeni detay kümeleri

        /// <summary>Liman: yük paletleri (ahşap taban + koli yığını), 2-3 yığın.</summary>
        private static void BuildPalletStacks(Transform parent, Terrain terrain, Vector2 center, System.Random rng, int tier, List<Bounds> structuresOut)
        {
            var count = Mathf.Max(2, PoiDetailPlan.Scaled(3, tier));
            var slots = PoiDetailPlan.ScatterInDisc(center, 9f, count, rng.Next(), 3.2f, 1f, 1f);
            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                var g = Ground(terrain, s.Pos);
                if (ContentOverrides.TryGetValue("YukPaleti", out var prefab) && prefab != null)
                {
                    UnityEngine.Object.Instantiate(prefab, g, Quaternion.Euler(0f, s.Yaw, 0f), parent);
                    continue;
                }

                var root = StructureKit.CreateGroup(parent, "Yük Paleti", g, Quaternion.Euler(0f, s.Yaw, 0f));
                StructureKit.CreateBox(root.transform, "Palet", new Vector3(0f, 0.08f, 0f), new Vector3(1.2f, 0.16f, 1.0f), Quaternion.identity, MaterialId.WoodDark);
                var layers = 1 + rng.Next(0, 3);
                for (var l = 0; l < layers; l++)
                {
                    var jitter = (float)(rng.NextDouble() - 0.5) * 0.1f;
                    StructureKit.CreateBox(root.transform, "Koli" + l, new Vector3(jitter, 0.16f + 0.4f + l * 0.8f, 0f), new Vector3(1.05f, 0.8f, 0.85f),
                        Quaternion.Euler(0f, jitter * 40f, 0f), (l + i) % 2 == 0 ? MaterialId.Wood : MaterialId.Hesco);
                }
                StructureKit.MarkStatic(root);
                structuresOut?.Add(new Bounds(g + Vector3.up * (0.2f + layers * 0.4f), new Vector3(1.2f, 0.4f + layers * 0.8f, 1.0f)));
            }
        }

        /// <summary>Liman: halat bobinleri (yatık kalın disk + iç göbek), iskele/balık hali kenarı.</summary>
        private static void BuildRopeCoils(Transform parent, Terrain terrain, Vector2 center, System.Random rng, int tier)
        {
            var count = Mathf.Max(2, PoiDetailPlan.Scaled(4, tier));
            var slots = PoiDetailPlan.ScatterInDisc(center, 7f, count, rng.Next(), 1.8f, 0.7f, 1.1f);
            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                var g = Ground(terrain, s.Pos);
                if (ContentOverrides.TryGetValue("HalatBobini", out var prefab) && prefab != null)
                {
                    UnityEngine.Object.Instantiate(prefab, g, Quaternion.Euler(0f, s.Yaw, 0f), parent);
                    continue;
                }

                var r = 0.55f * s.Scale;
                var coil = StructureKit.CreateCylinder(parent, "HalatBobini", g + Vector3.up * 0.12f, r, 0.24f, MaterialId.Hay, false);
                var hub = StructureKit.CreateCylinder(parent, "HalatGobek", g + Vector3.up * 0.14f, r * 0.4f, 0.26f, MaterialId.WoodDark, false);
                StructureKit.MarkStatic(coil);
                StructureKit.MarkStatic(hub);
            }
        }

        /// <summary>Ayaz: kar altında kalmış çit; yarı gömülü direkler + eğri tel kuşağı + kar yığını.</summary>
        private static void BuildBuriedFence(Transform parent, Terrain terrain, Vector2 pos, float yaw, System.Random rng, List<Bounds> structuresOut)
        {
            var g = Ground(terrain, pos);
            var root = StructureKit.CreateGroup(parent, "Karlı Çit", g, Quaternion.Euler(0f, yaw, 0f));
            const int posts = 6;
            for (var i = 0; i < posts; i++)
            {
                var x = (i - (posts - 1) * 0.5f) * 2.2f;
                var tilt = (float)(rng.NextDouble() - 0.5) * 18f;
                var h = 0.6f + (float)rng.NextDouble() * 0.5f;
                StructureKit.CreateBox(root.transform, "Direk" + i, new Vector3(x, h * 0.5f - 0.1f, 0f), new Vector3(0.12f, h, 0.12f),
                    Quaternion.Euler(0f, 0f, tilt), MaterialId.WoodDark, false);
            }
            StructureKit.CreateBox(root.transform, "Tel", new Vector3(0f, 0.55f, 0f), new Vector3(11f, 0.03f, 0.03f), Quaternion.Euler(0f, 0f, 3f), MaterialId.MetalDark, false);
            var drift = StructureKit.CreateCylinder(root.transform, "KarSeddi", new Vector3(0f, 0.15f, 0.2f), 1f, 0.5f, MaterialId.Snow, false);
            drift.transform.localScale = new Vector3(12f, 0.5f, 1.8f);
            StructureKit.MarkStatic(root);
            structuresOut?.Add(new Bounds(g + Vector3.up * 0.5f, new Vector3(11f, 1f, 1f)));
        }

        /// <summary>Ayaz: terk edilmiş kulübe; dört duvar (kapı boşluklu), çökmüş çatı, çatıda kar.</summary>
        private static void BuildAbandonedHut(Transform parent, Terrain terrain, Vector2 pos, float yaw, List<Bounds> structuresOut)
        {
            var g = Ground(terrain, pos);
            if (ContentOverrides.TryGetValue("TerkKulube", out var prefab) && prefab != null)
            {
                UnityEngine.Object.Instantiate(prefab, g, Quaternion.Euler(0f, yaw, 0f), parent);
                return;
            }

            var root = StructureKit.CreateGroup(parent, "Terk Kulübe", g, Quaternion.Euler(0f, yaw, 0f));
            const float w = 4f, d = 3.2f, h = 2.3f, t = 0.2f;
            StructureKit.CreateBox(root.transform, "Zemin", new Vector3(0f, 0.05f, 0f), new Vector3(w, 0.1f, d), Quaternion.identity, MaterialId.WoodDark);
            StructureKit.CreateBox(root.transform, "Arka", new Vector3(0f, h * 0.5f, d * 0.5f), new Vector3(w, h, t), Quaternion.identity, MaterialId.Wood);
            StructureKit.CreateBox(root.transform, "Sol", new Vector3(-w * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), Quaternion.identity, MaterialId.Wood);
            StructureKit.CreateBox(root.transform, "Sag", new Vector3(w * 0.5f, h * 0.5f - 0.3f, 0f), new Vector3(t, h - 0.6f, d), Quaternion.identity, MaterialId.WoodDark);
            // Ön duvar: kapı boşluğu bırakacak şekilde iki parça.
            StructureKit.CreateBox(root.transform, "OnA", new Vector3(-w * 0.5f + 0.6f, h * 0.5f, -d * 0.5f), new Vector3(1.2f, h, t), Quaternion.identity, MaterialId.Wood);
            StructureKit.CreateBox(root.transform, "OnB", new Vector3(w * 0.5f - 0.9f, h * 0.5f, -d * 0.5f), new Vector3(1.8f, h, t), Quaternion.identity, MaterialId.Wood);
            StructureKit.CreateBox(root.transform, "OnUst", new Vector3(0f, h - 0.3f, -d * 0.5f), new Vector3(w, 0.6f, t), Quaternion.identity, MaterialId.Wood);
            // Çökmüş çatı: yarısı eğik, yarısı yok; üstünde kar.
            StructureKit.CreateBox(root.transform, "Cati", new Vector3(-0.5f, h + 0.25f, 0.4f), new Vector3(w * 0.7f, 0.12f, d * 0.8f), Quaternion.Euler(8f, 0f, -14f), MaterialId.WoodDark);
            StructureKit.CreateBox(root.transform, "CatiKar", new Vector3(-0.5f, h + 0.38f, 0.4f), new Vector3(w * 0.7f, 0.1f, d * 0.8f), Quaternion.Euler(8f, 0f, -14f), MaterialId.Snow, false);
            var drift = StructureKit.CreateCylinder(root.transform, "KapiKari", new Vector3(0f, 0.1f, -d * 0.5f - 0.6f), 1f, 0.3f, MaterialId.Snow, false);
            drift.transform.localScale = new Vector3(3.2f, 0.3f, 1.6f);
            StructureKit.MarkStatic(root);
            structuresOut?.Add(new Bounds(g + Vector3.up * h * 0.5f, new Vector3(w, h, d)));
        }

        // ------------------------------------------------------------------ Ortak

        /// <summary>Devrilmiş araç bariyeri: yan yatmış enkaz + iki yanında Hesco/çuval.</summary>
        private static void BuildBarrier(Transform parent, Terrain terrain, Vector2 pos, float yaw, System.Random rng)
        {
            var g = Ground(terrain, pos);
            var wreck = PropFactory.Wreck(parent, g, yaw, rng);
            if (wreck != null)
                wreck.transform.rotation = Quaternion.Euler(0f, yaw, rng.NextDouble() > 0.5 ? 78f : -78f);
            var rad = yaw * Mathf.Deg2Rad;
            var along = new Vector2(Mathf.Cos(rad), -Mathf.Sin(rad));
            for (var i = -1; i <= 1; i += 2)
            {
                var p = Ground(terrain, pos + along * (i * 3.2f));
                var bag = StructureKit.CreateBox(parent, "Çuval", p + Vector3.up * 0.4f, new Vector3(1.6f, 0.8f, 0.7f), Quaternion.Euler(0f, yaw + 90f, 0f), MaterialId.Sandbag);
                StructureKit.MarkStatic(bag);
            }
        }

        /// <summary>Çatışma izleri: yanık lekeler (yassı siyah disk) + yakın mermi delikleri (küçük koyu kutular, yere serpili).</summary>
        private static void BuildBattleMarks(Transform parent, Terrain terrain, Vector2 center, float radius, System.Random rng, int tier, int count)
        {
            var burns = PoiDetailPlan.ScatterInDisc(center, radius, Mathf.Max(1, count / 2), rng.Next(), 4f, 0.8f, 2.2f);
            for (var i = 0; i < burns.Count; i++)
            {
                var g = Ground(terrain, burns[i].Pos);
                var b = StructureKit.CreateCylinder(parent, "YanıkLeke", g + Vector3.up * 0.03f, burns[i].Scale, 0.04f, MaterialId.Black, false);
                StructureKit.MarkStatic(b);
            }

            // Kalite düşükse mermi deliği decal'leri atlanır (çok sayıda küçük nesne).
            if (tier < 1)
                return;
            var holes = PoiDetailPlan.ScatterInDisc(center, radius, count * 3, rng.Next(), 0.6f, 0.6f, 1.2f);
            for (var i = 0; i < holes.Count; i++)
            {
                var g = Ground(terrain, holes[i].Pos);
                var h = StructureKit.CreateBox(parent, "MermiDeliği", g + Vector3.up * 0.02f, new Vector3(0.12f * holes[i].Scale, 0.02f, 0.12f * holes[i].Scale),
                    Quaternion.Euler(0f, holes[i].Yaw, 0f), MaterialId.BulletHole, false);
                StructureKit.MarkStatic(h);
            }
        }
    }
}
