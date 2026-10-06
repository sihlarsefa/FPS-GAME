using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure.Combat;
using Project.Infrastructure.World;
using Project.Presentation.Bootstrap;
using Project.Presentation.Player;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.Tutorial
{
    /// <summary>Poligon eğitimi arayüzü: adım metni, ipucu, ilerleme, atlama tuşları ve bitiş mesajı.</summary>
    /// <remarks>
    /// Olay tabanlı adımları <see cref="TutorialService"/> kendisi izler; sunum sinyallerini (duruş, yaslanma, nişan,
    /// dürbün, atış kipi, envanter, harita, sis, araç, yol noktası) oyuncunun gerçek durumundan (PlayerController /
    /// IPlayerHudSource) türetir. Tamamlanınca XP kariyere yazılır ve özet ekranı açılır.
    /// </remarks>
    public sealed class TutorialPresenter : MonoBehaviour
    {
        public const string ResourcePath = "Tutorial/tutorial_steps";

        [Serializable] private class LocText { public string tr; public string en; }
        [Serializable] private class FilterDto { public string WaypointId; public string Stance; public string Side; public string Order; public string FireMode; public string ItemId; public string VehicleType; public bool Started; public bool Completed; public float MinRadius; public float MinSeconds; public float MinZoom; public bool WhileAiming; public bool WhileScoped; }
        [Serializable] private class CondDto { public string coreEvent; public string presentationSignal; public int count; public FilterDto filters; }
        [Serializable] private class SuccessDto { public string mode; public CondDto[] conditions; }
        [Serializable] private class StepDto { public string id; public LocText goal; public LocText screenText; public LocText hint; public float timeLimitSeconds; public SuccessDto success; public int rewardXp; }
        [Serializable] private class CompletionDto { public LocText screenText; public int bonusXp; }
        [Serializable] private class RootDto { public StepDto[] steps; public CompletionDto completion; }

        /// <summary>Eğitimi kurar ve başlatır; başarısızsa false (poligon eğitimsiz devam eder).</summary>
        public static TutorialPresenter Begin(Transform parent, IEventBus bus, Transform player, PlayerController controller = null)
        {
            try
            {
                var go = new GameObject("[Eğitim]");
                go.transform.SetParent(parent, false);
                var p = go.AddComponent<TutorialPresenter>();
                var pc = controller != null ? controller : (player != null ? player.GetComponent<PlayerController>() : null);
                if (p.Init(bus, player, pc))
                    return p;
                Destroy(go);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            return null;
        }

        /// <summary>JSON metninden adımları okur (test edilebilir).</summary>
        public static List<TutorialStepDef> ParseSteps(string json, out string completionText)
            => ParseSteps(json, out completionText, out _);

        /// <summary>JSON metninden adımları ve bitiş bonus XP'sini okur.</summary>
        public static List<TutorialStepDef> ParseSteps(string json, out string completionText, out int bonusXp)
        {
            completionText = null;
            bonusXp = 0;
            var list = new List<TutorialStepDef>();
            var root = JsonUtility.FromJson<RootDto>(json);
            if (root == null || root.steps == null)
                return list;
            completionText = root.completion?.screenText?.tr;
            bonusXp = Mathf.Max(0, root.completion != null ? root.completion.bonusXp : 0);
            foreach (var s in root.steps)
            {
                if (s == null || string.IsNullOrEmpty(s.id))
                    continue;
                var def = new TutorialStepDef
                {
                    Id = s.id,
                    Goal = s.goal?.tr ?? string.Empty,
                    ScreenText = s.screenText?.tr ?? string.Empty,
                    Hint = s.hint?.tr ?? string.Empty,
                    TimeLimitSeconds = s.timeLimitSeconds,
                    RewardXp = s.rewardXp,
                    AnyOf = s.success != null && string.Equals(s.success.mode, "anyOf", StringComparison.OrdinalIgnoreCase),
                };
                var slice = StepSlice(json, s.id);
                var killMode = TriState(slice, "IsKill");
                var impactMode = TriState(slice, "IsImpact");
                if (s.success?.conditions != null)
                    foreach (var c in s.success.conditions)
                    {
                        var key = !string.IsNullOrEmpty(c.coreEvent) ? c.coreEvent : c.presentationSignal;
                        if (string.IsNullOrEmpty(key))
                            continue;
                        var f = c.filters;
                        var cond = new TutorialCondition { Key = key, Qualifier = Qualifier(key, f), Count = Mathf.Max(1, c.count) };
                        if (f != null)
                        {
                            cond.MinRadius = f.MinRadius;
                            cond.MinSeconds = f.MinSeconds;
                            cond.MinZoom = f.MinZoom;
                            cond.WhileAiming = f.WhileAiming;
                            cond.WhileScoped = f.WhileScoped;
                        }
                        // JsonUtility "yok" ile "false" ayırt edemez: IsKill/IsImpact ham metinden okunur.
                        if (key == "HitConfirmedEvent") cond.IsKill = killMode;
                        else if (key == "ArtilleryStrikeEvent") cond.IsImpact = impactMode;
                        def.Conditions.Add(cond);
                    }
                list.Add(def);
            }
            return list;
        }

        private static string StepSlice(string json, string id)
        {
            var m = Regex.Match(json, "\"id\"\\s*:\\s*\"" + Regex.Escape(id) + "\"");
            if (!m.Success)
                return string.Empty;
            var next = json.IndexOf("\"id\"", m.Index + m.Length, StringComparison.Ordinal);
            return next < 0 ? json.Substring(m.Index) : json.Substring(m.Index, next - m.Index);
        }

        /// <summary>Metindeki "Ad": true/false değerini -1 (yok) / 0 / 1 olarak döndürür.</summary>
        internal static int TriState(string slice, string name)
        {
            var m = Regex.Match(slice ?? string.Empty, "\"" + name + "\"\\s*:\\s*(true|false)");
            return !m.Success ? -1 : m.Groups[1].Value == "true" ? 1 : 0;
        }

        private static string Qualifier(string key, FilterDto f)
        {
            if (f == null)
                return string.Empty;
            if (key == "ItemUsedEvent" && !string.IsNullOrEmpty(f.ItemId))
                return f.ItemId + (f.Started ? ":started" : ":completed");
            if (key == "PlayerReachedWaypoint") return f.WaypointId ?? string.Empty;
            if (!string.IsNullOrEmpty(f.Stance)) return f.Stance;
            if (!string.IsNullOrEmpty(f.Side)) return f.Side;
            if (!string.IsNullOrEmpty(f.Order)) return f.Order;
            if (!string.IsNullOrEmpty(f.FireMode)) return f.FireMode;
            if (!string.IsNullOrEmpty(f.VehicleType)) return f.VehicleType;
            return string.Empty;
        }

        private const float VehicleDriveMeters = 25f;
        private const float FallbackWalkSqr = 36f;

        private TutorialService _service;
        private Transform _player;
        private PlayerController _controller;
        private IPlayerHudSource _hud;
        private Canvas _canvas;
        private GameObject _taskPanel;
        private Text _title, _goal, _hint, _footer;
        private UiProgressBar _bar;
        private string _completionText = "Eğitim tamam.";
        private int _bonusXp;
        private float _hideAt = -1f;
        private Vector3 _startPos;
        private bool _hasStart;
        private float _lookX, _lookY, _adsTime;
        private Stance _lastStance;
        private int _lastLeanSide;
        private FireMode _lastFireMode;
        private bool _wasScoped, _wasInventory, _wasMap, _wasInVehicle, _awarded;
        private int _lastSmokeCount;
        private float _vehicleMeters;
        private Vector3 _vehicleLast;
        private bool _vehicleKirpi;
        private float _findAt;
        private InventoryView _inventoryView;
        private FullMapView _mapView;
        private readonly List<string> _waypointIds = new List<string>(2);
        private Text _doneText, _checkText;
        private readonly TutorialChecklist _checklist = new TutorialChecklist();
        public const string RewardCosmeticId = "armband_atmaca";

        private bool Init(IEventBus bus, Transform player, PlayerController controller)
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
                return false;
            var steps = ParseSteps(asset.text, out var completion, out _bonusXp);
            if (steps.Count == 0)
                return false;
            if (!string.IsNullOrEmpty(completion))
                _completionText = completion;
            _player = player;
            _controller = controller;
            _hud = controller;
            _service = new TutorialService(steps, bus);
            _service.AimingProbe = () => _hud != null && _hud.IsAiming;
            _service.ScopedProbe = () => _hud != null && _hud.IsScoped;
            _service.ZoomProbe = () => _hud != null ? _hud.ScopeZoom : 0f;
            _service.StepStarted += OnStepStarted;
            _service.StepFinished += OnStepFinished;
            _service.Completed += OnCompleted;
            BuildUi();
            TutorialTipView.Begin(transform, bus, _hud, GameSession.Store, false);
            _service.Start();
            return true;
        }

        private void BuildUi()
        {
            _canvas = UiFactory.CreateCanvas("EğitimUI", 15);
            _canvas.transform.SetParent(transform, false);
            var panel = UiFactory.Panel(_canvas.transform, UiTheme.PanelDark);
            _taskPanel = panel.gameObject;
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(24f, -120f);
            panel.sizeDelta = new Vector2(520f, 210f);

            _title = Row(panel, 12f, 28f, 20, UiTheme.Amber, FontStyle.Bold);
            _goal = Row(panel, 44f, 60f, 22, Color.white, FontStyle.Normal);
            _hint = Row(panel, 108f, 44f, 17, UiTheme.Khaki, FontStyle.Italic);
            _footer = Row(panel, 182f, 22f, 14, new Color(1f, 1f, 1f, 0.6f), FontStyle.Normal);
            _footer.text = "N: adımı atla   F9: eğitimi bitir";

            var chk = UiFactory.Panel(_canvas.transform, UiTheme.PanelDark);
            chk.anchorMin = chk.anchorMax = chk.pivot = new Vector2(0f, 1f);
            chk.anchoredPosition = new Vector2(24f, -340f);
            chk.sizeDelta = new Vector2(520f, 140f);
            _checkText = Row(chk, 8f, 126f, 17, Color.white, FontStyle.Normal);
            _checkText.text = _checklist.Render(TutorialInputHints.GamepadActive);

            _bar = UiFactory.ProgressBar(panel);
            var br = _bar.transform as RectTransform;
            br.anchorMin = br.anchorMax = new Vector2(0f, 1f);
            br.pivot = new Vector2(0f, 1f);
            br.anchoredPosition = new Vector2(16f, -158f);
            br.sizeDelta = new Vector2(488f, 12f);

            // Tamamlanma özeti (başta gizli).
            var done = UiFactory.Panel(_canvas.transform, UiTheme.PanelDark);
            done.anchorMin = done.anchorMax = done.pivot = new Vector2(0.5f, 0.5f);
            done.anchoredPosition = Vector2.zero;
            done.sizeDelta = new Vector2(640f, 340f);
            var t = UiFactory.Label(done, "EĞİTİM TAMAMLANDI", 32, TextAnchor.UpperCenter, UiTheme.Amber, FontStyle.Bold);
            var tr = t.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = new Vector2(0f, -18f); tr.sizeDelta = new Vector2(-32f, 44f);
            _doneText = UiFactory.Label(done, string.Empty, 22, TextAnchor.UpperCenter, Color.white, FontStyle.Normal);
            var dr = _doneText.rectTransform;
            dr.anchorMin = new Vector2(0f, 1f); dr.anchorMax = new Vector2(1f, 1f); dr.pivot = new Vector2(0.5f, 1f);
            dr.anchoredPosition = new Vector2(0f, -76f); dr.sizeDelta = new Vector2(-48f, 250f);
            done.gameObject.SetActive(false);
            _doneText.transform.parent.name = "EğitimÖzet";
        }

        private static Text Row(RectTransform parent, float y, float h, int size, Color color, FontStyle style)
        {
            var t = UiFactory.Label(parent, string.Empty, size, TextAnchor.UpperLeft, color, style);
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(16f, -y);
            r.sizeDelta = new Vector2(488f, h);
            return t;
        }

        private void OnStepStarted(int index, TutorialStepDef step)
        {
            _lookX = _lookY = _adsTime = 0f;
            _hasStart = false;
            _waypointIds.Clear();
            foreach (var c in step.Conditions)
                if (c.Key == "PlayerReachedWaypoint")
                    _waypointIds.Add(c.Qualifier ?? string.Empty);
            SyncBaseline();
            TutorialVoiceHooks.Raise(TutorialVoiceHooks.StepCue(step.Id));
            UiFactory.SetText(_checkText, _checklist.Render(TutorialInputHints.GamepadActive));
            UiFactory.SetText(_title, "EĞİTİM — ADIM " + (index + 1) + "/" + _service.StepCount);
            UiFactory.SetText(_goal, string.IsNullOrEmpty(step.ScreenText) ? step.Goal : step.Goal + "\n" + step.ScreenText);
            UiFactory.SetText(_hint, step.Hint);
            if (_bar != null) _bar.Value = _service.Progress;
        }

        /// <summary>Adım başında "şu anki durum"u referans alır; böylece yalnızca yeni değişiklikler sayılır.</summary>
        private void SyncBaseline()
        {
            if (_controller == null)
                return;
            _lastStance = _controller.Motor != null ? _controller.Motor.CurrentStance : Stance.Standing;
            _lastLeanSide = LeanSide();
            _lastFireMode = _hud.ActiveWeapon != null ? _hud.ActiveWeapon.CurrentFireMode : FireMode.Single;
            _wasScoped = _hud.IsScoped;
            _wasInventory = _inventoryView != null && _inventoryView.IsOpen;
            _wasMap = _mapView != null && _mapView.IsOpen;
            _wasInVehicle = _hud.IsInVehicle;
            _lastSmokeCount = SmokeVolume.ActiveCount;
            _vehicleMeters = 0f;
            _vehicleLast = _hud.Position;
        }

        private void OnStepFinished(int index, TutorialStepDef step, bool success)
        {
            if (success)
            {
                UiFactory.SetText(_hint, "Tamam! +" + step.RewardXp + " XP");
                if (_checklist.MarkByStep(step.Id))
                {
                    UiFactory.SetText(_checkText, _checklist.Render(TutorialInputHints.GamepadActive));
                    TutorialVoiceHooks.Raise("tutorial.check." + step.Id);
                }
            }
        }

        private void OnCompleted()
        {
            var total = _service.TotalXp + _bonusXp;
            var rankLine = string.Empty;
            if (!_awarded)
            {
                _awarded = true;
                var career = GameSession.Career;
                if (career != null && total > 0)
                {
                    try
                    {
                        career.AddExperience(total);
                        rankLine = "\nRütbe: " + career.Current.Rank;
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
            try
            {
                var cos = Project.Infrastructure.Characters.CosmeticsRuntime.Service;
                if (cos != null && cos.Grant(RewardCosmeticId))
                    rankLine += "\nÖdül: eğitim arma kolluğu açıldı";
            }
            catch (Exception e) { Debug.LogException(e); }
            try
            {
                // Kalıcı bayrak (TİM sayfası okur) + "Poligon Mezunu" başarımı (yalnızca ilk tamamlamada).
                var store = GameSession.Store;
                if (TutorialProgress.MarkCompleted(store))
                {
                    var ach = Project.Infrastructure.DI.AchievementServiceProvider.GetOrCreate(store);
                    ach?.AddProgress(TutorialProgress.AchievementMetric, 1);
                }
            }
            catch (Exception e) { Debug.LogException(e); }
            TutorialVoiceHooks.Raise("tutorial.completed");
            if (_taskPanel != null) _taskPanel.SetActive(false);
            if (_checkText != null && _checkText.transform.parent != null) _checkText.transform.parent.gameObject.SetActive(false);
            if (_doneText != null)
            {
                UiFactory.SetText(_doneText, _completionText
                    + "\n\nTamamlanan adım: " + _service.CompletedSteps + "/" + _service.StepCount
                    + "\nAdım XP: +" + _service.TotalXp
                    + "\nBitirme bonusu: +" + _bonusXp
                    + "\nToplam: +" + total + " XP" + rankLine
                    + "\n\n(Enter ile kapat)");
                _doneText.transform.parent.gameObject.SetActive(true);
            }
            _hideAt = Time.unscaledTime + 15f;
        }

        private void Update()
        {
            if (_service == null)
                return;
            if (_hideAt >= 0f)
            {
                var kbDone = Keyboard.current;
                var enter = kbDone != null && kbDone.enterKey.wasPressedThisFrame && !UI.OverlayState.TextInputActive;
                if ((Time.unscaledTime >= _hideAt || enter) && _canvas != null)
                    _canvas.gameObject.SetActive(false);
                return;
            }

            _service.Tick(Time.deltaTime);
            if (!_service.IsRunning)
                return;

            var kb = Keyboard.current;
            var mouse = Mouse.current;
            // Duraklatma menüsü ya da konsol açıkken N / F9 adım atlamaz.
            if (kb != null && Time.timeScale > 0f && !UI.OverlayState.TextInputActive)
            {
                if (kb.nKey.wasPressedThisFrame) { _service.SkipStep(); return; }
                if (kb.f9Key.wasPressedThisFrame) { _service.SkipAll(); return; }
            }
            if (mouse != null)
                PollLook(mouse);
            if (_controller != null && _hud != null)
                PollPlayerState();
            PollWaypoints();
            if (_bar != null) _bar.Value = _service.Progress;
        }

        private int LeanSide()
        {
            var m = _controller != null ? _controller.Motor : null;
            if (m == null) return 0;
            return m.Lean > 0.6f ? 1 : m.Lean < -0.6f ? -1 : 0;
        }

        private void FindViews()
        {
            if (Time.unscaledTime < _findAt)
                return;
            _findAt = Time.unscaledTime + 1f;
            if (_inventoryView == null)
                _inventoryView = FindFirstObjectByType<InventoryView>(FindObjectsInactive.Include);
            if (_mapView == null)
            {
                _mapView = FindFirstObjectByType<FullMapView>(FindObjectsInactive.Include);
                if (_mapView != null)
                    _mapView.PointMarked += OnMapMarked;
            }
        }

        private void OnMapMarked(Vector3 point) => _service?.Signal("MapMarkerPlaced");

        /// <summary>Gerçek oyuncu durumundan sinyaller: duruş, yaslanma, nişan, dürbün, kip, envanter, harita, sis, araç.</summary>
        private void PollPlayerState()
        {
            FindViews();
            var motor = _controller.Motor;
            if (motor != null)
            {
                var st = motor.CurrentStance;
                if (st != _lastStance)
                {
                    _lastStance = st;
                    _service.Signal("StanceChanged", st == Stance.Crouching ? "Crouch" : st == Stance.Prone ? "Prone" : "Stand");
                }
                var lean = LeanSide();
                if (lean != _lastLeanSide)
                {
                    _lastLeanSide = lean;
                    if (lean != 0)
                        _service.Signal("LeanPerformed", lean > 0 ? "Right" : "Left");
                }
            }

            var weapon = _hud.ActiveWeapon;
            if (weapon != null && weapon.CurrentFireMode != _lastFireMode)
            {
                _lastFireMode = weapon.CurrentFireMode;
                _service.Signal("FireModeChanged", _lastFireMode.ToString());
            }

            if (_hud.IsAiming)
            {
                _adsTime += Time.deltaTime;
                if (_service.Wants("AdsActive"))
                    _service.Signal("AdsActive", null, TutorialSignalData.Held(_adsTime));
            }
            else
                _adsTime = 0f;

            var scoped = _hud.IsScoped;
            if (scoped && !_wasScoped)
                _service.Signal("ScopeActive");
            else if (scoped && _service.Wants("ScopeActive") && _hud.ScopeZoom > 0f)
                _service.Signal("ScopeActive");
            _wasScoped = scoped;

            var inv = _inventoryView != null && _inventoryView.IsOpen;
            if (inv && !_wasInventory)
                _service.Signal("InventoryOpened");
            _wasInventory = inv;

            var map = _mapView != null && _mapView.IsOpen;
            if (map && !_wasMap)
                _service.Signal("MapOpened");
            _wasMap = map;

            var smoke = SmokeVolume.ActiveCount;
            if (smoke > _lastSmokeCount)
                _service.Signal("SmokeVolumeSpawned");
            _lastSmokeCount = smoke;

            PollVehicle();
        }

        private void PollVehicle()
        {
            var inVehicle = _hud.IsInVehicle;
            if (inVehicle && !_wasInVehicle)
            {
                _vehicleKirpi = _controller.IsDriving;
                _vehicleMeters = 0f;
                _vehicleLast = _hud.Position;
                _service.Signal("VehicleEntered", _vehicleKirpi ? "Kirpi" : "Transport");
            }
            else if (inVehicle)
            {
                var pos = _hud.Position;
                _vehicleMeters += Vector3.Distance(pos, _vehicleLast);
                _vehicleLast = pos;
                if (_vehicleMeters >= VehicleDriveMeters)
                {
                    _vehicleMeters = float.MinValue / 2f;
                    _service.Signal("VehicleDriven");
                }
            }
            else if (!inVehicle && _wasInVehicle)
                _service.Signal("VehicleExited", _vehicleKirpi ? "Kirpi" : "Transport");
            _wasInVehicle = inVehicle;
        }

        private void PollLook(Mouse mouse)
        {
            var d = mouse.delta.ReadValue();
            _lookX += Mathf.Abs(d.x);
            _lookY += Mathf.Abs(d.y);
            if (_lookX > 600f && _lookY > 150f)
            {
                _lookX = _lookY = float.MinValue / 2f;
                _service.Signal("LookDeltaSatisfied");
            }
        }

        private void PollWaypoints()
        {
            if (_player == null || _waypointIds.Count == 0 || !_service.Wants("PlayerReachedWaypoint"))
                return;
            var pos = _player.position;
            var nearest = float.MaxValue;
            var tracked = false;
            foreach (var id in _waypointIds)
            {
                if (!TrainingRangeBuilder.TryGetWaypoint(id, out var wp))
                    continue;
                tracked = true;
                var dx = pos.x - wp.Position.x;
                var dz = pos.z - wp.Position.z;
                var dist = Mathf.Sqrt(dx * dx + dz * dz);
                nearest = Mathf.Min(nearest, dist);
                if (dist <= wp.Radius)
                {
                    _service.Signal("PlayerReachedWaypoint", id);
                    return;
                }
            }
            if (tracked)
            {
                UiFactory.SetText(_footer, "Yol noktası: " + Mathf.RoundToInt(nearest) + " m   N: atla   F9: bitir");
                return;
            }
            // Bilinmeyen yol noktası: başlangıçtan 6 m yürümek yeterli.
            if (!_hasStart) { _startPos = pos; _hasStart = true; }
            if ((pos - _startPos).sqrMagnitude > FallbackWalkSqr)
            {
                _hasStart = false;
                _service.Signal("PlayerReachedWaypoint", _waypointIds[0]);
            }
        }

        private void OnDestroy()
        {
            if (_mapView != null)
                _mapView.PointMarked -= OnMapMarked;
            _service?.Dispose();
        }
    }
}
