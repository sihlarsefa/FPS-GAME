using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Infrastructure.AI;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.Vfx;
using UnityEngine;

namespace Project.Infrastructure.Combat
{
    // Yeni çeşitler sona eklenir (ağ kodları/indeksler kalıcı).
    public enum ThrowableKind { Frag, Smoke, Flash, Molotov, Decoy }

    /// <summary>
    /// Atılan el bombası / sis bombası: Projectile katmanında sekebilen rijit küre (havuzlanır).
    /// Parçalı el bombası: 4 sn tapa, 9 m yarıçap, en fazla 135 hasar (ExplosionSystem).
    /// Sis bombası: 1.6 sn sonra 25 sn süren, görüşü kesen sis bulutu (SmokeVolume + GameVfx.SmokeCloud).
    /// </summary>
    public sealed class ThrowableProjectile : MonoBehaviour
    {
        public const float FragFuseSeconds = 4f;
        public const float FragRadius = 9f;
        public const float FragMaxDamage = 135f;
        public const float SmokeFuseSeconds = 1.6f;
        public const float SmokeRadius = 7f;
        public const float SmokeDurationSeconds = 25f;

        private const float ColliderRadius = 0.06f;
        private const float BounceMinSpeed = 1.2f;
        private const float BounceSoundInterval = 0.1f;
        private const float KillDepth = -200f;
        private const float MaxLifetime = 60f;

        private static readonly Stack<ThrowableProjectile> Pool = new();
        private static readonly List<ThrowableProjectile> ActiveList = new();
        private static readonly List<Collider> ColliderBuffer = new(16);
        private static Transform _poolRoot;
        private static PhysicsMaterial _bounceMaterial;

        private readonly List<Collider> _ignoredColliders = new(4);
        private Rigidbody _body;
        private SphereCollider _collider;
        private GameObject _fragVisual;
        private GameObject _smokeVisual;
        private GameObject _flashVisual;
        private GameObject _molotovVisual;
        private GameObject _decoyVisual;
        private float _nextDecoyShot;
        private int _decoyShotIndex;
        private int _decoyBurstLength;
        private float _launchTime;
        private float _fuseRemaining;
        private float _age;
        private float _lastBounceTime;
        private float _smokeEndTime;
        private bool _detonated;
        private bool _active;

        /// <summary>Uçuştaki / yerdeki etkin bombalar (AI kaçış kararları için). Döngüde indeks kullanın.</summary>
        public static IReadOnlyList<ThrowableProjectile> Active => ActiveList;

        public ThrowableKind Kind { get; private set; }
        public PlayerId Thrower { get; private set; } = PlayerId.Invalid;

        /// <summary>Patlamaya / sis açılmasına kalan süre (sn).</summary>
        public float FuseRemaining => _fuseRemaining;

        public bool HasDetonated => _detonated;
        public Vector3 Position => transform.position;

        /// <summary>Tehlike yarıçapı (parçalı için patlama yarıçapı, sis için 0).</summary>
        public float DangerRadius => Kind == ThrowableKind.Frag ? FragRadius : 0f;

        public static ThrowableProjectile Throw(ThrowableKind kind, Vector3 position, Vector3 velocity, PlayerId thrower)
            => Throw(kind, position, velocity, thrower, ThrowableRules.DefaultFuse(kind));

        /// <summary>Tapa süresi verilebilen sürüm (ör. pişirilmiş bomba).</summary>
        public static ThrowableProjectile Throw(ThrowableKind kind, Vector3 position, Vector3 velocity, PlayerId thrower, float fuseSeconds)
        {
            var projectile = Acquire();
            projectile.Launch(kind, position, velocity, thrower, Mathf.Max(0.05f, fuseSeconds));
            return projectile;
        }

        /// <summary>Tüm etkin bombaları havuza geri koyar (sahne sıfırlama).</summary>
        public static void RecycleAll()
        {
            for (var i = ActiveList.Count - 1; i >= 0; i--)
            {
                if (ActiveList[i] != null)
                    ActiveList[i].Recycle();
            }

            ActiveList.Clear();
        }

        private static ThrowableProjectile Acquire()
        {
            while (Pool.Count > 0)
            {
                var pooled = Pool.Pop();
                if (pooled != null)
                    return pooled;
            }

            return Build();
        }

        private static ThrowableProjectile Build()
        {
            var go = new GameObject("Throwable");
            go.layer = GameLayers.Projectile;
            go.SetActive(false);

            var collider = go.AddComponent<SphereCollider>();
            collider.radius = ColliderRadius;
            collider.sharedMaterial = BounceMaterial();

            var body = go.AddComponent<Rigidbody>();
            body.mass = 0.45f;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.6f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var projectile = go.AddComponent<ThrowableProjectile>();
            projectile._body = body;
            projectile._collider = collider;
            projectile._fragVisual = BuildFragVisual(go.transform);
            projectile._smokeVisual = BuildSmokeVisual(go.transform);
            projectile._flashVisual = BuildFlashVisual(go.transform);
            projectile._molotovVisual = BuildMolotovVisual(go.transform);
            projectile._decoyVisual = BuildDecoyVisual(go.transform);
            return projectile;
        }

