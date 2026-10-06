using Project.Core.Domain;
using Project.Core.Interfaces;
using UnityEngine;

namespace Project.Infrastructure.Combat
{
    /// <summary>Dinleyiciye yakın geçen merminin bilgisi (yakın-geçiş olayı).</summary>
    public readonly struct BulletPassInfo
    {
        public readonly Vector3 Origin;
        public readonly Vector3 ClosestPoint;
        /// <summary>Merminin dinleyiciye en yakın uzaklığı (m).</summary>
        public readonly float MissDistance;
        /// <summary>Atış noktasından en yakın noktaya ışın boyunca mesafe (m).</summary>
        public readonly float Along;
        public readonly float MuzzleVelocity;
        public readonly AmmoType Ammo;

        public BulletPassInfo(Vector3 origin, Vector3 closest, float miss, float along, float muzzleVelocity, AmmoType ammo)
        {
            Origin = origin; ClosestPoint = closest; MissDistance = miss; Along = along;
            MuzzleVelocity = muzzleVelocity; Ammo = ammo;
        }
    }

    /// <summary>
    /// Anlık ışın taraması (hitscan) — eski/test yolu (CombatService.TryFire). Vuruş kutularına (Hitbox) göre çalışır:
    /// isabet edilen kutunun sahibinin kimliği ve bölgesi (Head = kafadan vuruş) döner. Hitbox olmayan tetikleyiciler
    /// yok sayılır, katı collider'lar ışını durdurur. Kutusu olmayan eski hedefler için üst nesnedeki IDamageable kullanılır.
    /// </summary>
    public sealed class UnityHitScanner : IHitScanner
    {
        /// <summary>Dinleyiciye bu mesafeden (m) yakın geçen mermi için olay üretilir.</summary>
        public const float NearPassRadius = 3f;

        /// <summary>Yakın geçen mermi (yerel dinleyici). Ses (BulletFlybyAudio) ve bastırma (SuppressionDriver) dinler.</summary>
        public static event System.Action<BulletPassInfo> NearPass;

        /// <summary>Olaydaki ses hızı hesabı için namlu hızı (m/sn) ve mühimmat; atıcı silahına göre ayarlanabilir.</summary>
        public float MuzzleVelocity { get; set; } = 900f;
        public AmmoType Ammo { get; set; } = AmmoType.Mm556;

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
            var result = ScanHits(count, origin, direction, request.MaxRange);
            return result;
        }

        private HitScanResult ScanHits(int count, Vector3 origin, Vector3 direction, float maxRange)
        {
            var travel = maxRange;
            var scanned = ScanCore(count, ref travel);
            ReportNearPass(origin, direction, travel);
            FlinchNearbySoldiers(origin, direction, travel);
            return scanned;
        }

        /// <summary>Işın dinleyiciye &lt;= 3 m yaklaştıysa (kendi atışı hariç) yakın-geçiş olayı üretir.</summary>
        private void ReportNearPass(Vector3 origin, Vector3 direction, float travel)
        {
            if (NearPass == null)
                return;
            var cam = Camera.main;
            if (cam == null)
                return;
            var listener = cam.transform.position;
            if ((origin - listener).sqrMagnitude < 4f)
                return; // dinleyicinin kendi mermisi
            var along = Mathf.Clamp(Vector3.Dot(listener - origin, direction), 0f, travel);
            var closest = origin + direction * along;
            var miss = Vector3.Distance(closest, listener);
            if (miss <= NearPassRadius)
                NearPass.Invoke(new BulletPassInfo(origin, closest, miss, along, MuzzleVelocity, Ammo));
        }

        private static readonly Collider[] NearColliders = new Collider[48];
        private static readonly System.Collections.Generic.Dictionary<Combatant, Project.Infrastructure.Characters.SoldierModel> SoldierCache =
            new System.Collections.Generic.Dictionary<Combatant, Project.Infrastructure.Characters.SoldierModel>();
        private static readonly System.Collections.Generic.HashSet<Combatant> Flinched = new System.Collections.Generic.HashSet<Combatant>();

        /// <summary>Mermi yolunun 1,5 m yakınındaki askerler ürker (SoldierModel.Flinch). Vurulan hedef ve atıcı hariç.</summary>
        private void FlinchNearbySoldiers(Vector3 origin, Vector3 direction, float travel)
        {
            if (travel < 1f)
                return;
            var end = origin + direction * travel;
            var n = Physics.OverlapCapsuleNonAlloc(origin, end, Project.Infrastructure.Characters.Animation.WearyReactionRules.FlinchRadius,
                NearColliders, _hitMask, QueryTriggerInteraction.Collide);
            Flinched.Clear();
            for (var i = 0; i < n; i++)
            {
                var c = NearColliders[i];
                if (c == null || !c.TryGetComponent<Hitbox>(out var box))
                    continue;
                var owner = box.Owner;
                if (owner == null || !owner.IsAlive || !Flinched.Add(owner))
                    continue;
                if (IgnoreOwner.IsValid && owner.Id == IgnoreOwner)
                    continue;
                if (!SoldierCache.TryGetValue(owner, out var soldier))
                {
                    soldier = owner.GetComponentInChildren<Project.Infrastructure.Characters.SoldierModel>();
                    SoldierCache[owner] = soldier;
                }

                if (soldier == null)
                    continue;
                var pos = soldier.transform.position;
                var along = Mathf.Clamp(Vector3.Dot(pos - origin, direction), 0f, travel);
                var miss = Vector3.Distance(origin + direction * along, pos + Vector3.up);
                if (miss > Project.Infrastructure.Characters.Animation.WearyReactionRules.FlinchRadius)
                    continue;
                soldier.Flinch(Project.Infrastructure.Characters.Animation.WearyReactionRules.SideOf(origin, direction, pos),
                    Project.Infrastructure.Characters.Animation.WearyReactionRules.Closeness(miss));
            }
        }

        private HitScanResult ScanCore(int count, ref float travel)
        {
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
                    travel = hit.distance;
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
                    travel = hit.distance;
                    return new HitScanResult(true, damageable.OwnerId, false, hit.point.x, hit.point.y, hit.point.z);
                }

                if (damageable != null)
                    continue; // vuruş kutulu savaşanın gövde collider'ı — kutular belirleyicidir

                travel = hit.distance;
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
