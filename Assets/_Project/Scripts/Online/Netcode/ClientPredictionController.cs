using Project.Core.Domain;
using Project.Online.Sim;
using UnityEngine;

namespace Project.Online.Netcode
{
    /// <summary>
    /// İstemci tarafı hareket tahmini ve sunucu durumu ile uzlaştırma.
    /// Komutlar <see cref="PredictionBuffer"/> içinde sıra numarasıyla tutulur; sunucu ack'i geldiğinde
    /// o komut sonrası TAHMİN EDİLEN konum ile sunucu konumu karşılaştırılır (şimdiki konumla değil),
    /// fark toleransı aşarsa sunucu konumuna dönülüp onaylanmamış komutlar yeniden oynatılır.
    /// </summary>
    public sealed class ClientPredictionController : MonoBehaviour
    {
        public const float PositionErrorTolerance = 0.25f;
        /// <summary>Bu hatadan büyükse yumuşatmadan anında ışınla.</summary>
        public const float SnapDistance = 3f;

        private readonly PredictionBuffer _buffer = new(128);
        private NetworkPlayer _player;
        private uint _lastAckedTick;
        private uint _prevSequence;
        private bool _bound;
        private Vector3 _smoothOffset;

        public int PendingCount => _buffer.Count;
        public uint LastAckedTick => _lastAckedTick;
        public float LastError { get; private set; }

        public void Bind(NetworkPlayer player)
        {
            _player = player;
            _bound = player != null;
            _buffer.Clear();
            _prevSequence = 0;
        }

        /// <summary>
        /// Hareketi başka bir sistem (PlayerController) uyguluyorsa: yalnızca komutu kaydeder. Bir önceki komutun
        /// uygulanmış sonuç konumu şimdiki konumdur; böylece tahmin kayıtları gerçek sonuçla eşleşir.
        /// </summary>
        public void RecordLocal(PlayerCommand command)
        {
            if (!CanPredict())
                return;

            if (_prevSequence != 0)
                _buffer.SetPrediction(_prevSequence, transform.position);

            var seq = command.Tick != 0 ? command.Tick : _buffer.NextSequence();
            if (_buffer.Record(seq, command))
                _prevSequence = seq;
        }

        /// <summary>Hareketi bu bileşen uygular (kinematik tahmin) ve kaydeder.</summary>
        public void PredictLocal(PlayerCommand command, float moveSpeed, float sprintMultiplier, float dt)
        {
            if (!CanPredict())
                return;

            var seq = command.Tick != 0 ? command.Tick : _buffer.NextSequence();
            if (!_buffer.Record(seq, command))
                return;

            ApplyMovement(command, moveSpeed, sprintMultiplier, dt);
            _buffer.SetPrediction(seq, transform.position);
            _prevSequence = 0;
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
            if (!_buffer.Acknowledge(snapshot.Tick, snapshot.Position, PositionErrorTolerance, out var error))
            {
                LastError = error;
                return;
            }

            LastError = error;
            var before = transform.position;
            transform.position = snapshot.Position;

            var dt = 1f / NetcodeNetworkSession.SimulationHz;
            var move = _player.MoveSpeed;
            var sprint = _player.SprintMultiplier;
            for (var i = 0; i < _buffer.Count; i++)
            {
                var entry = _buffer.At(i);
                ApplyMovement(entry.Command, move, sprint, dt);
                _buffer.SetPrediction(entry.Sequence, transform.position);
            }

            // Yön: yalnızca yerel girdi yönünü bozma; sunucu yaw'ı yalnızca bekleyen komut yoksa uygulanır.
            if (_buffer.Count == 0)
                transform.rotation = Quaternion.Euler(0f, snapshot.Yaw, 0f);

            // Küçük düzeltmeler görsel olarak yumuşatılmak üzere kalan fark ofset olarak bırakılır.
            var residual = before - transform.position;
            if (ReconcilePolicy.Classify(error, PositionErrorTolerance, SnapDistance) == ReconcilePolicy.Action.Smooth)
            {
                _smoothOffset = residual;
                transform.position += residual; // eski görünür konumdan başla, LateUpdate'te sıfıra erit
            }
            else
            {
                _smoothOffset = Vector3.zero;
            }
        }

        private void LateUpdate()
        {
            if (_smoothOffset.sqrMagnitude < 1e-6f)
                return;

            var step = ReconcilePolicy.SmoothFactor(Time.deltaTime, 12f); // kareden bağımsız üstel erime
            var delta = _smoothOffset * step;
            transform.position -= delta;
            _smoothOffset -= delta;
        }

        private bool CanPredict() => _bound && _player != null && _player.IsOwner && !_player.IsServer;

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
