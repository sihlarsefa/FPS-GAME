namespace Project.Core.Domain
{
    /// <summary>10 kişilik timdeki görev dağılımı (başlangıç teçhizatını belirler).</summary>
    public enum TeamRole
    {
        Leader = 0,          // Tim Komutanı
        Rifleman = 1,        // Piyade
        Marksman = 2,        // Keskin Nişancı
        MachineGunner = 3,   // Makineli Tüfekçi
        Medic = 4,           // Sıhhiyeci
        Radioman = 5,        // Telsizci (topçu desteği)
        Grenadier = 6        // Bombacı / İstihkam
    }
}
