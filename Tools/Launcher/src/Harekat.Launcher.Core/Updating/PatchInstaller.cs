using System.IO.Compression;

namespace Harekat.Launcher.Core.Updating;

/// <summary>Kurulum başarısız oldu; <see cref="RolledBack"/> true ise kurulum dizini eski hâline döndü.</summary>
public sealed class PatchInstallException : Exception
{
    public PatchInstallException(string message, bool rolledBack, Exception? inner = null)
        : base(message, inner)
    {
        RolledBack = rolledBack;
    }

    public bool RolledBack { get; }
}

public sealed record PatchInstallResult(
    IReadOnlyList<string> UpdatedFiles,
    IReadOnlyList<string> DeletedPaths,
    bool BackupLeftBehind);

public sealed class PatchInstallerOptions
{
    /// <summary>Zip içindeki toplam açılmış boyut sınırı (zip bombasına karşı).</summary>
    public long MaxExtractedBytes { get; init; } = 64L * 1024 * 1024 * 1024;

    /// <summary>Zip içindeki en fazla girdi sayısı.</summary>
    public int MaxEntries { get; init; } = 250_000;
}

/// <summary>
/// Doğrulanmış yama zip'ini kurulum dizinine uygular.
/// <para>Akış: (1) zip güvenli biçimde <c>.harekat-update/staging-*</c> altına açılır (zip-slip, mutlak yol,
/// boyut sınırı denetimi) → (2) <c>harekat-delete.txt</c> okunur → (3) değişecek/silinecek her dosya önce
/// <c>backup-*</c> altına taşınır, yenisi yerine taşınır (aynı birimde atomik yeniden adlandırma) →
/// (4) herhangi bir hata/iptal olursa günlük tersine çalıştırılıp geri alınır.</para>
/// <para>Çalışan EXE'ler (ör. launcher'ın kendisi) Windows'ta silinemez ama yeniden adlandırılabilir;
/// bu yüzden yedeğe taşıma çalışır ve yedek klasörü bir sonraki açılışta <see cref="CleanupWorkDir"/> ile silinir.</para>
/// </summary>
public sealed class PatchInstaller
{
    /// <summary>Kurulum dizini altındaki çalışma klasörü (indirmeler, staging, yedek).</summary>
    public const string WorkDirName = ".harekat-update";

    /// <summary>Zip kökünde isteğe bağlı silme listesi: her satır bir göreli yol, '#' yorum.</summary>
    public const string DeleteListName = "harekat-delete.txt";

    public const string DownloadsDirName = "downloads";

    private readonly PatchInstallerOptions _options;

    public PatchInstaller(PatchInstallerOptions? options = null)
    {
        _options = options ?? new PatchInstallerOptions();
    }

    public static string GetWorkDir(string installDir) => Path.Combine(installDir, WorkDirName);

    public static string GetDownloadsDir(string installDir) => Path.Combine(GetWorkDir(installDir), DownloadsDirName);

    /// <summary>Kurulum sırasında zip'ten alınmayan (launcher'ın yönettiği) göreli yollar.</summary>
    public static bool IsReservedPath(string normalizedRelativePath) =>
        PathSafety.StartsWithSegment(normalizedRelativePath, WorkDirName)
        || normalizedRelativePath.Equals(LocalVersionStore.FileName, StringComparison.OrdinalIgnoreCase)
        || normalizedRelativePath.Equals(DeleteListName, StringComparison.OrdinalIgnoreCase);

    public PatchInstallResult Install(
        string zipPath,
        string installDir,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken ct = default)
    {
        var root = Path.GetFullPath(installDir);
        Directory.CreateDirectory(root);
        var work = GetWorkDir(root);
        var id = Guid.NewGuid().ToString("N");
        var staging = Path.Combine(work, "staging-" + id);
        var backup = Path.Combine(work, "backup-" + id);

        try
        {
            Directory.CreateDirectory(staging);

            // 1) Aç + doğrula. Bu aşamada kurulum dizinine dokunulmaz.
            var (files, deletes) = ExtractToStaging(zipPath, staging, progress, ct);

            // 2) Uygula (günlüklü).
            var journal = new List<JournalEntry>();
            try
            {
                ApplyWithJournal(root, staging, backup, files, deletes, journal, progress, ct);
            }
            catch (Exception ex)
            {
                progress?.Report(new UpdateProgress(UpdateStage.RollingBack, 0, journal.Count));
                var rollbackErrors = Rollback(root, backup, journal);
                if (rollbackErrors.Count == 0)
                {
                    TryDeleteDirectory(backup);
                    var msg = ex is OperationCanceledException
                        ? "Kurulum iptal edildi; dosyalar eski hâline döndürüldü."
                        : "Yama uygulanamadı; dosyalar eski hâline döndürüldü: " + ex.Message;
                    if (ex is OperationCanceledException)
                        throw new OperationCanceledException(msg, ex, ct);
                    throw new PatchInstallException(msg, rolledBack: true, ex);
                }

                throw new PatchInstallException(
                    "Yama uygulanamadı VE geri alma tamamlanamadı. Yedek: " + backup + "\n"
                    + string.Join("\n", rollbackErrors.Take(10)),
                    rolledBack: false, ex);
            }

            progress?.Report(new UpdateProgress(UpdateStage.Finalizing, 0, 1));
            var backupLeft = !TryDeleteDirectory(backup);
            return new PatchInstallResult(
                files,
                journal.Where(j => j.Kind is EntryKind.DeletedFile or EntryKind.DeletedDirectory)
                    .Select(j => j.RelativePath).ToList(),
                backupLeft);
        }
        finally
        {
            TryDeleteDirectory(staging);
        }
    }

