using System;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure.AI;
using Project.Infrastructure.Combat;
using Project.Infrastructure.DI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Project.Online.Netcode
{
    /// <summary>
    /// Netcode for GameObjects adaptörü: Host / Client / DedicatedServer.
    /// Paket yokken bu assembly derlenmez (<c>HAREKAT_NETCODE</c> / versionDefines).
    /// </summary>
    public sealed class NetcodeNetworkSession : INetworkSession, IPlayerCommandSink, IPlayerActionSink, IWorldEffectRelay
    {
        public const ushort DefaultPort = 7777;
        public const int DefaultMaxPlayers = 60;
        public const float SimulationHz = 30f;

        /// <summary>Sunucu: kopan oyuncunun slotunu token ile geri talep için tutar.</summary>
        public static readonly Project.Online.Sim.ReconnectGraceTable Grace = new();
        /// <summary>İstemci: yeniden bağlanma durum makinesi (snapshot/JoinState alımı eşitlemeyi tamamlar).</summary>
        public static Project.Online.Sim.ReconnectFlow Flow { get; private set; } = new();

        private readonly System.Collections.Generic.Dictionary<ulong, string> _clientTokens = new();
        private readonly System.Collections.Generic.Dictionary<ulong, int> _claims = new();
        private bool _intentionalDisconnect;
        private bool _everConnected;
        private ReconnectDriver _driver;
        private readonly PlayerId _localPlayerId;
        private NetworkManager _manager;
        private NetworkRole _role = NetworkRole.Offline;
        private bool _connected;
        private string _lastAddress = "127.0.0.1";
        private ushort _port = DefaultPort;
        private int _maxPlayers = DefaultMaxPlayers;

        public NetcodeNetworkSession(PlayerId localPlayerId = default)
        {
            _localPlayerId = localPlayerId.IsValid ? localPlayerId : new PlayerId(0);
        }

        public NetworkRole Role => _role;
        public bool IsHost => _role is NetworkRole.Host or NetworkRole.Offline;
        public bool IsConnected => _connected || _role == NetworkRole.Offline;
        public bool HasAuthority => _role != NetworkRole.Client;
        public PlayerId LocalPlayerId => _localPlayerId;

        public ushort Port
        {
            get => _port;
            set => _port = value == 0 ? DefaultPort : value;
        }

        public int MaxPlayers
        {
            get => _maxPlayers;
            set => _maxPlayers = Mathf.Clamp(value <= 0 ? DefaultMaxPlayers : value, 2, 100);
        }

        public NetworkManager Manager => _manager;

        public void ConfigureEndpoint(string address, ushort port, int maxPlayers = DefaultMaxPlayers)
        {
            if (!string.IsNullOrWhiteSpace(address))
                _lastAddress = address.Trim();
            Port = port;
            MaxPlayers = maxPlayers;
        }

        public void StartHost()
        {
            EnsureManager();
            ApplyTransport(_lastAddress, _port);
            if (!_manager.StartHost())
            {
                Debug.LogError("[Netcode] Host başlatılamadı.");
                return;
            }

            _role = NetworkRole.Host;
            _connected = true;
            BotRuntimeGate.ShouldRunBots = () => ServerBotGate.ShouldRunBots;
            BindInterest();
            Debug.Log($"[Netcode] Host dinliyor {_lastAddress}:{_port} (max {_maxPlayers}).");
        }

        public void StartClient(string address)
        {
            if (!string.IsNullOrWhiteSpace(address))
                ParseAddress(address, out _lastAddress, out _port);

            EnsureManager();
            ApplyTransport(_lastAddress, _port);
            if (!_manager.StartClient())
            {
                Debug.LogError($"[Netcode] İstemci bağlanamadı: {_lastAddress}:{_port}");
                return;
            }

            _role = NetworkRole.Client;
            _connected = true;
            _intentionalDisconnect = false;
            BotRuntimeGate.ShouldRunBots = () => ServerBotGate.ShouldRunBots;
            Debug.Log($"[Netcode] İstemci bağlanıyor {_lastAddress}:{_port}.");
        }

        public void StartServer()
        {
            EnsureManager();
            ApplyTransport(_lastAddress, _port);
            if (!_manager.StartServer())
            {
                Debug.LogError("[Netcode] Dedicated server başlatılamadı.");
                return;
            }

            _role = NetworkRole.DedicatedServer;
            _connected = true;
            BindInterest();
            Debug.Log($"[Netcode] Dedicated server {_lastAddress}:{_port} (max {_maxPlayers}).");
        }

        public void Disconnect()
        {
            _intentionalDisconnect = true;
            _everConnected = false;
            Flow = new Project.Online.Sim.ReconnectFlow();
            ExplosionSystem.Exploded -= OnServerExploded;
            if (_manager != null && _manager.IsListening)
                _manager.Shutdown();

            _connected = false;
            _role = NetworkRole.Offline;
            BotRuntimeGate.ShouldRunBots = null;
        }

        public void Submit(PlayerId playerId, PlayerCommand command)
        {
            if (_manager == null || !_manager.IsListening)
                return;

            var relay = NetworkPlayer.FindLocal();
            if (relay == null)
                return;

            if (HasAuthority && _manager.IsServer)
            {
                relay.ServerApplyCommand(command);
                return;
            }

            relay.RecordLocalCommand(command);
            relay.SubmitCommandServerRpc(NetworkPlayerCommand.From(command));
        }

        public void SubmitShot(ShotRequest request)
        {
            var relay = NetworkPlayer.FindLocal();
            if (relay == null)
                return;

            if (HasAuthority && _manager != null && _manager.IsServer)
            {
                relay.ServerValidateAndSimulateShot(request);
                return;
            }

            var net = NetworkShotRequest.From(request);
            if (net.Tick == 0u)
                net.Tick = relay.EstimateRenderTick(); // gecikmeli render edilen hedef konumuna göre geri sarma tick'i
            relay.SubmitShotServerRpc(net);
        }

        // ------------------------------------------------------------ bomba / yakın dövüş (istemci → sunucu)
        public void SubmitThrow(ThrowRequest request)
        {
            if (HasAuthority || _manager == null || !_manager.IsListening)
                return;

            NetworkPlayer.FindLocal()?.SubmitThrowServerRpc(NetworkThrowRequest.From(request));
        }

        public void SubmitMelee(MeleeRequest request)
        {
            if (HasAuthority || _manager == null || !_manager.IsListening)
                return;

            NetworkPlayer.FindLocal()?.SubmitMeleeServerRpc(NetworkMeleeRequest.From(request));
        }

        // ------------------------------------------------------------ efekt yayını (sunucu → istemciler)
        public void BroadcastExplosion(Float3 position, float radius, PlayerId attackerId)
        {
            if (!IsServerProcess)
                return;

            NetworkPlayer.FindAnySpawned()?.ExplosionClientRpc(new Vector3(position.X, position.Y, position.Z), radius);
        }

        public void BroadcastArtilleryWhistle(Float3 position)
        {
            if (!IsServerProcess)
                return;

            NetworkPlayer.FindAnySpawned()?.ArtilleryWhistleClientRpc(new Vector3(position.X, position.Y, position.Z));
        }

        private bool IsServerProcess => HasAuthority && _manager != null && _manager.IsServer;

        private void OnServerExploded(Vector3 position, float radius)
        {
            BroadcastExplosion(new Float3(position.x, position.y, position.z), radius, PlayerId.Invalid);
        }

        private void BindExplosionRelay()
        {
            ExplosionSystem.Exploded -= OnServerExploded;
            ExplosionSystem.Exploded += OnServerExploded;
        }

        private void EnsureManager()
        {
            if (_manager != null)
                return;

            var existing = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
            if (existing != null)
            {
                _manager = existing;
                HookCallbacks();
                return;
            }

            var go = new GameObject("HarekatNetworkManager");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _manager = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();
            _manager.NetworkConfig.NetworkTransport = transport;
            _manager.NetworkConfig.TickRate = (uint)SimulationHz;
            _manager.NetworkConfig.ConnectionApproval = true;
            _manager.NetworkConfig.EnableSceneManagement = false;
            _manager.ConnectionApprovalCallback = Approval;
            HookCallbacks();
        }

        private void HookCallbacks()
        {
            if (_manager == null)
                return;

            _manager.OnClientConnectedCallback -= OnClientConnected;
            _manager.OnClientDisconnectCallback -= OnClientDisconnected;
            _manager.OnServerStarted -= OnServerStarted;
            _manager.OnClientConnectedCallback += OnClientConnected;
            _manager.OnClientDisconnectCallback += OnClientDisconnected;
            _manager.OnServerStarted += OnServerStarted;
        }

        private void OnServerStarted()
        {
            ServerBotGate.SetServerAuthoritative(true);
            InterestManager.Ensure(_manager, _maxPlayers);
            ServerTickProfiler.Ensure(SimulationHz);
        }

        private void OnClientConnected(ulong clientId)
        {
            _connected = true;
            if (_manager != null && !_manager.IsServer)
            {
                _everConnected = true;
                if (Flow.State == Project.Online.Sim.ReconnectState.Reconnecting)
                {
                    Flow.OnTransportRestored();
                    _resyncStart = Time.unscaledTime;
                }
            }

            if (_manager != null && _manager.IsServer)
            {
                ApplyClaim(clientId);
                InterestManager.Ensure(_manager, _maxPlayers)?.Rebuild();
                SendJoinState(clientId);
            }
        }

        /// <summary>Join-in-progress: katılan uzak istemciye bölge fazı + hayatta kalanlar (kendi oyuncu nesnesi üzerinden).</summary>
        private void SendJoinState(ulong clientId)
        {
            if (clientId == NetworkManager.ServerClientId)
                return;

            var obj = _manager.SpawnManager?.GetPlayerNetworkObject(clientId);
            obj?.GetComponent<NetworkPlayer>()?.ServerSendJoinState();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            var tryReconnect = _manager != null && !_manager.IsServer && _role == NetworkRole.Client
                && _everConnected && !_intentionalDisconnect
                && Flow.State != Project.Online.Sim.ReconnectState.Failed;

            if (_manager != null && !_manager.IsListening && !tryReconnect)
            {
                _connected = false;
                if (_role == NetworkRole.Client)
                    _role = NetworkRole.Offline;
            }

            if (tryReconnect)
                BeginReconnect();

            if (_manager != null && _manager.IsServer)
            {
                // Kopan uzak oyuncunun slotunu tolerans süresince token ile sakla (geri talep için).
                if (_clientTokens.TryGetValue(clientId, out var token))
                {
                    Grace.Hold(token, (int)clientId, Time.unscaledTime);
                    _clientTokens.Remove(clientId);
                }

                InterestManager.Ensure(_manager, _maxPlayers)?.Rebuild();
            }
        }

        /// <summary>Sunucu: token'ı tolerans tablosunda olan istemciye eski oyuncu kimliğini geri verir.</summary>
        private void ApplyClaim(ulong clientId)
        {
            if (!_claims.TryGetValue(clientId, out var oldId))
                return;
            _claims.Remove(clientId);
            var np = _manager.SpawnManager?.GetPlayerNetworkObject(clientId)?.GetComponent<NetworkPlayer>();
            np?.ServerSetPlayerId(new PlayerId(oldId));
            Debug.Log($"[Netcode] İstemci {clientId} yeniden bağlandı, oyuncu {oldId} geri verildi.");
        }

        private void BeginReconnect()
        {
            if (Flow.State == Project.Online.Sim.ReconnectState.Connected)
                Flow.OnConnectionLost(Time.unscaledTime);
            else if (Flow.State == Project.Online.Sim.ReconnectState.Reconnecting)
                Flow.OnAttemptFailed(Time.unscaledTime);

            if (_driver == null)
            {
                var go = new GameObject("NetcodeReconnectDriver");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _driver = go.AddComponent<ReconnectDriver>();
            }
            _driver.Bind(this);
        }

        private void ReconnectTick(float now)
        {
            if (_intentionalDisconnect || Flow.State == Project.Online.Sim.ReconnectState.Failed
                || Flow.State == Project.Online.Sim.ReconnectState.Connected)
                return;

            if (Flow.State == Project.Online.Sim.ReconnectState.Resyncing)
            {
                // Başka oyuncu yoksa tam snapshot gelmeyebilir: zaman aşımında eşitlemeyi tamamla.
                if (now - _resyncStart > 5f)
                {
                    Flow.OnJoinStateReceived();
                    Flow.OnFullSnapshotReceived();
                }
                return;
            }

            if (Flow.TryBeginAttempt(now))
            {
                if (_manager.IsListening)
                    _manager.Shutdown();
                Debug.Log($"[Netcode] Yeniden bağlanma denemesi {Flow.Attempts}/{Project.Online.Sim.ReconnectFlow.DefaultMaxAttempts}.");
                _role = NetworkRole.Offline;
                StartClient(null);
                if (!_manager.IsListening)
                {
                    Flow.OnAttemptFailed(now);
                }
                _resyncStart = now;
            }
        }

        private float _resyncStart;

        private sealed class ReconnectDriver : MonoBehaviour
        {
            private NetcodeNetworkSession _session;
            public void Bind(NetcodeNetworkSession s) { _session = s; }
            private void Update() { _session?.ReconnectTick(Time.unscaledTime); }
        }

        /// <summary>
        /// Sunucu kimlik doğrulayıcısı (ServerBootstrap bağlar). null = eski davranış (herkes kabul).
        /// Dönüş: (kabul, ret nedeni).
        /// </summary>
        public Func<ulong, Project.Online.Backend.ConnectionIdentity.Payload, System.Threading.Tasks.Task<(bool ok, string reason)>> IdentityApprover;

        private void Approval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            var connected = _manager != null ? _manager.ConnectedClientsIds.Count : 0;
            var accept = connected < _maxPlayers;
            response.CreatePlayerObject = accept;
            if (!accept)
            {
                response.Approved = false;
                response.Pending = false;
                response.Reason = "Sunucu dolu.";
                return;
            }

            var approver = IdentityApprover;
            // Host (kendi bağlantısı) doğrulamadan geçer; yalnızca uzak istemciler doğrulanır.
            if (approver == null || request.ClientNetworkId == NetworkManager.ServerClientId)
            {
                response.Approved = true;
                response.Pending = false;
                return;
            }

            var payload = Project.Online.Backend.ConnectionIdentity.Parse(request.Payload);
            if (payload.HasToken)
            {
                _clientTokens[request.ClientNetworkId] = payload.Jwt;
                if (Grace.TryClaim(payload.Jwt, Time.unscaledTime, out var oldId))
                    _claims[request.ClientNetworkId] = oldId;
            }
            response.Pending = true;
            _ = ResolveApprovalAsync(approver, request.ClientNetworkId, payload, response);
        }

        private static async System.Threading.Tasks.Task ResolveApprovalAsync(
            Func<ulong, Project.Online.Backend.ConnectionIdentity.Payload, System.Threading.Tasks.Task<(bool ok, string reason)>> approver,
            ulong clientId, Project.Online.Backend.ConnectionIdentity.Payload payload,
            NetworkManager.ConnectionApprovalResponse response)
        {
            var ok = false;
            string reason = "Kimlik doğrulanamadı.";
            try { (ok, reason) = await approver(clientId, payload); }
            catch (Exception e) { Debug.LogWarning("[Netcode] Kimlik doğrulama hatası: " + e.Message); }

            response.Approved = ok;
            response.Reason = ok ? null : reason;
            response.Pending = false;
        }

        private void ApplyTransport(string address, ushort port)
        {
            if (_manager.NetworkConfig.NetworkTransport is not UnityTransport utp)
            {
                Debug.LogError("[Netcode] UnityTransport bulunamadı.");
                return;
            }

            utp.SetConnectionData(string.IsNullOrWhiteSpace(address) ? "0.0.0.0" : address, port);
            _manager.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(
                Project.Online.Backend.ConnectionIdentity.BuildLocal(_localPlayerId.Value));
        }

        private void BindInterest()
        {
            BindExplosionRelay();
            InterestManager.Ensure(_manager, _maxPlayers);
        }

        private static void ParseAddress(string raw, out string host, out ushort port)
        {
            host = "127.0.0.1";
            port = DefaultPort;
            if (string.IsNullOrWhiteSpace(raw))
                return;

            raw = raw.Trim();
            var colon = raw.LastIndexOf(':');
            if (colon > 0 && ushort.TryParse(raw.AsSpan(colon + 1), out var parsedPort))
            {
                host = raw.Substring(0, colon);
                port = parsedPort;
                return;
            }

            host = raw;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterFactory()
        {
            GameCompositionRoot.NetworkSessionFactory = id => new NetcodeNetworkSession(id);
            Debug.Log("[Netcode] NetcodeNetworkSession fabrikası kaydedildi.");
        }
    }
}
