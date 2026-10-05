using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.Vfx;
using UnityEngine;

namespace Project.Infrastructure.Combat
{
    public enum ThrowableKind { Frag, Smoke }

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
            => Throw(kind, position, velocity, thrower, kind == ThrowableKind.Frag ? FragFuseSeconds : SmokeFuseSeconds);

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
            _active = true;

            var t = transform;
            t.SetParent(null, false);
            t.SetPositionAndRotation(position, velocity.sqrMagnitude > 0.01f ? Quaternion.LookRotation(velocity) : Quaternion.identity);
            gameObject.name = kind == ThrowableKind.Frag ? "ElBombasi" : "SisBombasi";

            if (_fragVisual != null)
                _fragVisual.SetActive(kind == ThrowableKind.Frag);
            if (_smokeVisual != null)
                _smokeVisual.SetActive(kind == ThrowableKind.Smoke);

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
                // Sis kutusu bulut bitene kadar yerde kalır.
                if (Time.time >= _smokeEndTime)
                    Recycle();
                return;
            }

            _fuseRemaining -= dt;
            if (_fuseRemaining > 0f)
                return;

            if (Kind == ThrowableKind.Frag)
                DetonateFrag();
            else
                DeploySmoke();
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
