using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Project.Online.Backend
{
    /// <summary>
    /// UnityWebRequest tabanlı HAREKÂT API istemcisi.
    /// Uçlar Backend/ClientSdk ile aynı: auth, players/me, squads, matchmaking, leaderboards, achievements.
    /// </summary>
    public sealed class BackendClient : IDisposable
    {
        public const string DefaultBaseUrl = "http://127.0.0.1:5080";
        public const int DefaultTimeoutSeconds = 15;
        public const int MaxRetries = 2;

        private readonly string _baseUrl;
        private readonly TokenStore _tokens;
        private readonly int _timeoutSeconds;
        private readonly int _maxRetries;
        private bool _disposed;
        private bool _refreshing;

        public BackendClient(string baseUrl = null, TokenStore tokens = null, int timeoutSeconds = DefaultTimeoutSeconds, int maxRetries = MaxRetries)
        {
            _baseUrl = (baseUrl ?? DefaultBaseUrl).TrimEnd('/');
            _tokens = tokens ?? new TokenStore();
            _timeoutSeconds = Math.Max(5, timeoutSeconds);
            _maxRetries = Math.Max(0, maxRetries);
            _tokens.Load();
        }

        public TokenStore Tokens => _tokens;
        public string BaseUrl => _baseUrl;
        public bool IsLoggedIn => _tokens.HasAccessToken || _tokens.HasRefreshToken;
        public PlayerProfile CurrentPlayer { get; private set; }

        public void Dispose()
        {
            _disposed = true;
        }

        public async Task<AuthResult> RegisterAsync(string username, string email, string password, string region = "tr", CancellationToken ct = default)
        {
            var body = BackendJson.BuildRegisterBody(username, email, password, region);
            var json = await SendAsync("POST", "/auth/register", body, auth: false, ct: ct).ConfigureAwait(false);
            var result = BackendJson.ParseAuthResult(json);
            ApplyAuth(result);
            return result;
        }

        public async Task<AuthResult> LoginAsync(string username, string password, CancellationToken ct = default)
        {
            var body = BackendJson.BuildLoginBody(username, password);
            var json = await SendAsync("POST", "/auth/login", body, auth: false, ct: ct).ConfigureAwait(false);
            var result = BackendJson.ParseAuthResult(json);
            ApplyAuth(result);
            return result;
        }

        public async Task<AuthResult> RefreshAsync(CancellationToken ct = default)
        {
            if (!_tokens.HasRefreshToken)
                throw new BackendApiException(401, "", "Oturum yenilenemedi. Tekrar giriş yapın.");

            var body = BackendJson.BuildRefreshBody(_tokens.RefreshToken);
            var json = await SendAsync("POST", "/auth/refresh", body, auth: false, allowRefresh: false, ct: ct).ConfigureAwait(false);
            var result = BackendJson.ParseAuthResult(json);
            ApplyAuth(result);
            return result;
        }

        /// <summary>Erişim jetonu süresi dolmuşsa yeniler. Başarılıysa true.</summary>
        public async Task<bool> EnsureFreshTokenAsync(CancellationToken ct = default)
        {
            if (!_tokens.IsAccessExpired(TimeSpan.FromSeconds(30)))
                return _tokens.HasAccessToken;

            if (!_tokens.HasRefreshToken)
                return false;

            try
            {
                await RefreshAsync(ct).ConfigureAwait(false);
                return true;
            }
            catch (BackendApiException)
            {
                _tokens.Clear();
                CurrentPlayer = null;
                return false;
            }
        }

        public void LogoutLocal()
        {
            _tokens.Clear();
            CurrentPlayer = null;
        }

        public async Task<PlayerProfile> GetMeAsync(CancellationToken ct = default)
        {
            var json = await SendAsync("GET", "/players/me", null, auth: true, ct: ct).ConfigureAwait(false);
            CurrentPlayer = BackendJson.ParsePlayerProfile(json);
            return CurrentPlayer;
        }

        public async Task<SquadInfo> CreateSquadAsync(string name, string region = "tr", CancellationToken ct = default)
        {
            var body = BackendJson.BuildCreateSquadBody(name, region);
            var json = await SendAsync("POST", "/squads", body, auth: true, ct: ct).ConfigureAwait(false);
            return BackendJson.ParseSquadInfo(json);
        }

        public async Task<SquadInfo> JoinSquadAsync(string inviteCode, CancellationToken ct = default)
        {
            var body = BackendJson.BuildJoinSquadBody(inviteCode);
            var json = await SendAsync("POST", "/squads/join", body, auth: true, ct: ct).ConfigureAwait(false);
            return BackendJson.ParseSquadInfo(json);
        }

        public async Task<SquadInfo> GetMySquadAsync(CancellationToken ct = default)
        {
            var json = await SendAsync("GET", "/squads/me", null, auth: true, ct: ct).ConfigureAwait(false);
            return BackendJson.ParseSquadInfo(json);
        }

        public async Task<ReadyStatusDto> SetReadyAsync(bool ready, CancellationToken ct = default)
        {
            var path = "/squads/ready?ready=" + (ready ? "true" : "false");
            var json = await SendAsync("POST", path, null, auth: true, ct: ct).ConfigureAwait(false);
            return BackendJson.ParseReadyStatus(json);
        }

        public async Task LeaveSquadAsync(CancellationToken ct = default)
        {
            await SendAsync("POST", "/squads/leave", null, auth: true, ct: ct).ConfigureAwait(false);
        }

        public async Task<QueueInfo> EnqueueAsync(string region = null, int? maxPingMs = null, CancellationToken ct = default)
        {
            var body = BackendJson.BuildQueueBody(region, maxPingMs);
            var json = await SendAsync("POST", "/matchmaking/queue", body, auth: true, ct: ct).ConfigureAwait(false);
            return BackendJson.ParseQueueInfo(json);
        }

        public async Task CancelQueueAsync(CancellationToken ct = default)
        {
            await SendAsync("DELETE", "/matchmaking/queue", null, auth: true, ct: ct).ConfigureAwait(false);
        }

        public async Task<QueueInfo> GetTicketAsync(Guid ticketId, CancellationToken ct = default)
        {
            var json = await SendAsync("GET", "/matchmaking/tickets/" + ticketId.ToString("D"), null, auth: true, ct: ct).ConfigureAwait(false);
            return BackendJson.ParseQueueInfo(json);
        }

        public async Task<IReadOnlyList<LeaderboardRow>> GetLeaderboardAsync(string metric = "experience", int take = 50, CancellationToken ct = default)
        {
            var path = "/leaderboards?metric=" + UnityWebRequest.EscapeURL(metric ?? "experience") + "&take=" + take;
            var json = await SendAsync("GET", path, null, auth: false, ct: ct).ConfigureAwait(false);
            return ParseLeaderboard(json);
        }

        public async Task<IReadOnlyList<AchievementDto>> GetMyAchievementsAsync(CancellationToken ct = default)
        {
            var json = await SendAsync("GET", "/achievements/me", null, auth: true, ct: ct).ConfigureAwait(false);
            return ParseAchievements(json);
        }

        private void ApplyAuth(AuthResult result)
        {
            if (result == null)
                return;

            _tokens.Save(result.AccessToken, result.RefreshToken, result.ExpiresAt);
            if (result.Player != null)
                CurrentPlayer = result.Player;
        }

        private async Task<string> SendAsync(string method, string path, string jsonBody, bool auth, bool allowRefresh = true, CancellationToken ct = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BackendClient));

            if (auth)
            {
                await EnsureFreshTokenAsync(ct).ConfigureAwait(false);
                if (!_tokens.HasAccessToken)
                    throw new BackendApiException(401, "", "Önce giriş yapın.");
            }

            Exception lastNetwork = null;
            for (var attempt = 0; attempt <= _maxRetries; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                using (var req = BuildRequest(method, path, jsonBody, auth))
                {
                    try
                    {
                        await SendWebRequestAsync(req, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        lastNetwork = ex;
                        if (attempt < _maxRetries)
                        {
                            await DelayAsync(200 * (attempt + 1), ct).ConfigureAwait(false);
                            continue;
                        }

                        throw new BackendApiException(0, "", BackendErrors.NetworkFailure(ex.Message));
                    }

                    var code = (int)req.responseCode;
                    var text = req.downloadHandler != null ? req.downloadHandler.text : "";

                    if (req.result == UnityWebRequest.Result.ConnectionError ||
                        req.result == UnityWebRequest.Result.DataProcessingError)
                    {
                        lastNetwork = new Exception(req.error);
                        if (attempt < _maxRetries)
                        {
                            await DelayAsync(200 * (attempt + 1), ct).ConfigureAwait(false);
                            continue;
                        }

                        throw new BackendApiException(0, text, BackendErrors.NetworkFailure(req.error));
                    }

                    if (code == 401 && auth && allowRefresh && !_refreshing && _tokens.HasRefreshToken)
                    {
                        _refreshing = true;
                        try
                        {
                            await RefreshAsync(ct).ConfigureAwait(false);
                        }
                        finally
                        {
                            _refreshing = false;
                        }

                        // bir kez yeniden dene
                        return await SendAsync(method, path, jsonBody, auth: true, allowRefresh: false, ct: ct).ConfigureAwait(false);
                    }

                    if (code >= 200 && code < 300)
                        return text ?? "";

                    // 408 / 5xx tekrar denenebilir
                    if ((code == 408 || code >= 500) && attempt < _maxRetries)
                    {
                        await DelayAsync(250 * (attempt + 1), ct).ConfigureAwait(false);
                        continue;
                    }

                    throw new BackendApiException(code, text, BackendErrors.ToTurkish(code, text));
                }
            }

            throw new BackendApiException(0, "", BackendErrors.NetworkFailure(lastNetwork != null ? lastNetwork.Message : null));
        }

        private UnityWebRequest BuildRequest(string method, string path, string jsonBody, bool auth)
        {
            var url = _baseUrl + path;
            UnityWebRequest req;

            if (method == "GET")
            {
                req = UnityWebRequest.Get(url);
            }
            else if (method == "DELETE")
            {
                req = UnityWebRequest.Delete(url);
                req.downloadHandler = new DownloadHandlerBuffer();
            }
            else
            {
                req = new UnityWebRequest(url, method);
                var payload = Encoding.UTF8.GetBytes(jsonBody ?? "{}");
                req.uploadHandler = new UploadHandlerRaw(payload);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
            }

            req.timeout = _timeoutSeconds;
            req.SetRequestHeader("Accept", "application/json");
            if (auth && _tokens.HasAccessToken)
                req.SetRequestHeader("Authorization", "Bearer " + _tokens.AccessToken);

            return req;
        }

        private static Task SendWebRequestAsync(UnityWebRequest request, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<bool>();
            var op = request.SendWebRequest();
            CancellationTokenRegistration reg = default;

            if (ct.CanBeCanceled)
            {
                reg = ct.Register(() =>
                {
                    try { request.Abort(); } catch { /* ignore */ }
                    tcs.TrySetCanceled(ct);
                });
            }

            op.completed += _ =>
            {
                reg.Dispose();
                if (ct.IsCancellationRequested)
                    tcs.TrySetCanceled(ct);
                else
                    tcs.TrySetResult(true);
            };

            return tcs.Task;
        }

        private static Task DelayAsync(int milliseconds, CancellationToken ct)
        {
            if (milliseconds <= 0)
                return Task.CompletedTask;
            return Task.Delay(milliseconds, ct);
        }

        private static List<LeaderboardRow> ParseLeaderboard(string json)
        {
            var list = new List<LeaderboardRow>();
            if (string.IsNullOrEmpty(json))
                return list;

            var array = json.TrimStart().StartsWith("[", StringComparison.Ordinal)
                ? json
                : BackendJson.ExtractArray(json, "items") ?? "[]";

            foreach (var item in BackendJson.SplitArrayObjects(array))
            {
                if (item.Length > 0 && item[0] == '{')
                    list.Add(BackendJson.ParseLeaderboardRow(item));
            }

            return list;
        }

        private static List<AchievementDto> ParseAchievements(string json)
        {
            var list = new List<AchievementDto>();
            if (string.IsNullOrEmpty(json))
                return list;

            var array = json.TrimStart().StartsWith("[", StringComparison.Ordinal)
                ? json
                : BackendJson.ExtractArray(json, "items") ?? "[]";

            foreach (var item in BackendJson.SplitArrayObjects(array))
            {
                if (item.Length > 0 && item[0] == '{')
                    list.Add(BackendJson.ParseAchievement(item));
            }

            return list;
        }
    }
}
