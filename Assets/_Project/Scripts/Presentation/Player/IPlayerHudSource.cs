using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Player;
using UnityEngine;

namespace Project.Presentation.Player
{
    /// <summary>HUD/harita/envanter görünümlerinin yerel oyuncudan okuduğu salt-okunur durum.</summary>
    public interface IPlayerHudSource
    {
        Combatant Combatant { get; }
        InventoryService Inventory { get; }
        WeaponRuntimeService ActiveWeapon { get; }
        bool IsAiming { get; }
        bool IsScoped { get; }
        float ScopeZoom { get; }
        float SpreadAngle { get; }
        string InteractionPrompt { get; }
        DropState DropState { get; }
        ItemUseService ItemUse { get; }
        float Yaw { get; }
        Vector3 Position { get; }
        bool IsDead { get; }
        FirstPersonCameraController CameraController { get; }
        SquadOrder CurrentOrder { get; }
        float ArtilleryCooldown { get; }
        bool IsInVehicle { get; }
    }
}
