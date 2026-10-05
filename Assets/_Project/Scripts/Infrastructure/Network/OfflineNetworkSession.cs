using Project.Core.Domain;
using Project.Core.Interfaces;

namespace Project.Infrastructure.Network
{
    /// <summary>
    /// Tek oyunculu oturum: bu süreç her zaman otoritedir. Online faz için Netcode/Transport adaptörü aynı arayüzü uygular.
    /// </summary>
    public sealed class OfflineNetworkSession : INetworkSession
    {
        public OfflineNetworkSession(PlayerId localPlayerId = default)
        {
            LocalPlayerId = localPlayerId;
        }

        public NetworkRole Role => NetworkRole.Offline;
        public bool IsHost => true;
        public bool IsConnected => true;
        public bool HasAuthority => true;
        public PlayerId LocalPlayerId { get; set; }

        public void StartHost()
        {
        }

        public void StartClient(string address)
        {
            UnityEngine.Debug.LogWarning("[Network] Online istemci modu henüz yok (çevrimdışı oturum).");
        }

        public void StartServer()
        {
            UnityEngine.Debug.LogWarning("[Network] Dedicated server modu henüz yok (çevrimdışı oturum).");
        }

        public void Disconnect()
        {
        }
    }
}
