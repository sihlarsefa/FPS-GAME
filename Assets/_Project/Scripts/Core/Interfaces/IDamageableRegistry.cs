using Project.Core.Domain;

namespace Project.Core.Interfaces
{
    public interface IDamageableRegistry
    {
        void Register(IDamageable damageable);
        void Unregister(IDamageable damageable);
        bool TryGet(PlayerId id, out IDamageable damageable);
    }
}
