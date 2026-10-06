using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.Characters
{
    /// <summary>
    /// Askerin kemik hiyerarşisinden (prosedürel ya da humanoid) çalışma anında ragdoll kurar: kemik başına Rigidbody +
    /// ayrı "RagdollCol" çocuk nesnesinde (özel ragdoll katmanı) collider, eklemler CharacterJoint/HingeJoint.
    /// Yerleşince ya da 6 s sonra donar (eklem/collider/rigidbody kaldırılır, poz kalır). Kademeye göre aynı anda aktif
    /// ragdoll sayısı sınırlıdır; sınır aşılırsa en eskisi hemen dondurulur.
    /// </summary>
    public sealed class RagdollRig : MonoBehaviour
    {
        /// <summary>Ragdoll katmanı: BulletMask/LineOfSight/GroundMask dışında, oyuncu/bot/kutu/mermi ile çarpışmaz.</summary>
        public const int RagdollLayer = 15;

        public sealed class Bones
        {
            public readonly Transform[] Bone = new Transform[RagdollMath.PartCount];
            public readonly Transform[] End = new Transform[RagdollMath.PartCount];
            public Transform Frame;
        }

        private static readonly List<RagdollRig> ActiveRigs = new List<RagdollRig>(24);

        private readonly Rigidbody[] _bodies = new Rigidbody[RagdollMath.PartCount];
        private readonly List<Joint> _joints = new List<Joint>(16);
        private readonly List<GameObject> _colliderObjects = new List<GameObject>(16);
        private readonly List<Rigidbody> _trash = new List<Rigidbody>(16);
        private readonly List<Transform> _snapBones = new List<Transform>(16);
        private readonly List<Vector3> _snapPos = new List<Vector3>(16);
        private readonly List<Quaternion> _snapRot = new List<Quaternion>(16);
        private readonly RagdollSettle _settle = new RagdollSettle();

        private Rigidbody _weaponBody;
        private GameObject _weaponCol;
        private Transform _weaponRoot;
        private Transform _weaponParent;
        private Vector3 _weaponLocalPos;
        private Quaternion _weaponLocalRot;
        private Vector3 _weaponLocalScale;
        private float _sampleTimer;
        private int _trashFrame;

        private static PhysicsMaterial _bodyMaterial;

        // Düşen kask (kafadan vuruşta): parçalar geçici bir kök altında toplanır, Restore ile eski yerine döner.
        private GameObject _helmetHolder;
        private Rigidbody _helmetBody;
        private Transform[] _helmetParts;
        private Transform[] _helmetParents;
        private Vector3[] _helmetLocalPos;
        private Quaternion[] _helmetLocalRot;
        private Vector3[] _helmetLocalScale;

        private static PhysicsMaterial BodyMaterial()
        {
            if (_bodyMaterial != null)
                return _bodyMaterial;
            _bodyMaterial = new PhysicsMaterial("RagdollBody")
            {
                dynamicFriction = RagdollMath.BodyDynamicFriction,
                staticFriction = RagdollMath.BodyStaticFriction,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Maximum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            return _bodyMaterial;
        }

        public bool Active { get; private set; }

        public static int ActiveCount => ActiveRigs.Count;

        public Rigidbody Body(RagdollPart part) => _bodies[(int)part];

        public static void ConfigureCollisionLayers()
        {
            var l = RagdollLayer;
            Physics.IgnoreLayerCollision(l, l, true);
            Physics.IgnoreLayerCollision(l, GameLayers.Player, true);
            Physics.IgnoreLayerCollision(l, GameLayers.Bot, true);
            Physics.IgnoreLayerCollision(l, GameLayers.Hitbox, true);
            Physics.IgnoreLayerCollision(l, GameLayers.Loot, true);
            Physics.IgnoreLayerCollision(l, GameLayers.Projectile, true);
            Physics.IgnoreLayerCollision(l, GameLayers.Viewmodel, true);
        }

        /// <summary>Gövdeleri kurar. Başarısızsa (eksik kemik) false döner ve hiçbir şey eklenmez.</summary>
        public bool Build(Bones b, int tier)
        {
            if (Active || b == null || b.Frame == null)
                return false;
            var hips = b.Bone[(int)RagdollPart.Hips];
            var head = b.Bone[(int)RagdollPart.Head];
            if (hips == null || head == null)
                return false;

            ConfigureCollisionLayers();
            tier = RagdollMath.ClampTier(tier);
            var frame = b.Frame;
            var s = Mathf.Clamp(Vector3.Distance(hips.position, head.position) / 0.62f, 0.5f, 2f);

            _snapBones.Clear();
            _snapPos.Clear();
            _snapRot.Clear();
            _joints.Clear();
            _colliderObjects.Clear();
            for (var i = 0; i < _bodies.Length; i++)
                _bodies[i] = null;

            // Önce collider + rigidbody (pozlar bu aşamada değişmez).
            for (var i = 0; i < RagdollMath.PartCount; i++)
            {
                var bone = b.Bone[i];
                if (bone == null || !RagdollMath.PartEnabled((RagdollPart)i, tier))
                    continue;

                _snapBones.Add(bone);
                _snapPos.Add(bone.localPosition);
                _snapRot.Add(bone.localRotation);

                AddCollider(b, (RagdollPart)i, s);

                Rigidbody rb;
                if (!bone.TryGetComponent(out rb))
                    rb = bone.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.detectCollisions = true;
                rb.useGravity = true;
                rb.mass = Mathf.Max(0.5f, RagdollMath.MassOf((RagdollPart)i));
                rb.linearDamping = RagdollMath.BodyLinearDamping;
                rb.angularDamping = RagdollMath.AngularDamping(tier);
                rb.maxAngularVelocity = 20f;
                rb.maxDepenetrationVelocity = 2f;
                rb.sleepThreshold = 0.2f;
                rb.solverIterations = RagdollMath.SolverIterations(tier);
                rb.solverVelocityIterations = 2;
                rb.interpolation = RigidbodyInterpolation.None;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                _bodies[i] = rb;
            }

            // Sonra eklemler: üst zincirde olmayan (kapalı) parçalar atlanır.
            for (var i = 1; i < RagdollMath.PartCount; i++)
            {
                if (_bodies[i] == null)
                    continue;
                var parent = RagdollMath.Specs[i].Parent;
                while (parent >= 0 && _bodies[parent] == null)
                    parent = RagdollMath.Specs[parent].Parent;
                if (parent < 0)
                    continue;
                AddJoint(b, (RagdollPart)i, _bodies[parent], parent);
            }

            _settle.Reset();
            _sampleTimer = 0f;
            Active = true;
            Register();
            return true;
        }

        /// <summary>İsabet itkisini uygular. inheritVelocity: ölürken sahibin hızı (dünya).</summary>
        public void Launch(RagdollKick kick, RagdollPart hitPart, Vector3 inheritVelocity)
        {
            for (var i = 0; i < _bodies.Length; i++)
            {
                var rb = _bodies[i];
                if (rb == null)
                    continue;
                rb.linearVelocity = inheritVelocity + kick.SharedDeltaV;
            }

            var hit = _bodies[(int)hitPart] ?? _bodies[(int)RagdollPart.Chest] ?? _bodies[(int)RagdollPart.Hips];
            if (hit != null)
            {
                hit.linearVelocity += kick.HitPartDeltaV;
                hit.angularVelocity += kick.Spin;
            }

            if (_weaponBody != null)
            {
                _weaponBody.linearVelocity = RagdollMath.WeaponVelocity(inheritVelocity, kick.SharedDeltaV);
                // Küçük, deterministik dönüş: isabet dönüşünün bir kısmı (üst sınırlı).
                var spin = kick.Spin * 0.35f;
                if (spin.sqrMagnitude > RagdollMath.WeaponSpinMax * RagdollMath.WeaponSpinMax)
                    spin = spin.normalized * RagdollMath.WeaponSpinMax;
                _weaponBody.angularVelocity = spin;
            }
        }

        /// <summary>
        /// Kafadan vuruşta kaskı (parçalarıyla) fırlatır. parts: kaskın etkin nesneleri; hitDir: mermi yönü (dünya).
        /// Kask yoksa/parça yoksa hiçbir şey yapmaz.
        /// </summary>
        public void KnockOffHelmet(IList<Transform> parts, Transform head, Vector3 hitDir, Vector3 inheritVelocity)
        {
            if (!Active || parts == null || parts.Count == 0 || head == null || _helmetHolder != null)
                return;
            var n = 0;
            for (var i = 0; i < parts.Count; i++)
                if (parts[i] != null)
                    n++;
            if (n == 0)
                return;

            _helmetParts = new Transform[n];
            _helmetParents = new Transform[n];
            _helmetLocalPos = new Vector3[n];
            _helmetLocalRot = new Quaternion[n];
            _helmetLocalScale = new Vector3[n];
            var k = 0;
            for (var i = 0; i < parts.Count; i++)
            {
                var t = parts[i];
                if (t == null)
                    continue;
                _helmetParts[k] = t;
                _helmetParents[k] = t.parent;
                _helmetLocalPos[k] = t.localPosition;
                _helmetLocalRot[k] = t.localRotation;
                _helmetLocalScale[k] = t.localScale;
                k++;
            }

            var up = head.position + Vector3.up * 0.1f;
            _helmetHolder = new GameObject("RagdollHelmet");
            _helmetHolder.transform.SetPositionAndRotation(up, head.rotation);
            for (var i = 0; i < n; i++)
                _helmetParts[i].SetParent(_helmetHolder.transform, true);

            var colGo = new GameObject("RagdollCol");
            colGo.layer = RagdollLayer;
            colGo.transform.SetParent(_helmetHolder.transform, false);
            var sph = colGo.AddComponent<SphereCollider>();
            sph.radius = 0.11f;
            sph.sharedMaterial = BodyMaterial();

            var rb = _helmetHolder.AddComponent<Rigidbody>();
            rb.mass = RagdollMath.HelmetMass;
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.4f;
            rb.maxDepenetrationVelocity = 2f;
            rb.linearVelocity = RagdollMath.HelmetVelocity(hitDir, inheritVelocity);
            var axis = Vector3.Cross(Vector3.up, hitDir.sqrMagnitude < 1e-6f ? Vector3.back : hitDir.normalized);
            if (axis.sqrMagnitude < 1e-6f)
                axis = Vector3.right;
            rb.angularVelocity = axis.normalized * RagdollMath.HelmetSpinMax;
            _helmetBody = rb;
        }

        /// <summary>Elindeki silahı fizikli bir nesne olarak bırakır (Restore ile eski yerine döner).</summary>
        public void DropWeapon(Transform weaponRoot, Transform frame)
        {
            if (weaponRoot == null || !Active || !weaponRoot.gameObject.activeInHierarchy)
                return;

            var renderers = weaponRoot.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0)
                return;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            _weaponRoot = weaponRoot;
            _weaponParent = weaponRoot.parent;
            _weaponLocalPos = weaponRoot.localPosition;
            _weaponLocalRot = weaponRoot.localRotation;
            _weaponLocalScale = weaponRoot.localScale;
            weaponRoot.SetParent(frame, true);

            var col = new GameObject("RagdollCol");
            col.layer = RagdollLayer;
            col.transform.SetParent(weaponRoot, false);
            var box = col.AddComponent<BoxCollider>();
            box.center = weaponRoot.InverseTransformPoint(bounds.center);
            var sz = bounds.size;
            var inv = weaponRoot.InverseTransformVector(new Vector3(sz.x, 0f, 0f));
            var inv2 = weaponRoot.InverseTransformVector(new Vector3(0f, sz.y, 0f));
            var inv3 = weaponRoot.InverseTransformVector(new Vector3(0f, 0f, sz.z));
            var local = new Vector3(Mathf.Abs(inv.x) + Mathf.Abs(inv2.x) + Mathf.Abs(inv3.x),
                Mathf.Abs(inv.y) + Mathf.Abs(inv2.y) + Mathf.Abs(inv3.y),
                Mathf.Abs(inv.z) + Mathf.Abs(inv2.z) + Mathf.Abs(inv3.z));
            box.size = new Vector3(Mathf.Max(0.04f, local.x * 0.8f), Mathf.Max(0.04f, local.y * 0.8f), Mathf.Max(0.08f, local.z * 0.9f));
            box.sharedMaterial = BodyMaterial();
            _weaponCol = col;

            Rigidbody rb;
            if (!weaponRoot.TryGetComponent(out rb))
                rb = weaponRoot.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.mass = RagdollMath.WeaponMass;
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.5f;
            rb.maxDepenetrationVelocity = 2f;
            _weaponBody = rb;
        }

        /// <summary>Donar: eklemler/colliderlar kalkar, gövdeler kinematik olur (sonraki karede silinir). Poz kalır.</summary>
        public void Freeze()
        {
            if (!Active)
                return;
            Active = false;
            Unregister();

            for (var i = 0; i < _joints.Count; i++)
                if (_joints[i] != null)
                    Destroy(_joints[i]);
            _joints.Clear();

            for (var i = 0; i < _colliderObjects.Count; i++)
                if (_colliderObjects[i] != null)
                    Destroy(_colliderObjects[i]);
            _colliderObjects.Clear();

            for (var i = 0; i < _bodies.Length; i++)
            {
                var rb = _bodies[i];
                if (rb == null)
                    continue;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.detectCollisions = false;
                _trash.Add(rb);
                _bodies[i] = null;
            }

            if (_weaponBody != null)
            {
                _weaponBody.isKinematic = true;
                _weaponBody.detectCollisions = false;
                _trash.Add(_weaponBody);
                _weaponBody = null;
            }

            if (_weaponCol != null)
                Destroy(_weaponCol);
            _weaponCol = null;

            if (_helmetBody != null)
            {
                _helmetBody.isKinematic = true;
                _helmetBody.detectCollisions = false;
                _trash.Add(_helmetBody);
                _helmetBody = null;
            }

            _trashFrame = Time.frameCount;
        }

        /// <summary>Yeniden doğma: ragdoll kaldırılır, kemik yerel pozları ve silah bağı eski haline gelir.</summary>
        public void Restore()
        {
            Freeze();
            for (var i = 0; i < _snapBones.Count; i++)
            {
                if (_snapBones[i] == null)
                    continue;
                _snapBones[i].localPosition = _snapPos[i];
                _snapBones[i].localRotation = _snapRot[i];
            }

            if (_weaponRoot != null)
            {
                _weaponRoot.SetParent(_weaponParent, false);
                _weaponRoot.localPosition = _weaponLocalPos;
                _weaponRoot.localRotation = _weaponLocalRot;
                _weaponRoot.localScale = _weaponLocalScale;
            }

            _weaponRoot = null;
            _weaponParent = null;

            if (_helmetParts != null)
            {
                for (var i = 0; i < _helmetParts.Length; i++)
                {
                    var t = _helmetParts[i];
                    if (t == null)
                        continue;
                    t.SetParent(_helmetParents[i], false);
                    t.localPosition = _helmetLocalPos[i];
                    t.localRotation = _helmetLocalRot[i];
                    t.localScale = _helmetLocalScale[i];
                }
            }

            if (_helmetHolder != null)
                Destroy(_helmetHolder);
            _helmetHolder = null;
            _helmetParts = null;
        }

        private void Update()
        {
            if (_trash.Count > 0 && Time.frameCount > _trashFrame)
            {
                for (var i = 0; i < _trash.Count; i++)
                    if (_trash[i] != null && _trash[i].isKinematic)
                        Destroy(_trash[i]);
                _trash.Clear();
            }

            if (!Active)
                return;

            _sampleTimer += Time.deltaTime;
            if (_sampleTimer < 0.1f)
                return;
            var dt = _sampleTimer;
            _sampleTimer = 0f;

            var lin = 0f;
            var ang = 0f;
            for (var i = 0; i < _bodies.Length; i++)
            {
                var rb = _bodies[i];
                if (rb == null)
                    continue;
                lin = Mathf.Max(lin, rb.linearVelocity.magnitude);
                ang = Mathf.Max(ang, rb.angularVelocity.magnitude);
            }

            if (_settle.Tick(dt, lin, ang))
                Freeze();
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void OnDestroy()
        {
            Unregister();
        }

        private void Register()
        {
            if (!ActiveRigs.Contains(this))
                ActiveRigs.Add(this);
            var max = RagdollMath.MaxActive(Rendering.QualityTierApplier.LastTier < 0 ? 2 : Rendering.QualityTierApplier.LastTier);
            while (ActiveRigs.Count > max && ActiveRigs.Count > 1)
            {
                var oldest = ActiveRigs[0];
                if (oldest == this)
                    break;
                if (oldest == null)
                {
                    ActiveRigs.RemoveAt(0);
                    continue;
                }

                oldest.Freeze();
                // Freeze kendini listeden çıkarır; yine de takılmayı önle.
                if (ActiveRigs.Count > 0 && ActiveRigs[0] == oldest)
                    ActiveRigs.RemoveAt(0);
            }
        }

        private void Unregister()
        {
            ActiveRigs.Remove(this);
        }

        // ------------------------------------------------------------------ Geometri

        private void AddCollider(Bones b, RagdollPart part, float s)
        {
            var bone = b.Bone[(int)part];
            var end = b.End[(int)part];
            var frame = b.Frame;

            var go = new GameObject("RagdollCol");
            go.layer = RagdollLayer;
            go.transform.SetParent(bone, false);
            _colliderObjects.Add(go);

            var endLocal = Vector3.zero;
            var length = 0f;
            Vector3 dirWorld = -frame.up;
            if (end != null)
            {
                var d = end.position - bone.position;
                length = d.magnitude;
                if (length > 1e-4f)
                    dirWorld = d / length;
                endLocal = bone.InverseTransformPoint(end.position);
            }

            var spec = RagdollMath.Specs[(int)part];
            switch (part)
            {
                case RagdollPart.Head:
                {
                    var sph = go.AddComponent<SphereCollider>();
                    sph.radius = spec.Radius * s;
                    sph.center = bone.InverseTransformPoint(bone.position + frame.up * (0.09f * s));
                    break;
                }
                case RagdollPart.Hips:
                case RagdollPart.Spine:
                case RagdollPart.Chest:
                {
                    var width = part == RagdollPart.Chest ? 0.36f : (part == RagdollPart.Spine ? 0.30f : 0.32f);
                    var depth = part == RagdollPart.Chest ? 0.24f : 0.22f;
                    var len = Mathf.Max(0.12f * s, length * 1.1f);
                    var box = go.AddComponent<BoxCollider>();
                    box.center = endLocal * 0.5f;
                    var sz = Abs(bone.InverseTransformDirection(frame.right * (width * s))) +
                             Abs(bone.InverseTransformDirection(frame.forward * (depth * s))) +
                             Abs(bone.InverseTransformDirection(dirWorld * len));
                    box.size = sz;
                    break;
                }
                default:
                {
                    var cap = go.AddComponent<CapsuleCollider>();
                    var radius = spec.Radius * s;
                    var len = Mathf.Max(length, 0.15f * s);
                    var axis = DominantAxis(endLocal);
                    cap.direction = axis;
                    cap.radius = radius;
                    cap.height = Mathf.Max(radius * 2f, len + radius * 0.8f);
                    cap.center = end != null ? endLocal * 0.5f : Vector3.zero;
                    break;
                }
            }

            var col = go.GetComponent<Collider>();
            if (col != null)
                col.sharedMaterial = BodyMaterial();
        }

        private void AddJoint(Bones b, RagdollPart part, Rigidbody parent, int parentIndex)
        {
            var bone = b.Bone[(int)part];
            var frame = b.Frame;
            var spec = RagdollMath.Specs[(int)part];
            var end = b.End[(int)part];

            if (spec.Kind == RagdollJointKind.Hinge)
            {
                var hj = bone.gameObject.AddComponent<HingeJoint>();
                hj.connectedBody = parent;
                hj.axis = bone.InverseTransformDirection(frame.right).normalized;
                hj.enableCollision = false;
                hj.useLimits = true;

                // Mevcut bükülme açısı: üst parça yönü -> bu parça yönü, sağ eksen etrafında.
                var parentBone = b.Bone[parentIndex];
                var current = 0f;
                if (end != null && parentBone != null)
                {
                    var upper = (bone.position - parentBone.position);
                    var lower = (end.position - bone.position);
                    if (upper.sqrMagnitude > 1e-6f && lower.sqrMagnitude > 1e-6f)
                        current = Vector3.SignedAngle(upper, lower, frame.right);
                }

                RagdollMath.HingeRelativeLimits(spec.HingeMin, spec.HingeMax, current, out var min, out var max);
                var lim = hj.limits;
                lim.min = min;
                lim.max = max;
                hj.limits = lim;
                _joints.Add(hj);
                return;
            }

            var cj = bone.gameObject.AddComponent<CharacterJoint>();
            cj.connectedBody = parent;
            cj.enableCollision = false;
            cj.enablePreprocessing = false;
            cj.enableProjection = true;

            Vector3 twistWorld = part == RagdollPart.Head ? frame.up : -frame.up;
            if (end != null && (end.position - bone.position).sqrMagnitude > 1e-6f)
                twistWorld = (end.position - bone.position).normalized;
            var axis = bone.InverseTransformDirection(twistWorld).normalized;
            var swing = bone.InverseTransformDirection(frame.right).normalized;
            Vector3.OrthoNormalize(ref axis, ref swing);
            cj.axis = axis;
            cj.swingAxis = swing;

            var low = cj.lowTwistLimit;
            low.limit = spec.TwistLow;
            cj.lowTwistLimit = low;
            var high = cj.highTwistLimit;
            high.limit = spec.TwistHigh;
            cj.highTwistLimit = high;
            var s1 = cj.swing1Limit;
            s1.limit = spec.Swing1;
            cj.swing1Limit = s1;
            var s2 = cj.swing2Limit;
            s2.limit = spec.Swing2;
            cj.swing2Limit = s2;
            _joints.Add(cj);
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private static int DominantAxis(Vector3 v)
        {
            var ax = Mathf.Abs(v.x);
            var ay = Mathf.Abs(v.y);
            var az = Mathf.Abs(v.z);
            if (ay >= ax && ay >= az)
                return 1;
            return ax >= az ? 0 : 2;
        }
    }
}
