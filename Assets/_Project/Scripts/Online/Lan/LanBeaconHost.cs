using System;
using Project.Online.Bootstrap;
using UnityEngine;

namespace Project.Online.Lan
{
    /// <summary>
    /// Sunucu kurulduktan sonra panel kapansa bile LAN beacon yayınını sürdüren görünmez çalıştırıcı.
    /// Bağlantı bitince (OnlineSessionBridge.IsConnected false) kendini kapatır.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LanBeaconHost : MonoBehaviour
    {
        private static LanBeaconHost _instance;
        private LanDiscovery _discovery;
        private Func<LanBeacon> _source;
        private float _grace;

        public static void Begin(Func<LanBeacon> source)
        {
            if (_instance == null)
            {
                var go = new GameObject("[LanBeaconHost]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<LanBeaconHost>();
            }

            _instance._source = source;
            _instance._grace = 3f;
            if (_instance._discovery == null)
                _instance._discovery = new LanDiscovery();
            _instance._discovery.StartBroadcasting(source);
        }

        public static void End()
        {
            if (_instance != null)
                Destroy(_instance.gameObject);
            _instance = null;
        }

        private void Update()
        {
            _grace -= Time.unscaledDeltaTime;
            var up = OnlineSessionBridge.IsConnected != null && OnlineSessionBridge.IsConnected();
            if (!up && _grace <= 0f)
            {
                End();
                return;
            }

            _discovery?.Tick(Time.realtimeSinceStartupAsDouble);
        }

        private void OnDestroy()
        {
            _discovery?.Dispose();
            _discovery = null;
            if (_instance == this)
                _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }
}
