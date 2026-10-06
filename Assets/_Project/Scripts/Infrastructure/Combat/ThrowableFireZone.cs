using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.Vfx;
using UnityEngine;

namespace Project.Infrastructure.Combat
{
    /// <summary>
    /// Molotof yangın alanı: alan engelleme. Otoritede her 0,5 sn yanma hasarı (merkezde tam, kenarda yarı), alandan çıkınca
    /// birkaç tik daha yanma sürer. Eğimde aşağı yönde biraz yayılır. Yağmur (<see cref="Rain"/>) süreyi hızla tüketir,
    /// şiddetliyse söndürür. Alev parçacıkları + titreyen ışık + GameVfx duman bulutu.
    /// </summary>
    public sealed class FireZone : MonoBehaviour
    {
        /// <summary>Yağış şiddeti 0..1. WeatherSystem her karede atar.</summary>
        public static float Rain;

        private static readonly List<FireZone> ActiveList = new();
        private readonly Dictionary<PlayerId, int> _burning = new();
        private readonly List<PlayerId> _scratch = new(8);

        private ParticleSystem _flames;
        private Light _light;
        private PlayerId _thrower;
        private float _remaining;
        private float _nextTick;
        private float _seed;

        public static IReadOnlyList<FireZone> Active => ActiveList;
        public Vector3 Center { get; private set; }
        public float Radius { get; private set; }

        /// <summary>Nokta herhangi bir etkin yangın alanının içinde mi (AI kaçınma için).</summary>
        public static bool IsInFire(Vector3 point, float margin = 0f)
        {
            for (var i = 0; i < ActiveList.Count; i++)
            {
                var z = ActiveList[i];
                if (z == null)
                    continue;
                var d = point - z.Center;
                if (Mathf.Abs(d.y) > ThrowableRules.FireVerticalReach)
                    continue;
                d.y = 0f;
                var r = z.Radius + margin;
                if (d.sqrMagnitude <= r * r)
                    return true;
            }

            return false;
        }

        public static FireZone Spawn(Vector3 position, PlayerId thrower)
        {
            var center = position;
            var slope = 0f;
            var downhill = Vector3.zero;
            if (Physics.Raycast(position + Vector3.up * 0.6f, Vector3.down, out var hit, 3f, GameLayers.GroundMask,
                    QueryTriggerInteraction.Ignore))
            {
                center = hit.point;
                slope = Vector3.Angle(hit.normal, Vector3.up);
                downhill = Vector3.ProjectOnPlane(Vector3.down, hit.normal);
                downhill.y = 0f;
                if (downhill.sqrMagnitude > 1e-4f)
                    downhill.Normalize();
            }

            var radius = ThrowableRules.FireRadiusOnSlope(ThrowableRules.FireRadius, slope);
            center += downhill * (radius - ThrowableRules.FireRadius) * 0.6f;

            var go = new GameObject("MolotofAlani");
            go.SetActive(false);
            go.transform.position = center;
            var zone = go.AddComponent<FireZone>();
            zone.Init(center, radius, thrower);
            go.SetActive(true);
            return zone;
        }

        private void Init(Vector3 center, float radius, PlayerId thrower)
        {
            Center = center;
            Radius = radius;
            _thrower = thrower;
            _remaining = ThrowableRules.FireDurationSeconds;
            _nextTick = Time.time + ThrowableRules.FireTickSeconds;
            _seed = UnityEngine.Random.value * 100f;
            BuildFlames();
            BuildLight();
            ActiveList.Add(this);

            try
            {
                GameVfx.SmokeCloud(center + Vector3.up * 0.5f, radius * 0.8f, ThrowableRules.FireDurationSeconds * 0.8f);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            _remaining -= dt * ThrowableRules.FireDecayRate(Rain);
            if (ThrowableRules.RainExtinguishes(Rain))
                _remaining = Mathf.Min(_remaining, 1.2f);

            if (_remaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (_light != null)
            {
                var fade = Mathf.Clamp01(_remaining / 2f);
                var flicker = 0.75f + 0.25f * Mathf.PerlinNoise(_seed, Time.time * 11f);
                _light.intensity = 3.2f * flicker * fade;
            }

            if (_flames != null && _remaining < 2f)
            {
                var emission = _flames.emission;
                emission.rateOverTimeMultiplier = Mathf.Lerp(0f, 90f, _remaining / 2f);
            }

            if (Time.time >= _nextTick)
            {
                _nextTick += ThrowableRules.FireTickSeconds;
                if (GameContext.HasAuthority)
                    Tick();
            }
        }

        private void Tick()
        {
            var combat = CombatContext.Combat;
            if (combat == null)
                return;

            var all = CombatantRegistry.All;
            for (var i = all.Count - 1; i >= 0; i--)
            {
                if (i >= all.Count)
                    continue;
                var c = all[i];
                if (c == null || !c.IsAlive || !c.IsTargetable)
                    continue;

                var offset = c.transform.position - Center;
                var inside = Mathf.Abs(offset.y) <= ThrowableRules.FireVerticalReach;
                offset.y = 0f;
                var distance = offset.magnitude;
                inside &= distance <= Radius;

                _burning.TryGetValue(c.Id, out var ticks);
                ticks = ThrowableRules.BurnTicksLeft(inside, ticks);
                _burning[c.Id] = ticks;
                if (ticks <= 0)
                    continue;

                var damage = ThrowableRules.FireTickDamageAt(inside ? distance : 0f, Radius);
                if (damage <= 0f)
                    damage = ThrowableRules.FireTickDamage * ThrowableRules.FireEdgeDamageFactor;

                try
                {
                    combat.ApplyExplosion(_thrower, c.Id, ThrowableRules.MolotovSourceId, damage, CombatContext.ToFloat3(Center));
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            // Ölü/kaybolmuş kayıtları temizle.
            _scratch.Clear();
            foreach (var kv in _burning)
            {
                if (kv.Value <= 0)
                    _scratch.Add(kv.Key);
            }

            for (var i = 0; i < _scratch.Count; i++)
                _burning.Remove(_scratch[i]);
        }

        private void BuildFlames()
        {
            var go = new GameObject("Alev");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.1f;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
            main.gravityModifier = -0.12f;
            main.maxParticles = 220;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.25f, 0.9f), new Color(1f, 0.35f, 0.08f, 0.9f));

            var emission = ps.emission;
            emission.rateOverTime = 90f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Radius * 0.85f;
            shape.radiusThickness = 1f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 0.45f), new GradientColorKey(new Color(0.25f, 0.08f, 0.04f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.15f), new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.15f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            try
            {
                var mat = MaterialLibrary.ParticleAdditive;
                if (mat != null)
                    renderer.sharedMaterial = mat;
            }
            catch (Exception)
            {
                // Malzeme kütüphanesi hazır değil — varsayılan malzeme kalır.
            }

            go.SetActive(true);
            _flames = ps;
        }

        private void BuildLight()
        {
            var go = new GameObject("AlevIsigi");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 1.2f;
            _light = go.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(1f, 0.55f, 0.2f);
            _light.range = Radius * 2.4f;
            _light.shadows = LightShadows.None;
            _light.intensity = 3f;
        }

        private void OnDestroy()
        {
            ActiveList.Remove(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ActiveList.Clear();
            Rain = 0f;
        }
    }
}
