using System;
using Project.Core.Events;
using Project.Core.Interfaces;
using Project.Presentation.Player;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.Tutorial
{
    /// <summary>Bağlamsal ipucu açılır penceresi (ilk yağma/nişan/bölge/yaralı/araç); "bir daha gösterme" kalıcıdır.</summary>
    public sealed class TutorialTipView : MonoBehaviour
    {
        private const float ShowSeconds = 7f;
        private TutorialTipTracker _tracker;
        private IEventBus _bus;
        private IPlayerHudSource _hud;
        private GameObject _panel;
        private Text _title, _body, _foot;
        private TutorialTipId _current;
        private float _hideAt = -1f;
        private bool _wasAiming, _wasVehicle;

        public static TutorialTipView Instance { get; private set; }

        public static TutorialTipView Begin(Transform parent, IEventBus bus, IPlayerHudSource hud, ISettingsStore store, bool countMatch = true)
        {
            try
            {
                var go = new GameObject("[İpuçları]");
                go.transform.SetParent(parent, false);
                var v = go.AddComponent<TutorialTipView>();
                v._bus = bus; v._hud = hud; v._tracker = new TutorialTipTracker(store);
                v._tracker.MatchIndex = countMatch ? TutorialProgress.RegisterMatch(store) : 0;
                v.Build();
                if (bus != null) bus.Subscribe<LootPickedUpEvent>(v.OnLoot);
                Instance = v;
                return v;
            }
            catch (Exception e) { Debug.LogException(e); return null; }
        }

        /// <summary>Dış sistemler (bölge uyarısı, yaralı tim arkadaşı) için. Bölge/yaralı olayları bunu çağırabilir.</summary>
        public static void Notify(TutorialTipId id) { if (Instance != null) Instance.Show(id); }

        private void OnLoot(LootPickedUpEvent e) => Show(TutorialTipId.FirstLoot);

        private void Build()
        {
            var canvas = UiFactory.CreateCanvas("İpucuUI", 14);
            canvas.transform.SetParent(transform, false);
            var p = UiFactory.Panel(canvas.transform, UiTheme.PanelDark);
            _panel = p.gameObject;
            p.anchorMin = p.anchorMax = p.pivot = new Vector2(0.5f, 0f);
            p.anchoredPosition = new Vector2(0f, 160f);
            p.sizeDelta = new Vector2(560f, 150f);
            _title = Mk(p, 10f, 28f, 20, UiTheme.Amber, FontStyle.Bold);
            _body = Mk(p, 40f, 70f, 17, Color.white, FontStyle.Normal);
            _foot = Mk(p, 120f, 22f, 13, new Color(1f, 1f, 1f, 0.6f), FontStyle.Normal);
            _panel.SetActive(false);
        }

        private static Text Mk(RectTransform parent, float y, float h, int size, Color c, FontStyle s)
        {
            var t = UiFactory.Label(parent, string.Empty, size, TextAnchor.UpperLeft, c, s);
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(16f, -y); r.sizeDelta = new Vector2(528f, h);
            return t;
        }

        public void Show(TutorialTipId id)
        {
            if (_tracker == null || _panel == null || _panel.activeSelf || !_tracker.ShouldShow(id)) return;
            var d = TutorialTipTracker.Get(id);
            _current = id;
            _tracker.MarkShown(id);
            UiFactory.SetText(_title, d.Title);
            UiFactory.SetText(_body, TutorialInputHints.Format(d.Body, d.Action, TutorialInputHints.GamepadActive));
            UiFactory.SetText(_foot, TutorialInputHints.GamepadActive ? "B: kapat   Y: bir daha gösterme" : "Enter: kapat   N: bir daha gösterme");
            _panel.SetActive(true);
            _hideAt = Time.unscaledTime + ShowSeconds;
            TutorialVoiceHooks.Raise("tip." + id);
        }

        private void Update()
        {
            var pad = Gamepad.current;
            if (pad != null && pad.wasUpdatedThisFrame) TutorialInputHints.GamepadActive = true;
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) TutorialInputHints.GamepadActive = false;

            if (_hud != null)
            {
                var aim = _hud.IsAiming;
                if (aim && !_wasAiming) Show(TutorialTipId.FirstAds);
                _wasAiming = aim;
                var veh = _hud.IsInVehicle;
                if (veh && !_wasVehicle) Show(TutorialTipId.FirstVehicle);
                _wasVehicle = veh;
            }

            if (_panel == null || !_panel.activeSelf) return;
            var mute = !OverlayState.TextInputActive && ((kb != null && kb.nKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame));
            var close = (kb != null && kb.enterKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);
            if (mute) { _tracker.MuteForever(_current); _panel.SetActive(false); }
            else if (close || Time.unscaledTime >= _hideAt) _panel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_bus != null) _bus.Unsubscribe<LootPickedUpEvent>(OnLoot);
            if (Instance == this) Instance = null;
        }
    }
}
