using Project.Infrastructure.AI;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Combat;
using Project.Presentation.Player;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Ping yerleştirme: bakılan nokta (düşman savaşçıya bakılıyorsa düşman ping'i, yoksa konum ping'i).
    /// Panoya yazar, dünya HUD'unu açar, botları bilgilendirir (düşman ping'inde araştırmaya gelirler).
    /// </summary>
    public static class PingController
    {
        public const float MaxDistance = 600f;
        private const float ConeDegrees = 2.5f;

        /// <summary>Saf karar: düşman ping'i olmalı mı?</summary>
        public static bool IsEnemyTarget(bool targetIsCombatant, bool targetAlive, int targetTeam, int ownTeam)
            => targetIsCombatant && targetAlive && targetTeam != ownTeam;

        public static bool TryPlace(PlayerController owner)
        {
            var self = owner != null ? owner.Combatant : null;
            if (self == null || !self.IsAlive)
                return false;

            var origin = owner.AimOrigin;
            var forward = owner.AimForward;
            var enemy = FindEnemyAlongRay(owner, self, origin, forward, out var hitDistance, out var hitPoint, out var hasHit);

            Vector3 position;
            bool isEnemy;
            if (enemy != null)
            {
                position = enemy.transform.position;
                isEnemy = true;
            }
            else if (hasHit)
            {
                position = hitPoint;
                isEnemy = false;
            }
            else
            {
                owner.Notify("İşaret için hedef görülmüyor", 1.5f);
                return false;
            }

            var now = Time.time;
            PingBoard.Place(self.Team, position, isEnemy, now);
            PingWorldView.EnsureExists();

            if (Project.Infrastructure.GameContext.HasAuthority)
            {
                var director = BotDirector.Instance;
                if (director != null && isEnemy)
                    director.ReportPing(self.Team, position, now);
            }

            PlayerController.PlaySound2D(SoundId.RadioBeep, 0.5f);
            owner.Notify(isEnemy ? "Düşman işaretlendi!" : "Nokta işaretlendi", 1.4f, false);
            return true;
        }

        private static Combatant FindEnemyAlongRay(PlayerController owner, Combatant self, Vector3 origin, Vector3 forward,
            out float hitDistance, out Vector3 hitPoint, out bool hasHit)
        {
            hitDistance = MaxDistance;
            hitPoint = default;
            hasHit = false;
            Combatant direct = null;

            var hits = Physics.RaycastAll(origin, forward, MaxDistance, ~0, QueryTriggerInteraction.Ignore);
            var best = float.MaxValue;
            for (var i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.collider == null || h.distance >= best)
                    continue;

                var combatant = h.collider.GetComponentInParent<Combatant>();
                if (combatant == self || (combatant != null && combatant.transform.IsChildOf(owner.transform)))
                    continue;

                best = h.distance;
                hasHit = true;
                hitPoint = h.point;
                hitDistance = h.distance;
                direct = combatant;
            }

            if (direct != null && IsEnemyTarget(true, direct.IsAlive, direct.Team, self.Team))
                return direct;

            // Nişan payı: koni içinde, ilk engelden önce kalan düşman.
            var all = CombatantRegistry.All;
            Combatant nearest = null;
            var nearestAngle = ConeDegrees;
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c == self || !IsEnemyTarget(true, c.IsAlive, c.Team, self.Team))
                    continue;

                var to = c.transform.position + Vector3.up * 1.2f - origin;
                var d = to.magnitude;
                if (d < 1f || d > hitDistance + 0.5f)
                    continue;

                var angle = Vector3.Angle(forward, to);
                if (angle < nearestAngle)
                {
                    nearestAngle = angle;
                    nearest = c;
                }
            }

            return nearest;
        }
    }
}
