using System;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure.DI;
using Project.Online.Backend;
using Project.Online.Profile;
using Project.Online.UI;
using Project.Presentation.Bootstrap;
using Project.Presentation.UI;
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
            RegisterMenuButton();
        }

        private static void RegisterMenuButton()
        {
            const string label = "ONLİNE";
            var list = MainMenuController.ExtraButtons;
            for (var i = 0; i < list.Count; i++)
                if (list[i].label == label)
                    return;
            list.Add((label, root => OnlineLoginPanel.Show(root)));
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
            var tokens = _client.Tokens;
            ConnectionIdentity.TokenProvider = () => tokens.HasAccessToken ? tokens.AccessToken : null;
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
        }

        public static INetworkSession CreateNetworkSessionOrNull(PlayerId localPlayerId)
        {
            // F3-2 NetcodeNetworkSession HAREKAT_NETCODE ile kaydolur ve bu fabrikayı ezer.
            // F3-1: online istemci katmanı hazır; oturum adaptörü henüz yok → null = offline.
            return null;
        }
    }
}
