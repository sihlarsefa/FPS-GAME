using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Combat;
using Project.Online.Sim;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Project.Online.Netcode
{
    /// <summary>
    /// Oyuncu ağ nesnesi: can / zırh / mermi NetworkVariable; komut ve atış ServerRpc;
    /// istemci tahmini + sunucu uzlaştırması; lag compensated hitbox kaydı.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [DisallowMultipleComponent]
    public sealed class NetworkPlayer : NetworkBehaviour
    {
        public const float DefaultMaxHealth = 100f;
        public const float MaxFireRateHz = 20f;
        public const float ThrowCooldown = 1.0f;
        public const float MeleeCooldown = 0.5f;
        public const float MaxThrowSpeed = 24f;
        public const float MaxMeleeRange = 2.6f;
        public const float MaxActionOriginOffset = 3f;

        [SerializeField] private float capsuleHeight = 1.8f;
        [SerializeField] private float capsuleRadius = 0.35f;
        [SerializeField] private float moveSpeed = 5.5f;
        [SerializeField] private float sprintMultiplier = 1.45f;

        private readonly NetworkVariable<float> _health = new(
            DefaultMaxHealth,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _armor = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _ammoInMag = new(
            30,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _ammoReserve = new(
            90,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _playerIdValue = new(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private ClientPredictionController _prediction;
        private LagCompensationBuffer _lagBuffer;
        private float _lastShotTime = -999f;
        private uint _lastCommandTick;
        private ServerCommandGate _commandGate;

        /// <summary>Tahmin/uzlaştırma bu değerlerle aynı hareket modelini kullanır.</summary>
        public float MoveSpeed => moveSpeed;
        public float SprintMultiplier => sprintMultiplier;
        /// <summary>İstemci: yerel komutu tahmin tamponuna kaydeder (hareketi başka sistem uygular).</summary>
        public void RecordLocalCommand(PlayerCommand command) => _prediction?.RecordLocal(command);
        /// <summary>Sunucu: bu oyuncunun komut hız sınırı / sıra istatistikleri.</summary>
        public ServerCommandGate CommandGate => _commandGate;
        private NetworkTransform _networkTransform;

        public float Health => _health.Value;
        public float Armor => _armor.Value;
        public int AmmoInMag => _ammoInMag.Value;
        public int AmmoReserve => _ammoReserve.Value;
        public PlayerId DomainPlayerId => new(_playerIdValue.Value);
        public LagCompensationBuffer LagBuffer => _lagBuffer;

        private static readonly List<NetworkPlayer> Spawned = new();
        private readonly ThrowGate _throwGate = new();
        private float _nextMeleeAt;
        private static readonly RaycastHit[] WorldHits = new RaycastHit[16];

        /// <summary>İstemci: sunucudan başka oyuncunun fırlattığı bomba bildirimi (tür kodu, çıkış, hız, atan). Görsel/ses için.</summary>
        public static event Action<byte, Vector3, Vector3, PlayerId> ThrowAnnounced;

        /// <summary>Sunucu tarafında yayın (ClientRpc) göndermek için herhangi bir spawn edilmiş oyuncu nesnesi.</summary>
        public static NetworkPlayer FindAnySpawned()
        {
            for (var i = Spawned.Count - 1; i >= 0; i--)
            {
                var p = Spawned[i];
                if (p == null)
                {
                    Spawned.RemoveAt(i);
                    continue;
                }

                if (p.IsSpawned)
                    return p;
            }

            return null;
        }

        public static NetworkPlayer FindLocal()
        {
            if (NetworkManager.Singleton == null)
                return null;

            foreach (var player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
            {
                if (player != null && player.IsOwner)
                    return player;
            }

            return null;
        }

        public override void OnNetworkSpawn()
        {
            _lagBuffer = new LagCompensationBuffer(seconds: 1f, tickRate: NetcodeNetworkSession.SimulationHz);
            _prediction = GetComponent<ClientPredictionController>() ?? gameObject.AddComponent<ClientPredictionController>();
            _prediction.Bind(this);
            _commandGate = new ServerCommandGate(NetcodeNetworkSession.SimulationHz * 3f, 12f);

            _networkTransform = GetComponent<NetworkTransform>();
            if (_networkTransform == null)
                _networkTransform = gameObject.AddComponent<NetworkTransform>();

            if (!IsServer && !IsOwner)
            {
                // Saf istemcide uzak oyuncu: ham NetworkTransform atlamalarını uyarlanır gecikmeyle enterpole et.
                _networkTransform.Interpolate = false;
                if (GetComponent<RemotePlayerInterpolator>() == null)
                    gameObject.AddComponent<RemotePlayerInterpolator>();
            }

            if (IsServer)
            {
                if (_playerIdValue.Value < 0)
                    _playerIdValue.Value = (int)OwnerClientId;
                ServerBotGate.RegisterPlayerObject(this);
            }

            if (!Spawned.Contains(this))
                Spawned.Add(this);

            InterestManager.Instance?.Register(this);
        }

        public override void OnNetworkDespawn()
        {
            Spawned.Remove(this);
            _snapChannels.Clear();
            _snapRx.Reset();
            InterestManager.Instance?.Unregister(this);
            if (IsServer)
                ServerBotGate.UnregisterPlayerObject(this);
        }

        private void LateUpdate()
        {
            if (!IsServer || _lagBuffer == null)
                return;

            var tick = NetworkManager != null ? NetworkManager.ServerTime.Tick : 0u;
            _lagBuffer.Record(tick, transform.position, transform.rotation, capsuleHeight, capsuleRadius);
            ServerReplicateSnapshots(tick);
        }

        // ------------------------------------------------------------ kademeli snapshot (delta) replikasyonu
        private const int SnapshotBufferSize = SnapshotDelta.MaxFullBytes + 8;
        private readonly Dictionary<ulong, SnapshotChannel> _snapChannels = new();
        private readonly byte[] _snapBuf = new byte[SnapshotBufferSize];
        private readonly SnapshotReceiver _snapRx = new();
        private uint _lastSnapTick = uint.MaxValue;
        private float _lastPitch;

        /// <summary>İstemci: son çözülen uzak oyuncu snapshot'ı (görsel katman isterse okur).</summary>
        public PlayerSnap LastSnapshot => _snapRx.Last;
        public bool HasSnapshot => _snapRx.HasBaseline;

        /// <summary>
        /// Sunucu: her tick'te gözlemci başına ilgi kademesine göre (yakın tam hız, orta 20 Hz, uzak 5 Hz)
        /// delta/tam snapshot yollar. Sahibi ve host'a gönderilmez (onlar uzlaştırma / yerel durumu kullanır).
        /// </summary>
        private void ServerReplicateSnapshots(uint tick)
        {
            var im = InterestManager.Instance;
            if (im == null || tick == _lastSnapTick || !IsSpawned)
                return;
            _lastSnapTick = tick;

            var clients = im.ConnectedClients;
            for (var i = 0; i < clients.Count; i++)
            {
                var clientId = clients[i];
                if (clientId == OwnerClientId || clientId == NetworkManager.ServerClientId)
                    continue;
                if (!NetworkObject.IsNetworkVisibleTo(clientId) || !im.ShouldSendSnapshot(clientId, this, tick))
                    continue;

                if (!_snapChannels.TryGetValue(clientId, out var channel))
                    _snapChannels[clientId] = channel = new SnapshotChannel();

                var snap = PlayerSnap.From(
                    (int)OwnerClientId, transform.position, transform.eulerAngles.y, _lastPitch,
                    _health.Value / DefaultMaxHealth, _health.Value <= 0f ? (byte)1 : (byte)0);
                var o = 0;
                channel.Write(_snapBuf, ref o, snap, tick);
                var blob = new byte[o];
                Buffer.BlockCopy(_snapBuf, 0, blob, 0, o);
                if (im.Budget == null || im.Budget.TryConsume(clientId, o + 16, Time.unscaledTime))
                    SnapshotClientRpc(blob, ClientRpcTo(clientId));
                else
                    channel.Reset(); // gönderilemedi: baseline tutarsız olmasın, sonraki yazım tam durum
            }
        }

        [ClientRpc(Delivery = RpcDelivery.Unreliable)]
        private void SnapshotClientRpc(byte[] blob, ClientRpcParams rpcParams = default)
        {
            if (IsServer)
                return;
            var flow = NetcodeNetworkSession.Flow;
            if (_snapRx.Apply(blob, 0, flow == null || flow.AcceptDeltas, out var full) && full)
                flow?.OnFullSnapshotReceived();
        }

        [ServerRpc]
        public void SubmitCommandServerRpc(NetworkPlayerCommand command, ServerRpcParams rpcParams = default)
        {
            if (!IsServer)
                return;

            var sender = rpcParams.Receive.SenderClientId;
            if (sender != OwnerClientId)
                return;

            // Uzak istemci: tekrar/eski komut ve komut seli (hız hilesi) reddi.
            if (_commandGate != null && !_commandGate.Accept(command.Tick, Time.unscaledTime))
                return;

            ServerApplyCommand(command.ToDomain());
        }

        [ServerRpc]
        public void SubmitShotServerRpc(NetworkShotRequest shot, ServerRpcParams rpcParams = default)
        {
            if (!IsServer)
                return;

            var sender = rpcParams.Receive.SenderClientId;
            if (sender != OwnerClientId)
                return;

            ServerValidateAndSimulateShot(shot.ToDomain());
        }

        [ServerRpc]
        public void SubmitThrowServerRpc(NetworkThrowRequest request, ServerRpcParams rpcParams = default)
        {
            if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId)
                return;

            ServerExecuteThrow(request);
        }

        [ServerRpc]
        public void SubmitMeleeServerRpc(NetworkMeleeRequest request, ServerRpcParams rpcParams = default)
        {
            if (!IsServer || rpcParams.Receive.SenderClientId != OwnerClientId)
                return;

            ServerExecuteMelee(request);
        }

        /// <summary>İstemci bomba isteğini doğrular (bilinen kod, tür/genel bekleme, vektör temizleme) ve sunucuda fırlatır.</summary>
        private void ServerExecuteThrow(NetworkThrowRequest request)
        {
            if (!CombatantRegistry.TryGet(DomainPlayerId, out var combatant) || !combatant.IsAlive)
                return;

            if (!ThrowGate.Sanitize(request.Origin, request.Velocity, combatant.EyePosition, out var origin, out var velocity))
                return;

            if (!_throwGate.TryAccept(request.Kind, Time.time))
                return;

            ThrowableKind kind;
            switch ((ThrowKindCode)request.Kind)
            {
                case ThrowKindCode.Frag: kind = ThrowableKind.Frag; break;
                case ThrowKindCode.Smoke: kind = ThrowableKind.Smoke; break;
                case ThrowKindCode.Flash: kind = ThrowableKind.Flash; break;
                case ThrowKindCode.Molotov: kind = ThrowableKind.Molotov; break;
                case ThrowKindCode.Decoy: kind = ThrowableKind.Decoy; break;
                default: return; // TryAccept zaten bilinmeyen kodu eler; yeni kod eklenirse buraya eşleme eklenmeli.
            }

            ThrowableProjectile.Throw(kind, origin, velocity, DomainPlayerId);
            ThrowAnnouncedClientRpc(request.Kind, origin, velocity, DomainPlayerId.Value);
        }

        /// <summary>Sunucu → istemciler: bomba atıldı (hasar yok; görsel/ses için olay). Sunucu ve atan hariç.</summary>
        [ClientRpc]
        public void ThrowAnnouncedClientRpc(byte kindCode, Vector3 origin, Vector3 velocity, int throwerId)
        {
            if (IsServer || IsOwner)
                return;
            ThrowAnnounced?.Invoke(kindCode, origin, velocity, new PlayerId(throwerId));
        }

        /// <summary>Sunucu → katılan istemci: bölge fazı + hayatta kalanlar (join-in-progress).</summary>
        [ClientRpc]
        public void JoinStateClientRpc(byte[] blob, ClientRpcParams rpcParams = default)
        {
            if (IsServer)
                return;
            if (!JoinState.TryDecode(blob, out var state))
                return;
            // Tek yön gecikme kestirimi: ServerTime ile yerel zaman farkı yerine yarım RTT bilinmiyor; 0.05 sn varsayılır.
            state.CompensateLatency(0.05f);
            NetcodeNetworkSession.Flow?.OnJoinStateReceived();
            JoinStateProvider.Deliver(state);
        }

        /// <summary>Sunucu: bu oyuncu nesnesi üzerinden yalnızca sahibine join-in-progress durumunu yollar.</summary>
        public void ServerSendJoinState()
        {
            if (!IsServer || !IsSpawned)
                return;
            JoinStateClientRpc(JoinStateProvider.Build().Encode(), ClientRpcTo(OwnerClientId));
        }

        /// <summary>
        /// İstemci: atış için bildirilecek tick = görülen sunucu tick'i − uzak oyuncu interpolasyon gecikmesi
        /// (oyuncu hedefi gecikmeli render edilmiş konumda gördü; sunucu o zamana geri sarar).
        /// </summary>
        public uint EstimateRenderTick()
        {
            if (NetworkManager == null)
                return 0u;
            var tick = NetworkManager.ServerTime.Tick;
            var back = (uint)Mathf.RoundToInt(RemotePlayerInterpolator.CurrentDelay * NetcodeNetworkSession.SimulationHz);
            return tick > back ? (uint)(tick - back) : 0u;
        }

        private void ServerExecuteMelee(NetworkMeleeRequest request)
        {
            var now = Time.time;
            if (now < _nextMeleeAt)
                return;

            if (!CombatantRegistry.TryGet(DomainPlayerId, out var combatant) || !combatant.IsAlive)
                return;

            var origin = request.Origin;
            if ((origin - combatant.EyePosition).sqrMagnitude > MaxActionOriginOffset * MaxActionOriginOffset)
                origin = combatant.EyePosition;

            _nextMeleeAt = now + MeleeCooldown;
            MeleeAttack.TryPunch(combatant, origin, request.Direction, Mathf.Clamp(request.Range, 0.3f, MaxMeleeRange));
        }

        /// <summary>Sunucu: patlama görsel/ses efektini tüm istemcilere yayar (hasar yalnızca sunucuda).</summary>
        [ClientRpc]
        public void ExplosionClientRpc(Vector3 position, float radius)
        {
            if (IsServer)
                return; // Host zaten yerelde oynattı.

            // Hasar 0: istemcide yalnızca VFX/ses/kamera sarsıntısı/ExplosionEvent.
            ExplosionSystem.Explode(position, radius, 0f, PlayerId.Invalid, null);
        }

        [ClientRpc]
        public void ArtilleryWhistleClientRpc(Vector3 position)
        {
            if (IsServer)
                return;

            GameAudio.Play(SoundId.ArtilleryWhistle, position, 1f, UnityEngine.Random.Range(0.93f, 1.07f), 320f);
        }

        public void ServerApplyCommand(PlayerCommand command)
        {
            if (!IsServer)
                return;

            if (command.Tick != 0 && command.Tick <= _lastCommandTick)
                return;

            _lastCommandTick = command.Tick;
            _lastPitch = command.Pitch;

            var dt = 1f / NetcodeNetworkSession.SimulationHz;
            var yaw = command.Yaw;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var forward = transform.forward;
            var right = transform.right;
            var input = new Vector3(command.MoveRight, 0f, command.MoveForward);
            if (input.sqrMagnitude > 1f)
                input.Normalize();

            var speed = moveSpeed * (command.Has(PlayerButtons.Sprint) ? sprintMultiplier : 1f);
            var delta = (forward * input.z + right * input.x) * speed * dt;
            var next = transform.position + delta;

            // Temel hız / ışınlanma koruması
            var maxStep = speed * dt * 1.35f;
            if ((next - transform.position).sqrMagnitude > maxStep * maxStep)
                next = transform.position + (next - transform.position).normalized * maxStep;

            transform.position = next;

            if (IsOwner)
            {
                _prediction?.AcknowledgeServerState(BuildSnapshot(command.Tick, command.Pitch));
            }
            else
            {
                // Bant bütçesi: kritik olmayan uzlaştırma bütçe yoksa atlanır; sonraki komutun ack'i onu kapsar.
                var budget = InterestManager.Instance != null ? InterestManager.Instance.Budget : null;
                if (budget == null || budget.TryConsume(OwnerClientId, ReconcileWireBytes, Time.unscaledTime))
                    ReconcileClientRpc(BuildSnapshot(command.Tick, command.Pitch), ClientRpcTo(OwnerClientId));
            }
        }

        public void ServerValidateAndSimulateShot(ShotRequest request)
        {
            if (!IsServer)
                return;

            var serverTick = NetworkManager != null ? NetworkManager.ServerTime.Tick : 0u;
            var serverOrigin = transform.position + Vector3.up * (capsuleHeight * 0.85f);
            var check = HitscanRules.Evaluate(
                Time.time, _lastShotTime, MaxFireRateHz, _ammoInMag.Value,
                new Vector3(request.Origin.X, request.Origin.Y, request.Origin.Z),
                new Vector3(request.Direction.X, request.Direction.Y, request.Direction.Z),
                serverOrigin, request.Tick, serverTick,
                _lagBuffer != null ? _lagBuffer.History.MaxRewindTicks : 30u);

            if (!check.Accepted)
                return;

            _lastShotTime = Time.time;
            _ammoInMag.Value = Mathf.Max(0, _ammoInMag.Value - 1);
            LastShotRewindTicks = check.RewindTicks;
            if (check.WasClamped)
                RewindClampCount++;

            var worldBlock = WorldBlockDistance(check.Origin, check.Direction);
            if (InterestManager.Instance != null
                && InterestManager.Instance.TryHitWithLagCompensation(this, check.RewindTick, check.Origin, check.Direction, worldBlock, out var victim, out var headshot))
            {
                victim.ServerApplyDamage(HitscanRules.DamageFor(headshot), DomainPlayerId, request.WeaponId, headshot);
            }
            else if (InterestManager.Instance == null && Physics.Raycast(check.Origin, check.Direction, out var hit, HitscanRules.MaxRange, ~0, QueryTriggerInteraction.Ignore))
            {
                var other = hit.collider.GetComponentInParent<NetworkPlayer>();
                if (other != null && other != this)
                {
                    var head = hit.point.y >= other.transform.position.y + other.capsuleHeight * 0.72f;
                    other.ServerApplyDamage(HitscanRules.DamageFor(head), DomainPlayerId, request.WeaponId, head);
                }
            }
        }

        /// <summary>Son kabul edilen atışın geri sarma tick sayısı (telemetri) ve sınırlanan (şüpheli) atış sayısı.</summary>
        public uint LastShotRewindTicks { get; private set; }
        public int RewindClampCount { get; private set; }

        /// <summary>Dünya (oyuncu olmayan) çarpışmasına en yakın mesafe; yoksa +∞. Oyuncu kapsülleri geri sarılmış hitbox'larla ayrıca denenir.</summary>
        private float WorldBlockDistance(Vector3 origin, Vector3 direction)
        {
            var n = Physics.RaycastNonAlloc(origin, direction, WorldHits, HitscanRules.MaxRange, ~0, QueryTriggerInteraction.Ignore);
            var best = float.PositiveInfinity;
            for (var i = 0; i < n; i++)
            {
                var h = WorldHits[i];
                if (h.collider == null || h.collider.GetComponentInParent<NetworkPlayer>() != null)
                    continue;
                if (h.distance < best)
                    best = h.distance;
            }

            return best;
        }

        public void ServerApplyDamage(float amount, PlayerId attacker, string weaponId, bool headshot)
        {
            if (!IsServer || amount <= 0f || _health.Value <= 0f)
                return;

            var remaining = amount;
            if (_armor.Value > 0f)
            {
                var absorbed = Mathf.Min(_armor.Value, remaining * 0.55f);
                _armor.Value = Mathf.Max(0f, _armor.Value - absorbed);
                remaining -= absorbed;
            }

            _health.Value = Mathf.Max(0f, _health.Value - remaining);
            if (_health.Value <= 0f)
                Debug.Log($"[Netcode] Oyuncu {_playerIdValue.Value} öldü (saldırgan={attacker}, silah={weaponId}, kafa={headshot}).");
        }

        public void ServerSetVitals(float health, float armor, int ammoInMag, int ammoReserve)
        {
            if (!IsServer)
                return;

            _health.Value = Mathf.Clamp(health, 0f, DefaultMaxHealth);
            _armor.Value = Mathf.Max(0f, armor);
            _ammoInMag.Value = Mathf.Max(0, ammoInMag);
            _ammoReserve.Value = Mathf.Max(0, ammoReserve);
        }

        public void ServerSetPlayerId(PlayerId id)
        {
            if (IsServer && id.IsValid)
                _playerIdValue.Value = id.Value;
        }

        /// <summary>NetworkReconciliationSnapshot ≈ 4+12+4+4+4+4+4+4 bayt + RPC başlığı.</summary>
        private const int ReconcileWireBytes = 56;

        private static ClientRpcParams ClientRpcTo(ulong clientId) => new()
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        };

        [ClientRpc]
        private void ReconcileClientRpc(NetworkReconciliationSnapshot snapshot, ClientRpcParams rpcParams = default)
        {
            _prediction?.Reconcile(snapshot);
        }

        private NetworkReconciliationSnapshot BuildSnapshot(uint tick, float pitch) => new()
        {
            Tick = tick,
            Position = transform.position,
            Yaw = transform.eulerAngles.y,
            Pitch = pitch,
            Health = _health.Value,
            Armor = _armor.Value,
            AmmoInMag = _ammoInMag.Value,
            AmmoReserve = _ammoReserve.Value
        };

        public CapsuleHitbox CurrentHitbox() => new(transform.position, transform.rotation, capsuleHeight, capsuleRadius);
    }

    public readonly struct CapsuleHitbox
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly float Height;
        public readonly float Radius;

        public CapsuleHitbox(Vector3 position, Quaternion rotation, float height, float radius)
        {
            Position = position;
            Rotation = rotation;
            Height = height;
            Radius = radius;
        }

        public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out float distance, out bool headshot) =>
            CapsuleRay.Raycast(Position, Rotation, Height, Radius, origin, direction, maxDistance, out distance, out headshot);
    }
}
