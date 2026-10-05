using Project.Core.Domain;
using Project.Core.Interfaces;
using UnityEngine;

namespace Project.Infrastructure.Combat
{
    /// <summary>
    /// Anlık ışın taraması (hitscan) — eski/test yolu (CombatService.TryFire). Vuruş kutularına (Hitbox) göre çalışır:
    /// isabet edilen kutunun sahibinin kimliği ve bölgesi (Head = kafadan vuruş) döner. Hitbox olmayan tetikleyiciler
    /// yok sayılır, katı collider'lar ışını durdurur. Kutusu olmayan eski hedefler için üst nesnedeki IDamageable kullanılır.
    /// </summary>
    public sealed class UnityHitScanner : IHitScanner
    {
        private readonly LayerMask _hitMask;
        private readonly RaycastHit[] _hits = new RaycastHit[32];

        public UnityHitScanner() : this(GameLayers.BulletMask)
        {
        }

        public UnityHitScanner(LayerMask hitMask)
        {
            _hitMask = hitMask;
        }

        /// <summary>Atıcının kimliği: kendi vuruş kutuları atlanır.</summary>
        public PlayerId IgnoreOwner { get; set; } = PlayerId.Invalid;

        /// <summary>Son isabetin vücut bölgesi.</summary>
        public BodyPart LastBodyPart { get; private set; }

        public HitScanResult Scan(HitScanRequest request)
        {
            var origin = new Vector3(request.OriginX, request.OriginY, request.OriginZ);
            var direction = new Vector3(request.DirectionX, request.DirectionY, request.DirectionZ);
            if (direction.sqrMagnitude < 1e-8f || request.MaxRange <= 0f)
                return HitScanResult.Miss;

            direction.Normalize();
            var count = Physics.RaycastNonAlloc(origin, direction, _hits, request.MaxRange, _hitMask, QueryTriggerInteraction.Collide);
            SortHits(count);

            for (var i = 0; i < count; i++)
            {
                var hit = _hits[i];
                var collider = hit.collider;
                if (collider == null)
                    continue;

                if (collider.TryGetComponent<Hitbox>(out var hitbox))
                {
                    var owner = hitbox.Owner;
                    if (owner == null || !owner.IsAlive || !owner.IsTargetable)
                        continue;

                    if (IgnoreOwner.IsValid && owner.Id == IgnoreOwner)
                        continue;

                    LastBodyPart = hitbox.Part;
                    var point = hit.distance > 0f ? hit.point : hitbox.WorldCenter;
                    return new HitScanResult(true, owner.Id, hitbox.Part == BodyPart.Head, point.x, point.y, point.z);
                }

                if (collider.isTrigger)
                    continue;

                var damageable = collider.GetComponentInParent<IDamageable>();
                if (damageable != null && !(damageable is Combatant { Hitboxes: { Count: > 0 } }))
                {
                    if (!damageable.IsAlive || (IgnoreOwner.IsValid && damageable.OwnerId == IgnoreOwner))
                        continue;

                    LastBodyPart = BodyPart.Torso;
                    return new HitScanResult(true, damageable.OwnerId, false, hit.point.x, hit.point.y, hit.point.z);
                }

                if (damageable != null)
                    continue; // vuruş kutulu savaşanın gövde collider'ı — kutular belirleyicidir

                return HitScanResult.Miss; // dünya ışını durdurdu
            }

            return HitScanResult.Miss;
        }

        private void SortHits(int count)
        {
            for (var i = 1; i < count; i++)
            {
                var key = _hits[i];
                var j = i - 1;
                while (j >= 0 && _hits[j].distance > key.distance)
                {
                    _hits[j + 1] = _hits[j];
                    j--;
                }

                _hits[j + 1] = key;
            }
        }
    }
}
