using System;
using Project.Application.Movement;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure.Config;
using UnityEngine;

namespace Project.Infrastructure.Player
{
    /// <summary>
    /// CharacterController tabanlı FPP oyuncu hareketi (sunucu-otoriter mantığa hazır: tüm durum <see cref="ApplyMovement"/>
    /// çağrısına verilen deltaTime ile ilerler, Time.time kullanılmaz).
    /// <para>
    /// • Hızlar: yürüme 4,6 · koşu 7,2 (yalnız ileri) · çömelme 2,4 · yüzüstü 1,1 m/s (config). Geri/yan yürüme biraz yavaştır.<br/>
    /// • Duruş: C çömelmeyi aç/kapa, Ctrl basılı tutunca çömel, Z yüzüstü aç/kapa. Boy (1,8 / 1,2 / 0,6 m) yumuşak değişir;
    ///   kalkmadan önce tavan kontrolü yapılır (yer yoksa sığan en yüksek duruşta kalır, açılınca kendiliğinden kalkar).
    ///   Çömelik/yüzüstüyken Space ayağa kaldırır; koşu tuşuna basmak (ileri girdiyle) çömelme/yüzüstünü bozar.<br/>
    /// • Zıplama yalnız ayaktayken; coyote süresi + girdi tamponu; havada sınırlı kontrol; dik yamaçta kayma;
    ///   yokuş aşağı zemine yapışma; basamak/eğim CharacterController ile.<br/>
    /// • Yana eğilme (Q/E) -1..1 yumuşak; koşarken ve yüzüstüyken kapalı.<br/>
    /// • <see cref="ControlEnabled"/> = false (araçta) hareketi atlar ve çarpıştırıcıyı kapatır (araç fiziğiyle çakışmasın).
    /// </para>
    /// Pivot ayak hizasındadır (controller.center.y = boy/2).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed partial class CharacterControllerMotor : MonoBehaviour, IPlayerMotor
    {
        /// <summary>Yerdeyken zemine bastırma hızı (CharacterController'ın isGrounded'ı güvenilir kalsın).</summary>
        private const float GroundStickSpeed = 2f;

        private const float MaxStepDelta = 0.1f;
        private const float StanceHeightEpsilon = 0.01f;
        private const float LandedMinAirTime = 0.2f;
        private const float RigidbodyPushForce = 1.2f;
        private const float RigidbodyPushMaxMass = 60f;

        [SerializeField] private PlayerMovementConfig config;

        private CharacterController _controller;
        private bool _ownsConfig;
        private bool _initialized;

        // Hareket durumu
        private Vector3 _planarVelocity;
        private float _verticalVelocity;
        private Vector3 _velocity;
        private bool _grounded;
        private bool _onSteepSlope;
        private Vector3 _groundNormal = Vector3.up;
        private float _airTime;
        private float _timeSinceGrounded;
        private float _jumpBufferTimer;
        private float _jumpCooldownTimer;
        private bool _jumpedSinceGrounded;
        private bool _sprintHeldLastFrame;
        private bool _isSprinting;
        private float _speedNormalized;

        // Duruş
        private Stance _stance = Stance.Standing;
        private bool _crouchToggled;
        private bool _proneToggled;
        private float _currentHeight = 1.8f;
        private float _targetHeight = 1.8f;
        private float _appliedHeight = -1f;

        // Duruş geçiş eğrisi (ease-in-out): from -> target, süre boy farkından.
        private float _stanceFrom = 1.8f;
        private float _stanceTrackTarget = 1.8f;
        private float _stanceT = 1f;
        private float _stanceDuration = 0.3f;

        // Stamina / yük / ritim
        private readonly StaminaModel _stamina = new StaminaModel();
        private float _loadFraction;
        private float _armorScore;
        private float _burden;
        private float _stepDistance;
        private bool _wasExhausted;

        // Eğilme / diğer
        private float _lean;
        private float _speedMultiplier = 1f;
        private bool _controlEnabled = true;

        // OnControllerColliderHit ile toplanan en düz alt temas.
        private bool _collectingContacts;
        private float _bestGroundNormalY;
        private Vector3 _bestGroundNormal;

        /// <summary>Stamina tükendi (nefes nefese). PlayerBreathing/WearDriver abone.</summary>
        public event Action StaminaExhausted;

        /// <summary>Stamina eşiği aşıp tükenmişlikten çıkıldı.</summary>
        public event Action StaminaRecovered;

        /// <summary>Bir adım atıldı (mesafe tabanlı, hız/duruşa göre ritim); parametre 0..1 koşu hızına göre şiddet. FootstepEmitter kendi ritmini tutar; bu olay ek abonelere açıktır.</summary>
        public event Action<float> FootstepTaken;

        /// <summary>Yere iniş; parametre çarpma (aşağı) hızı m/s. Düşme hasarı: DamageCalculator.ComputeFallDamage.</summary>
        public event Action<float> Landed;

        /// <summary>Zıplama başladı.</summary>
        public event Action Jumped;

        /// <summary>Etkin duruş değişti.</summary>
        public event Action<Stance> StanceChanged;

        /// <summary>Etkin ayar (Configure çağrılmamışsa varsayılan örnek; asla null değil).</summary>
        public PlayerMovementConfig Config
        {
            get
            {
                EnsureInitialized();
                return config;
            }
        }

        public CharacterController Controller
        {
            get
            {
                if (_controller == null)
                    _controller = GetComponent<CharacterController>();
                return _controller;
            }
        }

        public Stance CurrentStance => _stance;
        public bool IsGrounded => _grounded;
        public bool IsSprinting => _isSprinting;
        public float SpeedNormalized => _speedNormalized;

        /// <summary>Stamina 0..1.</summary>
        public float StaminaNormalized => _stamina.Normalized;

        public float StaminaValue => _stamina.Current;
        public bool IsExhausted => _stamina.Exhausted;

        /// <summary>Nefes sesi şiddeti 0..1 (PlayerBreathing okur).</summary>
        public float BreathingIntensity => MovementRules.BreathingIntensity(_stamina.Normalized, _stamina.Exhausted);

        /// <summary>Yük/zırh etkisi 0..1 (0 = yüksüz).</summary>
        public float LoadBurden => _burden;

        /// <summary>Anlık adım ritmi (adım/sn).</summary>
        public float StepsPerSecond =>
            _grounded ? MovementRules.StepsPerSecond(HorizontalSpeed, MovementRules.StrideLength(_stance == Stance.Crouching, _stance == Stance.Prone, _isSprinting)) : 0f;

        /// <summary>
        /// Taşınan yükü bildirir: envanter doluluk oranı (CurrentWeight/Capacity) ve zırh puanı (yelek seviyesi + kask seviyesi/2).
        /// Hız, ivme (atalet), zıplama ve stamina harcamasını etkiler. Sunucu-otoriter modda aynı değerler envanterden gelir.
        /// </summary>
        public void SetLoad(float loadFraction, float armorScore)
        {
            _loadFraction = float.IsNaN(loadFraction) ? 0f : Mathf.Max(0f, loadFraction);
            _armorScore = float.IsNaN(armorScore) ? 0f : Mathf.Max(0f, armorScore);
            RecomputeBurden();
        }

        private void RecomputeBurden()
        {
            var scale = config != null ? config.loadEffectScale : 1f;
            _burden = Mathf.Clamp01(MovementRules.LoadBurden(_loadFraction, _armorScore) * scale);
        }

        /// <summary>Staminayı doldurur (doğma/iyileşme).</summary>
        public void RefillStamina() => _stamina.Refill();

        /// <summary>-1 sol, +1 sağ (yumuşatılmış).</summary>
        public float Lean => _lean;

        /// <summary>İyileşme/boost etkileri için hız çarpanı (0..3).</summary>
        public float SpeedMultiplier
        {
            get => _speedMultiplier;
            set => _speedMultiplier = float.IsNaN(value) ? 1f : Mathf.Clamp(value, 0f, 3f);
        }

        /// <summary>Kapsülün şu anki (geçişte ara değer) boyu, m.</summary>
        public float CurrentHeight => _currentHeight;

        /// <summary>Ayaktan göz yüksekliği (m): duruşa göre 1,62 / 1,05 / 0,35, boy geçişiyle yumuşak.</summary>
        public float EyeHeight
        {
            get
            {
                var c = config;
                return c != null ? c.EyeHeightForCapsule(_currentHeight) : 1.62f;
            }
        }

        /// <summary>Gerçekleşen dünya hızı (son hareketteki konum farkından), m/s.</summary>
        public Vector3 Velocity => _velocity;

        /// <summary>Yatay gerçek hız, m/s.</summary>
        public float HorizontalSpeed => new Vector2(_velocity.x, _velocity.z).magnitude;

        /// <summary>Son yere değmeden beri havada geçen süre (s).</summary>
        public float AirTime => _airTime;

        /// <summary>Basılan zeminin normali (yerdeyken).</summary>
        public Vector3 GroundNormal => _groundNormal;

        /// <summary>Yürünemeyecek kadar dik yüzeyde kayıyor.</summary>
        public bool IsOnSteepSlope => _onSteepSlope;

        /// <summary>Hedef duruşa geçiş sürüyor mu.</summary>
        public bool IsChangingStance => Mathf.Abs(_currentHeight - _targetHeight) > StanceHeightEpsilon;

        /// <summary>
        /// false: hareket/yerçekimi atlanır, hızlar sıfırlanır, eğilme ve duruş tuşları sıfırlanır ve CharacterController kapatılır
        /// (araç koltuğuna bağlıyken araç fiziğiyle çarpışmasın). true: controller yeniden açılır.
        /// </summary>
        public bool ControlEnabled
        {
            get => _controlEnabled;
            set
            {
                EnsureInitialized();
                if (_controlEnabled == value)
                    return;

                _controlEnabled = value;
                ResetMotion();

                if (!value)
                {
                    _lean = 0f;
                    _crouchToggled = false;
                    _proneToggled = false;
                }

                var controller = Controller;
                if (controller != null)
                    controller.enabled = value;
            }
        }

        // ------------------------------------------------------------------ Lifecycle

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnDestroy()
        {
            ReleaseOwnedConfig();
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;
            _controller = GetComponent<CharacterController>();
            if (config == null)
            {
                config = PlayerMovementConfig.CreateDefault();
                _ownsConfig = true;
            }

            ApplyControllerSettings();
            _stamina.Configure(config.ToStaminaTuning());
            _stance = Stance.Standing;
            _targetHeight = config.HeightFor(_stance);
            _currentHeight = _targetHeight;
            ApplyShape(true);
        }

        /// <summary>Ayarı uygular (null = varsayılanda kal). Mevcut duruşun boyuna anında geçer.</summary>
        public void Configure(PlayerMovementConfig movementConfig)
        {
            EnsureInitialized();
            if (movementConfig != null && movementConfig != config)
            {
                ReleaseOwnedConfig();
                config = movementConfig;
            }

            ApplyControllerSettings();
            _stamina.Configure(config.ToStaminaTuning());
            RecomputeBurden();
            _targetHeight = config.HeightFor(_stance);
            _currentHeight = _targetHeight;
            ApplyShape(true);
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

        private void ApplyControllerSettings()
        {
            var controller = Controller;
            if (controller == null || config == null)
                return;

            var minHeight = Mathf.Max(0.2f, Mathf.Min(config.proneHeight, Mathf.Min(config.crouchHeight, config.standingHeight)));
            controller.radius = Mathf.Clamp(config.radius, 0.05f, minHeight * 0.5f);
            controller.slopeLimit = Mathf.Clamp(config.slopeLimit, 0f, 89f);
            controller.stepOffset = _grounded ? GroundStepOffset() : 0f;
            controller.skinWidth = Mathf.Clamp(config.skinWidth, 0.005f, controller.radius * 0.5f);
            controller.minMoveDistance = 0f;
            controller.enableOverlapRecovery = true;
            _appliedHeight = -1f;
        }

        private float GroundStepOffset()
        {
            var controller = Controller;
            var radius = controller != null ? controller.radius : 0.3f;
            return Mathf.Clamp(config.stepOffset, 0f, _currentHeight + radius * 2f - 0.01f);
        }

        // ------------------------------------------------------------------ IPlayerMotor

        /// <summary>Gövdeyi yerel yukarı ekseni etrafında döndürür; parametre DERECE (hassasiyet çağıran tarafta uygulanmış).</summary>
        public void ApplyRotation(float yawDelta)
        {
            if (float.IsNaN(yawDelta) || float.IsInfinity(yawDelta) || yawDelta == 0f)
                return;

            transform.Rotate(0f, yawDelta, 0f, Space.Self);
        }

        public void ApplyMovement(MovementInputState input, float deltaTime)
        {
            EnsureInitialized();
            if (deltaTime <= 0f || float.IsNaN(deltaTime))
                return;

            var dt = Mathf.Min(deltaTime, MaxStepDelta);
            var controller = Controller;
            if (!_controlEnabled || controller == null || !controller.enabled || !isActiveAndEnabled)
            {
                _lean = Mathf.MoveTowards(_lean, 0f, config.leanSpeed * dt);
                _isSprinting = false;
                _speedNormalized = 0f;
                _velocity = Vector3.zero;
                return;
            }

            // --- Zamanlayıcılar ve tampon
            _jumpCooldownTimer = Mathf.Max(0f, _jumpCooldownTimer - dt);
            _jumpBufferTimer = Mathf.Max(0f, _jumpBufferTimer - dt);
            _slideCooldown = Mathf.Max(0f, _slideCooldown - dt);
            if (input.Jump)
                _jumpBufferTimer = Mathf.Max(0.0001f, config.jumpBufferTime);

            var forward = Sanitize(input.Forward);
            var right = Sanitize(input.Right);
            var sprintPressed = input.Sprint && !_sprintHeldLastFrame;
            _sprintHeldLastFrame = input.Sprint;

            // --- Tırmanma sürüyorsa yalnız onu ilerlet
            if (_mantling)
            {
                TickMantle(controller, dt);
                return;
            }

            // --- Duruş
            var stanceBefore = _stance;
            var wasSprintingBefore = _isSprinting;
            UpdateStanceToggles(input, forward, sprintPressed);
            ResolveStance(input.Crouch);
            BeginSlideIfNeeded(stanceBefore, wasSprintingBefore);
            UpdateHeight(dt);

            // --- Engel üstü tırmanma (zıplama tuşu + ileri girdi + ≤1,2 m engel)
            if (_jumpBufferTimer > 0f && forward > 0.3f && !_proneToggled && TryStartMantle(controller))
            {
                TickMantle(controller, dt);
                return;
            }

            // --- Koşu
            var standingReady = _stance == Stance.Standing && _currentHeight >= config.standingHeight - 0.08f;
            var wantsSprint = input.Sprint && forward >= config.sprintForwardThreshold && standingReady
                              && AdsMovementRules.SprintAllowed(_adsProgress)
                              && _stamina.CanSprint(_isSprinting);
            var sprinting = wantsSprint && (_grounded || _isSprinting);

            // --- Eğilme
            var leanTarget = 0f;
            if (!sprinting && _stance != Stance.Prone)
                leanTarget = (input.LeanRight ? 1f : 0f) - (input.LeanLeft ? 1f : 0f);
            if (_sliding)
                leanTarget = 0f;
            leanTarget = ClampLeanToClearance(leanTarget);
            // Ağır yükte eğilme biraz yavaş.
            _lean = Mathf.MoveTowards(_lean, leanTarget,
                LeanRules.Speed(config.leanSpeed, _burden, _adsProgress, Mathf.Abs(leanTarget) < Mathf.Abs(_lean)) * dt);

            // --- İstenen yatay hız
            var wish = ComputeWishVelocity(forward, right, sprinting);
            var hasInput = wish.sqrMagnitude > 0.0001f;
            if (_grounded && !_onSteepSlope)
            {
                var accel = hasInput ? config.groundAcceleration : config.groundDeceleration;
                accel *= MovementRules.LoadAccelFactor(_burden) * AdsMovementRules.AccelerationFactor(_adsProgress);
                // Koşuya geçiş ve koşudan durma ağır: gerçek atalet hissi.
                if (sprinting && hasInput)
                    accel *= 0.72f;
                else if (!hasInput && _isSprinting)
                    accel *= 0.8f;
                _planarVelocity = Vector3.MoveTowards(_planarVelocity, wish, Mathf.Max(0f, accel) * dt);
            }
            else if (hasInput)
            {
                var airAccel = Mathf.Max(0f, config.airAcceleration) * Mathf.Clamp01(config.airControl);
                _planarVelocity = Vector3.MoveTowards(_planarVelocity, wish, airAccel * dt);
            }

            TickSlide(dt, input);

            // --- Dikey hız + zıplama
            var jumpedThisFrame = false;
            if (_jumpBufferTimer > 0f && CanJump())
            {
                _verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(config.gravity) * Mathf.Max(0f, config.jumpHeight))
                                    * MovementRules.LoadJumpFactor(_burden);
                SpendStamina(MovementRules.JumpStaminaCost);
                _jumpBufferTimer = 0f;
                _jumpedSinceGrounded = true;
                _grounded = false;
                jumpedThisFrame = true;
                SetStepOffsetForAir(true);
                Jumped?.Invoke();
            }
            else if (_grounded && !_onSteepSlope && _verticalVelocity <= 0f)
            {
                _verticalVelocity = -GroundStickSpeed;
            }
            else
            {
                _verticalVelocity += config.gravity * dt;
                var maxDown = _grounded && _onSteepSlope
                    ? Mathf.Max(GroundStickSpeed, config.steepSlideSpeed)
                    : config.maxFallSpeed;
                if (_verticalVelocity < -maxDown)
                    _verticalVelocity = -maxDown;
            }

            // --- Hareket
            var displacement = _planarVelocity * dt;
            displacement.y = _verticalVelocity * dt;
            if (_onSteepSlope && _grounded && !jumpedThisFrame)
            {
                var slide = Vector3.ProjectOnPlane(Vector3.down, _groundNormal);
                if (slide.sqrMagnitude > 0.0001f)
                    displacement += slide.normalized * (config.steepSlideSpeed * dt);
            }

            var wasGrounded = _grounded;
            var impactSpeed = Mathf.Max(0f, -_verticalVelocity);
            var startPosition = transform.position;

            BeginContactCollection();
            var flags = controller.Move(displacement);
            var groundedNow = controller.isGrounded || (flags & CollisionFlags.Below) != 0;

            // Yokuş aşağı/basamak inişte zemine yapış.
            if (!groundedNow && wasGrounded && !jumpedThisFrame && _verticalVelocity <= 0f)
                groundedNow = TrySnapToGround(controller);

            EndContactCollection(groundedNow);

            // --- Sonuçlar
            var moved = transform.position - startPosition;
            _velocity = moved / dt;

            if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
                _verticalVelocity = 0f;

            if ((flags & CollisionFlags.Sides) != 0 && !_onSteepSlope)
            {
                // Duvara karşı biriken hızı gerçek hıza indir (havada duvar kenarından fırlamasın).
                // Dik yamaçta atlanır: gerçek hız kayma payını içerir, kalıcı hıza eklenmemeli.
                var actualPlanar = new Vector3(_velocity.x, 0f, _velocity.z);
                if (actualPlanar.sqrMagnitude < _planarVelocity.sqrMagnitude)
                    _planarVelocity = actualPlanar;
            }

            UpdateGroundedState(groundedNow, impactSpeed, dt);
            TickFeel(dt, impactSpeed, !wasGrounded && _grounded);

            var horizontal = new Vector2(_velocity.x, _velocity.z).magnitude;
            var sprintRef = Mathf.Max(0.01f, config.sprintSpeed);
            _speedNormalized = Mathf.Clamp01(horizontal / sprintRef);
            _isSprinting = sprinting && horizontal > 0.5f;

            TickStaminaAndSteps(dt, horizontal);
        }

        private void SpendStamina(float cost)
        {
            if (_stamina.Spend(cost))
                NotifyExhaustion();
        }

        private void NotifyExhaustion()
        {
            if (_stamina.Exhausted && !_wasExhausted)
            {
                _wasExhausted = true;
                StaminaExhausted?.Invoke();
            }
        }

        private void TickStaminaAndSteps(float dt, float horizontal)
        {
            var moving = horizontal > 0.4f;
            var regen = MovementRules.RegenMultiplier(moving, _stance != Stance.Standing, _stance == Stance.Prone);
            if (_stamina.Tick(dt, _isSprinting, regen, MovementRules.LoadDrainMultiplier(_burden) * _fatigue.DrainMultiplier))
                NotifyExhaustion();

            if (_wasExhausted && !_stamina.Exhausted)
            {
                _wasExhausted = false;
                StaminaRecovered?.Invoke();
            }

            // Adım ritmi: kat edilen mesafe / adım uzunluğu (hıza bağlı, durunca sıfırlanır).
            if (_grounded && !_mantling && horizontal > 0.6f && !_sliding)
            {
                _stepDistance += horizontal * dt;
                var stride = MovementRules.StrideLength(_stance == Stance.Crouching, _stance == Stance.Prone, _isSprinting);
                if (_stepDistance >= stride)
                {
                    _stepDistance -= stride;
                    FootstepTaken?.Invoke(Mathf.Clamp01(horizontal / Mathf.Max(0.01f, config.sprintSpeed)));
                }
            }
            else if (!_grounded || horizontal <= 0.6f)
            {
                _stepDistance = 0f;
            }
        }

        /// <summary>Eğilmeyi yan boşluğa göre sınırlar (duvara gömülmesin): yan ışın, eğilme ofsetini karşılamıyorsa oran düşer.</summary>
        private float ClampLeanToClearance(float leanTarget)
        {
            if (Mathf.Abs(leanTarget) < 0.01f)
                return leanTarget;

            var offset = Mathf.Max(0.01f, config.leanOffset);
            var side = leanTarget > 0f ? transform.right : -transform.right;
            var origin = transform.position + Vector3.up * Mathf.Max(0.3f, _currentHeight * 0.8f);
            if (!PlayerPhysicsQueries.SphereCastOther(origin, 0.14f, side, offset + 0.05f, GameLayers.MovementBlockMask, transform, out var hit))
                return leanTarget;

            var allowed = MovementRules.LeanAllowed(hit.distance, offset);
            return leanTarget * allowed;
        }

        // ------------------------------------------------------------------ Public extras

        /// <summary>Konum ve yön belirler (controller kapatılıp açılır, fizik konumu eşitlenir). Hızlar sıfırlanır.</summary>
        public void Teleport(Vector3 position, float yawDegrees)
        {
            EnsureInitialized();
            if (!IsFinite(position))
                return;

            var controller = Controller;
            if (controller != null)
                controller.enabled = false;

            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, float.IsNaN(yawDegrees) ? 0f : yawDegrees, 0f));

