namespace Harekat.Launcher.Core.Updating;

/// <summary>
/// Zip girdileri ve silme listesi için yol güvenliği (zip-slip, mutlak yol, sürücü harfi,
/// ".." kaçışı, Windows aygıt adları). Döndürülen yol her zaman '/' ayraçlı göreli yoldur.
/// </summary>
public static class PathSafety
{
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    // Windows'ta dosya adında geçersiz karakterler (macOS/Linux'ta da aynı kuralı uygularız ki
    // testler platformdan bağımsız olsun).
    private static readonly char[] InvalidChars = ['<', '>', ':', '"', '|', '?', '*', '\0'];

    /// <summary>
    /// Göreli yolu doğrular ve normalize eder (<c>a\b/./c</c> → <c>a/b/c</c>).
    /// Güvensizse <c>false</c>.
    /// </summary>
    public static bool TryNormalizeRelativePath(string? path, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var p = path.Replace('\\', '/');
        if (p.StartsWith('/'))
            return false; // mutlak / UNC
        if (p.Length >= 2 && p[1] == ':')
            return false; // C:...

        var segments = new List<string>();
        foreach (var raw in p.Split('/'))
        {
            if (raw.Length == 0 || raw == ".")
                continue;
            if (raw == "..")
                return false;
            if (raw.IndexOfAny(InvalidChars) >= 0 || raw.Any(char.IsControl))
                return false;
            // Windows sonda nokta/boşluk kırpar → "a." ile "a" çakışır; reddet.
            if (raw.EndsWith('.') || raw.EndsWith(' '))
                return false;
            var stem = raw.Split('.')[0];
            if (ReservedDeviceNames.Contains(stem))
                return false;
            segments.Add(raw);
        }

        if (segments.Count == 0)
            return false;

        normalized = string.Join('/', segments);
        return true;
    }

    /// <summary>
    /// <paramref name="root"/> altındaki tam yolu üretir; sonuç kökün dışına çıkarsa
    /// <see cref="InvalidDataException"/>.
    /// </summary>
    public static string CombineUnderRoot(string root, string relativePath)
    {
        if (!TryNormalizeRelativePath(relativePath, out var rel))
            throw new InvalidDataException($"Güvensiz yol: '{relativePath}'");

        var rootFull = EnsureTrailingSeparator(Path.GetFullPath(root));
        var full = Path.GetFullPath(Path.Combine(rootFull, rel.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(rootFull, PathComparison))
            throw new InvalidDataException($"Yol kök dizinin dışına çıkıyor: '{relativePath}'");
        return full;
    }

    /// <summary>İlk segment (ör. <c>.harekat-update</c>) eşleşiyor mu?</summary>
    public static bool StartsWithSegment(string normalizedRelativePath, string segment) =>
        normalizedRelativePath.Equals(segment, StringComparison.OrdinalIgnoreCase)
        || normalizedRelativePath.StartsWith(segment + "/", StringComparison.OrdinalIgnoreCase);

    public static string EnsureTrailingSeparator(string path) =>
        Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;

    /// <summary>Kök altındaki tam yoldan '/' ayraçlı göreli yol.</summary>
    public static string ToRelative(string root, string fullPath) =>
        Path.GetRelativePath(root, fullPath).Replace('\\', '/');

    public static StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
}
