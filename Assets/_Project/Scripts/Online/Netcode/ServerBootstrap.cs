using System;
using System.Globalization;
using System.Threading.Tasks;
using Project.Core.Domain;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Online.Netcode
{
    /// <summary>
    /// Dedicated server girişi.
    /// Komut satırı: <c>-server -port -region -backend -serverKey -maxPlayers</c>.
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
                TickRate = GetInt(raw, "-tickrate", DefaultTickRate)
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

            ConfigureHeadlessProcess(args.TickRate);
            ServerBotGate.SetServerAuthoritative(true);

            _session = new NetcodeNetworkSession(PlayerId.Invalid);
            _session.ConfigureEndpoint(args.BindAddress, args.Port, args.MaxPlayers);
            _session.StartServer();

            if (!string.IsNullOrEmpty(args.BackendUrl) && !string.IsNullOrEmpty(args.ServerKey))
            {
                _backend = new DedicatedServerBackend(args.BackendUrl, args.ServerKey);
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

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            _session?.Disconnect();
        }

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
        }
    }
}
