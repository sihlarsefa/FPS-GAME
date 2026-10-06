using Project.Application.Catalogs;
using Project.Core.Domain;

namespace Project.Infrastructure.Audio
{
    /// <summary>Silah verisi -> akustik çap sınıfı (Acoustics.OnShot / OnBulletPassed girdisi).</summary>
    public static class AcousticsBridge
    {
        public static CaliberClass CaliberFor(WeaponDefinitionData weapon)
        {
            if (weapon == null)
                return CaliberClass.Rifle;
            if (WeaponSoundProfiles.TryGet(weapon.WeaponId, out var p))
                return CaliberFor(p.Caliber);
            switch (weapon.Category)
            {
                case WeaponCategory.Pistol:
                case WeaponCategory.Smg: return CaliberClass.Pistol;
                case WeaponCategory.Lmg: return CaliberClass.MachineGun;
                case WeaponCategory.Sniper: return CaliberClass.Sniper;
                default: return CaliberClass.Rifle;
            }
        }

        public static CaliberClass CaliberFor(WeaponCaliber c)
        {
            switch (c)
            {
                case WeaponCaliber.Pistol9:
                case WeaponCaliber.Smg9: return CaliberClass.Pistol;
                case WeaponCaliber.Lmg762: return CaliberClass.MachineGun;
                case WeaponCaliber.Sniper762:
                case WeaponCaliber.Dmr762: return CaliberClass.Sniper;
                default: return CaliberClass.Rifle;
            }
        }
    }
}
