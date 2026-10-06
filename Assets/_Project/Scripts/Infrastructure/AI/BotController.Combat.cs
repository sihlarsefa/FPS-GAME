using System;
using Project.Application.Catalogs;
using Project.Application.Dialogue;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Combat;
using UnityEngine;

namespace Project.Infrastructure.AI
{
    /// <summary>
    /// Silah kullanımı: tepki gecikmesi, seri (burst) atış (<see cref="WeaponRuntimeService.TryTrigger"/> →
    /// <see cref="BallisticsSystem.FireWeapon"/>, başlangıç göz), geri tepme, mesafeye göre ateş modu ve silah seçimi,
    /// otomatik/taktik şarjör değiştirme, dost ateş hattı kontrolü, silahsızken yumruk, el/sis bombası ve
    /// tehlikeden (el bombası, topçu) kaçış. Kare başına bellek ayırmaz.
    /// </summary>
    public sealed partial class BotController
    {
        private const float GrenadeMaxSpeed = 21f;
        private const float GrenadeMinDistance = BotCombatRules.FragMinRange;
        private const float GrenadeMaxDistance = 38f;
        private const float WeaponSwitchCooldown = 2.5f;
        private const float AllyLineOfFireRadius = 0.75f;

        // tetik
        private int _burstRemaining;
        private float _burstPauseUntil;
        private bool _triggerWasHeld;
        private float _nextSemiShotTime;
        private WeaponRuntimeService _configuredWeapon;
        private float _nextWeaponSwitchTime;
        private float _nextPunchTime;

        // bombalar
        private float _nextGrenadeCheck;
        private float _nextGrenadeTime;
        private float _nextSmokeTime;
        private bool _suppressing;
        private float _nextSuppressCallout;

        // tehlikeden kaçış
        private float _nextDangerCheck;
        private float _evadeUntil;
        private Vector3 _evadePoint;

        /// <summary>Elde tutulan silah (yoksa null = yumruk).</summary>
        public WeaponRuntimeService ActiveWeapon
        {
            get
            {
                var inventory = Combatant != null ? Combatant.Inventory : null;
                return inventory != null ? inventory.ActiveWeapon : null;
            }
        }

        /// <summary>Şu an seri atış yapıyor mu.</summary>
        public bool IsFiring => _burstRemaining > 0;

        // ------------------------------------------------------------------ silah bakımı

        /// <summary>Her kare: etkin silahın zamanlayıcıları; boş şarjörde otomatik doldurma ya da silah değiştirme.</summary>
        private void TickWeapon(float dt)
        {
            var inventory = Combatant.Inventory;
            if (inventory == null)
                return;

            var weapon = inventory.ActiveWeapon;
            if (!ReferenceEquals(weapon, _configuredWeapon))
            {
                _configuredWeapon = weapon;
                _burstRemaining = 0;
                _triggerWasHeld = false;
                if (weapon != null)
                    weapon.TriggerInterruptsReload = false;
            }

            if (weapon == null)
                return;

            weapon.Tick(dt);

            if (weapon.CurrentAmmo > 0 || weapon.IsReloading)
                return;

            var itemUse = Combatant.ItemUse;
            if (weapon.CanReload)
            {
                if (itemUse == null || !itemUse.IsUsing)
                    weapon.TryBeginReload();
                return;
            }

            // Bu silahın mermisi bitti: kullanılabilir başka silaha geç, yoksa yumruğa.
            var now = Time.time;
            if (now < _nextWeaponSwitchTime)
                return;

            _nextWeaponSwitchTime = now + 0.6f;
            if (!SelectUsableWeapon(inventory, _perception.Target != null ? _perception.TargetDistance : 40f))
                inventory.SetActiveSlot(-1);
        }

