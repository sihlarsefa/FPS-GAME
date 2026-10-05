using System;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Loot;
using UnityEngine;

namespace Project.Infrastructure.AI
{
    /// <summary>Algı → duyu özeti → karar → durum yürütme (hedef nokta / bakış / duruş seçimi).</summary>
    public sealed partial class BotController
    {
        private const float DecisionInterval = 0.5f;
        private const float GoalRefreshInterval = 0.4f;
        private const int IgnoredLootCapacity = 6;

        private enum EngageTactic
        {
            Hold,
            Strafe,
            Advance,
            Cover,
            Chase
        }

        private float _nextPerception;
        private float _nextDecision;
        private float _lastDecisionTime = -999f;
        private float _nextDamageDecision;
        private bool _forceDecision;
        private float _stateEnterTime;
        private Combatant _lastPerceivedTarget;
        private int _orderRevision = -1;

        // emir
        private bool _hasOrder;
        private SquadOrder _currentOrder;
        private Vector3 _orderTarget;

        // hedef noktaları
        private float _nextGoalTime;
        private Vector3 _goalPoint;
        private bool _hasGoalPoint;
        private float _arrivedTime = -1f;

        // yağma
        private Func<LootPickupComponent, bool> _lootFilter;
        private LootPickupComponent _lootTarget;
        private float _lootDistance;
        private float _nextLootSearch;
        private float _lootStartTime;
        private readonly LootPickupComponent[] _ignoredLoot = new LootPickupComponent[IgnoredLootCapacity];
        private int _ignoredLootCursor;

        // keşif
        private Vector3 _roamPoint;
        private bool _hasRoamPoint;

        // çatışma taktiği
        private EngageTactic _tactic;
        private float _nextTacticTime;
        private Vector3 _tacticPoint;
        private bool _crouchWhileHolding;
        private bool _proneWhileHolding;
        private float _nextCoverSearch;
        private float _nextBoostAttempt;

        // ------------------------------------------------------------------ algı

        private void Perceive(float now)
        {
            var relations = _director != null ? _director.Relations : null;
            _perception.Scan(EyePosition, AimForward, Profile, relations, _director, now);
            _perception.ProcessHearing(_director, transform.position, Profile.HearingDistance, relations, now);

            var target = _perception.Target;
            if (target != null && _lastPerceivedTarget == null)
                RequestDecision();
            _lastPerceivedTarget = target;

            var orders = _director != null ? _director.Orders : null;
            if (orders != null)
            {
                var revision = orders.GetRevision(_team);
                if (revision != _orderRevision)
                {
                    _orderRevision = revision;
                    _nextGoalTime = 0f;
                    RequestDecision();
                }
            }
        }

        // ------------------------------------------------------------------ karar

        private void Decide(float now)
        {
            _nextDecision = now + DecisionInterval + Range(-0.08f, 0.08f);
            _lastDecisionTime = now;
            _forceDecision = false;

            ResolveLeader();

            if (now >= _nextLootSearch)
                RefreshLootTarget(now);

            var senses = BuildSenses(now);
            var previous = State;
            var next = BotDecisionService.Decide(senses, previous);

            if (_director != null && _director.MatchEnded)
                next = BotState.Idle;

            if (next != previous)
                OnStateChanged(previous, next, now);

            State = next;
            MaintainWeapon(now);
        }

        private void ResolveLeader()
        {
            Combatant leader = null;
            if (_explicitLeader != null && _explicitLeader.IsAlive && !ReferenceEquals(_explicitLeader, Combatant) &&
                _explicitLeader.Team == _team)
            {
                leader = _explicitLeader;
            }
            else if (_director != null)
            {
                leader = _director.GetCommander(_team);
            }

            if (leader == null || ReferenceEquals(leader, Combatant))
            {
                _leader = null;
                _isCommander = true;
            }
            else
            {
                _leader = leader;
                _isCommander = false;
            }
        }

