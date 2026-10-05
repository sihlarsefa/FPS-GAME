namespace Project.Application.Catalogs
{
    /// <summary>
    /// Kalıcı silah kimlikleri — Türk yapımı silahlar (ağ ve kayıt için asla yeniden adlandırmayın).
    /// </summary>
    public static class WeaponIds
    {
        public const string Sar9 = "pistol_sar9";       // Sarsılmaz SAR 9 — standart tabanca (9mm)
        public const string Tp9 = "pistol_tp9";         // Canik TP9 — tabanca (9mm)
        public const string Sar109 = "smg_sar109t";     // Sarsılmaz SAR 109T — hafif makineli (9mm)
        public const string Mpt55 = "ar_mpt55";         // MKE MPT-55 — piyade tüfeği (5.56)
        public const string Mpt76 = "ar_mpt76";         // MKE MPT-76 — milli piyade tüfeği (7.62)
        public const string G3 = "ar_g3a7";             // MKE G3A7 — piyade tüfeği (7.62)
        public const string Knt76 = "dmr_knt76";        // MKE KNT-76 — nişancı tüfeği (7.62)
        public const string Jng90 = "sr_jng90";         // MKE JNG-90 Bora-12 — keskin nişancı (7.62, sürgülü)
        public const string Pmt76 = "lmg_pmt76";        // MKE PMT-76 — makineli tüfek (7.62)
        public const string Escort = "sg_escort";       // Escort — pompalı (12 ga)
    }

    /// <summary>Silah dışı hasar kaynakları.</summary>
    public static class DamageSourceIds
    {
        public const string Zone = "zone";
        public const string Fall = "fall";
        public const string FragGrenade = "grenade_frag";
        public const string Fists = "melee_fists";
        public const string Artillery = "artillery";
        public const string Vehicle = "vehicle";
    }
}
