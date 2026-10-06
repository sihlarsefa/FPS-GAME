using System;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Transport;
using Project.Infrastructure.Vehicles;
using UnityEngine;
using Rules = Project.Infrastructure.Vehicles.HelicopterFlightRules;

namespace Project.Presentation.Player
{
    /// <summary>
    /// Uçurulabilir T-70 (FlyableHelicopter) için oyuncu tarafı: bin/in, pilot kontrolleri, kapı nişancısı, koltuk değiştirme.
    /// Mod olarak Mode.Driving yeniden kullanılır; <c>_heli != null</c> helikopter dalını seçer.
    /// </summary>
    public sealed partial class PlayerController
    {
        private FlyableHelicopter _heli;
        private Rules.SeatKind _heliSeat;
        private int _heliIndex;
        private LookInputState _heliLook;

        public FlyableHelicopter Helicopter => _heli;
        public bool IsPiloting => _mode == Mode.Driving && _heli != null && _heliSeat == Rules.SeatKind.Pilot;

        /// <summary>Pilotken fare uçuşu yönetir (kamera serbest bakışı uygulanmaz); bakış delta'sı saklanır.</summary>
        private bool HeliConsumesLook(LookInputState look)
        {
            if (IsPiloting)
            {
                _heliLook = look;
                return true;
            }

            _heliLook = LookInputState.Zero;
            return false;
        }

        private Transform HeliViewPoint()
            => _heli != null ? _heli.GetViewPoint(_heliSeat, _heliIndex) : null;

