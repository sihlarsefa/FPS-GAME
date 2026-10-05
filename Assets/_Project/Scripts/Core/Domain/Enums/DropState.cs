namespace Project.Core.Domain
{
    /// <summary>İntikal akışındaki durum. Freefall/Parachute ileride HALO atlayışı için ayrılmıştır.</summary>
    public enum DropState
    {
        InTransport = 0,
        Freefall = 1,
        Parachute = 2,
        Landed = 3
    }
}
