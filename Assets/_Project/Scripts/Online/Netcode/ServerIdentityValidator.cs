using System;
using System.Threading.Tasks;
using Project.Online.Backend;
using UnityEngine;
using UnityEngine.Networking;

namespace Project.Online.Netcode
{
    /// <summary>
    /// Dedicated server: istemcinin JWT'sini backend ile doğrular (<c>GET /players/me</c>, Bearer) ve varsa
    /// matchmaking biletini kontrol eder (<c>GET /matchmaking/tickets/{id}</c>).
    /// </summary>
    public sealed class ServerIdentityValidator
    {
        public readonly struct Result
        {
            public readonly bool Ok;
            public readonly PlayerProfile Profile;
            public readonly string Reason;

            public Result(bool ok, PlayerProfile profile, string reason)
            {
                Ok = ok;
                Profile = profile;
                Reason = reason;
            }
        }

        private readonly string _backendUrl;

        public ServerIdentityValidator(string backendUrl)
        {
            _backendUrl = (backendUrl ?? string.Empty).TrimEnd('/');
        }

        public async Task<Result> ValidateAsync(ConnectionIdentity.Payload payload)
        {
            if (!payload.Valid || !payload.HasToken)
                return new Result(false, null, "Giriş gerekli.");

            var (ok, json) = await GetAsync("/players/me", payload.Jwt);
            if (!ok)
                return new Result(false, null, "Kimlik doğrulanamadı.");

            var profile = BackendJson.ParsePlayerProfile(json);
            if (profile == null || profile.Id == Guid.Empty)
                return new Result(false, null, "Geçersiz profil.");

            if (payload.TicketId != Guid.Empty)
            {
                var (tOk, _) = await GetAsync("/matchmaking/tickets/" + payload.TicketId.ToString("D"), payload.Jwt);
                if (!tOk)
                    return new Result(false, null, "Matchmaking bileti geçersiz.");
            }

            return new Result(true, profile, null);
        }

        private async Task<(bool ok, string json)> GetAsync(string path, string jwt)
        {
            if (string.IsNullOrEmpty(_backendUrl))
                return (false, null);
            using var req = UnityWebRequest.Get(_backendUrl + path);
            req.SetRequestHeader("Authorization", "Bearer " + jwt);
            req.timeout = 8;
            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[ServerIdentity] {path} {req.responseCode}: {req.error}");
                return (false, null);
            }

            return (true, req.downloadHandler?.text);
        }
    }
}
