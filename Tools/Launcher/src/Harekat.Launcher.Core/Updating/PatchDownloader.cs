using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace Harekat.Launcher.Core.Updating;

/// <summary>İndirme ağ/sunucu hatası.</summary>
public sealed class PatchDownloadException : Exception
{
    public PatchDownloadException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>İndirilen dosya beklenen boyut ya da SHA-256 ile uyuşmuyor.</summary>
public sealed class PatchVerificationException : Exception
{
    public PatchVerificationException(string message) : base(message) { }
}

/// <summary>
/// Yama zip'ini indirir; SHA-256'yı akış sırasında hesaplar, boyutu ve özeti doğrular.
/// <list type="bullet">
/// <item>Dosya adı beklenen özetten türetilir (<c>{sha256}.zip</c>); farklı yamalar karışmaz.</item>
/// <item>Yarım kalan indirme <c>.part</c> olarak saklanır ve HTTP Range ile kaldığı yerden sürer.</item>
/// <item>Sunucu Range'i yok sayarsa (200) baştan indirir; doğrulanmayan dosya silinir, asla kurulmaz.</item>
/// </list>
/// </summary>
public sealed class PatchDownloader
{
    private const int BufferSize = 1 << 16;
    private const long ProgressStepBytes = 512 * 1024;

    private readonly HttpClient _http;

    public PatchDownloader(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    /// <summary>Bu süre boyunca hiç veri gelmezse indirme durdurulur (bağlantı asılı kaldıysa).</summary>
    public TimeSpan StallTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Büyük dosyalar için zaman aşımı yalnızca <see cref="StallTimeout"/> ile yönetilir.</summary>
    public static HttpClient CreateDefaultHttpClient()
    {
        var http = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.None, // özet ham baytlar üzerinden
            ConnectTimeout = TimeSpan.FromSeconds(20)
        })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(Api.BackendApiClient.UserAgent);
        return http;
    }

    public static string FinalFileName(string sha256) => HashUtil.NormalizeSha256(sha256) + ".zip";

    /// <returns>Doğrulanmış zip dosyasının yolu.</returns>
    public async Task<string> DownloadAsync(
        Uri url,
        string expectedSha256,
        long expectedSize,
        string cacheDir,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        var sha = HashUtil.NormalizeSha256(expectedSha256);
        Directory.CreateDirectory(cacheDir);
        var finalPath = Path.Combine(cacheDir, sha + ".zip");
        var partPath = finalPath + ".part";

        // 1) Daha önce tamamlanmış ve doğrulanmış indirme varsa yeniden kullan.
        if (File.Exists(finalPath))
        {
            progress?.Report(new UpdateProgress(UpdateStage.Verifying, 0, 1));
            if (await FileMatchesAsync(finalPath, sha, expectedSize, ct).ConfigureAwait(false))
            {
                progress?.Report(new UpdateProgress(UpdateStage.Verifying, 1, 1));
                return finalPath;
            }
            File.Delete(finalPath);
        }

        // 2) İndir (en fazla bir kez baştan yeniden dene: bozuk .part / 416 / beklenmeyen Content-Range).
        for (var attempt = 0; attempt < 2; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var existing = File.Exists(partPath) ? new FileInfo(partPath).Length : 0L;

            if (expectedSize > 0 && existing > expectedSize)
            {
                File.Delete(partPath);
                existing = 0;
            }

            if (expectedSize > 0 && existing == expectedSize)
            {
                // Önceki çalıştırmada tamamı inmiş ama taşınamamış.
                if (await FileMatchesAsync(partPath, sha, expectedSize, ct).ConfigureAwait(false))
                {
                    File.Move(partPath, finalPath, overwrite: true);
                    return finalPath;
                }
                File.Delete(partPath);
                existing = 0;
            }

            var outcome = await DownloadOnceAsync(url, partPath, existing, sha, expectedSize, progress, ct)
                .ConfigureAwait(false);

            switch (outcome)
            {
                case Outcome.Verified:
                    File.Move(partPath, finalPath, overwrite: true);
                    progress?.Report(new UpdateProgress(UpdateStage.Verifying, 1, 1));
                    return finalPath;
                case Outcome.RetryFromScratch:
                    TryDelete(partPath);
                    continue;
            }
        }

        TryDelete(partPath);
        throw new PatchDownloadException("Yama indirilemedi: yeniden deneme de başarısız oldu.");
    }

    private enum Outcome { Verified, RetryFromScratch }

