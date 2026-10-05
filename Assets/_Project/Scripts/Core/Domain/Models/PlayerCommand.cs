namespace Project.Core.Domain
{
    /// <summary>
    /// Bir simülasyon karesindeki oyuncu niyeti. Çevrimdışı modda doğrudan yerel simülasyona,
    /// online modda sunucuya gönderilir (sunucu-otoriteli model). Küçük ve serileştirilebilir tutulur.
    /// </summary>
    public readonly struct PlayerCommand
    {
        public uint Tick { get; }
        public float MoveForward { get; }
        public float MoveRight { get; }
        public float Yaw { get; }
        public float Pitch { get; }
        public PlayerButtons Buttons { get; }
        public sbyte SelectSlot { get; }
        public sbyte CycleWeapon { get; }

        public PlayerCommand(uint tick, float moveForward, float moveRight, float yaw, float pitch,
            PlayerButtons buttons, sbyte selectSlot, sbyte cycleWeapon)
        {
            Tick = tick;
            MoveForward = Clamp(moveForward);
            MoveRight = Clamp(moveRight);
            Yaw = yaw;
            Pitch = pitch;
            Buttons = buttons;
            SelectSlot = selectSlot;
            CycleWeapon = cycleWeapon;
        }

        public bool Has(PlayerButtons button) => (Buttons & button) == button;

        public MovementInputState ToMovement() => new(
            MoveForward,
            MoveRight,
            Has(PlayerButtons.Sprint),
            Has(PlayerButtons.Jump),
            Has(PlayerButtons.Crouch),
            Has(PlayerButtons.CrouchToggle),
            Has(PlayerButtons.ProneToggle),
            Has(PlayerButtons.LeanLeft),
            Has(PlayerButtons.LeanRight));

        public CombatInputState ToCombat() => new(
            Has(PlayerButtons.Fire),
            Has(PlayerButtons.FirePressed),
            Has(PlayerButtons.Reload),
            Has(PlayerButtons.Aim),
            Has(PlayerButtons.Interact),
            SelectSlot,
            CycleWeapon,
            Has(PlayerButtons.ToggleFireMode),
            Has(PlayerButtons.Heal),
            Has(PlayerButtons.Boost),
            Has(PlayerButtons.ThrowGrenade),
            Has(PlayerButtons.ThrowSmoke),
            Has(PlayerButtons.Holster));

        public static PlayerCommand From(uint tick, MovementInputState movement, CombatInputState combat, float yaw, float pitch)
        {
            var buttons = PlayerButtons.None;
            if (movement.Sprint) buttons |= PlayerButtons.Sprint;
            if (movement.Jump) buttons |= PlayerButtons.Jump;
            if (movement.Crouch) buttons |= PlayerButtons.Crouch;
            if (movement.CrouchToggle) buttons |= PlayerButtons.CrouchToggle;
            if (movement.ProneToggle) buttons |= PlayerButtons.ProneToggle;
            if (movement.LeanLeft) buttons |= PlayerButtons.LeanLeft;
            if (movement.LeanRight) buttons |= PlayerButtons.LeanRight;
            if (combat.Fire) buttons |= PlayerButtons.Fire;
            if (combat.FirePressed) buttons |= PlayerButtons.FirePressed;
            if (combat.Aim) buttons |= PlayerButtons.Aim;
            if (combat.Reload) buttons |= PlayerButtons.Reload;
            if (combat.Interact) buttons |= PlayerButtons.Interact;
            if (combat.Heal) buttons |= PlayerButtons.Heal;
            if (combat.Boost) buttons |= PlayerButtons.Boost;
            if (combat.ThrowGrenade) buttons |= PlayerButtons.ThrowGrenade;
            if (combat.ThrowSmoke) buttons |= PlayerButtons.ThrowSmoke;
            if (combat.ToggleFireMode) buttons |= PlayerButtons.ToggleFireMode;
            if (combat.Holster) buttons |= PlayerButtons.Holster;

            return new PlayerCommand(tick, movement.Forward, movement.Right, yaw, pitch, buttons,
                (sbyte)combat.SelectSlot, (sbyte)combat.CycleWeapon);
        }

        private static float Clamp(float v) => v < -1f ? -1f : v > 1f ? 1f : v;
    }
}
