using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;
using Project.Infrastructure.Player;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using Project.Presentation.Bootstrap;
using Project.Presentation.Player;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.Training
{
    /// <summary>
    /// Poligon meydan okumaları (Hızlı Atış, Keskin Nişancı, Kill House, Bomba Atma). Başlangıç pedlerinde E ile başlar;
    /// hedefleri çalışma zamanında üretir, puanı <see cref="ChallengeRules"/> ile hesaplar, rekoru ISettingsStore'a yazar
    /// ve sonuç ekranını gösterir. Eğitim (tutorial) ile aynı sahnede yaşar; yalnızca poligonda kurulur.
    /// </summary>
    public sealed class ChallengeController : MonoBehaviour
    {
        private const int IdBase = 5000;

        // TrainingRangeBuilder.BuildKillHouse ile aynı yerleşim (bina merkezi, yön).
        private static readonly Vector3 HouseA = new Vector3(-30f, 0f, 20f);
        private static readonly Vector3 HouseB = new Vector3(30f, 0f, 18f);
        private const float HouseBYaw = 15f;

        private enum Phase { Idle, Running, Results }

        private sealed class TargetInfo
        {
            public DamageableTarget Target;
            public bool Friendly;
            public float Distance;
            public bool HitByPlayer;
            public bool Headshot;
            public bool Scored;
            public float ExpireAt = -1f;
        }

        private IEventBus _bus;
        private IDamageableRegistry _registry;
        private PlayerController _player;
        private ISettingsStore _store;
        private PlayerId _localId;
        private Action<string, float> _message;

        private Phase _phase;
        private ChallengeKind _kind;
        private Transform _root;
        private Transform _runRoot;
        private Vector3 _origin;
        private Vector3 _forward = Vector3.forward;
        private Vector3 _right = Vector3.right;
        private float _startTime;
        private float _limit;
        private int _score;
        private int _hits;
        private int _kills;
        private int _misses;
        private int _friendlyHits;
        private int _throws;
        private int _nextId = IdBase;
        private int _scheduleIndex;
        private int _enemyTotal;
        private bool _cleared;
        private List<PopUpSpec> _schedule;
        private Vector3 _ringCenter;
        private readonly List<TargetInfo> _targets = new List<TargetInfo>(32);
        private readonly Dictionary<DamageableTarget, TargetInfo> _byTarget = new Dictionary<DamageableTarget, TargetInfo>();

        private Canvas _canvas;
        private Text _prompt;
        private Text _hud;
        private GameObject _resultsPanel;
        private Text _resultsTitle;
        private Text _resultsBody;
        private bool _subscribed;

        /// <summary>Poligon meydan okuma denetleyicisini kurar; başarısızsa null.</summary>
        public static ChallengeController Create(Transform parent, IEventBus bus, IDamageableRegistry registry,
            PlayerController player, PlayerId localId, ISettingsStore store, Vector3 rangeForward, Action<string, float> message)
        {
            if (bus == null || registry == null || player == null)
                return null;

            var go = new GameObject("[Meydan Okumalar]");
            if (parent != null)
                go.transform.SetParent(parent, false);
            var c = go.AddComponent<ChallengeController>();
            c._bus = bus;
            c._registry = registry;
            c._player = player;
            c._localId = localId;
            c._store = store;
            c._message = message;
            c._root = go.transform;
            var f = rangeForward;
            f.y = 0f;
            c._forward = f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward;
            c._right = new Vector3(c._forward.z, 0f, -c._forward.x);
            c._bus.Subscribe<ExplosionEvent>(c.OnExplosion);
            c._subscribed = true;
            c.BuildUi();
            return c;
        }

        public bool IsRunning => _phase == Phase.Running;

        // ------------------------------------------------------------------ Arayüz

        private void BuildUi()
        {
            _canvas = UiFactory.CreateCanvas("MeydanOkumaUI", 14);
            _canvas.transform.SetParent(transform, false);

            _prompt = Anchored(_canvas.transform, 24, TextAnchor.MiddleCenter, UiTheme.Amber, new Vector2(0.5f, 0.22f), new Vector2(900f, 80f));
            _hud = Anchored(_canvas.transform, 22, TextAnchor.UpperCenter, Color.white, new Vector2(0.5f, 0.96f), new Vector2(1000f, 90f));
            _hud.rectTransform.pivot = new Vector2(0.5f, 1f);

            var panel = UiFactory.Panel(_canvas.transform, UiTheme.PanelDark);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(620f, 380f);
            _resultsTitle = UiFactory.Label(panel, string.Empty, 32, TextAnchor.UpperCenter, UiTheme.Amber, FontStyle.Bold);
            Stretch(_resultsTitle.rectTransform, -18f, 44f);
            _resultsBody = UiFactory.Label(panel, string.Empty, 22, TextAnchor.UpperCenter, Color.white, FontStyle.Normal);
            Stretch(_resultsBody.rectTransform, -76f, 290f);
            _resultsPanel = panel.gameObject;
            _resultsPanel.SetActive(false);
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

        // ------------------------------------------------------------------ Döngü

        private void Update()
        {
            if (_player == null)
                return;

            var kb = Keyboard.current;
            var input = kb != null && Time.timeScale > 0f && !OverlayState.TextInputActive && _player.GameplayInputActive;

            switch (_phase)
            {
                case Phase.Idle:
                    TickIdle(kb, input);
                    break;
                case Phase.Running:
                    TickRunning(kb, input);
                    break;
                case Phase.Results:
                    TickResults(kb, input);
                    break;
            }
        }

        private void TickIdle(Keyboard kb, bool input)
        {
            if (TryFindPad(out var kind))
            {
                var best = ChallengeRules.GetBest(_store, kind);
                var text = "[E] " + ChallengeRules.Name(kind) + " — En iyi: " + best + " (" + ChallengeRules.Rank(kind, best) + ")\n"
                           + ChallengeRules.Description(kind);
                SetPrompt(text);
                if (input && kb.eKey.wasPressedThisFrame)
                    Begin(kind);
            }
            else
            {
                SetPrompt(string.Empty);
            }
        }

        private bool TryFindPad(out ChallengeKind kind)
        {
            kind = ChallengeKind.QuickFire;
            var p = _player.transform.position;
            foreach (ChallengeKind k in Enum.GetValues(typeof(ChallengeKind)))
            {
                if (!TrainingRangeBuilder.TryGetWaypoint(ChallengeRules.PadId(k), out var wp))
                    continue;
                var d = p - wp.Position;
                d.y = 0f;
                if (d.sqrMagnitude <= wp.Radius * wp.Radius)
                {
                    kind = k;
                    return true;
                }
            }

            return false;
        }

        private void SetPrompt(string text)
        {
            if (_prompt != null)
                UiFactory.SetText(_prompt, text);
        }

        private void TickRunning(Keyboard kb, bool input)
        {
            var elapsed = Time.time - _startTime;
            var combatant = _player.Combatant;
            if (combatant != null && combatant.IsInitialized && !combatant.IsAlive)
            {
                Finish("Vuruldun");
                return;
            }

            if (input && kb.xKey.wasPressedThisFrame)
            {
                Finish("İptal");
                return;
            }

            switch (_kind)
            {
                case ChallengeKind.QuickFire:
                    TickQuickFire(elapsed);
                    break;
                case ChallengeKind.Sniper:
                    TickExpiry(elapsed);
                    if (_phase == Phase.Running && AllScoredOrGone())
                    {
                        Finish("Tamamlandı");
                        return;
                    }
                    break;
                case ChallengeKind.KillHouse:
                    if (CountEnemiesDown() >= _enemyTotal)
                    {
                        _cleared = true;
                        Finish("Temizlendi");
                        return;
                    }
                    break;
                case ChallengeKind.Grenade:
                    if (_throws >= ChallengeRules.GrenadeThrows)
                    {
                        Finish("Tamamlandı");
                        return;
                    }
                    break;
            }

            if (_phase == Phase.Running && elapsed >= _limit)
            {
                Finish("Süre doldu");
                return;
            }

            if (_phase == Phase.Running)
                UiFactory.SetText(_hud, BuildHud(elapsed));
        }

        private string BuildHud(float elapsed)
        {
            var left = Mathf.Max(0f, _limit - elapsed);
            var head = ChallengeRules.Name(_kind).ToUpperInvariant() + "   Süre " + Mathf.CeilToInt(left) + "   Puan " + Score(elapsed);
            switch (_kind)
            {
                case ChallengeKind.QuickFire: head += "   İsabet " + _hits + "   Kaçan " + _misses; break;
                case ChallengeKind.Sniper: head += "   Hedef " + _kills + "/" + _targets.Count; break;
                case ChallengeKind.KillHouse: head += "   Düşman " + CountEnemiesDown() + "/" + _enemyTotal + "   Dost vuruşu " + _friendlyHits; break;
                case ChallengeKind.Grenade: head += "   Bomba " + _throws + "/" + ChallengeRules.GrenadeThrows; break;
            }

            return head + "\nX: iptal";
        }

        private int Score(float elapsed)
        {
            return _kind == ChallengeKind.KillHouse
                ? ChallengeRules.KillHouseScore(CountEnemiesDown(), _friendlyHits, elapsed, false)
                : _score;
        }

        private void TickResults(Keyboard kb, bool input)
        {
            if (!input)
                return;
            if (kb.enterKey.wasPressedThisFrame)
            {
                CloseResults();
                Begin(_kind);
            }
            else if (kb.xKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
            {
                CloseResults();
            }
        }

        // ------------------------------------------------------------------ Başlat / bitir

        private void Begin(ChallengeKind kind)
        {
            Cleanup();
            _kind = kind;
            _phase = Phase.Running;
            _score = _hits = _kills = _misses = _friendlyHits = _throws = _scheduleIndex = _enemyTotal = 0;
            _cleared = false;
            _nextId = IdBase;
            _startTime = Time.time;
            _origin = _player.transform.position;
            _origin.y = 0f;
            _runRoot = new GameObject("Meydan_" + kind).transform;
            _runRoot.SetParent(_root, false);
            SetPrompt(string.Empty);
            if (_resultsPanel != null)
                _resultsPanel.SetActive(false);

            try
            {
                switch (kind)
                {
                    case ChallengeKind.QuickFire: StartQuickFire(); break;
                    case ChallengeKind.Sniper: StartSniper(); break;
                    case ChallengeKind.KillHouse: StartKillHouse(); break;
                    case ChallengeKind.Grenade: StartGrenade(); break;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Cleanup();
                _phase = Phase.Idle;
                return;
            }

            _message?.Invoke(ChallengeRules.Name(kind).ToUpperInvariant() + " başladı!", 2.5f);
        }

        private void Finish(string reason)
        {
            if (_phase != Phase.Running)
                return;

            var elapsed = Time.time - _startTime;
            var final = _kind == ChallengeKind.KillHouse
                ? ChallengeRules.KillHouseScore(CountEnemiesDown(), _friendlyHits, elapsed, _cleared)
                : _score;
            var isBest = ChallengeRules.RecordResult(_store, _kind, final);
            var best = ChallengeRules.GetBest(_store, _kind);

            Cleanup();
            _phase = Phase.Results;
            UiFactory.SetText(_hud, string.Empty);

            var body = reason + "\n\nPUAN: " + final + "   DERECE: " + ChallengeRules.Rank(_kind, final)
                       + "\nSüre: " + elapsed.ToString("0.0") + " sn\n" + Details()
                       + "\nEn iyi: " + best + (isBest ? "   YENİ REKOR!" : string.Empty)
                       + "\n\nEnter: tekrar   X / Esc: kapat";
            UiFactory.SetText(_resultsTitle, ChallengeRules.Name(_kind).ToUpperInvariant());
            UiFactory.SetText(_resultsBody, body);
            if (_resultsPanel != null)
                _resultsPanel.SetActive(true);
        }

        private string Details()
        {
            switch (_kind)
            {
                case ChallengeKind.QuickFire: return "İsabet: " + _hits + "   Kaçırılan: " + _misses;
                case ChallengeKind.Sniper: return "Vurulan hedef: " + _kills + "/" + _targets.Count;
                case ChallengeKind.KillHouse: return "Düşman: " + CountEnemiesDown() + "/" + _enemyTotal + "   Dost vuruşu: " + _friendlyHits;
                case ChallengeKind.Grenade: return "Atılan bomba: " + _throws + "/" + ChallengeRules.GrenadeThrows;
                default: return string.Empty;
            }
        }

        private void CloseResults()
        {
            if (_resultsPanel != null)
                _resultsPanel.SetActive(false);
            _phase = Phase.Idle;
        }

        private void Cleanup()
        {
            for (var i = 0; i < _targets.Count; i++)
            {
                var info = _targets[i];
                if (info.Target != null)
                {
                    info.Target.Hit -= OnTargetHit;
                    info.Target.KnockedDown -= OnTargetDown;
                    Destroy(info.Target.gameObject);
                }
            }

            _targets.Clear();
            _byTarget.Clear();
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

        // ------------------------------------------------------------------ Hızlı Atış

        private void StartQuickFire()
        {
            _limit = ChallengeRules.QuickFireDuration;
            _schedule = ChallengeRules.BuildQuickFireSchedule(Environment.TickCount & 0x7fffffff);
            EnsureWeapon(WeaponIds.Mpt76);
        }

        private void TickQuickFire(float elapsed)
        {
            while (_scheduleIndex < _schedule.Count && _schedule[_scheduleIndex].SpawnTime <= elapsed)
            {
                var spec = _schedule[_scheduleIndex++];
                var info = SpawnTarget(PointAt(spec.Distance, spec.Lateral), false, spec.Distance, 100f);
                if (info != null)
                    info.ExpireAt = Time.time + spec.LifeSeconds;
            }

            TickExpiry(elapsed);
        }

        /// <summary>Süresi dolan ve vurulmamış hedefleri kaldırır (Hızlı Atış'ta kaçan sayılır).</summary>
        private void TickExpiry(float elapsed)
        {
            for (var i = _targets.Count - 1; i >= 0; i--)
            {
                var info = _targets[i];
                if (info.ExpireAt < 0f || Time.time < info.ExpireAt)
                    continue;

                if (!info.Scored && !info.Friendly)
                    _misses++;
                if (_kind == ChallengeKind.QuickFire)
                    RemoveTarget(info, i);
                else
                    info.ExpireAt = -1f;
            }
        }

        private bool AllScoredOrGone()
        {
            for (var i = 0; i < _targets.Count; i++)
                if (!_targets[i].Scored && !_targets[i].Friendly)
                    return false;
            return _targets.Count > 0;
        }

        // ------------------------------------------------------------------ Keskin Nişancı

        private void StartSniper()
        {
            _limit = ChallengeRules.SniperTimeLimit;
            EnsureWeapon(WeaponIds.Jng90);

            // Haritanın ötesinde uzak hedefler için geçici zemin.
            var farZ = _origin.z + 560f;
            var startZ = TrainingRangeBuilder.HalfSize;
            var length = Mathf.Max(10f, farZ - startZ);
            var slab = StructureKit.CreateBox(_runRoot, "UzakZemin", new Vector3(_origin.x, -0.05f, startZ + length * 0.5f),
                new Vector3(140f, 0.1f, length), Quaternion.identity, MaterialId.Dirt);
            StructureKit.MarkStatic(slab);

            var layout = ChallengeRules.BuildSniperLayout(Environment.TickCount & 0x7fffffff);
            for (var i = 0; i < layout.Count; i++)
            {
                var s = layout[i];
                var info = SpawnTarget(PointAt(s.Distance, s.Lateral), false, s.Distance, 100f);
                if (info != null)
                    info.ExpireAt = Time.time + s.LifeSeconds;
            }

            _enemyTotal = _targets.Count;
        }

        // ------------------------------------------------------------------ Kill House

        private void StartKillHouse()
        {
            _limit = ChallengeRules.KillHouseTimeLimit;
            EnsureWeapon(WeaponIds.Sar9);

            // (yerel x, yerel z, dost mu) — bina merkezine göre.
            var a = new[]
            {
                new Vector3(-4.5f, 0f, -2.5f), new Vector3(3.5f, 0f, 2.5f), new Vector3(-2f, 0f, 3f), new Vector3(5f, 0f, -2f)
            };
            var aFriends = new[] { new Vector3(0f, 0f, -1f), new Vector3(-5f, 0f, 2.5f) };
            var b = new[] { new Vector3(-2.5f, 0f, -1.5f), new Vector3(2.5f, 0f, 1.5f), new Vector3(0f, 0f, 2.5f) };
            var bFriends = new[] { new Vector3(2.5f, 0f, -2f) };

            SpawnGroup(HouseA, 0f, a, false);
            SpawnGroup(HouseA, 0f, aFriends, true);
            SpawnGroup(HouseB, HouseBYaw, b, false);
            SpawnGroup(HouseB, HouseBYaw, bFriends, true);
            _enemyTotal = 0;
            for (var i = 0; i < _targets.Count; i++)
                if (!_targets[i].Friendly)
                    _enemyTotal++;
        }

        private void SpawnGroup(Vector3 center, float yaw, Vector3[] offsets, bool friendly)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            for (var i = 0; i < offsets.Length; i++)
            {
                var info = SpawnTarget(center + rot * offsets[i], friendly, 0f, 100f);
                if (info != null && info.Target != null)
                {
                    info.Target.FacePlayer = !friendly;
                    if (friendly)
                        Tint(info.Target.gameObject, new Color(0.25f, 0.85f, 0.35f));
                }
            }
        }

        private int CountEnemiesDown()
        {
            var n = 0;
            for (var i = 0; i < _targets.Count; i++)
                if (!_targets[i].Friendly && _targets[i].Scored)
                    n++;
            return n;
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

        // ------------------------------------------------------------------ Bomba Atma

        private void StartGrenade()
        {
            _limit = ChallengeRules.GrenadeTimeLimit;
            var inv = _player.Combatant != null ? _player.Combatant.Inventory : null;
            if (inv != null)
                inv.GiveItem(ItemIds.FragGrenade, ChallengeRules.GrenadeThrows);

            _ringCenter = new Vector3(_origin.x, 0.05f, _origin.z) + _forward * 32f;
            var radii = ChallengeRules.GrenadeRingRadii;
            for (var i = 0; i < radii.Length; i++)
                BuildRing(radii[i], i == 0 ? MaterialId.LandingZone : MaterialId.White);
            _message?.Invoke("Bomba at: " + ChallengeRules.GrenadeThrows + " hak, merkez 32 m ileride", 3f);
        }

        private void BuildRing(float radius, MaterialId material)
        {
            const int segments = 28;
            var segLen = 2f * Mathf.PI * radius / segments * 1.05f;
            for (var i = 0; i < segments; i++)
            {
                var ang = i * Mathf.PI * 2f / segments;
                var pos = _ringCenter + new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius);
                var rot = Quaternion.Euler(0f, -ang * Mathf.Rad2Deg + 90f, 0f);
                StructureKit.CreateBox(_runRoot, "Halka", pos, new Vector3(segLen, 0.05f, 0.3f), rot, material, false);
            }
        }

        private void OnExplosion(ExplosionEvent e)
        {
            if (_phase != Phase.Running || _kind != ChallengeKind.Grenade || !e.AttackerId.Equals(_localId))
                return;

            var pos = new Vector3(e.Position.X, 0f, e.Position.Z);
            var center = new Vector3(_ringCenter.x, 0f, _ringCenter.z);
            var points = ChallengeRules.GrenadeScore(Vector3.Distance(pos, center));
            _score += points;
            _throws++;
            _message?.Invoke(points > 0 ? "+" + points : "Halka dışı", 1.5f);
        }

        // ------------------------------------------------------------------ Hedefler

        private Vector3 PointAt(float distance, float lateral)
            => _origin + _forward * distance + _right * lateral;

        private TargetInfo SpawnTarget(Vector3 position, bool friendly, float distance, float health)
        {
            var target = DamageableTarget.CreateDummy(position, _bus, _registry, _nextId++, health);
            if (target == null)
                return null;

            target.RespawnSeconds = 9999f;
            target.FacePlayer = true;
            if (_runRoot != null)
                target.transform.SetParent(_runRoot, true);

            var info = new TargetInfo { Target = target, Friendly = friendly, Distance = distance };
            target.Hit += OnTargetHit;
            target.KnockedDown += OnTargetDown;
            _targets.Add(info);
            _byTarget[target] = info;
            return info;
        }

        private void RemoveTarget(TargetInfo info, int index)
        {
            if (info.Target != null)
            {
                info.Target.Hit -= OnTargetHit;
                info.Target.KnockedDown -= OnTargetDown;
                _byTarget.Remove(info.Target);
                Destroy(info.Target.gameObject);
            }

            _targets.RemoveAt(index);
        }

        private void OnTargetHit(DamageableTarget target, DamageInfo damage)
        {
            if (_phase != Phase.Running || !damage.AttackerId.Equals(_localId) || !_byTarget.TryGetValue(target, out var info))
                return;

            if (info.Friendly)
            {
                _friendlyHits++;
                _message?.Invoke("DOST! -" + ChallengeRules.KillHouseFriendlyPenalty, 1.5f);
                return;
            }

            info.HitByPlayer = true;
            info.Headshot |= damage.IsHeadshot;
            _hits++;
        }

        private void OnTargetDown(DamageableTarget target)
        {
            if (_phase != Phase.Running || !_byTarget.TryGetValue(target, out var info) || info.Scored || info.Friendly || !info.HitByPlayer)
                return;

            info.Scored = true;
            _kills++;
            if (_kind == ChallengeKind.QuickFire)
                _score += ChallengeRules.QuickFireHitScore(info.Distance, info.Headshot);
            else if (_kind == ChallengeKind.Sniper)
                _score += ChallengeRules.SniperHitScore(info.Distance, info.Headshot);
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
