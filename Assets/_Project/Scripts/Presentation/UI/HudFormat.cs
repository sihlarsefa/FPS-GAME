using System;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// HUD metin biçimleri ve önbellekleri: derece, metre, saniye (ondalık virgüllü), yakınlaştırma, ateş modu,
    /// görev/emir adları ve Türkçe yönelme (-e/-a) eki. Sık güncellenen değerler önbellekten döner (kare başına çöp yok).
    /// </summary>
    public static class HudFormat
    {
        public const string Infinity = "∞";
        public const string Dash = "—";

        private static string[] _degrees;
        private static string[] _meters;
        private static string[] _kilometers;
        private static string[] _tenths;
        private static string[] _seconds;
        private static string[] _zoom;

        /// <summary>"247°" (0..359, önbellekli).</summary>
        public static string Degrees(float degrees)
        {
            var d = Mathf.RoundToInt(Mathf.Repeat(degrees, 360f)) % 360;
            if (_degrees == null)
                _degrees = new string[360];
            return _degrees[d] ??= d + "°";
        }

        /// <summary>"123 m" (0..999 m), "1,2 km" (1..9,9 km), daha uzakta "12 km".</summary>
        public static string Meters(float meters)
        {
            if (float.IsNaN(meters) || meters < 0f)
                meters = 0f;

            var m = Mathf.RoundToInt(meters);
            if (m < 1000)
            {
                if (_meters == null)
                    _meters = new string[1000];
                return _meters[m] ??= m + " m";
            }

            var tenthsKm = Mathf.RoundToInt(meters / 100f);
            if (tenthsKm < 100)
            {
                if (_kilometers == null)
                    _kilometers = new string[100];
                return _kilometers[tenthsKm] ??= (tenthsKm / 10) + "," + (tenthsKm % 10) + " km";
            }

            return Mathf.RoundToInt(meters / 1000f) + " km";
        }

        /// <summary>Saniyeyi tek ondalıkla (Türkçe virgül) verir: "3,4". 0..99,9 önbellekli.</summary>
        public static string Tenths(float value)
        {
            if (float.IsNaN(value) || value < 0f)
                value = 0f;

            var t = Mathf.CeilToInt(value * 10f - 0.001f);
            if (t < 0)
                t = 0;
            if (t >= 1000)
                return Mathf.RoundToInt(value).ToString();

            if (_tenths == null)
                _tenths = new string[1000];
            return _tenths[t] ??= (t / 10) + "," + (t % 10);
        }

        /// <summary>Kalan süre: "3,4 sn" (0..99,9 önbellekli).</summary>
        public static string SecondsLeft(float value)
        {
            if (float.IsNaN(value) || value < 0f)
                value = 0f;

            var t = Mathf.CeilToInt(value * 10f - 0.001f);
            if (t < 0)
                t = 0;
            if (t >= 1000)
                return Mathf.RoundToInt(value) + " sn";

            if (_seconds == null)
                _seconds = new string[1000];
            return _seconds[t] ??= (t / 10) + "," + (t % 10) + " sn";
        }

        /// <summary>Dürbün büyütmesi: "6x", "1,5x".</summary>
        public static string Zoom(float zoom)
        {
            if (float.IsNaN(zoom) || zoom < 1f)
                zoom = 1f;

            var t = Mathf.RoundToInt(zoom * 10f);
            if (t >= 200)
                return Mathf.RoundToInt(zoom) + "x";

            if (_zoom == null)
                _zoom = new string[200];
            return _zoom[t] ??= (t % 10 == 0 ? (t / 10).ToString() : (t / 10) + "," + (t % 10)) + "x";
        }

        /// <summary>Ateş modu kısaltması: TEK / SERİ / OTO.</summary>
        public static string FireMode(FireMode mode)
        {
            switch (mode)
            {
                case Core.Domain.FireMode.Burst: return "SERİ";
                case Core.Domain.FireMode.Auto: return "OTO";
                default: return "TEK";
            }
        }

        /// <summary>Tim görevinin kısa etiketi (tim paneli).</summary>
        public static string RoleShort(TeamRole role)
        {
            switch (role)
            {
                case TeamRole.Leader: return "KMT";
                case TeamRole.Marksman: return "KNS";
                case TeamRole.MachineGunner: return "MAK";
                case TeamRole.Medic: return "SIH";
                case TeamRole.Radioman: return "TEL";
                case TeamRole.Grenadier: return "BMB";
                default: return "PİY";
            }
        }

        /// <summary>Tim görevinin tam adı.</summary>
        public static string RoleName(TeamRole role)
        {
            switch (role)
            {
                case TeamRole.Leader: return "Tim Komutanı";
                case TeamRole.Marksman: return "Keskin Nişancı";
                case TeamRole.MachineGunner: return "Makineli Tüfekçi";
                case TeamRole.Medic: return "Sıhhiyeci";
                case TeamRole.Radioman: return "Telsizci";
                case TeamRole.Grenadier: return "Bombacı";
                default: return "Piyade";
            }
        }

        /// <summary>Emrin kısa adı (tim paneli): TAKİP ET / MEVZİ AL / TAARRUZ / TOPLAN.</summary>
        public static string OrderShort(SquadOrder order)
        {
            switch (order)
            {
                case SquadOrder.HoldPosition: return "MEVZİ AL";
                case SquadOrder.Attack: return "TAARRUZ";
                case SquadOrder.Regroup: return "TOPLAN";
                default: return "TAKİP ET";
            }
        }

        /// <summary>Emir bildirimi: "Tim emri: Beni takip edin".</summary>
        public static string OrderMessage(SquadOrder order)
        {
            switch (order)
            {
                case SquadOrder.HoldPosition: return "Tim emri: Mevzi alın, bulunduğunuz yerde bekleyin";
                case SquadOrder.Attack: return "Tim emri: İşaretli noktaya taarruz";
                case SquadOrder.Regroup: return "Tim emri: Komutanın yanında toplanın";
                default: return "Tim emri: Beni takip edin";
            }
        }

        /// <summary>İntikal aracının adı: T-70 / Kirpi.</summary>
        public static string InsertionVehicle(InsertionMethod method)
        {
            return method == InsertionMethod.ArmoredVehicle ? "Kirpi" : "T-70";
        }

        /// <summary>
        /// Türkçe yönelme hâli eki (kesme işaretiyle): "Demir'e", "Yılmaz'a", "Kaya'ya", "Öztürk'e".
        /// Son ünlüye göre büyük ünlü uyumu; ünlüyle bitiyorsa kaynaştırma "y".
        /// </summary>
        public static string Dative(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name ?? string.Empty;

            var trimmed = name.TrimEnd();
            if (trimmed.Length == 0)
                return name;

            var back = true;
            for (var i = trimmed.Length - 1; i >= 0; i--)
            {
                var c = ToTurkishLower(trimmed[i]);
                if (!IsVowel(c))
                    continue;

                back = c == 'a' || c == 'ı' || c == 'o' || c == 'u' || c == 'â';
                break;
            }

            var endsWithVowel = IsVowel(ToTurkishLower(trimmed[trimmed.Length - 1]));
            var suffix = back ? "a" : "e";
            return trimmed + "'" + (endsWithVowel ? "y" : string.Empty) + suffix;
        }

        /// <summary>Türkçe küçük harf (I → ı, İ → i); diğerleri değişmez kültürle.</summary>
        private static char ToTurkishLower(char c)
        {
            if (c == 'I')
                return 'ı';
            if (c == 'İ')
                return 'i';
            return char.ToLowerInvariant(c);
        }

        private static bool IsVowel(char c)
        {
            switch (c)
            {
                case 'a':
                case 'e':
                case 'ı':
                case 'i':
                case 'o':
                case 'ö':
                case 'u':
                case 'ü':
                case 'â':
                case 'î':
                case 'û':
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Rengi zengin metin etiketi için "#RRGGBBAA" biçiminde verir.</summary>
        public static string Hex(Color color)
        {
            return "#" + ColorUtility.ToHtmlStringRGBA(color);
        }

        /// <summary>Metni zengin metin rengiyle sarar (null güvenli).</summary>
        public static string Colorize(string text, string hexColor)
        {
            return "<color=" + hexColor + ">" + (text ?? string.Empty) + "</color>";
        }

        /// <summary>Açı farkı (−180..180): hedef yön − bakış yönü.</summary>
        public static float DeltaAngle(float from, float to)
        {
            return Mathf.DeltaAngle(from, to);
        }

        /// <summary>Konumdan hedefe dünya yönü (derece, kuzey = +Z = 0, saat yönünde artar).</summary>
        public static float Bearing(Vector3 from, Vector3 to)
        {
            var dx = to.x - from.x;
            var dz = to.z - from.z;
            if (dx * dx + dz * dz < 1e-6f)
                return 0f;
            return Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
        }

        /// <summary>Güvenli string karşılaştırma (sıralı).</summary>
        public static bool Same(string a, string b) => string.Equals(a, b, StringComparison.Ordinal);
    }
}