        private BotSenses BuildSenses(float now)
        {
            var combatant = Combatant;
            var inventory = combatant.Inventory;
            var health = combatant.Health;
            var position = transform.position;
            var senses = new BotSenses();

            senses.HealthNormalized = health != null && health.Max > 0f ? Mathf.Clamp01(health.Current / health.Max) : 0f;

            var weapon = inventory != null ? inventory.ActiveWeapon : null;
            senses.HasWeapon = inventory != null && inventory.HasAnyWeapon;
            senses.HasAmmo = inventory != null && inventory.HasUsableWeapon;
            senses.IsReloading = weapon != null && weapon.IsReloading;
            senses.HasHealItem = inventory != null && health != null && inventory.BestHealItem(health.Current) != null;
            senses.HasBoostItem = inventory != null && inventory.BestBoostItem(combatant.Boost != null ? combatant.Boost.Value : 0f) != null;

            var target = _perception.Target;
            senses.CanSeeEnemy = target != null;
            senses.EnemyDistance = target != null ? FlatDistance(position, target.transform.position) : 999f;
            senses.SecondsSinceEnemySeen = Mathf.Min(now - _perception.LastSeenTime, now - _perception.LastEnemyDamageTime);
            senses.SecondsSinceDamaged = now - _perception.LastDamagedTime;
            senses.HasLastKnownEnemyPosition = _perception.HasLastKnownEnemyPosition;
            senses.HeardGunfireRecently = now - _perception.LastHeardTime < BotPerception.HeardMemorySeconds;

            var zone = _director != null ? _director.Zone : null;
            if (zone != null && BotDirector.SafeZoneActive(zone))
            {
                try
                {
                    senses.IsOutsideZone = !zone.IsInsideZone(position.x, position.z);
                    senses.IsOutsideNextZone = !BotDirector.SafeNextZoneContains(zone, position);
                    senses.ZoneIsShrinking = zone.Stage == ZoneStage.Shrinking;
                    senses.SecondsUntilZoneShrinks = zone.Stage == ZoneStage.Waiting ? zone.StageRemainingSeconds : 0f;
                }
                catch (Exception)
                {
                    senses.IsOutsideZone = false;
                    senses.IsOutsideNextZone = false;
                }
            }

            var lootKnown = _lootTarget != null && _lootTarget.IsAvailable;
            senses.KnowsUsefulLoot = lootKnown;
            senses.NearestUsefulLootDistance = lootKnown ? FlatDistance(position, _lootTarget.transform.position) : 999f;
            senses.NeedsLoot = ComputeNeedsLoot(inventory, health, lootKnown ? senses.NearestUsefulLootDistance : 999f);

            // Tim
            senses.IsSquadMember = _leader != null;
            senses.LeaderAlive = _leader != null && _leader.IsAlive;
            senses.DistanceToLeader = _leader != null ? FlatDistance(position, _leader.transform.position) : 0f;

            _hasOrder = false;
            var orders = _director != null ? _director.Orders : null;
            if (_leader != null && !_leader.IsBot && orders != null &&
                orders.TryGetOrder(_team, out var order, out var orderTarget))
            {
                _hasOrder = true;
                _currentOrder = order;
                _orderTarget = ToVector3(orderTarget);
                senses.HasSquadOrder = true;
                senses.Order = order;
                senses.DistanceToOrderTarget = FlatDistance(position, _orderTarget);
            }

            senses.AllyNeedsHelp = _perception.AllyNeedsHelp;
            return senses;
        }

        private bool ComputeNeedsLoot(InventoryService inventory, HealthService health, float knownLootDistance)
        {
            if (inventory == null)
                return false;

            if (!inventory.HasUsableWeapon)
                return true;

            var weapon = inventory.ActiveWeapon;
            if (weapon != null)
            {
                var magazine = Mathf.Max(1, weapon.MagazineSize);
                if (weapon.CurrentAmmo + weapon.ReserveAmmo < magazine * 2)
                    return true;
            }

            var healValue = inventory.GetCount(ItemIds.Bandage) + inventory.GetCount(ItemIds.FirstAid) * 3 + inventory.GetCount(ItemIds.MedKit) * 5;
            if (healValue < 3)
                return true;

            var vest = inventory.Vest;
            var helmet = inventory.Helmet;
            if (vest == null || vest.IsBroken || helmet == null || helmet.IsBroken)
                return true;

            // Fırsatçı: çok yakında işe yarar bir şey varsa al.
            return knownLootDistance < 10f;
        }

        private void OnStateChanged(BotState previous, BotState next, float now)
        {
            _stateEnterTime = now;
            _nextGoalTime = 0f;
            _hasGoalPoint = false;
            _arrivedTime = -1f;
            _moveFailed = false;

            if (previous == BotState.Loot && _lootTarget != null)
                _director?.ReleaseClaim(_lootTarget, this);

            if (next == BotState.Loot)
                _lootStartTime = now;

            if (next == BotState.Engage)
            {
                _nextTacticTime = 0f;
                _burstPauseUntil = Mathf.Max(_burstPauseUntil, now);

                // Çatışmaya girerken tedaviyi kes.
                var itemUse = Combatant.ItemUse;
                if (itemUse != null && itemUse.IsUsing)
                    itemUse.Cancel();
            }

            if (previous == BotState.Engage)
            {
                _burstRemaining = 0;
                _crouchWhileHolding = false;
                _proneWhileHolding = false;
            }

            if (next == BotState.Roam)
                _hasRoamPoint = false;
        }

