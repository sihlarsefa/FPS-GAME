using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Harekat.LoadTest.Metrics;
using Harekat.LoadTest.Options;

namespace Harekat.LoadTest.Client;

public sealed class ApiModels
{
    public sealed record RegisterRequest(string Username, string Email, string Password);
    public sealed record LoginRequest(string Username, string Password);
    public sealed record AuthResponse(string AccessToken, string PlayerId, string? Username = null);
    public sealed record PlayerProfile(string Id, string Username, string? Rank = null, int Experience = 0);
    public sealed record CreateSquadRequest(string Name);
    public sealed record SquadResponse(string Id, string Name, IReadOnlyList<string>? MemberIds = null);
    public sealed record JoinSquadRequest(string PlayerId);
    public sealed record QueueRequest(string SquadId, string Region);
    public sealed record QueueResponse(string TicketId, string? MatchId = null, string Status = "Queued");
    public sealed record MatchResultRequest(
        string MatchId,
        IReadOnlyList<TeamResult> Teams,
        string? Region = null);
    public sealed record TeamResult(
        string SquadId,
        int Placement,
        bool Won,
        IReadOnlyList<PlayerMatchStats> Players);
    public sealed record PlayerMatchStats(
        string PlayerId,
        int Kills,
        int Deaths,
        int Headshots,
        int Assists = 0);
}

