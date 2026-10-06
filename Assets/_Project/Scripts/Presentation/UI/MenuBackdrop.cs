using System;
using System.Collections.Generic;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.World.Lobby;
using Project.Presentation.Lobby;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Ana menünün 3B dioraması (tamamen kodla kurulur), PUBG lobisi gibi KÜÇÜK ve KESKİN bir iç mekân: kameraya yakın tek asker
    /// (<see cref="Project.Infrastructure.Characters.SoldierModel"/>, alçak hazır), arkasında kum torbası siperi + sandık/varil,
    /// tepede kamuflaj ağı şeritleri, geride park halinde Kirpi, 12 m'lik gerçek zemin, yan ateş (şekilli alev kartları).
    /// DARK MODE (mavi saat): koyu lacivert ortam, soldan soğuk ay ışığı, arkadan soğuk kontur, soğuk spotlar; ana vurgu kamp ateşi. Menü kamerası için yerel post-process (koyu kenar vinyeti, DoF yok)
    /// ve çok seyrek sis. Kamera sabit; yalnız ±1 cm nefes süzülmesi.
    /// <para>Başsız (batch) modda görsel kurulmaz. Ürettiği mesh'leri ve ses döngülerini yok edilirken temizler.</para>
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
            Settings,
            /// <summary>OYNA: helipaddeki T-70, Kirpi ve tim (sağa bakış).</summary>
            Play,
            /// <summary>DONANIM: sandıklar, cephane ve ateş başı yakın plan.</summary>
            Loadout,
            /// <summary>TİM: askerler ve komutan, hafif yakın.</summary>
            Team
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

        private const float ShotBlendSeconds = 1.8f;
        private const float SoldierRefreshInterval = 1.5f;
        private const int MaxPendingBooms = 8;

        // Kamera (0, 1.6, 0) civarında sabit; asker z = 3.2 m'de, sağa yakın. Tüm kadrajlar küçük dioramanın içinde kalır.
        private static readonly ShotPose[] Shots =
        {
            // Ana kadraj (referans): komutan göğüs-üstü baskın, sağda tim + ateş, solda Kirpi. Hedef = SoldierPosition göğüs hizası.
            new ShotPose(new Vector3(0.1f, 1.55f, 0f), new Vector3(0.2f, 1.35f, 2.6f), 37f),
            new ShotPose(new Vector3(-0.8f, 1.5f, 0.3f), new Vector3(-2.5f, 1.3f, 10.5f), 40f),
            new ShotPose(new Vector3(0.2f, 1.65f, 1.0f), new Vector3(0.7f, 1.55f, 3.2f), 32f),
            new ShotPose(new Vector3(-0.5f, 1.6f, 0f), new Vector3(1.5f, 1.4f, 8f), 46f),
            new ShotPose(new Vector3(0.3f, 1.7f, -0.2f), new Vector3(1.0f, 1.3f, 5f), 50f),
            new ShotPose(new Vector3(-0.6f, 1.3f, 1.0f), new Vector3(-1.8f, 0.5f, 5f), 42f),
            new ShotPose(new Vector3(0f, 1.6f, 0.3f), new Vector3(0.6f, 1.3f, 3.2f), 38f)
        };

        private static MenuBackdrop _current;

        private MenuBackdropBuilder.Context _context;
        private List<MenuBackdropBuilder.SoldierPose> _soldiers;
        private MenuFlagCloth _flag;
        private MenuCampfire _campfire;
        private ParticleSystem _flashes;
        private ParticleSystem _tracers;
        private Light _flashLight;
        private MenuBackdropLook _look;
        private LobbyAmbience _ambience;
        private LobbyParallax _parallax;
        private float _flashLightIntensity;
        private AudioSource _battleLoop;

        private CameraRig _rig;
        private Transform _cameraTransform;
        private Camera _camera;
        private Shot _shot = Shot.Main;
        private ShotPose _from;
        private ShotPose _to;
        private float _blend = 1f;
        private float _startTime;
        private float _soldierTimer;
        private float _nextFlash;
        private float _nextTracer;
        private int _flashCluster;
        private readonly PendingBoom[] _booms = new PendingBoom[MaxPendingBooms];
        private int _boomCount;
        private System.Random _rng;
        private bool _built;
        private List<Light> _floodlights;
        private List<float> _floodBase;
        private float _floodFlickerLeft;
        private int _floodFlickerIndex;

        /// <summary>Sahnedeki etkin dekor (yoksa null).</summary>
        public static MenuBackdrop Current => _current;

        /// <summary>Dekorun kamerası (kurulamadıysa null).</summary>
        public Camera Camera => _camera;

        /// <summary>Dekorun kamera düzeneği (kurulamadıysa null).</summary>
        public CameraRig Rig => _rig;

        /// <summary>Etkin kadraj.</summary>
        public Shot CurrentShot => _shot;

        /// <summary>Uzak çatışma efektleri (parlama, iz mermisi, ses) açık mı.</summary>
        public bool DistantBattleEnabled { get; set; }

        /// <summary>Fare paralaksı açık mı.</summary>
        public bool ParallaxEnabled { get; set; }

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

        /// <summary>Diorama sabit yönlüdür (ışık dekora göre kurulur; güneşe göre döndürme yok).</summary>
        private static float ComputeRootYaw() => 0f;

        private void Awake()
        {
            _current = this;
            // Menü/lobi derecelendirmesi: mavi saat + kırmızı vurgu (PostProcessing tek yazar; 2,5 sn yumuşak geçiş).
            Project.Infrastructure.Rendering.PostProcessing.SetLobbyGrade(true);
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

            Step(() => MenuDioramaBuilder.BuildGround(_context), "Zemin");
            Step(() => MenuDioramaBuilder.BuildEnclosure(_context), "Duvarlar");
            Step(() => MenuDioramaBuilder.BuildCamoStrips(_context), "Kamuflaj şeritleri");
            Step(() => MenuDioramaBuilder.BuildSandbagWall(_context), "Kum torbaları");
            Step(() => MenuDioramaBuilder.BuildProps(_context), "Malzemeler");
            Step(() => MenuDioramaBuilder.BuildVehicle(_context), "Kirpi");
            Step(() => _campfire = MenuCampfire.Create(transform, MenuDioramaBuilder.CampfirePosition, 77), "Kamp ateşi");
            Step(() => _soldiers = MenuDioramaBuilder.BuildSoldier(_context), "Asker");
            Step(() => _look = MenuBackdropLook.Apply(transform), "Işık ve görünüm");
            Step(() => _ambience = LobbyAmbience.Create(transform, MenuDioramaBuilder.CampfirePosition,
                MenuDioramaBuilder.SoldierPosition, QualitySettings.GetQualityLevel()), "Lobi atmosferi");
            Step(() => _parallax = gameObject.AddComponent<LobbyParallax>(), "Lobi paralaksı");
            Step(BuildCamera, "Kamera");
            Step(StartAudio, "Ses");

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

            if (_camera != null)
            {
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = MenuBackdropLook.BackgroundColor;
            }

            if (_camera == null)
            {
                var go = new GameObject("Kamera");
                go.transform.SetParent(pivot, false);
                go.tag = "MainCamera";
                _camera = go.AddComponent<Camera>();
                _camera.fieldOfView = Shots[0].FieldOfView;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = MenuBackdropLook.BackgroundColor;
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
            UpdateFloodlightFlicker(dt);
        }

        // ------------------------------------------------------------------ Uzak projektör titremesi

        private void UpdateFloodlightFlicker(float dt)
        {
            if (_floodlights == null)
            {
                _floodlights = new List<Light>(2);
                _floodBase = new List<float>(2);
                foreach (var l in GetComponentsInChildren<Light>(true))
                {
                    if (l != null && l.name.StartsWith("Projektör", StringComparison.Ordinal))
                    {
                        _floodlights.Add(l);
                        _floodBase.Add(l.intensity);
                    }
                }
            }

            if (_floodlights.Count == 0)
                return;

            if (_floodFlickerLeft <= 0f)
            {
                // Saniyede %0,5 olasılık.
                if (_rng.NextDouble() < 0.005 * dt)
                {
                    _floodFlickerLeft = Range(0.18f, 0.4f);
                    _floodFlickerIndex = _rng.Next(0, _floodlights.Count);
                }

                return;
            }

            _floodFlickerLeft -= dt;
            var i = _floodFlickerIndex;
            if (i < 0 || i >= _floodlights.Count || _floodlights[i] == null)
            {
                _floodFlickerLeft = 0f;
                return;
            }

            var k = _floodFlickerLeft <= 0f ? 1f : Mathf.Lerp(0.55f, 1f, Mathf.PerlinNoise(Time.unscaledTime * 38f, i * 7.1f));
            _floodlights[i].intensity = _floodBase[i] * k;
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

            // Kamera hayatı: yörünge yok; ~2 mm yavaş süzülme + %1 yakınlaşma nefesi (8 sn periyot).
            var life = Time.unscaledTime - _startTime;
            if (float.IsNaN(life) || float.IsInfinity(life))
                life = 0f;
            var position = pose.Position + new Vector3(
                Mathf.Sin(life * 0.31f) * 0.002f,
                Mathf.Sin(life * 0.23f + 1.3f) * 0.002f,
                Mathf.Sin(life * 0.17f + 2.1f) * 0.002f);
            // Yavaş dolly (60 sn periyot, ±4 cm ileri-geri) + imleç paralaksı (yalnız ParallaxEnabled).
            position += pose.Target - pose.Position != Vector3.zero
                ? (pose.Target - pose.Position).normalized * (Mathf.Sin(life * 0.105f) * 0.04f)
                : Vector3.zero;
            if (ParallaxEnabled && _parallax != null)
                position += _parallax.Offset;
            var look = pose.Target - position;
            if (look.sqrMagnitude < 1e-4f)
                look = Vector3.forward;

            _cameraTransform.localPosition = position;
            _cameraTransform.localRotation = Quaternion.LookRotation(look, Vector3.up);

            var fov = pose.FieldOfView * (1f + 0.01f * Mathf.Sin(life * (Mathf.PI * 2f / 8f)));
            if (_rig != null)
                _rig.SetFieldOfView(fov);
            else if (_camera != null && !Mathf.Approximately(_camera.fieldOfView, fov))
                _camera.fieldOfView = fov;
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
            {
                _current = null;
                Project.Infrastructure.Rendering.PostProcessing.SetLobbyGrade(false);
            }

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

            if (_look != null)
                _look.Dispose();
            _look = null;
            MenuBackdropBuilder.DestroyOwned(_context);
            _context = null;
            _soldiers = null;
        }
    }
}
