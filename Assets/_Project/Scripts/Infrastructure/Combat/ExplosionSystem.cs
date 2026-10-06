using System;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Player;
using Project.Infrastructure.Vfx;
using UnityEngine;

namespace Project.Infrastructure.Combat
{
    /// <summary>
    /// Patlamalar (el bombası, topçu): görsel/ses her yerde; hasar yalnızca otoritede.
    /// Yarıçap içindeki canlı ve hedef alınabilir savaşanlara görüş hattı (ayak/gövde/baş) açıksa
    /// <see cref="CombatService.ApplyExplosion"/> ile mesafeye göre azalan hasar uygulanır. ExplosionEvent yayınlanır,
    /// yakındaki yerel oyuncunun kamerası sarsılır, serbest rijit cisimler itilir.
    /// </summary>
    public static class ExplosionSystem
    {
        /// <summary>Kamera sarsıntısının hissedildiği mesafe = yarıçap × bu çarpan.</summary>
        public const float ShakeRangeMultiplier = 6f;

        private const float LosOriginLift = 0.3f;
        private const float PushVelocity = 6f;
        private static readonly Collider[] OverlapBuffer = new Collider[48];
        private static readonly Vector3[] SamplePoints = new Vector3[3];

        private static Combatant _shakeOwner;
        private static IFirstPersonCamera _shakeCamera;

        /// <summary>Son patlama (hata ayıklama/AI tepkisi için).</summary>
        public static Vector3 LastExplosionPosition { get; private set; }
        public static float LastExplosionTime { get; private set; } = -999f;

        /// <summary>Her patlamada (pozisyon, yarıçap) — AI kaçış/tepki için yerel bildirim.</summary>
        public static event Action<Vector3, float> Exploded;

