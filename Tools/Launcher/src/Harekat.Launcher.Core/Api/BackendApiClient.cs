using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Harekat.Launcher.Core.Api;

/// <summary>Backend'den dönen başarısız yanıt (HTTP durum kodu + backend hata kodu/mesajı).</summary>
public sealed class BackendApiException : Exception
{
    public BackendApiException(int statusCode, string? errorCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }

    public int StatusCode { get; }
    public string? ErrorCode { get; }
}

/// <summary>
/// Launcher'ın kullandığı backend uçları: <c>/news</c>, <c>/client/version</c>, <c>/auth/login</c>.
/// HttpClient dışarıdan verilebilir (testlerde sahte handler).
/// </summary>
public sealed class BackendApiClient : IDisposable
{
    public const string UserAgent = "HarekatLauncher/0.1";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public BackendApiClient(HttpClient http, bool ownsHttp = false)
    {
        ArgumentNullException.ThrowIfNull(http);
        if (http.BaseAddress is null)
            throw new ArgumentException("HttpClient.BaseAddress ayarlanmalı.", nameof(http));
        _http = http;
        _ownsHttp = ownsHttp;
    }

    /// <summary>Taban URL'den kendi HttpClient'ını oluşturur. IIS sanal dizini (ör. https://host/api) desteklenir.</summary>
    public static BackendApiClient Create(string baseUrl, TimeSpan? timeout = null)
    {
        var http = new HttpClient
        {
            BaseAddress = NormalizeBaseAddress(baseUrl),
            Timeout = timeout ?? TimeSpan.FromSeconds(20)
        };
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return new BackendApiClient(http, ownsHttp: true);
    }

    /// <summary>Göreli yolların doğru birleşmesi için sonda tek '/' olacak şekilde normalize eder.</summary>
    public static Uri NormalizeBaseAddress(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl?.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new ArgumentException($"Geçersiz API adresi: '{baseUrl}'", nameof(baseUrl));
        var s = uri.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/";
        return new Uri(s, UriKind.Absolute);
    }

    /// <summary><c>GET /news?lang=&amp;take=</c> — yayımlanmış haberler (backend sıralı döner).</summary>
    public async Task<IReadOnlyList<NewsItemDto>> GetNewsAsync(string? language, int take = 20, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 100);
        var path = string.IsNullOrWhiteSpace(language)
            ? $"news?take={take}"
            : $"news?lang={Uri.EscapeDataString(language.Trim())}&take={take}";
        using var res = await _http.GetAsync(path, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(res, ct).ConfigureAwait(false);
        var list = await res.Content.ReadFromJsonAsync<List<NewsItemDto>>(Json, ct).ConfigureAwait(false);
        return list ?? [];
    }

    /// <summary>
    /// <c>GET /client/version?channel=</c>. Kanal için yayımlanmış sürüm yoksa backend 404 döner → <c>null</c>.
    /// </summary>
    public async Task<ClientVersionDto?> GetClientVersionAsync(string channel, CancellationToken ct = default)
    {
        var path = $"client/version?channel={Uri.EscapeDataString(string.IsNullOrWhiteSpace(channel) ? "stable" : channel.Trim())}";
        using var res = await _http.GetAsync(path, ct).ConfigureAwait(false);
        if (res.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureSuccessAsync(res, ct).ConfigureAwait(false);
        return await res.Content.ReadFromJsonAsync<ClientVersionDto>(Json, ct).ConfigureAwait(false)
               ?? throw new BackendApiException((int)res.StatusCode, null, "Sürüm yanıtı boş.");
    }

    /// <summary><c>POST /auth/login</c>. Başarısızsa <see cref="BackendApiException"/>.</summary>
    public async Task<AuthResponseDto> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var body = new LoginRequestDto { Username = username ?? "", Password = password ?? "" };
        using var res = await _http.PostAsJsonAsync("auth/login", body, Json, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(res, ct).ConfigureAwait(false);
        var auth = await res.Content.ReadFromJsonAsync<AuthResponseDto>(Json, ct).ConfigureAwait(false);
        if (auth is null || string.IsNullOrWhiteSpace(auth.AccessToken))
            throw new BackendApiException((int)res.StatusCode, null, "Giriş yanıtında token yok.");
        return auth;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage res, CancellationToken ct)
    {
        if (res.IsSuccessStatusCode)
            return;

        string? code = null;
        string message = $"HTTP {(int)res.StatusCode} {res.ReasonPhrase}";
        try
        {
            var text = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(text))
            {
                var err = JsonSerializer.Deserialize<ApiErrorDto>(text, Json);
                if (err is not null)
                {
                    code = err.Error;
                    if (!string.IsNullOrWhiteSpace(err.Message))
                        message = err.Message!;
                }
            }
        }
        catch (JsonException)
        {
            // Gövde JSON değil (ör. IIS hata sayfası) → HTTP satırı yeterli.
        }

        throw new BackendApiException((int)res.StatusCode, code, message);
    }

    public void Dispose()
    {
        if (_ownsHttp)
            _http.Dispose();
    }
}
