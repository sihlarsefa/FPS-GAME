using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Menü dekorundaki kamp ateşi: taş halka, çatılmış odunlar, kor yatağı, şekilli alev kartları (UV kare kaydırma) / kıvılcım / duman parçacıkları ve
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
        private Mesh _cardMesh;
        private Texture2D _flameTexture;
        private Material _flameMaterial;
        private const int CardCount = 5;
        private float[] _cardScale;
        private Transform[] _cards;
        private Renderer[] _cardRenderers;
        private MaterialPropertyBlock _block;

        private const int FlameFrames = 4;
        private const int FrameWidth = 64;
        private const int FrameHeight = 128;
        private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int MainTexSt = Shader.PropertyToID("_MainTex_ST");

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

            // Kor yatağı (parlak, ışıksız).
            var emberMaterial = SafeUnlit(new Color(0.95f, 0.36f, 0.08f));
            var bed = MenuBackdropBuilder.MeshObject("KorYatağı", root, MeshFactory.Disc(0.4f, 14), new[] { emberMaterial },
                new Vector3(0f, 0.02f, 0f), Quaternion.identity, Vector3.one, ShadowCastingMode.Off);
            if (bed != null)
                _probe = bed.GetComponent<Renderer>();

            // Çatılmış odunlar (teepee).
            var bark = MaterialLibrary.Lit(new Color(0.30f, 0.19f, 0.10f), 0.15f, 0f);   // Okunur kütük kahvesi.
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

            BuildSparksAndSmoke(root);
            BuildDriftEmbers(root);
            BuildFlameCards(root);

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

        /// <summary>Çapraz 4 alev kartı: 4 kareli atlas dokusu, UV kaydırma ile kare değişimi, titreyen boy. Blob yok, dil biçimli alev.</summary>
        private void BuildFlameCards(Transform root)
        {
            _flameTexture = BuildFlameTexture();
            _flameMaterial = new Material(MaterialLibrary.ParticleAdditive) { name = "HK_MenuFlame" };
            if (_flameMaterial.HasProperty("_BaseMap"))
                _flameMaterial.SetTexture("_BaseMap", _flameTexture);
            if (_flameMaterial.HasProperty("_MainTex"))
                _flameMaterial.SetTexture("_MainTex", _flameTexture);
            if (_flameMaterial.HasProperty("_BaseColor"))
                _flameMaterial.SetColor("_BaseColor", Color.white);

            const float halfWidth = 0.26f, height = 0.42f;   // En çok 0.45 m; taban en/boy ~0.8:1.
            var b = new MeshBuilder();
            var uv0 = new Vector2(0f, 0f);
            var uv1 = new Vector2(0f, 1f);
            var uv2 = new Vector2(1f, 1f);
            var uv3 = new Vector2(1f, 0f);
            b.AddQuad(0, new Vector3(-halfWidth, 0f, 0f), new Vector3(-halfWidth, height, 0f), new Vector3(halfWidth, height, 0f), new Vector3(halfWidth, 0f, 0f), uv0, uv1, uv2, uv3);
            b.AddQuad(0, new Vector3(halfWidth, 0f, 0f), new Vector3(halfWidth, height, 0f), new Vector3(-halfWidth, height, 0f), new Vector3(-halfWidth, 0f, 0f), uv3, uv2, uv1, uv0);
            b.SanitizeNonFinite("HK_MenuFlameCard");
            _cardMesh = b.ToMesh("HK_MenuFlameCard");

            _cards = new Transform[CardCount];
            _cardRenderers = new Renderer[CardCount];
            _cardScale = new float[CardCount];
            _block = new MaterialPropertyBlock();
            for (var i = 0; i < CardCount; i++)
            {
                _cardScale[i] = i < 3 ? 1f : 0.7f;
                var go = MenuBackdropBuilder.MeshObject("AlevKartı" + i, root, _cardMesh, new[] { _flameMaterial },
                    new Vector3(i < 3 ? 0f : Mathf.Sin(i * 2.1f) * 0.1f, 0.06f, i < 3 ? 0f : Mathf.Cos(i * 2.1f) * 0.1f),
                    Quaternion.Euler(0f, i < 3 ? i * 60f + 10f : i * 90f + 40f, 0f), Vector3.one, ShadowCastingMode.Off, false);
                if (go == null)
                    continue;
                _cards[i] = go.transform;
                _cardRenderers[i] = go.GetComponent<Renderer>();
            }
        }

        /// <summary>Yumuşak, dil biçimli alev maskesi: geniş yuvarlak taban, yumuşakça incelen uç (iğne yok), tabanda parlak çekirdek.</summary>
        private static float TeardropMask(int frame, float u, float v)
        {
            var best = 0f;
            for (var k = 0; k < 2; k++)
            {
                var height = k == 0 ? 0.92f - 0.05f * Mathf.Sin(frame * 2.3f) : 0.6f + 0.06f * Mathf.Sin(frame * 1.7f);
                if (v >= height)
                    continue;
                var t = v / height;
                var profile = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.45f)) * (1f - 0.35f * t);
                var halfW = (k == 0 ? 0.34f : 0.2f) * Mathf.Max(0f, profile);
                if (halfW < 1e-3f)
                    continue;
                var cx = 0.5f + (k == 0 ? 0f : (frame % 2 == 0 ? 0.15f : -0.15f)) * (1f - t)
                         + Mathf.Sin(frame * 1.9f + k * 2.3f + t * 3.2f) * 0.05f * t;
                var d = Mathf.Abs(u - cx) / halfW;
                var m = Mathf.Clamp01(1f - d * d);   // Gauss benzeri yumuşak kenar.
                m *= 1f - t * t * t * 0.6f;
                best = Mathf.Max(best, m);
            }

            return best;
        }

        private static Texture2D BuildFlameTexture()
        {
            var tex = new Texture2D(FrameWidth * FlameFrames, FrameHeight, TextureFormat.RGBA32, false)
            {
                name = "HK_MenuFlameAtlas",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[tex.width * tex.height];
            for (var f = 0; f < FlameFrames; f++)
            {
                for (var y = 0; y < FrameHeight; y++)
                {
                    var v = (y + 0.5f) / FrameHeight;
                    for (var x = 0; x < FrameWidth; x++)
                    {
                        var u = (x + 0.5f) / FrameWidth;
                        var m = TeardropMask(f, u, v);
                        // Renk: tabanda sarı-beyaz çekirdek, ortada turuncu, uçta kızıl.
                        var heat = Mathf.Clamp01(m * (1.15f - v));
                        var c = Color.Lerp(new Color(0.85f, 0.14f, 0.03f), new Color(1f, 0.58f, 0.12f), Mathf.Clamp01(heat * 1.6f));
                        c = Color.Lerp(c, new Color(1f, 0.95f, 0.7f), Mathf.Clamp01((heat - 0.55f) * 2.2f));
                        var a = Mathf.Clamp01(m * 0.95f);
                        pixels[y * tex.width + f * FrameWidth + x] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)(a * 255f));
                    }
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private void AnimateFlames(float t)
        {
            if (_cards == null || _block == null)
                return;

            for (var i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] == null || _cardRenderers[i] == null)
                    continue;

                // UV kaydırma: atlasta kare seç (her kart farklı fazda).
                var frame = ((int)(t * 9f + i * 1.7f + _seed)) & (FlameFrames - 1);
                var st = new Vector4(1f / FlameFrames, 1f, frame / (float)FlameFrames, 0f);
                _cardRenderers[i].GetPropertyBlock(_block);
                _block.SetVector(BaseMapSt, st);
                _block.SetVector(MainTexSt, st);
                _cardRenderers[i].SetPropertyBlock(_block);

                // Titreme ±%15 (boy) ve ±%10 (en); tam boy gerilmesi yok.
                var flick = 0.85f + 0.3f * Mathf.PerlinNoise(t * 6f + i * 3.7f, _seed);
                var wide = 0.9f + 0.2f * Mathf.PerlinNoise(t * 4f, i * 2.1f + _seed);
                var s = _cardScale != null && i < _cardScale.Length ? _cardScale[i] : 1f;
                _cards[i].localScale = new Vector3(wide * s, flick * s, 1f);
            }
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

        private static void BuildSparksAndSmoke(Transform root)
        {
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
            AnimateFlames(t);
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
            if (_cardMesh != null)
                Destroy(_cardMesh);
            if (_flameTexture != null)
                Destroy(_flameTexture);
            if (_flameMaterial != null)
                Destroy(_flameMaterial);

            if (_logsMesh != null)
            {
                Destroy(_logsMesh);
                _logsMesh = null;
            }
        }
    }
}
