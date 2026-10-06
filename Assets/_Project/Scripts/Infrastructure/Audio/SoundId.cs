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
        HelicopterRotor, VehicleEngine, Wind, Ambience, DistantBattle, MenuMusic, RadioBeep, RadioChatter, Death, Heartbeat, VehicleDoor,
        // Ses kalitesi geçişi (sona eklendi)
        ShotDistantMid, ShotDistantFar, FootstepGrass, FootstepConcrete, FootstepWood, FootstepMetal, ClothRustle, ShellCasing,
        ReloadPistol, ReloadRifle, ReloadLmg, ReloadShotgun, ReloadSniper,
        // Ses katmanları C11 (sona eklendi)
        ShotMech, ShotThump, ShotTailOutdoor, ShotTailIndoor, ShotTailValley, BulletCrack,
        ShellDropGrass, ShellDropConcrete, ShellDropMetal,
        // Oyun hissi kancaları (sona eklendi)
        BreathHeavy,
        // Silah sesi cilası v2 (sona eklendi)
        ShotSuppressed, MgBeltRattle,
        BulletImpactDirt, BulletImpactWood, BulletImpactFlesh, BulletImpactWater, BulletImpactSnow, BulletImpactFoliage,
        // Adım çeşitliliği (sona eklendi)
        FootstepSnow, FootstepGravel, FootstepMud, WoodCreak, GearJingle,
        // Hava (sona eklendi)
        Thunder
    }
}
