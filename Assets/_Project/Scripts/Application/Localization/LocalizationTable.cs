using System.Collections.Generic;
using System.Text;

namespace Project.Application.Localization
{
    /// <summary>Dil kodları ve düz {"anahtar":"metin"} JSON ayrıştırıcısı (saf C#).</summary>
    public sealed class LocalizationTable
    {
        public static readonly string[] Languages = { "tr", "en", "de", "az", "ar" };
        public static readonly string[] LanguageNames = { "Türkçe", "English", "Deutsch", "Azərbaycanca", "العربية" };
        public const string DefaultLanguage = "tr";

        private readonly Dictionary<string, string> _map = new();

        public int Count => _map.Count;

        public static string NormalizeLanguage(string code)
        {
            if (!string.IsNullOrEmpty(code))
                foreach (var l in Languages)
                    if (l == code.ToLowerInvariant())
                        return l;
            return DefaultLanguage;
        }

        public static int IndexOf(string code)
        {
            var n = NormalizeLanguage(code);
            for (var i = 0; i < Languages.Length; i++)
                if (Languages[i] == n)
                    return i;
            return 0;
        }

        public bool TryGet(string key, out string value) => _map.TryGetValue(key ?? string.Empty, out value);

        /// <summary>Düz JSON nesnesini ekler (var olan anahtarların üzerine yazar). Bozuk girdide kısmi sonuç kalır, false döner.</summary>
        public bool Merge(string json)
        {
            if (string.IsNullOrEmpty(json))
                return false;
            var i = 0;
            Skip(json, ref i);
            if (i >= json.Length || json[i] != '{')
                return false;
            i++;
            while (true)
            {
                Skip(json, ref i);
                if (i >= json.Length)
                    return false;
                if (json[i] == '}')
                    return true;
                if (json[i] == ',') { i++; continue; }
                if (!ReadString(json, ref i, out var key))
                    return false;
                Skip(json, ref i);
                if (i >= json.Length || json[i] != ':')
                    return false;
                i++;
                Skip(json, ref i);
                if (i < json.Length && json[i] == '"')
                {
                    if (!ReadString(json, ref i, out var val))
                        return false;
                    _map[key] = val;
                }
                else
                {
                    // Dize olmayan değer: sayıyı/literali atla.
                    while (i < json.Length && json[i] != ',' && json[i] != '}')
                        i++;
                }
            }
        }

        private static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i]))
                i++;
        }

        private static bool ReadString(string s, ref int i, out string result)
        {
            result = null;
            if (i >= s.Length || s[i] != '"')
                return false;
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                var c = s[i++];
                if (c == '"') { result = sb.ToString(); return true; }
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length)
                    return false;
                var e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 > s.Length || !int.TryParse(s.Substring(i, 4), System.Globalization.NumberStyles.HexNumber, null, out var code))
                            return false;
                        sb.Append((char)code);
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
            return false;
        }
    }
}
