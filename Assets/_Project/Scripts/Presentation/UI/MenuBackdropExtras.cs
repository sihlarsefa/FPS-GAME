using Project.Infrastructure;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Presentation.UI
{
    /// <summary>Ana rotoru yavaşça döndürür (lobideki bekleme devri).</summary>
    [DisallowMultipleComponent]
    public sealed class MenuRotorSpin : MonoBehaviour
    {
        public float DegreesPerSecond = 70f;

        private void Update() => transform.Rotate(0f, DegreesPerSecond * Time.unscaledDeltaTime, 0f, Space.Self);
    }

    /// <summary>
    /// Lobi dioramasının ek parçaları (alacakaranlık sahnesi): helipadda bekleyen T-70, kamuflaj ağı, kum torbası sığınağı,
    /// yer pusu ve kontur ışığı. Hepsi mevcut prosedürel yapı taşlarıyla (MeshBuilder/MeshFactory/MaterialLibrary) kurulur;
    /// üretilen mesh'ler <see cref="MenuBackdropBuilder.Context.OwnedMeshes"/>'e eklenir.
    /// </summary>
    internal static class MenuBackdropExtras
    {
        /// <summary>Helipad merkezi (yerel).</summary>
        public static readonly Vector3 HelipadCenter = new Vector3(5.5f, 0f, 14.5f);

        /// <summary>Helikopter yönü (derece).</summary>
        public const float HelicopterYaw = 142f;

        // ================================================================== Helipad ve T-70

        public static void BuildHelipad(MenuBackdropBuilder.Context ctx)
        {
            var y = MenuBackdropBuilder.GroundHeight(HelipadCenter.x, HelipadCenter.z);
            var center = new Vector3(HelipadCenter.x, y, HelipadCenter.z);

            // Beton zemin + sarı halka + "H" işareti.
            MenuBackdropBuilder.MeshObject("HelipadZemin", ctx.Root, MeshFactory.Disc(5.4f, 40), new[] { MenuBackdropBuilder.Mat(MaterialId.Concrete) },
                center + Vector3.up * 0.04f, Quaternion.identity, Vector3.one, ShadowCastingMode.Off);
            MenuBackdropBuilder.MeshObject("HelipadHalka", ctx.Root, MeshFactory.Ring(4.5f, 4.75f, 48), new[] { MenuBackdropBuilder.Mat(MaterialId.Yellow) },
                center + Vector3.up * 0.06f, Quaternion.identity, Vector3.one, ShadowCastingMode.Off);

            var mark = new MeshBuilder(1);
            MeshFactory.AddBox(mark, 0, new Vector3(-0.9f, 0f, 0f), new Vector3(0.28f, 0.02f, 2.6f));
            MeshFactory.AddBox(mark, 0, new Vector3(0.9f, 0f, 0f), new Vector3(0.28f, 0.02f, 2.6f));
            MeshFactory.AddBox(mark, 0, new Vector3(0f, 0f, 0f), new Vector3(1.6f, 0.02f, 0.28f));
            var markMesh = ctx.Own(mark.ToMesh("HK_MenuHelipadH"));
            MenuBackdropBuilder.MeshObject("HelipadH", ctx.Root, markMesh, new[] { MenuBackdropBuilder.Mat(MaterialId.Yellow) },
                center + Vector3.up * 0.07f, Quaternion.Euler(0f, HelicopterYaw, 0f), Vector3.one, ShadowCastingMode.Off);

            BuildHelicopter(ctx, center + Vector3.up * 0.04f);
        }

        private static void BuildHelicopter(MenuBackdropBuilder.Context ctx, Vector3 position)
        {
            const int body = 0, dark = 1, glass = 2, metal = 3, tire = 4;
            var b = new MeshBuilder(5);

            // Kabin, burun, motor kamburu, kuyruk, dikey/yatay kuyruk yüzeyleri.
            MeshFactory.AddBox(b, body, new Vector3(0f, 1.95f, 0.5f), new Vector3(2.3f, 1.9f, 3.8f));
            MeshFactory.AddBox(b, body, new Vector3(0f, 1.7f, 2.9f), new Vector3(1.9f, 1.3f, 1.8f), Quaternion.Euler(-10f, 0f, 0f));
            MeshFactory.AddBox(b, glass, new Vector3(0f, 2.05f, 3.62f), new Vector3(1.8f, 0.85f, 0.06f), Quaternion.Euler(-28f, 0f, 0f));
            MeshFactory.AddBox(b, glass, new Vector3(-1.0f, 2.1f, 2.7f), new Vector3(0.05f, 0.7f, 1.1f));
            MeshFactory.AddBox(b, glass, new Vector3(1.0f, 2.1f, 2.7f), new Vector3(0.05f, 0.7f, 1.1f));
            MeshFactory.AddBox(b, body, new Vector3(0f, 3.1f, -0.2f), new Vector3(1.7f, 0.75f, 2.6f));
            MeshFactory.AddBox(b, dark, new Vector3(0f, 3.0f, -1.6f), new Vector3(1.0f, 0.5f, 0.2f));
            MeshFactory.AddBox(b, body, new Vector3(0f, 2.55f, -3.4f), new Vector3(0.85f, 0.95f, 3.4f), Quaternion.Euler(4f, 0f, 0f));
            MeshFactory.AddBox(b, body, new Vector3(0f, 2.75f, -6.6f), new Vector3(0.5f, 0.7f, 3.4f), Quaternion.Euler(4f, 0f, 0f));
            MeshFactory.AddBox(b, body, new Vector3(0f, 3.5f, -8.1f), new Vector3(0.14f, 2.0f, 1.5f), Quaternion.Euler(-24f, 0f, 0f));
            MeshFactory.AddBox(b, body, new Vector3(0f, 3.0f, -7.7f), new Vector3(2.6f, 0.1f, 0.85f));

            // Yan kapı boşlukları, kapı rayı, buruna sensör.
            for (var side = -1; side <= 1; side += 2)
            {
                MeshFactory.AddBox(b, dark, new Vector3(side * 1.16f, 1.85f, 0.5f), new Vector3(0.05f, 1.15f, 1.9f));
                MeshFactory.AddBox(b, body, new Vector3(side * 1.18f, 1.4f, 0.5f), new Vector3(0.05f, 0.08f, 2.1f));
                MeshFactory.AddBox(b, metal, new Vector3(side * 0.95f, 2.9f, 0.2f), new Vector3(0.1f, 0.1f, 1.8f));
            }

            MeshFactory.AddFrustum(b, dark, new Vector3(0f, 1.4f, 3.7f), Quaternion.Euler(90f, 0f, 0f), 0.22f, 0.2f, 0.3f, 10, true, false);

            // Tekerlekler ve payandalar.
            MeshFactory.AddFrustum(b, tire, new Vector3(0.14f, 0.34f, 2.5f), Quaternion.Euler(0f, 0f, -90f), 0.34f, 0.34f, 0.16f, 12, true, true);
            MeshFactory.AddBox(b, metal, new Vector3(0f, 0.8f, 2.5f), new Vector3(0.08f, 0.9f, 0.1f));
            for (var side = -1; side <= 1; side += 2)
            {
                MeshFactory.AddFrustum(b, tire, new Vector3(side * 1.1f - 0.1f * side, 0.38f, -0.4f), Quaternion.Euler(0f, 0f, -90f), 0.38f, 0.38f, 0.2f, 12, true, true);
                MeshFactory.AddBox(b, metal, new Vector3(side * 1.05f, 0.85f, -0.4f), new Vector3(0.1f, 1.0f, 0.12f), Quaternion.Euler(0f, 0f, side * 12f));
            }

            // Kuyruk rotoru göbeği.
            MeshFactory.AddBox(b, metal, new Vector3(0.3f, 3.65f, -8.2f), new Vector3(0.06f, 1.5f, 0.12f), Quaternion.Euler(0f, 0f, 24f));

            var mesh = ctx.Own(b.ToMesh("HK_MenuT70"));
            var materials = new[]
            {
                MenuBackdropBuilder.Mat(MaterialId.HeliOlive), MenuBackdropBuilder.Mat(MaterialId.MetalDark), MenuBackdropBuilder.Mat(MaterialId.Windshield),
                MenuBackdropBuilder.Mat(MaterialId.GunMetal), MenuBackdropBuilder.Mat(MaterialId.Tire)
            };

            var rotation = Quaternion.Euler(0f, HelicopterYaw, 0f);
            var heli = MenuBackdropBuilder.MeshObject("T70", ctx.Root, mesh, materials, position, rotation, Vector3.one * 0.82f, ShadowCastingMode.On);
            if (heli == null)
                return;

            // Ana rotor: mil + 4 kanat; yavaş bekleme devri.
            var rb = new MeshBuilder(2);
            MeshFactory.AddFrustum(rb, 1, new Vector3(0f, 0f, 0f), 0.16f, 0.12f, 0.45f, 8, true, true);
            MeshFactory.AddBox(rb, 1, new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.2f, 0.5f));
            for (var i = 0; i < 4; i++)
            {
                var q = Quaternion.Euler(0f, i * 90f + 12f, 0f);
                MeshFactory.AddBox(rb, 0, q * new Vector3(0f, 0.34f, 3.6f), new Vector3(0.42f, 0.04f, 6.6f), q);
            }

            var rotorMesh = ctx.Own(rb.ToMesh("HK_MenuT70Rotor"));
            var rotor = MenuBackdropBuilder.MeshObject("T70Rotor", heli.transform, rotorMesh,
                new[] { MenuBackdropBuilder.Mat(MaterialId.RotorBlade), MenuBackdropBuilder.Mat(MaterialId.GunMetal) },
                new Vector3(0f, 3.52f, 0.2f), Quaternion.identity, Vector3.one, ShadowCastingMode.On);
            if (rotor != null)
                rotor.AddComponent<MenuRotorSpin>().DegreesPerSecond = 55f;
        }

        // ================================================================== Kamuflaj ağı

        public static void BuildCamoNet(MenuBackdropBuilder.Context ctx)
        {
            var b = new MeshBuilder(2);
            var origin = new Vector3(4.6f, 0f, -0.35f);

            // Direkler.
            var corners = new[]
            {
                new Vector3(-1.7f, 0f, -1.5f), new Vector3(1.7f, 0f, -1.5f), new Vector3(1.7f, 0f, 1.5f), new Vector3(-1.7f, 0f, 1.5f)
            };
            var heights = new[] { 2.55f, 2.45f, 2.05f, 2.15f };
            var top = new Vector3[4];
            for (var i = 0; i < 4; i++)
            {
                top[i] = origin + corners[i] + Vector3.up * heights[i];
                MeshFactory.AddFrustum(b, 1, origin + corners[i], 0.045f, 0.035f, heights[i], 6, true, false);
            }

            // Ağ yüzeyi (çift yönlü) ve sarkan kenarlar.
            Quad(b, 0, top[0], top[1], top[2], top[3]);
            for (var i = 0; i < 4; i++)
            {
                var a = top[i];
                var c = top[(i + 1) % 4];
                var drop = Vector3.down * 0.42f;
                var mid = (a + c) * 0.5f;
                Quad(b, 0, a, c, c + drop * 0.7f, a + drop * 0.55f);
                Quad(b, 0, mid - (c - a) * 0.12f, mid + (c - a) * 0.12f, mid + (c - a) * 0.12f + drop * 1.25f, mid - (c - a) * 0.12f + drop);
            }

            var mesh = ctx.Own(b.ToMesh("HK_MenuCamoNet"));
            MenuBackdropBuilder.MeshObject("KamuflajAğı", ctx.Root, mesh, new[] { MenuBackdropBuilder.Mat(MaterialId.CamoNet), MenuBackdropBuilder.Mat(MaterialId.WoodDark) },
                Vector3.zero, Quaternion.identity, Vector3.one, ShadowCastingMode.On);
        }

        private static void Quad(MeshBuilder b, int submesh, Vector3 a, Vector3 c, Vector3 d, Vector3 e)
        {
            var uv0 = new Vector2(0f, 0f);
            var uv1 = new Vector2(1f, 0f);
            var uv2 = new Vector2(1f, 1f);
            var uv3 = new Vector2(0f, 1f);
            b.AddQuad(submesh, a, c, d, e, uv0, uv1, uv2, uv3);
            b.AddQuad(submesh, e, d, c, a, uv3, uv2, uv1, uv0);
        }

        // ================================================================== Kum torbası sığınağı

        /// <summary>Helipad yanında yarım daire kum torbası sığınağı (iki sıra).</summary>
        public static void BuildSandbagNest(MenuBackdropBuilder.Context ctx)
        {
            var bag = MeshFactory.Sphere(0.5f, 9, 5);
            var b = new MeshBuilder();
            var center = new Vector3(1.2f, 0f, 9.4f);
            const float radius = 1.7f;
            for (var row = 0; row < 3; row++)
            {
                var offset = (row & 1) * 0.28f;
                for (var a = 20f + offset * 30f; a <= 200f; a += 28f)
                {
                    var rad = a * Mathf.Deg2Rad;
                    var position = center + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * radius;
                    position.y = MenuBackdropBuilder.GroundHeight(position.x, position.z) + 0.1f + row * 0.15f;
                    var rotation = Quaternion.Euler(ctx.Range(-3f, 3f), -a + 90f + ctx.Range(-5f, 5f), ctx.Range(-3f, 3f));
                    b.Append(bag, Matrix4x4.TRS(position, rotation, new Vector3(0.58f, 0.18f, 0.38f)), 0);
                }
            }

            var mesh = ctx.Own(b.ToMesh("HK_MenuSandbagNest"));
            MenuBackdropBuilder.MeshObject("KumTorbasıSığınağı", ctx.Root, mesh, new[] { MenuBackdropBuilder.Mat(MaterialId.Sandbag) },
                Vector3.zero, Quaternion.identity, Vector3.one, ShadowCastingMode.On);
        }

        // ================================================================== Yer pusu ve kontur ışığı

        /// <summary>Plato üstünde alçak, yavaş sürüklenen pus (hacimsel hissi verir).</summary>
        public static ParticleSystem BuildGroundMist(MenuBackdropBuilder.Context ctx)
        {
            var mist = MenuBackdropBuilder.CreateParticles("YerPusu", ctx.Root, new Vector3(3f, 0.7f, 6f), MaterialLibrary.ParticleAlpha, 90);
            if (mist == null)
                return null;

            var fog = RenderSettings.fog ? RenderSettings.fogColor : new Color(0.84f, 0.67f, 0.52f);
            var main = mist.main;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(16f, 26f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
            main.startSize = new ParticleSystem.MinMaxCurve(5f, 11f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(fog.r, fog.g, fog.b, 0.05f), new Color(fog.r * 0.9f, fog.g * 0.88f, fog.b * 0.9f, 0.1f));

            var emission = mist.emission;
            emission.rateOverTime = 4f;

            var shape = mist.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 1.2f, 22f);

            var velocity = mist.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0.04f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);

            var color = mist.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(MenuBackdropBuilder.Gradient(new[] { Color.white, Color.white }, new[] { 0f, 1f },
                new[] { 0f, 1f, 1f, 0f }, new[] { 0f, 0.3f, 0.7f, 1f }));

            var rotation = mist.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);

            mist.Play();
            return mist;
        }

        /// <summary>Arkadan gelen soğuk kontur ışığı (asker ve araç siluetlerini ayırır; gölgesiz).</summary>
        public static Light BuildRimLight(Transform root)
        {
            var go = new GameObject("KonturIşığı");
            go.transform.SetParent(root, false);
            go.transform.localRotation = Quaternion.Euler(16f, 188f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.52f, 0.68f, 1f);
            light.intensity = 0.55f;
            light.shadows = LightShadows.None;
            return light;
        }
    }
}