        private void Launch(ThrowableKind kind, Vector3 position, Vector3 velocity, PlayerId thrower, float fuseSeconds)
        {
            Kind = kind;
            Thrower = thrower;
            _fuseRemaining = fuseSeconds;
            _age = 0f;
            _lastBounceTime = -1f;
            _detonated = false;
            _smokeEndTime = 0f;
            _launchTime = Time.time;
            _decoyShotIndex = 0;
            _decoyBurstLength = 3;
            _active = true;

            var t = transform;
            t.SetParent(null, false);
            t.SetPositionAndRotation(position, velocity.sqrMagnitude > 0.01f ? Quaternion.LookRotation(velocity) : Quaternion.identity);
            gameObject.name = NameOf(kind);

            if (_fragVisual != null)
                _fragVisual.SetActive(kind == ThrowableKind.Frag);
            if (_smokeVisual != null)
                _smokeVisual.SetActive(kind == ThrowableKind.Smoke);
            if (_flashVisual != null)
                _flashVisual.SetActive(kind == ThrowableKind.Flash);
            if (_molotovVisual != null)
                _molotovVisual.SetActive(kind == ThrowableKind.Molotov);
            if (_decoyVisual != null)
                _decoyVisual.SetActive(kind == ThrowableKind.Decoy);

            IgnoreThrowerColliders(thrower);

            gameObject.SetActive(true);
            _body.isKinematic = false;
            _body.position = position;
            _body.rotation = t.rotation;
            _body.linearVelocity = velocity;
            _body.angularVelocity = new Vector3(UnityEngine.Random.Range(-8f, 8f), UnityEngine.Random.Range(-4f, 4f),
                UnityEngine.Random.Range(-8f, 8f));
            _body.WakeUp();

            if (!ActiveList.Contains(this))
                ActiveList.Add(this);
        }

        private void Update()
        {
            if (!_active)
                return;

            var dt = Time.deltaTime;
            _age += dt;

            if (transform.position.y < KillDepth || _age > MaxLifetime)
            {
                Recycle();
                return;
            }

            if (_detonated)
            {
                // Sis kutusu bulut bitene kadar yerde kalır; aldatma bombası bu sürede sahte atış yapar.
                if (Time.time >= _smokeEndTime)
                {
                    Recycle();
                    return;
                }

                if (Kind == ThrowableKind.Decoy)
                    TickDecoy();
                return;
            }

            _fuseRemaining -= dt;
            if (_fuseRemaining > 0f)
                return;

            switch (Kind)
            {
                case ThrowableKind.Frag: DetonateFrag(); break;
                case ThrowableKind.Flash: DetonateFlash(); break;
                case ThrowableKind.Molotov: ShatterMolotov(); break;
                case ThrowableKind.Decoy: ArmDecoy(); break;
                default: DeploySmoke(); break;
            }
        }

        private static string NameOf(ThrowableKind kind)
        {
            switch (kind)
            {
                case ThrowableKind.Smoke: return "SisBombasi";
                case ThrowableKind.Flash: return "FlasBombasi";
                case ThrowableKind.Molotov: return "Molotof";
                case ThrowableKind.Decoy: return "AldatmaBombasi";
                default: return "ElBombasi";
            }
        }

        private void DetonateFlash()
        {
            _detonated = true;
            var position = transform.position + Vector3.up * 0.1f;
            Recycle();
            FlashbangSystem.Detonate(position, Thrower);
        }

