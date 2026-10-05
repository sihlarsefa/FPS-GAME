using System;
using Project.Core.Domain;
using Project.Core.Interfaces;
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
    public sealed class NetcodeNetworkSession : INetworkSession, IPlayerCommandSink
    {
        public const ushort DefaultPort = 7777;
        public const int DefaultMaxPlayers = 60;
        public const float SimulationHz = 30f;

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
            if (_manager != null && _manager.IsListening)
                _manager.Shutdown();

            _connected = false;
            _role = NetworkRole.Offline;
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

            relay.SubmitShotServerRpc(NetworkShotRequest.From(request));
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
        }

        private void OnClientConnected(ulong clientId)
        {
            _connected = true;
            if (_manager != null && _manager.IsServer)
                InterestManager.Ensure(_manager, _maxPlayers)?.Rebuild();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (_manager != null && !_manager.IsListening)
            {
                _connected = false;
                if (_role == NetworkRole.Client)
                    _role = NetworkRole.Offline;
            }

            if (_manager != null && _manager.IsServer)
                InterestManager.Ensure(_manager, _maxPlayers)?.Rebuild();
        }

        private void Approval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            var connected = _manager != null ? _manager.ConnectedClientsIds.Count : 0;
            var accept = connected < _maxPlayers;
            response.Approved = accept;
            response.CreatePlayerObject = accept;
            response.Pending = false;
            if (!accept)
                response.Reason = "Sunucu dolu.";
        }

        private void ApplyTransport(string address, ushort port)
        {
            if (_manager.NetworkConfig.NetworkTransport is not UnityTransport utp)
            {
                Debug.LogError("[Netcode] UnityTransport bulunamadı.");
                return;
            }

            utp.SetConnectionData(string.IsNullOrWhiteSpace(address) ? "0.0.0.0" : address, port);
            _manager.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes($"harekat|{_localPlayerId.Value}");
        }

        private void BindInterest()
        {
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
