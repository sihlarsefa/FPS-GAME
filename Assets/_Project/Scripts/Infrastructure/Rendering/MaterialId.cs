namespace Project.Infrastructure.Rendering
{
    /// <summary>Paylaşılan malzeme kimlikleri. Sıra GameArtLibrary dizisinin indeksidir — SADECE sona ekleyin.</summary>
    public enum MaterialId
    {
        White = 0, Black, Red, Yellow, Blue, Green, Orange, Gray,
        // Arazi / doğa
        Grass, DryGrass, Dirt, Mud, Rock, RockDark, Sand, Snow, Gravel, Asphalt, Water, Foliage, FoliageDark, PineNeedles, Bark, DeadWood,
        // Yapı
        Concrete, ConcreteDark, Plaster, PlasterWarm, Stone, StoneDark, Brick, RoofTile, RoofMetal, Wood, WoodDark, Glass,
        MetalPanel, MetalDark, Rust, Hesco, Sandbag, CamoNet, TentCanvas, Hay, TurkishFlag, MosqueDome, Carpet,
        // Araç
        VehicleOlive, VehicleTan, VehicleDark, Tire, HeliOlive, Windshield, RotorBlade,
        // Silah
        GunMetal, GunPolymer, GunWood, GunTan,
        // Karakter
        CamoWoodland, CamoMountain, CamoDesert, CamoUrban, Skin, SkinDark, Gear, Boots, Beret, ArmbandBlue, ArmbandRed, ArmbandYellow, ArmbandGreen,
        // Efekt / işaret
        ZoneWall, Tracer, MuzzleFlash, Smoke, Fire, Blood, Spark, BulletHole, LootHighlight, AllyMarker, EnemyMarker, LandingZone, Parachute
    }
}