        // ------------------------------------------------------------------ durum yürütme

        private void ExecuteState(float now)
        {
            _desiredStance = Stance.Standing;

            switch (State)
            {
                case BotState.Engage:
                    ExecuteEngage(now);
                    break;
                case BotState.Flee:
                    ExecuteFlee(now);
                    break;
                case BotState.Heal:
                    ExecuteHeal(now);
                    break;
                case BotState.MoveToZone:
                    ExecuteMoveToZone(now);
                    break;
                case BotState.Loot:
                    ExecuteLoot(now);
                    break;
                case BotState.Investigate:
                    ExecuteInvestigate(now);
                    break;
                case BotState.Follow:
                    ExecuteFormation(now, true);
                    break;
                case BotState.Idle:
                    ExecuteIdle(now);
                    break;
                case BotState.Hold:
                    ExecuteHoldOrAssault(now, false);
                    break;
                case BotState.Assault:
                    ExecuteHoldOrAssault(now, true);
                    break;
                default:
                    ExecuteRoam(now);
                    break;
            }

            ApplyStance();
        }

        private void ExecuteIdle(float now)
        {
            if (_leader != null && !(_director != null && _director.MatchEnded))
            {
                ExecuteFormation(now, false);
                return;
            }

            StopMoving();
            LookYaw(ScanYaw(transform.eulerAngles.y, now));
            TryTacticalReload(now);
            TryBoost(now);
        }

        /// <summary>Kama düzenindeki yuvaya git / yuvada bekle.</summary>
        private void ExecuteFormation(float now, bool hurry)
        {
            var leader = _leader;
            if (leader == null)
            {
                ExecuteRoam(now);
                return;
            }

            var leaderPosition = leader.transform.position;
            var leaderVelocity = leader.Velocity;
            if (now >= _nextGoalTime || !_hasGoalPoint)
            {
                _nextGoalTime = now + GoalRefreshInterval;
                var spacing = _hasOrder && _currentOrder == SquadOrder.Regroup ? 0.55f : 1f;
                var yaw = _director != null ? _director.GetFormationYaw(_team, leader) : leader.transform.eulerAngles.y;
                var slot = BotTactics.WedgeSlot(leaderPosition, leaderVelocity, yaw, Mathf.Max(1, FormationIndex), spacing);
                if (!SampleDestination(slot, 5f, out var sampled))
                    sampled = leaderPosition;

                _goalPoint = sampled;
                _hasGoalPoint = true;
            }

            var position = transform.position;
            var distance = FlatDistance(position, _goalPoint);
            var leaderSpeed = new Vector2(leaderVelocity.x, leaderVelocity.z).magnitude;
            var regroup = _hasOrder && _currentOrder == SquadOrder.Regroup;

            if (distance > 1.4f || leaderSpeed > 0.8f)
            {
                MoveSpeed speed;
                if (hurry || regroup || distance > 14f || leaderSpeed > 4.2f)
                    speed = distance > 30f ? MoveSpeed.Sprint : MoveSpeed.Run;
                else if (distance > 6f || leaderSpeed > 2.5f)
                    speed = MoveSpeed.Jog;
                else
                    speed = MoveSpeed.Walk;

                MoveTo(_goalPoint, speed, 1f);
                LookMovement();
            }
            else
            {
                StopMoving();
                var baseYaw = _director != null ? _director.GetFormationYaw(_team, leader) : leader.transform.eulerAngles.y;

                // Dış kanattakiler dışarı bakar: düzen 360° gözetler.
                var side = (FormationIndex & 1) == 1 ? -1f : 1f;
                var outward = FormationIndex <= 2 ? 25f : FormationIndex <= 6 ? 60f : 110f;
                LookYaw(ScanYaw(baseYaw + side * outward, now));

                if (leader.Stance != Stance.Standing)
                    _desiredStance = Stance.Crouching;

                TryTacticalReload(now);
                TryBoost(now);
            }
        }

