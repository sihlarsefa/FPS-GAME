using Project.Core.Domain;
using Project.Core.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Infrastructure.Input
{
    /// <summary>
    /// Input System tabanlı klavye/fare okuyucu. Bir karede birden çok kez okunabilir (kenar tetikleri kare bazlıdır).
    /// GameplayEnabled false iken oyun girdileri sıfır döner (menü/harita açıkken). UI girdisi her zaman okunur.
    /// Tuşlar: WASD, Fare, Space zıpla, Shift koş, Ctrl (basılı) / C (aç-kapa) eğil, Z yüzüstü, Q/E eğilme,
    /// Sol tık ateş, Sağ tık nişan, R şarjör, F etkileşim, 1-4 silah, tekerlek silah değiştir, B ateş modu,
    /// H iyileş, J boost, G el bombası, T sis bombası, X silahı indir, Tab envanter, M harita, Esc duraklat.
    /// </summary>
    public sealed class UnityInputReader : MonoBehaviour, IMovementInputReader, ILookInputReader, ICombatInputReader
    {
        private const float MouseDeltaScale = 1f;

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
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
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
            return new MovementInputState(
                move.y,
                move.x,
                KeyHeld(Key.LeftShift),
                KeyPressed(Key.Space),
                KeyHeld(Key.LeftCtrl),
                KeyPressed(Key.C),
                KeyPressed(Key.Z),
                KeyHeld(Key.Q),
                KeyHeld(Key.E));
        }

        public LookInputState ReadLook()
        {
            if (!GameplayEnabled || _lookAction == null)
                return LookInputState.Zero;

            var look = _lookAction.ReadValue<Vector2>() * MouseDeltaScale;
            return new LookInputState(look.y, look.x);
        }

        public CombatInputState ReadCombat()
        {
            if (!GameplayEnabled || _fireAction == null)
                return CombatInputState.Zero;

            var slot = -1;
            if (KeyPressed(Key.Digit1)) slot = 0;
            else if (KeyPressed(Key.Digit2)) slot = 1;
            else if (KeyPressed(Key.Digit3)) slot = 2;
            else if (KeyPressed(Key.Digit4)) slot = 3;

            var scroll = _scrollAction.ReadValue<Vector2>().y;
            var cycle = scroll > 0.01f ? -1 : scroll < -0.01f ? 1 : 0;

            return new CombatInputState(
                _fireAction.IsPressed(),
                _fireAction.WasPressedThisFrame(),
                KeyPressed(Key.R),
                _aimAction.IsPressed(),
                KeyPressed(Key.F),
                slot,
                cycle,
                KeyPressed(Key.B),
                KeyPressed(Key.H),
                KeyPressed(Key.J),
                KeyPressed(Key.G),
                KeyPressed(Key.T),
                KeyPressed(Key.X));
        }

        public UiInputState ReadUi()
        {
            return new UiInputState(
                KeyPressed(Key.Escape),
                KeyPressed(Key.Tab) || KeyPressed(Key.I),
                KeyPressed(Key.M),
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
