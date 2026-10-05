using Project.Core.Domain;

namespace Project.Core.Interfaces
{
    public interface IAmmoSource
    {
        int GetAmmo(AmmoType type);
        int TakeAmmo(AmmoType type, int maxAmount);
    }
}
