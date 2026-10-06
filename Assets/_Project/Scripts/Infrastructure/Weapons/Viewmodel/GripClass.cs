namespace Project.Infrastructure.Weapons.Viewmodel
{
    /// <summary>Tutuş davranışı için silah sınıfı (el konumu, FOV, sol el kayması sınıfa göre değişir).</summary>
    public enum GripClass
    {
        None = 0,
        Pistol,
        Smg,
        Rifle,
        Dmr,
        Sniper,
        Lmg,
        Shotgun
    }

    public static class GripClasses
    {
        public static GripClass From(WeaponStyle style)
        {
            switch (style)
            {
                case WeaponStyle.Sar9:
                case WeaponStyle.Tp9:
                case WeaponStyle.MeteSft:
                    return GripClass.Pistol;
                case WeaponStyle.Sar109:
                    return GripClass.Smg;
                case WeaponStyle.Mpt55:
                case WeaponStyle.Mpt76:
                case WeaponStyle.Mpt76K:
                case WeaponStyle.Sar223:
                case WeaponStyle.G3a7:
                    return GripClass.Rifle;
                case WeaponStyle.Knt76:
                case WeaponStyle.Sar762Mt:
                    return GripClass.Dmr;
                case WeaponStyle.Jng90:
                    return GripClass.Sniper;
                case WeaponStyle.Pmt76:
                case WeaponStyle.Mg3:
                    return GripClass.Lmg;
                case WeaponStyle.Escort:
                case WeaponStyle.EscortMagnum:
                    return GripClass.Shotgun;
                default:
                    return GripClass.None;
            }
        }
    }
}
