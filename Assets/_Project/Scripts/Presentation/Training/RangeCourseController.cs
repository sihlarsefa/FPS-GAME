using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Dialogue;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;
using Project.Infrastructure.Audio.Dialogue;
using Project.Infrastructure.Player;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.Vehicles;
using Project.Infrastructure.World;
using Project.Presentation.Player;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.Training
{
    /// <summary>
    /// Poligon Pro istasyonları: Tepki Atışı (bölge + tepki süresi puanı), Ray Hedefleri, zamanlı Atış Evi (rehineler, par,
    /// yıldız), Bomba Çukuru ve Araç Pisti (Kirpi + Cobra; T-70 iniş pedi). Pedlere E ile (araç pistinde Enter) başlanır,
    /// F6 skor tablosunu açar; rekorlar ISettingsStore'a yazılır. Menzil subayı replikleri mesaj + DialogueDirector ile verilir.
    /// ChallengeController'dan bağımsız çalışır (farklı pedler, farklı hedef kimlikleri).
    /// </summary>
    public sealed class RangeCourseController : MonoBehaviour
    {
        private const int IdBase = 7000;
        private const float PadRadius = 2.5f;
        private const string OfficerPrefix = "MENZİL SUBAYI: ";

        private enum Phase { Idle, Running, Results }

        private sealed class TargetInfo
        {
            public DamageableTarget Target;
            public bool Hostage;
            public bool HitByPlayer;
            public bool Scored;
            public HitZone LastZone = HitZone.Torso;
            public float SpawnAt;
            public float ExpireAt = -1f;
            public float RemoveAt = -1f;
            public float Speed;
        }

        private IEventBus _bus;
        private IDamageableRegistry _registry;
        private PlayerController _player;
        private ISettingsStore _store;
        private PlayerId _localId;
        private Action<string, float> _message;

        private Phase _phase;
        private RangeStation _station;
        private Transform _root;
        private Transform _runRoot;
        private Vector3 _origin;
        private Vector3 _forward = Vector3.forward;
        private Vector3 _right = Vector3.right;
        private Vector3 _frame;           // koşunun yerel başlangıcı (ev girişi / çukur / sürüş çizgisi)
        private float _startTime;
        private float _limit;
        private int _score;
        private int _hits;
        private int _misses;
        private int _kills;
        private int _hostageHits;
        private int _throws;
        private int _nextId = IdBase;
        private int _scheduleIndex;
        private int _checkpoint;
        private int _barkCounter;
        private float _nextBarkAt;
        private float _par;
        private bool _houseStarted;
        private bool _subscribed;
        private int _threatTotal;
        private List<RangePopUp> _schedule;
        private readonly List<TargetInfo> _targets = new List<TargetInfo>(32);
        private readonly Dictionary<DamageableTarget, TargetInfo> _byTarget = new Dictionary<DamageableTarget, TargetInfo>();
        private readonly List<GameObject> _checkpointMarks = new List<GameObject>(4);

        private Text _prompt;
        private Text _hud;
        private GameObject _panel;
        private Text _panelTitle;
        private Text _panelBody;
        private bool _boardOpen;

        public bool IsRunning => _phase == Phase.Running;

        public static RangeCourseController Create(Transform parent, IEventBus bus, IDamageableRegistry registry,
            PlayerController player, PlayerId localId, ISettingsStore store, Vector3 origin, Vector3 rangeForward,
            Action<string, float> message)
        {
            if (bus == null || registry == null || player == null)
                return null;

            var go = new GameObject("[Poligon Pro]");
            if (parent != null)
                go.transform.SetParent(parent, false);
            var c = go.AddComponent<RangeCourseController>();
            c._bus = bus;
            c._registry = registry;
            c._player = player;
            c._localId = localId;
            c._store = store;
            c._message = message;
            c._root = go.transform;
            c._origin = new Vector3(origin.x, origin.y, origin.z);
            var f = rangeForward;
            f.y = 0f;
            c._forward = f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward;
            c._right = new Vector3(c._forward.z, 0f, -c._forward.x);
            c._bus.Subscribe<ExplosionEvent>(c.OnExplosion);
            c._subscribed = true;
            c.BuildUi();
            c.BuildPads();
            c.BuildVehiclePad();
            c.Bark(RangeBark.Welcome, true);
            return c;
        }

        // ------------------------------------------------------------------ Yerleşim

        /// <summary>Ped konumu: atış çizgisinin 4 m gerisi, yanlama istasyona göre.</summary>
        public Vector3 PadPosition(RangeStation station)
        {
            float x;
            switch (station)
            {
                case RangeStation.Reaction: x = -75f; break;
                case RangeStation.Rails: x = -60f; break;
                case RangeStation.ShootHouse: x = 60f; break;
                case RangeStation.GrenadePit: x = 75f; break;
                default: x = 90f; break;
            }

            return _origin + _right * x - _forward * 4f;
        }

        private Vector3 HouseOrigin => _origin + _right * -70f + _forward * 22f;

        private void BuildPads()
        {
            foreach (RangeStation s in Enum.GetValues(typeof(RangeStation)))
            {
                var p = PadPosition(s);
                var disc = StructureKit.CreateCylinder(_root, "Ped_" + s, p + Vector3.up * 0.03f, PadRadius, 0.06f, MaterialId.LandingZone, false);
                StructureKit.MarkStatic(disc);
                AddLabel(_root, (int)s + 1 + " " + RangeScoring.StationName(s).ToUpperInvariant(), p + Vector3.up * 2.2f, 0.07f, Color.white);
            }
        }

        private void BuildVehiclePad()
        {
            var p = PadPosition(RangeStation.VehiclePad);
            var yaw = StructureKit.YawOf(_forward);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var deck = StructureKit.CreateBox(_root, "AracPlatform", p + _forward * 14f + Vector3.up * 0.05f, new Vector3(18f, 0.1f, 22f), rot, MaterialId.Concrete);
            StructureKit.MarkStatic(deck);
            AddLabel(_root, "ARAÇ TEST PİSTİ — KİRPİ / COBRA", p + _forward * 4f + Vector3.up * 3.2f, 0.08f, Color.white);

            try { DrivableVehicle.Spawn(p + _forward * 9f + _right * -3.5f + Vector3.up * 0.4f, yaw); }
            catch (Exception e) { Debug.LogException(e); }
            try { DrivableVehicle.Spawn(p + _forward * 9f + _right * 3.5f + Vector3.up * 0.4f, yaw, VehicleConfig.Cobra); }
            catch (Exception e) { Debug.LogException(e); }

            // T-70 iniş pedi (helikopter hizalama işareti).
            var heli = p + _right * 28f + _forward * 14f;
            var pad = StructureKit.CreateCylinder(_root, "T70Ped", heli + Vector3.up * 0.06f, 8f, 0.12f, MaterialId.Concrete);
            StructureKit.MarkStatic(pad);
            var ring = StructureKit.CreateCylinder(_root, "T70Halka", heli + Vector3.up * 0.13f, 6.2f, 0.02f, MaterialId.Yellow, false);
            StructureKit.MarkStatic(ring);
            var inner = StructureKit.CreateCylinder(_root, "T70Ic", heli + Vector3.up * 0.15f, 5.8f, 0.02f, MaterialId.Concrete, false);
            StructureKit.MarkStatic(inner);
            AddLabel(_root, "H  T-70 İNİŞ PEDİ", heli + Vector3.up * 0.5f, 0.09f, Color.white, true);
        }

        private static void AddLabel(Transform parent, string text, Vector3 position, float size, Color color, bool lying = false)
        {
            var go = new GameObject("Etiket");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = lying ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.characterSize = size;
            mesh.fontSize = 64;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            var font = UiFactory.DefaultFont;
            if (font != null)
            {
                mesh.font = font;
                var r = go.GetComponent<MeshRenderer>();
                if (r != null && font.material != null)
                    r.sharedMaterial = font.material;
            }
        }

        // ------------------------------------------------------------------ Arayüz

        private void BuildUi()
        {
            var canvas = UiFactory.CreateCanvas("PoligonProUI", 15);
            canvas.transform.SetParent(transform, false);

            _prompt = Anchored(canvas.transform, 24, TextAnchor.MiddleCenter, UiTheme.Amber, new Vector2(0.5f, 0.22f), new Vector2(900f, 90f));
            _hud = Anchored(canvas.transform, 22, TextAnchor.UpperCenter, Color.white, new Vector2(0.5f, 0.96f), new Vector2(1000f, 90f));
            _hud.rectTransform.pivot = new Vector2(0.5f, 1f);

            var panel = UiFactory.Panel(canvas.transform, UiTheme.PanelDark);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(680f, 420f);
            _panelTitle = UiFactory.Label(panel, string.Empty, 32, TextAnchor.UpperCenter, UiTheme.Amber, FontStyle.Bold);
            Stretch(_panelTitle.rectTransform, -18f, 44f);
            _panelBody = UiFactory.Label(panel, string.Empty, 21, TextAnchor.UpperCenter, Color.white, FontStyle.Normal);
            Stretch(_panelBody.rectTransform, -76f, 330f);
            _panel = panel.gameObject;
            _panel.SetActive(false);
        }

        private static Text Anchored(Transform parent, int size, TextAnchor anchor, Color color, Vector2 anchorPos, Vector2 sizeDelta)
        {
            var t = UiFactory.Label(parent, string.Empty, size, anchor, color, FontStyle.Bold);
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = anchorPos;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = Vector2.zero;
            r.sizeDelta = sizeDelta;
            return t;
        }

        private static void Stretch(RectTransform r, float y, float h)
        {
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, y);
            r.sizeDelta = new Vector2(-40f, h);
        }

        private void ShowPanel(string title, string body)
        {
            UiFactory.SetText(_panelTitle, title);
            UiFactory.SetText(_panelBody, body);
            if (_panel != null)
                _panel.SetActive(true);
        }

        private void HidePanel()
        {
            _boardOpen = false;
            if (_panel != null)
                _panel.SetActive(false);
        }

        /// <summary>Tüm istasyonların en iyi skor/süre/yıldız tablosu.</summary>
        public string BuildScoreboard()
        {
            var sb = new System.Text.StringBuilder(512);
            foreach (RangeStation s in Enum.GetValues(typeof(RangeStation)))
            {
                sb.Append(RangeScoring.StationName(s).PadRight(14));
                sb.Append("  Skor ").Append(RangeScoring.GetBestScore(_store, s));
                sb.Append("   Süre ").Append(RangeScoring.FormatTime(RangeScoring.GetBestTimeMs(_store, s)));
                if (s == RangeStation.ShootHouse)
                    sb.Append("   [").Append(RangeScoring.StarText(RangeScoring.GetBestStars(_store, s))).Append(']');
                sb.Append("   x").Append(RangeScoring.GetRuns(_store, s)).Append('\n');
            }

            sb.Append("\nF6 / X: kapat");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ Döngü

        private void Update()
        {
            if (_player == null)
                return;

            var kb = Keyboard.current;
            var input = kb != null && Time.timeScale > 0f && !OverlayState.TextInputActive && _player.GameplayInputActive;

            switch (_phase)
            {
                case Phase.Idle: TickIdle(kb, input); break;
                case Phase.Running: TickRunning(kb, input); break;
                case Phase.Results: TickResults(kb, input); break;
            }
        }

        private Vector3 TrackedPosition()
        {
            if (_player.IsInVehicle && _player.Vehicle != null)
                return _player.Vehicle.transform.position;
            return _player.transform.position;
        }

        private bool TryFindPad(out RangeStation station)
        {
            station = RangeStation.Reaction;
            var p = TrackedPosition();
            foreach (RangeStation s in Enum.GetValues(typeof(RangeStation)))
            {
                var d = p - PadPosition(s);
                d.y = 0f;
                var radius = s == RangeStation.VehiclePad ? 9f : PadRadius;
                if (d.sqrMagnitude <= radius * radius)
                {
                    station = s;
                    return true;
                }
            }

            return false;
        }

        private void TickIdle(Keyboard kb, bool input)
        {
            if (input && kb.f6Key.wasPressedThisFrame)
            {
                if (_boardOpen) HidePanel();
                else { _boardOpen = true; ShowPanel("POLİGON SKOR TABLOSU", BuildScoreboard()); }
            }
            else if (_boardOpen && input && (kb.xKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
            {
                HidePanel();
            }

            if (TryFindPad(out var s))
            {
                var key = s == RangeStation.VehiclePad ? "ENTER" : "E";
                var stars = s == RangeStation.ShootHouse ? " " + RangeScoring.StarText(RangeScoring.GetBestStars(_store, s)) : string.Empty;
                SetPrompt("[" + key + "] " + RangeScoring.StationName(s) + " — Skor " + RangeScoring.GetBestScore(_store, s)
                          + "  Süre " + RangeScoring.FormatTime(RangeScoring.GetBestTimeMs(_store, s)) + stars + "\n"
                          + RangeScoring.StationDescription(s));
                var pressed = s == RangeStation.VehiclePad ? kb != null && kb.enterKey.wasPressedThisFrame : kb != null && kb.eKey.wasPressedThisFrame;
                if (input && pressed)
                    Begin(s);
            }
            else
            {
                SetPrompt(string.Empty);
            }
        }

        private void SetPrompt(string text)
        {
            if (_prompt != null)
                UiFactory.SetText(_prompt, text);
        }

        private void TickRunning(Keyboard kb, bool input)
        {
            var combatant = _player.Combatant;
            if (combatant != null && combatant.IsInitialized && !combatant.IsAlive)
            {
                Finish("Vuruldun");
                return;
            }

            if (input && kb.xKey.wasPressedThisFrame)
            {
                Bark(RangeBark.CeaseFire, true);
                Finish("İptal");
                return;
            }

            var elapsed = Time.time - _startTime;
            switch (_station)
            {
                case RangeStation.Reaction: TickReaction(elapsed); break;
                case RangeStation.Rails: TickRails(elapsed); break;
                case RangeStation.ShootHouse: TickHouse(); break;
                case RangeStation.GrenadePit:
                    if (_throws >= RangeScoring.GrenadeThrows) { Finish("Tamamlandı"); return; }
                    break;
                case RangeStation.VehiclePad: TickDrive(); break;
            }

            if (_phase != Phase.Running)
                return;

            TickRemovals();

            var houseWaiting = _station == RangeStation.ShootHouse && !_houseStarted;
            if (!houseWaiting && elapsed >= _limit)
            {
                if (_station == RangeStation.ShootHouse)
                    Bark(RangeBark.SlowTime, true);
                Finish("Süre doldu");
                return;
            }

            UiFactory.SetText(_hud, BuildHud(houseWaiting ? 0f : elapsed, houseWaiting));
        }

        private string BuildHud(float elapsed, bool waiting)
        {
            var name = RangeScoring.StationName(_station).ToUpperInvariant();
            var head = name + "   Süre " + (waiting ? "--" : Mathf.CeilToInt(Mathf.Max(0f, _limit - elapsed)).ToString());
            switch (_station)
            {
                case RangeStation.Reaction: head += "   Puan " + _score + "   İsabet " + _hits + "   Kaçan " + _misses; break;
                case RangeStation.Rails: head += "   Puan " + _score + "   Vurulan " + _kills; break;
                case RangeStation.ShootHouse:
                    head += waiting ? "   Koridora gir!" : "   Tehdit " + _kills + "/" + _threatTotal + "   Rehine " + _hostageHits + "   Par " + _par.ToString("0");
                    break;
                case RangeStation.GrenadePit: head += "   Puan " + _score + "   Bomba " + _throws + "/" + RangeScoring.GrenadeThrows; break;
                case RangeStation.VehiclePad: head += "   Kontrol " + _checkpoint + "/" + RangeScoring.DriveCheckpointCount; break;
            }

            return head + "\nX: iptal";
        }

        private void TickResults(Keyboard kb, bool input)
        {
            if (!input)
                return;
            if (kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)
            {
                HidePanel();
                Begin(_station);
            }
            else if (kb.xKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
            {
                HidePanel();
                _phase = Phase.Idle;
            }
            else if (kb.f6Key.wasPressedThisFrame)
            {
                _boardOpen = true;
                ShowPanel("POLİGON SKOR TABLOSU", BuildScoreboard());
                _phase = Phase.Idle;
            }
        }

        // ------------------------------------------------------------------ Başlat / bitir

        private void Begin(RangeStation station)
        {
            Cleanup();
            HidePanel();
            _station = station;
            _phase = Phase.Running;
            _score = _hits = _misses = _kills = _hostageHits = _throws = _scheduleIndex = _checkpoint = 0;
            _threatTotal = 0;
            _houseStarted = false;
            _nextId = IdBase;
            _startTime = Time.time;
            _runRoot = new GameObject("Istasyon_" + station).transform;
            _runRoot.SetParent(_root, false);
            SetPrompt(string.Empty);
            var seed = Environment.TickCount & 0x7fffffff;

            try
            {
                switch (station)
                {
                    case RangeStation.Reaction: StartReaction(seed); break;
                    case RangeStation.Rails: StartRails(); break;
                    case RangeStation.ShootHouse: StartHouse(seed); break;
                    case RangeStation.GrenadePit: StartPit(); break;
                    case RangeStation.VehiclePad: StartDrive(); break;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Cleanup();
                _phase = Phase.Idle;
                return;
            }

            Bark(station == RangeStation.GrenadePit ? RangeBark.GrenadeOut
                : station == RangeStation.VehiclePad ? RangeBark.VehicleStart : RangeBark.StationStart, true);
        }

        private void Finish(string reason)
        {
            if (_phase != Phase.Running)
                return;

            var elapsed = Time.time - _startTime;
            var score = _score;
            var timeMs = 0;
            var stars = 0;
            var detail = string.Empty;

            switch (_station)
            {
                case RangeStation.Reaction:
                    detail = "İsabet: " + _hits + "/" + RangeScoring.ReactionTargets + "   Kaçan: " + _misses
                             + "\nDerece: " + RangeScoring.ReactionRank(_score, RangeScoring.ReactionTargets);
                    break;
                case RangeStation.Rails:
                    detail = "Vurulan hedef: " + _kills;
                    break;
                case RangeStation.ShootHouse:
                    var cleared = reason == "Temizlendi";
                    score = RangeScoring.HouseScore(_kills, _hostageHits, elapsed, _par, cleared);
                    stars = RangeScoring.HouseStars(cleared, _hostageHits, elapsed, _par);
                    if (cleared) timeMs = Mathf.RoundToInt(elapsed * 1000f);
                    detail = "Tehdit: " + _kills + "/" + _threatTotal + "   Rehine vuruşu: " + _hostageHits
                             + "\nPar: " + _par.ToString("0") + " sn   Yıldız: " + RangeScoring.StarText(stars);
                    break;
                case RangeStation.GrenadePit:
                    detail = "Atılan bomba: " + _throws + "/" + RangeScoring.GrenadeThrows;
                    break;
                case RangeStation.VehiclePad:
                    var done = _checkpoint >= RangeScoring.DriveCheckpointCount;
                    score = done ? Mathf.Max(0, Mathf.RoundToInt((RangeScoring.DriveTimeLimit - elapsed) * 10f)) : 0;
                    if (done) timeMs = Mathf.RoundToInt(elapsed * 1000f);
                    detail = "Kontrol noktası: " + _checkpoint + "/" + RangeScoring.DriveCheckpointCount;
                    break;
            }

            RangeScoring.RecordRun(_store, _station, score, timeMs, stars, out var newScore, out var newTime);
            Cleanup();
            _phase = Phase.Results;
            UiFactory.SetText(_hud, string.Empty);

            if (_station == RangeStation.ShootHouse && reason == "Temizlendi")
                Bark(RangeBark.CourseClear, true);
            if (newScore || newTime)
                Bark(RangeBark.NewRecord, true);

            var body = reason + "\n\nPUAN: " + score + "   Süre: " + elapsed.ToString("0.0") + " sn\n" + detail
                       + "\nEn iyi skor: " + RangeScoring.GetBestScore(_store, _station)
                       + "   En iyi süre: " + RangeScoring.FormatTime(RangeScoring.GetBestTimeMs(_store, _station))
                       + (newScore || newTime ? "   YENİ REKOR!" : string.Empty)
                       + "\n\nEnter: tekrar   F6: skor tablosu   X / Esc: kapat";
            ShowPanel(RangeScoring.StationName(_station).ToUpperInvariant(), body);
        }

        private void Cleanup()
        {
            for (var i = 0; i < _targets.Count; i++)
            {
                var info = _targets[i];
                if (info.Target == null)
                    continue;
                info.Target.Hit -= OnTargetHit;
                info.Target.KnockedDown -= OnTargetDown;
                info.Target.Respawned -= OnTargetRespawned;
                Destroy(info.Target.gameObject);
            }

            _targets.Clear();
            _byTarget.Clear();
            _checkpointMarks.Clear();
            if (_runRoot != null)
                Destroy(_runRoot.gameObject);
            _runRoot = null;
        }

        private void OnDestroy()
        {
            if (_subscribed && _bus != null)
                _bus.Unsubscribe<ExplosionEvent>(OnExplosion);
            Cleanup();
        }

        // ------------------------------------------------------------------ Menzil subayı

        private void Bark(RangeBark bark, bool force)
        {
            if (!force && Time.time < _nextBarkAt)
                return;

            _nextBarkAt = Time.time + 3.5f;
            var text = RangeScoring.BarkText(bark, _barkCounter++);
            _message?.Invoke(OfficerPrefix + text, 3.2f);

            // Mevcut diyalog sistemine bağlı sesli karşılıklar (replik kitabındaki kategoriler).
            try
            {
                var speaker = _player != null ? _player.Combatant : null;
                if (speaker == null)
                    return;
                if (bark == RangeBark.HostageHit) DialogueDirector.Say(speaker, DialogueCats.FriendlyFire);
                else if (bark == RangeBark.CourseClear) DialogueDirector.Say(speaker, DialogueCats.Clear);
                else if (bark == RangeBark.GrenadeOut) DialogueDirector.Say(speaker, DialogueCats.GrenadeThrow);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // ------------------------------------------------------------------ Tepki Atışı

        private void StartReaction(int seed)
        {
            _limit = 120f;
            _frame = _origin;
            _schedule = RangeScoring.BuildPopUpSchedule(seed);
            EnsureWeapon(WeaponIds.Mpt76);
        }

        private void TickReaction(float elapsed)
        {
            while (_scheduleIndex < _schedule.Count && _schedule[_scheduleIndex].SpawnTime <= elapsed)
            {
                var spec = _schedule[_scheduleIndex++];
                var pos = _origin + _forward * spec.Distance + _right * RangeScoring.LaneLateral(spec.Lane);
                var info = SpawnTarget(pos, false, 100f);
                if (info != null)
                    info.ExpireAt = Time.time + spec.Life;
            }

            for (var i = _targets.Count - 1; i >= 0; i--)
            {
                var info = _targets[i];
                if (info.ExpireAt < 0f || info.Scored || Time.time < info.ExpireAt)
                    continue;
                _misses++;
                Bark(RangeBark.Miss, false);
                RemoveTarget(i);
            }

            if (_scheduleIndex >= _schedule.Count && _targets.Count == 0)
                Finish("Tamamlandı");
        }

        // ------------------------------------------------------------------ Ray Hedefleri

        private void StartRails()
        {
            _limit = RangeScoring.RailsDuration;
            _frame = _origin;
            EnsureWeapon(WeaponIds.Mpt76);

            var distances = new[] { 30f, 60f, 90f };
            var speeds = new[] { 2.5f, 4.5f, 6.5f };
            for (var i = 0; i < distances.Length; i++)
            {
                var a = _origin + _forward * distances[i] - _right * 22f;
                var b = _origin + _forward * distances[i] + _right * 22f;
                var mid = (a + b) * 0.5f;
                var rail = StructureKit.CreateBox(_runRoot, "Ray", mid + Vector3.up * 0.04f, new Vector3(44f, 0.08f, 0.25f),
                    Quaternion.Euler(0f, StructureKit.YawOf(_right), 0f), MaterialId.MetalDark, false);
                StructureKit.MarkStatic(rail);

                var target = DamageableTarget.CreateMoving(a, b, speeds[i], _bus, _registry, _nextId++);
                if (target == null)
                    continue;
                target.RespawnSeconds = 1.5f;
                target.transform.SetParent(_runRoot, true);
                Track(target, false, speeds[i]);
            }
        }

        private void TickRails(float elapsed)
        {
            // Ray hedefleri kendi kendine yeniden kalkar; süre dolunca Finish çağrılır.
        }

        // ------------------------------------------------------------------ Atış Evi

        private void StartHouse(int seed)
        {
            var rooms = 4 + (seed % 3);
            var layout = RangeScoring.BuildShootHouse(seed, rooms);
            _par = RangeScoring.ParSeconds(layout.Rooms.Count);
            _limit = RangeScoring.HouseTimeLimit(layout.Rooms.Count);
            _frame = HouseOrigin;
            _threatTotal = layout.ThreatCount;
            EnsureWeapon(WeaponIds.Sar9);

            BuildHouseWalls(layout);

            for (var i = 0; i < layout.Targets.Count; i++)
            {
                var t = layout.Targets[i];
                var info = SpawnTarget(HouseLocal(t.X, t.Z), t.Hostage, 100f);
                if (info == null || info.Target == null)
                    continue;
                info.Target.FacePlayer = !t.Hostage;
                if (t.Hostage)
                    Tint(info.Target.gameObject, new Color(0.25f, 0.85f, 0.35f));
            }

            _message?.Invoke("Atış evi hazır: " + layout.ThreatCount + " tehdit, " + layout.HostageCount + " rehine. Koridora gir!", 4f);
        }

        private Vector3 HouseLocal(float x, float z) => _frame + _right * x + _forward * z;

        private void BuildHouseWalls(HouseLayout layout)
        {
            var yaw = Quaternion.Euler(0f, StructureKit.YawOf(_forward), 0f);
            const float h = 3f;
            const float t = 0.2f;
            var outer = HouseLayout.CorridorHalfWidth + HouseLayout.RoomWidth;
            var len = layout.Length;

            Wall(yaw, -outer, 0f, -outer, len, h, t);
            Wall(yaw, outer, 0f, outer, len, h, t);
            Wall(yaw, -outer, len, outer, len, h, t);
            Wall(yaw, -outer, 0f, -HouseLayout.CorridorHalfWidth, 0f, h, t);
            Wall(yaw, HouseLayout.CorridorHalfWidth, 0f, outer, 0f, h, t);

            for (var row = 0; row < layout.Rows; row++)
            {
                var z0 = row * HouseLayout.RowDepth;
                var z1 = z0 + HouseLayout.RowDepth;
                var zc = (z0 + z1) * 0.5f;
                for (var side = -1; side <= 1; side += 2)
                {
                    var x = side * HouseLayout.CorridorHalfWidth;
                    var roomIndex = row * 2 + (side < 0 ? 0 : 1);
                    if (roomIndex < layout.Rooms.Count)
                    {
                        Wall(yaw, x, z0, x, zc - 0.6f, h, t);
                        Wall(yaw, x, zc + 0.6f, x, z1, h, t);
                        Wall(yaw, x, zc - 0.6f, x, zc + 0.6f, 0.7f, t, 2.3f);
                    }
                    else
                    {
                        Wall(yaw, x, z0, x, z1, h, t);
                    }

                    if (row > 0)
                        Wall(yaw, x, z0, side * outer, z0, h, t);
                }
            }
        }

        /// <summary>Yerel (x,z) uçlar arası duvar; baseY &gt; 0 ise (lento) yerden o kadar yukarıda.</summary>
        private void Wall(Quaternion yaw, float x0, float z0, float x1, float z1, float height, float thickness, float baseY = 0f)
        {
            var a = HouseLocal(x0, z0);
            var b = HouseLocal(x1, z1);
            var mid = (a + b) * 0.5f;
            var dx = Mathf.Abs(x1 - x0);
            var dz = Mathf.Abs(z1 - z0);
            var size = new Vector3(Mathf.Max(thickness, dx), height, Mathf.Max(thickness, dz));
            var go = StructureKit.CreateBox(_runRoot, "Duvar", mid + Vector3.up * (baseY + height * 0.5f), size, yaw, MaterialId.Concrete);
            StructureKit.MarkStatic(go);
        }

        private void TickHouse()
        {
            if (!_houseStarted)
            {
                var rel = TrackedPosition() - _frame;
                var f = Vector3.Dot(rel, _forward);
                var l = Vector3.Dot(rel, _right);
                if (f >= 0f && f < 4f && Mathf.Abs(l) < HouseLayout.CorridorHalfWidth + 0.5f)
                {
                    _houseStarted = true;
                    _startTime = Time.time;
                    Bark(RangeBark.StationStart, true);
                }

                return;
            }

            if (_kills >= _threatTotal)
                Finish("Temizlendi");
        }

        // ------------------------------------------------------------------ Bomba Çukuru

        private void StartPit()
        {
            _limit = 100f;
            _frame = TrackedPosition();
            _frame.y = _origin.y;
            var inv = _player.Combatant != null ? _player.Combatant.Inventory : null;
            if (inv != null)
                inv.GiveItem(ItemIds.FragGrenade, RangeScoring.GrenadeThrows);

            for (var i = 0; i < RangeScoring.PitDistances.Length; i++)
            {
                var pos = _frame + _forward * RangeScoring.PitDistances[i];
                var disc = StructureKit.CreateCylinder(_runRoot, "Kova", pos + Vector3.up * 0.04f, RangeScoring.PitBinRadius, 0.08f,
                    i == 2 ? MaterialId.Red : (i == 1 ? MaterialId.Orange : MaterialId.Yellow), false);
                StructureKit.MarkStatic(disc);
                var wall = StructureKit.CreateBox(_runRoot, "Siper", pos + _forward * (RangeScoring.PitBinRadius + 0.6f) + Vector3.up * 0.7f,
                    new Vector3(RangeScoring.PitBinRadius * 2.4f, 1.4f, 0.8f), Quaternion.Euler(0f, StructureKit.YawOf(_forward), 0f), MaterialId.Sandbag);
                StructureKit.MarkStatic(wall);
                AddLabel(_runRoot, RangeScoring.PitPoints[i] + " PUAN — " + (int)RangeScoring.PitDistances[i] + " m", pos + Vector3.up * 2.4f, 0.07f, Color.white);
            }

            _message?.Invoke("Bomba çukuru: " + RangeScoring.GrenadeThrows + " bomba, kovalar 20/30/40 m ileride", 3f);
        }

        private void OnExplosion(ExplosionEvent e)
        {
            if (_phase != Phase.Running || _station != RangeStation.GrenadePit || !e.AttackerId.Equals(_localId))
                return;

            var rel = new Vector3(e.Position.X, 0f, e.Position.Z) - new Vector3(_frame.x, 0f, _frame.z);
            var pts = RangeScoring.PitScore(Vector3.Dot(rel, _forward), Vector3.Dot(rel, _right));
            _score += pts;
            _throws++;
            _message?.Invoke(pts > 0 ? "+" + pts : "Kova dışı", 1.5f);
        }

        // ------------------------------------------------------------------ Araç Pisti

        private void StartDrive()
        {
            _limit = RangeScoring.DriveTimeLimit;
            _frame = PadPosition(RangeStation.VehiclePad);
            _frame.y = _origin.y;
            for (var i = 0; i < RangeScoring.DriveCheckpointCount; i++)
            {
                RangeScoring.DriveCheckpoint(i, out var f, out var l);
                var pos = _frame + _forward * f + _right * l;
                var ring = StructureKit.CreateCylinder(_runRoot, "Kontrol" + (i + 1), pos + Vector3.up * 0.05f, RangeScoring.DriveCheckpointRadius,
                    0.1f, i == 0 ? MaterialId.Green : MaterialId.Yellow, false);
                StructureKit.MarkStatic(ring);
                AddLabel(_runRoot, (i + 1).ToString(), pos + Vector3.up * 3f, 0.14f, Color.white);
                _checkpointMarks.Add(ring);
            }
        }

        private void TickDrive()
        {
            if (_checkpoint >= RangeScoring.DriveCheckpointCount)
            {
                Finish("Tamamlandı");
                return;
            }

            RangeScoring.DriveCheckpoint(_checkpoint, out var f, out var l);
            var target = _frame + _forward * f + _right * l;
            var d = TrackedPosition() - target;
            d.y = 0f;
            if (d.sqrMagnitude > RangeScoring.DriveCheckpointRadius * RangeScoring.DriveCheckpointRadius)
                return;

            if (_checkpoint < _checkpointMarks.Count && _checkpointMarks[_checkpoint] != null)
                _checkpointMarks[_checkpoint].SetActive(false);
            _checkpoint++;
            _message?.Invoke("Kontrol noktası " + _checkpoint + "/" + RangeScoring.DriveCheckpointCount, 1.5f);
            if (_checkpoint < _checkpointMarks.Count && _checkpointMarks[_checkpoint] != null)
                BootstrapTint(_checkpointMarks[_checkpoint], new Color(0.2f, 0.9f, 0.3f));
        }

        private static void BootstrapTint(GameObject go, Color c) => Tint(go, c);

        // ------------------------------------------------------------------ Hedefler

        private TargetInfo SpawnTarget(Vector3 position, bool hostage, float health)
        {
            var target = DamageableTarget.CreateDummy(position, _bus, _registry, _nextId++, health);
            if (target == null)
                return null;

            target.RespawnSeconds = 9999f;
            target.FacePlayer = true;
            if (_runRoot != null)
                target.transform.SetParent(_runRoot, true);
            return Track(target, hostage, 0f);
        }

        private TargetInfo Track(DamageableTarget target, bool hostage, float speed)
        {
            var info = new TargetInfo { Target = target, Hostage = hostage, SpawnAt = Time.time, Speed = speed };
            target.Hit += OnTargetHit;
            target.KnockedDown += OnTargetDown;
            target.Respawned += OnTargetRespawned;
            _targets.Add(info);
            _byTarget[target] = info;
            return info;
        }

        private void RemoveTarget(int index)
        {
            var info = _targets[index];
            if (info.Target != null)
            {
                info.Target.Hit -= OnTargetHit;
                info.Target.KnockedDown -= OnTargetDown;
                info.Target.Respawned -= OnTargetRespawned;
                _byTarget.Remove(info.Target);
                Destroy(info.Target.gameObject);
            }

            _targets.RemoveAt(index);
        }

        private void TickRemovals()
        {
            for (var i = _targets.Count - 1; i >= 0; i--)
            {
                var info = _targets[i];
                if (info.RemoveAt >= 0f && Time.time >= info.RemoveAt)
                    RemoveTarget(i);
            }
        }

        private void OnTargetHit(DamageableTarget target, DamageInfo damage)
        {
            if (_phase != Phase.Running || !damage.AttackerId.Equals(_localId) || !_byTarget.TryGetValue(target, out var info))
                return;

            info.LastZone = RangeScoring.ZoneOf(damage.BodyPart, damage.IsHeadshot);

            if (info.Hostage)
            {
                _hostageHits++;
                _message?.Invoke("REHİNE! -" + (int)RangeScoring.HostagePenalty, 1.5f);
                Bark(RangeBark.HostageHit, false);
                return;
            }

            info.HitByPlayer = true;
            _hits++;

            if (_station == RangeStation.Reaction && !info.Scored)
            {
                info.Scored = true;
                var reaction = Time.time - info.SpawnAt;
                var pts = RangeScoring.PopUpScore(info.LastZone, reaction);
                _score += pts;
                _kills++;
                info.RemoveAt = Time.time + 0.8f;
                info.ExpireAt = -1f;
                _message?.Invoke("+" + pts + "  " + RangeScoring.ZoneName(info.LastZone) + "  " + reaction.ToString("0.00") + " sn", 1.2f);
                Bark(info.LastZone == HitZone.Head ? RangeBark.Headshot : RangeBark.GoodShot, false);
            }
        }

        private void OnTargetDown(DamageableTarget target)
        {
            if (_phase != Phase.Running || !_byTarget.TryGetValue(target, out var info) || info.Scored || info.Hostage || !info.HitByPlayer)
                return;

            switch (_station)
            {
                case RangeStation.Rails:
                    info.Scored = true;
                    _kills++;
                    var pts = RangeScoring.RailScore(info.LastZone, info.Speed);
                    _score += pts;
                    _message?.Invoke("+" + pts + "  " + RangeScoring.ZoneName(info.LastZone), 1.2f);
                    Bark(info.LastZone == HitZone.Head ? RangeBark.Headshot : RangeBark.GoodShot, false);
                    break;
                case RangeStation.ShootHouse:
                    info.Scored = true;
                    _kills++;
                    break;
            }
        }

        private void OnTargetRespawned(DamageableTarget target)
        {
            if (_byTarget.TryGetValue(target, out var info))
            {
                info.Scored = false;
                info.HitByPlayer = false;
            }
        }

        private static void Tint(GameObject go, Color color)
        {
            var block = new MaterialPropertyBlock();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null)
                    continue;
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                r.SetPropertyBlock(block);
            }
        }

        private void EnsureWeapon(string weaponId)
        {
            var inv = _player.Combatant != null ? _player.Combatant.Inventory : null;
            if (inv == null)
                return;

            var slot = inv.FindWeaponSlot(weaponId);
            if (slot < 0)
            {
                inv.GiveWeapon(weaponId, true);
                slot = inv.FindWeaponSlot(weaponId);
            }

            if (slot >= 0)
                inv.SetActiveSlot(slot);
        }
    }
}
