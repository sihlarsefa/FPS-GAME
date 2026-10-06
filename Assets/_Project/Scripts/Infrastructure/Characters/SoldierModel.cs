using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Characters.WeaponHold;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Content;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.Weapons;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Characters
{
    /// <summary>
    /// Düşük poligonlu TSK askeri (~1,8 m): dijital kamuflaj üniforma, kask (seviye 1-3) / bordo bere (tim komutanı) /
    /// kep, plaka taşıyıcı yelek (seviye 1-3), sırt çantası (seviye 1-3), tim kolluğu, Türk bayrağı arması, rütbe arması,
    /// postal ve eldiven. Prosedürel animasyon: yürüme/koşma döngüsü (bacaklar IK ile yere basar), çömelme, yüzüstü,
    /// oturma (araç), nişan açısı (gövde + kollar + baş), atış sekmesi, isabet irkilmesi, ölüm düşüşü (0,6 s).
    ///
    /// Kurallar:
    ///  • Kök (bu nesne) AYAK TABANINDADIR, +Z ileri. Yön (yaw) ebeveynden gelir.
    ///  • Oturma pozunda kalça kökten <see cref="SeatHeight"/> (varsayılan 0,45 m) yukarıdadır — kök araç zeminine
    ///    konmalıdır. Koltuk Transform'u minder yüzeyindeyse SeatHeight = 0 verin.
    ///  • <see cref="SetAimPitch"/> Unity kamera konvansiyonunu kullanır: pozitif = aşağı, negatif = yukarı.
    ///  • Görsel parçalar visualLayer katmanındadır ve collider içermez. Vuruş kutuları (createHitboxes) kemiklere bağlı,
    ///    GameLayers.Hitbox katmanında tetikleyicilerdir: baş küre, gövde 2 kutu, kollar ve bacaklar kapsül.
    ///    Hiyerarşinin katmanını toptan değiştirmeyin (SetLayerRecursively) — model vuruş kutusu katmanını yine de onarır.
    ///  • Sahibi (Combatant) verildiyse: EyePoint/AimPoint boşsa atanır, ölümde otomatik düşer, envanterdeki kask/yelek/
    ///    çanta seviyesi ve rütbe periyodik olarak yansıtılır, SetLocomotion çağrılmazsa Combatant.Velocity/Stance kullanılır.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class SoldierModel : MonoBehaviour
    {
        // ------------------------------------------------------------------ Ölçüler (metre)
        private const float StandHipHeight = SoldierRigMath.StandHipHeight;
        private const float CrouchHipHeight = 0.62f;
        private const float ProneHipHeight = 0.17f;
        private const float HipJointX = 0.095f;
        private const float HipJointY = SoldierRigMath.HipJointY;
        private const float ThighLength = SoldierRigMath.ThighLength;
        private const float ShinLength = SoldierRigMath.ShinLength;
        private const float AnkleHeight = SoldierRigMath.AnkleHeight;
        private const float ShoulderX = 0.215f;
        private const float UpperArmLength = 0.28f;
        private const float ForearmLength = 0.26f;
        private const float DeathDuration = 0.6f;
        private const float EquipmentPollInterval = 0.5f;
        private const int MaxCachedWeapons = 6;
        private const float MaxArmReach = 0.524f;
        private const float OffscreenUpdateInterval = 0.1f;
        private const string LeftHandAnchorName = "LeftHand";
        private const string RightHandAnchorName = "RightHand";

        // Tüfek taşıma duruşu: namlu ileri-aşağı ~24°, hafif sola çapraz — dipçik sağ omuzda, namlu gövde siluetini
        // keser. Yuva dönüşü birim kalırsa silah tam gövde ekseninde uzanır ve önden/arkadan yalnızca ~4×8 cm'lik
        // kesiti görünür (gövdenin arkasında kaybolur) — "elinde silah yok" hatasının kökü buydu.
        private const float RifleCarryPitch = 24f;
        private const float RifleCarryYaw = -14f;

        /// <summary>Tüfek taşıma dönüşü (weight 0..1: yüzüstünde düzleşir).</summary>
        private static Quaternion RifleCarry(float weight = 1f) =>
            Quaternion.Euler(RifleCarryPitch * weight, RifleCarryYaw * weight, 0f);

        private static readonly Quaternion BoneToZ = Quaternion.Euler(-90f, 0f, 0f);

        // ------------------------------------------------------------------ İç tipler
        private enum HoldKind
        {
            None,
            Rifle,
            Pistol
        }

        private struct RendererEntry
        {
            public Renderer Renderer;
            public ShadowCastingMode DefaultShadows;
        }

        private sealed class HeldWeapon
        {
            public string Key;
            public GameObject Root;
            public Transform Muzzle;
            public HoldKind Kind;
            public float LeftHandReach;
            public WeaponHoldProfile Profile;
            public float LastUsed;

            /// <summary>Silah fabrikasının sol/sağ el bileği bağlantıları (silah kökü uzayında), varsa.</summary>
            public bool HasLeftGrip;
            public Vector3 LeftGrip;
            public bool HasRightGrip;
            public Vector3 RightGrip;
        }

        private sealed class Variant
        {
            public readonly List<GameObject> Objects = new List<GameObject>(12);

            public void SetActive(bool active)
            {
                for (var i = 0; i < Objects.Count; i++)
                {
                    var go = Objects[i];
                    if (go != null && go.activeSelf != active)
                        go.SetActive(active);
                }
            }
        }

        private sealed class Materials
        {
            public Material Camo;
            public Material Skin;
            public Material SkinFace;
            public Material Gear;
            public Material Helmet;
            public Material NvgLens;
            public Material GearDark;
            public Material Boots;
            public Material Gloves;
            public Material Armband;
            public Material Metal;
            public Material Beret;
            public Material Flag;
            public Material Hair;
            public Material Gold;
            public Material Silver;
            public Material Red;
            public Material Cloth;
            public Material White;
        }

        // ------------------------------------------------------------------ Durum
        private readonly List<RendererEntry> _renderers = new List<RendererEntry>(96);
        private readonly List<Hitbox> _hitboxes = new List<Hitbox>(12);
        private readonly List<HeldWeapon> _weapons = new List<HeldWeapon>(4);
        private readonly Variant[] _helmets = new Variant[4];
        private readonly Variant[] _vests = new Variant[4];
        private readonly Variant[] _backpacks = new Variant[4];
        private readonly GameObject[] _rankPips = new GameObject[3];
        private readonly GameObject[] _rankChevrons = new GameObject[3];

        private Variant _beret;
        private bool _showBeretOverHelmet = true;
        private SoldierLook _look;
        private Materials _mat;
        private Renderer _skullRenderer;
        private Combatant _owner;
        private int _visualLayer;
        private bool _built;

        // Kemikler
        private Transform _body;
        private Transform _spine;
        private Transform _neck;
        private Transform _armsPivot;
        private Transform _leftHip;
        private Transform _rightHip;
        private Transform _leftKnee;
        private Transform _rightKnee;
        private Transform _leftAnkle;
        private Transform _rightAnkle;
        private Transform _leftShoulder;
        private Transform _rightShoulder;
        private Transform _leftElbow;
        private Transform _rightElbow;
        private Transform _leftHand;
        private Transform _rightHand;
        private Transform _eye;
        private Transform _aimPoint;
        private Transform _backpackRoot;
        private Transform _rankRoot;
        private GameObject _rankPatch;

        // Girdiler
        private Vector3 _velocity;
        private Stance _stance;
        private bool _grounded = true;
        private float _lastLocomotionTime = -999f;
        private float _aimPitchTarget;
        private bool _seated;
        private bool _visible = true;
        private bool _shadowsOnly;

        // Animasyon
        private float _aimPitch;
        private float _speed;
        private Vector3 _moveDir = Vector3.forward;
        private float _phase;
        private float _crouch;
        private float _prone;
        private float _seat;
        private float _air;
        private float _recoil;
        private WeaponHoldProfile _profile = WeaponHoldProfile.Default;
        private readonly WeaponReadyState _ready = new WeaponReadyState();
        private readonly WeaponSpring _aimSpring = new WeaponSpring();
        private float _obstacleDistance = float.PositiveInfinity;
        private Quaternion _leftHandAlign = Quaternion.Euler(-20f, 0f, 0f);
        private Quaternion _rightHandAlign = Quaternion.Euler(-35f, 0f, 0f);
        private float _flinch;
        private float _flinchSide;
        private int _gesture; // 0 yok, 1 şarjör, 2 fırlatma
        private float _gestureTime;
        private float _gestureDuration;
        private float _breath;

        // Silah
        private HeldWeapon _current;
        private HoldKind _hold = HoldKind.None;
        private Quaternion _ikLeftShoulder = Quaternion.identity;
        private Quaternion _ikLeftElbow = Quaternion.identity;
        private Quaternion _ikRightShoulder = Quaternion.identity;
        private Quaternion _ikRightElbow = Quaternion.identity;
        private Vector3 _socketRest;

        // Ekipman
        private int _helmetLevel = -1;
        private int _vestLevel = -1;
        private int _backpackLevel = -1;
        private int _rankApplied = -1;
        private float _pollTimer;
        private bool _ownerEquipmentFailed;
        private WeaponDefinitionData _lastOwnerWeapon;
        private bool _ownerWeaponFailed;

        // Ekran dışı seyreltme
        private Renderer _visibilityProbe;
        private float _pendingDt;

        // Ölüm
        private bool _dead;
        private float _deathTime;
        private Vector3 _fallAxis = Vector3.right;
        private Quaternion _deathStartRotation;
        private Vector3 _deathStartPosition;
        private bool _deathFromProne;
        private bool _deathFromSeat;
        private bool _deathWeaponHidden;
        private bool _poseStored;
        private Quaternion _restRotation;
        private Vector3 _restPosition;

        // ------------------------------------------------------------------ Genel API (sözleşme)

        public Transform Head { get; private set; }
        public Transform Chest { get; private set; }

        /// <summary>
        /// Silahın bağlandığı nokta (sağ el kabzası, +Z namlu yönü). Tüfekte yuva taşıma dönüşü alır
        /// (<see cref="RifleCarry"/>): namlu ileri-aşağı ~24°, hafif sola.
        /// </summary>
        public Transform WeaponSocket { get; private set; }

        // ------------------------------------------------------------------ Ek API

        /// <summary>Kalça (gövde pozu kökü).</summary>
        public Transform Hips => _body;

        /// <summary>Göz noktası (baş kemiğine bağlı).</summary>
        public Transform EyePoint => _eye;

        /// <summary>Gövde nişan noktası (göğüs merkezi).</summary>
        public Transform AimPoint => _aimPoint;

        /// <summary>Elde tutulan silahın namlu ucu (silah yoksa null).</summary>
        public Transform Muzzle => _current != null && _current.Muzzle != null ? _current.Muzzle : null;

        /// <summary>
        /// Elde tutulan silah görselinin kökü (silah yoksa null). Humanoid override'da silah yuvadan ele taşındığı
        /// için görünürlük denetimleri WeaponSocket yerine bunu kullanmalıdır.
        /// </summary>
        public Transform CurrentWeaponRoot => _current != null && _current.Root != null ? _current.Root.transform : null;

        /// <summary>Namlu ucunun dünya konumu (silah yoksa yuva önü tahmini).</summary>
        public Vector3 MuzzlePosition
        {
            get
            {
                var m = Muzzle;
                if (m != null)
                    return m.position;
                return WeaponSocket != null ? WeaponSocket.position + WeaponSocket.forward * 0.55f : transform.position + Vector3.up * 1.4f;
            }
        }

        public Combatant Owner => _owner;
        public SoldierLook Look => _look;
        /// <summary>Geçerli savaş yıpranması 0..1 (nicemlenmemiş son değer).</summary>
        public float Wear => _look != null ? _look.Wear : 0f;
        public IReadOnlyList<Hitbox> Hitboxes => _hitboxes;
        public bool IsDead => _dead;
        public bool IsVisible => _visible;
        public bool IsSeated => _seated;
        public WeaponDefinitionData HeldWeaponDefinition { get; private set; }
        public int HelmetLevel => _helmetLevel;
        public int VestLevel => _vestLevel;
        public int BackpackLevel => _backpackLevel;

        /// <summary>Oturma pozunda kalçanın kök üzerindeki yüksekliği (kök araç zeminindeyse 0,45 m).</summary>
        public float SeatHeight { get; set; } = 0.45f;

        /// <summary>Tim komutanı berisi kaskın yerine gösterilsin mi (varsayılan: evet). Kask seviyesi 0 iken bere her zaman görünür.</summary>
        public bool ShowBeretOverHelmet
        {
            get => _showBeretOverHelmet;
            set
            {
                if (_showBeretOverHelmet == value)
                    return;

                _showBeretOverHelmet = value;
                if (_built && _helmetLevel >= 0)
                    ApplyHeadgear();
            }
        }

        /// <summary>Sahibin envanterinden kask/yelek/çanta seviyesini ve rütbesini periyodik olarak yansıt.</summary>
        public bool AutoSyncEquipment { get; set; } = true;

        /// <summary>Sahip ölünce otomatik ölüm düşüşü oynat.</summary>
        public bool AutoPlayDeath { get; set; } = true;

        /// <summary>
        /// Sahibin envanterindeki etkin silah değişince modeli otomatik olarak o silahı tutar hale getirir.
        /// Açık <see cref="HoldWeapon"/> çağrıları, envanterdeki etkin silah bir sonraki değişene kadar geçerli kalır.
        /// </summary>
        public bool AutoSyncWeapon { get; set; } = true;

        /// <summary>
        /// Hiçbir kamera (gölge dahil) tarafından çizilmiyorsa poz güncellemesi ~10 Hz'e seyreltilir (vuruş kutuları yine izlenir).
        /// </summary>
        public bool ThrottleWhenOffscreen { get; set; } = true;

        // ------------------------------------------------------------------ Kurulum

        /// <summary>
        /// Asker modelini kurar. parent altında (yerel sıfır) "SoldierModel" nesnesi oluşturur. look null → varsayılan.
        /// owner: vuruş kutularının sahibi (null olabilir). Görsel parçalar visualLayer katmanına konur.
        /// </summary>
        public static SoldierModel Build(Transform parent, SoldierLook look, Combatant owner, bool createHitboxes, int visualLayer)
        {
            var go = new GameObject("SoldierModel");
            var layer = Mathf.Clamp(visualLayer, 0, 31);
            go.layer = layer;
            var t = go.transform;
            if (parent != null)
                t.SetParent(parent, false);

            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            if (look != null && owner != null && owner.IsLocalPlayer)
                CosmeticsRuntime.ApplyToLook(look);

            var model = go.AddComponent<SoldierModel>();
            model.Construct(look ?? SoldierLook.Default, owner, createHitboxes, layer);
            return model;
        }

        private void Construct(SoldierLook look, Combatant owner, bool createHitboxes, int visualLayer)
        {
            _look = look.Clone();
            _owner = owner;
            _visualLayer = visualLayer;
            if (owner != null && owner.Role == TeamRole.Leader)
                _look.Beret = true;

            _mat = CreateMaterials(_look);
            BuildSkeleton();
            BuildBody();
            CombineStaticParts();
            BuildRankInsignia();

            if (createHitboxes)
                BuildHitboxes(owner);

            if (owner != null)
            {
                if (owner.EyePoint == null)
                    owner.EyePoint = _eye;
                if (owner.AimPoint == null)
                    owner.AimPoint = _aimPoint;

                owner.Died += OnOwnerDied;
                owner.Damaged += OnOwnerDamaged;
            }

            _built = true;
            _pollTimer = UnityEngine.Random.Range(0f, EquipmentPollInterval);

            if (!TrySyncFromOwner(true))
                SetEquipment(1, 1, 1);

            if (owner != null)
            {
                SetRank(owner.Rank);
                SyncWeaponFromOwner();
            }

            if (_current == null)
                ComputeArmIk(null);
            ApplyPose(0f);
            TryApplyHumanoidOverride();
        }

        // ------------------------------------------------------------------ ContentOverrides humanoid (hazır asker)

        private GameObject _humanoidRoot;
        private Animator _humanoidAnimator;
        private Transform _humanoidHand;
        private HashSet<string> _animFloats;
        private HashSet<string> _animBools;

        /// <summary>Hazır humanoid prefab varsa görsel gövde olur; prosedürel parçalar gizlenir (vuruş kutuları kalır). Hata → prosedürel.</summary>
        private void TryApplyHumanoidOverride()
        {
            try
            {
                if (!ContentOverrides.TryGetSoldier(out var prefab, out var controller) || prefab == null)
                    return;

                var instance = UnityEngine.Object.Instantiate(prefab, transform, false);
                instance.name = "HumanoidVisual";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;

                var colliders = instance.GetComponentsInChildren<Collider>(true);
                for (var i = 0; i < colliders.Length; i++)
                {
                    colliders[i].enabled = false;
                    SafeDestroy(colliders[i]);
                }

                GameLayers.SetLayerRecursively(instance, _visualLayer);

                // Prosedürel görselleri gizle (silahlar hariç; onlar sonradan eklenir/taşınır).
                for (var i = _renderers.Count - 1; i >= 0; i--)
                {
                    var r = _renderers[i].Renderer;
                    if (r == null)
                    {
                        _renderers.RemoveAt(i);
                        continue;
                    }

                    if (WeaponSocket != null && r.transform.IsChildOf(WeaponSocket))
                        continue;
                    r.enabled = false;
                    _renderers.RemoveAt(i);
                }

                _humanoidRoot = instance;
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                for (var i = 0; i < renderers.Length; i++)
                    RegisterRenderer(renderers[i]);

                _humanoidAnimator = instance.GetComponentInChildren<Animator>(true);
                if (_humanoidAnimator == null)
                    _humanoidAnimator = instance.AddComponent<Animator>();
                _humanoidAnimator.applyRootMotion = false;
                if (controller != null)
                    _humanoidAnimator.runtimeAnimatorController = controller;

                _animFloats = new HashSet<string>();
                _animBools = new HashSet<string>();
                if (_humanoidAnimator.runtimeAnimatorController != null)
                {
                    var ps = _humanoidAnimator.parameters;
                    for (var i = 0; i < ps.Length; i++)
                    {
                        if (ps[i].type == AnimatorControllerParameterType.Float)
                            _animFloats.Add(ps[i].name);
                        else if (ps[i].type == AnimatorControllerParameterType.Bool)
                            _animBools.Add(ps[i].name);
                    }
                }

                _humanoidHand = _humanoidAnimator.isHuman ? _humanoidAnimator.GetBoneTransform(HumanBodyBones.RightHand) : null;
                if (_current != null)
                    AttachWeaponToHand(_current);
                SetupHumanoidExtras(instance);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SoldierModel] Humanoid override uygulanamadı, prosedürel kalıyor: " + e.Message);
                if (_humanoidRoot != null)
                    SafeDestroy(_humanoidRoot);
                _humanoidRoot = null;
                _humanoidAnimator = null;
                _humanoidHand = null;
                _socketHead = null;
                _socketChest = null;
            }
        }

        // ------------------------------------------------------------------ C9: humanoid ekstra (culling, LOD, kamuflaj, soketler, ragdoll)

        private Transform _socketHead;
        private Transform _socketChest;
        private SkinnedMeshRenderer[] _humanoidSkins;
        private int _animTier = -1;
        private float _animAccum;
        private bool _ragdoll;
        private Camera _lodCamera;

        /// <summary>Humanoid kafa aksesuar soketi (kask/bere); override yoksa null.</summary>
        public Transform HeadSocket => _socketHead;

        /// <summary>Humanoid göğüs aksesuar soketi (yelek/anten/çanta); override yoksa null.</summary>
        public Transform ChestSocket => _socketChest;

        /// <summary>Humanoid görsel kökü (yoksa null).</summary>
        public GameObject HumanoidRoot => _humanoidRoot;

        /// <summary>Ragdoll kurucu için humanoid kemik (override/humanoid rig yoksa null).</summary>
        public Transform GetHumanoidBone(HumanBodyBones bone)
        {
            return _humanoidAnimator != null && _humanoidAnimator.isHuman ? _humanoidAnimator.GetBoneTransform(bone) : null;
        }

        /// <summary>Ragdoll uyumu: Animator durdurulur, kemikler fiziğe bırakılır. Humanoid yoksa false.</summary>
        public bool EnterRagdollMode()
        {
            if (_humanoidAnimator == null)
                return false;
            _ragdoll = true;
            _humanoidAnimator.enabled = false;
            return true;
        }

        private Transform MapAccessoryParent(Transform parent)
        {
            if (_humanoidRoot == null || parent == null)
                return parent;
            if (parent == Head && _socketHead != null)
                return _socketHead;
            if (parent == Chest && _socketChest != null)
                return _socketChest;
            return parent;
        }

        private Transform MakeSocket(string name, Transform procedural, HumanBodyBones bone, HumanBodyBones fallback)
        {
            var b = _humanoidAnimator.GetBoneTransform(bone);
            if (b == null)
                b = _humanoidAnimator.GetBoneTransform(fallback);
            if (b == null || procedural == null)
                return null;
            var go = new GameObject(name);
            go.layer = _visualLayer;
            var t = go.transform;
            t.SetParent(b, false);
            t.SetPositionAndRotation(procedural.position, procedural.rotation);
            return t;
        }

        private void SetupHumanoidExtras(GameObject instance)
        {
            try
            {
                _humanoidAnimator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                _humanoidSkins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (var i = 0; i < _humanoidSkins.Length; i++)
                {
                    _humanoidSkins[i].updateWhenOffscreen = false;
                    _humanoidSkins[i].skinnedMotionVectors = false;
                }

                // Kamuflaj yuvalarını palet malzemesiyle değiştir.
                Material camo = null;
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                for (var i = 0; i < renderers.Length; i++)
                {
                    var mats = renderers[i].sharedMaterials;
                    var changed = false;
                    for (var k = 0; k < mats.Length; k++)
                    {
                        if (mats[k] == null || !SoldierDetailRules.IsCamoSlotName(mats[k].name))
                            continue;
                        if (camo == null)
                            camo = MaterialLibrary.Get(SoldierDetailRules.CamoMaterialFor(_look != null ? _look.PaletteIndex : 0));
                        if (camo == null)
                            continue;
                        mats[k] = camo;
                        changed = true;
                    }

                    if (changed)
                        renderers[i].sharedMaterials = mats;
                }

                if (_humanoidAnimator.isHuman)
                {
                    _socketHead = MakeSocket("Socket_Head", Head, HumanBodyBones.Head, HumanBodyBones.Neck);
                    _socketChest = MakeSocket("Socket_Chest", Chest, HumanBodyBones.UpperChest, HumanBodyBones.Chest);
                    AdoptAccessories(_helmets);
                    AdoptAccessories(_vests);
                    AdoptAccessories(_backpacks);
                    AdoptVariant(_beret);
                }
                else
                {
                    Debug.LogWarning("[SoldierModel] Humanoid rig değil: rol aksesuarları bağlanamadı.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SoldierModel] Humanoid ekstra kurulum hatası: " + e.Message);
            }
        }

        private void AdoptAccessories(Variant[] variants)
        {
            for (var i = 0; i < variants.Length; i++)
                AdoptVariant(variants[i]);
        }

        private void AdoptVariant(Variant v)
        {
            if (v == null)
                return;
            for (var i = 0; i < v.Objects.Count; i++)
            {
                var go = v.Objects[i];
                if (go == null)
                    continue;
                var p = go.transform.parent;
                var target = MapAccessoryParent(p);
                if (target == p)
                    continue;
                go.transform.SetParent(target, false);
                var r = go.GetComponent<Renderer>();
                if (r != null && !_renderers.Exists(e => e.Renderer == r))
                {
                    var entry = new RendererEntry { Renderer = r, DefaultShadows = r.shadowCastingMode };
                    _renderers.Add(entry);
                    ApplyRendererState(entry);
                }
            }
        }

        /// <summary>Mesafeye göre Animator güncelleme sıklığı, skin kalitesi ve culling.</summary>
        private void TickHumanoidLod()
        {
            if (_ragdoll)
                return;
            var dist = 0f;
            if (_lodCamera == null)
                _lodCamera = Camera.main;
            if (_lodCamera != null)
                dist = Vector3.Distance(_lodCamera.transform.position, transform.position);

            var tier = SoldierDetailRules.EffectiveTier(dist, _dead);
            _humanoidAnimator.cullingMode = SoldierDetailRules.CullCompletely(_visible, _shadowsOnly)
                ? AnimatorCullingMode.CullCompletely
                : AnimatorCullingMode.CullUpdateTransforms;

            if (tier != _animTier)
            {
                _animTier = tier;
                _humanoidAnimator.enabled = tier == 0;
                _animAccum = 0f;
                if (_humanoidSkins != null)
                {
                    var bones = SoldierDetailRules.SkinBonesFor(tier);
                    var q = bones >= 4 ? SkinQuality.Bone4 : (bones == 2 ? SkinQuality.Bone2 : SkinQuality.Bone1);
                    for (var i = 0; i < _humanoidSkins.Length; i++)
                        if (_humanoidSkins[i] != null)
                            _humanoidSkins[i].quality = q;
                }
            }

            if (tier == 0)
                return;
            _animAccum += Time.deltaTime;
            var interval = SoldierDetailRules.AnimInterval(tier);
            if (_animAccum >= interval && (_visible || _shadowsOnly))
            {
                _humanoidAnimator.Update(_animAccum);
                _animAccum = 0f;
            }
        }

        /// <summary>
        /// Humanoid override: silahı sağ el kemiğine bağlar. El kemiği eksenleri rastgele olabilir ve kemik ölçeği
        /// birim olmayabilir — yerel sıfır/birim bırakılırsa silah gövdeye saplanır ya da büzülür. Bu yüzden dünya
        /// ölçeği 1'e normalize edilir, yön gövdeye göre taşıma duruşuna çevrilir ve kabza bağlantısı avuca oturtulur.
        /// </summary>
        private void AttachWeaponToHand(HeldWeapon held)
        {
            if (_humanoidHand == null || held == null || held.Root == null)
                return;

            var t = held.Root.transform;
            if (t.parent != _humanoidHand)
                t.SetParent(_humanoidHand, true);

            var s = _humanoidHand.lossyScale;
            t.localScale = new Vector3(
                1f / Mathf.Max(Mathf.Abs(s.x), 1e-4f),
                1f / Mathf.Max(Mathf.Abs(s.y), 1e-4f),
                1f / Mathf.Max(Mathf.Abs(s.z), 1e-4f));

            t.rotation = transform.rotation * (held.Kind == HoldKind.Rifle ? RifleCarry() : Quaternion.identity);

            var grip = FindDescendant(t, RightHandAnchorName);
            t.position = grip != null ? t.position + (_humanoidHand.position - grip.position) : _humanoidHand.position;
        }

        private void SetAnim(string name, float value)
        {
            if (_animFloats != null && _animFloats.Contains(name))
                _humanoidAnimator.SetFloat(name, value);
        }

        private void SetAnim(string name, bool value)
        {
            if (_animBools != null && _animBools.Contains(name))
                _humanoidAnimator.SetBool(name, value);
        }

        private void DriveHumanoid()
        {
            if (_humanoidAnimator == null)
                return;
            SetAnim("Speed", _speed);
            SetAnim("Crouch", _crouch);
            SetAnim("Crouch", _crouch > 0.5f);
            SetAnim("Prone", _prone);
            SetAnim("Prone", _prone > 0.5f);
            SetAnim("AimPitch", _aimPitch);
            SetAnim("Dead", _dead);
            TickHumanoidLod();
        }

        /// <summary>
        /// Savaş yıpranmasını (0..1) ayarlar. Doku seviyesi (0/.33/.66/1) değişmediyse hiçbir şey yapmaz; değiştiyse paylaşımlı
        /// önbellekli malzemeleri yeniden bağlar (mesh/renderer yeniden kurulmaz). Seviye değiştiyse true döner.
        /// </summary>
        public bool SetWear(float wear)
        {
            if (_look == null || _mat == null)
                return false;
            wear = Mathf.Clamp01(wear);
            var changed = WearRules.NeedsRebind(_look.Wear, wear);
            _look.Wear = wear;
            if (!changed)
                return false;

            var old = _mat;
            var next = CreateMaterials(_look);
            var map = new Dictionary<Material, Material>(16);
            void Pair(Material a, Material b)
            {
                if (a != null && b != null && a != b && !map.ContainsKey(a))
                    map[a] = b;
            }

            Pair(old.Camo, next.Camo); Pair(old.Skin, next.Skin); Pair(old.Gear, next.Gear); Pair(old.GearDark, next.GearDark);
            Pair(old.Helmet, next.Helmet); Pair(old.Boots, next.Boots); Pair(old.Gloves, next.Gloves); Pair(old.Cloth, next.Cloth);
            var shared = new List<Material>(4);
            for (var i = 0; i < _renderers.Count; i++)
            {
                var r = _renderers[i].Renderer;
                if (r == null)
                    continue;
                if (r == _skullRenderer)
                {
                    r.sharedMaterial = next.SkinFace ?? next.Skin;
                    continue;
                }

                r.GetSharedMaterials(shared);
                var dirty = false;
                for (var k = 0; k < shared.Count; k++)
                {
                    if (shared[k] != null && map.TryGetValue(shared[k], out var repl))
                    {
                        shared[k] = repl;
                        dirty = true;
                    }
                }

                if (dirty)
                    r.SetSharedMaterials(shared);
            }

            // Ortak malzemeleri yeniden kullanan sonradan kurulan parçalar için yeni set geçerli olur.
            next.Metal = old.Metal;
            _mat = next;
            return true;
        }

        private static Materials CreateMaterials(SoldierLook look)
        {
            var m = new Materials();
            var wear = WearRules.Quantize(look.Wear);
            try
            {
                m.Camo = CharacterMaterials.Camo(look.CamoA, look.CamoB, look.CamoC, look.CamoD, look.CamoSeed, wear)
                    ?? MaterialLibrary.Camo(look.CamoA, look.CamoB, look.CamoC, look.CamoD, look.CamoSeed, 1f);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SoldierModel] Kamuflaj malzemesi üretilemedi, düz renk kullanılıyor: " + e.Message);
                m.Camo = null;
            }

            if (m.Camo == null)
                m.Camo = MaterialLibrary.Lit(look.CamoA, 0.12f);

            // Gerçekçi karakter shader'ları (HAREKAT/Character/*); bulunamazsa URP/Lit yedeği.
            m.Skin = CharacterMaterials.Skin(look.Skin) ?? MaterialLibrary.Lit(look.Skin, 0.3f);
            m.SkinFace = wear > 0f ? CharacterMaterials.SkinFace(look.Skin, wear, WearRules.FaceVariant(look.CamoSeed)) ?? m.Skin : m.Skin;
            m.Gear = CharacterMaterials.Solid(CharacterMaterialKind.Cordura, look.Gear, -1f, wear) ?? MaterialLibrary.Lit(look.Gear, 0.15f);
            m.GearDark = CharacterMaterials.Solid(CharacterMaterialKind.Cordura, look.Gear * 0.8f, -1f, wear) ?? MaterialLibrary.Lit(look.Gear * 0.8f, 0.15f);
            m.Helmet = CharacterMaterials.Solid(CharacterMaterialKind.HelmetPaint, look.Gear, -1f, wear) ?? m.Gear;
            m.Boots = CharacterMaterials.Solid(CharacterMaterialKind.Leather, SoldierLook.BootColor, 0.04f, wear) ?? MaterialLibrary.Lit(SoldierLook.BootColor, 0.04f);
            m.Gloves = CharacterMaterials.Solid(CharacterMaterialKind.Rubber, SoldierLook.GloveColor, 0.06f, wear) ?? MaterialLibrary.Lit(SoldierLook.GloveColor, 0.06f);
            m.NvgLens = CharacterMaterials.NvgLens() ?? MaterialLibrary.Get(MaterialId.GunMetal);
            m.Armband = ArmbandMaterial(look.Armband);
            m.Metal = MaterialLibrary.Get(MaterialId.GunMetal);
            m.Beret = MaterialLibrary.Lit(look.HasBeretColor ? look.BeretColor : SoldierLook.BeretBordo, 0.05f);
            m.Flag = MaterialLibrary.Get(MaterialId.TurkishFlag);
            m.Hair = MaterialLibrary.Lit(new Color(0.07f, 0.055f, 0.045f), 0.2f);
            m.Gold = MaterialLibrary.Lit(new Color(0.86f, 0.68f, 0.2f), 0.6f, 0.8f);
            m.Silver = MaterialLibrary.Lit(new Color(0.76f, 0.77f, 0.79f), 0.6f, 0.8f);
            m.Red = MaterialLibrary.Lit(new Color(0.75f, 0.08f, 0.08f), 0.3f);
            m.White = MaterialLibrary.Lit(new Color(0.92f, 0.92f, 0.9f), 0.25f);
            m.Cloth = CharacterMaterials.Solid(CharacterMaterialKind.Fabric, ClothColor(look), -1f, wear) ?? MaterialLibrary.Lit(ClothColor(look), 0.1f);
            return m;
        }

        /// <summary>Balaklava/şemagh kumaş rengi: palete göre.</summary>
        private static Color ClothColor(SoldierLook look)
        {
            switch (look.PaletteIndex)
            {
                case 1: return new Color(0.2f, 0.21f, 0.19f);
                case 2: return look.FaceCover == FaceCoverKind.Shemagh ? new Color(0.76f, 0.68f, 0.5f) : new Color(0.55f, 0.47f, 0.34f);
                case 3: return new Color(0.1f, 0.1f, 0.11f);
                default: return new Color(0.13f, 0.16f, 0.1f);
            }
        }

        /// <summary>Kütüphanedeki parlayan kolluk renklerinden biriyse onu kullan (uzaktan seçilebilir), yoksa düz Lit.</summary>
        private static Material ArmbandMaterial(Color c)
        {
            // Mat kumaş: emisyon yok, hafif koyu ton (parlayan kütüphane kolluklarının yerine).
            if (Near(c, new Color(0.1f, 0.3f, 0.85f)))
                return MaterialLibrary.Lit(SoldierLook.ArmbandMatBlue, 0.02f);
            return MaterialLibrary.Lit(new Color(c.r * 0.78f, c.g * 0.78f, c.b * 0.78f), 0.04f);
        }

        private static bool Near(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f;
        }

        private void BuildSkeleton()
        {
            var root = transform;
            _body = Bone("Hips", root, new Vector3(0f, StandHipHeight, 0f));
            _leftHip = Bone("LeftHip", _body, new Vector3(-HipJointX, HipJointY, 0f));
            _rightHip = Bone("RightHip", _body, new Vector3(HipJointX, HipJointY, 0f));
            _leftKnee = Bone("LeftKnee", _leftHip, new Vector3(0f, -ThighLength, 0f));
            _rightKnee = Bone("RightKnee", _rightHip, new Vector3(0f, -ThighLength, 0f));
            _leftAnkle = Bone("LeftAnkle", _leftKnee, new Vector3(0f, -ShinLength, 0f));
            _rightAnkle = Bone("RightAnkle", _rightKnee, new Vector3(0f, -ShinLength, 0f));

            _spine = Bone("Spine", _body, new Vector3(0f, 0.08f, 0f));
            Chest = Bone("Chest", _spine, new Vector3(0f, 0.2f, 0f));
            _neck = Bone("Neck", Chest, new Vector3(0f, 0.27f, 0f));
            Head = Bone("Head", _neck, new Vector3(0f, 0.07f, 0.01f));
            _eye = Bone("Eye", Head, new Vector3(0f, 0.1f, 0.1f));
            _aimPoint = Bone("AimPoint", Chest, new Vector3(0f, 0.12f, 0f));

            _armsPivot = Bone("ArmsPivot", Chest, new Vector3(0f, 0.21f, 0f));
            _leftShoulder = Bone("LeftShoulder", _armsPivot, new Vector3(-ShoulderX, 0f, 0f));
            _rightShoulder = Bone("RightShoulder", _armsPivot, new Vector3(ShoulderX, 0f, 0f));
            _leftElbow = Bone("LeftElbow", _leftShoulder, new Vector3(0f, -UpperArmLength, 0f));
            _rightElbow = Bone("RightElbow", _rightShoulder, new Vector3(0f, -UpperArmLength, 0f));
            _leftHand = Bone("LeftHand", _leftElbow, new Vector3(0f, -ForearmLength, 0f));
            _rightHand = Bone("RightHand", _rightElbow, new Vector3(0f, -ForearmLength, 0f));

            WeaponSocket = Bone("WeaponSocket", _armsPivot, new Vector3(0.1f, -0.08f, 0.22f));
            _socketRest = WeaponSocket.localPosition;

            _backpackRoot = Bone("Backpack", Chest, new Vector3(0f, 0f, -0.11f));
            _rankRoot = Bone("Rank", Chest, new Vector3(0f, 0.2f, 0.118f));
        }

        private Transform Bone(string name, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.layer = _visualLayer;
            var t = go.transform;
            t.SetParent(MapAccessoryParent(parent), false);
            t.localPosition = localPosition;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            return t;
        }

        private GameObject Part(string name, Transform parent, Mesh mesh, Material material, Vector3 localPosition, Quaternion localRotation,
            bool castShadows = true)
        {
            var go = new GameObject(name);
            go.layer = _visualLayer;
            var t = go.transform;
            t.SetParent(MapAccessoryParent(parent), false);
            t.localPosition = localPosition;
            t.localRotation = localRotation;
            t.localScale = Vector3.one;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = true;
            RegisterRenderer(renderer);
            return go;
        }

        private GameObject Part(string name, Transform parent, Mesh mesh, Material material, Vector3 localPosition, bool castShadows = true)
        {
            return Part(name, parent, mesh, material, localPosition, Quaternion.identity, castShadows);
        }

        private void RegisterRenderer(Renderer renderer)
        {
            if (renderer == null)
                return;

            if (_humanoidRoot != null && !renderer.transform.IsChildOf(_humanoidRoot.transform) &&
                (WeaponSocket == null || !renderer.transform.IsChildOf(WeaponSocket)))
            {
                renderer.enabled = false;
                return;
            }

            var entry = new RendererEntry { Renderer = renderer, DefaultShadows = renderer.shadowCastingMode };
            _renderers.Add(entry);
            ApplyRendererState(entry);
        }

        private void ApplyRendererState(RendererEntry entry)
        {
            var r = entry.Renderer;
            if (r == null)
                return;

            var enabled = _visible || _shadowsOnly;
            if (r.enabled != enabled)
                r.enabled = enabled;

            var mode = !_visible && _shadowsOnly
                ? (entry.DefaultShadows == ShadowCastingMode.Off ? ShadowCastingMode.Off : ShadowCastingMode.ShadowsOnly)
                : entry.DefaultShadows;
            if (r.shadowCastingMode != mode)
                r.shadowCastingMode = mode;
        }

        // ------------------------------------------------------------------ Gövde

        private void BuildBody()
        {
            var m = _mat;

            // Kalça + kemer
            Part("Pelvis", _body, CharacterMeshes.Cloth("pelvisLSculpt", new[] {
                new CharacterMeshes.Ring(-0.12f, 0.125f, 0.088f), new CharacterMeshes.Ring(-0.03f, 0.155f, 0.1f), new CharacterMeshes.Ring(0.08f, 0.165f, 0.105f)
            }), m.Camo, Vector3.zero);
            Part("Belt", _body, CharacterMeshes.Cylinder("beltSculpt", -0.0275f, 0.0275f, 0.173f, 0.173f, 24, false, 0.66f), m.GearDark,
                new Vector3(0f, 0.05f, 0f), false);
            Part("Buckle", _body, CharacterMeshes.Box("buckle", Vector3.zero, new Vector3(0.05f, 0.04f, 0.012f)), m.Metal,
                new Vector3(0f, 0.05f, 0.116f), false);
            Part("BeltPouch", _body, CharacterMeshes.RoundedBox("beltPouchRounded", Vector3.zero, new Vector3(0.07f, 0.09f, 0.05f)), m.GearDark,
                new Vector3(0.13f, 0.02f, -0.08f), false);

            BuildLeg(_leftHip, _leftKnee, _leftAnkle, -1f);
            BuildLeg(_rightHip, _rightKnee, _rightAnkle, 1f);

            // Gövde
            Part("Abdomen", _spine, CharacterMeshes.Cloth("abdomenLSculpt", new[] {
                new CharacterMeshes.Ring(0f, 0.155f, 0.1f), new CharacterMeshes.Ring(0.1f, 0.146f, 0.097f), new CharacterMeshes.Ring(0.21f, 0.17f, 0.104f)
            }), m.Camo, Vector3.zero);
            // Göğüs: koltuk altından omuza doğru genişleyip trapez hattıyla boyuna eğimli daralır (omuz eğimi).
            var chestShape = Part("ChestShape", Chest, CharacterMeshes.Cloth("chestL2Sculpt", new[] {
                    new CharacterMeshes.Ring(0f, 0.17f, 0.103f), new CharacterMeshes.Ring(0.1f, 0.185f, 0.112f),
                    new CharacterMeshes.Ring(0.17f, 0.2f, 0.112f), new CharacterMeshes.Ring(0.22f, 0.19f, 0.1f),
                    new CharacterMeshes.Ring(0.255f, 0.13f, 0.082f), new CharacterMeshes.Ring(0.285f, 0.075f, 0.068f)
                }),
                m.Camo, Vector3.zero);
            _visibilityProbe = chestShape.GetComponent<Renderer>();
            Part("Collar", Chest, CharacterMeshes.Lathe("collarL", new[]
            {
                new CharacterMeshes.Ring(0.235f, 0.125f, 0.098f), new CharacterMeshes.Ring(0.275f, 0.088f, 0.078f), new CharacterMeshes.Ring(0.31f, 0.07f, 0.07f)
            }, 10, false, false), m.Camo, Vector3.zero, false);

            // Boyun ve baş
            Part("NeckShape", _neck, CharacterMeshes.Lathe("neckL", new[]
            {
                new CharacterMeshes.Ring(-0.05f, 0.075f, 0.065f), new CharacterMeshes.Ring(0f, 0.058f, 0.056f),
                new CharacterMeshes.Ring(0.045f, 0.052f, 0.054f, 0.005f), new CharacterMeshes.Ring(0.09f, 0.05f, 0.052f, 0.01f)
            }, 10, false, false), m.Skin, Vector3.zero);
            // Kafatası: ~0.235 m yüksekliğinde, çeneye doğru daralan yumurta profili (kutu/küre değil).
            _skullRenderer = Part("Skull", Head, CharacterMeshes.SculptedHead(), m.SkinFace ?? m.Skin, Vector3.zero).GetComponent<Renderer>();
            Part("Hair", Head, CharacterMeshes.Ellipsoid("hair2", new Vector3(0f, 0.095f, -0.006f), new Vector3(0.083f, 0.12f, 0.104f), 16, 6, 12f, 90f),
                m.Hair, Vector3.zero, false);
            BuildEyes();
            // Kulaklar: kafatasına yaslanan küçük basık elipsoitler (dışa taşma ~6 mm — eski 9 mm'lik çıkıntı azaltıldı).
            var ear = CharacterMeshes.Ellipsoid("ear3", Vector3.zero, new Vector3(0.006f, 0.021f, 0.015f), 10, 4);
            Part("EarL", Head, ear, m.Skin, new Vector3(-0.0755f, 0.083f, -0.004f), false);
            Part("EarR", Head, ear, m.Skin, new Vector3(0.0755f, 0.083f, -0.004f), false);
            BuildFaceDetail();
            if (_look.Mustache && _look.FaceCover == FaceCoverKind.None)
            {
                Part("Mustache", Head, CharacterMeshes.Box("mustache", Vector3.zero, new Vector3(0.062f, 0.014f, 0.016f)), m.Hair,
                    new Vector3(0f, 0.052f, 0.101f), false);
            }

            BuildArm(_leftShoulder, _leftElbow, _leftHand, -1f);
            BuildArm(_rightShoulder, _rightElbow, _rightHand, 1f);
        }

        private void BuildLeg(Transform hip, Transform knee, Transform ankle, float side)
        {
            var m = _mat;
            Part("Thigh", hip, CharacterMeshes.Cloth("thighL2Sculpt", new[] {
                new CharacterMeshes.Ring(-0.435f, 0.052f, 0.056f), new CharacterMeshes.Ring(-0.34f, 0.06f, 0.065f),
                new CharacterMeshes.Ring(-0.2f, 0.076f, 0.082f), new CharacterMeshes.Ring(-0.08f, 0.088f, 0.092f),
                new CharacterMeshes.Ring(0.03f, 0.085f, 0.088f)
            }), m.Camo, Vector3.zero);
            Part("CargoPocket", hip, CharacterMeshes.RoundedBox("cargoSculpt", Vector3.zero, new Vector3(0.03f, 0.12f, 0.1f)), m.Camo,
                new Vector3(side * 0.071f, -0.21f, 0f), false);
            Part("Shin", knee, CharacterMeshes.Cloth("shinL2Sculpt", new[] {
                new CharacterMeshes.Ring(-0.38f, 0.05f, 0.054f), new CharacterMeshes.Ring(-0.31f, 0.064f, 0.068f, -0.002f),
                new CharacterMeshes.Ring(-0.27f, 0.052f, 0.058f, -0.004f), new CharacterMeshes.Ring(-0.2f, 0.056f, 0.064f, -0.012f),
                new CharacterMeshes.Ring(-0.1f, 0.052f, 0.058f, -0.006f), new CharacterMeshes.Ring(-0.02f, 0.056f, 0.061f),
                new CharacterMeshes.Ring(0.02f, 0.058f, 0.062f)
            }), m.Camo, Vector3.zero);
            Part("KneePad", knee, CharacterMeshes.Ellipsoid("kneepadL2", Vector3.zero, new Vector3(0.036f, 0.048f, 0.014f), 10, 4), m.Gear,
                new Vector3(0f, -0.012f, 0.058f), false);
            Part("KneePadStrap", knee, CharacterMeshes.Box("kneepadStrap2", Vector3.zero, new Vector3(0.108f, 0.012f, 0.116f)), m.GearDark,
                new Vector3(0f, -0.058f, 0f), false);
            Part("KneePadStrapTop", knee, CharacterMeshes.Box("kneepadStrapTop", Vector3.zero, new Vector3(0.106f, 0.012f, 0.114f)), m.GearDark,
                new Vector3(0f, 0.036f, 0f), false);
            // Bot: bilekte daralan kaftan, öne uzanan yumuşak ayak, burun kapağı, taban ve bağcık.
            Part("BootShaft", ankle, CharacterMeshes.Lathe("bootShaftL", new[]
            {
                new CharacterMeshes.Ring(-0.055f, 0.05f, 0.058f), new CharacterMeshes.Ring(0f, 0.049f, 0.056f, -0.002f),
                new CharacterMeshes.Ring(0.05f, 0.054f, 0.06f, -0.002f), new CharacterMeshes.Ring(0.09f, 0.057f, 0.064f, -0.003f)
            }, 10, false, true), m.Boots, Vector3.zero);
            Part("BootFoot", ankle, CharacterMeshes.Ellipsoid("bootFoot2", new Vector3(0f, -0.042f, 0.08f), new Vector3(0.053f, 0.046f, 0.138f), 20, 10),
                m.Boots, Vector3.zero);
            Part("BootToe", ankle, CharacterMeshes.RoundedBox("bootToeCap2", new Vector3(0f, -0.05f, 0.195f), new Vector3(0.088f, 0.044f, 0.07f)),
                m.Gloves, Vector3.zero, false);
            Part("BootSole", ankle, CharacterMeshes.RoundedBox("bootSoleSculpt", new Vector3(0f, -0.07f, 0.08f), new Vector3(0.110f, 0.025f, 0.278f)), m.Gloves,
                Vector3.zero, false);
            Part("BootLaces", ankle, CharacterMeshes.BootLacing(), m.GearDark, Vector3.zero, false);
        }

        private void BuildArm(Transform shoulder, Transform elbow, Transform hand, float side)
        {
            var m = _mat;
            Part("ShoulderCap", shoulder, CharacterMeshes.Ellipsoid("shoulderCap2", new Vector3(0f, -0.015f, 0f), new Vector3(0.062f, 0.058f, 0.062f), 16, 8),
                m.Camo, Vector3.zero);
            // Deltoid omuz pedi: yelek kenarı ile kol arasındaki sert dikişi örter (yumuşak normal, kolla döner).
            Part("DeltoidPad", shoulder, CharacterMeshes.Ellipsoid("deltoidPadV1", new Vector3(0f, -0.028f, 0f), new Vector3(0.071f, 0.085f, 0.071f), 10, 4, -38f, 90f),
                m.Gear, Vector3.zero);
            Part("UpperArm", shoulder, CharacterMeshes.Cloth("upperArmL2Sculpt", new[] {
                new CharacterMeshes.Ring(-0.28f, 0.04f, 0.043f), new CharacterMeshes.Ring(-0.2f, 0.047f, 0.049f),
                new CharacterMeshes.Ring(-0.1f, 0.052f, 0.054f), new CharacterMeshes.Ring(-0.02f, 0.056f, 0.056f),
                new CharacterMeshes.Ring(0f, 0.05f, 0.05f)
            }), m.Camo, Vector3.zero);
            Part("Armband", shoulder, CharacterMeshes.Lathe("armbandL", new[]
            {
                new CharacterMeshes.Ring(-0.1425f, 0.0555f, 0.0575f), new CharacterMeshes.Ring(-0.11f, 0.0575f, 0.0595f)
            }, 10, false, false), m.Armband, Vector3.zero, false);
            Part("ArmbandSeam", shoulder, CharacterMeshes.Box("armbandSeam", Vector3.zero, new Vector3(0.012f, 0.034f, 0.004f)), m.GearDark,
                new Vector3(0f, -0.1275f, 0.0595f), false);
            if (side < 0f)
            {
                // Türk bayrağı arması (sol kol dış yüzü).
                Part("FlagPatch", shoulder, CharacterMeshes.Quad("flagPatch", 0.075f, 0.05f), m.Flag, new Vector3(-0.0585f, -0.065f, 0f),
                    Quaternion.Euler(0f, -90f, 0f), false);
            }

            Part("Forearm", elbow, CharacterMeshes.Cloth("forearmL2Sculpt", new[] {
                new CharacterMeshes.Ring(-0.26f, 0.032f, 0.032f), new CharacterMeshes.Ring(-0.18f, 0.038f, 0.037f),
                new CharacterMeshes.Ring(-0.08f, 0.045f, 0.044f), new CharacterMeshes.Ring(0.01f, 0.046f, 0.045f)
            }), m.Camo, Vector3.zero);
            // Eldiven manşeti: bilekte koyu kauçuk gauntlet — kamuflaj kol ağzı bileğe kadar iner, ten görünmez.
            Part("Cuff", elbow, CharacterMeshes.Cylinder("cuff3", -0.275f, -0.22f, 0.0345f, 0.041f, 10, false), m.Gloves, Vector3.zero, false);
            Part("ElbowPad", elbow, CharacterMeshes.Ellipsoid("elbowPad", Vector3.zero, new Vector3(0.045f, 0.04f, 0.03f), 6, 3), m.Gear,
                new Vector3(0f, 0.0f, -0.04f), false);
            Part("Glove", hand, CharacterMeshes.Hand(side < 0f ? "handL" : "handR", -side), m.Gloves, Vector3.zero, false);
        }

        // ------------------------------------------------------------------ Rütbe

        private void BuildRankInsignia()
        {
            _rankPatch = Part("RankPatch", _rankRoot, CharacterMeshes.Box("rankPatch", Vector3.zero, new Vector3(0.06f, 0.075f, 0.006f)),
                _mat.GearDark, Vector3.zero, false);
            for (var i = 0; i < 3; i++)
            {
                _rankPips[i] = Part("Pip" + i, _rankRoot, CharacterMeshes.Box("rankPip", Vector3.zero, new Vector3(0.014f, 0.014f, 0.006f)),
                    _mat.Gold, new Vector3(0f, 0.022f - i * 0.022f, 0.004f), Quaternion.Euler(0f, 0f, 45f), false);
                _rankChevrons[i] = Part("Chevron" + i, _rankRoot, CharacterMeshes.Box("rankChevron", Vector3.zero, new Vector3(0.044f, 0.008f, 0.006f)),
                    _mat.Silver, new Vector3(0f, 0.02f - i * 0.016f, 0.004f), false);
                _rankPips[i].SetActive(false);
                _rankChevrons[i].SetActive(false);
            }

            _rankPatch.SetActive(false);
        }

        /// <summary>Göğüs rütbe arması: subay altın yıldız, astsubay gümüş, uzman/sözleşmeli koyu altın, er/onbaşı/çavuş kırmızı şerit.</summary>
        public void SetRank(MilitaryRank rank)
        {
            if (!_built)
                return;

            var value = (int)rank;
            if (value == _rankApplied)
                return;

            _rankApplied = value;
            int pips = 0, chevrons = 0;
            Material chevronMaterial = _mat.Silver;
            Material pipMaterial = _mat.Gold;

            if (rank >= MilitaryRank.Astegmen)
            {
                switch (rank)
                {
                    case MilitaryRank.Astegmen:
                    case MilitaryRank.Tegmen:
                        pips = 1;
                        break;
                    case MilitaryRank.Ustegmen:
                        pips = 2;
                        break;
                    default:
                        pips = 3;
                        break;
                }
            }
            else if (rank >= MilitaryRank.AstsubayCavus)
            {
                chevrons = rank >= MilitaryRank.AstsubayKidemliUstcavus ? 3 : rank >= MilitaryRank.AstsubayKidemliCavus ? 2 : 1;
                chevronMaterial = _mat.Silver;
            }
            else if (rank >= MilitaryRank.SozlesmeliEr)
            {
                chevrons = rank == MilitaryRank.UzmanCavus ? 2 : 1;
                chevronMaterial = _mat.Gold;
            }
            else
            {
                chevrons = rank == MilitaryRank.Cavus ? 2 : rank == MilitaryRank.Onbasi ? 1 : 0;
                chevronMaterial = _mat.Red;
            }

            for (var i = 0; i < 3; i++)
            {
                var pip = _rankPips[i];
                if (pip != null)
                {
                    pip.SetActive(i < pips);
                    SetMaterial(pip, pipMaterial);
                }

                var chevron = _rankChevrons[i];
                if (chevron != null)
                {
                    chevron.SetActive(i < chevrons);
                    SetMaterial(chevron, chevronMaterial);
                }
            }

            if (_rankPatch != null)
                _rankPatch.SetActive(pips > 0 || chevrons > 0);
        }

        private static void SetMaterial(GameObject go, Material material)
        {
            if (go != null && go.TryGetComponent<MeshRenderer>(out var r) && r.sharedMaterial != material)
                r.sharedMaterial = material;
        }

        // ------------------------------------------------------------------ Vuruş kutuları

        private void BuildHitboxes(Combatant owner)
        {
            AddHitbox(Hitbox.CreateSphere(Head, owner, BodyPart.Head, new Vector3(0f, 0.1f, 0f), 0.13f));
            AddHitbox(Hitbox.CreateBox(Chest, owner, BodyPart.Torso, new Vector3(0f, 0.14f, 0f), new Vector3(0.46f, 0.3f, 0.3f)));
            AddHitbox(Hitbox.CreateBox(_spine, owner, BodyPart.Torso, new Vector3(0f, 0.02f, 0f), new Vector3(0.38f, 0.32f, 0.27f)));
            AddHitbox(Hitbox.CreateCapsule(_leftShoulder, owner, BodyPart.Arm, new Vector3(0f, -0.14f, 0f), 0.065f, 0.33f, 1));
            AddHitbox(Hitbox.CreateCapsule(_rightShoulder, owner, BodyPart.Arm, new Vector3(0f, -0.14f, 0f), 0.065f, 0.33f, 1));
            AddHitbox(Hitbox.CreateCapsule(_leftElbow, owner, BodyPart.Arm, new Vector3(0f, -0.15f, 0f), 0.055f, 0.32f, 1));
            AddHitbox(Hitbox.CreateCapsule(_rightElbow, owner, BodyPart.Arm, new Vector3(0f, -0.15f, 0f), 0.055f, 0.32f, 1));
            AddHitbox(Hitbox.CreateCapsule(_leftHip, owner, BodyPart.Leg, new Vector3(0f, -0.2f, 0f), 0.09f, 0.48f, 1));
            AddHitbox(Hitbox.CreateCapsule(_rightHip, owner, BodyPart.Leg, new Vector3(0f, -0.2f, 0f), 0.09f, 0.48f, 1));
            AddHitbox(Hitbox.CreateCapsule(_leftKnee, owner, BodyPart.Leg, new Vector3(0f, -0.22f, 0.02f), 0.075f, 0.5f, 1));
            AddHitbox(Hitbox.CreateCapsule(_rightKnee, owner, BodyPart.Leg, new Vector3(0f, -0.22f, 0.02f), 0.075f, 0.5f, 1));

            // Kemiklerle hareket eden tetikleyiciler için kinematik gövde (PhysX statik collider taşıma maliyetini önler).
            if (!TryGetComponent<Rigidbody>(out var body))
                body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }

        private void AddHitbox(Hitbox hitbox)
        {
            if (hitbox != null)
                _hitboxes.Add(hitbox);
        }

        private void SetHitboxesEnabled(bool enabled)
        {
            for (var i = 0; i < _hitboxes.Count; i++)
            {
                var hb = _hitboxes[i];
                if (hb == null)
                    continue;

                var c = hb.Collider;
                if (c != null && c.enabled != enabled)
                    c.enabled = enabled;
            }
        }

        private void RepairHitboxLayers()
        {
            for (var i = 0; i < _hitboxes.Count; i++)
            {
                var hb = _hitboxes[i];
                if (hb != null && hb.gameObject.layer != GameLayers.Hitbox)
                    hb.gameObject.layer = GameLayers.Hitbox;
            }
        }

        // ------------------------------------------------------------------ Ekipman

        /// <summary>Kask / yelek / çanta görselleri (0 = yok, 1-3 seviye). Değer değişmediyse hiçbir şey yapmaz.</summary>
        public void SetEquipment(int helmetLevel, int vestLevel, int backpackLevel)
        {
            if (!_built)
                return;

            helmetLevel = Mathf.Clamp(helmetLevel, 0, 3);
            vestLevel = Mathf.Clamp(vestLevel, 0, 3);
            backpackLevel = Mathf.Clamp(backpackLevel, 0, 3);

            if (vestLevel != _vestLevel)
            {
                _vestLevel = vestLevel;
                for (var i = 0; i < _vests.Length; i++)
                {
                    if (i == vestLevel)
                        EnsureVest(i).SetActive(true);
                    else
                        _vests[i]?.SetActive(false);
                }

                var front = vestLevel == 0 ? 0.118f : vestLevel == 1 ? 0.128f : 0.152f;
                var back = vestLevel == 0 ? -0.11f : vestLevel == 1 ? -0.125f : -0.15f;
                _rankRoot.localPosition = new Vector3(0f, vestLevel >= 2 ? 0.215f : 0.2f, front);
                _backpackRoot.localPosition = new Vector3(0f, 0f, back);
            }

            if (helmetLevel != _helmetLevel)
            {
                _helmetLevel = helmetLevel;
                ApplyHeadgear();
            }

            if (backpackLevel != _backpackLevel)
            {
                _backpackLevel = backpackLevel;
                for (var i = 1; i < _backpacks.Length; i++)
                {
                    if (i == backpackLevel)
                        EnsureBackpack(i).SetActive(true);
                    else
                        _backpacks[i]?.SetActive(false);
                }
            }
        }

        private void ApplyHeadgear()
        {
            var beret = _look.Beret && (_showBeretOverHelmet || _helmetLevel <= 0);
            var show = beret ? -1 : _helmetLevel;
            for (var i = 0; i < _helmets.Length; i++)
            {
                if (i == show)
                    EnsureHelmet(i).SetActive(true);
                else
                    _helmets[i]?.SetActive(false);
            }

            if (beret)
            {
                if (_beret == null)
                    _beret = BuildBeret();
                _beret.SetActive(true);
            }
            else
            {
                _beret?.SetActive(false);
            }
        }

        private Variant EnsureVest(int level)
        {
            if (_vests[level] != null)
                return _vests[level];

            var v = new Variant();
            var m = _mat;
            switch (level)
            {
                case 0:
                    // Yelek yok: hafif askı kayışları.
                    v.Objects.Add(Part("Suspender", Chest, CharacterMeshes.Box("suspenderL", Vector3.zero, new Vector3(0.035f, 0.27f, 0.235f)), m.GearDark,
                        new Vector3(-0.1f, 0.12f, 0f), false));
                    v.Objects.Add(Part("Suspender", Chest, CharacterMeshes.Box("suspenderL", Vector3.zero, new Vector3(0.035f, 0.27f, 0.235f)), m.GearDark,
                        new Vector3(0.1f, 0.12f, 0f), false));
                    break;

                case 1:
                    // Yumuşak yelek.
                    v.Objects.Add(Part("Vest", Chest, CharacterMeshes.Lathe("vest1L", new[] { new CharacterMeshes.Ring(0f, 0.182f, 0.115f), new CharacterMeshes.Ring(0.1f, 0.197f, 0.124f), new CharacterMeshes.Ring(0.17f, 0.212f, 0.124f), new CharacterMeshes.Ring(0.22f, 0.2f, 0.108f), new CharacterMeshes.Ring(0.245f, 0.165f, 0.092f) }, 10, false, false),
                        m.Gear, Vector3.zero));
                    v.Objects.Add(Part("VestLow", _spine, CharacterMeshes.Lathe("vest1LowL", new[] { new CharacterMeshes.Ring(0.07f, 0.165f, 0.115f), new CharacterMeshes.Ring(0.21f, 0.18f, 0.12f) }, 10, false, false),
                        m.Gear, Vector3.zero));
                    AddMagPouches(v, 0.142f, 2);
                    break;

                default:
                    // Plaka taşıyıcı (2) / ağır plaka taşıyıcı (3).
                    var heavy = level == 3;
                    v.Objects.Add(Part("Cummerbund", Chest, CharacterMeshes.Lathe("cummerbundL", new[] { new CharacterMeshes.Ring(0f, 0.186f, 0.118f), new CharacterMeshes.Ring(0.13f, 0.2f, 0.123f) }, 10, false, false),
                        m.Gear, Vector3.zero));
                    v.Objects.Add(Part("CummerbundLow", _spine, CharacterMeshes.Lathe("cummerbundLowL", new[] { new CharacterMeshes.Ring(0.1f, 0.172f, 0.116f), new CharacterMeshes.Ring(0.21f, 0.185f, 0.12f) }, 10, false, false),
                        m.Gear, Vector3.zero));
                    v.Objects.Add(Part("FrontPlate", Chest, CharacterMeshes.CarrierPlate(heavy ? "plateF3SculptV2" : "plateF2SculptV2",
                        heavy ? new Vector3(0.33f, 0.33f, 0.06f) : new Vector3(0.3f, 0.3f, 0.05f)), m.Gear, new Vector3(0f, 0.13f, 0.125f)));
                    v.Objects.Add(Part("BackPlate", Chest, CharacterMeshes.CarrierPlate(heavy ? "plateB3SculptV2" : "plateB2SculptV2",
                        heavy ? new Vector3(0.33f, 0.34f, 0.06f) : new Vector3(0.3f, 0.32f, 0.05f)), m.Gear, new Vector3(0f, 0.13f, -0.125f)));
                    v.Objects.Add(Part("StrapL", Chest, CharacterMeshes.RoundedBox("vestStrapSculpt", Vector3.zero, new Vector3(0.065f, 0.025f, 0.25f)), m.Gear,
                        new Vector3(-0.115f, 0.262f, 0f), false));
                    v.Objects.Add(Part("StrapR", Chest, CharacterMeshes.RoundedBox("vestStrapSculpt", Vector3.zero, new Vector3(0.065f, 0.025f, 0.25f)), m.Gear,
                        new Vector3(0.115f, 0.262f, 0f), false));
                    AddMagPouches(v, heavy ? 0.172f : 0.166f, 3);
                    AddCarrierPouches(v);
                    AddCarrierWebbing(v, heavy);
                    v.Objects.Add(Part("RadioPouch", Chest, CharacterMeshes.RoundedBox("radioPouchRounded", Vector3.zero, new Vector3(0.05f, 0.12f, 0.07f)), m.GearDark,
                        new Vector3(-0.215f, 0.08f, -0.03f), false));
                    v.Objects.Add(Part("Antenna", Chest, CharacterMeshes.Cylinder("antenna", 0f, 0.3f, 0.005f, 0.003f, 4, false), m.Gloves,
                        new Vector3(-0.215f, 0.14f, -0.05f), false));

                    if (heavy)
                    {
                        v.Objects.Add(Part("Collar", Chest, CharacterMeshes.Lathe("vestCollarL", new[] { new CharacterMeshes.Ring(0.24f, 0.15f, 0.125f), new CharacterMeshes.Ring(0.31f, 0.1f, 0.095f) }, 10, false, false),
                            m.Gear, Vector3.zero));
                        v.Objects.Add(Part("SidePlateL", Chest, CharacterMeshes.Box("sidePlate", Vector3.zero, new Vector3(0.035f, 0.17f, 0.16f)), m.Gear,
                            new Vector3(-0.205f, 0.09f, 0f), false));
                        v.Objects.Add(Part("SidePlateR", Chest, CharacterMeshes.Box("sidePlate", Vector3.zero, new Vector3(0.035f, 0.17f, 0.16f)), m.Gear,
                            new Vector3(0.205f, 0.09f, 0f), false));
                        v.Objects.Add(Part("Groin", _body, CharacterMeshes.Box("groinPlate", Vector3.zero, new Vector3(0.17f, 0.15f, 0.03f)), m.Gear,
                            new Vector3(0f, -0.07f, 0.115f), false));
                        v.Objects.Add(Part("PauldronL", _leftShoulder, CharacterMeshes.Frustum("pauldron", -0.11f, 0.035f, new Vector2(0.13f, 0.128f),
                            new Vector2(0.142f, 0.14f)), m.Gear, Vector3.zero));
                        v.Objects.Add(Part("PauldronR", _rightShoulder, CharacterMeshes.Frustum("pauldron", -0.11f, 0.035f, new Vector2(0.13f, 0.128f),
                            new Vector2(0.142f, 0.14f)), m.Gear, Vector3.zero));
                    }

                    break;
            }

            v.SetActive(false);
            _vests[level] = v;
            return v;
        }

        private void AddMagPouches(Variant v, float frontZ, int count)
        {
            var mesh = CharacterMeshes.RoundedBox("magPouchRounded", Vector3.zero, new Vector3(0.07f, 0.11f, 0.04f));
            var start = -(count - 1) * 0.04f;
            for (var i = 0; i < count; i++)
            {
                v.Objects.Add(Part("MagPouch", Chest, mesh, _mat.GearDark, new Vector3(start + i * 0.08f, 0.065f, frontZ), false));
                v.Objects.Add(Part("MagFlap", Chest, CharacterMeshes.Box("magFlap", Vector3.zero, new Vector3(0.073f, 0.022f, 0.044f)), _mat.Gear,
                    new Vector3(start + i * 0.08f, 0.116f, frontZ + 0.002f), false));
            }
        }

        private Variant EnsureHelmet(int level)
        {
            if (_helmets[level] != null)
                return _helmets[level];

            var v = new Variant();
            var m = _mat;
            if (level == 0)
            {
                // Kep (kamuflaj).
                v.Objects.Add(Part("Cap", Head, CharacterMeshes.Ellipsoid("cap2", new Vector3(0f, 0.125f, -0.006f), new Vector3(0.088f, 0.082f, 0.108f), 10, 3, 0f, 90f),
                    m.Camo, Vector3.zero));
                v.Objects.Add(Part("CapBrim", Head, CharacterMeshes.Box("capBrim", Vector3.zero, new Vector3(0.13f, 0.012f, 0.06f)), m.Camo,
                    new Vector3(0f, 0.13f, 0.1f), Quaternion.Euler(-8f, 0f, 0f), false));
            }
            else
            {
                // TSK kaskı: 1 düz, 2 kamuflaj kılıflı + çene kayışı, 3 + gece görüş bağlantısı, raylar ve kulaklık.
                var shell = CharacterMeshes.Ellipsoid("helmet2", new Vector3(0f, 0.115f, -0.01f), new Vector3(0.1f, 0.105f, 0.125f), 20, 8, 0f, 90f, 0.03f);
                v.Objects.Add(Part("Helmet", Head, shell, level == 1 ? m.Helmet : m.Camo, Vector3.zero));
                // Kılıf kenar dudağı: kabuğun alt kenarını saran dışa taşkın bez şerit — kask kılıflı kompozit okunur.
                v.Objects.Add(Part("HelmetRim", Head, CharacterMeshes.Lathe("helmetRimV1", new[]
                {
                    new CharacterMeshes.Ring(0.102f, 0.106f, 0.131f), new CharacterMeshes.Ring(0.112f, 0.11f, 0.1355f),
                    new CharacterMeshes.Ring(0.126f, 0.1035f, 0.1285f)
                }, 14, false, false), level == 1 ? m.Helmet : m.Camo, new Vector3(0f, 0f, -0.01f), Quaternion.identity, false));
                if (level >= 2)
                {
                    v.Objects.Add(Part("HelmetBand", Head, CharacterMeshes.Cylinder("helmetBand2", 0.112f, 0.14f, 0.107f, 0.101f, 12, false, 1.2f),
                        m.GearDark, new Vector3(0f, 0f, -0.01f), false));
                    // Çene kayışı: kaskın altından yanaklara inip çene altında birleşir.
                    v.Objects.Add(Part("ChinStrapL", Head, CharacterMeshes.Box("chinStrap2", Vector3.zero, new Vector3(0.006f, 0.119f, 0.012f)), m.Gloves,
                        new Vector3(-0.065f, 0.055f, 0.033f), Quaternion.Euler(-16f, 0f, -24f), false));
                    v.Objects.Add(Part("ChinStrapR", Head, CharacterMeshes.Box("chinStrap2", Vector3.zero, new Vector3(0.006f, 0.119f, 0.012f)), m.Gloves,
                        new Vector3(0.065f, 0.055f, 0.033f), Quaternion.Euler(-16f, 0f, 24f), false));
                    v.Objects.Add(Part("ChinCup", Head, CharacterMeshes.Ellipsoid("chinCupSculpt", Vector3.zero, new Vector3(0.036f, 0.009f, 0.024f), 16, 6), m.Gloves,
                        new Vector3(0f, -0.022f, 0.04f), false));
                }

                if (level >= 3)
                {
                    v.Objects.Add(Part("NvgMount", Head, CharacterMeshes.Box("nvgMount", Vector3.zero, new Vector3(0.05f, 0.045f, 0.03f)), m.Metal,
                        new Vector3(0f, 0.17f, 0.108f), Quaternion.Euler(-20f, 0f, 0f), false));
                    v.Objects.Add(Part("RailL", Head, CharacterMeshes.Box("helmetRail", Vector3.zero, new Vector3(0.012f, 0.025f, 0.12f)), m.Metal,
                        new Vector3(-0.098f, 0.16f, -0.01f), false));
                    v.Objects.Add(Part("RailR", Head, CharacterMeshes.Box("helmetRail", Vector3.zero, new Vector3(0.012f, 0.025f, 0.12f)), m.Metal,
                        new Vector3(0.098f, 0.16f, -0.01f), false));
                    AddNvg(v);
                    var cup = CharacterMeshes.Cylinder("earCup", -0.022f, 0.022f, 0.04f, 0.038f, 8, true);
                    v.Objects.Add(Part("EarCupL", Head, cup, m.GearDark, new Vector3(-0.088f, 0.085f, -0.002f), Quaternion.Euler(0f, 0f, 90f), false));
                    v.Objects.Add(Part("EarCupR", Head, cup, m.GearDark, new Vector3(0.088f, 0.085f, -0.002f), Quaternion.Euler(0f, 0f, -90f), false));
                }
            }

            v.SetActive(false);
            _helmets[level] = v;
            return v;
        }

        private Variant BuildBeret()
        {
            var v = new Variant();
            var m = _mat;
            // Bordo bere: sola yatık, siyah kenar bandı, sol önde siyah yuvarlak arma (ASKER_REFERANSI).
            v.Objects.Add(Part("Beret", Head, CharacterMeshes.Ellipsoid("beret2", Vector3.zero, new Vector3(0.094f, 0.045f, 0.108f), 16, 6), m.Beret,
                new Vector3(-0.015f, 0.188f, -0.006f), Quaternion.Euler(0f, 0f, 12f)));
            v.Objects.Add(Part("BeretBand", Head, CharacterMeshes.Cylinder("beretBand2", 0.13f, 0.155f, 0.084f, 0.082f, 10, false, 1.19f), BlackTrim,
                new Vector3(0f, 0f, -0.004f), false));
            AddBeretBadge(v);
            v.SetActive(false);
            return v;
        }

        private Variant EnsureBackpack(int level)
        {
            if (_backpacks[level] != null)
                return _backpacks[level];

            var v = new Variant();
            var m = _mat;
            var root = _backpackRoot;
            switch (level)
            {
                case 1:
                    // Hücum çantası: kubbeli tepe, %20 inceltilmiş derinlik (0.13→0.105), iki yan cep.
                    v.Objects.Add(Part("Pack", root, CharacterMeshes.RoundedBox("pack1RoundedV2", new Vector3(0f, 0.115f, -0.0525f), new Vector3(0.27f, 0.27f, 0.105f)), m.Gear,
                        Vector3.zero));
                    v.Objects.Add(Part("PackDome", root, CharacterMeshes.Ellipsoid("pack1DomeV2", Vector3.zero, new Vector3(0.132f, 0.05f, 0.05f), 10, 3, 0f, 90f), m.Gear,
                        new Vector3(0f, 0.245f, -0.0525f)));
                    v.Objects.Add(Part("PackSideL", root, CharacterMeshes.RoundedBox("packMiniPouch", Vector3.zero, new Vector3(0.045f, 0.12f, 0.08f)), m.GearDark,
                        new Vector3(-0.155f, 0.08f, -0.0525f), false));
                    v.Objects.Add(Part("PackSideR", root, CharacterMeshes.RoundedBox("packMiniPouch", Vector3.zero, new Vector3(0.045f, 0.12f, 0.08f)), m.GearDark,
                        new Vector3(0.155f, 0.08f, -0.0525f), false));
                    break;

                case 2:
                    // Sırt çantası: kubbeli tepe + üstte yatay yatak rulosu, derinlik 0.18→0.145, yan cepler.
                    v.Objects.Add(Part("Pack", root, CharacterMeshes.RoundedBox("pack2RoundedV2", new Vector3(0f, 0.1f, -0.0725f), new Vector3(0.32f, 0.36f, 0.145f)), m.Gear,
                        Vector3.zero));
                    v.Objects.Add(Part("PackDome", root, CharacterMeshes.Ellipsoid("pack2DomeV2", Vector3.zero, new Vector3(0.157f, 0.06f, 0.0705f), 12, 3, 0f, 90f), m.Gear,
                        new Vector3(0f, 0.272f, -0.0725f)));
                    v.Objects.Add(Part("Bedroll", root, CharacterMeshes.Cylinder("bedroll2", -0.17f, 0.17f, 0.055f, 0.055f, 10, true), m.Camo,
                        new Vector3(0f, 0.345f, -0.075f), Quaternion.Euler(0f, 0f, 90f)));
                    v.Objects.Add(Part("PackSideL", root, CharacterMeshes.RoundedBox("packSideRounded", Vector3.zero, new Vector3(0.05f, 0.2f, 0.12f)), m.GearDark,
                        new Vector3(-0.182f, 0.05f, -0.0725f), false));
                    v.Objects.Add(Part("PackSideR", root, CharacterMeshes.RoundedBox("packSideRounded", Vector3.zero, new Vector3(0.05f, 0.2f, 0.12f)), m.GearDark,
                        new Vector3(0.182f, 0.05f, -0.0725f), false));
                    break;

                default:
                    // Büyük sırt çantası: kubbeli tepe + yatak rulosu, derinlik 0.22→0.175, yumuşak kenarlı yan cepler.
                    v.Objects.Add(Part("Pack", root, CharacterMeshes.RoundedBox("pack3RoundedV2", new Vector3(0f, 0.12f, -0.0875f), new Vector3(0.36f, 0.48f, 0.175f)), m.Gear,
                        Vector3.zero));
                    v.Objects.Add(Part("PackDome", root, CharacterMeshes.Ellipsoid("pack3DomeV2", Vector3.zero, new Vector3(0.177f, 0.07f, 0.0855f), 12, 3, 0f, 90f), m.Gear,
                        new Vector3(0f, 0.352f, -0.0875f)));
                    v.Objects.Add(Part("Bedroll", root, CharacterMeshes.Cylinder("bedroll3", -0.19f, 0.19f, 0.062f, 0.062f, 10, true), m.Camo,
                        new Vector3(0f, 0.43f, -0.09f), Quaternion.Euler(0f, 0f, 90f)));
                    v.Objects.Add(Part("PackSideL", root, CharacterMeshes.RoundedBox("packSide3Rounded", Vector3.zero, new Vector3(0.06f, 0.26f, 0.14f)), m.GearDark,
                        new Vector3(-0.2f, 0.05f, -0.0875f), false));
                    v.Objects.Add(Part("PackSideR", root, CharacterMeshes.RoundedBox("packSide3Rounded", Vector3.zero, new Vector3(0.06f, 0.26f, 0.14f)), m.GearDark,
                        new Vector3(0.2f, 0.05f, -0.0875f), false));
                    break;
            }

            AddPackStraps(v);
            AddBackpackDetail(v, level);
            v.SetActive(false);
            _backpacks[level] = v;
            return v;
        }

        /// <summary>Çanta omuz askıları: omuz üzerinden geçen bant + önde göğse inen uç (çanta varyantıyla birlikte açılır/kapanır).</summary>
        private void AddPackStraps(Variant v)
        {
            var over = CharacterMeshes.Box("packStrapOver", Vector3.zero, new Vector3(0.05f, 0.014f, 0.24f));
            var front = CharacterMeshes.Box("packStrapFront", Vector3.zero, new Vector3(0.048f, 0.17f, 0.014f));
            for (var i = 0; i < 2; i++)
            {
                var sx = i == 0 ? -1f : 1f;
                v.Objects.Add(Part("PackStrapOver", Chest, over, _mat.GearDark,
                    new Vector3(sx * 0.1f, 0.277f, -0.005f), Quaternion.Euler(12f, 0f, sx * 4f), false));
                v.Objects.Add(Part("PackStrapFront", Chest, front, _mat.GearDark,
                    new Vector3(sx * 0.1f, 0.185f, 0.127f), Quaternion.Euler(4f, 0f, 0f), false));
            }
        }

        // ------------------------------------------------------------------ Silah

        /// <summary>
        /// Silahı ele alır (WeaponModelFactory ile kurulur, silah başına önbelleklenir). null → silahsız (kollar serbest).
        /// Tüfek sınıfı silahlar sağ el kabzada, sol el el kundağında; tabanca iki elle önde tutulur.
        /// </summary>
        public void HoldWeapon(WeaponDefinitionData weapon)
        {
            if (!_built)
                return;

            HeldWeaponDefinition = weapon;
            if (weapon == null || weapon.Category == WeaponCategory.None || weapon.Category == WeaponCategory.Melee)
            {
                SetCurrentWeapon(null);
                return;
            }

            var key = string.IsNullOrEmpty(weapon.WeaponId) ? (weapon.DisplayName ?? "silah") : weapon.WeaponId;
            HeldWeapon held = null;
            for (var i = 0; i < _weapons.Count; i++)
            {
                if (_weapons[i].Key == key && _weapons[i].Root != null)
                {
                    held = _weapons[i];
                    break;
                }
            }

            if (held != null && held == _current)
            {
                held.LastUsed = Time.time;
                return;
            }

            if (held == null)
            {
                held = CreateWeaponVisual(weapon, key);
                if (held == null)
                {
                    SetCurrentWeapon(null);
                    return;
                }
            }

            SetCurrentWeapon(held);
        }

        /// <summary>Sahibin envanterindeki etkin silah değiştiyse onu ele al (ölüyken yapılmaz — envanter yere düşer).</summary>
        private void SyncWeaponFromOwner()
        {
            if (!AutoSyncWeapon || _owner == null || _dead || _ownerWeaponFailed)
                return;

            try
            {
                if (!_owner.IsAlive)
                    return;

                var inventory = _owner.Inventory;
                var active = inventory != null ? inventory.ActiveWeapon : null;
                var definition = active != null ? active.Definition : null;
                if (ReferenceEquals(definition, _lastOwnerWeapon))
                    return;

                _lastOwnerWeapon = definition;
                HoldWeapon(definition);
            }
            catch (Exception e)
            {
                _ownerWeaponFailed = true;
                Debug.LogWarning("[SoldierModel] Envanterden etkin silah okunamadı: " + e.Message);
            }
        }

        private HeldWeapon CreateWeaponVisual(WeaponDefinitionData weapon, string key)
        {
            TrimWeaponCache();

            var kind = weapon.Category == WeaponCategory.Pistol ? HoldKind.Pistol : HoldKind.Rifle;
            GameObject root = null;
            Transform muzzle = null;
            try
            {
                root = WeaponModelFactory.Build(weapon, WeaponSocket, _visualLayer, false, out muzzle);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SoldierModel] Silah modeli kurulamadı (" + key + "), basit model kullanılıyor: " + e.Message);
                if (root != null)
                    SafeDestroy(root);
                root = null;
            }

            if (root != null && root.GetComponentInChildren<Renderer>(true) == null)
            {
                // Görseli olmayan model (fabrika henüz hazır değil) — basit modele düş.
                SafeDestroy(root);
                root = null;
            }

            var fromFactory = root != null;
            if (root == null)
                root = BuildFallbackWeapon(kind, out muzzle);

            // Model uzayı sözleşmesi: orijin kabzanın üstü, +Z namlu → yuvaya sıfır konum/dönüşle oturur.
            var t = root.transform;
            if (t.parent != WeaponSocket)
                t.SetParent(WeaponSocket, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;

            // Görsel silahın collider'ı olmamalı (mermiler ve hareket etkilenmesin). Destroy kare sonunda işlediği
            // için önce hemen kapatılır.
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
                SafeDestroy(colliders[i]);
            }

            GameLayers.SetLayerRecursively(root, _visualLayer);
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                renderers[i].motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                RegisterRenderer(renderers[i]);
            }

            var held = new HeldWeapon
            {
                Key = key,
                Root = root,
                Muzzle = muzzle,
                Kind = kind,
                LeftHandReach = LeftHandReachFor(weapon.Category),
                Profile = WeaponHoldProfile.ForCategory(weapon.Category)
            };

            if (fromFactory)
            {
                // Fabrikanın el bileği bağlantıları varsa eller tam kabzaya / el kundağına oturur.
                var left = FindDescendant(t, LeftHandAnchorName);
                if (left != null)
                {
                    held.HasLeftGrip = true;
                    held.LeftGrip = ClampGrip(t.InverseTransformPoint(left.position));
                }

                var right = FindDescendant(t, RightHandAnchorName);
                if (right != null)
                {
                    held.HasRightGrip = true;
                    held.RightGrip = ClampGrip(t.InverseTransformPoint(right.position));
                }
            }

            root.SetActive(false);
            _weapons.Add(held);
            return held;
        }

        /// <summary>Hatalı bağlantıların kolları koparmaması için makul sınırlar (m).</summary>
        private static Vector3 ClampGrip(Vector3 p)
        {
            return new Vector3(Mathf.Clamp(p.x, -0.15f, 0.15f), Mathf.Clamp(p.y, -0.2f, 0.15f), Mathf.Clamp(p.z, -0.25f, 0.7f));
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null)
                return null;

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name)
                    return child;

                var found = FindDescendant(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null)
                return;

            if (UnityEngine.Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }

        private static float LeftHandReachFor(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.Smg: return 0.17f;
                case WeaponCategory.Shotgun: return 0.22f;
                case WeaponCategory.Sniper:
                case WeaponCategory.Lmg:
                case WeaponCategory.Dmr: return 0.24f;
                default: return 0.21f;
            }
        }

        /// <summary>Fabrika yoksa / hata verirse: basit düşük poligonlu tüfek/tabanca.</summary>
        private GameObject BuildFallbackWeapon(HoldKind kind, out Transform muzzle)
        {
            var root = new GameObject("FallbackWeapon");
            root.layer = _visualLayer;
            root.transform.SetParent(WeaponSocket, false);
            var metal = _mat.Metal;
            var t = root.transform;
            if (kind == HoldKind.Pistol)
            {
                Part("Slide", t, CharacterMeshes.Box("fbPistolSlide", Vector3.zero, new Vector3(0.03f, 0.035f, 0.19f)), metal, new Vector3(0f, 0.045f, 0.05f), false);
                Part("Grip", t, CharacterMeshes.Box("fbPistolGrip", Vector3.zero, new Vector3(0.028f, 0.1f, 0.04f)), metal, new Vector3(0f, -0.01f, 0f),
                    Quaternion.Euler(-12f, 0f, 0f), false);
                muzzle = Bone("Muzzle", t, new Vector3(0f, 0.045f, 0.15f));
            }
            else
            {
                Part("Receiver", t, CharacterMeshes.Box("fbRifleReceiver", Vector3.zero, new Vector3(0.05f, 0.08f, 0.38f)), metal, new Vector3(0f, 0.05f, 0.08f));
                Part("Barrel", t, CharacterMeshes.Box("fbRifleBarrel", Vector3.zero, new Vector3(0.022f, 0.022f, 0.36f)), metal, new Vector3(0f, 0.06f, 0.44f), false);
                Part("Stock", t, CharacterMeshes.Box("fbRifleStock", Vector3.zero, new Vector3(0.045f, 0.1f, 0.24f)), metal, new Vector3(0f, 0.03f, -0.2f), false);
                Part("Magazine", t, CharacterMeshes.Box("fbRifleMag", Vector3.zero, new Vector3(0.03f, 0.14f, 0.06f)), metal, new Vector3(0f, -0.04f, 0.12f),
                    Quaternion.Euler(12f, 0f, 0f), false);
                Part("Grip", t, CharacterMeshes.Box("fbRifleGrip", Vector3.zero, new Vector3(0.03f, 0.09f, 0.04f)), metal, new Vector3(0f, -0.01f, 0f),
                    Quaternion.Euler(-15f, 0f, 0f), false);
                muzzle = Bone("Muzzle", t, new Vector3(0f, 0.06f, 0.62f));
            }

            return root;
        }

        private void TrimWeaponCache()
        {
            while (_weapons.Count >= MaxCachedWeapons)
            {
                var oldest = -1;
                var oldestTime = float.MaxValue;
                for (var i = 0; i < _weapons.Count; i++)
                {
                    if (_weapons[i] == _current)
                        continue;
                    if (_weapons[i].LastUsed < oldestTime)
                    {
                        oldestTime = _weapons[i].LastUsed;
                        oldest = i;
                    }
                }

                if (oldest < 0)
                    return;

                var w = _weapons[oldest];
                _weapons.RemoveAt(oldest);
                if (w.Root != null)
                {
                    RemoveRenderersUnder(w.Root.transform);
                    SafeDestroy(w.Root);
                }
            }
        }

        private void RemoveRenderersUnder(Transform root)
        {
            for (var i = _renderers.Count - 1; i >= 0; i--)
            {
                var r = _renderers[i].Renderer;
                if (r == null || r.transform.IsChildOf(root))
                    _renderers.RemoveAt(i);
            }
        }

        private void SetCurrentWeapon(HeldWeapon held)
        {
            if (_current != null && _current != held && _current.Root != null)
                _current.Root.SetActive(false);

            _current = held;
            _deathWeaponHidden = false;
            if (held == null)
            {
                _hold = HoldKind.None;
                ComputeArmIk(null);
                return;
            }

            held.LastUsed = Time.time;
            _hold = held.Kind;
            _profile = held.Profile;
            _ready.NotifyActivity();
            AttachWeaponToHand(held);
            if (held.Root != null)
                held.Root.SetActive(!_dead);

            LogWeaponAttach(held);
            ComputeArmIk(held);
        }

        /// <summary>Takılan silahın tek satırlık tanı kaydı: boyut/merkez/ebeveyn (görünmez/büzülmüş silah hatalarını yakalar).</summary>
        private static void LogWeaponAttach(HeldWeapon held)
        {
            if (held == null || held.Root == null)
                return;

            var renderers = held.Root.GetComponentsInChildren<Renderer>(true);
            var bounds = new Bounds(held.Root.transform.position, Vector3.zero);
            var any = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;
                if (!any)
                {
                    bounds = renderers[i].bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            Debug.Log("[SILAH] " + held.Key + " boyut=" + bounds.size.ToString("F2") + " merkez=" + bounds.center.ToString("F2")
                      + " parent=" + TransformPath(held.Root.transform.parent));
        }

        /// <summary>Hiyerarşi yolu (tanı kaydı için).</summary>
        private static string TransformPath(Transform t)
        {
            if (t == null)
                return "(yok)";
            var path = t.name;
            for (var p = t.parent; p != null; p = p.parent)
                path = p.name + "/" + path;
            return path;
        }

        /// <summary>
        /// Tutuş türüne göre kolların IK pozunu (ArmsPivot uzayında) silah başına bir kez hesaplar. Silah fabrikası el
        /// bağlantıları verdiyse bilekler oraya, yoksa kategoriye göre tahmini noktalara gider. Sol el uzanamıyorsa
        /// el kundağı boyunca geriye kaydırılır. Tüfekte yuva taşıma dönüşü alır (<see cref="RifleCarry"/>): namlu
        /// ileri-aşağı, hafif sola — silah gövde siluetinden her açıdan taşar.
        /// </summary>
        private void ComputeArmIk(HeldWeapon held)
        {
            if (WeaponSocket == null)
                return;

            var kind = held != null ? held.Kind : HoldKind.None;
            var carry = kind == HoldKind.Rifle ? RifleCarry() : Quaternion.identity;
            Vector3 socket;
            Vector3 rightWrist;
            Vector3 leftWrist;
            if (kind == HoldKind.Pistol)
            {
                socket = new Vector3(0.04f, 0.0f, 0.4f);
                rightWrist = socket + (held.HasRightGrip ? held.RightGrip : new Vector3(0f, -0.035f, -0.03f));
                leftWrist = socket + (held.HasLeftGrip ? held.LeftGrip : new Vector3(-0.035f, -0.05f, -0.02f));
            }
            else
            {
                // z 0.24: dipçik gövde içinde kalmasın, kabza göğüs önüne çıksın.
                socket = new Vector3(0.1f, -0.08f, 0.24f);
                var reach = held != null ? held.LeftHandReach : 0.21f;
                rightWrist = socket + carry * (held != null && held.HasRightGrip ? held.RightGrip : new Vector3(0.0f, -0.035f, -0.035f));
                leftWrist = socket + carry * (held != null && held.HasLeftGrip ? held.LeftGrip : new Vector3(-0.03f, -0.005f, Mathf.Max(0.12f, reach)));
            }

            _socketRest = socket;
            WeaponSocket.localPosition = socket;
            WeaponSocket.localRotation = carry;

            var rightShoulder = new Vector3(ShoulderX, 0f, 0f);
            var leftShoulder = new Vector3(-ShoulderX, 0f, 0f);

            // Uzun silahlarda el kundağı omuzdan erişilemeyecek kadar öndeyse eli namlu ekseni boyunca geri çek.
            var barrel = carry * Vector3.forward;
            leftWrist = WeaponHoldMath.ResolveWristReach(socket, barrel, leftWrist, leftShoulder, MaxArmReach, 0.06f);
            rightWrist = WeaponHoldMath.ResolveWristReach(Vector3.zero, Vector3.forward, rightWrist, rightShoulder, MaxArmReach, rightWrist.z > 0f ? 0f : rightWrist.z);
            SolveTwoBone(rightShoulder, rightWrist, rightShoulder + new Vector3(0.3f, -0.4f, -0.2f), UpperArmLength, ForearmLength, -1f,
                out _ikRightShoulder, out _ikRightElbow);
            SolveTwoBone(leftShoulder, leftWrist, leftShoulder + new Vector3(-0.3f, -0.45f, -0.05f), UpperArmLength, ForearmLength, -1f,
                out _ikLeftShoulder, out _ikLeftElbow);

            // Eller silah eksenine hizalanır (sol: avuç el kundağının altında, hafif içe yatık; sağ: kabzada).
            var grip = WeaponHoldMath.GripFrame(barrel, 0f);
            _leftHandAlign = WeaponHoldMath.AlignHand(_ikLeftShoulder * _ikLeftElbow, grip, Quaternion.Euler(-12f, 0f, -14f),
                Quaternion.Euler(-20f, 0f, 0f));
            _rightHandAlign = WeaponHoldMath.AlignHand(_ikRightShoulder * _ikRightElbow, grip, Quaternion.Euler(-30f, 0f, 8f),
                Quaternion.Euler(-35f, 0f, 0f));
        }

        /// <summary>
        /// İki kemikli analitik IK. Kemik ekseni yerel -Y. Dönüşler kök eklemin ebeveyn uzayındadır.
        /// frontSign: kemiğin +Z (ön) yüzü bükülme yönüne göre (+1 bacak: diz öne, -1 kol: dirsek arkaya).
        /// </summary>
        private static void SolveTwoBone(Vector3 root, Vector3 target, Vector3 pole, float l1, float l2, float frontSign,
            out Quaternion upper, out Quaternion lowerLocal)
        {
            var toTarget = target - root;
            var dist = toTarget.magnitude;
            var dir = dist > 1e-5f ? toTarget / dist : Vector3.down;
            dist = Mathf.Clamp(dist, Mathf.Abs(l1 - l2) + 1e-3f, (l1 + l2) * 0.9995f);

            var toPole = pole - root;
            var bend = toPole - Vector3.Dot(toPole, dir) * dir;
            if (bend.sqrMagnitude < 1e-8f)
            {
                bend = Vector3.Cross(dir, Vector3.right);
                if (bend.sqrMagnitude < 1e-8f)
                    bend = Vector3.Cross(dir, Vector3.forward);
            }

            bend.Normalize();
            var cosA = Mathf.Clamp((l1 * l1 + dist * dist - l2 * l2) / (2f * l1 * dist), -1f, 1f);
            var sinA = Mathf.Sqrt(Mathf.Max(0f, 1f - cosA * cosA));
            var elbow = root + (dir * cosA + bend * sinA) * l1;
            var end = root + dir * dist;
            var upperDir = (elbow - root).normalized;
            var lowerDir = end - elbow;
            lowerDir = lowerDir.sqrMagnitude > 1e-10f ? lowerDir.normalized : upperDir;

            // Bükülme düzleminin normali; kemiklerin "ön" ekseni düzlem içinde, kemiğe dik.
            var normal = Vector3.Cross(dir, bend);
            var upperFront = Vector3.Cross(normal, upperDir) * frontSign;
            var lowerFront = Vector3.Cross(normal, lowerDir) * frontSign;
            upper = Quaternion.LookRotation(upperDir, upperFront) * BoneToZ;
            var lower = Quaternion.LookRotation(lowerDir, lowerFront) * BoneToZ;
            lowerLocal = Quaternion.Inverse(upper) * lower;

            // NaN/sonsuz dönüş kemiği (ve altındaki tüm parçaları) uzaya fırlatır: parçalar dağılmış görünür. Güvenli poza düş.
            if (!SoldierRigMath.IsFinite(upper) || !SoldierRigMath.IsFinite(lowerLocal))
            {
                upper = Quaternion.identity;
                lowerLocal = Quaternion.identity;
            }
        }

        // ------------------------------------------------------------------ Girdiler

        /// <summary>Hareket girdisi (dünya hızı, duruş, yerde mi). Her kare ya da hız değişince çağrılmalı.</summary>
        public void SetLocomotion(Vector3 worldVelocity, Stance stance, bool grounded)
        {
            _velocity = worldVelocity;
            _stance = stance;
            _grounded = grounded;
            _lastLocomotionTime = Time.time;
        }

        /// <summary>Nişan açısı (derece). Unity konvansiyonu: pozitif = aşağı, negatif = yukarı. ±75'e sınırlanır.</summary>
        public void SetAimPitch(float pitchDegrees)
        {
            if (float.IsNaN(pitchDegrees) || float.IsInfinity(pitchDegrees))
                return;

            if (pitchDegrees > 180f)
                pitchDegrees -= 360f;
            _aimPitchTarget = Mathf.Clamp(pitchDegrees, -75f, 75f);
        }

        /// <summary>Araç koltuğu oturma pozu (bkz. <see cref="SeatHeight"/>).</summary>
        public void SetSeated(bool seated)
        {
            _seated = seated;
        }

        /// <summary>Şarjör değiştirme jesti: sol el belden şarjör alır, silaha takar (görsel, süre saniye).</summary>
        public void PlayReloadGesture(float durationSeconds)
        {
            if (_dead)
                return;

            _gesture = 1;
            _gestureTime = 0f;
            _gestureDuration = Mathf.Clamp(durationSeconds, 0.3f, 12f);
        }

        /// <summary>Bomba atma jesti: sağ kol geri sallanır, öne fırlatır.</summary>
        public void PlayThrowGesture()
        {
            if (_dead)
                return;

            _gesture = 2;
            _gestureTime = 0f;
            _gestureDuration = 0.8f;
        }

        /// <summary>Atış sekmesi (kollar ve silah yukarı-geri teper).</summary>
        public void PlayFire()
        {
            if (_dead)
                return;

            _recoil = 1f;
            _ready.NotifyActivity();
        }

        /// <summary>Önündeki engele mesafe (m); yakınsa silah gövdeye çekilir. Sonsuz = engel yok.</summary>
        public void SetObstacleDistance(float meters)
        {
            _obstacleDistance = float.IsNaN(meters) ? float.PositiveInfinity : Mathf.Max(0f, meters);
        }

        /// <summary>
        /// Ölüm düşüşü: vuruş kutuları kapanır, gövde 0,6 s içinde isabet yönüne doğru yere yatar (dizler önce çöker).
        /// hitDirection: merminin gidiş yönü (atıcıdan kurbana). Sıfırsa geriye düşer.
        /// </summary>
        public void PlayDeath(Vector3 hitDirection)
        {
            if (!_built || _dead)
                return;

            _dead = true;
            _deathTime = 0f;
            _deathWeaponHidden = false;
            SetHitboxesEnabled(false);
            DriveHumanoid();

            var t = transform;
            if (!_poseStored)
            {
                _restRotation = t.localRotation;
                _restPosition = t.localPosition;
                _poseStored = true;
            }

            _deathStartRotation = t.localRotation;
            _deathStartPosition = t.localPosition;
            _deathFromProne = _prone > 0.5f;
            _deathFromSeat = _seat > 0.5f;

            var parent = t.parent;
            var dir = hitDirection;
            if (parent != null)
                dir = parent.InverseTransformDirection(dir);
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f)
            {
                var back = -(t.localRotation * Vector3.forward);
                dir = new Vector3(back.x, 0f, back.z);
            }

            if (dir.sqrMagnitude < 1e-6f)
                dir = Vector3.back;

            dir.Normalize();
            _fallAxis = Vector3.Cross(Vector3.up, dir);
            if (_fallAxis.sqrMagnitude < 1e-6f)
                _fallAxis = Vector3.right;
            _fallAxis.Normalize();
        }

        /// <summary>Ölüm pozunu geri alır (yeniden doğma / havuz): kök dönüşü, vuruş kutuları ve silah eski haline gelir.</summary>
        public void ResetPose()
        {
            if (!_built)
                return;

            if (_poseStored)
            {
                transform.localRotation = _restRotation;
                transform.localPosition = _restPosition;
                _poseStored = false;
            }

            ExitRagdoll();
            _hit.Clear();
            _dead = false;
            _deathTime = 0f;
            _deathWeaponHidden = false;
            _recoil = 0f;
            _ready.Reset();
            _flinch = 0f;
            _gesture = 0;
            DriveHumanoid();
            SetHitboxesEnabled(true);
            if (_current != null && _current.Root != null)
                _current.Root.SetActive(true);
        }

        /// <summary>Tüm görsel parçaları (silah dahil) gösterir/gizler. Vuruş kutuları etkilenmez.</summary>
        public void SetVisible(bool visible)
        {
            if (_visible == visible)
                return;

            _visible = visible;
            RefreshRenderers();
        }

        /// <summary>Gizliyken yalnızca gölge düşürsün (FPP oyuncunun kendi gölgesi için).</summary>
        public void SetShadowsOnly(bool shadowsOnly)
        {
            if (_shadowsOnly == shadowsOnly)
                return;

            _shadowsOnly = shadowsOnly;
            RefreshRenderers();
        }

        private void RefreshRenderers()
        {
            for (var i = _renderers.Count - 1; i >= 0; i--)
            {
                if (_renderers[i].Renderer == null)
                {
                    _renderers.RemoveAt(i);
                    continue;
                }

                ApplyRendererState(_renderers[i]);
            }
        }

        // ------------------------------------------------------------------ Sahip olayları

        private void OnOwnerDied(Combatant combatant, DamageInfo damage)
        {
            if (!AutoPlayDeath || _dead || this == null)
                return;

            var dir = Vector3.zero;
            if (damage.HasSourcePosition)
            {
                var source = new Vector3(damage.SourcePosition.X, damage.SourcePosition.Y, damage.SourcePosition.Z);
                dir = transform.position - source;
            }
            else if (combatant != null)
            {
                dir = transform.position - combatant.LastDamageSource;
            }

            var weaponId = damage.SourceWeaponId;
            var explosive = RagdollMath.IsExplosive(weaponId);
            var force = RagdollMath.ImpulseNs(weaponId, damage.Amount, damage.BodyPart == BodyPart.Head);
            Die(dir, force, damage.BodyPart, explosive);
        }

        private void OnOwnerDamaged(Combatant combatant, DamageInfo damage)
        {
            if (_dead || this == null)
                return;

            _flinch = Mathf.Clamp01(0.4f + damage.Amount / 40f);
            _flinchSide = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            Stagger(_flinchSide, damage.Amount);

            var hitDir = Vector3.zero;
            if (damage.HasSourcePosition)
                hitDir = transform.position - new Vector3(damage.SourcePosition.X, damage.SourcePosition.Y, damage.SourcePosition.Z);
            else if (combatant != null)
                hitDir = transform.position - combatant.LastDamageSource;
            var blast = RagdollMath.IsExplosive(damage.SourceWeaponId);
            PlayHit(damage.BodyPart, hitDir, blast ? 1f : Mathf.Clamp01(damage.Amount / 55f), blast);
        }

        private bool TrySyncFromOwner(bool force)
        {
            if (_owner == null || _ownerEquipmentFailed || (!AutoSyncEquipment && !force))
                return false;

            try
            {
                var inventory = _owner.Inventory;
                if (inventory == null)
                    return false;

                var helmet = inventory.Helmet;
                var vest = inventory.Vest;
                SetEquipment(helmet != null ? helmet.Level : 0, vest != null ? vest.Level : 0, inventory.BackpackLevel);
                return true;
            }
            catch (Exception e)
            {
                _ownerEquipmentFailed = true;
                Debug.LogWarning("[SoldierModel] Envanterden ekipman okunamadı: " + e.Message);
                return false;
            }
        }

        // ------------------------------------------------------------------ Animasyon

        private void LateUpdate()
        {
            if (!_built)
                return;

            var dt = Time.deltaTime;
            if (dt <= 0f)
                return;
            if (dt > 0.1f)
                dt = 0.1f;

            // Ragdoll (fizik ya da donmuş): kemikleri fizik yönetir, poz sürücüsü çalışmaz (ResetPose ile geri döner).
            if (_ragdollPosed)
                return;

            // Yere yatmış ceset: poz sabittir, güncelleme gerekmez (ResetPose ile yeniden başlar).
            if (_dead && _deathTime > DeathDuration + 0.6f)
                return;

            // Ekranda değilse (hiçbir kamera, gölge dahil, çizmiyorsa) ~10 Hz güncelle; ölüm düşüşü her zaman akıcıdır.
            _pendingDt += dt;
            if (ThrottleWhenOffscreen && !_dead && _visibilityProbe != null && !_visibilityProbe.isVisible &&
                _pendingDt < OffscreenUpdateInterval)
                return;

            dt = Mathf.Min(_pendingDt, 0.1f);
            _pendingDt = 0f;

            _pollTimer -= dt;
            if (_pollTimer <= 0f)
            {
                _pollTimer = EquipmentPollInterval;
                if (_owner != null)
                {
                    if (AutoSyncEquipment)
                        TrySyncFromOwner(false);
                    SetRank(_owner.Rank);
                }

                RepairHitboxLayers();
            }

            if (_owner != null)
                SyncWeaponFromOwner();

            ApplyPose(dt);
            if (!_dead)
                ApplyHitReaction(dt);
            DriveHumanoid();
        }

        private void ApplyPose(float dt)
        {
            if (_dead)
            {
                UpdateDeath(dt);
                return;
            }

            // --- Girdiler (SetLocomotion yoksa Combatant'tan) ---
            var velocity = _velocity;
            var stance = _stance;
            var grounded = _grounded;
            if (_owner != null && Time.time - _lastLocomotionTime > 0.3f)
            {
                velocity = _owner.Velocity;
                stance = _owner.Stance;
                grounded = true;
            }

            var local = transform.InverseTransformDirection(velocity);
            local.y = 0f;
            var speed = local.magnitude;
            var k = dt > 0f ? 1f - Mathf.Exp(-10f * dt) : 1f;
            _speed = Mathf.Lerp(_speed, speed, k);
            if (speed > 0.05f)
                _moveDir = Vector3.Slerp(_moveDir, local / speed, dt > 0f ? 1f - Mathf.Exp(-12f * dt) : 1f);

            _crouch = Mathf.MoveTowards(_crouch, stance == Stance.Crouching ? 1f : 0f, dt * 5f);
            _prone = Mathf.MoveTowards(_prone, stance == Stance.Prone ? 1f : 0f, dt * 2.5f);
            _seat = Mathf.MoveTowards(_seat, _seated ? 1f : 0f, dt * 4f);
            _air = Mathf.MoveTowards(_air, grounded ? 0f : 1f, dt * 6f);
            _aimPitch = Mathf.Lerp(_aimPitch, _aimPitchTarget, dt > 0f ? 1f - Mathf.Exp(-15f * dt) : 1f);
            _recoil = Mathf.MoveTowards(_recoil, 0f, dt * 9f);
            _flinch = Mathf.MoveTowards(_flinch, 0f, dt * 5f);
            if (_gesture != 0)
            {
                _gestureTime += dt;
                if (_gestureTime >= _gestureDuration)
                    _gesture = 0;
            }
            _breath += dt * 1.6f;
            if (_breath > Mathf.PI * 2f)
                _breath -= Mathf.PI * 2f;

            var crouch = Smooth(_crouch) * (1f - _prone);
            var prone = Smooth(_prone);
            var seat = Smooth(_seat);
            var air = _air * (1f - seat);

            // --- Yürüme döngüsü ---
            var moveWeight = Mathf.Clamp01(_speed / 0.8f) * (1f - seat) * (1f - air);
            var run = Mathf.Clamp01((_speed - 3.2f) / 2.3f) * (1f - crouch) * (1f - prone);
            var cycle = Mathf.Clamp(0.9f + _speed * 0.28f, 1f, 2.6f);
            if (prone > 0.5f)
                cycle = 0.8f;
            _phase += dt * Mathf.PI * 2f * _speed / cycle;
            if (_phase > Mathf.PI * 2f)
                _phase -= Mathf.PI * 2f;

            var sin = Mathf.Sin(_phase);
            var cos = Mathf.Cos(_phase);
            var strideHalf = cycle * 0.25f * moveWeight * Mathf.Lerp(1f, 0.7f, crouch);
            var bob = moveWeight * Mathf.Lerp(0.022f, 0.055f, run) * (Mathf.Abs(cos) - 1f) * (1f - prone);

            // --- Kalça ---
            var uprightHip = Mathf.Lerp(StandHipHeight, CrouchHipHeight, crouch) + bob - air * 0.05f;
            var hipY = Mathf.Lerp(uprightHip, ProneHipHeight, prone);
            hipY = Mathf.Lerp(hipY, SeatHeight + 0.12f, seat);
            var hipZ = -0.04f * crouch;
            _body.localPosition = new Vector3(SoldierDetailRules.HipSway(sin, moveWeight, prone, seat), hipY, hipZ);
            var hipRoll = moveWeight * sin * 3f * (1f - prone) + moveWeight * sin * 5f * prone * (1f - seat) - moveWeight * sin * 1.5f * crouch;
            var bodyPitch = 90f * prone;
            _body.localRotation = Quaternion.Euler(bodyPitch, 0f, hipRoll);

            // --- Bacaklar ---
            ApplyLeg(_leftHip, _leftKnee, _leftAnkle, -1f, _phase, uprightHip, strideHalf, moveWeight, run, crouch, prone, seat, air);
            ApplyLeg(_rightHip, _rightKnee, _rightAnkle, 1f, _phase + Mathf.PI, uprightHip, strideHalf, moveWeight, run, crouch, prone, seat, air);

            // --- Gövde, baş, kollar ---
            var pitch = Mathf.Lerp(_aimPitch, Mathf.Clamp(_aimPitch, -25f, 20f), prone);
            var hasWeapon = _hold != HoldKind.None;
            var breathing = Mathf.Sin(_breath) * 0.8f * (1f - moveWeight);
            var holdOut = new HoldOutput { SpineAim = pitch * 0.25f, ChestAim = pitch * 0.25f, HeadPitch = pitch * 0.9f };
            if (hasWeapon)
            {
                _ready.Update(dt, _speed, _profile.IdleToLowReady, _obstacleDistance, _profile.Length, _gesture == 0 && _recoil <= 0f);
                var lag = WeaponHoldMath.ClampLag(_aimSpring.Step(dt, pitch, _profile.SpringHz, _profile.SpringDamping), pitch);
                holdOut = WeaponHoldMath.Evaluate(new HoldInput
                {
                    Pitch = pitch, Run = run, Crouch = crouch, Prone = prone, Seat = seat, MoveWeight = moveWeight,
                    Breathing = breathing, Recoil = _recoil, LowReady = _ready.LowReady, WallPull = _ready.WallPull,
                    StepCos = cos, StepPhase = _phase, SpringLag = lag, Profile = _profile
                });
            }
            else
                _aimSpring.Reset(pitch);

            if (hasWeapon)
                _body.localRotation = Quaternion.Euler(bodyPitch + holdOut.PelvisPitch, 0f, hipRoll);

            var lean = run * 9f + crouch * 12f + _flinch * -7f + breathing * 0.5f;
            var spineX = lean + holdOut.SpineAim - 5f * prone + seat * 5f;
            var chestX = holdOut.ChestAim + crouch * 4f - 15f * prone;
            var spineYaw = -sin * 5f * moveWeight * (1f - prone) * (hasWeapon ? 0.3f : 1f);
            _spine.localRotation = Quaternion.Euler(spineX, spineYaw, _flinch * _flinchSide * 4f);
            Chest.localRotation = Quaternion.Euler(chestX, SoldierDetailRules.ChestCounterTwist(sin, moveWeight, hasWeapon) * (1f - prone), -hipRoll * 0.35f);
            UpdateAntennaMotion(SoldierDetailRules.AntennaTilt(_speed, _phase), Mathf.Sin(_phase) * 2f * moveWeight);

            var upperTotal = bodyPitch + holdOut.PelvisPitch + spineX + chestX;
            var headTotal = holdOut.HeadPitch;
            Head.localRotation = Quaternion.Euler(headTotal - upperTotal - _flinch * 9f, -spineYaw * 0.8f + _flinch * _flinchSide * 12f, _flinch * _flinchSide * 5f);

            if (hasWeapon)
            {
                _armsPivot.localRotation = Quaternion.Euler(holdOut.ArmsPitch - upperTotal, holdOut.ArmsYaw - spineYaw, 0f);
                WeaponSocket.localPosition = _socketRest + holdOut.SocketOffset;
                if (_hold == HoldKind.Rifle)
                    WeaponSocket.localRotation = RifleCarry(1f - prone); // Yüzüstünde namlu yere saplanmasın.
                _leftShoulder.localRotation = _ikLeftShoulder;
                _leftElbow.localRotation = _ikLeftElbow;
                _rightShoulder.localRotation = _ikRightShoulder;
                _rightElbow.localRotation = _ikRightElbow;
                _leftHand.localRotation = _leftHandAlign;
                _rightHand.localRotation = _rightHandAlign;
                ApplyGesture();
            }
            else
            {
                // Silahsız: kollar dikey sarkar, yürürken salınır.
                _armsPivot.localRotation = Quaternion.Euler(-upperTotal * (1f - prone), 0f, 0f);
                var swing = Mathf.Lerp(18f, 42f, run) * moveWeight * (1f - prone);
                var elbow = Mathf.Lerp(14f, 85f, run) + crouch * 20f;
                var leftArm = Quaternion.Euler(sin * swing, 0f, -4f);
                var rightArm = Quaternion.Euler(-sin * swing, 0f, 4f);
                var leftElbow = Quaternion.Euler(-elbow - Mathf.Max(0f, -sin) * 15f * moveWeight, 0f, 0f);
                var rightElbow = Quaternion.Euler(-elbow - Mathf.Max(0f, sin) * 15f * moveWeight, 0f, 0f);

                if (prone > 0.001f)
                {
                    var crawl = Mathf.Sin(_phase) * 25f * moveWeight;
                    leftArm = Quaternion.Slerp(leftArm, Quaternion.Euler(-150f + crawl, 0f, -25f), prone);
                    rightArm = Quaternion.Slerp(rightArm, Quaternion.Euler(-150f - crawl, 0f, 25f), prone);
                    leftElbow = Quaternion.Slerp(leftElbow, Quaternion.Euler(-100f, 0f, 0f), prone);
                    rightElbow = Quaternion.Slerp(rightElbow, Quaternion.Euler(-100f, 0f, 0f), prone);
                }

                if (seat > 0.001f)
                {
                    leftArm = Quaternion.Slerp(leftArm, Quaternion.Euler(-38f, 0f, -4f), seat);
                    rightArm = Quaternion.Slerp(rightArm, Quaternion.Euler(-38f, 0f, 4f), seat);
                    leftElbow = Quaternion.Slerp(leftElbow, Quaternion.Euler(-55f, 0f, 0f), seat);
                    rightElbow = Quaternion.Slerp(rightElbow, Quaternion.Euler(-55f, 0f, 0f), seat);
                }

                if (air > 0.001f)
                {
                    leftArm = Quaternion.Slerp(leftArm, Quaternion.Euler(-30f, 0f, -35f), air);
                    rightArm = Quaternion.Slerp(rightArm, Quaternion.Euler(-30f, 0f, 35f), air);
                }

                _leftShoulder.localRotation = leftArm;
                _rightShoulder.localRotation = rightArm;
                _leftElbow.localRotation = leftElbow;
                _rightElbow.localRotation = rightElbow;
                _leftHand.localRotation = Quaternion.identity;
                _rightHand.localRotation = Quaternion.identity;
                WeaponSocket.localPosition = _socketRest;
            }
        }

        /// <summary>Şarjör/bomba jesti: IK kol pozunun üstüne eklenen ofsetler.</summary>
        private void ApplyGesture()
        {
            if (_gesture == 0 || _gestureDuration <= 0f)
                return;

            var t = Mathf.Clamp01(_gestureTime / _gestureDuration);
            if (_gesture == 1)
            {
                // Çıkar (0.1-0.3), cepten yenisi (0.3-0.5), tak (0.5-0.75), toparlan.
                var reach = Smooth(Mathf.Clamp01((t - 0.08f) / 0.15f)) * (1f - Smooth(Mathf.Clamp01((t - 0.72f) / 0.2f)));
                var dip = Mathf.Sin(Mathf.Clamp01((t - 0.25f) / 0.3f) * Mathf.PI);
                _leftShoulder.localRotation = _leftShoulder.localRotation * Quaternion.Euler(22f * reach + 12f * dip, 0f, 10f * reach);
                _leftElbow.localRotation = _leftElbow.localRotation * Quaternion.Euler(-25f * reach, 0f, 0f);
                _armsPivot.localRotation = _armsPivot.localRotation * Quaternion.Euler(6f * reach, -4f * reach, 8f * reach);
                _spine.localRotation = _spine.localRotation * Quaternion.Euler(3f * reach, 0f, 0f);
            }
            else
            {
                var wind = Smooth(Mathf.Clamp01(t / 0.35f));
                var release = Smooth(Mathf.Clamp01((t - 0.35f) / 0.15f));
                var back = Smooth(Mathf.Clamp01((t - 0.65f) / 0.35f));
                var swing = wind * (1f - release) * 70f - release * (1f - back) * 55f;
                _rightShoulder.localRotation = _rightShoulder.localRotation * Quaternion.Euler(swing, 0f, -12f * wind * (1f - release));
                _rightElbow.localRotation = _rightElbow.localRotation * Quaternion.Euler(-40f * wind * (1f - release), 0f, 0f);
                _spine.localRotation = _spine.localRotation * Quaternion.Euler(0f, 10f * wind * (1f - release) - 8f * release * (1f - back), 0f);
            }
        }

        private void ApplyLeg(Transform hip, Transform knee, Transform ankle, float side, float legPhase, float uprightHip, float strideHalf,
            float moveWeight, float run, float crouch, float prone, float seat, float air)
        {
            // Ayakta / çömelik: ayak bileği hedefi (Hips uzayı) + iki kemikli IK.
            var lsin = Mathf.Sin(legPhase);
            var lcos = Mathf.Cos(legPhase);
            var offset = _moveDir * (strideHalf * lsin);
            var lift = Mathf.Max(0f, lcos) * moveWeight * Mathf.Lerp(0.07f, 0.16f, run);
            var crouchStep = crouch * (side < 0f ? 0.12f : -0.2f);
            var hipJoint = new Vector3(side * HipJointX, HipJointY, 0f);
            var ankleTarget = new Vector3(side * (HipJointX + 0.012f) + offset.x, -uprightHip + AnkleHeight + lift, offset.z + crouchStep + 0.04f * crouch);
            SolveTwoBone(hipJoint, ankleTarget, hipJoint + new Vector3(side * 0.05f, -0.3f, 1f), ThighLength, ShinLength, 1f,
                out var thigh, out var kneeRot);
            var foot = Quaternion.Inverse(thigh * kneeRot) * Quaternion.Euler(-lift * 120f, 0f, 0f);

            if (air > 0.001f)
            {
                thigh = Quaternion.Slerp(thigh, Quaternion.Euler(-28f + side * 6f, 0f, side * 4f), air);
                kneeRot = Quaternion.Slerp(kneeRot, Quaternion.Euler(50f, 0f, 0f), air);
                foot = Quaternion.Slerp(foot, Quaternion.Euler(-15f, 0f, 0f), air);
            }

            if (prone > 0.001f)
            {
                var crawl = Mathf.Max(0f, Mathf.Sin(legPhase)) * 55f * moveWeight;
                var proneThigh = Quaternion.Euler(-crawl * 0.4f, 0f, side * (8f + crawl * 0.5f));
                var proneKnee = Quaternion.Euler(6f + crawl, 0f, 0f);
                var proneFoot = Quaternion.Euler(75f, 0f, 0f);
                thigh = Quaternion.Slerp(thigh, proneThigh, prone);
                kneeRot = Quaternion.Slerp(kneeRot, proneKnee, prone);
                foot = Quaternion.Slerp(foot, proneFoot, prone);
            }

            if (seat > 0.001f)
            {
                thigh = Quaternion.Slerp(thigh, Quaternion.Euler(-88f, side * 4f, side * 3f), seat);
                kneeRot = Quaternion.Slerp(kneeRot, Quaternion.Euler(88f, 0f, 0f), seat);
                foot = Quaternion.Slerp(foot, Quaternion.identity, seat);
            }

            hip.localRotation = thigh;
            knee.localRotation = kneeRot;
            ankle.localRotation = foot;
        }

        private void UpdateDeath(float dt)
        {
            _deathTime += dt;
            var t = _deathTime;

            // Dizler önce çöker, sonra gövde devrilir; kollar gevşer.
            var buckle = Mathf.Clamp01(t / 0.18f);
            var relax = Mathf.Clamp01((t - 0.35f) / 0.4f);
            var crouch = Mathf.Lerp(0f, 0.55f, buckle) * (1f - relax * 0.7f);
            if (_deathFromProne || _deathFromSeat)
                crouch = 0f;

            if (!_deathFromProne && !_deathFromSeat)
            {
                float angle;
                if (t < DeathDuration)
                {
                    var f = Mathf.Clamp01((t - 0.06f) / (DeathDuration - 0.06f));
                    angle = 88f * Mathf.Pow(f, 1.7f);
                }
                else
                {
                    var b = Mathf.Clamp01((t - DeathDuration) / 0.22f);
                    angle = 88f - 6f * Mathf.Sin(b * Mathf.PI) * (1f - 0.4f * b);
                }

                var fall = angle / 88f;
                transform.localRotation = Quaternion.AngleAxis(angle, _fallAxis) * _deathStartRotation;
                transform.localPosition = _deathStartPosition + Vector3.up * (0.13f * fall);

                var hipY = Mathf.Lerp(StandHipHeight, CrouchHipHeight, crouch);
                _body.localPosition = new Vector3(0f, hipY, 0f);
                _body.localRotation = Quaternion.identity;
                var thigh = Quaternion.Euler(-60f * crouch, 0f, 0f);
                var knee = Quaternion.Euler(100f * crouch, 0f, 0f);
                _leftHip.localRotation = thigh * Quaternion.Euler(0f, 0f, -6f * relax);
                _rightHip.localRotation = thigh * Quaternion.Euler(0f, 0f, 8f * relax);
                _leftKnee.localRotation = knee;
                _rightKnee.localRotation = knee;
                _spine.localRotation = Quaternion.Euler(10f * buckle * (1f - relax), 0f, 0f);
                Chest.localRotation = Quaternion.identity;
            }
            else if (_deathFromSeat)
            {
                var slump = Mathf.Clamp01(t / DeathDuration);
                _spine.localRotation = Quaternion.Euler(25f * slump, 0f, 8f * slump);
            }
            else
            {
                var roll = Mathf.Clamp01(t / DeathDuration);
                _spine.localRotation = Quaternion.Euler(-5f, 0f, 12f * roll);
            }

            // Kollar ve baş gevşer.
            var limp = Mathf.Clamp01(t / 0.3f);
            _armsPivot.localRotation = Quaternion.Slerp(_armsPivot.localRotation, Quaternion.identity, limp);
            _leftShoulder.localRotation = Quaternion.Slerp(_leftShoulder.localRotation, Quaternion.Euler(-12f, 0f, -28f), limp);
            _rightShoulder.localRotation = Quaternion.Slerp(_rightShoulder.localRotation, Quaternion.Euler(-8f, 0f, 32f), limp);
            _leftElbow.localRotation = Quaternion.Slerp(_leftElbow.localRotation, Quaternion.Euler(-25f, 0f, 0f), limp);
            _rightElbow.localRotation = Quaternion.Slerp(_rightElbow.localRotation, Quaternion.Euler(-15f, 0f, 0f), limp);
            Head.localRotation = Quaternion.Slerp(Head.localRotation, Quaternion.Euler(-10f, 25f, 10f), limp);

            // Düşüş bitince eldeki silah gizlenir (envanter zaten yere düştü).
            if (!_deathWeaponHidden && t >= DeathDuration)
            {
                _deathWeaponHidden = true;
                if (_current != null && _current.Root != null)
                    _current.Root.SetActive(false);
            }
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private readonly List<Mesh> _combinedMeshes = new List<Mesh>(8);

        /// <summary>BuildBody sonrası statik parçaları kemik+malzeme başına tek mesh'e birleştirir (SoldierMeshCombiner.Enabled ile kapatılır).</summary>
        private void CombineStaticParts()
        {
            try
            {
                if (!SoldierMeshCombiner.Enabled || _renderers.Count <= SoldierMeshCombinerRules.MinPartCount)
                    return;
                var parts = new List<MeshRenderer>(_renderers.Count);
                for (var i = 0; i < _renderers.Count; i++)
                {
                    if (_renderers[i].Renderer is MeshRenderer mr && mr != _skullRenderer && mr != _visibilityProbe)
                        parts.Add(mr);
                }

                if (!SoldierMeshCombiner.TryCombine(parts, _visualLayer, out var res))
                    return;

                var removed = new HashSet<GameObject>(res.Removed);
                for (var i = _renderers.Count - 1; i >= 0; i--)
                {
                    var r = _renderers[i].Renderer;
                    if (r != null && removed.Contains(r.gameObject))
                        _renderers.RemoveAt(i);
                }

                foreach (var go in res.Removed)
                {
                    if (go == null)
                        continue;
                    go.SetActive(false);
                    if (UnityEngine.Application.isPlaying)
                        Destroy(go);
                    else
                        DestroyImmediate(go);
                }

                foreach (var go in res.Created)
                    RegisterRenderer(go.GetComponent<Renderer>());
                _combinedMeshes.AddRange(res.Meshes);
                Debug.Log("[BIRLESTIR] Asker renderer " + res.Before + " -> " + res.After + " (" + res.Created.Count + " birlesik grup)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BIRLESTIR] Birlestirme atlandi: " + e.Message);
            }
        }

        private void OnDestroy()
        {
            for (var i = 0; i < _combinedMeshes.Count; i++)
            {
                if (_combinedMeshes[i] != null)
                    Destroy(_combinedMeshes[i]);
            }

            _combinedMeshes.Clear();
            if (_owner != null)
            {
                _owner.Died -= OnOwnerDied;
                _owner.Damaged -= OnOwnerDamaged;
            }

            _renderers.Clear();
            _weapons.Clear();
            _hitboxes.Clear();
        }
    }
}
