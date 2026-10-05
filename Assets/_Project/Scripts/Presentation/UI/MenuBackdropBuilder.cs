using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Infrastructure;
using Project.Infrastructure.Characters;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Project.Presentation.UI
{
    /// <summary>
    /// <see cref="MenuBackdrop"/> dioramasının prosedürel parçaları. Tüm konumlar dekor kökünün yerel uzayındadır:
    /// kamera yerel +Z yönüne (vadiye) bakar, plato (ateş, askerler, Kirpi, siper) orijin çevresindedir ve y = 0'dadır.
    /// Üretilen (paylaşılmayan) mesh'ler <see cref="Context.OwnedMeshes"/> listesine eklenir; dekor yok edilirken silinir.
    /// MeshFactory/RockFactory önbellekli mesh'leri ve MaterialLibrary malzemeleri paylaşılır, silinmez.
    /// </summary>
    internal static class MenuBackdropBuilder
    {
        /// <summary>Düz platonun yarıçapı (eliptik ölçüyle, m).</summary>
        public const float PlateauRadius = 11.5f;

        /// <summary>Plato merkezi (yerel).</summary>
        public static readonly Vector3 PlateauCenter = new Vector3(2f, 0f, 3f);

        /// <summary>Kamp ateşi konumu.</summary>
        public static readonly Vector3 CampfirePosition = new Vector3(2.6f, 0f, 1.6f);

        /// <summary>Bayrak direği konumu (siperin arkasında).</summary>
        public static readonly Vector3 FlagPolePosition = new Vector3(3.3f, 0f, 8.3f);

        /// <summary>Kirpi konumu ve yönü.</summary>
        public static readonly Vector3 VehiclePosition = new Vector3(7.5f, 0f, 2.9f);

        public const float VehicleYaw = 206f;

        /// <summary>Kum torbası siperi yayının merkezi ve yarıçapı.</summary>
        private static readonly Vector3 WallCenter = new Vector3(2f, 0f, -2.6f);

        private const float WallRadius = 9.1f;

        /// <summary>Üretim bağlamı: kök, sahip olunan mesh'ler, tohumlu rastgele.</summary>
        internal sealed class Context
        {
            public Transform Root;
            public readonly List<Mesh> OwnedMeshes = new List<Mesh>(24);
            public System.Random Rng = new System.Random(1923);

            public Mesh Own(Mesh mesh)
            {
                if (mesh != null)
                    OwnedMeshes.Add(mesh);
                return mesh;
            }

            public float Range(float min, float max) => min + (float)Rng.NextDouble() * (max - min);
        }

        /// <summary>Asker pozu (dekor güncellemesi için).</summary>
        internal struct SoldierPose
        {
            public SoldierModel Model;
            public Stance Stance;
            public float BasePitch;
            public float PitchAmplitude;
            public float PitchSpeed;
            public float Phase;
        }

        // ================================================================== Yardımcılar

        /// <summary>Paylaşılan malzeme (hata yerine null + uyarı).</summary>
        public static Material Mat(MaterialId id)
        {
            try
            {
                return MaterialLibrary.Get(id);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MenuBackdrop] Malzeme alınamadı (" + id + "): " + e.Message);
                return null;
            }
        }

        /// <summary>Mesh nesnesi (çarpıştırıcısız, Default katmanı).</summary>
        public static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material[] materials, Vector3 localPosition,
            Quaternion localRotation, Vector3 localScale, ShadowCastingMode shadows = ShadowCastingMode.On, bool receiveShadows = true)
        {
            if (mesh == null)
                return null;

            var go = new GameObject(name);
            go.layer = GameLayers.Default;
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = localRotation;
            t.localScale = localScale;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials ?? Array.Empty<Material>();
            renderer.shadowCastingMode = shadows;
            renderer.receiveShadows = receiveShadows;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        /// <summary>
        /// Durdurulmuş, yapılandırılmaya hazır parçacık sistemi (dünya uzayı, döngülü). Çağıran ayarladıktan sonra Play()
        /// çağırmalıdır. Malzeme null ise null döner.
        /// </summary>
        public static ParticleSystem CreateParticles(string name, Transform parent, Vector3 localPosition, Material material, int maxParticles)
        {
            if (material == null)
                return null;

            var go = new GameObject(name);
            go.layer = GameLayers.Default;
            go.SetActive(false);   // Etkin değilken eklenen sistem kendiliğinden başlamaz; ayarlar güvenle değiştirilir.
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(1, maxParticles);
            main.startColor = Color.white;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer == null)
                renderer = go.AddComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            go.SetActive(true);
            return ps;
        }

        /// <summary>Renk ve alfa anahtarlarından gradyan.</summary>
        public static Gradient Gradient(Color[] colors, float[] colorTimes, float[] alphas, float[] alphaTimes)
        {
            var colorKeys = new GradientColorKey[colors.Length];
            for (var i = 0; i < colors.Length; i++)
                colorKeys[i] = new GradientColorKey(colors[i], colorTimes[i]);
            var alphaKeys = new GradientAlphaKey[alphas.Length];
            for (var i = 0; i < alphas.Length; i++)
                alphaKeys[i] = new GradientAlphaKey(alphas[i], alphaTimes[i]);

            var gradient = new Gradient();
            gradient.SetKeys(colorKeys, alphaKeys);
            return gradient;
        }

        /// <summary>Dörtgeni, normali <paramref name="outward"/> yönüne bakacak sırayla ekler.</summary>
        private static void AddQuadFacing(MeshBuilder b, int submesh, Vector3 a, Vector3 c1, Vector3 c2, Vector3 d, Vector3 outward, float uvScale = 1f)
        {
            var normal = Vector3.Cross(c1 - a, c2 - a);
            if (Vector3.Dot(normal, outward) >= 0f)
                b.AddFlatQuad(submesh, a, c1, c2, d, uvScale);
            else
                b.AddFlatQuad(submesh, a, d, c2, c1, uvScale);
        }

        /// <summary>Üçgeni, normali <paramref name="outward"/> yönüne bakacak sırayla ekler.</summary>
        private static void AddTriangleFacing(MeshBuilder b, int submesh, Vector3 a, Vector3 c1, Vector3 c2, Vector3 outward, float uvScale = 1f)
        {
            var normal = Vector3.Cross(c1 - a, c2 - a);
            if (Vector3.Dot(normal, outward) >= 0f)
                b.AddFlatTriangle(submesh, a, c1, c2, uvScale);
            else
                b.AddFlatTriangle(submesh, a, c2, c1, uvScale);
        }

        private static float Smooth(float edge0, float edge1, float x)
        {
            var t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Platonun merkezine eliptik uzaklık (x ekseninde daha geniş).</summary>
        public static float PlateauDistance(float x, float z)
        {
            var dx = (x - PlateauCenter.x) * 0.75f;
            var dz = z - PlateauCenter.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Zemin yüksekliği (yerel): platoda tam 0; dışarıda önde (+z) vadiye iniş, yanlarda ve arkada yükselen yamaçlar.
        /// </summary>
        public static float GroundHeight(float x, float z)
        {
            var blend = Smooth(PlateauRadius, PlateauRadius + 13f, PlateauDistance(x, z));
            if (blend <= 0f)
                return 0f;

            var n1 = Mathf.PerlinNoise(x * 0.033f + 11.3f, z * 0.033f + 4.7f) - 0.5f;
            var n2 = Mathf.PerlinNoise(x * 0.12f + 3.1f, z * 0.12f + 8.9f) - 0.5f;
            var valley = -Smooth(6f, 88f, z) * 34f;
            var sides = Mathf.Max(0f, Mathf.Abs(x - 2f) - 24f) * 0.34f * (1f - Smooth(30f, 110f, z) * 0.6f);
            var back = Mathf.Max(0f, -8f - z) * 0.38f;
            var terrain = valley + sides + back + n1 * 10f + n2 * 2.4f;
            return terrain * blend;
        }

        // ================================================================== Arazi

        /// <summary>Plato + vadi zemini (düz gölgeli düşük poligon; dik yamaçlar kaya).</summary>
        public static void BuildGround(Context ctx)
        {
            const float minX = -132f, maxX = 136f, minZ = -44f, maxZ = 176f, cell = 4f;
            var nx = Mathf.RoundToInt((maxX - minX) / cell);
            var nz = Mathf.RoundToInt((maxZ - minZ) / cell);
            var heights = new float[(nx + 1) * (nz + 1)];
            for (var j = 0; j <= nz; j++)
            {
                for (var i = 0; i <= nx; i++)
                    heights[j * (nx + 1) + i] = GroundHeight(minX + i * cell, minZ + j * cell);
            }

            var b = new MeshBuilder(2);
            for (var j = 0; j < nz; j++)
            {
                for (var i = 0; i < nx; i++)
                {
                    var x0 = minX + i * cell;
                    var z0 = minZ + j * cell;
                    var p00 = new Vector3(x0, heights[j * (nx + 1) + i], z0);
                    var p10 = new Vector3(x0 + cell, heights[j * (nx + 1) + i + 1], z0);
                    var p01 = new Vector3(x0, heights[(j + 1) * (nx + 1) + i], z0 + cell);
                    var p11 = new Vector3(x0 + cell, heights[(j + 1) * (nx + 1) + i + 1], z0 + cell);
                    AddGroundTriangle(b, p00, p01, p11);
                    AddGroundTriangle(b, p00, p11, p10);
                }
            }

            var mesh = ctx.Own(b.ToMesh("HK_MenuGround"));
            MeshObject("Zemin", ctx.Root, mesh, new[] { Mat(MaterialId.DryGrass), Mat(MaterialId.Rock) }, Vector3.zero, Quaternion.identity,
                Vector3.one, ShadowCastingMode.Off);

            // Ateş çevresinde ezilmiş toprak ve platoda patika lekeleri.
            var dirt = new[] { Mat(MaterialId.Dirt) };
            MeshObject("Toprak", ctx.Root, MeshFactory.Disc(2.6f, 20), dirt, CampfirePosition + new Vector3(0f, 0.012f, 0f), Quaternion.identity,
                new Vector3(1.15f, 1f, 1f), ShadowCastingMode.Off);
            MeshObject("Patika", ctx.Root, MeshFactory.Disc(1.8f, 16), dirt, new Vector3(5.6f, 0.01f, 0.4f), Quaternion.Euler(0f, 30f, 0f),
                new Vector3(2.2f, 1f, 1f), ShadowCastingMode.Off);
        }

        private static void AddGroundTriangle(MeshBuilder b, Vector3 a, Vector3 c1, Vector3 c2)
        {
            var normal = Vector3.Cross(c1 - a, c2 - a);
            var steep = normal.sqrMagnitude > 1e-8f && normal.normalized.y < 0.8f;
            b.AddFlatTriangle(steep ? 1 : 0, a, c1, c2, 0.22f);
        }

        // ================================================================== Dağlar

        /// <summary>Sise karışan dört katman alacakaranlık dağ sırtı (uzaktakiler karlı).</summary>
        public static void BuildMountains(Context ctx)
        {
            var root = new GameObject("Dağlar").transform;
            root.SetParent(ctx.Root, false);

            BuildRidge(ctx, root, "Sırt1", 138f, 70f, 380f, -42f, 16f, 44f, 999f, 11, Mat(MaterialId.DryGrass), Mat(MaterialId.Rock));
            BuildRidge(ctx, root, "Sırt2", 205f, 90f, 500f, -44f, 34f, 78f, 999f, 23, Mat(MaterialId.Rock), Mat(MaterialId.RockDark));
            BuildRidge(ctx, root, "Sırt3", 290f, 110f, 640f, -46f, 58f, 112f, 50f, 37, Mat(MaterialId.RockDark), Mat(MaterialId.Snow));
            BuildRidge(ctx, root, "Sırt4", 400f, 140f, 820f, -48f, 92f, 165f, 82f, 51, Mat(MaterialId.RockDark), Mat(MaterialId.Snow));
        }

        private static void BuildRidge(Context ctx, Transform parent, string name, float zCenter, float depth, float halfWidth, float baseY,
            float minPeak, float maxPeak, float snowLine, int seed, Material lower, Material upper)
        {
            const int nx = 52;
            const int nz = 7;
            var cellX = halfWidth * 2f / nx;
            var cellZ = depth / nz;
            var points = new Vector3[(nx + 1) * (nz + 1)];
            var rng = new System.Random(seed * 7919);

            for (var j = 0; j <= nz; j++)
            {
                var zf = j / (float)nz;
                // Sırt çizgisi biraz öne kaymış (izleyiciye bakan yamaç daha dik).
                var profile = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(zf, 0.85f)), 0.75f);
                for (var i = 0; i <= nx; i++)
                {
                    var x = -halfWidth + i * cellX;
                    var z = zCenter - depth * 0.5f + j * cellZ;
                    var interior = i > 0 && i < nx && j > 0 && j < nz;
                    if (interior)
                    {
                        x += ((float)rng.NextDouble() - 0.5f) * cellX * 0.55f;
                        z += ((float)rng.NextDouble() - 0.5f) * cellZ * 0.5f;
                    }

                    var peak = minPeak + (maxPeak - minPeak) * RidgeNoise(x, seed);
                    var detail = (Mathf.PerlinNoise(x * 0.045f + seed, z * 0.045f + seed * 0.5f) - 0.5f) * minPeak * 0.45f;
                    var y = baseY + (peak + detail) * profile;
                    points[j * (nx + 1) + i] = new Vector3(x, y, z);
                }
            }

            var b = new MeshBuilder(2);
            for (var j = 0; j < nz; j++)
            {
                for (var i = 0; i < nx; i++)
                {
                    var p00 = points[j * (nx + 1) + i];
                    var p10 = points[j * (nx + 1) + i + 1];
                    var p01 = points[(j + 1) * (nx + 1) + i];
                    var p11 = points[(j + 1) * (nx + 1) + i + 1];
                    AddRidgeTriangle(b, p00, p01, p11, snowLine);
                    AddRidgeTriangle(b, p00, p11, p10, snowLine);
                }
            }

            var mesh = ctx.Own(b.ToMesh("HK_MenuRidge_" + name));
            MeshObject(name, parent, mesh, new[] { lower, upper }, Vector3.zero, Quaternion.identity, Vector3.one, ShadowCastingMode.Off, false);
        }

        private static void AddRidgeTriangle(MeshBuilder b, Vector3 a, Vector3 c1, Vector3 c2, float snowLine)
        {
            var maxY = Mathf.Max(a.y, Mathf.Max(c1.y, c2.y));
            var avgY = (a.y + c1.y + c2.y) / 3f;
            int submesh;
            if (snowLine < 900f)
                submesh = avgY > snowLine || maxY > snowLine + 22f ? 1 : 0;
            else
            {
                var normal = Vector3.Cross(c1 - a, c2 - a);
                submesh = normal.sqrMagnitude > 1e-8f && normal.normalized.y < 0.62f ? 1 : 0;
            }

            b.AddFlatTriangle(submesh, a, c1, c2, 0.05f);
        }

        private static float RidgeNoise(float x, int seed)
        {
            var broad = Mathf.PerlinNoise(x * 0.0042f + seed * 13.1f, seed * 0.37f);
            var ridged = 1f - Mathf.Abs(Mathf.PerlinNoise(x * 0.011f + seed * 3.7f, seed * 1.9f) * 2f - 1f);
            var fine = Mathf.PerlinNoise(x * 0.032f + seed, seed * 2.3f);
            return Mathf.Clamp01(broad * 0.55f + ridged * 0.35f + fine * 0.15f);
        }

        // ================================================================== Bitki örtüsü ve kayalar

        /// <summary>Yamaçlara çam ağaçları, plato kenarına çalı ve kayalar.</summary>
        public static void BuildVegetation(Context ctx)
        {
            var root = new GameObject("Bitki").transform;
            root.SetParent(ctx.Root, false);

            var treeMaterials = new[] { Mat(MaterialId.Bark), Mat(MaterialId.PineNeedles) };
            var deadMaterials = new[] { Mat(MaterialId.DeadWood), Mat(MaterialId.DeadWood) };
            var pines = new[] { MeshFactory.PineTree(10f, 1), MeshFactory.PineTree(10f, 2), MeshFactory.PineTree(10f, 3) };

            var placed = 0;
            for (var attempt = 0; attempt < 900 && placed < 95; attempt++)
            {
                var x = ctx.Range(-128f, 132f);
                var z = ctx.Range(-26f, 170f);
                var r = PlateauDistance(x, z);
                if (r < PlateauRadius + 5f)
                    continue;

                // Ağaçlar yamaçlarda kümelensin: gürültü eşiği.
                if (Mathf.PerlinNoise(x * 0.03f + 5f, z * 0.03f + 9f) < 0.42f)
                    continue;

                var y = GroundHeight(x, z);
                var scale = ctx.Range(0.65f, 1.35f);
                var dead = ctx.Rng.NextDouble() < 0.06;
                var mesh = dead ? MeshFactory.DeadTree(8f, placed) : pines[placed % pines.Length];
                var near = r < 45f;
                MeshObject(dead ? "KuruAğaç" : "Çam", root, mesh, dead ? deadMaterials : treeMaterials, new Vector3(x, y - 0.25f, z),
                    Quaternion.Euler(ctx.Range(-3f, 3f), ctx.Range(0f, 360f), ctx.Range(-3f, 3f)), Vector3.one * scale,
                    near ? ShadowCastingMode.On : ShadowCastingMode.Off, near);
                placed++;
            }

            // Plato kenarında çalılar.
            var bushMaterials = new[] { Mat(MaterialId.Bark), Mat(MaterialId.FoliageDark) };
            for (var i = 0; i < 10; i++)
            {
                var angle = ctx.Range(0f, Mathf.PI * 2f);
                var radius = ctx.Range(PlateauRadius - 1.5f, PlateauRadius + 4f);
                var x = PlateauCenter.x + Mathf.Sin(angle) * radius / 0.75f;
                var z = PlateauCenter.z + Mathf.Cos(angle) * radius;
                if (z < -6f && Mathf.Abs(x + 2f) < 5f)
                    continue;   // Kamera önünü kapatma.
                MeshObject("Çalı", root, MeshFactory.Bush(ctx.Range(0.6f, 1.2f), i), bushMaterials, new Vector3(x, GroundHeight(x, z) - 0.05f, z),
                    Quaternion.Euler(0f, ctx.Range(0f, 360f), 0f), Vector3.one, ShadowCastingMode.On);
            }

            // Kayalar: plato kenarı ve yamaçlar (+ sol önde çerçeveleyen büyük kaya).
            var rock = new[] { Mat(MaterialId.Rock) };
            var rockDark = new[] { Mat(MaterialId.RockDark) };
            MeshObject("BüyükKaya", root, MeshFactory.Rock(91), rockDark, new Vector3(-7.5f, 0.35f, -1.5f), Quaternion.Euler(0f, 40f, 0f),
                new Vector3(2.4f, 1.6f, 2.0f), ShadowCastingMode.On);
            MeshObject("Kaya", root, MeshFactory.Rock(92), rock, new Vector3(-5.6f, 0.15f, 0.6f), Quaternion.Euler(0f, 110f, 0f),
                new Vector3(0.9f, 0.6f, 0.8f), ShadowCastingMode.On);
            for (var i = 0; i < 26; i++)
            {
                var x = ctx.Range(-60f, 70f);
                var z = ctx.Range(-14f, 80f);
                var r = PlateauDistance(x, z);
                if (r < PlateauRadius - 0.5f)
                    continue;
                var s = ctx.Range(0.6f, r > 30f ? 4.5f : 2.2f);
                MeshObject("Kaya", root, MeshFactory.Rock(100 + i), i % 3 == 0 ? rockDark : rock, new Vector3(x, GroundHeight(x, z) + s * 0.15f, z),
                    Quaternion.Euler(ctx.Range(-8f, 8f), ctx.Range(0f, 360f), ctx.Range(-8f, 8f)), new Vector3(s, s * ctx.Range(0.5f, 0.85f), s * ctx.Range(0.7f, 1.1f)),
                    r < 40f ? ShadowCastingMode.On : ShadowCastingMode.Off);
            }
        }

        // ================================================================== Siper

        /// <summary>Platoyu vadiden ayıran, hafif kavisli kum torbası siperi (tek mesh).</summary>
        public static void BuildSandbagWall(Context ctx)
        {
            const float bagLength = 0.58f;
            const float bagHeight = 0.17f;
            const float bagDepth = 0.36f;
            const int rows = 6;
            const float startAngle = -31f * Mathf.Deg2Rad;
            const float endAngle = 30f * Mathf.Deg2Rad;

            var bag = MeshFactory.Sphere(0.5f, 9, 5);
            var b = new MeshBuilder();
            for (var row = 0; row < rows; row++)
            {
                var radius = WallRadius - row * 0.035f;   // Üst sıralar hafif içe basamaklı.
                var arcLength = (endAngle - startAngle) * radius;
                var offset = (row & 1) * bagLength * 0.5f;
                for (var s = offset; s <= arcLength; s += bagLength)
                {
                    var angle = startAngle + s / radius;
                    var position = WallCenter + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * (radius + ctx.Range(-0.03f, 0.03f));
                    position.y = bagHeight * 0.5f + row * bagHeight * 0.9f;
                    var yaw = angle * Mathf.Rad2Deg + 90f + ctx.Range(-6f, 6f);
                    var rotation = Quaternion.Euler(ctx.Range(-3f, 3f), yaw, ctx.Range(-4f, 4f));
                    var scale = new Vector3(bagLength * ctx.Range(1.0f, 1.1f), bagHeight * ctx.Range(1.0f, 1.12f), bagDepth * ctx.Range(0.92f, 1.05f));
                    b.Append(bag, Matrix4x4.TRS(position, rotation, scale), 0);
                }
            }

            var mesh = ctx.Own(b.ToMesh("HK_MenuSandbags"));
            MeshObject("KumTorbaları", ctx.Root, mesh, new[] { Mat(MaterialId.Sandbag) }, Vector3.zero, Quaternion.identity, Vector3.one, ShadowCastingMode.On);
        }

        // ================================================================== Kirpi

        /// <summary>Kirpi benzeri 4x4 zırhlı araç (bloklardan, tek mesh, 7 alt mesh).</summary>
        public static GameObject BuildVehicle(Context ctx)
        {
            const int body = 0, dark = 1, tire = 2, glass = 3, metal = 4, lamp = 5, flag = 6;
            var b = new MeshBuilder(7);

            // Gövde: alt V-gövde, mürettebat bölmesi, motor kaputu.
            MeshFactory.AddBox(b, body, new Vector3(0f, 1.05f, 0.05f), new Vector3(1.9f, 0.62f, 5.7f));
            MeshFactory.AddBox(b, dark, new Vector3(-0.45f, 0.7f, 0.05f), new Vector3(1.02f, 0.1f, 5.3f), Quaternion.Euler(0f, 0f, -24f));
            MeshFactory.AddBox(b, dark, new Vector3(0.45f, 0.7f, 0.05f), new Vector3(1.02f, 0.1f, 5.3f), Quaternion.Euler(0f, 0f, 24f));
            MeshFactory.AddBox(b, body, new Vector3(0f, 1.98f, -0.55f), new Vector3(2.46f, 1.26f, 4.5f));
            MeshFactory.AddBox(b, body, new Vector3(0f, 1.6f, 2.22f), new Vector3(2.3f, 0.55f, 1.5f), Quaternion.Euler(6f, 0f, 0f));
            MeshFactory.AddBox(b, dark, new Vector3(0f, 1.9f, 1.5f), new Vector3(2.3f, 0.08f, 0.12f));

            // Ön ızgara, tampon, farlar, ön cam.
            MeshFactory.AddBox(b, dark, new Vector3(0f, 1.22f, 2.98f), new Vector3(2.0f, 0.5f, 0.12f));
            MeshFactory.AddBox(b, dark, new Vector3(0f, 0.9f, 3.05f), new Vector3(2.36f, 0.2f, 0.22f));
            for (var i = -3; i <= 3; i++)
                MeshFactory.AddBox(b, metal, new Vector3(i * 0.24f, 1.24f, 3.05f), new Vector3(0.05f, 0.38f, 0.04f));
            MeshFactory.AddBox(b, lamp, new Vector3(-0.84f, 1.5f, 3.0f), new Vector3(0.26f, 0.15f, 0.06f));
            MeshFactory.AddBox(b, lamp, new Vector3(0.84f, 1.5f, 3.0f), new Vector3(0.26f, 0.15f, 0.06f));
            MeshFactory.AddBox(b, glass, new Vector3(-0.57f, 2.16f, 1.76f), new Vector3(0.96f, 0.62f, 0.05f), Quaternion.Euler(-18f, 0f, 0f));
            MeshFactory.AddBox(b, glass, new Vector3(0.57f, 2.16f, 1.76f), new Vector3(0.96f, 0.62f, 0.05f), Quaternion.Euler(-18f, 0f, 0f));
            MeshFactory.AddBox(b, dark, new Vector3(0f, 2.16f, 1.76f), new Vector3(0.12f, 0.66f, 0.07f), Quaternion.Euler(-18f, 0f, 0f));

            // Yan pencereler, basamaklar, aynalar.
            for (var side = -1; side <= 1; side += 2)
            {
                MeshFactory.AddBox(b, glass, new Vector3(side * 1.24f, 2.22f, -1.9f), new Vector3(0.04f, 0.3f, 0.48f));
                MeshFactory.AddBox(b, glass, new Vector3(side * 1.24f, 2.22f, -0.75f), new Vector3(0.04f, 0.3f, 0.48f));
                MeshFactory.AddBox(b, glass, new Vector3(side * 1.24f, 2.22f, 0.6f), new Vector3(0.04f, 0.3f, 0.48f));
                MeshFactory.AddBox(b, dark, new Vector3(side * 1.1f, 0.8f, -0.6f), new Vector3(0.3f, 0.06f, 1.2f));
                MeshFactory.AddBox(b, dark, new Vector3(side * 1.4f, 2.1f, 1.6f), new Vector3(0.18f, 0.04f, 0.04f));
                MeshFactory.AddBox(b, dark, new Vector3(side * 1.48f, 2.18f, 1.6f), new Vector3(0.05f, 0.28f, 0.18f));

                // Çamurluklar ve tekerlekler.
                for (var axle = -1; axle <= 1; axle += 2)
                {
                    var z = axle * 1.95f;
                    MeshFactory.AddBox(b, body, new Vector3(side * 1.2f, 1.2f, z), new Vector3(0.5f, 0.1f, 1.3f));
                    var wheelRotation = Quaternion.Euler(0f, 0f, -90f * side);
                    MeshFactory.AddFrustum(b, tire, new Vector3(side * 0.98f, 0.55f, z), wheelRotation, 0.55f, 0.55f, 0.44f, 14, true, true);
                    MeshFactory.AddFrustum(b, metal, new Vector3(side * 1.4f, 0.55f, z), wheelRotation, 0.26f, 0.2f, 0.08f, 8, true, false);
                }
            }

            // Tavan: kapaklar, taret halkası, kalkan, makineli tüfek, ekipman sepeti, antenler.
            MeshFactory.AddBox(b, dark, new Vector3(-0.66f, 2.63f, 0.95f), new Vector3(0.62f, 0.05f, 0.62f));
            MeshFactory.AddBox(b, dark, new Vector3(0.66f, 2.63f, 0.95f), new Vector3(0.62f, 0.05f, 0.62f));
            MeshFactory.AddFrustum(b, dark, new Vector3(0f, 2.6f, -0.75f), Quaternion.identity, 0.52f, 0.48f, 0.18f, 16, true, false);
            MeshFactory.AddBox(b, body, new Vector3(0f, 3.02f, -0.36f), new Vector3(0.98f, 0.55f, 0.06f), Quaternion.Euler(-8f, 0f, 0f));
            MeshFactory.AddBox(b, body, new Vector3(-0.5f, 2.98f, -0.62f), new Vector3(0.06f, 0.45f, 0.5f), Quaternion.Euler(0f, -12f, 0f));
            MeshFactory.AddBox(b, body, new Vector3(0.5f, 2.98f, -0.62f), new Vector3(0.06f, 0.45f, 0.5f), Quaternion.Euler(0f, 12f, 0f));
            MeshFactory.AddBox(b, metal, new Vector3(0f, 2.9f, -0.72f), new Vector3(0.15f, 0.17f, 0.62f));
            MeshFactory.AddFrustum(b, metal, new Vector3(0f, 2.92f, -0.42f), Quaternion.Euler(90f, 0f, 0f), 0.032f, 0.03f, 1.05f, 6, true, false);
            MeshFactory.AddBox(b, metal, new Vector3(0.12f, 2.84f, -0.7f), new Vector3(0.1f, 0.12f, 0.2f));
            MeshFactory.AddBox(b, dark, new Vector3(0f, 2.67f, -2.2f), new Vector3(2.1f, 0.1f, 0.95f));
            MeshFactory.AddBox(b, dark, new Vector3(0f, 2.8f, -2.2f), new Vector3(1.4f, 0.2f, 0.6f));
            MeshFactory.AddFrustum(b, metal, new Vector3(0.98f, 2.6f, -2.55f), Quaternion.Euler(-7f, 0f, 5f), 0.014f, 0.006f, 2.5f, 4, false, false);
            MeshFactory.AddFrustum(b, metal, new Vector3(-0.98f, 2.6f, -2.55f), Quaternion.Euler(-7f, 0f, -5f), 0.014f, 0.006f, 2.5f, 4, false, false);

            // Arka kapı ve yedek lastik.
            MeshFactory.AddBox(b, dark, new Vector3(0.1f, 1.9f, -2.81f), new Vector3(0.8f, 1.05f, 0.06f));
            MeshFactory.AddFrustum(b, tire, new Vector3(-0.82f, 1.85f, -2.81f), Quaternion.Euler(-90f, 0f, 0f), 0.45f, 0.45f, 0.3f, 12, true, true);

            // Türk bayrağı arması (sağ yan, kameraya dönük; doku düz okunur).
            const float fx = 1.236f, fy0 = 1.5f, fy1 = 1.9f, fz0 = 0.2f, fz1 = 0.8f;
            b.AddQuad(flag, new Vector3(fx, fy0, fz0), new Vector3(fx, fy1, fz0), new Vector3(fx, fy1, fz1), new Vector3(fx, fy0, fz1),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));
            const float lx = -1.236f;
            b.AddQuad(flag, new Vector3(lx, fy0, fz1), new Vector3(lx, fy1, fz1), new Vector3(lx, fy1, fz0), new Vector3(lx, fy0, fz0),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));

            var mesh = ctx.Own(b.ToMesh("HK_MenuKirpi"));
            var lampMaterial = SafeUnlit(new Color(1f, 0.92f, 0.72f));
            var materials = new[]
            {
                Mat(MaterialId.VehicleOlive), Mat(MaterialId.VehicleDark), Mat(MaterialId.Tire), Mat(MaterialId.Windshield),
                Mat(MaterialId.GunMetal), lampMaterial, Mat(MaterialId.TurkishFlag)
            };
            return MeshObject("Kirpi", ctx.Root, mesh, materials, VehiclePosition, Quaternion.Euler(0f, VehicleYaw, 0f), Vector3.one, ShadowCastingMode.On);
        }

        private static Material SafeUnlit(Color color)
        {
            try
            {
                return MaterialLibrary.Unlit(color);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MenuBackdrop] Işıksız malzeme alınamadı: " + e.Message);
                return null;
            }
        }

        // ================================================================== Kamp malzemeleri

        /// <summary>Cephane sandıkları, bidonlar, telsiz, HESCO bariyerleri ve çadır.</summary>
        public static void BuildProps(Context ctx)
        {
            const int wood = 0, band = 1, olive = 2, black = 3, hesco = 4;
            var b = new MeshBuilder(5);

            // Oturulan sandık (makineli tüfekçinin altında) ve araç yanındaki sandık yığını.
            AddCrate(b, wood, band, new Vector3(1.4f, 0.21f, 2.2f), new Vector3(0.76f, 0.42f, 0.44f), 116.6f);
            AddCrate(b, wood, band, new Vector3(5.5f, 0.26f, 0.35f), new Vector3(0.92f, 0.52f, 0.56f), 14f);
            AddCrate(b, wood, band, new Vector3(5.55f, 0.75f, 0.38f), new Vector3(0.8f, 0.46f, 0.5f), 24f);
            AddCrate(b, wood, band, new Vector3(4.6f, 0.21f, -0.35f), new Vector3(0.76f, 0.42f, 0.44f), -32f);

            // Metal cephane kutuları.
            MeshFactory.AddBox(b, olive, new Vector3(4.35f, 0.13f, 1.55f), new Vector3(0.3f, 0.26f, 0.16f), Quaternion.Euler(0f, 40f, 0f));
            MeshFactory.AddBox(b, olive, new Vector3(4.62f, 0.13f, 1.4f), new Vector3(0.3f, 0.26f, 0.16f), Quaternion.Euler(0f, 52f, 0f));
            MeshFactory.AddBox(b, band, new Vector3(4.35f, 0.275f, 1.55f), new Vector3(0.12f, 0.03f, 0.05f), Quaternion.Euler(0f, 40f, 0f));

            // Bidonlar.
            MeshFactory.AddBox(b, olive, new Vector3(6.35f, 0.24f, -0.1f), new Vector3(0.18f, 0.47f, 0.34f), Quaternion.Euler(0f, 70f, 0f));
            MeshFactory.AddBox(b, olive, new Vector3(6.55f, 0.24f, 0.18f), new Vector3(0.18f, 0.47f, 0.34f), Quaternion.Euler(0f, 64f, 0f));

            // Sandık üstünde telsiz + anteni.
            MeshFactory.AddBox(b, black, new Vector3(5.55f, 1.09f, 0.4f), new Vector3(0.34f, 0.22f, 0.26f), Quaternion.Euler(0f, 24f, 0f));
            MeshFactory.AddBox(b, band, new Vector3(5.5f, 1.12f, 0.27f), new Vector3(0.2f, 0.08f, 0.02f), Quaternion.Euler(0f, 24f, 0f));
            MeshFactory.AddFrustum(b, black, new Vector3(5.66f, 1.2f, 0.46f), Quaternion.Euler(0f, 0f, -6f), 0.008f, 0.004f, 1.1f, 4, false, false);

            // HESCO bariyer hattı (aracın arkasında).
            for (var i = 0; i < 4; i++)
            {
                var t = i / 3f;
                var position = Vector3.Lerp(new Vector3(10.2f, 0.575f, 6.1f), new Vector3(13.6f, 0.575f, 7.7f), t);
                MeshFactory.AddBox(b, hesco, position, new Vector3(1.02f, 1.15f, 1.02f), Quaternion.Euler(0f, 25f + ctx.Range(-3f, 3f), 0f));
            }

            var mesh = ctx.Own(b.ToMesh("HK_MenuProps"));
            var materials = new[] { Mat(MaterialId.WoodDark), Mat(MaterialId.GunMetal), Mat(MaterialId.VehicleOlive), Mat(MaterialId.Black), Mat(MaterialId.Hesco) };
            MeshObject("Malzemeler", ctx.Root, mesh, materials, Vector3.zero, Quaternion.identity, Vector3.one, ShadowCastingMode.On);

            BuildTent(ctx, new Vector3(-4.9f, 0f, 3.6f), 72f);
        }

        private static void AddCrate(MeshBuilder b, int body, int band, Vector3 center, Vector3 size, float yaw)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            MeshFactory.AddBox(b, body, center, size, rotation);
            MeshFactory.AddBox(b, band, center + rotation * new Vector3(0f, size.y * 0.5f, 0f), new Vector3(size.x * 1.03f, 0.03f, size.z * 1.03f), rotation);
            MeshFactory.AddBox(b, band, center + rotation * new Vector3(-size.x * 0.32f, 0f, 0f), new Vector3(0.04f, size.y * 1.02f, size.z * 1.04f), rotation);
            MeshFactory.AddBox(b, band, center + rotation * new Vector3(size.x * 0.32f, 0f, 0f), new Vector3(0.04f, size.y * 1.02f, size.z * 1.04f), rotation);
        }

        private static void BuildTent(Context ctx, Vector3 position, float yaw)
        {
            const float length = 3.6f, width = 2.8f, height = 2.0f, eave = 0.15f;
            var hl = length * 0.5f;
            var hw = width * 0.5f + eave;
            var b = new MeshBuilder(2);

            var f0 = new Vector3(-hw, 0.05f, hl);
            var f1 = new Vector3(hw, 0.05f, hl);
            var fTop = new Vector3(0f, height, hl + 0.05f);
            var b0 = new Vector3(-hw, 0.05f, -hl);
            var b1 = new Vector3(hw, 0.05f, -hl);
            var bTop = new Vector3(0f, height, -hl - 0.05f);

            AddQuadFacing(b, 0, b0, bTop, fTop, f0, new Vector3(-1f, 1f, 0f), 0.5f);
            AddQuadFacing(b, 0, f1, fTop, bTop, b1, new Vector3(1f, 1f, 0f), 0.5f);
            AddTriangleFacing(b, 0, b0, b1, bTop, Vector3.back, 0.5f);
            AddTriangleFacing(b, 0, f0, fTop, f1, Vector3.forward, 0.5f);

            // Ön kapı aralığı (koyu üçgen) ve direkler.
            AddTriangleFacing(b, 1, new Vector3(-0.45f, 0.06f, hl + 0.02f), new Vector3(0f, 1.3f, hl + 0.04f), new Vector3(0.45f, 0.06f, hl + 0.02f), Vector3.forward);
            MeshFactory.AddFrustum(b, 1, new Vector3(0f, 0f, hl + 0.1f), Quaternion.identity, 0.03f, 0.03f, height + 0.15f, 5, true, false);
            MeshFactory.AddFrustum(b, 1, new Vector3(0f, 0f, -hl - 0.1f), Quaternion.identity, 0.03f, 0.03f, height + 0.15f, 5, true, false);

            var mesh = ctx.Own(b.ToMesh("HK_MenuTent"));
            MeshObject("Çadır", ctx.Root, mesh, new[] { Mat(MaterialId.TentCanvas), Mat(MaterialId.WoodDark) }, position, Quaternion.Euler(0f, yaw, 0f),
                Vector3.one, ShadowCastingMode.On);
        }

        // ================================================================== Bayrak direği

        /// <summary>Türk bayrağı direği (siperin arkasında); dalgalanan kumaşı döndürür.</summary>
        public static MenuFlagCloth BuildFlagPole(Context ctx)
        {
            const float poleHeight = 6.8f;
            var pole = new GameObject("BayrakDireği").transform;
            pole.SetParent(ctx.Root, false);
            pole.localPosition = FlagPolePosition;
            pole.localRotation = Quaternion.Euler(0f, -14f, 0f);

            var b = new MeshBuilder(2);
            MeshFactory.AddFrustum(b, 0, Vector3.zero, Quaternion.identity, 0.055f, 0.035f, poleHeight, 8, true, true);
            MeshFactory.AddFrustum(b, 0, Vector3.zero, Quaternion.identity, 0.2f, 0.16f, 0.25f, 8, true, false);   // Kaide
            MeshFactory.AddFrustum(b, 1, new Vector3(0f, poleHeight, 0f), Quaternion.identity, 0.07f, 0.0f, 0.18f, 8, true, false);   // Alem
            var mesh = ctx.Own(b.ToMesh("HK_MenuFlagPole"));
            var gold = SafeLit(new Color(0.85f, 0.68f, 0.28f), 0.6f, 0.8f);
            MeshObject("Direk", pole, mesh, new[] { Mat(MaterialId.MetalPanel), gold }, Vector3.zero, Quaternion.identity, Vector3.one, ShadowCastingMode.On);

            var clothRoot = new GameObject("Bayrak").transform;
            clothRoot.SetParent(pole, false);
            clothRoot.localPosition = new Vector3(0.04f, poleHeight - 0.08f, 0f);
            clothRoot.localRotation = Quaternion.identity;
            return MenuFlagCloth.Create(clothRoot, 2.25f, 1.5f, Mat(MaterialId.TurkishFlag));
        }

        private static Material SafeLit(Color color, float smoothness, float metallic)
        {
            try
            {
                return MaterialLibrary.Lit(color, smoothness, metallic);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MenuBackdrop] Malzeme alınamadı: " + e.Message);
                return null;
            }
        }

        // ================================================================== Askerler

        /// <summary>Ateş başında bekleyen dört asker (komutan, makineli tüfekçi, keskin nişancı, nöbetçi).</summary>
        public static List<SoldierPose> BuildSoldiers(Context ctx)
        {
            var poses = new List<SoldierPose>(4);
            var rng = new System.Random(1920);

            TrySpawn(ctx, poses, rng, "TimKomutanı", new Vector3(3.95f, 0f, 2.55f), 214f, WeaponIds.Mpt76, MilitaryRank.Yuzbasi,
                Stance.Standing, false, true, 2, 2, 1, 8f, 5f, 0.21f);
            TrySpawn(ctx, poses, rng, "MakineliTüfekçi", new Vector3(1.4f, 0f, 2.2f), 116.6f, WeaponIds.Pmt76, MilitaryRank.UzmanCavus,
                Stance.Standing, true, false, 2, 2, 2, 18f, 3f, 0.17f);
            TrySpawn(ctx, poses, rng, "KeskinNişancı", new Vector3(0.55f, 0f, 5.55f), 6f, WeaponIds.Jng90, MilitaryRank.AstsubayKidemliCavus,
                Stance.Crouching, false, false, 1, 1, 1, -2f, 1.5f, 0.09f);
            TrySpawn(ctx, poses, rng, "Nöbetçi", new Vector3(5.65f, 0f, 4.55f), 27f, WeaponIds.Mpt55, MilitaryRank.SozlesmeliEr,
                Stance.Standing, false, false, 2, 1, 2, 2f, 6f, 0.07f);
            return poses;
        }

        private static void TrySpawn(Context ctx, List<SoldierPose> poses, System.Random rng, string name, Vector3 position, float yaw, string weaponId,
            MilitaryRank rank, Stance stance, bool seated, bool beret, int helmet, int vest, int backpack, float pitch, float pitchAmplitude, float pitchSpeed)
        {
            try
            {
                var holder = new GameObject(name).transform;
                holder.SetParent(ctx.Root, false);
                holder.localPosition = position;
                holder.localRotation = Quaternion.Euler(0f, yaw, 0f);

                var look = SoldierLook.ForTeam(0, rng);
                look.Beret = beret;
                var model = SoldierModel.Build(holder, look, null, false, GameLayers.Default);
                if (model == null)
                    return;

                model.AutoSyncEquipment = false;
                model.AutoPlayDeath = false;
                model.SetEquipment(helmet, vest, backpack);
                model.SetRank(rank);
                model.SetSeated(seated);
                model.SetLocomotion(Vector3.zero, stance, true);
                model.SetAimPitch(pitch);

                if (WeaponCatalog.TryGet(weaponId, out var weapon) && weapon != null)
                {
                    try
                    {
                        model.HoldWeapon(weapon);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[MenuBackdrop] Silah modeli kurulamadı (" + weaponId + "): " + e.Message);
                    }
                }

                poses.Add(new SoldierPose
                {
                    Model = model,
                    Stance = stance,
                    BasePitch = pitch,
                    PitchAmplitude = pitchAmplitude,
                    PitchSpeed = pitchSpeed,
                    Phase = (float)rng.NextDouble() * 10f
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MenuBackdrop] Asker kurulamadı (" + name + "): " + e.Message);
            }
        }

        // ================================================================== Pus ve uzak çatışma

        /// <summary>Vadi tabanında yavaş sürüklenen pus bulutları.</summary>
        public static ParticleSystem BuildValleyMist(Context ctx)
        {
            var mist = CreateParticles("VadiPusu", ctx.Root, new Vector3(4f, -24f, 100f), MaterialLibrary.ParticleAlpha, 70);
            if (mist == null)
                return null;

            var fog = RenderSettings.fog ? RenderSettings.fogColor : new Color(0.84f, 0.67f, 0.52f);
            var main = mist.main;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(36f, 52f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
            main.startSize = new ParticleSystem.MinMaxCurve(34f, 62f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(fog.r, fog.g, fog.b, 0.13f), new Color(fog.r * 0.92f, fog.g * 0.9f, fog.b * 0.9f, 0.2f));

            var emission = mist.emission;
            emission.rateOverTime = 1.5f;

            var shape = mist.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(300f, 8f, 150f);

            var velocity = mist.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0.08f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

            var color = mist.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(Gradient(new[] { Color.white, Color.white }, new[] { 0f, 1f },
                new[] { 0f, 1f, 1f, 0f }, new[] { 0f, 0.25f, 0.75f, 1f }));

            var rotation = mist.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);

            mist.Play();
            return mist;
        }

        /// <summary>Uzak patlama parlamaları için yayımsız parçacık sistemi (Emit ile tetiklenir).</summary>
        public static ParticleSystem BuildFlashSystem(Context ctx)
        {
            var flashes = CreateParticles("UzakParlamalar", ctx.Root, Vector3.zero, MaterialLibrary.ParticleAdditive, 48);
            if (flashes == null)
                return null;

            var main = flashes.main;
            main.startLifetime = 0.4f;
            main.startSpeed = 0f;
            main.startSize = 10f;

            var emission = flashes.emission;
            emission.enabled = false;

            var shape = flashes.shape;
            shape.enabled = false;

            var color = flashes.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(Gradient(new[] { new Color(1f, 0.9f, 0.7f), new Color(1f, 0.45f, 0.15f) }, new[] { 0f, 1f },
                new[] { 1f, 0.55f, 0f }, new[] { 0f, 0.25f, 1f }));

            var size = flashes.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.4f));

            flashes.Play();
            return flashes;
        }

        /// <summary>Uzak iz mermisi akışları için uzatılmış (stretch) parçacık sistemi (Emit ile tetiklenir).</summary>
        public static ParticleSystem BuildTracerSystem(Context ctx)
        {
            var tracers = CreateParticles("UzakİzMermileri", ctx.Root, Vector3.zero, MaterialLibrary.ParticleAdditive, 64);
            if (tracers == null)
                return null;

            var main = tracers.main;
            main.startLifetime = 0.8f;
            main.startSpeed = 0f;
            main.startSize = 0.7f;
            main.gravityModifier = 0.15f;

            var emission = tracers.emission;
            emission.enabled = false;

            var shape = tracers.shape;
            shape.enabled = false;

            var color = tracers.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(Gradient(new[] { Color.white, Color.white }, new[] { 0f, 1f },
                new[] { 1f, 1f, 0f }, new[] { 0f, 0.7f, 1f }));

            var renderer = tracers.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.05f;
                renderer.lengthScale = 1f;
            }

            tracers.Play();
            return tracers;
        }

        /// <summary>Sahip olunan mesh'leri siler.</summary>
        public static void DestroyOwned(Context ctx)
        {
            if (ctx == null)
                return;

            for (var i = 0; i < ctx.OwnedMeshes.Count; i++)
            {
                var mesh = ctx.OwnedMeshes[i];
                if (mesh == null)
                    continue;
                if (UnityEngine.Application.isPlaying)
                    Object.Destroy(mesh);
                else
                    Object.DestroyImmediate(mesh);
            }

            ctx.OwnedMeshes.Clear();
        }
    }
}
