using System;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure.DI;
using Project.Online.Backend;
using Project.Online.Profile;
using Project.Presentation.Bootstrap;
using UnityEngine;

namespace Project.Online.Bootstrap
{
    /// <summary>
    /// Online katmanı tekil servisleri. Ana oyun Online'ı bilmez; bu sınıf
    /// <see cref="RuntimeInitializeOnLoadMethod"/> ile kendini kaydeder.
    /// </summary>
    public static class OnlineServices
    {
        public const string BaseUrlPrefsKey = "harekat.online.baseUrl";

        private static BackendClient _client;
        private static OnlineProfileService _profile;
        private static OfflineMatchQueue _queue;
        private static bool _registered;

        public static BackendClient Client
        {
            get
            {
                Ensure();
                return _client;
            }
        }

        public static OnlineProfileService Profile
        {
            get
            {
                Ensure();
                return _profile;
            }
        }

        public static OfflineMatchQueue Queue
        {
            get
            {
                Ensure();
                return _queue;
            }
        }

        public static bool IsReady => _client != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoRegister()
        {
            Ensure();
            RegisterNetworkSessionFactory();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _client = null;
            _profile = null;
            _queue = null;
            _registered = false;
        }

        public static void Ensure()
        {
            if (_client != null && _profile != null)
                return;

            GameSession.EnsureInitialized();

            var baseUrl = PlayerPrefs.GetString(BaseUrlPrefsKey, BackendClient.DefaultBaseUrl);
            _client = new BackendClient(baseUrl);
            _queue = new OfflineMatchQueue();
            _queue.Load();

            CareerStatsService career = null;
            try { career = GameSession.Career; }
            catch (Exception) { /* menü öncesi */ }

            _profile = new OnlineProfileService(_client, career, _queue);
        }

        /// <summary>
        /// GameCompositionRoot.NetworkSessionFactory'ye kaydolur (şu an mevcut; null ise).
        /// GameSession.NetworkSessionFactory kancası uygulandığında da oraya yazılır (FAZ3_KANCALAR).
        /// Netcode (F3-2) AfterAssembliesLoaded'da önce kaydolursa bu metot onu ezmez.
        /// </summary>
        public static void RegisterNetworkSessionFactory()
        {
            if (_registered)
                return;
            _registered = true;

            Func<PlayerId, INetworkSession> factory = CreateNetworkSessionOrNull;

            try
            {
                if (GameCompositionRoot.NetworkSessionFactory == null)
                    GameCompositionRoot.NetworkSessionFactory = factory;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Online] GameCompositionRoot fabrikası ayarlanamadı: " + e.Message);
            }

            // Kanca hazırsa GameSession.NetworkSessionFactory'ye de yaz (Func<INetworkSession>).
            TrySetGameSessionFactory(() => CreateNetworkSessionOrNull(GameCompositionRoot.DefaultLocalPlayerId));
        }

        public static INetworkSession CreateNetworkSessionOrNull(PlayerId localPlayerId)
        {
            // F3-2 NetcodeNetworkSession HAREKAT_NETCODE ile kaydolur ve bu fabrikayı ezer.
            // F3-1: online istemci katmanı hazır; oturum adaptörü henüz yok → null = offline.
            return null;
        }

        private static void TrySetGameSessionFactory(Func<INetworkSession> factory)
        {
            try
            {
                var type = typeof(GameSession);
                var prop = type.GetProperty("NetworkSessionFactory",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop != null && prop.CanWrite && prop.PropertyType == typeof(Func<INetworkSession>))
                    prop.SetValue(null, factory);
            }
            catch (Exception)
            {
                // Kanca henüz yok — FAZ3_KANCALAR.md
            }
        }
    }
}
