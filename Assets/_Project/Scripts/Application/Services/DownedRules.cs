namespace Project.Application.Services
{
    /// <summary>
    /// Yaralı (DBNO) durumunun savaş/araç/teçhizat etkileşimlerindeki saf kuralları (Unity'siz, test edilebilir).
    /// Infrastructure/Presentation katmanları aynı kararı buradan alır; böylece yolcu, sürücü, İHA, topçu ve takım
    /// elenmesi için tek bir doğruluk kaynağı olur.
    /// </summary>
    public static class DownedRules
    {
        /// <summary>Telsiz/teçhizat (İHA, topçu, emir) yalnızca sağ ve yaralı olmayan askerce kullanılır.</summary>
        public static bool CanUseEquipment(bool alive, bool downed) => alive && !downed;

        /// <summary>Araca (sürücü/yolcu) yalnızca sağ ve yaralı olmayan asker binebilir.</summary>
        public static bool CanBoardVehicle(bool alive, bool downed) => alive && !downed;

        /// <summary>Araçtaki sürücü yaralı düşerse güvenle indirilmelidir (sürüş/taret kullanılamaz).</summary>
        public static bool ShouldEjectFromVehicle(bool alive, bool downed) => alive && downed;

        /// <summary>
        /// Mürettebat yolcusu ölü, yaralı ya da koltuğu kaybolmuşsa indirilir (yaralıyı müttefik ancak yerdeyken kaldırabilir).
        /// </summary>
        public static bool ShouldUnboardPassenger(bool dead, bool downed, bool slotLost) => dead || downed || slotLost;

        /// <summary>Takımda yaralı olmayan sağ üye kalmadı ve yaralı üye var: takım elendi, yaralılar ölür.</summary>
        public static bool IsTeamWiped(int healthyAlive, int downedAlive) => healthyAlive <= 0 && downedAlive > 0;
    }
}
