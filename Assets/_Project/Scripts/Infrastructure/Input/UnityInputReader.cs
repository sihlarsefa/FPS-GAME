using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Infrastructure.Input
{
    /// <summary>
    /// Input System tabanlı klavye/fare okuyucu. Bir karede birden çok kez okunabilir (kenar tetikleri kare bazlıdır).
    /// GameplayEnabled false iken oyun girdileri sıfır döner (menü/harita açıkken). UI girdisi her zaman okunur.
    /// Tuşlar (varsayılan, InputBindings ile değiştirilebilir; gamepad eşlemesi eklidir): WASD, Fare, Space zıpla, Shift koş, Ctrl (basılı) / C (aç-kapa) eğil, Z yüzüstü, Q/E eğilme,
    /// Sol tık ateş, Sağ tık nişan, R şarjör, F etkileşim, 1-4 silah, tekerlek silah değiştir, B ateş modu,
    /// H iyileş, J boost, G el bombası, T sis bombası, X silahı indir, Tab envanter, M harita, Esc duraklat.
    /// </summary>
    public sealed class UnityInputReader : MonoBehaviour, IMovementInputReader, ILookInputReader, ICombatInputReader
    {
        private const float MouseDeltaScale = 1f;
        private const float StickLookPixelsPerSecond = 1100f;

        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _fireAction;
        private InputAction _aimAction;
        private InputAction _scrollAction;

        /// <summary>false iken hareket/bakış/çatışma girdileri yok sayılır.</summary>
        public bool GameplayEnabled { get; set; } = true;

        public Vector2 PointerPosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        private void Awake()
        {
            _moveAction = new InputAction("Move", InputActionType.Value);
            _moveAction.AddBinding("<Gamepad>/leftStick");

            _lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
            _fireAction = new InputAction("Fire", InputActionType.Button, "<Mouse>/leftButton");
            _aimAction = new InputAction("Aim", InputActionType.Button, "<Mouse>/rightButton");
            _scrollAction = new InputAction("Scroll", InputActionType.Value, "<Mouse>/scroll");
        }

        private void OnEnable()
        {
            _moveAction?.Enable();
            _lookAction?.Enable();
            _fireAction?.Enable();
            _aimAction?.Enable();
            _scrollAction?.Enable();
        }

        private void OnDisable()
        {
            _moveAction?.Disable();
            _lookAction?.Disable();
            _fireAction?.Disable();
            _aimAction?.Disable();
            _scrollAction?.Disable();
        }

        private void OnDestroy()
        {
            _moveAction?.Dispose();
            _lookAction?.Dispose();
            _fireAction?.Dispose();
            _aimAction?.Dispose();
            _scrollAction?.Dispose();
        }

        MovementInputState IMovementInputReader.Read() => ReadMovement();
        LookInputState ILookInputReader.Read() => ReadLook();
        CombatInputState ICombatInputReader.Read() => ReadCombat();

        public MovementInputState ReadMovement()
        {
            if (!GameplayEnabled || _moveAction == null)
                return MovementInputState.Zero;

            var move = _moveAction.ReadValue<Vector2>();
            var fwd = (InputBindings.Held(BindAction.MoveForward) ? 1f : 0f) - (InputBindings.Held(BindAction.MoveBack) ? 1f : 0f);
            var side = (InputBindings.Held(BindAction.MoveRight) ? 1f : 0f) - (InputBindings.Held(BindAction.MoveLeft) ? 1f : 0f);
            var v = new Vector2(side + move.x, fwd + move.y);
            if (v.sqrMagnitude > 1f) v.Normalize();
            var pad = Gamepad.current;
            return new MovementInputState(
                v.y,
                v.x,
                InputBindings.Held(BindAction.Sprint) || (pad != null && pad.leftStickButton.isPressed),
                InputBindings.Pressed(BindAction.Jump) || (pad != null && pad.buttonSouth.wasPressedThisFrame),
                InputBindings.Held(BindAction.CrouchHold),
                InputBindings.Pressed(BindAction.CrouchToggle) || (pad != null && pad.buttonEast.wasPressedThisFrame),
                InputBindings.Pressed(BindAction.Prone) || (pad != null && pad.rightStickButton.wasPressedThisFrame),
                InputBindings.Held(BindAction.LeanLeft),
                InputBindings.Held(BindAction.LeanRight));
        }

        public LookInputState ReadLook()
        {
            if (!GameplayEnabled || _lookAction == null)
                return LookInputState.Zero;

            var look = _lookAction.ReadValue<Vector2>() * MouseDeltaScale;
            var pad = Gamepad.current;
            if (pad != null)
            {
                var stick = pad.rightStick.ReadValue();
                if (stick.sqrMagnitude > 0.04f)
                    look += stick * (StickLookPixelsPerSecond * InputBindings.StickSensitivity * Time.unscaledDeltaTime);
            }
            return new LookInputState(look.y, look.x);
        }

        public CombatInputState ReadCombat()
        {
            if (!GameplayEnabled || _fireAction == null)
                return CombatInputState.Zero;

            var pad = Gamepad.current;
            var slot = -1;
            if (InputBindings.Pressed(BindAction.Slot1) || (pad != null && pad.dpad.up.wasPressedThisFrame)) slot = 0;
            else if (InputBindings.Pressed(BindAction.Slot2) || (pad != null && pad.dpad.right.wasPressedThisFrame)) slot = 1;
            else if (InputBindings.Pressed(BindAction.Slot3) || (pad != null && pad.dpad.down.wasPressedThisFrame)) slot = 2;
            else if (InputBindings.Pressed(BindAction.Slot4) || (pad != null && pad.dpad.left.wasPressedThisFrame)) slot = 3;

            var scroll = _scrollAction.ReadValue<Vector2>().y;
            var cycle = scroll > 0.01f ? -1 : scroll < -0.01f ? 1 : 0;

            var padFire = pad != null && pad.rightTrigger.ReadValue() > 0.5f;
            var padFireDown = pad != null && pad.rightTrigger.wasPressedThisFrame;
            var padAim = pad != null && pad.leftTrigger.ReadValue() > 0.5f;
            var aimPressed = _aimAction.WasPressedThisFrame() || (pad != null && pad.leftTrigger.wasPressedThisFrame);

            return new CombatInputState(
                _fireAction.IsPressed() || padFire,
                _fireAction.WasPressedThisFrame() || padFireDown,
                InputBindings.Pressed(BindAction.Reload) || (pad != null && pad.buttonWest.wasPressedThisFrame),
                _aimAction.IsPressed() || padAim,
                InputBindings.Pressed(BindAction.Interact) || (pad != null && pad.buttonNorth.wasPressedThisFrame),
                slot,
                cycle,
                InputBindings.Pressed(BindAction.FireMode),
                InputBindings.Pressed(BindAction.Heal) || (pad != null && pad.leftShoulder.wasPressedThisFrame),
                InputBindings.Pressed(BindAction.Boost),
                InputBindings.Pressed(BindAction.Grenade) || (pad != null && pad.rightShoulder.wasPressedThisFrame),
                InputBindings.Pressed(BindAction.Smoke),
                InputBindings.Pressed(BindAction.Lower),
                aimPressed);
        }

        public UiInputState ReadUi()
        {
            var pad = Gamepad.current;
            return new UiInputState(
                KeyPressed(Key.Escape) || (pad != null && pad.startButton.wasPressedThisFrame),
                InputBindings.Pressed(BindAction.Inventory),
                InputBindings.Pressed(BindAction.Map) || (pad != null && pad.selectButton.wasPressedThisFrame),
                KeyHeld(Key.CapsLock));
        }

        private static bool KeyHeld(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].isPressed;
        }

        private static bool KeyPressed(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].wasPressedThisFrame;
        }
    }
}
