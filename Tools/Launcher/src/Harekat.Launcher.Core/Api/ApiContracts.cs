using System.Text.Json.Serialization;

namespace Harekat.Launcher.Core.Api;

// Backend sözleşmesi (F3-8). Alan adları Backend/Harekat.Application/Dtos/Dtos.cs ile birebir
// aynıdır; ASP.NET Core minimal API varsayılanı camelCase JSON üretir.
// Örnek JSON gövdeleri: Tools/Launcher/README.md → "Backend sözleşmesi".

/// <summary><c>GET /news?lang=tr&amp;take=20</c> yanıtındaki dizi öğesi.</summary>
public sealed class NewsItemDto
{
    [JsonPropertyName("id")] public Guid Id { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("body")] public string Body { get; set; } = "";
    [JsonPropertyName("language")] public string Language { get; set; } = "tr";
    [JsonPropertyName("author")] public string? Author { get; set; }
    [JsonPropertyName("publishedAt")] public DateTimeOffset PublishedAt { get; set; }
    [JsonPropertyName("sortOrder")] public int SortOrder { get; set; }

    public override string ToString() => Title;
}

/// <summary><c>GET /client/version?channel=stable</c> yanıtı (kanal başına tek aktif sürüm).</summary>
public sealed class ClientVersionDto
{
    [JsonPropertyName("channel")] public string Channel { get; set; } = "stable";
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    /// <summary>Kümülatif ya da tam yama zip'inin mutlak URL'si (https).</summary>
    [JsonPropertyName("patchUrl")] public string PatchUrl { get; set; } = "";
    /// <summary>Zip dosyasının SHA-256 özeti, 64 karakter hex (büyük/küçük harf fark etmez).</summary>
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = "";
    /// <summary>Zip boyutu (bayt). 0 = bilinmiyor; &gt;0 ise indirme sonunda boyut da doğrulanır.</summary>
    [JsonPropertyName("patchSizeBytes")] public long PatchSizeBytes { get; set; }
    [JsonPropertyName("releaseNotes")] public string? ReleaseNotes { get; set; }
    /// <summary>true ise bu sürümden eski istemciler güncellemeden OYNA'ya basamaz.</summary>
    [JsonPropertyName("mandatory")] public bool Mandatory { get; set; }
    [JsonPropertyName("publishedAt")] public DateTimeOffset PublishedAt { get; set; }
}

/// <summary><c>POST /auth/login</c> isteği.</summary>
public sealed class LoginRequestDto
{
    [JsonPropertyName("username")] public string Username { get; set; } = "";
    [JsonPropertyName("password")] public string Password { get; set; } = "";
}

/// <summary><c>POST /auth/login</c> yanıtı (Backend AuthResponse).</summary>
public sealed class AuthResponseDto
{
    [JsonPropertyName("accessToken")] public string AccessToken { get; set; } = "";
    [JsonPropertyName("refreshToken")] public string RefreshToken { get; set; } = "";
    [JsonPropertyName("expiresAt")] public DateTimeOffset ExpiresAt { get; set; }
    [JsonPropertyName("player")] public PlayerSummaryDto? Player { get; set; }
}

/// <summary>AuthResponse.player içinden launcher'ın kullandığı alt küme.</summary>
public sealed class PlayerSummaryDto
{
    [JsonPropertyName("id")] public Guid Id { get; set; }
    [JsonPropertyName("username")] public string Username { get; set; } = "";
}

/// <summary>Backend hata gövdesi: <c>{ "error": "...", "message": "..." }</c> (ExceptionMiddleware).</summary>
public sealed class ApiErrorDto
{
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}
