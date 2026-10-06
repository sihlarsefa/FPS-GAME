using System;
using Project.Infrastructure;
using Project.Infrastructure.Network;
using Project.Presentation.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Fare orta tuşu / Y girdisi: orta tuşa kısa tık = ping; orta tuş ya da Y basılı tutma = radyal komut çarkı
    /// (bırakınca seçilir, Esc iptal). Çark açıkken yalnızca çevrimdışı modda zaman yavaşlar (0.25x); bakış kilitlenir.
    /// </summary>
    public sealed class CommandWheelController : IDisposable
    {
        public const float HoldSeconds = 0.28f;
        public const float SlowScale = 0.25f;
        private const float DeltaToUnit = 0.012f;

        private readonly PlayerController _owner;
        private readonly Action<CommandWheelItem> _execute;

        private bool _middleHeld;
        private float _middlePressedAt;
        private bool _open;
        private bool _openedByKey;
        private Vector2 _offset;
        private bool _slowed;
        private bool _inputLocked;

        public CommandWheelController(PlayerController owner, Action<CommandWheelItem> execute)
        {
            _owner = owner;
            _execute = execute;
        }

        public bool IsOpen => _open;

        /// <summary>Oyun girdisi açıkken her karede çağrılır.</summary>
        public void Tick()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null && mouse == null)
                return;

            var now = Time.unscaledTime;

            if (mouse != null)
            {
                if (mouse.middleButton.wasPressedThisFrame && !_open)
                {
                    _middleHeld = true;
                    _middlePressedAt = now;
                }
                else if (_middleHeld && mouse.middleButton.isPressed && !_open && now - _middlePressedAt >= HoldSeconds)
                {
                    Open(false);
                }

                if (mouse.middleButton.wasReleasedThisFrame || (_middleHeld && !mouse.middleButton.isPressed))
                {
                    var wasHeld = _middleHeld;
                    _middleHeld = false;
                    if (_open && !_openedByKey)
                        Confirm();
                    else if (wasHeld && !_open)
                        PingController.TryPlace(_owner);
                }
            }

            if (keyboard != null)
            {
                if (keyboard.yKey.wasPressedThisFrame && !_open)
                    Open(true);
                else if (_open && _openedByKey && (keyboard.yKey.wasReleasedThisFrame || !keyboard.yKey.isPressed))
                    Confirm();
            }

            if (_open)
            {
                var pad = Gamepad.current;
                if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame))
                {
                    // Esc önce çarkı kapatır; duraklatma menüsü aynı kareden sonraki basışta açılır.
                    OverlayState.ConsumeEscape();
                    Close();
                    return;
                }

                if (mouse != null)
                {
                    _offset += mouse.delta.ReadValue() * DeltaToUnit * new Vector2(1f, 1f);
                    if (_offset.magnitude > 1f)
                        _offset = _offset.normalized;
                }

                var view = CommandWheelView.EnsureExists();
                view.SetSelection(CommandWheelMath.SliceAt(_offset, CommandWheelMath.ItemCount, CommandWheelView.DeadZone), _offset);
            }
        }

        private void Open(bool byKey)
        {
            _open = true;
            OverlayState.CommandWheelOpen = true;
            _openedByKey = byKey;
            _middleHeld = false;
            _offset = Vector2.zero;
            CommandWheelView.EnsureExists().Open();

            var input = _owner.Input;
            if (input != null && input.GameplayEnabled)
            {
                input.GameplayEnabled = false;
                _inputLocked = true;
            }

            if (GameContext.Network is OfflineNetworkSession && Time.timeScale > 0.9f)
            {
                Time.timeScale = SlowScale;
                _slowed = true;
            }
        }

        private void Confirm()
        {
            var index = CommandWheelMath.SliceAt(_offset, CommandWheelMath.ItemCount, CommandWheelView.DeadZone);
            Close();
            if (index < 0)
                return;

            try
            {
                _execute?.Invoke((CommandWheelItem)index);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>Çarkı seçim yapmadan kapatır; kilitleri ve zamanı geri verir.</summary>
        public void Close()
        {
            if (_slowed)
            {
                if (Mathf.Approximately(Time.timeScale, SlowScale))
                    Time.timeScale = 1f;
                _slowed = false;
            }

            if (_inputLocked)
            {
                var input = _owner != null ? _owner.Input : null;
                if (input != null && _owner.InputEnabled && !_owner.IsDead)
                    input.GameplayEnabled = true;
                _inputLocked = false;
            }

            _open = false;
            OverlayState.CommandWheelOpen = false;
            _middleHeld = false;
            var view = CommandWheelView.Current;
            if (view != null)
                view.Close();
        }

        /// <summary>Çark açıksa güvenle kapatır (ölüm/duraklatma/harita).</summary>
        public void ForceClose()
        {
            if (_open || _slowed || _inputLocked)
                Close();
        }

        public void Dispose() => ForceClose();
    }
}
