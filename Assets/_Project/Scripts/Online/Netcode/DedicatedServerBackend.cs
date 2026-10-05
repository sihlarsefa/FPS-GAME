using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Project.Online.Netcode
{
    /// <summary>
    /// Dedicated server → backend kayıt / heartbeat / maç sonucu.
    /// DTO alanları Backend <c>RegisterServerRequest</c>, <c>ServerHeartbeatRequest</c>, <c>MatchResultRequest</c> ile uyumlu.
    /// </summary>
    public sealed class DedicatedServerBackend
    {
        private readonly string _backendUrl;
        private readonly string _serverKey;
        private Guid _serverId;
        private string _host;
        private int _port;
        private string _region;
        private int _maxPlayers;

        public DedicatedServerBackend(string backendUrl, string serverKey)
        {
            _backendUrl = TrimSlash(backendUrl ?? string.Empty);
            _serverKey = serverKey ?? string.Empty;
        }

        public Guid ServerId => _serverId;
        public bool IsRegistered => _serverId != Guid.Empty;
        public string BackendUrl => _backendUrl;

        public async Task<bool> RegisterAsync(string host, int port, string region, int maxPlayers)
        {
            _host = host;
            _port = port;
            _region = string.IsNullOrWhiteSpace(region) ? "tr" : region.Trim();
            _maxPlayers = maxPlayers <= 0 ? InterestManager.DefaultMaxPlayers : maxPlayers;

            if (string.IsNullOrEmpty(_backendUrl) || string.IsNullOrEmpty(_serverKey))
            {
                Debug.LogWarning("[ServerBackend] Backend URL veya serverKey yok — kayıt atlandı.");
                return false;
            }

            var body = JsonUtility.ToJson(new RegisterBody
            {
                Host = string.IsNullOrWhiteSpace(host) ? "0.0.0.0" : host,
                Port = port,
                Region = _region,
                ServerKey = _serverKey,
                MaxPlayers = _maxPlayers
            });

            var (ok, json) = await PostAsync("/servers/register", body, auth: false);
            if (!ok)
                return false;

            var dto = JsonUtility.FromJson<ServerDtoBody>(json);
            if (dto == null || string.IsNullOrEmpty(dto.Id) || !Guid.TryParse(dto.Id, out _serverId))
            {
                Debug.LogError("[ServerBackend] Kayıt yanıtı geçersiz.");
                return false;
            }

            Debug.Log($"[ServerBackend] Kayıt tamam: {_serverId} ({_host}:{_port}, {_region}).");
            return true;
        }

        public async Task<bool> HeartbeatAsync(int currentPlayers, string status = "idle")
        {
            if (!IsRegistered)
                return false;

            var body = JsonUtility.ToJson(new HeartbeatBody
            {
                ServerId = _serverId.ToString(),
                ServerKey = _serverKey,
                CurrentPlayers = Mathf.Max(0, currentPlayers),
                Status = string.IsNullOrWhiteSpace(status) ? "idle" : status
            });

            var (ok, _) = await PostAsync("/servers/heartbeat", body, auth: false);
            return ok;
        }

        public async Task<bool> SubmitMatchResultAsync(Guid matchId, string teamsJson)
        {
            if (!IsRegistered || matchId == Guid.Empty)
                return false;

            // teamsJson: MatchResultRequest gövdesi {"Teams":[...]} — dışarıdan hazırlanır
            var payload = string.IsNullOrWhiteSpace(teamsJson) ? "{\"Teams\":[]}" : teamsJson;
            var path = $"/matches/{matchId:D}/result";
            var (ok, _) = await PostAsync(path, payload, auth: true);
            if (ok)
                Debug.Log($"[ServerBackend] Maç sonucu gönderildi: {matchId}");
            return ok;
        }

        private async Task<(bool ok, string json)> PostAsync(string path, string jsonBody, bool auth)
        {
            var url = _backendUrl + path;
            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            var bytes = Encoding.UTF8.GetBytes(jsonBody ?? "{}");
            req.uploadHandler = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            if (auth)
            {
                req.SetRequestHeader("X-Server-Id", _serverId.ToString("D"));
                req.SetRequestHeader("X-Server-Key", _serverKey);
            }

            req.timeout = 10;
            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

#if UNITY_2020_2_OR_NEWER
            var failed = req.result != UnityWebRequest.Result.Success;
#else
            var failed = req.isNetworkError || req.isHttpError;
#endif
            if (failed)
            {
                Debug.LogWarning($"[ServerBackend] HTTP hata {req.responseCode} {url}: {req.error} / {req.downloadHandler?.text}");
                return (false, null);
            }

            return (true, req.downloadHandler?.text);
        }

        private static string TrimSlash(string url)
        {
            if (string.IsNullOrEmpty(url))
                return string.Empty;
            return url.EndsWith("/", StringComparison.Ordinal) ? url.TrimEnd('/') : url;
        }

        [Serializable]
        private sealed class RegisterBody
        {
            public string host;
            public int port;
            public string region;
            public string serverKey;
            public int maxPlayers;

            public string Host { set => host = value; }
            public int Port { set => port = value; }
            public string Region { set => region = value; }
            public string ServerKey { set => serverKey = value; }
            public int MaxPlayers { set => maxPlayers = value; }
        }

        [Serializable]
        private sealed class HeartbeatBody
        {
            public string serverId;
            public string serverKey;
            public int currentPlayers;
            public string status;

            public string ServerId { set => serverId = value; }
            public string ServerKey { set => serverKey = value; }
            public int CurrentPlayers { set => currentPlayers = value; }
            public string Status { set => status = value; }
        }

        [Serializable]
        private sealed class ServerDtoBody
        {
            public string id;
            public string endpoint;
            public string region;
            public string status;
            public int currentPlayers;
            public int maxPlayers;

            public string Id => id;
            public string Endpoint => endpoint;
            public string Region => region;
            public string Status => status;
            public int CurrentPlayers => currentPlayers;
            public int MaxPlayers => maxPlayers;
        }
    }
}
