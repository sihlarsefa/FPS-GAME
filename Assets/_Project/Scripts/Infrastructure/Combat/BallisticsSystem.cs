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

        /// <summary>Yerel oyuncunun yanından geçen düşman mermisi (yakın ıska): mesafe metre cinsinden. Bastırma sistemi dinler.</summary>
        public static event System.Action<float> NearMiss;

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
        private const int MaxPenetrationsPerFrame = 24;
        private const float DefaultMuzzleSpeed = 700f;
        private const float MaxBarrierProbeMeters = 3f;
        private const float ExitImpactVolume = 0.4f;
        private const float TorsoHipAboveBottom = 0.11f;

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
            /// <summary>Delme/sekme sonrası kalan hasar oranı (1 = tam).</summary>
            public float DamageScale;
            public byte Penetrations;
            public byte Ricochets;
            /// <summary>Atıcının kendi aracı: bu kökün altındaki collider'lar (gövde, koltuktaki mürettebat) mermiyi durdurmaz.</summary>
            public Transform IgnoreRoot;
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
        private int _penFrame = -1;
        private int _penThisFrame;

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
            {
                Instance = null;
                AudioMatchHooks.End(); // AudioMix.Unbind + AmbienceBeds.Stop
            }

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
            if (isLocal)
            {
                // Dürbün sıfırlama açısı: namlu yukarı pitch (yalnız yerel oyuncu; dürbünsüz silahta 0).
                var zero = Project.Infrastructure.Weapons.Scope.ZeroingAngle(definition.WeaponId);
                if (zero > 0.0001f)
                {
                    var axis = Vector3.Cross(Vector3.up, aim);
                    if (axis.sqrMagnitude > 1e-6f)
                        aim = (Quaternion.AngleAxis(-zero, axis.normalized) * aim).normalized;
                }
            }

            var muzzle = ResolveMuzzle(origin, visualMuzzle);

            var pellets = Mathf.Max(1, definition.PelletCount);
            for (var i = 0; i < pellets; i++)
            {
                var direction = ApplySpread(aim, spreadDegrees);
                if (pellets > 1 && definition.PelletSpread > 0f)
                    direction = ApplySpread(direction, definition.PelletSpread);

                var suppressed = weapon is WeaponRuntimeService wrt && wrt.IsSuppressed;
                var tracer = !suppressed && (pellets == 1 || i < 2);
                // Eklentili namlu hızı (susturucu x0.94 vb.) yalnız çalışma zamanı servisinde bulunur.
                var velocityScale = weapon is WeaponRuntimeService wrv && definition.MuzzleVelocity > 1f
                    ? wrv.MuzzleVelocity / definition.MuzzleVelocity
                    : 1f;
                Spawn(shooterId, team, isLocal, definition, origin, direction, muzzle, tracer, null, velocityScale);
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
                var isSuppressed = weapon is WeaponRuntimeService wrs && wrs.IsSuppressed;
                GameAudio.PlayGunshot(definition, muzzle, isLocal, isSuppressed, aim);
                if (!isLocal)
                    GameVfx.MuzzleFlash(muzzle, aim, MuzzleScale(definition), definition.Category, isSuppressed);
                GameVfx.EjectShell(muzzle, aim, definition.Category);
            }
            catch (Exception e)
            {
                LogOnce(e);
            }
        }

        /// <summary>Tek bir mermi üretir (olay/ses yok). Atıcının timi ve yerel olup olmadığı kayıttan çözülür.</summary>
        public void SpawnProjectile(PlayerId shooterId, WeaponDefinitionData weapon, Vector3 origin, Vector3 direction,
            Vector3 visualMuzzle, bool tracer, Transform ignoreRoot = null)
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
            Spawn(shooterId, team, isLocal, weapon, origin, dir, ResolveMuzzle(origin, visualMuzzle), tracer, ignoreRoot);
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
            Vector3 direction, Vector3 muzzle, bool tracer, Transform ignoreRoot = null, float velocityScale = 1f)
        {
            if (_count >= _projectiles.Length)
                Array.Resize(ref _projectiles, _projectiles.Length * 2);

            var speed = (weapon.MuzzleVelocity > 1f ? weapon.MuzzleVelocity : 700f) * (velocityScale > 0.1f ? velocityScale : 1f);
            var range = weapon.Range > 1f ? weapon.Range : 300f;
            // Her atılan ışın (isabet/ıska fark etmez) için mermi geçiş sesi (crack/whiz).
            try { Acoustics.OnBulletPassed(origin, direction, range * RangeMultiplier, AcousticsBridge.CaliberFor(weapon)); }
            catch (Exception e) { LogOnce(e); }
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
                Whizzed = false,
                DamageScale = 1f,
                IgnoreRoot = ignoreRoot
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
            AudioMatchHooks.Tick(Bus, CombatContext.Config); // maç başı ses kurulumu (bir kez; sonra tek referans karşılaştırması)
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
            // Üstel sürükleme (BallisticsMath.DragPerMeter): v(x) = v0 * exp(-k x); uçuş süresi/düşüş tabloyla uyumlu olur.
            if (p.Weapon != null)
            {
                var dragStep = BallisticsMath.DragPerMeter(p.Weapon.AmmoType) * newVelocity.magnitude * dt;
                if (dragStep > 0f)
                    newVelocity *= Mathf.Exp(-dragStep);
            }

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

                // Taret/araç içinden atılan mermi kendi aracının gövdesine ve mürettebatına takılmaz.
                if (p.IgnoreRoot != null && collider.transform.IsChildOf(p.IgnoreRoot))
                    continue;

                if (collider.TryGetComponent<Hitbox>(out var hitbox))
                {
                    var owner = hitbox.Owner;
                    if (!IsValidTarget(ref p, owner))
                        continue;

                    var point = hit.distance > 0f ? hit.point : hitbox.WorldCenter;
                    var normal = hit.distance > 0f ? hit.normal : -direction;
                    OnHitCombatant(ref p, owner, hitbox.Part, point, normal, p.Traveled + hit.distance, hitbox, direction);
                    end = hit.distance > 0f ? hit.point : start;
                    victim = owner;
                    stopped = true;
                    break;
                }

                if (collider.isTrigger)
                    continue;

                // Yıkılabilir cam/ahşap: kırılırsa mermi içinden geçer (kırılmazsa normal isabet işlenir).
                if (collider.gameObject.layer == GameLayers.Default && p.Weapon != null
                    && Destructible.TryBulletHit(collider, HitPoint(hit, start), direction, p.Weapon.Damage * p.DamageScale))
                    continue;

                // Hitbox'sız eski hedefler (IDamageable) — kutusu olan savaşanların gövde collider'ı geçirgen sayılır.
                var damageable = collider.gameObject.layer == GameLayers.Vehicle ? null : collider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    // T-129 ATAK Desteği helikopter gövdesi (dost mermisi geçer).
                    if (damageable is Project.Infrastructure.Support.SupportHeliHull heliHull)
                    {
                        if (!heliHull.IsAlive)
                            continue;
                        if (p.Damaging
                            ? !heliHull.ApplyBullet(p.ShooterTeam, p.ShooterId, p.Weapon != null ? p.Weapon.Damage * p.DamageScale : 0f)
                            : p.ShooterTeam >= 0 && p.ShooterTeam == heliHull.Team)
                            continue;
                        end = HitPoint(hit, start);
                        GameVfx.Impact(end, HitNormal(hit, direction), SurfaceKind.Metal, collider);
                        stopped = true;
                        break;
                    }

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

                if (p.Damaging && collider.gameObject.layer == GameLayers.Vehicle)
                    DamageDrivableVehicle(ref p, collider);

                var worldPoint = HitPoint(hit, start);
                var worldNormal = HitNormal(hit, direction);
                if (collider.gameObject.layer != GameLayers.Vehicle && hit.distance > 0f
                    && TryPenetrateOrRicochet(ref p, collider, worldPoint, worldNormal, direction, ref newVelocity, out var bounced))
                {
                    if (bounced)
                    {
                        end = worldPoint;
                        break;
                    }

                    continue;
                }

                OnHitWorld(collider, worldPoint, worldNormal);
                end = worldPoint;
                stopped = true;
                break;
            }

            var traveledBefore = p.Traveled;
            p.Traveled += (end - start).magnitude;

            if (p.Tracer)
                DrawTracer(ref p, start, end, traveledBefore, p.Traveled, dt);

            if (!p.Whizzed && !p.ShooterIsLocal && _localPlayer != null && victim != _localPlayer)
                CheckWhiz(ref p, start, end);

            if (p.ShooterIsLocal)
                CheckBotNearMiss(ref p, start, end);

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

        private void OnHitCombatant(ref Projectile p, Combatant owner, BodyPart part, Vector3 point, Vector3 normal, float distance,
            Hitbox hitbox = null, Vector3 shotDirection = default)
        {
            var outcome = HitOutcome.None;
            var showHit = true;
            if (p.Damaging)
            {
                var combat = Combat;
                if (combat != null)
                {
                    outcome = combat.ApplyBulletHit(p.ShooterId, p.Weapon, owner.Id, part, distance,
                        CombatContext.ToFloat3(p.Origin), p.DamageScale, ArmorCoverageFor(owner, hitbox, part, point, shotDirection));
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

        /// <summary>
        /// Zırh kapsama çarpanı (1 = tam, 0 = zırhı atlar; ArmorZones): vuruş noktası gövdeye/kafaya göre yerel koordinata çevrilir.
        /// Gövde: kalça = alt gövde kutusunun tabanı + 0,11 m (oyuncu ve asker rig'lerinde ~kalça), x = gövde sağ ekseninde kutu
        /// merkezinden sapma; plaka yalnız orta gövdeyi kaplar (omuz/yan atlar). Kafa: baş merkezine göre yükseklik ve yüze geliş
        /// (kabuk tam, kenar kısmen, yüz hiç). Hitbox verisi yoksa, yatan/yaralı duruşta ya da kol/bacakta nötr (1).
        /// </summary>
        private static float ArmorCoverageFor(Combatant owner, Hitbox hitbox, BodyPart part, Vector3 point, Vector3 shotDirection)
        {
            if (owner == null || hitbox == null || (part != BodyPart.Torso && part != BodyPart.Head)
                || shotDirection.sqrMagnitude < 1e-6f || owner.Stance == Stance.Prone || owner.IsDowned)
                return 1f;

            var body = owner.transform;
            var forward = body.forward;
            var azimuth = ArmorZones.AzimuthDeg(shotDirection.x, shotDirection.z, forward.x, forward.z);
            if (part == BodyPart.Head)
            {
                var frontHit = shotDirection.x * forward.x + shotDirection.z * forward.z < 0f; // mermi yüze doğru geliyor
                return ArmorZones.HelmetFactor(point.y - hitbox.WorldCenter.y, azimuth, frontHit);
            }

            var lowest = float.MaxValue;
            var boxes = owner.Hitboxes;
            for (var i = 0; i < boxes.Count; i++)
            {
                var box = boxes[i];
                if (box == null || box.Part != BodyPart.Torso)
                    continue;

                var collider = box.Collider;
                if (collider == null || !collider.enabled)
                    continue;

                lowest = Mathf.Min(lowest, collider.bounds.min.y);
            }

            if (lowest == float.MaxValue)
                return 1f;

            var localX = Vector3.Dot(point - hitbox.WorldCenter, body.right);
            return ArmorZones.TorsoFactor(localX, point.y - (lowest + TorsoHipAboveBottom), azimuth);
        }

        private void OnHitLegacy(ref Projectile p, IDamageable damageable, Vector3 point, Vector3 normal, float distance)
        {
            if (p.Damaging)
            {
                var combat = Combat;
                if (combat != null)
                    combat.ApplyBulletHit(p.ShooterId, p.Weapon, damageable.OwnerId, BodyPart.Torso, distance,
                        CombatContext.ToFloat3(p.Origin), p.DamageScale);
            }

            GameVfx.Blood(point, normal);
        }

        /// <summary>Sürülebilir araç gövdesine isabet: küçük silah hasarı zırhla azaltılarak araç canına yazılır.</summary>
        private void DamageDrivableVehicle(ref Projectile p, Collider collider)
        {
            if (p.Weapon == null)
                return;

            var vehicle = collider.GetComponentInParent<Project.Infrastructure.Vehicles.DrivableVehicle>();
            if (vehicle == null)
            {
                var heli = collider.GetComponentInParent<Project.Infrastructure.Transport.FlyableHelicopter>();
                if (heli != null && heli.Health > 0f && !heli.IsOccupant(p.ShooterId))
                {
                    var pilot = heli.Pilot;
                    if (pilot == null || _friendlyFire || p.ShooterTeam < 0 || pilot.Team != p.ShooterTeam)
                        heli.ApplyDamage(Project.Infrastructure.Vehicles.KirpiCrewRules.BulletDamageToVehicle(p.Weapon.Damage * p.DamageScale), p.ShooterId);
                }

                return;
            }

            if (vehicle.Health <= 0f || vehicle.IsOccupant(p.ShooterId))
                return;

            var driver = vehicle.Driver;
            if (driver != null && !_friendlyFire && p.ShooterTeam >= 0 && driver.Team == p.ShooterTeam)
                return;

            vehicle.ApplyDamage(Project.Infrastructure.Vehicles.KirpiCrewRules.BulletDamageToVehicle(p.Weapon.Damage * p.DamageScale), p.ShooterId);
        }

        /// <summary>
        /// Dünya isabetinde sekme (metal/beton, sığ açı) veya delme (ahşap/sıva/ince sac/yaprak) dener.
        /// true: mermi durmadı (<paramref name="bounced"/> true ise sekti ve bu kare izi biter, false ise delip geçti).
        /// Kare başına delme sayısı ve mermi başına delme/sekme sayısı sınırlıdır.
        /// </summary>
        private bool TryPenetrateOrRicochet(ref Projectile p, Collider collider, Vector3 point, Vector3 normal, Vector3 direction,
            ref Vector3 velocity, out bool bounced)
        {
            bounced = false;
            if (p.Weapon == null || !PenetrationRules.StillLethal(p.DamageScale))
                return false;

            var surface = GameVfx.Classify(collider, point);

            // Sekme (SS2): sert yüzey + sığ açı (<25°) + atış tohumundan deterministik %20 (sunucu/istemci aynı).
            if (p.Ricochets == 0 && RicochetRules.IsHardSurface(surface))
            {
                var grazing = Penetration.GrazingAngle(direction, normal);
                var seed = RicochetRules.ShotSeed(p.Origin, point, p.Ricochets);
                if (RicochetRules.ShouldRicochet(surface, grazing, seed))
                {
                    var dir = RicochetRules.Deflect(direction, normal, seed);
                    velocity = dir * (velocity.magnitude * PenetrationRules.RicochetSpeedKeep);
                    p.DamageScale *= RicochetRules.DamageKeep(grazing);
                    p.Ricochets++;
                    p.Whizzed = false;
                    GameVfx.Impact(point, normal, surface, collider);
                    RicochetFx.Play(point, normal, dir, seed, _hasListener, _listenerPosition, ImpactAudioDistance * 2f);
                    bounced = true;
                    return true;
                }
            }

            var material = Penetration.ToMaterial(collider, surface);
            if (material == PenetrationMaterial.Solid)
                return false;

            var frame = Time.frameCount;
            if (_penFrame != frame)
            {
                _penFrame = frame;
                _penThisFrame = 0;
            }

            // Delinebilir engel: eski dört malzeme + tuğla (beton ve kum torbası durdurur). Bullet/kare sınırları aynen korunur.
            if (!(PenetrationRules.IsPenetrable(material) || material == PenetrationMaterial.Brick)
                || p.Penetrations >= PenetrationRules.MaxPenetrationsPerBullet || _penThisFrame >= MaxPenetrationsPerFrame)
                return false;

            // Joule modeli (BarrierModel, RC3): giriş enerjisi mevcut hızdan (E ∝ v²); engel enerji düşürür, kalan enerji
            // sonraki engele taşınır. Enerji yetmezse (EvaluateBarrier Penetrated=false) mermi durur: normal isabet işlenir.
            var speed = velocity.magnitude;
            var entryEnergy = EntryEnergyJoules(p.Weapon, speed);
            var joulesPerMeter = BarrierModel.Row(material).JoulesPerMeter;
            var max = Mathf.Min(MaxBarrierProbeMeters, entryEnergy / joulesPerMeter);
            if (max <= 0.002f
                || !Penetration.TryMeasure(collider, point, direction, max, out var thickness, out var exitPoint))
                return false;

            var result = Penetration.EvaluateBarrier(material, entryEnergy, speed, thickness, direction, exitPoint);
            if (!result.Penetrated)
                return false;

            _penThisFrame++;
            p.Penetrations++;
            p.DamageScale *= result.DamageScale;
            // Çıkış hızı enerji kaybından, yön engelin yayılım konisinden (Evaluate); iz bu karenin sonundan itibaren sapar.
            velocity = new Vector3(result.DirX, result.DirY, result.DirZ) * result.ExitSpeed;

            // Giriş izi burada; çıkış izi/kıvılcım/ses Penetration.Exited aboneliğinde (OnPenetrationExited).
            GameVfx.Impact(point, normal, surface, collider);
            return true;
        }

        /// <summary>
        /// Mermi enerjisi (Joule): namlu enerjisi × (hız / namlu hızı)². Saçmada toplam enerji pelet sayısına bölünür.
        /// Eklentili namlu hızı ve sürükleme hızı zaten <paramref name="speed"/>'e yansır.
        /// </summary>
        private static float EntryEnergyJoules(WeaponDefinitionData weapon, float speed)
        {
            var muzzle = weapon.MuzzleVelocity > 1f ? weapon.MuzzleVelocity : DefaultMuzzleSpeed;
            var ratio = Mathf.Clamp(speed / muzzle, 0f, 1.25f);
            return BarrierModel.MuzzleEnergy(weapon.AmmoType) / Mathf.Max(1, weapon.PelletCount) * ratio * ratio;
        }

        private void OnEnable()
        {
            Penetration.Exited += OnPenetrationExited;
        }

        private void OnDisable()
        {
            Penetration.Exited -= OnPenetrationExited;
        }

        /// <summary>
        /// Delme çıkışı (Penetration.Exited): çıkış izi/toz-kıymık (yüzeye göre), ince metalde kıvılcım saçılımı (RicochetFx, ıslıksız)
        /// ve dinleyiciye yakınsa kısık darbe sesi (ImpactAudio, yoksa GameAudio yedeği).
        /// </summary>
        private void OnPenetrationExited(Penetration.ExitInfo info)
        {
            if (Instance != this)
                return;

            try
            {
                var surface = Penetration.SurfaceFor(info.Material);
                var exitNormal = info.Direction.sqrMagnitude > 1e-6f ? info.Direction.normalized : Vector3.up;
                GameVfx.Impact(info.Point, exitNormal, surface);

                var seed = RicochetRules.ShotSeed(info.Point, info.Point + info.Direction, (int)(info.EnergyJoules * 0.1f));
                if (info.Material == PenetrationMaterial.ThinMetal)
                    RicochetFx.Play(info.Point, exitNormal, info.Direction, seed, false, Vector3.zero, 0f);

                if (!_hasListener || (info.Point - _listenerPosition).sqrMagnitude > ImpactAudioDistance * ImpactAudioDistance)
                    return;

                if (!ImpactAudio.PlayImpact(surface, info.Point, _listenerPosition, seed, ExitImpactVolume, ImpactAudioDistance))
                    GameAudio.PlayBulletImpact(surface, info.Point, ExitImpactVolume, ImpactAudioDistance);
            }
            catch (Exception e)
            {
                LogOnce(e);
            }
        }

        private void OnHitWorld(Collider collider, Vector3 point, Vector3 normal)
        {
            var isVehicle = collider.gameObject.layer == GameLayers.Vehicle;
            var surface = isVehicle ? SurfaceKind.Metal : GameVfx.Classify(collider, point);
            GameVfx.Impact(point, normal, surface);
            if (surface == SurfaceKind.Water)
            {
                try { Project.Infrastructure.Rendering.WaterSurfaceSplash.Ripple(point, 0.6f, false); }
                catch (Exception) { /* halka isteğe bağlı */ }
            }

            if (!_hasListener || (point - _listenerPosition).sqrMagnitude > ImpactAudioDistance * ImpactAudioDistance)
                return;

            if (!ImpactAudio.PlayImpact(surface, point, _listenerPosition, RicochetRules.ShotSeed(point, point, Time.frameCount), 0.55f, ImpactAudioDistance))
                GameAudio.PlayBulletImpact(surface, point, 0.55f, ImpactAudioDistance);
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

            // Yeni balistik iz: 'her 3. mermi + tüm MG' kuralını GameVfx kendi sayacıyla uygular (eski çağrı ile birlikte ÇİZİLMEZ).
            GameVfx.Tracer(from, (to - from).normalized, Mathf.Max(60f, p.Velocity.magnitude), CaliberOf(p.Weapon));
        }

        private static CaliberClass CaliberOf(WeaponDefinitionData weapon)
        {
            switch (weapon != null ? weapon.Category : WeaponCategory.AssaultRifle)
            {
                case WeaponCategory.Lmg: return CaliberClass.MachineGun;
                case WeaponCategory.Sniper:
                case WeaponCategory.Dmr: return CaliberClass.Sniper;
                case WeaponCategory.Pistol:
                case WeaponCategory.Smg: return CaliberClass.Pistol;
                default: return CaliberClass.Rifle;
            }
        }

        /// <summary>Yerel oyuncunun mermisi düşman botların yanından geçerse tam baskı (Combatant'lı yakın geçiş).</summary>
        private static void CheckBotNearMiss(ref Projectile p, Vector3 start, Vector3 end)
        {
            var bots = Project.Infrastructure.AI.BotController.All;
            if (bots.Count == 0)
                return;

            var segment = end - start;
            var lengthSqr = segment.sqrMagnitude;
            if (lengthSqr < 1e-6f)
                return;

            for (var i = 0; i < bots.Count; i++)
            {
                var bot = bots[i];
                if (bot == null || bot.Team == p.ShooterTeam)
                    continue;

                var target = bot.transform.position + Vector3.up * 1.1f;
                var t = Vector3.Dot(target - start, segment) / lengthSqr;
                if (t < 0f || t >= 1f)
                    continue; // en yakın nokta bu parçada değil: bir sonraki/önceki adımda sayılır (tek sefer)

                var distance = Vector3.Distance(start + segment * t, target);
                if (distance <= WhizRadius)
                    bot.ReceiveNearMiss(distance, p.Weapon);
            }
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
            NearMiss?.Invoke(distance);
            var volume = Mathf.Lerp(1f, 0.45f, distance / WhizRadius);
            GameAudio.Play(SoundId.BulletWhiz, closest, volume, UnityEngine.Random.Range(0.9f, 1.15f), 12f);
        }

        // ------------------------------------------------------------------ yardımcılar

        private void RemoveAt(int index)
        {
            // Hasar olayları (ör. maç sonu) uçuş sırasında ClearProjectiles çağırmış olabilir.
            if (index < 0 || index >= _count)
                return;

            var last = _count - 1;
            if (index < last)
                _projectiles[index] = _projectiles[last];

            _projectiles[last].Weapon = null;
            _projectiles[last].IgnoreRoot = null;
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
