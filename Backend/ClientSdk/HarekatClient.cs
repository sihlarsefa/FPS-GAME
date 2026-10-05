using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Harekat.ClientSdk;

/// <summary>
/// Unity'ye kopyalanmaya hazır, bağımlılıksız HttpClient tabanlı HAREKÂT API istemcisi.
///
/// Unity uyarlama notları:
/// 1) UnityWebRequest kullanmayın — System.Net.Http.HttpClient (Unity 2022+ / .NET Standard 2.1).
/// 2) Bu klasörü Assets/_Project/Scripts/ClientSdk/ altına kopyalayın; başka proje referansı gerekmez.
/// 3) IL2CPP: link.xml ile System.Net.Http korunmalı.
/// 4) Ana thread'de uzun await etmeyin.
/// </summary>
public sealed class HarekatClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
    private string? _accessToken;

    public HarekatClient(string baseUrl, HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public void SetAccessToken(string? token)
    {
        _accessToken = token;
        _http.DefaultRequestHeaders.Authorization = string.IsNullOrEmpty(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<AuthResult> RegisterAsync(string username, string email, string password, string region = "tr", CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync("auth/register", new { username, email, password, region }, ct);
        await EnsureSuccess(res, ct);
        var body = await res.Content.ReadFromJsonAsync<AuthResult>(_json, ct)
                   ?? throw new InvalidOperationException("Boş yanıt");
        SetAccessToken(body.AccessToken);
        return body;
    }

    public async Task<AuthResult> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync("auth/login", new { username, password }, ct);
        await EnsureSuccess(res, ct);
        var body = await res.Content.ReadFromJsonAsync<AuthResult>(_json, ct)
                   ?? throw new InvalidOperationException("Boş yanıt");
        SetAccessToken(body.AccessToken);
        return body;
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync("auth/refresh", new { refreshToken }, ct);
        await EnsureSuccess(res, ct);
        var body = await res.Content.ReadFromJsonAsync<AuthResult>(_json, ct)
                   ?? throw new InvalidOperationException("Boş yanıt");
        SetAccessToken(body.AccessToken);
        return body;
    }

    public async Task<PlayerProfile> GetMeAsync(CancellationToken ct = default)
    {
        EnsureAuth();
        return await _http.GetFromJsonAsync<PlayerProfile>("players/me", _json, ct)
               ?? throw new InvalidOperationException("Profil alınamadı");
    }

    public async Task<SquadInfo> CreateSquadAsync(string name, string region = "tr", CancellationToken ct = default)
    {
        EnsureAuth();
        var res = await _http.PostAsJsonAsync("squads", new { name, region }, ct);
        await EnsureSuccess(res, ct);
        return (await res.Content.ReadFromJsonAsync<SquadInfo>(_json, ct))!;
    }

    public async Task<SquadInfo> JoinSquadAsync(string inviteCode, CancellationToken ct = default)
    {
        EnsureAuth();
        var res = await _http.PostAsJsonAsync("squads/join", new { inviteCode }, ct);
        await EnsureSuccess(res, ct);
        return (await res.Content.ReadFromJsonAsync<SquadInfo>(_json, ct))!;
    }

    public async Task SetReadyAsync(bool ready, CancellationToken ct = default)
    {
        EnsureAuth();
        var res = await _http.PostAsync($"squads/ready?ready={ready.ToString().ToLowerInvariant()}", null, ct);
        await EnsureSuccess(res, ct);
    }

    public async Task<QueueInfo> EnqueueAsync(string? region = null, int? maxPingMs = null, CancellationToken ct = default)
    {
        EnsureAuth();
        var res = await _http.PostAsJsonAsync("matchmaking/queue", new { region, maxPingMs }, ct);
        await EnsureSuccess(res, ct);
        return (await res.Content.ReadFromJsonAsync<QueueInfo>(_json, ct))!;
    }

    public async Task CancelQueueAsync(CancellationToken ct = default)
    {
        EnsureAuth();
        var res = await _http.DeleteAsync("matchmaking/queue", ct);
        await EnsureSuccess(res, ct);
    }

    public async Task<IReadOnlyList<LeaderboardRow>> GetLeaderboardAsync(string metric = "experience", int take = 50, CancellationToken ct = default)
    {
        return await _http.GetFromJsonAsync<List<LeaderboardRow>>($"leaderboards?metric={Uri.EscapeDataString(metric)}&take={take}", _json, ct)
               ?? [];
    }

    public async Task<string> GetHealthAsync(CancellationToken ct = default)
    {
        var res = await _http.GetAsync("health", ct);
        await EnsureSuccess(res, ct);
        return await res.Content.ReadAsStringAsync(ct);
    }

    private static async Task EnsureSuccess(HttpResponseMessage res, CancellationToken ct)
    {
        if (res.IsSuccessStatusCode) return;
        var body = await res.Content.ReadAsStringAsync(ct);
        throw new HarekatApiException((int)res.StatusCode, body);
    }

    private void EnsureAuth()
    {
        if (string.IsNullOrEmpty(_accessToken))
            throw new InvalidOperationException("Önce giriş yapın (LoginAsync/RegisterAsync).");
    }

    public void Dispose() => _http.Dispose();
}

public sealed class HarekatApiException : Exception
{
    public int StatusCode { get; }
    public HarekatApiException(int statusCode, string body) : base($"API {statusCode}: {body}") => StatusCode = statusCode;
}

public sealed class AuthResult
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public PlayerProfile? Player { get; set; }
}

public sealed class PlayerProfile
{
    public Guid Id { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public CareerStatsDto? Stats { get; set; }
    public int Rank { get; set; }
    public int EloRating { get; set; }
    public Guid? SquadId { get; set; }
}

public sealed class CareerStatsDto
{
    public int Matches { get; set; }
    public int Wins { get; set; }
    public int Kills { get; set; }
    public int Headshots { get; set; }
    public int BestPlacement { get; set; }
    public float TotalDamage { get; set; }
    public float LongestSurvivalSeconds { get; set; }
    public int Experience { get; set; }
    public int Rank { get; set; }
}

public sealed class SquadInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Guid LeaderId { get; set; }
    public List<Guid> MemberIds { get; set; } = [];
    public string InviteCode { get; set; } = "";
    public int OpenSlots { get; set; }
    public bool AllReady { get; set; }
}

public sealed class QueueInfo
{
    public Guid TicketId { get; set; }
    public string Status { get; set; } = "";
    public string Region { get; set; } = "";
    public DateTimeOffset EnqueuedAt { get; set; }
}

public sealed class LeaderboardRow
{
    public int Rank { get; set; }
    public Guid PlayerId { get; set; }
    public string Username { get; set; } = "";
    public int MilitaryRank { get; set; }
    public int Value { get; set; }
    public int Elo { get; set; }
}
