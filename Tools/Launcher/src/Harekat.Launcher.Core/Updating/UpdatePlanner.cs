using Harekat.Launcher.Core.Api;
using Harekat.Launcher.Core.Versioning;

namespace Harekat.Launcher.Core.Updating;

public enum UpdateState
{
    /// <summary>Yerel sürüm sunucudakiyle aynı.</summary>
    UpToDate,
    /// <summary>Yeni sürüm var, isteğe bağlı.</summary>
    UpdateAvailable,
    /// <summary>Yeni sürüm zorunlu; güncellemeden oynanamaz.</summary>
    UpdateRequired,
    /// <summary>Yerel sürüm sunucudakinden yeni (geliştirici/QA build'i).</summary>
    LocalNewer,
    /// <summary>Oyun dosyası yok; yama zip'i tam paket olarak kurulabilir.</summary>
    NotInstalled,
    /// <summary>Backend'e ulaşılamadı; kuruluysa çevrimdışı oynanabilir.</summary>
    ServerUnavailable,
    /// <summary>Kanal için yayımlanmış sürüm yok (backend 404).</summary>
    NoVersionPublished,
    /// <summary>Sunucunun bildirdiği sürüm metni ayrıştırılamadı.</summary>
    InvalidServerVersion
}

/// <summary>Launcher'ın OYNA / GÜNCELLE düğmeleri için tek karar noktası.</summary>
public sealed record UpdatePlan(
    UpdateState State,
    GameVersion Local,
    GameVersion? Remote,
    ClientVersionDto? Release,
    bool CanPlay,
    bool CanUpdate,
    string? Problem)
{
    public bool NeedsUpdate => State is UpdateState.UpdateAvailable or UpdateState.UpdateRequired or UpdateState.NotInstalled;
}

public static class UpdatePlanner
{
    /// <param name="localVersionText">version.txt içeriği (yoksa null).</param>
    /// <param name="gameInstalled">Oyun EXE'si diskte var mı?</param>
    /// <param name="release">/client/version yanıtı; 404 ise null.</param>
    /// <param name="serverReachable">Backend'e ulaşıldı mı (ağ/5xx hatası değilse true).</param>
    public static UpdatePlan Decide(
        string? localVersionText,
        bool gameInstalled,
        ClientVersionDto? release,
        bool serverReachable,
        bool allowInsecureHttp = false)
    {
        var local = gameInstalled ? GameVersion.ParseOrZero(localVersionText) : GameVersion.Zero;

        if (!serverReachable)
            return new UpdatePlan(UpdateState.ServerUnavailable, local, null, null,
                CanPlay: gameInstalled, CanUpdate: false, Problem: null);

        if (release is null)
            return new UpdatePlan(UpdateState.NoVersionPublished, local, null, null,
                CanPlay: gameInstalled, CanUpdate: false, Problem: null);

        if (!GameVersion.TryParse(release.Version, out var remote))
            return new UpdatePlan(UpdateState.InvalidServerVersion, local, null, release,
                CanPlay: gameInstalled, CanUpdate: false,
                Problem: $"Sunucu sürümü geçersiz: '{release.Version}'.");

        var patchProblem = PatchReleaseValidator.Validate(release, allowInsecureHttp);
        var patchOk = patchProblem is null;

        if (!gameInstalled)
            return new UpdatePlan(UpdateState.NotInstalled, local, remote, release,
                CanPlay: false, CanUpdate: patchOk, Problem: patchProblem);

        var cmp = local.CompareTo(remote);
        if (cmp == 0)
            return new UpdatePlan(UpdateState.UpToDate, local, remote, release, true, false, null);
        if (cmp > 0)
            return new UpdatePlan(UpdateState.LocalNewer, local, remote, release, true, false, null);

        return release.Mandatory
            ? new UpdatePlan(UpdateState.UpdateRequired, local, remote, release,
                CanPlay: false, CanUpdate: patchOk, Problem: patchProblem)
            : new UpdatePlan(UpdateState.UpdateAvailable, local, remote, release,
                CanPlay: true, CanUpdate: patchOk, Problem: patchProblem);
    }
}
