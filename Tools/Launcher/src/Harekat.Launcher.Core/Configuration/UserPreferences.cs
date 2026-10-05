using System.Text.Json;

namespace Harekat.Launcher.Core.Configuration;

/// <summary>
/// Kullanıcıya özel tercihler: <c>%LOCALAPPDATA%\HAREKAT\launcher.user.json</c>.
/// Bilerek token/şifre SAKLANMAZ (yalnızca dil ve son kullanıcı adı).
/// </summary>
public sealed class UserPreferences
{
    public const string FileName = "launcher.user.json";

    public string? Language { get; set; }
    public string? LastUsername { get; set; }

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HAREKAT");

    public static UserPreferences Load(string directory)
    {
        try
        {
            var path = Path.Combine(directory, FileName);
            if (!File.Exists(path))
                return new UserPreferences();
            return JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(path), LauncherSettings.JsonOptions)
                   ?? new UserPreferences();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new UserPreferences();
        }
    }

    public bool TrySave(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, FileName);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, LauncherSettings.JsonOptions));
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
