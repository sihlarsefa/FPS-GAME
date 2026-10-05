using System.Text;
using Project.Application.Services;
using Project.Core.Domain;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Menü ekranlarının Türkçe metin yardımcıları: Türkçe büyük harf (i → İ, ı → I), süre/sayı/yüzde biçimleri,
    /// zorluk ve intikal adları. Kültür ayarından bağımsızdır (sunucu/oyuncu derlemelerinde aynı sonucu verir).
    /// </summary>
    public static class MenuText
    {
        /// <summary>Zorluk seçenekleri (Er / Uzman / Komando → Easy / Normal / Hard).</summary>
        public static readonly string[] DifficultyNames = { "Er", "Uzman", "Komando" };

        /// <summary>İntikal seçenekleri (T-70 helikopteri / Kirpi zırhlı araç).</summary>
        public static readonly string[] InsertionNames = { "Helikopter (T-70)", "Zırhlı Araç (Kirpi)" };

        /// <summary>Grafik kalitesi seçenekleri (0..3).</summary>
        public static readonly string[] QualityNames = { "Düşük", "Orta", "Yüksek", "Ultra" };

        private static readonly StringBuilder Builder = new StringBuilder(64);

        /// <summary>Türkçe kurallarıyla büyük harfe çevirir ("Kartal Timi" → "KARTAL TİMİ"). null → boş.</summary>
        public static string ToUpperTr(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            Builder.Clear();
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                switch (c)
                {
                    case 'i':
                        Builder.Append('İ');
                        break;
                    case 'ı':
                        Builder.Append('I');
                        break;
                    default:
                        Builder.Append(char.ToUpperInvariant(c));
                        break;
                }
            }

            return Builder.ToString();
        }

        /// <summary>Süreyi "dd:ss" ya da bir saati aşarsa "s:dd:ss" biçiminde yazar. Geçersiz değer → "00:00".</summary>
        public static string FormatDuration(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
                seconds = 0f;

            var total = (int)seconds;
            var hours = total / 3600;
            var minutes = total / 60 % 60;
            var secs = total % 60;
            if (hours > 0)
                return hours + ":" + minutes.ToString("00") + ":" + secs.ToString("00");
            return minutes.ToString("00") + ":" + secs.ToString("00");
        }

        /// <summary>Binlik ayraçlı tam sayı (Türkçe: nokta) — 12500 → "12.500".</summary>
        public static string FormatThousands(long value)
        {
            var negative = value < 0;
            var abs = negative ? -value : value;
            var digits = abs.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (digits.Length <= 3)
                return negative ? "-" + digits : digits;

            Builder.Clear();
            if (negative)
                Builder.Append('-');
            var lead = digits.Length % 3;
            for (var i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (i - lead) % 3 == 0)
                    Builder.Append('.');
                Builder.Append(digits[i]);
            }

            return Builder.ToString();
        }

        /// <summary>0..1 oranı Türkçe yüzde olarak yazar: 0.425 → "%42,5" (ondalık gerekirse), 0.4 → "%40".</summary>
        public static string FormatPercent(float fraction01, bool oneDecimal = false)
        {
            if (float.IsNaN(fraction01) || float.IsInfinity(fraction01))
                fraction01 = 0f;

            var percent = fraction01 * 100f;
            if (!oneDecimal)
                return "%" + ((int)System.Math.Round(percent)).ToString(System.Globalization.CultureInfo.InvariantCulture);

            var tenths = (int)System.Math.Round(percent * 10f);
            var whole = tenths / 10;
            var frac = System.Math.Abs(tenths % 10);
            return frac == 0 ? "%" + whole : "%" + whole + "," + frac;
        }

        /// <summary>Hasar miktarını tam sayı olarak binlik ayraçla yazar.</summary>
        public static string FormatDamage(float damage)
        {
            if (float.IsNaN(damage) || float.IsInfinity(damage) || damage < 0f)
                damage = 0f;
            return FormatThousands((long)System.Math.Round(damage));
        }

        /// <summary>Zorluk adı (Er / Uzman / Komando).</summary>
        public static string DifficultyName(BotDifficulty difficulty)
        {
            var index = (int)difficulty;
            return index >= 0 && index < DifficultyNames.Length ? DifficultyNames[index] : DifficultyNames[1];
        }

        /// <summary>Zorluk açıklaması (kurulum ekranı).</summary>
        public static string DifficultyDescription(BotDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BotDifficulty.Easy:
                    return "Düşman timleri geç tepki verir, isabetleri düşüktür. Harekâta yeni başlayanlar için.";
                case BotDifficulty.Hard:
                    return "Hızlı tepki, yüksek isabet ve saldırgan taarruz. Komando timleri hata affetmez.";
                default:
                    return "Dengeli tatbikat: eğitimli düşman timleri siper alır, kanattan dolanır.";
            }
        }

        /// <summary>İntikal yöntemi adı.</summary>
        public static string InsertionName(InsertionMethod method)
        {
            var index = (int)method;
            return index >= 0 && index < InsertionNames.Length ? InsertionNames[index] : InsertionNames[0];
        }

        /// <summary>İntikal açıklaması (kurulum ekranı).</summary>
        public static string InsertionDescription(InsertionMethod method)
        {
            return method == InsertionMethod.ArmoredVehicle
                ? "Kirpi zırhlı aracıyla karadan intikal. Daha korunaklı ama yavaş; tim indirme noktasında araçtan iner."
                : "T-70 helikopteriyle hızlı hava intikali. Tim iniş bölgesine helikopterden atlar.";
        }

        /// <summary>"4 Tim = 40 Asker" biçiminde tim özeti.</summary>
        public static string TeamSummary(int teamCount)
        {
            if (teamCount < 1)
                teamCount = 1;
            return teamCount + " Tim = " + teamCount * SettingsService.TeamSize + " Asker";
        }

        /// <summary>Kalite seviyesi adı (0 Düşük … 3 Ultra).</summary>
        public static string QualityName(int level)
        {
            if (level < 0)
                level = 0;
            if (level >= QualityNames.Length)
                level = QualityNames.Length - 1;
            return QualityNames[level];
        }

        /// <summary>Sıralama metni: "#2 / 4" (toplam bilinmiyorsa "#2").</summary>
        public static string FormatPlacement(int placement, int total)
        {
            if (placement <= 0)
                return "—";
            return total > 0 ? "#" + placement + " / " + total : "#" + placement;
        }
    }
}
