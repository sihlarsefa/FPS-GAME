namespace Harekat.Telemetry.Domain.Catalogs;

/// <summary>
/// Kalıcı silah kimlikleri — Unity WeaponIds ile birebir aynı.
/// </summary>
public static class WeaponIds
{
    public const string Sar9 = "pistol_sar9";
    public const string Tp9 = "pistol_tp9";
    public const string Sar109 = "smg_sar109t";
    public const string Mpt55 = "ar_mpt55";
    public const string Mpt76 = "ar_mpt76";
    public const string G3 = "ar_g3a7";
    public const string Knt76 = "dmr_knt76";
    public const string Jng90 = "sr_jng90";
    public const string Pmt76 = "lmg_pmt76";
    public const string Escort = "sg_escort";

    public static readonly IReadOnlyList<string> All =
    [
        Sar9, Tp9, Sar109, Mpt55, Mpt76, G3, Knt76, Jng90, Pmt76, Escort
    ];

    public static bool IsKnown(string? weaponId) =>
        !string.IsNullOrWhiteSpace(weaponId) && All.Contains(weaponId, StringComparer.Ordinal);
}
