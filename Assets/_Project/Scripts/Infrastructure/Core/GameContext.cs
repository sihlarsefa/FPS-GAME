using Project.Core.Interfaces;
using UnityEngine;
using IServiceProvider = Project.Core.Interfaces.IServiceProvider;

namespace Project.Infrastructure
{
    /// <summary>
    /// Sahne kapsamlı servis erişim noktası. Kompozisyon kökü (bootstrap) Set() çağırır, sahne kapanırken Clear().
    /// MonoBehaviour'lar bağımlılıklarını Awake/Start'ta buradan çözer.
    /// </summary>
    public static class GameContext
    {
        private static IServiceProvider _services;
        private static INetworkSession _network;

        public static IServiceProvider Services => _services;
        public static bool IsReady => _services != null;

        /// <summary>Kayıtlı ağ oturumu; yoksa çevrimdışı oturum (her zaman otorite).</summary>
        public static INetworkSession Network
        {
            get
            {
                if (_network != null)
                    return _network;

                if (_services != null && _services.TryResolve<INetworkSession>(out var session))
                    return _network = session;

                return _network = new Network.OfflineNetworkSession();
            }
        }

        public static bool HasAuthority => Network.HasAuthority;

        public static void Set(IServiceProvider services)
        {
            _services = services;
            _network = null;
        }

        public static void Clear()
        {
            _services = null;
            _network = null;
        }

        public static T Get<T>() where T : class
        {
            if (_services == null)
                throw new System.InvalidOperationException($"GameContext not ready (requested {typeof(T).Name}).");

            return _services.Resolve<T>();
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (_services != null && _services.TryResolve(out service))
                return true;

            service = null;
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _services = null;
            _network = null;
        }
    }
}
