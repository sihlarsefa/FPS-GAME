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
                if (request.result != UnityWebRequest.Result.Success)
                    Debug.LogWarning("[Diagnostics] client-errors HTTP: " + request.responseCode + " " + request.error);
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
            if (request.result != UnityWebRequest.Result.Success)
                Debug.LogWarning("[Diagnostics] client-errors sync HTTP: " + request.responseCode + " " + request.error);
        }
    }
}
