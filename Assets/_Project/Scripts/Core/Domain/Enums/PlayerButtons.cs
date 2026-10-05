using System;

namespace Project.Core.Domain
{
    /// <summary>Ağ üzerinden gönderilebilen kompakt buton bit maskesi.</summary>
    [Flags]
    public enum PlayerButtons : uint
    {
        None = 0,
        Fire = 1 << 0,
        FirePressed = 1 << 1,
        Aim = 1 << 2,
        Jump = 1 << 3,
        Sprint = 1 << 4,
        Crouch = 1 << 5,
        CrouchToggle = 1 << 6,
        ProneToggle = 1 << 7,
        LeanLeft = 1 << 8,
        LeanRight = 1 << 9,
        Reload = 1 << 10,
        Interact = 1 << 11,
        Heal = 1 << 12,
        Boost = 1 << 13,
        ThrowGrenade = 1 << 14,
        ThrowSmoke = 1 << 15,
        ToggleFireMode = 1 << 16,
        Holster = 1 << 17
    }
}
