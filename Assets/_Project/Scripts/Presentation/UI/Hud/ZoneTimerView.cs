using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Bölge sayacı + ikmal uyarısı: radyal silinen geri sayım halkası (beyaz→amber→kırmızı), "BÖLGE KAPANIYOR"
    /// kayarak giren 2 sn'lik şerit ve paraşüt simgeli/yön oklu "İKMAL YOLDA" şeridi. Tüm animasyonlar 0.15–0.25 sn,
    /// yumuşatmalı; HUD kökünün (ölçek + opaklık) altında olduğu için HUD ayarlarını otomatik izler.
    /// Kendi kendini kurar: <see cref="Create"/> ile HUD köküne eklenir ve kendi Update'i ile çalışır.
    /// ENTEGRASYON HudController: <c>Make("ZoneTimerView", () => ZoneTimerView.Create(Root, _ctx))</c> satırı Build()'e eklenmeli.
    /// ENTEGRASYON Audio: <see cref="HudStingers.Played"/> olayı faz geçişlerinde tetiklenir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ZoneTimerView : MonoBehaviour
    {
        private const float RingSize = 64f;
        private const float TopOffset = 96f;
        private const float BannerWidth = 360f;
        private const float BannerHeight = 34f;
        private const float SlideDistance = 70f;

        private struct Drop
        {
            public int Id;
            public AirdropStage Stage;
            public Vector3 Position;
            public float EventTime;
            public float Seconds;
        }

        private HudContext _ctx;
        private CanvasGroup _ringGroup;
        private Image _ringBack;
        private Image _ring;
        private Text _clock;
        private Text _label;

        private RectTransform _zoneBanner;
        private CanvasGroup _zoneBannerGroup;
        private Text _zoneBannerText;
        private float _zoneBannerAge = -1f;

        private RectTransform _airBanner;
        private CanvasGroup _airBannerGroup;
        private Text _airText;
        private Text _airDistance;
        private RectTransform _airArrow;
        private RectTransform _airGlyph;
        private float _airShow;

        private readonly List<Drop> _drops = new List<Drop>(4);
        private IEventBus _bus;
        private readonly Action<AirdropEvent> _onAirdrop;

        private ZoneStage _lastStage = ZoneStage.Idle;
        private int _lastPhase = -1;
        private float _fill;
        private float _ringAlpha;

        public RectTransform Root { get; private set; }

        public ZoneTimerView()
        {
            _onAirdrop = OnAirdrop;
        }

        public static ZoneTimerView Create(RectTransform parent, HudContext context)
        {
            var root = HudBuild.Fill("ZoneTimer", parent);
            var view = root.gameObject.AddComponent<ZoneTimerView>();
            view._ctx = context;
            view.Root = root;
            view.Build();
            return view;
        }

        private void Build()
        {
            var top = new Vector2(0.5f, 1f);

            var ring = HudBuild.Rect("Ring", Root, top, top, new Vector2(0f, -TopOffset), new Vector2(RingSize, RingSize));
            _ringGroup = HudBuild.PassiveGroup(ring, 0f);
            _ringBack = HudBuild.FillImage("Back", ring, UiSprites.Ring, UiTheme.WithAlpha(Color.black, 0.45f));
            _ring = HudBuild.FillImage("Wipe", ring, UiSprites.Ring, Color.white);
            _ring.type = Image.Type.Filled;
            _ring.fillMethod = Image.FillMethod.Radial360;
            _ring.fillOrigin = (int)Image.Origin360.Top;
            _ring.fillClockwise = false;
            _ring.fillAmount = 0f;
            _clock = HudBuild.Text("Clock", ring, string.Empty, UiTheme.FontMedium, TextAnchor.MiddleCenter, Color.white,
                FontStyle.Bold, HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(RingSize, 24f));
            _label = HudBuild.Text("Label", ring, string.Empty, 11, TextAnchor.UpperCenter, UiTheme.TextDim, FontStyle.Bold,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(140f, 14f));

            _zoneBanner = HudBuild.Rect("ZoneBanner", Root, top, top, new Vector2(0f, -(TopOffset + RingSize + 26f)),
                new Vector2(BannerWidth, BannerHeight));
            _zoneBannerGroup = HudBuild.PassiveGroup(_zoneBanner, 0f);
            HudBuild.FillImage("Bg", _zoneBanner, HudBuild.Banner, UiTheme.WithAlpha(UiTheme.Danger, 0.8f));
            _zoneBannerText = HudBuild.FillText("Text", _zoneBanner, "BÖLGE KAPANIYOR", UiTheme.FontMedium, TextAnchor.MiddleCenter,
                Color.white, FontStyle.Bold);

            _airBanner = HudBuild.Rect("AirdropBanner", Root, top, top, new Vector2(0f, -(TopOffset + RingSize + 70f)),
                new Vector2(BannerWidth, BannerHeight + 4f));
            _airBannerGroup = HudBuild.PassiveGroup(_airBanner, 0f);
            HudBuild.FillImage("Bg", _airBanner, HudBuild.Banner, UiTheme.WithAlpha(new Color(0.1f, 0.14f, 0.1f), 0.8f));
            BuildParachuteGlyph();
            _airText = HudBuild.Text("Text", _airBanner, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleCenter, UiTheme.Amber,
                FontStyle.Bold, HudBuild.Center, HudBuild.Center, new Vector2(0f, 0f), new Vector2(220f, BannerHeight));
            _airArrow = HudBuild.Rect("Arrow", _airBanner, new Vector2(1f, 0.5f), HudBuild.Center, new Vector2(-34f, 0f), new Vector2(22f, 22f));
            HudBuild.FillImage("Tip", _airArrow, UiSprites.Triangle, UiTheme.Amber);
            _airDistance = HudBuild.Text("Distance", _airBanner, string.Empty, 12, TextAnchor.MiddleRight, UiTheme.Text, FontStyle.Bold,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-52f, 0f), new Vector2(64f, 18f));
        }

        /// <summary>Paraşüt simgesi: kubbe (yassı daire) + 3 ip + sandık, tamamen prosedürel sprite.</summary>
        private void BuildParachuteGlyph()
        {
            _airGlyph = HudBuild.Rect("Parachute", _airBanner, new Vector2(0f, 0.5f), HudBuild.Center, new Vector2(30f, 0f), new Vector2(30f, 26f));
            HudBuild.Image("Canopy", _airGlyph, UiSprites.Circle, UiTheme.Amber, new Vector2(0f, 6f), new Vector2(26f, 14f));
            for (var i = -1; i <= 1; i++)
            {
                var line = HudBuild.Image("Line" + i, _airGlyph, UiSprites.White, UiTheme.WithAlpha(UiTheme.Text, 0.8f),
                    new Vector2(i * 4.5f, -3f), new Vector2(1.2f, 13f));
                line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -i * 22f);
            }

            HudBuild.Image("Crate", _airGlyph, UiSprites.White, UiTheme.Text, new Vector2(0f, -11f), new Vector2(7f, 5f));
        }

        private void OnEnable() => Rebind();

        private void OnDisable()
        {
            if (_bus != null)
            {
                try { _bus.Unsubscribe(_onAirdrop); }
                catch (Exception) { /* yok sayılır */ }
                _bus = null;
            }
        }

        private void Rebind()
        {
            var bus = _ctx != null ? _ctx.Bus : null;
            if (ReferenceEquals(bus, _bus))
                return;

            if (_bus != null)
            {
                try { _bus.Unsubscribe(_onAirdrop); }
                catch (Exception) { /* yok sayılır */ }
            }

            _bus = bus;
            if (_bus != null)
                _bus.Subscribe(_onAirdrop);
        }

        private void OnAirdrop(AirdropEvent e)
        {
            var pos = new Vector3(e.Position.X, e.Position.Y, e.Position.Z);
            var index = _drops.FindIndex(d => d.Id == e.Id);
            var drop = new Drop { Id = e.Id, Stage = e.Stage, Position = pos, EventTime = Time.unscaledTime, Seconds = Mathf.Max(0f, e.SecondsToNextStage) };
            if (e.Stage == AirdropStage.Opened)
            {
                if (index >= 0)
                    _drops.RemoveAt(index);
                return;
            }

            if (index >= 0)
                _drops[index] = drop;
            else
                _drops.Add(drop);

            if (e.Stage == AirdropStage.Announced)
                HudStingers.Raise(HudStingerKind.AirdropInbound);
            else if (e.Stage == AirdropStage.Landed)
                HudStingers.Raise(HudStingerKind.AirdropLanded);
        }

        private void Update()
        {
            if (_ctx == null)
                return;

            var dt = Time.unscaledDeltaTime;
            Rebind();
            try
            {
                TickZone(dt);
                TickAirdrop(dt);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                enabled = false;
            }
        }

        private void TickZone(float dt)
        {
            var zone = _ctx.Zone;
            var active = false;
            var stage = ZoneStage.Idle;
            float remaining = 0f, duration = 0f;
            int phase = 0, phaseCount = 0;
            if (zone != null)
            {
                active = zone.IsActive;
                stage = zone.Stage;
                remaining = zone.StageRemainingSeconds;
                duration = zone.StageDurationSeconds;
                phase = zone.PhaseIndex;
                phaseCount = zone.PhaseCount;
            }

            var show = active && stage != ZoneStage.Idle && stage != ZoneStage.Finished && _ctx.LocalAlive;

            if (active && (stage != _lastStage || phase != _lastPhase))
            {
                if (stage == ZoneStage.Shrinking)
                    _zoneBannerAge = 0f;
                if (HudTimerRules.TryStingerForZone(stage, phase, phaseCount, out var kind))
                    HudStingers.Raise(kind);
            }

            _lastStage = stage;
            _lastPhase = phase;

            _ringAlpha = HudTimerRules.Approach(_ringAlpha, show ? 1f : 0f, dt, HudTimerRules.AnimDefault);
            HudBuild.SetAlpha(_ringGroup, HudTimerRules.EaseInOut(_ringAlpha));
            if (show)
            {
                var targetFill = stage == ZoneStage.Shrinking
                    ? HudTimerRules.RemainingFraction(remaining, duration)
                    : 1f - HudTimerRules.RemainingFraction(remaining, duration);
                _fill = HudTimerRules.Approach(_fill, targetFill, dt, HudTimerRules.AnimMax);
                HudBuild.SetFill(_ring, _fill);

                var color = stage == ZoneStage.Shrinking
                    ? UiTheme.Danger
                    : HudTimerRules.CountdownColor(remaining, Color.white, UiTheme.Amber, UiTheme.Danger);
                if (_ring.color != color)
                    _ring.color = color;
                UiFactory.SetText(_clock, UiWidgets.Clock(remaining));
                _clock.color = color;
                UiFactory.SetText(_label, stage == ZoneStage.Shrinking ? "DARALIYOR" : "BÖLGE");
            }

            if (_zoneBannerAge >= 0f)
            {
                _zoneBannerAge += dt;
                var slide = HudTimerRules.BannerSlide(_zoneBannerAge, HudTimerRules.ClosingBannerSeconds, HudTimerRules.AnimDefault);
                var blink = _zoneBannerAge > HudTimerRules.AnimDefault ? 0.85f + 0.15f * Mathf.Sin(_zoneBannerAge * 10f) : 1f;
                HudBuild.SetAlpha(_zoneBannerGroup, (1f - slide) * blink);
                HudBuild.SetPosition(_zoneBanner, new Vector2(0f, -(TopOffset + RingSize + 26f) + slide * SlideDistance));
                if (_zoneBannerAge >= HudTimerRules.ClosingBannerSeconds)
                {
                    _zoneBannerAge = -1f;
                    HudBuild.SetAlpha(_zoneBannerGroup, 0f);
                }
            }
        }

        private void TickAirdrop(float dt)
        {
            var now = Time.unscaledTime;
            var best = -1;
            var bestDist = float.MaxValue;
            for (var i = _drops.Count - 1; i >= 0; i--)
            {
                var d = _drops[i];
                if (!HudVisualRules.IsFinite(d.Position))
                {
                    _drops.RemoveAt(i);
                    continue;
                }

                var dx = d.Position.x - _ctx.Position.x;
                var dz = d.Position.z - _ctx.Position.z;
                var dist = dx * dx + dz * dz;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }

            var show = best >= 0 && _ctx.LocalAlive && _ctx.PlayerValid;
            _airShow = HudTimerRules.Approach(_airShow, show ? 1f : 0f, dt, HudTimerRules.AnimDefault);
            var slide = 1f - HudTimerRules.EaseOut(_airShow);
            HudBuild.SetAlpha(_airBannerGroup, 1f - slide);
            HudBuild.SetPosition(_airBanner, new Vector2(0f, -(TopOffset + RingSize + 70f) + slide * SlideDistance));
            if (!show)
                return;

            var drop = _drops[best];
            var left = Mathf.Max(0f, drop.Seconds - (now - drop.EventTime));
            var inbound = drop.Stage == AirdropStage.Announced;
            UiFactory.SetText(_airText, (inbound ? "İKMAL YOLDA  " : "İKMAL İNDİ  ") + UiWidgets.Clock(left));
            var meters = Mathf.Sqrt(bestDist);
            UiFactory.SetText(_airDistance, HudFormat.Meters(meters));
            var rel = HudRules.RelativeBearing(_ctx.Position, _ctx.Yaw, drop.Position);
            HudBuild.SetRotation(_airArrow, HudTimerRules.ArrowRotation(rel));
            // Paraşüt inerken hafifçe sallanır.
            HudBuild.SetRotation(_airGlyph, inbound ? Mathf.Sin(now * 2.2f) * 6f : 0f);
        }
    }
}
