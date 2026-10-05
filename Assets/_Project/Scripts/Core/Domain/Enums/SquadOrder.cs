namespace Project.Core.Domain
{
    /// <summary>Tim komutanının tim üyelerine verdiği emir.</summary>
    public enum SquadOrder
    {
        Follow = 0,        // Beni takip et
        HoldPosition = 1,  // Mevzi al / bekle
        Attack = 2,        // İşaretli noktaya taarruz
        Regroup = 3        // Toplan
    }
}
