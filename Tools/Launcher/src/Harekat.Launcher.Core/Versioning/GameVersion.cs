using System.Globalization;

namespace Harekat.Launcher.Core.Versioning;

/// <summary>
/// İstemci sürümü. SemVer 2.0 öncelik kurallarını izler, ek olarak:
/// <list type="bullet">
/// <item>1–4 sayısal bileşen kabul eder (<c>1</c>, <c>1.2</c>, <c>1.2.3</c>, <c>1.2.3.4</c>); eksikler 0 sayılır.</item>
/// <item>Baştaki <c>v</c>/<c>V</c> yok sayılır; <c>+build</c> meta verisi karşılaştırmaya katılmaz.</item>
/// <item>Ön sürüm (<c>-beta.2</c>) aynı sayısal sürümün kararlısından küçüktür.</item>
/// </list>
/// </summary>
public sealed class GameVersion : IComparable<GameVersion>, IEquatable<GameVersion>
{
    public static readonly GameVersion Zero = new([0, 0, 0, 0], [], "0.0.0");

    private readonly int[] _numbers;      // her zaman 4 eleman
    private readonly string[] _preRelease;

    private GameVersion(int[] numbers, string[] preRelease, string text)
    {
        _numbers = numbers;
        _preRelease = preRelease;
        Text = text;
    }

    public int Major => _numbers[0];
    public int Minor => _numbers[1];
    public int Patch => _numbers[2];
    public int Revision => _numbers[3];
    public IReadOnlyList<string> PreRelease => _preRelease;
    public bool IsPreRelease => _preRelease.Length > 0;

    /// <summary>Ayrıştırılan özgün metin (kırpılmış).</summary>
    public string Text { get; }

    public static GameVersion Parse(string text) =>
        TryParse(text, out var v) ? v : throw new FormatException($"Geçersiz sürüm: '{text}'");

    public static bool TryParse(string? text, out GameVersion version)
    {
        version = Zero;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var s = text.Trim();
        var original = s;
        if (s.Length > 0 && (s[0] == 'v' || s[0] == 'V'))
            s = s[1..];

        var plus = s.IndexOf('+');
        if (plus >= 0)
        {
            var build = s[(plus + 1)..];
            if (build.Length == 0 || !build.Split('.').All(IsValidIdentifier))
                return false;
            s = s[..plus];
        }

        string[] pre = [];
        var dash = s.IndexOf('-');
        if (dash >= 0)
        {
            var preText = s[(dash + 1)..];
            if (preText.Length == 0)
                return false;
            pre = preText.Split('.');
            if (!pre.All(IsValidIdentifier))
                return false;
            s = s[..dash];
        }

        var parts = s.Split('.');
        if (parts.Length is < 1 or > 4)
            return false;

        var numbers = new int[4];
        for (var i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            if (p.Length == 0 || !p.All(char.IsAsciiDigit))
                return false;
            if (!int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
                return false;
        }

        version = new GameVersion(numbers, pre, original);
        return true;
    }

    /// <summary>Geçersizse <see cref="Zero"/> döner (yerel version.txt bozuksa güncelleme önerilsin diye).</summary>
    public static GameVersion ParseOrZero(string? text) => TryParse(text, out var v) ? v : Zero;

    private static bool IsValidIdentifier(string id) =>
        id.Length > 0 && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    public int CompareTo(GameVersion? other)
    {
        if (other is null)
            return 1;

        for (var i = 0; i < 4; i++)
        {
            var c = _numbers[i].CompareTo(other._numbers[i]);
            if (c != 0)
                return c;
        }

        // Ön sürümü olmayan, ön sürümlü olandan büyüktür.
        if (_preRelease.Length == 0 && other._preRelease.Length == 0) return 0;
        if (_preRelease.Length == 0) return 1;
        if (other._preRelease.Length == 0) return -1;

        var n = Math.Min(_preRelease.Length, other._preRelease.Length);
        for (var i = 0; i < n; i++)
        {
            var c = ComparePreReleaseIdentifier(_preRelease[i], other._preRelease[i]);
            if (c != 0)
                return c;
        }

        return _preRelease.Length.CompareTo(other._preRelease.Length);
    }

    private static int ComparePreReleaseIdentifier(string a, string b)
    {
        var aNum = a.All(char.IsAsciiDigit);
        var bNum = b.All(char.IsAsciiDigit);
        if (aNum && bNum)
        {
            // Uzun sayısal kimlikler için önce uzunluk (baştaki sıfırları atarak), sonra sözlük sırası.
            var at = a.TrimStart('0');
            var bt = b.TrimStart('0');
            var len = at.Length.CompareTo(bt.Length);
            return len != 0 ? len : string.CompareOrdinal(at, bt);
        }

        if (aNum) return -1; // sayısal < alfanümerik
        if (bNum) return 1;
        return Math.Sign(string.CompareOrdinal(a, b));
    }

    public bool Equals(GameVersion? other) => other is not null && CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is GameVersion v && Equals(v);

    public override int GetHashCode()
    {
        var h = new HashCode();
        foreach (var n in _numbers) h.Add(n);
        foreach (var p in _preRelease)
            h.Add(p.All(char.IsAsciiDigit) ? p.TrimStart('0') : p, StringComparer.Ordinal);
        return h.ToHashCode();
    }

    /// <summary>Normalize gösterim: en az 3 bileşen, revizyon 0 değilse 4; ön sürüm korunur.</summary>
    public override string ToString()
    {
        var core = Revision != 0
            ? $"{Major}.{Minor}.{Patch}.{Revision}"
            : $"{Major}.{Minor}.{Patch}";
        return _preRelease.Length == 0 ? core : core + "-" + string.Join('.', _preRelease);
    }

    public static bool operator ==(GameVersion? a, GameVersion? b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(GameVersion? a, GameVersion? b) => !(a == b);
    public static bool operator <(GameVersion a, GameVersion b) => a.CompareTo(b) < 0;
    public static bool operator >(GameVersion a, GameVersion b) => a.CompareTo(b) > 0;
    public static bool operator <=(GameVersion a, GameVersion b) => a.CompareTo(b) <= 0;
    public static bool operator >=(GameVersion a, GameVersion b) => a.CompareTo(b) >= 0;
}
