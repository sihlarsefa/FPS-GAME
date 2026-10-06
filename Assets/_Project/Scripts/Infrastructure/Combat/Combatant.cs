using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;
using Project.Infrastructure.Loot;
using UnityEngine;

namespace Project.Infrastructure.Combat
{
    /// <summary>
    /// Oyuncu ve botların ortak savaşan bileşeni: kimlik, tim, rütbe/görev, can, envanter, boost, eşya kullanımı.
    /// Initialize: HealthService/InventoryService/BoostService/ItemUseService oluşturur, IDamageableRegistry ve
    /// CombatantRegistry'ye kaydeder. Update yalnızca otoritede eşya kullanımı ve boost'u işletir.
    /// Ölümde: hedef alınamaz olur, hasar kaydından çıkar, envanter yere düşer (DropLootOnDeath), Died yayınlanır.
    /// CombatantRegistry kaydı nesne yok edilene kadar sürer (bkz. CombatantRegistry).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Combatant : MonoBehaviour, IDamageable, IHealable, IHealthReadModel, IArmored
    {
        private const float StandingEyeHeight = 1.62f;
        private const float CrouchingEyeHeight = 1.1f;
        private const float ProneEyeHeight = 0.35f;

        private readonly List<Hitbox> _hitboxes = new(12);
        private IEventBus _eventBus;
        private IDamageableRegistry _registry;
        private bool _initialized;
        private bool _deathHandled;
        private bool _hitboxScanDone;
        private bool _inDamageRegistry;
        private bool _downed;

        public PlayerId Id { get; private set; } = PlayerId.Invalid;
        public PlayerId OwnerId => Id;
        public string DisplayName { get; private set; }
        public bool IsLocalPlayer { get; private set; }
        public bool IsBot { get; private set; }
        public int Team { get; private set; }
        public TeamRole Role { get; private set; }

        /// <summary>TSK rütbesi (komuta zinciri ve görünen ad). Kurulumda bootstrap/ChainOfCommand atar.</summary>
        public MilitaryRank Rank { get; set; }

        public HealthService Health { get; private set; }
        public InventoryService Inventory { get; private set; }
        public BoostService Boost { get; private set; }
        public ItemUseService ItemUse { get; private set; }

        /// <summary>Uzuv yaraları (bacak/kol/kanama/sıyrık) — sunucu tarafı, botlar da aynı cezaları yer.</summary>
        public LimbDamageState Limbs { get; } = new LimbDamageState();

        /// <summary>Yaralı kol: nişan sarsıntısı çarpanı (HUD/silah kancası).</summary>
        public float LimbSwayMultiplier => Limbs.SwayMultiplier;

        /// <summary>Yaralı kol: ADS süresi çarpanı (1 = normal).</summary>
        public float LimbAdsTimeMultiplier => Limbs.AdsTimeMultiplier;

        /// <summary>Kasklı kafa sıyrığı sersemliği (1,2 sn). AudioMix boğma + ağır sarsıntı bunu okur.</summary>
        public bool IsFlinching => Limbs.IsFlinching;

        public HealthState State => Health != null ? Health.State : new HealthState(0f, 100f);
        public bool IsAlive => Health != null && Health.IsAlive;

        /// <summary>"Yaralı" (DBNO): yaşıyor ama sürünür, ateş edemez, kanar; müttefik kaldırabilir.</summary>
        public bool IsDowned => _downed && IsAlive;

        /// <summary>Kalan kanama süresi (sn); yaralı değilse 0.</summary>
        public float BleedRemaining => IsDowned ? ReviveRuntime.Service.BleedRemaining(Id) : 0f;

        /// <summary>Kaldırma ilerlemesi 0..1.</summary>
        public float ReviveProgress => IsDowned ? ReviveRuntime.Service.ReviveProgress(Id) : 0f;

        /// <summary>Yaralıya düşüldüğünde / kalkınca tetiklenir.</summary>
        public event Action<Combatant> BecameDowned;
        public event Action<Combatant> Recovered;
        public IArmorProvider Armor => Inventory;

        public Transform EyePoint { get; set; }
        public Transform AimPoint { get; set; }
        public Vector3 Velocity { get; set; }
        public bool IsTargetable { get; set; } = true;
        public DropState DropState { get; set; } = DropState.Landed;
        public Stance Stance { get; set; }
        public bool DropLootOnDeath { get; set; } = true;

        public float LastDamageTime { get; private set; } = -999f;
        public Vector3 LastDamageSource { get; private set; }
        public PlayerId LastAttackerId { get; private set; } = PlayerId.Invalid;

        /// <summary>Ölüm anındaki Time.time (hayattaysa -1).</summary>
        public float DeathTime { get; private set; } = -1f;

        /// <summary>Son ölüm bilgisi (öldüren, silah, bölge).</summary>
        public DamageInfo LastDeathInfo { get; private set; }

        public bool IsInitialized => _initialized;

        /// <summary>Rütbeli görünen ad: "Yzb. Ahmet Yılmaz".</summary>
        public string RankedName
        {
            get
            {
                try
                {
                    return RankCatalog.FormatName(Rank, DisplayName);
                }
                catch (Exception)
                {
                    return DisplayName;
                }
            }
        }

        /// <summary>Gözün dünya konumu (EyePoint yoksa duruşa göre tahmin).</summary>
        public Vector3 EyePosition => EyePoint != null ? EyePoint.position : transform.position + Vector3.up * EyeHeightForStance();

        /// <summary>Boost ve eşya kullanımından gelen hareket hızı çarpanı (motor/AI uygular).</summary>
        public float MovementSpeedMultiplier
        {
            get
            {
                if (!IsAlive)
                    return 1f;

                var m = _downed ? 0.18f : 1f; // yaralı sürünür
                if (Boost != null)
                    m *= Boost.SpeedMultiplier;
                if (ItemUse != null)
                    m *= ItemUse.MovementSpeedMultiplier;
                m *= Limbs.MoveSpeedMultiplier;

                return m > 0f && !float.IsNaN(m) ? m : 1f;
            }
        }

        /// <summary>Kayıtlı vuruş kutuları (Hitbox.Bind ekler).</summary>
        public IReadOnlyList<Hitbox> Hitboxes => _hitboxes;

        public event Action<Combatant, DamageInfo> Damaged;
        public event Action<Combatant, DamageInfo> Died;

        public void Initialize(PlayerId id, string displayName, bool isLocalPlayer, bool isBot, int team, TeamRole role,
            IEventBus eventBus, IDamageableRegistry registry, float maxHealth = 100f)
        {
            if (_initialized)
                Teardown();

            if (eventBus == null && GameContext.TryGet<IEventBus>(out var bus))
                eventBus = bus;

            if (registry == null && GameContext.TryGet<IDamageableRegistry>(out var reg))
                registry = reg;

            Id = id;
            DisplayName = string.IsNullOrEmpty(displayName) ? "Asker " + id.Value : displayName;
            IsLocalPlayer = isLocalPlayer;
            IsBot = isBot;
            Team = team;
            Role = role;
            _eventBus = eventBus;
            _registry = registry;

            if (maxHealth <= 0f)
                maxHealth = 100f;

            Health = new HealthService(id, maxHealth, eventBus);
            Inventory = new InventoryService(eventBus, id);
            Boost = new BoostService();
            ItemUse = new ItemUseService(id, Inventory, Health, Boost, eventBus);
            Limbs.Reset();
            ItemUse.Completed += OnItemCompletedClearWounds;

            Health.Damaged += OnHealthDamaged;
            Health.Died += OnHealthDied;

            IsTargetable = true;
            _deathHandled = false;
            DeathTime = -1f;
            LastDamageTime = -999f;
            LastAttackerId = PlayerId.Invalid;
            _initialized = true;

            RegisterDamageable();
            CombatantRegistry.Register(this);
        }

        /// <summary>
        /// Antrenman hedefleri için: canı doldurur, tekrar hedef alınabilir yapar ve hasar kaydına geri ekler.
        /// </summary>
        public void Revive()
        {
            if (!_initialized || Health == null)
                return;

            if (_downed)
            {
                _downed = false;
                ReviveRuntime.Service.Remove(Id);
                // Maç servisi yaralı kaydını düşsün (kaldıran yok: antrenman/yeniden doğma).
                try { _eventBus?.Publish(new RevivedEvent(Id, PlayerId.Invalid)); }
                catch (Exception e) { Debug.LogException(e, this); }
            }

            Health.ResetToFull();
            Boost?.Reset();
            Limbs.Reset();
            _deathHandled = false;
            DeathTime = -1f;
            IsTargetable = true;
            RegisterDamageable();
            if (!ContainsInRegistry())
                CombatantRegistry.Register(this);
        }

        /// <summary>Zırh sonrası hasar (CombatService çağırır). Ölüyse yok sayılır.</summary>
        public void ApplyDamage(DamageInfo damage)
        {
            if (Health == null || !Health.IsAlive || damage.Amount <= 0f)
                return;

            if (_downed)
            {
                // Yaralıyı yalnızca düşman (ya da çevre/kendisi) bitirir; müttefik hasarı yok sayılır.
                if (damage.AttackerId.IsValid && damage.AttackerId != Id
                    && CombatantRegistry.TryGet(damage.AttackerId, out var friend) && friend != null && friend.Team == Team)
                    return;

                FinishOff(damage);
                return;
            }

            if (ShouldGoDown(damage))
            {
                EnterDowned(damage);
                return;
            }

            Health.ApplyDamage(damage);
        }

        private bool ShouldGoDown(DamageInfo damage)
        {
            if (!ReviveRuntime.Enabled || Team < 0 || !GameContext.HasAuthority || Health.Current - damage.Amount > 0.001f)
                return false;

            return ReviveRuntime.CountHealthyAllies(this) > 0;
        }

        private void EnterDowned(DamageInfo damage)
        {
            // Ölümcül hasar 1 can bırakacak biçimde kısılır.
            var capped = Mathf.Max(0f, Health.Current - 1f);
            if (capped > 0f)
                Health.ApplyDamage(damage.WithAmount(capped));

            if (!Health.IsAlive)
                return;

            _downed = true;
            Velocity = Vector3.zero;
            try
            {
                if (ItemUse != null && ItemUse.IsUsing)
                    ItemUse.Cancel();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            if (!ReviveRuntime.Service.TryDown(Id, damage.AttackerId, Team, ReviveRuntime.CountHealthyAllies(this)))
            {
                _downed = false;
                Health.ApplyDamage(damage.WithAmount(Health.Current + 1f));
                return;
            }

            try
            {
                BecameDowned?.Invoke(this);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            ReviveRuntime.KillTeamIfWiped(Team);
        }

        private void FinishOff(DamageInfo damage)
        {
            _downed = false;
            ReviveRuntime.Service.Remove(Id);
            Health.ApplyDamage(damage.WithAmount(Health.Current + 1f));
        }

        /// <summary>Kanamadan ya da takım yok olunca ölüm (servis çağırır).</summary>
        internal void BleedToDeath(PlayerId attacker)
        {
            if (!_downed || Health == null || !Health.IsAlive)
                return;

            _downed = false;
            ReviveRuntime.Service.Remove(Id);
            Health.ApplyDamage(new DamageInfo(Health.Current + 1f, attacker, "bleedout"));
        }

        /// <summary>Kaldırma tamamlandı: ayağa kalk, canın %30'u (servis çağırır).</summary>
        internal void CompleteRevive()
        {
            if (!_downed || Health == null || !Health.IsAlive)
                return;

            _downed = false;
            var target = Health.Max * ReviveService.ReviveHealthFraction;
            Health.HealCapped(target, target);
            try
            {
                Recovered?.Invoke(this);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        public void Heal(float amount)
        {
            if (Health == null || !Health.IsAlive || amount <= 0f || _downed)
                return;

            Health.Heal(amount);
        }

        /// <summary>Vücut bölgesinin dünya konumu: Hitbox → EyePoint/AimPoint → duruşa göre tahmin.</summary>
        public Vector3 GetAimPosition(BodyPart part)
        {
            EnsureHitboxes();
            Hitbox fallback = null;
            for (var i = 0; i < _hitboxes.Count; i++)
            {
                var hb = _hitboxes[i];
                if (hb == null)
                    continue;

                if (hb.Part == part)
                    return hb.WorldCenter;

                if (fallback == null && hb.Part == BodyPart.Torso)
                    fallback = hb;
            }

            var root = transform.position;
            switch (part)
            {
                case BodyPart.Head:
                    if (EyePoint != null)
                        return EyePoint.position + Vector3.up * 0.06f;
                    return root + Vector3.up * (EyeHeightForStance() + 0.06f);

                case BodyPart.Leg:
                    return root + Vector3.up * (Stance == Stance.Prone ? 0.15f : 0.5f);

                default:
                    if (AimPoint != null)
                        return AimPoint.position;
                    if (fallback != null)
                        return fallback.WorldCenter;
                    return root + Vector3.up * (EyeHeightForStance() * 0.72f);
            }
        }

        public bool IsAllyOf(Combatant other) => other != null && other.Team == Team;

        internal void RegisterHitbox(Hitbox hitbox)
        {
            if (hitbox != null && !_hitboxes.Contains(hitbox))
                _hitboxes.Add(hitbox);
        }

        internal void UnregisterHitbox(Hitbox hitbox)
        {
            _hitboxes.Remove(hitbox);
        }

        private void Update()
        {
            if (!_initialized || _deathHandled || Health == null || !Health.IsAlive)
                return;

            if (!GameContext.HasAuthority)
                return;

            var dt = Time.deltaTime;
            if (dt <= 0f || _downed)
                return;

            ItemUse?.Tick(dt);
            Boost?.Tick(dt, Health, Health.IsAlive);

            var bleed = Limbs.Tick(dt);
            if (bleed > 0f)
                ApplyDamage(new DamageInfo(bleed, PlayerId.Invalid, LimbDamageRules.BleedSourceId));
        }

        private void OnItemCompletedClearWounds(string itemId)
        {
            if (ItemCatalog.TryGet(itemId, out var def) && def.Category == ItemCategory.Medical)
                Limbs.Bandage();
        }

        private void RecordLimbHit(DamageInfo damage)
        {
            if (!GameContext.HasAuthority || LimbDamageRules.IsEnvironmentalSource(damage.SourceWeaponId))
                return;

            var vest = Inventory != null ? Inventory.GetArmorFor(BodyPart.Torso) : null;
            var helmet = Inventory != null ? Inventory.GetArmorFor(BodyPart.Head) : null;
            Limbs.OnHit(damage.BodyPart, damage.Amount, vest == null || vest.IsBroken, helmet != null, !Health.IsAlive);
        }

        private void OnHealthDamaged(DamageInfo damage)
        {
            RecordLimbHit(damage);
            LastDamageTime = Time.time;
            LastAttackerId = damage.AttackerId;
            LastDamageSource = damage.HasSourcePosition
                ? CombatContext.ToVector3(damage.SourcePosition)
                : transform.position;

            var handler = Damaged;
            if (handler == null)
                return;

            try
            {
                handler(this, damage);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private void OnHealthDied(DamageInfo damage)
        {
            if (_deathHandled)
                return;

            _deathHandled = true;
            var wasHealthyDeath = !_downed;
            if (_downed)
            {
                _downed = false;
                ReviveRuntime.Service.Remove(Id);
            }

            DeathTime = Time.time;
            LastDeathInfo = damage;
            IsTargetable = false;
            Velocity = Vector3.zero;

            try
            {
                if (ItemUse != null && ItemUse.IsUsing)
                    ItemUse.Cancel();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            UnregisterDamageable();

            if (DropLootOnDeath && GameContext.HasAuthority)
                DropInventory();

            var handler = Died;
            if (handler != null)
            {
                try
                {
                    handler(this, damage);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }

            // Yaralı olmayan son sağ müttefik öldüyse (ör. yaralı arkadaşı varken doğrudan ölüm) kalan yaralılar kimse kaldıramayacağı
            // için 45 sn beklemeden elenir; takım ve maç durumu tutarlı kalır.
            if (wasHealthyDeath && Team >= 0 && GameContext.HasAuthority)
            {
                try
                {
                    ReviveRuntime.KillTeamIfWiped(Team);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }
        }

        private void DropInventory()
        {
            if (Inventory == null)
                return;

            try
            {
                var items = new List<LootItemData>(16);
                Inventory.DropAll(items);
                for (var i = items.Count - 1; i >= 0; i--)
                {
                    if (!items[i].IsValid)
                        items.RemoveAt(i);
                }

                if (items.Count > 0)
                    LootSpawner.DropAround(transform.position + Vector3.up * 0.3f, items);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private float EyeHeightForStance()
        {
            switch (Stance)
            {
                case Stance.Crouching: return CrouchingEyeHeight;
                case Stance.Prone: return ProneEyeHeight;
                default: return StandingEyeHeight;
            }
        }

        private void EnsureHitboxes()
        {
            if (_hitboxScanDone)
                return;

            _hitboxScanDone = true;
            if (_hitboxes.Count > 0)
                return;

            // Bind çağrılmadan önce eklenmiş vuruş kutuları için tek seferlik tarama.
            var found = GetComponentsInChildren<Hitbox>(true);
            for (var i = 0; i < found.Length; i++)
            {
                if (found[i].Owner == null || found[i].Owner == this)
                    RegisterHitbox(found[i]);
            }
        }

        private bool ContainsInRegistry()
        {
            var all = CombatantRegistry.All;
            for (var i = 0; i < all.Count; i++)
            {
                if (ReferenceEquals(all[i], this))
                    return true;
            }

            return false;
        }

        private void RegisterDamageable()
        {
            if (_registry == null || _inDamageRegistry || !Id.IsValid)
                return;

            _registry.Register(this);
            _inDamageRegistry = true;
        }

        private void UnregisterDamageable()
        {
            if (_registry == null || !_inDamageRegistry)
                return;

            _inDamageRegistry = false;
            if (_registry.TryGet(Id, out var current) && !ReferenceEquals(current, this))
                return; // aynı kimlikle daha yeni bir kayıt var — dokunma

            _registry.Unregister(this);
        }

        private void Teardown()
        {
            if (Health != null)
            {
                Health.Damaged -= OnHealthDamaged;
                Health.Died -= OnHealthDied;
            }

            UnregisterDamageable();
            CombatantRegistry.Unregister(this);
            _initialized = false;
        }

        private void OnDestroy()
        {
            Teardown();
            Damaged = null;
            Died = null;
        }
    }
}
