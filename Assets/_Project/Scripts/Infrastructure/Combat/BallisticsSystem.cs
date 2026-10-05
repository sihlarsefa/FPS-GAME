using System;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Vfx;
using UnityEngine;

namespace Project.Infrastructure.Combat
{
    /// <summary>
    /// Balistik mermi simülasyonu (GameObject'siz, yapı dizisi). Her mermi kare başına yerçekimli bir doğru parçası
    /// ilerler; parça <see cref="GameLayers.BulletMask"/> üzerinde RaycastNonAlloc ile (tetikleyiciler dahil) taranır,
    /// isabetler mesafeye göre sıralanır:
    ///  • Hitbox → atıcının kendi kutuları ve (dost ateşi kapalıysa) müttefikler atlanır; diğerlerine
    ///    <see cref="CombatService.ApplyBulletHit"/> (katedilen mesafe ile) + kan efekti + yakınsa vuruş sesi.
    ///  • Hitbox olmayan tetikleyiciler (yağma, araç binme alanları) yok sayılır.
    ///  • Katı collider (dünya, araç gövdesi) mermiyi durdurur → GameVfx.Impact (araç = metal).
    /// Mermi izi (tracer) her kare parçası için çizilir; görsel namludan başlayıp gerçek yörüngeye yakınsar.
    /// Yerel oyuncunun 3 m yakınından geçen yabancı mermiler vızıltı sesi çıkarır. Ömür: 3 sn ya da menzil.
    /// Hasar yalnızca otoritede (GameContext.HasAuthority) uygulanır; istemcide mermiler yalnızca görseldir.
    /// </summary>
    public sealed class BallisticsSystem : MonoBehaviour
    {
        public const float MaxLifetimeSeconds = 3f;
        public const float WhizRadius = 3f;

        /// <summary>Merminin uçabileceği en uzun yol = silah menzili × bu çarpan.</summary>
        public const float RangeMultiplier = 1.15f;

        private const int InitialCapacity = 256;
        private const int HitBufferSize = 32;
        private const float TracerConvergeDistance = 18f;
        private const float TracerCullDistance = 450f;
        private const float HitAudioDistance = 40f;
        private const float ImpactAudioDistance = 45f;
        private const float MaxMuzzleOffset = 4f;
        private const float KillDepth = -500f;

        private struct Projectile
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public Vector3 Origin;
            public Vector3 VisualOffset;
            public float Traveled;
            public float MaxDistance;
            public float Age;
            public WeaponDefinitionData Weapon;
            public PlayerId ShooterId;
            public int ShooterTeam;
            public int LastStepFrame;
            public bool ShooterIsLocal;
            public bool Tracer;
            public bool Damaging;
            public bool Whizzed;
        }

        private readonly RaycastHit[] _hits = new RaycastHit[HitBufferSize];
        private Projectile[] _projectiles = new Projectile[InitialCapacity];
        private int _count;
        private CombatService _combat;
        private IEventBus _eventBus;

        // Kare başına önbellek
        private int _frameCacheFrame = -1;
        private bool _friendlyFire;
        private bool _hasListener;
        private Vector3 _listenerPosition;
        private Combatant _localPlayer;
        private Vector3 _localHead;
        private int _syncedFrame = -1;
        private bool _loggedError;

        public static BallisticsSystem Instance { get; private set; }

        public int ActiveProjectiles => _count;

        /// <summary>Yerçekimi çarpanı (1 = gerçekçi mermi düşüşü).</summary>
        public float GravityScale { get; set; } = 1f;

        /// <summary>Taramadan önce hareketli vuruş kutularının fizik dönüşümlerini eşitle (karede en fazla bir kez).</summary>
        public bool SyncTransformsBeforeTrace { get; set; } = true;

        public static BallisticsSystem Create(CombatService combat, IEventBus eventBus)
        {
            var system = Instance;
            if (system == null)
            {
                var go = new GameObject("BallisticsSystem");
                system = go.AddComponent<BallisticsSystem>();
            }

            system.Configure(combat, eventBus);
            return system;
        }

