using System;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Lobi dioramasının "yaşayan kamp" dekoru: ateş yanında harita masası (katlanır masa, açık harita dokusu, gaz lambası + sıcak nokta ışık),
    /// telsiz sandığı (parlayan kadran, kıvrık anten), açık mühimmat sandığı (kemer + şarjörler), yere dayalı iki sırt çantası, çadır önünde
    /// rüzgârda dalgalanan kamuflaj branda, dağınık kovanlar + kablo, Kirpi yanında yakıt bidonları ve direklerde gergi halatları.
    /// Tüm parçalar prosedürel (MeshBuilder), MaterialLibrary malzemeli, tohumlu (deterministik) ve toplam ≤ 8000 üçgendir.
    /// Üretilen mesh'ler <see cref="MenuBackdropBuilder.Context.OwnedMeshes"/> listesine eklenir. L1 (<see cref="MenuBackdropBuilder"/> sahibi) çağırır.
    /// </summary>
    internal static class MenuSetDressing
    {
        /// <summary>Üçgen bütçesi (toplam).</summary>
        public const int TriangleBudget = 8000;

        private static Texture2D _mapTexture;
        private static int _triangles;

        /// <summary>Tüm set dekorunu kurar; her parça kendi hata yakalamasıyla izole edilir.</summary>
        internal static void Build(MenuBackdropBuilder.Context ctx)
        {
            if (ctx == null || ctx.Root == null)
                return;

            _triangles = 0;
            var rng = new System.Random(4417);
            Safe("HaritaMasasi", () => BuildMapTable(ctx, new Vector3(-0.1f, 0f, 3.2f), 18f));
            Safe("TelsizSandigi", () => BuildRadioCase(ctx, new Vector3(-1.5f, 0f, 2.0f), -24f));
            Safe("MuhimmatSandigi", () => BuildAmmoCrate(ctx, new Vector3(0.5f, 0f, 0.7f), 14f));
            Safe("SirtCantalari", () => BuildBackpacks(ctx));
            Safe("Branda", () => BuildTarp(ctx, new Vector3(-4.4f, 0f, 5.4f), 90f));
            Safe("Kovanlar", () => BuildShellsAndCable(ctx, rng));
            Safe("YakitBidonlari", () => BuildFuel(ctx));
            Safe("GergiHalatlari", () => BuildGuyRopes(ctx));

            if (_triangles > TriangleBudget)
                Debug.LogWarning("[MenuSetDressing] Üçgen bütçesi aşıldı: " + _triangles + " > " + TriangleBudget);
        }

        // ================================================================== Yardımcılar

        private static void Safe(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MenuSetDressing] " + name + " kurulamadı: " + e.Message);
            }
        }

        private static Vector3 Ground(float x, float z) => new Vector3(x, MenuBackdropBuilder.GroundHeight(x, z) + 0.03f, z);

        private static Material Lit(Color c, float smooth, float metal)
        {
            try
            {
                return MaterialLibrary.Lit(c, smooth, metal);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MenuSetDressing] Malzeme alınamadı: " + e.Message);
                return null;
            }
        }

        private static Material Glow(Color c)
        {
            try
            {
                return MaterialLibrary.Unlit(c);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static GameObject Emit(MenuBackdropBuilder.Context ctx, string name, Vector3 pos, float yaw, Material[] mats, Action<MeshBuilder> fill,
            ShadowCastingMode shadows = ShadowCastingMode.On)
        {
            var b = new MeshBuilder(mats.Length);
            fill(b);
            b.SanitizeNonFinite("HK_Set_" + name);
            var mesh = ctx.Own(b.ToMesh("HK_Set_" + name));
            if (mesh == null)
                return null;
            for (var i = 0; i < mesh.subMeshCount; i++)
                _triangles += (int)(mesh.GetIndexCount(i) / 3);
            return MenuBackdropBuilder.MeshObject(name, ctx.Root, mesh, mats, pos, Quaternion.Euler(0f, yaw, 0f), Vector3.one, shadows);
        }

        private static void AddLight(MenuBackdropBuilder.Context ctx, string name, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(ctx.Root, false);
            go.transform.localPosition = pos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        /// <summary>İki nokta arasında ince çubuk (halat, kablo, ayak).</summary>
        private static void Rod(MeshBuilder b, int sub, Vector3 p0, Vector3 p1, float thickness)
        {
            var d = p1 - p0;
            var len = d.magnitude;
            if (len < 1e-4f)
                return;
            var dir = d / len;
            var up = Mathf.Abs(dir.y) > 0.95f ? Vector3.right : Vector3.up;
            MeshFactory.AddBox(b, sub, (p0 + p1) * 0.5f, new Vector3(thickness, thickness, len), Quaternion.LookRotation(dir, up));
        }

        /// <summary>Sarkmalı halat: kısa doğrusal parçalar.</summary>
        private static void Rope(MeshBuilder b, int sub, Vector3 p0, Vector3 p1, float thickness, float sag, int segs)
        {
            var prev = p0;
            for (var i = 1; i <= segs; i++)
            {
                var s = i / (float)segs;
                var p = Vector3.Lerp(p0, p1, s);
                p.y -= sag * 4f * s * (1f - s);
                Rod(b, sub, prev, p, thickness);
                prev = p;
            }
        }

        /// <summary>Yatay silindir (kovan, bidon boyu vb.): merkez, yön yaw, uzunluk ekseni x.</summary>
        private static void Lying(MeshBuilder b, int sub, Vector3 center, float yaw, float radius, float length, int segments)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, 90f);
            var basePoint = center - rot * new Vector3(0f, length * 0.5f, 0f);
            MeshFactory.AddFrustum(b, sub, basePoint, rot, radius, radius, length, segments, true, false);
        }

        // ================================================================== Harita masası

        private static void BuildMapTable(MenuBackdropBuilder.Context ctx, Vector3 at, float yaw)
        {
            var wood = Lit(new Color(0.42f, 0.3f, 0.18f), 0.2f, 0f);
            var metal = Lit(new Color(0.16f, 0.17f, 0.18f), 0.5f, 0.7f);
            var map = MaterialLibrary.Textured(GetMapTexture(), Color.white, 0.12f, 0f, 1f);
            var lampGlass = Glow(new Color(2.2f, 1.5f, 0.7f));
            var cloth = Lit(new Color(0.22f, 0.25f, 0.15f), 0.1f, 0f);
            var brass = Lit(new Color(0.8f, 0.62f, 0.25f), 0.6f, 0.8f);
            var mats = new[] { wood, metal, map, lampGlass, cloth, brass };

            Emit(ctx, "HaritaMasasi", Ground(at.x, at.z), yaw, mats, b =>
            {
                const float top = 0.76f;
                MeshFactory.AddBox(b, 0, new Vector3(0f, top - 0.02f, 0f), new Vector3(1.3f, 0.035f, 0.86f));
                MeshFactory.AddBox(b, 1, new Vector3(0f, top - 0.05f, 0.4f), new Vector3(1.26f, 0.025f, 0.03f));
                MeshFactory.AddBox(b, 1, new Vector3(0f, top - 0.05f, -0.4f), new Vector3(1.26f, 0.025f, 0.03f));
                for (var side = -1; side <= 1; side += 2)
                {
                    var x = side * 0.55f;
                    Rod(b, 1, new Vector3(x, top - 0.05f, -0.33f), new Vector3(x, 0.02f, 0.33f), 0.03f);
                    Rod(b, 1, new Vector3(x, top - 0.05f, 0.33f), new Vector3(x, 0.02f, -0.33f), 0.03f);
                }

                // Açık harita (yüzü yukarı).
                const float y = top;
                b.AddQuad(2, new Vector3(-0.58f, y, -0.37f), new Vector3(-0.58f, y, 0.37f), new Vector3(0.58f, y, 0.37f), new Vector3(0.58f, y, -0.37f),
                    new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));

                // Pusula, kurşun kalem, cetvel, kupa.
                MeshFactory.AddFrustum(b, 1, new Vector3(0.3f, y + 0.002f, 0.18f), 0.04f, 0.04f, 0.014f, 10, true, false);
                MeshFactory.AddBox(b, 5, new Vector3(0.3f, y + 0.0165f, 0.18f), new Vector3(0.05f, 0.002f, 0.05f), Quaternion.Euler(0f, 20f, 0f));
                Rod(b, 5, new Vector3(-0.2f, y + 0.006f, 0.1f), new Vector3(0.05f, y + 0.006f, -0.12f), 0.01f);
                MeshFactory.AddBox(b, 0, new Vector3(-0.1f, y + 0.004f, -0.2f), new Vector3(0.3f, 0.004f, 0.04f), Quaternion.Euler(0f, -28f, 0f));
                MeshFactory.AddFrustum(b, 1, new Vector3(-0.46f, y, -0.25f), 0.04f, 0.045f, 0.09f, 10, true, false);

                // Gaz lambası (sağ üst köşe).
                var lamp = new Vector3(0.5f, y, -0.22f);
                MeshFactory.AddFrustum(b, 1, lamp, 0.07f, 0.06f, 0.035f, 8, true, false);
                MeshFactory.AddFrustum(b, 3, lamp + new Vector3(0f, 0.035f, 0f), 0.052f, 0.048f, 0.17f, 8, false, false);
                MeshFactory.AddFrustum(b, 1, lamp + new Vector3(0f, 0.205f, 0f), 0.065f, 0.03f, 0.04f, 8, true, false);
                Rod(b, 1, lamp + new Vector3(-0.05f, 0.04f, 0f), lamp + new Vector3(-0.05f, 0.2f, 0f), 0.008f);
                Rod(b, 1, lamp + new Vector3(0.05f, 0.04f, 0f), lamp + new Vector3(0.05f, 0.2f, 0f), 0.008f);

                // İki katlanır tabure.
                Stool(b, new Vector3(0.15f, 0f, -0.95f), 8f);
                Stool(b, new Vector3(-0.95f, 0f, 0.1f), 84f);
            });

            var q = Quaternion.Euler(0f, yaw, 0f);
            AddLight(ctx, "MasaLambasi", Ground(at.x, at.z) + q * new Vector3(0.5f, 1.05f, -0.22f), new Color(1f, 0.72f, 0.4f), 2.4f, 5.5f);
        }

        private static void Stool(MeshBuilder b, Vector3 c, float yaw)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            MeshFactory.AddBox(b, 4, c + new Vector3(0f, 0.43f, 0f), new Vector3(0.34f, 0.03f, 0.34f), q);
            for (var i = 0; i < 4; i++)
            {
                var sx = (i & 1) == 0 ? -1f : 1f;
                var sz = (i & 2) == 0 ? -1f : 1f;
                Rod(b, 1, c + q * new Vector3(sx * 0.14f, 0.42f, sz * 0.14f), c + q * new Vector3(sx * 0.2f, 0.01f, sz * 0.2f), 0.02f);
            }
        }

        /// <summary>Açık harita dokusu: kâğıt, kontur çizgileri, nehir, kareli ızgara, kırmızı hedef işareti. Tohumsuz saf fonksiyon (deterministik).</summary>
        private static Texture2D GetMapTexture()
        {
            if (_mapTexture != null)
                return _mapTexture;

            const int w = 128, h = 96;
            var px = new Color32[w * h];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var paper = 0.84f + (Mathf.PerlinNoise(x * 0.31f + 5f, y * 0.31f + 9f) - 0.5f) * 0.07f;
                    var c = new Color(paper, paper * 0.93f, paper * 0.72f);
                    var elev = Mathf.PerlinNoise(x * 0.04f + 3f, y * 0.04f + 7f);

                    // Orman yamaları (soluk yeşil) ve yüksek zemin (soluk kahve).
                    if (Mathf.PerlinNoise(x * 0.06f + 20f, y * 0.06f + 2f) > 0.62f)
                        c = Color.Lerp(c, new Color(0.55f, 0.66f, 0.4f), 0.45f);

                    // Kontur çizgileri.
                    var f = elev * 10f - Mathf.Floor(elev * 10f);
                    if (f < 0.07f)
                        c = Color.Lerp(c, new Color(0.5f, 0.34f, 0.2f), 0.7f);

                    // Nehir.
                    var rx = 52f + 16f * Mathf.Sin(y * 0.07f) + (y * 0.2f);
                    if (Mathf.Abs(x - rx) < 2f)
                        c = Color.Lerp(c, new Color(0.35f, 0.55f, 0.72f), 0.9f);

                    // Izgara.
                    if (x % 16 == 0 || y % 16 == 0)
                        c = Color.Lerp(c, new Color(0.45f, 0.3f, 0.3f), 0.4f);

                    // Kırmızı hedef halkası ve çarpı.
                    var dx = x - 92f;
                    var dy = y - 58f;
                    var d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (Mathf.Abs(d - 10f) < 1.2f || (Mathf.Abs(Mathf.Abs(dx) - Mathf.Abs(dy)) < 1.1f && d < 7f))
                        c = new Color(0.78f, 0.1f, 0.1f);

                    // Kenar solması.
                    var edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                    if (edge < 3)
                        c *= 0.82f + edge * 0.06f;

                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "HK_MenuMap", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _mapTexture = tex;
            return tex;
        }

        // ================================================================== Telsiz sandığı

        private static void BuildRadioCase(MenuBackdropBuilder.Context ctx, Vector3 at, float yaw)
        {
            var green = Lit(new Color(0.2f, 0.27f, 0.15f), 0.25f, 0.2f);
            var dark = Lit(new Color(0.07f, 0.08f, 0.07f), 0.3f, 0.4f);
            var dial = Glow(new Color(0.4f, 1.7f, 0.7f));
            var metal = Lit(new Color(0.55f, 0.55f, 0.52f), 0.5f, 0.8f);
            var rubber = Lit(new Color(0.03f, 0.03f, 0.03f), 0.2f, 0f);
            var mats = new[] { green, dark, dial, metal, rubber };

            Emit(ctx, "TelsizSandigi", Ground(at.x, at.z), yaw, mats, b =>
            {
                MeshFactory.AddBox(b, 0, new Vector3(0f, 0.15f, 0f), new Vector3(0.56f, 0.3f, 0.4f));
                MeshFactory.AddBox(b, 1, new Vector3(0f, 0.304f, 0f), new Vector3(0.5f, 0.01f, 0.34f));
                for (var i = 0; i < 4; i++)
                    MeshFactory.AddBox(b, 3, new Vector3((i & 1) == 0 ? -0.28f : 0.28f, 0.28f, (i & 2) == 0 ? -0.2f : 0.2f), new Vector3(0.025f, 0.05f, 0.025f));

                // Parlayan kadran + küçük kadran + düğmeler + hoparlör ızgarası.
                MeshFactory.AddFrustum(b, 2, new Vector3(-0.12f, 0.309f, 0.02f), 0.075f, 0.075f, 0.006f, 14, true, false);
                MeshFactory.AddFrustum(b, 2, new Vector3(0.1f, 0.309f, 0.1f), 0.035f, 0.035f, 0.006f, 10, true, false);
                for (var i = 0; i < 3; i++)
                    MeshFactory.AddFrustum(b, 3, new Vector3(0.06f + i * 0.07f, 0.309f, -0.08f), 0.02f, 0.017f, 0.03f, 8, true, false);
                for (var i = 0; i < 4; i++)
                    MeshFactory.AddBox(b, 3, new Vector3(-0.12f + i * 0.05f, 0.31f, -0.12f), new Vector3(0.03f, 0.004f, 0.02f));

                // Taşıma sapı (yan).
                Rod(b, 3, new Vector3(0.285f, 0.25f, -0.1f), new Vector3(0.33f, 0.3f, -0.1f), 0.015f);
                Rod(b, 3, new Vector3(0.33f, 0.3f, -0.1f), new Vector3(0.33f, 0.3f, 0.1f), 0.015f);
                Rod(b, 3, new Vector3(0.33f, 0.3f, 0.1f), new Vector3(0.285f, 0.25f, 0.1f), 0.015f);

                // Kıvrık anten.
                var start = new Vector3(-0.22f, 0.31f, -0.14f);
                MeshFactory.AddFrustum(b, 3, start, 0.022f, 0.016f, 0.04f, 8, true, false);
                var prev = start + new Vector3(0f, 0.04f, 0f);
                for (var i = 1; i <= 10; i++)
                {
                    var t = i / 10f;
                    var p = start + new Vector3(0.12f * t * t, 0.04f + 1.05f * t - 0.28f * t * t, 0.5f * t * t * t);
                    Rod(b, 3, prev, p, 0.009f);
                    prev = p;
                }
            });

            AddLight(ctx, "TelsizIsigi", Ground(at.x, at.z) + Quaternion.Euler(0f, yaw, 0f) * new Vector3(-0.12f, 0.45f, 0.02f), new Color(0.4f, 1f, 0.55f), 0.35f, 1.6f);
        }

        // ================================================================== Mühimmat sandığı

        private static void BuildAmmoCrate(MenuBackdropBuilder.Context ctx, Vector3 at, float yaw)
        {
            var wood = Lit(new Color(0.3f, 0.33f, 0.19f), 0.2f, 0f);
            var brass = Lit(new Color(0.85f, 0.66f, 0.26f), 0.65f, 0.85f);
            var gun = Lit(new Color(0.12f, 0.12f, 0.13f), 0.45f, 0.8f);
            var belt = Lit(new Color(0.3f, 0.28f, 0.22f), 0.3f, 0.5f);
            var mats = new[] { wood, brass, gun, belt };

            Emit(ctx, "MuhimmatSandigi", Ground(at.x, at.z), yaw, mats, b =>
            {
                MeshFactory.AddBox(b, 0, new Vector3(0f, 0.01f, 0f), new Vector3(0.6f, 0.02f, 0.35f));
                MeshFactory.AddBox(b, 0, new Vector3(0f, 0.15f, 0.165f), new Vector3(0.6f, 0.3f, 0.02f));
                MeshFactory.AddBox(b, 0, new Vector3(0f, 0.15f, -0.165f), new Vector3(0.6f, 0.3f, 0.02f));
                MeshFactory.AddBox(b, 0, new Vector3(0.29f, 0.15f, 0f), new Vector3(0.02f, 0.3f, 0.35f));
                MeshFactory.AddBox(b, 0, new Vector3(-0.29f, 0.15f, 0f), new Vector3(0.02f, 0.3f, 0.35f));

                // Arkaya yaslı açık kapak.
                var pivot = new Vector3(0f, 0.3f, -0.175f);
                var rot = Quaternion.Euler(-108f, 0f, 0f);
                MeshFactory.AddBox(b, 0, pivot + rot * new Vector3(0f, 0.01f, 0.175f), new Vector3(0.6f, 0.02f, 0.35f), rot);

                // Şerit mühimmat (sağ yarı): iki sıra kovan + kemer.
                for (var i = 0; i < 8; i++)
                {
                    var x = 0.02f + i * 0.033f;
                    MeshFactory.AddFrustum(b, 1, new Vector3(x, 0.025f, 0.05f), 0.011f, 0.011f, 0.07f, 5, true, false);
                    MeshFactory.AddFrustum(b, 1, new Vector3(x, 0.025f, -0.05f), 0.011f, 0.011f, 0.07f, 5, true, false);
                }

                MeshFactory.AddBox(b, 3, new Vector3(0.15f, 0.05f, 0.05f), new Vector3(0.27f, 0.008f, 0.03f));
                MeshFactory.AddBox(b, 3, new Vector3(0.15f, 0.05f, -0.05f), new Vector3(0.27f, 0.008f, 0.03f));

                // Şarjörler (sol yarı), hafif eğik dizili.
                for (var i = 0; i < 3; i++)
                    MeshFactory.AddBox(b, 2, new Vector3(-0.22f + i * 0.07f, 0.1f, 0.02f), new Vector3(0.05f, 0.17f, 0.1f), Quaternion.Euler(0f, 0f, (i - 1) * 6f));

                // Yerde tek şarjör ve kovan.
                MeshFactory.AddBox(b, 2, new Vector3(0.5f, 0.025f, 0.3f), new Vector3(0.05f, 0.05f, 0.17f), Quaternion.Euler(0f, -35f, 0f));
            });
        }

        // ================================================================== Sırt çantaları

        private static void BuildBackpacks(MenuBackdropBuilder.Context ctx)
        {
            BuildPack(ctx, "SirtCantasi1", new Vector3(-0.1f, 0f, 6.5f), 168f, MaterialLibrary.Get(MaterialId.CamoWoodland), 1f);
            BuildPack(ctx, "SirtCantasi2", new Vector3(0.6f, 0f, 6.7f), 196f, Lit(new Color(0.28f, 0.27f, 0.19f), 0.15f, 0f), 0.88f);
        }

        private static void BuildPack(MenuBackdropBuilder.Context ctx, string name, Vector3 at, float yaw, Material body, float scale)
        {
            var dark = Lit(new Color(0.1f, 0.11f, 0.09f), 0.15f, 0f);
            var roll = Lit(new Color(0.55f, 0.48f, 0.34f), 0.1f, 0f);
            var lean = Quaternion.Euler(-12f, 0f, 0f);
            Emit(ctx, name, Ground(at.x, at.z), yaw, new[] { body, dark, roll }, b =>
            {
                Vector3 P(float x, float y, float z) => lean * (new Vector3(x, y, z) * scale);
                MeshFactory.AddBox(b, 0, P(0f, 0.31f, 0f), new Vector3(0.42f, 0.62f, 0.24f) * scale, lean);
                MeshFactory.AddBox(b, 0, P(0f, 0.65f, 0f), new Vector3(0.44f, 0.12f, 0.27f) * scale, lean);
                MeshFactory.AddBox(b, 1, P(0.26f, 0.2f, 0.01f), new Vector3(0.1f, 0.28f, 0.2f) * scale, lean);
                MeshFactory.AddBox(b, 1, P(-0.26f, 0.2f, 0.01f), new Vector3(0.1f, 0.28f, 0.2f) * scale, lean);
                MeshFactory.AddBox(b, 1, P(0f, 0.27f, 0.15f), new Vector3(0.3f, 0.22f, 0.07f) * scale, lean);
                MeshFactory.AddBox(b, 1, P(0.12f, 0.35f, -0.13f), new Vector3(0.05f, 0.5f, 0.02f) * scale, lean);
                MeshFactory.AddBox(b, 1, P(-0.12f, 0.35f, -0.13f), new Vector3(0.05f, 0.5f, 0.02f) * scale, lean);

                // Yatak rulosu (alt/üst).
                var rollRot = lean * Quaternion.Euler(0f, 0f, 90f);
                var c = P(0f, 0.77f, 0f);
                MeshFactory.AddFrustum(b, 2, c - rollRot * new Vector3(0f, 0.23f * scale, 0f), rollRot, 0.07f * scale, 0.07f * scale, 0.46f * scale, 8, true, false);
            });
        }

        // ================================================================== Kamuflaj branda

        private static void BuildTarp(MenuBackdropBuilder.Context ctx, Vector3 at, float yaw)
        {
            const int cols = 9, rows = 6;
            const float halfWidth = 1.6f, depth = 2.4f, topY = 2.3f, lowY = 0.95f;
            var origin = Ground(at.x, at.z);
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var camo = MaterialLibrary.Camo(new Color(0.24f, 0.28f, 0.15f), new Color(0.43f, 0.38f, 0.22f), new Color(0.12f, 0.15f, 0.09f),
                new Color(0.3f, 0.22f, 0.13f), 77, 1.5f);

            var count = cols * rows;
            var basePositions = new Vector3[count];
            var uv = new Vector2[count * 2];
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    var u = c / (float)(cols - 1);
                    var v = r / (float)(rows - 1);
                    var x = Mathf.Lerp(-halfWidth, halfWidth, u);
                    var sag = 0.12f * Mathf.Sin(Mathf.PI * u) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(v));
                    var i = r * cols + c;
                    basePositions[i] = new Vector3(x, Mathf.Lerp(topY, lowY, v) - sag, v * depth);
                    uv[i] = new Vector2(u * 1.6f, v * 1.2f);
                    uv[i + count] = uv[i];
                }
            }

            var triangles = new int[(cols - 1) * (rows - 1) * 12];
            var t = 0;
            for (var r = 0; r < rows - 1; r++)
            {
                for (var c = 0; c < cols - 1; c++)
                {
                    var a = r * cols + c;
                    var bI = a + 1;
                    var d = a + cols;
                    var e = d + 1;
                    // Üst yüz (yukarı/öne bakar), alt yüz ters sırayla.
                    triangles[t++] = a; triangles[t++] = d; triangles[t++] = bI;
                    triangles[t++] = bI; triangles[t++] = d; triangles[t++] = e;
                    triangles[t++] = a + count; triangles[t++] = bI + count; triangles[t++] = d + count;
                    triangles[t++] = bI + count; triangles[t++] = e + count; triangles[t++] = d + count;
                }
            }

            var mesh = ctx.Own(new Mesh { name = "HK_Set_Branda" });
            mesh.MarkDynamic();
            var verts = new Vector3[count * 2];
            Array.Copy(basePositions, verts, count);
            Array.Copy(basePositions, 0, verts, count, count);
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _triangles += triangles.Length / 3;

            var go = MenuBackdropBuilder.MeshObject("Branda", ctx.Root, mesh, new[] { camo }, origin, rotation, Vector3.one, ShadowCastingMode.On);
            if (go != null)
            {
                var wind = go.AddComponent<MenuTarpWind>();
                wind.Setup(mesh, basePositions, cols, rows, 0.37f);
            }

            // Direkler ve gergi halatları (dünya yerel koordinatı).
            Vector3 W(float x, float y, float z) => origin + rotation * new Vector3(x, y, z);
            var wood = Lit(new Color(0.3f, 0.22f, 0.13f), 0.15f, 0f);
            var rope = Lit(new Color(0.5f, 0.43f, 0.28f), 0.1f, 0f);
            Emit(ctx, "BrandaDirekleri", Vector3.zero, 0f, new[] { wood, rope }, b =>
            {
                for (var side = -1; side <= 1; side += 2)
                {
                    var x = side * halfWidth;
                    MeshFactory.AddFrustum(b, 0, W(x, 0f, -0.05f), 0.04f, 0.03f, topY + 0.05f, 6, true, false);
                    MeshFactory.AddFrustum(b, 0, W(x, 0f, depth + 0.05f), 0.035f, 0.028f, lowY + 0.05f, 6, true, false);
                    Rope(b, 1, W(x, topY, 0f), W(x * 1.5f, 0.02f, -1.4f), 0.012f, 0.05f, 3);
                    Rope(b, 1, W(x, lowY, depth), W(x * 1.45f, 0.02f, depth + 1.1f), 0.012f, 0.04f, 3);
                    MeshFactory.AddBox(b, 0, W(x * 1.5f, 0.05f, -1.4f), new Vector3(0.04f, 0.12f, 0.04f));
                    MeshFactory.AddBox(b, 0, W(x * 1.45f, 0.05f, depth + 1.1f), new Vector3(0.04f, 0.12f, 0.04f));
                }
            });
        }

        // ================================================================== Kovanlar + kablo

        private static void BuildShellsAndCable(MenuBackdropBuilder.Context ctx, System.Random rng)
        {
            float R(float min, float max) => min + (float)rng.NextDouble() * (max - min);
            var brass = Lit(new Color(0.85f, 0.66f, 0.26f), 0.7f, 0.85f);
            var black = Lit(new Color(0.03f, 0.03f, 0.03f), 0.25f, 0f);
            var mats = new[] { brass, black };

            Emit(ctx, "KovanlarKablo", Vector3.zero, 0f, mats, b =>
            {
                // Ateş çevresi ve nöbetçi mevzisi yanında dağınık kovanlar.
                for (var i = 0; i < 34; i++)
                {
                    var near = i < 22;
                    var x = near ? R(3.4f, 6.2f) : R(3.2f, 5.6f);
                    var z = near ? R(-0.9f, 1.2f) : R(5.2f, 7.2f);
                    var p = Ground(x, z);
                    if (rng.NextDouble() < 0.25)
                        MeshFactory.AddFrustum(b, 0, p, 0.008f, 0.008f, 0.045f, 5, true, false);
                    else
                        Lying(b, 0, p + new Vector3(0f, 0.008f, 0f), R(0f, 360f), 0.008f, 0.045f, 5);
                }

                // Telsizden masaya uzanan kablo (hafif kıvrımlı, yerde).
                var from = Ground(-1.3f, 2.2f);
                var to = Ground(-0.65f, 3.0f);
                var prev = from + new Vector3(0f, -0.005f, 0f);
                const int segs = 10;
                for (var i = 1; i <= segs; i++)
                {
                    var s = i / (float)segs;
                    var p = Vector3.Lerp(from, to, s);
                    p += new Vector3(Mathf.Sin(s * 9f) * 0.16f * Mathf.Sin(s * Mathf.PI), -0.005f + 0.012f, Mathf.Cos(s * 7f) * 0.1f * Mathf.Sin(s * Mathf.PI));
                    Rod(b, 1, prev, p, 0.016f);
                    prev = p;
                }

                // Telsizden ikinci kablo (anten besleme), ateşe doğru.
                var q0 = Ground(-1.2f, 1.9f);
                var qPrev = q0;
                for (var i = 1; i <= 8; i++)
                {
                    var s = i / 8f;
                    var p = Ground(Mathf.Lerp(-1.2f, 0.4f, s), Mathf.Lerp(1.9f, 0.9f, s) + Mathf.Sin(s * 6f) * 0.12f);
                    p.y += 0.012f;
                    Rod(b, 1, qPrev, p, 0.014f);
                    qPrev = p;
                }
            }, ShadowCastingMode.Off);
        }

        // ================================================================== Yakıt bidonları (Kirpi yanı)

        private static void BuildFuel(MenuBackdropBuilder.Context ctx)
        {
            var olive = Lit(new Color(0.2f, 0.26f, 0.14f), 0.3f, 0.45f);
            var rust = Lit(new Color(0.42f, 0.2f, 0.1f), 0.2f, 0.6f);
            var wood = Lit(new Color(0.4f, 0.3f, 0.18f), 0.15f, 0f);
            var rim = Lit(new Color(0.1f, 0.11f, 0.1f), 0.4f, 0.6f);
            var mats = new[] { olive, rust, wood, rim };

            Emit(ctx, "YakitBidonlari", Ground(10.3f, 2.4f), 12f, mats, b =>
            {
                // Palet.
                for (var i = 0; i < 3; i++)
                    MeshFactory.AddBox(b, 2, new Vector3(-0.55f + i * 0.55f, 0.06f, 0f), new Vector3(0.12f, 0.1f, 1.2f));
                for (var i = 0; i < 6; i++)
                    MeshFactory.AddBox(b, 2, new Vector3(0f, 0.12f, -0.55f + i * 0.22f), new Vector3(1.2f, 0.025f, 0.15f));

                // Üç dik varil + bir yatık varil.
                Drum(b, new Vector3(-0.3f, 0.13f, -0.28f), 0);
                Drum(b, new Vector3(0.32f, 0.13f, -0.3f), 1);
                Drum(b, new Vector3(-0.05f, 0.13f, 0.36f), 0);
                var lyingRot = Quaternion.Euler(0f, 70f, 0f) * Quaternion.Euler(0f, 0f, 90f);
                MeshFactory.AddFrustum(b, 1, new Vector3(0.9f, 0.3f, 1.1f) - lyingRot * new Vector3(0f, 0.44f, 0f), lyingRot, 0.29f, 0.29f, 0.88f, 14, true, false);
                MeshFactory.AddFrustum(b, 3, new Vector3(0.9f, 0.3f, 1.1f) - lyingRot * new Vector3(0f, 0.2f, 0f), lyingRot, 0.3f, 0.3f, 0.05f, 14, false, false);

                // İki bidon (jerrycan) yerde.
                Jerry(b, new Vector3(1.1f, 0f, -0.2f), 20f);
                Jerry(b, new Vector3(1.4f, 0f, 0.1f), -35f);
            });
        }

        private static void Drum(MeshBuilder b, Vector3 baseP, int sub)
        {
            MeshFactory.AddFrustum(b, sub, baseP, 0.29f, 0.29f, 0.88f, 14, true, false);
            MeshFactory.AddFrustum(b, 3, baseP + new Vector3(0f, 0.2f, 0f), 0.3f, 0.3f, 0.04f, 14, false, false);
            MeshFactory.AddFrustum(b, 3, baseP + new Vector3(0f, 0.6f, 0f), 0.3f, 0.3f, 0.04f, 14, false, false);
            MeshFactory.AddFrustum(b, 3, baseP + new Vector3(0.12f, 0.88f, 0.05f), 0.035f, 0.035f, 0.025f, 6, true, false);
        }

        private static void Jerry(MeshBuilder b, Vector3 p, float yaw)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            MeshFactory.AddBox(b, 0, p + q * new Vector3(0f, 0.2f, 0f), new Vector3(0.34f, 0.4f, 0.16f), q);
            MeshFactory.AddBox(b, 3, p + q * new Vector3(0f, 0.41f, 0f), new Vector3(0.2f, 0.03f, 0.1f), q);
            MeshFactory.AddFrustum(b, 3, p + q * new Vector3(0.11f, 0.4f, 0f), 0.03f, 0.025f, 0.07f, 6, true, false);
        }

        // ================================================================== Gergi halatları

        private static void BuildGuyRopes(MenuBackdropBuilder.Context ctx)
        {
            // Projektör direkleri: MenuDioramaBuilder.BuildFloodlights ile aynı konumlar (h = 6.2).
            var poles = new[] { new Vector3(0.6f, 0f, 15.5f), new Vector3(8.5f, 0f, 12.4f) };
            var rope = Lit(new Color(0.45f, 0.4f, 0.27f), 0.1f, 0f);
            var peg = Lit(new Color(0.12f, 0.12f, 0.12f), 0.4f, 0.6f);
            Emit(ctx, "GergiHalatlari", Vector3.zero, 0f, new[] { rope, peg }, b =>
            {
                for (var i = 0; i < poles.Length; i++)
                {
                    var p = poles[i];
                    var top = new Vector3(p.x, MenuBackdropBuilder.GroundHeight(p.x, p.z) + 5.4f, p.z);
                    for (var k = 0; k < 3; k++)
                    {
                        var a = (k * 120f + 40f + i * 25f) * Mathf.Deg2Rad;
                        var gx = p.x + Mathf.Sin(a) * 3.4f;
                        var gz = p.z + Mathf.Cos(a) * 3.4f;
                        var ground = new Vector3(gx, MenuBackdropBuilder.GroundHeight(gx, gz) + 0.05f, gz);
                        Rope(b, 0, top, ground, 0.014f, 0.08f, 4);
                        MeshFactory.AddBox(b, 1, ground + new Vector3(0f, 0.04f, 0f), new Vector3(0.05f, 0.14f, 0.05f), Quaternion.Euler(0f, 0f, 0f));
                    }
                }
            });
        }

        // ================================================================== Rüzgâr

        /// <summary>Brandanın köşe vertex'lerini hafifçe dalgalandırır (üst kenar sabit, alt uç serbest, köşeler sabitlenmiş).</summary>
        internal sealed class MenuTarpWind : MonoBehaviour
        {
            private Mesh _mesh;
            private Vector3[] _base;
            private Vector3[] _verts;
            private int _cols;
            private int _rows;
            private float _seed;
            private int _frame;

            public void Setup(Mesh mesh, Vector3[] basePositions, int cols, int rows, float seed)
            {
                _mesh = mesh;
                _base = basePositions;
                _cols = cols;
                _rows = rows;
                _seed = seed;
                _verts = new Vector3[basePositions.Length * 2];
            }

            private void Update()
            {
                if (_mesh == null || _base == null)
                    return;
                _frame++;
                if ((_frame & 1) != 0)
                    return;

                var t = Time.time + _seed * 10f;
                var n = _base.Length;
                for (var r = 0; r < _rows; r++)
                {
                    var v = r / (float)(_rows - 1);
                    for (var c = 0; c < _cols; c++)
                    {
                        var u = c / (float)(_cols - 1);
                        var i = r * _cols + c;
                        var edge = Mathf.Sin(Mathf.PI * u);          // yan köşeler sabit
                        var free = v * (0.35f + 0.65f * v) * edge;
                        var p = _base[i];
                        p.y += Mathf.Sin(t * 1.7f + u * 5f + v * 3.2f) * 0.07f * free + Mathf.Sin(t * 0.7f + u * 2f) * 0.03f * free;
                        p.z += Mathf.Sin(t * 1.1f + u * 3.1f) * 0.05f * free;
                        p.x += Mathf.Sin(t * 0.9f + v * 4f) * 0.02f * free;
                        _verts[i] = p;
                        _verts[i + n] = p;
                    }
                }

                _mesh.vertices = _verts;
                _mesh.RecalculateNormals();
                _mesh.RecalculateBounds();
            }
        }
    }
}
