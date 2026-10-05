using System.Collections.Generic;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Combat;
using Project.Presentation.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Sağ alt köşedeki kare mini harita. Kuzey her zaman yukarıdadır; oyuncu merkezde, yaklaşık 250 m'lik alan görünür
    /// (araçta/intikalde 450 m'ye uzaklaşır). Gösterilenler: <see cref="Project.Infrastructure.World.WorldMetadata.MinimapTexture"/>
    /// (yoksa gri zemin), pafta çizgileri, mavi bölge kenarı + dışı mavi tonlama, sonraki güvenli bölge (beyaz),
    /// güvenli bölgeye yön çizgisi, bakış yönüne dönen oyuncu oku, tim arkadaşları (mavi noktalar, görüş dışındakiler kenara
    /// sabitlenir), iniş bölgesi (LZ) + intikal rotası, topçu hedefi, taarruz emri hedefi ve harita işareti. Altta pafta
    /// etiketi ("C7 · Kuzgun Köyü") ve bölge/işaret mesafesi. RectMask2D ile kırpılır; her karede tahsis yapmaz.
    /// </summary>
    public sealed class MinimapView : MonoBehaviour
    {
        /// <summary>Varsayılan kenar uzunluğu (px, 1920×1080 referans).</summary>
        public const float DefaultSize = 268f;

        /// <summary>Yaya iken görünen alan (m, kenar boyu).</summary>
        public const float DefaultViewMeters = 250f;

        /// <summary>Araçta/intikalde görünen alan (m).</summary>
        public const float MountedViewMeters = 450f;

        private const float FrameThickness = 3f;
        private const float LabelHeight = 30f;
        private const float EdgeMargin = 24f;
        private const float MarkerEdgePadding = 7f;
        private const float ZoomSpeedMetersPerSecond = 500f;
        private const float LabelRefreshInterval = 0.2f;

        private static readonly Color OffMapColor = UiTheme.Hex(0x1A, 0x1F, 0x14);
        private static readonly Color FallbackMapColor = new Color(0.36f, 0.38f, 0.33f, 1f);
        private static readonly Color GridColor = new Color(0f, 0f, 0f, 0.3f);
        private static readonly Color ZoneEdgeColor = UiTheme.WithAlpha(UiTheme.ZoneBlue, 1f);
        private static readonly Color ZoneTintColor = UiTheme.WithAlpha(UiTheme.ZoneBlue, 0.26f);
        private static readonly Color RouteColor = UiTheme.WithAlpha(UiTheme.Amber, 0.9f);
        private static readonly Color SafeLineColor = new Color(1f, 1f, 1f, 0.75f);
        private static readonly Color LandingZoneColor = UiTheme.Success;
        private static readonly Color WaypointColor = UiTheme.Amber;
        private static readonly Color ArtilleryColor = UiTheme.EnemyRed;
        private static readonly Color AttackColor = UiTheme.Accent;
        private static readonly Color PlayerColor = UiTheme.Amber;
        private static readonly Color DeadPlayerColor = UiTheme.TextMuted;

        private sealed class AllyDot
        {
            public RectTransform Root;
            public Image Fill;
            public bool Visible;
            public bool Clamped;
        }

        private MapDataSource _data;
        private CanvasGroup _group;
        private RectTransform _viewport;
        private RectTransform _content;
        private RawImage _mapImage;
        private RectTransform _markers;
        private MapRingGraphic _zoneTint;
        private MapRingGraphic _zoneEdge;
        private MapRingGraphic _nextZone;
        private MapRingGraphic _artilleryRing;
        private MapLineGraphic _routeLine;
        private MapLineGraphic _safeLine;
        private RectTransform _playerArrow;
        private Image _playerArrowFill;
        private RectTransform _landingZone;
        private RectTransform _transportMarker;
        private RectTransform _waypoint;
        private RectTransform _artilleryCross;
        private RectTransform _attackMarker;
        private Text _gridLabel;
        private Text _infoLabel;
        private readonly List<AllyDot> _allies = new List<AllyDot>(12);

        private float _viewportSize;
        private float _viewMeters = DefaultViewMeters;
        private float _mapPixels = -1f;
        private Texture _appliedTexture;
        private bool _textureApplied;
        private float _lastYaw = float.NaN;
        private bool _lastDead;
        private Vector2 _zoneAnchor = new Vector2(-1f, -1f);
        private Vector2 _nextZoneAnchor = new Vector2(-1f, -1f);
        private float _nextLabelRefresh;
        private string _shownGrid;
        private string _shownLocation;
        private int _shownInfoKind = -1;
        private int _shownInfoMeters = -1;
        private bool _built;

        /// <summary>
        /// Mini haritayı HUD köküne (sağ alt) ekler. <paramref name="hudRoot"/> null ise kendi tuvalini oluşturur.
        /// </summary>
        public static MinimapView Create(RectTransform hudRoot, IPlayerHudSource player)
        {
            Transform parent = hudRoot;
            if (parent == null)
            {
                var canvas = UiFactory.CreateCanvas("MinimapCanvas", 11);
                parent = canvas.transform;
            }

            var root = UiFactory.CreateRect("Minimap", parent);
            var view = root.gameObject.AddComponent<MinimapView>();
            view.Build(player);
            return view;
        }

        /// <summary>Mini haritanın kök dönüşümü (HUD yerleşimini değiştirmek için).</summary>
        public RectTransform Root => (RectTransform)transform;

        /// <summary>Yerel oyuncu kaynağı (yeniden doğma / izleyici için değiştirilebilir).</summary>
        public IPlayerHudSource Player
        {
            get => _data?.Player;
            set
            {
                if (_data != null)
                    _data.Player = value;
            }
        }

        /// <summary>Yaya iken görünen alanın kenar boyu (m). 80..1000 aralığına sınırlanır.</summary>
        public float ViewDistance { get; set; } = DefaultViewMeters;

        /// <summary>Araçta/intikalde otomatik uzaklaşma açık mı?</summary>
        public bool AutoZoomWhenMounted { get; set; } = true;

        /// <summary>Mini haritayı gösterir/gizler (bileşenler çalışmaya devam eder).</summary>
        public void SetVisible(bool visible)
        {
            if (_group == null)
                _group = UiFactory.EnsureCanvasGroup(this);
            UiFactory.SetVisible(this, visible);
        }

        /// <summary>Mini harita görünür mü?</summary>
        public bool IsVisible => _group == null || _group.alpha > 0.01f;

        private void Build(IPlayerHudSource player)
        {
            _data = new MapDataSource(player);

            var root = Root;
            UiFactory.Anchor(root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-EdgeMargin, EdgeMargin),
                new Vector2(DefaultSize, DefaultSize + LabelHeight));
            _group = UiFactory.EnsureCanvasGroup(root);
            _group.interactable = false;
            _group.blocksRaycasts = false;

            // Çerçeve + kırpılmış görüntü alanı.
            var frame = UiFactory.Panel(root, UiTheme.PanelBorder, UiSprites.ChamferRect);
            frame.gameObject.name = "Frame";
            UiFactory.SetRect(frame, Vector2.zero, Vector2.one, new Vector2(0f, LabelHeight), Vector2.zero);
            frame.GetComponent<Image>().raycastTarget = false;

            _viewport = UiFactory.CreateRect("Viewport", frame);
            UiFactory.Stretch(_viewport, FrameThickness);
            var viewportImage = _viewport.gameObject.AddComponent<Image>();
            viewportImage.color = OffMapColor;
            viewportImage.raycastTarget = false;
            _viewport.gameObject.AddComponent<RectMask2D>();
            _viewportSize = DefaultSize - FrameThickness * 2f;

            // Hareketli harita içeriği (pivot sol-alt; çocuklar harita UV'sine çapalıdır).
            _content = UiFactory.CreateRect("Content", _viewport);
            _content.anchorMin = new Vector2(0.5f, 0.5f);
            _content.anchorMax = new Vector2(0.5f, 0.5f);
            _content.pivot = Vector2.zero;
            _content.sizeDelta = new Vector2(1024f, 1024f);

            _mapImage = UiFactory.RawImage(_content, null);
            _mapImage.gameObject.name = "MapTexture";
            UiFactory.Stretch(_mapImage);
            _mapImage.color = FallbackMapColor;

            BuildGrid(_content);

            _zoneTint = MapRingGraphic.Create(_content, "ZoneTint", ZoneTintColor);
            _nextZone = MapRingGraphic.Create(_content, "NextZone", UiTheme.SafeZoneWhite);
            _zoneEdge = MapRingGraphic.Create(_content, "ZoneEdge", ZoneEdgeColor);
            _zoneTint.enabled = false;
            _nextZone.enabled = false;
            _zoneEdge.enabled = false;

            _routeLine = MapLineGraphic.Create(_content, "Route", RouteColor, 2f);
            _routeLine.SetDash(7f, 5f);

            // Sabit işaret katmanı (merkez = oyuncu).
            _markers = UiFactory.CreateRect("Markers", _viewport);

            _safeLine = MapLineGraphic.Create(_markers, "SafeZoneLine", SafeLineColor, 1.6f);
            _safeLine.SetDash(5f, 4f);

            _artilleryRing = MapRingGraphic.Create(_markers, "ArtilleryRing", UiTheme.WithAlpha(ArtilleryColor, 0.85f));
            _artilleryRing.DashCount = 12;
            _artilleryRing.enabled = false;
            _artilleryCross = MapIcons.Cross(_markers, "Artillery", 12f, ArtilleryColor, out _);
            _artilleryCross.gameObject.SetActive(false);

            _attackMarker = MapIcons.Target(_markers, "AttackTarget", 16f, AttackColor, out _);
            _attackMarker.gameObject.SetActive(false);

            _landingZone = MapIcons.Diamond(_markers, "LandingZone", 14f, LandingZoneColor, out _);
            MapIcons.Badge(_landingZone, "Label", "LZ", 11, LandingZoneColor, new Vector2(0f, -14f), 40f);
            _landingZone.gameObject.SetActive(false);

            _transportMarker = MapIcons.Diamond(_markers, "Transport", 10f, RouteColor, out _);
            _transportMarker.gameObject.SetActive(false);

            _waypoint = MapIcons.Pin(_markers, "Waypoint", 16f, WaypointColor, out _);
            _waypoint.gameObject.SetActive(false);

            _playerArrow = MapIcons.Arrow(_markers, "Player", 15f, PlayerColor, out _playerArrowFill);

            // Kuzey rozeti.
            var north = UiFactory.Panel(frame, UiTheme.PanelDark, UiSprites.ChamferRect);
            north.gameObject.name = "North";
            north.GetComponent<Image>().raycastTarget = false;
            UiFactory.Anchor(north, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(26f, 20f));
            var northLabel = UiFactory.Label(north, "K", UiTheme.FontTiny, TextAnchor.MiddleCenter, UiTheme.Amber, FontStyle.Bold);
            northLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Alt şerit: pafta + bölge adı, sağda mesafe bilgisi.
            var strip = UiFactory.Panel(root, UiTheme.PanelDark, UiSprites.ChamferRect);
            strip.gameObject.name = "LabelStrip";
            strip.GetComponent<Image>().raycastTarget = false;
            UiFactory.SetRect(strip, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, LabelHeight - 4f));

            _gridLabel = UiFactory.Label(strip, string.Empty, UiTheme.FontTiny + 1, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold);
            _gridLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Stretch(_gridLabel, 8f, 0f, 8f, 0f);
            UiFactory.AddShadow(_gridLabel, UiTheme.TextShadow, new Vector2(1f, -1f));

            _infoLabel = UiFactory.Label(strip, string.Empty, UiTheme.FontTiny, TextAnchor.MiddleRight, UiTheme.TextDim, FontStyle.Bold);
            _infoLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Stretch(_infoLabel, 8f, 0f, 8f, 0f);
            UiFactory.AddShadow(_infoLabel, UiTheme.TextShadow, new Vector2(1f, -1f));

            _built = true;
            _viewMeters = Mathf.Clamp(ViewDistance, 80f, 1000f);
            Refresh(true);
        }

        private void BuildGrid(RectTransform content)
        {
            for (var i = 0; i <= MapMath.GridDivisions; i++)
            {
                var t = i / (float)MapMath.GridDivisions;
                var thickness = i == 0 || i == MapMath.GridDivisions ? 2f : 1.2f;

                var vertical = UiFactory.Image(content, UiSprites.White, GridColor);
                vertical.gameObject.name = "GridV" + i;
                var vrt = vertical.rectTransform;
                vrt.anchorMin = new Vector2(t, 0f);
                vrt.anchorMax = new Vector2(t, 1f);
                vrt.pivot = new Vector2(0.5f, 0.5f);
                vrt.sizeDelta = new Vector2(thickness, 0f);
                vrt.anchoredPosition = Vector2.zero;

                var horizontal = UiFactory.Image(content, UiSprites.White, GridColor);
                horizontal.gameObject.name = "GridH" + i;
                var hrt = horizontal.rectTransform;
                hrt.anchorMin = new Vector2(0f, t);
                hrt.anchorMax = new Vector2(1f, t);
                hrt.pivot = new Vector2(0.5f, 0.5f);
                hrt.sizeDelta = new Vector2(0f, thickness);
                hrt.anchoredPosition = Vector2.zero;
            }
        }

        private void LateUpdate()
        {
            if (!_built)
                return;
            Refresh(false);
        }

        private void Refresh(bool force)
        {
            _data.Refresh();
            var frame = _data.Frame;

            ApplyTexture();

            // Yakınlaştırma (araçta uzaklaşır).
            var target = Mathf.Clamp(ViewDistance, 80f, 1000f);
            if (AutoZoomWhenMounted && _data.IsPlayerMounted)
                target = Mathf.Max(target, MountedViewMeters);
            _viewMeters = force ? target : Mathf.MoveTowards(_viewMeters, target, ZoomSpeedMetersPerSecond * Time.unscaledDeltaTime);

            var ppm = _viewportSize / Mathf.Max(1f, _viewMeters);
            var mapPixels = frame.Size * ppm;
            if (Mathf.Abs(mapPixels - _mapPixels) > 0.01f)
            {
                _mapPixels = mapPixels;
                _content.sizeDelta = new Vector2(mapPixels, mapPixels);
            }

            var playerPos = _data.PlayerPosition;
            var playerUv = frame.WorldToUV(playerPos);
            _content.anchoredPosition = -playerUv * mapPixels;

            var halfExtents = new Vector2(_viewportSize * 0.5f - MarkerEdgePadding, _viewportSize * 0.5f - MarkerEdgePadding);

            UpdateZones(frame, playerPos, ppm);
            UpdateInsertion(frame, playerPos, ppm, mapPixels, halfExtents);
            UpdateArtilleryAndOrders(playerPos, ppm, halfExtents);
            UpdateWaypoint(playerPos, ppm, halfExtents);
            UpdateAllies(playerPos, ppm, halfExtents);
            UpdatePlayerArrow();

            if (force || Time.unscaledTime >= _nextLabelRefresh)
            {
                _nextLabelRefresh = Time.unscaledTime + LabelRefreshInterval;
                UpdateLabels(frame, playerPos);
            }
        }

        private void ApplyTexture()
        {
            var world = _data.World;
            var texture = world != null ? world.MinimapTexture : null;
            if (_textureApplied && texture == _appliedTexture)
                return;

            _textureApplied = true;
            _appliedTexture = texture;
            _mapImage.texture = texture;
            _mapImage.color = texture != null ? Color.white : FallbackMapColor;
        }

        private void UpdateZones(MapFrame frame, Vector3 playerPos, float ppm)
        {
            if (!_data.TryGetZones(out var current, out var hasNext, out var next))
            {
                SetEnabled(_zoneTint, false);
                SetEnabled(_zoneEdge, false);
                SetEnabled(_nextZone, false);
                _safeLine.ClearPoints();
                return;
            }

            var currentUv = frame.WorldToUV(current.CenterX, current.CenterZ);
            SetAnchor(_zoneEdge.rectTransform, currentUv, ref _zoneAnchor);
            _zoneTint.rectTransform.anchorMin = _zoneEdge.rectTransform.anchorMin;
            _zoneTint.rectTransform.anchorMax = _zoneEdge.rectTransform.anchorMax;

            var radiusPx = current.Radius * ppm;
            SetEnabled(_zoneEdge, true);
            _zoneEdge.SetStroke(radiusPx, 2.5f);

            // Bölge dışı tonlama: görüntü alanının bölge dışında kalan kısmını kaplayacak kadar geniş halka.
            var dx = playerPos.x - current.CenterX;
            var dz = playerPos.z - current.CenterZ;
            var distancePx = Mathf.Sqrt(dx * dx + dz * dz) * ppm;
            var halfDiagonal = _viewportSize * 0.7072f;
            var needsTint = distancePx + halfDiagonal > radiusPx;
            SetEnabled(_zoneTint, needsTint);
            if (needsTint)
                _zoneTint.SetRing(radiusPx, Mathf.Max(radiusPx + 1f, distancePx + halfDiagonal + 6f));

            if (hasNext)
            {
                var nextUv = frame.WorldToUV(next.CenterX, next.CenterZ);
                SetAnchor(_nextZone.rectTransform, nextUv, ref _nextZoneAnchor);
                SetEnabled(_nextZone, true);
                _nextZone.SetStroke(next.Radius * ppm, 2f);
            }
            else
            {
                SetEnabled(_nextZone, false);
            }

            // Güvenli bölgeye yön çizgisi (oyuncu hedef çemberin dışındaysa).
            var safe = hasNext ? next : current;
            var sx = playerPos.x - safe.CenterX;
            var sz = playerPos.z - safe.CenterZ;
            var distance = Mathf.Sqrt(sx * sx + sz * sz);
            if (distance > safe.Radius + 1f && !_data.IsPlayerDead)
            {
                var toEdge = (distance - safe.Radius) / distance;
                var end = new Vector2(-sx * toEdge, -sz * toEdge) * ppm;
                var center = new Vector2(_viewportSize * 0.5f, _viewportSize * 0.5f);
                _safeLine.SetSegment(center, center + end);
            }
            else
            {
                _safeLine.ClearPoints();
            }
        }

        private void UpdateInsertion(MapFrame frame, Vector3 playerPos, float ppm, float mapPixels, Vector2 halfExtents)
        {
            if (!_data.TryGetInsertion(out var start, out var landingZone, out _, out var transport))
            {
                SetActive(_landingZone, false);
                SetActive(_transportMarker, false);
                _routeLine.ClearPoints();
                return;
            }

            var startPx = frame.WorldToUV(start) * mapPixels;
            var lzPx = frame.WorldToUV(landingZone) * mapPixels;
            _routeLine.SetSegment(startPx, lzPx);

            SetActive(_landingZone, true);
            _landingZone.anchoredPosition = MapMath.ClampToRect(Offset(landingZone, playerPos, ppm), halfExtents, out _);

            if (transport != null && !_data.IsPlayerMounted)
            {
                var offset = Offset(transport.transform.position, playerPos, ppm);
                var inside = Mathf.Abs(offset.x) <= halfExtents.x && Mathf.Abs(offset.y) <= halfExtents.y;
                SetActive(_transportMarker, inside);
                if (inside)
                    _transportMarker.anchoredPosition = offset;
            }
            else
            {
                SetActive(_transportMarker, false);
            }
        }

        private void UpdateArtilleryAndOrders(Vector3 playerPos, float ppm, Vector2 halfExtents)
        {
            if (_data.TryGetArtillery(out var target, out _))
            {
                var offset = Offset(target, playerPos, ppm);
                SetActive(_artilleryCross, true);
                _artilleryCross.anchoredPosition = MapMath.ClampToRect(offset, halfExtents, out var clamped);
                var pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
                SetEnabled(_artilleryRing, !clamped);
                if (!clamped)
                {
                    _artilleryRing.rectTransform.anchoredPosition = offset;
                    _artilleryRing.SetStroke((ArtilleryService.SpreadRadius + ArtilleryService.ShellRadius) * ppm, 2f);
                    _artilleryRing.color = UiTheme.WithAlpha(ArtilleryColor, pulse);
                }
            }
            else
            {
                SetActive(_artilleryCross, false);
                SetEnabled(_artilleryRing, false);
            }

            if (_data.TryGetSquadOrder(out var order, out var orderTarget) && order == SquadOrder.Attack && MapMath.IsFinite(orderTarget))
            {
                SetActive(_attackMarker, true);
                _attackMarker.anchoredPosition = MapMath.ClampToRect(Offset(orderTarget, playerPos, ppm), halfExtents, out _);
            }
            else
            {
                SetActive(_attackMarker, false);
            }
        }

        private void UpdateWaypoint(Vector3 playerPos, float ppm, Vector2 halfExtents)
        {
            if (!MapMarkers.HasWaypoint)
            {
                SetActive(_waypoint, false);
                return;
            }

            SetActive(_waypoint, true);
            _waypoint.anchoredPosition = MapMath.ClampToRect(Offset(MapMarkers.Waypoint, playerPos, ppm), halfExtents, out _);
        }

        private void UpdateAllies(Vector3 playerPos, float ppm, Vector2 halfExtents)
        {
            var used = 0;
            var self = _data.Self;
            if (self != null)
            {
                var team = self.Team;
                var all = CombatantRegistry.All;
                for (var i = 0; i < all.Count; i++)
                {
                    var c = all[i];
                    if (c == null || c == self || c.Team != team || !c.IsAlive)
                        continue;

                    var dot = GetAllyDot(used++);
                    var offset = MapMath.ClampToRect(Offset(c.transform.position, playerPos, ppm), halfExtents, out var clamped);
                    dot.Root.anchoredPosition = offset;
                    if (!dot.Visible)
                    {
                        dot.Visible = true;
                        dot.Root.gameObject.SetActive(true);
                    }

                    if (dot.Clamped != clamped)
                    {
                        dot.Clamped = clamped;
                        dot.Root.localScale = clamped ? new Vector3(0.75f, 0.75f, 1f) : Vector3.one;
                        dot.Fill.color = clamped ? UiTheme.WithAlpha(UiTheme.AllyBlue, 0.75f) : UiTheme.AllyBlue;
                    }
                }
            }

            for (var i = used; i < _allies.Count; i++)
            {
                var dot = _allies[i];
                if (!dot.Visible)
                    continue;
                dot.Visible = false;
                dot.Root.gameObject.SetActive(false);
            }

            // Oyuncu oku her zaman en üstte kalsın.
            if (used > 0 && _playerArrow.GetSiblingIndex() != _markers.childCount - 1)
                _playerArrow.SetAsLastSibling();
        }

        private AllyDot GetAllyDot(int index)
        {
            while (_allies.Count <= index)
            {
                var root = MapIcons.Dot(_markers, "Ally" + _allies.Count, 9f, UiTheme.AllyBlue, out var fill);
                root.gameObject.SetActive(false);
                _allies.Add(new AllyDot { Root = root, Fill = fill });
            }

            return _allies[index];
        }

        private void UpdatePlayerArrow()
        {
            var yaw = _data.PlayerYaw;
            if (float.IsNaN(_lastYaw) || Mathf.Abs(Mathf.DeltaAngle(yaw, _lastYaw)) > 0.2f)
            {
                _lastYaw = yaw;
                _playerArrow.localRotation = Quaternion.Euler(0f, 0f, MapMath.YawToUiAngle(yaw));
            }

            var dead = _data.IsPlayerDead || !_data.HasPlayer;
            if (dead != _lastDead || _playerArrowFill.color == default)
            {
                _lastDead = dead;
                _playerArrowFill.color = dead ? DeadPlayerColor : PlayerColor;
            }
        }

        private void UpdateLabels(MapFrame frame, Vector3 playerPos)
        {
            var grid = MapMath.GridLabel(frame, playerPos);
            var world = _data.World;
            var location = world != null ? world.GetLocationName(playerPos) : string.Empty;
            if (!ReferenceEquals(grid, _shownGrid) || !string.Equals(location, _shownLocation))
            {
                _shownGrid = grid;
                _shownLocation = location;
                _gridLabel.text = string.IsNullOrEmpty(location)
                    ? grid
                    : grid + "  <color=" + UiTheme.ToHex(UiTheme.Khaki) + ">" + location + "</color>";
            }

            // Sağ bilgi: bölge dışında → güvenli bölgeye mesafe (mavi); işaret varsa → işarete mesafe (kehribar).
            var kind = 0;
            var meters = 0;
            if (_data.TryGetZones(out var current, out var hasNext, out var next) && !_data.IsPlayerDead)
            {
                var safe = hasNext ? next : current;
                var dx = playerPos.x - safe.CenterX;
                var dz = playerPos.z - safe.CenterZ;
                var outside = Mathf.Sqrt(dx * dx + dz * dz) - safe.Radius;
                if (outside > 1f)
                {
                    kind = 1;
                    meters = Mathf.RoundToInt(outside / 5f) * 5;
                }
            }

            if (kind == 0 && MapMarkers.HasWaypoint)
            {
                kind = 2;
                meters = Mathf.RoundToInt(MapMath.DistanceXZ(playerPos, MapMarkers.Waypoint) / 5f) * 5;
            }

            if (kind == _shownInfoKind && meters == _shownInfoMeters)
                return;

            _shownInfoKind = kind;
            _shownInfoMeters = meters;
            switch (kind)
            {
                case 1:
                    _infoLabel.color = UiTheme.AllyBlue;
                    _infoLabel.text = "BÖLGEYE " + MapMath.FormatDistance(meters);
                    break;
                case 2:
                    _infoLabel.color = UiTheme.Amber;
                    _infoLabel.text = "İŞARET " + MapMath.FormatDistance(meters);
                    break;
                default:
                    _infoLabel.text = string.Empty;
                    break;
            }
        }

        private static Vector2 Offset(Vector3 world, Vector3 playerPos, float ppm)
        {
            return new Vector2((world.x - playerPos.x) * ppm, (world.z - playerPos.z) * ppm);
        }

        private static void SetAnchor(RectTransform rt, Vector2 uv, ref Vector2 cached)
        {
            if ((uv - cached).sqrMagnitude < 1e-10f)
                return;
            cached = uv;
            rt.anchorMin = uv;
            rt.anchorMax = uv;
            rt.anchoredPosition = Vector2.zero;
        }

        private static void SetEnabled(Behaviour behaviour, bool enabled)
        {
            if (behaviour != null && behaviour.enabled != enabled)
                behaviour.enabled = enabled;
        }

        private static void SetActive(Component component, bool active)
        {
            if (component != null && component.gameObject.activeSelf != active)
                component.gameObject.SetActive(active);
        }
    }
}