    /// <summary>
    /// Önceki çalıştırmalardan kalan staging/yedek klasörlerini siler (en iyi çaba).
    /// <c>downloads/</c> korunur (yarım indirmeler devam edebilsin). Silinemeyen klasör sayısını döner.
    /// </summary>
    public static int CleanupWorkDir(string installDir)
    {
        var work = GetWorkDir(installDir);
        if (!Directory.Exists(work))
            return 0;
        var failed = 0;
        foreach (var dir in Directory.EnumerateDirectories(work))
        {
            var name = Path.GetFileName(dir);
            if (name.StartsWith("staging-", StringComparison.Ordinal) || name.StartsWith("backup-", StringComparison.Ordinal))
            {
                if (!TryDeleteDirectory(dir))
                    failed++;
            }
        }
        return failed;
    }

    // ───────────────────────────── açma ─────────────────────────────

    private (List<string> Files, List<string> Deletes) ExtractToStaging(
        string zipPath, string staging, IProgress<UpdateProgress>? progress, CancellationToken ct)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count > _options.MaxEntries)
            throw new InvalidDataException($"Zip çok fazla girdi içeriyor ({archive.Entries.Count}).");

        long declaredTotal = 0;
        foreach (var e in archive.Entries)
        {
            declaredTotal += e.Length;
            if (declaredTotal > _options.MaxExtractedBytes)
                throw new InvalidDataException("Zip açılmış boyut sınırını aşıyor.");
        }

        var files = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deletes = new List<string>();
        long written = 0;
        var index = 0;

        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();
            index++;

            var isDirectory = entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');
            if (isDirectory && entry.Length == 0)
            {
                // Klasör girdisi: yine de yolu doğrula (kötü niyetli ad zararsız olsa da reddedilir).
                if (!PathSafety.TryNormalizeRelativePath(entry.FullName, out _))
                    throw new InvalidDataException($"Zip güvensiz klasör yolu içeriyor: '{entry.FullName}'");
                continue;
            }

            if (!PathSafety.TryNormalizeRelativePath(entry.FullName, out var rel))
                throw new InvalidDataException($"Zip güvensiz yol içeriyor: '{entry.FullName}'");

            if (PathSafety.StartsWithSegment(rel, WorkDirName))
                throw new InvalidDataException($"Zip ayrılmış klasöre yazmaya çalışıyor: '{entry.FullName}'");

            if (!seen.Add(rel))
                throw new InvalidDataException($"Zip aynı dosyayı iki kez içeriyor: '{rel}'");

            var dest = PathSafety.CombineUnderRoot(staging, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

            using (var input = entry.Open())
            using (var output = new FileStream(dest, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16))
            {
                written += CopyBounded(input, output, entry.Length, ct);
                if (written > _options.MaxExtractedBytes)
                    throw new InvalidDataException("Zip açılmış boyut sınırını aşıyor.");
            }

            if (rel.Equals(DeleteListName, StringComparison.OrdinalIgnoreCase))
            {
                deletes.AddRange(ParseDeleteList(File.ReadAllLines(dest)));
                File.Delete(dest);
            }
            else if (IsReservedPath(rel))
            {
                // version.txt gibi launcher'ın yönettiği dosyalar zip'ten alınmaz.
                File.Delete(dest);
            }
            else
            {
                files.Add(rel);
            }

            progress?.Report(new UpdateProgress(UpdateStage.Extracting, index, archive.Entries.Count));
        }

        // Silinip aynı zamanda güncellenen yol → güncelleme kazanır (silme listesinden çıkar).
        var fileSet = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
        deletes = deletes.Where(d => !fileSet.Contains(d)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return (files, deletes);
    }

    /// <summary>Silme listesi satırlarını doğrular; güvensiz satır varsa tüm yama reddedilir.</summary>
    public static List<string> ParseDeleteList(IEnumerable<string> lines)
    {
        var result = new List<string>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            if (!PathSafety.TryNormalizeRelativePath(line, out var rel))
                throw new InvalidDataException($"Silme listesinde güvensiz yol: '{line}'");
            if (IsReservedPath(rel))
                throw new InvalidDataException($"Silme listesi ayrılmış yolu hedefliyor: '{line}'");
            result.Add(rel);
        }
        return result;
    }

    private static long CopyBounded(Stream input, Stream output, long declaredLength, CancellationToken ct)
    {
        var buffer = new byte[1 << 16];
        long total = 0;
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            total += read;
            if (total > declaredLength)
                throw new InvalidDataException("Zip girdisi bildirilen boyuttan büyük açılıyor (bozuk/kötü niyetli zip).");
            output.Write(buffer, 0, read);
        }
        return total;
    }

    // ───────────────────────────── uygulama + geri alma ─────────────────────────────

    private enum EntryKind { AddedFile, ReplacedFile, DeletedFile, DeletedDirectory }

    private sealed record JournalEntry(EntryKind Kind, string RelativePath);

    private static void ApplyWithJournal(
        string root, string staging, string backup,
        IReadOnlyList<string> files, IReadOnlyList<string> deletes,
        List<JournalEntry> journal, IProgress<UpdateProgress>? progress, CancellationToken ct)
    {
        var total = files.Count + deletes.Count;
        var done = 0;

        // Önce silmeler (dosya → klasör dönüşümü gibi durumlarda yer açılsın).
        foreach (var rel in deletes)
        {
            ct.ThrowIfCancellationRequested();
            var target = PathSafety.CombineUnderRoot(root, rel);
            var bak = PathSafety.CombineUnderRoot(backup, rel);
            if (File.Exists(target))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(bak)!);
                File.Move(target, bak);
                journal.Add(new JournalEntry(EntryKind.DeletedFile, rel));
            }
            else if (Directory.Exists(target))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(bak)!);
                Directory.Move(target, bak);
                journal.Add(new JournalEntry(EntryKind.DeletedDirectory, rel));
            }
            progress?.Report(new UpdateProgress(UpdateStage.Installing, ++done, total));
        }

        foreach (var rel in files)
        {
            ct.ThrowIfCancellationRequested();
            var src = PathSafety.CombineUnderRoot(staging, rel);
            var target = PathSafety.CombineUnderRoot(root, rel);
            var targetDir = Path.GetDirectoryName(target)!;
            if (File.Exists(targetDir))
                throw new IOException($"'{PathSafety.ToRelative(root, targetDir)}' bir dosya; klasör olarak kullanılamaz.");
            Directory.CreateDirectory(targetDir);

            if (File.Exists(target))
            {
                var bak = PathSafety.CombineUnderRoot(backup, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(bak)!);
                File.Move(target, bak);                       // çalışan EXE için de çalışır (yeniden adlandırma)
                journal.Add(new JournalEntry(EntryKind.ReplacedFile, rel));
            }
            else if (Directory.Exists(target))
            {
                throw new IOException($"'{rel}' kurulumda bir klasör; dosya ile değiştirilemez.");
            }
            else
            {
                journal.Add(new JournalEntry(EntryKind.AddedFile, rel));
            }

            File.Move(src, target);
            progress?.Report(new UpdateProgress(UpdateStage.Installing, ++done, total));
        }
    }

    private static List<string> Rollback(string root, string backup, List<JournalEntry> journal)
    {
        var errors = new List<string>();
        for (var i = journal.Count - 1; i >= 0; i--)
        {
            var (kind, rel) = (journal[i].Kind, journal[i].RelativePath);
            try
            {
                var target = PathSafety.CombineUnderRoot(root, rel);
                var bak = PathSafety.CombineUnderRoot(backup, rel);
                switch (kind)
                {
                    case EntryKind.AddedFile:
                        if (File.Exists(target)) File.Delete(target);
                        break;
                    case EntryKind.ReplacedFile:
                        if (File.Exists(target)) File.Delete(target);
                        File.Move(bak, target);
                        break;
                    case EntryKind.DeletedFile:
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        File.Move(bak, target);
                        break;
                    case EntryKind.DeletedDirectory:
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        Directory.Move(bak, target);
                        break;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                errors.Add($"{kind} {rel}: {ex.Message}");
            }
        }
        return errors;
    }

    private static bool TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
