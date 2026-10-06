using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure;
using Project.Infrastructure.AI;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Vehicles;
using Project.Infrastructure.World;
using Project.Presentation.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Project.Infrastructure.Localization;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Tam ekran harekât haritası (M). Kuzey yukarıda; pafta çizgileri ve kenar cetvelleri (A-J / 1-10, ~100 m),
    /// <see cref="WorldMetadata.MinimapTexture"/> (yoksa gri zemin), lokasyon adları, harekât alanı (mavi kenar + dışı
    /// tonlu), sonraki güvenli bölge (beyaz), tim arkadaşları (mavi noktalar; yakınlaşınca adlarıyla), oyuncu oku,
    /// intikal rotası + iniş bölgesi (LZ) + intikal aracı, topçu hedefi, taarruz emri hedefi ve harita işareti.
    /// Sağda durum/lejant/kontrol paneli. Sol tık işaret koyar (<see cref="PointMarked"/> → pres-player tim taarruz
    /// emri / topçu hedefi), sağ tık işareti kaldırır, tekerlek imleç etrafında yakınlaştırır, sürükleme kaydırır,
    /// Boşluk oyuncuya ortalar. Shift+sol tık tehlike (düşman) işareti koyar, sağ sürükleme mesafe/kerteriz ölçer,
    /// WASD/oklar kaydırır. Askeri karelaj koordinatı (36S KD 051 087), alt pafta çizgileri, görüş konili tim simgeleri
    /// (rütbeli), araçlar, düşen tim arkadaşları ve sonraki bölge sayacı gösterilir. Açıkken imleç serbesttir ve oyun girdisi kapalıdır (kapanınca önceki durum geri gelir).
    /// Kapalıyken hiçbir iş yapmaz; açıkken kare başına tahsis yapmaz (yazılar yalnızca değişince güncellenir).
    /// </summary>
    public sealed class FullMapView : MonoBehaviour
    {
        /// <summary>En fazla yakınlaştırma (1 = haritanın tamamı).</summary>
        public const float MaxZoom = 6f;

        /// <summary>Harita görüntü alanının kenarı (px, 1920×1080 referans).</summary>
        public const float ViewportSize = 880f;

        private const float RulerSize = 24f;
        private const float BodyWidth = 1344f;
        private const float BodyHeight = 1010f;
        private const float HeaderHeight = 56f;
        private const float MapTop = 66f;
        private const float MapFrameSize = ViewportSize + RulerSize * 2f;
        private const float LegendLeft = MapFrameSize + 24f;
        private const float LegendWidth = BodyWidth - LegendLeft;
        private const float ZoomStep = 1.25f;
        private const float AllyNameZoom = 2.5f;
        private const float TextRefreshInterval = 0.2f;
        private const float SubGridZoom = 2f;
        private const int SubGridDivisions = 5;
        private const float AllyConeMeters = 30f;
        private const float AllyConeAngle = 70f;
        private const float PlayerConeMeters = 45f;
        private const float KeyboardPanSpeed = 0.6f;

        private static readonly Color OffMapColor = UiTheme.Hex(0x1A, 0x1F, 0x14);
        private static readonly Color FallbackMapColor = new Color(0.36f, 0.38f, 0.33f, 1f);
        private static readonly Color GridColor = new Color(0f, 0f, 0f, 0.32f);
        private static readonly Color SubGridColor = new Color(0f, 0f, 0f, 0.12f);
        private static readonly Color DangerColor = UiTheme.EnemyRed;
        private static readonly Color VehicleColor = UiTheme.Hex(0xB5, 0xB0, 0x8A);
        private static readonly Color FallenColor = UiTheme.Hex(0xC9, 0x6A, 0x5E);
        private static readonly Color MeasureColor = UiTheme.Text;
        private static readonly Color ZoneEdgeColor = UiTheme.WithAlpha(UiTheme.ZoneBlue, 1f);
        private static readonly Color ZoneTintColor = UiTheme.WithAlpha(UiTheme.ZoneBlue, 0.24f);
        private static readonly Color RouteColor = UiTheme.WithAlpha(UiTheme.Amber, 0.9f);
        private static readonly Color LandingZoneColor = UiTheme.Success;
        private static readonly Color WaypointColor = UiTheme.Amber;
        private static Color ArtilleryColor => UiTheme.EnemyRed;
        private static readonly Color AttackColor = UiTheme.Accent;
        private static readonly Color PlayerColor = UiTheme.Amber;
        private static readonly Color LootColor = UiTheme.Hex(0xE0, 0xB4, 0x4A);
        private static readonly Color DeadPlayerColor = UiTheme.TextMuted;
        private static readonly Color LocationMajorColor = UiTheme.Text;
        private static readonly Color LocationMinorColor = UiTheme.Khaki;
        private static readonly int[] ScaleBarSteps = { 25, 50, 100, 200, 250, 500 };

        private sealed class AllyMarker
        {
            public RectTransform Root;
            public Image Fill;
            public Text Name;
            public Combatant Bound;
            public bool Visible;
            public int NameMode = -1;
            public int Downed = -1;
            public MapConeGraphic Cone;
            public float ConeYaw = float.NaN;
            public float ConePpm = -1f;
            public Vector2 Anchor = new Vector2(-1f, -1f);
        }

        private sealed class VehicleMarker
        {
            public RectTransform Root;
            public RectTransform Body;
            public Image Fill;
            public Text Label;
            public DrivableVehicle Bound;
            public int Occupied = -1;
            public bool Visible;
            public Vector2 Anchor = new Vector2(-1f, -1f);
            public float Yaw = float.NaN;
        }

        private sealed class FallenIcon
        {
            public RectTransform Root;
            public Text Label;
            public int BoundIndex = -1;
            public int BoundSerial = -1;
            public bool Visible;
        }

        private MapDataSource _data;
        private IPlayerHudSource _playerSource;
        private RectTransform _panel;
        private Canvas _rootCanvas;

        // Harita
        private RectTransform _viewport;
        private RectTransform _content;
        private RawImage _mapImage;
        private MapClickSurface _surface;
        private MapRingGraphic _zoneTint;
        private MapRingGraphic _zoneEdge;
        private MapRingGraphic _nextZone;
        private MapRingGraphic _artilleryRing;
        private MapLineGraphic _routeLine;
        private RectTransform _locationLayer;
        private RectTransform _iconLayer;
        private RectTransform _playerArrow;
        private Image _playerArrowFill;
        private RectTransform _landingZone;
        private RectTransform _transportMarker;
        private Text _transportLabel;
        private RectTransform _waypoint;
        private Text _waypointLabel;
        private RectTransform _artilleryCross;
        private Text _artilleryLabel;
        private RectTransform _attackMarker;
        private readonly List<AllyMarker> _allies = new List<AllyMarker>(12);
        private readonly List<VehicleMarker> _vehicles = new List<VehicleMarker>(8);
        private readonly List<FallenIcon> _fallen = new List<FallenIcon>(MapMarkers.MaxFallen);
        private RectTransform _subGridRoot;
        private bool _subGridShown;
        private MapConeGraphic _playerCone;
        private float _playerConePpm = -1f;
        private Text _nextZoneLabel;
        private int _shownNextZoneKey = int.MinValue;
        private RectTransform _dangerPing;
        private MapRingGraphic _dangerRing;
        private Text _dangerLabel;
        private MapLineGraphic _measureLine;
        private RectTransform _measureA;
        private RectTransform _measureB;
        private Text _measureLabel;
        private bool _measureActive;
        private bool _measuring;
        private Vector2 _measureStartUv;
        private Vector2 _measureEndUv;
        private int _shownMeasureKey = int.MinValue;
        private Text _coordValue;
        private int _shownCoordKey = int.MinValue;
        private int _shownCursorEasting = -1;
        private int _shownCursorNorthing = -1;
        private readonly Text[] _columnTop = new Text[MapMath.GridDivisions];
        private readonly Text[] _columnBottom = new Text[MapMath.GridDivisions];
        private readonly Text[] _rowLeft = new Text[MapMath.GridDivisions];
        private readonly Text[] _rowRight = new Text[MapMath.GridDivisions];
        private RectTransform _scaleBar;
        private Text _scaleLabel;
        private Text _cursorLabel;
        private Text _zoomLabel;
        private Text _noMapLabel;
        private RectTransform _legendOverlay;
        private bool _legendShown = true;
        private RectTransform _cursorTag;
        private Text _cursorTagText;
        private Vector2 _lastPointer = new Vector2(float.NaN, float.NaN);
        private string _shownTagGrid;
        private RectTransform _radial;
        private readonly Image[] _radialSlices = new Image[4];
        private bool _radialOpen;
        private int _radialSel = -1;
        private Vector2 _radialCenterScreen;
        private Vector2 _radialUv;
        private Vector2 _lootUv;
        private RectTransform _lootMarker;
        private bool _hasLoot;
        private Vector2 _lootAnchor = new Vector2(-1f, -1f);
        private Vector2 _nextZoneLabelPos = new Vector2(float.NaN, float.NaN);
        private int _shownZoomPercent = -1;
        private const float RadialDeadZone = 26f;
        private static readonly string[] RadialNames = { "GİDİYORUM", "DÜŞMAN", "YAĞMA", "TEHLİKE" };
        private static readonly Vector2[] RadialOffsets = { new Vector2(0f, 58f), new Vector2(84f, 0f), new Vector2(0f, -58f), new Vector2(-84f, 0f) };

        // Başlık / panel
        private Text _zoneHeader;
        private Text _gridValue;
        private Text _locationValue;
        private Text _zoneValue;
        private Text _safeValue;
        private Text _teamValue;
        private Text _orderValue;
        private Text _artilleryValue;
        private Text _markerValue;

        // Görünüm durumu
        private float _zoom = 1f;
        private Vector2 _focus = new Vector2(0.5f, 0.5f);
        private bool _viewDirty = true;
        private float _mapPixels = ViewportSize;
        private Texture _appliedTexture;
        private bool _textureApplied;
        private WorldMetadata _locationsWorld;
        private int _locationsCount = -1;
        private float _lastYaw = float.NaN;
        private int _lastDead = -1;
        private Vector2 _zoneAnchor = new Vector2(-1f, -1f);
        private Vector2 _nextZoneAnchor = new Vector2(-1f, -1f);
        private Vector2 _playerAnchor = new Vector2(-1f, -1f);
        private float _nextTextRefresh;
        private string _shownCursorGrid;
        private int _shownCursorMeters = -1;
        private bool _cursorShown;
        private int _shownZoneKey = int.MinValue;
        private int _shownSafeKey = int.MinValue;
        private int _shownTeamKey = int.MinValue;
        private int _shownOrderKey = int.MinValue;
        private int _shownArtilleryKey = int.MinValue;
        private int _shownArtilleryLabelKey = int.MinValue;
        private int _shownMarkerKey = int.MinValue;
        private int _shownWaypointMeters = -1;
        private string _shownGrid;
        private string _shownLocation;
        private float _waypointPulse;
        private bool _built;

        /// <summary>Haritayı tuvale ekler (kapalı başlar). <paramref name="canvasRoot"/> null ise kendi tuvalini oluşturur.</summary>
        public static FullMapView Create(Transform canvasRoot, IPlayerHudSource player)
        {
            var parent = canvasRoot;
            if (parent == null)
            {
                var canvas = UiFactory.CreateCanvas("FullMapCanvas", 20);
                parent = canvas.transform;
            }

            var root = UiFactory.CreateRect("FullMap", parent);
            var view = root.gameObject.AddComponent<FullMapView>();
            view.Build(player);
            return view;
        }

        /// <summary>Harita açık mı?</summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// Oyuncu haritaya sol tıkladığında (dünya konumu; Y = zemin yüksekliği). pres-player bu noktayı tim taarruz
        /// emri / topçu hedefi olarak kullanır. Nokta ayrıca <see cref="MapMarkers.Waypoint"/> olarak saklanır.
        /// </summary>
        public event Action<Vector3> PointMarked;

        /// <summary>Oyuncu işareti sağ tıkla kaldırdığında.</summary>
        public event Action MarkerCleared;

        /// <summary>Yerel oyuncu kaynağı (yeniden doğma / izleyici için değiştirilebilir).</summary>
        public IPlayerHudSource Player
        {
            get => _data?.Player;
            set
            {
                _playerSource = value;
                if (_data != null)
                    _data.Player = value;
            }
        }

        /// <summary>Geçerli yakınlaştırma (1 = tüm harita).</summary>
        public float Zoom => _zoom;

        /// <summary>Haritayı açar/kapatır.</summary>
        public void Toggle()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }

        /// <summary>Haritayı açar (imleç serbest, oyun girdisi kapalı).</summary>
        public void Open()
        {
            if (IsOpen || !_built)
                return;

            IsOpen = true;
            _panel.gameObject.SetActive(true);
            MapOverlayInput.Acquire(this, _data.Player ?? _playerSource);
            UiWidgets.PlaySound(SoundId.UiClick);

            // Yakınken açılırsa oyuncuya ortala; tam görünümde haritanın tamamı.
            _data.Refresh();
            if (_zoom > 1.01f)
                CenterOn(_data.Frame.WorldToUV(_data.PlayerPosition));

            _viewDirty = true;
            _nextTextRefresh = 0f;
            _shownZoneKey = _shownSafeKey = _shownTeamKey = _shownOrderKey = _shownArtilleryKey = _shownMarkerKey = int.MinValue;
            _shownArtilleryLabelKey = int.MinValue;
            Refresh(true);
        }

        /// <summary>Haritayı kapatır ve önceki imleç/girdi durumunu geri yükler.</summary>
        public void Close()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            _measuring = false;
            CloseRadial();
            if (_panel != null)
                _panel.gameObject.SetActive(false);
            MapOverlayInput.Release(this);
            UiFactory.ClearSelection();
        }

        /// <summary>Görünümü dünya konumuna ortalar.</summary>
        public void CenterOnWorld(Vector3 world)
        {
            if (_data == null)
                return;
            CenterOn(_data.Frame.WorldToUV(world));
        }

        // ================================================================== Kurulum

        private void Build(IPlayerHudSource player)
        {
            _playerSource = player;
            _data = new MapDataSource(player);
            MapMarkers.StartTrackingFallen();

            var root = (RectTransform)transform;
            UiFactory.Stretch(root);

            _panel = UiFactory.CreateRect("Panel", root);
            var backdrop = _panel.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.03f, 0.04f, 0.02f, 0.88f);
            backdrop.raycastTarget = true;

            var body = UiFactory.CreateRect("Body", _panel);
            UiFactory.Anchor(body, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BodyWidth, BodyHeight));

            BuildHeader(body);
            BuildMap(body);
            BuildLegend(body);

            _built = true;
            _panel.gameObject.SetActive(false);
            IsOpen = false;
        }

        private void BuildHeader(RectTransform body)
        {
            var header = UiFactory.Panel(body, UiTheme.PanelDark, UiSprites.ChamferRect);
            header.gameObject.name = "Header";
            UiFactory.SetRect(header, 0f, 0f, BodyWidth, HeaderHeight);

            var accent = UiFactory.Image(header, null, UiTheme.Accent);
            accent.gameObject.name = "Accent";
            UiFactory.SetRect(accent, new Vector2(0f, 0.18f), new Vector2(0f, 0.82f), new Vector2(10f, 0f), new Vector2(16f, 0f));

            var title = UiFactory.Label(header, Loc.Get("map.full_title", "HAREKÂT HARİTASI"), UiTheme.FontMedium, TextAnchor.MiddleLeft, UiTheme.TextHeader, FontStyle.Bold);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Stretch(title, 30f, 0f, 0f, 0f);
            UiFactory.AddShadow(title, UiTheme.TextShadow, new Vector2(1f, -1f));

            var region = UiFactory.Label(header, MapMath.ToUpperTurkish(WorldMetadata.DefaultRegionName),
                UiTheme.FontNormal, TextAnchor.MiddleCenter, UiTheme.Khaki, FontStyle.Bold);
            region.horizontalOverflow = HorizontalWrapMode.Overflow;

            _zoneHeader = UiFactory.Label(header, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleRight, UiTheme.Text, FontStyle.Bold);
            _zoneHeader.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Stretch(_zoneHeader, 0f, 0f, 20f, 0f);
            UiFactory.AddShadow(_zoneHeader, UiTheme.TextShadow, new Vector2(1f, -1f));
        }

        private void BuildMap(RectTransform body)
        {
            var frame = UiFactory.Panel(body, UiTheme.PanelDark, UiSprites.ChamferRect);
            frame.gameObject.name = "MapFrame";
            UiFactory.SetRect(frame, 0f, MapTop, MapFrameSize, MapFrameSize);

            // Kenar cetvelleri (pafta harfleri/rakamları görünüme göre kayar).
            var top = Ruler(frame, "RulerTop", RulerSize, 0f, ViewportSize, RulerSize);
            var bottom = Ruler(frame, "RulerBottom", RulerSize, RulerSize + ViewportSize, ViewportSize, RulerSize);
            var left = Ruler(frame, "RulerLeft", 0f, RulerSize, RulerSize, ViewportSize);
            var right = Ruler(frame, "RulerRight", RulerSize + ViewportSize, RulerSize, RulerSize, ViewportSize);
            for (var i = 0; i < MapMath.GridDivisions; i++)
            {
                _columnTop[i] = RulerLabel(top, MapMath.ColumnLabel(i));
                _columnBottom[i] = RulerLabel(bottom, MapMath.ColumnLabel(i));
                _rowLeft[i] = RulerLabel(left, MapMath.RowLabel(i));
                _rowRight[i] = RulerLabel(right, MapMath.RowLabel(i));
            }

            // Görüntü alanı (tıklama yüzeyi + kırpma).
            _viewport = UiFactory.CreateRect("Viewport", frame);
            UiFactory.SetRect(_viewport, RulerSize, RulerSize, ViewportSize, ViewportSize);
            var viewportImage = _viewport.gameObject.AddComponent<Image>();
            viewportImage.color = OffMapColor;
            viewportImage.raycastTarget = true;
            _viewport.gameObject.AddComponent<RectMask2D>();
            _surface = _viewport.gameObject.AddComponent<MapClickSurface>();
            _surface.Clicked = OnMapClicked;
            _surface.DragStarted = OnMapDragStarted;
            _surface.Dragged = OnMapDragged;
            _surface.DragEnded = OnMapDragEnded;
            _surface.Scrolled = OnMapScrolled;

            // Hareketli içerik (pivot sol-alt; çocuklar harita UV'sine çapalı).
            _content = UiFactory.CreateRect("Content", _viewport);
            _content.anchorMin = new Vector2(0.5f, 0.5f);
            _content.anchorMax = new Vector2(0.5f, 0.5f);
            _content.pivot = Vector2.zero;
            _content.sizeDelta = new Vector2(ViewportSize, ViewportSize);
            _content.anchoredPosition = new Vector2(-ViewportSize * 0.5f, -ViewportSize * 0.5f);

            _mapImage = UiFactory.RawImage(_content, null);
            _mapImage.gameObject.name = "MapTexture";
            UiFactory.Stretch(_mapImage);
            _mapImage.color = FallbackMapColor;

            BuildSubGrid(_content);
            BuildGrid(_content);

            _zoneTint = MapRingGraphic.Create(_content, "ZoneTint", ZoneTintColor);
            _nextZone = MapRingGraphic.Create(_content, "NextZone", UiTheme.SafeZoneWhite);
            _zoneEdge = MapRingGraphic.Create(_content, "ZoneEdge", ZoneEdgeColor);
            _zoneTint.enabled = false;
            _nextZone.enabled = false;
            _zoneEdge.enabled = false;

            _routeLine = MapLineGraphic.Create(_content, "Route", RouteColor, 3f);
            _routeLine.SetDash(12f, 7f);

            _measureLine = MapLineGraphic.Create(_content, "Measure", MeasureColor, 2.5f);

            _locationLayer = UiFactory.CreateRect("Locations", _content);

            _artilleryRing = MapRingGraphic.Create(_content, "ArtilleryRing", UiTheme.WithAlpha(ArtilleryColor, 0.85f));
            _artilleryRing.DashCount = 16;
            _artilleryRing.enabled = false;

            _iconLayer = UiFactory.CreateRect("Icons", _content);

            _landingZone = MapIcons.Diamond(_iconLayer, "LandingZone", 18f, LandingZoneColor, out _);
            MapIcons.Badge(_landingZone, "Label", "LZ", UiTheme.FontTiny, LandingZoneColor, new Vector2(0f, -17f), 60f);
            _landingZone.gameObject.SetActive(false);

            _transportMarker = MapIcons.Diamond(_iconLayer, "Transport", 13f, RouteColor, out _);
            _transportLabel = MapIcons.Badge(_transportMarker, "Label", "T-70", UiTheme.FontTiny - 2, RouteColor, new Vector2(0f, 15f), 80f);
            _transportMarker.gameObject.SetActive(false);

            _attackMarker = MapIcons.Target(_iconLayer, "AttackTarget", 22f, AttackColor, out _);
            MapIcons.Badge(_attackMarker, "Label", "TAARRUZ", UiTheme.FontTiny - 2, AttackColor, new Vector2(0f, -20f), 100f);
            _attackMarker.gameObject.SetActive(false);

            _artilleryCross = MapIcons.Cross(_iconLayer, "Artillery", 16f, ArtilleryColor, out _);
            _artilleryLabel = MapIcons.Badge(_artilleryCross, "Label", "TOPÇU", UiTheme.FontTiny - 2, ArtilleryColor, new Vector2(0f, -18f), 120f);
            _artilleryCross.gameObject.SetActive(false);

            _waypoint = MapIcons.Pin(_iconLayer, "Waypoint", 24f, WaypointColor, out _);
            _waypointLabel = MapIcons.Badge(_waypoint, "Label", string.Empty, UiTheme.FontTiny - 1, WaypointColor, new Vector2(0f, 36f), 90f);
            _waypoint.gameObject.SetActive(false);

            _nextZoneLabel = MapIcons.Badge(_iconLayer, "NextZoneLabel", string.Empty, UiTheme.FontTiny, UiTheme.SafeZoneWhite, Vector2.zero, 200f);
            _nextZoneLabel.enabled = false;

            _dangerRing = MapRingGraphic.Create(_iconLayer, "DangerRing", UiTheme.WithAlpha(DangerColor, 0.9f));
            _dangerRing.enabled = false;
            _dangerPing = MapIcons.Diamond(_iconLayer, "DangerPing", 20f, DangerColor, out _);
            _dangerLabel = MapIcons.Badge(_dangerPing, "Label", "TEHLİKE!", UiTheme.FontTiny - 1, DangerColor, new Vector2(0f, 22f), 100f);
            _dangerPing.gameObject.SetActive(false);

            _lootMarker = MapIcons.Diamond(_iconLayer, "LootPing", 16f, LootColor, out _);
            MapIcons.Badge(_lootMarker, "Label", "YAĞMA", UiTheme.FontTiny - 1, LootColor, new Vector2(0f, -18f), 80f);
            _lootMarker.gameObject.SetActive(false);

            _measureA = MapIcons.Dot(_iconLayer, "MeasureA", 9f, MeasureColor, out _);
            _measureB = MapIcons.Dot(_iconLayer, "MeasureB", 9f, MeasureColor, out _);
            _measureLabel = MapIcons.Badge(_iconLayer, "MeasureLabel", string.Empty, UiTheme.FontSmall - 1, MeasureColor, Vector2.zero, 220f);
            _measureA.gameObject.SetActive(false);
            _measureB.gameObject.SetActive(false);
            _measureLabel.gameObject.SetActive(false);

            _playerArrow = MapIcons.Arrow(_iconLayer, "Player", 20f, PlayerColor, out _playerArrowFill);
            _playerCone = MapConeGraphic.Create(_playerArrow, "ViewCone", UiTheme.WithAlpha(PlayerColor, 0.38f));
            _playerCone.transform.SetAsFirstSibling();

            // Sabit katman: ölçek çubuğu, imleç bilgisi, yakınlaştırma göstergesi.
            var overlay = UiFactory.CreateRect("Overlay", _viewport);

            var scaleRoot = UiFactory.CreateRect("ScaleBar", overlay);
            UiFactory.Anchor(scaleRoot, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(14f, 12f), new Vector2(200f, 26f));
            var scaleBack = UiFactory.Image(scaleRoot, null, MapIcons.OutlineColor);
            scaleBack.gameObject.name = "Back";
            _scaleBar = scaleBack.rectTransform;
            _scaleBar.anchorMin = Vector2.zero;
            _scaleBar.anchorMax = Vector2.zero;
            _scaleBar.pivot = Vector2.zero;
            _scaleBar.anchoredPosition = Vector2.zero;
            _scaleBar.sizeDelta = new Vector2(100f, 6f);
            var scaleFill = UiFactory.Image(_scaleBar, null, UiTheme.Text);
            scaleFill.gameObject.name = "Fill";
            UiFactory.Stretch(scaleFill, 1f);
            _scaleLabel = UiFactory.Label(scaleRoot, "100 m", UiTheme.FontTiny, TextAnchor.LowerLeft, UiTheme.Text, FontStyle.Bold);
            _scaleLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Stretch(_scaleLabel, 0f, 0f, 0f, 8f);
            UiFactory.AddOutline(_scaleLabel, MapIcons.OutlineColor, 1f);

            _cursorLabel = UiFactory.Label(overlay, string.Empty, UiTheme.FontSmall, TextAnchor.LowerRight, UiTheme.Text, FontStyle.Bold);
            _cursorLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Stretch(_cursorLabel, 0f, 0f, 14f, 10f);
            UiFactory.AddOutline(_cursorLabel, MapIcons.OutlineColor, 1.2f);

            _zoomLabel = UiFactory.Label(overlay, string.Empty, UiTheme.FontTiny, TextAnchor.UpperRight, UiTheme.TextDim, FontStyle.Bold);
            _zoomLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Stretch(_zoomLabel, 0f, 12f, 52f, 0f);
            UiFactory.AddOutline(_zoomLabel, MapIcons.OutlineColor, 1f);

            BuildNorthArrow(overlay);
            BuildLegendOverlay(overlay);
            BuildCursorTag(overlay);
            BuildRadial(overlay);

            _noMapLabel = UiFactory.Label(overlay, Loc.Get("map.loading", "Harita verisi yükleniyor…"), UiTheme.FontNormal, TextAnchor.MiddleCenter, UiTheme.TextDim, FontStyle.Bold);
            _noMapLabel.enabled = false;
        }

        private static RectTransform Ruler(RectTransform parent, string name, float x, float y, float width, float height)
        {
            var ruler = UiFactory.CreateRect(name, parent);
            UiFactory.SetRect(ruler, x, y, width, height);
            ruler.gameObject.AddComponent<RectMask2D>();
            return ruler;
        }

        private static Text RulerLabel(RectTransform ruler, string text)
        {
            var label = UiFactory.Label(ruler, text, UiTheme.FontTiny, TextAnchor.MiddleCenter, UiTheme.Khaki, FontStyle.Bold);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            var rt = label.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(RulerSize, RulerSize);
            return label;
        }

        private void BuildSubGrid(RectTransform content)
        {
            _subGridRoot = UiFactory.CreateRect("SubGrid", content);
            UiFactory.Stretch(_subGridRoot);
            var total = MapMath.GridDivisions * SubGridDivisions;
            for (var i = 1; i < total; i++)
            {
                if (i % SubGridDivisions == 0)
                    continue;

                var t = i / (float)total;
                var vertical = UiFactory.Image(_subGridRoot, null, SubGridColor);
                vertical.gameObject.name = "SubV" + i;
                vertical.raycastTarget = false;
                var vrt = vertical.rectTransform;
                vrt.anchorMin = new Vector2(t, 0f);
                vrt.anchorMax = new Vector2(t, 1f);
                vrt.pivot = new Vector2(0.5f, 0.5f);
                vrt.sizeDelta = new Vector2(0.8f, 0f);
                vrt.anchoredPosition = Vector2.zero;

                var horizontal = UiFactory.Image(_subGridRoot, null, SubGridColor);
                horizontal.gameObject.name = "SubH" + i;
                horizontal.raycastTarget = false;
                var hrt = horizontal.rectTransform;
                hrt.anchorMin = new Vector2(0f, t);
                hrt.anchorMax = new Vector2(1f, t);
                hrt.pivot = new Vector2(0.5f, 0.5f);
                hrt.sizeDelta = new Vector2(0f, 0.8f);
                hrt.anchoredPosition = Vector2.zero;
            }

            _subGridRoot.gameObject.SetActive(false);
        }

        private static void BuildGrid(RectTransform content)
        {
            for (var i = 0; i <= MapMath.GridDivisions; i++)
            {
                var t = i / (float)MapMath.GridDivisions;
                var thickness = i == 0 || i == MapMath.GridDivisions ? 2f : 1.2f;

                var vertical = UiFactory.Image(content, null, GridColor);
                vertical.gameObject.name = "GridV" + i;
                var vrt = vertical.rectTransform;
                vrt.anchorMin = new Vector2(t, 0f);
                vrt.anchorMax = new Vector2(t, 1f);
                vrt.pivot = new Vector2(0.5f, 0.5f);
                vrt.sizeDelta = new Vector2(thickness, 0f);
                vrt.anchoredPosition = Vector2.zero;

                var horizontal = UiFactory.Image(content, null, GridColor);
                horizontal.gameObject.name = "GridH" + i;
                var hrt = horizontal.rectTransform;
                hrt.anchorMin = new Vector2(0f, t);
                hrt.anchorMax = new Vector2(1f, t);
                hrt.pivot = new Vector2(0.5f, 0.5f);
                hrt.sizeDelta = new Vector2(0f, thickness);
                hrt.anchoredPosition = Vector2.zero;
            }
        }

        private void BuildLegend(RectTransform body)
        {
            var panel = UiFactory.Panel(body, UiTheme.Panel, UiSprites.ChamferRect);
            panel.gameObject.name = "Legend";
            UiFactory.SetRect(panel, LegendLeft, MapTop, LegendWidth, MapFrameSize);

            var list = UiFactory.VerticalList(panel, 4f, UiTheme.Padding);
            list.gameObject.name = "List";

            UiWidgets.Header(list, "DURUM", UiTheme.FontNormal);
            UiWidgets.KeyValueRow(list, "Pafta", "-", out _gridValue, UiTheme.FontSmall);
            UiWidgets.KeyValueRow(list, "Koordinat", "-", out _coordValue, UiTheme.FontSmall);
            UiWidgets.KeyValueRow(list, "Bölge", "-", out _locationValue, UiTheme.FontSmall);
            UiWidgets.KeyValueRow(list, "Harekât alanı", "-", out _zoneValue, UiTheme.FontSmall);
            UiWidgets.KeyValueRow(list, "Güvenli bölgeye", "-", out _safeValue, UiTheme.FontSmall);
            UiWidgets.KeyValueRow(list, "Tim", "-", out _teamValue, UiTheme.FontSmall);
            UiWidgets.KeyValueRow(list, "Tim emri", "-", out _orderValue, UiTheme.FontSmall);
            UiWidgets.KeyValueRow(list, "Topçu desteği", "-", out _artilleryValue, UiTheme.FontSmall);
            UiWidgets.KeyValueRow(list, "İşaret", "-", out _markerValue, UiTheme.FontSmall);

            UiFactory.Spacer(list, 6f);
            UiWidgets.Header(list, "LEJANT", UiTheme.FontNormal);
            LegendRow(list, "Oyuncu (bakış yönü)", r => MapIcons.Arrow(r, "Icon", 16f, PlayerColor, out _));
            LegendRow(list, "Tim arkadaşı", r => MapIcons.Dot(r, "Icon", 11f, UiTheme.AllyBlue, out _));
            LegendRow(list, "Harekât alanı sınırı", r => LegendRing(r, ZoneEdgeColor, 0));
            LegendRow(list, "Sonraki güvenli bölge", r => LegendRing(r, UiTheme.SafeZoneWhite, 0));
            LegendRow(list, "İniş bölgesi (LZ)", r => MapIcons.Diamond(r, "Icon", 15f, LandingZoneColor, out _));
            LegendRow(list, "İntikal rotası / aracı", r => LegendLine(r));
            LegendRow(list, "Topçu atış hedefi", r => MapIcons.Cross(r, "Icon", 14f, ArtilleryColor, out _));
            LegendRow(list, "Taarruz hedefi", r => MapIcons.Target(r, "Icon", 17f, AttackColor, out _));
            LegendRow(list, "Tehlike işareti", r => MapIcons.Diamond(r, "Icon", 14f, DangerColor, out _));
            LegendRow(list, "Araç", r => VehicleIcon(r, "Icon", VehicleColor, out _, out _));
            UiFactory.Spacer(list, 6f);
            UiWidgets.Header(list, "KONTROLLER", UiTheme.FontNormal);
            HintRow(list, "Sol tık", "İşaret koy (taarruz / topçu hedefi)");
            HintRow(list, "Çift tık", "Hızlı \"Gidiyorum\" pingi");
            HintRow(list, "Orta tık", "Ping menüsü (basılı tut)");
            HintRow(list, "L", "Lejantı göster / gizle");
            HintRow(list, "Shift+Sol tık", "Tehlike işareti (düşman)");
            HintRow(list, "Sağ sürükle", "Mesafe / kerteriz ölç");
            HintRow(list, "Sağ tık", "İşareti ve ölçümü kaldır");
            HintRow(list, "Tekerlek", "Yakınlaştır / uzaklaştır");
            HintRow(list, "Sürükle / WASD", "Haritayı kaydır");
            HintRow(list, "Boşluk", "Konumuma ortala");
            HintRow(list, "M / Esc", "Haritayı kapat");
        }

        private static void LegendRow(RectTransform list, string text, Func<RectTransform, RectTransform> icon)
        {
            var row = UiFactory.CreateRect("Legend_" + text, list);
            UiFactory.LayoutSize(row, -1f, 24f, 1f);

            var holder = UiFactory.CreateRect("IconHolder", row);
            UiFactory.Anchor(holder, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(28f, 26f));
            holder.pivot = new Vector2(0f, 0.5f);
            icon?.Invoke(holder);

            var label = UiFactory.Label(row, text, UiTheme.FontTiny + 1, TextAnchor.MiddleLeft, UiTheme.TextDim);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Stretch(label, 42f, 0f, 0f, 0f);
        }

        private static void BuildNorthArrow(RectTransform overlay)
        {
            var holder = UiFactory.CreateRect("NorthArrow", overlay);
            UiFactory.Anchor(holder, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -8f), new Vector2(30f, 38f));
            var arrow = MapIcons.Arrow(holder, "Icon", 16f, UiTheme.Text, out _);
            arrow.anchorMin = arrow.anchorMax = new Vector2(0.5f, 0.5f);
            arrow.anchoredPosition = new Vector2(0f, -4f);
            var n = UiFactory.Label(holder, "K", UiTheme.FontTiny, TextAnchor.UpperCenter, UiTheme.Text, FontStyle.Bold);
            n.horizontalOverflow = HorizontalWrapMode.Overflow;
            n.raycastTarget = false;
            UiFactory.Stretch(n, 0f, 0f, 0f, 0f);
            UiFactory.AddOutline(n, MapIcons.OutlineColor, 1f);
        }

        private void BuildLegendOverlay(RectTransform overlay)
        {
            var panel = UiFactory.Panel(overlay, UiTheme.WithAlpha(UiTheme.PanelDark, 0.84f), UiSprites.ChamferRect);
            panel.gameObject.name = "LegendOverlay";
            panel.GetComponent<Image>().raycastTarget = false;
            UiFactory.Anchor(panel, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(12f, 48f), new Vector2(196f, 150f));
            _legendOverlay = panel;
            var list = UiFactory.VerticalList(panel, 2f, 6);
            list.gameObject.name = "List";
            LegendRow(list, "Tim", r => MapIcons.Dot(r, "Icon", 11f, UiTheme.AllyBlue, out _));
            LegendRow(list, "Düşman (bilinen)", r => MapIcons.Diamond(r, "Icon", 14f, DangerColor, out _));
            LegendRow(list, "Araç", r => VehicleIcon(r, "Icon", VehicleColor, out _, out _));
            LegendRow(list, "İkmal / yağma", r => MapIcons.Diamond(r, "Icon", 13f, LootColor, out _));
            LegendRow(list, "Bölge", r => LegendRing(r, ZoneEdgeColor, 0));
            foreach (var graphic in panel.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }

        private void BuildCursorTag(RectTransform overlay)
        {
            var tag = UiFactory.Panel(overlay, UiTheme.WithAlpha(UiTheme.PanelDark, 0.88f), UiSprites.GetRoundedRect(4));
            tag.gameObject.name = "CursorTag";
            tag.GetComponent<Image>().raycastTarget = false;
            UiFactory.Anchor(tag, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), Vector2.zero, new Vector2(44f, 20f));
            _cursorTagText = UiFactory.Label(tag, string.Empty, UiTheme.FontTiny, TextAnchor.MiddleCenter, UiTheme.Text, FontStyle.Bold);
            _cursorTagText.raycastTarget = false;
            _cursorTagText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Stretch(_cursorTagText);
            _cursorTag = tag;
            tag.gameObject.SetActive(false);
        }

        private void BuildRadial(RectTransform overlay)
        {
            _radial = UiFactory.CreateRect("PingRadial", overlay);
            UiFactory.Anchor(_radial, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 4f));
            MapIcons.Dot(_radial, "Hub", 10f, UiTheme.Text, out _);
            for (var i = 0; i < 4; i++)
            {
                var slice = UiFactory.Panel(_radial, UiTheme.WithAlpha(UiTheme.PanelDark, 0.92f), UiSprites.GetRoundedRect(6));
                slice.gameObject.name = "Slice_" + RadialNames[i];
                slice.GetComponent<Image>().raycastTarget = false;
                UiFactory.Anchor(slice, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), RadialOffsets[i], new Vector2(104f, 32f));
                var label = UiFactory.Label(slice, RadialNames[i], UiTheme.FontSmall - 1, TextAnchor.MiddleCenter, UiTheme.Text, FontStyle.Bold);
                label.raycastTarget = false;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiFactory.Stretch(label);
                _radialSlices[i] = slice.GetComponent<Image>();
            }

            _radial.gameObject.SetActive(false);
        }

        private static Color RadialColor(int index, bool selected)
        {
            if (!selected)
                return UiTheme.WithAlpha(UiTheme.PanelDark, 0.92f);
            switch (index)
            {
                case 1:
                case 3:
                    return DangerColor;
                case 2:
                    return LootColor;
                default:
                    return WaypointColor;
            }
        }

        private void OpenRadial()
        {
            var mouse = Mouse.current;
            if (mouse == null || _radial == null)
                return;

            var screen = mouse.position.ReadValue();
            if (!ScreenToContentUv(screen, null, out var uv)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, screen, null, out var local))
                return;

            _radialUv = ClampUv(uv);
            _radialCenterScreen = screen;
            _radial.anchoredPosition = local - _viewport.rect.center;
            _radialSel = -1;
            for (var i = 0; i < 4; i++)
                _radialSlices[i].color = RadialColor(i, false);
            _radial.gameObject.SetActive(true);
            _radialOpen = true;
            if (_cursorTag != null)
                _cursorTag.gameObject.SetActive(false);
        }

        private void UpdateRadial()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            if (!_radialOpen)
            {
                if (mouse.middleButton.wasPressedThisFrame && _surface != null && _surface.IsHovered)
                    OpenRadial();
                return;
            }

            var sel = MapMath.RadialSlice((mouse.position.ReadValue() - _radialCenterScreen) / CanvasScale(), RadialDeadZone);
            if (sel != _radialSel)
            {
                if (_radialSel >= 0)
                    _radialSlices[_radialSel].color = RadialColor(_radialSel, false);
                _radialSel = sel;
                if (sel >= 0)
                    _radialSlices[sel].color = RadialColor(sel, true);
            }

            if (!mouse.middleButton.isPressed)
            {
                var chosen = _radialSel;
                CloseRadial();
                if (chosen >= 0)
                    ApplyRadialChoice(chosen, _radialUv);
                else
                    CenterOn(_data.Frame.WorldToUV(_data.PlayerPosition));
            }
        }

        private void CloseRadial()
        {
            _radialOpen = false;
            _radialSel = -1;
            if (_radial != null)
                _radial.gameObject.SetActive(false);
        }

        private Vector3 WorldAtUv(Vector2 uv)
        {
            var world = _data.Frame.UVToWorld(uv);
            var metadata = _data.World;
            if (metadata != null)
            {
                try
                {
                    world.y = metadata.SampleGroundHeight(world);
                }
                catch (Exception)
                {
                    world.y = 0f;
                }
            }

            return world;
        }

        private void ApplyRadialChoice(int slice, Vector2 uv)
        {
            var world = WorldAtUv(uv);
            if (!MapMath.IsFinite(world))
                return;

            switch (slice)
            {
                case 0: // Gidiyorum
                    MapMarkers.SetWaypoint(world);
                    _waypointPulse = 1f;
                    _shownMarkerKey = int.MinValue;
                    PlacePing(world, false);
                    UiWidgets.PlaySound(SoundId.UiConfirm);
                    PointMarked?.Invoke(world);
                    break;
                case 1: // Düşman
                case 3: // Tehlike
                    PlacePing(world, true);
                    UiWidgets.PlaySound(SoundId.RadioBeep);
                    break;
                default: // Yağma
                    _hasLoot = true;
                    _lootUv = uv;
                    _lootAnchor = new Vector2(-1f, -1f);
                    PlacePing(world, false);
                    UiWidgets.PlaySound(SoundId.UiClick);
                    break;
            }
        }

        private void UpdateLoot()
        {
            if (_lootMarker == null)
                return;
            SetActive(_lootMarker, _hasLoot);
            if (_hasLoot)
                SetAnchor(_lootMarker, _lootUv, ref _lootAnchor);
        }

        private static RectTransform LegendRing(RectTransform holder, Color color, int dashes)
        {
            var ring = MapRingGraphic.Create(holder, "Icon", color);
            ring.DashCount = dashes;
            ring.SetStroke(9f, 2.2f);
            return ring.rectTransform;
        }

        private static RectTransform LegendLine(RectTransform holder)
        {
            var line = MapLineGraphic.Create(holder, "Icon", RouteColor, 2.5f);
            line.SetDash(6f, 4f);
            line.SetSegment(new Vector2(1f, 13f), new Vector2(27f, 13f));
            return line.rectTransform;
        }

        private static void HintRow(RectTransform list, string key, string action)
        {
            var row = UiFactory.CreateRect("Hint_" + key, list);
            UiFactory.LayoutSize(row, -1f, 24f, 1f);

            var keyBox = UiFactory.Panel(row, UiTheme.Hex(0xEC, 0xEB, 0xE0, 0xE6), UiSprites.GetRoundedRect(4));
            keyBox.gameObject.name = "Key";
            keyBox.GetComponent<Image>().raycastTarget = false;
            UiFactory.Anchor(keyBox, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(2f, 0f), new Vector2(104f, 22f));
            var keyText = UiFactory.Label(keyBox, key, UiTheme.FontTiny, TextAnchor.MiddleCenter, UiTheme.Background, FontStyle.Bold);
            keyText.horizontalOverflow = HorizontalWrapMode.Overflow;

            var label = UiFactory.Label(row, action, UiTheme.FontTiny + 1, TextAnchor.MiddleLeft, UiTheme.TextDim);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Stretch(label, 118f, 0f, 0f, 0f);
        }

        private static RectTransform VehicleIcon(Transform parent, string name, Color color, out RectTransform body, out Image fill)
        {
            var root = MapIcons.Container(parent, name, 18f);
            body = MapIcons.Container(root, "Body", 18f);
            MapIcons.Shape(body, "Outline", UiSprites.White, MapIcons.OutlineColor, 12f, 18f);
            fill = MapIcons.Shape(body, "Fill", UiSprites.White, color, 8f, 14f);
            return root;
        }

        // ================================================================== Kare döngüsü

        private void LateUpdate()
        {
            if (!_built || !IsOpen)
                return;

            MapOverlayInput.Maintain();
            HandleKeyboard();
            Refresh(false);
        }

        private void Refresh(bool force)
        {
            _data.Refresh();
            var frame = _data.Frame;

            ApplyTexture();
            EnsureLocations(frame);

            if (_viewDirty || force)
                ApplyView(frame);

            var ppm = _mapPixels / Mathf.Max(1f, frame.Size);
            var playerPos = _data.PlayerPosition;

            UpdateZones(frame, ppm);
            UpdateInsertion(frame);
            UpdateArtilleryAndOrders(frame, ppm);
            UpdateWaypoint(frame);
            UpdateDangerPing(frame);
            UpdateVehicles(frame);
            UpdateFallen(frame);
            UpdateAllies(frame, ppm);
            UpdatePlayer(frame, playerPos, ppm);
            UpdateMeasure(frame);
            UpdateLoot();
            UpdateRadial();
            UpdateCursor(frame, playerPos);

            if (force || Time.unscaledTime >= _nextTextRefresh)
            {
                _nextTextRefresh = Time.unscaledTime + TextRefreshInterval;
                UpdatePanelTexts(frame, playerPos);
            }
        }

        private void ApplyTexture()
        {
            var world = _data.World;
            var noWorld = world == null;
            if (_noMapLabel.enabled != noWorld)
                _noMapLabel.enabled = noWorld;

            var texture = world != null ? world.MinimapTexture : null;
            if (_textureApplied && texture == _appliedTexture)
                return;

            _textureApplied = true;
            _appliedTexture = texture;
            if (texture != null)
            {
                // Arazi renkli haritası yakınlaştırınca bulanık ama yumuşak kalsın (kenar sızıntısı yok).
                try
                {
                    texture.filterMode = FilterMode.Bilinear;
                    texture.wrapMode = TextureWrapMode.Clamp;
                }
                catch (Exception)
                {
                    // Okunamaz doku: varsayılan örnekleme kalır.
                }
            }

            _mapImage.texture = texture;
            _mapImage.color = texture != null ? Color.white : FallbackMapColor;
        }

        private void EnsureLocations(MapFrame frame)
        {
            var world = _data.World;
            var count = world != null && world.Locations != null ? world.Locations.Count : 0;
            if (world == _locationsWorld && count == _locationsCount)
                return;

            _locationsWorld = world;
            _locationsCount = count;
            UiFactory.ClearChildren(_locationLayer);
            if (world == null || world.Locations == null)
                return;

            for (var i = 0; i < world.Locations.Count; i++)
            {
                var location = world.Locations[i];
                if (location == null || string.IsNullOrEmpty(location.Name))
                    continue;

                var uv = frame.WorldToUV(location.Center.x, location.Center.y);
                if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f)
                    continue;

                var size = location.IsMajor ? UiTheme.FontSmall : UiTheme.FontTiny;
                var color = location.IsMajor ? LocationMajorColor : LocationMinorColor;
                var label = UiFactory.Label(_locationLayer, location.Name, size, TextAnchor.MiddleCenter, color,
                    location.IsMajor ? FontStyle.Bold : FontStyle.Normal);
                label.gameObject.name = "Loc_" + location.Name;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                var rt = label.rectTransform;
                rt.anchorMin = uv;
                rt.anchorMax = uv;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(240f, size + 8f);
                rt.anchoredPosition = Vector2.zero;
                UiFactory.AddOutline(label, MapIcons.OutlineColor, location.IsMajor ? 1.6f : 1.2f);
            }
        }

        private void ApplyView(MapFrame frame)
        {
            _viewDirty = false;
            _zoom = Mathf.Clamp(_zoom, 1f, MaxZoom);
            ClampFocus();

            var showSubGrid = _zoom >= SubGridZoom;
            if (showSubGrid != _subGridShown)
            {
                _subGridShown = showSubGrid;
                _subGridRoot.gameObject.SetActive(showSubGrid);
            }

            _mapPixels = ViewportSize * _zoom;
            _content.sizeDelta = new Vector2(_mapPixels, _mapPixels);
            _content.anchoredPosition = -_focus * _mapPixels;

            // Cetveller: hücre merkezleri görüntü alanı merkezine göre.
            var half = ViewportSize * 0.5f;
            for (var i = 0; i < MapMath.GridDivisions; i++)
            {
                var center = MapMath.GridCellCenterUV(i, i);
                var x = (center.x - _focus.x) * _mapPixels;
                var y = (center.y - _focus.y) * _mapPixels;
                PlaceRulerLabel(_columnTop[i], new Vector2(x, 0f), Mathf.Abs(x) <= half - 6f);
                PlaceRulerLabel(_columnBottom[i], new Vector2(x, 0f), Mathf.Abs(x) <= half - 6f);
                PlaceRulerLabel(_rowLeft[i], new Vector2(0f, y), Mathf.Abs(y) <= half - 6f);
                PlaceRulerLabel(_rowRight[i], new Vector2(0f, y), Mathf.Abs(y) <= half - 6f);
            }

            // Ölçek çubuğu (~120 px'e en yakın yuvarlak mesafe).
            var ppm = _mapPixels / Mathf.Max(1f, frame.Size);
            var meters = ScaleBarSteps[ScaleBarSteps.Length - 1];
            for (var i = 0; i < ScaleBarSteps.Length; i++)
            {
                if (ScaleBarSteps[i] * ppm >= 90f)
                {
                    meters = ScaleBarSteps[i];
                    break;
                }
            }

            _scaleBar.sizeDelta = new Vector2(Mathf.Max(8f, meters * ppm), 6f);
            UiFactory.SetText(_scaleLabel, UiWidgets.Number(meters) + " m");
            var zoomPercent = MapMath.ZoomPercent(_zoom);
            if (zoomPercent != _shownZoomPercent)
            {
                _shownZoomPercent = zoomPercent;
                UiFactory.SetText(_zoomLabel, "%" + zoomPercent);
            }
        }

        private static void PlaceRulerLabel(Text label, Vector2 position, bool visible)
        {
            if (label == null)
                return;
            if (label.enabled != visible)
                label.enabled = visible;
            if (visible)
                label.rectTransform.anchoredPosition = position;
        }

        private void UpdateZones(MapFrame frame, float ppm)
        {
            if (!_data.TryGetZones(out var current, out var hasNext, out var next))
            {
                SetEnabled(_zoneTint, false);
                SetEnabled(_zoneEdge, false);
                SetEnabled(_nextZone, false);
                return;
            }

            var currentUv = frame.WorldToUV(current.CenterX, current.CenterZ);
            SetAnchor(_zoneEdge.rectTransform, currentUv, ref _zoneAnchor);
            _zoneTint.rectTransform.anchorMin = _zoneEdge.rectTransform.anchorMin;
            _zoneTint.rectTransform.anchorMax = _zoneEdge.rectTransform.anchorMax;

            var radiusPx = current.Radius * ppm;
            SetEnabled(_zoneEdge, true);
            _zoneEdge.SetStroke(radiusPx, 3f);

            // Bölge dışı tonlama: halkanın dış yarıçapı görüntü alanının en uzak köşesini aşar.
            var centerInViewport = (currentUv - _focus) * _mapPixels;
            var farthest = centerInViewport.magnitude + ViewportSize * 0.7072f + 8f;
            var needsTint = farthest > radiusPx;
            SetEnabled(_zoneTint, needsTint);
            if (needsTint)
                _zoneTint.SetRing(radiusPx, Mathf.Max(radiusPx + 1f, farthest));

            if (hasNext)
            {
                var nextUv = frame.WorldToUV(next.CenterX, next.CenterZ);
                SetAnchor(_nextZone.rectTransform, nextUv, ref _nextZoneAnchor);
                SetEnabled(_nextZone, true);
                _nextZone.SetStroke(next.Radius * ppm, 2.2f);
                UpdateNextZoneLabel(nextUv, next.Radius * ppm);
            }
            else
            {
                SetEnabled(_nextZone, false);
                SetEnabled(_nextZoneLabel, false);
            }
        }

        private void UpdateNextZoneLabel(Vector2 nextUv, float radiusPx)
        {
            var zone = _data.Zone;
            if (zone == null)
            {
                SetEnabled(_nextZoneLabel, false);
                return;
            }

            SetEnabled(_nextZoneLabel, true);
            var rt = _nextZoneLabel.rectTransform;
            rt.anchorMin = nextUv;
            rt.anchorMax = nextUv;
            var toView = _focus - nextUv;
            var dir = toView.sqrMagnitude > 1e-8f ? toView.normalized : Vector2.up;
            var labelPos = dir * (radiusPx + 12f);
            if ((labelPos - _nextZoneLabelPos).sqrMagnitude > 0.25f || float.IsNaN(_nextZoneLabelPos.x))
            {
                _nextZoneLabelPos = labelPos;
                rt.anchoredPosition = labelPos;
            }

            var stage = zone.Stage;
            var seconds = Mathf.CeilToInt(Mathf.Max(0f, zone.StageRemainingSeconds));
            var key = ((int)stage + 1) * 100000 + Mathf.Clamp(seconds, 0, 99999);
            if (key == _shownNextZoneKey)
                return;

            _shownNextZoneKey = key;
            switch (stage)
            {
                case ZoneStage.Waiting:
                    _nextZoneLabel.text = "SONRAKİ ALAN  " + UiWidgets.Clock(seconds);
                    break;
                case ZoneStage.Shrinking:
                    _nextZoneLabel.text = "DARALIYOR  " + UiWidgets.Clock(seconds);
                    break;
                default:
                    _nextZoneLabel.text = "SON GÜVENLİ ALAN";
                    break;
            }
        }

        private void UpdateInsertion(MapFrame frame)
        {
            if (!_data.TryGetInsertion(out var start, out var landingZone, out var method, out var transport))
            {
                SetActive(_landingZone, false);
                SetActive(_transportMarker, false);
                _routeLine.ClearPoints();
                return;
            }

            _routeLine.SetSegment(frame.WorldToUV(start) * _mapPixels, frame.WorldToUV(landingZone) * _mapPixels);

            SetActive(_landingZone, true);
            PlaceAtUv(_landingZone, ClampUv(frame.WorldToUV(landingZone)));

            if (transport != null)
            {
                SetActive(_transportMarker, true);
                PlaceAtUv(_transportMarker, ClampUv(frame.WorldToUV(transport.transform.position)));
                UiFactory.SetText(_transportLabel, method == InsertionMethod.Helicopter ? "T-70" : "KİRPİ");
            }
            else
            {
                SetActive(_transportMarker, false);
            }
        }

        private void UpdateArtilleryAndOrders(MapFrame frame, float ppm)
        {
            if (_data.TryGetArtillery(out var target, out var seconds))
            {
                var uv = frame.WorldToUV(target);
                SetActive(_artilleryCross, true);
                PlaceAtUv(_artilleryCross, uv);

                SetEnabled(_artilleryRing, true);
                var rt = _artilleryRing.rectTransform;
                rt.anchorMin = uv;
                rt.anchorMax = uv;
                rt.anchoredPosition = Vector2.zero;
                _artilleryRing.SetStroke((ArtilleryService.SpreadRadius + ArtilleryService.ShellRadius) * ppm, 2.2f);
                var pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
                _artilleryRing.color = UiTheme.WithAlpha(ArtilleryColor, pulse);

                var key = Mathf.CeilToInt(Mathf.Max(0f, seconds));
                if (key != _shownArtilleryLabelKey)
                {
                    _shownArtilleryLabelKey = key;
                    _artilleryLabel.text = key > 0 ? "TOPÇU " + UiWidgets.Clock(key) : "TOPÇU ATIŞI";
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
                PlaceAtUv(_attackMarker, ClampUv(frame.WorldToUV(orderTarget)));
            }
            else
            {
                SetActive(_attackMarker, false);
            }
        }

        private void UpdateWaypoint(MapFrame frame)
        {
            if (!MapMarkers.HasWaypoint)
            {
                SetActive(_waypoint, false);
                return;
            }

            SetActive(_waypoint, true);
            PlaceAtUv(_waypoint, ClampUv(frame.WorldToUV(MapMarkers.Waypoint)));

            // Yeni işarette kısa "zıplama" geri bildirimi.
            if (_waypointPulse > 0f)
            {
                _waypointPulse = Mathf.Max(0f, _waypointPulse - Time.unscaledDeltaTime * 3f);
                var s = 1f + 0.45f * _waypointPulse;
                _waypoint.localScale = new Vector3(s, s, 1f);
            }
            else if (_waypoint.localScale != Vector3.one)
            {
                _waypoint.localScale = Vector3.one;
            }
        }

        private void UpdateDangerPing(MapFrame frame)
        {
            var self = _data.Self;
            if (self == null || !PingBoard.TryGet(self.Team, Time.time, out var ping) || !ping.IsEnemy)
            {
                SetActive(_dangerPing, false);
                SetEnabled(_dangerRing, false);
                return;
            }

            var uv = ClampUv(frame.WorldToUV(ping.Position));
            SetActive(_dangerPing, true);
            PlaceAtUv(_dangerPing, uv);

            // Nabız halkası: ömür azaldıkça solar.
            var remaining = ping.Remaining01(Time.time);
            var phase = Mathf.Repeat(Time.unscaledTime * 1.4f, 1f);
            SetEnabled(_dangerRing, true);
            PlaceAtUv(_dangerRing.rectTransform, uv);
            _dangerRing.SetStroke(10f + phase * 26f, 2.5f);
            _dangerRing.color = UiTheme.WithAlpha(DangerColor, (1f - phase) * Mathf.Clamp01(remaining * 1.5f));
        }

        private void UpdateVehicles(MapFrame frame)
        {
            var used = 0;
            var list = VehicleRegistry.All;
            for (var i = 0; i < list.Count; i++)
            {
                var vehicle = list[i];
                if (vehicle == null || !vehicle.isActiveAndEnabled)
                    continue;

                var marker = GetVehicleMarker(used++);
                if (!marker.Visible)
                {
                    marker.Visible = true;
                    marker.Root.gameObject.SetActive(true);
                }

                if (!ReferenceEquals(marker.Bound, vehicle))
                {
                    marker.Bound = vehicle;
                    marker.Occupied = -1;
                    var config = vehicle.Config;
                    marker.Label.text = config != null && !string.IsNullOrEmpty(config.DisplayName)
                        ? MapMath.ToUpperTurkish(config.DisplayName)
                        : "ARAÇ";
                }

                var uv = ClampUv(frame.WorldToUV(vehicle.transform.position));
                if ((uv - marker.Anchor).sqrMagnitude > 1e-10f)
                {
                    marker.Anchor = uv;
                    PlaceAtUv(marker.Root, uv);
                }

                var yaw = vehicle.transform.eulerAngles.y;
                if (float.IsNaN(marker.Yaw) || Mathf.Abs(Mathf.DeltaAngle(yaw, marker.Yaw)) > 0.5f)
                {
                    marker.Yaw = yaw;
                    marker.Body.localRotation = Quaternion.Euler(0f, 0f, MapMath.YawToUiAngle(yaw));
                }

                var occupied = vehicle.HasDriver ? 1 : 0;
                if (occupied != marker.Occupied)
                {
                    marker.Occupied = occupied;
                    marker.Fill.color = occupied == 1 ? UiTheme.Amber : VehicleColor;
                }
            }

            for (var i = used; i < _vehicles.Count; i++)
            {
                var marker = _vehicles[i];
                if (!marker.Visible)
                    continue;
                marker.Visible = false;
                marker.Bound = null;
                marker.Root.gameObject.SetActive(false);
            }
        }

        private VehicleMarker GetVehicleMarker(int index)
        {
            while (_vehicles.Count <= index)
            {
                var root = VehicleIcon(_iconLayer, "Vehicle" + _vehicles.Count, VehicleColor, out var body, out var fill);
                var label = MapIcons.Badge(root, "Label", string.Empty, UiTheme.FontTiny - 2, VehicleColor, new Vector2(0f, -17f), 90f);
                root.gameObject.SetActive(false);
                _vehicles.Add(new VehicleMarker { Root = root, Body = body, Fill = fill, Label = label });
            }

            return _vehicles[index];
        }

        private void UpdateFallen(MapFrame frame)
        {
            var list = MapMarkers.Fallen;
            var count = list.Count;
            for (var i = 0; i < count; i++)
            {
                var icon = GetFallenIcon(i);
                var record = list[i];
                if (!icon.Visible)
                {
                    icon.Visible = true;
                    icon.Root.gameObject.SetActive(true);
                }

                // Aynı kayıt (konum+zaman karması) için etiketi yeniden kurma.
                var serial = Mathf.RoundToInt(record.Time * 10f) ^ (int)record.Rank;
                if (icon.BoundIndex != i || icon.BoundSerial != serial)
                {
                    icon.BoundIndex = i;
                    icon.BoundSerial = serial;
                    icon.Label.text = RankCatalog.FormatName(record.Rank, record.Name);
                }

                PlaceAtUv(icon.Root, ClampUv(frame.WorldToUV(record.Position)));
            }

            for (var i = count; i < _fallen.Count; i++)
            {
                var icon = _fallen[i];
                if (!icon.Visible)
                    continue;
                icon.Visible = false;
                icon.BoundIndex = -1;
                icon.Root.gameObject.SetActive(false);
            }
        }

        private FallenIcon GetFallenIcon(int index)
        {
            while (_fallen.Count <= index)
            {
                var root = MapIcons.Cross(_iconLayer, "Fallen" + _fallen.Count, 13f, FallenColor, out _);
                var label = MapIcons.Badge(root, "Label", string.Empty, UiTheme.FontTiny - 2, FallenColor, new Vector2(0f, -15f), 160f);
                root.gameObject.SetActive(false);
                _fallen.Add(new FallenIcon { Root = root, Label = label });
            }

            return _fallen[index];
        }

        private void UpdateMeasure(MapFrame frame)
        {
            if (!_measureActive)
            {
                if (_measureLabel.gameObject.activeSelf)
                {
                    _measureLine.ClearPoints();
                    _measureA.gameObject.SetActive(false);
                    _measureB.gameObject.SetActive(false);
                    _measureLabel.gameObject.SetActive(false);
                }

                return;
            }

            _measureA.gameObject.SetActive(true);
            _measureB.gameObject.SetActive(true);
            _measureLabel.gameObject.SetActive(true);
            _measureLine.SetSegment(_measureStartUv * _mapPixels, _measureEndUv * _mapPixels);
            PlaceAtUv(_measureA, _measureStartUv);
            PlaceAtUv(_measureB, _measureEndUv);

            var mid = (_measureStartUv + _measureEndUv) * 0.5f;
            var rt = _measureLabel.rectTransform;
            rt.anchorMin = mid;
            rt.anchorMax = mid;
            rt.anchoredPosition = new Vector2(0f, 16f);

            var a = frame.UVToWorld(_measureStartUv);
            var b = frame.UVToWorld(_measureEndUv);
            var distance = Mathf.RoundToInt(MapMath.DistanceXZ(a, b));
            var key = distance * 1000 + MapMath.BearingDegrees(MapMath.BearingTo(a, b));
            if (key == _shownMeasureKey)
                return;

            _shownMeasureKey = key;
            _measureLabel.text = MapMath.FormatMeasure(a, b);
        }

        private void UpdateAllies(MapFrame frame, float ppm)
        {
            var used = 0;
            var self = _data.Self;
            var nameMode = _zoom >= AllyNameZoom ? 1 : 0;
            if (self != null)
            {
                var team = self.Team;
                var all = CombatantRegistry.All;
                for (var i = 0; i < all.Count; i++)
                {
                    var c = all[i];
                    if (c == null || c == self || c.Team != team || !c.IsAlive)
                        continue;

                    var marker = GetAllyMarker(used++);
                    if (!marker.Visible)
                    {
                        marker.Visible = true;
                        marker.Root.gameObject.SetActive(true);
                    }

                    var rebind = !ReferenceEquals(marker.Bound, c);
                    if (rebind)
                    {
                        marker.Bound = c;
                        marker.NameMode = -1;
                        marker.Downed = -1;
                    }

                    if (marker.NameMode != nameMode)
                    {
                        marker.NameMode = nameMode;
                        marker.Name.text = nameMode == 1 ? ShortName(c) : RankCatalog.GetShortName(c.Rank);
                    }

                    var uv = ClampUv(frame.WorldToUV(c.transform.position));
                    if ((uv - marker.Anchor).sqrMagnitude > 1e-10f)
                    {
                        marker.Anchor = uv;
                        PlaceAtUv(marker.Root, uv);
                    }

                    // Görüş konisi (bakış yönü) — yaralı (yerde) tim arkadaşında koni kapalı.
                    var downed = c.IsDowned ? 1 : 0;
                    if (downed != marker.Downed)
                    {
                        marker.Downed = downed;
                        marker.Fill.color = downed == 1 ? UiTheme.Amber : UiTheme.AllyBlue;
                        marker.Cone.enabled = downed == 0;
                    }

                    if (downed == 0)
                    {
                        if (Mathf.Abs(marker.ConePpm - ppm) > 1e-4f)
                        {
                            marker.ConePpm = ppm;
                            marker.Cone.SetShape(AllyConeMeters * ppm, AllyConeAngle);
                        }

                        var yaw = c.transform.eulerAngles.y;
                        if (float.IsNaN(marker.ConeYaw) || Mathf.Abs(Mathf.DeltaAngle(yaw, marker.ConeYaw)) > 1f)
                        {
                            marker.ConeYaw = yaw;
                            marker.Cone.rectTransform.localRotation = Quaternion.Euler(0f, 0f, MapMath.YawToUiAngle(yaw));
                        }
                    }
                }
            }

            for (var i = used; i < _allies.Count; i++)
            {
                var marker = _allies[i];
                if (!marker.Visible)
                    continue;
                marker.Visible = false;
                marker.Bound = null;
                marker.Root.gameObject.SetActive(false);
            }

            if (_playerArrow.GetSiblingIndex() != _iconLayer.childCount - 1)
                _playerArrow.SetAsLastSibling();
        }

        private AllyMarker GetAllyMarker(int index)
        {
            while (_allies.Count <= index)
            {
                var root = MapIcons.Dot(_iconLayer, "Ally" + _allies.Count, 11f, UiTheme.AllyBlue, out var fill);
                var name = MapIcons.Badge(root, "Name", string.Empty, UiTheme.FontTiny - 2, UiTheme.AllyBlue, new Vector2(0f, 13f), 160f);
                var cone = MapConeGraphic.Create(root, "ViewCone", UiTheme.WithAlpha(UiTheme.AllyBlue, 0.34f));
                cone.transform.SetAsFirstSibling();
                root.gameObject.SetActive(false);
                _allies.Add(new AllyMarker { Root = root, Fill = fill, Name = name, Cone = cone });
            }

            return _allies[index];
        }

        private static string ShortName(Combatant combatant)
        {
            if (combatant == null)
                return string.Empty;
            var name = combatant.RankedName;
            return string.IsNullOrEmpty(name) ? combatant.DisplayName ?? string.Empty : name;
        }

        private void UpdatePlayer(MapFrame frame, Vector3 playerPos, float ppm)
        {
            var hasPlayer = _data.HasPlayer;
            SetActive(_playerArrow, hasPlayer);
            if (!hasPlayer)
                return;

            var coneOn = !_data.IsPlayerDead;
            SetEnabled(_playerCone, coneOn);
            if (coneOn && Mathf.Abs(_playerConePpm - ppm) > 1e-4f)
            {
                _playerConePpm = ppm;
                _playerCone.SetShape(PlayerConeMeters * ppm, AllyConeAngle);
            }

            var uv = ClampUv(frame.WorldToUV(playerPos));
            if ((uv - _playerAnchor).sqrMagnitude > 1e-10f)
            {
                _playerAnchor = uv;
                PlaceAtUv(_playerArrow, uv);
            }

            var yaw = _data.PlayerYaw;
            if (float.IsNaN(_lastYaw) || Mathf.Abs(Mathf.DeltaAngle(yaw, _lastYaw)) > 0.2f)
            {
                _lastYaw = yaw;
                _playerArrow.localRotation = Quaternion.Euler(0f, 0f, MapMath.YawToUiAngle(yaw));
            }

            var dead = _data.IsPlayerDead ? 1 : 0;
            if (dead != _lastDead)
            {
                _lastDead = dead;
                _playerArrowFill.color = dead == 1 ? DeadPlayerColor : PlayerColor;
            }
        }

        private void UpdateCursor(MapFrame frame, Vector3 playerPos)
        {
            var uv = default(Vector2);
            var show = _surface != null && _surface.IsHovered && TryGetPointerUv(out uv)
                       && uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
            if (!show || _radialOpen)
            {
                if (_cursorTag != null && _cursorTag.gameObject.activeSelf)
                    _cursorTag.gameObject.SetActive(false);
                _lastPointer = new Vector2(float.NaN, float.NaN);
                if (_cursorShown)
                {
                    _cursorShown = false;
                    _shownCursorGrid = null;
                    _shownCursorMeters = -1;
                    UiFactory.SetText(_cursorLabel, string.Empty);
                }

                return;
            }

            var world = frame.UVToWorld(uv);
            var grid = MapMath.GridLabel(frame, world);
            UpdateCursorTag(grid);
            var meters = Mathf.RoundToInt(MapMath.DistanceXZ(playerPos, world) / 5f) * 5;
            MapMath.GridDigits(frame, world, out var easting, out var northing);
            if (_cursorShown && ReferenceEquals(grid, _shownCursorGrid) && meters == _shownCursorMeters
                && easting == _shownCursorEasting && northing == _shownCursorNorthing)
                return;

            _cursorShown = true;
            _shownCursorGrid = grid;
            _shownCursorMeters = meters;
            _shownCursorEasting = easting;
            _shownCursorNorthing = northing;
            _cursorLabel.text = MapMath.FormatMilitaryGrid(easting, northing) + "  ·  " + grid + "  ·  " + MapMath.FormatDistance(meters);
        }

        /// <summary>İmleç yanındaki küçük pafta etiketi; yalnız imleç/hücre değişince güncellenir.</summary>
        private void UpdateCursorTag(string grid)
        {
            var mouse = Mouse.current;
            if (_cursorTag == null || mouse == null)
                return;

            var screen = mouse.position.ReadValue();
            if (!_cursorTag.gameObject.activeSelf)
                _cursorTag.gameObject.SetActive(true);

            if ((screen - _lastPointer).sqrMagnitude >= 1f || float.IsNaN(_lastPointer.x))
            {
                _lastPointer = screen;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, screen, null, out var local))
                {
                    var p = local - _viewport.rect.center + new Vector2(14f, -10f);
                    var half = ViewportSize * 0.5f;
                    p.x = Mathf.Min(p.x, half - 50f);
                    p.y = Mathf.Max(p.y, -half + 24f);
                    _cursorTag.anchoredPosition = p;
                }
            }

            if (!ReferenceEquals(grid, _shownTagGrid))
            {
                _shownTagGrid = grid;
                _cursorTagText.text = grid;
            }
        }

        private void UpdatePanelTexts(MapFrame frame, Vector3 playerPos)
        {
            // Pafta + lokasyon.
            var grid = MapMath.GridLabel(frame, playerPos);
            if (!ReferenceEquals(grid, _shownGrid))
            {
                _shownGrid = grid;
                _gridValue.text = grid;
            }

            var world = _data.World;
            var location = world != null ? world.GetLocationName(playerPos) : string.Empty;
            if (!string.Equals(location, _shownLocation, StringComparison.Ordinal))
            {
                _shownLocation = location;
                _locationValue.text = string.IsNullOrEmpty(location) ? "Açık arazi" : location;
            }

            MapMath.GridDigits(frame, playerPos, out var easting, out var northing);
            var coordKey = easting * 1000 + northing;
            if (coordKey != _shownCoordKey)
            {
                _shownCoordKey = coordKey;
                _coordValue.text = MapMath.FormatMilitaryGrid(easting, northing);
            }

            UpdateZoneTexts(playerPos);
            UpdateTeamTexts();
            UpdateMarkerText(playerPos);
        }

        private void UpdateZoneTexts(Vector3 playerPos)
        {
            var zone = _data.Zone;
            var stage = zone != null ? zone.Stage : ZoneStage.Idle;
            var seconds = zone != null ? Mathf.CeilToInt(Mathf.Max(0f, zone.StageRemainingSeconds)) : 0;
            var phase = zone != null ? zone.PhaseIndex : 0;
            var key = zone == null ? -1 : ((int)stage * 1000 + Mathf.Clamp(phase, 0, 99)) * 10000 + Mathf.Clamp(seconds, 0, 9999);
            if (key != _shownZoneKey)
            {
                _shownZoneKey = key;
                string header;
                string value;
                if (zone == null)
                {
                    header = string.Empty;
                    value = "Bilgi yok";
                }
                else
                {
                    var phaseText = zone.PhaseCount > 0
                        ? "Faz " + UiWidgets.Number(Mathf.Clamp(phase + 1, 1, zone.PhaseCount)) + "/" + UiWidgets.Number(zone.PhaseCount)
                        : string.Empty;
                    switch (stage)
                    {
                        case ZoneStage.Waiting:
                            header = "Harekât alanı daralacak  " + UiWidgets.Clock(seconds);
                            value = phaseText + "  ·  " + UiWidgets.Clock(seconds);
                            break;
                        case ZoneStage.Shrinking:
                            header = UiTheme.Colorize("Harekât alanı daralıyor  " + UiWidgets.Clock(seconds), UiTheme.AllyBlue);
                            value = UiTheme.Colorize(phaseText + "  ·  " + UiWidgets.Clock(seconds), UiTheme.AllyBlue);
                            break;
                        case ZoneStage.Finished:
                            header = "Son güvenli bölge";
                            value = "Son bölge";
                            break;
                        default:
                            header = "Harekât alanı henüz belirlenmedi";
                            value = "Bekleniyor";
                            break;
                    }
                }

                UiFactory.SetText(_zoneHeader, header);
                UiFactory.SetText(_zoneValue, value);
            }

            // Güvenli bölgeye mesafe.
            var safeKey = -2;
            if (_data.TryGetZones(out var current, out var hasNext, out var next))
            {
                var safe = hasNext ? next : current;
                var dx = playerPos.x - safe.CenterX;
                var dz = playerPos.z - safe.CenterZ;
                var outside = Mathf.Sqrt(dx * dx + dz * dz) - safe.Radius;
                safeKey = outside > 1f ? Mathf.RoundToInt(outside / 5f) * 5 : -1;
            }

            if (safeKey != _shownSafeKey)
            {
                _shownSafeKey = safeKey;
                if (safeKey == -2)
                    _safeValue.text = "-";
                else if (safeKey == -1)
                    _safeValue.text = UiTheme.Colorize("İçeride", UiTheme.Success);
                else
                    _safeValue.text = UiTheme.Colorize(MapMath.FormatDistance(safeKey), UiTheme.AllyBlue);
            }
        }

        private void UpdateTeamTexts()
        {
            var team = _data.Team;
            var alive = CombatantRegistry.CountAliveInTeam(team);
            var size = ConfiguredTeamSize();
            var teamKey = alive * 1000 + size;
            if (teamKey != _shownTeamKey)
            {
                _shownTeamKey = teamKey;
                _teamValue.text = size > 0
                    ? UiWidgets.Number(alive) + "/" + UiWidgets.Number(size) + " hayatta"
                    : UiWidgets.Number(alive) + " hayatta";
            }

            var hasOrder = _data.TryGetSquadOrder(out var order, out _);
            var orderKey = hasOrder ? (int)order : -1;
            if (orderKey != _shownOrderKey)
            {
                _shownOrderKey = orderKey;
                _orderValue.text = hasOrder ? OrderName(order) : OrderName(_data.Player != null ? _data.Player.CurrentOrder : SquadOrder.Follow);
            }

            // Topçu: atış sürüyor → kalan süre; bekleme → hazırlanıyor; yetki yok; hazır.
            int artilleryKey;
            string artilleryText;
            if (_data.TryGetArtillery(out _, out var impactSeconds))
            {
                var s = Mathf.CeilToInt(Mathf.Max(0f, impactSeconds));
                artilleryKey = 100000 + s;
                artilleryText = UiTheme.Colorize(s > 0 ? "Atış: " + UiWidgets.Clock(s) : "Atış sürüyor", ArtilleryColor);
            }
            else
            {
                var cd = Mathf.CeilToInt(Mathf.Max(0f, _data.ArtilleryCooldown));
                var access = HasArtilleryAccess();
                if (!access)
                {
                    artilleryKey = -1;
                    artilleryText = UiTheme.Colorize("Telsizci yok", UiTheme.TextMuted);
                }
                else if (cd > 0)
                {
                    artilleryKey = 200000 + cd;
                    artilleryText = UiTheme.Colorize("Hazırlanıyor " + UiWidgets.Clock(cd), UiTheme.Amber);
                }
                else
                {
                    artilleryKey = 0;
                    artilleryText = UiTheme.Colorize("HAZIR [V]", UiTheme.Success);
                }
            }

            if (artilleryKey != _shownArtilleryKey)
            {
                _shownArtilleryKey = artilleryKey;
                _artilleryValue.text = artilleryText;
            }
        }

        private void UpdateMarkerText(Vector3 playerPos)
        {
            var has = MapMarkers.HasWaypoint;
            var meters = has ? Mathf.RoundToInt(MapMath.DistanceXZ(playerPos, MapMarkers.Waypoint) / 5f) * 5 : -1;
            var key = has ? meters : -1;
            if (key == _shownMarkerKey)
                return;

            _shownMarkerKey = key;
            if (!has)
            {
                _markerValue.text = "Yok";
                UiFactory.SetText(_waypointLabel, string.Empty);
                _shownWaypointMeters = -1;
                return;
            }

            var frame = _data.Frame;
            var distance = MapMath.FormatDistance(meters);
            _markerValue.text = MapMath.GridLabel(frame, MapMarkers.Waypoint) + "  ·  " + distance;
            if (meters != _shownWaypointMeters)
            {
                _shownWaypointMeters = meters;
                _waypointLabel.text = distance;
            }
        }

        private bool HasArtilleryAccess()
        {
            var player = _data.Player;
            if (player is PlayerController controller && controller != null)
                return controller.CanCallArtillery;
            return true;
        }

        private static int ConfiguredTeamSize()
        {
            if (GameContext.TryGet<MatchConfig>(out var config) && config != null)
                return Mathf.Max(0, config.TeamSize);
            return 0;
        }

        private static string OrderName(SquadOrder order)
        {
            switch (order)
            {
                case SquadOrder.HoldPosition: return "MEVZİDE KAL";
                case SquadOrder.Attack: return UiTheme.Colorize("TAARRUZ", AttackColor);
                case SquadOrder.Regroup: return "TOPLAN";
                default: return "BENİ TAKİP ET";
            }
        }

        // ================================================================== Girdi

        private void HandleKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.lKey.wasPressedThisFrame && _legendOverlay != null)
            {
                _legendShown = !_legendShown;
                _legendOverlay.gameObject.SetActive(_legendShown);
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                CenterOn(_data.Frame.WorldToUV(_data.PlayerPosition));
            }

            if (_zoom > 1.001f)
            {
                var pan = Vector2.zero;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                    pan.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                    pan.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                    pan.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                    pan.x -= 1f;
                if (pan != Vector2.zero)
                {
                    _focus += pan.normalized * (KeyboardPanSpeed / _zoom * Time.unscaledDeltaTime);
                    _viewDirty = true;
                }
            }

            if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame)
                ZoomAround(_zoom * ZoomStep, ViewportCenterUv());
            if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame)
                ZoomAround(_zoom / ZoomStep, ViewportCenterUv());
        }

        private void OnMapClicked(PointerEventData eventData)
        {
            if (!IsOpen || eventData == null)
                return;

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (_measureActive)
                {
                    _measureActive = false;
                    _shownMeasureKey = int.MinValue;
                    UiWidgets.PlaySound(SoundId.UiClick);
                    return;
                }

                if (MapMarkers.HasWaypoint)
                {
                    MapMarkers.ClearWaypoint();
                    if (_data.Player is PlayerController controller && controller != null)
                        controller.ClearMapMarker();
                    UiWidgets.PlaySound(SoundId.UiClick);
                    _shownMarkerKey = int.MinValue;
                    MarkerCleared?.Invoke();
                }

                return;
            }

            if (eventData.button == PointerEventData.InputButton.Middle)
                return; // Radyal ping menüsü UpdateRadial içinde yönetilir.

            if (eventData.clickCount >= 2 && !IsDangerModifierHeld())
            {
                // Çift tık: hızlı "Gidiyorum" pingi (işaret ilk tıkta konmuştu).
                if (ScreenToContentUv(eventData.position, eventData.pressEventCamera, out var quickUv))
                {
                    var quickWorld = WorldAtUv(ClampUv(quickUv));
                    if (MapMath.IsFinite(quickWorld))
                    {
                        _waypointPulse = 1f;
                        PlacePing(quickWorld, false);
                        UiWidgets.PlaySound(SoundId.RadioBeep);
                    }
                }

                return;
            }

            if (!ScreenToContentUv(eventData.position, eventData.pressEventCamera, out var uv))
                return;

            uv = ClampUv(uv);
            var world = _data.Frame.UVToWorld(uv);
            var metadata = _data.World;
            if (metadata != null)
            {
                try
                {
                    world.y = metadata.SampleGroundHeight(world);
                }
                catch (Exception)
                {
                    world.y = 0f;
                }
            }

            if (!MapMath.IsFinite(world))
                return;

            if (IsDangerModifierHeld())
            {
                PlacePing(world, true);
                UiWidgets.PlaySound(SoundId.RadioBeep);
                return;
            }

            MapMarkers.SetWaypoint(world);
            _waypointPulse = 1f;
            _shownMarkerKey = int.MinValue;
            UiWidgets.PlaySound(SoundId.UiConfirm);
            PointMarked?.Invoke(world);
        }

        private static bool IsDangerModifierHeld()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed
                                        || keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
        }

        /// <summary>Tim ping panosuna yazar (düşmansa botları da bilgilendirir); dünya HUD'u görünür olur.</summary>
        private void PlacePing(Vector3 world, bool isEnemy)
        {
            var self = _data.Self;
            if (self == null || !self.IsAlive || !MapMath.IsFinite(world))
                return;

            var now = Time.time;
            PingBoard.Place(self.Team, world, isEnemy, now);
            PingWorldView.EnsureExists();

            if (isEnemy && GameContext.HasAuthority)
            {
                var director = BotDirector.Instance;
                if (director != null)
                    director.ReportPing(self.Team, world, now);
            }
        }

        private void OnMapDragStarted(PointerEventData eventData)
        {
            if (!IsOpen || eventData == null || eventData.button != PointerEventData.InputButton.Right)
                return;

            if (!ScreenToContentUv(eventData.pressPosition, eventData.pressEventCamera, out var uv))
                return;

            _measuring = true;
            _measureActive = true;
            _measureStartUv = ClampUv(uv);
            _measureEndUv = _measureStartUv;
            _shownMeasureKey = int.MinValue;
        }

        private void OnMapDragEnded(PointerEventData eventData)
        {
            _measuring = false;
        }

        private void OnMapDragged(PointerEventData eventData)
        {
            if (!IsOpen || eventData == null)
                return;

            if (_measuring)
            {
                if (ScreenToContentUv(eventData.position, eventData.pressEventCamera, out var end))
                    _measureEndUv = ClampUv(end);
                return;
            }

            if (_zoom <= 1.001f)
                return;

            var scale = CanvasScale();
            var delta = eventData.delta / scale;
            _focus -= delta / Mathf.Max(1f, _mapPixels);
            _viewDirty = true;
        }

        private void OnMapScrolled(PointerEventData eventData)
        {
            if (!IsOpen || eventData == null)
                return;

            var scroll = eventData.scrollDelta.y;
            if (Mathf.Abs(scroll) < 0.01f)
                return;

            var pivot = ScreenToContentUv(eventData.position, eventData.enterEventCamera, out var uv) ? uv : ViewportCenterUv();
            ZoomAround(scroll > 0f ? _zoom * ZoomStep : _zoom / ZoomStep, pivot);
        }

        /// <summary>Yakınlaştırmayı değiştirir; <paramref name="pivotUv"/> imlecin altında sabit kalır.</summary>
        private void ZoomAround(float zoom, Vector2 pivotUv)
        {
            zoom = Mathf.Clamp(zoom, 1f, MaxZoom);
            if (Mathf.Approximately(zoom, _zoom))
                return;

            // İmlecin altındaki nokta ekranda sabit kalır.
            var oldPixels = _mapPixels;
            _zoom = zoom;
            _focus = MapMath.ZoomFocus(_focus, pivotUv, oldPixels, ViewportSize * _zoom);
            _viewDirty = true;
        }

        private void CenterOn(Vector2 uv)
        {
            if (float.IsNaN(uv.x) || float.IsNaN(uv.y))
                return;
            _focus = uv;
            _viewDirty = true;
        }

        private void ClampFocus()
        {
            _focus = MapMath.ClampFocus(_focus, _zoom);
        }

        private Vector2 ViewportCenterUv() => _focus;

        private bool TryGetPointerUv(out Vector2 uv)
        {
            uv = default;
            var mouse = Mouse.current;
            if (mouse == null)
                return false;
            return ScreenToContentUv(mouse.position.ReadValue(), null, out uv);
        }

        private bool ScreenToContentUv(Vector2 screen, Camera eventCamera, out Vector2 uv)
        {
            uv = default;
            if (_content == null || _mapPixels <= 0f)
                return false;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_content, screen, eventCamera, out var local))
                return false;

            // İçerik pivotu sol-alt: yerel nokta 0..mapPixels.
            uv = local / _mapPixels;
            return !(float.IsNaN(uv.x) || float.IsNaN(uv.y));
        }

        private float CanvasScale()
        {
            if (_rootCanvas == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                _rootCanvas = canvas != null ? canvas.rootCanvas : null;
            }

            var scale = _rootCanvas != null ? _rootCanvas.scaleFactor : 1f;
            return scale > 0.0001f ? scale : 1f;
        }

        // ================================================================== Yardımcılar

        private static Vector2 ClampUv(Vector2 uv)
        {
            return new Vector2(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y));
        }

        private static void PlaceAtUv(RectTransform rt, Vector2 uv)
        {
            rt.anchorMin = uv;
            rt.anchorMax = uv;
            rt.anchoredPosition = Vector2.zero;
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

        private void OnDisable()
        {
            // Nesne kapatılırsa/yok edilirse imleç ve girdi durumu geri yüklenir.
            if (IsOpen)
            {
                IsOpen = false;
                if (_panel != null)
                    _panel.gameObject.SetActive(false);
                MapOverlayInput.Release(this);
            }
        }

        private void OnDestroy()
        {
            MapOverlayInput.Release(this);
            PointMarked = null;
            MarkerCleared = null;
        }
    }
}
