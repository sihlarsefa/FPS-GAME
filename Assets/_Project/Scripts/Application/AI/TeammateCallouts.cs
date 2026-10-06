using Project.Application.Dialogue;
using Project.Core.Domain;

namespace Project.Application.AI
{
    /// <summary>Takım arkadaşı sesli bildirim türü; Türkçe diyalog kategorisine eşlenir.</summary>
    public enum CalloutKind
    {
        None = 0,
        Contact = 1,
        MagEmpty = 2,
        MagLow = 3,
        Reloading = 4,
        Advancing = 5,
        Holding = 6,
        Clear = 7
    }

    /// <summary>
    /// Bildirim kararı (saf mantık): temas yönü (saat), şarjör durumu ve ilerleme. Üretilen kategori
    /// mevcut DialogueCats/DialogueArbiter hattına verilir (kanca: BotController -> DialogueDirector).
    /// </summary>
    public static class TeammateCallouts
    {
        public const float LowMagFraction = 0.25f;

        /// <summary>Temas bildirimi için saat yönü (1..12) — botun bakış yönüne göre.</summary>
        public static int ContactClock(float botYawDegrees, Float3 bot, Float3 enemy)
        {
            var d = enemy - bot;
            // Unity yaw: Z ileri, X sağ -> bearing = atan2(x, z).
            var bearing = (float)(System.Math.Atan2(d.X, d.Z) * 180.0 / System.Math.PI);
            var rel = TurkishNumbers.RelativeAngle(botYawDegrees, bearing);
            return TurkishNumbers.ClockHour(rel);
        }

        /// <summary>Şarjör durumundan bildirim: boş, azalıyor ya da yok.</summary>
        public static CalloutKind MagStatus(int roundsInMag, int magSize, int reserveMags)
        {
            if (magSize <= 0) return CalloutKind.None;
            if (roundsInMag <= 0) return CalloutKind.MagEmpty;
            if (reserveMags >= 0 && (float)roundsInMag / magSize <= LowMagFraction) return CalloutKind.MagLow;
            return CalloutKind.None;
        }

        /// <summary>İlerleme: lider yönünde hareket başlayınca "ilerliyoruz", durunca "mevzideyiz", alan temizse "temiz".</summary>
        public static CalloutKind Progress(bool wasMoving, bool isMoving, bool enemyKnown, float secondsSinceContact)
        {
            if (enemyKnown) return CalloutKind.None;
            if (secondsSinceContact > 6f && secondsSinceContact < 12f && !isMoving) return CalloutKind.Clear;
            if (!wasMoving && isMoving) return CalloutKind.Advancing;
            if (wasMoving && !isMoving) return CalloutKind.Holding;
            return CalloutKind.None;
        }

        /// <summary>Kategori adı (DialogueCats); None -> null.</summary>
        public static string Category(CalloutKind kind)
        {
            switch (kind)
            {
                case CalloutKind.Contact: return DialogueCats.ContactFull;
                case CalloutKind.MagEmpty: return DialogueCats.MagEmpty;
                case CalloutKind.MagLow: return DialogueCats.ReloadLast;
                case CalloutKind.Reloading: return DialogueCats.Reload;
                case CalloutKind.Advancing: return DialogueCats.Advance;
                case CalloutKind.Holding: return DialogueCats.Holding;
                case CalloutKind.Clear: return DialogueCats.Clear;
                default: return null;
            }
        }
    }
}
