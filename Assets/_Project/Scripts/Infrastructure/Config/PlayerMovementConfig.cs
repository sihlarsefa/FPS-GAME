using UnityEngine;

namespace Project.Infrastructure.Config
{
    /// <summary>
    /// Oyuncu hareketi ve FPP kamera ayarları. Mevcut alan adları (walkSpeed, sprintSpeed, crouchSpeed, jumpHeight, gravity,
    /// mouseSensitivity, minPitch, maxPitch, standingHeight, crouchHeight, stanceTransitionSpeed) diskteki .asset ile uyum için
    /// korunur. Atanmamışsa <see cref="CreateDefault"/> ile çalışma zamanında varsayılan örnek üretilir.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerMovementConfig", menuName = "Project/Player Movement Config")]
    public sealed class PlayerMovementConfig : ScriptableObject
    {
        /// <summary>
        /// Veri sürümü: eski prototip değerlerini (5/8/2.5 m/s, 2 m boy) yeni varsayılanlara taşımak için (v2), yakınlaştırma
        /// hassasiyet ölçeklemesini açmak için (v3).
        /// </summary>
        public const int CurrentDataVersion = 3;

        [Header("Speed (m/s)")]
        public float walkSpeed = 4.6f;
        public float sprintSpeed = 7.2f;
        public float crouchSpeed = 2.4f;
        public float proneSpeed = 1.1f;

        [Tooltip("Geri yürürken hız çarpanı.")]
        [Range(0.3f, 1f)] public float backwardSpeedFactor = 0.8f;

        [Tooltip("Yalnız yana yürürken hız çarpanı.")]
        [Range(0.3f, 1f)] public float strafeSpeedFactor = 0.92f;

        [Tooltip("Koşu için gereken asgari ileri girdi (yalnız ileri koşulur).")]
        [Range(0.05f, 1f)] public float sprintForwardThreshold = 0.5f;

        [Header("Acceleration (m/s²)")]
        public float groundAcceleration = 38f;
        public float groundDeceleration = 30f;
        public float airAcceleration = 14f;

        [Tooltip("Havada yön kontrolü (0 = yok, 1 = yerdeki gibi).")]
        [Range(0f, 1f)] public float airControl = 0.3f;

        [Header("Jump & Gravity")]
        public float jumpHeight = 1.05f;
        public float gravity = -18f;
        public float maxFallSpeed = 55f;
        public float jumpCooldown = 0.3f;

        [Tooltip("Kenardan düştükten sonra hâlâ zıplanabilen süre (s).")]
        public float coyoteTime = 0.12f;

        [Tooltip("Yere değmeden hemen önce basılan zıplamanın hatırlanma süresi (s).")]
        public float jumpBufferTime = 0.12f;

        [Tooltip("Landed olayı için asgari çarpma hızı (m/s) — daha küçükse hava süresi kontrol edilir.")]
        public float landedEventMinSpeed = 2.5f;

        [Header("Stamina & Load (hareket hissi)")]
        public float staminaMax = 100f;

        [Tooltip("Koşarken saniyede harcanan stamina (yük çarpanıyla artar).")]
        public float staminaSprintDrain = 11f;

        [Tooltip("Harcamadan sonra toparlanma başlamadan önceki bekleme (s).")]
        public float staminaRegenDelay = 1.1f;

        public float staminaRegenRate = 15f;

        [Tooltip("Tükenince (0) bekleme ek süresi (s).")]
        public float staminaExhaustedExtraDelay = 0.9f;

        [Tooltip("Tükenmiş durumdan çıkış eşiği.")]
        public float staminaExhaustedRecoverAt = 25f;

        [Tooltip("Koşuya yeniden başlamak için asgari stamina.")]
        public float staminaSprintStartMin = 12f;

        [Tooltip("Ağır yük/zırh etkisini (hız, ivme, stamina) 0..2 ölçekler. 0 = yük etkisi kapalı.")]
        [Range(0f, 2f)] public float loadEffectScale = 1f;

        [Tooltip("Eğim hız modifikatörü açık mı (yokuş yukarı yavaş, aşağı hafif hızlı).")]
        public bool slopeSpeedModifier = true;

        [Header("Look")]
        public float mouseSensitivity = 0.12f;
        public float minPitch = -85f;
        public float maxPitch = 85f;

        [Tooltip("Açık (varsayılan): kamera, yakınlaştırmada (nişan/dürbün) hassasiyeti gerçek FOV oranıyla " +
                 "(tan(fov/2)/tan(taban/2)) kendisi ölçekler; çağıran taraf (PlayerController) yalnız ayar hassasiyetini ve " +
                 "isteğe bağlı nişan çarpanını SetSensitivity ile verir. Kapalı: ölçekleme tamamen çağırana kalır.")]
        public bool scaleSensitivityWithZoom = true;

