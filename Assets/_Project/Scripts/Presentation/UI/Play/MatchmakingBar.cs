using System;
using Project.Infrastructure.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.UI.Play
{
    /// <summary>
    /// Eşleşme durum çubuğu: "ARANIYOR 0:12 · tahmini ~0:20 – 0:45" + ilerleme + İPTAL;
    /// eşleşme bulununca "MAÇ BULUNDU" hazır kontrolü (20 sn geri sayım, tim noktaları, KABUL ET).
    /// Sayfanın üstüne tam ekran yarı saydam katman olarak biner; etkinken alttaki kartlar tıklanmaz.
    /// </summary>
    public sealed class MatchmakingBar : MonoBehaviour
    {
        private static readonly Color Gold = new Color(0.95f, 0.78f, 0.28f, 1f);
        private static readonly Color Ready = new Color(0.45f, 0.82f, 0.40f, 1f);

        private readonly QueueEstimator _estimator = new QueueEstimator();
        private readonly DodgePenalty _penalty = new DodgePenalty();
        private CanvasGroup _group;
        private Text _status;
        private Text _clock;
        private Text _sub;
        private Text _hint;
        private RectTransform _fill;
        private Image _fillImage;
        private Button _cancel;
        private Button _accept;
        private Image[] _dots = new Image[0];
        private RectTransform _dotRow;
        private MatchmakingFlow _flow;
        private Action _launch;
        private string _context = string.Empty;
        private float _clockNow;
        private float _shown;

        /// <summary>Çubuk bir şey gösteriyor mu (sayfa girişi kilitlemek için).</summary>
        public bool Active => _flow != null && _flow.Active;

        public static MatchmakingBar Create(RectTransform page)
        {
            var root = UiFactory.CreateRect("MatchmakingBar", page);
            UiFactory.Stretch(root);
            var bar = root.gameObject.AddComponent<MatchmakingBar>();
            bar.Build(root);
            return bar;
        }

        private void Build(RectTransform root)
        {
            _group = UiFactory.EnsureCanvasGroup(root);
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            var scrim = UiFactory.Image(root, null, new Color(0f, 0f, 0f, 0.62f));
            scrim.raycastTarget = true;
            UiFactory.Stretch(scrim);

            var panel = UiFactory.Image(root, null, UiTheme.WithAlpha(UiKitTokens.Bg, 0.97f));
            UiFactory.SetRect(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-340f, -130f), new Vector2(340f, 130f));
            var edge = UiFactory.Image(panel.rectTransform, null, UiKitTokens.Accent);
            UiFactory.SetRect(edge, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -3f), new Vector2(0f, 0f));

            _status = UiFactory.Label(panel.rectTransform, "ARANIYOR", 30, TextAnchor.UpperLeft, UiTheme.Text, FontStyle.Bold);
            UiFactory.SetRect(_status, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -64f), new Vector2(-190f, -14f));

            _clock = UiFactory.Label(panel.rectTransform, "0:00", 44, TextAnchor.UpperRight, UiTheme.Text, FontStyle.Bold);
            UiFactory.SetRect(_clock, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-190f, -72f), new Vector2(-24f, -10f));

            _sub = UiFactory.Label(panel.rectTransform, string.Empty, UiTheme.FontNormal, TextAnchor.UpperLeft, UiKitTokens.TextDim);
            UiFactory.SetRect(_sub, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -100f), new Vector2(-24f, -70f));

            var track = UiFactory.Image(panel.rectTransform, null, new Color(1f, 1f, 1f, 0.10f));
            UiFactory.SetRect(track, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 112f), new Vector2(-24f, 120f));
            var fill = UiFactory.Image(track.rectTransform, null, UiKitTokens.Accent);
            fill.raycastTarget = false;
            _fillImage = fill;
            _fill = fill.rectTransform;
            UiFactory.SetRect(_fill, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);

            _dotRow = UiFactory.CreateRect("Dots", panel.rectTransform);
            UiFactory.SetRect(_dotRow, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 128f), new Vector2(-24f, 150f));

            _hint = UiFactory.Label(panel.rectTransform, string.Empty, UiTheme.FontSmall, TextAnchor.LowerLeft, Gold, FontStyle.Bold);
            UiFactory.SetRect(_hint, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 66f), new Vector2(-24f, 108f));

            _cancel = UiFactory.Button(panel.rectTransform, "İPTAL", OnCancelClicked, UiButtonStyle.Default);
            UiFactory.Anchor(_cancel, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 14f), new Vector2(180f, 48f));

            _accept = UiFactory.Button(panel.rectTransform, "KABUL ET", OnAcceptClicked, UiButtonStyle.Primary);
            UiFactory.Anchor(_accept, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 14f), new Vector2(260f, 48f));
            _accept.gameObject.SetActive(false);
        }

        /// <summary>Aramayı başlatır; geri sayım bitince <paramref name="launch"/> çağrılır (var olan LaunchMode akışı).</summary>
        public void Begin(PlayQueueKind kind, string context, Action launch)
        {
            if (Active) return;
            if (_penalty.IsBlocked(_clockNow))
            {
                ShowPenalty();
                return;
            }

            _launch = launch;
            _context = context ?? string.Empty;
            _flow = new MatchmakingFlow(_estimator, kind, DateTime.Now.Hour, Environment.TickCount);
            _flow.PhaseChanged += OnPhase;
            _flow.LaunchRequested += OnLaunch;
            BuildDots(_flow.SquadSize);
            _flow.Start();
            Refresh();
        }

        /// <summary>Esc/geri: etkinse iptal eder ve true döner.</summary>
        public bool HandleBack()
        {
            if (!Active) return false;
            OnCancelClicked();
            return true;
        }

        private void ShowPenalty()
        {
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = true;
            _status.text = "KUYRUK CEZASI";
            _clock.text = QueueEstimator.FormatClock(_penalty.Remaining(_clockNow));
            _sub.text = "Hazır kontrolünü kaçırdığın için kısa süre bekle.";
            _hint.text = string.Empty;
            _cancel.gameObject.SetActive(true);
            UiFactory.SetButtonLabel(_cancel, "TAMAM");
            _penaltyShown = true;
        }

        private bool _penaltyShown;

        private void BuildDots(int count)
        {
            UiFactory.ClearChildren(_dotRow);
            _dots = new Image[count];
            var show = Mathf.Min(count, 10);
            for (var i = 0; i < count; i++)
            {
                var d = UiFactory.Image(_dotRow, null, new Color(1f, 1f, 1f, 0.2f));
                d.raycastTarget = false;
                var x = 14f * i;
                UiFactory.Anchor(d, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x + 6f, 0f), new Vector2(10f, 10f));
                _dots[i] = d;
                d.gameObject.SetActive(i < show);
            }
        }

        private void OnPhase(MatchPhase phase)
        {
            if (phase == MatchPhase.ReadyCheck)
                UiWidgets.PlaySound(SoundId.UiClick);
            if (phase == MatchPhase.Cancelled)
            {
                if (_flow.Reason == CancelReason.ReadyDeclined || _flow.Reason == CancelReason.ReadyTimeout)
                    _penalty.Register(_clockNow);
                Close();
            }
        }

        private void OnLaunch()
        {
            var launch = _launch;
            Close();
            launch?.Invoke();
        }

        private void OnCancelClicked()
        {
            if (_penaltyShown)
            {
                Close();
                return;
            }

            UiWidgets.PlaySound(SoundId.UiClick);
            _flow?.Cancel();
        }

        private void OnAcceptClicked()
        {
            if (_flow != null && _flow.Accept())
                UiWidgets.PlaySound(SoundId.UiClick);
        }

        private void Close()
        {
            if (_flow != null)
            {
                _flow.PhaseChanged -= OnPhase;
                _flow.LaunchRequested -= OnLaunch;
            }

            _flow = null;
            _penaltyShown = false;
            UiFactory.SetButtonLabel(_cancel, "İPTAL");
            _group.interactable = false;
            _group.blocksRaycasts = false;
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _clockNow += dt;
            var visible = _flow != null || _penaltyShown;
            _shown = MainMenuMotion.Approach(_shown, visible ? 1f : 0f, 14f, dt);
            _group.alpha = _shown;
            if (_flow == null)
                return;

            _group.interactable = true;
            _group.blocksRaycasts = true;
            _flow.Tick(dt);
            if (_flow == null)
                return;
            Refresh();

            var kb = Keyboard.current;
            if (kb != null && _flow != null && _flow.Phase == MatchPhase.ReadyCheck && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                OnAcceptClicked();
        }

        private void Refresh()
        {
            var f = _flow;
            if (f == null)
                return;
            var hour = DateTime.Now.Hour;
            var info = PlayQueueModes.Get(f.Kind);
            var head = info.Title + (string.IsNullOrEmpty(_context) ? string.Empty : "  ·  " + _context);
            var requeue = f.Requeues > 0 ? "  (öncelikli yeniden arama)" : string.Empty;

            switch (f.Phase)
            {
                case MatchPhase.Searching:
                    _status.text = "ARANIYOR";
                    _clock.text = QueueEstimator.FormatClock(f.SearchElapsed);
                    _sub.text = head + "\nTahmini bekleme " + _estimator.FormatRange(f.Kind, hour) + requeue;
                    SetFill(QueueEstimator.Progress(f.SearchElapsed, f.Estimate), UiKitTokens.Accent);
                    var alt = QueueEstimator.SuggestAlternative(f.Kind, f.SearchElapsed);
                    _hint.text = _estimator.IsOverdue(f.Kind, hour, f.SearchElapsed)
                        ? "Beklenenden uzun sürdü" + (alt.HasValue ? " — " + PlayQueueModes.Get(alt.Value).Title + " daha hızlı eşleşir." : ".")
                        : string.Empty;
                    _cancel.gameObject.SetActive(true);
                    _accept.gameObject.SetActive(false);
                    break;
                case MatchPhase.ReadyCheck:
                    _status.text = "MAÇ BULUNDU";
                    _clock.text = Mathf.CeilToInt(f.ReadyRemaining).ToString();
                    _sub.text = head + "\nHazır: " + f.ReadyCount + " / " + f.SquadSize;
                    SetFill(f.ReadyRemaining / MatchmakingFlow.ReadySeconds, Gold);
                    _hint.text = f.PlayerAccepted ? "Takım bekleniyor…" : "Süre dolmadan KABUL ET'e bas (Enter).";
                    _accept.gameObject.SetActive(!f.PlayerAccepted);
                    _cancel.gameObject.SetActive(true);
                    break;
                case MatchPhase.Starting:
                    _status.text = "HAREKÂT BAŞLIYOR";
                    _clock.text = Mathf.CeilToInt(f.StartRemaining).ToString();
                    _sub.text = head + "\nTim tamam, intikal hazırlanıyor.";
                    SetFill(1f - f.StartRemaining / MatchmakingFlow.StartCountdown, Ready);
                    _hint.text = string.Empty;
                    _accept.gameObject.SetActive(false);
                    _cancel.gameObject.SetActive(false);
                    break;
            }

            for (var i = 0; i < _dots.Length; i++)
                _dots[i].color = f.SlotReady(i) ? Ready : new Color(1f, 1f, 1f, 0.2f);
        }

        private void SetFill(float t, Color color)
        {
            _fill.anchorMax = new Vector2(Mathf.Clamp01(t), 1f);
            _fill.offsetMin = Vector2.zero;
            _fill.offsetMax = Vector2.zero;
            _fillImage.color = color;
        }
    }
}
