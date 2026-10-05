using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Combat;
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
    public sealed class SoldierModel : MonoBehaviour
    {
        // ------------------------------------------------------------------ Ölçüler (metre)
        private const float StandHipHeight = 0.97f;
        private const float CrouchHipHeight = 0.62f;
        private const float ProneHipHeight = 0.17f;
        private const float HipJointX = 0.095f;
        private const float HipJointY = -0.07f;
        private const float ThighLength = 0.42f;
        private const float ShinLength = 0.40f;
        private const float AnkleHeight = 0.08f;
        private const float ShoulderX = 0.215f;
        private const float UpperArmLength = 0.29f;
        private const float ForearmLength = 0.26f;
        private const float DeathDuration = 0.6f;
        private const float EquipmentPollInterval = 0.5f;
        private const int MaxCachedWeapons = 6;

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
            public float LastUsed;
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
            public Material Gear;
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
        private float _flinch;
        private float _flinchSide;
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

        /// <summary>Silahın bağlandığı nokta (sağ el kabzası, +Z namlu yönü).</summary>
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
                SetRank(owner.Rank);

            ComputeArmIk(HoldKind.None, 0f);
            ApplyPose(0f);
        }

        private static Materials CreateMaterials(SoldierLook look)
        {
            var m = new Materials();
            try
            {
                m.Camo = MaterialLibrary.Camo(look.CamoA, look.CamoB, look.CamoC, look.CamoD, look.CamoSeed, 1f);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SoldierModel] Kamuflaj malzemesi üretilemedi, düz renk kullanılıyor: " + e.Message);
                m.Camo = null;
            }

            if (m.Camo == null)
                m.Camo = MaterialLibrary.Lit(look.CamoA, 0.12f);

            m.Skin = MaterialLibrary.Lit(look.Skin, 0.3f);
            m.Gear = MaterialLibrary.Lit(look.Gear, 0.15f);
            m.GearDark = MaterialLibrary.Lit(look.Gear * 0.6f, 0.15f);
            m.Boots = MaterialLibrary.Get(MaterialId.Boots);
            m.Gloves = MaterialLibrary.Lit(new Color(0.09f, 0.09f, 0.08f), 0.25f);
            m.Armband = ArmbandMaterial(look.Armband);
            m.Metal = MaterialLibrary.Get(MaterialId.GunMetal);
            m.Beret = MaterialLibrary.Get(MaterialId.Beret);
            m.Flag = MaterialLibrary.Get(MaterialId.TurkishFlag);
            m.Hair = MaterialLibrary.Lit(new Color(0.07f, 0.055f, 0.045f), 0.2f);
            m.Gold = MaterialLibrary.Lit(new Color(0.86f, 0.68f, 0.2f), 0.6f, 0.8f);
            m.Silver = MaterialLibrary.Lit(new Color(0.76f, 0.77f, 0.79f), 0.6f, 0.8f);
            m.Red = MaterialLibrary.Lit(new Color(0.75f, 0.08f, 0.08f), 0.3f);
            return m;
        }

        /// <summary>Kütüphanedeki parlayan kolluk renklerinden biriyse onu kullan (uzaktan seçilebilir), yoksa düz Lit.</summary>
        private static Material ArmbandMaterial(Color c)
        {
            if (Near(c, new Color(0.1f, 0.3f, 0.85f)))
                return MaterialLibrary.Get(MaterialId.ArmbandBlue);
            if (Near(c, new Color(0.85f, 0.1f, 0.1f)))
                return MaterialLibrary.Get(MaterialId.ArmbandRed);
            if (Near(c, new Color(0.95f, 0.8f, 0.1f)))
                return MaterialLibrary.Get(MaterialId.ArmbandYellow);
            if (Near(c, new Color(0.2f, 0.75f, 0.2f)))
                return MaterialLibrary.Get(MaterialId.ArmbandGreen);
            return MaterialLibrary.Lit(c, 0.2f);
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
            t.SetParent(parent, false);
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
            t.SetParent(parent, false);
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
            Part("Pelvis", _body, CharacterMeshes.Frustum("pelvis", -0.12f, 0.08f, new Vector2(0.3f, 0.19f), new Vector2(0.33f, 0.21f)), m.Camo,
                Vector3.zero);
            Part("Belt", _body, CharacterMeshes.Box("belt", Vector3.zero, new Vector3(0.345f, 0.055f, 0.225f)), m.GearDark,
                new Vector3(0f, 0.05f, 0f), false);
            Part("Buckle", _body, CharacterMeshes.Box("buckle", Vector3.zero, new Vector3(0.05f, 0.04f, 0.012f)), m.Metal,
                new Vector3(0f, 0.05f, 0.116f), false);
            Part("BeltPouch", _body, CharacterMeshes.Box("beltPouch", Vector3.zero, new Vector3(0.07f, 0.09f, 0.05f)), m.GearDark,
                new Vector3(0.13f, 0.02f, -0.08f), false);

            BuildLeg(_leftHip, _leftKnee, _leftAnkle, -1f);
            BuildLeg(_rightHip, _rightKnee, _rightAnkle, 1f);

            // Gövde
            Part("Abdomen", _spine, CharacterMeshes.Frustum("abdomen", 0f, 0.21f, new Vector2(0.31f, 0.2f), new Vector2(0.34f, 0.21f)), m.Camo,
                Vector3.zero);
            Part("ChestShape", Chest, CharacterMeshes.Frustum("chest", 0f, 0.27f, new Vector2(0.35f, 0.21f), new Vector2(0.43f, 0.22f)), m.Camo,
                Vector3.zero);
            Part("Collar", Chest, CharacterMeshes.Frustum("collar", 0.24f, 0.3f, new Vector2(0.2f, 0.15f), new Vector2(0.15f, 0.12f)), m.Camo,
                Vector3.zero, false);

            // Boyun ve baş
            Part("NeckShape", _neck, CharacterMeshes.Cylinder("neck", -0.02f, 0.09f, 0.055f, 0.05f, 6, false), m.Skin, Vector3.zero);
            Part("Skull", Head, CharacterMeshes.Ellipsoid("head", new Vector3(0f, 0.09f, 0f), new Vector3(0.095f, 0.115f, 0.105f), 8, 6), m.Skin,
                Vector3.zero);
            Part("Hair", Head, CharacterMeshes.Ellipsoid("hair", new Vector3(0f, 0.095f, -0.006f), new Vector3(0.099f, 0.117f, 0.108f), 8, 3, 15f, 90f),
                m.Hair, Vector3.zero, false);
            Part("Nose", Head, CharacterMeshes.Box("nose", Vector3.zero, new Vector3(0.028f, 0.045f, 0.03f)), m.Skin,
                new Vector3(0f, 0.075f, 0.104f), false);
            Part("Brows", Head, CharacterMeshes.Box("brows", Vector3.zero, new Vector3(0.085f, 0.013f, 0.012f)), m.Hair,
                new Vector3(0f, 0.122f, 0.097f), false);
            Part("EyeL", Head, CharacterMeshes.Box("eye", Vector3.zero, new Vector3(0.022f, 0.011f, 0.008f)), m.Hair,
                new Vector3(-0.034f, 0.104f, 0.099f), false);
            Part("EyeR", Head, CharacterMeshes.Box("eye", Vector3.zero, new Vector3(0.022f, 0.011f, 0.008f)), m.Hair,
                new Vector3(0.034f, 0.104f, 0.099f), false);
            Part("EarL", Head, CharacterMeshes.Box("ear", Vector3.zero, new Vector3(0.02f, 0.05f, 0.035f)), m.Skin,
                new Vector3(-0.094f, 0.085f, 0f), false);
            Part("EarR", Head, CharacterMeshes.Box("ear", Vector3.zero, new Vector3(0.02f, 0.05f, 0.035f)), m.Skin,
                new Vector3(0.094f, 0.085f, 0f), false);
            if (_look.Mustache)
            {
                Part("Mustache", Head, CharacterMeshes.Box("mustache", Vector3.zero, new Vector3(0.062f, 0.014f, 0.016f)), m.Hair,
                    new Vector3(0f, 0.051f, 0.1f), false);
            }

            BuildArm(_leftShoulder, _leftElbow, _leftHand, -1f);
            BuildArm(_rightShoulder, _rightElbow, _rightHand, 1f);
        }

        private void BuildLeg(Transform hip, Transform knee, Transform ankle, float side)
        {
            var m = _mat;
            Part("Thigh", hip, CharacterMeshes.Frustum("thigh", -0.43f, 0.03f, new Vector2(0.12f, 0.13f), new Vector2(0.16f, 0.17f)), m.Camo,
                Vector3.zero);
            Part("CargoPocket", hip, CharacterMeshes.Box("cargo", Vector3.zero, new Vector3(0.03f, 0.12f, 0.1f)), m.Camo,
                new Vector3(side * 0.071f, -0.21f, 0f), false);
            Part("Shin", knee, CharacterMeshes.Frustum("shin", -0.36f, 0.02f, new Vector2(0.09f, 0.1f), new Vector2(0.115f, 0.125f)), m.Camo,
                Vector3.zero);
            Part("KneePad", knee, CharacterMeshes.Box("kneepad", Vector3.zero, new Vector3(0.11f, 0.12f, 0.04f)), m.Gear,
                new Vector3(0f, -0.01f, 0.058f), false);
            Part("BootShaft", ankle, CharacterMeshes.Frustum("bootShaft", -0.06f, 0.1f, new Vector2(0.105f, 0.12f), new Vector2(0.112f, 0.126f)),
                m.Boots, Vector3.zero);
            Part("BootFoot", ankle, CharacterMeshes.Box("bootFoot", new Vector3(0f, -0.045f, 0.05f), new Vector3(0.1f, 0.07f, 0.27f)), m.Boots,
                Vector3.zero);
        }

        private void BuildArm(Transform shoulder, Transform elbow, Transform hand, float side)
        {
            var m = _mat;
            Part("ShoulderCap", shoulder, CharacterMeshes.Ellipsoid("shoulderCap", new Vector3(0f, -0.02f, 0f), new Vector3(0.066f, 0.06f, 0.066f), 6, 4),
                m.Camo, Vector3.zero);
            Part("UpperArm", shoulder, CharacterMeshes.Frustum("upperArm", -0.29f, 0f, new Vector2(0.09f, 0.095f), new Vector2(0.11f, 0.11f)), m.Camo,
                Vector3.zero);
            Part("Armband", shoulder, CharacterMeshes.Frustum("armband", -0.165f, -0.1f, new Vector2(0.106f, 0.109f), new Vector2(0.111f, 0.113f)),
                m.Armband, Vector3.zero, false);
            if (side < 0f)
            {
                // Türk bayrağı arması (sol kol dış yüzü).
                Part("FlagPatch", shoulder, CharacterMeshes.Quad("flagPatch", 0.075f, 0.05f), m.Flag, new Vector3(-0.0585f, -0.065f, 0f),
                    Quaternion.Euler(0f, -90f, 0f), false);
            }

            Part("Forearm", elbow, CharacterMeshes.Frustum("forearm", -0.26f, 0.01f, new Vector2(0.075f, 0.07f), new Vector2(0.092f, 0.09f)), m.Camo,
                Vector3.zero);
            Part("Glove", hand, CharacterMeshes.Box("hand", new Vector3(0f, -0.045f, 0.005f), new Vector3(0.05f, 0.1f, 0.085f)), m.Gloves,
                Vector3.zero, false);
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
                    v.Objects.Add(Part("Vest", Chest, CharacterMeshes.Frustum("vest1", 0f, 0.245f, new Vector2(0.37f, 0.245f), new Vector2(0.44f, 0.252f)),
                        m.Gear, Vector3.zero));
                    v.Objects.Add(Part("VestLow", _spine, CharacterMeshes.Frustum("vest1Low", 0.07f, 0.21f, new Vector2(0.335f, 0.235f), new Vector2(0.36f, 0.24f)),
                        m.Gear, Vector3.zero));
                    AddMagPouches(v, 0.142f, 2);
                    break;

                default:
                    // Plaka taşıyıcı (2) / ağır plaka taşıyıcı (3).
                    var heavy = level == 3;
                    v.Objects.Add(Part("Cummerbund", Chest, CharacterMeshes.Frustum("cummerbund", 0f, 0.13f, new Vector2(0.38f, 0.24f), new Vector2(0.4f, 0.245f)),
                        m.Gear, Vector3.zero));
                    v.Objects.Add(Part("CummerbundLow", _spine, CharacterMeshes.Frustum("cummerbundLow", 0.1f, 0.21f, new Vector2(0.345f, 0.235f), new Vector2(0.37f, 0.24f)),
                        m.Gear, Vector3.zero));
                    v.Objects.Add(Part("FrontPlate", Chest, CharacterMeshes.Box(heavy ? "plateF3" : "plateF2", Vector3.zero,
                        heavy ? new Vector3(0.33f, 0.33f, 0.06f) : new Vector3(0.3f, 0.3f, 0.05f)), m.Gear, new Vector3(0f, 0.13f, 0.125f)));
                    v.Objects.Add(Part("BackPlate", Chest, CharacterMeshes.Box(heavy ? "plateB3" : "plateB2", Vector3.zero,
                        heavy ? new Vector3(0.33f, 0.34f, 0.06f) : new Vector3(0.3f, 0.32f, 0.05f)), m.Gear, new Vector3(0f, 0.13f, -0.125f)));
                    v.Objects.Add(Part("StrapL", Chest, CharacterMeshes.Box("vestStrap", Vector3.zero, new Vector3(0.065f, 0.025f, 0.25f)), m.Gear,
                        new Vector3(-0.115f, 0.262f, 0f), false));
                    v.Objects.Add(Part("StrapR", Chest, CharacterMeshes.Box("vestStrap", Vector3.zero, new Vector3(0.065f, 0.025f, 0.25f)), m.Gear,
                        new Vector3(0.115f, 0.262f, 0f), false));
                    AddMagPouches(v, heavy ? 0.172f : 0.166f, 3);
                    v.Objects.Add(Part("RadioPouch", Chest, CharacterMeshes.Box("radioPouch", Vector3.zero, new Vector3(0.05f, 0.12f, 0.07f)), m.GearDark,
                        new Vector3(-0.215f, 0.08f, -0.03f), false));
                    v.Objects.Add(Part("Antenna", Chest, CharacterMeshes.Cylinder("antenna", 0f, 0.3f, 0.005f, 0.003f, 4, false), m.Gloves,
                        new Vector3(-0.215f, 0.14f, -0.05f), false));

                    if (heavy)
                    {
                        v.Objects.Add(Part("Collar", Chest, CharacterMeshes.Frustum("vestCollar", 0.24f, 0.31f, new Vector2(0.3f, 0.26f), new Vector2(0.25f, 0.22f)),
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
            var mesh = CharacterMeshes.Box("magPouch", Vector3.zero, new Vector3(0.07f, 0.11f, 0.04f));
            var start = -(count - 1) * 0.04f;
            for (var i = 0; i < count; i++)
            {
                v.Objects.Add(Part("MagPouch", Chest, mesh, _mat.GearDark, new Vector3(start + i * 0.08f, 0.065f, frontZ), false));
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
                v.Objects.Add(Part("Cap", Head, CharacterMeshes.Ellipsoid("cap", new Vector3(0f, 0.125f, -0.006f), new Vector3(0.106f, 0.09f, 0.115f), 8, 3, 0f, 90f),
                    m.Camo, Vector3.zero));
                v.Objects.Add(Part("CapBrim", Head, CharacterMeshes.Box("capBrim", Vector3.zero, new Vector3(0.15f, 0.012f, 0.07f)), m.Camo,
                    new Vector3(0f, 0.128f, 0.12f), Quaternion.Euler(-8f, 0f, 0f), false));
            }
            else
            {
                // TSK kaskı: 1 düz, 2 kamuflaj kılıflı + çene kayışı, 3 + gece görüş bağlantısı, raylar ve kulaklık.
                var shell = CharacterMeshes.Ellipsoid("helmet", new Vector3(0f, 0.1f, -0.008f), new Vector3(0.122f, 0.126f, 0.13f), 10, 4, 4f, 90f, 0.05f);
                v.Objects.Add(Part("Helmet", Head, shell, level == 1 ? m.Gear : m.Camo, Vector3.zero));
                if (level >= 2)
                {
                    v.Objects.Add(Part("HelmetBand", Head, CharacterMeshes.Cylinder("helmetBand", 0.13f, 0.155f, 0.124f, 0.117f, 10, false, 1.07f),
                        m.GearDark, new Vector3(0f, 0f, -0.008f), false));
                    v.Objects.Add(Part("ChinStrapL", Head, CharacterMeshes.Box("chinStrap", Vector3.zero, new Vector3(0.008f, 0.11f, 0.015f)), m.Gloves,
                        new Vector3(-0.096f, 0.055f, 0.01f), false));
                    v.Objects.Add(Part("ChinStrapR", Head, CharacterMeshes.Box("chinStrap", Vector3.zero, new Vector3(0.008f, 0.11f, 0.015f)), m.Gloves,
                        new Vector3(0.096f, 0.055f, 0.01f), false));
                }

                if (level >= 3)
                {
                    v.Objects.Add(Part("NvgMount", Head, CharacterMeshes.Box("nvgMount", Vector3.zero, new Vector3(0.05f, 0.045f, 0.03f)), m.Metal,
                        new Vector3(0f, 0.16f, 0.122f), Quaternion.Euler(-20f, 0f, 0f), false));
                    v.Objects.Add(Part("RailL", Head, CharacterMeshes.Box("helmetRail", Vector3.zero, new Vector3(0.012f, 0.025f, 0.12f)), m.Metal,
                        new Vector3(-0.124f, 0.135f, -0.008f), false));
                    v.Objects.Add(Part("RailR", Head, CharacterMeshes.Box("helmetRail", Vector3.zero, new Vector3(0.012f, 0.025f, 0.12f)), m.Metal,
                        new Vector3(0.124f, 0.135f, -0.008f), false));
                    var cup = CharacterMeshes.Cylinder("earCup", -0.022f, 0.022f, 0.042f, 0.04f, 8, true);
                    v.Objects.Add(Part("EarCupL", Head, cup, m.GearDark, new Vector3(-0.108f, 0.08f, 0f), Quaternion.Euler(0f, 0f, 90f), false));
                    v.Objects.Add(Part("EarCupR", Head, cup, m.GearDark, new Vector3(0.108f, 0.08f, 0f), Quaternion.Euler(0f, 0f, -90f), false));
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
            // Bordo bere: sağa yatık, sol önde altın arma.
            v.Objects.Add(Part("Beret", Head, CharacterMeshes.Ellipsoid("beret", Vector3.zero, new Vector3(0.12f, 0.047f, 0.124f), 10, 4), m.Beret,
                new Vector3(0.02f, 0.178f, -0.006f), Quaternion.Euler(0f, 0f, -12f)));
            v.Objects.Add(Part("BeretBand", Head, CharacterMeshes.Cylinder("beretBand", 0.125f, 0.15f, 0.104f, 0.104f, 10, false, 1.08f), m.Beret,
                new Vector3(0f, 0f, -0.004f), false));
            v.Objects.Add(Part("BeretBadge", Head, CharacterMeshes.Box("beretBadge", Vector3.zero, new Vector3(0.026f, 0.032f, 0.01f)), m.Gold,
                new Vector3(-0.05f, 0.155f, 0.1f), Quaternion.Euler(-10f, -18f, 0f), false));
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
                    v.Objects.Add(Part("Pack", root, CharacterMeshes.Box("pack1", new Vector3(0f, 0.13f, -0.065f), new Vector3(0.27f, 0.3f, 0.13f)), m.Gear,
                        Vector3.zero));
                    v.Objects.Add(Part("PackFlap", root, CharacterMeshes.Box("pack1Flap", new Vector3(0f, 0.27f, -0.07f), new Vector3(0.25f, 0.04f, 0.125f)),
                        m.GearDark, Vector3.zero, false));
                    break;

                case 2:
                    v.Objects.Add(Part("Pack", root, CharacterMeshes.Box("pack2", new Vector3(0f, 0.12f, -0.09f), new Vector3(0.32f, 0.42f, 0.18f)), m.Gear,
                        Vector3.zero));
                    v.Objects.Add(Part("PackFlap", root, CharacterMeshes.Box("pack2Flap", new Vector3(0f, 0.32f, -0.095f), new Vector3(0.3f, 0.05f, 0.17f)),
                        m.GearDark, Vector3.zero, false));
                    v.Objects.Add(Part("PackSideL", root, CharacterMeshes.Box("packSide", Vector3.zero, new Vector3(0.05f, 0.2f, 0.12f)), m.GearDark,
                        new Vector3(-0.185f, 0.06f, -0.09f), false));
                    v.Objects.Add(Part("PackSideR", root, CharacterMeshes.Box("packSide", Vector3.zero, new Vector3(0.05f, 0.2f, 0.12f)), m.GearDark,
                        new Vector3(0.185f, 0.06f, -0.09f), false));
                    break;

                default:
                    v.Objects.Add(Part("Pack", root, CharacterMeshes.Box("pack3", new Vector3(0f, 0.14f, -0.11f), new Vector3(0.36f, 0.55f, 0.22f)), m.Gear,
                        Vector3.zero));
                    v.Objects.Add(Part("PackFlap", root, CharacterMeshes.Box("pack3Flap", new Vector3(0f, 0.4f, -0.115f), new Vector3(0.34f, 0.06f, 0.21f)),
                        m.GearDark, Vector3.zero, false));
                    v.Objects.Add(Part("Bedroll", root, CharacterMeshes.Cylinder("bedroll", -0.2f, 0.2f, 0.065f, 0.065f, 8, true), m.Camo,
                        new Vector3(0f, 0.5f, -0.11f), Quaternion.Euler(0f, 0f, 90f)));
                    v.Objects.Add(Part("PackSideL", root, CharacterMeshes.Box("packSide3", Vector3.zero, new Vector3(0.06f, 0.26f, 0.14f)), m.GearDark,
                        new Vector3(-0.21f, 0.05f, -0.11f), false));
                    v.Objects.Add(Part("PackSideR", root, CharacterMeshes.Box("packSide3", Vector3.zero, new Vector3(0.06f, 0.26f, 0.14f)), m.GearDark,
                        new Vector3(0.21f, 0.05f, -0.11f), false));
                    break;
            }

            v.SetActive(false);
            _backpacks[level] = v;
            return v;
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
                    Destroy(root);
                root = null;
            }

            if (root != null && root.GetComponentInChildren<Renderer>(true) == null)
            {
                // Görseli olmayan model (fabrika henüz hazır değil) — basit modele düş.
                Destroy(root);
                root = null;
            }

            if (root == null)
                root = BuildFallbackWeapon(kind, out muzzle);

            var t = root.transform;
            if (t.parent != WeaponSocket)
                t.SetParent(WeaponSocket, false);

            // Görsel silahın collider'ı olmamalı (mermiler ve hareket etkilenmesin).
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
                Destroy(colliders[i]);

            GameLayers.SetLayerRecursively(root, _visualLayer);
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                renderers[i].motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                RegisterRenderer(renderers[i]);
            }

            root.SetActive(false);
            var held = new HeldWeapon
            {
                Key = key,
                Root = root,
                Muzzle = muzzle,
                Kind = kind,
                LeftHandReach = LeftHandReachFor(weapon.Category)
            };
            _weapons.Add(held);
            return held;
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
                    Destroy(w.Root);
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
                ComputeArmIk(HoldKind.None, 0f);
                return;
            }

            held.LastUsed = Time.time;
            _hold = held.Kind;
            if (held.Root != null)
                held.Root.SetActive(!_dead);

            ComputeArmIk(held.Kind, held.LeftHandReach);
        }

        /// <summary>Tutuş türüne göre kolların IK pozunu (ArmsPivot uzayında) bir kez hesaplar.</summary>
        private void ComputeArmIk(HoldKind kind, float leftReach)
        {
            if (WeaponSocket == null)
                return;

            Vector3 socket;
            Vector3 rightWrist;
            Vector3 leftWrist;
            if (kind == HoldKind.Pistol)
            {
                socket = new Vector3(0.04f, 0.0f, 0.4f);
                rightWrist = socket + new Vector3(0f, -0.035f, -0.03f);
                leftWrist = socket + new Vector3(-0.035f, -0.05f, -0.02f);
            }
            else
            {
                socket = new Vector3(0.1f, -0.08f, 0.22f);
                rightWrist = socket + new Vector3(0.0f, -0.035f, -0.035f);
                leftWrist = socket + new Vector3(-0.03f, -0.005f, Mathf.Max(0.12f, leftReach));
            }

            _socketRest = socket;
            WeaponSocket.localPosition = socket;
            WeaponSocket.localRotation = Quaternion.identity;

            var rightShoulder = new Vector3(ShoulderX, 0f, 0f);
            var leftShoulder = new Vector3(-ShoulderX, 0f, 0f);
            SolveTwoBone(rightShoulder, rightWrist, rightShoulder + new Vector3(0.3f, -0.4f, -0.2f), UpperArmLength, ForearmLength, -1f,
                out _ikRightShoulder, out _ikRightElbow);
            SolveTwoBone(leftShoulder, leftWrist, leftShoulder + new Vector3(-0.3f, -0.45f, -0.05f), UpperArmLength, ForearmLength, -1f,
                out _ikLeftShoulder, out _ikLeftElbow);
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

        /// <summary>Atış sekmesi (kollar ve silah yukarı-geri teper).</summary>
        public void PlayFire()
        {
            if (_dead)
                return;

            _recoil = 1f;
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

            _dead = false;
            _deathTime = 0f;
            _deathWeaponHidden = false;
            _recoil = 0f;
            _flinch = 0f;
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

            PlayDeath(dir);
        }

        private void OnOwnerDamaged(Combatant combatant, DamageInfo damage)
        {
            if (_dead || this == null)
                return;

            _flinch = Mathf.Clamp01(0.4f + damage.Amount / 40f);
            _flinchSide = UnityEngine.Random.value < 0.5f ? -1f : 1f;
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

            ApplyPose(dt);
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
            _body.localPosition = new Vector3(0f, hipY, hipZ);
            var hipRoll = moveWeight * sin * 3f * (1f - prone);
            var bodyPitch = 90f * prone;
            _body.localRotation = Quaternion.Euler(bodyPitch, 0f, hipRoll);

            // --- Bacaklar ---
            ApplyLeg(_leftHip, _leftKnee, _leftAnkle, -1f, _phase, uprightHip, strideHalf, moveWeight, run, crouch, prone, seat, air);
            ApplyLeg(_rightHip, _rightKnee, _rightAnkle, 1f, _phase + Mathf.PI, uprightHip, strideHalf, moveWeight, run, crouch, prone, seat, air);

            // --- Gövde, baş, kollar ---
            var pitch = Mathf.Lerp(_aimPitch, Mathf.Clamp(_aimPitch, -25f, 20f), prone);
            var hasWeapon = _hold != HoldKind.None;
            var breathing = Mathf.Sin(_breath) * 0.8f * (1f - moveWeight);
            var lean = run * 9f + crouch * 12f + _flinch * -7f + breathing * 0.5f;
            var spineX = lean + pitch * 0.25f - 5f * prone + seat * 5f;
            var chestX = pitch * 0.25f + crouch * 4f - 15f * prone;
            var spineYaw = -sin * 5f * moveWeight * (1f - prone) * (hasWeapon ? 0.3f : 1f);
            _spine.localRotation = Quaternion.Euler(spineX, spineYaw, _flinch * _flinchSide * 4f);
            Chest.localRotation = Quaternion.Euler(chestX, 0f, 0f);

            var upperTotal = bodyPitch + spineX + chestX;
            var headTotal = pitch * 0.9f;
            Head.localRotation = Quaternion.Euler(headTotal - upperTotal, -spineYaw * 0.8f, 0f);

            if (hasWeapon)
            {
                var sprintDrop = run * 30f;
                var seatDrop = seat * 35f;
                var armsTotal = pitch + sprintDrop + seatDrop - _recoil * 6f + breathing * 0.3f;
                var armsYaw = run * 25f;
                _armsPivot.localRotation = Quaternion.Euler(armsTotal - upperTotal, armsYaw - spineYaw, 0f);
                WeaponSocket.localPosition = _socketRest + new Vector3(0f, 0.004f * Mathf.Sin(_phase * 2f) * moveWeight, -0.03f * _recoil);
                _leftShoulder.localRotation = _ikLeftShoulder;
                _leftElbow.localRotation = _ikLeftElbow;
                _rightShoulder.localRotation = _ikRightShoulder;
                _rightElbow.localRotation = _ikRightElbow;
                _leftHand.localRotation = Quaternion.Euler(-20f, 0f, 0f);
                _rightHand.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            }
            else
            {
                // Silahsız: kollar dikey sarkar, yürürken salınır.
                _armsPivot.localRotation = Quaternion.Euler(-upperTotal * (1f - prone), 0f, 0f);
                var swing = Mathf.Lerp(18f, 42f, run) * moveWeight * (1f - prone);
                var elbow = Mathf.Lerp(12f, 85f, run) + crouch * 20f;
                var leftArm = Quaternion.Euler(sin * swing, 0f, -6f);
                var rightArm = Quaternion.Euler(-sin * swing, 0f, 6f);
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
                    angle = 88f * f * f;
                }
                else
                {
                    var b = Mathf.Clamp01((t - DeathDuration) / 0.22f);
                    angle = 88f - 6f * Mathf.Sin(b * Mathf.PI);
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

        private void OnDestroy()
        {
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