        internal bool TryEnterHeli(FlyableHelicopter heli)
        {
            if (_mode != Mode.OnFoot || heli == null || _combatant == null || heli.Health <= 0f)
                return false;

            Rules.SeatKind kind;
            int index;
            try
            {
                kind = heli.TryEnterAny(_combatant, out index);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                return false;
            }

            if (kind == Rules.SeatKind.None)
                return false;

            _weapons.OnLeaveFoot();
            _heli = heli;
            _heliSeat = kind;
            _heliIndex = index;
            _mode = Mode.Driving;

            SetPhysicalBody(false);
            transform.SetParent(heli.transform, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            _lastPosition = transform.position;

            if (_combatant != null)
                _combatant.Stance = Stance.Crouching;

            _weapons.SetViewModelVisible(false);
            AlignSeat(HeliViewPoint());
            PlaySound2D(SoundId.VehicleDoor, 0.7f);
            Notify(HeliSeatHint(kind), 3.5f, false);
            return true;
        }

        private static string HeliSeatHint(Rules.SeatKind kind)
        {
            switch (kind)
            {
                case Rules.SeatKind.Pilot:
                    return "T-70 pilotu — [W/S] kolektif, [A/D] yaw, fare eğim/yatış, [B] otomatik asılı kalma, [1/2/3] koltuk";
                case Rules.SeatKind.Gunner:
                    return "Kapı nişancısı — [Sol Tık] ateş, [Şarjör] mermi, [1] pilot koltuğu";
                default:
                    return "T-70 yolcusu — [F] in (alçakta ve yavaşken)";
            }
        }

        private void SimulateHeli(PlayerCommand command, float dt)
        {
            var heli = _heli;
            if (heli == null || !heli.isActiveAndEnabled || heli.Health <= 0f || transform.parent == null)
            {
                ExitHeli(true);
                return;
            }

            if (_combatant != null && DownedRules.ShouldEjectFromVehicle(_combatant.IsAlive, _combatant.IsDowned))
            {
                ExitHeli(true);
                Notify("Yaralandın — helikopterden indirildin", 2f, false);
                return;
            }

            if (GameplayInputActive)
                TickHeliSeatSwitch(heli);

            try
            {
                switch (_heliSeat)
                {
                    case Rules.SeatKind.Pilot:
                        TickHeliPilot(heli, command, dt);
                        break;
                    case Rules.SeatKind.Gunner:
                        TickHeliGunner(heli);
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            SeatedTick(command, dt);
        }

        private void TickHeliPilot(FlyableHelicopter heli, PlayerCommand command, float dt)
        {
            var active = GameplayInputActive;
            var collective = active ? command.MoveForward : 0f;
            var yaw = active ? command.MoveRight : 0f;
            var look = active ? _heliLook : LookInputState.Zero;
            heli.SetPilotInput(collective, yaw, look.PitchDelta, look.YawDelta, dt);

            if (active && Infrastructure.Input.InputBindings.Pressed(BindAction.FireMode))
            {
                heli.ToggleHoverAssist();
                Notify(heli.HoverAssist ? "Otomatik asılı kalma: AÇIK" : "Otomatik asılı kalma: KAPALI (manuel kolektif)", 1.8f, false);
            }
        }

        private void TickHeliGunner(FlyableHelicopter heli)
        {
            var turret = heli.GetTurret(_heliIndex);
            if (turret == null)
                return;
            turret.PlayerGunner = true;
            TickKirpiTurret(turret,
                keyboardReload: GameplayInputActive && Infrastructure.Input.InputBindings.Pressed(BindAction.Reload));
        }

        /// <summary>1 = pilot, 2/3 = sol/sağ kapı nişancısı. Koltuk doluysa değişmez.</summary>
        private void TickHeliSeatSwitch(FlyableHelicopter heli)
        {
            var input = Infrastructure.Input.InputBindings.Map;
            if (input == null || _combatant == null)
                return;

            if (Infrastructure.Input.InputBindings.Pressed(BindAction.Slot1) && _heliSeat != Rules.SeatKind.Pilot)
                SwitchHeliSeat(heli, Rules.SeatKind.Pilot, 0);
            else if (Infrastructure.Input.InputBindings.Pressed(BindAction.Slot2) && !(_heliSeat == Rules.SeatKind.Gunner && _heliIndex == 0))
                SwitchHeliSeat(heli, Rules.SeatKind.Gunner, 0);
            else if (Infrastructure.Input.InputBindings.Pressed(BindAction.Slot3) && !(_heliSeat == Rules.SeatKind.Gunner && _heliIndex == 1))
                SwitchHeliSeat(heli, Rules.SeatKind.Gunner, 1);
        }

        private void SwitchHeliSeat(FlyableHelicopter heli, Rules.SeatKind kind, int index)
        {
            var oldKind = _heliSeat;
            var oldIndex = _heliIndex;
            var oldTurret = heli.GetTurret(oldIndex);
            heli.Leave(_combatant);
            if (oldTurret != null)
                oldTurret.PlayerGunner = false;

            var ok = kind == Rules.SeatKind.Pilot
                ? heli.TryEnterPilot(_combatant)
                : heli.TryEnterGunner(_combatant, index);

            if (!ok)
            {
                // Eski koltuğa geri dön.
                switch (oldKind)
                {
                    case Rules.SeatKind.Pilot: heli.TryEnterPilot(_combatant); break;
                    case Rules.SeatKind.Gunner: heli.TryEnterGunner(_combatant, oldIndex); break;
                    default: heli.TryEnterPassenger(_combatant, out oldIndex); break;
                }

                _heliSeat = oldKind;
                _heliIndex = oldIndex;
                Notify("Koltuk dolu", 1.2f, false);
                return;
            }

            _heliSeat = kind;
            _heliIndex = index;
            AlignSeat(HeliViewPoint());
            Notify(HeliSeatHint(kind), 3f, false);
        }

        /// <summary>Helikopterden iner. force=false: yalnızca alçakta ve yavaşken.</summary>
        internal void ExitHeli(bool force)
        {
            if (_mode != Mode.Driving || _heli == null)
                return;

            var heli = _heli;
            var alive = heli != null;
            if (!force && alive && !heli.CanExitNow)
            {
                Notify("İnmek için alçal ve yavaşla", 1.6f);
                return;
            }

            var exitPoint = alive ? heli.GetExitPoint(_combatant) : SnapToGround(transform.position);
            if (alive)
            {
                try
                {
                    var turret = heli.GetTurret(_heliIndex);
                    if (turret != null)
                        turret.PlayerGunner = false;
                    heli.Leave(_combatant);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }

            _heli = null;
            _heliSeat = Rules.SeatKind.None;
            _heliLook = LookInputState.Zero;

            var yaw = Yaw;
            transform.SetParent(null, true);
            _mode = Mode.OnFoot;
            TeleportMotor(exitPoint, yaw);
            SetPhysicalBody(true);
            if (_combatant != null)
                _combatant.IsTargetable = true;

            _fallGraceUntil = Time.time + VehicleExitFallGraceSeconds;
            _weapons.SetViewModelVisible(true);
            PlaySound2D(SoundId.VehicleDoor, 0.7f);
        }

        /// <summary>Ölüm/sahne kapanışı: koltuğu boşalt, hareketi bozmadan bağı kopar.</summary>
        private void LeaveHeliImmediate()
        {
            if (_heli == null)
                return;
            try
            {
                var turret = _heli.GetTurret(_heliIndex);
                if (turret != null)
                    turret.PlayerGunner = false;
                if (_combatant != null)
                    _heli.Leave(_combatant);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            _heli = null;
            _heliSeat = Rules.SeatKind.None;
        }
    }
}
