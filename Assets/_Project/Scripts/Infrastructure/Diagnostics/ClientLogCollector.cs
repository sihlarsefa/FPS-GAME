using System;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Infrastructure.Diagnostics
{
    /// <summary>
    /// <see cref="UnityEngine.Application.logMessageReceivedThreaded"/> ile son 500 satırı halka tamponda tutar.
    /// Exception ve uygulama çıkışında rapor üretir; isteğe bağlı Backend'e gönderir.
    /// </summary>
    public sealed class ClientLogCollector : MonoBehaviour
    {
        public const int RingCapacity = 500;

        private static ClientLogCollector _instance;
        private static int _bootstrapped;

        private readonly object _gate = new();
        private readonly ClientLogEntry[] _ring = new ClientLogEntry[RingCapacity];
        private int _writeIndex;
        private int _count;
        private bool _quitHooked;
        private bool _exceptionReportQueued;
        private string _pendingExceptionType;
        private string _pendingMessage;
        private string _pendingStack;

        public static ClientLogCollector Instance => _instance;

        public int Count
        {
            get
            {
                lock (_gate) return _count;
            }
        }

        public static ClientLogCollector Ensure()
        {
            if (_instance != null)
                return _instance;

            var go = new GameObject("[ClientLogCollector]");
            DontDestroyOnLoad(go);
            return go.AddComponent<ClientLogCollector>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Interlocked.Exchange(ref _bootstrapped, 1) != 0)
                return;
            Ensure();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            UnityEngine.Application.logMessageReceivedThreaded += OnLogThreaded;
            if (!_quitHooked)
            {
                UnityEngine.Application.quitting += OnQuitting;
                _quitHooked = true;
            }
        }

        private void OnDestroy()
        {
            UnityEngine.Application.logMessageReceivedThreaded -= OnLogThreaded;
            if (_quitHooked && _instance == this)
            {
                UnityEngine.Application.quitting -= OnQuitting;
                _quitHooked = false;
            }

            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            if (!_exceptionReportQueued)
                return;

            _exceptionReportQueued = false;
            var report = BuildReport("exception", _pendingExceptionType, _pendingMessage, _pendingStack);
            ClientErrorReporter.Submit(report);
        }

        private void OnLogThreaded(string condition, string stackTrace, LogType type)
        {
            var entry = new ClientLogEntry(DateTimeOffset.UtcNow, type, condition, stackTrace);
            lock (_gate)
            {
                _ring[_writeIndex] = entry;
                _writeIndex = (_writeIndex + 1) % RingCapacity;
                if (_count < RingCapacity)
                    _count++;
            }

            if (type != LogType.Exception && type != LogType.Assert)
                return;

            _pendingExceptionType = type.ToString();
            _pendingMessage = condition;
            _pendingStack = stackTrace;
            _exceptionReportQueued = true;
        }

        private void OnQuitting()
        {
            try
            {
                var report = BuildReport("quit", null, "Application quitting", null);
                ClientErrorReporter.Submit(report, sync: true);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Diagnostics] Çıkış raporu yazılamadı: " + e.Message);
            }
        }

        public ClientLogEntry[] Snapshot()
        {
            lock (_gate)
            {
                var result = new ClientLogEntry[_count];
                var start = _count < RingCapacity ? 0 : _writeIndex;
                for (var i = 0; i < _count; i++)
                    result[i] = _ring[(start + i) % RingCapacity];
                return result;
            }
        }

        public string[] SnapshotLines()
        {
            var entries = Snapshot();
            var lines = new string[entries.Length];
            for (var i = 0; i < entries.Length; i++)
                lines[i] = entries[i].ToString();
            return lines;
        }

        public ClientErrorReport BuildReport(string trigger, string exceptionType, string message, string stackTrace)
        {
            var logs = SnapshotLines();
            return new ClientErrorReport
            {
                Id = Guid.NewGuid().ToString("N"),
                Trigger = trigger ?? "manual",
                Version = UnityEngine.Application.version,
                Scene = SafeSceneName(),
                Platform = UnityEngine.Application.platform.ToString(),
                DeviceModel = SystemInfo.deviceModel,
                OperatingSystem = SystemInfo.operatingSystem,
                ProcessorType = SystemInfo.processorType,
                ProcessorCount = SystemInfo.processorCount,
                SystemMemoryMb = SystemInfo.systemMemorySize,
                GraphicsDeviceName = SystemInfo.graphicsDeviceName,
                GraphicsMemoryMb = SystemInfo.graphicsMemorySize,
                UnityVersion = UnityEngine.Application.unityVersion,
                ExceptionType = exceptionType ?? string.Empty,
                Message = message ?? string.Empty,
                StackTrace = stackTrace ?? string.Empty,
                RecentLogs = logs,
                CreatedAtUtc = DateTimeOffset.UtcNow.ToString("o")
            };
        }

        public string FormatRingDump()
        {
            var sb = new StringBuilder(Math.Max(256, Count * 64));
            foreach (var line in SnapshotLines())
                sb.AppendLine(line);
            return sb.ToString();
        }

        private static string SafeSceneName()
        {
            try
            {
                var scene = SceneManager.GetActiveScene();
                return scene.IsValid() ? scene.name : "(none)";
            }
            catch
            {
                return "(unknown)";
            }
        }
    }
}
