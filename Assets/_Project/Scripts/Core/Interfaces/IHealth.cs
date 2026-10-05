using Project.Core.Domain;

namespace Project.Core.Interfaces
{
    public interface IHealthReadModel
    {
        PlayerId OwnerId { get; }
        HealthState State { get; }
    }

    public interface IDamageable
    {
        PlayerId OwnerId { get; }
        bool IsAlive { get; }
        void ApplyDamage(DamageInfo damage);
    }

    public interface IHealable
    {
        void Heal(float amount);
    }

    /// <summary>Zırh taşıyabilen hedef (CombatService hasarı zırhla azaltır).</summary>
    public interface IArmored
    {
        IArmorProvider Armor { get; }
    }
}