        public static void Explode(Vector3 position, float radius, float maxDamage, PlayerId attackerId, string sourceId)
        {
            if (radius < 0.1f)
                radius = 0.1f;

            LastExplosionPosition = position;
            LastExplosionTime = Time.time;

            PlayEffects(position, radius);

            if (GameContext.HasAuthority && maxDamage > 0f)
                ApplyDamage(position, radius, maxDamage, attackerId, string.IsNullOrEmpty(sourceId) ? DamageSourceIds.FragGrenade : sourceId);

            PushBodies(position, radius);
            try { Destructible.ExplodeAt(position, radius, maxDamage); } catch (Exception e) { Debug.LogException(e); }
            ShakeLocalCamera(position, radius);

            try
            {
                CombatContext.EventBus?.Publish(new ExplosionEvent(CombatContext.ToFloat3(position), radius, attackerId));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            try
            {
                Exploded?.Invoke(position, radius);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void PlayEffects(Vector3 position, float radius)
        {
            try
            {
                GameVfx.Explosion(position, radius);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            try
            {
                var pitch = UnityEngine.Random.Range(0.88f, 1.05f) * Mathf.Lerp(1.1f, 0.85f, Mathf.InverseLerp(4f, 12f, radius));
                // Yakın rapor burada; >15 m gecikmeli rapor + yankılar Acoustics.OnExplosion'da (çift çalma yok).
                GameAudio.PlayExplosion(position, radius, pitch, Mathf.Clamp(radius * 30f, 150f, 400f));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void ApplyDamage(Vector3 position, float radius, float maxDamage, PlayerId attackerId, string sourceId)
        {
            var combat = CombatContext.Combat;
            if (combat == null)
                return;

            var source = CombatContext.ToFloat3(position);
            var losOrigin = position + Vector3.up * LosOriginLift;
            var all = CombatantRegistry.All;
            var reach = radius + 2.2f; // kök → baş mesafesi payı
            var radiusSqr = reach * reach;

            // Hasar uygulaması kaydı değiştirebilir (ölüm → olaylar); geriye doğru indeksle güvenli dolaş.
            for (var i = all.Count - 1; i >= 0; i--)
            {
                if (i >= all.Count)
                    continue;

                var c = all[i];
                if (c == null || !c.IsAlive || !c.IsTargetable)
                    continue;

                var root = c.transform.position;
                if ((root - position).sqrMagnitude > radiusSqr)
                    continue;

                SamplePoints[0] = c.GetAimPosition(BodyPart.Torso);
                SamplePoints[1] = c.GetAimPosition(BodyPart.Head);
                SamplePoints[2] = root + Vector3.up * 0.3f;

                var bestDistance = float.MaxValue;
                for (var s = 0; s < SamplePoints.Length; s++)
                {
                    var point = SamplePoints[s];
                    var distance = Vector3.Distance(position, point);
                    if (distance > radius || distance >= bestDistance)
                        continue;

                    if (HasLineOfSight(losOrigin, point))
                        bestDistance = distance;
                }

                if (bestDistance > radius)
                    continue;

                var raw = DamageCalculator.ComputeExplosionDamage(maxDamage, radius, bestDistance);
                if (float.IsNaN(raw) || raw <= 0.5f)
                    continue;

                try
                {
                    combat.ApplyExplosion(attackerId, c.Id, sourceId, raw, source);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        private static bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            var delta = to - from;
            var distance = delta.magnitude;
            if (distance < 0.05f)
                return true;

            // Hedefe çok yakın engelleri (ör. üzerinde durduğu zemin) yok saymak için biraz kısa tut.
            return !Physics.Raycast(from, delta / distance, Mathf.Max(0f, distance - 0.15f), GameLayers.LineOfSightMask,
                QueryTriggerInteraction.Ignore);
        }

        private static void PushBodies(Vector3 position, float radius)
        {
            var mask = (1 << GameLayers.Projectile) | (1 << GameLayers.Loot) | (1 << GameLayers.Default);
            var count = Physics.OverlapSphereNonAlloc(position, radius, OverlapBuffer, mask, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var body = OverlapBuffer[i] != null ? OverlapBuffer[i].attachedRigidbody : null;
                OverlapBuffer[i] = null;
                if (body == null || body.isKinematic || body.gameObject.layer == GameLayers.Vehicle)
                    continue;

                var offset = body.worldCenterOfMass - position;
                var distance = offset.magnitude;
                var falloff = 1f - Mathf.Clamp01(distance / radius);
                if (falloff <= 0f)
                    continue;

                var direction = distance > 0.01f ? offset / distance : Vector3.up;
                direction = (direction + Vector3.up * 0.6f).normalized;
                body.AddForce(direction * (PushVelocity * falloff), ForceMode.VelocityChange);
            }
        }

        private static void ShakeLocalCamera(Vector3 position, float radius)
        {
            var local = CombatantRegistry.LocalPlayer;
            if (local == null)
                return;

            var range = radius * ShakeRangeMultiplier;
            var distance = Vector3.Distance(local.EyePosition, position);
            if (distance >= range)
                return;

            var camera = ResolveCamera(local);
            if (camera == null)
                return;

            var intensity = 1f - distance / range;
            intensity *= intensity;
            intensity *= Mathf.Clamp(radius / 9f, 0.5f, 1.5f);
            try
            {
                camera.Shake(Mathf.Clamp01(intensity), 0.3f + 0.6f * intensity);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static IFirstPersonCamera ResolveCamera(Combatant local)
        {
            if (ReferenceEquals(_shakeOwner, local) && _shakeCamera is UnityEngine.Object cached && cached != null)
                return _shakeCamera;

            _shakeOwner = local;
            _shakeCamera = local.GetComponentInChildren<FirstPersonCameraController>(true);
            if (_shakeCamera == null)
                _shakeCamera = local.GetComponentInChildren<IFirstPersonCamera>(true);
            if (_shakeCamera == null)
                _shakeCamera = UnityEngine.Object.FindAnyObjectByType<FirstPersonCameraController>();

            return _shakeCamera;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _shakeOwner = null;
            _shakeCamera = null;
            Exploded = null;
            LastExplosionTime = -999f;
        }
    }
}
