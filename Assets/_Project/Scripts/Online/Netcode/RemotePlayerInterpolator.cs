using Project.Online.Sim;
using Unity.Netcode;
using UnityEngine;

namespace Project.Online.Netcode
{
    /// <summary>
    /// Uzak oyuncu görsel interpolasyonu (yalnızca saf istemci, sahip olmayan): NetworkTransform'un yazdığı ham
    /// konumlar snapshot olarak biriktirilir, render zamanı = sunucu zamanı − uyarlanır gecikme (JitterEstimator)
    /// ile LateUpdate'te enterpole poz uygulanır. Gecikme <see cref="CurrentDelay"/> ile atış geri sarma tick'ine yansır.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    [DisallowMultipleComponent]
    public sealed class RemotePlayerInterpolator : MonoBehaviour
    {
        /// <summary>En son değerlendirilen uzak oyuncunun interpolasyon gecikmesi (sn); atış tick'i kestirimi için.</summary>
        public static float CurrentDelay { get; private set; }

        private readonly RemoteInterpolation _state = new(NetcodeNetworkSession.SimulationHz);
        private NetworkManager _manager;
        private Vector3 _lastWritten;
        private float _lastYaw;
        private bool _hasWritten;

        public RemoteInterpolation State => _state;

        private void OnEnable()
        {
            _hasWritten = false;
            _state.Clear();
        }

        private void LateUpdate()
        {
            _manager ??= NetworkManager.Singleton;
            if (_manager == null || !_manager.IsListening)
                return;

            var t = transform;
            var pos = t.position;
            var yaw = t.eulerAngles.y;
            var serverNow = (float)_manager.ServerTime.Time;

            // Ham (NetworkTransform) değişim: bizim son yazdığımızdan farklıysa yeni snapshot.
            if (!_hasWritten || pos != _lastWritten || !Mathf.Approximately(yaw, _lastYaw))
                _state.Feed(Time.unscaledTime, serverNow, pos, yaw);

            if (_state.Evaluate(serverNow, Time.unscaledDeltaTime, out var pose))
            {
                t.position = pose.Position;
                t.rotation = Quaternion.Euler(0f, pose.Yaw, 0f);
                _lastWritten = t.position;
                _lastYaw = t.eulerAngles.y;
                _hasWritten = true;
                CurrentDelay = _state.Delay;
            }
        }
    }
}
