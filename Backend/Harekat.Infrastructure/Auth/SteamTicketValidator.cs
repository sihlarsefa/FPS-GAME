using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Harekat.Application.Abstractions;
using Harekat.Application.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Harekat.Infrastructure.Auth;

/// <summary>
/// Steam Web API ticket doğrulayıcı.
/// Geliştirme: <c>Steam:AllowDevTickets=true</c> iken ticket <c>DEV:{steamid64}:{persona}</c> kabul edilir.
/// </summary>
public sealed class SteamTicketValidator : ISteamTicketValidator
{
    private static readonly Regex DevTicketRegex = new(
        @"^DEV:(\d{5,20})(?::(.*))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<SteamTicketValidator> _logger;

    public SteamTicketValidator(HttpClient http, IConfiguration config, ILogger<SteamTicketValidator> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<SteamTicketIdentity> ValidateAsync(string ticketHex, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ticketHex))
            throw new AppException("Steam ticket gerekli.", 400, ErrorCodes.Validation);

        var ticket = ticketHex.Trim();
        var allowDev = string.Equals(_config["Steam:AllowDevTickets"], "true", StringComparison.OrdinalIgnoreCase);
        if (allowDev)
        {
            var m = DevTicketRegex.Match(ticket);
            if (m.Success && ulong.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var devId))
            {
                var persona = m.Groups[2].Success && !string.IsNullOrWhiteSpace(m.Groups[2].Value)
                    ? m.Groups[2].Value.Trim()
                    : null;
                _logger.LogWarning("Steam DEV ticket kabul edildi: {SteamId}", devId);
                return new SteamTicketIdentity(devId, persona);
            }
        }

        var apiKey = _config["Steam:WebApiKey"];
        var appId = _config["Steam:AppId"] ?? "480";
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new AppException(
                "Steam Web API anahtarı yapılandırılmamış. Geliştirme için Steam:AllowDevTickets=true kullanın.",
                503,
                ErrorCodes.Validation);

        var url =
            $"https://api.steampowered.com/ISteamUserAuth/AuthenticateUserTicket/v1/?key={Uri.EscapeDataString(apiKey)}" +
            $"&appid={Uri.EscapeDataString(appId)}&ticket={Uri.EscapeDataString(ticket)}";

        using var res = await _http.GetAsync(url, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            _logger.LogError("Steam AuthenticateUserTicket HTTP {Status}: {Body}", (int)res.StatusCode, body);
            throw new AppException("Steam doğrulama servisine ulaşılamadı.", 502, ErrorCodes.Validation);
        }

        SteamAuthApiResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<SteamAuthApiResponse>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Steam yanıtı parse edilemedi: {Body}", body);
            throw new AppException("Steam yanıtı geçersiz.", 502, ErrorCodes.Validation);
        }

        var p = parsed?.Response?.Params;
        if (p is null || !string.Equals(p.Result, "OK", StringComparison.OrdinalIgnoreCase))
        {
            var err = parsed?.Response?.Error?.ErrorDesc ?? p?.Result ?? "unknown";
            _logger.LogWarning("Steam ticket reddedildi: {Error}", err);
            throw new AppException("Steam oturumu doğrulanamadı.", 401, ErrorCodes.Unauthorized);
        }

        if (p.VacBanned || p.PublisherBanned)
            throw new AppException("Steam hesabı yasaklı.", 403, ErrorCodes.Banned);

        if (!ulong.TryParse(p.SteamId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var steamId) || steamId == 0)
            throw new AppException("Steam kimliği alınamadı.", 401, ErrorCodes.Unauthorized);

        return new SteamTicketIdentity(steamId, null);
    }

    private sealed class SteamAuthApiResponse
    {
        [JsonPropertyName("response")]
        public SteamAuthResponseBody? Response { get; set; }
    }

    private sealed class SteamAuthResponseBody
    {
        [JsonPropertyName("params")]
        public SteamAuthParams? Params { get; set; }

        [JsonPropertyName("error")]
        public SteamAuthError? Error { get; set; }
    }

    private sealed class SteamAuthParams
    {
        [JsonPropertyName("result")]
        public string? Result { get; set; }

        [JsonPropertyName("steamid")]
        public string? SteamId { get; set; }

        [JsonPropertyName("vacbanned")]
        public bool VacBanned { get; set; }

        [JsonPropertyName("publisherbanned")]
        public bool PublisherBanned { get; set; }
    }

    private sealed class SteamAuthError
    {
        [JsonPropertyName("errorcode")]
        public int ErrorCode { get; set; }

        [JsonPropertyName("errordesc")]
        public string? ErrorDesc { get; set; }
    }
}