        [Header("Stance")]
        public float standingHeight = 1.8f;
        public float crouchHeight = 1.2f;
        public float proneHeight = 0.6f;

        [Tooltip("Boy geçişi üstel hız katsayısı (büyük = hızlı).")]
        public float stanceTransitionSpeed = 10f;

        public float standingEyeHeight = 1.62f;
        public float crouchEyeHeight = 1.05f;
        public float proneEyeHeight = 0.35f;

        [Header("Character Controller")]
        public float radius = 0.3f;
        [Range(0f, 89f)] public float slopeLimit = 46f;
        public float stepOffset = 0.38f;
        public float skinWidth = 0.04f;

        [Tooltip("Yokuş aşağı inerken zemine yapışma mesafesi (m).")]
        public float groundSnapDistance = 0.45f;

        [Tooltip("Dik yüzeylerden kayma hızı (m/s).")]
        public float steepSlideSpeed = 6f;

        [Header("Lean (Q/E)")]
        [Tooltip("Eğilme değerinin saniyedeki değişimi (-1..1 ölçeğinde).")]
        public float leanSpeed = 5.5f;

        public float leanAngle = 12f;
        public float leanOffset = 0.35f;

        [Header("Camera")]
        [Tooltip("Varsayılan dikey görüş açısı (derece).")]
        public float fieldOfView = 64f;

        public float fovSmoothSpeed = 14f;
        public float eyeHeightSmoothSpeed = 14f;

        [Header("Recoil (camera spring)")]
        [Tooltip("Sekme tepkisinin hedefe ulaşma süresi (s, kritik sönümlü yay). Küçük = ani tepki.")]
        public float recoilKickTime = 0.035f;

        [Tooltip("Sekmenin sıfıra geri dönüş hızı (1/s).")]
        public float recoilRecovery = 5.5f;

        [Tooltip("Birikebilecek en fazla geçici sekme (derece).")]
        public float maxRecoilPitch = 12f;

        [Range(0f, 1f)]
        [Tooltip("Dikey sekmenin ne kadarı kalıcı olarak bakış açısına işler (0 = tamamen geri döner).")]
        public float recoilPermanentFraction = 0.35f;

        [Header("Landing")]
        [Tooltip("Yere inişte kamera çökmesi: çarpma hızı başına metre.")]
        public float landingDipPerSpeed = 0.012f;

        public float landingDipMax = 0.16f;

        [Header("Head Bob")]
        public float bobFrequency = 1.4f;
        public float bobVerticalAmplitude = 0.035f;
        public float bobHorizontalAmplitude = 0.025f;
        public float bobRollAmplitude = 0.35f;

        [Header("Shake")]
        public float shakeMaxAngle = 4.5f;
        public float shakeFrequency = 22f;

        [SerializeField, HideInInspector] private int dataVersion;

        /// <summary>Varsayılan değerlerle (yeni) bir örnek üretir; sahne/asset gerekmez.</summary>
        public static PlayerMovementConfig CreateDefault()
        {
            var config = CreateInstance<PlayerMovementConfig>();
            config.name = "PlayerMovementConfig (Default)";
            config.hideFlags = HideFlags.DontSave;
            return config;
        }

        /// <summary>Duruşun kapsül boyu (m).</summary>
        public float HeightFor(Project.Core.Domain.Stance stance)
        {
            switch (stance)
            {
                case Project.Core.Domain.Stance.Crouching:
                    return crouchHeight;
                case Project.Core.Domain.Stance.Prone:
                    return proneHeight;
                default:
                    return standingHeight;
            }
        }

        /// <summary>Duruşun göz yüksekliği (m, ayaktan).</summary>
        public float EyeHeightFor(Project.Core.Domain.Stance stance)
        {
            switch (stance)
            {
                case Project.Core.Domain.Stance.Crouching:
                    return crouchEyeHeight;
                case Project.Core.Domain.Stance.Prone:
                    return proneEyeHeight;
                default:
                    return standingEyeHeight;
            }
        }

        /// <summary>Duruşun temel yürüme hızı (m/s).</summary>
        public float SpeedFor(Project.Core.Domain.Stance stance)
        {
            switch (stance)
            {
                case Project.Core.Domain.Stance.Crouching:
                    return crouchSpeed;
                case Project.Core.Domain.Stance.Prone:
                    return proneSpeed;
                default:
                    return walkSpeed;
            }
        }

