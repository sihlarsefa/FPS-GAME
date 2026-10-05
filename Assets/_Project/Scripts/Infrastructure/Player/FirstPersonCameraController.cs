using Project.Core.Interfaces;
using Project.Infrastructure.Config;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.Player
{
    /// <summary>
    /// FPP kamera: bakış (pitch), hassasiyet, ters Y, yay benzeri geri dönen sekme, yana eğilme (±12° yuvarlanma, ±0,35 m kayma,
    /// duvara girmez), göz yüksekliği yumuşatma, iniş çökmesi, kafa sallantısı, FOV yumuşatma (fov = 2·atan(tan(taban/2)/zoom)),
    /// Perlin kamera sarsıntısı.
    /// <para>
    /// Hiyerarşi: <c>Gövde (yaw, CharacterControllerMotor) → pitchPivot (göz) → CameraRig/Kamera</c>. Pivot'un yerel konumu
    /// (göz yüksekliği + eğilme) ve dönüşü (pitch + sekme + eğilme yuvarlanması) her LateUpdate'te yazılır; sallantı/sarsıntı
    /// pivot altındaki kamera nesnesine uygulanır (nişan yönünü bozmaz). Rig verilmezse pivot altındaki Camera kullanılır.
    /// </para>
    /// <para>
    /// YAW: <see cref="ApplyLook"/> yalnız pitch'i uygular; yaw'ı hassasiyetle DERECEYE çevirip <see cref="LastYawDegrees"/>'e
    /// yazar. Gövdeyi çevirmek için çağıran <c>motor.ApplyRotation(camera.LastYawDegrees)</c> der ya da <see cref="YawTarget"/>
    /// atar (atanırsa ApplyLook gövdeyi kendisi çevirir — ikisini birden yapmayın).
    /// </para>
    /// <para>
    /// Göz yüksekliği / eğilme / sallantı: SetEyeHeight / SetLean / SetBob hiç çağrılmazsa değerler üst hiyerarşideki
    /// <see cref="CharacterControllerMotor"/>'dan otomatik okunur; bir kez çağrılan kanal elle sürülür.
    /// </para>
    /// <see cref="Pitch"/> Unity X açısı kuralındadır (+ aşağı bakış), ayarın minPitch/maxPitch sınırlarıyla.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    public sealed class FirstPersonCameraController : MonoBehaviour, IFirstPersonCamera
    {
        private const float LeanProbeRadius = 0.14f;
        private const float MinZoom = 0.5f;
        private const float MaxZoom = 20f;
        private const float MinBaseFov = 20f;
        private const float MaxBaseFov = 130f;
        private const float LeanVerticalDropFactor = 0.18f;
        private const float ShakePositionScale = 0.025f;
        private const float TwoPi = Mathf.PI * 2f;

        [SerializeField] private Transform pitchPivot;
        [SerializeField] private PlayerMovementConfig config;

        private bool _initialized;
        private bool _ownsConfig;
        private bool _bound;
        private Transform _boundPivot;

        // Bağlantılar
        private CameraRig _rig;
        private Camera _camera;
        private Transform _viewTransform;
        private Vector3 _viewBaseLocalPosition;
        private Quaternion _viewBaseLocalRotation = Quaternion.identity;
        private Vector3 _pivotBaseLocalPosition;
        private bool _drivePivotPosition;
        private CharacterControllerMotor _motor;
        private Transform _ignoreRoot;

        // Bakış
        private float _pitch;
        private float _sensitivity = 0.12f;
        private bool _sensitivityExplicit;
        private bool _invertY;
        private float _lastYawDegrees;

        // Sekme
        private Vector2 _recoilTarget;
        private Vector2 _recoilCurrent;
        private float _recoilVelocityX;
        private float _recoilVelocityY;
        private float _pendingPermanentPitch;

        // FOV
        private float _baseFov = 80f;
        private bool _baseFovExplicit;
        private float _zoom = 1f;
        private float _currentFov = 80f;
        private float _appliedFov = -1f;

        // Eğilme
        private float _leanTarget;
        private float _lean;
        private float _leanClearance = 1f;
        private float _effectiveLean;

        // Göz / iniş
        private float _eyeTarget = 1.62f;
        private float _eye = 1.62f;
        private float _dipTarget;
        private float _dip;
        private float _dipVelocity;

        // Elle sürülen kanallar
        private bool _manualEye;
        private bool _manualLean;
        private bool _manualBob;

        // Sallantı
        private float _bobSpeed;
        private bool _bobGrounded = true;
        private float _bobPhase;
        private float _bobWeight;

        // Sarsıntı
        private float _shakeAmplitude;
        private float _shakeDecay;
        private float _shakeTime;
        private float _shakeSeed;

        // ------------------------------------------------------------------ Properties

        /// <summary>Kalıcı bakış açısı (Unity X: + aşağı). Geçici sekme hariç.</summary>
        public float Pitch => _pitch;

        /// <summary>Ekrandaki gerçek bakış açısı (sekme dahil, sınırlı).</summary>
        public float ViewPitch => Mathf.Clamp(_pitch - _recoilCurrent.x, MinPitch, MaxPitch);

        /// <summary>Ayarlardaki temel dikey görüş açısı (derece). Yakınlaştırma bunun üzerine uygulanır.</summary>
        public float BaseFieldOfView
        {
            get => _baseFov;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    return;
                _baseFov = Mathf.Clamp(value, MinBaseFov, MaxBaseFov);
                _baseFovExplicit = true;
            }
        }

        public bool InvertY
        {
            get => _invertY;
            set => _invertY = value;
        }

        /// <summary>Dünya kamerası (rig yoksa pivot altındaki Camera). Olmayabilir (null).</summary>
        public Camera Camera => _camera;

        public CameraRig Rig => _rig;
        public Transform PitchPivot => pitchPivot;
        public PlayerMovementConfig Config => config;

        /// <summary>Fare sayımı başına derece (ham ayar değeri).</summary>
        public float Sensitivity => _sensitivity;

        /// <summary>Yakınlaştırma ölçeği uygulanmış etkin hassasiyet.</summary>
        public float EffectiveSensitivity
        {
            get
            {
                if (!ZoomSensitivityScaling || _zoom <= 1.0001f && Mathf.Approximately(_currentFov, _baseFov))
                    return _sensitivity;

                var baseTan = Mathf.Tan(_baseFov * 0.5f * Mathf.Deg2Rad);
                var currentTan = Mathf.Tan(Mathf.Clamp(_currentFov, 1f, 170f) * 0.5f * Mathf.Deg2Rad);
                return baseTan > 0.0001f ? _sensitivity * Mathf.Clamp(currentTan / baseTan, 0.02f, 2f) : _sensitivity;
            }
        }

        /// <summary>Yakınlaştırmada hassasiyeti FOV oranıyla ölçekle (varsayılan: ayardan).</summary>
        public bool ZoomSensitivityScaling { get; set; } = true;

        public float Zoom => _zoom;
        public float CurrentFieldOfView => _currentFov;

        /// <summary>Son ApplyLook çağrısında yaw girdisinin derece karşılığı (gövdeye uygulanacak).</summary>
        public float LastYawDegrees => _lastYawDegrees;

        /// <summary>Atanırsa ApplyLook yaw'ı bu motora kendisi uygular. Varsayılan null.</summary>
        public IPlayerMotor YawTarget { get; set; }

        /// <summary>Etkin (duvar sınırlı) eğilme değeri -1..1.</summary>
        public float EffectiveLean => _effectiveLean;

        /// <summary>Nişan çıkış noktası: göz + eğilme kayması (sallantı/sarsıntı hariç).</summary>
        public Vector3 AimOrigin
        {
            get
            {
                var pivot = pitchPivot;
                if (pivot == null)
                    return _camera != null ? _camera.transform.position : transform.position;

                var parent = pivot.parent;
                if (!_drivePivotPosition || parent == null)
                    return pivot.position;

                return parent.TransformPoint(PivotLocalPosition());
            }
        }

        /// <summary>Nişan yönü: gövde yaw'ı + bakış + sekme (sallantı/sarsıntı/yuvarlanma hariç).</summary>
        public Vector3 AimForward => AimRotation * Vector3.forward;

        public Quaternion AimRotation
        {
            get
            {
                var pivot = pitchPivot;
                if (pivot == null)
                    return _camera != null ? _camera.transform.rotation : transform.rotation;

                var parent = pivot.parent;
                var parentRotation = parent != null ? parent.rotation : Quaternion.identity;
                return parentRotation * Quaternion.Euler(ViewPitch, _recoilCurrent.y, 0f);
            }
        }

        private float MinPitch => config != null ? config.minPitch : -85f;
        private float MaxPitch => config != null ? config.maxPitch : 85f;

        // ------------------------------------------------------------------ Lifecycle

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            // Yeniden açılış (ölüm kamerası / canlanma sonrası): geçici etkiler temiz başlasın, FOV yeniden yazılsın.
            ClearRecoil();
            _shakeAmplitude = 0f;
            _dip = 0f;
            _dipTarget = 0f;
            _dipVelocity = 0f;
            _bobWeight = 0f;
            _eye = _eyeTarget;
            _appliedFov = -1f;
        }

        private void OnDisable()
        {
            // Sallantı/sarsıntıyı geri al (ölüm/araç kamerasına temiz geçiş).
            if (_viewTransform != null)
            {
                _viewTransform.localPosition = _viewBaseLocalPosition;
                _viewTransform.localRotation = _viewBaseLocalRotation;
            }
        }

        private void OnDestroy()
        {
            BindMotor(null);
            ReleaseOwnedConfig();
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;
            if (config == null)
            {
                config = PlayerMovementConfig.CreateDefault();
                _ownsConfig = true;
            }

            _sensitivity = Mathf.Max(0.0001f, config.mouseSensitivity);
            ZoomSensitivityScaling = config.scaleSensitivityWithZoom;
            _shakeSeed = Random.Range(0f, 1000f);
            _eye = _eyeTarget = config.standingEyeHeight;
            _baseFov = _currentFov = Mathf.Clamp(config.fieldOfView, MinBaseFov, MaxBaseFov);
            // Bağlama burada yapılmaz: AddComponent sırasında hiyerarşi henüz kurulmamış olabilir. Configure çağrılmazsa
            // ilk LateUpdate'te varsayılan pivot bulunur (eski sahne kurulumları için).
        }

        /// <summary>
        /// Kamerayı bağlar. pitchPivot: gövdenin (yaw) altında göz noktası; null ise bu nesne/alt kamera kullanılır.
        /// rig: CameraRig (pivot altında değilse pivot'a taşınır); null ise pivot altındaki Camera.
        /// </summary>
        public void Configure(Transform pitchPivot, PlayerMovementConfig config, CameraRig rig)
        {
            EnsureInitialized();
            if (config != null && config != this.config)
            {
                ReleaseOwnedConfig();
                this.config = config;
                ZoomSensitivityScaling = config.scaleSensitivityWithZoom;
                if (!_sensitivityExplicit)
                    _sensitivity = Mathf.Max(0.0001f, config.mouseSensitivity);
            }

            _bound = true;
            Bind(ResolveDefaultPivot(pitchPivot != null ? pitchPivot : this.pitchPivot), rig, true);
        }

        private Transform ResolveDefaultPivot(Transform candidate)
        {
            if (candidate != null && candidate.GetComponent<CharacterController>() == null)
                return candidate;

            // Gövdenin kendisi pivot olamaz (yaw'ı ezer): alt kamerayı pivot yap.
            var root = candidate != null ? candidate : transform;
            if (root.GetComponent<CharacterController>() == null)
                return root;

            var childCamera = root.GetComponentInChildren<Camera>(true);
            if (childCamera != null && childCamera.transform != root)
                return childCamera.transform;

            var childRig = root.GetComponentInChildren<CameraRig>(true);
            return childRig != null && childRig.transform != root ? childRig.transform : null;
        }

        private void Bind(Transform pivot, CameraRig rig, bool allowReparent)
        {
            var pivotChanged = pivot != _boundPivot;
            _boundPivot = pivot;
            pitchPivot = pivot;

            // Rig
            if (rig == null && pivot != null)
                rig = pivot.GetComponentInChildren<CameraRig>(true);
            if (rig == null)
                rig = GetComponentInChildren<CameraRig>(true);

            if (rig != null && pivot != null && allowReparent && rig.transform != pivot
                && !rig.transform.IsChildOf(pivot) && !pivot.IsChildOf(rig.transform))
            {
                rig.transform.SetParent(pivot, false);
                rig.transform.localPosition = Vector3.zero;
                rig.transform.localRotation = Quaternion.identity;
            }

            _rig = rig;

            // Kamera
            Camera camera = null;
            if (rig != null)
                camera = rig.WorldCamera != null ? rig.WorldCamera : rig.GetComponent<Camera>();
            if (camera == null && pivot != null)
                camera = pivot.GetComponentInChildren<Camera>(true);
            if (camera == null)
                camera = GetComponentInChildren<Camera>(true);
            _camera = camera;

            // Sallantı/sarsıntı taşıyıcısı: pivot'un altındaki kamera nesnesi.
            Transform view = null;
            if (pivot != null)
            {
                if (rig != null && rig.transform != pivot && rig.transform.IsChildOf(pivot))
                    view = rig.transform;
                else if (camera != null && camera.transform != pivot && camera.transform.IsChildOf(pivot))
                    view = camera.transform;
            }

            if (view != _viewTransform)
            {
                if (_viewTransform != null)
                {
                    _viewTransform.localPosition = _viewBaseLocalPosition;
                    _viewTransform.localRotation = _viewBaseLocalRotation;
                }

                _viewTransform = view;
                if (view != null)
                {
                    _viewBaseLocalPosition = view.localPosition;
                    _viewBaseLocalRotation = view.localRotation;
                }
            }

            // Pivot taban konumu ve göz yüksekliği
            if (pivot != null && (pivotChanged || !_drivePivotPosition))
            {
                _drivePivotPosition = pivot.parent != null;
                var local = pivot.localPosition;
                _pivotBaseLocalPosition = new Vector3(local.x, 0f, local.z);
                var eye = local.y > 0.05f ? local.y : config.standingEyeHeight;
                if (!_manualEye)
                {
                    _eye = eye;
                    _eyeTarget = eye;
                }

                var x = pivot.localEulerAngles.x;
                if (x > 180f)
                    x -= 360f;
                _pitch = Mathf.Clamp(x, MinPitch, MaxPitch);
            }

            // Motor (otomatik göz/eğilme/sallantı + iniş çökmesi)
            CharacterControllerMotor motor = null;
            if (pivot != null)
                motor = pivot.GetComponentInParent<CharacterControllerMotor>();
            if (motor == null)
                motor = GetComponentInParent<CharacterControllerMotor>();
            BindMotor(motor);

            _ignoreRoot = motor != null ? motor.transform : pivot != null && pivot.parent != null ? pivot.parent : pivot;

            // FOV
            if (!_baseFovExplicit)
                _baseFov = Mathf.Clamp(rig != null && rig.WorldCamera != null ? rig.FieldOfView
                    : camera != null ? camera.fieldOfView : config.fieldOfView, MinBaseFov, MaxBaseFov);
            SnapFieldOfView();
            ApplyPose();
        }

        private void BindMotor(CharacterControllerMotor motor)
        {
            if (_motor == motor)
                return;

            if (_motor != null)
                _motor.Landed -= AddLandingImpact;

            _motor = motor;
            if (_motor != null)
                _motor.Landed += AddLandingImpact;
        }

        private void ReleaseOwnedConfig()
        {
            if (!_ownsConfig || config == null)
                return;

            var owned = config;
            _ownsConfig = false;
            config = null;
            if (UnityEngine.Application.isPlaying)
                Destroy(owned);
            else
                DestroyImmediate(owned);
        }

        // ------------------------------------------------------------------ IFirstPersonCamera

        /// <summary>
        /// Ham bakış girdisi (fare sayımı). Pitch'i hassasiyet/ters Y/yakınlaştırma ölçeğiyle uygular; yaw'ı dereceye çevirip
        /// <see cref="LastYawDegrees"/>'e yazar (YawTarget atanmışsa ona uygular).
        /// </summary>
        public void ApplyLook(float pitchDelta, float yawDelta)
        {
            EnsureInitialized();
            var scale = EffectiveSensitivity;
            var pitchInput = Sanitize(pitchDelta) * scale * (_invertY ? -1f : 1f);
            _pitch = Mathf.Clamp(_pitch - pitchInput, MinPitch, MaxPitch);

            _lastYawDegrees = Sanitize(yawDelta) * scale;
            if (YawTarget != null && _lastYawDegrees != 0f)
                YawTarget.ApplyRotation(_lastYawDegrees);
        }

        public void SetSensitivity(float sensitivity)
        {
            if (float.IsNaN(sensitivity) || float.IsInfinity(sensitivity))
                return;

            _sensitivity = Mathf.Clamp(sensitivity, 0.0001f, 10f);
            _sensitivityExplicit = true;
        }

        /// <summary>Sekme: pitchDegrees yukarı, yawDegrees sağa. Bir kısmı kalıcı bakışa işler, kalanı yayla geri döner.</summary>
        public void AddRecoil(float pitchDegrees, float yawDegrees)
        {
            EnsureInitialized();
            var pitch = Sanitize(pitchDegrees);
            var yaw = Sanitize(yawDegrees);
            var max = Mathf.Max(0.1f, config.maxRecoilPitch);

            var permanent = pitch * Mathf.Clamp01(config.recoilPermanentFraction);
            _pendingPermanentPitch += permanent;
            _recoilTarget.x = Mathf.Clamp(_recoilTarget.x + (pitch - permanent), -max, max);
            _recoilTarget.y = Mathf.Clamp(_recoilTarget.y + yaw, -max, max);
        }

        /// <summary>Temel FOV'u ayarlar (ayarlar menüsü). Yakınlaştırma korunur.</summary>
        public void SetFieldOfView(float fieldOfView)
        {
            BaseFieldOfView = fieldOfView;
        }

        /// <summary>Perlin sarsıntısı: intensity 0..1, süre boyunca söner. Üst üste gelenlerin büyüğü geçerli.</summary>
        public void Shake(float intensity, float durationSeconds)
        {
            if (float.IsNaN(intensity) || intensity <= 0f)
                return;

            var amplitude = Mathf.Clamp01(intensity);
            var duration = Mathf.Max(0.05f, float.IsNaN(durationSeconds) ? 0.3f : durationSeconds);
            if (amplitude >= _shakeAmplitude)
            {
                _shakeAmplitude = amplitude;
                _shakeDecay = amplitude / duration;
            }
            else
            {
                _shakeAmplitude = Mathf.Min(1f, _shakeAmplitude + amplitude * 0.25f);
                _shakeDecay = Mathf.Min(_shakeDecay, _shakeAmplitude / duration);
            }
        }

        // ------------------------------------------------------------------ Contract extras

        /// <summary>Yakınlaştırma (1 = yok, 4 = 4x dürbün). FOV yumuşak geçer.</summary>
        public void SetZoom(float zoomFactor)
        {
            if (float.IsNaN(zoomFactor) || float.IsInfinity(zoomFactor))
                zoomFactor = 1f;
            _zoom = Mathf.Clamp(zoomFactor, MinZoom, MaxZoom);
        }

        /// <summary>Eğilme -1 (sol) .. +1 (sağ). Çağrıldıktan sonra motor değeri otomatik okunmaz.</summary>
        public void SetLean(float lean)
        {
            _manualLean = true;
            _leanTarget = float.IsNaN(lean) ? 0f : Mathf.Clamp(lean, -1f, 1f);
        }

        /// <summary>Gövde pivotundan göz yüksekliği (m). Yumuşatılarak uygulanır.</summary>
        public void SetEyeHeight(float height)
        {
            _manualEye = true;
            if (!float.IsNaN(height) && !float.IsInfinity(height))
                _eyeTarget = Mathf.Clamp(height, 0f, 5f);
        }

        /// <summary>Kafa sallantısı: speed01 = 0..1 (koşu hızına göre), grounded = yerde mi.</summary>
        public void SetBob(float speed01, bool grounded)
        {
            _manualBob = true;
            _bobSpeed = float.IsNaN(speed01) ? 0f : Mathf.Clamp01(speed01);
            _bobGrounded = grounded;
        }

        /// <summary>Kalıcı bakış açısını ayarlar (Unity X: + aşağı).</summary>
        public void SetPitch(float pitchDegrees)
        {
            EnsureInitialized();
            _pitch = Mathf.Clamp(Sanitize(pitchDegrees), MinPitch, MaxPitch);
        }

        /// <summary>Geçici ve bekleyen sekmeyi temizler (silah değişimi, doğma).</summary>
        public void ClearRecoil()
        {
            _recoilTarget = Vector2.zero;
            _recoilCurrent = Vector2.zero;
            _recoilVelocityX = 0f;
            _recoilVelocityY = 0f;
            _pendingPermanentPitch = 0f;
        }

        /// <summary>Bakış, sekme, eğilme, sarsıntı ve yakınlaştırmayı sıfırlar (doğma/araçtan iniş).</summary>
        public void ResetView()
        {
            _pitch = 0f;
            ClearRecoil();
            _lean = 0f;
            _leanTarget = 0f;
            _effectiveLean = 0f;
            _leanClearance = 1f;
            _shakeAmplitude = 0f;
            _dip = 0f;
            _dipTarget = 0f;
            _dipVelocity = 0f;
            _bobWeight = 0f;
            _zoom = 1f;
            SnapFieldOfView();
            _eye = _eyeTarget;
            ApplyPose();
        }

        /// <summary>Yere iniş çökmesi (motorun Landed olayına otomatik bağlıdır).</summary>
        public void AddLandingImpact(float impactSpeed)
        {
            if (config == null || float.IsNaN(impactSpeed) || impactSpeed <= 0f)
                return;

            var amount = Mathf.Min(impactSpeed * Mathf.Max(0f, config.landingDipPerSpeed), Mathf.Max(0f, config.landingDipMax));
            _dipTarget = Mathf.Min(_dipTarget, -amount);
        }

        /// <summary>FOV'u yumuşatmadan hedefe getirir (dürbüne anında geçiş).</summary>
        public void SnapFieldOfView()
        {
            _currentFov = TargetFieldOfView();
            ApplyFieldOfView(true);
        }

        /// <summary>Göz yüksekliğini yumuşatmadan hedefe getirir.</summary>
        public void SnapEyeHeight()
        {
            _eye = _eyeTarget;
        }

        // ------------------------------------------------------------------ Update

        private void LateUpdate()
        {
            if (config == null)
                return;

            if (!_bound)
            {
                _bound = true;
                Bind(ResolveDefaultPivot(pitchPivot), null, false);
            }

            var dt = Time.deltaTime;
            if (dt > 0f)
            {
                if (dt > 0.1f)
                    dt = 0.1f;

                AutoFeedFromMotor();
                UpdateRecoil(dt);
                UpdateEye(dt);
                UpdateLean(dt);
                UpdateBob(dt);
                UpdateShake(dt);
                UpdateFieldOfView(dt);
            }

            ApplyPose();
        }

        private void AutoFeedFromMotor()
        {
            if (_motor == null || _manualEye && _manualLean && _manualBob)
                return;

            if (!_manualEye)
                _eyeTarget = _motor.EyeHeight;
            if (!_manualLean)
                _leanTarget = _motor.Lean;
            if (!_manualBob)
            {
                _bobSpeed = _motor.SpeedNormalized;
                _bobGrounded = _motor.IsGrounded;
            }
        }

        private void UpdateRecoil(float dt)
        {
            var recovery = 1f - Mathf.Exp(-Mathf.Max(0f, config.recoilRecovery) * dt);
            _recoilTarget = Vector2.Lerp(_recoilTarget, Vector2.zero, recovery);
            if (_recoilTarget.sqrMagnitude < 1e-8f)
                _recoilTarget = Vector2.zero;

            var kickTime = Mathf.Max(0.005f, config.recoilKickTime);
            _recoilCurrent.x = Mathf.SmoothDamp(_recoilCurrent.x, _recoilTarget.x, ref _recoilVelocityX, kickTime, Mathf.Infinity, dt);
            _recoilCurrent.y = Mathf.SmoothDamp(_recoilCurrent.y, _recoilTarget.y, ref _recoilVelocityY, kickTime, Mathf.Infinity, dt);

            if (_pendingPermanentPitch != 0f)
            {
                var step = _pendingPermanentPitch * (1f - Mathf.Exp(-dt / kickTime));
                if (Mathf.Abs(_pendingPermanentPitch - step) < 0.0005f)
                    step = _pendingPermanentPitch;

                _pitch = Mathf.Clamp(_pitch - step, MinPitch, MaxPitch);
                _pendingPermanentPitch -= step;
            }
        }

        private void UpdateEye(float dt)
        {
            var k = Mathf.Max(0.1f, config.eyeHeightSmoothSpeed);
            _eye = Mathf.Lerp(_eye, _eyeTarget, 1f - Mathf.Exp(-k * dt));
            if (Mathf.Abs(_eye - _eyeTarget) < 0.0005f)
                _eye = _eyeTarget;

            _dipTarget = Mathf.Lerp(_dipTarget, 0f, 1f - Mathf.Exp(-7f * dt));
            _dip = Mathf.SmoothDamp(_dip, _dipTarget, ref _dipVelocity, 0.06f, Mathf.Infinity, dt);
            if (Mathf.Abs(_dip) < 0.0002f && Mathf.Abs(_dipTarget) < 0.0002f)
            {
                _dip = 0f;
                _dipTarget = 0f;
                _dipVelocity = 0f;
            }
        }

        private void UpdateLean(float dt)
        {
            _lean = Mathf.Lerp(_lean, _leanTarget, 1f - Mathf.Exp(-16f * dt));
            if (Mathf.Abs(_lean - _leanTarget) < 0.001f)
                _lean = _leanTarget;

            var allowed = 1f;
            var offset = Mathf.Max(0f, config.leanOffset);
            var pivot = pitchPivot;
            if (Mathf.Abs(_lean) > 0.01f && offset > 0.001f && pivot != null)
            {
                var parent = pivot.parent;
                var origin = parent != null && _drivePivotPosition
                    ? parent.TransformPoint(new Vector3(_pivotBaseLocalPosition.x, _eye + _dip, _pivotBaseLocalPosition.z))
                    : pivot.position;
                var right = parent != null ? parent.right : pivot.right;
                right.y = 0f;
                if (right.sqrMagnitude > 0.0001f)
                {
                    right.Normalize();
                    var direction = _lean > 0f ? right : -right;
                    if (PlayerPhysicsQueries.SphereCastOther(origin, LeanProbeRadius, direction, offset + 0.05f,
                            GameLayers.MovementBlockMask, _ignoreRoot, out var hit))
                    {
                        allowed = Mathf.Clamp01((hit.distance - 0.03f) / offset);
                    }
                }
            }

            // Duvara yaklaşırken anında daral, açılırken yumuşak genişle.
            _leanClearance = allowed < _leanClearance ? allowed : Mathf.MoveTowards(_leanClearance, allowed, 4f * dt);
            _effectiveLean = Mathf.Clamp(_lean, -_leanClearance, _leanClearance);
        }

        private void UpdateBob(float dt)
        {
            var moving = _bobGrounded && _bobSpeed > 0.05f;
            var targetWeight = moving ? Mathf.Clamp01(_bobSpeed * 1.6f) : 0f;
            _bobWeight = Mathf.MoveTowards(_bobWeight, targetWeight, 4f * dt);

            if (_bobWeight > 0f)
            {
                var frequency = Mathf.Max(0f, config.bobFrequency) * Mathf.Lerp(0.75f, 1.35f, _bobSpeed);
                _bobPhase = Mathf.Repeat(_bobPhase + dt * frequency * TwoPi, TwoPi);
            }
        }

        private void UpdateShake(float dt)
        {
            if (_shakeAmplitude <= 0f)
                return;

            _shakeTime += dt;
            _shakeAmplitude = Mathf.MoveTowards(_shakeAmplitude, 0f, Mathf.Max(0.01f, _shakeDecay) * dt);
            if (_shakeTime > 10000f)
                _shakeTime = 0f;
        }

        private float TargetFieldOfView()
        {
            if (_zoom <= 1.0001f && _zoom >= 0.9999f)
                return _baseFov;

            var halfTan = Mathf.Tan(_baseFov * 0.5f * Mathf.Deg2Rad) / _zoom;
            return Mathf.Clamp(2f * Mathf.Atan(halfTan) * Mathf.Rad2Deg, 1f, 170f);
        }

        private void UpdateFieldOfView(float dt)
        {
            var target = TargetFieldOfView();
            var k = Mathf.Max(0.1f, config.fovSmoothSpeed);
            _currentFov = Mathf.Lerp(_currentFov, target, 1f - Mathf.Exp(-k * dt));
            if (Mathf.Abs(_currentFov - target) < 0.01f)
                _currentFov = target;

            ApplyFieldOfView(false);
        }

        private void ApplyFieldOfView(bool force)
        {
            if (!force && Mathf.Abs(_currentFov - _appliedFov) < 0.001f)
                return;

            if (_rig != null)
                _rig.SetFieldOfView(_currentFov);
            else if (_camera != null)
                _camera.fieldOfView = Mathf.Clamp(_currentFov, 1f, 170f);
            else
                return;

            _appliedFov = _currentFov;
        }

        // ------------------------------------------------------------------ Pose

        private Vector3 PivotLocalPosition()
        {
            var offset = config != null ? config.leanOffset : 0.35f;
            var leanX = _effectiveLean * offset;
            var leanY = -Mathf.Abs(_effectiveLean) * offset * LeanVerticalDropFactor;
            return new Vector3(_pivotBaseLocalPosition.x + leanX, _eye + leanY + _dip, _pivotBaseLocalPosition.z);
        }

        private void ApplyPose()
        {
            var pivot = pitchPivot;
            if (pivot == null || config == null)
                return;

            var roll = -_effectiveLean * config.leanAngle;
            var lookRotation = Quaternion.Euler(ViewPitch, _recoilCurrent.y, roll);

            // Sallantı (ADS/dürbünde azalır)
            var bobPosition = Vector3.zero;
            var bobRoll = 0f;
            if (_bobWeight > 0.0001f)
            {
                var zoomDamp = 1f / Mathf.Max(1f, _zoom * _zoom);
                var amplitude = _bobWeight * zoomDamp * Mathf.Lerp(0.7f, 1.25f, _bobSpeed);
                bobPosition = new Vector3(
                    Mathf.Sin(_bobPhase) * config.bobHorizontalAmplitude * amplitude,
                    Mathf.Sin(_bobPhase * 2f) * config.bobVerticalAmplitude * amplitude,
                    0f);
                bobRoll = Mathf.Sin(_bobPhase) * config.bobRollAmplitude * amplitude;
            }

            // Sarsıntı (Perlin)
            var shakePosition = Vector3.zero;
            var shakeEuler = Vector3.zero;
            if (_shakeAmplitude > 0f)
            {
                var magnitude = _shakeAmplitude * _shakeAmplitude;
                var t = _shakeTime * Mathf.Max(0.1f, config.shakeFrequency);
                var angle = config.shakeMaxAngle * magnitude;
                shakeEuler = new Vector3(
                    (Mathf.PerlinNoise(_shakeSeed, t) * 2f - 1f) * angle,
                    (Mathf.PerlinNoise(_shakeSeed + 17.31f, t) * 2f - 1f) * angle,
                    (Mathf.PerlinNoise(_shakeSeed + 41.73f, t) * 2f - 1f) * angle * 0.5f);
                shakePosition = new Vector3(
                    (Mathf.PerlinNoise(_shakeSeed + 73.1f, t) * 2f - 1f),
                    (Mathf.PerlinNoise(_shakeSeed + 91.7f, t) * 2f - 1f),
                    0f) * (ShakePositionScale * magnitude);
            }

            var basePosition = _drivePivotPosition ? PivotLocalPosition() : pivot.localPosition;
            var effectRotation = Quaternion.Euler(shakeEuler.x, shakeEuler.y, shakeEuler.z + bobRoll);

            if (_viewTransform != null)
            {
                if (_drivePivotPosition)
                    pivot.localPosition = basePosition;
                pivot.localRotation = lookRotation;
                _viewTransform.localPosition = _viewBaseLocalPosition + bobPosition + shakePosition;
                _viewTransform.localRotation = _viewBaseLocalRotation * effectRotation;
            }
            else
            {
                if (_drivePivotPosition)
                    pivot.localPosition = basePosition + lookRotation * (bobPosition + shakePosition);
                pivot.localRotation = lookRotation * effectRotation;
            }
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }
    }
}
