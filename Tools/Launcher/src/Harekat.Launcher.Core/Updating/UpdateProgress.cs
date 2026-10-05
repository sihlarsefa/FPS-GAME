namespace Harekat.Launcher.Core.Updating;

public enum UpdateStage
{
    Downloading,
    Verifying,
    Extracting,
    Installing,
    RollingBack,
    Finalizing,
    Completed
}

/// <summary>Güncelleme ilerlemesi. <see cref="Percent"/> tüm akış için 0–100 arası tek çubuk değeridir.</summary>
public readonly record struct UpdateProgress(UpdateStage Stage, long Done, long Total)
{
    /// <summary>Aşama içi oran (0..1). Toplam bilinmiyorsa 0.</summary>
    public double StageFraction => Total > 0 ? Math.Clamp((double)Done / Total, 0, 1) : 0;

    /// <summary>
    /// Akışın tamamı için yüzde: indirme %0–70, doğrulama %70–75, açma %75–88,
    /// kurulum %88–98, sonlandırma %98–100.
    /// </summary>
    public int Percent
    {
        get
        {
            var (start, end) = Stage switch
            {
                UpdateStage.Downloading => (0, 70),
                UpdateStage.Verifying => (70, 75),
                UpdateStage.Extracting => (75, 88),
                UpdateStage.Installing => (88, 98),
                UpdateStage.RollingBack => (0, 0),
                UpdateStage.Finalizing => (98, 100),
                UpdateStage.Completed => (100, 100),
                _ => (0, 0)
            };
            return (int)Math.Round(start + (end - start) * StageFraction);
        }
    }
}
