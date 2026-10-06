using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Infrastructure;
using Project.Infrastructure.Content;
using Project.Infrastructure.Characters;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Project.Presentation.UI
{
    /// <summary>
    /// PUBG tarzı yakın lobi dioramasının parçaları. Yerel uzay: kamera (0, 1.6, 0), +Z ileri. Zemin 12 m'lik gerçek ThirdParty
    /// toprak malzemesi; asker z = 3.2 m'de, arkada kum torbası + sandık/varil, tepede kamuflaj şeritleri, geride Kirpi.
    /// Üretilen mesh'ler <see cref="MenuBackdropBuilder.Context.OwnedMeshes"/> listesine eklenir.
    /// </summary>
    internal static class MenuDioramaBuilder
    {
        // Referans kare: komutan ön merkezde göğüs-üstü, sağda kum torbası + ateş, solda Kirpi + metal depo duvarı.
        public static readonly Vector3 SoldierPosition = new Vector3(0.2f, 0f, 2.6f);
        public static readonly Vector3 CampfirePosition = new Vector3(3.1f, 0f, 5.2f);
        public static readonly Vector3 VehiclePosition = new Vector3(-3.9f, 0f, 9.6f);
        private const float VehicleYaw = 250f;

        private const float GroundMinX = -9f, GroundMaxX = 9f, GroundMinZ = -1.5f, GroundMaxZ = 14f;
        private const float WallZ = 13f;
        private const float SandbagZ = 7.6f;
        private const float WarehouseMaxX = -1.4f;

        // ------------------------------------------------------------------ Zemin

        public static void BuildGround(MenuBackdropBuilder.Context ctx)
        {
            var gravel = MenuBackdropBuilder.Mat(MaterialId.Gravel);
            if (gravel == null)
                gravel = MenuBackdropBuilder.Mat(MaterialId.Dirt);
            if (gravel == null)
                gravel = MaterialLibrary.Lit(new Color(0.25f, 0.2f, 0.15f), 0.1f, 0f);

            // Ana zemin: çakıl-taş, yakın ve net.
            var b = new MeshBuilder(1);
            b.AddFlatQuad(0, new Vector3(GroundMinX, 0f, GroundMinZ), new Vector3(GroundMinX, 0f, GroundMaxZ),
                new Vector3(GroundMaxX, 0f, GroundMaxZ), new Vector3(GroundMaxX, 0f, GroundMinZ), 0.5f);
            b.SanitizeNonFinite("HK_MenuDioramaGround");
            var mesh = ctx.Own(b.ToMesh("HK_MenuDioramaGround"));
            MenuBackdropBuilder.MeshObject("Zemin", ctx.Root, mesh, new[] { gravel }, Vector3.zero, Quaternion.identity, Vector3.one,
                ShadowCastingMode.Off);

            // Dış düz zemin: dağ eteğine kadar uzanır (ufukta boşluk kalmaz).
            var dirt = MenuBackdropBuilder.Mat(MaterialId.Dirt);
            if (dirt == null)
                dirt = gravel;
            var o = new MeshBuilder(1);
            o.AddFlatQuad(0, new Vector3(-420f, -0.03f, -40f), new Vector3(-420f, -0.03f, 150f), new Vector3(420f, -0.03f, 150f),
                new Vector3(420f, -0.03f, -40f), 0.12f);
            o.SanitizeNonFinite("HK_MenuDioramaOuter");
            MenuBackdropBuilder.MeshObject("DışZemin", ctx.Root, ctx.Own(o.ToMesh("HK_MenuDioramaOuter")), new[] { dirt }, Vector3.zero,
                Quaternion.identity, Vector3.one, ShadowCastingMode.Off);

            // Ateş çukuru çevresi: çamur.
            var mud = MenuBackdropBuilder.Mat(MaterialId.Mud);
            if (mud != null)
                MenuBackdropBuilder.MeshObject("AteşÇamuru", ctx.Root, MeshFactory.Disc(1.6f, 20), new[] { mud },
                    CampfirePosition + new Vector3(0f, 0.012f, 0f), Quaternion.identity, Vector3.one, ShadowCastingMode.Off);

            BuildTireRuts(ctx, mud != null ? mud : dirt);
            BuildSnowPatches(ctx);
            BuildGroundRocks(ctx);
            BuildSky(ctx);
            try
            {
                MenuBackdropBuilder.BuildMountains(ctx);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MenuBackdrop] Dağ silüetleri kurulamadı: " + e.Message);
            }
        }

        /// <summary>Kirpi'den kameraya doğru iki paralel, hafif kıvrımlı koyu tekerlek izi şeridi.</summary>
        private static void BuildTireRuts(MenuBackdropBuilder.Context ctx, Material material)
        {
            var b = new MeshBuilder(1);
            const int steps = 14;
            for (var lane = 0; lane < 2; lane++)
            {
                var laneOffset = lane == 0 ? -0.9f : 0.9f;
                for (var i = 0; i < steps; i++)
                {
                    var t0 = i / (float)steps;
                    var t1 = (i + 1) / (float)steps;
                    var p0 = RutPoint(t0, laneOffset);
                    var p1 = RutPoint(t1, laneOffset);
                    var side = Vector3.Cross(Vector3.up, (p1 - p0).normalized) * 0.17f;
                    b.AddFlatQuad(0, p0 - side, p1 - side, p1 + side, p0 + side, 1.2f);
                }
            }

            b.SanitizeNonFinite("HK_MenuRuts");
            MenuBackdropBuilder.MeshObject("Tekerlekİzleri", ctx.Root, ctx.Own(b.ToMesh("HK_MenuRuts")), new[] { material },
                new Vector3(0f, 0.018f, 0f), Quaternion.identity, Vector3.one, ShadowCastingMode.Off);
        }

        private static Vector3 RutPoint(float t, float laneOffset)
        {
            var z = Mathf.Lerp(10.2f, 0.6f, t);
            var x = Mathf.Lerp(-3.4f, -1.9f, t) + Mathf.Sin(t * 5.2f) * 0.5f + laneOffset;
            return new Vector3(x, 0f, z);
        }

        /// <summary>Çakıl üstünde dağınık kar yamaları.</summary>
        private static void BuildSnowPatches(MenuBackdropBuilder.Context ctx)
        {
            var snow = MenuBackdropBuilder.Mat(MaterialId.Snow);
            if (snow == null)
                return;
            var rng = new System.Random(4242);
            var disc = MeshFactory.Disc(1f, 10);
            var b = new MeshBuilder(1);
            for (var i = 0; i < 16; i++)
            {
                var x = Mathf.Lerp(-6.5f, 7.5f, (float)rng.NextDouble());
                var z = Mathf.Lerp(0.8f, 12f, (float)rng.NextDouble());
                if (Mathf.Abs(x - SoldierPosition.x) < 0.7f && z < SoldierPosition.z + 0.8f)
                    continue;   // Kahramanın ayak altı temiz.
                var sx = Mathf.Lerp(0.25f, 0.8f, (float)rng.NextDouble());
                var sz = sx * Mathf.Lerp(0.5f, 0.9f, (float)rng.NextDouble());
                var rot = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                b.Append(disc, Matrix4x4.TRS(new Vector3(x, 0.024f + i * 0.0006f, z), rot, new Vector3(sx, 1f, sz)), 0);
            }

            b.SanitizeNonFinite("HK_MenuSnow");
            MenuBackdropBuilder.MeshObject("KarYamaları", ctx.Root, ctx.Own(b.ToMesh("HK_MenuSnow")), new[] { snow }, Vector3.zero,
                Quaternion.identity, Vector3.one, ShadowCastingMode.Off);
        }

        /// <summary>Zemine gömülü taş yığınları (kameraya yakın hat ve ateş çevresi).</summary>
        private static void BuildGroundRocks(MenuBackdropBuilder.Context ctx)
        {
            var rock = MenuBackdropBuilder.Mat(MaterialId.Rock);
            if (rock == null)
                return;
            var rng = new System.Random(808);
            var sphere = MeshFactory.Sphere(0.5f, 8, 5);
            var b = new MeshBuilder(1);
            for (var i = 0; i < 40; i++)
            {
                var x = Mathf.Lerp(-6.5f, 7.5f, (float)rng.NextDouble());
                var z = Mathf.Lerp(0.8f, 11f, (float)rng.NextDouble());
                if (Mathf.Abs(x - SoldierPosition.x) < 0.6f && z < SoldierPosition.z + 0.6f)
                    continue;
                var s = Mathf.Lerp(0.12f, 0.34f, (float)rng.NextDouble());
                var rot = Quaternion.Euler((float)rng.NextDouble() * 20f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 20f);
                b.Append(sphere, Matrix4x4.TRS(new Vector3(x, s * 0.12f, z), rot, new Vector3(s * 1.4f, s * 0.7f, s)), 0);
            }

            // Ateş çevresi taş halkası.
            for (var i = 0; i < 9; i++)
            {
                var a = i / 9f * Mathf.PI * 2f;
                var s = 0.26f + (float)rng.NextDouble() * 0.1f;
                var pos = CampfirePosition + new Vector3(Mathf.Cos(a) * 0.95f, s * 0.15f, Mathf.Sin(a) * 0.95f);
                b.Append(sphere, Matrix4x4.TRS(pos, Quaternion.Euler(0f, a * 57f, 0f), new Vector3(s * 1.5f, s * 0.8f, s * 1.1f)), 0);
            }

            b.SanitizeNonFinite("HK_MenuGroundRocks");
            MenuBackdropBuilder.MeshObject("YerTaşları", ctx.Root, ctx.Own(b.ToMesh("HK_MenuGroundRocks")), new[] { rock }, Vector3.zero,
                Quaternion.identity, Vector3.one, ShadowCastingMode.Off);
        }

        /// <summary>Mavi saat gökyüzü: sisten etkilenmeyen (Sprites/Default) dikey gradyan, dağların arkasında.</summary>
        private static void BuildSky(MenuBackdropBuilder.Context ctx)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return;

            const int h = 64;
            var tex = new Texture2D(2, h, TextureFormat.RGBA32, false) { name = "HK_MenuSky", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[2 * h];
            for (var y = 0; y < h; y++)
            {
                Color32 c = MenuSceneMath.SkyGradient(y / (float)(h - 1));
                px[y * 2] = c;
                px[y * 2 + 1] = c;
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);

            var mat = new Material(shader) { name = "HK_MenuSky", renderQueue = 1000 };
            mat.mainTexture = tex;
            var b = new MeshBuilder(1);
            // v=0 ufuk altı, v=1 tepe; kameraya bakan (-Z) yüz.
            b.AddQuad(0, new Vector3(-1500f, -60f, 1100f), new Vector3(-1500f, 520f, 1100f), new Vector3(1500f, 520f, 1100f),
                new Vector3(1500f, -60f, 1100f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));
            b.SanitizeNonFinite("HK_MenuSkyQuad");
            var go = MenuBackdropBuilder.MeshObject("Gökyüzü", ctx.Root, ctx.Own(b.ToMesh("HK_MenuSkyQuad")), new[] { mat }, Vector3.zero,
                Quaternion.identity, Vector3.one, ShadowCastingMode.Off, false);
            if (go != null)
            {
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                    mr.sharedMaterials = new[] { mat };
            }
        }

        // ------------------------------------------------------------------ Duvarlar

        /// <summary>Referanstaki gibi yalnız sol-arkada alçak metal depo duvarı; sağ ve orta açık (dağ silüeti görünür).</summary>
        public static void BuildEnclosure(MenuBackdropBuilder.Context ctx)
        {
            const int metal = 0, hesco = 1, wood = 2;
            const float h = 3.2f;
            var b = new MeshBuilder(3);

            // Depo duvarı (kameraya bakar, -Z): ahşap/çelik direk + üst kenar şeridi.
            b.AddFlatQuad(metal, new Vector3(-16f, 0f, WallZ), new Vector3(-16f, h, WallZ), new Vector3(WarehouseMaxX, h, WallZ),
                new Vector3(WarehouseMaxX, 0f, WallZ), 0.35f);
            for (var x = -15.5f; x <= WarehouseMaxX; x += 2.5f)
                MeshFactory.AddBox(b, wood, new Vector3(x, h * 0.5f, WallZ - 0.12f), new Vector3(0.22f, h, 0.22f));
            MeshFactory.AddBox(b, wood, new Vector3((-16f + WarehouseMaxX) * 0.5f, h + 0.05f, WallZ - 0.1f), new Vector3(WarehouseMaxX + 16f, 0.12f, 0.3f));

            // Sol: alçak HESCO hattı.
            for (var z = 3f; z <= 12.5f; z += 1.1f)
                MeshFactory.AddBox(b, hesco, new Vector3(-7.4f, 0.575f, z), new Vector3(1.02f, 1.15f, 1.02f), Quaternion.Euler(0f, ctx.Range(-4f, 4f), 0f));

            b.SanitizeNonFinite("HK_MenuDioramaWalls");
            var mesh = ctx.Own(b.ToMesh("HK_MenuDioramaWalls"));
            MenuBackdropBuilder.MeshObject("Duvarlar", ctx.Root, mesh, new[]
            {
                Mat(MaterialId.MetalPanel, 0.2f), Mat(MaterialId.Hesco, 0.3f), Mat(MaterialId.WoodDark, 0.3f)
            }, Vector3.zero, Quaternion.identity, Vector3.one, ShadowCastingMode.On);
        }

        // ------------------------------------------------------------------ Kamuflaj şeritleri

        /// <summary>Referans karede tepe örtüsü yok (gökyüzü ve dağ görünmeli): bilinçli boş. Çağıran sıra korunur.</summary>
        public static void BuildCamoStrips(MenuBackdropBuilder.Context ctx)
        {
        }

        // ------------------------------------------------------------------ Kum torbası

        public static void BuildSandbagWall(MenuBackdropBuilder.Context ctx)
        {
            const float bagLength = 0.58f, bagHeight = 0.17f, bagDepth = 0.36f;
            const int rows = 5;
            var bag = MeshFactory.Sphere(0.5f, 9, 5);
            var b = new MeshBuilder();
            for (var row = 0; row < rows; row++)
            {
                var offset = (row & 1) * bagLength * 0.5f;
                for (var x = 1.7f + offset; x <= 5.9f; x += bagLength)
                {
                    var position = new Vector3(x, bagHeight * 0.5f + row * bagHeight * 0.9f, SandbagZ + ctx.Range(-0.03f, 0.03f));
                    var rotation = Quaternion.Euler(ctx.Range(-3f, 3f), ctx.Range(-5f, 5f), ctx.Range(-4f, 4f));
                    var scale = new Vector3(bagLength * ctx.Range(1.0f, 1.1f), bagHeight * ctx.Range(1.0f, 1.12f), bagDepth * ctx.Range(0.92f, 1.05f));
                    b.Append(bag, Matrix4x4.TRS(position, rotation, scale), 0);
                }
            }

            b.SanitizeNonFinite("HK_MenuDioramaSandbags");
            var mesh = ctx.Own(b.ToMesh("HK_MenuDioramaSandbags"));
            MenuBackdropBuilder.MeshObject("KumTorbaları", ctx.Root, mesh, new[] { Mat(MaterialId.Sandbag, 0.1f) }, Vector3.zero,
                Quaternion.identity, Vector3.one, ShadowCastingMode.On);
        }

        // ------------------------------------------------------------------ Sandık, varil

        public static void BuildProps(MenuBackdropBuilder.Context ctx)
        {
            PlaceProp(ctx, "crate", new Vector3(-1.3f, 0f, 7.2f), 12f, PropFallback.Crate);
            PlaceProp(ctx, "crate", new Vector3(-0.4f, 0f, 7.9f), -20f, PropFallback.Crate);
            PlaceProp(ctx, "barrel", new Vector3(5.5f, 0f, 6.7f), 0f, PropFallback.Barrel);
            PlaceProp(ctx, "barrel", new Vector3(6.1f, 0f, 7.1f), 30f, PropFallback.Barrel);
            PlaceProp(ctx, "jerrycan", new Vector3(-0.8f, 0f, 4.5f), 40f, PropFallback.Barrel);
            PlaceProp(ctx, "bags", new Vector3(-3.6f, 0f, 3.4f), 80f, PropFallback.None);
            BuildContactShadows(ctx);
            BuildFloodlights(ctx);
        }

        private enum PropFallback { None, Crate, Barrel }

        private static void PlaceProp(MenuBackdropBuilder.Context ctx, string id, Vector3 position, float yaw, PropFallback fallback)
        {
            try
            {
                if (ContentOverrides.TryGetProp(id, out var prefab) && prefab != null)
                {
                    var go = Object.Instantiate(prefab, ctx.Root);
                    go.name = "Prop_" + id;
                    go.transform.localPosition = position;
                    go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    SetLayer(go, GameLayers.Default);
                    return;
                }

                var rng = new System.Random(position.GetHashCode());
                GameObject fb = null;
                if (fallback == PropFallback.Crate)
                    fb = PropFactory.AmmoCrate(ctx.Root, position, yaw, rng);
                else if (fallback == PropFallback.Barrel)
                    fb = PropFactory.Barrel(ctx.Root, position, yaw, rng);
                if (fb != null)
                    SetLayer(fb, GameLayers.Default);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MenuBackdrop] Prop kurulamadı (" + id + "): " + e.Message);
            }
        }

        private static void SetLayer(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }

        // ------------------------------------------------------------------ Projektör direkleri

        /// <summary>Referanstaki iki saha direği: ince direk, parlak lamba kafası, sıcak-beyaz spot (ışık konisi mesh'i kaldırıldı: ucuz mavi üçgen görünümü veriyordu).</summary>
        private static void BuildFloodlights(MenuBackdropBuilder.Context ctx)
        {
            try
            {
                var poleMat = Mat(MaterialId.MetalDark, 0.12f);
                var lamp = MaterialLibrary.Unlit(new Color(1.6f, 1.45f, 1.15f));
                var poles = new[] { new Vector3(0.6f, 0f, 15.5f), new Vector3(8.5f, 0f, 12.4f) };
                for (var i = 0; i < poles.Length; i++)
                {
                    var p = poles[i];
                    const float h = 6.2f;
                    var pb = new MeshBuilder(1);
                    MeshFactory.AddFrustum(pb, 0, p, 0.08f, 0.055f, h, 6, true, false);
                    MeshFactory.AddBox(pb, 0, p + new Vector3(0f, h, 0f), new Vector3(0.9f, 0.08f, 0.1f));
                    pb.SanitizeNonFinite("HK_MenuPole");
                    MenuBackdropBuilder.MeshObject("Direk" + i, ctx.Root, ctx.Own(pb.ToMesh("HK_MenuPole")), new[] { poleMat }, Vector3.zero,
                        Quaternion.identity, Vector3.one, ShadowCastingMode.Off);

                    var head = new Vector3(p.x, h - 0.1f, p.z - 0.1f);
                    for (var k = -1; k <= 1; k += 2)
                        MenuBackdropBuilder.MeshObject("Lamba" + i, ctx.Root, MeshFactory.Sphere(0.16f, 8, 5), new[] { lamp },
                            head + new Vector3(0.3f * k, 0f, 0f), Quaternion.identity, Vector3.one, ShadowCastingMode.Off, false);

                    var go = new GameObject("Projektör" + i);
                    go.transform.SetParent(ctx.Root, false);
                    go.transform.localPosition = head;
                    go.transform.localRotation = Quaternion.LookRotation(new Vector3(-0.15f, -0.85f, -0.5f), Vector3.up);
                    var light = go.AddComponent<Light>();
                    light.type = LightType.Spot;
                    light.color = new Color(1f, 0.93f, 0.78f);
                    light.intensity = 14f;
                    light.range = 22f;
                    light.spotAngle = 68f;
                    light.shadows = LightShadows.None;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MenuBackdrop] Projektörler kurulamadı: " + e.Message);
            }
        }

        // ------------------------------------------------------------------ Kirpi

        public static void BuildVehicle(MenuBackdropBuilder.Context ctx)
        {
            var go = MenuBackdropBuilder.BuildVehicle(ctx);
            if (go == null)
                return;
            go.transform.localPosition = VehiclePosition;
            go.transform.localRotation = Quaternion.Euler(0f, VehicleYaw, 0f);
        }

        // ------------------------------------------------------------------ Asker

        /// <summary>
        /// Kameraya yakın tek asker: kask (seviye 2) + yelek + sırt çantası, alçak hazır tüfek, ağırlık kaydırma, kafa kamera soluna dönük.
        /// Takılan kask/silah görünmüyorsa (<see cref="MenuSoldierPose"/> doğrular) prosedürel yedek eklenir.
        /// </summary>
        public static List<MenuBackdropBuilder.SoldierPose> BuildSoldier(MenuBackdropBuilder.Context ctx)
        {
            var poses = new List<MenuBackdropBuilder.SoldierPose>(1);
            try
            {
                var rng = new System.Random(1920);
                var holder = new GameObject("LobiAskeri").transform;
                holder.SetParent(ctx.Root, false);
                holder.localPosition = SoldierPosition;
                holder.localRotation = Quaternion.Euler(0f, SoldierYaw, 0f);

                var look = SoldierLook.ForTeam(0, rng);
                look.Beret = false;   // Bere kaskı gizlerdi; lobide kask görünmeli.
                // SoldierModel.Build, ContentOverrides soldier varsa gerçek humanoid modeli otomatik kullanır; yoksa prosedürel.
                var model = SoldierModel.Build(holder, look, null, false, GameLayers.Default);
                if (model == null)
                    return poses;

                model.AutoSyncEquipment = false;
                model.AutoPlayDeath = false;
                model.ShowBeretOverHelmet = false;
                model.SetEquipment(2, 2, 2);
                model.SetRank(MilitaryRank.Yuzbasi);
                model.SetSeated(false);
                model.SetLocomotion(Vector3.zero, Stance.Standing, true);
                model.SetAimPitch(LowReadyPitch);
                ApplyWear(model, 0.85f, 0.7f);

                if (WeaponCatalog.TryGet(WeaponIds.Mpt76, out var weapon) && weapon != null)
                {
                    try
                    {
                        model.HoldWeapon(weapon);
                        DressHeroWeapon(model);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[MenuBackdrop] Lobi silahı kurulamadı: " + e.Message);
                    }
                }
                try { BuildSling(model); }
                catch (Exception e) { Debug.LogWarning("[MenuBackdrop] Askı kayışı kurulamadı: " + e.Message); }

                holder.gameObject.AddComponent<MenuSoldierPose>().Init(model);
                poses.Add(new MenuBackdropBuilder.SoldierPose
                {
                    Model = model,
                    Stance = Stance.Standing,
                    BasePitch = LowReadyPitch,
                    PitchAmplitude = 0f,
                    PitchSpeed = 0.1f,
                    Phase = 0f
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MenuBackdrop] Lobi askeri kurulamadı: " + e.Message);
            }

            BuildBackgroundSquad(ctx, poses);
            TryBuildSetDressing(ctx);
            return poses;
        }

        /// <summary>L2'nin MenuSetDressing.Build(ctx) çağrısı; tür/metot yoksa sessiz geçer (yansıma, derleme bağımlılığı yok).</summary>
        private static void TryBuildSetDressing(MenuBackdropBuilder.Context ctx)
        {
            try
            {
                Type t = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = asm.GetType("Project.Presentation.UI.MenuSetDressing", false);
                    if (t != null)
                        break;
                }
                var m = t != null ? t.GetMethod("Build", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null, new[] { typeof(MenuBackdropBuilder.Context) }, null) : null;
                if (m != null)
                    m.Invoke(null, new object[] { ctx });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MenuBackdrop] MenuSetDressing atlandı: " + e.Message);
            }
        }

        /// <summary>Hero tüfek: kırmızı nokta + susturucu (WeaponAttachmentVisuals.ApplyWorld) ve dürbün camında soğuk yansıma parıltısı.</summary>
        private static void DressHeroWeapon(SoldierModel model)
        {
            var root = model.CurrentWeaponRoot;
            if (root == null)
                return;
            var wm = root.GetComponentInChildren<Project.Infrastructure.Weapons.WeaponModel>();
            if (wm == null)
                return;
            Project.Infrastructure.Weapons.WeaponAttachmentVisuals.ApplyWorld(wm, new[] { "att_reddot", "att_suppressor" }, GameLayers.Default);

            var sight = wm.SightPoint != null ? wm.SightPoint : wm.transform;
            var glint = new GameObject("DurbunParilti");
            glint.layer = GameLayers.Default;
            glint.transform.SetParent(sight, false);
            glint.transform.localPosition = new Vector3(0f, 0.03f, 0.015f);
            glint.transform.localRotation = Quaternion.identity;
            glint.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Box(new Vector3(0.016f, 0.016f, 0.003f));
            glint.AddComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Unlit(new Color(0.6f, 0.85f, 1.6f));
        }

        /// <summary>Göğüste çapraz askı: sağ omuzdan sol kalçaya ince mesh şerit (göğüs soketine bağlı).</summary>
        private static void BuildSling(SoldierModel model)
        {
            var chest = model.ChestSocket != null ? model.ChestSocket : model.Chest;
            if (chest == null)
                return;
            var from = new Vector3(0.17f, 0.16f, 0.07f);
            var to = new Vector3(-0.2f, -0.22f, 0.08f);
            var go = new GameObject("TufekAskisi");
            go.layer = GameLayers.Default;
            go.transform.SetParent(chest, false);
            var dir = to - from;
            go.transform.localPosition = (from + to) * 0.5f;
            go.transform.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.forward);
            go.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Box(new Vector3(0.035f, 0.008f, dir.magnitude));
            go.AddComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Lit(new Color(0.12f, 0.12f, 0.1f), 0.1f, 0f);
        }

        /// <summary>
        /// Ateş başında 5 kişilik arka plan tim (kameradan 8-12 m): iki ayakta sohbet eden, ellerini ısıtan çömelmiş, tüfekli nöbetçi, kenarda gözcü.
        /// Her asker ayrı korunur; biri kurulamazsa diğerleri etkilenmez.
        /// </summary>
        private static void BuildBackgroundSquad(MenuBackdropBuilder.Context ctx, List<MenuBackdropBuilder.SoldierPose> poses)
        {
            var fire = CampfirePosition;
            var talkA = new Vector3(1.7f, 0f, 8.9f);
            var talkB = new Vector3(2.7f, 0f, 9.3f);
            Spawn(ctx, poses, "Tim_Sohbet1", talkA, MenuSceneMath.YawToward(talkA, talkB) + 12f, WeaponIds.Mpt55, MilitaryRank.Cavus,
                Stance.Standing, 2, 2, 1, 6f, 4f, 0.09f, 11);
            Spawn(ctx, poses, "Tim_Sohbet2", talkB, MenuSceneMath.YawToward(talkB, talkA) - 10f, WeaponIds.Mpt76, MilitaryRank.UzmanCavus,
                Stance.Standing, 2, 2, 2, 10f, 5f, 0.07f, 12);
            var warm = new Vector3(4.3f, 0f, 6.0f);
            Spawn(ctx, poses, "Tim_Isinan", warm, MenuSceneMath.YawToward(warm, fire), null, MilitaryRank.Er, Stance.Crouching,
                1, 1, 1, 30f, 3f, 0.12f, 13);
            var watch = new Vector3(5.3f, 0f, 9.8f);
            Spawn(ctx, poses, "Tim_Nobetci", watch, MenuSceneMath.YawToward(watch, new Vector3(-2f, 0f, 0f)) + 20f, WeaponIds.Mpt55, MilitaryRank.SozlesmeliEr,
                Stance.Standing, 2, 1, 2, LowReadyPitch, 2f, 0.06f, 14);
            var edge = new Vector3(0.9f, 0f, 11.4f);
            Spawn(ctx, poses, "Tim_Gozcu", edge, 190f, WeaponIds.Mpt55, MilitaryRank.Er, Stance.Standing, 2, 1, 1, 4f, 6f, 0.05f, 15);
        }

        private static void Spawn(MenuBackdropBuilder.Context ctx, List<MenuBackdropBuilder.SoldierPose> poses, string name, Vector3 position, float yaw,
            string weaponId, MilitaryRank rank, Stance stance, int helmet, int vest, int backpack, float pitch, float amp, float speed, int seed)
        {
            try
            {
                var rng = new System.Random(seed * 97);
                var holder = new GameObject(name).transform;
                holder.SetParent(ctx.Root, false);
                holder.localPosition = position;
                holder.localRotation = Quaternion.Euler(0f, yaw, 0f);

                var look = SoldierLook.ForTeam(0, rng);
                look.Beret = false;
                var model = SoldierModel.Build(holder, look, null, false, GameLayers.Default);
                if (model == null)
                    return;

                model.AutoSyncEquipment = false;
                model.AutoPlayDeath = false;
                model.ShowBeretOverHelmet = false;
                model.SetEquipment(helmet, vest, backpack);
                model.SetRank(rank);
                model.SetSeated(false);
                model.SetLocomotion(Vector3.zero, stance, true);
                model.SetAimPitch(pitch);
                ApplyWear(model, 0.5f, 0.5f);

                if (weaponId != null && WeaponCatalog.TryGet(weaponId, out var weapon) && weapon != null)
                {
                    try
                    {
                        model.HoldWeapon(weapon);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[MenuBackdrop] Tim silahı kurulamadı (" + name + "): " + e.Message);
                    }
                }

                poses.Add(new MenuBackdropBuilder.SoldierPose
                {
                    Model = model,
                    Stance = stance,
                    BasePitch = pitch,
                    PitchAmplitude = amp,
                    PitchSpeed = speed,
                    Phase = (float)rng.NextDouble() * 10f
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MenuBackdrop] Tim askeri kurulamadı (" + name + "): " + e.Message);
            }
        }

        /// <summary>
        /// Savaş yıpranması (BW1/BW2 API'si): SetWear (kir/kan/is) ve SetWeary (yorgun duruş). Metot henüz yoksa sessizce atlanır;
        /// derleme bağımlılığı yaratmamak için yansıma kullanılır.
        /// </summary>
        private static void ApplyWear(SoldierModel model, float wear, float weary)
        {
            TryInvokeFloat(model, "SetWear", wear);
            TryInvokeFloat(model, "SetWeary", weary);
        }

        private static void TryInvokeFloat(object target, string method, float value)
        {
            if (target == null)
                return;
            try
            {
                var m = target.GetType().GetMethod(method, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null,
                    new[] { typeof(float) }, null);
                if (m != null)
                    m.Invoke(target, new object[] { value });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MenuBackdrop] " + method + " uygulanamadı: " + e.Message);
            }
        }

        private const float SoldierYaw = 182f;
        private const float LowReadyPitch = 24f;

        // ------------------------------------------------------------------ Temas gölgesi

        /// <summary>Askerin ve sahne malzemelerinin altına yumuşak, koyu, yarı saydam temas gölgesi lekeleri.</summary>
        public static void BuildContactShadows(MenuBackdropBuilder.Context ctx)
        {
            try
            {
                var material = MenuContactShadow.SharedMaterial();
                if (material == null)
                    return;

                MenuContactShadow.Place(ctx.Root, material, SoldierPosition + new Vector3(0f, 0f, 0f), 1.15f, 0.9f, 0.6f);
                MenuContactShadow.Place(ctx.Root, material, new Vector3(-1.3f, 0f, 7.2f), 1.3f, 1.1f, 0.5f);
                MenuContactShadow.Place(ctx.Root, material, new Vector3(-0.4f, 0f, 7.9f), 1.3f, 1.1f, 0.5f);
                MenuContactShadow.Place(ctx.Root, material, new Vector3(5.5f, 0f, 6.7f), 1.0f, 1.0f, 0.55f);
                MenuContactShadow.Place(ctx.Root, material, new Vector3(6.1f, 0f, 7.1f), 1.0f, 1.0f, 0.55f);
                MenuContactShadow.Place(ctx.Root, material, new Vector3(-0.8f, 0f, 4.5f), 0.6f, 0.6f, 0.5f);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MenuBackdrop] Temas gölgeleri kurulamadı: " + e.Message);
            }
        }

        private static Material Mat(MaterialId id, float fallbackGray)
        {
            var m = MenuBackdropBuilder.Mat(id);
            return m != null ? m : MaterialLibrary.Lit(new Color(fallbackGray, fallbackGray, fallbackGray), 0.1f, 0f);
        }
    }
}

