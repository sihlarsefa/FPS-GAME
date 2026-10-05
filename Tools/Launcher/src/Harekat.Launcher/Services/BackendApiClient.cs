using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Harekat.Launcher.Services;

public sealed class BackendApiClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public BackendApiClient(string baseUrl)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("HarekatLauncher/0.1");
    }

    public async Task<IReadOnlyList<NewsItemDto>> GetNewsAsync(string lang, int take = 20, CancellationToken ct = default)
    {
        var list = await _http.GetFromJsonAsync<List<NewsItemDto>>(
            $"news?lang={Uri.EscapeDataString(lang)}&take={take}", _json, ct);
        return list ?? [];
    }

    public async Task<ClientVersionDto> GetVersionAsync(string channel, CancellationToken ct = default)
    {
        return await _http.GetFromJsonAsync<ClientVersionDto>(
                   $"client/version?channel={Uri.EscapeDataString(channel)}", _json, ct)
               ?? throw new InvalidOperationException("Sürüm yanıtı boş.");
    }

    public async Task<AuthResponseDto> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync("auth/login", new { username, password }, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Giriş başarısız ({(int)res.StatusCode}): {body}");
        return JsonSerializer.Deserialize<AuthResponseDto>(body, _json)
               ?? throw new InvalidOperationException("Giriş yanıtı boş.");
    }

    public void Dispose() => _http.Dispose();
}
