using Project.Core.Domain;

namespace Project.Core.Interfaces
{
    public interface IWeaponRuntime : IWeapon
    {
        WeaponDefinitionData Definition { get; }
        bool IsReloading { get; }
        float ReloadProgress { get; }
        FireMode CurrentFireMode { get; }

        /// <summary>Envanterdeki yedek mermi (AmmoSource yoksa 0).</summary>
        int ReserveAmmo { get; }

        bool TryBeginReload();
        void CancelReload();
        void CycleFireMode();

        /// <summary>
        /// Tetik durumunu işler; bu çağrıda bir atış üretildiyse true döner (mod/burst/soğuma kurallarını uygular).
        /// </summary>
        bool TryTrigger(bool triggerHeld, bool triggerPressedThisFrame);

        /// <summary>Anlık sapma konisi yarı açısı (derece).</summary>
        float GetSpreadAngle(bool aiming, float moveSpeedNormalized, bool grounded, Stance stance);

        void Tick(float deltaTime);
    }
}
