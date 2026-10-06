using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure;
using Project.Online.Bootstrap;
using Project.Online.Lan;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Project.Online.Netcode
{
    /// <summary>
    /// Netcode paketi varken (HAREKAT_NETCODE) OnlineSessionBridge kancalarını doldurur;
    /// ONLINE paneli bu sayede Project.Online'dan Netcode'a derleme bağımlılığı olmadan host/katıl yapar.
    /// </summary>
    internal static class NetcodeBridgeRegistration
    {
        private static NetcodeNetworkSession _session;
        private static bool _hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register()
        {
            OnlineSessionBridge.StartHost = StartHost;
            OnlineSessionBridge.StartClient = StartClient;
            OnlineSessionBridge.Stop = Stop;
            OnlineSessionBridge.IsConnected = IsConnected;
            OnlineSessionBridge.Roster = Roster;
            OnlineSessionBridge.LocalPingMs = LocalPing;
        }

        private static NetcodeNetworkSession Session()
        {
            if (_session == null)
                _session = GameContext.Network as NetcodeNetworkSession ?? new NetcodeNetworkSession();
            return _session;
        }

        private static void HookManager()
        {
            var m = NetworkManager.Singleton;
            if (m == null || _hooked)
                return;
            _hooked = true;
            m.OnClientDisconnectCallback += id =>
            {
                if (m != null && id == m.LocalClientId)
                    OnlineSessionBridge.LastDisconnectReason = m.DisconnectReason;
            };
        }

        private static string StartHost(HostOptions o)
        {
            try
            {
                OnlineSessionBridge.HostedMapId = MapCatalog.Normalize(o.MapId);
                OnlineSessionBridge.HostedFillWithBots = o.FillWithBots;
                OnlineSessionBridge.HostedMaxPlayers = o.MaxPlayers;
                var s = Session();
                s.ConfigureEndpoint("0.0.0.0", o.Port, o.MaxPlayers);
                s.StartHost();
                HookManager();
                return s.Manager != null && s.Manager.IsListening ? null : "Sunucu başlatılamadı (port kullanımda olabilir).";
            }
            catch (Exception e) { return e.Message; }
        }

        private static string StartClient(string host, ushort port)
        {
            try
            {
                OnlineSessionBridge.LastDisconnectReason = null;
                var s = Session();
                s.ConfigureEndpoint(host, port);
                s.StartClient(host + ":" + port);
                HookManager();
                return s.Manager != null && s.Manager.IsListening ? null : "Bağlantı başlatılamadı.";
            }
            catch (Exception e) { return e.Message; }
        }

        private static void Stop()
        {
            try { _session?.Disconnect(); }
            catch (Exception e) { Debug.LogWarning("[Netcode] Durdurma: " + e.Message); }
        }

        private static bool IsConnected()
        {
            var m = NetworkManager.Singleton;
            return m != null && m.IsListening && (m.IsServer || m.IsConnectedClient);
        }

        private static int LocalPing()
        {
            var m = NetworkManager.Singleton;
            if (m == null || !m.IsClient || !m.IsConnectedClient)
                return -1;
            return m.NetworkConfig.NetworkTransport is UnityTransport utp
                ? (int)utp.GetCurrentRtt(NetworkManager.ServerClientId)
                : -1;
        }

        private static IReadOnlyList<LobbyPlayer> Roster()
        {
            var list = new List<LobbyPlayer>();
            var m = NetworkManager.Singleton;
            if (m == null || !m.IsListening)
                return list;

            var utp = m.NetworkConfig.NetworkTransport as UnityTransport;
            if (m.IsServer)
            {
                foreach (var id in m.ConnectedClientsIds)
                {
                    var isHostSelf = id == m.LocalClientId;
                    var ping = isHostSelf || utp == null ? 0 : (int)utp.GetCurrentRtt(id);
                    var ready = m.ConnectedClients.TryGetValue(id, out var c) && c.PlayerObject != null;
                    list.Add(new LobbyPlayer(isHostSelf ? "Sen (Sunucu)" : "Oyuncu #" + id, ping, ready, isHostSelf));
                }
            }
            else
            {
                list.Add(new LobbyPlayer("Sen", LocalPing(), m.LocalClient != null && m.LocalClient.PlayerObject != null));
            }

            return list;
        }
    }
}
