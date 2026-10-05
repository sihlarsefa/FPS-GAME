using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Menü dekorundaki kamp ateşi: taş halka, çatılmış odunlar, kor yatağı, alev / kıvılcım / duman parçacıkları ve
    /// titreşen turuncu nokta ışığı. Işık titreşimi Perlin gürültüsüyle hesaplanır (kare başına bellek ayırmaz).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuCampfire : MonoBehaviour
    {
        private static readonly Color FireLightColor = new Color(1f, 0.56f, 0.24f);

        private Light _light;
        private Vector3 _lightBase;
        private float _seed;
        private bool _visible = true;
        private Renderer _probe;
        private Mesh _logsMesh;

        /// <summary>Temel ışık şiddeti.</summary>
        public float BaseIntensity { get; set; } = 2.6f;

        /// <summary>Ateşin nokta ışığı (yoksa null).</summary>
        public Light FireLight => _light;

        /// <summary>Kamp ateşini <paramref name="parent"/> altında yerel <paramref name="localPosition"/> konumunda kurar.</summary>
        public static MenuCampfire Create(Transform parent, Vector3 localPosition, int seed)
        {
            var go = new GameObject("KampAteşi");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;

            var fire = go.AddComponent<MenuCampfire>();
            fire._seed = seed * 0.137f + 3.1f;
            fire.Build(seed);
            return fire;
        }

        private void Build(int seed)
        {
            var root = transform;
            var rng = new System.Random(seed);

            // Taş halka.
            var stoneMaterial = MenuBackdropBuilder.Mat(MaterialId.RockDark);
            for (var i = 0; i < 9; i++)
            {
                var angle = i / 9f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.2f;
                var radius = 0.5f + (float)rng.NextDouble() * 0.05f;
                var position = new Vector3(Mathf.Sin(angle) * radius, 0.05f, Mathf.Cos(angle) * radius);
                var scale = new Vector3(0.17f, 0.12f, 0.15f) * (0.8f + (float)rng.NextDouble() * 0.45f);
                MenuBackdropBuilder.MeshObject("Taş", root, MeshFactory.Rock(700 + i), new[] { stoneMaterial }, position,
                    Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), scale, ShadowCastingMode.On);
            }

            // Kor yatağı (parlak, ışıksız).
            var emberMaterial = SafeUnlit(new Color(0.95f, 0.36f, 0.08f));
            var bed = MenuBackdropBuilder.MeshObject("KorYatağı", root, MeshFactory.Disc(0.4f, 14), new[] { emberMaterial },
                new Vector3(0f, 0.02f, 0f), Quaternion.identity, Vector3.one, ShadowCastingMode.Off);
            if (bed != null)
                _probe = bed.GetComponent<Renderer>();

            // Çatılmış odunlar (teepee).
            var bark = MenuBackdropBuilder.Mat(MaterialId.Bark);
            var builder = new MeshBuilder();
            for (var i = 0; i < 5; i++)
            {
                var angle = i / 5f * 360f + (float)rng.NextDouble() * 20f;
                var rotation = Quaternion.Euler(0f, angle, 0f) * Quaternion.Euler(58f, 0f, 0f);   // Dış halkadan merkeze doğru eğik.
                var basePoint = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0.02f, -0.36f);
                MeshFactory.AddFrustum(builder, 0, basePoint, rotation, 0.055f, 0.035f, 0.62f, 6, true, false);
            }

            // Yanında yatan iki kütük.
            MeshFactory.AddFrustum(builder, 0, new Vector3(-0.85f, 0.07f, -0.3f), Quaternion.Euler(0f, 70f, 90f), 0.07f, 0.065f, 0.75f, 7, true, false);
            MeshFactory.AddFrustum(builder, 0, new Vector3(-0.8f, 0.07f, 0.05f), Quaternion.Euler(0f, 95f, 90f), 0.06f, 0.06f, 0.7f, 7, true, false);
            _logsMesh = builder.ToMesh("HK_MenuLogs");
            MenuBackdropBuilder.MeshObject("Odunlar", root, _logsMesh, new[] { bark }, Vector3.zero, Quaternion.identity, Vector3.one, ShadowCastingMode.On);

            BuildParticles(root);

            // Titreşen ateş ışığı.
            var lightGo = new GameObject("AteşIşığı");
            lightGo.transform.SetParent(root, false);
            _lightBase = new Vector3(0f, 0.75f, 0f);
            lightGo.transform.localPosition = _lightBase;
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = FireLightColor;
            _light.range = 9f;
            _light.intensity = BaseIntensity;
            _light.shadows = LightShadows.None;
            _light.renderMode = LightRenderMode.ForcePixel;
        }

        private static Material SafeUnlit(Color color)
        {
            try
            {
                return MaterialLibrary.Unlit(color);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[MenuCampfire] Işıksız malzeme alınamadı: " + e.Message);
                return null;
            }
        }

        private static void BuildParticles(Transform root)
        {
            // Alev.
            var flame = MenuBackdropBuilder.CreateParticles("Alev", root, new Vector3(0f, 0.08f, 0f), MenuBackdropBuilder.Mat(MaterialId.Fire), 70);
            if (flame != null)
            {
                var main = flame.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 1.05f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.45f, 1f), new Color(1f, 0.55f, 0.2f, 1f));
                main.gravityModifier = -0.08f;

                var emission = flame.emission;
                emission.rateOverTime = 34f;

                var shape = flame.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 9f;
                shape.radius = 0.16f;
                shape.rotation = new Vector3(-90f, 0f, 0f);

                var color = flame.colorOverLifetime;
                color.enabled = true;
                color.color = new ParticleSystem.MinMaxGradient(MenuBackdropBuilder.Gradient(
                    new[] { new Color(1f, 0.9f, 0.55f), new Color(1f, 0.55f, 0.18f), new Color(0.75f, 0.18f, 0.04f) },
                    new[] { 0f, 0.35f, 1f },
                    new[] { 0f, 0.95f, 0.55f, 0f },
                    new[] { 0f, 0.12f, 0.6f, 1f }));

                var size = flame.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));

                var rotation = flame.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
                flame.Play();
            }

            // Kıvılcımlar.
            var embers = MenuBackdropBuilder.CreateParticles("Kıvılcımlar", root, new Vector3(0f, 0.25f, 0f), MaterialLibrary.ParticleAdditive, 60);
            if (embers != null)
            {
                var main = embers.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.022f, 0.05f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.35f, 1f), new Color(1f, 0.45f, 0.12f, 1f));
                main.gravityModifier = -0.04f;

                var emission = embers.emission;
                emission.rateOverTime = 9f;

                var shape = embers.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 20f;
                shape.radius = 0.22f;
                shape.rotation = new Vector3(-90f, 0f, 0f);

                var noise = embers.noise;
                noise.enabled = true;
                noise.strength = new ParticleSystem.MinMaxCurve(0.45f);
                noise.frequency = 0.6f;
                noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.35f);
                noise.quality = ParticleSystemNoiseQuality.Low;

                var force = embers.forceOverLifetime;
                force.enabled = true;
                force.x = new ParticleSystem.MinMaxCurve(0.12f);
                force.y = new ParticleSystem.MinMaxCurve(0f);
                force.z = new ParticleSystem.MinMaxCurve(0.04f);

                var color = embers.colorOverLifetime;
                color.enabled = true;
                color.color = new ParticleSystem.MinMaxGradient(MenuBackdropBuilder.Gradient(
                    new[] { new Color(1f, 0.85f, 0.5f), new Color(1f, 0.35f, 0.08f) },
                    new[] { 0f, 1f },
                    new[] { 1f, 1f, 0f },
                    new[] { 0f, 0.7f, 1f }));
                embers.Play();
            }

            // Duman.
            var smoke = MenuBackdropBuilder.CreateParticles("Duman", root, new Vector3(0f, 0.8f, 0f), MaterialLibrary.ParticleAlpha, 40);
            if (smoke != null)
            {
                var main = smoke.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 7f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.19f, 0.18f, 1f), new Color(0.3f, 0.28f, 0.26f, 1f));

                var emission = smoke.emission;
                emission.rateOverTime = 3.5f;

                var shape = smoke.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 6f;
                shape.radius = 0.12f;
                shape.rotation = new Vector3(-90f, 0f, 0f);

                var force = smoke.forceOverLifetime;
                force.enabled = true;
                force.x = new ParticleSystem.MinMaxCurve(0.18f);
                force.y = new ParticleSystem.MinMaxCurve(0.02f);
                force.z = new ParticleSystem.MinMaxCurve(0.06f);

                var size = smoke.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 3.2f));

                var rotation = smoke.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

                var color = smoke.colorOverLifetime;
                color.enabled = true;
                color.color = new ParticleSystem.MinMaxGradient(MenuBackdropBuilder.Gradient(
                    new[] { Color.white, Color.white },
                    new[] { 0f, 1f },
                    new[] { 0f, 0.3f, 0.18f, 0f },
                    new[] { 0f, 0.18f, 0.6f, 1f }));
                smoke.Play();
            }
        }

        private void Update()
        {
            if (_light == null)
                return;

            if (_probe != null)
            {
                var visible = _probe.isVisible;
                if (!visible && !_visible)
                    return;
                _visible = visible;
            }

            var t = Time.time;
            var flicker = 0.78f + 0.34f * Mathf.PerlinNoise(t * 5.5f, _seed) + 0.14f * Mathf.PerlinNoise(t * 17f, _seed + 4.1f);
            _light.intensity = BaseIntensity * flicker;

            var jitter = new Vector3(
                (Mathf.PerlinNoise(t * 3.1f, _seed + 9.3f) - 0.5f) * 0.12f,
                (Mathf.PerlinNoise(t * 4.3f, _seed + 2.7f) - 0.5f) * 0.1f,
                (Mathf.PerlinNoise(t * 2.7f, _seed + 6.1f) - 0.5f) * 0.12f);
            _light.transform.localPosition = _lightBase + jitter;
        }

        private void OnDestroy()
        {
            if (_logsMesh != null)
            {
                Destroy(_logsMesh);
                _logsMesh = null;
            }
        }
    }
}