        private void ExecuteHoldOrAssault(float now, bool assault)
        {
            if (!_hasOrder)
            {
                ExecuteFormation(now, false);
                return;
            }

            if (now >= _nextGoalTime || !_hasGoalPoint)
            {
                _nextGoalTime = now + 1f;
                var point = BotTactics.RingSlot(_orderTarget, Mathf.Max(1, FormationIndex), assault ? 5f : 3.5f);
                if (!SampleDestination(point, 5f, out var sampled) && !SampleDestination(_orderTarget, 6f, out sampled))
                    sampled = _orderTarget;

                _goalPoint = sampled;
                _hasGoalPoint = true;
            }

            var distance = FlatDistance(transform.position, _goalPoint);
            if (distance > 1.2f && !_moveFailed)
            {
                var speed = assault ? (distance > 25f ? MoveSpeed.Sprint : MoveSpeed.Run) : distance > 12f ? MoveSpeed.Run : MoveSpeed.Jog;
                MoveTo(_goalPoint, speed, 0.9f);
                if (assault && distance < 40f)
                    LookAt(_orderTarget + Vector3.up * 1.4f);
                else
                    LookMovement();
                return;
            }

            StopMoving();
            _desiredStance = Stance.Crouching;

            // Mevzide dışarı bak (merkezden yuvaya doğru), tehdit varsa ona.
            var outward = _goalPoint - _orderTarget;
            outward.y = 0f;
            float yaw;
            if (_perception.HasLastKnownEnemyPosition && now - _perception.LastSeenTime < 20f)
            {
                var toThreat = _perception.LastKnownEnemyPosition - transform.position;
                yaw = Mathf.Atan2(toThreat.x, toThreat.z) * Mathf.Rad2Deg;
            }
            else if (outward.sqrMagnitude > 0.25f)
            {
                yaw = Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg;
            }
            else
            {
                yaw = _leader != null ? _leader.transform.eulerAngles.y : transform.eulerAngles.y;
            }

            LookYaw(ScanYaw(yaw, now, 40f));
            TryTacticalReload(now);
        }

        private void ExecuteRoam(float now)
        {
            var position = transform.position;
            Vector3 destination;
            var walking = true;

            if (_director != null && _director.TryGetObjective(_team, out var objective))
            {
                destination = objective;
                walking = FlatDistance(position, objective) < 120f;
            }
            else
            {
                if (!_hasRoamPoint || FlatDistance(position, _roamPoint) < 4f || _moveFailed)
                    PickRoamPoint(position);
                destination = _roamPoint;
            }

            // İnişten hemen sonra araçtan uzaklaş (helikopter rotoru / Kirpi çevresi).
            if (now - _landedTime < 4f && _disembarkAwayDirection.sqrMagnitude > 0.1f)
            {
                destination = position + _disembarkAwayDirection * 8f;
                walking = false;
            }

            var distance = FlatDistance(position, destination);
            if (distance < 6f)
            {
                StopMoving();
                if (_arrivedTime < 0f)
                    _arrivedTime = now;

                LookYaw(ScanYaw(transform.eulerAngles.y, now, 90f));
                if (now - _arrivedTime > 3f)
                {
                    _arrivedTime = -1f;
                    _director?.InvalidateObjective(_team);
                    _hasRoamPoint = false;
                }

                TryTacticalReload(now);
                TryBoost(now);
                return;
            }

            _arrivedTime = -1f;
            if (_moveFailed)
            {
                _moveFailed = false;
                _director?.InvalidateObjective(_team);
                _hasRoamPoint = false;
            }

            MoveTo(destination, walking ? (_isCommander && _leaderHasFollowers ? MoveSpeed.Walk : MoveSpeed.Jog) : MoveSpeed.Run, 4f);
            LookMovement();
        }

        private bool _leaderHasFollowers => _director != null && _director.GetTeam(_team) is { } intel && intel.Bots.Count > 1;

        private void PickRoamPoint(Vector3 position)
        {
            _hasRoamPoint = true;
            _moveFailed = false;
            var zone = _director != null ? _director.Zone : null;
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var angle = Rand() * Mathf.PI * 2f;
                var distance = Range(35f, 110f);
                var candidate = position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * distance;
                if (_director != null)
                    candidate = _director.ClampToMap(candidate);

                if (zone != null && BotDirector.SafeZoneActive(zone) && !BotDirector.SafeNextZoneContains(zone, candidate))
                    continue;

                if (SampleDestination(candidate, 15f, out var sampled))
                {
                    _roamPoint = sampled;
                    return;
                }
            }

            if (zone != null && _director != null && BotDirector.SafeZoneActive(zone))
                _roamPoint = _director.PointInsideNextZone(zone, position, 0.5f);
            else
                _roamPoint = position + transform.forward * 20f;
        }

