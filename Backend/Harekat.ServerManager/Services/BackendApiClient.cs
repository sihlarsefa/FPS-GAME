using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Harekat.ServerManager.Services;

public sealed record MatchDto(Guid Id, string Region, string Status, string? ServerEndpoint);
public sealed record ServerDto(Guid Id, string Endpoint, string Region, string Status, int CurrentPlayers, int MaxPlayers);
public sealed record ClaimMatchRequest(string Host, int Port, string ServerKey, string? Region = null, int MaxPlayers = 100);
public sealed record ClaimMatchResponse(MatchDto Match, Guid ServerId, string Endpoint);
public sealed record RegisterServerRequest(string Host, int Port, string Region, string ServerKey, int MaxPlayers = 100);
public sealed record ServerHeartbeatRequest(Guid ServerId, string ServerKey, int CurrentPlayers, string Status);
public sealed record ReleaseServerRequest(Guid ServerId, string ServerKey);

/// <summary>
/// Backend HTTP istemcisi — /servers/* ve /matches/* uçları.
/// </summary>
public sealed class BackendApiClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly ServerManagerOptions _options;
    private readonly ILogger<BackendApiClient> _log;

    public BackendApiClient(HttpClient http, IOptions<ServerManagerOptions> options, ILogger<BackendApiClient> log)
    {
        _http = http;
        _options = options.Value;
        _log = log;
        _http.BaseAddress = new Uri(_options.BackendBaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<IReadOnlyList<MatchDto>> GetPendingAllocationsAsync(CancellationToken ct)
    {
        var region = Uri.EscapeDataString(_options.Region);
        var list = await _http.GetFromJsonAsync<List<MatchDto>>(
            $"matches/pending-allocation?region={region}", JsonOpts, ct);
        return list ?? [];
    }

    public async Task<ClaimMatchResponse?> ClaimMatchAsync(Guid matchId, int port, CancellationToken ct)
    {
        var body = new ClaimMatchRequest(
            _options.PublicHost,
            port,
            _options.ServerKey,
            _options.Region,
            _options.MaxPlayersPerMatch);

        using var res = await _http.PostAsJsonAsync($"matches/{matchId:D}/claim", body, JsonOpts, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            _log.LogWarning("Claim başarısız {MatchId} HTTP {Code}: {Body}", matchId, (int)res.StatusCode, err);
            return null;
        }

        return await res.Content.ReadFromJsonAsync<ClaimMatchResponse>(JsonOpts, ct);
    }

    public async Task<ServerDto?> RegisterReadyAsync(int port, CancellationToken ct)
    {
        var body = new RegisterServerRequest(
            _options.PublicHost,
            port,
            _options.Region,
            _options.ServerKey,
            _options.MaxPlayersPerMatch);

        using var res = await _http.PostAsJsonAsync("servers/register", body, JsonOpts, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            _log.LogWarning("Register başarısız port={Port} HTTP {Code}: {Body}", port, (int)res.StatusCode, err);
            return null;
        }

        return await res.Content.ReadFromJsonAsync<ServerDto>(JsonOpts, ct);
    }

    public async Task HeartbeatAsync(Guid serverId, int currentPlayers, string status, CancellationToken ct)
    {
        var body = new ServerHeartbeatRequest(serverId, _options.ServerKey, currentPlayers, status);
        using var res = await _http.PostAsJsonAsync("servers/heartbeat", body, JsonOpts, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            _log.LogWarning("Heartbeat başarısız {ServerId} HTTP {Code}: {Body}", serverId, (int)res.StatusCode, err);
        }
    }

    public async Task ReleaseAsync(Guid serverId, CancellationToken ct)
    {
        var body = new ReleaseServerRequest(serverId, _options.ServerKey);
        using var res = await _http.PostAsJsonAsync("servers/release", body, JsonOpts, ct);
        if (!res.IsSuccessStatusCode)
            _log.LogWarning("Release başarısız {ServerId} HTTP {Code}", serverId, (int)res.StatusCode);
    }

    public async Task<ServerDto?> FindServerByEndpointAsync(string host, int port, CancellationToken ct)
    {
        var encoded = Uri.EscapeDataString(host);
        var list = await _http.GetFromJsonAsync<List<ServerDto>>($"servers/by-host/{encoded}", JsonOpts, ct);
        if (list is null || list.Count == 0)
            return null;

        var endpoint = $"{host}:{port}";
        return list.FirstOrDefault(s =>
            string.Equals(s.Endpoint, endpoint, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<int> GetQueueDepthAsync(CancellationToken ct)
    {
        try
        {
            using var res = await _http.GetAsync("matchmaking/queue-depth", ct);
            if (!res.IsSuccessStatusCode) return 0;
            await using var stream = await res.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (doc.RootElement.TryGetProperty("depth", out var d))
                return d.GetInt32();
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "queue-depth alınamadı");
        }
        return 0;
    }
}
