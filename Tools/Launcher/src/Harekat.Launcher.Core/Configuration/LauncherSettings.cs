using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Harekat.Launcher.Core.Configuration;

/// <summary>
/// <c>appsettings.json</c> (launcher EXE'sinin yanında; kurulumla gelir).
/// Kullanıcı tercihleri ayrı dosyada: <see cref="UserPreferences"/>.
/// </summary>
public sealed class LauncherSettings
{
    public const string FileName = "appsettings.json";

    /// <summary>Backend taban adresi (IIS sitesi / sanal dizin). Ör. https://api.harekat.example</summary>
    public string ApiBaseUrl { get; set; } = "https://api.harekat.example";

    /// <summary>Sürüm kanalı: stable, beta, qa…</summary>
    public string Channel { get; set; } = "stable";

    /// <summary>Varsayılan arayüz/haber dili: tr | en (kullanıcı tercihi bunu ezer).</summary>
    public string Language { get; set; } = "tr";

    /// <summary>Kurulum dizinine göre oyun EXE'si.</summary>
    public string GameExecutable { get; set; } = "HAREKAT.exe";

    /// <summary>Launcher klasörüne göre kurulum kökü (Inno kurulumunda launcher <c>{app}\Launcher</c> → "..").</summary>
    public string GameRelativeDir { get; set; } = "..";

    /// <summary>Boş değilse <see cref="GameRelativeDir"/> yerine mutlak kurulum dizini.</summary>
    public string InstallDir { get; set; } = "";

    /// <summary>Oyuna her zaman eklenecek ek argümanlar (ör. ["-screen-fullscreen", "1"]). Token argümanları buraya yazılamaz.</summary>
    public string[] ExtraGameArgs { get; set; } = [];

    /// <summary>Oyuna <c>-backend</c> ile API adresini geçir (Unity Online/Diagnostics aynı backend'i kullansın).</summary>
    public bool PassBackendUrlToGame { get; set; } = true;

    /// <summary>Yalnızca geliştirme: http:// yama adreslerine izin ver (loopback her zaman serbest).</summary>
    public bool AllowInsecureHttp { get; set; }

    /// <summary>OYNA sonrası launcher'ı kapat (false → simge durumuna küçült).</summary>
    public bool CloseOnPlay { get; set; }

    private static readonly Regex ChannelPattern = new("^[a-z0-9][a-z0-9_-]{0,31}$", RegexOptions.CultureInvariant);

    public static string NormalizeLanguage(string? lang) =>
        lang is not null && lang.Trim().StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "tr";

    /// <summary>Değerleri normalize eder; geçersiz alanları varsayılana çekip uyarı döner.</summary>
    public IReadOnlyList<string> Normalize()
    {
        var warnings = new List<string>();
        var defaults = new LauncherSettings();

        if (!Uri.TryCreate(ApiBaseUrl?.Trim(), UriKind.Absolute, out var api)
            || (api.Scheme != Uri.UriSchemeHttps && api.Scheme != Uri.UriSchemeHttp))
        {
            warnings.Add($"ApiBaseUrl geçersiz ('{ApiBaseUrl}'); varsayılan kullanılıyor.");
            ApiBaseUrl = defaults.ApiBaseUrl;
        }
        else
        {
            ApiBaseUrl = ApiBaseUrl!.Trim();
            if (api.Scheme == Uri.UriSchemeHttp && !api.IsLoopback && !AllowInsecureHttp)
                warnings.Add("ApiBaseUrl HTTPS değil; üretimde HTTPS kullanın (token düz metin gider).");
        }

        var ch = (Channel ?? "").Trim().ToLowerInvariant();
        if (!ChannelPattern.IsMatch(ch))
        {
            warnings.Add($"Channel geçersiz ('{Channel}'); 'stable' kullanılıyor.");
            ch = "stable";
        }
        Channel = ch;

        Language = NormalizeLanguage(Language);

        if (string.IsNullOrWhiteSpace(GameExecutable)
            || !Updating.PathSafety.TryNormalizeRelativePath(GameExecutable, out var exe))
        {
            warnings.Add($"GameExecutable geçersiz ('{GameExecutable}'); varsayılan kullanılıyor.");
            GameExecutable = defaults.GameExecutable;
        }
        else
        {
            GameExecutable = exe;
        }

        GameRelativeDir = string.IsNullOrWhiteSpace(GameRelativeDir) ? "." : GameRelativeDir.Trim();
        InstallDir = InstallDir?.Trim() ?? "";
        ExtraGameArgs = (ExtraGameArgs ?? []).Where(a => !string.IsNullOrWhiteSpace(a)).ToArray();
        return warnings;
    }

    /// <summary>Kurulum kökü: <see cref="InstallDir"/> ya da <c>launcherDir/GameRelativeDir</c>.</summary>
    public string ResolveInstallDir(string launcherDir) =>
        !string.IsNullOrWhiteSpace(InstallDir)
            ? Path.GetFullPath(InstallDir)
            : Path.GetFullPath(Path.Combine(launcherDir, GameRelativeDir));

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>
    /// Dosyayı okur ve normalize eder. Dosya yoksa/bozuksa varsayılanlar + uyarı (launcher yine açılır).
    /// </summary>
    public static (LauncherSettings Settings, IReadOnlyList<string> Warnings) Load(string path)
    {
        var warnings = new List<string>();
        LauncherSettings settings;
        try
        {
            if (!File.Exists(path))
            {
                warnings.Add($"{FileName} bulunamadı; varsayılanlar kullanılıyor.");
                settings = new LauncherSettings();
            }
            else
            {
                settings = JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(path), JsonOptions)
                           ?? new LauncherSettings();
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            warnings.Add($"{FileName} okunamadı ({ex.Message}); varsayılanlar kullanılıyor.");
            settings = new LauncherSettings();
        }

        warnings.AddRange(settings.Normalize());
        return (settings, warnings);
    }
}