        /// <summary>
        /// Kapsül boyuna göre göz yüksekliği (yüzüstü→çömelme→ayakta arasında parçalı doğrusal). Boy geçişi sırasında
        /// bakış noktası kapsülle birlikte yumuşak hareket eder.
        /// </summary>
        public float EyeHeightForCapsule(float capsuleHeight)
        {
            var prone = Mathf.Max(0.1f, proneHeight);
            var crouch = Mathf.Max(prone + 0.01f, crouchHeight);
            var stand = Mathf.Max(crouch + 0.01f, standingHeight);

            if (capsuleHeight <= crouch)
            {
                var t = Mathf.InverseLerp(prone, crouch, capsuleHeight);
                return Mathf.Lerp(proneEyeHeight, crouchEyeHeight, t);
            }

            var u = Mathf.InverseLerp(crouch, stand, capsuleHeight);
            return Mathf.Lerp(crouchEyeHeight, standingEyeHeight, u);
        }

        private void OnEnable()
        {
            MigrateLegacyValues();
        }

        private void OnValidate()
        {
            walkSpeed = Mathf.Max(0f, walkSpeed);
            sprintSpeed = Mathf.Max(walkSpeed, sprintSpeed);
            crouchSpeed = Mathf.Max(0f, crouchSpeed);
            proneSpeed = Mathf.Max(0f, proneSpeed);
            jumpHeight = Mathf.Max(0f, jumpHeight);
            gravity = Mathf.Min(-0.1f, gravity);
            maxFallSpeed = Mathf.Max(1f, maxFallSpeed);
            radius = Mathf.Clamp(radius, 0.1f, 1f);
            proneHeight = Mathf.Max(radius * 2f, proneHeight);
            crouchHeight = Mathf.Max(proneHeight, crouchHeight);
            standingHeight = Mathf.Max(crouchHeight, standingHeight);
            stanceTransitionSpeed = Mathf.Max(0.1f, stanceTransitionSpeed);
            minPitch = Mathf.Clamp(minPitch, -89.9f, 0f);
            maxPitch = Mathf.Clamp(maxPitch, 0f, 89.9f);
            fieldOfView = Mathf.Clamp(fieldOfView, 30f, 120f);
            leanSpeed = Mathf.Max(0.1f, leanSpeed);
            staminaMax = Mathf.Max(1f, staminaMax);
            staminaSprintDrain = Mathf.Max(0f, staminaSprintDrain);
            staminaRegenRate = Mathf.Max(0f, staminaRegenRate);
        }

        /// <summary>Stamina modeli ayarlarına dönüştürür.</summary>
        public Project.Application.Services.StaminaTuning ToStaminaTuning()
        {
            return new Project.Application.Services.StaminaTuning
            {
                Max = Mathf.Max(1f, staminaMax),
                SprintDrain = Mathf.Max(0f, staminaSprintDrain),
                RegenDelay = Mathf.Max(0f, staminaRegenDelay),
                ExhaustedExtraDelay = Mathf.Max(0f, staminaExhaustedExtraDelay),
                RegenRate = Mathf.Max(0f, staminaRegenRate),
                ExhaustedRecoverAt = Mathf.Clamp(staminaExhaustedRecoverAt, 1f, Mathf.Max(1f, staminaMax)),
                SprintStartMin = Mathf.Clamp(staminaSprintStartMin, 0f, Mathf.Max(1f, staminaMax)),
            };
        }

        /// <summary>
        /// Diskteki eski prototip asset'i (dataVersion yok = 0) tam olarak eski varsayılanları taşıyorsa yeni varsayılanlara
        /// yükseltir. Elle ayarlanmış değerlere dokunmaz. Yeni örneklerde değerler zaten yenidir, işlem etkisizdir.
        /// </summary>
        private void MigrateLegacyValues()
        {
            if (dataVersion >= CurrentDataVersion)
                return;

            // v3: yakınlaştırma hassasiyet ölçeklemesi kameraya geçti (v2'de varsayılan kapalıydı).
            if (dataVersion < 3)
                scaleSensitivityWithZoom = true;

            if (dataVersion >= 2)
            {
                dataVersion = CurrentDataVersion;
                return;
            }

            if (Mathf.Approximately(walkSpeed, 5f) && Mathf.Approximately(sprintSpeed, 8f)
                && Mathf.Approximately(crouchSpeed, 2.5f))
            {
                walkSpeed = 4.6f;
                sprintSpeed = 7.2f;
                crouchSpeed = 2.4f;
            }

            if (Mathf.Approximately(standingHeight, 2f) && Mathf.Approximately(crouchHeight, 1.2f))
                standingHeight = 1.8f;

            if (Mathf.Approximately(jumpHeight, 1.2f) && Mathf.Approximately(gravity, -20f))
            {
                jumpHeight = 1.05f;
                gravity = -18f;
            }

            if (Mathf.Approximately(mouseSensitivity, 0.15f))
                mouseSensitivity = 0.12f;

            dataVersion = CurrentDataVersion;
        }
    }
}
