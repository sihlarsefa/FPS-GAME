using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace Project.Infrastructure.Diagnostics
{
    /// <summary>
    /// İstemci hata raporunu diske yazar ve isteğe bağlı olarak Backend telemetri ucuna gönderir.
    /// Uç: <c>{base}/client-errors</c> (Web proxy: <c>/telemetry/client-errors</c>).
    /// </summary>
    public static class ClientErrorReporter
    {
        private static string _endpointOverride;
        private static int _submitting;
        private static int _failures;
        private static double _nextAttemptAt;
        private const int MaxFailures = 5;

        /// <summary>Backend yapılandırılmış mı (-telemetry/-backend ya da SetEndpointOverride). Varsayılan localhost yalnızca deneme içindir.</summary>
        public static bool BackendConfigured =>
            !string.IsNullOrEmpty(_endpointOverride)
            || (DiagnosticsCommandLine.TryGetArg("-telemetry", out var t) && !string.IsNullOrWhiteSpace(t))
            || (DiagnosticsCommandLine.TryGetArg("-backend", out var b) && !string.IsNullOrWhiteSpace(b));

        private static double Now => System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;

        /// <summary>Üstel geri çekilme (5 sn, 10, 20...; en çok 5 dk).</summary>
        public static double BackoffSeconds(int failures) => failures <= 0 ? 0 : Math.Min(300.0, 5.0 * Math.Pow(2, failures - 1));

        private static bool ShouldSend(bool sync)
        {
            if (!BackendConfigured || _failures >= MaxFailures)
                return false;
            return sync || Now >= _nextAttemptAt;
        }

        private static void RecordResult(bool ok)
        {
            if (ok) { _failures = 0; _nextAttemptAt = 0; return; }
            _failures++;
            _nextAttemptAt = Now + BackoffSeconds(_failures);
            if (_failures == MaxFailures)
                Debug.LogWarning("[Diagnostics] Telemetri art arda başarısız; gönderim kapatıldı (yerel dosya yazımı sürer).");
        }

        public static string DefaultEndpoint
        {
            get
            {
                if (!string.IsNullOrEmpty(_endpointOverride))
                    return _endpointOverride;

                if (DiagnosticsCommandLine.TryGetArg("-telemetry", out var url) && !string.IsNullOrWhiteSpace(url))
                    return url.TrimEnd('/') + "/client-errors";

                if (DiagnosticsCommandLine.TryGetArg("-backend", out var backend) && !string.IsNullOrWhiteSpace(backend))
                    return backend.TrimEnd('/') + "/client-errors";

                return "http://localhost:5081/client-errors";
            }
        }

        public static void SetEndpointOverride(string url) => _endpointOverride = url;

        public static string Submit(ClientErrorReport report, bool sync = false)
        {
            if (report == null)
                return null;

            var path = WriteLocal(report);
            if (DiagnosticsCommandLine.HasArg("-notelemetry"))
                return path;

            try
            {
                if (!ShouldSend(sync))
                    return path;
                var json = JsonUtility.ToJson(report);
                if (sync)
                    PostSync(DefaultEndpoint, json);
                else if (Interlocked.CompareExchange(ref _submitting, 1, 0) == 0)
                    PostAsync(DefaultEndpoint, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Diagnostics] Telemetri gönderimi başarısız: " + e.Message);
                Interlocked.Exchange(ref _submitting, 0);
            }

            return path;
        }

        public static string WriteLocal(ClientErrorReport report)
        {
            var dir = DiagnosticsPaths.LogsDirectory;
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "client_error_" + (report.Id ?? Guid.NewGuid().ToString("N")) + ".json");
            File.WriteAllText(file, JsonUtility.ToJson(report, true), Encoding.UTF8);
            return file;
        }

        private static void PostAsync(string url, string json)
        {
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            var body = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 8;
            var op = request.SendWebRequest();
            op.completed += _ =>
            {
                var ok = request.result == UnityWebRequest.Result.Success;
                if (!ok && _failures == 0)
                    Debug.LogWarning("[Diagnostics] client-errors HTTP: " + request.responseCode + " " + request.error);
                RecordResult(ok);
                request.Dispose();
                Interlocked.Exchange(ref _submitting, 0);
            };
        }

        private static void PostSync(string url, string json)
        {
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            var body = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 5;
            var op = request.SendWebRequest();
            while (!op.isDone)
                Thread.Sleep(10);
            var ok = request.result == UnityWebRequest.Result.Success;
            if (!ok && _failures == 0)
                Debug.LogWarning("[Diagnostics] client-errors sync HTTP: " + request.responseCode + " " + request.error);
            RecordResult(ok);
        }
    }
}
