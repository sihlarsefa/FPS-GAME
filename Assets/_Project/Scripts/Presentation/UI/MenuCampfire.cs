using Project.Infrastructure.Rendering;
using Project.Infrastructure.Vfx.Fire;
using Project.Infrastructure.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Menü dekorundaki kamp ateşi: taş halka, çatılmış odunlar, kor yatağı, yumuşak prosedürel alev parçacıkları / kıvılcım / duman parçacıkları ve
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
        private Texture2D _flameTexture;
        private Material _flameMaterial;   // Önbellekli paylaşılan (yok edilmez).
        private Material[] _emberMaterials;


        /// <summary>Temel ışık şiddeti.</summary>
        public float BaseIntensity { get; set; } = 12f;

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
            var stoneMaterial = MaterialLibrary.Lit(new Color(0.34f, 0.32f, 0.30f), 0.2f, 0f);   // Ateş ışığında okunur açık taş.
            for (var i = 0; i < 9; i++)
            {
                var angle = i / 9f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.2f;
                var radius = 0.5f + (float)rng.NextDouble() * 0.05f;
                var position = new Vector3(Mathf.Sin(angle) * radius, 0.05f, Mathf.Cos(angle) * radius);
                var scale = new Vector3(0.17f, 0.12f, 0.15f) * (0.8f + (float)rng.NextDouble() * 0.45f);
                MenuBackdropBuilder.MeshObject("Taş", root, MeshFactory.Rock(700 + i), new[] { stoneMaterial }, position,
                    Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), scale, ShadowCastingMode.On);
            }

            // Kor yatağı: yavaş nabızlı turuncu emissive disk + alttaki kömür parçaları.
            var tier = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, 3);
            var ember0 = NewEmberMaterial("HK_MenuEmberBed");
            var ember1 = NewEmberMaterial("HK_MenuEmberCoal");
            _emberMaterials = new[] { ember0, ember1 };
            var bed = MenuBackdropBuilder.MeshObject("KorYatağı", root, MeshFactory.Disc(0.4f, 14), new[] { ember0 },
                new Vector3(0f, 0.02f, 0f), Quaternion.identity, Vector3.one, ShadowCastingMode.Off);
            if (bed != null)
                _probe = bed.GetComponent<Renderer>();
            for (var i = 0; i < 7; i++)
            {
                var ang = i / 7f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.5f;
                var rad = 0.08f + (float)rng.NextDouble() * 0.22f;
                MenuBackdropBuilder.MeshObject("Kömür", root, MeshFactory.Rock(820 + i), new[] { ember1 },
                    new Vector3(Mathf.Sin(ang) * rad, 0.04f, Mathf.Cos(ang) * rad),
                    Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f),
                    new Vector3(0.09f, 0.05f, 0.08f) * (0.8f + (float)rng.NextDouble() * 0.5f), ShadowCastingMode.Off);
            }

            // Odun yığını: kabuk dokulu (Wood/WoodDark PBR) silindir kütükler, uçları kömürleşmiş koyu (alt mesh).
            var bark = MaterialLibrary.Get(MaterialId.WoodDark);
            var charred = MaterialLibrary.Lit(new Color(0.05f, 0.045f, 0.04f), 0.05f, 0f);
            var builder = new MeshBuilder(2);
            for (var i = 0; i < 5; i++)
            {
                var angle = i / 5f * 360f + (float)rng.NextDouble() * 20f;
                var rotation = Quaternion.Euler(0f, angle, 0f) * Quaternion.Euler(58f, 0f, 0f);   // Dış halkadan merkeze doğru eğik.
                var basePoint = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0.02f, -0.36f);
                AddLog(builder, basePoint, rotation, 0.055f, 0.045f, 0.62f);
            }

            // Yanında yatan iki kütük.
            AddLog(builder, new Vector3(-0.85f, 0.07f, -0.3f), Quaternion.Euler(0f, 70f, 90f), 0.07f, 0.065f, 0.75f);
            AddLog(builder, new Vector3(-0.8f, 0.07f, 0.05f), Quaternion.Euler(0f, 95f, 90f), 0.06f, 0.06f, 0.7f);
            _logsMesh = builder.ToMesh("HK_MenuLogs");
            MenuBackdropBuilder.MeshObject("Odunlar", root, _logsMesh, new[] { bark, charred }, Vector3.zero, Quaternion.identity, Vector3.one, ShadowCastingMode.On);

            BuildSparksAndSmoke(root, tier);
            BuildDriftEmbers(root);
            BuildFlameParticles(root, tier);

            // Titreşen ateş ışığı.
            var lightGo = new GameObject("AteşIşığı");
            lightGo.transform.SetParent(root, false);
            _lightBase = new Vector3(0f, 0.5f, 0f);
            lightGo.transform.localPosition = _lightBase;
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = FireLightColor;
            _light.range = 8.5f;
            _light.intensity = BaseIntensity;
            _light.shadows = LightShadows.None;
            _light.renderMode = LightRenderMode.ForcePixel;
        }

        /// <summary>Kabuk silindiri (alt mesh 0) + iki ucunda kömürleşmiş koyu halka (alt mesh 1).</summary>
        private static void AddLog(MeshBuilder b, Vector3 basePoint, Quaternion rotation, float rBottom, float rTop, float length)
        {
            const float charLen = 0.11f;
            MeshFactory.AddFrustum(b, 0, basePoint, rotation, rBottom, rTop, length, 9, true, true);
            var dir = rotation * Vector3.up;
            MeshFactory.AddFrustum(b, 1, basePoint, rotation, rBottom * 1.06f, rBottom * 1.04f, charLen, 9, true, true);
            MeshFactory.AddFrustum(b, 1, basePoint + dir * (length - charLen), rotation, rTop * 1.04f, rTop * 1.06f, charLen, 9, true, true);
        }

        /// <summary>Yumuşak kenarlı alev: prosedürel radyal gradyan+gürültü dokusu, toplamalı ParticleSystem (keskin poligon yok).</summary>
        private void BuildFlameParticles(Transform root, int tier)
        {
            CampfireFlameMath.CountsForTier(tier, out var flameMax, out _, out _);
            _flameTexture = CampfireFlameMath.BuildFlameTexture(tier >= 2 ? 128 : 64, _seed);
            _flameMaterial = MaterialLibrary.Particle(_flameTexture, true);
            var ps = MenuBackdropBuilder.CreateParticles("Alev", root, new Vector3(0f, 0.12f, 0f), _flameMaterial, flameMax);
            if (ps == null)
                return;

            var main = ps.main;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.32f, 0.55f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.6f, 0.9f), new Color(1f, 0.6f, 0.25f, 0.8f));

            var emission = ps.emission;
            emission.rateOverTime = flameMax / 0.75f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.2f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.1f)));

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.25f);
            noise.frequency = 1.1f;
            noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.8f);
            noise.quality = ParticleSystemNoiseQuality.Low;

            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(MenuBackdropBuilder.Gradient(
                new[] { new Color(1f, 0.95f, 0.75f), new Color(1f, 0.5f, 0.12f), new Color(0.6f, 0.1f, 0.03f) },
                new[] { 0f, 0.45f, 1f },
                new[] { 0f, 1f, 0.6f, 0f },
                new[] { 0f, 0.15f, 0.6f, 1f }));
            ps.Play();
        }

        /// <summary>Kor yatağının yavaş nabzı: örnek malzemelerin rengi (MaterialPropertyBlock yok).</summary>
        private void AnimateEmbers(float t)
        {
            if (_emberMaterials == null)
                return;

            for (var i = 0; i < _emberMaterials.Length; i++)
            {
                var m = _emberMaterials[i];
                if (m == null)
                    continue;
                var p = CampfireFlameMath.EmberPulse(t, _seed + i * 1.9f);
                var c = Color.Lerp(new Color(0.35f, 0.07f, 0.02f), new Color(1f, 0.42f, 0.1f), p);
                if (m.HasProperty("_BaseColor"))
                    m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Color"))
                    m.SetColor("_Color", c);
            }
        }

        private Material NewEmberMaterial(string name)
        {
            var src = SafeUnlit(new Color(0.95f, 0.36f, 0.08f));
            if (src == null)
                return null;
            return new Material(src) { name = name };
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

        /// <summary>Kadrajın sağ-alt ön planında sola süzülen 6-10 parlak kor: dünya uzayı, eklemeli, titreşen alfa.</summary>
        private static void BuildDriftEmbers(Transform root)
        {
            var ps = MenuBackdropBuilder.CreateParticles("SüzülenKorlar", root, new Vector3(0f, 0.3f, 0f), MaterialLibrary.ParticleAdditive, 10);
            if (ps == null)
                return;

            var main = ps.main;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.2f, 4.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1.4f, 0.8f, 0.35f, 1f), new Color(1.3f, 0.4f, 0.12f, 1f));
            main.gravityModifier = 0f;

            var emission = ps.emission;
            emission.rateOverTime = 2.2f;   // ~2,2 /sn x ~3,8 sn ≈ 8 canlı kor.

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.2f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            // Dünya uzayında sola (-x) ve kameraya doğru (-z) süzülür, yükselir.
            var force = ps.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.World;
            force.x = new ParticleSystem.MinMaxCurve(-0.55f, -0.3f);
            force.y = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
            force.z = new ParticleSystem.MinMaxCurve(-0.45f, -0.2f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.3f);
            noise.frequency = 0.5f;
            noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.4f);
            noise.quality = ParticleSystemNoiseQuality.Low;

            // Titreyen alfa (parlayıp sönme) + ömür sonunda kararma.
            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(MenuBackdropBuilder.Gradient(
                new[] { new Color(1f, 0.85f, 0.5f), new Color(1f, 0.3f, 0.08f) },
                new[] { 0f, 1f },
                new[] { 0f, 1f, 0.45f, 1f, 0.5f, 0.9f, 0f },
                new[] { 0f, 0.1f, 0.25f, 0.4f, 0.55f, 0.75f, 1f }));
            ps.Play();
        }

        private static void BuildSparksAndSmoke(Transform root, int tier)
        {
            CampfireFlameMath.CountsForTier(tier, out _, out var sparkMax, out var smokeMax);
            // Kıvılcımlar.
            var embers = MenuBackdropBuilder.CreateParticles("Kıvılcımlar", root, new Vector3(0f, 0.25f, 0f), MaterialLibrary.ParticleAdditive, sparkMax);
            if (embers != null)
            {
                var main = embers.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.022f, 0.05f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.35f, 1f), new Color(1f, 0.45f, 0.12f, 1f));
                main.gravityModifier = -0.04f;

                var emission = embers.emission;
                emission.rateOverTime = sparkMax / 2.5f;

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
            var smoke = MenuBackdropBuilder.CreateParticles("Duman", root, new Vector3(0f, 0.8f, 0f), MaterialLibrary.ParticleAlpha, smokeMax);
            if (smoke != null)
            {
                var main = smoke.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 7f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.19f, 0.18f, 1f), new Color(0.3f, 0.28f, 0.26f, 1f));

                var emission = smoke.emission;
                emission.rateOverTime = smokeMax / 5.8f;

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
                    new[] { 0f, 0.22f, 0.12f, 0f },
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
            AnimateEmbers(t);
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
            if (_flameTexture != null)
                Destroy(_flameTexture);
            if (_emberMaterials != null)
            {
                foreach (var m in _emberMaterials)
                {
                    if (m != null)
                        Destroy(m);
                }
            }

            if (_logsMesh != null)
            {
                Destroy(_logsMesh);
                _logsMesh = null;
            }
        }
    }
}
