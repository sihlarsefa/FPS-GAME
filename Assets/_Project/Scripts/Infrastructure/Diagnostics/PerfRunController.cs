using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Project.Core.Domain;
using Project.Infrastructure.AI;
using Project.Infrastructure.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Infrastructure.Diagnostics
{
    /// <summary>
    /// <c>-perfrun</c>: KuzgunVadisi'nde 60 botla 3 dakikalık sabit kamera turu;
    /// sonuçları <c>Logs/perf_*.json</c> yazar; isteğe bağlı Backend'e gönderir.
    /// </summary>
    public sealed class PerfRunController : MonoBehaviour
    {
        public const int DefaultBotCount = 60;
        public const float DefaultDurationSeconds = 180f;
        public const string TargetScene = "KuzgunVadisi";

        private static int _bootstrapped;
        private PerfSampler _sampler;
        private readonly List<float> _frameHistory = new(4096);
        private readonly List<BotController> _spawned = new(64);
        private float _elapsed;
        private float _duration = DefaultDurationSeconds;
        private int _botTarget = DefaultBotCount;
        private bool _botsReady;
        private bool _finished;
        private Camera _cam;
        private Vector3 _orbitCenter = new(0f, 40f, 0f);
        private float _orbitRadius = 180f;
        private float _orbitHeight = 55f;
        private float _orbitSpeedDeg = 12f;

        public static bool IsRequested => DiagnosticsCommandLine.HasArg("-perfrun");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_bootstrapped != 0) return;
            if (!IsRequested) return;
            _bootstrapped = 1;

            var go = new GameObject("[PerfRun]");
            DontDestroyOnLoad(go);
            go.AddComponent<PerfRunController>();
        }

        private void Awake()
        {
            if (DiagnosticsCommandLine.TryGetFloat("-perfrun", out var dur) && dur > 5f)
                _duration = Mathf.Clamp(dur, 10f, 3600f);
            else if (DiagnosticsCommandLine.TryGetInt("-perfduration", out var sec) && sec > 5)
                _duration = Mathf.Clamp(sec, 10, 3600);

            if (DiagnosticsCommandLine.TryGetInt("-perfbots", out var bots) && bots > 0)
                _botTarget = Mathf.Clamp(bots, 1, 120);

            _sampler = new PerfSampler(600);
            Debug.Log($"[PerfRun] Başlıyor: {_botTarget} bot, {_duration:0}s, sahne hedefi={TargetScene}");
        }

        private void Start()
        {
            EnsureScene();
            EnsureCamera();
            SpawnBots();
            _botsReady = true;
        }

        private void Update()
        {
            if (_finished) return;

            _sampler.SampleFrame();
            _frameHistory.Add(_sampler.LastFrameMs);
            _elapsed += Time.unscaledDeltaTime;

            OrbitCamera();

            if (_botsReady && _elapsed >= _duration)
                Finish();
        }

        private void OnDestroy()
        {
            _sampler?.Dispose();
        }

        private void EnsureScene()
        {
            var active = SceneManager.GetActiveScene();
            if (active.IsValid() && string.Equals(active.name, TargetScene, StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                var op = SceneManager.LoadSceneAsync(TargetScene, LoadSceneMode.Single);
                if (op != null)
                {
                    // Senkron bekleme yok; Start sonrası ilk karelerde sahne değişebilir.
                    // Botlar mevcut sahnede de doğabilir — koşu yine geçerli örnek üretir.
                    Debug.Log("[PerfRun] KuzgunVadisi yükleniyor…");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PerfRun] Sahne yüklenemedi (mevcut sahnede devam): " + e.Message);
            }
        }

        private void EnsureCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var go = new GameObject("PerfRunCamera");
                _cam = go.AddComponent<Camera>();
                go.tag = "MainCamera";
                go.AddComponent<AudioListener>();
            }

            _cam.transform.position = _orbitCenter + new Vector3(_orbitRadius, _orbitHeight, 0f);
            _cam.transform.LookAt(_orbitCenter);
        }

        private void OrbitCamera()
        {
            if (_cam == null) return;
            var angle = (_elapsed * _orbitSpeedDeg) * Mathf.Deg2Rad;
            var pos = _orbitCenter + new Vector3(Mathf.Cos(angle) * _orbitRadius, _orbitHeight, Mathf.Sin(angle) * _orbitRadius);
            _cam.transform.position = pos;
            _cam.transform.LookAt(_orbitCenter + Vector3.up * 8f);
        }

        private void SpawnBots()
        {
            var existing = BotController.All != null ? BotController.All.Count : 0;
            var need = Mathf.Max(0, _botTarget - existing);
            if (need == 0)
            {
                Debug.Log($"[PerfRun] Zaten {existing} bot var; ek doğuş yok.");
                return;
            }

            var ring = 40f;
            for (var i = 0; i < need; i++)
            {
                var team = (i % 6) + 1;
                var angle = (i / (float)need) * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Cos(angle) * ring, 30f, Mathf.Sin(angle) * ring);
                if (Physics.Raycast(pos + Vector3.up * 80f, Vector3.down, out var hit, 200f))
                    pos = hit.point;

                var args = new BotSpawnArgs
                {
                    Id = new PlayerId(90000 + i),
                    Name = "Perf" + i,
                    Team = team,
                    Role = TeamRole.Rifleman,
                    Difficulty = BotDifficulty.Normal,
                    SpawnOnGround = true,
                    GroundPosition = pos,
                    GroundYaw = angle * Mathf.Rad2Deg,
                    Slot = i % 10,
                    Seed = 1923 + i,
                    RegisterWithMatch = false,
                    DropLootOnDeath = false
                };

                try
                {
                    var bot = BotController.Create(args);
                    if (bot != null)
                        _spawned.Add(bot);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[PerfRun] Bot doğuşu başarısız: " + e.Message);
                }
            }

            Debug.Log($"[PerfRun] {_spawned.Count} bot doğuruldu (hedef {_botTarget}, önceden {existing}).");
        }

        private void Finish()
        {
            if (_finished) return;
            _finished = true;

            var report = BuildReport();
            var path = WriteReport(report);
            Debug.Log("[PerfRun] Tamamlandı → " + path);

            if (!DiagnosticsCommandLine.HasArg("-notelemetry"))
                TryUpload(report);

            if (DiagnosticsCommandLine.HasArg("-quit") || DiagnosticsCommandLine.HasArg("-perfrun"))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                UnityEngine.Application.Quit(0);
#endif
            }
        }

        private PerfRunReport BuildReport()
        {
            var botCount = BotController.All != null ? BotController.All.Count : _spawned.Count;
            var projectiles = BallisticsSystem.Instance != null ? BallisticsSystem.Instance.ActiveProjectiles : 0;

            // %1 low: frameHistory üzerinden
            var sorted = _frameHistory.ToArray();
            Array.Sort(sorted);
            var p1Index = sorted.Length == 0 ? 0 : Mathf.Clamp(sorted.Length - 1 - sorted.Length / 100, 0, sorted.Length - 1);
            var onePctMs = sorted.Length == 0 ? 0f : sorted[p1Index];
            double sum = 0;
            for (var i = 0; i < sorted.Length; i++) sum += sorted[i];
            var avgMs = sorted.Length == 0 ? 0.0 : sum / sorted.Length;

            return new PerfRunReport
            {
                Id = Guid.NewGuid().ToString("N"),
                Scene = SceneManager.GetActiveScene().name,
                DurationSeconds = _elapsed,
                TargetDurationSeconds = _duration,
                BotTarget = _botTarget,
                BotCount = botCount,
                ActiveProjectiles = projectiles,
                FrameCount = _frameHistory.Count,
                AvgFrameMs = (float)avgMs,
                AvgFps = avgMs > 0.0001 ? (float)(1000.0 / avgMs) : 0f,
                OnePercentLowMs = onePctMs,
                OnePercentLowFps = onePctMs > 0.0001f ? 1000f / onePctMs : 0f,
                GcAllocBytesLast = _sampler.GcAllocBytesLast,
                DrawCallsLast = _sampler.DrawCallsLast,
                BatchesLast = _sampler.BatchesLast,
                DeviceModel = SystemInfo.deviceModel,
                GraphicsDeviceName = SystemInfo.graphicsDeviceName,
                OperatingSystem = SystemInfo.operatingSystem,
                UnityVersion = UnityEngine.Application.unityVersion,
                AppVersion = UnityEngine.Application.version,
                CreatedAtUtc = DateTimeOffset.UtcNow.ToString("o"),
                FrameMsSampleStride = Mathf.Max(1, _frameHistory.Count / 300),
                FrameMsSamples = Downsample(_frameHistory, 300)
            };
        }

        private static float[] Downsample(List<float> source, int max)
        {
            if (source.Count == 0) return Array.Empty<float>();
            if (source.Count <= max) return source.ToArray();
            var result = new float[max];
            for (var i = 0; i < max; i++)
            {
                var idx = (int)((i / (float)(max - 1)) * (source.Count - 1));
                result[i] = source[idx];
            }

            return result;
        }

        private static string WriteReport(PerfRunReport report)
        {
            var dir = DiagnosticsPaths.LogsDirectory;
            Directory.CreateDirectory(dir);
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var path = Path.Combine(dir, "perf_" + stamp + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true), Encoding.UTF8);
            return path;
        }

        private static void TryUpload(PerfRunReport report)
        {
            // İsteğe bağlı: client-errors kanalına özet not olarak da düşürülebilir; asıl dosya yerelde.
            var note = ClientLogCollector.Ensure().BuildReport(
                "perfrun",
                "PerfRun",
                $"avgFps={report.AvgFps:0.0} 1%low={report.OnePercentLowFps:0.0} bots={report.BotCount}",
                JsonUtility.ToJson(report));
            ClientErrorReporter.Submit(note);
        }
    }

    [Serializable]
    public sealed class PerfRunReport
    {
        public string Id;
        public string Scene;
        public float DurationSeconds;
        public float TargetDurationSeconds;
        public int BotTarget;
        public int BotCount;
        public int ActiveProjectiles;
        public int FrameCount;
        public float AvgFrameMs;
        public float AvgFps;
        public float OnePercentLowMs;
        public float OnePercentLowFps;
        public long GcAllocBytesLast;
        public long DrawCallsLast;
        public long BatchesLast;
        public string DeviceModel;
        public string GraphicsDeviceName;
        public string OperatingSystem;
        public string UnityVersion;
        public string AppVersion;
        public string CreatedAtUtc;
        public int FrameMsSampleStride;
        public float[] FrameMsSamples;
    }
}