        /// <summary>Varsa mevcut örneği, yoksa GameContext servisleriyle yenisini döndürür.</summary>
        public static BallisticsSystem GetOrCreate()
        {
            if (Instance != null)
                return Instance;

            return Create(CombatContext.Combat, CombatContext.EventBus);
        }

        public void Configure(CombatService combat, IEventBus eventBus)
        {
            CombatContext.ClearOverrides(_combat, _eventBus);
            _combat = combat;
            _eventBus = eventBus;
            CombatContext.SetOverrides(combat, eventBus);
        }

        private CombatService Combat => _combat ?? CombatContext.Combat;
        private IEventBus Bus => _eventBus ?? CombatContext.EventBus;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Balistik] Sahnede ikinci BallisticsSystem bulundu; yenisi devre dışı.", this);
                enabled = false;
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            CombatContext.ClearOverrides(_combat, _eventBus);
        }

        // ------------------------------------------------------------------ atış

        /// <summary>
        /// Bir atışı (pompalıda saçma sayısı kadar mermi) üretir. Mermiyi TÜKETMEZ — çağıran önce
        /// <see cref="IWeaponRuntime.TryTrigger"/> ile atışı onaylatır. WeaponFiredEvent yayınlar, silah sesini çalar,
        /// yerel olmayan atıcılar için dünya namlu alevi gösterir.
        /// </summary>
        /// <param name="origin">Simülasyon başlangıcı (göz/kamera).</param>
        /// <param name="aimDirection">Nişan yönü (normalize edilir).</param>
        /// <param name="spreadDegrees">Sapma konisi yarı açısı (GetSpreadAngle).</param>
        /// <param name="visualMuzzle">Namlu ucunun dünya konumu (tracer/alev/ses); Vector3.zero = origin.</param>
        public void FireWeapon(Combatant shooter, IWeaponRuntime weapon, Vector3 origin, Vector3 aimDirection, float spreadDegrees,
            Vector3 visualMuzzle)
        {
            if (weapon == null)
                return;

            var definition = weapon.Definition;
            if (definition == null)
                return;

            var shooterId = shooter != null ? shooter.Id : PlayerId.Invalid;
            var isLocal = shooter != null && shooter.IsLocalPlayer;
            var team = shooter != null ? shooter.Team : CombatContext.ResolveTeam(shooterId);
            var aim = NormalizeAim(aimDirection, shooter);
            var muzzle = ResolveMuzzle(origin, visualMuzzle);

            var pellets = Mathf.Max(1, definition.PelletCount);
            for (var i = 0; i < pellets; i++)
            {
                var direction = ApplySpread(aim, spreadDegrees);
                if (pellets > 1 && definition.PelletSpread > 0f)
                    direction = ApplySpread(direction, definition.PelletSpread);

                var tracer = pellets == 1 || i < 2;
                Spawn(shooterId, team, isLocal, definition, origin, direction, muzzle, tracer);
            }

            try
            {
                Bus?.Publish(new WeaponFiredEvent(shooterId, definition.WeaponId, SafeAmmo(weapon), CombatContext.ToFloat3(origin)));
            }
            catch (Exception e)
            {
                LogOnce(e);
            }

            try
            {
                GameAudio.PlayGunshot(definition, muzzle, isLocal);
                if (!isLocal)
                    GameVfx.MuzzleFlash(muzzle, aim, MuzzleScale(definition));
            }
            catch (Exception e)
            {
                LogOnce(e);
            }
        }

        /// <summary>Tek bir mermi üretir (olay/ses yok). Atıcının timi ve yerel olup olmadığı kayıttan çözülür.</summary>
        public void SpawnProjectile(PlayerId shooterId, WeaponDefinitionData weapon, Vector3 origin, Vector3 direction,
            Vector3 visualMuzzle, bool tracer)
        {
            if (weapon == null)
                return;

            var isLocal = false;
            int team;
            if (CombatantRegistry.TryGet(shooterId, out var shooter))
            {
                isLocal = shooter.IsLocalPlayer;
                team = shooter.Team;
            }
            else
            {
                team = CombatContext.ResolveTeam(shooterId);
            }

            var dir = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.forward;
            Spawn(shooterId, team, isLocal, weapon, origin, dir, ResolveMuzzle(origin, visualMuzzle), tracer);
        }

        /// <summary>Yönü, yarı açısı <paramref name="spreadDegrees"/> olan koni içinde düzgün dağılımlı saptırır.</summary>
        public static Vector3 ApplySpread(Vector3 direction, float spreadDegrees)
        {
            var dir = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.forward;
            if (spreadDegrees <= 0.0001f)
                return dir;

            var angle = spreadDegrees * Mathf.Sqrt(UnityEngine.Random.value);
            var roll = UnityEngine.Random.value * 360f;
            var reference = Mathf.Abs(dir.y) < 0.99f ? Vector3.up : Vector3.right;
            var perpendicular = Vector3.Cross(dir, reference).normalized;
            perpendicular = Quaternion.AngleAxis(roll, dir) * perpendicular;
            return (Quaternion.AngleAxis(angle, perpendicular) * dir).normalized;
        }

        /// <summary>Tüm uçuştaki mermileri siler.</summary>
        public void ClearProjectiles()
        {
            for (var i = 0; i < _count; i++)
                _projectiles[i].Weapon = null;

            _count = 0;
        }

        private void Spawn(PlayerId shooterId, int team, bool isLocal, WeaponDefinitionData weapon, Vector3 origin,
            Vector3 direction, Vector3 muzzle, bool tracer)
        {
            if (_count >= _projectiles.Length)
                Array.Resize(ref _projectiles, _projectiles.Length * 2);

            var speed = weapon.MuzzleVelocity > 1f ? weapon.MuzzleVelocity : 700f;
            var range = weapon.Range > 1f ? weapon.Range : 300f;
            var index = _count++;
            _projectiles[index] = new Projectile
            {
                Position = origin,
                Velocity = direction * speed,
                Origin = origin,
                VisualOffset = muzzle - origin,
                Traveled = 0f,
                MaxDistance = range * RangeMultiplier,
                Age = 0f,
                Weapon = weapon,
                ShooterId = shooterId,
                ShooterTeam = team,
                LastStepFrame = -1,
                ShooterIsLocal = isLocal,
                Tracer = tracer,
                Damaging = GameContext.HasAuthority,
                Whizzed = false
            };

            // İlk parçayı hemen işle: yakın mesafede isabet ve iz aynı karede görünür.
            var dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            PrepareFrame();
            _projectiles[index].LastStepFrame = Time.frameCount;
            if (!SafeStep(ref _projectiles[index], dt))
                RemoveAt(index);
        }

        // ------------------------------------------------------------------ simülasyon

        private void Update()
        {
            if (_count == 0)
                return;

            var dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            PrepareFrame();
            var frame = Time.frameCount;
            var i = 0;
            while (i < _count)
            {
                if (_projectiles[i].LastStepFrame == frame)
                {
                    i++;
                    continue;
                }

                _projectiles[i].LastStepFrame = frame;
                if (SafeStep(ref _projectiles[i], dt))
                    i++;
                else
                    RemoveAt(i);
            }
        }

        private void PrepareFrame()
        {
            var frame = Time.frameCount;
            if (SyncTransformsBeforeTrace && _syncedFrame != frame)
            {
                _syncedFrame = frame;
                Physics.SyncTransforms();
            }

            if (_frameCacheFrame == frame)
                return;

            _frameCacheFrame = frame;
            _friendlyFire = CombatContext.FriendlyFire;

            _localPlayer = CombatantRegistry.LocalPlayer;
            if (_localPlayer != null && _localPlayer.IsAlive)
            {
                _localHead = _localPlayer.EyePosition;
                _listenerPosition = _localHead;
                _hasListener = true;
            }
            else
            {
                _localPlayer = null;
                var cam = Camera.main;
                _hasListener = cam != null;
                _listenerPosition = _hasListener ? cam.transform.position : Vector3.zero;
            }
        }

        private bool SafeStep(ref Projectile p, float dt)
        {
            try
            {
                return Step(ref p, dt);
            }
            catch (Exception e)
            {
                LogOnce(e);
                return false;
            }
        }

        /// <returns>Mermi uçmaya devam ediyorsa true.</returns>
        private bool Step(ref Projectile p, float dt)
        {
            p.Age += dt;
            var expire = p.Age >= MaxLifetimeSeconds;

            var newVelocity = p.Velocity + Physics.gravity * (GravityScale * dt);
            var start = p.Position;
            var delta = (p.Velocity + newVelocity) * (0.5f * dt);
            var segmentLength = delta.magnitude;
            var remaining = p.MaxDistance - p.Traveled;
            if (segmentLength >= remaining)
            {
                if (remaining <= 0f)
                    return false;

                delta *= remaining / segmentLength;
                segmentLength = remaining;
                expire = true;
            }

            if (segmentLength < 1e-4f)
            {
                p.Velocity = newVelocity;
                return !expire;
            }

            var direction = delta / segmentLength;
            var end = start + delta;
            var stopped = false;
            Combatant victim = null;

            var hitCount = Physics.RaycastNonAlloc(start, direction, _hits, segmentLength, GameLayers.BulletMask,
                QueryTriggerInteraction.Collide);
            if (hitCount > 1)
                SortHits(hitCount);

            for (var i = 0; i < hitCount; i++)
            {
                var hit = _hits[i];
                var collider = hit.collider;
                if (collider == null)
                    continue;

                if (collider.TryGetComponent<Hitbox>(out var hitbox))
                {
                    var owner = hitbox.Owner;
                    if (!IsValidTarget(ref p, owner))
                        continue;

                    var point = hit.distance > 0f ? hit.point : hitbox.WorldCenter;
                    var normal = hit.distance > 0f ? hit.normal : -direction;
                    OnHitCombatant(ref p, owner, hitbox.Part, point, normal, p.Traveled + hit.distance);
                    end = hit.distance > 0f ? hit.point : start;
                    victim = owner;
                    stopped = true;
                    break;
                }

                if (collider.isTrigger)
                    continue;

                // Hitbox'sız eski hedefler (IDamageable) — kutusu olan savaşanların gövde collider'ı geçirgen sayılır.
                var damageable = collider.gameObject.layer == GameLayers.Vehicle ? null : collider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    if (damageable is Combatant combatant)
                    {
                        if (combatant.Hitboxes.Count > 0 || !IsValidTarget(ref p, combatant))
                            continue;

                        OnHitCombatant(ref p, combatant, BodyPart.Torso, HitPoint(hit, start), HitNormal(hit, direction),
                            p.Traveled + hit.distance);
                        end = HitPoint(hit, start);
                        victim = combatant;
                        stopped = true;
                        break;
                    }

                    if (damageable.IsAlive && damageable.OwnerId != p.ShooterId)
                    {
                        OnHitLegacy(ref p, damageable, HitPoint(hit, start), HitNormal(hit, direction), p.Traveled + hit.distance);
                        end = HitPoint(hit, start);
                        stopped = true;
                        break;
                    }
                }

                OnHitWorld(collider, HitPoint(hit, start), HitNormal(hit, direction));
                end = HitPoint(hit, start);
                stopped = true;
                break;
            }

            var traveledBefore = p.Traveled;
            p.Traveled += (end - start).magnitude;

            if (p.Tracer)
                DrawTracer(ref p, start, end, traveledBefore, p.Traveled, dt);

            if (!p.Whizzed && !p.ShooterIsLocal && _localPlayer != null && victim != _localPlayer)
                CheckWhiz(ref p, start, end);

            p.Position = end;
            p.Velocity = newVelocity;

            if (stopped || expire || end.y < KillDepth)
                return false;

            return true;
        }

        private bool IsValidTarget(ref Projectile p, Combatant owner)
        {
            if (owner == null || !owner.IsAlive || !owner.IsTargetable)
                return false;

            if (p.ShooterId.IsValid && owner.Id == p.ShooterId)
                return false;

            if (!_friendlyFire && p.ShooterTeam >= 0 && owner.Team == p.ShooterTeam)
                return false;

            return true;
        }

        private void OnHitCombatant(ref Projectile p, Combatant owner, BodyPart part, Vector3 point, Vector3 normal, float distance)
        {
            var outcome = HitOutcome.None;
            var showHit = true;
            if (p.Damaging)
            {
                var combat = Combat;
                if (combat != null)
                {
                    outcome = combat.ApplyBulletHit(p.ShooterId, p.Weapon, owner.Id, part, distance,
                        CombatContext.ToFloat3(p.Origin));
                    showHit = outcome.Applied;
                }
            }

            if (!showHit)
                return;

            var helmetHit = outcome.ArmorAbsorbed && part == BodyPart.Head;
            if (helmetHit)
                GameVfx.Impact(point, normal, SurfaceKind.Metal);

            GameVfx.Blood(point, normal);

            var victimIsLocal = owner.IsLocalPlayer;
            if (!victimIsLocal && (!_hasListener || (point - _listenerPosition).sqrMagnitude > HitAudioDistance * HitAudioDistance))
                return;

            var sound = outcome.ArmorAbsorbed ? (helmetHit ? SoundId.HitHelmet : SoundId.HitArmor) : SoundId.HitFlesh;
            var pitch = UnityEngine.Random.Range(0.92f, 1.08f);
            if (victimIsLocal)
                GameAudio.Play2D(sound, 0.85f, pitch);
            else
                GameAudio.Play(sound, point, 0.75f, pitch, HitAudioDistance);
        }

        private void OnHitLegacy(ref Projectile p, IDamageable damageable, Vector3 point, Vector3 normal, float distance)
        {
            if (p.Damaging)
            {
                var combat = Combat;
                if (combat != null)
                    combat.ApplyBulletHit(p.ShooterId, p.Weapon, damageable.OwnerId, BodyPart.Torso, distance,
                        CombatContext.ToFloat3(p.Origin));
            }

            GameVfx.Blood(point, normal);
        }

        private void OnHitWorld(Collider collider, Vector3 point, Vector3 normal)
        {
            var isVehicle = collider.gameObject.layer == GameLayers.Vehicle;
            var surface = isVehicle ? SurfaceKind.Metal : GameVfx.Classify(collider, point);
            GameVfx.Impact(point, normal, surface);

            if (!_hasListener || (point - _listenerPosition).sqrMagnitude > ImpactAudioDistance * ImpactAudioDistance)
                return;

            var sound = surface == SurfaceKind.Metal ? SoundId.BulletImpactMetal : SoundId.BulletImpact;
            GameAudio.Play(sound, point, 0.55f, UnityEngine.Random.Range(0.88f, 1.12f), ImpactAudioDistance);
        }

        private void DrawTracer(ref Projectile p, Vector3 start, Vector3 end, float traveledBefore, float traveledAfter, float dt)
        {
            if (_hasListener)
            {
                var cull = TracerCullDistance * TracerCullDistance;
                if (SegmentDistanceSqr(_listenerPosition, start, end) > cull)
                    return;
            }

            var f0 = 1f - Mathf.Clamp01(traveledBefore / TracerConvergeDistance);
            var f1 = 1f - Mathf.Clamp01(traveledAfter / TracerConvergeDistance);
            var from = start + p.VisualOffset * f0;
            var to = end + p.VisualOffset * f1;
            if ((to - from).sqrMagnitude < 0.01f)
                return;

            var duration = Mathf.Clamp(dt * 2.2f, 0.03f, 0.07f);
            GameVfx.Tracer(from, to, duration, TracerWidth(p.Weapon));
        }

        private void CheckWhiz(ref Projectile p, Vector3 start, Vector3 end)
        {
            if (_localPlayer == null || (p.ShooterId.IsValid && p.ShooterId == _localPlayer.Id))
                return;

            var segment = end - start;
            var lengthSqr = segment.sqrMagnitude;
            var t = lengthSqr > 1e-6f ? Mathf.Clamp01(Vector3.Dot(_localHead - start, segment) / lengthSqr) : 0f;
            var closest = start + segment * t;
            var distance = Vector3.Distance(closest, _localHead);
            if (distance > WhizRadius)
                return;

            p.Whizzed = true;
            var volume = Mathf.Lerp(1f, 0.45f, distance / WhizRadius);
            GameAudio.Play(SoundId.BulletWhiz, closest, volume, UnityEngine.Random.Range(0.9f, 1.15f), 12f);
        }

        // ------------------------------------------------------------------ yardımcılar

        private void RemoveAt(int index)
        {
            var last = _count - 1;
            if (index < last)
                _projectiles[index] = _projectiles[last];

            _projectiles[last].Weapon = null;
            _count = last;
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

        private static Vector3 HitPoint(RaycastHit hit, Vector3 start) => hit.distance > 0f ? hit.point : start;

        private static Vector3 HitNormal(RaycastHit hit, Vector3 direction) =>
            hit.distance > 0f && hit.normal.sqrMagnitude > 0.5f ? hit.normal : -direction;

        private static float SegmentDistanceSqr(Vector3 point, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            var lengthSqr = ab.sqrMagnitude;
            if (lengthSqr < 1e-6f)
                return (point - a).sqrMagnitude;

            var t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSqr);
            return (point - (a + ab * t)).sqrMagnitude;
        }

        private static Vector3 NormalizeAim(Vector3 aim, Combatant shooter)
        {
            if (aim.sqrMagnitude > 1e-8f)
                return aim.normalized;

            return shooter != null ? shooter.transform.forward : Vector3.forward;
        }

        private static Vector3 ResolveMuzzle(Vector3 origin, Vector3 visualMuzzle)
        {
            if (visualMuzzle == Vector3.zero)
                return origin;

            return (visualMuzzle - origin).sqrMagnitude <= MaxMuzzleOffset * MaxMuzzleOffset ? visualMuzzle : origin;
        }

        private static int SafeAmmo(IWeaponRuntime weapon)
        {
            try
            {
                return weapon.CurrentAmmo;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static float MuzzleScale(WeaponDefinitionData weapon)
        {
            switch (weapon.Category)
            {
                case WeaponCategory.Pistol: return 0.65f;
                case WeaponCategory.Smg: return 0.8f;
                case WeaponCategory.Shotgun: return 1.25f;
                case WeaponCategory.Sniper: return 1.3f;
                case WeaponCategory.Lmg: return 1.2f;
                case WeaponCategory.Dmr: return 1.1f;
                default: return 1f;
            }
        }

        private static float TracerWidth(WeaponDefinitionData weapon)
        {
            if (weapon == null)
                return 0.022f;

            switch (weapon.Category)
            {
                case WeaponCategory.Pistol:
                case WeaponCategory.Smg:
                case WeaponCategory.Shotgun:
                    return 0.016f;
                case WeaponCategory.Lmg:
                    return 0.03f;
                case WeaponCategory.Sniper:
                case WeaponCategory.Dmr:
                    return 0.026f;
                default:
                    return 0.022f;
            }
        }

        private void LogOnce(Exception e)
        {
            if (_loggedError)
                return;

            _loggedError = true;
            Debug.LogException(e, this);
        }
    }
}
