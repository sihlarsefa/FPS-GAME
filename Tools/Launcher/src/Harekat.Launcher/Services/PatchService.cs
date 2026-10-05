using System.IO.Compression;
using System.Security.Cryptography;

namespace Harekat.Launcher.Services;

public sealed class PatchService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(30) };

    public async Task ApplyAsync(
        ClientVersionDto remote,
        string installDir,
        IProgress<(string Status, int Percent)>? progress,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(installDir);
        var tempDir = Path.Combine(Path.GetTempPath(), "harekat-patch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var zipPath = Path.Combine(tempDir, "patch.zip");

        try
        {
            progress?.Report(("Yama indiriliyor…", 5));
            await DownloadAsync(remote.PatchUrl, zipPath, remote.PatchSizeBytes, progress, ct);

            progress?.Report(("SHA-256 doğrulanıyor…", 70));
            var hash = await ComputeSha256HexAsync(zipPath, ct);
            if (!string.Equals(hash, remote.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"SHA-256 uyuşmuyor.\nBeklenen: {remote.Sha256}\nHesaplanan: {hash}");

            progress?.Report(("Yama uygulanıyor…", 80));
            ZipFile.ExtractToDirectory(zipPath, installDir, overwriteFiles: true);

            WriteLocalVersion(installDir, remote.Version);
            progress?.Report(("Güncelleme tamamlandı.", 100));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    public static string ReadLocalVersion(string installDir)
    {
        var path = Path.Combine(installDir, "version.txt");
        if (!File.Exists(path)) return "0.0.0";
        return File.ReadAllText(path).Trim();
    }

    public static void WriteLocalVersion(string installDir, string version)
    {
        File.WriteAllText(Path.Combine(installDir, "version.txt"), version.Trim() + Environment.NewLine);
    }

    public static int CompareVersions(string a, string b)
    {
        static Version Parse(string s) =>
            Version.TryParse(s.Split('-', '+')[0], out var v) ? v : new Version(0, 0, 0);
        return Parse(a).CompareTo(Parse(b));
    }

    private async Task DownloadAsync(
        string url,
        string destPath,
        long expectedSize,
        IProgress<(string Status, int Percent)>? progress,
        CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.ParseAdd("HarekatLauncher/0.1");
        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        res.EnsureSuccessStatusCode();

        var total = res.Content.Headers.ContentLength ?? (expectedSize > 0 ? expectedSize : -1);
        await using var input = await res.Content.ReadAsStreamAsync(ct);
        await using var output = File.Create(destPath);

        var buffer = new byte[81920];
        long readTotal = 0;
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
            readTotal += read;
            if (total > 0)
            {
                var pct = (int)Math.Clamp(5 + readTotal * 60 / total, 5, 65);
                progress?.Report(($"İndiriliyor… {readTotal / 1024 / 1024} MB", pct));
            }
        }
    }

    private static async Task<string> ComputeSha256HexAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
