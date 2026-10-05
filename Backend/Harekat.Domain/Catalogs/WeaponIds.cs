namespace Harekat.Domain.Catalogs;

/// <summary>
/// Kalıcı silah kimlikleri — Türk yapımı silahlar (ağ ve kayıt için asla yeniden adlandırmayın).
/// Unity WeaponIds ile birebir aynı olmalıdır.
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
}

/// <summary>Silah dışı hasar kaynakları.</summary>
public static class DamageSourceIds
{
    public const string Zone = "zone";
    public const string Fall = "fall";
    public const string FragGrenade = "grenade_frag";
    public const string Fists = "melee_fists";
    public const string Artillery = "artillery";
    public const string Vehicle = "vehicle";
}
