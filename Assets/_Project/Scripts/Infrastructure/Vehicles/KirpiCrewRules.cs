namespace Project.Infrastructure.Vehicles
{
    /// <summary>Kirpi mürettebat kararlarının saf mantığı (Unity'siz, test edilebilir).</summary>
    public static class KirpiCrewRules
    {
        public const float MovedKmh = 8f;
        public const float StoppedKmh = 1.5f;
        public const float StopDisembarkSeconds = 2.5f;
        /// <summary>Küçük silah mermisinin zırhlı araca verdiği hasar oranı (silah hasarı × oran).</summary>
        public const float BulletArmorFactor = 0.15f;

        /// <summary>Mermi hasarının araç canına yansıyan kısmı (zırh payı düşülmüş); negatif/NaN → 0.</summary>
        public static float BulletDamageToVehicle(float weaponDamage)
            => weaponDamage > 0f ? weaponDamage * BulletArmorFactor : 0f;

        /// <summary>İlk boş yolcu koltuğu; yoksa -1.</summary>
        public static int FirstFreeSeat(bool[] occupied)
        {
            if (occupied == null)
                return -1;
            for (var i = 0; i < occupied.Length; i++)
                if (!occupied[i])
                    return i;
            return -1;
        }

        /// <summary>Araç hareket ettikten sonra StopDisembarkSeconds boyunca durduysa true.</summary>
        public static bool ShouldDisembarkOnStop(bool hasMoved, float stoppedSeconds)
            => hasMoved && stoppedSeconds >= StopDisembarkSeconds;

        /// <summary>Yeni bir emir geldiğinde (revizyon değişti) yolcular inmeli mi: Takip dışındaki her emir.</summary>
        public static bool ShouldDisembarkOnOrder(bool orderIsFollow) => !orderIsFollow;

        public static string Hud(float speedKmh, float health, float maxHealth, bool gunner, int ammo, bool reloading, string name = "Kirpi")
        {
            var pct = maxHealth > 0f ? (int)(health / maxHealth * 100f) : 0;
            var line = name + "  " + (int)speedKmh + " km/sa  |  Zırh %" + pct;
            if (gunner)
                line += "  |  MG " + (reloading ? "dolduruluyor" : ammo.ToString());
            return line + "  |  [1] Sürücü  [2] Taret";
        }
    }
}
