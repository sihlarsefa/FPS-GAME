using System.Collections.Generic;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Online.Netcode
{
    /// <summary>
    /// İstemci tarafı hareket tahmini ve sunucu durumu ile uzlaştırma.
    /// </summary>
    public sealed class ClientPredictionController : MonoBehaviour
    {
        private const float PositionErrorTolerance = 0.08f;
        private const int MaxBufferedCommands = 64;

        private readonly Queue<PlayerCommand> _pending = new();
        private NetworkPlayer _player;
        private uint _lastAckedTick;
        private bool _bound;

        public void Bind(NetworkPlayer player)
        {
            _player = player;
            _bound = player != null;
        }

        public void PredictLocal(PlayerCommand command, float moveSpeed, float sprintMultiplier, float dt)
        {
            if (!_bound || _player == null || !_player.IsOwner || _player.IsServer)
                return;

            while (_pending.Count >= MaxBufferedCommands)
                _pending.Dequeue();

            _pending.Enqueue(command);
            ApplyMovement(command, moveSpeed, sprintMultiplier, dt);
        }

        public void AcknowledgeServerState(NetworkReconciliationSnapshot snapshot)
        {
            Reconcile(snapshot);
        }

        public void Reconcile(NetworkReconciliationSnapshot snapshot)
        {
            if (!_bound || _player == null || !_player.IsOwner)
                return;

            _lastAckedTick = snapshot.Tick;

            while (_pending.Count > 0 && _pending.Peek().Tick <= snapshot.Tick)
                _pending.Dequeue();

            var error = Vector3.Distance(transform.position, snapshot.Position);
            if (error > PositionErrorTolerance)
            {
                transform.position = snapshot.Position;
                transform.rotation = Quaternion.Euler(0f, snapshot.Yaw, 0f);

                // Onaylanmamış komutları yeniden uygula
                var dt = 1f / NetcodeNetworkSession.SimulationHz;
                foreach (var cmd in _pending)
                    ApplyMovement(cmd, 5.5f, 1.45f, dt);
            }
        }

        public uint LastAckedTick => _lastAckedTick;

        private void ApplyMovement(PlayerCommand command, float moveSpeed, float sprintMultiplier, float dt)
        {
            transform.rotation = Quaternion.Euler(0f, command.Yaw, 0f);
            var input = new Vector3(command.MoveRight, 0f, command.MoveForward);
            if (input.sqrMagnitude > 1f)
                input.Normalize();

            var speed = moveSpeed * (command.Has(PlayerButtons.Sprint) ? sprintMultiplier : 1f);
            var delta = (transform.forward * input.z + transform.right * input.x) * speed * dt;
            transform.position += delta;
        }
    }
}