namespace Project.Presentation.UI
{
    /// <summary>Paylaşılan radyal gradyanlı yumuşak gölge malzemesi ve yerleştirme.</summary>
    internal static class MenuContactShadow
    {
        private static Material _shared;
        private static Texture2D _texture;

        public static Material SharedMaterial()
        {
            if (_shared != null)
                return _shared;

            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "HK_MenuBlob", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[32 * 32];
            for (var y = 0; y < 32; y++)
            {
                for (var x = 0; x < 32; x++)
                {
                    var dx = (x + 0.5f) / 16f - 1f;
                    var dy = (y + 0.5f) / 16f - 1f;
                    var d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    var a = d * d * (3f - 2f * d);
                    px[y * 32 + x] = new Color32(0, 0, 0, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            _texture = tex;

            var baseMaterial = MaterialLibrary.Transparent(Color.black, true);
            if (baseMaterial == null)
                return null;
            _shared = new Material(baseMaterial) { name = "HK_MenuBlob" };
            if (_shared.HasProperty("_BaseMap"))
                _shared.SetTexture("_BaseMap", tex);
            if (_shared.HasProperty("_MainTex"))
                _shared.SetTexture("_MainTex", tex);
            return _shared;
        }

        public static void Place(Transform root, Material material, Vector3 position, float width, float depth, float strength)
        {
            var go = new GameObject("TemasGölgesi");
            go.layer = GameLayers.Default;
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(position.x, 0.02f, position.z);
            go.transform.localRotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Quad(width, depth);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(0f, 0f, 0f, Mathf.Clamp01(strength)));
            block.SetColor("_Color", new Color(0f, 0f, 0f, Mathf.Clamp01(strength)));
            r.SetPropertyBlock(block);
        }
    }

    /// <summary>
    /// Lobi askerinin sahne pozu: ağırlık kaydırma + kafa kamera soluna. Kask ve silah görünür mü ilk karelerde doğrulanır; değilse
    /// prosedürel yedek eklenir. Bone dönüşleri her kare Animator/model yazdıysa üzerine, yazmadıysa kayıtlı tabana uygulanır (birikmez).
    /// </summary>
    [DefaultExecutionOrder(500)]
    internal sealed class MenuSoldierPose : MonoBehaviour
    {
        private const float HeadYaw = -28f;
        private const float HipShift = 0.022f;
        private const float HipRoll = 2.2f;

        private SoldierModel _model;
        private Transform _head;
        private Transform _hips;
        private Quaternion _headApplied, _hipsRotApplied;
        private Vector3 _hipsPosApplied;
        private bool _headHas, _hipsHas;
        private Quaternion _headBase, _hipsRotBase;
        private Vector3 _hipsPosBase;
        private int _frames;
        private bool _verified;

        public void Init(SoldierModel model)
        {
            _model = model;
            _head = model.GetHumanoidBone(HumanBodyBones.Head);
            if (_head == null)
                _head = model.Head;
            _hips = model.GetHumanoidBone(HumanBodyBones.Hips);
            if (_hips == null)
                _hips = model.Hips;
        }

        private void LateUpdate()
        {
            if (_model == null)
                return;

            _frames++;
            ApplyHead();
            ApplyHips();
            if (!_verified && _frames >= 4)
            {
                _verified = true;
                try
                {
                    // Gerçek (satın alınmış) model aktifse kask modelle gelir; prosedürel kask yedeği eklenmez.
                    if (_model.HumanoidRoot == null)
                        VerifyHeadgear();
                    VerifyWeapon();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[MenuBackdrop] Asker doğrulaması başarısız: " + e.Message);
                }
            }
        }

        private void ApplyHead()
        {
            if (_head == null)
                return;
            // Model bu karede yazmadıysa (değer son uyguladığımızla aynı) kayıtlı tabandan, yazdıysa yeni değerden başla.
            var current = _head.localRotation;
            _headBase = _headHas && current == _headApplied ? _headBase : current;
            _headHas = true;
            _head.localRotation = _headBase;
            _head.rotation = Quaternion.AngleAxis(HeadYaw, transform.up) * _head.rotation;
            _headApplied = _head.localRotation;
        }

        private void ApplyHips()
        {
            if (_hips == null)
                return;
            var rot = _hips.localRotation;
            var pos = _hips.localPosition;
            if (!(_hipsHas && rot == _hipsRotApplied && pos == _hipsPosApplied))
            {
                _hipsRotBase = rot;
                _hipsPosBase = pos;
            }

            _hipsHas = true;
            _hips.localRotation = _hipsRotBase;
            _hips.localPosition = _hipsPosBase;
            _hips.position += transform.right * HipShift;
            _hips.rotation = Quaternion.AngleAxis(HipRoll, transform.forward) * _hips.rotation;
            _hipsRotApplied = _hips.localRotation;
            _hipsPosApplied = _hips.localPosition;
        }

        private static bool HasVisible(Transform root, string nameContains)
        {
            if (root == null)
                return false;
            var renderers = root.GetComponentsInChildren<Renderer>(false);
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                    continue;
                if (nameContains != null && r.name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                return true;
            }

            return false;
        }

        private void VerifyHeadgear()
        {
            if (HasVisible(_model.transform, "helmet") || HasVisible(_model.transform, "beret") || HasVisible(_model.transform, "cap"))
                return;

            Debug.LogWarning("[MenuBackdrop] Kask görünmüyor; prosedürel yedek kask ekleniyor.");
            var parent = _model.HeadSocket != null ? _model.HeadSocket : _model.Head;
            if (parent == null)
                return;

            var go = new GameObject("YedekKask");
            go.layer = GameLayers.Default;
            var origin = _model.EyePoint != null ? _model.EyePoint.position : parent.position + transform.up * 0.08f;
            go.transform.SetParent(parent, true);
            go.transform.position = origin + transform.up * 0.045f;
            go.transform.rotation = transform.rotation;
            var ls = parent.lossyScale;
            var k = Mathf.Max(0.0001f, (Mathf.Abs(ls.x) + Mathf.Abs(ls.y) + Mathf.Abs(ls.z)) / 3f);
            go.transform.localScale = new Vector3(1.0f, 0.9f, 1.12f) / k;
            go.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Hemisphere(0.125f, 14, 6);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = MaterialLibrary.Lit(new Color(0.22f, 0.27f, 0.17f), 0.25f, 0f);
        }

        private void VerifyWeapon()
        {
            // Humanoid override silahı yuvadan ele taşıyabilir: önce gerçek silah kökünü, sonra yuvayı denetle.
            var weaponRoot = _model.CurrentWeaponRoot;
            if (weaponRoot != null && HasVisible(weaponRoot, null))
                return;

            var socket = _model.WeaponSocket;
            if (socket == null || (weaponRoot == null && HasVisible(socket, null)))
                return;

            Debug.LogWarning("[MenuBackdrop] Silah görünmüyor; prosedürel yedek tüfek ekleniyor.");
            var root = new GameObject("YedekTüfek");
            root.layer = GameLayers.Default;
            root.transform.SetParent(socket, false);
            var metal = MaterialLibrary.Lit(new Color(0.09f, 0.095f, 0.1f), 0.3f, 0.6f);
            Box(root.transform, metal, new Vector3(0.05f, 0.08f, 0.38f), new Vector3(0f, 0.05f, 0.08f), 0f);
            Box(root.transform, metal, new Vector3(0.022f, 0.022f, 0.36f), new Vector3(0f, 0.06f, 0.44f), 0f);
            Box(root.transform, metal, new Vector3(0.045f, 0.1f, 0.24f), new Vector3(0f, 0.03f, -0.2f), 0f);
            Box(root.transform, metal, new Vector3(0.03f, 0.14f, 0.06f), new Vector3(0f, -0.04f, 0.12f), 12f);
            Box(root.transform, metal, new Vector3(0.03f, 0.09f, 0.04f), new Vector3(0f, -0.01f, 0f), -15f);
        }

        private static void Box(Transform parent, Material material, Vector3 size, Vector3 position, float pitch)
        {
            var go = new GameObject("Parça");
            go.layer = GameLayers.Default;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Box(size);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
