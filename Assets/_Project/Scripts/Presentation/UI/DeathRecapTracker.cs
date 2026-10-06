using System;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Combat;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Yerel oyuncunun aldığı hasarı (kim, hangi silah, hangi bölge, kaç metreden) kaydeder; ölümde
    /// <see cref="DeathRecap"/> üretir. Kendini sahne yüklenince kurar (DontDestroyOnLoad), yerel savaşanı
    /// yoklayarak bulur; başka sisteme bağımlı değildir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeathRecapTracker : MonoBehaviour
    {
        private static DeathRecapTracker _instance;
        private readonly DeathRecapBuilder _builder = new DeathRecapBuilder();
        private Combatant _local;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null)
                return;

            var go = new GameObject("[ÖlümÖzetiKaydı]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DeathRecapTracker>();
        }

        private void Update()
        {
            if (_local != null && _local.IsInitialized)
                return;

            Combatant found = null;
            var all = CombatantRegistry.All;
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c != null && c.IsInitialized && c.IsLocalPlayer)
                {
                    found = c;
                    break;
                }
            }

            if (found == _local)
                return;

            if (_local != null)
                _local.Damaged -= OnDamaged;
            _local = found;
            _builder.Clear();
            if (_local != null)
                _local.Damaged += OnDamaged;
        }

        private void OnDestroy()
        {
            if (_local != null)
                _local.Damaged -= OnDamaged;
            if (_instance == this)
                _instance = null;
        }

        private void OnDamaged(Combatant victim, DamageInfo info)
        {
            try
            {
                var dist = 0f;
                var attacker = -1;
                if (info.AttackerId.IsValid && info.AttackerId != victim.Id)
                {
                    attacker = info.AttackerId.Value;
                    if (CombatantRegistry.TryGet(info.AttackerId, out var a) && a != null)
                        dist = Vector3.Distance(a.transform.position, victim.transform.position);
                    else if (info.HasSourcePosition)
                        dist = Vector3.Distance(new Vector3(info.SourcePosition.X, info.SourcePosition.Y, info.SourcePosition.Z), victim.transform.position);
                }

                _builder.Add(new DamageTaken(Time.time, info.Amount, info.BodyPart, attacker, info.SourceWeaponId, dist));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>Verilen (ölmüş) yerel savaşan için özet üretir; kayıt yoksa son ölüm bilgisinden doldurur.</summary>
        public static DeathRecap Build(Combatant local)
        {
            var builder = _instance != null ? _instance._builder : new DeathRecapBuilder();
            var deathTime = local != null && local.DeathTime >= 0f ? local.DeathTime : Time.time;
            var recap = builder.Build(deathTime);
            recap.TimelineEnd = deathTime;
            if (local == null)
                return recap;

            string weaponId = null;
            if (recap.Timeline.Count > 0)
                weaponId = recap.Timeline[recap.Timeline.Count - 1].WeaponId;
            if (string.IsNullOrEmpty(weaponId))
                weaponId = local.LastDeathInfo.SourceWeaponId;
            recap.WeaponName = WeaponCatalog.GetDisplayName(weaponId);
            if (!recap.HasKiller && local.LastDeathInfo.IsHeadshot)
                recap.FinalHeadshot = true;

            if (local.LastAttackerId.IsValid && local.LastAttackerId != local.Id
                && CombatantRegistry.TryGet(local.LastAttackerId, out var killer) && killer != null)
            {
                recap.HasKiller = true;
                recap.KillerName = killer.RankedName;
                recap.KillerAlive = killer.IsAlive;
                var hs = killer.State;
                recap.KillerHealth = hs.Current;
                recap.KillerMaxHealth = hs.Max;
                if (recap.Distance <= 0f)
                    recap.Distance = Vector3.Distance(killer.transform.position, local.transform.position);
            }
            else
            {
                recap.HasKiller = false;
                recap.KillerName = string.Empty;
            }

            return recap;
        }
    }
}
