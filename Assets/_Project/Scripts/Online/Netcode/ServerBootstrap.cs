using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.AI;
using Project.Online.Backend;
using Project.Presentation.Bootstrap;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Online.Netcode
{
    /// <summary>
    /// Dedicated server girişi.
    /// Komut satırı: <c>-server -port -region -backend -serverKey -maxPlayers -map -matchId -serverId</c>.
    /// Grafik/ses kapalı, hedef 30 Hz; backend kayıt + heartbeat + maç sonucu.
    /// </summary>
    public sealed class ServerBootstrap : MonoBehaviour
    {
        public const int DefaultPort = 7777;
        public const int DefaultMaxPlayers = 60;
        public const int DefaultTickRate = 30;
        public const float HeartbeatIntervalSeconds = 10f;

        private NetcodeNetworkSession _session;
        private DedicatedServerBackend _backend;
        private ServerArgs _args;
        private float _nextHeartbeat;
        private Guid _activeMatchId;
        private string _status = "idle";
        private bool _started;

        public static ServerBootstrap Instance { get; private set; }
        public NetcodeNetworkSession Session => _session;
        public DedicatedServerBackend Backend => _backend;
        public bool IsRunning => _started;
        public Guid ActiveMatchId => _activeMatchId;

        /// <summary>
        /// <c>-server</c> varsa bootstrap nesnesini oluşturur ve Netcode sunucusunu başlatır.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!TryParseArgs(out var args) || !args.IsServer)
                return;

            if (Instance != null)
                return;

            var go = new GameObject("ServerBootstrap");
            DontDestroyOnLoad(go);
            var bootstrap = go.AddComponent<ServerBootstrap>();
            bootstrap.Begin(args);
        }

        public static bool TryParseArgs(out ServerArgs args)
        {
            args = default;
            var raw = Environment.GetCommandLineArgs() ?? Array.Empty<string>();
            var isServer = HasFlag(raw, "-server");
#if UNITY_SERVER
            isServer = true;
#endif
            if (!isServer && UnityEngine.Application.isBatchMode)
                isServer = true;

            if (!isServer)
                return false;

            args = new ServerArgs
            {
                IsServer = true,
                Port = (ushort)GetInt(raw, "-port", DefaultPort),
                Region = GetString(raw, "-region", "tr"),
                BackendUrl = GetString(raw, "-backend", string.Empty),
                ServerKey = GetString(raw, "-serverKey", string.Empty),
                MaxPlayers = GetInt(raw, "-maxPlayers", DefaultMaxPlayers),
                BindAddress = GetString(raw, "-bind", "0.0.0.0"),
                TickRate = GetInt(raw, "-tickrate", DefaultTickRate),
                Map = GetString(raw, "-map", string.Empty),
                MatchId = ParseGuid(GetString(raw, "-matchId", string.Empty)),
                ServerId = ParseGuid(GetString(raw, "-serverId", string.Empty))
            };

            if (args.Port == 0)
                args.Port = DefaultPort;
            if (args.MaxPlayers <= 0)
                args.MaxPlayers = DefaultMaxPlayers;
            if (args.TickRate < 10 || args.TickRate > 120)
                args.TickRate = DefaultTickRate;

            return true;
        }

        public void Begin(ServerArgs args)
        {
            if (_started)
                return;

            Instance = this;
            _args = args;
            _started = true;
            if (args.MatchId != Guid.Empty)
            {
                _activeMatchId = args.MatchId;
                Debug.Log("[ServerBootstrap] Backend maç kimliği (-matchId): " + args.MatchId);
            }

            if (!string.IsNullOrEmpty(args.Map))
            {
                GameSession.SetMapOverride(args.Map);
                Debug.Log($"[ServerBootstrap] Harita={GameSession.SelectedMap}, sahne={GameSession.ResolveSceneForMap(args.Map)}.");
            }
            ConfigureHeadlessProcess(args.TickRate);
            ServerBotGate.SetServerAuthoritative(true);
            BotRuntimeGate.ShouldRunBots = () => ServerBotGate.ShouldRunBots;
            GameSession.MatchStarting += OnGameMatchStarting;
            GameSession.MatchFinished += OnGameMatchFinished;
            GameSession.MatchSummaryReady += OnMatchSummaryReady;

            _session = new NetcodeNetworkSession(PlayerId.Invalid);
            _session.ConfigureEndpoint(args.BindAddress, args.Port, args.MaxPlayers);
            _session.StartServer();

            if (!string.IsNullOrEmpty(args.BackendUrl) && !string.IsNullOrEmpty(args.ServerKey))
            {
                _backend = new DedicatedServerBackend(args.BackendUrl, args.ServerKey);
                _validator = new ServerIdentityValidator(args.BackendUrl);
                _session.IdentityApprover = ApproveIdentityAsync;
                if (NetworkManager.Singleton != null)
                    NetworkManager.Singleton.OnClientDisconnectCallback += OnClientGone;
                _ = RegisterAndHeartbeatLoop();
            }
            else
            {
                Debug.LogWarning("[ServerBootstrap] -backend / -serverKey yok — backend kaydı yapılmayacak.");
            }

            Debug.Log($"[ServerBootstrap] Dedicated server hazır — port={args.Port}, region={args.Region}, max={args.MaxPlayers}, tick={args.TickRate}Hz.");
        }

        public void NotifyMatchStarted(Guid matchId)
        {
            _activeMatchId = matchId;
            _status = "in_match";
            _ = HeartbeatNow();
        }

        public void NotifyMatchIdle()
        {
            _activeMatchId = Guid.Empty;
            _status = "idle";
            _ = HeartbeatNow();
        }

        /// <summary>Maç bitince backend'e sonuç gönderir; ardından idle'a döner.</summary>
        public async Task<bool> SubmitMatchResultAndIdleAsync(Guid matchId, string matchResultJson)
        {
            var ok = false;
            if (_backend != null && matchId != Guid.Empty)
                ok = await _backend.SubmitMatchResultAsync(matchId, matchResultJson);

            NotifyMatchIdle();
            return ok;
        }

        private async Task RegisterAndHeartbeatLoop()
        {
            try
            {
                var registered = await _backend.RegisterAsync(_args.BindAddress, _args.Port, _args.Region, _args.MaxPlayers);
                if (!registered)
                    Debug.LogWarning("[ServerBootstrap] Backend kaydı başarısız — heartbeat yine de denenecek değil.");

                _nextHeartbeat = Time.unscaledTime + 1f;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void Update()
        {
            if (!_started)
                return;

            if (_backend != null && _backend.IsRegistered && Time.unscaledTime >= _nextHeartbeat)
            {
                _nextHeartbeat = Time.unscaledTime + HeartbeatIntervalSeconds;
                _ = HeartbeatNow();
            }

            // Hedef kare hızını koru
            if (UnityEngine.Application.targetFrameRate != _args.TickRate)
                UnityEngine.Application.targetFrameRate = _args.TickRate;
        }

        private async Task HeartbeatNow()
        {
            if (_backend == null || !_backend.IsRegistered)
                return;

            var players = 0;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                players = NetworkManager.Singleton.ConnectedClientsIds.Count;

            try
            {
                await _backend.HeartbeatAsync(players, _status);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ServerBootstrap] Heartbeat hata: " + e.Message);
            }
        }

        private static void ConfigureHeadlessProcess(int tickRate)
        {
            try
            {
                QualitySettings.vSyncCount = 0;
                UnityEngine.Application.targetFrameRate = tickRate;
                UnityEngine.Application.runInBackground = true;
                AudioListener.volume = 0f;
                AudioListener.pause = true;

#if !UNITY_EDITOR
                // Grafik yükünü düşür (batchmode / nographics ile birlikte)
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    QualitySettings.SetQualityLevel(0, true);
                    QualitySettings.shadows = ShadowQuality.Disable;
                    QualitySettings.antiAliasing = 0;
                }
#endif
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnGameMatchStarting(MatchConfig config)
        {
            _summarySubmitted = false;
            // Backend maç kimliği tahsis edilmediyse heartbeat yine de in_match durumuna geçer.
            NotifyMatchStarted(_activeMatchId);
        }

        private readonly IdentityRegistry _identities = new IdentityRegistry();
        private readonly Dictionary<int, string> _squadIds = new Dictionary<int, string>();
        private readonly Dictionary<ulong, int> _playerByClient = new Dictionary<ulong, int>();
        private ServerIdentityValidator _validator;
        private bool _summarySubmitted;

        public IdentityRegistry Identities => _identities;

        /// <summary>Oyuncunun (PlayerId.Value) backend profil kimliğini eşler; sonuçta playerId olarak gönderilir.</summary>
        public void RegisterPlayerProfile(int playerId, Guid profileId) => _identities.TryBind(playerId, profileId, null, out _);

        /// <summary>Timin (tim dizini) backend takım (squad) kimliğini eşler.</summary>
        public void RegisterTeamSquad(int team, Guid squadId)
        {
            if (squadId != Guid.Empty)
                _squadIds[team] = squadId.ToString("D");
        }

        /// <summary>ConnectionApproval: JWT/bilet backend ile doğrulanır; geçerliyse profil + squad eşlenir.</summary>
        private async Task<(bool ok, string reason)> ApproveIdentityAsync(ulong clientId, ConnectionIdentity.Payload payload)
        {
            var r = await _validator.ValidateAsync(payload);
            if (!r.Ok)
                return (false, r.Reason);

            if (!_identities.TryBind(payload.PlayerId, r.Profile.Id, r.Profile.SquadId, out var reason))
                return (false, reason);

            _playerByClient[clientId] = payload.PlayerId;
            Debug.Log($"[ServerBootstrap] Kimlik doğrulandı: {r.Profile.Username} ({r.Profile.Id}) -> oyuncu {payload.PlayerId}.");
            return (true, null);
        }

        private void OnClientGone(ulong clientId)
        {
            if (_playerByClient.TryGetValue(clientId, out var pid))
            {
                _playerByClient.Remove(clientId);
                _identities.Release(pid);
            }
        }

        private void OnMatchSummaryReady(MatchSummary summary)
        {
            if (summary == null || _summarySubmitted)
                return;

            _summarySubmitted = true;
            var json = BuildMatchResultJson(summary, _identities, _squadIds, _validator != null);
            _ = SubmitMatchResultAndIdleAsync(_activeMatchId, json);
        }

        /// <summary>
        /// Tüm timlerin/oyuncuların gerçek sonuçlarını backend JSON'una çevirir. Profil eşlemesi olan oyuncular profil
        /// kimliğiyle yazılır. <paramref name="humansOnly"/> true ise (kimlik doğrulamalı sunucu) eşlenmemiş savaşanlar
        /// (botlar) sonuçtan çıkarılır; boş kalan timler atlanır. Squad: tim eşlemesi, yoksa üyenin squad'ı, yoksa tim dizini.
        /// </summary>
        public static string BuildMatchResultJson(MatchSummary summary, IdentityRegistry identities,
            IReadOnlyDictionary<int, string> squadIds, bool humansOnly)
        {
            var teams = new List<MatchResultJson.TeamResult>();
            var groups = summary.GroupByTeam();
            for (var i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                var players = new List<MatchResultJson.PlayerResult>(g.Members.Count);
                string memberSquad = null;
                for (var m = 0; m < g.Members.Count; m++)
                {
                    var c = g.Members[m];
                    string pid;
                    if (identities != null && identities.TryGetProfile(c.Id.Value, out var mapped))
                    {
                        pid = mapped;
                        if (memberSquad == null && identities.TryGetSquad(c.Id.Value, out var msq))
                            memberSquad = msq;
                    }
                    else if (humansOnly)
                        continue; // bot / kimliksiz: backend'e gönderilmez
                    else
                        pid = "p" + c.Id.Value;

                    players.Add(new MatchResultJson.PlayerResult(pid, c.Kills, c.Headshots, c.DamageDealt, c.SurvivalSeconds));
                }

                if (players.Count == 0)
                    continue;

                var squad = squadIds != null && squadIds.TryGetValue(g.Team, out var sq) ? sq
                    : memberSquad ?? (g.Team >= 0 ? g.Team.ToString(CultureInfo.InvariantCulture) : (g.TeamName ?? string.Empty));
                teams.Add(new MatchResultJson.TeamResult(squad, g.Placement, players));
            }

            return MatchResultJson.Build(teams);
        }

        private void OnGameMatchFinished(MatchResult result)
        {
            // Dedicated sunucuda gerçek sonuç MatchSummaryReady ile gelir; bu yol yalnızca yerel host içindir.
            if (_summarySubmitted || (_session != null && _session.Role == NetworkRole.DedicatedServer))
                return;

            var player = new MatchResultJson.PlayerResult("local", result.Kills, result.Headshots,
                result.DamageDealt, result.SurvivalSeconds);
            var team = new MatchResultJson.TeamResult(result.TeamName ?? string.Empty, result.TeamPlacement,
                new List<MatchResultJson.PlayerResult> { player });
            var json = MatchResultJson.Build(new List<MatchResultJson.TeamResult> { team });
            _ = SubmitMatchResultAndIdleAsync(_activeMatchId, json);
        }

        private void OnDestroy()
        {
            GameSession.MatchStarting -= OnGameMatchStarting;
            GameSession.MatchFinished -= OnGameMatchFinished;
            GameSession.MatchSummaryReady -= OnMatchSummaryReady;
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientGone;
            if (BotRuntimeGate.ShouldRunBots != null && _started)
                BotRuntimeGate.ShouldRunBots = null;
            if (Instance == this)
                Instance = null;
            _session?.Disconnect();
        }

        /// <summary>Geçerli Guid ise döndürür, değilse (boş/bozuk) Guid.Empty.</summary>
        public static Guid ParseGuid(string text)
            => !string.IsNullOrWhiteSpace(text) && Guid.TryParse(text.Trim().Trim('"'), out var g) ? g : Guid.Empty;

        private static bool HasFlag(string[] args, string name)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string GetString(string[] args, string name, string fallback)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(args[i + 1])
                    && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
                    return args[i + 1];
            }

            return fallback;
        }

        private static int GetInt(string[] args, string name, int fallback)
        {
            var text = GetString(args, name, null);
            return text != null && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;
        }

        public struct ServerArgs
        {
            public bool IsServer;
            public ushort Port;
            public string Region;
            public string BackendUrl;
            public string ServerKey;
            public int MaxPlayers;
            public string BindAddress;
            public int TickRate;
            public string Map;
            /// <summary>Backend'in tahsis ettiği maç kimliği (<c>-matchId</c>); yoksa Guid.Empty.</summary>
            public Guid MatchId;
            public Guid ServerId;
        }
    }
}
