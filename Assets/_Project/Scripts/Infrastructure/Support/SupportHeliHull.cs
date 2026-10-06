using System;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using UnityEngine;

namespace Project.Infrastructure.Support
{
    /// <summary>
    /// T-129 gövdesi: mermilerin isabet ettiği collider üzerindeki <see cref="IDamageable"/>. Kayıt defterine girmez
    /// (OwnerId geçersiz); BallisticsSystem bu türü tanıyıp <see cref="ApplyBullet"/> çağırır. Aynı tim mermisi geçer.
    /// </summary>
    public sealed class SupportHeliHull : MonoBehaviour, IDamageable
    {
        private readonly SupportHeliHealth _health = new SupportHeliHealth();

        public int Team { get; set; } = -1;
        public PlayerId Killer { get; private set; } = PlayerId.Invalid;
        public float Health => _health.Current;
        public float MaxHealth => _health.Max;

        public PlayerId OwnerId => PlayerId.Invalid;
        public bool IsAlive => !_health.IsDown;

        /// <summary>Gövde yok olduğunda (bir kez).</summary>
        public event Action<SupportHeliHull> Destroyed;

        /// <summary>Mermi isabeti. Dost mermisi için false döner (mermi geçer); aksi halde mermi durur.</summary>
        public bool ApplyBullet(int shooterTeam, PlayerId shooter, float damage)
        {
            if (!IsAlive)
                return false;
            if (shooterTeam >= 0 && shooterTeam == Team)
                return false;
            Hurt(damage, shooter);
            return true;
        }

        public void ApplyDamage(DamageInfo damage)
        {
            if (CombatantTeamOf(damage.AttackerId) == Team)
                return;
            Hurt(damage.Amount, damage.AttackerId);
        }

        private static int CombatantTeamOf(PlayerId id)
        {
            return Combat.CombatantRegistry.TryGet(id, out var c) && c != null ? c.Team : -2;
        }

        private void Hurt(float amount, PlayerId attacker)
        {
            if (_health.Damage(amount))
            {
                Killer = attacker;
                try { Destroyed?.Invoke(this); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
        }

        private void OnDestroy() => Destroyed = null;
    }
}
