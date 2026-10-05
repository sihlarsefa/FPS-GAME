using System.Diagnostics;
using System.Globalization;
using Harekat.Launcher.Core.Configuration;
using Harekat.Launcher.Core.Updating;

namespace Harekat.Launcher.Core.Launch;

/// <summary>Launcher'da giriş yapılmışsa oyuna aktarılan oturum.</summary>
public sealed record LaunchSession(string AccessToken, string? RefreshToken, DateTimeOffset? ExpiresAt, string? Username);

/// <summary>Başlatılacak sürecin tam tanımı (test edilebilir; token'lar <see cref="ToDisplayString"/>'de gizlenir).</summary>
public sealed record GameLaunchSpec(
    string FileName,
    string WorkingDirectory,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment)
{
    public ProcessStartInfo ToStartInfo()
    {
        var psi = new ProcessStartInfo
        {
            FileName = FileName,
            WorkingDirectory = WorkingDirectory,
            UseShellExecute = false
        };
        // ArgumentList → .NET doğru tırnaklama yapar (token içinde boşluk/tırnak olsa da güvenli).
        foreach (var a in Arguments)
            psi.ArgumentList.Add(a);
        foreach (var (k, v) in Environment)
            psi.Environment[k] = v;
        return psi;
    }

    /// <summary>Günlük için: token değerleri <c>***</c> ile değiştirilir.</summary>
    public string ToDisplayString()
    {
        var shown = new List<string>(Arguments.Count);
        for (var i = 0; i < Arguments.Count; i++)
        {
            shown.Add(Arguments[i]);
            if (string.Equals(Arguments[i], GameLaunchArgs.TokenArg, StringComparison.OrdinalIgnoreCase) && i + 1 < Arguments.Count)
            {
                shown.Add("***");
                i++;
            }
        }
        return $"{FileName} {string.Join(' ', shown)}";
    }
}

/// <summary>
/// OYNA sözleşmesi (Unity tarafı F3-1 <c>Scripts/Online</c> bunu okur — README "Oyuna aktarım"):
/// <code>
/// HAREKAT.exe [ExtraGameArgs…] -launcher -lang tr -channel stable [-backend URL] [-token ACCESS_JWT]
/// env: HAREKAT_ACCESS_TOKEN, HAREKAT_REFRESH_TOKEN, HAREKAT_TOKEN_EXPIRES_AT (ISO-8601), HAREKAT_BACKEND_URL
/// </code>
/// Refresh token komut satırına YAZILMAZ (görev yöneticisinde görünür); yalnızca ortam değişkeniyle geçer.
/// </summary>
public static class GameLaunchArgs
{
    public const string TokenArg = "-token";
    public const string BackendArg = "-backend";
    public const string LauncherArg = "-launcher";
    public const string LanguageArg = "-lang";
    public const string ChannelArg = "-channel";

    public const string AccessTokenEnv = "HAREKAT_ACCESS_TOKEN";
    public const string RefreshTokenEnv = "HAREKAT_REFRESH_TOKEN";
    public const string TokenExpiresAtEnv = "HAREKAT_TOKEN_EXPIRES_AT";
    public const string BackendEnv = "HAREKAT_BACKEND_URL";

    /// <summary>Launcher'ın yönettiği argümanlar; ExtraGameArgs içinden ayıklanır.</summary>
    private static readonly HashSet<string> ManagedValueArgs = new(StringComparer.OrdinalIgnoreCase)
    {
        TokenArg, BackendArg, LanguageArg, ChannelArg
    };

    public static GameLaunchSpec Build(LauncherSettings settings, string installDir, string language, LaunchSession? session)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var root = Path.GetFullPath(installDir);
        var exe = PathSafety.CombineUnderRoot(root, settings.GameExecutable);

        var args = new List<string>();
        args.AddRange(FilterManaged(settings.ExtraGameArgs));
        args.Add(LauncherArg);
        args.Add(LanguageArg);
        args.Add(LauncherSettings.NormalizeLanguage(language));
        args.Add(ChannelArg);
        args.Add(settings.Channel);

        var env = new Dictionary<string, string>(StringComparer.Ordinal);

        if (settings.PassBackendUrlToGame && !string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
        {
            var backend = settings.ApiBaseUrl.Trim().TrimEnd('/');
            args.Add(BackendArg);
            args.Add(backend);
            env[BackendEnv] = backend;
        }

        if (session is not null && !string.IsNullOrWhiteSpace(session.AccessToken))
        {
            args.Add(TokenArg);
            args.Add(session.AccessToken);
            env[AccessTokenEnv] = session.AccessToken;
            if (!string.IsNullOrWhiteSpace(session.RefreshToken))
                env[RefreshTokenEnv] = session.RefreshToken!;
            if (session.ExpiresAt is { } exp)
                env[TokenExpiresAtEnv] = exp.ToString("O", CultureInfo.InvariantCulture);
        }

        return new GameLaunchSpec(exe, Path.GetDirectoryName(exe)!, args, env);
    }

    internal static IEnumerable<string> FilterManaged(IEnumerable<string> extra)
    {
        var list = extra.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var a = list[i];
            if (string.Equals(a, LauncherArg, StringComparison.OrdinalIgnoreCase))
                continue;
            if (ManagedValueArgs.Contains(a))
            {
                if (i + 1 < list.Count && !list[i + 1].StartsWith('-'))
                    i++; // değerini de atla
                continue;
            }
            yield return a;
        }
    }
}