/// <summary>
/// HAREKÂT Backend API istemcisi — GÖREV 1 sözleşmesine uyumlu.
/// </summary>
public sealed class HarekatApiClient : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly LoadTestOptions _options;
    private readonly MetricsCollector _metrics;
    private readonly bool _ownsHttp;

    public HarekatApiClient(LoadTestOptions options, MetricsCollector metrics, HttpClient? http = null)
    {
        _options = options;
        _metrics = metrics;
        if (http is null)
        {
            _http = new HttpClient
            {
                BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
            };
            _ownsHttp = true;
        }
        else
        {
            _http = http;
            _ownsHttp = false;
        }
    }

    public async Task<ApiModels.AuthResponse> RegisterAsync(string username, string email, string password, CancellationToken ct)
    {
        var body = new ApiModels.RegisterRequest(username, email, password);
        return await SendAsync<ApiModels.AuthResponse>(
            "register",
            HttpMethod.Post,
            "auth/register",
            body,
            bearer: null,
            ct);
    }

    public async Task<ApiModels.AuthResponse> LoginAsync(string username, string password, CancellationToken ct)
    {
        var body = new ApiModels.LoginRequest(username, password);
        return await SendAsync<ApiModels.AuthResponse>(
            "login",
            HttpMethod.Post,
            "auth/login",
            body,
            bearer: null,
            ct);
    }

    public async Task<ApiModels.PlayerProfile> GetMeAsync(string token, CancellationToken ct)
    {
        return await SendAsync<ApiModels.PlayerProfile>(
            "players_me",
            HttpMethod.Get,
            "players/me",
            body: null,
            bearer: token,
            ct);
    }

    public async Task<ApiModels.SquadResponse> CreateSquadAsync(string token, string name, CancellationToken ct)
    {
        var body = new ApiModels.CreateSquadRequest(name);
        return await SendAsync<ApiModels.SquadResponse>(
            "squad_create",
            HttpMethod.Post,
            "squads",
            body,
            bearer: token,
            ct);
    }

    public async Task JoinSquadAsync(string token, string squadId, string playerId, CancellationToken ct)
    {
        var body = new ApiModels.JoinSquadRequest(playerId);
        await SendAsync<object>(
            "squad_join",
            HttpMethod.Post,
            $"squads/{Uri.EscapeDataString(squadId)}/join",
            body,
            bearer: token,
            ct,
            allowEmpty: true);
    }

    public async Task<ApiModels.QueueResponse> EnqueueAsync(string token, string squadId, string region, CancellationToken ct)
    {
        var body = new ApiModels.QueueRequest(squadId, region);
        return await SendAsync<ApiModels.QueueResponse>(
            "matchmaking_queue",
            HttpMethod.Post,
            "matchmaking/queue",
            body,
            bearer: token,
            ct);
    }

    public async Task SubmitMatchResultAsync(string matchId, ApiModels.MatchResultRequest result, CancellationToken ct)
    {
        await SendAsync<object>(
            "match_result",
            HttpMethod.Post,
            $"matches/{Uri.EscapeDataString(matchId)}/result",
            result,
            bearer: null,
            ct,
            allowEmpty: true,
            extraHeaders: new Dictionary<string, string>
            {
                ["X-Server-Key"] = _options.ServerKey
            });
    }

    public async Task<bool> HealthAsync(CancellationToken ct)
    {
        try
        {
            await SendAsync<object>("health", HttpMethod.Get, "health", null, null, ct, allowEmpty: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<T> SendAsync<T>(
        string operation,
        HttpMethod method,
        string path,
        object? body,
        string? bearer,
        CancellationToken ct,
        bool allowEmpty = false,
        IReadOnlyDictionary<string, string>? extraHeaders = null)
    {
        if (_options.DryRun)
        {
            return Simulate<T>(operation);
        }

        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(body, JsonOpts),
                Encoding.UTF8,
                "application/json");
        }

        if (!string.IsNullOrEmpty(bearer))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        if (extraHeaders is not null)
        {
            foreach (var (k, v) in extraHeaders)
            {
                request.Headers.TryAddWithoutValidation(k, v);
            }
        }

        var sw = Stopwatch.StartNew();
        try
        {
            using var response = await _http.SendAsync(request, ct);
            sw.Stop();
            var success = response.IsSuccessStatusCode;
            string? err = null;
            if (!success)
            {
                err = await response.Content.ReadAsStringAsync(ct);
            }

            _metrics.Record(new TimedResult(operation, sw.ElapsedMilliseconds, success, (int)response.StatusCode, err));

            if (!success)
            {
                throw new ApiException(operation, (int)response.StatusCode, err);
            }

            if (allowEmpty || typeof(T) == typeof(object))
            {
                return default!;
            }

            var payload = await response.Content.ReadFromJsonAsync<T>(JsonOpts, ct);
            if (payload is null)
            {
                throw new ApiException(operation, (int)response.StatusCode, "Boş yanıt gövdesi");
            }

            return payload;
        }
        catch (ApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _metrics.Record(new TimedResult(operation, sw.ElapsedMilliseconds, false, 0, ex.Message));
            throw;
        }
    }

    private T Simulate<T>(string operation)
    {
        // Deterministik sahte gecikme (5–80 ms)
        var ms = 5 + (HashCode.Combine(operation, Environment.TickCount) & 0x4F);
        Thread.Sleep(ms);
        _metrics.Record(new TimedResult(operation, ms, true, 200));

        if (typeof(T) == typeof(object))
        {
            return default!;
        }

        object fake = typeof(T).Name switch
        {
            nameof(ApiModels.AuthResponse) => new ApiModels.AuthResponse($"tok-{Guid.NewGuid():N}", $"pid-{Guid.NewGuid():N}", "dry"),
            nameof(ApiModels.PlayerProfile) => new ApiModels.PlayerProfile($"pid-{Guid.NewGuid():N}", "dry", "Er", 0),
            nameof(ApiModels.SquadResponse) => new ApiModels.SquadResponse($"sq-{Guid.NewGuid():N}", "Tim", Array.Empty<string>()),
            nameof(ApiModels.QueueResponse) => new ApiModels.QueueResponse($"tkt-{Guid.NewGuid():N}", $"m-{Guid.NewGuid():N}", "Matched"),
            _ => Activator.CreateInstance(typeof(T))!
        };

        return (T)fake;
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}

public sealed class ApiException : Exception
{
    public string Operation { get; }
    public int StatusCode { get; }

    public ApiException(string operation, int statusCode, string? body)
        : base($"{operation} başarısız ({statusCode}): {Truncate(body)}")
    {
        Operation = operation;
        StatusCode = statusCode;
    }

    private static string Truncate(string? s) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= 200 ? s : s[..200] + "…";
}