    private async Task<Outcome> DownloadOnceAsync(
        Uri url, string partPath, long existing, string sha, long expectedSize,
        IProgress<UpdateProgress>? progress, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (existing > 0)
            req.Headers.Range = new RangeHeaderValue(existing, null);

        HttpResponseMessage res;
        try
        {
            res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new PatchDownloadException("Yama sunucusuna bağlanılamadı: " + ex.Message, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new PatchDownloadException("Yama sunucusu yanıt vermedi (zaman aşımı).", ex);
        }

        using (res)
        {
            if (res.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable && existing > 0)
                return Outcome.RetryFromScratch;
            if (!res.IsSuccessStatusCode)
                throw new PatchDownloadException($"Yama indirilemedi: HTTP {(int)res.StatusCode} {res.ReasonPhrase}");

            var append = false;
            if (existing > 0)
            {
                if (res.StatusCode == HttpStatusCode.PartialContent)
                {
                    if (res.Content.Headers.ContentRange?.From != existing)
                        return Outcome.RetryFromScratch;
                    append = true;
                }
                // 200 OK → sunucu Range desteklemiyor; baştan yaz.
            }
            if (!append)
                existing = 0;

            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            if (append)
                await FeedFileAsync(partPath, hasher, ct).ConfigureAwait(false);

            var total = expectedSize > 0
                ? expectedSize
                : res.Content.Headers.ContentLength is { } len ? existing + len : -1;

            long done = existing;
            var oversize = false;
            await using (var output = new FileStream(partPath, append ? FileMode.Append : FileMode.Create,
                             FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
            await using (var input = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            {
                var buffer = new byte[BufferSize];
                using var stall = CancellationTokenSource.CreateLinkedTokenSource(ct);
                long lastReported = -ProgressStepBytes;
                progress?.Report(new UpdateProgress(UpdateStage.Downloading, done, total));

                while (true)
                {
                    stall.CancelAfter(StallTimeout);
                    int read;
                    try
                    {
                        read = await input.ReadAsync(buffer.AsMemory(), stall.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
                    {
                        throw new PatchDownloadException("İndirme durdu: sunucudan veri gelmiyor.", ex);
                    }
                    catch (IOException ex)
                    {
                        throw new PatchDownloadException("İndirme bağlantısı koptu: " + ex.Message, ex);
                    }

                    if (read == 0)
                        break;

                    if (expectedSize > 0 && done + read > expectedSize)
                    {
                        oversize = true;
                        break;
                    }

                    hasher.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    done += read;

                    if (done - lastReported >= ProgressStepBytes)
                    {
                        lastReported = done;
                        progress?.Report(new UpdateProgress(UpdateStage.Downloading, done, total));
                    }
                }

                await output.FlushAsync(ct).ConfigureAwait(false);
            }

            progress?.Report(new UpdateProgress(UpdateStage.Downloading, done, total > 0 ? total : done));
            progress?.Report(new UpdateProgress(UpdateStage.Verifying, 0, 1));

            if (oversize)
            {
                TryDelete(partPath);
                throw new PatchVerificationException(
                    $"Yama beklenenden büyük (beklenen {expectedSize} bayt). Dosya silindi.");
            }

            if (expectedSize > 0 && done != expectedSize)
            {
                // Bağlantı erken kapandı: .part kalsın, bir sonraki denemede kaldığı yerden devam eder.
                throw new PatchDownloadException(
                    $"İndirme eksik kaldı ({done}/{expectedSize} bayt). Tekrar deneyin; kaldığı yerden devam eder.");
            }

            var actual = HashUtil.ToHex(hasher.GetHashAndReset());
            if (string.Equals(actual, sha, StringComparison.Ordinal))
                return Outcome.Verified;

            if (append)
                return Outcome.RetryFromScratch; // eski .part bozuk olabilir; temiz indirme dene

            TryDelete(partPath);
            throw new PatchVerificationException(
                $"SHA-256 uyuşmuyor. Beklenen: {sha}, hesaplanan: {actual}. Dosya silindi.");
        }
    }

    private static async Task FeedFileAsync(string path, IncrementalHash hasher, CancellationToken ct)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        var buffer = new byte[BufferSize];
        int read;
        while ((read = await fs.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false)) > 0)
            hasher.AppendData(buffer, 0, read);
    }

    private static async Task<bool> FileMatchesAsync(string path, string sha, long expectedSize, CancellationToken ct)
    {
        if (expectedSize > 0 && new FileInfo(path).Length != expectedSize)
            return false;
        var actual = await HashUtil.ComputeFileSha256Async(path, ct).ConfigureAwait(false);
        return string.Equals(actual, sha, StringComparison.Ordinal);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
