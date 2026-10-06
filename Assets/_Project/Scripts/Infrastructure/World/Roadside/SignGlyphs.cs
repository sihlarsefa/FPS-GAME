using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Tabela yazısı için saf bitmap font (5x7 + Türkçe aksan işaretleri: İ Ö Ü Ş Ğ Ç). Font/RenderTexture gerekmez,
    /// platformdan bağımsız ve deterministik; testlenebilir. Çıktı: Color32 dizisi (satır 0 = üst).
    /// </summary>
    public static class SignGlyphs
    {
        public const int GlyphW = 5;
        public const int GlyphH = 7;
        /// <summary>Aksan dahil toplam satır: 2 üst + 7 gövde + 2 alt.</summary>
        public const int TotalRows = 11;
        private const int TopRows = 2;

        private static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
        {
            { 'A', "01110,10001,10001,11111,10001,10001,10001" }, { 'B', "11110,10001,10001,11110,10001,10001,11110" },
            { 'C', "01110,10001,10000,10000,10000,10001,01110" }, { 'D', "11110,10001,10001,10001,10001,10001,11110" },
            { 'E', "11111,10000,10000,11110,10000,10000,11111" }, { 'F', "11111,10000,10000,11110,10000,10000,10000" },
            { 'G', "01110,10001,10000,10111,10001,10001,01111" }, { 'H', "10001,10001,10001,11111,10001,10001,10001" },
            { 'I', "01110,00100,00100,00100,00100,00100,01110" }, { 'J', "00111,00010,00010,00010,00010,10010,01100" },
            { 'K', "10001,10010,10100,11000,10100,10010,10001" }, { 'L', "10000,10000,10000,10000,10000,10000,11111" },
            { 'M', "10001,11011,10101,10101,10001,10001,10001" }, { 'N', "10001,11001,10101,10011,10001,10001,10001" },
            { 'O', "01110,10001,10001,10001,10001,10001,01110" }, { 'P', "11110,10001,10001,11110,10000,10000,10000" },
            { 'Q', "01110,10001,10001,10001,10101,10010,01101" }, { 'R', "11110,10001,10001,11110,10100,10010,10001" },
            { 'S', "01111,10000,10000,01110,00001,00001,11110" }, { 'T', "11111,00100,00100,00100,00100,00100,00100" },
            { 'U', "10001,10001,10001,10001,10001,10001,01110" }, { 'V', "10001,10001,10001,10001,10001,01010,00100" },
            { 'W', "10001,10001,10001,10101,10101,11011,10001" }, { 'X', "10001,10001,01010,00100,01010,10001,10001" },
            { 'Y', "10001,10001,01010,00100,00100,00100,00100" }, { 'Z', "11111,00001,00010,00100,01000,10000,11111" },
            { '0', "01110,10001,10011,10101,11001,10001,01110" }, { '1', "00100,01100,00100,00100,00100,00100,01110" },
            { '2', "01110,10001,00001,00010,00100,01000,11111" }, { '3', "11110,00001,00001,01110,00001,00001,11110" },
            { '4', "00010,00110,01010,10010,11111,00010,00010" }, { '5', "11111,10000,11110,00001,00001,10001,01110" },
            { '6', "00110,01000,10000,11110,10001,10001,01110" }, { '7', "11111,00001,00010,00100,01000,01000,01000" },
            { '8', "01110,10001,10001,01110,10001,10001,01110" }, { '9', "01110,10001,10001,01111,00001,00010,01100" },
            { '-', "00000,00000,00000,11111,00000,00000,00000" }, { '.', "00000,00000,00000,00000,00000,01100,01100" },
            { '/', "00001,00010,00010,00100,01000,01000,10000" }, { '!', "00100,00100,00100,00100,00100,00000,00100" },
            { ' ', "00000,00000,00000,00000,00000,00000,00000" }
        };

        /// <summary>Türkçe büyük harfe çevirir (i→İ, ı→I dahil).</summary>
        public static char Upper(char c)
        {
            switch (c)
            {
                case 'i': return 'İ';
                case 'ı': return 'I';
                case 'ö': return 'Ö';
                case 'ü': return 'Ü';
                case 'ş': return 'Ş';
                case 'ğ': return 'Ğ';
                case 'ç': return 'Ç';
                default: return char.ToUpperInvariant(c);
            }
        }

        private static char BaseOf(char c, out int accent)
        {
            accent = 0;
            switch (c)
            {
                case 'İ': accent = 1; return 'I';
                case 'Ö': accent = 2; return 'O';
                case 'Ü': accent = 2; return 'U';
                case 'Ğ': accent = 3; return 'G';
                case 'Ş': accent = 4; return 'S';
                case 'Ç': accent = 4; return 'C';
                default: return c;
            }
        }

        public static bool Supports(char c)
        {
            c = Upper(c);
            return Glyphs.ContainsKey(BaseOf(c, out _));
        }

        /// <summary>Glif hücresinde (kolon 0..4, satır 0..TotalRows-1) piksel dolu mu? Desteklenmeyen karakter boş.</summary>
        public static bool IsSet(char c, int col, int row)
        {
            c = Upper(c);
            var b = BaseOf(c, out var accent);
            if (col < 0 || col >= GlyphW || row < 0 || row >= TotalRows || !Glyphs.TryGetValue(b, out var rows))
                return false;

            var body = row - TopRows;
            if (body >= 0 && body < GlyphH)
                return rows[body * (GlyphW + 1) + col] == '1';

            switch (accent)
            {
                case 1: return row == 0 && col == 2;
                case 2: return row == 0 && (col == 1 || col == 3);
                case 3: return (row == 0 && (col == 1 || col == 3)) || (row == 1 && col == 2);
                case 4: return (row == TopRows + GlyphH && col == 2) || (row == TopRows + GlyphH + 1 && col == 1);
                default: return false;
            }
        }

        /// <summary>Metnin piksel genişliği (scale=1): harf başı 6 kolon, sondaki boşluk hariç.</summary>
        public static int TextColumns(string text)
            => string.IsNullOrEmpty(text) ? 0 : text.Length * (GlyphW + 1) - 1;

        /// <summary>Hücreye sığan en büyük tam sayı ölçek (kenar boşluğu margin piksel).</summary>
        public static int FitScale(string text, int w, int h, int margin)
        {
            var cols = Mathf.Max(1, TextColumns(text));
            return Mathf.Max(1, Mathf.Min((w - 2 * margin) / cols, (h - 2 * margin) / TotalRows));
        }

        /// <summary>Metni hücreye ortalı çizer; çerçeve (frame px) fg renkte. Satır 0 = üst.</summary>
        public static Color32[] RenderCell(string text, int w, int h, Color32 bg, Color32 fg, int frame = 3)
        {
            var px = new Color32[w * h];
            for (var i = 0; i < px.Length; i++)
                px[i] = bg;
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                if (x < frame || y < frame || x >= w - frame || y >= h - frame)
                    px[y * w + x] = fg;

            if (string.IsNullOrEmpty(text))
                return px;

            var scale = FitScale(text, w, h, frame + 4);
            var tw = TextColumns(text) * scale;
            var x0 = (w - tw) / 2;
            var y0 = (h - TotalRows * scale) / 2;
            for (var ci = 0; ci < text.Length; ci++)
            for (var gy = 0; gy < TotalRows; gy++)
            for (var gx = 0; gx < GlyphW; gx++)
            {
                if (!IsSet(text[ci], gx, gy))
                    continue;
                for (var sy = 0; sy < scale; sy++)
                for (var sx = 0; sx < scale; sx++)
                {
                    var X = x0 + (ci * (GlyphW + 1) + gx) * scale + sx;
                    var Y = y0 + gy * scale + sy;
                    if (X >= 0 && X < w && Y >= 0 && Y < h)
                        px[Y * w + X] = fg;
                }
            }

            return px;
        }
    }
}
