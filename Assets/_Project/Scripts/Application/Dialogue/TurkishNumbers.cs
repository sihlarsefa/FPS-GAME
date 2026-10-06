using System;
using System.Collections.Generic;

namespace Project.Application.Dialogue
{
    /// <summary>Türkçe sayı/yön yardımcıları: mesafe okunuşu, klip parça kimlikleri, saat yönü.</summary>
    public static class TurkishNumbers
    {
        private static readonly string[] Ones = { "", "bir", "iki", "üç", "dört", "beş", "altı", "yedi", "sekiz", "dokuz" };
        private static readonly string[] Tens = { "", "on", "yirmi", "otuz", "kırk", "elli", "altmış", "yetmiş", "seksen", "doksan" };

        /// <summary>0..9999 arası okunuş: 100 = "yüz", 1000 = "bin", 250 = "iki yüz elli".</summary>
        public static string ToWords(int n)
        {
            if (n <= 0)
                return "sıfır";
            if (n > 9999)
                n = 9999;

            var parts = new List<string>(6);
            AppendWords(n, parts, false);
            return string.Join(" ", parts);
        }

        /// <summary>Sayıyı klip parça kimliklerine böler: 350 -> num_3, num_100, num_50.</summary>
        public static void ToPartIds(int n, List<string> ids)
        {
            if (ids == null)
                return;
            if (n <= 0)
                return;
            if (n > 9999)
                n = 9999;

            var parts = new List<string>(6);
            AppendWords(n, parts, true);
            for (var i = 0; i < parts.Count; i++)
                ids.Add(parts[i]);
        }

        private static void AppendWords(int n, List<string> output, bool ids)
        {
            if (n >= 1000)
            {
                var th = n / 1000;
                if (th > 1)
                    output.Add(ids ? "num_" + th : Ones[th]);
                output.Add(ids ? "num_1000" : "bin");
                n %= 1000;
            }

            if (n >= 100)
            {
                var h = n / 100;
                if (h > 1)
                    output.Add(ids ? "num_" + h : Ones[h]);
                output.Add(ids ? "num_100" : "yüz");
                n %= 100;
            }

            if (n >= 10)
            {
                var t = n / 10;
                output.Add(ids ? "num_" + (t * 10) : Tens[t]);
                n %= 10;
            }

            if (n > 0)
                output.Add(ids ? "num_" + n : Ones[n]);
        }

        /// <summary>Konuşma için mesafe yuvarlama: yakın 5, orta 10, uzak 25, çok uzak 50/100 m.</summary>
        public static int RoundDistance(float meters)
        {
            if (float.IsNaN(meters) || meters < 5f)
                return 5;
            if (meters >= 2000f)
                return 2000;

            int step;
            if (meters < 50f) step = 5;
            else if (meters < 150f) step = 10;
            else if (meters < 500f) step = 25;
            else if (meters < 1000f) step = 50;
            else step = 100;

            return Math.Max(step, (int)Math.Round(meters / step) * step);
        }

        /// <summary>Göreceli yön (derece, -180..180; sağ pozitif) -> saat yönü 1..12 (ön = 12, sağ = 3).</summary>
        public static int ClockHour(float relativeDegrees)
        {
            if (float.IsNaN(relativeDegrees))
                return 12;
            var d = relativeDegrees % 360f;
            if (d < 0f)
                d += 360f;
            var hour = (int)Math.Round(d / 30f) % 12;
            return hour == 0 ? 12 : hour;
        }

        /// <summary>Bakış (yaw) ve hedef yönü (bearing) dereceleri -> göreceli açı -180..180.</summary>
        public static float RelativeAngle(float yawDegrees, float bearingDegrees)
        {
            var d = (bearingDegrees - yawDegrees) % 360f;
            if (d > 180f) d -= 360f;
            if (d < -180f) d += 360f;
            return d;
        }
    }
}
