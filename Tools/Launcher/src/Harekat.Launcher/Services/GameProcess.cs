using System.Diagnostics;
using System.Text;

namespace Harekat.Launcher.Services;

public static class GameProcess
{
    /// <summary>
    /// Oyunu başlatır. Token varsa -token &lt;jwt&gt; ve HAREKAT_ACCESS_TOKEN ortam değişkeni ile aktarılır.
    /// </summary>
    public static Process Start(string gameExePath, string? extraArgs, string? accessToken)
    {
        if (!File.Exists(gameExePath))
            throw new FileNotFoundException("Oyun yürütülebilir dosyası bulunamadı.", gameExePath);

        var args = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(extraArgs))
            args.Append(extraArgs.Trim()).Append(' ');

        var psi = new ProcessStartInfo
        {
            FileName = gameExePath,
            WorkingDirectory = Path.GetDirectoryName(gameExePath) ?? ".",
            UseShellExecute = false
        };

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            args.Append("-token ").Append(Quote(accessToken));
            psi.Environment["HAREKAT_ACCESS_TOKEN"] = accessToken;
        }

        psi.Arguments = args.ToString().Trim();
        return Process.Start(psi) ?? throw new InvalidOperationException("Oyun süreci başlatılamadı.");
    }

    private static string Quote(string value)
    {
        if (value.Contains('"'))
            value = value.Replace("\"", "\\\"");
        return $"\"{value}\"";
    }
}
