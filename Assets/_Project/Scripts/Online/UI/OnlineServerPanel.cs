using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Online.Bootstrap;
using Project.Online.Lan;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Online.UI
{
    /// <summary>
    /// SUNUCU KUR / KATIL / LAN sekmeleri + bağlıyken lobi (oyuncu, ping, hazır).
    /// Netcode paketi yoksa (köprü kayıtsız) "Netcode paketi gerekli" bilgisi gösterir; LAN taraması yine çalışır.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OnlineServerPanel : MonoBehaviour
    {
        private enum Mode { Host, Join, Lan, Lobby }

        private const float WindowWidth = 700f;
        private const float WindowHeight = 720f;
        private const float RowH = 40f;
        private const float ConnectTimeout = 12f;
        private const string PrefRecent = "harekat.online.recent";
        private const string PrefPort = "harekat.online.hostport";

        private static readonly int[] MaxPlayerChoices = { 2, 4, 10, 20, 40, 60 };

        private Action _onClosed;
        private bool _closed;
        private CanvasGroup _group;
        private float _fade;
        private RectTransform _content;
        private Text _status;
        private Mode _mode = Mode.Host;
        private Mode _lastTab = Mode.Host;

        // Host seçenekleri (panel ömrü boyunca)
        private string _portText = "7777";
        private int _maxIndex = 2;
        private int _mapIndex;
        private bool _fillBots = true;

        // Katıl
        private string _addressText = "";
        private bool _connecting;
        private float _connectStart;
        private string _connectingEndpoint;
        private bool _wasConnected;

        // LAN
        private LanDiscovery _lan;
        private Text _lanInfo;
        private RectTransform _lanList;
        private float _lanRefresh;

        // Lobi
        private RectTransform _rosterList;
        private Text _rosterSummary;
        private Text _pingLabel;
        private float _rosterRefresh;

        public bool IsOpen => !_closed && this != null;

        public static OnlineServerPanel Show(Transform parent, Action onClosed = null)
        {
            OnlineServices.Ensure();
            var root = OnlineUi.CreateDimRoot("[Online Sunucu]", parent, out var group);
            var panel = root.gameObject.AddComponent<OnlineServerPanel>();
            panel._onClosed = onClosed;
            panel._group = group;
            panel.Build(root);
            return panel;
        }

        public void Close()
        {
            if (_closed)
                return;
            _closed = true;
            DisposeLan();
            ClearSelection();
            var cb = _onClosed;
            _onClosed = null;
            gameObject.SetActive(false);
            UiFactory.DestroySafe(gameObject);
            if (cb == null)
                return;
            try { cb(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void OnDestroy() => DisposeLan();

        private void DisposeLan()
        {
            _lan?.Dispose();
            _lan = null;
        }

        // ---------- yapı ----------

        private void Build(RectTransform root)
        {
            var window = OnlineUi.CreateWindow(root, WindowWidth, WindowHeight, "SUNUCU / KATIL");
            var body = OnlineUi.CreateBody(window, 110f, 100f);

            try { _portText = PlayerPrefs.GetInt(PrefPort, 7777).ToString(); }
            catch (Exception) { /* PlayerPrefs yok */ }

            if (!OnlineSessionBridge.NetcodeAvailable)
            {
                var warn = UiFactory.Label(body, "Netcode paketi gerekli — sunucu kurma ve katılma kapalı. LAN taraması yine çalışır.",
                    UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.Amber);
                warn.horizontalOverflow = HorizontalWrapMode.Wrap;
                UiFactory.LayoutSize(warn, -1f, 48f, 1f);
            }

            var tabs = UiFactory.HorizontalList(body, 10f, 0, TextAnchor.MiddleCenter);
            UiFactory.LayoutSize(tabs, -1f, 48f, 1f);
            AddTab(tabs, "SUNUCU KUR", Mode.Host);
            AddTab(tabs, "KATIL", Mode.Join);
            AddTab(tabs, "LAN", Mode.Lan);

            _content = UiFactory.VerticalList(body, 10f, 0, TextAnchor.UpperCenter);
            UiFactory.LayoutSize(_content, -1f, -1f, 1f, 1f);
            var v = _content.GetComponent<VerticalLayoutGroup>();
            if (v != null)
            {
                v.childForceExpandWidth = true;
                v.childForceExpandHeight = false;
            }

            _status = OnlineUi.StatusLabel(body);

            OnlineUi.CreateFooter(window, out _, Close, "GERİ");

            SetMode(OnlineSessionBridge.IsConnected != null && OnlineSessionBridge.IsConnected() ? Mode.Lobby : Mode.Host);
        }

        private void AddTab(Transform parent, string label, Mode mode)
        {
            var b = UiFactory.Button(parent, label, () => OnTab(mode), UiButtonStyle.Default);
            UiFactory.LayoutSize(b, -1f, 48f, 1f);
        }

        private void OnTab(Mode mode)
        {
            if (_mode == Mode.Lobby)
                return; // bağlıyken sekme yok; önce BAĞLANTIYI KES
            SetMode(mode);
        }

        private void SetMode(Mode mode)
        {
            _mode = mode;
            if (mode != Mode.Lobby)
                _lastTab = mode;
            UiFactory.ClearChildren(_content);
            _lanInfo = null; _lanList = null; _rosterList = null; _rosterSummary = null; _pingLabel = null;

            switch (mode)
            {
                case Mode.Host: BuildHost(); break;
                case Mode.Join: BuildJoin(); break;
                case Mode.Lan: BuildLan(); break;
                case Mode.Lobby: BuildLobby(); break;
            }

            if (mode != Mode.Lan && !(OnlineSessionBridge.IsConnected?.Invoke() ?? false))
            {
                // LAN sekmesi dışındayken dinlemeyi kapat (soket serbest kalsın)
                _lan?.StopListening();
            }
        }

        private Button Btn(Transform parent, string label, Action act, UiButtonStyle style = UiButtonStyle.Default, float h = 46f)
        {
            var b = UiFactory.Button(parent, label, act, style);
            UiFactory.LayoutSize(b, -1f, h, 1f);
            return b;
        }

        private Text Small(Transform parent, string text, Color color, float h = 28f)
        {
            var l = UiFactory.Label(parent, text, UiTheme.FontSmall, TextAnchor.MiddleLeft, color);
            UiFactory.LayoutSize(l, -1f, h, 1f);
            return l;
        }

        // ---------- SUNUCU KUR ----------

        private void BuildHost()
        {
            Small(_content, "PORT (1024-65535)", UiTheme.TextMuted, 24f);
            var port = OnlineUi.CreateField(_content, "7777", false, 46f);
            port.text = _portText;
            port.onValueChanged.AddListener(t => _portText = t);

            Btn(_content, "MAKS OYUNCU: " + MaxPlayerChoices[_maxIndex], () =>
            {
                _maxIndex = (_maxIndex + 1) % MaxPlayerChoices.Length;
                SetMode(Mode.Host);
            });

            Btn(_content, "HARİTA: " + MapCatalog.DisplayNames()[_mapIndex], () =>
            {
                _mapIndex = (_mapIndex + 1) % MapCatalog.Count;
                SetMode(Mode.Host);
            });

            var toggle = UiFactory.Toggle(_content, "Boş yerleri botla doldur", _fillBots, v => _fillBots = v);
            UiFactory.LayoutSize(toggle, -1f, 40f, 1f);

            var start = Btn(_content, "SUNUCUYU BAŞLAT", StartHost, UiButtonStyle.Primary, 54f);
            start.interactable = OnlineSessionBridge.NetcodeAvailable && !_connecting;
            Status(OnlineSessionBridge.NetcodeAvailable
                ? "Hazır. Aynı ağdaki oyuncular LAN listesinde seni görür."
                : "Netcode paketi gerekli.", UiTheme.TextDim);
        }

        private void StartHost()
        {
            if (!OnlineSessionBridge.NetcodeAvailable)
            {
                Status("Netcode paketi gerekli.", UiTheme.Warning);
                return;
            }

            if (!ServerEndpoint.TryParsePort(_portText, out var port))
            {
                Status("Port geçersiz: 1024 ile 65535 arası olmalı.", UiTheme.AccentLight);
                return;
            }

            try { PlayerPrefs.SetInt(PrefPort, port); }
            catch (Exception) { }

            var name = ProfileName();
            var max = MaxPlayerChoices[_maxIndex];
            var map = MapCatalog.IdAt(_mapIndex);
            var err = OnlineSessionBridge.StartHost(new HostOptions(name, port, max, _fillBots, map));
            if (err != null)
            {
                Status("Sunucu başlatılamadı: " + ServerEndpoint.FriendlyError(err), UiTheme.AccentLight);
                return;
            }

            LanBeaconHost.Begin(() =>
            {
                var n = OnlineSessionBridge.Roster?.Invoke()?.Count ?? 1;
                return new LanBeacon(name, port, map, n, max);
            });
            _wasConnected = true;
            SetMode(Mode.Lobby);
            Status("Sunucu dinliyor, port " + port + ".", UiTheme.Success);
        }

        // ---------- KATIL ----------

        private void BuildJoin()
        {
            Small(_content, "SUNUCU ADRESİ (IP:port)", UiTheme.TextMuted, 24f);
            var field = OnlineUi.CreateField(_content, "192.168.1.20:7777", false, 46f);
            field.text = _addressText;
            field.onValueChanged.AddListener(t => _addressText = t);

            var join = Btn(_content, _connecting ? "BAĞLANIYOR…" : "KATIL", () => TryJoin(_addressText), UiButtonStyle.Primary, 54f);
            join.interactable = OnlineSessionBridge.NetcodeAvailable && !_connecting;

            if (_connecting)
                Btn(_content, "İPTAL", CancelConnect, UiButtonStyle.Danger, 42f);

            Small(_content, "SON SUNUCULAR", UiTheme.Amber, 28f);
            var recent = RecentServers.Parse(ReadRecent());
            if (recent.Count == 0)
                Small(_content, "Henüz yok.", UiTheme.TextMuted);
            for (var i = 0; i < recent.Count && i < 5; i++)
            {
                var ep = recent[i];
                Btn(_content, ep, () => { _addressText = ep; TryJoin(ep); }, UiButtonStyle.Ghost, 38f)
                    .interactable = OnlineSessionBridge.NetcodeAvailable && !_connecting;
            }

            Status(OnlineSessionBridge.NetcodeAvailable ? "Adres gir veya listeden seç." : "Netcode paketi gerekli.", UiTheme.TextDim);
        }

        private void TryJoin(string raw)
        {
            if (_connecting)
                return;
            if (!OnlineSessionBridge.NetcodeAvailable)
            {
                Status("Netcode paketi gerekli.", UiTheme.Warning);
                return;
            }

            if (!ServerEndpoint.TryParse(raw, out var host, out var port, out var error))
            {
                Status(error, UiTheme.AccentLight);
                return;
            }

            var err = OnlineSessionBridge.StartClient(host, port);
            if (err != null)
            {
                Status(ServerEndpoint.FriendlyError(err), UiTheme.AccentLight);
                return;
            }

            _connecting = true;
            _connectStart = Time.unscaledTime;
            _connectingEndpoint = ServerEndpoint.Format(host, port);
            SetMode(Mode.Join);
            Status(_connectingEndpoint + " adresine bağlanılıyor…", UiTheme.Amber);
        }

        private void CancelConnect()
        {
            _connecting = false;
            OnlineSessionBridge.Stop?.Invoke();
            SetMode(Mode.Join);
            Status("Bağlantı iptal edildi.", UiTheme.TextDim);
        }

        private static string ReadRecent()
        {
            try { return PlayerPrefs.GetString(PrefRecent, ""); }
            catch (Exception) { return ""; }
        }

        private void RememberServer(string endpoint)
        {
            try
            {
                PlayerPrefs.SetString(PrefRecent, RecentServers.Add(ReadRecent(), endpoint));
                PlayerPrefs.Save();
            }
            catch (Exception) { /* tercih yazılamadı: önemli değil */ }
        }

        // ---------- LAN ----------

        private void BuildLan()
        {
            _lan ??= new LanDiscovery();
            var ok = _lan.StartListening();
            _lanInfo = Small(_content, ok ? "Aranıyor… (liste 3 sn'de bir tazelenir)" : "LAN dinlenemedi: " + _lan.LastError,
                ok ? UiTheme.TextDim : UiTheme.AccentLight);
            _lanList = UiFactory.VerticalList(_content, 6f, 0, TextAnchor.UpperCenter);
            UiFactory.LayoutSize(_lanList, -1f, -1f, 1f, 1f);
            var v = _lanList.GetComponent<VerticalLayoutGroup>();
            if (v != null)
            {
                v.childForceExpandWidth = true;
                v.childForceExpandHeight = false;
            }

            RefreshLanList();
            Status("Aynı ağdaki sunucular burada görünür.", UiTheme.TextDim);
        }

        private void RefreshLanList()
        {
            if (_lanList == null || _lan == null)
                return;
            UiFactory.ClearChildren(_lanList);
            var servers = _lan.Servers.Snapshot();
            if (servers.Count == 0)
            {
                Small(_lanList, "Sunucu bulunamadı.", UiTheme.TextMuted);
                return;
            }

            for (var i = 0; i < servers.Count && i < 7; i++)
            {
                var e = servers[i];
                var b = Btn(_lanList, LobbyRules.ServerLine(e), () =>
                {
                    _addressText = e.Endpoint;
                    SetMode(Mode.Join);
                    TryJoin(e.Endpoint);
                }, e.IsFull ? UiButtonStyle.Ghost : UiButtonStyle.Default, 42f);
                b.interactable = !e.IsFull && OnlineSessionBridge.NetcodeAvailable;
            }
        }

        // ---------- LOBİ ----------

        private void BuildLobby()
        {
            _rosterSummary = UiFactory.Label(_content, "", UiTheme.FontMedium, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold);
            UiFactory.LayoutSize(_rosterSummary, -1f, 36f, 1f);
            _pingLabel = Small(_content, "", UiTheme.TextDim);
            _rosterList = UiFactory.VerticalList(_content, 4f, 0, TextAnchor.UpperCenter);
            UiFactory.LayoutSize(_rosterList, -1f, -1f, 1f, 1f);
            var v = _rosterList.GetComponent<VerticalLayoutGroup>();
            if (v != null)
            {
                v.childForceExpandWidth = true;
                v.childForceExpandHeight = false;
            }

            Btn(_content, "BAĞLANTIYI KES", Disconnect, UiButtonStyle.Danger, 50f);
            RefreshRoster();
        }

        private void RefreshRoster()
        {
            if (_rosterList == null)
                return;
            var players = LobbyRules.Sort(OnlineSessionBridge.Roster?.Invoke());
            var max = OnlineSessionBridge.HostedMaxPlayers > 0 ? OnlineSessionBridge.HostedMaxPlayers : Math.Max(players.Count, 1);
            if (_rosterSummary != null)
                _rosterSummary.text = LobbyRules.Summary(players, max);

            if (_pingLabel != null)
            {
                var ping = OnlineSessionBridge.LocalPingMs?.Invoke() ?? -1;
                _pingLabel.text = "Bağlantı: " + (_connectingEndpoint ?? "yerel sunucu") + "  ·  ping " + LobbyRules.PingText(ping);
                _pingLabel.color = PingColor(LobbyRules.Quality(ping));
            }

            UiFactory.ClearChildren(_rosterList);
            for (var i = 0; i < players.Count && i < 10; i++)
                AddRosterRow(players[i]);
            if (players.Count > 10)
                Small(_rosterList, "+" + (players.Count - 10) + " oyuncu daha", UiTheme.TextMuted, 26f);
        }

        private void AddRosterRow(LobbyPlayer p)
        {
            var row = UiFactory.HorizontalList(_rosterList, 10f, 0, TextAnchor.MiddleLeft);
            UiFactory.LayoutSize(row, -1f, 32f, 1f);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            if (h != null)
            {
                h.childForceExpandWidth = false;
                h.childControlWidth = true;
            }

            var name = UiFactory.Label(row, (p.IsHost ? "★ " : "") + p.Name, UiTheme.FontSmall, TextAnchor.MiddleLeft,
                p.IsHost ? UiTheme.Amber : UiTheme.Text);
            UiFactory.LayoutSize(name, -1f, 32f, 1f);
            var ping = UiFactory.Label(row, LobbyRules.PingText(p.PingMs), UiTheme.FontSmall, TextAnchor.MiddleRight,
                PingColor(LobbyRules.Quality(p.PingMs)));
            UiFactory.LayoutSize(ping, 100f, 32f);
            var ready = UiFactory.Label(row, LobbyRules.ReadyText(p.Ready), UiTheme.FontSmall, TextAnchor.MiddleRight,
                p.Ready ? UiTheme.Success : UiTheme.TextMuted, FontStyle.Bold);
            UiFactory.LayoutSize(ready, 120f, 32f);
        }

        private static Color PingColor(LobbyRules.PingQuality q) => q switch
        {
            LobbyRules.PingQuality.Good => UiTheme.Success,
            LobbyRules.PingQuality.Ok => UiTheme.Amber,
            LobbyRules.PingQuality.Bad => UiTheme.AccentLight,
            _ => UiTheme.TextMuted,
        };

        private void Disconnect()
        {
            OnlineSessionBridge.Stop?.Invoke();
            LanBeaconHost.End();
            _connecting = false;
            _wasConnected = false;
            _connectingEndpoint = null;
            SetMode(_lastTab == Mode.Lobby ? Mode.Host : _lastTab);
            Status("Bağlantı kesildi.", UiTheme.TextDim);
        }

        // ---------- döngü ----------

        private void Status(string msg, Color color) => OnlineUi.SetStatus(_status, msg, color);

        private static string ProfileName()
        {
            try
            {
                var p = OnlineServices.Client != null ? OnlineServices.Client.CurrentPlayer : null;
                if (p != null && !string.IsNullOrWhiteSpace(p.Username))
                    return p.Username;
            }
            catch (Exception) { }
            return "Komutan";
        }

        private void Update()
        {
            if (_closed)
                return;

            if (_group != null && _fade < 1f)
            {
                _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / UiTheme.FadeDuration);
                _group.alpha = _fade;
            }

            var now = Time.realtimeSinceStartupAsDouble;
            var up = OnlineSessionBridge.IsConnected != null && OnlineSessionBridge.IsConnected();

            if (_connecting)
            {
                if (up)
                {
                    _connecting = false;
                    _wasConnected = true;
                    RememberServer(_connectingEndpoint);
                    SetMode(Mode.Lobby);
                    Status("Bağlandı: " + _connectingEndpoint, UiTheme.Success);
                }
                else if (!string.IsNullOrEmpty(OnlineSessionBridge.LastDisconnectReason))
                {
                    FailConnect(ServerEndpoint.FriendlyError(OnlineSessionBridge.LastDisconnectReason));
                }
                else if (Time.unscaledTime - _connectStart > ConnectTimeout)
                {
                    FailConnect("Bağlantı zaman aşımına uğradı. Adres/port ve güvenlik duvarını kontrol et.");
                }
            }
            else if (_wasConnected && !up && _mode == Mode.Lobby)
            {
                _wasConnected = false;
                var why = OnlineSessionBridge.LastDisconnectReason;
                SetMode(_lastTab == Mode.Lobby ? Mode.Host : _lastTab);
                Status(string.IsNullOrEmpty(why) ? "Sunucu bağlantısı koptu." : ServerEndpoint.FriendlyError(why), UiTheme.AccentLight);
            }

            if (_mode == Mode.Lan && _lan != null)
            {
                var changed = _lan.Tick(now);
                _lanRefresh -= Time.unscaledDeltaTime;
                if (changed || _lanRefresh <= 0f)
                {
                    _lanRefresh = 3f;
                    RefreshLanList();
                    if (_lanInfo != null && _lan.Listening)
                        _lanInfo.text = _lan.Servers.Count + " sunucu bulundu (3 sn tarama)";
                }
            }

            if (_mode == Mode.Lobby)
            {
                _rosterRefresh -= Time.unscaledDeltaTime;
                if (_rosterRefresh <= 0f)
                {
                    _rosterRefresh = 0.5f;
                    RefreshRoster();
                }
            }
        }

        private void FailConnect(string message)
        {
            _connecting = false;
            OnlineSessionBridge.Stop?.Invoke();
            OnlineSessionBridge.LastDisconnectReason = null;
            SetMode(Mode.Join);
            Status(message, UiTheme.AccentLight);
        }

        private void Start()
        {
            var es = EventSystem.current;
            if (es != null)
                es.SetSelectedGameObject(null);
        }

        private void ClearSelection()
        {
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null &&
                es.currentSelectedGameObject.transform.IsChildOf(transform))
                es.SetSelectedGameObject(null);
        }
    }
}
