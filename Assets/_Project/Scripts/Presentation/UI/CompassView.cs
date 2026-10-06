using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Drone;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Üst orta pusula şeridi: 5°'lik çentikler, 15°'de bir derece / ana-ara yön harfleri (K, KD, D, GD, G, GB, B, KB),
    /// ortada bakış derecesi ("247°") ve şerit üzerinde işaretler: tim arkadaşları (mavi), iniş bölgesi (amber),
    /// harita işareti (sarı), topçu hedefi (kırmızı). Görüş dışındaki işaretler kenara yapışır (soluk).
    /// Şerit içeriği bir kez kurulur; her karede yalnızca içerik kaydırılır (iç içe tuval).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompassView : MonoBehaviour
    {
        public const float Width = 720f;
        public const float StripHeight = 34f;
        public const float TopMargin = 14f;
        public const float VisibleDegrees = 180f;

        private const float PixelsPerDegree = Width / VisibleDegrees;
        private const int MinDegree = -105;
        private const int MaxDegree = 465;
        private const int MaxAllyMarkers = 12;

        private static readonly string[] Cardinals = { "K", "KD", "D", "GD", "G", "GB", "B", "KB" };

        private enum MarkerKind
        {
            Ally,
            LandingZone,
            Waypoint,
            Artillery,
            Ping
        }

        private sealed class Marker
        {
            public RectTransform Rect;
            public Image Icon;
            public Text Label;
            public MarkerKind Kind;
        }

        private HudContext _ctx;
        private RectTransform _viewport;
        private RectTransform _content;
        private RectTransform _markerLayer;
        private Text _degrees;
        private readonly List<Marker> _allyMarkers = new List<Marker>(MaxAllyMarkers);
        private Marker _landingZone;
        private Marker _waypoint;
        private Marker _artillery;
        private Marker _ping;
        private readonly List<Marker> _reconMarkers = new List<Marker>(8);
        private readonly List<Combatant> _reconBuffer = new List<Combatant>(16);
        private float _shownYaw = float.NaN;
        private string _pingText;
        private bool _pingEnemy;

        public RectTransform Root { get; private set; }

        public static CompassView Create(RectTransform parent, HudContext context)
        {
            var root = HudBuild.Rect("Compass", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -TopMargin),
                new Vector2(Width + 40f, StripHeight + 40f));
            var view = root.gameObject.AddComponent<CompassView>();
            view._ctx = context;
            view.Root = root;
            view.Build();
            return view;
        }

        private void Build()
        {
            HudBuild.PassiveGroup(Root);
            HudBuild.NestedCanvas(Root);

            // Şerit zemini: kenarlara doğru solan koyu bant.
            var backdrop = HudBuild.Image("Backdrop", Root, UiSprites.White, UiTheme.WithAlpha(Color.black, 0.32f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(Width, StripHeight));
            backdrop.gameObject.name = "Backdrop";

            _viewport = HudBuild.Rect("Viewport", Root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero,
                new Vector2(Width, StripHeight));
            _viewport.gameObject.AddComponent<RectMask2D>();

            _content = HudBuild.Rect("Content", _viewport, HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(1f, StripHeight));

            for (var d = MinDegree; d <= MaxDegree; d += 5)
                BuildTick(d);

            // İşaret katmanı (kaydırılmaz; konumlar bakış farkından hesaplanır).
            _markerLayer = HudBuild.Rect("Markers", Root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero,
                new Vector2(Width, StripHeight));

            // Orta işaretçi ve derece kutusu.
            // Şeridin altında, şeride bakan (yukarı) üçgen.
            HudBuild.Image("Pointer", Root, UiSprites.Triangle, UiTheme.Amber, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -StripHeight - 1f), new Vector2(12f, 9f));

            var centerLine = HudBuild.Image("CenterLine", Root, UiSprites.White, UiTheme.Amber, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, new Vector2(2f, StripHeight));
            centerLine.gameObject.name = "CenterLine";

            var box = HudBuild.Image("DegreeBox", Root, UiSprites.ChamferRect, UiTheme.WithAlpha(UiTheme.PanelDark, 0.8f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -StripHeight - 11f), new Vector2(64f, 24f));
            _degrees = HudBuild.Text("Degrees", box.transform, "0°", UiTheme.FontSmall, TextAnchor.MiddleCenter, UiTheme.Text,
                FontStyle.Bold, HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(64f, 24f), false);

            _landingZone = CreateMarker(MarkerKind.LandingZone, UiSprites.Chevron, UiTheme.Amber, new Vector2(16f, 12f), "İB");
            _waypoint = CreateMarker(MarkerKind.Waypoint, UiSprites.Diamond, new Color(1f, 0.86f, 0.25f, 1f), new Vector2(12f, 12f), null);
            _artillery = CreateMarker(MarkerKind.Artillery, UiSprites.Diamond, UiTheme.EnemyRed, new Vector2(12f, 12f), "TOPÇU");
            _ping = CreateMarker(MarkerKind.Ping, UiSprites.Triangle, UiTheme.Amber, new Vector2(14f, 12f), null);
        }

        private void BuildTick(int degree)
        {
            var normalized = ((degree % 360) + 360) % 360;
            var major = normalized % 15 == 0;
            var cardinal = normalized % 45 == 0;
            var x = degree * PixelsPerDegree;

            var height = cardinal ? 12f : major ? 9f : 5f;
            var color = cardinal ? UiTheme.Text : UiTheme.WithAlpha(UiTheme.TextDim, major ? 0.85f : 0.5f);
            var tick = HudBuild.Image("Tick", _content, UiSprites.White, color, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(x, 0f), new Vector2(cardinal ? 2f : 1.5f, height));
            tick.gameObject.name = "T" + degree;

            if (!major)
                return;

            string text;
            int size;
            Color labelColor;
            if (cardinal)
            {
                text = Cardinals[normalized / 45];
                size = UiTheme.FontSmall;
                labelColor = normalized == 0 ? UiTheme.Amber : UiTheme.Text;
            }
            else
            {
                text = UiWidgets.Number(normalized);
                size = 13;
                labelColor = UiTheme.TextDim;
            }

            var label = HudBuild.Text("L" + degree, _content, text, size, TextAnchor.UpperCenter, labelColor,
                cardinal ? FontStyle.Bold : FontStyle.Normal, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(x, -1f), new Vector2(40f, 20f), false);
            label.gameObject.name = "L" + degree;
        }

        private Marker CreateMarker(MarkerKind kind, Sprite sprite, Color color, Vector2 size, string label)
        {
            var marker = new Marker { Kind = kind };
            marker.Rect = HudBuild.Rect(kind.ToString(), _markerLayer, HudBuild.Center, HudBuild.Center, Vector2.zero, size);
            marker.Icon = HudBuild.FillImage("Icon", marker.Rect, sprite, color);
            if (kind == MarkerKind.LandingZone)
                marker.Icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);

            if (!string.IsNullOrEmpty(label))
            {
                marker.Label = HudBuild.Text("Label", marker.Rect, label, 14, TextAnchor.UpperCenter, color, FontStyle.Bold,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -1f), new Vector2(60f, 14f));
            }

            HudBuild.SetActive(marker.Rect, false);
            return marker;
        }

        private Marker GetAllyMarker(int index)
        {
            while (_allyMarkers.Count <= index)
            {
                var marker = CreateMarker(MarkerKind.Ally, UiSprites.Diamond, UiTheme.AllyBlue, new Vector2(9f, 9f), null);
                _allyMarkers.Add(marker);
            }

            return _allyMarkers[index];
        }

        /// <summary>HUD denetleyicisi her karede çağırır.</summary>
        public void Tick(float deltaTime)
        {
            var yaw = _ctx.Yaw;
            if (float.IsNaN(_shownYaw) || Mathf.Abs(Mathf.DeltaAngle(yaw, _shownYaw)) > 0.05f)
            {
                _shownYaw = yaw;
                HudBuild.SetPosition(_content, new Vector2(-yaw * PixelsPerDegree, 0f));
                UiFactory.SetText(_degrees, HudFormat.Degrees(yaw));
            }

            UpdateMarkers(yaw);
        }

        private void UpdateMarkers(float yaw)
        {
            var origin = _ctx.Position;

            // Tim arkadaşları.
            var squad = _ctx.Squad;
            var used = 0;
            var local = _ctx.Local;
            for (var i = 0; i < squad.Count && used < MaxAllyMarkers; i++)
            {
                var c = squad[i].Combatant;
                if (c == null || c == local || !c.IsAlive)
                    continue;

                var marker = GetAllyMarker(used++);
                PlaceMarker(marker, origin, c.transform.position, yaw, -StripHeight * 0.5f + 6f, false, 1f);
            }

            for (var i = used; i < _allyMarkers.Count; i++)
                HudBuild.SetActive(_allyMarkers[i].Rect, false);

            // İHA keşfi: işaretli düşmanlar.
            var reconUsed = 0;
            if (local != null)
            {
                ReconDroneSystem.GetMarked(local.Team, _reconBuffer);
                for (var i = 0; i < _reconBuffer.Count && reconUsed < MaxAllyMarkers; i++)
                {
                    while (_reconMarkers.Count <= reconUsed)
                        _reconMarkers.Add(CreateMarker(MarkerKind.Artillery, UiSprites.Diamond, UiTheme.EnemyRed, new Vector2(10f, 10f), null));
                    PlaceMarker(_reconMarkers[reconUsed++], origin, _reconBuffer[i].transform.position, yaw, -StripHeight * 0.5f + 6f, false, 1f);
                }
            }

            for (var i = reconUsed; i < _reconMarkers.Count; i++)
                HudBuild.SetActive(_reconMarkers[i].Rect, false);

            // İniş bölgesi (intikal sırasında).
            var showLz = false;
            var lz = Vector3.zero;
            var pc = _ctx.PlayerController;
            if (pc != null)
            {
                try
                {
                    var transport = pc.Transport;
                    if (transport != null && (pc.IsInTransport || _ctx.Player.DropState == DropState.InTransport))
                    {
                        lz = transport.LandingZone;
                        showLz = true;
                    }
                }
                catch (Exception)
                {
                    showLz = false;
                }
            }

            if (showLz)
                PlaceMarker(_landingZone, origin, lz, yaw, 0f, true, 1f);
            else
                HudBuild.SetActive(_landingZone.Rect, false);

            // Harita işareti.
            var showWaypoint = false;
            var waypoint = Vector3.zero;
            if (pc != null)
            {
                try
                {
                    var marker = pc.MapMarker;
                    if (marker.HasValue)
                    {
                        waypoint = marker.Value;
                        showWaypoint = true;
                    }
                }
                catch (Exception)
                {
                    showWaypoint = false;
                }
            }

            if (showWaypoint)
                PlaceMarker(_waypoint, origin, waypoint, yaw, 0f, true, 1f);
            else
                HudBuild.SetActive(_waypoint.Rect, false);

            // Etkin topçu atışı (yerel timin).
            var showArtillery = false;
            var target = Vector3.zero;
            var artillery = _ctx.Artillery;
            if (artillery != null && _ctx.LocalTeam >= 0)
            {
                try
                {
                    if (artillery.TryGetActiveStrike(_ctx.LocalTeam, out var t))
                    {
                        target = new Vector3(t.X, t.Y, t.Z);
                        showArtillery = true;
                    }
                }
                catch (Exception)
                {
                    showArtillery = false;
                }
            }

            if (showArtillery)
                PlaceMarker(_artillery, origin, target, yaw, 0f, true, 1f);
            else
                HudBuild.SetActive(_artillery.Rect, false);

            // Tim ping'i (düşman: düşman rengi, nokta: kehribar); süre bittikçe solar.
            if (_ctx.LocalTeam >= 0 && PingBoard.TryGet(_ctx.LocalTeam, Time.time, out var ping))
            {
                var color = ping.IsEnemy ? UiTheme.EnemyRed : UiTheme.Amber;
                _ping.Icon.color = color;
                if (_ping.Label == null)
                    _ping.Label = HudBuild.Text("Label", _ping.Rect, string.Empty, 14, TextAnchor.UpperCenter, color, FontStyle.Bold,
                        new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -1f), new Vector2(80f, 14f));
                var meters = HudFormat.Meters(Vector3.Distance(origin, ping.Position));
                if (_pingText != meters || _pingEnemy != ping.IsEnemy)
                {
                    _pingText = meters;
                    _pingEnemy = ping.IsEnemy;
                    UiFactory.SetText(_ping.Label, ping.Label + " " + meters);
                }

                UiFactory.SetColor(_ping.Label, color);
                PlaceMarker(_ping, origin, ping.Position, yaw, 0f, true, Mathf.Lerp(0.5f, 1f, ping.Remaining01(Time.time)));
            }
            else
            {
                HudBuild.SetActive(_ping.Rect, false);
            }
        }

        private static void PlaceMarker(Marker marker, Vector3 origin, Vector3 target, float yaw, float y, bool clampToEdge, float alpha)
        {
            var bearing = HudFormat.Bearing(origin, target);
            var delta = Mathf.DeltaAngle(yaw, bearing);
            var half = VisibleDegrees * 0.5f - 2f;
            var outside = Mathf.Abs(delta) > half;
            if (outside && !clampToEdge)
            {
                HudBuild.SetActive(marker.Rect, false);
                return;
            }

            HudBuild.SetActive(marker.Rect, true);
            var clamped = Mathf.Clamp(delta, -half, half);
            HudBuild.SetPosition(marker.Rect, new Vector2(clamped * PixelsPerDegree, y));
            HudBuild.SetAlpha(marker.Icon, outside ? alpha * 0.45f : alpha);
            if (marker.Label != null)
                HudBuild.SetAlpha(marker.Label, outside ? alpha * 0.45f : alpha);
        }
    }
}
