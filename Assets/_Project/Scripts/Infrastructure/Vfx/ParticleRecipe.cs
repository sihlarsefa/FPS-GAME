using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Vfx
{
    /// <summary>
    /// Tek bir ParticleSystem'in koddan kurulum tarifi. Şablonlar bir kez üretilir, havuz örnekleri bunlardan kopyalanır.
    /// Emisyon yönü yerel +Z'dir (koni). Şekil dönüşü <see cref="ShapeRotation"/> ile verilir (örn. (-90,0,0) = yukarı).
    /// </summary>
    internal sealed class ParticleRecipe
    {
        public string Name = "Particles";
        public Material Material;
        public ParticleSystemRenderMode RenderMode = ParticleSystemRenderMode.Billboard;
        public float VelocityScale;
        public float LengthScale = 1f;
        public float MaxParticleSize = 0.5f;
        public bool SortByDistance;
        public float SortingFudge;

        public float Duration = 0.1f;
        public float RateOverTime;
        public int BurstMin;
        public int BurstMax;
        public int MaxParticles = 32;

        public Vector2 Lifetime = new Vector2(0.5f, 1f);
        public Vector2 Speed = new Vector2(1f, 2f);
        public Vector2 Size = new Vector2(0.1f, 0.2f);
        public bool RandomRotation = true;
        public float Spin;
        public Color ColorA = Color.white;
        public Color ColorB = Color.white;
        public Gradient ColorOverLifetime;
        public AnimationCurve SizeOverLifetime;
        public float Gravity;
        public float Drag;
        public float NoiseStrength;
        public float NoiseFrequency = 0.5f;

        public bool ShapeEnabled = true;
        public ParticleSystemShapeType Shape = ParticleSystemShapeType.Cone;
        public float ShapeAngle = 25f;
        public float ShapeRadius = 0.02f;
        public float RadiusThickness = 1f;
        public Vector3 ShapeRotation;
        public Vector3 ShapePosition;

        /// <summary>En uzun parçacığın görünür kalabileceği süre (havuz meşguliyeti için).</summary>
        public float MaxLifetime => Duration + Lifetime.y;

        public ParticleSystem Build(Transform parent)
        {
            var go = new GameObject(Name);
            go.layer = GameLayers.Default;
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Apply(ps);
            return ps;
        }

        private void Apply(ParticleSystem ps)
        {
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = Mathf.Max(0.05f, Duration);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(1, MaxParticles);
            main.startLifetime = new ParticleSystem.MinMaxCurve(Lifetime.x, Lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(Speed.x, Speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(Size.x, Size.y);
            main.startRotation = RandomRotation
                ? new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f)
                : new ParticleSystem.MinMaxCurve(0f);
            main.startColor = new ParticleSystem.MinMaxGradient(ColorA, ColorB);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(Gravity);
            main.stopAction = ParticleSystemStopAction.None;
            main.cullingMode = ParticleSystemCullingMode.Automatic;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(RateOverTime);
            emission.rateOverDistance = new ParticleSystem.MinMaxCurve(0f);
            if (BurstMax > 0)
            {
                var min = (short)Mathf.Clamp(BurstMin, 0, short.MaxValue);
                var max = (short)Mathf.Clamp(Mathf.Max(BurstMin, BurstMax), 0, short.MaxValue);
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, min, max) });
            }
            else
            {
                emission.burstCount = 0;
            }

            var shape = ps.shape;
            shape.enabled = ShapeEnabled;
            if (ShapeEnabled)
            {
                shape.shapeType = Shape;
                shape.angle = ShapeAngle;
                shape.radius = Mathf.Max(0.0001f, ShapeRadius);
                shape.radiusThickness = RadiusThickness;
                shape.rotation = ShapeRotation;
                shape.position = ShapePosition;
                shape.arc = 360f;
            }

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = ColorOverLifetime != null;
            if (ColorOverLifetime != null)
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(ColorOverLifetime);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = SizeOverLifetime != null;
            if (SizeOverLifetime != null)
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, SizeOverLifetime);

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = Spin > 0f;
            if (Spin > 0f)
                rotation.z = new ParticleSystem.MinMaxCurve(-Spin, Spin);

            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = Drag > 0f;
            if (Drag > 0f)
            {
                limit.limit = new ParticleSystem.MinMaxCurve(10000f);
                limit.dampen = 0f;
                limit.drag = new ParticleSystem.MinMaxCurve(Drag);
                limit.multiplyDragByParticleSize = false;
                limit.multiplyDragByParticleVelocity = false;
            }

            var noise = ps.noise;
            noise.enabled = NoiseStrength > 0f;
            if (NoiseStrength > 0f)
            {
                noise.strength = new ParticleSystem.MinMaxCurve(NoiseStrength);
                noise.frequency = NoiseFrequency;
                noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.15f);
                noise.octaveCount = 1;
                noise.quality = ParticleSystemNoiseQuality.Low;
                noise.damping = true;
            }

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = Material;
                renderer.renderMode = RenderMode;
                renderer.velocityScale = VelocityScale;
                renderer.lengthScale = LengthScale;
                renderer.maxParticleSize = MaxParticleSize;
                renderer.minParticleSize = 0f;
                renderer.sortMode = SortByDistance ? ParticleSystemSortMode.Distance : ParticleSystemSortMode.None;
                renderer.sortingFudge = SortingFudge;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderer.alignment = ParticleSystemRenderSpace.View;
            }
        }

        // ---------------------------------------------------------------- yardımcılar

        /// <summary>Renk c0→c1, alfa anahtarları (zaman, alfa) çiftleri halinde.</summary>
        public static Gradient Gradient(Color c0, Color c1, params float[] alphaPairs)
        {
            var gradient = new Gradient();
            var alphaKeys = new GradientAlphaKey[Mathf.Max(2, alphaPairs.Length / 2)];
            if (alphaPairs.Length >= 4)
            {
                for (var i = 0; i < alphaKeys.Length; i++)
                    alphaKeys[i] = new GradientAlphaKey(alphaPairs[i * 2 + 1], alphaPairs[i * 2]);
            }
            else
            {
                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                alphaKeys[1] = new GradientAlphaKey(0f, 1f);
            }

            gradient.SetKeys(
                new[] { new GradientColorKey(c0, 0f), new GradientColorKey(c1, 1f) },
                alphaKeys);
            return gradient;
        }

        /// <summary>Üç renkli geçiş (alev: sarı → turuncu → koyu kırmızı).</summary>
        public static Gradient Gradient3(Color c0, Color c1, float t1, Color c2, params float[] alphaPairs)
        {
            var gradient = Gradient(c0, c2, alphaPairs);
            var alphaKeys = gradient.alphaKeys;
            gradient.SetKeys(
                new[] { new GradientColorKey(c0, 0f), new GradientColorKey(c1, t1), new GradientColorKey(c2, 1f) },
                alphaKeys);
            return gradient;
        }

        public static AnimationCurve Curve(float start, float end)
        {
            return AnimationCurve.Linear(0f, start, 1f, end);
        }

        /// <summary>Hızlı büyüyüp yavaşlayan eğri (duman/toz genişlemesi).</summary>
        public static AnimationCurve EaseOut(float start, float end)
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, start, (end - start) * 2f, (end - start) * 2f),
                new Keyframe(1f, end, 0f, 0f));
            return curve;
        }
    }
}
