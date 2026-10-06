using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Replay;
using Project.Core.Domain;
using Project.Infrastructure;
using Project.Infrastructure.Characters;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using Project.Presentation.Bootstrap;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.Replay
{
    /// <summary>
    /// Sahnesiz tekrar izleyici: harita sahnesi MatchBootstrap yerine bu bileşenle açılır. Kayıtlı askerler hafif
    /// "hayalet" SoldierModel'ler olarak oynatılır; serbest uçuş ve takip kamerası, zaman çizelgesi (oynat/duraklat,
    /// 0.25x-4x hız, ölüm işaretleri) sunar. Kaydedilen dünya tohumuyla harita yeniden üretilir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class ReplayViewer : MonoBehaviour
    {
        private static readonly float[] Speeds = { 0.25f, 0.5f, 1f, 2f, 4f };
        private const int DefaultSpeedIndex = 2;
        private const float FeedLifetime = 6f;

        private sealed class Ghost
        {
            public ReplayPlayer Player;
            public Transform Root;
            public SoldierModel Model;
            public TextMesh Label;
            public Vector3 LastPos;
            public bool HasLast;
            public bool Dead;
            public int Weapon = -2;
            public bool Visible;
            public Vector3 Position;
            public float Yaw;
            public float Pitch;
        }

        private struct Flash
        {
            public Transform Transform;
            public float Start;
            public float Radius;
        }

        // Başlatma: menüden Open() → harita sahnesi yüklenir → MatchBootstrap.Awake → TryHandleStart.
        private static ReplayData _pending;

        private ReplayData _data;
        private readonly List<Ghost> _ghosts = new List<Ghost>();
        private readonly Dictionary<int, Ghost> _ghostById = new Dictionary<int, Ghost>();
        private readonly List<Flash> _flashes = new List<Flash>();
        private readonly List<string> _feed = new List<string>();
        private readonly List<float> _feedTimes = new List<float>();
        private Transform _runtimeRoot;
        private LineRenderer _zoneRing;
        private Material _lineMaterial;
        private Camera _camera;

        private float _time;
        private bool _playing = true;
        private int _speedIndex = DefaultSpeedIndex;
        private int _eventCursor;
        private bool _ended;

        // Kamera
        private bool _follow = true;
        private int _followId = -1;
        private float _freeYaw;
        private float _freePitch = 20f;
        private float _orbitYaw;
        private float _orbitPitch = 15f;
        private float _orbitDistance = 4.5f;
        private Vector3 _smoothFocus;
        private bool _focusInit;

        /// <summary>Tekrar dosyasını yükler ve haritanın sahnesini açar. Başarısızsa false.</summary>
        public static bool Open(string path)
        {
            var data = Project.Infrastructure.Replay.ReplayRecorder.Load(path);
            if (data == null || data.Frames.Count < 2)
                return false;
            _pending = data;
            GameSession.Mode = GameMode.BattleRoyale;
            GameSession.LoadScene(SceneNames.OperationSceneFor(data.MapId));
            return true;
        }

        /// <summary>MatchBootstrap.Awake tek satır kancası: bekleyen tekrar varsa izleyiciyi kurar ve true döner.</summary>
        public static bool TryHandleStart(GameObject host)
        {
            if (_pending == null || host == null)
                return false;
            var data = _pending;
            _pending = null;
            host.AddComponent<ReplayViewer>().Init(data);
            return true;
        }

        private void Init(ReplayData data)
        {
            _data = data;
            BootstrapUtility.PrepareScene();
            var quality = GameSession.Settings != null ? GameSession.Settings.Current.QualityLevel : 2;
            BootstrapUtility.Try(() => BootstrapUtility.InitializeEngineSystems(PostProcessing.Look.Gameplay, quality, false), "Tekrar motor sistemleri");

            _runtimeRoot = new GameObject("[Tekrar]").transform;
            BootstrapUtility.Try(SetupWorld, "Tekrar dünyası");
            BootstrapUtility.Try(SetupCamera, "Tekrar kamerası");
            BootstrapUtility.Try(SpawnGhosts, "Tekrar askerleri");
            BootstrapUtility.Try(SetupZoneRing, "Tekrar bölge halkası");
            BootstrapUtility.Try(BuildUi, "Tekrar arayüzü");
            BootstrapUtility.Try(SetupExtras, "Tekrar cilası");

            _followId = _data.FindPlayer(_data.LocalPlayerId) != null ? _data.LocalPlayerId : (_data.Players.Count > 0 ? _data.Players[0].Id : -1);
            _follow = _followId >= 0;
            if (_data.Frames.Count > 0 && ReplayTimeline.TryGetSample(_data.Frames[0], _followId, out var s))
                _orbitYaw = s.Yaw;
            GameSession.HideLoading();
            UiFactory.SetCursorFree(true);
            Apply(0f, 0f);
        }

        private void SetupWorld()
        {
            var world = WorldMetadata.Instance != null ? WorldMetadata.Instance : FindAnyObjectByType<WorldMetadata>();
            if (world == null)
            {
                var parent = new GameObject("[Dünya]").transform;
                var options = new WorldGenerationOptions
                {
                    Parent = parent,
                    MapId = _data.MapId,
                    BakeNavMesh = false,
                    GenerateMinimap = false
                };
                if (_data.WorldSeed != 0)
                    options.Seed = _data.WorldSeed;
                world = WorldGenerator.Generate(options);
            }
            BootstrapUtility.EnsureSun();
        }

        private void SetupCamera()
        {
            var cams = Camera.allCameras;
            Camera source = null;
            for (var i = 0; i < cams.Length; i++)
                if (cams[i] != null && (source == null || cams[i].depth > source.depth))
                    source = cams[i];

            var go = new GameObject("TekrarKamera");
            go.transform.SetParent(_runtimeRoot, false);
            _camera = go.AddComponent<Camera>();
            if (source != null)
            {
                _camera.CopyFrom(source);
                _camera.targetTexture = null;
                _camera.depth = source.depth + 10f;
            }
            else
            {
                _camera.depth = 10f;
                _camera.farClipPlane = 1500f;
            }
            _camera.tag = "MainCamera";
            _camera.enabled = true;
            go.AddComponent<AudioListener>();
            for (var i = 0; i < cams.Length; i++)
            {
                if (cams[i] == null || cams[i] == _camera)
                    continue;
                cams[i].enabled = false;
                var l = cams[i].GetComponent<AudioListener>();
                if (l != null)
                    l.enabled = false;
            }

            var first = _data.Frames[0];
            var p = Vector3.up * 40f;
            if (ReplayTimeline.TryGetSample(first, _data.LocalPlayerId, out var s))
                p = new Vector3(s.X, s.Y + 6f, s.Z);
            go.transform.position = p;
            _smoothFocus = p;
        }

        private void SpawnGhosts()
        {
            for (var i = 0; i < _data.Players.Count; i++)
            {
                var player = _data.Players[i];
                var g = new Ghost { Player = player };
                try
                {
                    var holder = new GameObject("Hayalet_" + player.Id).transform;
                    holder.SetParent(_runtimeRoot, false);
                    g.Root = holder;
                    var look = SoldierLook.ForTeam(player.Team, new System.Random(player.Id * 7919 + 13));
                    var model = SoldierModel.Build(holder, look, null, false, GameLayers.Default);
                    if (model != null)
                    {
                        model.AutoSyncEquipment = false;
                        model.AutoPlayDeath = false;
                        model.AutoSyncWeapon = false;
                        model.SetEquipment(1, 1, 0);
                        g.Model = model;
                    }
                    g.Label = CreateLabel(holder, player);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Tekrar] Hayalet kurulamadı (" + player.Id + "): " + e.Message);
                }
                _ghosts.Add(g);
                _ghostById[player.Id] = g;
            }
        }

        private TextMesh CreateLabel(Transform parent, ReplayPlayer player)
        {
            var font = UiTheme.Font;
            if (font == null)
                return null;
            var go = new GameObject("Etiket");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 2.25f, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = player.Name;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.characterSize = 0.06f;
            tm.fontSize = 48;
            var local = _data.FindPlayer(_data.LocalPlayerId);
            var ally = local != null && local.Team == player.Team;
            tm.color = ally ? UiTheme.AllyBlue : UiTheme.EnemyRed;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
                mr.sharedMaterial = font.material;
            return tm;
        }

        private Material LineMaterial()
        {
            if (_lineMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null)
                    _lineMaterial = new Material(shader);
            }
            return _lineMaterial;
        }

        private void SetupZoneRing()
        {
            var go = new GameObject("BölgeHalkası");
            go.transform.SetParent(_runtimeRoot, false);
            _zoneRing = go.AddComponent<LineRenderer>();
            _zoneRing.loop = true;
            _zoneRing.useWorldSpace = true;
            _zoneRing.positionCount = 128;
            _zoneRing.widthMultiplier = 2.5f;
            var mat = LineMaterial();
            if (mat != null)
                _zoneRing.sharedMaterial = mat;
            _zoneRing.startColor = _zoneRing.endColor = new Color(0.2f, 0.5f, 1f, 0.9f);
            _zoneRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ Döngü

        private void Update()
        {
            if (_data == null)
                return;

            HandleInput();

            var dt = Time.unscaledDeltaTime;
            var step = _playing ? dt * Speeds[_speedIndex] : 0f;
            var prev = _time;
            _time = Mathf.Clamp(_time + step, 0f, _data.Duration);
            if (_playing && _time >= _data.Duration)
            {
                _playing = false;
                _ended = true;
            }

            Apply(prev, step);
            UpdateCamera(dt);
            UpdateExtras(dt);
            UpdateFlashes();
            UpdateUi();
        }

        private void HandleInput()
        {
            var kb = Keyboard.current;
            if (kb == null)
                return;

            if (kb.escapeKey.wasPressedThisFrame)
            {
                OverlayState.ConsumeEscape();
                Exit();
                return;
            }
            if (kb.spaceKey.wasPressedThisFrame) TogglePlay();
            if (kb.rightArrowKey.wasPressedThisFrame) Seek(_time + 5f);
            if (kb.leftArrowKey.wasPressedThisFrame) Seek(_time - 5f);
            if (kb.periodKey.wasPressedThisFrame || kb.equalsKey.wasPressedThisFrame) ChangeSpeed(1);
            if (kb.commaKey.wasPressedThisFrame || kb.minusKey.wasPressedThisFrame) ChangeSpeed(-1);
            if (kb.fKey.wasPressedThisFrame) ToggleCamera();
            if (kb.tabKey.wasPressedThisFrame) CycleTarget(kb.leftShiftKey.isPressed ? -1 : 1);
            ExtraInput(kb);
        }

        public void TogglePlay()
        {
            if (_ended && !_playing)
            {
                Seek(0f);
                _ended = false;
            }
            _playing = !_playing;
        }

        public void ChangeSpeed(int delta) => _speedIndex = Mathf.Clamp(_speedIndex + delta, 0, Speeds.Length - 1);

        public void Seek(float t)
        {
            _time = Mathf.Clamp(t, 0f, _data.Duration);
            _eventCursor = ReplayTimeline.FirstEventAtOrAfter(_data.Events, _time);
            _feed.Clear();
            _feedTimes.Clear();
            ClearDamageNumbers();
            _ended = false;
            for (var i = 0; i < _ghosts.Count; i++)
                _ghosts[i].HasLast = false;
            Apply(_time, 0f);
        }

        public void ToggleCamera()
        {
            _follow = !_follow && _followId >= 0;
            if (!_follow && _camera != null)
            {
                var e = _camera.transform.eulerAngles;
                _freeYaw = e.y;
                _freePitch = e.x > 180f ? e.x - 360f : e.x;
            }
            else if (_ghostById.TryGetValue(_followId, out var g))
            {
                _orbitYaw = g.Yaw;
            }
        }

        public void CycleTarget(int dir)
        {
            if (_data.Players.Count == 0)
                return;
            var index = 0;
            for (var i = 0; i < _data.Players.Count; i++)
                if (_data.Players[i].Id == _followId)
                    index = i;
            index = ((index + dir) % _data.Players.Count + _data.Players.Count) % _data.Players.Count;
            _followId = _data.Players[index].Id;
            _follow = true;
            _focusInit = false;
            if (_ghostById.TryGetValue(_followId, out var g))
                _orbitYaw = g.Yaw;
        }

        public void Exit()
        {
            GameSession.ReturnToMainMenu();
        }

        // ------------------------------------------------------------------ Oynatma

        private void Apply(float prevTime, float dt)
        {
            var frameIndex = ReplayTimeline.FindFrame(_data.Frames, _time);
            if (frameIndex >= 0)
            {
                var f = _data.Frames[frameIndex];
                var next = frameIndex + 1 < _data.Frames.Count ? _data.Frames[frameIndex + 1] : f;
                var span = next.Time - f.Time;
                var t = span > 1e-5f ? Mathf.Clamp01((_time - f.Time) / span) : 0f;
                UpdateZone(Mathf.Lerp(f.ZoneX, next.ZoneX, t), Mathf.Lerp(f.ZoneZ, next.ZoneZ, t), Mathf.Lerp(f.ZoneRadius, next.ZoneRadius, t));
            }

            for (var i = 0; i < _ghosts.Count; i++)
                ApplyGhost(_ghosts[i], dt);

            // Olaylar yalnızca ileri oynatmada tüketilir.
            while (_eventCursor < _data.Events.Count && _data.Events[_eventCursor].Time < _time)
            {
                var e = _data.Events[_eventCursor++];
                if (dt > 0f && e.Time >= prevTime)
                    PlayEvent(e);
            }
        }

        private void ApplyGhost(Ghost g, float dt)
        {
            if (g.Root == null)
                return;

            if (!ReplayTimeline.Sample(_data, g.Player.Id, _time, out var s))
            {
                SetGhostVisible(g, false);
                return;
            }

            SetGhostVisible(g, true);
            var pos = new Vector3(s.X, s.Y, s.Z);
            g.Position = pos;
            g.Yaw = s.Yaw;
            g.Pitch = s.Pitch;
            g.Root.SetPositionAndRotation(pos, Quaternion.Euler(0f, s.Yaw, 0f));

            var model = g.Model;
            if (model == null)
                return;

            if (s.Weapon != g.Weapon)
            {
                g.Weapon = s.Weapon;
                try
                {
                    var name = _data.WeaponName(s.Weapon);
                    model.HoldWeapon(!string.IsNullOrEmpty(name) && WeaponCatalog.TryGet(name, out var def) ? def : null);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Tekrar] Silah gösterilemedi: " + e.Message);
                }
            }

            if (!s.Alive)
            {
                if (!g.Dead)
                {
                    g.Dead = true;
                    model.PlayDeath(g.Root.forward);
                }
                g.HasLast = false;
                return;
            }

            if (g.Dead)
            {
                g.Dead = false;
                model.ResetPose();
            }

            var velocity = Vector3.zero;
            if (g.HasLast && dt > 1e-4f)
            {
                velocity = (pos - g.LastPos) / dt;
                velocity.y = 0f;
                if (velocity.magnitude > 12f)
                    velocity = Vector3.zero;
            }
            g.LastPos = pos;
            g.HasLast = true;
            model.SetLocomotion(velocity, (Stance)Mathf.Clamp(s.Stance, 0, 2), true);
            model.SetAimPitch(s.Pitch);
        }

        private static void SetGhostVisible(Ghost g, bool visible)
        {
            if (g.Visible == visible && g.Root.gameObject.activeSelf == visible)
                return;
            g.Visible = visible;
            g.Root.gameObject.SetActive(visible);
        }

        private void UpdateZone(float x, float z, float r)
        {
            if (_zoneRing == null)
                return;
            if (r < 1f)
            {
                _zoneRing.enabled = false;
                return;
            }
            _zoneRing.enabled = true;
            const float y = 3f;
            for (var i = 0; i < _zoneRing.positionCount; i++)
            {
                var a = i / (float)_zoneRing.positionCount * Mathf.PI * 2f;
                var px = x + Mathf.Cos(a) * r;
                var pz = z + Mathf.Sin(a) * r;
                _zoneRing.SetPosition(i, new Vector3(px, GroundY(px, pz) + y, pz));
            }
        }

        private static float GroundY(float x, float z)
        {
            return Physics.Raycast(new Vector3(x, 500f, z), Vector3.down, out var hit, 1000f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : 0f;
        }

        private void PlayEvent(ReplayEvent e)
        {
            switch (e.Type)
            {
                case ReplayEventType.Shot:
                    if (_ghostById.TryGetValue(e.Actor, out var shooter) && shooter.Model != null && !shooter.Dead && shooter.Visible)
                        shooter.Model.PlayFire();
                    break;
                case ReplayEventType.Hit:
                    SpawnDamageNumber(e);
                    break;
                case ReplayEventType.Explosion:
                    SpawnFlash(new Vector3(e.X, e.Y, e.Z), Mathf.Max(2f, e.Value));
                    AddFeed("PATLAMA");
                    break;
                case ReplayEventType.Death:
                    AddFeed(NameOf(e.Actor) + (e.Actor == e.Target || e.Actor < 0 ? "" : "  >  " + NameOf(e.Target))
                            + (e.Actor == e.Target || e.Actor < 0 ? "  elendi" : "") + WeaponSuffix(e) + (e.Flag ? "  [KAFA]" : ""));
                    break;
            }
        }

        private string WeaponSuffix(ReplayEvent e)
        {
            var w = _data.WeaponName(e.Weapon);
            if (string.IsNullOrEmpty(w))
                return string.Empty;
            return WeaponCatalog.TryGet(w, out var def) && def != null && !string.IsNullOrEmpty(def.DisplayName) ? "  (" + def.DisplayName + ")" : "  (" + w + ")";
        }

        private string NameOf(int id)
        {
            var p = _data.FindPlayer(id);
            return p != null ? p.Name : "Bilinmeyen";
        }

        private void AddFeed(string line)
        {
            _feed.Add(line);
            _feedTimes.Add(Time.unscaledTime);
            if (_feed.Count > 6)
            {
                _feed.RemoveAt(0);
                _feedTimes.RemoveAt(0);
            }
        }

        private void SpawnFlash(Vector3 pos, float radius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "PatlamaIşığı";
            var col = go.GetComponent<Collider>();
            if (col != null)
                Destroy(col);
            go.transform.SetParent(_runtimeRoot, false);
            go.transform.position = pos;
            var mr = go.GetComponent<MeshRenderer>();
            var mat = LineMaterial();
            if (mr != null && mat != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            _flashes.Add(new Flash { Transform = go.transform, Start = Time.unscaledTime, Radius = radius });
        }

        private void UpdateFlashes()
        {
            for (var i = _flashes.Count - 1; i >= 0; i--)
            {
                var f = _flashes[i];
                var age = Time.unscaledTime - f.Start;
                if (f.Transform == null || age > 0.6f)
                {
                    if (f.Transform != null)
                        Destroy(f.Transform.gameObject);
                    _flashes.RemoveAt(i);
                    continue;
                }
                var k = age / 0.6f;
                f.Transform.localScale = Vector3.one * (f.Radius * 2f * Mathf.Sqrt(k));
                var mr = f.Transform.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null)
                {
                    var block = new MaterialPropertyBlock();
                    block.SetColor("_Color", new Color(1f, 0.6f, 0.15f, 0.7f * (1f - k)));
                    mr.SetPropertyBlock(block);
                }
            }
        }

        // ------------------------------------------------------------------ Kamera

        private void UpdateCamera(float dt)
        {
            if (_camera == null)
                return;

            var mouse = Mouse.current;
            var kb = Keyboard.current;
            var look = mouse != null && mouse.rightButton.isPressed ? mouse.delta.ReadValue() : Vector2.zero;
            var wheel = mouse != null ? mouse.scroll.ReadValue().y : 0f;
            var tr = _camera.transform;

            var ctrl = kb != null && kb.leftCtrlKey.isPressed;
            if (_follow && _ghostById.TryGetValue(_followId, out var g) && g.Visible)
            {
                _freeInit = false;
                if (_firstPerson)
                {
                    var eye = g.Position + Vector3.up * 1.62f;
                    var fr = Quaternion.Euler(g.Pitch, g.Yaw, 0f);
                    _smoothFocus = eye;
                    _focusInit = true;
                    tr.SetPositionAndRotation(eye + fr * Vector3.forward * 0.12f, fr);
                    _targetFov = Mathf.Clamp(_targetFov - wheel * 0.02f, 30f, 100f);
                    _fov = Mathf.Lerp(_fov, _targetFov, 1f - Mathf.Exp(-12f * dt));
                    return;
                }
                _orbitYaw += look.x * 0.15f;
                _orbitPitch = Mathf.Clamp(_orbitPitch - look.y * 0.15f, -20f, 70f);
                if (ctrl)
                    _targetFov = Mathf.Clamp(_targetFov - wheel * 0.02f, 30f, 100f);
                else
                    _orbitDistance = Mathf.Clamp(_orbitDistance - wheel * 0.004f, 1.5f, 15f);
                _fov = Mathf.Lerp(_fov, _targetFov, 1f - Mathf.Exp(-12f * dt));
                var rot = Quaternion.Euler(_orbitPitch, _orbitYaw, 0f);
                var focus = g.Position + Vector3.up * (_shoulder ? 1.75f : 1.6f);
                if (_shoulder)
                    focus += rot * Vector3.right * 0.65f;
                _smoothFocus = _focusInit ? Vector3.Lerp(_smoothFocus, focus, 1f - Mathf.Exp(-14f * dt)) : focus;
                _focusInit = true;
                var dist = _shoulder ? Mathf.Min(_orbitDistance, 3.2f) : _orbitDistance;
                var desired = _smoothFocus - rot * Vector3.forward * dist;
                var dir = desired - _smoothFocus;
                if (Physics.SphereCast(_smoothFocus, 0.25f, dir.normalized, out var hit, dir.magnitude, ~0, QueryTriggerInteraction.Ignore)
                    && !hit.transform.IsChildOf(g.Root))
                    desired = _smoothFocus + dir.normalized * Mathf.Max(0.4f, hit.distance - 0.2f);
                tr.SetPositionAndRotation(desired, rot);
                return;
            }

            if (!_freeInit)
            {
                _freeYawTarget = _freeYaw;
                _freePitchTarget = _freePitch;
                _freeVelocity = Vector3.zero;
                _freeInit = true;
            }
            _freeYawTarget += look.x * 0.15f;
            _freePitchTarget = Mathf.Clamp(_freePitchTarget - look.y * 0.15f, -89f, 89f);
            var k = 1f - Mathf.Exp(-16f * dt);
            _freeYaw = Mathf.LerpAngle(_freeYaw, _freeYawTarget, k);
            _freePitch = Mathf.Lerp(_freePitch, _freePitchTarget, k);
            _targetFov = Mathf.Clamp(_targetFov - wheel * 0.02f, 20f, 100f);
            _fov = Mathf.Lerp(_fov, _targetFov, 1f - Mathf.Exp(-10f * dt));
            var rotation = Quaternion.Euler(_freePitch, _freeYaw, 0f);
            var move = Vector3.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed) move += Vector3.forward;
                if (kb.sKey.isPressed) move += Vector3.back;
                if (kb.dKey.isPressed) move += Vector3.right;
                if (kb.aKey.isPressed) move += Vector3.left;
                if (kb.eKey.isPressed) move += Vector3.up;
                if (kb.qKey.isPressed) move += Vector3.down;
            }
            var speed = (kb != null && kb.leftShiftKey.isPressed ? 60f : 15f);
            var wanted = rotation * move.normalized * speed;
            _freeVelocity = Vector3.Lerp(_freeVelocity, wanted, 1f - Mathf.Exp(-9f * dt));
            tr.SetPositionAndRotation(tr.position + _freeVelocity * dt, rotation);
        }

        private void OnDestroy()
        {
            if (_lineMaterial != null)
                Destroy(_lineMaterial);
        }
    }
}
