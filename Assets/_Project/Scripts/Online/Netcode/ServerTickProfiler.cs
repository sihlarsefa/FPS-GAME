using System.Diagnostics;
using Project.Online.Sim;
using UnityEngine;

namespace Project.Online.Netcode
{
    /// <summary>
    /// Sunucu tick bütçesi ölçümü: her karenin betik süresi (Update başı → LateUpdate sonu) ServerTickMetrics'e yazılır;
    /// bütçe (1/tickHz) aşımı yüksekse periyodik uyarı basılır. Yalnızca sunucu sürecinde çalışır.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class ServerTickProfiler : MonoBehaviour
    {
        public const float ReportIntervalSeconds = 10f;

        private readonly Stopwatch _watch = new();
        private ServerTickMetrics _metrics;
        private float _nextReport;
        private bool _measuring;

        public static ServerTickProfiler Instance { get; private set; }
        public ServerTickMetrics Metrics => _metrics;

        public static ServerTickProfiler Ensure(float tickRateHz)
        {
            if (Instance != null)
                return Instance;

            var go = new GameObject("ServerTickProfiler");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<ServerTickProfiler>();
            Instance._metrics = new ServerTickMetrics(tickRateHz);
            Instance.gameObject.AddComponent<ServerTickProfilerTail>().Bind(Instance);
            return Instance;
        }

        private void Update()
        {
            _watch.Restart();
            _measuring = true;
        }

        internal void EndFrame()
        {
            if (!_measuring || _metrics == null)
                return;

            _measuring = false;
            _metrics.Record((float)_watch.Elapsed.TotalSeconds);
            if (Time.unscaledTime >= _nextReport)
            {
                _nextReport = Time.unscaledTime + ReportIntervalSeconds;
                if (!_metrics.IsHealthy)
                    UnityEngine.Debug.LogWarning("[Netcode] Sunucu tick bütçesi baskı altında: " + _metrics.Summary());
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }

    /// <summary>LateUpdate sonunda (en geç yürütme sırası) ölçümü kapatır.</summary>
    [DefaultExecutionOrder(32000)]
    internal sealed class ServerTickProfilerTail : MonoBehaviour
    {
        private ServerTickProfiler _owner;
        public void Bind(ServerTickProfiler owner) => _owner = owner;
        private void LateUpdate() => _owner?.EndFrame();
    }
}
