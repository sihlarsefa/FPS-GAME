using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Project.Infrastructure.Audio
{
    /// <summary>
    /// Kat edilen yatay mesafeye göre ayak sesi çalar (yürüme 2.2 m, koşu 2.8 m adım; eğilirken/yüzüstüyken daha kısık
    /// ve daha kısa menzilli; havadayken ses yok). Oyuncu ve botlar için aynı bileşen: yalnızca
    /// <c>AddComponent&lt;FootstepEmitter&gt;()</c> yeterlidir — durum kaynağı otomatik bulunur:
    /// <list type="number">
    /// <item><see cref="SetExternalState"/> ile verilen durum (varsa, öncelikli),</item>
    /// <item><see cref="Combatant"/> (ölü, intikal aracında, serbest düşüşte ya da paraşütteyken ses yok; duruş),</item>
    /// <item><see cref="IPlayerMotor"/> (yerde mi, koşuyor mu, duruş),</item>
    /// <item><see cref="NavMeshAgent"/> / <see cref="CharacterController"/> (etkin ve yerde mi),</item>
    /// <item>yedek: dikey hız küçükse yerde kabul edilir.</item>
    /// </list>
    /// Işınlanma (tek karede &gt; 3 m) ve araç hızları (&gt; 11 m/s) adım sayılmaz. Uzun düşüşten sonra iniş sesi
    /// çalar (<see cref="EmitLanding"/>). Kare başına bellek ayırmaz.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FootstepEmitter : MonoBehaviour
    {
        [Header("Adım uzunluğu (m)")]
        public float WalkStride = 2.2f;
        public float SprintStride = 2.8f;
        public float CrouchStride = 1.7f;
        public float ProneStride = 1.2f;

        [Header("Ses")]
        [Range(0f, 1f)] public float Volume = 0.55f;
        public float SprintVolumeScale = 1.3f;
        public float CrouchVolumeScale = 0.4f;
        public float ProneVolumeScale = 0.25f;

        /// <summary>Yerel oyuncunun kendi adımları biraz daha kısık (düşman adımları öne çıksın).</summary>
        public float LocalPlayerVolumeScale = AudioQuality.LocalStepVolumeScale;

        [Header("Duyulma menzili (m)")]
        public float HearingDistance = 32f;
        public float SprintHearingScale = 1.5f;
        public float CrouchHearingScale = 0.45f;
        public float ProneHearingScale = 0.3f;

        /// <summary>Motor yoksa bu yatay hızın üstü koşu sayılır (m/s).</summary>
        [Header("Algılama")]
        public float SprintSpeedThreshold = 6.2f;

        /// <summary>Bu yatay hızın üstü yürüme sayılmaz (araç, itilme).</summary>
        public float MaxFootSpeed = 11f;

        /// <summary>Ayak sesinin çalındığı nokta, nesne kökünün bu kadar üstü.</summary>
        public float FootHeight = 0.05f;

        /// <summary>Havada en az bu kadar kaldıktan sonra yere inince iniş sesi çalar.</summary>
        public bool EmitLanding = true;
        public float LandingMinAirTime = 0.45f;

        private const float TeleportDistance = 3f;
        private const float StillSpeed = 0.25f;
        private const float StillPrimeTime = 0.35f;
        private const float FallbackGroundedVerticalSpeed = 1.5f;
        private const float ResolveRetryInterval = 1f;
        private const int MaxResolveAttempts = 6;

        private IPlayerMotor _motor;
        private Behaviour _motorBehaviour;
        private Combatant _combatant;
        private CharacterController _controller;
        private NavMeshAgent _agent;

        private bool _external;
        private bool _externalGrounded;
        private bool _externalSprinting;
        private Stance _externalStance;

        private Vector3 _lastPosition;
        private bool _hasLastPosition;
        private float _accumulated;
        private float _stillTime;
        private float _airTime;
        private bool _wasGrounded = true;
        private bool _leftFoot;
        private int _resolveAttempts;
        private float _nextResolveTime;
        private bool _resolvedAny;

        /// <summary>true iken hiç ses çalınmaz (ör. araçta, ara sahnede).</summary>
        public bool Muted { get; set; }

        /// <summary>Son karede yerde kabul edildi mi (hata ayıklama).</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>
        /// Durumu dışarıdan verir (kendi hareket sistemini kullanan botlar için). Çağrıldıktan sonra otomatik
        /// algılamaya göre önceliklidir; <see cref="ClearExternalState"/> ile bırakılır.
        /// </summary>
        public void SetExternalState(bool grounded, bool sprinting, Stance stance)
        {
            _external = true;
            _externalGrounded = grounded;
            _externalSprinting = sprinting;
            _externalStance = stance;
        }

        public void ClearExternalState() => _external = false;

        /// <summary>Bileşenleri yeniden arar (ör. motor sonradan eklendiyse).</summary>
        public void Rebind()
        {
            _resolveAttempts = 0;
            Resolve();
        }

        /// <summary>Biriken adım mesafesini sıfırlar (ışınlama/yeniden doğma sonrası).</summary>
        public void ResetStride()
        {
            _accumulated = 0f;
            _stillTime = 0f;
            _airTime = 0f;
            _hasLastPosition = false;
        }

        private void OnEnable()
        {
            ResetStride();
            _wasGrounded = true;
        }

        private void Start()
        {
            Resolve();
        }

        private void Resolve()
        {
            _motor = GetComponentInParent<IPlayerMotor>();
            _motorBehaviour = _motor as Behaviour;
            _combatant = GetComponentInParent<Combatant>();
            _controller = GetComponentInParent<CharacterController>();
            _agent = GetComponentInParent<NavMeshAgent>();
            _isBot = GetComponentInParent<Project.Infrastructure.AI.BotController>() != null;
            // Yalnızca bir hareket kaynağı (motor/ajan/kontrolcü) bulununca aramayı bırak: bileşen, hareket
            // sistemi eklenmeden önce eklenmiş olabilir (Combatant tek başına yerde olup olmadığını söylemez).
            _resolvedAny = _motor != null || _controller != null || _agent != null;
            _resolveAttempts++;
            _nextResolveTime = Time.unscaledTime + ResolveRetryInterval;
        }

        private void Update()
        {
            // Başsız sunucuda (GameAudio kapalı) hiçbir iş yapma.
            if (!GameAudio.Enabled)
            {
                _hasLastPosition = false;
                return;
            }

            if (!_resolvedAny && _resolveAttempts < MaxResolveAttempts && Time.unscaledTime >= _nextResolveTime)
                Resolve();

            var dt = Time.deltaTime;
            var position = transform.position;
            if (!_hasLastPosition)
            {
                _lastPosition = position;
                _hasLastPosition = true;
                return;
            }

            var delta = position - _lastPosition;
            _lastPosition = position;
            if (dt <= 0f)
                return; // duraklatıldı

            var horizontal = Mathf.Sqrt(delta.x * delta.x + delta.z * delta.z);
            if (horizontal > TeleportDistance)
            {
                _accumulated = 0f;
                return;
            }

            if (Muted || !IsAliveAndOnFoot())
            {
                IsGrounded = false;
                _wasGrounded = true; // ölüm/araç sonrası sahte iniş sesi olmasın
                _airTime = 0f;
                _accumulated = 0f;
                return;
            }

            var verticalSpeed = delta.y / dt;
            var grounded = ResolveGrounded(verticalSpeed);
            IsGrounded = grounded;
            if (!grounded)
            {
                _airTime += dt;
                _wasGrounded = false;
                return;
            }

            var stance = ResolveStance();
            if (!_wasGrounded && EmitLanding && _airTime >= LandingMinAirTime)
                PlayLanding(position, stance);
            _wasGrounded = true;
            _airTime = 0f;

            var speed = horizontal / dt;
            if (speed > MaxFootSpeed)
            {
                _accumulated = 0f;
                return;
            }

            var stride = StrideFor(stance, false);
            if (speed < StillSpeed)
            {
                _stillTime += dt;
                // Uzun süre durduktan sonra ilk adım, harekete geçer geçmez gelsin.
                if (_stillTime > StillPrimeTime)
                    _accumulated = Mathf.Max(_accumulated, stride * 0.55f);
                return;
            }

            _stillTime = 0f;
            var sprinting = ResolveSprinting(speed) && stance == Stance.Standing;
            stride = StrideFor(stance, sprinting);
            _accumulated += horizontal;
            if (_accumulated < stride)
                return;

            _accumulated -= stride;
            if (_accumulated > stride)
                _accumulated = 0f;
            PlayStep(position, stance, sprinting);
        }

        private bool IsAliveAndOnFoot()
        {
            if (_combatant == null)
                return true;

            return _combatant.IsAlive && _combatant.DropState == DropState.Landed;
        }

        private bool ResolveGrounded(float verticalSpeed)
        {
            if (_external)
                return _externalGrounded;

            if (_motor != null && (_motorBehaviour == null || _motorBehaviour.isActiveAndEnabled))
            {
                // Araçta oturan oyuncunun kontrolcüsü genelde kapatılır.
                if (_controller != null && !_controller.enabled)
                    return false;
                return _motor.IsGrounded;
            }

            if (_agent != null && _agent.isActiveAndEnabled)
                return _agent.isOnNavMesh && !_agent.isOnOffMeshLink;

            if (_controller != null && _controller.enabled)
                return _controller.isGrounded || Mathf.Abs(verticalSpeed) < FallbackGroundedVerticalSpeed;

            if (_agent != null || _controller != null)
                return false; // var ama kapalı → araçta/ragdoll

            return Mathf.Abs(verticalSpeed) < FallbackGroundedVerticalSpeed;
        }

        private bool ResolveSprinting(float speed)
        {
            if (_external)
                return _externalSprinting;

            if (_motor != null && (_motorBehaviour == null || _motorBehaviour.isActiveAndEnabled))
                return _motor.IsSprinting;

            return speed > SprintSpeedThreshold;
        }

        private Stance ResolveStance()
        {
            if (_external)
                return _externalStance;

            if (_motor != null && (_motorBehaviour == null || _motorBehaviour.isActiveAndEnabled))
                return _motor.CurrentStance;

            return _combatant != null ? _combatant.Stance : Stance.Standing;
        }

        private float StrideFor(Stance stance, bool sprinting)
        {
            switch (stance)
            {
                case Stance.Crouching: return Mathf.Max(0.3f, CrouchStride);
                case Stance.Prone: return Mathf.Max(0.3f, ProneStride);
                default: return Mathf.Max(0.3f, sprinting ? SprintStride : WalkStride);
            }
        }

        private void PlayStep(Vector3 position, Stance stance, bool sprinting)
        {
            float volumeScale;
            float hearingScale;
            float pitchScale;
            switch (stance)
            {
                case Stance.Crouching:
                    volumeScale = CrouchVolumeScale;
                    hearingScale = CrouchHearingScale;
                    pitchScale = 0.95f;
                    break;
                case Stance.Prone:
                    volumeScale = ProneVolumeScale;
                    hearingScale = ProneHearingScale;
                    pitchScale = 0.8f;
                    break;
                default:
                    volumeScale = sprinting ? SprintVolumeScale : 1f;
                    hearingScale = sprinting ? SprintHearingScale : 1f;
                    pitchScale = sprinting ? 1.04f : 1f;
                    break;
            }

            var isLocal = _combatant != null && _combatant.IsLocalPlayer;
            var isBot = !isLocal && _isBot;
            if (isLocal)
                volumeScale *= LocalPlayerVolumeScale;
            else if (isBot)
                volumeScale *= AudioQuality.BotStepVolumeScale;

            var surface = AudioQuality.SampleSurface(position);
            _lastVariant = AudioQuality.NextStepVariant(surface, _lastVariant, Random.value);
            var recipe = AudioQuality.StepVariant(surface, _lastVariant);
            var gait = AudioQuality.GaitVolume(stance, sprinting);
            // Zaten stance/sprint çarpanı (volumeScale) var; yürüme yumuşaklığı için yalnızca ayakta yürümeyi kıs.
            if (stance == Stance.Standing && !sprinting)
                volumeScale *= gait;

            var volume = Volume * volumeScale * recipe.Volume * Random.Range(0.85f, 1f);
            var pitch = pitchScale * recipe.Pitch * Random.Range(0.94f, 1.06f) * (_leftFoot ? 0.97f : 1.03f);
            _leftFoot = !_leftFoot;
            var feet = position + Vector3.up * FootHeight;
            var hearing = Mathf.Max(2f, HearingDistance * hearingScale);
            var id = recipe.Id;
            if (surface == Project.Infrastructure.Vfx.SurfaceKind.Metal) { volume *= 1.15f; hearing *= 1.1f; }
            else if (id == SoundId.FootstepGrass) volume *= 0.8f;
            volume = AudioQuality.FinalStepVolume(volume, isLocal, isBot);
            volume *= HdrMix.AudioMix.HdrGainFor(HdrMix.HdrEventKind.Footstep, feet); // yüksek sesli olay sırasında adımlar pencere altında kalır
            GameAudio.Play(id, feet, volume, pitch, hearing);
            if (recipe.Extra != SoundId.None)
                GameAudio.Play(recipe.Extra, feet, volume * recipe.ExtraVolume / Mathf.Max(0.01f, recipe.Volume) , pitch * 0.97f, hearing);
            if (sprinting || surface == Project.Infrastructure.Vfx.SurfaceKind.Snow)
                Project.Infrastructure.Vfx.GameVfx.FootstepDust(position, surface, sprinting ? 1f : 0.6f);
            _stepSurface = surface;

            // Kıyafet/teçhizat hışırtısı: koşarken her adım, yürürken her iki adımda bir, çömelmede/sürünmede seyrek.
            _stepCount++;
            var rustleEvery = sprinting ? 1 : stance == Stance.Standing ? 2 : 3;
            if (_stepCount % rustleEvery == 0)
                GameAudio.Play(SoundId.ClothRustle, feet + Vector3.up * 0.9f, Volume * 0.35f * volumeScale * Random.Range(0.7f, 1f),
                    Random.Range(0.9f, 1.1f), Mathf.Max(2f, hearing * 0.3f));
            if (AudioQuality.GearJingleOnStep(stance, sprinting, _stepCount))
                GameAudio.Play(SoundId.GearJingle, feet + Vector3.up * 1.0f, Volume * 0.3f * volumeScale * Random.Range(0.7f, 1f),
                    Random.Range(0.92f, 1.08f), Mathf.Max(2f, hearing * 0.5f));
        }

        private int _stepCount;
        private int _lastVariant = -1;
        private bool _isBot;
        private Project.Infrastructure.Vfx.SurfaceKind _stepSurface;

        private void PlayLanding(Vector3 position, Stance stance)
        {
            var heavy = Mathf.Clamp01((_airTime - LandingMinAirTime) / 1.2f);
            var volume = Volume * (0.9f + 0.6f * heavy) * (stance == Stance.Standing ? 1f : 0.7f);
            if (_combatant != null && _combatant.IsLocalPlayer)
                volume *= LocalPlayerVolumeScale;

            var surface = AudioQuality.SampleSurface(position);
            AudioQuality.LandingTweak(surface, out var surfVol, out var surfPitch, out var layer);
            volume *= surfVol;
            var isBot = _isBot && !(_combatant != null && _combatant.IsLocalPlayer);
            if (isBot)
                volume *= AudioQuality.BotStepVolumeScale;
            volume = AudioQuality.FinalStepVolume(volume, false, isBot);

            var feet = position + Vector3.up * FootHeight;
            var landHearing = Mathf.Max(2f, HearingDistance * 1.2f);
            GameAudio.Play(SoundId.Land, feet, Mathf.Clamp01(volume), Random.Range(0.92f, 1.05f) * surfPitch, landHearing);
            GameAudio.Play(layer, feet, Mathf.Clamp01(volume * 0.9f), Random.Range(0.85f, 0.95f), landHearing);
            _accumulated = 0f;
        }
    }
}
