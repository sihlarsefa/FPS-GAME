using Project.Core.Domain;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    public sealed class BasicWeaponService : IWeapon
    {
        private readonly string _weaponId;
        private readonly WeaponCategory _category;
        private readonly float _damage;
        private readonly int _magazineSize;
        private int _currentAmmo;

        public BasicWeaponService(string weaponId, WeaponCategory category, float damage, int magazineSize)
        {
            _weaponId = weaponId;
            _category = category;
            _damage = damage;
            _magazineSize = magazineSize;
            _currentAmmo = magazineSize;
        }

        public string WeaponId => _weaponId;
        public WeaponCategory Category => _category;
        public int CurrentAmmo => _currentAmmo;
        public int MagazineSize => _magazineSize;
        public bool CanFire => _currentAmmo > 0;

        public bool TryFire(out DamageInfo damage)
        {
            if (!CanFire)
            {
                damage = default;
                return false;
            }

            _currentAmmo--;
            damage = new DamageInfo(_damage, PlayerId.Invalid, _weaponId);
            return true;
        }

        public void Reload()
        {
            _currentAmmo = _magazineSize;
        }
    }
}
