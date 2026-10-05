using System;
using Project.Core.Domain;
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
        private NetworkTransform _networkTransform;

        public float Health => _health.Value;
        public float Armor => _armor.Value;
        public int AmmoInMag => _ammoInMag.Value;
        public int AmmoReserve => _ammoReserve.Value;
        public PlayerId DomainPlayerId => new(_playerIdValue.Value);
        public LagCompensationBuffer LagBuffer => _lagBuffer;

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

            _networkTransform = GetComponent<NetworkTransform>();
            if (_networkTransform == null)
                _networkTransform = gameObject.AddComponent<NetworkTransform>();

            if (IsServer)
            {
                if (_playerIdValue.Value < 0)
                    _playerIdValue.Value = (int)OwnerClientId;
                ServerBotGate.RegisterPlayerObject(this);
            }

            InterestManager.Instance?.Register(this);
        }

        public override void OnNetworkDespawn()
        {
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
        }

        [ServerRpc]
        public void SubmitCommandServerRpc(NetworkPlayerCommand command, ServerRpcParams rpcParams = default)
        {
            if (!IsServer)
                return;

            var sender = rpcParams.Receive.SenderClientId;
            if (sender != OwnerClientId)
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

        public void ServerApplyCommand(PlayerCommand command)
        {
            if (!IsServer)
                return;

            if (command.Tick != 0 && command.Tick <= _lastCommandTick)
                return;

            _lastCommandTick = command.Tick;

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
                _prediction?.AcknowledgeServerState(BuildSnapshot(command.Tick, command.Pitch));
            else
                ReconcileClientRpc(BuildSnapshot(command.Tick, command.Pitch));
        }

        public void ServerValidateAndSimulateShot(ShotRequest request)
        {
            if (!IsServer)
                return;

            var now = Time.time;
            var minInterval = 1f / MaxFireRateHz;
            if (now - _lastShotTime < minInterval * 0.9f)
                return;

            if (_ammoInMag.Value <= 0)
                return;

            _lastShotTime = now;
            _ammoInMag.Value = Mathf.Max(0, _ammoInMag.Value - 1);

            var origin = new Vector3(request.Origin.X, request.Origin.Y, request.Origin.Z);
            var direction = new Vector3(request.Direction.X, request.Direction.Y, request.Direction.Z);
            if (direction.sqrMagnitude < 1e-6f)
                return;
            direction.Normalize();

            // İstemci origin'ine kör güvenme: yaklaşık namlu / göz noktası
            var serverOrigin = transform.position + Vector3.up * (capsuleHeight * 0.85f);
            if ((origin - serverOrigin).sqrMagnitude > 2.5f * 2.5f)
                origin = serverOrigin;

            if (InterestManager.Instance != null
                && InterestManager.Instance.TryHitWithLagCompensation(this, request.Tick, origin, direction, out var victim, out var headshot))
            {
                victim.ServerApplyDamage(headshot ? 70f : 28f, DomainPlayerId, request.WeaponId, headshot);
            }
            else if (Physics.Raycast(origin, direction, out var hit, 400f, ~0, QueryTriggerInteraction.Ignore))
            {
                var other = hit.collider.GetComponentInParent<NetworkPlayer>();
                if (other != null && other != this)
                {
                    var head = hit.point.y >= other.transform.position.y + other.capsuleHeight * 0.72f;
                    other.ServerApplyDamage(head ? 70f : 28f, DomainPlayerId, request.WeaponId, head);
                }
            }
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

        [ClientRpc]
        private void ReconcileClientRpc(NetworkReconciliationSnapshot snapshot)
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

        public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out float distance, out bool headshot)
        {
            distance = 0f;
            headshot = false;
            var up = Rotation * Vector3.up;
            var half = Mathf.Max(0f, Height * 0.5f - Radius);
            var p0 = Position + up * half;
            var p1 = Position - up * half;

            // Kapsül ≈ kalınlaştırılmış doğru: küre süpürme yaklaşımı
            if (!RaycastCapsule(origin, direction, maxDistance, p0, p1, Radius, out distance))
                return false;

            var hitPoint = origin + direction * distance;
            headshot = Vector3.Dot(hitPoint - Position, up) > Height * 0.22f;
            return true;
        }

        private static bool RaycastCapsule(
            Vector3 origin, Vector3 dir, float maxDist,
            Vector3 a, Vector3 b, float radius, out float distance)
        {
            distance = maxDist;
            var hit = false;

            if (RaycastSphere(origin, dir, maxDist, a, radius, out var d0) && d0 < distance)
            {
                distance = d0;
                hit = true;
            }

            if (RaycastSphere(origin, dir, maxDist, b, radius, out var d1) && d1 < distance)
            {
                distance = d1;
                hit = true;
            }

            var ab = b - a;
            var abLenSq = ab.sqrMagnitude;
            if (abLenSq > 1e-6f)
            {
                var n = ab / Mathf.Sqrt(abLenSq);
                var w = origin - a;
                var v = dir;
                var d = Vector3.Dot(w, n);
                var e = Vector3.Dot(v, n);
                var m = w - n * d;
                var u = v - n * e;
                var aQuad = Vector3.Dot(u, u);
                var bQuad = 2f * Vector3.Dot(m, u);
                var cQuad = Vector3.Dot(m, m) - radius * radius;
                if (Mathf.Abs(aQuad) > 1e-8f)
                {
                    var disc = bQuad * bQuad - 4f * aQuad * cQuad;
                    if (disc >= 0f)
                    {
                        var t = (-bQuad - Mathf.Sqrt(disc)) / (2f * aQuad);
                        if (t >= 0f && t <= maxDist)
                        {
                            var y = d + t * e;
                            if (y >= 0f && y * y <= abLenSq && t < distance)
                            {
                                distance = t;
                                hit = true;
                            }
                        }
                    }
                }
            }

            return hit;
        }

        private static bool RaycastSphere(Vector3 origin, Vector3 dir, float maxDist, Vector3 center, float radius, out float t)
        {
            t = 0f;
            var oc = origin - center;
            var b = Vector3.Dot(oc, dir);
            var c = Vector3.Dot(oc, oc) - radius * radius;
            var disc = b * b - c;
            if (disc < 0f)
                return false;
            t = -b - Mathf.Sqrt(disc);
            return t >= 0f && t <= maxDist;
        }
    }
}
