using Harekat.Launcher.Core.Versioning;

namespace Harekat.Launcher.Core.Updating;

/// <summary>
/// Kurulu istemci sürümü: <c>{InstallDir}/version.txt</c> (tek satır, ör. <c>0.1.0</c>).
/// Inno Setup kurulumda yazar; launcher başarılı yamadan sonra atomik olarak günceller.
/// </summary>
public static class LocalVersionStore
{
    public const string FileName = "version.txt";

    public static string GetPath(string installDir) => Path.Combine(installDir, FileName);

    /// <summary>Ham metin; dosya yoksa/okunamazsa null.</summary>
    public static string? ReadRaw(string installDir)
    {
        var path = GetPath(installDir);
        try
        {
            if (!File.Exists(path))
                return null;
            var text = File.ReadAllText(path).Trim().TrimStart('﻿');
            return text.Length == 0 ? null : text.Split('\n')[0].Trim();
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    public static GameVersion Read(string installDir) => GameVersion.ParseOrZero(ReadRaw(installDir));

    /// <summary>Geçici dosyaya yazıp üzerine taşır (yarım yazılmış version.txt kalmaz).</summary>
    public static void Write(string installDir, string version)
    {
        if (!GameVersion.TryParse(version, out _))
            throw new FormatException($"Geçersiz sürüm yazılamaz: '{version}'");
        Directory.CreateDirectory(installDir);
        var path = GetPath(installDir);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, version.Trim() + Environment.NewLine);
        File.Move(tmp, path, overwrite: true);
    }
}
