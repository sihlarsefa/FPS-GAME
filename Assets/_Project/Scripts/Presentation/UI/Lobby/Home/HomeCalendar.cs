using System;

namespace Project.Presentation.UI.Lobby.Home
{
    /// <summary>Sezon penceresi: baslangic, bitis, gecen oran, kalan sure.</summary>
    public readonly struct HomeSeasonWindow
    {
        public readonly int Number;
        public readonly DateTime StartUtc;
        public readonly DateTime EndUtc;

        public HomeSeasonWindow(int number, DateTime start, DateTime end)
        {
            Number = number; StartUtc = start; EndUtc = end;
        }

        public TimeSpan Remaining(DateTime utc) => utc >= EndUtc ? TimeSpan.Zero : EndUtc - utc;

        /// <summary>Gecen oran 0..1.</summary>
        public float Elapsed01(DateTime utc)
        {
            var total = (EndUtc - StartUtc).TotalSeconds;
            if (total <= 0.0) return 1f;
            var v = (utc - StartUtc).TotalSeconds / total;
            return (float)Math.Max(0.0, Math.Min(1.0, v));
        }
    }

    /// <summary>Haftalik etkinlik: aktif ise bitisine, degilse baslangicina geri sayim.</summary>
    public readonly struct HomeEventWindow
    {
        public readonly string Id;
        public readonly string Title;
        public readonly DateTime StartUtc;
        public readonly DateTime EndUtc;
        public readonly bool Active;

        public HomeEventWindow(string id, string title, DateTime start, DateTime end, bool active)
        {
            Id = id; Title = title; StartUtc = start; EndUtc = end; Active = active;
        }

        public DateTime Target => Active ? EndUtc : StartUtc;
        public TimeSpan Remaining(DateTime utc) => utc >= Target ? TimeSpan.Zero : Target - utc;
    }

    /// <summary>
    /// Sunucusuz takvim kurallari: sezon sabit bir capadan 56 gunluk dilimlerle doner, hafta sonu etkinligi
    /// Cuma 18:00 UTC - Pazartesi 00:00 UTC arasidir. Gercek sunucu takvimi gelince bu sinif yerine
    /// ayni imzali bir saglayici baglanir (ENTEGRASYON noktasi: HomeInfoWidgets.CalendarNow).
    /// </summary>
    public static class HomeCalendar
    {
        public const int SeasonLengthDays = 56;
        public static readonly DateTime SeasonAnchorUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        public const int WeekendStartHourUtc = 18;

        public static HomeSeasonWindow SeasonAt(DateTime utc)
        {
            var days = (utc - SeasonAnchorUtc).TotalDays;
            var idx = (int)Math.Floor(days / SeasonLengthDays);
            if (idx < 0) idx = 0;
            var start = SeasonAnchorUtc.AddDays(idx * (double)SeasonLengthDays);
            return new HomeSeasonWindow(idx + 1, start, start.AddDays(SeasonLengthDays));
        }

        /// <summary>Verilen ana gore hafta sonu etkinligi: aktifse o, degilse siradaki Cuma.</summary>
        public static HomeEventWindow WeekendEventAt(DateTime utc)
        {
            // Bu haftanin Cuma 18:00'i (Pazartesi baslangicli hafta degil, ayin gunune gore geri git).
            var daysSinceFriday = ((int)utc.DayOfWeek - (int)DayOfWeek.Friday + 7) % 7;
            var friday = utc.Date.AddDays(-daysSinceFriday).AddHours(WeekendStartHourUtc);
            if (utc < friday && daysSinceFriday == 0)
                friday = friday.AddDays(-7); // Cuma ama 18:00'den once: gecen haftanin penceresi kontrolu
            var end = friday.AddDays(2).AddHours(24 - WeekendStartHourUtc); // Pazartesi 00:00
            if (utc >= friday && utc < end)
                return new HomeEventWindow("weekend_xp", "HAFTA SONU ÇİFTE XP", friday, end, true);

            var next = utc < friday ? friday : friday.AddDays(7);
            return new HomeEventWindow("weekend_xp", "HAFTA SONU ÇİFTE XP", next, next.AddDays(2).AddHours(24 - WeekendStartHourUtc), false);
        }

        /// <summary>
        /// Kalan sureyi kisa yazar: >=1 gun "3G 04S", >=1 saat "04S 12D", digeri "12D 05SN". Negatif sifira sabitlenir.
        /// </summary>
        public static string FormatRemaining(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            var totalSeconds = (long)Math.Floor(t.TotalSeconds);
            var d = totalSeconds / 86400;
            var h = totalSeconds % 86400 / 3600;
            var m = totalSeconds % 3600 / 60;
            var s = totalSeconds % 60;
            if (d > 0) return d + "G " + h.ToString("00") + "S";
            if (h > 0) return h.ToString("00") + "S " + m.ToString("00") + "D";
            return m.ToString("00") + "D " + s.ToString("00") + "SN";
        }

        /// <summary>Son 1 saatte geri sayim kirmiziya doner (aciliyet).</summary>
        public static bool IsUrgent(TimeSpan t) => t > TimeSpan.Zero && t < TimeSpan.FromHours(1);
    }
}