        private void ShatterMolotov()
        {
            _detonated = true;
            var position = transform.position;
            Recycle();
            try
            {
                GameAudio.Play(SoundId.GrenadeBounce, position, 1f, 0.6f, 60f);
                GameAudio.Play(SoundId.Explosion, position, 0.35f, 1.9f, 80f);
                FireZone.Spawn(position, Thrower);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void ArmDecoy()
        {
            _detonated = true;
            _smokeEndTime = Time.time + ThrowableRules.DecoyDurationSeconds;
            _decoyShotIndex = 0;
            _decoyBurstLength = ThrowableRules.DecoyBurstLength(UnityEngine.Random.value);
            _nextDecoyShot = Time.time + 0.2f;
        }

        private void TickDecoy()
        {
            if (Time.time < _nextDecoyShot)
                return;

            FireDecoyShot();
            _nextDecoyShot = Time.time + ThrowableRules.DecoyNextDelay(_decoyShotIndex, _decoyBurstLength, UnityEngine.Random.value);
            _decoyShotIndex++;
            if (_decoyShotIndex >= _decoyBurstLength)
            {
                _decoyShotIndex = 0;
                _decoyBurstLength = ThrowableRules.DecoyBurstLength(UnityEngine.Random.value);
            }
        }

        /// <summary>Sahte atış: ses + yankı (Acoustics) + namlu parlaması; botlar mevcut işitme tamponundan duyar.</summary>
        private void FireDecoyShot()
        {
            var pos = transform.position + Vector3.up * 0.3f;
            var dir = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * Vector3.forward;
            try
            {
                var id = UnityEngine.Random.value < 0.6f ? SoundId.ShotRifle556 : SoundId.ShotRifle762;
                GameAudio.Play(id, pos, 1f, UnityEngine.Random.Range(0.95f, 1.05f), 220f);
                Acoustics.OnShot(pos, dir, CaliberClass.Rifle, false);
                GameVfx.MuzzleFlash(pos, dir, 0.8f);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            try
            {
                var team = CombatantRegistry.TryGet(Thrower, out var owner) && owner != null ? owner.Team : -1;
                BotDirector.Instance?.RecordGunfire(pos, Thrower, team, ThrowableRules.DecoyLoudness);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void DetonateFrag()
        {
            _detonated = true;
            var position = transform.position + Vector3.up * 0.05f;
            Recycle();
            ExplosionSystem.Explode(position, FragRadius, FragMaxDamage, Thrower, DamageSourceIds.FragGrenade);
        }

        private void DeploySmoke()
        {
            _detonated = true;
            _smokeEndTime = Time.time + SmokeDurationSeconds;
            var center = transform.position + Vector3.up * 1.2f;
            SmokeVolume.Spawn(center, SmokeRadius, SmokeDurationSeconds);

            try
            {
                GameVfx.SmokeCloud(transform.position, SmokeRadius, SmokeDurationSeconds);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            try
            {
                GameAudio.Play(SoundId.SmokeHiss, transform.position, 0.8f, UnityEngine.Random.Range(0.95f, 1.05f), 45f);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_active)
                return;

            var speed = collision.relativeVelocity.magnitude;
            if (Kind == ThrowableKind.Molotov && !_detonated && Time.time - _launchTime > 0.08f
                && speed >= ThrowableRules.MolotovShatterMinSpeed)
            {
                ShatterMolotov();
                return;
            }

            if (speed < BounceMinSpeed || Time.time - _lastBounceTime < BounceSoundInterval)
                return;

            _lastBounceTime = Time.time;
            var point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            var volume = Mathf.Clamp01(speed / 10f) * 0.8f + 0.1f;
            try
            {
                GameAudio.Play(SoundId.GrenadeBounce, point, volume, UnityEngine.Random.Range(0.9f, 1.15f), 30f);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void Recycle()
        {
            if (!_active)
                return;

            _active = false;
            ActiveList.Remove(this);
            RestoreIgnoredCollisions();

            if (_body != null)
            {
                if (!_body.isKinematic)
                {
                    _body.linearVelocity = Vector3.zero;
                    _body.angularVelocity = Vector3.zero;
                }

                _body.isKinematic = true;
            }

            gameObject.SetActive(false);
            transform.SetParent(PoolRoot(), false);
            Pool.Push(this);
        }

        private void IgnoreThrowerColliders(PlayerId thrower)
        {
            RestoreIgnoredCollisions();
            if (_collider == null || !CombatantRegistry.TryGet(thrower, out var combatant))
                return;

            ColliderBuffer.Clear();
            combatant.GetComponentsInChildren(false, ColliderBuffer);
            for (var i = 0; i < ColliderBuffer.Count; i++)
            {
                var c = ColliderBuffer[i];
                if (c == null || c.isTrigger)
                    continue;

                Physics.IgnoreCollision(_collider, c, true);
                _ignoredColliders.Add(c);
            }

            ColliderBuffer.Clear();
        }

        private void RestoreIgnoredCollisions()
        {
            for (var i = 0; i < _ignoredColliders.Count; i++)
            {
                var c = _ignoredColliders[i];
                if (c != null && _collider != null)
                    Physics.IgnoreCollision(_collider, c, false);
            }

            _ignoredColliders.Clear();
        }

        private void OnDestroy()
        {
            ActiveList.Remove(this);
        }

        // ---------------------------------------------------------------- görseller (düşük poligon)

        private static GameObject BuildFragVisual(Transform parent)
        {
            var root = new GameObject("Frag");
            root.layer = GameLayers.Projectile;
            root.transform.SetParent(parent, false);

            var olive = new Color(0.24f, 0.29f, 0.17f);
            var metal = new Color(0.32f, 0.33f, 0.33f);
            AddPart(root.transform, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.095f, 0.115f, 0.095f), olive, 0.25f, 0f);
            AddPart(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.065f, 0f), new Vector3(0.035f, 0.012f, 0.035f), metal, 0.5f, 0.6f);
            AddPart(root.transform, PrimitiveType.Cube, new Vector3(0.022f, 0.045f, 0f), new Vector3(0.012f, 0.07f, 0.02f), metal, 0.5f, 0.6f);
            return root;
        }

        private static GameObject BuildFlashVisual(Transform parent)
        {
            var root = new GameObject("Flash");
            root.layer = GameLayers.Projectile;
            root.transform.SetParent(parent, false);

            AddPart(root.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.05f, 0.07f, 0.05f), new Color(0.62f, 0.64f, 0.66f), 0.5f, 0.7f);
            AddPart(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.045f, 0f), new Vector3(0.052f, 0.01f, 0.052f), new Color(0.9f, 0.9f, 0.92f), 0.3f, 0.2f);
            AddPart(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.085f, 0f), new Vector3(0.028f, 0.007f, 0.028f), new Color(0.3f, 0.3f, 0.3f), 0.5f, 0.6f);
            return root;
        }

        private static GameObject BuildMolotovVisual(Transform parent)
        {
            var root = new GameObject("Molotov");
            root.layer = GameLayers.Projectile;
            root.transform.SetParent(parent, false);

            AddPart(root.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.07f, 0.08f, 0.07f), new Color(0.2f, 0.32f, 0.2f), 0.85f, 0f);
            AddPart(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.11f, 0f), new Vector3(0.026f, 0.04f, 0.026f), new Color(0.2f, 0.32f, 0.2f), 0.85f, 0f);
            AddPart(root.transform, PrimitiveType.Sphere, new Vector3(0f, 0.17f, 0f), new Vector3(0.04f, 0.06f, 0.04f), new Color(0.72f, 0.55f, 0.3f), 0.1f, 0f);
            return root;
        }

        private static GameObject BuildDecoyVisual(Transform parent)
        {
            var root = new GameObject("Decoy");
            root.layer = GameLayers.Projectile;
            root.transform.SetParent(parent, false);

            AddPart(root.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.06f, 0.07f, 0.06f), new Color(0.2f, 0.22f, 0.24f), 0.3f, 0.3f);
            AddPart(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f), new Vector3(0.062f, 0.008f, 0.062f), new Color(0.85f, 0.7f, 0.15f), 0.2f, 0f);
            AddPart(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.078f, 0f), new Vector3(0.03f, 0.008f, 0.03f), new Color(0.3f, 0.3f, 0.3f), 0.5f, 0.6f);
            return root;
        }

        private static GameObject BuildSmokeVisual(Transform parent)
        {
            var root = new GameObject("Smoke");
            root.layer = GameLayers.Projectile;
            root.transform.SetParent(parent, false);

            var body = new Color(0.36f, 0.4f, 0.32f);
            var band = new Color(0.85f, 0.85f, 0.82f);
            AddPart(root.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.065f, 0.065f, 0.065f), body, 0.2f, 0f);
            AddPart(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.035f, 0f), new Vector3(0.067f, 0.008f, 0.067f), band, 0.2f, 0f);
            AddPart(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.072f, 0f), new Vector3(0.03f, 0.008f, 0.03f), new Color(0.3f, 0.3f, 0.3f), 0.5f, 0.6f);
            return root;
        }

        private static void AddPart(Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Color color,
            float smoothness, float metallic)
        {
            var go = GameObject.CreatePrimitive(type);
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                DestroyImmediate(collider);

            go.layer = GameLayers.Projectile;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material material = null;
                try
                {
                    material = MaterialLibrary.Lit(color, smoothness, metallic);
                }
                catch (Exception)
                {
                    // Malzeme kütüphanesi hazır değil — varsayılan malzeme kalır.
                }

                if (material != null)
                    renderer.sharedMaterial = material;

                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private static PhysicsMaterial BounceMaterial()
        {
            if (_bounceMaterial != null)
                return _bounceMaterial;

            _bounceMaterial = new PhysicsMaterial("GrenadeBounce")
            {
                bounciness = 0.32f,
                dynamicFriction = 0.55f,
                staticFriction = 0.7f,
                bounceCombine = PhysicsMaterialCombine.Average,
                frictionCombine = PhysicsMaterialCombine.Average
            };
            return _bounceMaterial;
        }

        private static Transform PoolRoot()
        {
            if (_poolRoot != null)
                return _poolRoot;

            var go = new GameObject("[ThrowablePool]");
            _poolRoot = go.transform;
            return _poolRoot;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Pool.Clear();
            ActiveList.Clear();
            _poolRoot = null;
            _bounceMaterial = null;
        }
    }
}
