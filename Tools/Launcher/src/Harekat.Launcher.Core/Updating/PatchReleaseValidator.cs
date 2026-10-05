using Harekat.Launcher.Core.Api;
using Harekat.Launcher.Core.Versioning;

namespace Harekat.Launcher.Core.Updating;

/// <summary>
/// <c>/client/version</c> yanıtının indirilebilir/kurulabilir olup olmadığını denetler.
/// Bütünlük SHA-256 ile sağlanır; özet backend'den HTTPS ile geldiği için yama URL'si de HTTPS olmalıdır
/// (yerel geliştirme için loopback http'ye ya da <c>AllowInsecureHttp</c>'ye izin verilir).
/// </summary>
public static class PatchReleaseValidator
{
    /// <summary>Sorun yoksa <c>null</c>, varsa kullanıcıya gösterilebilir açıklama.</summary>
    public static string? Validate(ClientVersionDto? release, bool allowInsecureHttp = false)
    {
        if (release is null)
            return "Sürüm bilgisi yok.";
        if (!GameVersion.TryParse(release.Version, out _))
            return $"Sunucu sürümü geçersiz: '{release.Version}'.";
        if (string.IsNullOrWhiteSpace(release.PatchUrl))
            return "Yama adresi (patchUrl) boş.";
        if (!Uri.TryCreate(release.PatchUrl.Trim(), UriKind.Absolute, out var uri))
            return "Yama adresi mutlak bir URL değil.";
        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            var insecureOk = uri.Scheme == Uri.UriSchemeHttp && (allowInsecureHttp || uri.IsLoopback);
            if (!insecureOk)
                return "Yama adresi HTTPS olmalı.";
        }
        if (!HashUtil.IsSha256Hex(release.Sha256))
            return "SHA-256 değeri geçersiz (64 karakter hex bekleniyor).";
        if (release.PatchSizeBytes < 0)
            return "Yama boyutu negatif olamaz.";
        return null;
    }
}
