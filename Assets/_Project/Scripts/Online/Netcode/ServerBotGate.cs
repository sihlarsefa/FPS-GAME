using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Project.Online.Netcode
{
    /// <summary>
    /// Botlar yalnızca sunucuda çalışır. İstemci süreçlerinde AI güncellemesi kapalı tutulur.
    /// </summary>
    public static class ServerBotGate
    {
        private static readonly HashSet<NetworkPlayer> Players = new();
        private static bool _serverAuthoritative;

        public static bool IsServerAuthoritative =>
            _serverAuthoritative
            || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);

        public static bool ShouldRunBots => IsServerAuthoritative;

        public static void SetServerAuthoritative(bool value) => _serverAuthoritative = value;

        public static void RegisterPlayerObject(NetworkPlayer player)
        {
            if (player != null)
                Players.Add(player);
        }

        public static void UnregisterPlayerObject(NetworkPlayer player)
        {
            if (player != null)
                Players.Remove(player);
        }

        /// <summary>
        /// Bot NetworkObject spawn için: yalnızca sunucu çağırır; istemciye görsel çoğaltma gider,
        /// AI bileşenleri sunucuda etkin kalır.
        /// </summary>
        public static bool TrySpawnBot(GameObject prefab, Vector3 position, Quaternion rotation, out NetworkObject netObj)
        {
            netObj = null;
            if (!ShouldRunBots || prefab == null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                return false;

            var instance = Object.Instantiate(prefab, position, rotation);
            netObj = instance.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Object.Destroy(instance);
                return false;
            }

            // Bot sahipliği sunucuda kalır (clientId yok)
            netObj.Spawn(destroyWithScene: true);
            DisableClientOnlyBehaviours(instance);
            return true;
        }

        public static void DisableClientOnlyBehaviours(GameObject root)
        {
            if (root == null)
                return;

            // İstemci süreçlerinde bot AI kapalı olsun diye sunucu dışı bileşenleri kapatma kancası.
            // Asıl BotController kancası FAZ3_KANCALAR.md'de; burada ağ güvenliği için NetworkBehaviour dışı
            // "Bot" adlı bileşenleri sunucu değilse kapatırız.
            if (ShouldRunBots)
                return;

            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                var b = behaviours[i];
                if (b == null)
                    continue;
                var name = b.GetType().Name;
                if (name.IndexOf("Bot", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && b is not NetworkBehaviour)
                    b.enabled = false;
            }
        }
    }
}
