using System;
using System.Collections.Generic;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Ana menünün 3B dioraması (tamamen kodla kurulur): alacakaranlıkta sisli dağ sırtları ve vadi, kavisli kum torbası
    /// siperi, Kirpi benzeri zırhlı araç, kamp ateşi başında bekleyen dört asker (<see cref="Project.Infrastructure.Characters.SoldierModel"/>),
    /// direkte dalgalanan Türk bayrağı, çadır/sandık/HESCO dekoru, vadide pus; yavaşça süzülen kamera (fare ile hafif
    /// paralaks), arada bir uzak sırtlarda patlama parlaması + gecikmeli gümbürtü ve sürekli uzak çatışma sesi.
    /// <para>Kök, gün batımı güneşi kadrajın sağ üstünde kalacak şekilde döndürülür. Başsız (batch) modda görsel
    /// kurulmaz. Ürettiği mesh'leri ve ses döngülerini yok edilirken temizler.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuBackdrop : MonoBehaviour
    {
        /// <summary>Kamera kadrajları (menü panellerine göre).</summary>
        public enum Shot
        {
            /// <summary>Ana menü: ateş başı, askerler ve vadi.</summary>
            Main,
            /// <summary>Harekât kurulumu: Kirpi ve tim.</summary>
            Setup,
            /// <summary>Kariyer: tim komutanına yakın plan.</summary>
            Career,
            /// <summary>Ayarlar: bayrak ve vadi.</summary>
            Settings
        }

        private struct ShotPose
        {
            public Vector3 Position;
            public Vector3 Target;
            public float FieldOfView;

            public ShotPose(Vector3 position, Vector3 target, float fieldOfView)
            {
                Position = position;
                Target = target;
                FieldOfView = fieldOfView;
            }
        }

        private struct PendingBoom
        {
            public float Time;
            public float Volume;
            public float Pitch;
        }

        private const float SunLocalYaw = 40f;
        private const float IntroSeconds = 4.5f;
        private const float ShotBlendSeconds = 1.8f;
        private const float SoldierRefreshInterval = 1.5f;
        private const int MaxPendingBooms = 8;

        private static readonly ShotPose[] Shots =
        {
            new ShotPose(new Vector3(-2.2f, 2.0f, -8.6f), new Vector3(1.9f, 1.3f, 3.2f), 45f),
            new ShotPose(new Vector3(0.6f, 1.85f, -5.6f), new Vector3(6.6f, 1.45f, 2.4f), 42f),
            new ShotPose(new Vector3(1.0f, 1.7f, -2.3f), new Vector3(3.9f, 1.5f, 2.5f), 36f),
            new ShotPose(new Vector3(-1.0f, 1.7f, -4.5f), new Vector3(3.3f, 4.6f, 8.3f), 44f)
        };

        private static MenuBackdrop _current;

        private MenuBackdropBuilder.Context _context;
        private List<MenuBackdropBuilder.SoldierPose> _soldiers;
        private MenuFlagCloth _flag;
        private MenuCampfire _campfire;
        private ParticleSystem _flashes;
        private ParticleSystem _tracers;
        private Light _flashLight;
        private float _flashLightIntensity;
        private AudioSource _battleLoop;

        private CameraRig _rig;
        private Transform _cameraTransform;
        private Camera _camera;
        private Shot _shot = Shot.Main;
        private ShotPose _from;
        private ShotPose _to;
        private float _blend = 1f;
        private Vector2 _parallax;
        private float _startTime;
        private float _soldierTimer;
        private float _nextFlash;
        private float _nextTracer;
        private int _flashCluster;
        private readonly PendingBoom[] _booms = new PendingBoom[MaxPendingBooms];
        private int _boomCount;
        private System.Random _rng;
        private bool _built;

        /// <summary>Sahnedeki etkin dekor (yoksa null).</summary>
        public static MenuBackdrop Current => _current;

        /// <summary>Dekorun kamerası (kurulamadıysa null).</summary>
        public Camera Camera => _camera;

        /// <summary>Dekorun kamera düzeneği (kurulamadıysa null).</summary>
        public CameraRig Rig => _rig;

        /// <summary>Etkin kadraj.</summary>
        public Shot CurrentShot => _shot;

        /// <summary>Uzak çatışma efektleri (parlama, iz mermisi, ses) açık mı.</summary>
        public bool DistantBattleEnabled { get; set; } = true;

        /// <summary>Fare paralaksı açık mı.</summary>
        public bool ParallaxEnabled { get; set; } = true;

        /// <summary>
        /// Dioramayı kurar. <paramref name="parent"/> null ise sahne kökünde oluşturulur. Başsız modda yalnızca boş bir
        /// bileşen döner (kamera kurulmaz).
        /// </summary>
        public static MenuBackdrop Create(Transform parent)
        {
            var go = new GameObject("MenuBackdrop");
            if (parent != null)
                go.transform.SetParent(parent, false);

            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, ComputeRootYaw(), 0f));
            var backdrop = go.AddComponent<MenuBackdrop>();
            backdrop.Build();
            return backdrop;
        }

        /// <summary>Kamerayı verilen kadraja yumuşakça taşır.</summary>
        public void SetShot(Shot shot)
        {
            var index = (int)shot;
            if (index < 0 || index >= Shots.Length)
                shot = Shot.Main;

            if (shot == _shot && _blend >= 1f)
                return;

            _from = CurrentPose();
            _to = Shots[(int)shot];
            _shot = shot;
            _blend = 0f;
        }

        private static float ComputeRootYaw()
        {
            var sun = RenderSettings.sun;
            if (sun == null)
                return 0f;

            var toSun = -sun.transform.forward;
            toSun.y = 0f;
            if (toSun.sqrMagnitude < 1e-4f)
                return 0f;

            return Mathf.Atan2(toSun.x, toSun.z) * Mathf.Rad2Deg - SunLocalYaw;
        }

        private void Awake()
        {
            _current = this;
            _rng = new System.Random(Environment.TickCount);
            _startTime = Time.unscaledTime;
            _to = Shots[0];
            _from = _to;
        }

        private void Start()
        {
            // Sahneye elle yerleştirildiyse (Create çağrılmadıysa) burada kurulur.
            if (_built)
                return;

            if (transform.parent == null && transform.rotation == Quaternion.identity)
                transform.rotation = Quaternion.Euler(0f, ComputeRootYaw(), 0f);
            Build();
        }

        private void Build()
        {
            if (_built)
                return;
            _built = true;

            if (UnityEngine.Application.isBatchMode)
            {
                enabled = false;
                return;
            }

            _context = new MenuBackdropBuilder.Context { Root = transform };

            Step(() => MenuBackdropBuilder.BuildGround(_context), "Zemin");
            Step(() => MenuBackdropBuilder.BuildMountains(_context), "Dağlar");
            Step(() => MenuBackdropBuilder.BuildVegetation(_context), "Bitki örtüsü");
            Step(() => MenuBackdropBuilder.BuildSandbagWall(_context), "Kum torbaları");
            Step(() => MenuBackdropBuilder.BuildVehicle(_context), "Kirpi");
            Step(() => MenuBackdropBuilder.BuildProps(_context), "Malzemeler");
            Step(() => _flag = MenuBackdropBuilder.BuildFlagPole(_context), "Bayrak");
            Step(() => _campfire = MenuCampfire.Create(transform, MenuBackdropBuilder.CampfirePosition, 77), "Kamp ateşi");
            Step(() => _soldiers = MenuBackdropBuilder.BuildSoldiers(_context), "Askerler");
            Step(() => MenuBackdropBuilder.BuildValleyMist(_context), "Pus");
            Step(() => _flashes = MenuBackdropBuilder.BuildFlashSystem(_context), "Parlamalar");
            Step(() => _tracers = MenuBackdropBuilder.BuildTracerSystem(_context), "İz mermileri");
            Step(BuildFlashLight, "Parlama ışığı");
            Step(BuildCamera, "Kamera");
            Step(StartAudio, "Ses");

            _nextFlash = Time.unscaledTime + 2.5f;
            _nextTracer = Time.unscaledTime + 1.2f;
            ApplyCamera(0f);
        }

        private static void Step(Action action, string context)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MenuBackdrop] " + context + " kurulamadı: " + e.Message);
            }
        }

        private void BuildCamera()
        {
            var pivot = new GameObject("MenüKamerası").transform;
            pivot.SetParent(transform, false);
            _cameraTransform = pivot;

            try
            {
                _rig = CameraRig.Create(pivot, Shots[0].FieldOfView, false);
                _camera = _rig != null ? _rig.WorldCamera : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MenuBackdrop] CameraRig kurulamadı, düz kamera kullanılıyor: " + e.Message);
                _rig = null;
            }

            if (_camera == null)
            {
                var go = new GameObject("Kamera");
                go.transform.SetParent(pivot, false);
                go.tag = "MainCamera";
                _camera = go.AddComponent<Camera>();
                _camera.fieldOfView = Shots[0].FieldOfView;
                _camera.nearClipPlane = 0.05f;
                _camera.farClipPlane = 1500f;
                go.AddComponent<AudioListener>();
            }
        }

        private void BuildFlashLight()
        {
            var go = new GameObject("ParlamaIşığı");
            go.transform.SetParent(transform, false);
            _flashLight = go.AddComponent<Light>();
            _flashLight.type = LightType.Point;
            _flashLight.color = new Color(1f, 0.62f, 0.3f);
            _flashLight.range = 140f;
            _flashLight.intensity = 0f;
            _flashLight.shadows = LightShadows.None;
            _flashLight.enabled = false;
        }

        private void StartAudio()
        {
            if (!GameAudio.IsInitialized)
                GameAudio.Initialize();
            _battleLoop = GameAudio.StartLoop(SoundId.DistantBattle, transform, 0.22f, false);
        }

        // ------------------------------------------------------------------ Güncelleme

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            if (dt > 0.1f)
                dt = 0.1f;

            ApplyCamera(dt);
            UpdateSoldiers(dt);

            if (DistantBattleEnabled)
                UpdateDistantBattle(dt);
            else if (_flashLight != null && _flashLight.enabled)
                _flashLight.enabled = false;

            UpdateBooms();
        }

        private ShotPose CurrentPose()
        {
            var t = Mathf.Clamp01(_blend);
            var eased = t * t * (3f - 2f * t);
            return new ShotPose(
                Vector3.Lerp(_from.Position, _to.Position, eased),
                Vector3.Lerp(_from.Target, _to.Target, eased),
                Mathf.Lerp(_from.FieldOfView, _to.FieldOfView, eased));
        }

        private void ApplyCamera(float dt)
        {
            if (_cameraTransform == null)
                return;

            if (_blend < 1f)
                _blend = Mathf.Min(1f, _blend + dt / ShotBlendSeconds);

            var pose = CurrentPose();
            var t = Time.unscaledTime;
            var since = t - _startTime;

            // Açılış: kamera geriden ve yukarıdan süzülerek yerine oturur.
            var intro = 1f - Mathf.Clamp01(since / IntroSeconds);
            intro = intro * intro * (3f - 2f * intro);

            var forward = pose.Target - pose.Position;
            if (forward.sqrMagnitude < 1e-4f)
                forward = Vector3.forward;
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward).normalized;

            var drift = right * (Mathf.Sin(t * 0.071f) * 0.7f) +
                        Vector3.up * (Mathf.Sin(t * 0.093f + 1f) * 0.12f) +
                        forward * (Mathf.Sin(t * 0.047f + 2f) * 0.5f);

            if (ParallaxEnabled)
                UpdateParallax(dt);
            var parallax = right * (_parallax.x * 0.35f) + Vector3.up * (_parallax.y * 0.14f);

            var position = pose.Position + drift + parallax + (forward * -3.5f + Vector3.up * 1.1f + right * -1.2f) * intro;
            var target = pose.Target + new Vector3(Mathf.Sin(t * 0.06f) * 0.3f, Mathf.Sin(t * 0.08f) * 0.07f, 0f) + parallax * 0.4f;

            var rotation = Quaternion.LookRotation(target - position, Vector3.up);
            var handheld = Quaternion.Euler(
                (Mathf.PerlinNoise(t * 0.35f, 1.7f) - 0.5f) * 0.5f,
                (Mathf.PerlinNoise(t * 0.31f, 8.2f) - 0.5f) * 0.5f,
                (Mathf.PerlinNoise(t * 0.22f, 4.4f) - 0.5f) * 0.4f);

            _cameraTransform.localPosition = position;
            _cameraTransform.localRotation = rotation * handheld;

            var fov = pose.FieldOfView + intro * 4f;
            if (_rig != null)
                _rig.SetFieldOfView(fov);
            else if (_camera != null && !Mathf.Approximately(_camera.fieldOfView, fov))
                _camera.fieldOfView = fov;
        }

        private void UpdateParallax(float dt)
        {
            var target = Vector2.zero;
            var mouse = Mouse.current;
            if (mouse != null && Screen.width > 0 && Screen.height > 0)
            {
                var p = mouse.position.ReadValue();
                target = new Vector2(Mathf.Clamp(p.x / Screen.width * 2f - 1f, -1f, 1f), Mathf.Clamp(p.y / Screen.height * 2f - 1f, -1f, 1f));
            }

            var k = 1f - Mathf.Exp(-dt * 1.6f);
            _parallax = Vector2.Lerp(_parallax, target, k);
        }

        private void UpdateSoldiers(float dt)
        {
            if (_soldiers == null || _soldiers.Count == 0)
                return;

            var t = Time.time;
            _soldierTimer -= dt;
            var refresh = _soldierTimer <= 0f;
            if (refresh)
                _soldierTimer = SoldierRefreshInterval;

            for (var i = 0; i < _soldiers.Count; i++)
            {
                var pose = _soldiers[i];
                if (pose.Model == null)
                    continue;

                // Başını hafifçe kaldırıp indirir (etrafı kolaçan eder).
                var pitch = pose.BasePitch + Mathf.Sin(t * pose.PitchSpeed + pose.Phase) * pose.PitchAmplitude;
                pose.Model.SetAimPitch(pitch);
                if (refresh)
                    pose.Model.SetLocomotion(Vector3.zero, pose.Stance, true);
            }
        }

        // ------------------------------------------------------------------ Uzak çatışma

        private void UpdateDistantBattle(float dt)
        {
            var now = Time.unscaledTime;

            if (_flashLight != null && _flashLight.enabled)
            {
                _flashLightIntensity *= Mathf.Exp(-dt * 7f);
                if (_flashLightIntensity < 4f)
                {
                    _flashLightIntensity = 0f;
                    _flashLight.enabled = false;
                }

                _flashLight.intensity = _flashLightIntensity;
            }

            if (now >= _nextFlash)
            {
                TriggerFlash();
                if (_flashCluster > 0)
                {
                    _flashCluster--;
                    _nextFlash = now + Range(0.25f, 0.9f);
                }
                else
                {
                    _flashCluster = _rng.NextDouble() < 0.35 ? _rng.Next(1, 3) : 0;
                    _nextFlash = now + Range(4.5f, 11f);
                }
            }

            if (now >= _nextTracer)
            {
                TriggerTracers();
                _nextTracer = now + Range(1.6f, 5.5f);
            }
        }

        private Vector3 RandomBattlePoint(float minDistance, float maxDistance)
        {
            // Kameranın baktığı yay içinde, vadinin ilerisinde.
            var yaw = Range(-38f, 50f) * Mathf.Deg2Rad;
            var distance = Range(minDistance, maxDistance);
            var x = Mathf.Sin(yaw) * distance;
            var z = Mathf.Cos(yaw) * distance;
            var y = Mathf.Lerp(-22f, 40f, Mathf.InverseLerp(120f, 360f, distance)) + Range(-6f, 14f);
            return transform.TransformPoint(new Vector3(x, y, z));
        }

        private void TriggerFlash()
        {
            if (_flashes == null)
                return;

            var position = RandomBattlePoint(150f, 340f);
            var camPos = _cameraTransform != null ? _cameraTransform.position : transform.position;
            var distance = Vector3.Distance(camPos, position);

            var core = new ParticleSystem.EmitParams
            {
                position = position,
                startSize = Range(9f, 17f),
                startLifetime = Range(0.25f, 0.45f),
                startColor = new Color(1f, 0.82f, 0.55f, 1f),
                applyShapeToPosition = false
            };
            _flashes.Emit(core, 1);

            var glow = new ParticleSystem.EmitParams
            {
                position = position + Vector3.up * 3f,
                startSize = Range(28f, 46f),
                startLifetime = Range(0.5f, 0.85f),
                startColor = new Color(1f, 0.5f, 0.2f, 0.35f),
                applyShapeToPosition = false
            };
            _flashes.Emit(glow, 1);

            if (_flashLight != null)
            {
                _flashLight.transform.position = position + Vector3.up * 8f;
                // URP nokta ışığı ters kare zayıflar: onlarca metre öteyi aydınlatmak için yüksek şiddet gerekir.
                _flashLightIntensity = Range(700f, 1500f);
                _flashLight.intensity = _flashLightIntensity;
                _flashLight.enabled = true;
            }

            QueueBoom(distance);
        }

        private void TriggerTracers()
        {
            if (_tracers == null)
                return;

            var origin = RandomBattlePoint(150f, 300f);

            // Kadrajı yatay kesen akış (kameraya doğru uçmasın): yerel yan yön ± 35°, hafif yukarı.
            var side = _rng.NextDouble() < 0.5 ? -1f : 1f;
            var heading = (90f * side + Range(-35f, 35f)) * Mathf.Deg2Rad;
            var localDirection = new Vector3(Mathf.Sin(heading), Range(0.02f, 0.2f), Mathf.Cos(heading));
            var direction = transform.TransformDirection(localDirection).normalized;
            var green = _rng.NextDouble() < 0.4;
            var color = green ? new Color(0.65f, 1f, 0.45f, 1f) : new Color(1f, 0.45f, 0.18f, 1f);
            var count = _rng.Next(3, 8);
            var speed = Range(160f, 240f);
            for (var i = 0; i < count; i++)
            {
                var emit = new ParticleSystem.EmitParams
                {
                    position = origin - direction * (i * Range(6f, 10f)),
                    velocity = (direction + new Vector3(Range(-0.03f, 0.03f), Range(-0.02f, 0.03f), Range(-0.03f, 0.03f))) * speed,
                    startSize = Range(0.5f, 0.9f),
                    startLifetime = Range(0.55f, 1.0f),
                    startColor = color,
                    applyShapeToPosition = false
                };
                _tracers.Emit(emit, 1);
            }
        }

        private void QueueBoom(float distance)
        {
            if (_boomCount >= MaxPendingBooms)
                return;

            var nearness = Mathf.InverseLerp(360f, 150f, distance);
            _booms[_boomCount++] = new PendingBoom
            {
                Time = Time.unscaledTime + distance / GameAudio.SpeedOfSound,
                Volume = Mathf.Lerp(0.14f, 0.42f, nearness),
                Pitch = Range(0.42f, 0.62f)
            };
        }

        private void UpdateBooms()
        {
            if (_boomCount == 0)
                return;

            var now = Time.unscaledTime;
            for (var i = _boomCount - 1; i >= 0; i--)
            {
                if (_booms[i].Time > now)
                    continue;

                var boom = _booms[i];
                _booms[i] = _booms[_boomCount - 1];
                _boomCount--;

                try
                {
                    GameAudio.Play2D(SoundId.Explosion, boom.Volume, boom.Pitch);
                }
                catch (Exception)
                {
                    // Ses sistemi yoksa sessiz geç.
                }
            }
        }

        private float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        private void OnDisable()
        {
            if (_flashLight != null)
                _flashLight.enabled = false;
        }

        private void OnDestroy()
        {
            if (_current == this)
                _current = null;

            if (_battleLoop != null)
            {
                try
                {
                    GameAudio.StopLoop(_battleLoop);
                }
                catch (Exception)
                {
                    // Ses sistemi kapanmış olabilir.
                }

                _battleLoop = null;
            }

            MenuBackdropBuilder.DestroyOwned(_context);
            _context = null;
            _soldiers = null;
        }
    }
}
