using System.Text.Json.Serialization;

namespace Harekat.Launcher;

public sealed class LauncherSettings
{
    public string ApiBaseUrl { get; set; } = "https://api.harekat.example";
    public string Channel { get; set; } = "stable";
    public string Language { get; set; } = "tr";
    public string GameExecutable { get; set; } = "HAREKAT.exe";
    /// <summary>Launcher'a göre oyun kökü (kurulumda genellikle "..").</summary>
    public string GameRelativeDir { get; set; } = "..";
    /// <summary>Boşsa GameRelativeDir çözülür.</summary>
    public string InstallDir { get; set; } = "";
    public string GameArgs { get; set; } = "";
}

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

public sealed class ClientVersionDto
{
    [JsonPropertyName("channel")] public string Channel { get; set; } = "stable";
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("patchUrl")] public string PatchUrl { get; set; } = "";
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = "";
    [JsonPropertyName("patchSizeBytes")] public long PatchSizeBytes { get; set; }
    [JsonPropertyName("releaseNotes")] public string? ReleaseNotes { get; set; }
    [JsonPropertyName("mandatory")] public bool Mandatory { get; set; }
    [JsonPropertyName("publishedAt")] public DateTimeOffset PublishedAt { get; set; }
}

public sealed class AuthResponseDto
{
    [JsonPropertyName("accessToken")] public string AccessToken { get; set; } = "";
    [JsonPropertyName("refreshToken")] public string RefreshToken { get; set; } = "";
    [JsonPropertyName("expiresAt")] public DateTimeOffset ExpiresAt { get; set; }
}