            // Kontrol kapalıysa (araçta) controller kapalı kalır; açılınca fizik konumu transform'a eşitlenir.
            if (controller != null)
                controller.enabled = _controlEnabled;

            ResetMotion();
        }

        /// <summary>
        /// Girdi/hız durumunu değiştirmeden ham yer değiştirme (itme, platform). Controller etkinse çarpışmalı
        /// (CharacterController.Move), değilse doğrudan transform taşınır.
        /// </summary>
        public void MoveRaw(Vector3 displacement)
        {
            EnsureInitialized();
            if (!IsFinite(displacement) || displacement.sqrMagnitude <= 0f)
                return;

            var controller = Controller;
            if (controller != null && controller.enabled && gameObject.activeInHierarchy)
                controller.Move(displacement);
            else
                transform.position += displacement;
        }

        /// <summary>Duruşu doğrudan ayarlar (doğma, araçtan iniş). immediate: boy anında değişir (tavan izin verirse).</summary>
        public void SetStance(Stance stance, bool immediate)
        {
            EnsureInitialized();
            _proneToggled = stance == Stance.Prone;
            _crouchToggled = stance == Stance.Crouching;
            ResolveStance(false);
            if (immediate)
            {
                _currentHeight = _targetHeight;
                ApplyShape(true);
            }
        }

        /// <summary>Tüm hızları ve hava/zıplama durumunu sıfırlar.</summary>
        public void ResetMotion()
        {
            _planarVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _velocity = Vector3.zero;
            _airTime = 0f;
            _timeSinceGrounded = 0f;
            _jumpBufferTimer = 0f;
            _jumpedSinceGrounded = false;
            _grounded = false;
            _onSteepSlope = false;
            _groundNormal = Vector3.up;
            _isSprinting = false;
            _speedNormalized = 0f;
            _sliding = false;
            _mantling = false;
            _slideCooldown = 0f;
            _stepDistance = 0f;
        }

        /// <summary>Ayağa kalkmak için yeterli tavan boşluğu var mı.</summary>
        public bool CanStandUp => HasClearanceFor(config != null ? config.standingHeight : 1.8f);

        // ------------------------------------------------------------------ Stance

        private void UpdateStanceToggles(MovementInputState input, float forward, bool sprintPressed)
        {
            if (input.ProneToggle)
            {
                if (_proneToggled)
                {
                    _proneToggled = false;
                    _crouchToggled = false;
                }
                else if (_grounded)
                {
                    _proneToggled = true;
                    _crouchToggled = false;
                }
            }

            if (input.CrouchToggle)
            {
                if (_proneToggled)
                {
                    _proneToggled = false;
                    _crouchToggled = true;
                }
                else
                {
                    _crouchToggled = !_crouchToggled;
                }
            }

            // Çömelik/yüzüstüyken Space: ayağa kalk (zıplama tüketilir).
            if (_jumpBufferTimer > 0f && (_proneToggled || _crouchToggled) && _grounded && !input.Crouch)
            {
                _proneToggled = false;
                _crouchToggled = false;
                _jumpBufferTimer = 0f;
            }

            // Koşu tuşuna basmak (ileri girdiyle) çömelmeyi / yüzüstünü bozar (tavan izin verirse ResolveStance kaldırır).
            if (sprintPressed && (_crouchToggled || _proneToggled) && !input.Crouch && _grounded
                && forward >= config.sprintForwardThreshold)
            {
                _crouchToggled = false;
                _proneToggled = false;
            }
        }

        private void ResolveStance(bool crouchHeld)
        {
            Stance desired;
            if (_proneToggled)
                desired = Stance.Prone;
            else if (_crouchToggled || crouchHeld)
                desired = Stance.Crouching;
            else
                desired = Stance.Standing;

            var resolved = desired;
            if (StanceRank(desired) < StanceRank(_stance))
            {
                // Daha yükseğe çıkılıyor: tavan kontrolü; olmazsa bir alt duruşu dene.
                if (!HasClearanceFor(config.HeightFor(desired)))
                {
                    resolved = _stance;
                    if (desired == Stance.Standing && _stance == Stance.Prone
                        && HasClearanceFor(config.HeightFor(Stance.Crouching)))
                    {
                        resolved = Stance.Crouching;
                    }
                }
            }

            if (resolved != _stance)
            {
                _stance = resolved;
                StanceChanged?.Invoke(_stance);
            }

            _targetHeight = config.HeightFor(_stance);
        }

        private static int StanceRank(Stance stance)
        {
            switch (stance)
            {
                case Stance.Prone:
                    return 2;
                case Stance.Crouching:
                    return 1;
                default:
                    return 0;
            }
        }

        private void UpdateHeight(float dt)
        {
            if (Mathf.Abs(_currentHeight - _targetHeight) <= StanceHeightEpsilon)
            {
                if (_currentHeight != _targetHeight)
                {
                    _currentHeight = _targetHeight;
                    ApplyShape(false);
                }

                _stanceTrackTarget = _targetHeight;
                _stanceFrom = _currentHeight;
                _stanceT = 1f;
                return;
            }

            // Hedef değiştiyse yeni ease-in-out geçişi başlat (boy farkına orantılı süre).
            if (!Mathf.Approximately(_stanceTrackTarget, _targetHeight))
            {
                _stanceTrackTarget = _targetHeight;
                _stanceFrom = _currentHeight;
                _stanceT = 0f;
                _stanceDuration = MovementRules.StanceDuration(_currentHeight, _targetHeight, config.stanceTransitionSpeed);
            }

            _stanceT = Mathf.Min(1f, _stanceT + dt / Mathf.Max(0.01f, _stanceDuration));
            var next = Mathf.Lerp(_stanceFrom, _stanceTrackTarget, MovementRules.StanceEase(_stanceT));
            if (_stanceT >= 1f || Mathf.Abs(next - _targetHeight) <= StanceHeightEpsilon)
                next = _targetHeight;

            _currentHeight = next;
            ApplyShape(false);
        }

        private void ApplyShape(bool force)
        {
            var controller = Controller;
            if (controller == null)
                return;

            if (!force && Mathf.Abs(_appliedHeight - _currentHeight) < 0.0005f)
                return;

            var radius = controller.radius;
            var height = Mathf.Max(_currentHeight, radius * 2f);
            controller.height = height;
            controller.center = new Vector3(0f, height * 0.5f, 0f);
            if (_grounded || force)
                controller.stepOffset = GroundStepOffset();
            _appliedHeight = _currentHeight;
        }

        /// <summary>Ayaktan verilen boya kadar kafa üstünde boşluk var mı (kendi çarpıştırıcıları hariç).</summary>
        private bool HasClearanceFor(float height)
        {
            var controller = Controller;
            if (controller == null || height <= _currentHeight + StanceHeightEpsilon)
                return true;

            var radius = controller.radius * 0.92f;
            var feet = transform.position + Vector3.up * controller.skinWidth;
            var bottom = feet + Vector3.up * Mathf.Max(radius, _currentHeight - controller.radius);
            var top = feet + Vector3.up * Mathf.Max(radius, height - controller.radius + 0.02f);
            return !PlayerPhysicsQueries.OverlapsOther(bottom, top, radius, GameLayers.MovementBlockMask, transform);
        }

        // ------------------------------------------------------------------ Movement helpers

        private Vector3 ComputeWishVelocity(float forward, float right, bool sprinting)
        {
            var input = new Vector2(right, forward);
            if (input.sqrMagnitude > 1f)
                input.Normalize();
            if (input.sqrMagnitude < 0.0001f)
                return Vector3.zero;

            var speed = sprinting ? config.sprintSpeed : config.SpeedFor(_stance);
            if (!sprinting)
            {
                var direction = input.normalized;
                var factor = 1f;
                if (direction.y < 0f)
                    factor *= Mathf.Lerp(1f, config.backwardSpeedFactor, -direction.y);
                factor *= Mathf.Lerp(1f, config.strafeSpeedFactor, Mathf.Abs(direction.x) * (1f - Mathf.Abs(direction.y)));
                speed *= factor;
            }

            speed *= _speedMultiplier;
            speed *= MovementRules.LoadSpeedFactor(_burden);
            speed *= MovementRules.ExhaustedSpeedFactor(_stamina.Exhausted);
            speed *= MovementRules.StanceTransitionSpeedFactor(_currentHeight, _targetHeight);
            speed *= AdsSpeedFactor(Mathf.Abs(input.x) * (1f - Mathf.Abs(input.y)));
            speed *= LeanRules.MoveSpeedFactor(_lean);

            var fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f)
                fwd = Vector3.forward;
            fwd.Normalize();
            var rgt = new Vector3(fwd.z, 0f, -fwd.x);

            var dir = fwd * input.y + rgt * input.x;
            if (config.slopeSpeedModifier && _grounded && !_onSteepSlope)
            {
                var downhill = Vector3.ProjectOnPlane(Vector3.down, _groundNormal);
                if (downhill.sqrMagnitude > 0.0001f && dir.sqrMagnitude > 0.0001f)
                {
                    var slopeDeg = Vector3.Angle(_groundNormal, Vector3.up);
                    var dot = Vector3.Dot(dir.normalized, downhill.normalized);
                    speed *= MovementRules.SlopeSpeedFactor(slopeDeg, dot);
                }
            }

            return dir * speed;
        }

        private bool CanJump()
        {
            if (_jumpCooldownTimer > 0f || _jumpedSinceGrounded || _onSteepSlope)
                return false;
            if (_stance != Stance.Standing || _proneToggled || _crouchToggled)
                return false;
            if (_currentHeight < config.standingHeight - 0.08f)
                return false;
            if (!_stamina.CanAfford(MovementRules.JumpStaminaCost))
                return false;

            return _grounded || _timeSinceGrounded <= Mathf.Max(0f, config.coyoteTime);
        }

        private bool TrySnapToGround(CharacterController controller)
        {
            var distance = Mathf.Max(0f, config.groundSnapDistance);
            if (distance <= 0f)
                return false;

            var radius = controller.radius * 0.9f;
            var origin = transform.position + Vector3.up * (controller.radius + controller.skinWidth);
            if (!PlayerPhysicsQueries.SphereCastOther(origin, radius, Vector3.down, distance + controller.skinWidth,
                    GameLayers.MovementBlockMask, transform, out var hit))
            {
                return false;
            }

            if (Vector3.Angle(hit.normal, Vector3.up) > controller.slopeLimit + 1f)
                return false;

            var flags = controller.Move(Vector3.down * (hit.distance + controller.skinWidth));
            return controller.isGrounded || (flags & CollisionFlags.Below) != 0;
        }

        private void UpdateGroundedState(bool groundedNow, float impactSpeed, float dt)
        {
            var wasGrounded = _grounded;
            _grounded = groundedNow;

            if (groundedNow)
            {
                if (!wasGrounded)
                {
                    var airTime = _airTime;
                    _jumpedSinceGrounded = false;
                    _jumpCooldownTimer = Mathf.Max(_jumpCooldownTimer, config.jumpCooldown);
                    SetStepOffsetForAir(false);
                    // Sert iniş: nefes kesilir, yatay hız söner (diz kırılması).
                    SpendStamina(MovementRules.LandingStaminaCost(impactSpeed));
                    if (impactSpeed > 8f)
                        NoteWeaponExit(SprintExitKind.HardLanding);
                    _planarVelocity *= MovementRules.LandingVelocityKeep(impactSpeed);
                    if (impactSpeed >= config.landedEventMinSpeed || airTime >= LandedMinAirTime)
                        Landed?.Invoke(impactSpeed);
                }

                _airTime = 0f;
                _timeSinceGrounded = 0f;
                if (_verticalVelocity < 0f && !_onSteepSlope)
                    _verticalVelocity = -GroundStickSpeed;
            }
            else
            {
                if (wasGrounded)
                {
                    SetStepOffsetForAir(true);
                    // Kenardan yürüyerek düşüş: bastırma hızını sıfırla, gerçek düşüş 0'dan başlasın.
                    if (_verticalVelocity < 0f)
                        _verticalVelocity = 0f;
                }

                _airTime += dt;
                _timeSinceGrounded += dt;
                _onSteepSlope = false;
            }
        }

        private void SetStepOffsetForAir(bool airborne)
        {
            var controller = Controller;
            if (controller == null)
                return;

            var value = airborne ? 0f : GroundStepOffset();
            if (!Mathf.Approximately(controller.stepOffset, value))
                controller.stepOffset = value;
        }

        // ------------------------------------------------------------------ Contacts

        private void BeginContactCollection()
        {
            _collectingContacts = true;
            _bestGroundNormalY = -2f;
            _bestGroundNormal = Vector3.up;
        }

        private void EndContactCollection(bool grounded)
        {
            _collectingContacts = false;
            if (grounded && _bestGroundNormalY > -1.5f)
            {
                _groundNormal = _bestGroundNormal;
                var controller = Controller;
                var limit = controller != null ? controller.slopeLimit : 45f;
                _onSteepSlope = Vector3.Angle(_groundNormal, Vector3.up) > limit + 1f;
            }
            else if (grounded)
            {
                _groundNormal = Vector3.up;
                _onSteepSlope = false;
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit == null)
                return;

            if (_collectingContacts)
            {
                var controller = Controller;
                var footLimit = transform.position.y + (controller != null ? controller.radius : 0.3f) * 0.95f;
                var normal = hit.normal;
                if (hit.point.y <= footLimit && normal.y > _bestGroundNormalY)
                {
                    _bestGroundNormalY = normal.y;
                    _bestGroundNormal = normal;
                }
            }

            // Hafif fizik nesnelerini it (el bombası, varil...). Araçlar gibi ağır gövdeler etkilenmez.
            var body = hit.rigidbody;
            if (body == null || body.isKinematic || body.mass > RigidbodyPushMaxMass || hit.moveDirection.y < -0.3f)
                return;

            var push = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            if (push.sqrMagnitude < 0.0001f)
                return;

            body.AddForceAtPosition(push.normalized * RigidbodyPushForce, hit.point, ForceMode.Impulse);
        }

        // ------------------------------------------------------------------ Utils

        private static float Sanitize(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0f;
            return Mathf.Clamp(value, -1f, 1f);
        }

        private static bool IsFinite(Vector3 v)
        {
            return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)
                     || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        }
    }

    /// <summary>Oyuncu hareket/kamera fizik sorguları: paylaşılan tamponlar (GC yok), kendi hiyerarşisini yok sayar.</summary>
    internal static class PlayerPhysicsQueries
    {
        private const int BufferSize = 24;
        private static readonly Collider[] OverlapBuffer = new Collider[BufferSize];
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[BufferSize];

        public static bool OverlapsOther(Vector3 point0, Vector3 point1, float radius, int mask, Transform self)
        {
            var count = Physics.OverlapCapsuleNonAlloc(point0, point1, radius, OverlapBuffer, mask, QueryTriggerInteraction.Ignore);
            var found = false;
            for (var i = 0; i < count; i++)
            {
                var other = OverlapBuffer[i];
                OverlapBuffer[i] = null;
                if (found || other == null || IsSelf(other, self))
                    continue;
                found = true;
            }

            return found;
        }

        /// <summary>En yakın (başlangıçta iç içe olmayan) yabancı isabeti döndürür.</summary>
        public static bool SphereCastOther(Vector3 origin, float radius, Vector3 direction, float distance, int mask,
            Transform self, out RaycastHit nearest)
        {
            nearest = default;
            if (distance <= 0f)
                return false;

            var count = Physics.SphereCastNonAlloc(origin, radius, direction, HitBuffer, distance, mask, QueryTriggerInteraction.Ignore);
            var best = float.MaxValue;
            var found = false;
            for (var i = 0; i < count; i++)
            {
                var hit = HitBuffer[i];
                var other = hit.collider;
                if (other == null || IsSelf(other, self))
                    continue;

                // Başlangıçta iç içe olan temaslar (mesafe 0) yön bilgisi taşımaz.
                if (hit.distance <= 0f && hit.point == Vector3.zero)
                    continue;

                if (hit.distance < best)
                {
                    best = hit.distance;
                    nearest = hit;
                    found = true;
                }
            }

            return found;
        }

        private static bool IsSelf(Collider other, Transform self)
        {
            if (self == null)
                return false;

            var t = other.transform;
            return t == self || t.IsChildOf(self);
        }
    }
}