        /// <summary>Karar adımında: duruma/mesafeye en uygun silahı kuşan (histerezisli).</summary>
        private void MaintainWeapon(float now)
        {
            var inventory = Combatant.Inventory;
            if (inventory == null || _burstRemaining > 0 || now < _nextWeaponSwitchTime)
                return;

            var itemUse = Combatant.ItemUse;
            if (itemUse != null && itemUse.IsUsing)
                return;

            var active = inventory.ActiveWeapon;
            var target = _perception.Target;
            var distance = target != null ? _perception.TargetDistance
                : _perception.HasLastKnownEnemyPosition && now - _perception.LastSeenTime < 15f
                    ? FlatDistance(transform.position, _perception.LastKnownEnemyPosition)
                    : 40f;

            if (!inventory.HasUsableWeapon)
            {
                if (active != null)
                {
                    _nextWeaponSwitchTime = now + 1f;
                    inventory.SetActiveSlot(-1);
                }

                return;
            }

            if (active == null || !IsUsable(active))
            {
                _nextWeaponSwitchTime = now + 0.8f;
                SelectUsableWeapon(inventory, distance);
                return;
            }

            if (active.IsReloading)
            {
                // Yakın çatışmada boş şarjör: tabancaya geçmek doldurmaktan hızlıdır.
                if (target != null && distance < 22f && active.CurrentAmmo == 0 && active.ReloadProgress < 0.4f &&
                    active.Definition.Category != WeaponCategory.Pistol)
                {
                    var sidearm = inventory.GetWeapon(InventoryService.SidearmSlot);
                    if (sidearm != null && sidearm.CurrentAmmo > 0 && inventory.SetActiveSlot(InventoryService.SidearmSlot))
                        _nextWeaponSwitchTime = now + WeaponSwitchCooldown;
                }

                return;
            }

            var activeSlot = inventory.ActiveSlot;
            var bestSlot = activeSlot;
            var bestScore = WeaponScore(active, activeSlot, distance) + 2.5f; // histerezis
            for (var i = 0; i < InventoryService.WeaponSlotCount; i++)
            {
                if (i == activeSlot)
                    continue;

                var candidate = inventory.GetWeapon(i);
                if (candidate == null || !IsUsable(candidate))
                    continue;

                var score = WeaponScore(candidate, i, distance);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestSlot = i;
                }
            }

            if (bestSlot != activeSlot && inventory.SetActiveSlot(bestSlot))
                _nextWeaponSwitchTime = now + WeaponSwitchCooldown;
        }

        private bool SelectUsableWeapon(InventoryService inventory, float distance)
        {
            var bestSlot = -1;
            var bestScore = float.MinValue;
            for (var i = 0; i < InventoryService.WeaponSlotCount; i++)
            {
                var candidate = inventory.GetWeapon(i);
                if (candidate == null || !IsUsable(candidate))
                    continue;

                var score = WeaponScore(candidate, i, distance);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestSlot = i;
                }
            }

