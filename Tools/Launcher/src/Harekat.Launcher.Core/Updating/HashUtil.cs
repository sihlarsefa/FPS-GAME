using System.Security.Cryptography;

namespace Harekat.Launcher.Core.Updating;

/// <summary>SHA-256 yardımcıları (hex: 64 karakter, küçük harf normalize).</summary>
public static class HashUtil
{
    public const int Sha256HexLength = 64;

    public static bool IsSha256Hex(string? value)
    {
        if (value is null)
            return false;
        var s = value.Trim();
        return s.Length == Sha256HexLength && s.All(char.IsAsciiHexDigit);
    }

    /// <summary>Kırpar ve küçük harfe çevirir. Geçersizse <see cref="FormatException"/>.</summary>
    public static string NormalizeSha256(string value)
    {
        if (!IsSha256Hex(value))
            throw new FormatException("SHA-256 değeri 64 karakterlik hex olmalı.");
        return value.Trim().ToLowerInvariant();
    }

    public static bool Sha256Equals(string? a, string? b) =>
        IsSha256Hex(a) && IsSha256Hex(b)
        && string.Equals(a!.Trim(), b!.Trim(), StringComparison.OrdinalIgnoreCase);

    public static string ToHex(ReadOnlySpan<byte> hash) => Convert.ToHexString(hash).ToLowerInvariant();

    public static string ComputeSha256(ReadOnlySpan<byte> data) => ToHex(SHA256.HashData(data));

    public static async Task<string> ComputeFileSha256Async(string path, CancellationToken ct = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1 << 16, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return ToHex(hash);
    }

    public static string ComputeFileSha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
        return ToHex(SHA256.HashData(stream));
    }
}
