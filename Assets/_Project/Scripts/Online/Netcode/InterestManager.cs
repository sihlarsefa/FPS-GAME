using System.Collections.Generic;
using Project.Infrastructure.Combat;
using Project.Online.Sim;
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
        /// <summary>Gözlemciye bu mesafe içindeki veya aynı timdeki oyuncular replike edilir.</summary>
        public const float ReplicationRadius = InterestRules.Radius;

        private readonly List<NetworkPlayer> _players = new(DefaultMaxPlayers);
        private readonly BandwidthBudget _budget = new();
        private NetworkManager _manager;
        private int _maxPlayers = DefaultMaxPlayers;
        private float _rebuildAt;
        private float _graceAt;

        public static InterestManager Instance { get; private set; }

        /// <summary>Sunucu tarafı istemci başına bant genişliği bütçesi.</summary>
        public BandwidthBudget Budget => _budget;

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
            _budget.Register(player.OwnerClientId);
            BindVisibility(player);
        }

        public void Unregister(NetworkPlayer player)
        {
            if (player == null)
                return;
            _players.Remove(player);
            _budget.Remove(player.OwnerClientId);
        }

        /// <summary>Gözlemci-hedef ilgi kademesi (mesafe + tim; görüş hattı ucuzluk için varsayılan açık).</summary>
        public InterestTier TierFor(NetworkPlayer observer, NetworkPlayer target, bool currentlyVisible)
        {
            return InterestTiers.Classify(currentlyVisible,
                observer.transform.position, target.transform.position,
                TeamOf(observer), TeamOf(target), true);
        }

        /// <summary>
        /// Sunucu: bu tick'te hedefin snapshot'ı gözlemciye gönderilsin mi? Yakın = her tick, orta 20 Hz, uzak 5 Hz.
        /// Faz hedef kimliğidir (gönderimler tick'lere dağılır).
        /// </summary>
        public bool ShouldSendSnapshot(ulong observerClientId, NetworkPlayer target, uint tick)
        {
            var observer = FindByClient(observerClientId);
            if (observer == null || target == null)
                return true;
            var visible = target.NetworkObject != null && target.NetworkObject.IsNetworkVisibleTo(observerClientId);
            var tier = TierFor(observer, target, visible);
            return InterestTiers.ShouldSend(tier, tick, (int)target.NetworkObjectId, (int)NetcodeNetworkSession.SimulationHz);
        }

        /// <summary>Bağlı istemci kimlikleri (sunucuda); yoksa boş.</summary>
        public IReadOnlyList<ulong> ConnectedClients => _manager != null && _manager.IsServer ? _manager.ConnectedClientsIds : System.Array.Empty<ulong>();

        public void Rebuild()
        {
            _rebuildAt = 0f;
            UpdateVisibility(force: true);
        }

        private void Update()
        {
            if (_manager == null || !_manager.IsServer)
                return;

            _budget.Refill(Time.unscaledTime);
            if (Time.unscaledTime >= _graceAt)
            {
                _graceAt = Time.unscaledTime + 1f;
                NetcodeNetworkSession.Grace.Expire(Time.unscaledTime); // süresi dolan slotlar serbest
            }
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

                return TierFor(observer, player, netObj.IsNetworkVisibleTo(clientId)) != InterestTier.Culled;
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

        private readonly List<HitCandidate> _candidates = new(DefaultMaxPlayers);

        /// <summary>
        /// Geri sarılmış (rewindTick, shooter tarafında zaten 1 sn'ye kırpılmış) kapsüllere karşı en yakın isabet.
        /// worldBlockDistance: dünya engeli mesafesi; isabetten yakınsa atış engellenir.
        /// </summary>
        public bool TryHitWithLagCompensation(
            NetworkPlayer shooter,
            uint rewindTick,
            Vector3 origin,
            Vector3 direction,
            float worldBlockDistance,
            out NetworkPlayer victim,
            out bool headshot)
        {
            victim = null;
            headshot = false;
            _candidates.Clear();

            for (var i = 0; i < _players.Count; i++)
            {
                var candidate = _players[i];
                if (candidate == null || candidate == shooter || candidate.Health <= 0f)
                    continue;

                PositionHistory.Sample sample;
                var box = default(CapsuleHitbox);
                if (candidate.LagBuffer != null && candidate.LagBuffer.History.TrySampleTick(rewindTick, out sample))
                {
                    _candidates.Add(new HitCandidate(i, sample));
                    continue;
                }

                box = candidate.CurrentHitbox();
                _candidates.Add(new HitCandidate(i, new PositionHistory.Sample(rewindTick, 0f, box.Position, box.Rotation, box.Height, box.Radius)));
            }

            if (!HitscanRules.PickVictim(_candidates, origin, direction, worldBlockDistance, out var index, out _, out headshot))
                return false;

            victim = _players[index];
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

        private static int TeamOf(NetworkPlayer p)
        {
            return CombatantRegistry.TryGet(p.DomainPlayerId, out var c) && c != null ? c.Team : -1;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
