using UnityEngine;

namespace Project.Online.Sim
{
    /// <summary>
    /// Uzak oyuncu için birleşik interpolasyon durumu (saf): snapshot varışlarını <see cref="JitterEstimator"/> ile
    /// ölçer, render zamanını (sunucu zamanı − uyarlanır gecikme) üretir ve <see cref="SnapshotInterpolator"/>
    /// üzerinden poz verir. Büyük sıçrama (ışınlanma/yeniden doğma) tamponu sıfırlar.
    /// </summary>
    public sealed class RemoteInterpolation
    {
        public const float TeleportDistance = 12f;

        private readonly JitterEstimator _jitter;
        private readonly SnapshotInterpolator _buffer;
        private Vector3 _lastPosition;

        public RemoteInterpolation(float tickRate = 30f, int capacity = 32)
        {
            _jitter = new JitterEstimator(tickRate);
            _buffer = new SnapshotInterpolator(capacity);
        }

        public float Delay => _jitter.Delay;
        public float Jitter => _jitter.Jitter;
        public int Count => _buffer.Count;
        public int Teleports { get; private set; }

        /// <summary>arrivalTime: yerel zaman; serverTime: snapshot'ın sunucu zaman damgası.</summary>
        public bool Feed(float arrivalTime, float serverTime, Vector3 position, float yaw)
        {
            if (_buffer.Count > 0 && (position - _lastPosition).sqrMagnitude > TeleportDistance * TeleportDistance)
            {
                _buffer.Clear();
                Teleports++;
            }

            if (!_buffer.Add(serverTime, position, yaw))
                return false;

            _jitter.Observe(arrivalTime);
            _lastPosition = position;
            return true;
        }

        /// <summary>serverNow: şu anki tahmini sunucu zamanı. Gecikme dt ile yumuşak kayar.</summary>
        public bool Evaluate(float serverNow, float dt, out SnapshotInterpolator.Pose pose)
        {
            var delay = _jitter.Advance(dt);
            return _buffer.TrySample(serverNow - delay, out pose);
        }

        public void Clear()
        {
            _buffer.Clear();
            Teleports = 0;
        }
    }

    /// <summary>İstemci uzlaştırma politikası (saf): hata → karar ve görsel ofset erimesi.</summary>
    public static class ReconcilePolicy
    {
        public enum Action : byte { None, Smooth, Snap }

        public static Action Classify(float error, float tolerance, float snapDistance)
        {
            if (error <= tolerance) return Action.None;
            return error >= snapDistance ? Action.Snap : Action.Smooth;
        }

        /// <summary>Kareden bağımsız üstel erime katsayısı (0..1): 1 - e^(-rate·dt).</summary>
        public static float SmoothFactor(float dt, float rate = 12f) =>
            1f - Mathf.Exp(-Mathf.Max(0f, rate) * Mathf.Max(0f, dt));
    }
}
