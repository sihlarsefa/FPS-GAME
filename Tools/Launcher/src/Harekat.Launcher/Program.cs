using System.Text.Json;
using Harekat.Launcher.Services;

namespace Harekat.Launcher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        var settings = LoadSettings();
        Application.Run(new MainForm(settings));
    }

    private static LauncherSettings LoadSettings()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path))
            return new LauncherSettings();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<LauncherSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new LauncherSettings();
        }
        catch
        {
            return new LauncherSettings();
        }
    }
}
