using Project.Core.Domain;

namespace Project.Core.Interfaces
{
    public interface IWeapon
    {
        string WeaponId { get; }
        WeaponCategory Category { get; }
        int CurrentAmmo { get; }
        int MagazineSize { get; }
        bool CanFire { get; }
        bool TryFire(out DamageInfo damage);
        void Reload();
    }
}