        private void ExecuteInvestigate(float now)
        {
            Vector3 point;
            var position = transform.position;
            if (_perception.AllyNeedsHelp)
                point = _perception.AllyHelpPosition;
            else if (now - _perception.LastHeardTime < BotPerception.HeardMemorySeconds)
                point = _perception.HeardPosition;
            else if (_perception.HasLastKnownEnemyPosition)
                point = _perception.LastKnownEnemyPosition;
            else
            {
                RequestDecision();
                ExecuteIdle(now);
                return;
            }

            if (now >= _nextGoalTime || !_hasGoalPoint)
            {
                _nextGoalTime = now + 1f;
                // Doğrudan sesin üstüne değil, biraz gerisine yaklaş.
                var toPoint = point - position;
                toPoint.y = 0f;
                var distanceToPoint = toPoint.magnitude;
                var approach = distanceToPoint > 20f ? point - toPoint / distanceToPoint * 12f : point;
                if (!SampleDestination(approach, 8f, out var sampled))
                    sampled = approach;

                _goalPoint = sampled;
                _hasGoalPoint = true;
            }

            var distance = FlatDistance(position, _goalPoint);
            if (distance < 3.5f || _moveFailed)
            {
                StopMoving();
                if (_arrivedTime < 0f)
                    _arrivedTime = now;

                _desiredStance = Stance.Crouching;
                LookYaw(ScanYaw(transform.eulerAngles.y, now, 120f));
                if (now - _arrivedTime > 2.5f)
                {
                    _perception.ClearInvestigation();
                    _arrivedTime = -1f;
                    RequestDecision();
                }

                return;
            }

            _arrivedTime = -1f;
            MoveTo(_goalPoint, distance > 30f ? MoveSpeed.Run : MoveSpeed.Jog, 3f);

            if (FlatDistance(position, point) < 45f)
                LookAt(point + Vector3.up * 1.3f);
            else
                LookMovement();

            // Siper arkasındaki düşmana bomba.
            if (_perception.HasLastKnownEnemyPosition)
            {
                var sinceSeen = now - _perception.LastSeenTime;
                if (sinceSeen > 1.5f && sinceSeen < 8f && now >= _nextGrenadeCheck)
                {
                    _nextGrenadeCheck = now + 1.5f;
                    if (Rand() < 0.25f + Profile.AggressionChance * 0.3f)
                        TryThrowFrag(_perception.LastKnownEnemyPosition, now);
                }
            }
        }

        private void ExecuteFlee(float now)
        {
            var position = transform.position;
            if (now >= _nextGoalTime || !_hasGoalPoint || FlatDistance(position, _goalPoint) < 3f || _moveFailed)
            {
                _nextGoalTime = now + 1.5f;
                _moveFailed = false;
                var threat = _perception.Target != null
                    ? _perception.Target.transform.position
                    : _perception.HasLastKnownEnemyPosition ? _perception.LastKnownEnemyPosition : _perception.DamageSourcePosition;
                var away = position - threat;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f)
                    away = -transform.forward;

                away.Normalize();
                away = Quaternion.Euler(0f, Range(-35f, 35f), 0f) * away;
                var candidate = position + away * 25f;
                if (_director != null)
                    candidate = _director.ClampToMap(candidate);

                if (!SampleDestination(candidate, 10f, out var sampled))
                    sampled = candidate;

                _goalPoint = sampled;
                _hasGoalPoint = true;
            }

