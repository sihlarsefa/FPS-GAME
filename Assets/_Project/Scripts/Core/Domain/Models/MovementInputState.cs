namespace Project.Core.Domain
{
    public readonly struct MovementInputState
    {
        public float Forward { get; }
        public float Right { get; }
        public bool Sprint { get; }
        public bool Jump { get; }

        /// <summary>Eğilme tuşu basılı tutuluyor (Sol Ctrl).</summary>
        public bool Crouch { get; }

        /// <summary>Bu karede eğilme aç/kapa (C).</summary>
        public bool CrouchToggle { get; }

        /// <summary>Bu karede yüzüstü aç/kapa (Z).</summary>
        public bool ProneToggle { get; }

        public bool LeanLeft { get; }
        public bool LeanRight { get; }

        public MovementInputState(float forward, float right, bool sprint, bool jump, bool crouch)
            : this(forward, right, sprint, jump, crouch, false, false, false, false)
        {
        }

        public MovementInputState(float forward, float right, bool sprint, bool jump, bool crouch,
            bool crouchToggle, bool proneToggle, bool leanLeft, bool leanRight)
        {
            Forward = forward;
            Right = right;
            Sprint = sprint;
            Jump = jump;
            Crouch = crouch;
            CrouchToggle = crouchToggle;
            ProneToggle = proneToggle;
            LeanLeft = leanLeft;
            LeanRight = leanRight;
        }

        public bool HasMovement => Forward * Forward + Right * Right > 0.01f;

        public static MovementInputState Zero => new(0f, 0f, false, false, false);
    }
}
