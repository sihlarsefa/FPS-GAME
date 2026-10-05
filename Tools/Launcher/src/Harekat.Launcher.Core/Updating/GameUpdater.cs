using Harekat.Launcher.Core.Api;

namespace Harekat.Launcher.Core.Updating;

public sealed record UpdateResult(string Version, PatchInstallResult Install, bool LauncherFilesChanged);

/// <summary>
/// Uçtan uca güncelleme: doğrula → indir (+SHA-256) → güvenli kur (geri almalı) → version.txt.
/// version.txt yalnızca kurulum başarılı olduktan sonra yazılır; böylece yarım kalan bir güncelleme
/// bir sonraki açılışta yeniden denenir.
/// </summary>
public sealed class GameUpdater
{
    private readonly PatchDownloader _downloader;
    private readonly PatchInstaller _installer;
    private readonly bool _allowInsecureHttp;

    public GameUpdater(PatchDownloader downloader, PatchInstaller installer, bool allowInsecureHttp = false)
    {
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _installer = installer ?? throw new ArgumentNullException(nameof(installer));
        _allowInsecureHttp = allowInsecureHttp;
    }

    /// <param name="launcherDir">Launcher'ın kendi klasörü; yama bu klasörde dosya değiştirirse yeniden başlatma önerilir.</param>
    public async Task<UpdateResult> UpdateAsync(
        ClientVersionDto release,
        string installDir,
        string? launcherDir = null,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken ct = default)
    {
        var problem = PatchReleaseValidator.Validate(release, _allowInsecureHttp);
        if (problem is not null)
            throw new InvalidOperationException(problem);

        var root = Path.GetFullPath(installDir);
        var downloads = PatchInstaller.GetDownloadsDir(root);

        var zip = await _downloader.DownloadAsync(
            new Uri(release.PatchUrl.Trim()), release.Sha256, release.PatchSizeBytes,
            downloads, progress, ct).ConfigureAwait(false);

        var install = await Task.Run(() => _installer.Install(zip, root, progress, ct), ct).ConfigureAwait(false);

        LocalVersionStore.Write(root, release.Version);

        // Başarılı kurulumdan sonra indirme önbelleğini temizle (yer kaplamasın).
        try { File.Delete(zip); } catch (IOException) { } catch (UnauthorizedAccessException) { }

        var launcherChanged = launcherDir is not null && TouchesDirectory(root, launcherDir, install);
        progress?.Report(new UpdateProgress(UpdateStage.Completed, 1, 1));
        return new UpdateResult(release.Version.Trim(), install, launcherChanged);
    }

    internal static bool TouchesDirectory(string root, string directory, PatchInstallResult install)
    {
        var rel = PathSafety.ToRelative(root, Path.GetFullPath(directory)).TrimEnd('/');
        if (rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel))
            return false; // launcher kurulum dizini dışında
        if (rel is "" or ".")
            return install.UpdatedFiles.Count > 0 || install.DeletedPaths.Count > 0;
        return install.UpdatedFiles.Concat(install.DeletedPaths)
            .Any(p => PathSafety.StartsWithSegment(p, rel));
    }
}
