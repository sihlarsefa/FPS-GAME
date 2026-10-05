using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Config;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace Project.Infrastructure.Weapons
{
    /// <summary>
    /// FPP silah görünümü: kamuflaj kollar + eldivenli eller silahı tutar (iki kemikli IK, el bilekleri silahın tutma
    /// noktalarına bağlı), silahsızken yumruk duruşu. Kuşanmada indir/kaldır (EquipSeconds), nişan alma (nişan hattı ekran
    /// merkezine hizalanır; dürbünlüde tam nişanda çağıran SetHidden ile gizler), geri tepme (konum + dönüş, yay ile geri
    /// döner), şarjör değiştirme (şarjör iner/çıkar; JNG-90'da sürgü), her atıştan sonra sürgü (JNG-90) / pompa (Escort) /
    /// kızak (tabanca), yumruk, bomba atma, iyileşme (sargı/ilk yardım/içecek) pozları, bakıştan salınım, hızdan sallanma,
    /// koşu pozu, namlu alevi ve kovan atma. Tüm nesneler verilen katmandadır. Kare başına bellek ayırmaz.
    /// Kameranın çocuğu olarak çalışır (Create).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class WeaponViewModel : MonoBehaviour
    {
        private const float AdsSeconds = 0.18f;
        private const float ScopeExtraSeconds = 0.08f;
        private const float FistsEquipSeconds = 0.3f;
        private const float MaxDeltaTime = 0.1f;
        private const float SpringStep = 1f / 120f;
        private const float FlashSeconds = 0.05f;
        private const int ShellPoolSize = 8;
        private const float ShellLifetime = 0.75f;
        private const float OverrideReleaseRate = 8f;

        /// <summary>Salınım dönüş merkezi (eller civarı) — kamera uzayı.</summary>
        private static readonly Vector3 SwayPivot = new Vector3(0.07f, -0.12f, 0.26f);

        private static readonly Vector3 RightShoulder = new Vector3(0.2f, -0.46f, -0.3f);
        private static readonly Vector3 LeftShoulder = new Vector3(-0.21f, -0.42f, -0.24f);
        private static readonly Vector3 RightPole = new Vector3(0.6f, -1f, -0.2f);
        private static readonly Vector3 LeftPole = new Vector3(-0.6f, -1f, -0.2f);

        // ------------------------------------------------------------------ Types

        private enum EquipPhase
        {
            None,
            Lowering,
            Raising
        }

        private enum GoalKind
        {
            None,
            Anchor,
            ModelSpace,
            PoseSpace
        }

        private sealed class Arm
        {
            public bool IsLeft;
            public Transform Upper;
            public Transform Fore;
            public Transform Hand;
            public MeshFilter HandFilter;
            public MeshRenderer UpperRenderer;
            public MeshRenderer ForeRenderer;
            public MeshRenderer HandRenderer;
            public Vector3 Shoulder;
            public Vector3 Pole;
            public bool FistMesh;

            public Transform PropSocket;
            public MeshFilter PropFilter;
            public MeshRenderer PropRenderer;
            public ViewmodelProp Prop;

            // Bu karenin hedefi (eylemler yazar).
            public GoalKind Goal;
            public float GoalWeight;
            public Transform GoalAnchor;
            public Transform GoalAnchorB;
            public float GoalAnchorBlend;
            public Vector3 GoalPosition;
            public Quaternion GoalRotation = Quaternion.identity;

            // Görüntülenen geçersiz kılma (bırakılırken son pozda söner).
            public float Weight;
            public Vector3 LastPosition;
            public Quaternion LastRotation = Quaternion.identity;

            public void ClearGoal()
            {
                Goal = GoalKind.None;
                GoalWeight = 0f;
                GoalAnchor = null;
                GoalAnchorB = null;
                GoalAnchorBlend = 0f;
            }

            public void SetAnchorGoal(Transform anchor, float weight)
            {
                if (anchor == null || weight <= 0f)
                    return;

                Goal = GoalKind.Anchor;
                GoalAnchor = anchor;
                GoalAnchorB = null;
                GoalWeight = Mathf.Clamp01(weight);
            }

            public void SetAnchorGoal(Transform a, Transform b, float blend, float weight)
            {
                if (weight <= 0f)
                    return;

                if (a == null)
                {
                    SetAnchorGoal(b, weight);
                    return;
                }

                Goal = GoalKind.Anchor;
                GoalAnchor = a;
                GoalAnchorB = b;
                GoalAnchorBlend = Mathf.Clamp01(blend);
                GoalWeight = Mathf.Clamp01(weight);
            }

            public void SetPoseGoal(GoalKind kind, Vector3 position, Quaternion rotation, float weight)
            {
                if (weight <= 0f)
                    return;

                Goal = kind;
                GoalPosition = position;
                GoalRotation = rotation;
                GoalWeight = Mathf.Clamp01(weight);
            }
        }

        private struct Shell
        {
            public Transform Transform;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Vector3 Position;
            public Vector3 Velocity;
            public Vector3 AngularVelocity;
            public Quaternion Rotation;
            public float Life;
        }

        // ------------------------------------------------------------------ State

        private int _layer;
        private bool _built;
        private bool _buildFailed;
        private Transform _sway;
        private Transform _holder;
        private Arm _right;
        private Arm _left;

        private readonly Dictionary<WeaponStyle, WeaponModel> _models = new Dictionary<WeaponStyle, WeaponModel>(4);
        private WeaponModel _model;
        private WeaponStyle _shownStyle = WeaponStyle.None;
        private WeaponDefinitionData _shownDefinition;
        private PoseProfile _profile;
        private Vector3 _sightLocal;
        private float _eyeRelief = 0.14f;

        // Kuşanma.
        private EquipPhase _equipPhase;
        private float _equipLower;
        private float _lowerRate;
        private float _raiseDuration;
        private float _raiseTime;
        private float _raiseFrom;
        private bool _hasPending;
        private WeaponStyle _pendingStyle;
        private WeaponDefinitionData _pendingDefinition;

        // Nişan.
        private bool _wantsAim;
        private float _aimBlock;

        // Hareket.
        private float _speed01;
        private bool _sprinting;
        private bool _grounded = true;
        private bool _wasGrounded = true;
        private float _yawRate;
        private float _pitchRate;
        private int _motionFrame = -10;
        private float _sprintBlend;
        private float _bobPhase;
        private float _bobAmp;
        private float _airBlend;
        private float _idleTime;
        private Vector3 _swayEuler;
        private Vector3 _swayPos;
        private Vector3 _bobPos;
        private Vector3 _bobEuler;
        private float _landOffset;
        private float _landVelocity;

        // Geri tepme yayı.
        private Vector3 _kickPos;
        private Vector3 _kickPosVel;
        private Vector3 _kickRot;
        private Vector3 _kickRotVel;
        private float _springStiffness = 220f;
        private float _springAccumulator;

        // Namlu alevi ve kovanlar.
        private Transform _flash;
        private MeshRenderer _flashRenderer;
        private Light _flashLight;
        private float _flashTimer;
        private Shell[] _shells;
        private int _nextShell;

        private bool _hidden;

        // ------------------------------------------------------------------ Contract API

        public WeaponDefinitionData Current { get; private set; }

        public bool IsEquipping { get; private set; }

        /// <summary>0 = kalçadan, 1 = tam nişan (doğrusal; çağıranın zamanlamasıyla aynı).</summary>
        public float AimBlend { get; private set; }

        /// <summary>Gösterilen silahın namlu ucu (silah yoksa kök).</summary>
        public Transform Muzzle => _model != null && _model.Muzzle != null ? _model.Muzzle : transform;

        public Vector3 MuzzleWorldPosition => Muzzle.position;

        // ------------------------------------------------------------------ Extra API

        /// <summary>Gösterilen silah modeli (yumrukta null).</summary>
        public WeaponModel Model => _model;

        public bool IsHidden => _hidden;

        public bool IsFists => _model == null;

        public bool IsReloading => _action == ViewAction.Reload;

        public bool IsBusy => _action != ViewAction.None;

        public int Layer => _layer;

        public static WeaponViewModel Create(Transform cameraTransform, int layer)
        {
            var go = new GameObject("WeaponViewModel");
            go.layer = layer;
            var t = go.transform;
            if (cameraTransform != null)
                t.SetParent(cameraTransform, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            var viewModel = go.AddComponent<WeaponViewModel>();
            viewModel.SetLayer(layer);
            return viewModel;
        }

        /// <summary>Eski API: yapılandırmadaki silahla kurar (katman kameranınkidir).</summary>
        public static WeaponViewModel CreateDefault(Transform cameraTransform, WeaponConfig config)
        {
            var vm = Create(cameraTransform, cameraTransform != null ? cameraTransform.gameObject.layer : 0);
            vm.Equip(config != null ? config.ToDefinition() : WeaponDefinitionData.AssaultRifle);
            return vm;
        }

        /// <summary>Silah kuşanır (null = yumruk). Önceki silah indirilir, yenisi EquipSeconds içinde kaldırılır.</summary>
        public void Equip(WeaponDefinitionData weapon)
        {
            EnsureBuilt();
            Current = weapon;
            CancelActions();

            var style = ResolveStyle(weapon);
            var seconds = weapon != null ? Mathf.Clamp(weapon.EquipSeconds, 0.15f, 3f) : FistsEquipSeconds;
            _pendingStyle = style;
            _pendingDefinition = weapon;
            _hasPending = true;
            IsEquipping = true;

            if (_equipLower < 0.6f)
            {
                var lowerSeconds = Mathf.Min(0.2f, seconds * 0.35f);
                _equipPhase = EquipPhase.Lowering;
                _lowerRate = Mathf.Max(0.01f, 1f - _equipLower) / Mathf.Max(0.05f, lowerSeconds);
                _raiseDuration = Mathf.Max(0.1f, seconds - lowerSeconds);
            }
            else
            {
                ApplyPending();
                BeginRaise(seconds);
            }
        }

        public void SetAim(bool aiming)
        {
            _wantsAim = aiming;
        }

        /// <summary>Atış: geri tepme, namlu alevi, kovan; tabancada kızak, JNG-90'da sürgü, Escort'ta pompa döngüsü.</summary>
        public void OnFire()
        {
            EnsureBuilt();
            if (_model == null)
                return;

            var definition = _shownDefinition ?? Current;
            var strength = 1f;
            if (definition != null)
                strength = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0.05f, definition.RecoilVertical) / 0.6f), 0.6f, 1.8f);

            var aim = Smooth01(AimBlend);
            var posScale = Mathf.Lerp(1f, 0.55f, aim) * strength;
            var rotScale = Mathf.Lerp(1f, 0.4f, aim) * strength;
            var side = Random.Range(-1f, 1f);
            var p = _profile;
            AddKick(new Vector3(side * p.KickSide, p.KickBack * 0.12f, -p.KickBack) * posScale,
                new Vector3(-p.KickPitch * Random.Range(0.85f, 1.1f), side * p.KickPitch * 0.22f, side * p.KickRoll) * rotScale);

            ShowFlash();
            HandleFireCycle(definition);
        }

        /// <summary>Eski API: verilen geri tepme miktarı (m) ve toparlanma hızıyla tepme.</summary>
        public void ApplyRecoil(float kickback, float recoverySpeed)
        {
            EnsureBuilt();
            if (recoverySpeed > 0f)
                _springStiffness = Mathf.Clamp(recoverySpeed * 25f, 80f, 500f);

            var k = Mathf.Clamp(kickback, 0f, 0.2f);
            AddKick(new Vector3(0f, k * 0.12f, -k), new Vector3(-k * 120f, Random.Range(-1f, 1f) * k * 20f, 0f));
        }

        public void PlayReload(float durationSeconds)
        {
            EnsureBuilt();
            if (_model == null || _hasPending)
                return;

            CancelActions();
            StartAction(ViewAction.Reload, Mathf.Clamp(durationSeconds, 0.3f, 12f));
            _shellCount = Mathf.Clamp(Mathf.RoundToInt((_actionDuration - 0.5f) / 0.45f), 1, 7);
        }

        public void StopReload()
        {
            if (_action != ViewAction.Reload)
                return;

            EndAction();
        }

        public void PlayMelee()
        {
            EnsureBuilt();
            if (_action == ViewAction.Reload || _action == ViewAction.Use || _action == ViewAction.Throw)
                CancelActions();

            _punchLeft = !_punchLeft;
            StartAction(ViewAction.Melee, _model == null ? 0.55f : 0.6f);
        }

        public void PlayThrow()
        {
            PlayThrow(false);
        }

        /// <summary>Bomba atma pozu (smoke = sis bombası görünümü).</summary>
        public void PlayThrow(bool smoke)
        {
            EnsureBuilt();
            CancelActions();
            _throwProp = smoke ? ViewmodelProp.SmokeGrenade : ViewmodelProp.FragGrenade;
            StartAction(ViewAction.Throw, 0.85f);
        }

        public void PlayUse(float durationSeconds)
        {
            PlayUse(durationSeconds, null);
        }

        /// <summary>İyileşme/takviye pozu; itemId'ye göre eşya (sargı, ilk yardım, enerji içeceği, ağrı kesici).</summary>
        public void PlayUse(float durationSeconds, string itemId)
        {
            EnsureBuilt();
            CancelActions();
            _useProp = PropForItem(itemId);
            _useExiting = false;
            _useExitTime = 0f;
            StartAction(ViewAction.Use, Mathf.Clamp(durationSeconds, 0.2f, 30f));
        }

        public void StopUse()
        {
            if (_action != ViewAction.Use || _useExiting)
                return;

            _useExiting = true;
            _useExitTime = 0f;
        }

        /// <summary>
        /// Hareket girdisi (her kare): speed01 0..1 yürüyüş/koşu, bakış delta (derece/kare) salınımı belirler.
        /// </summary>
        public void SetMotion(float speed01, bool sprinting, bool grounded, float lookYawDelta, float lookPitchDelta)
        {
            _speed01 = Mathf.Clamp01(float.IsNaN(speed01) ? 0f : speed01);
            _sprinting = sprinting;
            _grounded = grounded;
            var dt = Time.deltaTime;
            if (dt > 1e-5f)
            {
                _yawRate = Mathf.Clamp(SafeFloat(lookYawDelta) / dt, -900f, 900f);
                _pitchRate = Mathf.Clamp(SafeFloat(lookPitchDelta) / dt, -900f, 900f);
            }

            _motionFrame = Time.frameCount;
        }

        /// <summary>Tüm görüntüleyicileri gizler/gösterir (mantık çalışmaya devam eder).</summary>
        public void SetHidden(bool hidden)
        {
            if (_hidden == hidden)
                return;

            _hidden = hidden;
            ApplyVisibility();
        }

        /// <summary>Tüm görünüm modeli nesnelerinin katmanını değiştirir.</summary>
        public void SetLayer(int layer)
        {
            _layer = layer;
            GameLayers.SetLayerRecursively(gameObject, layer);
        }

        // ------------------------------------------------------------------ Lifecycle

        private void Awake()
        {
            _layer = gameObject.layer;
            EnsureBuilt();
        }

        private void OnDisable()
        {
            _flashTimer = 0f;
            if (_flashRenderer != null)
                _flashRenderer.enabled = false;
            if (_flashLight != null)
                _flashLight.enabled = false;
        }

        private void LateUpdate()
        {
            if (!_built || _buildFailed)
                return;

            var dt = Mathf.Min(Time.deltaTime, MaxDeltaTime);
            if (dt <= 0f)
                return;

            try
            {
                if (_right != null)
                    _right.ClearGoal();
                if (_left != null)
                    _left.ClearGoal();

                UpdateEquip(dt);
                UpdateAim(dt);
                UpdateMotion(dt);
                UpdateRecoil(dt);
                UpdateActions(dt);
                UpdateCycles(dt);
                ApplyPose();
                ResolveGoals();
                UpdateArms(dt);
                UpdateFlash(dt);
                UpdateShells(dt);
            }
            catch (Exception e)
            {
                // Görsel bir hata oyunu durdurmamalı; tek sefer bildirilir.
                if (!_loggedError)
                {
                    _loggedError = true;
                    Debug.LogException(e, this);
                }
            }
        }

        private bool _loggedError;

        // ------------------------------------------------------------------ Build

        private void EnsureBuilt()
        {
            if (_built)
                return;

            _built = true;
            try
            {
                BuildRig();
                ShowStyle(WeaponStyle.None, null);
                _equipLower = 1f;
                BeginRaise(FistsEquipSeconds);
                IsEquipping = true;
            }
            catch (Exception e)
            {
                _buildFailed = true;
                Debug.LogWarning("[Silah] Görünüm modeli kurulamadı: " + e.Message);
            }
        }

        private void BuildRig()
        {
            ViewmodelMeshes.Ensure();

            _sway = CreateChild("Sway", transform);
            _sway.localPosition = SwayPivot;
            _holder = CreateChild("WeaponHolder", _sway);
            _holder.localPosition = -SwayPivot;

            _right = CreateArm(false);
            _left = CreateArm(true);

            // Namlu alevi.
            var flashPart = ViewmodelMeshes.Flash;
            _flash = CreateChild("MuzzleFlash", transform);
            if (flashPart != null)
            {
                _flashRenderer = AddRenderer(_flash.gameObject, flashPart);
                _flashRenderer.enabled = false;
            }

            var lightGo = new GameObject("FlashLight");
            lightGo.layer = _layer;
            lightGo.transform.SetParent(_flash, false);
            lightGo.transform.localPosition = new Vector3(0f, 0f, 0.06f);
            _flashLight = lightGo.AddComponent<Light>();
            _flashLight.type = LightType.Point;
            _flashLight.range = 6f;
            _flashLight.intensity = 2.5f;
            _flashLight.color = new Color(1f, 0.74f, 0.42f);
            _flashLight.shadows = LightShadows.None;
            _flashLight.enabled = false;

            // Kovan havuzu (dünya uzayında benzetilir).
            _shells = new Shell[ShellPoolSize];
            var shellPart = ViewmodelMeshes.GetProp(ViewmodelProp.RifleShell);
            for (var i = 0; i < ShellPoolSize; i++)
            {
                var t = CreateChild("Shell" + i, transform);
                var shell = new Shell { Transform = t, Life = 0f, Rotation = Quaternion.identity };
                if (shellPart != null)
                {
                    shell.Renderer = AddRenderer(t.gameObject, shellPart);
                    shell.Filter = t.GetComponent<MeshFilter>();
                    shell.Renderer.enabled = false;
                }

                _shells[i] = shell;
            }

            GameLayers.SetLayerRecursively(gameObject, _layer);
        }

        private Arm CreateArm(bool left)
        {
            var arm = new Arm
            {
                IsLeft = left,
                Shoulder = left ? LeftShoulder : RightShoulder,
                Pole = (left ? LeftPole : RightPole).normalized
            };

            var name = left ? "Left" : "Right";
            arm.Upper = CreateChild(name + "UpperArm", transform);
            arm.Fore = CreateChild(name + "Forearm", transform);
            arm.Hand = CreateChild(name + "Hand", transform);
            if (ViewmodelMeshes.UpperArm != null)
                arm.UpperRenderer = AddRenderer(arm.Upper.gameObject, ViewmodelMeshes.UpperArm);
            if (ViewmodelMeshes.Forearm != null)
                arm.ForeRenderer = AddRenderer(arm.Fore.gameObject, ViewmodelMeshes.Forearm);

            var handPart = left ? ViewmodelMeshes.FistLeft : ViewmodelMeshes.FistRight;
            if (handPart != null)
            {
                arm.HandRenderer = AddRenderer(arm.Hand.gameObject, handPart);
                arm.HandFilter = arm.Hand.GetComponent<MeshFilter>();
                arm.FistMesh = true;
            }

            arm.PropSocket = CreateChild("Prop", arm.Hand);
            arm.PropFilter = arm.PropSocket.gameObject.AddComponent<MeshFilter>();
            arm.PropRenderer = arm.PropSocket.gameObject.AddComponent<MeshRenderer>();
            ConfigureRenderer(arm.PropRenderer);
            arm.PropRenderer.enabled = false;

            // Kollar görünür noktada başlasın.
            arm.LastPosition = arm.Shoulder + new Vector3(0f, 0.2f, 0.45f);
            return arm;
        }

        private Transform CreateChild(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.layer = _layer;
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            return t;
        }

        private MeshRenderer AddRenderer(GameObject go, BuiltMeshPart part)
        {
            var filter = go.GetComponent<MeshFilter>();
            if (filter == null)
                filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = part.Mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null)
                renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = part.Materials;
            ConfigureRenderer(renderer);
            return renderer;
        }

        private static void ConfigureRenderer(MeshRenderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = false;
        }

        // ------------------------------------------------------------------ Equip / model swap

        private static WeaponStyle ResolveStyle(WeaponDefinitionData weapon)
        {
            if (weapon == null || weapon.Category == WeaponCategory.Melee)
                return WeaponStyle.None;

            return WeaponStyles.Resolve(weapon);
        }

        private void BeginRaise(float seconds)
        {
            _equipPhase = EquipPhase.Raising;
            _raiseDuration = Mathf.Max(0.05f, seconds);
            _raiseTime = 0f;
            _raiseFrom = _equipLower;
        }

        private void UpdateEquip(float dt)
        {
            switch (_equipPhase)
            {
                case EquipPhase.Lowering:
                    _equipLower = Mathf.MoveTowards(_equipLower, 1f, _lowerRate * dt);
                    if (_equipLower >= 0.999f)
                    {
                        _equipLower = 1f;
                        ApplyPending();
                        BeginRaise(_raiseDuration);
                    }

                    break;

                case EquipPhase.Raising:
                    _raiseTime += dt;
                    var x = Mathf.Clamp01(_raiseTime / _raiseDuration);
                    var eased = 1f - (1f - x) * (1f - x) * (1f - x);
                    _equipLower = _raiseFrom * (1f - eased);
                    if (x >= 1f)
                    {
                        _equipLower = 0f;
                        _equipPhase = EquipPhase.None;
                        IsEquipping = false;
                    }

                    break;

                default:
                    IsEquipping = false;
                    break;
            }
        }

        private void ApplyPending()
        {
            if (!_hasPending)
                return;

            _hasPending = false;
            ShowStyle(_pendingStyle, _pendingDefinition);
        }

        private void ShowStyle(WeaponStyle style, WeaponDefinitionData definition)
        {
            if (_model != null)
            {
                _model.ResetParts();
                _model.gameObject.SetActive(false);
            }

            _model = null;
            if (style != WeaponStyle.None && _holder != null)
            {
                if (!_models.TryGetValue(style, out var model) || model == null)
                {
                    try
                    {
                        model = WeaponModelFactory.BuildModel(style, definition, _holder, _layer, true);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[Silah] Görünüm silahı kurulamadı (" + style + "): " + e.Message);
                        model = null;
                    }

                    if (model != null)
                    {
                        GameLayers.SetLayerRecursively(model.gameObject, _layer);
                        _models[style] = model;
                    }
                }

                if (model != null)
                {
                    model.gameObject.SetActive(true);
                    var mt = model.transform;
                    mt.localPosition = Vector3.zero;
                    mt.localRotation = Quaternion.identity;
                    mt.localScale = Vector3.one;
                    model.Definition = definition;
                    model.ResetParts();
                    model.SetVisible(!_hidden);
                    _model = model;
                    _sightLocal = model.SightPoint != null ? mt.InverseTransformPoint(model.SightPoint.position) : new Vector3(0f, 0.08f, 0f);
                    _eyeRelief = Mathf.Max(0.06f, model.EyeRelief);
                }
            }

            _shownStyle = _model != null ? style : WeaponStyle.None;
            _shownDefinition = definition;
            _profile = PoseProfile.For(_shownStyle);

            var fists = _model == null;
            SetHandMesh(_right, fists);
            SetHandMesh(_left, fists);

            if (_flash != null)
            {
                var parent = _model != null && _model.Muzzle != null ? _model.Muzzle : transform;
                _flash.SetParent(parent, false);
                _flash.localPosition = Vector3.zero;
                _flash.localRotation = Quaternion.identity;
            }

            _flashTimer = 0f;
            UpdateFlashVisibility();
        }

        private static void SetHandMesh(Arm arm, bool fist)
        {
            if (arm == null || arm.HandFilter == null || arm.FistMesh == fist)
                return;

            var part = fist
                ? (arm.IsLeft ? ViewmodelMeshes.FistLeft : ViewmodelMeshes.FistRight)
                : (arm.IsLeft ? ViewmodelMeshes.HandGripLeft : ViewmodelMeshes.HandGripRight);
            if (part == null)
                return;

            arm.HandFilter.sharedMesh = part.Mesh;
            if (arm.HandRenderer != null)
                arm.HandRenderer.sharedMaterials = part.Materials;
            arm.FistMesh = fist;
        }

        // ------------------------------------------------------------------ Aim / motion / recoil

        private void UpdateAim(float dt)
        {
            var scoped = _shownDefinition != null && _shownDefinition.HasScope;
            var duration = AdsSeconds + (scoped ? ScopeExtraSeconds : 0f);
            var target = _wantsAim && _model != null ? 1f : 0f;
            AimBlend = Mathf.MoveTowards(AimBlend, target, dt / duration);

            var block = IsEquipping && _equipLower > 0.05f ? 1f : 0f;
            if (_action == ViewAction.Reload || _action == ViewAction.Use || _action == ViewAction.Throw)
                block = 1f;
            else if (_action == ViewAction.Melee && _model != null)
                block = 1f;
            _aimBlock = Mathf.MoveTowards(_aimBlock, block, dt * 6f);
        }

        private float AimPose => Smooth01(AimBlend) * (1f - _aimBlock);

        private void UpdateMotion(float dt)
        {
            if (Time.frameCount - _motionFrame > 1)
            {
                _yawRate = 0f;
                _pitchRate = 0f;
            }

            var weight = _profile.SwayScale;
            var targetEuler = new Vector3(
                Mathf.Clamp(_pitchRate * 0.01f, -5f, 5f),
                Mathf.Clamp(-_yawRate * 0.012f, -6f, 6f),
                Mathf.Clamp(-_yawRate * 0.016f, -7f, 7f)) * weight;
            var targetPos = new Vector3(
                Mathf.Clamp(-_yawRate * 0.00005f, -0.02f, 0.02f),
                Mathf.Clamp(-_pitchRate * 0.00004f, -0.015f, 0.015f),
                0f) * weight;
            var follow = 1f - Mathf.Exp(-9f / Mathf.Max(0.5f, weight) * dt);
            _swayEuler = Vector3.Lerp(_swayEuler, targetEuler, follow);
            _swayPos = Vector3.Lerp(_swayPos, targetPos, follow);

            var busy = _action == ViewAction.Reload || _action == ViewAction.Use || _action == ViewAction.Throw;
            var sprintTarget = _sprinting && _grounded && _speed01 > 0.2f && !busy ? 1f : 0f;
            _sprintBlend = Mathf.MoveTowards(_sprintBlend, sprintTarget, dt * 5f);

            var moving = _grounded ? _speed01 : 0f;
            _bobAmp = Mathf.MoveTowards(_bobAmp, moving * (_sprinting ? 1.5f : 1f), dt * 4f);
            _bobPhase += dt * Mathf.Lerp(6f, _sprinting ? 13.5f : 10f, moving);
            if (_bobPhase > Mathf.PI * 200f)
                _bobPhase -= Mathf.PI * 200f;
            _idleTime += dt;
            if (_idleTime > 1000f)
                _idleTime -= 1000f;

            var sin = Mathf.Sin(_bobPhase);
            var cos = Mathf.Cos(_bobPhase);
            _bobPos = new Vector3(sin * 0.008f, -Mathf.Abs(cos) * 0.011f + 0.0055f, 0f) * _bobAmp
                      + new Vector3(0f, Mathf.Sin(_idleTime * 1.3f) * 0.0018f, 0f);
            _bobEuler = new Vector3(Mathf.Sin(_bobPhase * 2f) * 0.6f, sin * 0.5f, sin * 1.4f) * _bobAmp
                        + new Vector3(Mathf.Sin(_idleTime * 1.1f) * 0.3f, Mathf.Sin(_idleTime * 0.7f) * 0.2f, 0f);

            _airBlend = Mathf.MoveTowards(_airBlend, _grounded ? 0f : 1f, dt * 4f);
            if (_grounded && !_wasGrounded)
                _landVelocity -= 0.35f;
            _wasGrounded = _grounded;
        }

        private void AddKick(Vector3 position, Vector3 euler)
        {
            _kickPos += position * 0.5f;
            _kickPosVel += position * 16f;
            _kickRot += euler * 0.5f;
            _kickRotVel += euler * 16f;
            _kickPos.z = Mathf.Max(_kickPos.z, -0.09f);
            _kickPos.y = Mathf.Clamp(_kickPos.y, -0.03f, 0.03f);
            _kickPos.x = Mathf.Clamp(_kickPos.x, -0.03f, 0.03f);
            _kickRot.x = Mathf.Max(_kickRot.x, -16f);
        }

        private void UpdateRecoil(float dt)
        {
            _springAccumulator += dt;
            var k = _springStiffness;
            var c = 2f * Mathf.Sqrt(k) * 0.72f;
            var steps = 0;
            while (_springAccumulator >= SpringStep && steps < 16)
            {
                _springAccumulator -= SpringStep;
                steps++;
                _kickPosVel += (-k * _kickPos - c * _kickPosVel) * SpringStep;
                _kickPos += _kickPosVel * SpringStep;
                _kickRotVel += (-k * _kickRot - c * _kickRotVel) * SpringStep;
                _kickRot += _kickRotVel * SpringStep;

                _landVelocity += (-140f * _landOffset - 15f * _landVelocity) * SpringStep;
                _landOffset += _landVelocity * SpringStep;
            }

            if (steps >= 16)
                _springAccumulator = 0f;
        }

        // ------------------------------------------------------------------ Pose

        private void ApplyPose()
        {
            var aim = AimPose;
            var damp = Mathf.Lerp(1f, 0.18f, aim);

            var swayPos = (_swayPos + _bobPos) * damp + new Vector3(0f, _landOffset * 0.06f - 0.012f * _airBlend, 0f) * damp;
            var swayEuler = (_swayEuler + _bobEuler) * damp + new Vector3(2.5f * _airBlend, 0f, 0f) * damp;
            _sway.localPosition = SwayPivot + swayPos;
            _sway.localRotation = Quaternion.Euler(swayEuler);

            if (_model == null)
                return;

            var p = _profile;
            var adsPos = new Vector3(-_sightLocal.x, -_sightLocal.y, _eyeRelief - _sightLocal.z);
            var pos = Vector3.Lerp(p.HipPosition, adsPos, aim);
            var rot = Quaternion.Slerp(p.HipRotation, Quaternion.identity, aim);

            var sprint = Smooth01(_sprintBlend) * (1f - aim);
            pos = Vector3.Lerp(pos, p.SprintPosition, sprint);
            rot = Quaternion.Slerp(rot, p.SprintRotation, sprint);

            var lower = Mathf.Max(Smooth01(_equipLower), _actionLower);
            pos = Vector3.Lerp(pos, p.LowerPosition, lower);
            rot = Quaternion.Slerp(rot, p.LowerRotation, lower);

            pos += _actionPos + _cyclePos;
            rot = rot * _actionRot * _cycleRot;

            pos += _kickPos;
            rot = rot * Quaternion.Euler(_kickRot);

            _holder.localPosition = pos - SwayPivot;
            _holder.localRotation = rot;
        }

        /// <summary>Poz uzayı (salınımsız kamera uzayı) → kök uzayı.</summary>
        private Vector3 PoseToRoot(Vector3 position) => _sway.localPosition + _sway.localRotation * (position - SwayPivot);

        private Quaternion PoseToRoot(Quaternion rotation) => _sway.localRotation * rotation;

        private void WorldToRoot(Transform t, out Vector3 position, out Quaternion rotation)
        {
            position = transform.InverseTransformPoint(t.position);
            rotation = Quaternion.Inverse(transform.rotation) * t.rotation;
        }

        // ------------------------------------------------------------------ Hands / IK

        private void ResolveGoals()
        {
            ResolveGoal(_right);
            ResolveGoal(_left);
        }

        private void ResolveGoal(Arm arm)
        {
            if (arm == null)
                return;

            switch (arm.Goal)
            {
                case GoalKind.Anchor:
                    if (arm.GoalAnchor == null)
                    {
                        arm.GoalWeight = 0f;
                        break;
                    }

                    WorldToRoot(arm.GoalAnchor, out var pa, out var ra);
                    if (arm.GoalAnchorB != null && arm.GoalAnchorBlend > 0f)
                    {
                        WorldToRoot(arm.GoalAnchorB, out var pb, out var rb);
                        pa = Vector3.Lerp(pa, pb, arm.GoalAnchorBlend);
                        ra = Quaternion.Slerp(ra, rb, arm.GoalAnchorBlend);
                    }

                    arm.LastPosition = pa;
                    arm.LastRotation = ra;
                    break;

                case GoalKind.ModelSpace:
                    if (_model == null)
                    {
                        arm.GoalWeight = 0f;
                        break;
                    }

                    var mt = _model.transform;
                    arm.LastPosition = transform.InverseTransformPoint(mt.TransformPoint(arm.GoalPosition));
                    arm.LastRotation = Quaternion.Inverse(transform.rotation) * (mt.rotation * arm.GoalRotation);
                    break;

                case GoalKind.PoseSpace:
                    arm.LastPosition = PoseToRoot(arm.GoalPosition);
                    arm.LastRotation = PoseToRoot(arm.GoalRotation);
                    break;
            }
        }

        private void UpdateArms(float dt)
        {
            UpdateArm(_right, dt);
            UpdateArm(_left, dt);
        }

        private void UpdateArm(Arm arm, float dt)
        {
            if (arm == null)
                return;

            // Temel hedef: silahın tutma noktası ya da yumruk duruşu.
            Vector3 basePos;
            Quaternion baseRot;
            var grip = _model != null ? (arm.IsLeft ? _model.LeftHandGrip : _model.RightHandGrip) : null;
            if (grip != null)
            {
                WorldToRoot(grip, out basePos, out baseRot);
            }
            else
            {
                FistPose(arm.IsLeft, out var fp, out var fr);
                basePos = PoseToRoot(fp);
                baseRot = PoseToRoot(fr);
            }

            var goal = arm.Goal == GoalKind.None ? 0f : arm.GoalWeight;
            arm.Weight = goal >= arm.Weight ? goal : Mathf.MoveTowards(arm.Weight, goal, dt * OverrideReleaseRate);

            Vector3 target;
            Quaternion targetRot;
            if (arm.Weight <= 0.0001f)
            {
                target = basePos;
                targetRot = baseRot;
            }
            else
            {
                var w = arm.Weight;
                target = Vector3.Lerp(basePos, arm.LastPosition, w);
                targetRot = Quaternion.Slerp(baseRot, arm.LastRotation, w);
            }

            SolveArm(arm, target, targetRot);
        }

        /// <summary>İki kemikli IK (omuz–dirsek–bilek). Hedef erişimin dışındaysa omuz hedefe doğru kaydırılır (boşluk kalmaz).</summary>
        private static void SolveArm(Arm arm, Vector3 wrist, Quaternion handRotation)
        {
            const float a = ViewmodelMeshes.UpperArmLength;
            const float b = ViewmodelMeshes.ForearmLength;
            var shoulder = arm.Shoulder;
            var toTarget = wrist - shoulder;
            var distance = toTarget.magnitude;
            var dir = distance > 1e-5f ? toTarget / distance : Vector3.forward;

            var maxReach = (a + b) * 0.995f;
            var minReach = Mathf.Abs(a - b) + 0.05f;
            if (distance > maxReach)
            {
                shoulder = wrist - dir * maxReach;
                distance = maxReach;
            }
            else if (distance < minReach)
            {
                shoulder = wrist - dir * minReach;
                distance = minReach;
            }

            var cosA = Mathf.Clamp((a * a + distance * distance - b * b) / (2f * a * distance), -1f, 1f);
            var sinA = Mathf.Sqrt(Mathf.Max(0f, 1f - cosA * cosA));
            var pole = arm.Pole - Vector3.Dot(arm.Pole, dir) * dir;
            if (pole.sqrMagnitude < 1e-6f)
                pole = Vector3.Cross(dir, Vector3.right);
            pole.Normalize();
            var elbow = shoulder + dir * (a * cosA) + pole * (a * sinA);

            var upperDir = elbow - shoulder;
            var foreDir = wrist - elbow;
            arm.Upper.localPosition = shoulder;
            arm.Upper.localRotation = SafeLook(upperDir, -pole);
            arm.Fore.localPosition = elbow;
            arm.Fore.localRotation = SafeLook(foreDir, handRotation * Vector3.up);
            arm.Hand.localPosition = wrist;
            arm.Hand.localRotation = handRotation;
        }

        private static Quaternion SafeLook(Vector3 forward, Vector3 up)
        {
            if (forward.sqrMagnitude < 1e-10f)
                return Quaternion.identity;

            if (up.sqrMagnitude < 1e-10f || Vector3.Cross(forward, up).sqrMagnitude < 1e-10f)
                up = Mathf.Abs(forward.normalized.y) < 0.95f ? Vector3.up : Vector3.forward;
            return Quaternion.LookRotation(forward, up);
        }

        private void SetProp(Arm arm, ViewmodelProp prop)
        {
            if (arm == null || arm.PropRenderer == null)
                return;

            if (arm.Prop != prop)
            {
                arm.Prop = prop;
                var part = prop == ViewmodelProp.None ? null : ViewmodelMeshes.GetProp(prop);
                if (part != null)
                {
                    arm.PropFilter.sharedMesh = part.Mesh;
                    arm.PropRenderer.sharedMaterials = part.Materials;
                    PropPose(prop, out var position, out var rotation);
                    arm.PropSocket.localPosition = position;
                    arm.PropSocket.localRotation = rotation;
                }
                else
                {
                    arm.Prop = ViewmodelProp.None;
                }
            }

            var visible = arm.Prop != ViewmodelProp.None && !_hidden;
            if (arm.PropRenderer.enabled != visible)
                arm.PropRenderer.enabled = visible;
        }

        /// <summary>Eşyanın el uzayındaki tutuluş pozu (avuç tarafı -Y).</summary>
        private static void PropPose(ViewmodelProp prop, out Vector3 position, out Quaternion rotation)
        {
            switch (prop)
            {
                case ViewmodelProp.FragGrenade:
                case ViewmodelProp.SmokeGrenade:
                    position = new Vector3(0f, -0.04f, 0.062f);
                    rotation = Quaternion.Euler(-70f, 0f, 0f);
                    break;
                case ViewmodelProp.Bandage:
                    position = new Vector3(0f, -0.045f, 0.058f);
                    rotation = Quaternion.identity;
                    break;
                case ViewmodelProp.FirstAid:
                    position = new Vector3(0f, -0.045f, 0.06f);
                    rotation = Quaternion.Euler(0f, 0f, 180f);
                    break;
                case ViewmodelProp.EnergyDrink:
                case ViewmodelProp.Painkiller:
                    position = new Vector3(0f, -0.04f, 0.062f);
                    rotation = Quaternion.Euler(0f, 0f, 90f);
                    break;
                case ViewmodelProp.ShotgunShell:
                case ViewmodelProp.RifleShell:
                    position = new Vector3(-0.006f, -0.03f, 0.1f);
                    rotation = Quaternion.identity;
                    break;
                default:
                    position = Vector3.zero;
                    rotation = Quaternion.identity;
                    break;
            }
        }

        // ------------------------------------------------------------------ Flash / shells

        private void ShowFlash()
        {
            if (_flash == null || _profile.FlashScale <= 0f)
                return;

            _flashTimer = FlashSeconds;
            var s = _profile.FlashScale * Random.Range(0.8f, 1.2f);
            _flash.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            _flash.localScale = new Vector3(s, s, s * Random.Range(0.85f, 1.35f));
            if (_flashLight != null)
                _flashLight.intensity = Random.Range(1.8f, 3.2f) * Mathf.Clamp(_profile.FlashScale, 0.4f, 1.4f);
            UpdateFlashVisibility();
        }

        private void UpdateFlash(float dt)
        {
            if (_flashTimer <= 0f)
                return;

            _flashTimer -= dt;
            if (_flashTimer <= 0f)
            {
                _flashTimer = 0f;
                UpdateFlashVisibility();
            }
        }

        private void UpdateFlashVisibility()
        {
            var on = _flashTimer > 0f && _model != null;
            if (_flashRenderer != null)
                _flashRenderer.enabled = on && !_hidden;
            if (_flashLight != null)
                _flashLight.enabled = on;
        }

        private void EjectShell()
        {
            if (_shells == null || _model == null || _model.EjectPort == null)
                return;

            var index = _nextShell;
            _nextShell = (_nextShell + 1) % _shells.Length;
            ref var shell = ref _shells[index];
            if (shell.Transform == null || shell.Renderer == null)
                return;

            var shotgun = _model.ShotgunShells;
            var part = ViewmodelMeshes.GetProp(shotgun ? ViewmodelProp.ShotgunShell : ViewmodelProp.RifleShell);
            if (part != null && shell.Filter != null && shell.Filter.sharedMesh != part.Mesh)
            {
                shell.Filter.sharedMesh = part.Mesh;
                shell.Renderer.sharedMaterials = part.Materials;
            }

            var small = WeaponStyles.IsPistol(_shownStyle) || _shownStyle == WeaponStyle.Sar109;
            shell.Transform.localScale = small ? new Vector3(1f, 1f, 0.55f) : Vector3.one;

            var port = _model.EjectPort;
            var right = _holder.right;
            var up = _holder.up;
            var forward = _holder.forward;
            shell.Position = port.position;
            shell.Velocity = right * Random.Range(1.3f, 2.1f) + up * Random.Range(0.8f, 1.5f) - forward * Random.Range(0.1f, 0.5f);
            shell.Rotation = port.rotation * Quaternion.Euler(0f, 90f + Random.Range(-15f, 15f), 0f);
            shell.AngularVelocity = new Vector3(Random.Range(-900f, 900f), Random.Range(-300f, 300f), Random.Range(-900f, 900f));
            shell.Life = ShellLifetime;
            shell.Transform.SetPositionAndRotation(shell.Position, shell.Rotation);
            shell.Renderer.enabled = !_hidden;
        }

        private void UpdateShells(float dt)
        {
            if (_shells == null)
                return;

            var gravity = Physics.gravity;
            for (var i = 0; i < _shells.Length; i++)
            {
                ref var shell = ref _shells[i];
                if (shell.Life <= 0f || shell.Transform == null)
                    continue;

                shell.Life -= dt;
                if (shell.Life <= 0f)
                {
                    if (shell.Renderer != null)
                        shell.Renderer.enabled = false;
                    continue;
                }

                shell.Velocity += gravity * dt;
                shell.Position += shell.Velocity * dt;
                shell.Rotation = Quaternion.Euler(shell.AngularVelocity * dt) * shell.Rotation;
                shell.Transform.SetPositionAndRotation(shell.Position, shell.Rotation);
            }
        }

        // ------------------------------------------------------------------ Visibility

        private void ApplyVisibility()
        {
            var visible = !_hidden;
            SetArmVisible(_right, visible);
            SetArmVisible(_left, visible);
            if (_model != null)
                _model.SetVisible(visible);

            UpdateFlashVisibility();
            if (_shells != null)
            {
                for (var i = 0; i < _shells.Length; i++)
                {
                    if (_shells[i].Renderer != null)
                        _shells[i].Renderer.enabled = visible && _shells[i].Life > 0f;
                }
            }
        }

        private static void SetArmVisible(Arm arm, bool visible)
        {
            if (arm == null)
                return;

            if (arm.UpperRenderer != null)
                arm.UpperRenderer.enabled = visible;
            if (arm.ForeRenderer != null)
                arm.ForeRenderer.enabled = visible;
            if (arm.HandRenderer != null)
                arm.HandRenderer.enabled = visible;
            if (arm.PropRenderer != null)
                arm.PropRenderer.enabled = visible && arm.Prop != ViewmodelProp.None;
        }

        // ------------------------------------------------------------------ Helpers

        private static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private static float Ramp(float t, float a, float b) => b <= a ? (t >= b ? 1f : 0f) : Mathf.Clamp01((t - a) / (b - a));

        private static float SmoothRamp(float t, float a, float b) => Smooth01(Ramp(t, a, b));

        /// <summary>a→b arası yükselir, c→d arası iner (yumuşak).</summary>
        private static float Window(float t, float a, float b, float c, float d) => SmoothRamp(t, a, b) * (1f - SmoothRamp(t, c, d));

        private static float SafeFloat(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
    }
}
