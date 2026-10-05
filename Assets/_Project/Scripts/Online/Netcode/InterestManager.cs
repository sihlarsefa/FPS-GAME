using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Project.Online.Netcode
{
    /// <summary>
    /// 60 oyunculuk maç için ilgi alanı yönetimi: yakındaki oyuncular sık,
    /// uzaktakiler seyrek güncellenir / gizlenir.
    /// </summary>
    public sealed class InterestManager : MonoBehaviour
    {
        public const int DefaultMaxPlayers = 60;
        public const float NearRange = 80f;
        public const float FarRange = 220f;
        public const float FarUpdateInterval = 0.35f;

        private readonly List<NetworkPlayer> _players = new(DefaultMaxPlayers);
        private readonly Dictionary<ulong, float> _lastFarUpdate = new();
        private NetworkManager _manager;
        private int _maxPlayers = DefaultMaxPlayers;
        private float _rebuildAt;

        public static InterestManager Instance { get; private set; }

        public static InterestManager Ensure(NetworkManager manager, int maxPlayers)
        {
            if (manager == null)
                return null;

            if (Instance == null)
            {
                var go = new GameObject("InterestManager");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<InterestManager>();
            }

            Instance._manager = manager;
            Instance._maxPlayers = Mathf.Clamp(maxPlayers <= 0 ? DefaultMaxPlayers : maxPlayers, 2, 100);
            return Instance;
        }

        public void Register(NetworkPlayer player)
        {
            if (player == null || _players.Contains(player))
                return;
            _players.Add(player);
            BindVisibility(player);
        }

        public void Unregister(NetworkPlayer player)
        {
            if (player == null)
                return;
            _players.Remove(player);
        }

        public void Rebuild()
        {
            _rebuildAt = 0f;
            UpdateVisibility(force: true);
        }

        private void Update()
        {
            if (_manager == null || !_manager.IsServer)
                return;

            if (Time.unscaledTime >= _rebuildAt)
            {
                _rebuildAt = Time.unscaledTime + 0.1f;
                UpdateVisibility(force: false);
            }
        }

        private void BindVisibility(NetworkPlayer player)
        {
            var netObj = player.NetworkObject;
            if (netObj == null)
                return;

            netObj.CheckObjectVisibility = clientId =>
            {
                if (player.OwnerClientId == clientId)
                    return true;

                var observer = FindByClient(clientId);
                if (observer == null)
                    return true;

                var dist = Vector3.Distance(observer.transform.position, player.transform.position);
                if (dist <= NearRange)
                    return true;

                if (dist > FarRange)
                    return false;

                // Orta mesafe: seyrek göster
                if (!_lastFarUpdate.TryGetValue(CompositeKey(clientId, player.OwnerClientId), out var last)
                    || Time.unscaledTime - last >= FarUpdateInterval)
                {
                    _lastFarUpdate[CompositeKey(clientId, player.OwnerClientId)] = Time.unscaledTime;
                    return true;
                }

                return netObj.IsNetworkVisibleTo(clientId);
            };
        }

        private void UpdateVisibility(bool force)
        {
            for (var i = _players.Count - 1; i >= 0; i--)
            {
                var player = _players[i];
                if (player == null)
                {
                    _players.RemoveAt(i);
                    continue;
                }

                var netObj = player.NetworkObject;
                if (netObj == null || !netObj.IsSpawned)
                    continue;

                foreach (var clientId in _manager.ConnectedClientsIds)
                {
                    var shouldShow = netObj.CheckObjectVisibility == null
                        || netObj.CheckObjectVisibility(clientId);

                    var visible = netObj.IsNetworkVisibleTo(clientId);
                    if (shouldShow && !visible)
                        netObj.NetworkShow(clientId);
                    else if (!shouldShow && visible)
                        netObj.NetworkHide(clientId);
                    else if (force && shouldShow)
                        netObj.NetworkShow(clientId);
                }
            }
        }

        public bool TryHitWithLagCompensation(
            NetworkPlayer shooter,
            uint tick,
            Vector3 origin,
            Vector3 direction,
            out NetworkPlayer victim,
            out bool headshot)
        {
            victim = null;
            headshot = false;
            var best = float.MaxValue;

            for (var i = 0; i < _players.Count; i++)
            {
                var candidate = _players[i];
                if (candidate == null || candidate == shooter || candidate.Health <= 0f)
                    continue;

                CapsuleHitbox box;
                if (candidate.LagBuffer == null || !candidate.LagBuffer.TrySample(tick, out box))
                    box = candidate.CurrentHitbox();

                if (!box.Raycast(origin, direction, 400f, out var dist, out var hs))
                    continue;

                if (dist < best)
                {
                    best = dist;
                    victim = candidate;
                    headshot = hs;
                }
            }

            return victim != null;
        }

        private NetworkPlayer FindByClient(ulong clientId)
        {
            for (var i = 0; i < _players.Count; i++)
            {
                var p = _players[i];
                if (p != null && p.OwnerClientId == clientId)
                    return p;
            }

            return null;
        }

        private static ulong CompositeKey(ulong a, ulong b) => (a * 397UL) ^ (b + 0x9e3779b97f4a7c15UL);

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
