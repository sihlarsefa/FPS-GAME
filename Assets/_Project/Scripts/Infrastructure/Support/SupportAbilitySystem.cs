using System;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;
using Project.Infrastructure.Combat;
using UnityEngine;

namespace Project.Infrastructure.Support
{
    /// <summary>HUD bildirimi türleri (T-129 ATAK Desteği).</summary>
    public enum SupportNoticeKind
    {
        Ready,
        Incoming,
        Departing,
        ShotDown
    }

    /// <summary>
    /// T-129 ATAK Desteği statik girişi: tim öldürme sayacı (PlayerDiedEvent), hazır bildirimi, çağrı (oyuncu/YZ komutan).
    /// Saf mantık <see cref="SupportAbilityService"/>'tedir. Yalnızca otoritede helikopter doğar.
    /// </summary>
    public static class SupportAbilitySystem
    {
        public const float SpawnDistance = 450f;
        public const float AiMinContactDistance = 60f;
        public const float AiMaxContactDistance = 350f;

        private static readonly SupportAbilityService Service = new SupportAbilityService();

        /// <summary>Tim, tür, hedef konum. HUD yalnızca kendi timi/düşman uyarısı için filtreler.</summary>
        public static event Action<int, SupportNoticeKind, Vector3> Notice;

        public static float GetCooldownRemaining(int team) => Service.GetCooldownRemaining(team, Time.time);
        public static bool IsReady(int team) => Service.IsReady(team, Time.time);
        public static int GetKills(int team) => Service.GetKills(team, Time.time);
        public static int KillsRequired => Service.KillsRequired;

        /// <summary>Hazırsa helikopteri çağırır (bekleme/öldürme sayacı sıfırlanır). Yalnızca otoritede.</summary>
        public static bool TryCall(int team, PlayerId callerId, Vector3 target)
        {
            if (!GameContext.HasAuthority || float.IsNaN(target.x) || float.IsNaN(target.z))
                return false;
            if (!Service.TryActivate(team, Time.time))
                return false;

            SupportHelicopter.Spawn(team, callerId, target);
            Raise(team, SupportNoticeKind.Incoming, target);
            return true;
        }

        /// <summary>YZ komutan çağrısı: yakın temas varsa ve hazırsa.</summary>
        public static bool TryAiCall(int team, Combatant commander, Vector3 contact)
        {
            if (commander == null || !commander.IsAlive || !Service.IsReady(team, Time.time))
                return false;

            var d = commander.transform.position - contact;
            d.y = 0f;
            var dist = d.magnitude;
            if (dist < AiMinContactDistance || dist > AiMaxContactDistance)
                return false;

            return TryCall(team, commander.Id, contact);
        }

        internal static void Finished(int team, bool shotDown, Vector3 position)
        {
            Service.EndActive(team);
            Raise(team, shotDown ? SupportNoticeKind.ShotDown : SupportNoticeKind.Departing, position);
        }

        internal static void Raise(int team, SupportNoticeKind kind, Vector3 position)
        {
            try { Notice?.Invoke(team, kind, position); }
            catch (Exception e) { Debug.LogException(e); }
        }

        internal static void OnKill(PlayerDiedEvent e)
        {
            if (!e.KillerId.IsValid || e.KillerId == e.VictimId)
                return;
            if (!CombatantRegistry.TryGet(e.KillerId, out var killer) || killer == null)
                return;
            if (!CombatantRegistry.TryGet(e.VictimId, out var victim) || victim == null || victim.Team == killer.Team)
                return;
            Service.RegisterKill(killer.Team, Time.time);
        }

        private const int DummyTeam = 99;
        private static readonly System.Collections.Generic.List<int> TeamsSeen = new(32);

        internal static void PollReadyNotices()
        {
            var all = CombatantRegistry.All;
            var now = Time.time;
            TeamsSeen.Clear();
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !c.IsAlive || c.Team < 0 || c.Team == DummyTeam || TeamsSeen.Contains(c.Team))
                    continue;
                TeamsSeen.Add(c.Team);
                if (Service.ConsumeReadyNotice(c.Team, now))
                    Raise(c.Team, SupportNoticeKind.Ready, c.transform.position);
            }
        }

        public static void ResetAll() => Service.Reset();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ResetAll();
            Notice = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => SupportAbilityRunner.EnsureExists();
    }

    /// <summary>Olay veriyolu aboneliği + hazır bildirimi için hafif yürütücü (sahnede tek, kalıcı değil).</summary>
    public sealed class SupportAbilityRunner : MonoBehaviour
    {
        private static SupportAbilityRunner _instance;
        private IEventBus _bus;
        private Action<PlayerDiedEvent> _onDied;
        private float _lastTime;
        private float _nextPoll;

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("[SupportAbility]");
            _instance = go.AddComponent<SupportAbilityRunner>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            _onDied = SupportAbilitySystem.OnKill;
        }

        private void Update()
        {
            var now = Time.time;
            if (now < _lastTime) SupportAbilitySystem.ResetAll(); // sahne/maç yeniden başladı
            _lastTime = now;

            IEventBus bus = null;
            try { bus = CombatContext.EventBus; } catch (Exception) { }
            if (!ReferenceEquals(bus, _bus))
            {
                _bus?.Unsubscribe(_onDied);
                _bus = bus;
                _bus?.Subscribe(_onDied);
            }

            if (now >= _nextPoll)
            {
                _nextPoll = now + 1f;
                try { SupportAbilitySystem.PollReadyNotices(); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
        }

        private void OnDestroy()
        {
            _bus?.Unsubscribe(_onDied);
            if (_instance == this) _instance = null;
        }
    }
}
