namespace Project.Infrastructure.Content
{
    /// <summary>
    /// ContentOverrides / Design/Assets CSV kimlik sabitleri.
    /// C3-1 CSV <c>models.csv</c> ilk kolon değerleriyle birebir aynı tutulmalı.
    /// </summary>
    public static class ContentIds
    {
        public const string Soldier = "asker";
        public const string Kirpi = "kirpi";
        public const string Cobra = "cobra";
        public const string Helicopter = "t70";

        public const string WeaponMuzzle = "Muzzle";
        public const string WeaponGripR = "Grip_R";
        public const string WeaponGripL = "Grip_L";
        public const string WeaponMagazine = "Magazine";
        public const string WeaponBolt = "Bolt";
        public const string WeaponSight = "Sight";

        // Vegetation species (VegetationOverrideEntry.speciesId)
        public const string VegPine = "pine";
        public const string VegOak = "oak";
        public const string VegBush = "bush";
        public const string VegDead = "dead";
        public const string VegPineSlim = "pine_slim";
        public const string VegPoplar = "poplar";
        public const string VegDwarfOak = "dwarf_oak";
        public const string VegShrubRound = "shrub_round";
        public const string VegShrubSparse = "shrub_sparse";

        public const string RockSmall = "small";
        public const string RockMedium = "medium";
        public const string RockLarge = "large";

        public const string SkyDayClear = "day_clear";
        public const string SkyCloudy = "cloudy";
        public const string SkySunset = "sunset";
    }
}
