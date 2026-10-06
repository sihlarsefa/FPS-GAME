namespace Project.Core.Domain
{
    public readonly struct CombatInputState
    {
        /// <summary>Tetik basılı tutuluyor.</summary>
        public bool Fire { get; }

        /// <summary>Tetik bu karede basıldı (tek atış için).</summary>
        public bool FirePressed { get; }

        public bool Reload { get; }
        public bool Aim { get; }

        /// <summary>Nişan tuşu bu karede basıldı (kenar; kareler arası tıklar kaçmaz).</summary>
        public bool AimPressed { get; }
        public bool Interact { get; }

        /// <summary>0..3 seçilen yuva, -1 seçim yok.</summary>
        public int SelectSlot { get; }

        /// <summary>Fare tekerleği: +1 sonraki, -1 önceki silah.</summary>
        public int CycleWeapon { get; }

        public bool ToggleFireMode { get; }
        public bool Heal { get; }
        public bool Boost { get; }
        public bool ThrowGrenade { get; }
        public bool ThrowSmoke { get; }
        public bool Holster { get; }

        public CombatInputState(bool fire, bool reload, bool aim, bool interact)
            : this(fire, fire, reload, aim, interact, -1, 0, false, false, false, false, false, false)
        {
        }

        public CombatInputState(bool fire, bool firePressed, bool reload, bool aim, bool interact,
            int selectSlot, int cycleWeapon, bool toggleFireMode, bool heal, bool boost,
            bool throwGrenade, bool throwSmoke, bool holster)
            : this(fire, firePressed, reload, aim, interact, selectSlot, cycleWeapon, toggleFireMode, heal, boost,
                throwGrenade, throwSmoke, holster, false)
        {
        }

        public CombatInputState(bool fire, bool firePressed, bool reload, bool aim, bool interact,
            int selectSlot, int cycleWeapon, bool toggleFireMode, bool heal, bool boost,
            bool throwGrenade, bool throwSmoke, bool holster, bool aimPressed)
        {
            AimPressed = aimPressed;
            Fire = fire;
            FirePressed = firePressed;
            Reload = reload;
            Aim = aim;
            Interact = interact;
            SelectSlot = selectSlot;
            CycleWeapon = cycleWeapon;
            ToggleFireMode = toggleFireMode;
            Heal = heal;
            Boost = boost;
            ThrowGrenade = throwGrenade;
            ThrowSmoke = throwSmoke;
            Holster = holster;
        }

        public static CombatInputState Zero => new(false, false, false, false);
    }
}
