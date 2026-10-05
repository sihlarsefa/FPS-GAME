using System.Diagnostics;

namespace Harekat.Launcher.Core.Launch;

/// <summary>Oyun sürecini başlatır / çalışıp çalışmadığını denetler (güncellemeden önce).</summary>
public static class GameLauncher
{
    public static Process Start(GameLaunchSpec spec)
    {
        if (!File.Exists(spec.FileName))
            throw new FileNotFoundException("Oyun dosyası bulunamadı.", spec.FileName);
        return Process.Start(spec.ToStartInfo())
               ?? throw new InvalidOperationException("Oyun süreci başlatılamadı.");
    }

    /// <summary>
    /// Aynı EXE yolundan çalışan süreç var mı? Yol okunamazsa (erişim yok) ad eşleşmesi yeterli sayılır
    /// — güncellemeyi gereksiz yere engellemek, açık dosyaların üzerine yazmaktan iyidir.
    /// </summary>
    public static bool IsRunning(string exePath)
    {
        var full = Path.GetFullPath(exePath);
        var name = Path.GetFileNameWithoutExtension(full);
        Process[] procs;
        try
        {
            procs = Process.GetProcessesByName(name);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        try
        {
            foreach (var p in procs)
            {
                try
                {
                    var path = p.MainModule?.FileName;
                    if (path is null || string.Equals(Path.GetFullPath(path), full, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    return true;
                }
            }
            return false;
        }
        finally
        {
            foreach (var p in procs)
                p.Dispose();
        }
    }
}
