using System;
using System.Globalization;
using System.Text;

namespace Project.Application.Settings
{
    /// <summary>
    /// Ayar profili paylaşım kodu: "HK1-" + onaltılık sayı dizisi + "-" + sağlama toplamı (FNV-1a 32). Değerler 1/1000
    /// çözünürlükte tamsayıya yuvarlanır. Bozuk/değiştirilmiş kod reddedilir.
    /// </summary>
    public static class SettingsShareCode
    {
        public const string Prefix = "HK1";

        public static string Encode(ExtendedSettings s)
        {
            var clean = s.Clone();
            clean.Sanitize();
            var sb = new StringBuilder();
            sb.Append(Prefix).Append('-');
            var pairs = clean.ToPairs();
            for (var i = 0; i < pairs.Count; i++)
            {
                if (i > 0)
                    sb.Append('.');
                sb.Append(((long)Math.Round(pairs[i].Value * 1000.0)).ToString("x", CultureInfo.InvariantCulture));
            }
            var body = sb.ToString();
            return body + "-" + Checksum(body).ToString("x8", CultureInfo.InvariantCulture);
        }

        public static bool TryDecode(string code, out ExtendedSettings result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(code))
                return false;
            code = code.Trim();
            var cut = code.LastIndexOf('-');
            if (cut <= 0 || !code.StartsWith(Prefix + "-", StringComparison.Ordinal))
                return false;
            var body = code.Substring(0, cut);
            if (!uint.TryParse(code.Substring(cut + 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var sum) || sum != Checksum(body))
                return false;
            var parts = body.Substring(Prefix.Length + 1).Split('.');
            var template = new ExtendedSettings().ToPairs();
            if (parts.Length != template.Count)
                return false;
            var values = new float[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                if (!long.TryParse(parts[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var raw))
                    return false;
                values[i] = (float)(raw / 1000.0);
            }
            result = ExtendedSettings.FromReader((key, def) =>
            {
                for (var i = 0; i < template.Count; i++)
                    if (template[i].Key == key)
                        return values[i];
                return def;
            });
            return true;
        }

        private static uint Checksum(string text)
        {
            var h = 2166136261u;
            for (var i = 0; i < text.Length; i++)
            {
                h ^= text[i];
                h *= 16777619u;
            }
            return h;
        }
    }
}