            MoveTo(_goalPoint, MoveSpeed.Sprint, 2f);
            LookMovement();
        }

        private void ExecuteHeal(float now)
        {
            var itemUse = Combatant.ItemUse;
            if (itemUse == null)
            {
                RequestDecision();
                return;
            }

            // Yakın zamanda vurulduysa önce siper.
            var threatened = now - _perception.LastEnemyDamageTime < 8f && _perception.HasLastKnownEnemyPosition;
            if (threatened && !itemUse.IsUsing)
            {
                if (!_hasGoalPoint && now >= _nextCoverSearch)
                {
                    _nextCoverSearch = now + 3f;
                    if (BotTactics.TryFindCover(transform.position, _perception.LastKnownEnemyPosition + Vector3.up * 1.5f, 18f, _useNavMesh, out var cover))
                    {
                        _goalPoint = cover;
                        _hasGoalPoint = true;
                    }
                }

                if (_hasGoalPoint && FlatDistance(transform.position, _goalPoint) > 1f && !_moveFailed)
                {
                    MoveTo(_goalPoint, MoveSpeed.Sprint, 0.7f);
                    LookMovement();
                    return;
                }
            }

            StopMoving();
            _desiredStance = Stance.Crouching;
            if (_perception.HasLastKnownEnemyPosition)
                LookAt(_perception.LastKnownEnemyPosition + Vector3.up * 1.3f);
            else
                LookYaw(ScanYaw(transform.eulerAngles.y, now, 60f));

            if (!itemUse.IsUsing)
            {
                var started = false;
                try
                {
                    started = itemUse.TryBeginBestHeal();
                }
                catch (Exception)
                {
                    started = false;
                }

                if (!started)
                {
                    TryBoost(now);
                    RequestDecision();
                }
            }
        }

        private void ExecuteMoveToZone(float now)
        {
            var zone = _director != null ? _director.Zone : null;
            var position = transform.position;
            if (zone == null || !BotDirector.SafeZoneActive(zone))
            {
                RequestDecision();
                ExecuteRoam(now);
                return;
            }

            var outsideCurrent = false;
            try
            {
                outsideCurrent = !zone.IsInsideZone(position.x, position.z);
            }
            catch (Exception)
            {
                outsideCurrent = false;
            }

            if (now >= _nextGoalTime || !_hasGoalPoint || _moveFailed ||
                !BotDirector.SafeNextZoneContains(zone, _goalPoint) || FlatDistance(position, _goalPoint) < 5f)
            {
                _nextGoalTime = now + 4f;
                _moveFailed = false;

                // Takipçiler komutanın hedefini, diğerleri kendi tarafındaki bir noktayı kullanır.
                Vector3 candidate;
                if (_director.TryGetObjective(_team, out var objective) && BotDirector.SafeNextZoneContains(zone, objective))
                    candidate = objective + new Vector3(Range(-8f, 8f), 0f, Range(-8f, 8f));
                else
                    candidate = _director.PointInsideNextZone(zone, position, 0.5f);

                if (!SampleDestination(candidate, 30f, out var sampled))
                    sampled = candidate;

                _goalPoint = sampled;
                _hasGoalPoint = true;
            }

            MoveTo(_goalPoint, outsideCurrent ? MoveSpeed.Sprint : MoveSpeed.Run, 4f);
            LookMovement();
        }

        private void ExecuteLoot(float now)
        {
            var loot = _lootTarget;
            if (loot == null || !loot.IsAvailable || (_director != null && _director.IsClaimedByOther(loot, this)))
            {
                _lootTarget = null;
                _nextLootSearch = now;
                RequestDecision();
                ExecuteIdle(now);
                return;
            }

            _director?.Claim(loot, this);

            var position = transform.position;
            var lootPosition = loot.transform.position;
            var distance = FlatDistance(position, lootPosition);
            if (distance < 1.9f && Mathf.Abs(lootPosition.y - position.y) < 2.3f)
            {
                StopMoving();
                PickUp(loot, now);
                return;
            }

            if (_moveFailed || now - _lootStartTime > 25f)
            {
                IgnoreLoot(loot);
                _moveFailed = false;
                RequestDecision();
                return;
            }

            MoveTo(lootPosition, distance > 15f ? MoveSpeed.Run : MoveSpeed.Jog, 1.2f);
            if (distance < 6f)
                LookAt(lootPosition);
            else
                LookMovement();
        }

        private void PickUp(LootPickupComponent loot, float now)
        {
            var accepted = false;
            var wasWeapon = false;
            try
            {
                wasWeapon = loot.Item.Category == ItemCategory.Weapon;
                var result = loot.PickupBy(Combatant);
                accepted = result.Accepted;
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            _director?.ReleaseClaim(loot, this);
            if (!accepted)
                IgnoreLoot(loot);

            if (accepted && wasWeapon)
            {
                var inventory = Combatant.Inventory;
                var active = inventory != null ? inventory.ActiveWeapon : null;
                if (inventory != null && (active == null || !IsUsable(active) || _perception.Target == null))
                    inventory.SelectBestWeapon();
            }

            _lootTarget = null;
            _nextLootSearch = now;
            _equipmentDirty = true;
            RequestDecision();
        }

        private void RefreshLootTarget(float now)
        {
            _nextLootSearch = now + Range(1.4f, 2f);
            if (_lootTarget != null && (!_lootTarget.IsAvailable || IsIgnoredLoot(_lootTarget)))
            {
                _director?.ReleaseClaim(_lootTarget, this);
                _lootTarget = null;
            }

            var radius = _leader != null ? 24f : 45f;
            LootPickupComponent found = null;
            try
            {
                found = LootRegistry.FindNearest(transform.position, radius, _lootFilter);
            }
            catch (Exception)
            {
                found = null;
            }

            if (!ReferenceEquals(found, _lootTarget))
            {
                if (_lootTarget != null)
                    _director?.ReleaseClaim(_lootTarget, this);
                _lootTarget = found;
                if (State == BotState.Loot)
                    _lootStartTime = now;
            }

            _lootDistance = found != null ? FlatDistance(transform.position, found.transform.position) : 999f;
        }

        private bool IsUsefulLoot(LootPickupComponent loot)
        {
            if (loot == null || !loot.IsAvailable || IsIgnoredLoot(loot))
                return false;

            if (_director != null && _director.IsClaimedByOther(loot, this))
                return false;

            if (Mathf.Abs(loot.transform.position.y - transform.position.y) > 6f)
                return false;

            var inventory = Combatant != null ? Combatant.Inventory : null;
            if (inventory == null)
                return false;

            try
            {
                return inventory.WantsItem(loot.Item);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool IsIgnoredLoot(LootPickupComponent loot)
        {
            for (var i = 0; i < _ignoredLoot.Length; i++)
            {
                if (ReferenceEquals(_ignoredLoot[i], loot))
                    return true;
            }

            return false;
        }

        private void IgnoreLoot(LootPickupComponent loot)
        {
            if (loot == null)
                return;

            _director?.ReleaseClaim(loot, this);
            _ignoredLoot[_ignoredLootCursor] = loot;
            _ignoredLootCursor = (_ignoredLootCursor + 1) % _ignoredLoot.Length;
            if (ReferenceEquals(_lootTarget, loot))
                _lootTarget = null;
        }

        // ------------------------------------------------------------------ çatışma hareketi

        private void ExecuteEngage(float now)
        {
            var target = _perception.Target;
            var position = transform.position;
            if (target == null || !target.IsAlive)
            {
                // Hedef algı adımları arasında kayboldu: son bilinen konuma dön, karar bekle.
                StopMoving();
                if (_perception.HasLastKnownEnemyPosition)
                    LookAt(_perception.LastKnownEnemyPosition + Vector3.up * 1.3f);
                RequestDecision();
                return;
            }

            LookAtTarget();
            var targetPosition = target.transform.position;
            var distance = FlatDistance(position, targetPosition);
            var inventory = Combatant.Inventory;
            var weapon = inventory != null ? inventory.ActiveWeapon : null;
            var armed = weapon != null && IsUsable(weapon);

            if (!armed)
            {
                // Silahsız: yakına koş, yumrukla.
                _tactic = EngageTactic.Chase;
                if (distance > 1.4f)
                    MoveTo(targetPosition, MoveSpeed.Sprint, 1.1f);
                else
                    StopMoving();
                return;
            }

            if (now >= _nextTacticTime || (_tactic == EngageTactic.Cover && FlatDistance(position, _tacticPoint) < 0.8f && !weapon.IsReloading && now - _perception.LastEnemyDamageTime > 2f && HealthFraction > 0.5f))
                ChooseTactic(now, target, distance, weapon);

            switch (_tactic)
            {
                case EngageTactic.Cover:
                    if (FlatDistance(position, _tacticPoint) > 0.8f && !_moveFailed)
                    {
                        MoveTo(_tacticPoint, MoveSpeed.Sprint, 0.6f);
                    }
                    else
                    {
                        StopMoving();
                        _desiredStance = Stance.Crouching;
                    }

                    break;

                case EngageTactic.Advance:
                    if (now >= _nextGoalTime || !_hasGoalPoint)
                    {
                        _nextGoalTime = now + 0.8f;
                        var preferred = BotTactics.PreferredRange(weapon.Definition);
                        var toSelf = position - targetPosition;
                        toSelf.y = 0f;
                        var dir = toSelf.sqrMagnitude > 0.01f ? toSelf.normalized : -transform.forward;
                        var point = targetPosition + dir * (preferred * 0.8f);
                        if (!SampleDestination(point, 6f, out var sampled))
                            sampled = point;
                        _goalPoint = sampled;
                        _hasGoalPoint = true;
                    }

                    if (distance <= BotTactics.PreferredRange(weapon.Definition) || _moveFailed)
                    {
                        _moveFailed = false;
                        _nextTacticTime = Mathf.Min(_nextTacticTime, now + 0.2f);
                        StopMoving();
                    }
                    else
                    {
                        MoveTo(_goalPoint, distance > 60f ? MoveSpeed.Run : MoveSpeed.Jog, 2.5f);
                    }

                    break;

                case EngageTactic.Strafe:
                    if (FlatDistance(position, _tacticPoint) > 0.6f && !_moveFailed)
                    {
                        MoveTo(_tacticPoint, distance < 20f ? MoveSpeed.Jog : MoveSpeed.Walk, 0.5f);
                    }
                    else
                    {
                        _moveFailed = false;
                        StopMoving();
                        if (_crouchWhileHolding)
                            _desiredStance = Stance.Crouching;
                    }

                    break;

                default: // Hold
                    StopMoving();
                    if (_proneWhileHolding)
                        _desiredStance = Stance.Prone;
                    else if (_crouchWhileHolding)
                        _desiredStance = Stance.Crouching;
                    break;
            }
        }

        private float HealthFraction
        {
            get
            {
                var health = Combatant.Health;
                return health != null && health.Max > 0f ? health.Current / health.Max : 0f;
            }
        }

        private void ChooseTactic(float now, Combatant target, float distance, WeaponRuntimeService weapon)
        {
            _nextTacticTime = now + Range(1.3f, 2.6f);
            _moveFailed = false;
            _hasGoalPoint = false;

            var position = transform.position;
            var preferred = BotTactics.PreferredRange(weapon.Definition);
            var health = HealthFraction;
            var reloading = weapon.IsReloading || weapon.CurrentAmmo <= 0;
            var recentlyHit = now - _perception.LastEnemyDamageTime < 2.5f;

            // Yaralı ya da çatışma ortasında şarjör değiştiriyor: siper.
            if ((health < 0.5f || (reloading && distance < 45f) || (recentlyHit && health < 0.7f)) && now >= _nextCoverSearch)
            {
                _nextCoverSearch = now + 2f;
                if (BotTactics.TryFindCover(position, target.EyePosition, 20f, _useNavMesh, out var cover))
                {
                    _tactic = EngageTactic.Cover;
                    _tacticPoint = cover;
                    _nextTacticTime = now + Range(3f, 5f);
                    if (health < 0.5f && distance > 15f)
                        TryThrowSmoke(target, distance, now);
                    return;
                }
            }

            // Sabit hedefe bomba (özellikle bombacı).
            var targetSpeed = target.Velocity.sqrMagnitude;
            if (distance > 12f && distance < 34f && targetSpeed < 1f && now >= _nextGrenadeCheck)
            {
                _nextGrenadeCheck = now + 2f;
                var chance = _role == TeamRole.Grenadier ? 0.3f : 0.08f * (0.5f + Profile.AggressionChance);
                if (Rand() < chance && TryThrowFrag(target.transform.position, now))
                    return;
            }

            // Menzil dışındaki hedefe yaklaş (saldırganlık).
            var range = weapon.Definition != null && weapon.Definition.Range > 1f ? weapon.Definition.Range : 150f;
            if (distance > range * 0.95f || (distance > preferred * 1.6f && Rand() < Profile.AggressionChance + 0.15f))
            {
                _tactic = EngageTactic.Advance;
                return;
            }

            var r = Rand();
            float holdChance;
            if (distance > 70f)
                holdChance = 0.7f;
            else if (distance > 25f)
                holdChance = 0.45f;
            else
                holdChance = 0.2f;

            _crouchWhileHolding = distance > 18f && Rand() < 0.7f;
            _proneWhileHolding = weapon.Definition != null &&
                                 (weapon.Definition.Category == WeaponCategory.Sniper || weapon.Definition.Category == WeaponCategory.Lmg) &&
                                 distance > 90f && Rand() < 0.4f;

            if (r < holdChance)
            {
                _tactic = EngageTactic.Hold;
                return;
            }

            // Yan adım: hedefe dik yönde 2.5-6 m; çok yakınsa biraz geri.
            _tactic = EngageTactic.Strafe;
            var toTarget = target.transform.position - position;
            toTarget.y = 0f;
            var forward = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : transform.forward;
            var right = new Vector3(forward.z, 0f, -forward.x);
            var side = Rand() < 0.5f ? -1f : 1f;
            var point = position + right * (side * Range(2.5f, 6f));
            if (distance < preferred * 0.5f)
                point -= forward * Range(1f, 3f);

            if (!SampleDestination(point, 2.5f, out var sampled))
            {
                point = position - right * (side * Range(2.5f, 5f));
                if (!SampleDestination(point, 2.5f, out sampled))
                {
                    _tactic = EngageTactic.Hold;
                    return;
                }
            }

            _tacticPoint = sampled;
        }

        private void TryBoost(float now)
        {
            if (now < _nextBoostAttempt)
                return;

            _nextBoostAttempt = now + Range(4f, 8f);
            var itemUse = Combatant.ItemUse;
            var boost = Combatant.Boost;
            if (itemUse == null || boost == null || itemUse.IsUsing)
                return;

            if (now - _perception.LastSeenTime < 8f || now - _perception.LastEnemyDamageTime < 8f)
                return;

            // Can eksikse ya da boost düşükse (ve elde varsa) kullan.
            if (boost.Value < 40f && (HealthFraction < 0.95f || Rand() < 0.25f))
            {
                try
                {
                    itemUse.TryBeginBestBoost();
                }
                catch (Exception)
                {
                    // boost yok
                }
            }
        }

        private float ScanYaw(float baseYaw, float now, float range = 70f)
        {
            if (now >= _nextScanTime)
            {
                _nextScanTime = now + Range(2.5f, 5f);
                _scanOffset = Range(-range, range);
            }

            return baseYaw + Mathf.Clamp(_scanOffset, -range, range);
        }
    }
}
