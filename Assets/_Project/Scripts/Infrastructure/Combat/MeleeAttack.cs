using System;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Vfx;
using UnityEngine;

namespace Project.Infrastructure.Combat
{
    /// <summary>
    /// Silahsız yakın dövüş (yumruk). Gözden ileri doğru ince bir küre taraması yapar; ilk engel duvar ise vuruş olmaz.
    /// İsabette <see cref="Project.Application.Services.CombatService.ApplyMeleeHit"/> (yalnızca otoritede) ve yumruk sesi.
    /// </summary>
    public static class MeleeAttack
    {
        public const float DefaultRange = 2.2f;
        private const float SweepRadius = 0.22f;
        private static readonly RaycastHit[] Hits = new RaycastHit[24];

        /// <returns>Bir savaşana isabet ettiyse true.</returns>
        public static bool TryPunch(Combatant attacker, Vector3 origin, Vector3 direction, float range = DefaultRange)
        {
            if (attacker == null || !attacker.IsAlive)
                return false;

            if (direction.sqrMagnitude < 1e-8f)
                direction = attacker.transform.forward;

            direction.Normalize();
            range = Mathf.Max(0.3f, range);

            var mask = (1 << GameLayers.Hitbox) | GameLayers.LineOfSightMask;
            var count = Physics.SphereCastNonAlloc(origin, SweepRadius, direction, Hits, range, mask, QueryTriggerInteraction.Collide);
            SortHits(count);

            var friendlyFire = CombatContext.FriendlyFire;
            for (var i = 0; i < count; i++)
            {
                var hit = Hits[i];
                var collider = hit.collider;
                if (collider == null)
                    continue;

                if (collider.TryGetComponent<Hitbox>(out var hitbox))
                {
                    var victim = hitbox.Owner;
                    if (victim == null || victim == attacker || !victim.IsAlive || !victim.IsTargetable)
                        continue;

                    if (!friendlyFire && victim.Team == attacker.Team)
                        continue;

                    var point = hit.distance > 0f ? hit.point : hitbox.WorldCenter;
                    Strike(attacker, victim, hitbox.Part, origin, point, -direction);
                    return true;
                }

                if (collider.isTrigger)
                    continue;

                // Duvar / araç yumruktan önce: engellendi.
                if (hit.distance > 0f)
                {
                    PlaySound(hit.point, 0.45f);
                    return false;
                }
            }

            return false;
        }

        private static void Strike(Combatant attacker, Combatant victim, BodyPart part, Vector3 origin, Vector3 point, Vector3 normal)
        {
            if (GameContext.HasAuthority)
            {
                var combat = CombatContext.Combat;
                if (combat != null)
                {
                    try
                    {
                        combat.ApplyMeleeHit(attacker.Id, victim.Id, part, CombatContext.ToFloat3(origin));
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }

            PlaySound(point, 1f);

            try
            {
                GameVfx.Impact(point, normal, SurfaceKind.Flesh);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void PlaySound(Vector3 point, float volume)
        {
            try
            {
                GameAudio.Play(SoundId.Punch, point, volume, UnityEngine.Random.Range(0.9f, 1.1f), 25f);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void SortHits(int count)
        {
            for (var i = 1; i < count; i++)
            {
                var key = Hits[i];
                var j = i - 1;
                while (j >= 0 && Hits[j].distance > key.distance)
                {
                    Hits[j + 1] = Hits[j];
                    j--;
                }

                Hits[j + 1] = key;
            }
        }
    }
}
