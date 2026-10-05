using System;
using Project.Core.Domain;
using Project.Infrastructure.Combat;
using Project.Infrastructure.World;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Sağ üst durum: "TİM: x | HAYATTA: y | ÖLDÜRME: z", harekât alanı sayacı ("Harekât alanı daralıyor 01:20"),
    /// alan dışı uyarısı (güvenli bölgeye uzaklık) ve bulunulan yer adı; sol üstte isteğe bağlı FPS sayacı.
    /// Saniyede 4 kez güncellenir, metin yalnızca değerler değişince oluşturulur.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudStatusView : MonoBehaviour
    {
        public const float Width = 560f;
        public const float TopMargin = 18f;
        public const float RightMargin = 24f;
        public const float Height = 112f;
        private const float UpdateInterval = 0.25f;
        private const float FpsWindow = 0.5f;

        private static string _labelHex;
        private static string _valueHex;
        private static string _sepHex;

        private HudContext _ctx;
        private Text _counters;
        private Image _countersBackdrop;
        private Text _zoneText;
        private Text _outsideText;
        private Text _locationText;
        private Text _fps;

        private float _nextUpdate;
        private int _shownTeams = int.MinValue;
        private int _shownAlive = int.MinValue;
        private int _shownKills = int.MinValue;
        private int _zoneKey = int.MinValue;
        private int _outsideKey = int.MinValue;
        private float _nextLocation;
        private string _shownLocation;

        private bool _fpsEnabled;
        private float _fpsElapsed;
        private int _fpsFrames;
        private int _shownFps = -1;

        public RectTransform Root { get; private set; }

        /// <summary>Oyuncu alan dışında mı (son güncelleme)?</summary>
        public bool OutsideZone { get; private set; }

        public static HudStatusView Create(RectTransform parent, HudContext context)
        {
            var root = HudBuild.Fill("Status", parent);
            var view = root.gameObject.AddComponent<HudStatusView>();
            view._ctx = context;
            view.Root = root;
            view.Build();
            return view;
        }

        private void Build()
        {
            HudBuild.PassiveGroup(Root);
            EnsureColors();

            var panel = HudBuild.Rect("TopRight", Root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-RightMargin, -TopMargin),
                new Vector2(Width, Height));

            _countersBackdrop = HudBuild.Image("CountersBg", panel, UiSprites.ChamferRect, UiTheme.WithAlpha(UiTheme.PanelDark, 0.72f),
                new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(Width, 34f));
            _counters = HudBuild.Text("Counters", panel, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleRight, UiTheme.Text,
                FontStyle.Bold, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, 0f), new Vector2(Width - 28f, 34f));

            _zoneText = HudBuild.Text("Zone", panel, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleRight, UiTheme.Text,
                FontStyle.Bold, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-4f, -38f), new Vector2(Width, 24f));
            _outsideText = HudBuild.Text("Outside", panel, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleRight, UiTheme.AllyBlue,
                FontStyle.Bold, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-4f, -62f), new Vector2(Width, 24f));
            _locationText = HudBuild.Text("Location", panel, string.Empty, UiTheme.FontTiny, TextAnchor.MiddleRight, UiTheme.TextDim,
                FontStyle.Bold, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-4f, -86f), new Vector2(Width, 20f));

            _fps = HudBuild.Text("Fps", Root, string.Empty, UiTheme.FontTiny, TextAnchor.UpperLeft, UiTheme.Success, FontStyle.Bold,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -8f), new Vector2(120f, 20f));
            _fps.enabled = false;
        }

        private static void EnsureColors()
        {
            if (_labelHex != null)
                return;

            _labelHex = HudFormat.Hex(UiTheme.TextDim);
            _valueHex = HudFormat.Hex(UiTheme.Text);
            _sepHex = HudFormat.Hex(UiTheme.TextMuted);
        }

        /// <summary>FPS sayacını açar/kapatır (ayar: ShowFps).</summary>
        public void SetFpsVisible(bool visible)
        {
            _fpsEnabled = visible;
            _fps.enabled = visible;
            _fpsElapsed = 0f;
            _fpsFrames = 0;
            _shownFps = -1;
        }

        /// <summary>HUD denetleyicisi her karede çağırır.</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (_fpsEnabled)
                UpdateFps(unscaledDeltaTime);

            if (Time.unscaledTime < _nextUpdate)
                return;

            _nextUpdate = Time.unscaledTime + UpdateInterval;
            UpdateCounters();
            UpdateZone();
            UpdateLocation();
        }

        private void UpdateFps(float dt)
        {
            _fpsElapsed += dt;
            _fpsFrames++;
            if (_fpsElapsed < FpsWindow)
                return;

            var fps = Mathf.RoundToInt(_fpsFrames / Mathf.Max(0.0001f, _fpsElapsed));
            _fpsElapsed = 0f;
            _fpsFrames = 0;
            if (fps == _shownFps)
                return;

            _shownFps = fps;
            UiFactory.SetText(_fps, "FPS " + UiWidgets.Number(fps));
            UiFactory.SetColor(_fps, fps >= 55 ? UiTheme.Success : fps >= 30 ? UiTheme.Amber : UiTheme.HealthLow);
        }

        private void UpdateCounters()
        {
            int teams;
            int alive;
            var match = _ctx.Match;
            if (match != null)
            {
                try
                {
                    teams = match.AliveTeamCount;
                    alive = match.AlivePlayerCount;
                }
                catch (Exception)
                {
                    teams = CountTeamsFallback(out alive);
                }
            }
            else
            {
                teams = CountTeamsFallback(out alive);
            }

            var kills = _ctx.LocalKills;
            if (teams == _shownTeams && alive == _shownAlive && kills == _shownKills)
                return;

            _shownTeams = teams;
            _shownAlive = alive;
            _shownKills = kills;

            var sep = HudFormat.Colorize("   |   ", _sepHex);
            var text = HudFormat.Colorize("TİM: ", _labelHex) + HudFormat.Colorize(UiWidgets.Number(teams), _valueHex) + sep
                       + HudFormat.Colorize("HAYATTA: ", _labelHex) + HudFormat.Colorize(UiWidgets.Number(alive), _valueHex) + sep
                       + HudFormat.Colorize("ÖLDÜRME: ", _labelHex) + HudFormat.Colorize(UiWidgets.Number(kills), HudFormat.Hex(kills > 0 ? UiTheme.Amber : UiTheme.Text));
            _counters.text = text;
            var width = Mathf.Clamp(_counters.preferredWidth + 32f, 200f, Width);
            _countersBackdrop.rectTransform.sizeDelta = new Vector2(width, 34f);
        }

        private static int CountTeamsFallback(out int alive)
        {
            alive = 0;
            var mask = 0L;
            var extra = 0;
            var all = CombatantRegistry.All;
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !c.IsAlive || !c.IsInitialized)
                    continue;

                alive++;
                if (c.Team >= 0 && c.Team < 64)
                    mask |= 1L << c.Team;
                else
                    extra = 1;
            }

            var teams = 0;
            while (mask != 0)
            {
                mask &= mask - 1;
                teams++;
            }

            return teams + extra;
        }

        private void UpdateZone()
        {
            var zone = _ctx.Zone;
            var active = false;
            var stage = ZoneStage.Idle;
            var remaining = 0f;
            var phase = 0;
            var phaseCount = 0;
            if (zone != null)
            {
                try
                {
                    active = zone.IsActive;
                    stage = zone.Stage;
                    remaining = zone.StageRemainingSeconds;
                    phase = zone.PhaseIndex;
                    phaseCount = zone.PhaseCount;
                }
                catch (Exception)
                {
                    active = false;
                }
            }

            if (!active || stage == ZoneStage.Idle)
            {
                SetZoneText(-1, string.Empty, UiTheme.Text);
                SetOutside(-1, string.Empty);
                OutsideZone = false;
                return;
            }

            var seconds = Mathf.Max(0, Mathf.CeilToInt(remaining));
            var key = ((int)stage * 100 + Mathf.Clamp(phase, 0, 99)) * 100000 + Mathf.Min(seconds, 99999);
            if (key != _zoneKey)
            {
                var phaseText = phaseCount > 0 ? "FAZ " + UiWidgets.Number(Mathf.Clamp(phase + 1, 1, phaseCount)) + "/" + UiWidgets.Number(phaseCount) + "  ·  " : string.Empty;
                switch (stage)
                {
                    case ZoneStage.Shrinking:
                        SetZoneText(key, phaseText + "Harekât alanı daralıyor " + UiWidgets.Clock(remaining),
                            seconds <= 10 ? UiTheme.HealthLow : UiTheme.Amber);
                        break;
                    case ZoneStage.Waiting:
                        SetZoneText(key, phaseText + "Harekât alanı " + UiWidgets.Clock(remaining) + " sonra daralacak",
                            seconds <= 15 ? UiTheme.Amber : UiTheme.Text);
                        break;
                    default:
                        SetZoneText(key, "Harekât alanı son sınırında", UiTheme.TextDim);
                        break;
                }
            }

            // Alan dışı / sonraki alan dışı uyarısı.
            var pos = _ctx.Position;
            var inside = true;
            var insideNext = true;
            var distance = 0f;
            try
            {
                inside = zone.IsInsideZone(pos.x, pos.z);
                if (!inside)
                {
                    var current = zone.CurrentZone;
                    var dx = pos.x - current.CenterX;
                    var dz = pos.z - current.CenterZ;
                    distance = Mathf.Max(0f, Mathf.Sqrt(dx * dx + dz * dz) - current.Radius);
                }
                else if (stage != ZoneStage.Finished)
                {
                    var next = zone.NextZone;
                    if (next.Radius > 0f && !next.Contains(pos.x, pos.z))
                    {
                        insideNext = false;
                        var dx = pos.x - next.CenterX;
                        var dz = pos.z - next.CenterZ;
                        distance = Mathf.Max(0f, Mathf.Sqrt(dx * dx + dz * dz) - next.Radius);
                    }
                }
            }
            catch (Exception)
            {
                inside = true;
                insideNext = true;
            }

            OutsideZone = !inside && _ctx.LocalAlive;
            var meters = Mathf.RoundToInt(distance);
            if (!_ctx.LocalAlive || (inside && insideNext))
            {
                SetOutside(-1, string.Empty);
            }
            else if (!inside)
            {
                SetOutside(meters, "ALAN DIŞINDASIN — güvenli bölgeye " + HudFormat.Meters(meters));
                UiFactory.SetColor(_outsideText, UiTheme.Lighten(UiTheme.AllyBlue, 0.2f));
            }
            else
            {
                SetOutside(1000000 + meters, "Sonraki alanın dışındasın — " + HudFormat.Meters(meters));
                UiFactory.SetColor(_outsideText, UiTheme.TextDim);
            }
        }

        private void SetZoneText(int key, string text, Color color)
        {
            if (key == _zoneKey && key != -1)
                return;

            _zoneKey = key;
            UiFactory.SetText(_zoneText, text);
            UiFactory.SetColor(_zoneText, color);
        }

        private void SetOutside(int key, string text)
        {
            if (key == _outsideKey)
                return;

            _outsideKey = key;
            UiFactory.SetText(_outsideText, text);
        }

        private void UpdateLocation()
        {
            if (Time.unscaledTime < _nextLocation)
                return;

            _nextLocation = Time.unscaledTime + 1f;
            string name = null;
            var world = WorldMetadata.Instance;
            if (world != null && _ctx.LocalAlive)
            {
                try
                {
                    name = world.GetLocationName(_ctx.Position);
                }
                catch (Exception)
                {
                    name = null;
                }
            }

            if (ReferenceEquals(name, _shownLocation))
                return;

            _shownLocation = name;
            UiFactory.SetText(_locationText, string.IsNullOrEmpty(name) ? string.Empty : "Konum: " + name);
        }
    }
}
