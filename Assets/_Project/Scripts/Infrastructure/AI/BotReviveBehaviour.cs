using Project.Application.Dialogue;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Combat;
using UnityEngine;

namespace Project.Infrastructure.AI
{
    /// <summary>
    /// Bot yaralı (DBNO) davranışı. Yaralıyken: ateş etmez, yatar, yakın müttefike doğru sürünür.
    /// Sağlamken: güvenliyse (hedef yok, yakın zamanda hasar almadı) en yakın yaralı müttefiki kaldırır;
    /// Sıhhiyeci daha uzaktan koşar. ExecuteState başında çağrılır; true dönerse normal durum yürütülmez.
    /// </summary>
    public sealed partial class BotController
    {
        private const float ReviveSafeSeconds = 2.5f;
        private const float MedicReviveSearchRange = 40f;
        private const float ReviveSearchRange = 18f;
        private const float DownedCrawlSeekRange = 14f;

        private float _nextReviveSearch;
        private Combatant _reviveVictim;
        private bool _revivingNow;

        private bool ExecuteReviveOverride(float now)
        {
            var me = Combatant;
            if (me == null)
                return false;

            if (me.IsDowned)
            {
                AbortReviveAttempt();
                _desiredStance = Stance.Prone;
                LookMovement();
                var ally = CombatantRegistry.FindNearest(transform.position, DownedCrawlSeekRange, -1, me.Team, me);
                if (ally != null && !ally.IsDowned && ally.Team == me.Team)
                    MoveTo(ally.transform.position, MoveSpeed.Walk, 2.5f);
                else
                    StopMoving();
                return true;
            }

            var safe = _perception != null && _perception.Target == null && now - _lastDamagedTime > ReviveSafeSeconds;
            if (!safe && !ReviveUnderSmokeAllowed(me, now))
            {
                AbortReviveAttempt();
                return false;
            }

            if (_reviveVictim != null && (!_reviveVictim.IsAlive || !_reviveVictim.IsDowned))
                AbortReviveAttempt();

            if (_reviveVictim == null)
            {
                if (now < _nextReviveSearch)
                    return false;

                _nextReviveSearch = now + 0.6f;
                _reviveVictim = FindDownedAlly(me);
                if (_reviveVictim == null)
                    return false;
            }

            var service = ReviveRuntime.Service;
            var toVictim = _reviveVictim.transform.position - transform.position;
            toVictim.y = 0f;
            if (toVictim.magnitude > ReviveService.ReviveRange * 0.8f)
            {
                if (_revivingNow)
                    service.CancelRevive(me.Id);

                _revivingNow = false;
                MoveTo(_reviveVictim.transform.position, MoveSpeed.Run, 1.4f);
                LookMovement();
                return true;
            }

            if (!service.BeginRevive(me.Id, me.Role, _reviveVictim.Id))
            {
                _reviveVictim = null; // başkası kaldırıyor
                _nextReviveSearch = now + 2f;
                return false;
            }

            if (!_revivingNow)
                Callout(DialogueCats.Reviving);

            _revivingNow = true;
            StopMoving();
            LookAt(_reviveVictim.transform.position + Vector3.up * 0.3f);
            _desiredStance = Stance.Crouching;
            return true;
        }

        /// <summary>
        /// Ateş altında yaralı kaldırma: yalnızca sis tehdit hattını kesiyorsa (ve can/baskı/yakınlık uygunsa). Hat açıksa yaralı
        /// yakındaysa ve sis varsa önce sis atılır, bir sonraki karede kaldırma denenir.
        /// </summary>
        private bool ReviveUnderSmokeAllowed(Combatant me, float now)
        {
            if (_perception == null || !_perception.HasLastKnownEnemyPosition || now - _perception.LastSeenTime > 20f)
                return false;

            if (_reviveVictim == null)
            {
                if (now < _nextReviveSearch)
                    return false;

                _nextReviveSearch = now + 0.6f;
                _reviveVictim = FindDownedAlly(me);
                if (_reviveVictim == null)
                    return false;
            }

            var victimPosition = _reviveVictim.transform.position;
            var threat = _perception.LastKnownEnemyPosition + Vector3.up * 1.5f;
            var victimPoint = victimPosition + Vector3.up * 0.5f;
            var smokeBlocks = SmokeVolume.BlocksLineOfSight(threat, victimPoint) || SmokeVolume.Contains(victimPosition);
            var victimDistance = FlatDistance(transform.position, victimPosition);

            if (BotCombatRules.CanReviveUnderSmoke(smokeBlocks, HealthFraction, EffectiveSuppression, victimDistance, _perception.VisibleEnemyCount))
                return true;

            var inventory = Combatant.Inventory;
            var hasSmoke = inventory != null && inventory.GetCount(Project.Application.Catalogs.ItemIds.SmokeGrenade) > 0;
            if (BotCombatRules.ShouldSmokeForRevive(true, smokeBlocks, hasSmoke, victimDistance))
            {
                var toThreat = threat - victimPosition;
                toThreat.y = 0f;
                var point = victimPosition + (toThreat.sqrMagnitude > 1f ? toThreat.normalized * 3f : Vector3.zero);
                if (BotTactics.TryGroundPoint(point, out var ground))
                    point = ground;
                TryThrowSmokeAtPoint(point, now);
            }

            return false;
        }

        private Combatant FindDownedAlly(Combatant me)
        {
            var range = me.Role == TeamRole.Medic ? MedicReviveSearchRange : ReviveSearchRange;
            var best = (Combatant)null;
            var bestSqr = range * range;
            var all = CombatantRegistry.All;
            var service = ReviveRuntime.Service;
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c == me || c.Team != me.Team || !c.IsAlive || !c.IsDowned)
                    continue;

                var other = service.CurrentReviver(c.Id);
                if (other.IsValid && other != me.Id)
                    continue;

                var sqr = (c.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = c;
                }
            }

            return best;
        }

        private void AbortReviveAttempt()
        {
            if (_revivingNow && Combatant != null)
                ReviveRuntime.Service.CancelRevive(Combatant.Id);

            _revivingNow = false;
            _reviveVictim = null;
        }
    }
}
