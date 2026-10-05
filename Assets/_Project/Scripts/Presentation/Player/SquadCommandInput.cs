using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Presentation.Player
{
    /// <summary>
    /// Tim emirleri ve topçu çağrısı (klavye doğrudan Input System ile okunur):
    /// F1 Beni takip et, F2 Mevzi al, F3 Taarruz (nişan noktası), F4 Toplan — yalnızca tim komutanıyken.
    /// V: topçu desteği (nişan noktası ≤ 600 m, yoksa harita işareti) — komutan ya da timde yaşayan telsizci varken.
    /// </summary>
    public sealed class SquadCommandInput : IDisposable
    {
        public const float OrderMaxDistance = 400f;
        private const float AccessRefreshInterval = 0.5f;
        private const float DangerCloseDistance = 40f;

        private readonly PlayerController _owner;
        private readonly List<Combatant> _teamBuffer = new(16);

        private SquadOrder _lastIssued = SquadOrder.Follow;
        private float _nextAccessRefresh;
        private bool _isCommander;
        private bool _radiomanAlive;

        public SquadCommandInput(PlayerController owner)
        {
            _owner = owner;
        }

        /// <summary>Oyuncu timinin komutanı mı (komuta zinciri yoksa Leader rolüne bakılır)?</summary>
        public bool IsCommander
        {
            get
            {
                RefreshAccess(false);
                return _isCommander;
            }
        }

        /// <summary>Topçu isteyebilir mi?</summary>
        public bool HasArtilleryAccess
        {
            get
            {
                RefreshAccess(false);
                return _isCommander || _radiomanAlive;
            }
        }

        /// <summary>Timin geçerli emri (SquadOrderService'ten; yoksa son verilen).</summary>
        public SquadOrder CurrentOrder
        {
            get
            {
                var orders = _owner.Orders;
                var combatant = _owner.Combatant;
                if (orders != null && combatant != null && orders.TryGetOrder(combatant.Team, out var order, out _))
                    return order;

                return _lastIssued;
            }
        }

        /// <summary>Topçu bekleme süresi (sn).</summary>
        public float ArtilleryCooldown
        {
            get
            {
                var artillery = _owner.Artillery;
                var combatant = _owner.Combatant;
                if (artillery == null || combatant == null)
                    return 0f;

                try
                {
                    return Mathf.Max(0f, artillery.GetCooldownRemaining(combatant.Team));
                }
                catch (Exception)
                {
                    return 0f;
                }
            }
        }

        public void Tick(float dt)
        {
            if (!_owner.InputEnabled || _owner.IsDead)
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.f1Key.wasPressedThisFrame)
                IssueOrder(SquadOrder.Follow);
            else if (keyboard.f2Key.wasPressedThisFrame)
                IssueOrder(SquadOrder.HoldPosition);
            else if (keyboard.f3Key.wasPressedThisFrame)
                IssueOrder(SquadOrder.Attack);
            else if (keyboard.f4Key.wasPressedThisFrame)
                IssueOrder(SquadOrder.Regroup);

            if (keyboard.vKey.wasPressedThisFrame)
                CallArtillery();
        }

        // ================================================================ emirler
        private void IssueOrder(SquadOrder order)
        {
            var combatant = _owner.Combatant;
            if (combatant == null)
                return;

            RefreshAccess(true);
            if (!_isCommander)
            {
                _owner.Notify("Emir vermek için tim komutanı olmalısınız", 1.8f);
                return;
            }

            var orders = _owner.Orders;
            if (orders == null)
            {
                _owner.Notify("Tim emir sistemi yok", 1.5f);
                return;
            }

            var target = _owner.transform.position;
            if (order == SquadOrder.Attack)
            {
                if (_owner.TryGetAimPoint(OrderMaxDistance, out var aimPoint))
                    target = aimPoint;
                else if (_owner.MapMarker.HasValue)
                    target = _owner.MapMarker.Value;
                else
                {
                    _owner.Notify("Taarruz hedefi görülmüyor", 1.5f);
                    return;
                }
            }

            if (!GameContext.HasAuthority)
                return;

            try
            {
                orders.Issue(combatant.Team, order, ToFloat3(target));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return;
            }

            _lastIssued = order;
            PlayerController.PlaySound2D(SoundId.RadioBeep, 0.6f);
            _owner.Notify(OrderText(order), 2f);
        }

        public static string OrderText(SquadOrder order)
        {
            switch (order)
            {
                case SquadOrder.HoldPosition:
                    return "Emir: Mevzi alın, bekleyin!";
                case SquadOrder.Attack:
                    return "Emir: Hedefe taarruz!";
                case SquadOrder.Regroup:
                    return "Emir: Toplanın!";
                default:
                    return "Emir: Beni takip edin!";
            }
        }

        public static string OrderShortName(SquadOrder order)
        {
            switch (order)
            {
                case SquadOrder.HoldPosition:
                    return "Mevzi Al";
                case SquadOrder.Attack:
                    return "Taarruz";
                case SquadOrder.Regroup:
                    return "Toplan";
                default:
                    return "Takip";
            }
        }

        // ================================================================ topçu
        private void CallArtillery()
        {
            var combatant = _owner.Combatant;
            if (combatant == null)
                return;

            RefreshAccess(true);
            if (!_isCommander && !_radiomanAlive)
            {
                _owner.Notify("Telsizci yok — topçu desteği istenemez", 2f);
                return;
            }

            var artillery = _owner.Artillery;
            if (artillery == null)
            {
                _owner.Notify("Topçu desteği kullanılamıyor", 1.5f);
                return;
            }

            var cooldown = ArtilleryCooldown;
            if (cooldown > 0f)
            {
                _owner.Notify(CooldownText(cooldown), 1.5f);
                return;
            }

            Vector3 target;
            if (_owner.TryGetAimPoint(PlayerController.ArtilleryMaxRange, out var aimPoint))
            {
                target = aimPoint;
            }
            else if (_owner.MapMarker.HasValue)
            {
                target = _owner.MapMarker.Value;
            }
            else
            {
                _owner.Notify("Hedef menzil dışında (en fazla 600 m)", 1.8f);
                return;
            }

            if (!GameContext.HasAuthority)
                return;

            bool called;
            try
            {
                called = artillery.TryCall(combatant.Team, combatant.Id, ToFloat3(target));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                called = false;
            }

            if (!called)
            {
                _owner.Notify("Topçu desteği reddedildi", 1.5f);
                return;
            }

            PlayerController.PlaySound2D(SoundId.RadioChatter, 0.7f);
            var distance = Vector3.Distance(_owner.transform.position, target);
            _owner.Notify(distance < DangerCloseDistance
                ? "Topçu ateşi istendi — DİKKAT, yakın atış!"
                : "Topçu ateşi istendi — atışlar yolda", 2.5f);
        }

        private static string CooldownText(float seconds)
        {
            var whole = Mathf.CeilToInt(seconds);
            return "Topçu hazır değil (" + whole + " sn)";
        }

        // ================================================================ yetki
        private void RefreshAccess(bool force)
        {
            if (!force && Time.time < _nextAccessRefresh)
                return;

            _nextAccessRefresh = Time.time + AccessRefreshInterval;
            var combatant = _owner.Combatant;
            if (combatant == null || !combatant.IsAlive)
            {
                _isCommander = false;
                _radiomanAlive = false;
                return;
            }

            var chain = _owner.Chain;
            var commander = combatant.Role == TeamRole.Leader;
            if (chain != null)
            {
                try
                {
                    if (chain.IsRegistered(combatant.Id))
                        commander = chain.IsCommander(combatant.Id);
                }
                catch (Exception)
                {
                    // zincir hazır değil: role göre
                }
            }

            _isCommander = commander;

            _radiomanAlive = false;
            _teamBuffer.Clear();
            CombatantRegistry.GetTeam(combatant.Team, _teamBuffer);
            for (var i = 0; i < _teamBuffer.Count; i++)
            {
                var member = _teamBuffer[i];
                if (member != null && member.IsAlive && member.Role == TeamRole.Radioman)
                {
                    _radiomanAlive = true;
                    break;
                }
            }

            _teamBuffer.Clear();
        }

        private static Float3 ToFloat3(Vector3 v) => new(v.x, v.y, v.z);

        public void Dispose()
        {
            _teamBuffer.Clear();
        }
    }
}
