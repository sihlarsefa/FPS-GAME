using Project.Core.Domain;

namespace Project.Core.Interfaces
{
    public interface IPlayerMotor
    {
        Stance CurrentStance { get; }
        bool IsGrounded { get; }
        bool IsSprinting { get; }

        /// <summary>0..1 arası (koşu hızına göre normalize) yatay hız.</summary>
        float SpeedNormalized { get; }

        /// <summary>-1 sol, +1 sağ eğilme.</summary>
        float Lean { get; }

        /// <summary>İyileşme/boost gibi etkilerden gelen hız çarpanı.</summary>
        float SpeedMultiplier { get; set; }

        void ApplyMovement(MovementInputState input, float deltaTime);
        void ApplyRotation(float yawDelta);
    }
}
