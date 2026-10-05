namespace Project.Infrastructure.Audio
{
    /// <summary>Prosedürel olarak üretilen ses kimlikleri. SADECE sona ekleyin.</summary>
    public enum SoundId
    {
        None = 0,
        ShotPistol, ShotSmg, ShotRifle556, ShotRifle762, ShotDmr, ShotSniper, ShotShotgun, ShotMachineGun,
        DryFire, ReloadMagOut, ReloadMagIn, ReloadBolt, ShellInsert, FireModeSwitch, WeaponEquip,
        Footstep, Jump, Land, HitMarker, KillConfirm, Headshot, HitFlesh, HitHelmet, HitArmor,
        BulletImpact, BulletImpactMetal, BulletWhiz, Punch, Explosion, ArtilleryWhistle, GrenadePin, GrenadeBounce, SmokeHiss,
        Bandage, Drink, Pickup, UiClick, UiHover, UiConfirm, ZoneWarning, ZoneDamage,
        HelicopterRotor, VehicleEngine, Wind, Ambience, DistantBattle, MenuMusic, RadioBeep, RadioChatter, Death, Heartbeat, VehicleDoor
    }
}