            return bestSlot >= 0 && inventory.SetActiveSlot(bestSlot);
        }

        private static float WeaponScore(WeaponRuntimeService weapon, int slot, float distance)
        {
            var score = BotTactics.RangeSuitability(weapon.Definition, distance);
            score += weapon.CurrentAmmo > 0 ? 1f : -3f;
            if (slot == InventoryService.SidearmSlot)
                score -= 1f;
            return score;
        }

        /// <summary>Şarjörde ya da yedekte mermisi var mı.</summary>
        private static bool IsUsable(WeaponRuntimeService weapon)
        {
            if (weapon == null)
                return false;

            return weapon.CurrentAmmo > 0 || weapon.IsReloading || weapon.HasInfiniteReserve || weapon.ReserveAmmo > 0;
        }

        /// <summary>Sakin anda yarım şarjörü doldur.</summary>
        private void TryTacticalReload(float now)
        {
            var weapon = ActiveWeapon;
            if (weapon == null || weapon.IsReloading || !weapon.CanReload || _burstRemaining > 0)
                return;

            if (now - _perception.LastSeenTime < 2.5f || now - _perception.LastEnemyDamageTime < 2.5f)
                return;

            var itemUse = Combatant.ItemUse;
            if (itemUse != null && itemUse.IsUsing)
                return;

            if (weapon.CurrentAmmo < Mathf.CeilToInt(weapon.MagazineSize * 0.6f))
                weapon.TryBeginReload();
        }

        // ------------------------------------------------------------------ tetik

        private void UpdateTrigger(float now)
        {
            var combatant = Combatant;
            var target = _perception.Target;
            var itemUse = combatant.ItemUse;

            if (_suppressing && State == BotState.Engage && _lookMode == LookMode.Point && !_coverHidden &&
                !(_director != null && _director.MatchEnded) && (itemUse == null || !itemUse.IsUsing))
            {
                UpdateSuppressTrigger(now);
                return;
            }

            var canEngage = State == BotState.Engage && _lookMode == LookMode.Target && target != null && target.IsAlive &&
                            target.IsTargetable && !(_director != null && _director.MatchEnded) &&
                            (itemUse == null || !itemUse.IsUsing);
            if (!canEngage)
            {
                CeaseFire();
                return;
            }

            // Tepki gecikmesi: hedefi ilk gördükten sonra (180-450 ms, beceriye bağlı); hedef değiştirirken ek gecikme.
            if (now - _perception.TargetAcquiredTime < Profile.ReactionSeconds || TriggerDelayActive(target, now))
            {
                CeaseFire();
                return;
            }

            // Siperde saklanma evresi: ateş yok (başını çıkarınca ateş eder).
            if (_coverHidden)
            {
                CeaseFire();
                return;
            }

            var inventory = combatant.Inventory;
            var weapon = inventory != null ? inventory.ActiveWeapon : null;
            if (weapon == null || !IsUsable(weapon))
            {
                CeaseFire();
                TryMelee(target, now);
                return;
            }

            if (weapon.IsReloading || weapon.IsEquipping || weapon.CurrentAmmo <= 0)
            {
                CeaseFire();
                return;
            }

            var eye = EyePosition;
            var distance = Vector3.Distance(eye, target.transform.position);
            var definition = weapon.Definition;
            var range = definition != null && definition.Range > 1f ? definition.Range : 150f;
            if (distance > range * 1.05f)
            {
                CeaseFire();
                return;
            }

            if (_burstRemaining <= 0)
            {
                if (now < _burstPauseUntil || !_aimOnTarget)
                {
                    _triggerWasHeld = false;
                    return;
                }

                if (AllyInLineOfFire(eye, target.GetAimPosition(BodyPart.Torso)))
                {
                    _burstPauseUntil = now + 0.35f;
                    _triggerWasHeld = false;
                    return;
                }

                StartBurst(weapon, distance, now);
            }
            else if (_aimOffYaw > 14f)
            {
                // Hedef namlu doğrultusundan çıktı (ani dönüş): seriyi kes.
                EndBurst(now, distance);
                return;
            }

            PullTrigger(weapon, distance, now);
        }

        /// <summary>
        /// Bastırma ateşi: hedef siper arkasına çekildi — son bilinen konuma kısa, seyrek seriler. Mermi harcamamak için
        /// şarjör %25 altındaysa ve dost ateş hattındaysa susar.
        /// </summary>
        private void UpdateSuppressTrigger(float now)
        {
            var weapon = ActiveWeapon;
            if (weapon == null || !IsUsable(weapon) || weapon.IsReloading || weapon.IsEquipping || weapon.CurrentAmmo <= 0 ||
                !_perception.HasLastKnownEnemyPosition)
            {
                CeaseFire();
                return;
            }

            var eye = EyePosition;
            var point = _perception.LastKnownEnemyPosition + Vector3.up * 1.1f;
            var to = point - eye;
            var distance = to.magnitude;
            var definition = weapon.Definition;
            var range = definition != null && definition.Range > 1f ? definition.Range : 150f;
            if (distance > range || weapon.CurrentAmmo * 4 < weapon.MagazineSize)
            {
                CeaseFire();
                return;
            }

            var yawOff = Mathf.DeltaAngle(_aimYaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            if (_burstRemaining <= 0)
            {
                if (now < _burstPauseUntil || !BotSquadTactics.AimedAt(yawOff, distance))
                {
                    _triggerWasHeld = false;
                    return;
                }

                if (AllyInLineOfFire(eye, point))
                {
                    _burstPauseUntil = now + 0.5f;
                    return;
                }

                StartBurst(weapon, distance, now);
                // Kapatıcı ateş: LMG/AR siper kenarına 3-5 mermilik seriler (pencere içinde, seyrek).
                var supDef = weapon.Definition;
                var supRounds = supDef != null && BotCombatRules.IsSuppressiveWeapon(supDef.Category)
                    ? BotCombatRules.SuppressiveBurstRounds(Rand())
                    : 5;
                _burstRemaining = Mathf.Min(_burstRemaining, supRounds);
                if (now >= _nextSuppressCallout)
                {
                    _nextSuppressCallout = now + 12f;
                    Callout(DialogueCats.Suppress);
                }
            }
            else if (Mathf.Abs(yawOff) > 14f)
            {
                EndBurst(now, distance);
                return;
            }

            PullTrigger(weapon, distance, now);
            if (_burstRemaining <= 0)
                _burstPauseUntil += Range(0.6f, 1.4f); // bastırma: seyrek
        }

        /// <summary>Bot yerel oyuncuya otomatik/MG ateşi açtı (mesafe m, çap mm); Presentation bastırma sistemi dinler.</summary>
        public static event Action<float, float> IncomingFireAtLocalPlayer;

        private void ReportIncomingFire(WeaponRuntimeService weapon, float distance)
        {
            if (IncomingFireAtLocalPlayer == null)
                return;
            var target = _perception.Target;
            var def = weapon.Definition;
            if (target == null || !target.IsLocalPlayer || def == null)
                return;
            if (weapon.CurrentFireMode != FireMode.Auto && def.Category != WeaponCategory.Lmg)
                return;
            float mm;
            switch (def.Category)
            {
                case WeaponCategory.Lmg: mm = 7.62f; break;
                case WeaponCategory.Smg: mm = 9f; break;
                default: mm = 5.56f; break;
            }

            try { IncomingFireAtLocalPlayer(distance, mm); }
            catch (Exception e) { LogThrottled(e); }
        }

        private void PullTrigger(WeaponRuntimeService weapon, float distance, float now)
        {
            var mode = weapon.CurrentFireMode;
            bool held;
            bool pressed;
            if (mode == FireMode.Auto)
            {
                held = true;
                pressed = !_triggerWasHeld;
            }
            else
            {
                held = false;
                pressed = now >= _nextSemiShotTime && !weapon.IsBurstActive;
                if (pressed)
                    _nextSemiShotTime = now + SemiShotInterval(weapon, distance);
            }

            var fired = weapon.TryTrigger(held, pressed);
            _triggerWasHeld = held;

            if (!fired)
                return;

            FireShot(weapon);
            ReportIncomingFire(weapon, distance);
            _burstRemaining--;
            if (_burstRemaining <= 0 || weapon.CurrentAmmo <= 0)
                EndBurst(now, distance);
        }

        private void StartBurst(WeaponRuntimeService weapon, float distance, float now)
        {
            var definition = weapon.Definition;
            var category = definition != null ? definition.Category : WeaponCategory.AssaultRifle;

            // Uzakta tek atış, yakında otomatik.
            if (definition != null && definition.SupportsFireMode(FireMode.Auto))
            {
                if (distance > 70f && category != WeaponCategory.Lmg && definition.SupportsFireMode(FireMode.Single))
                    weapon.TrySetFireMode(FireMode.Single);
                else
                    weapon.TrySetFireMode(FireMode.Auto);
            }

            int shots;
            switch (category)
            {
                case WeaponCategory.Sniper:
                    shots = 1;
                    break;
                case WeaponCategory.Dmr:
                    shots = _rng.Next(1, 4);
                    break;
                case WeaponCategory.Shotgun:
                    shots = _rng.Next(1, 3);
                    break;
                case WeaponCategory.Pistol:
                    shots = _rng.Next(2, 5);
                    break;
                default:
                {
                    var min = Mathf.Max(1, Profile.BurstMin);
                    var max = Mathf.Max(min, Profile.BurstMax);
                    shots = _rng.Next(min, max + 1);
                    if (category == WeaponCategory.Lmg)
                        shots += 2;
                    if (weapon.CurrentFireMode != FireMode.Auto)
                        shots = Mathf.Min(shots, 3);
                    else if (distance > 60f)
                        shots = Mathf.Max(1, shots - 2);
                    else if (distance < 12f)
                        shots += 2;
                    break;
                }
            }

            // Baskı altında kısa, kör seriler.
            shots = Mathf.Max(1, Mathf.RoundToInt(shots * BotSkill.SuppressedBurstScale(EffectiveSuppression)));
            _burstShotIndex = 0;
            _burstRemaining = Mathf.Max(1, shots);
            _aimPart = RollAimPart(distance);
            _triggerWasHeld = false;
            _nextSemiShotTime = now;
        }

        private void EndBurst(float now, float distance)
        {
            _burstRemaining = 0;
            _triggerWasHeld = false;

            var pause = Mathf.Max(0.1f, Profile.BurstPauseSeconds) * Range(0.7f, 1.4f);
            if (distance > 60f)
                pause *= 1.6f;

            var weapon = ActiveWeapon;
            if (weapon != null && weapon.Definition != null)
            {
                var category = weapon.Definition.Category;
                if (category == WeaponCategory.Sniper)
                    pause = Mathf.Max(pause * 2.2f, weapon.FireIntervalSeconds + 0.4f);
                else if (category == WeaponCategory.Dmr)
                    pause *= 1.5f;
            }

            _burstPauseUntil = now + pause;
        }

        private void CeaseFire()
        {
            if (_burstRemaining > 0)
                _burstRemaining = 0;
            _triggerWasHeld = false;
        }

        private float SemiShotInterval(WeaponRuntimeService weapon, float distance)
        {
            var definition = weapon.Definition;
            float interval;
            switch (definition != null ? definition.Category : WeaponCategory.AssaultRifle)
            {
                case WeaponCategory.Pistol: interval = 0.24f; break;
                case WeaponCategory.Dmr: interval = 0.5f; break;
                case WeaponCategory.Sniper: interval = 1.2f; break;
                case WeaponCategory.Shotgun: interval = 0.75f; break;
                default: interval = 0.26f; break;
            }

            interval *= 1f + Mathf.Clamp01(distance / 150f) * 0.6f;
            interval *= Range(0.85f, 1.25f);
            return Mathf.Max(interval, weapon.FireIntervalSeconds);
        }

        private void FireShot(WeaponRuntimeService weapon)
        {
            var combatant = Combatant;
            var eye = EyePosition;
            var forward = AimForward;
            var speed01 = Mathf.Clamp01(Mathf.Sqrt(_velocity.x * _velocity.x + _velocity.z * _velocity.z) / 6f);
            var aiming = speed01 < 0.5f;
            var stance = combatant.Stance;
            var spread = weapon.GetSpreadAngle(aiming, speed01, true, stance);
            var muzzle = Model != null ? Model.MuzzlePosition : Vector3.zero;

            try
            {
                var ballistics = BallisticsSystem.Instance != null ? BallisticsSystem.Instance : BallisticsSystem.GetOrCreate();
                if (ballistics != null)
                    ballistics.FireWeapon(combatant, weapon, eye, forward, spread, muzzle);

                SuppressAlongShot(eye, forward, weapon.Definition);
            }
            catch (Exception e)
            {
                LogThrottled(e);
            }

            try
            {
                Model?.PlayFire();
            }
            catch (Exception e)
            {
                LogThrottled(e);
            }

            // Geri tepme: bakış yukarı/yana kayar, dönüş hızıyla toparlanır.
            weapon.GetRecoilKick(aiming, stance, Rand(), out var kickPitch, out var kickYaw);
            var control = RecoilControlNow();
            _burstShotIndex++;
            _aimPitch = Mathf.Clamp(_aimPitch + kickPitch * control, -MaxPitch, MaxPitch);
            _aimYaw = Mathf.Repeat(_aimYaw + kickYaw * control, 360f);
        }

        /// <summary>Atış hattında (göz → hedef) bir tim arkadaşı var mı.</summary>
        private bool AllyInLineOfFire(Vector3 eye, Vector3 targetPoint)
        {
            var line = targetPoint - eye;
            var length = line.magnitude;
            if (length < 0.5f)
                return false;

            var direction = line / length;
            var all = CombatantRegistry.All;
            var self = Combatant;
            for (var i = 0; i < all.Count; i++)
            {
                var ally = all[i];
                if (ally == null || ReferenceEquals(ally, self) || !ally.IsAlive || ally.Team != _team)
                    continue;

                var center = ally.AimPoint != null ? ally.AimPoint.position : ally.transform.position + Vector3.up * 1.1f;
                var offset = center - eye;
                var along = Vector3.Dot(offset, direction);
                if (along <= 0.3f || along >= length - 0.3f)
                    continue;

                var perpendicular = offset - direction * along;
                if (perpendicular.sqrMagnitude < AllyLineOfFireRadius * AllyLineOfFireRadius)
                    return true;
            }

            return false;
        }

        // ------------------------------------------------------------------ yakın dövüş

        private void TryMelee(Combatant target, float now)
        {
            if (target == null || now < _nextPunchTime)
                return;

            var distance = FlatDistance(transform.position, target.transform.position);
            if (distance > MeleeAttack.DefaultRange + 0.25f || _aimOffYaw > 30f)
                return;

            _nextPunchTime = now + Range(0.55f, 0.85f);
            var eye = EyePosition;
            var direction = target.GetAimPosition(BodyPart.Torso) - eye;
            if (direction.sqrMagnitude < 1e-4f)
                direction = AimForward;

            try
            {
                MeleeAttack.TryPunch(Combatant, eye, direction.normalized, MeleeAttack.DefaultRange);
            }
            catch (Exception e)
            {
                LogThrottled(e);
            }
        }

        // ------------------------------------------------------------------ bombalar

        /// <summary>Hedef noktaya (siper arkasındaki düşman) parçalı el bombası. Atıldıysa true.</summary>
        private bool TryThrowFrag(Vector3 targetPosition, float now)
        {
            if (now < _nextGrenadeTime || !GameContext.HasAuthority)
                return false;

            var combatant = Combatant;
            var inventory = combatant.Inventory;
            if (inventory == null || inventory.GetCount(ItemIds.FragGrenade) <= 0)
                return false;

            var itemUse = combatant.ItemUse;
            if (itemUse != null && itemUse.IsUsing)
                return false;

            var position = transform.position;
            var distance = FlatDistance(position, targetPosition);
            if (!BotCombatRules.FragRangeOk(distance))
                return false;

            if (BotTactics.AllyNear(targetPosition, ThrowableProjectile.FragRadius + 2f, _team, combatant) ||
                BotTactics.AllyNear(position, 1.5f, _team, combatant) && distance < 14f)
                return false;

            // Sabit, alçak siper arkasındaki hedefe bombayı 1.5 sn pişir (havada kalma süresi kısalır, kaçamaz).
            var tgt = _perception.Target;
            var targetStatic = tgt != null && tgt.Velocity.sqrMagnitude < 0.25f && FlatDistance(tgt.transform.position, targetPosition) < 2.5f;
            var lowCover = tgt != null && tgt.Stance != Stance.Standing;
            var baseFuse = Range(3.1f, 4f);
            var fuse = baseFuse - BotCombatRules.FragCookSeconds(targetStatic, lowCover, baseFuse);

            // Atış hatası: zorluk ve mesafeyle büyür.
            var error = 0.6f + distance * Mathf.Tan(Mathf.Max(0.5f, Profile.AimErrorDegrees) * Mathf.Deg2Rad) * 1.4f;
            var aim = targetPosition + new Vector3(Gaussian() * error * 0.6f, 0f, Gaussian() * error * 0.6f);

            if (!TryLaunch(ThrowableKind.Frag, aim, fuse))
                return false;

            _nextGrenadeTime = now + Range(14f, 24f);
            inventory.Consume(ItemIds.FragGrenade, 1);
            Callout(DialogueCats.GrenadeThrow);
            return true;
        }

        /// <summary>Tehditle arasına sis bombası (geri çekilmeyi örtmek için). Atıldıysa true.</summary>
        private bool TryThrowSmoke(Combatant target, float distance, float now)
        {
            if (target == null || now < _nextSmokeTime || !GameContext.HasAuthority)
                return false;

            var inventory = Combatant.Inventory;
            if (inventory == null || inventory.GetCount(ItemIds.SmokeGrenade) <= 0)
                return false;

            var position = transform.position;
            var toTarget = target.transform.position - position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 1f)
                return false;

            var reach = Mathf.Clamp(distance * 0.35f, 4f, 14f);
            var point = position + toTarget.normalized * reach;
            if (BotTactics.TryGroundPoint(point, out var ground))
                point = ground;

            return TryThrowSmokeAtPoint(point, now);
        }

        /// <summary>Verilen noktaya sis bombası (yaralıyı ya da geri çekilmeyi örtmek için). Atıldıysa true.</summary>
        private bool TryThrowSmokeAtPoint(Vector3 point, float now)
        {
            if (now < _nextSmokeTime || !GameContext.HasAuthority)
                return false;

            var inventory = Combatant.Inventory;
            if (inventory == null || inventory.GetCount(ItemIds.SmokeGrenade) <= 0)
                return false;

            var distance = FlatDistance(transform.position, point);
            if (distance < 3f || distance > GrenadeMaxDistance)
                return false;

            if (!TryLaunch(ThrowableKind.Smoke, point, ThrowableProjectile.SmokeFuseSeconds))
                return false;

            _nextSmokeTime = now + Range(20f, 35f);
            inventory.Consume(ItemIds.SmokeGrenade, 1);
            Callout(DialogueCats.Smoke);
            return true;
        }

        private bool TryLaunch(ThrowableKind kind, Vector3 aim, float fuseSeconds)
        {
            var eye = EyePosition;
            var flat = aim - transform.position;
            flat.y = 0f;
            var forward = flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward;
            var origin = eye + forward * 0.45f + Vector3.up * 0.15f;

            if (!BotTactics.SolveThrow(origin, aim, GrenadeMaxSpeed, out var velocity))
                return false;

            // Elden çıkış yayı duvara/tavana çarpmasın.
            var probe = origin + velocity.normalized * 2.5f;
            if (Physics.Linecast(eye, probe, GameLayers.LineOfSightMask, QueryTriggerInteraction.Ignore))
                return false;

            try
            {
                ThrowableProjectile.Throw(kind, origin, velocity, Combatant.Id, fuseSeconds);
            }
            catch (Exception e)
            {
                LogThrottled(e);
                return false;
            }

            try
            {
                GameAudio.Play(SoundId.GrenadePin, origin, 0.7f, Range(0.95f, 1.05f), 25f);
            }
            catch (Exception)
            {
                // ses sistemi yok
            }

            // Atış hareketi kısa bir süre ateşi keser.
            _burstRemaining = 0;
            _triggerWasHeld = false;
            _burstPauseUntil = Mathf.Max(_burstPauseUntil, Time.time + 0.8f);
            return true;
        }

        // ------------------------------------------------------------------ tehlikeden kaçış

        /// <summary>Yakındaki pimi çekilmiş el bombası ya da topçu atış alanı: dışarı depar.</summary>
        private void CheckDangers(float now)
        {
            _nextDangerCheck = now + Range(0.2f, 0.3f);
            if (now < _evadeUntil)
                return;

            var position = transform.position;

            var active = ThrowableProjectile.Active;
            for (var i = 0; i < active.Count; i++)
            {
                var grenade = active[i];
                if (grenade == null || grenade.HasDetonated || grenade.Kind != ThrowableKind.Frag)
                    continue;

                var radius = grenade.DangerRadius + 1.5f;
                var offset = position - grenade.Position;
                if (offset.sqrMagnitude > radius * radius || grenade.FuseRemaining > 3.6f)
                    continue;

                // Görmediği bombayı (çok yakın değilse) fark etmez.
                if (offset.sqrMagnitude > 25f && !BotPerception.IsClear(EyePosition, grenade.Position + Vector3.up * 0.2f))
                    continue;

                offset.y = 0f;
                var away = offset.sqrMagnitude > 0.01f ? offset.normalized : -transform.forward;
                var escape = position + away * (radius - offset.magnitude + 2.5f);
                if (!SampleDestination(escape, 4f, out _evadePoint))
                    _evadePoint = escape;

                _evadeUntil = now + Mathf.Clamp(grenade.FuseRemaining + 0.3f, 0.8f, 3.5f);
                return;
            }

            var artillery = _director != null ? _director.Artillery : null;
            if (artillery == null)
                return;

            bool inDanger;
            try
            {
                inDanger = artillery.IsInDangerZone(new Float3(position.x, position.y, position.z), 4f);
            }
            catch (Exception)
            {
                inDanger = false;
            }

            if (!inDanger)
                return;

            // En yakın etkin atış merkezinden uzaklaş.
            var teamCount = _director.Config != null ? Mathf.Clamp(_director.Config.TeamCount, 2, 32) : 16;
            var bestSqr = float.MaxValue;
            var center = position - transform.forward * 5f;
            for (var t = 0; t < teamCount; t++)
            {
                if (!artillery.TryGetActiveStrike(t, out var strike))
                    continue;

                var c = new Vector3(strike.X, position.y, strike.Z);
                var sqr = (c - position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    center = c;
                }
            }

            var dir = position - center;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : -transform.forward;
            var safeDistance = ArtilleryService.SpreadRadius + ArtilleryService.ShellRadius + 8f;
            var target = center + dir * safeDistance;
            if (_director != null)
                target = _director.ClampToMap(target);

            if (!SampleDestination(target, 10f, out _evadePoint))
                _evadePoint = target;

            _evadeUntil = now + 5f;
        }

        private void LogThrottled(Exception e)
        {
            var now = Time.time;
            if (now < _nextErrorLog)
                return;

            _nextErrorLog = now + 5f;
            Debug.LogException(e, this);
        }
    }
}
