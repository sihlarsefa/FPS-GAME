namespace Project.Core.Domain
{
    public readonly struct KillFeedEntry
    {
        public string KillerName { get; }
        public string VictimName { get; }
        public string WeaponName { get; }
        public bool IsHeadshot { get; }
        public bool KillerIsLocal { get; }
        public bool VictimIsLocal { get; }

        /// <summary>Öldüren yerel oyuncunun timinden mi (HUD renklendirme).</summary>
        public bool KillerIsAlly { get; }
        public bool VictimIsAlly { get; }

        public KillFeedEntry(string killerName, string victimName, string weaponName, bool isHeadshot, bool killerIsLocal, bool victimIsLocal)
            : this(killerName, victimName, weaponName, isHeadshot, killerIsLocal, victimIsLocal, killerIsLocal, victimIsLocal)
        {
        }

        public KillFeedEntry(string killerName, string victimName, string weaponName, bool isHeadshot, bool killerIsLocal,
            bool victimIsLocal, bool killerIsAlly, bool victimIsAlly)
        {
            KillerName = killerName;
            VictimName = victimName;
            WeaponName = weaponName;
            IsHeadshot = isHeadshot;
            KillerIsLocal = killerIsLocal;
            VictimIsLocal = victimIsLocal;
            KillerIsAlly = killerIsAlly;
            VictimIsAlly = victimIsAlly;
        }
    }
}
