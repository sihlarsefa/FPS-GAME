using System.IO.Compression;

namespace Harekat.Launcher.Core.Updating;

public sealed class PatchBuildOptions
{
    /// <summary>
    /// Göreli yol önekleri (ör. <c>Logs</c>) — hem eski hem yeni ağaçta yok sayılır.
    /// Unity'nin "DoNotShip" klasörleri, version.txt ve launcher çalışma klasörü her zaman hariçtir.
    /// </summary>
    public IReadOnlyList<string> ExcludePrefixes { get; init; } = [];

    public CompressionLevel Compression { get; init; } = CompressionLevel.Optimal;
}

public sealed record PatchBuildResult(
    string ZipPath,
    string Sha256,
    long SizeBytes,
    int AddedOrChanged,
    int Unchanged,
    int Deleted,
    bool IsFull);

/// <summary>
/// Yama zip'i üretir (yönetici aracı: <c>Harekat.PatchTool</c>).
/// <list type="bullet">
/// <item><b>Tam</b> (<c>oldDir</c> yok): yeni build'in tüm dosyaları.</item>
/// <item><b>Fark</b> (<c>oldDir</c> var): yalnızca eklenen/değişen dosyalar (boyut + SHA-256 karşılaştırma)
/// ve kaldırılanlar için kökte <see cref="PatchInstaller.DeleteListName"/>.</item>
/// </list>
/// Not: Backend kanal başına tek yama tutar; fark yaması yalnızca <c>oldDir</c> sürümündeki istemcileri
/// doğru günceller. Daha eski istemciler varsa tam/kümülatif yama yayınlayın (README).
/// </summary>
public static class PatchBuilder
{
    private static readonly string[] AlwaysExcludedSuffixes =
    [
        "_BurstDebugInformation_DoNotShip",
        "_BackUpThisFolder_ButDontShipItWithYourGame"
    ];

    public static PatchBuildResult Build(string newDir, string? oldDir, string outputZip, PatchBuildOptions? options = null)
    {
        options ??= new PatchBuildOptions();
        var newRoot = Path.GetFullPath(newDir);
        if (!Directory.Exists(newRoot))
            throw new DirectoryNotFoundException("Yeni build klasörü yok: " + newRoot);

        var newFiles = Snapshot(newRoot, options);
        Dictionary<string, string>? oldFiles = null;
        if (!string.IsNullOrWhiteSpace(oldDir))
        {
            var oldRoot = Path.GetFullPath(oldDir);
            if (!Directory.Exists(oldRoot))
                throw new DirectoryNotFoundException("Eski build klasörü yok: " + oldRoot);
            oldFiles = Snapshot(oldRoot, options);
        }

        var include = new List<string>();
        var unchanged = 0;
        foreach (var (rel, full) in newFiles.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            if (oldFiles is not null && oldFiles.TryGetValue(rel, out var oldFull) && SameContent(full, oldFull))
            {
                unchanged++;
                continue;
            }
            include.Add(rel);
        }

        var deletes = oldFiles is null
            ? new List<string>()
            : oldFiles.Keys.Where(k => !newFiles.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();

        var outFull = Path.GetFullPath(outputZip);
        Directory.CreateDirectory(Path.GetDirectoryName(outFull)!);
        if (File.Exists(outFull))
            File.Delete(outFull);

        using (var zip = ZipFile.Open(outFull, ZipArchiveMode.Create))
        {
            foreach (var rel in include)
                zip.CreateEntryFromFile(newFiles[rel], rel, options.Compression);

            if (deletes.Count > 0)
            {
                var entry = zip.CreateEntry(PatchInstaller.DeleteListName, CompressionLevel.Optimal);
                using var w = new StreamWriter(entry.Open());
                w.WriteLine("# HAREKAT yama silme listesi — launcher bu yolları kaldırır.");
                foreach (var d in deletes)
                    w.WriteLine(d);
            }
        }

        return new PatchBuildResult(
            outFull,
            HashUtil.ComputeFileSha256(outFull),
            new FileInfo(outFull).Length,
            include.Count,
            unchanged,
            deletes.Count,
            IsFull: oldFiles is null);
    }

    /// <summary>Göreli yol ('/' ayraçlı) → tam yol. Hariç tutulanlar atlanır.</summary>
    internal static Dictionary<string, string> Snapshot(string root, PatchBuildOptions options)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var full in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var rel = PathSafety.ToRelative(root, full);
            if (IsExcluded(rel, options))
                continue;
            if (!PathSafety.TryNormalizeRelativePath(rel, out var norm))
                throw new InvalidDataException($"Build içinde launcher'ın kabul etmeyeceği yol: '{rel}'");
            map[norm] = full;
        }
        return map;
    }

    internal static bool IsExcluded(string rel, PatchBuildOptions options)
    {
        if (PatchInstaller.IsReservedPath(rel))
            return true;
        var segments = rel.Split('/');
        if (segments.Any(s => AlwaysExcludedSuffixes.Any(x => s.EndsWith(x, StringComparison.OrdinalIgnoreCase))))
            return true;
        return options.ExcludePrefixes.Any(p =>
            PathSafety.TryNormalizeRelativePath(p, out var np) && PathSafety.StartsWithSegment(rel, np));
    }

    private static bool SameContent(string a, string b)
    {
        var fa = new FileInfo(a);
        var fb = new FileInfo(b);
        if (fa.Length != fb.Length)
            return false;
        return HashUtil.ComputeFileSha256(a) == HashUtil.ComputeFileSha256(b);
    }
}
