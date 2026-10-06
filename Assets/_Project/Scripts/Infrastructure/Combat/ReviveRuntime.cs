using System;
using System.Collections.Generic;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using UnityEngine;

namespace Project.Infrastructure.Combat
{
    /// <summary>
    /// Yaralı (DBNO) sisteminin Unity tarafı: tek ReviveService örneği + onu işleten gizli sürücü.
    /// Servis ilk kullanımda (Combatant yaralı olduğunda / HUD sorgusunda) tembel oluşturulur.
    /// Kanamadan ölüm ve kaldırma sonuçlarını CombatantRegistry üzerinden ilgili Combatant'a iletir.
    /// </summary>
    public static class ReviveRuntime
    {
        /// <summary>Kapatılırsa herkes doğrudan ölür (eski davranış).</summary>
        public static bool Enabled = true;

        private static ReviveService _service;
        private static IEventBus _serviceBus;
        private static GameObject _driver;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _service = null;
            _serviceBus = null;
            _driver = null;
            Enabled = true;
        }

        public static ReviveService Service
        {
            get
            {
                IEventBus bus = null;
                GameContext.TryGet<IEventBus>(out bus);
                if (_service == null || !ReferenceEquals(bus, _serviceBus))
                {
                    _service?.Clear();
                    _serviceBus = bus;
                    _service = new ReviveService(bus);
                    _service.BledOut += OnBledOut;
                    _service.Revived += OnRevived;
                    _service.ReviverValidator = IsValidReviver;
                }

                if (_driver == null && UnityEngine.Application.isPlaying)
                {
                    _driver = new GameObject("[ReviveDriver]") { hideFlags = HideFlags.HideAndDontSave };
                    _driver.AddComponent<ReviveDriver>();
                }

                return _service;
            }
        }

        public static bool IsDowned(PlayerId id) => _service != null && _service.IsDowned(id);

        /// <summary>Kendisi hariç, yaralı olmayan sağ takım arkadaşı sayısı.</summary>
        public static int CountHealthyAllies(Combatant self)
        {
            if (self == null || self.Team < 0)
                return 0;

            var n = 0;
            var all = CombatantRegistry.All;
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c != null && c != self && c.Team == self.Team && c.IsAlive && !c.IsDowned)
                    n++;
            }

            return n;
        }

        /// <summary>
        /// Takımda yaralı olmayan sağ üye kalmadıysa tüm yaralıları öldürür (takım elendi). Yeniden girişe dayanıklıdır:
        /// ölüm olayları zincirleme çağırabilir. Her yaralı, onu yaralı bırakan saldırgana yazılır (kill krediler tek sefer).
        /// </summary>
        public static void KillTeamIfWiped(int team)
        {
            if (team < 0)
                return;

            List<Combatant> downed = null;
            var healthy = 0;
            var all = CombatantRegistry.All;
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c.Team != team || !c.IsAlive)
                    continue;

                if (!c.IsDowned)
                    healthy++;
                else
                    (downed ??= new List<Combatant>(4)).Add(c);
            }

            if (!DownedRules.IsTeamWiped(healthy, downed?.Count ?? 0))
                return;

            var service = _service;
            for (var i = 0; i < downed.Count; i++)
            {
                var attacker = service != null ? service.AttackerOf(downed[i].Id) : PlayerId.Invalid;
                downed[i].BleedToDeath(attacker);
            }
        }

        private static bool IsValidReviver(PlayerId id) =>
            CombatantRegistry.TryGet(id, out var c) && c != null && c.IsAlive && !c.IsDowned;

        private static void OnBledOut(PlayerId victim, PlayerId attacker)
        {
            if (CombatantRegistry.TryGet(victim, out var c) && c != null)
                c.BleedToDeath(attacker);
        }

        private static void OnRevived(PlayerId victim, PlayerId reviver)
        {
            if (CombatantRegistry.TryGet(victim, out var c) && c != null)
                c.CompleteRevive();
        }

        private sealed class ReviveDriver : MonoBehaviour
        {
            private void Update()
            {
                if (_service == null)
                    return;

                try
                {
                    if (GameContext.HasAuthority)
                        _service.Tick(Time.deltaTime);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }
        }
    }
}
