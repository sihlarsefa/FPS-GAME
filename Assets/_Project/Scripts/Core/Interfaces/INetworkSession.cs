using Project.Core.Domain;

namespace Project.Core.Interfaces
{
    /// <summary>
    /// Ağ oturumu soyutlaması. Oyun kuralları yalnızca HasAuthority true iken çalışır.
    /// Çevrimdışı mod: OfflineNetworkSession. Online: Netcode/Transport adaptörü (sonraki faz).
    /// </summary>
    public interface INetworkSession
    {
        NetworkRole Role { get; }
        bool IsHost { get; }
        bool IsConnected { get; }

        /// <summary>Oyun kurallarını bu süreç mi yürütüyor (Offline, Host, DedicatedServer)?</summary>
        bool HasAuthority { get; }

        PlayerId LocalPlayerId { get; }

        void StartHost();
        void StartClient(string address);
        void StartServer();
        void Disconnect();
    }
}
