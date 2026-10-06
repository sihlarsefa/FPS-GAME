using System;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    public readonly struct HitOutcome
    {
        public bool Applied { get; }
        public float Damage { get; }
        public bool Killed { get; }
        public bool Headshot { get; }
        public bool ArmorAbsorbed { get; }

        public HitOutcome(bool applied, float damage, bool killed, bool headshot, bool armorAbsorbed)
        {
            Applied = applied;
            Damage = damage;
            Killed = killed;
            Headshot = headshot;
            ArmorAbsorbed = armorAbsorbed;
        }

        public static HitOutcome None => new(false, 0f, false, false, false);
    }

    /// <summary>
    /// Otorite tarafı hasar çözümleyici: zırh, vücut bölgesi, mesafe düşüşü uygular; HitConfirmedEvent yayınlar.
    /// İstemciden gelen isabet bilgisine güvenmez — çağıran (balistik sistemi) sunucu tarafında çalışır.
    /// Dost ateşi kapalıyken aynı timdeki farklı oyuncular birbirine hasar veremez (kendine hasar, ör. el bombası, mümkündür).
    /// </summary>
    public sealed class CombatService
    {
        private readonly IEventBus _eventBus;
        private readonly IDamageableRegistry _registry;

        /// <param name="teams">Dost ateşi kontrolü için (null = herkes düşman).</param>
        public CombatService(IEventBus eventBus, IDamageableRegistry registry, ITeamRelations teams = null, bool friendlyFire = false)
        {
            _eventBus = eventBus;
            _registry = registry;
            Teams = teams;
            FriendlyFire = friendlyFire;
        }

        /// <summary>Tim ilişkileri (sonradan bağlanabilir; null = herkes düşman).</summary>
        public ITeamRelations Teams { get; set; }

        /// <summary>Dost ateşi açık mı?</summary>
        public bool FriendlyFire { get; set; }

        public IDamageableRegistry Registry => _registry;

        /// <summary>Saldırgan bu kurbana hasar verebilir mi? (dost ateşi kuralı)</summary>
        public bool CanDamage(PlayerId attackerId, PlayerId victimId) => !IsFriendlyFireBlocked(attackerId, victimId);

        /// <param name="armorCoverage">Zırh bölgesi çarpanı (ArmorZones; 1 = tam kapsama, 0 = zırhı atlar). Bkz. DamageCalculator.</param>
        public HitOutcome ApplyBulletHit(PlayerId attackerId, WeaponDefinitionData weapon, PlayerId victimId,
            BodyPart part, float distance, Float3 sourcePosition, float damageScale = 1f, float armorCoverage = 1f)
        {
            if (weapon == null || IsFriendlyFireBlocked(attackerId, victimId) || !TryGetLiveTarget(victimId, out var target))
                return HitOutcome.None;

            var armor = GetArmor(target, part);
            var result = DamageCalculator.ComputeBulletDamage(weapon, part, distance, armor, damageScale, armorCoverage);
            return ApplyAndReport(attackerId, victimId, target, result.Damage, weapon.WeaponId, part, sourcePosition, true,
                result.ArmorAbsorbed > 0f);
        }

        public HitOutcome ApplyMeleeHit(PlayerId attackerId, PlayerId victimId, BodyPart part, Float3 sourcePosition)
        {
            if (IsFriendlyFireBlocked(attackerId, victimId) || !TryGetLiveTarget(victimId, out var target))
                return HitOutcome.None;

            var damage = DamageCalculator.FistDamage;
            if (part == BodyPart.Head)
                damage *= DamageCalculator.FistHeadMultiplier;

            return ApplyAndReport(attackerId, victimId, target, damage, DamageSourceIds.Fists, part, sourcePosition, true, false);
        }

        /// <summary>Patlama hasarı: yelek etkinliği yarıya iner, kask etkisizdir.</summary>
        public HitOutcome ApplyExplosion(PlayerId attackerId, PlayerId victimId, string sourceId, float rawDamage,
            Float3 sourcePosition)
        {
            if (float.IsNaN(rawDamage) || rawDamage <= 0f)
                return HitOutcome.None;
            if (IsFriendlyFireBlocked(attackerId, victimId) || !TryGetLiveTarget(victimId, out var target))
                return HitOutcome.None;

            var vest = GetArmor(target, BodyPart.Torso);
            var result = DamageCalculator.ApplyArmor(rawDamage, vest, DamageCalculator.ExplosionVestEffectiveness);
            var source = string.IsNullOrEmpty(sourceId) ? DamageSourceIds.FragGrenade : sourceId;
            return ApplyAndReport(attackerId, victimId, target, result.Damage, source, BodyPart.Torso, sourcePosition, true,
                result.ArmorAbsorbed > 0f);
        }

        /// <summary>Bölge/düşme gibi çevresel hasar: zırhı yok sayar, saldırgan yoktur.</summary>
        public HitOutcome ApplyEnvironmentalDamage(PlayerId victimId, string sourceId, float amount)
        {
            if (float.IsNaN(amount) || amount <= 0f || !TryGetLiveTarget(victimId, out var target))
                return HitOutcome.None;

            return ApplyAndReport(PlayerId.Invalid, victimId, target, amount, sourceId, BodyPart.Torso, Float3.Zero, false, false);
        }

        /// <summary>Eski hitscan yolu (yakın dövüş/test). Mermiyi tüketir, tarar, hasarı uygular.</summary>
        public bool TryFire(PlayerId shooterId, IWeaponRuntime weapon, IHitScanner scanner, HitScanRequest request)
        {
            if (weapon == null || !weapon.TryFire(out var shot))
                return false;

            var hit = scanner != null ? scanner.Scan(request) : HitScanResult.Miss;
            _eventBus?.Publish(new WeaponFiredEvent(shooterId, weapon.WeaponId, weapon.CurrentAmmo, hit));

            if (!hit.HasHit || !hit.TargetId.IsValid || hit.TargetId == shooterId)
                return true;

            var origin = new Float3(request.OriginX, request.OriginY, request.OriginZ);
            var point = new Float3(hit.HitX, hit.HitY, hit.HitZ);
            var distance = Float3.Distance(origin, point);
            var part = hit.IsHeadshot ? BodyPart.Head : BodyPart.Torso;

            var definition = weapon.Definition;
            if (definition != null)
            {
                ApplyBulletHit(shooterId, definition, hit.TargetId, part, distance, origin);
                return true;
            }

            if (IsFriendlyFireBlocked(shooterId, hit.TargetId) || !TryGetLiveTarget(hit.TargetId, out var target))
                return true;

            var damage = shot.Amount * (part == BodyPart.Head ? DamageCalculator.BodyPartMultiplier(null, part) : 1f);
            ApplyAndReport(shooterId, hit.TargetId, target, damage, weapon.WeaponId, part, origin, true, false);
            return true;
        }

        private bool IsFriendlyFireBlocked(PlayerId attackerId, PlayerId victimId)
        {
            if (FriendlyFire)
                return false;

            var teams = Teams;
            if (teams == null || !attackerId.IsValid || !victimId.IsValid || attackerId == victimId)
                return false;

            return teams.AreAllies(attackerId, victimId);
        }

        private bool TryGetLiveTarget(PlayerId victimId, out IDamageable target)
        {
            target = null;
            if (_registry == null || !victimId.IsValid)
                return false;

            if (!_registry.TryGet(victimId, out target) || target == null)
                return false;

            return target.IsAlive;
        }

        private static ArmorPiece GetArmor(IDamageable target, BodyPart part)
        {
            if (!(target is IArmored armored))
                return null;

            var provider = armored.Armor;
            return provider?.GetArmorFor(part);
        }

        private HitOutcome ApplyAndReport(PlayerId attackerId, PlayerId victimId, IDamageable target, float damage,
            string sourceId, BodyPart part, Float3 sourcePosition, bool hasSourcePosition, bool armorAbsorbed)
        {
            if (float.IsNaN(damage) || damage < 0f)
                damage = 0f;

            var healthModel = target as IHealthReadModel;
            var before = healthModel != null ? healthModel.State.Current : 0f;

            if (damage > 0f)
                target.ApplyDamage(new DamageInfo(damage, attackerId, sourceId, part, sourcePosition, hasSourcePosition));

            var killed = !target.IsAlive;
            var dealt = damage;
            if (healthModel != null)
            {
                var after = healthModel.State.Current;
                dealt = before - after;
                if (dealt < 0f)
                    dealt = 0f;
            }

            var headshot = part == BodyPart.Head;

            if (attackerId.IsValid && attackerId != victimId)
                _eventBus?.Publish(new HitConfirmedEvent(attackerId, victimId, dealt, headshot, killed, armorAbsorbed));

            return new HitOutcome(true, dealt, killed, headshot, armorAbsorbed);
        }
    }
}
