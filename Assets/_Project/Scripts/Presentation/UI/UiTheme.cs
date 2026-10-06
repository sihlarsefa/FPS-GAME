using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// HAREKÂT arayüz teması: renk paleti (koyu zeytin yeşili paneller, Türk bayrağı kırmızısı vurgu, kehribar,
    /// dost mavisi, düşman kırmızısı), yazı boyutları, ölçüler ve ortak yazı tipi.
    /// Tüm değerler 1920×1080 referans çözünürlüğüne göredir (CanvasScaler ölçekler).
    /// </summary>
    public static class UiTheme
    {
        // ------------------------------------------------------------------ Paneller / zemin

        /// <summary>Çok koyu zeytin — tam ekran menü zemini.</summary>
        public static readonly Color Background = Hex(0x12, 0x15, 0x0E);

        /// <summary>Koyu zeytin panel (yarı saydam, HUD ve menü kutuları).</summary>
        public static readonly Color PanelDark = Hex(0x16, 0x1A, 0x11, 0xE6);

        /// <summary>Standart zeytin panel.</summary>
        public static readonly Color Panel = Hex(0x23, 0x29, 0x1B, 0xEB);

        /// <summary>Açık zeytin panel (iç kutular, liste satırları).</summary>
        public static readonly Color PanelLight = Hex(0x34, 0x3D, 0x28, 0xF0);

        /// <summary>Panel kenar / ayraç çizgisi rengi.</summary>
        public static readonly Color PanelBorder = Hex(0x5A, 0x63, 0x45, 0xFF);

        /// <summary>HUD öğelerinin arkasındaki hafif koyu şerit.</summary>
        public static readonly Color HudBackdrop = new Color(0f, 0f, 0f, 0.45f);

        /// <summary>Modal pencerelerin arkasındaki karartma.</summary>
        public static readonly Color Overlay = new Color(0f, 0f, 0f, 0.72f);

        // ------------------------------------------------------------------ Vurgu renkleri

        /// <summary>Türk bayrağı kırmızısı #E30A17 — birincil vurgu.</summary>
        public static readonly Color Accent = Hex(0xE3, 0x0A, 0x17);

        /// <summary>Vurgu kırmızısının koyu tonu (basılı düğme).</summary>
        public static readonly Color AccentDark = Hex(0x9E, 0x07, 0x10);

        /// <summary>Vurgu kırmızısının açık tonu (üzerine gelme).</summary>
        public static readonly Color AccentLight = Hex(0xFF, 0x3B, 0x47);

        /// <summary>Kehribar — uyarılar, seçili öğeler, rütbe vurgusu.</summary>
        public static readonly Color Amber = Hex(0xF2, 0xA9, 0x00);

        /// <summary>Haki / kum rengi (ikincil vurgu).</summary>
        public static readonly Color Khaki = Hex(0xC3, 0xB0, 0x7A);

        /// <summary>Dost (kendi tim) mavisi.</summary>
        public static Color AllyBlue
        {
            get
            {
                switch (ColorBlindPalette)
                {
                    case 1: return Hex(0x00, 0x72, 0xB2);
                    case 2: return Hex(0x56, 0xB4, 0xE9);
                    case 3: return Hex(0x00, 0x9E, 0x73);
                    default: return Hex(0x3D, 0x9B, 0xFF);
                }
            }
        }

        /// <summary>Renk körlüğü paleti: 0 kapalı, 1 deuteranopi, 2 protanopi, 3 tritanopi (kırmızı-yeşil/mavi-sarı ayrımına bağımlı değil).</summary>
        public static int ColorBlindPalette { get; set; }

        /// <summary>Herhangi bir renk körlüğü paleti açık mı (eski bool arayüz).</summary>
        public static bool ColorBlindMode
        {
            get => ColorBlindPalette != 0;
            set => ColorBlindPalette = value ? 1 : 0;
        }

        /// <summary>Düşman kırmızısı (vurgu kırmızısından ayırt edilebilir, turuncumsu).</summary>
        public static Color EnemyRed
        {
            get
            {
                switch (ColorBlindPalette)
                {
                    case 1: return Hex(0xFF, 0xB0, 0x00);
                    case 2: return Hex(0xF0, 0xE4, 0x42);
                    case 3: return Hex(0xE6, 0x4B, 0x8C);
                    default: return Hex(0xFF, 0x4A, 0x3D);
                }
            }
        }

        /// <summary>Öldürme isabet işareti rengi (renk körü modunda başlık vuruşu kehribarından ayrışır).</summary>
        public static Color KillMarker => ColorBlindPalette == 0 ? EnemyRed : Color.white;

        /// <summary>Başarı / iyileşme yeşili.</summary>
        public static readonly Color Success = Hex(0x5F, 0xC8, 0x4E);

        /// <summary>Uyarı rengi (= kehribar).</summary>
        public static readonly Color Warning = Amber;

        /// <summary>Tehlike rengi (= düşman kırmızısı).</summary>
        public static Color Danger => EnemyRed;

        /// <summary>Harekât alanı (mavi bölge) rengi.</summary>
        public static readonly Color ZoneBlue = Hex(0x2E, 0x7B, 0xFF, 0xB4);

        /// <summary>Güvenli bölge (beyaz çember) rengi.</summary>
        public static readonly Color SafeZoneWhite = new Color(1f, 1f, 1f, 0.85f);

        /// <summary>Takviye (boost) çubuğu rengi.</summary>
        public static readonly Color Boost = Hex(0xF2, 0xC1, 0x2E);

        /// <summary>Zırh çubuğu rengi.</summary>
        public static readonly Color Armor = Hex(0x8E, 0xB8, 0xD8);

        // ------------------------------------------------------------------ Metin

        /// <summary>Ana metin rengi (kırık beyaz).</summary>
        public static readonly Color Text = Hex(0xEC, 0xEB, 0xE0);

        /// <summary>İkincil metin rengi.</summary>
        public static readonly Color TextDim = Hex(0xB4, 0xB6, 0xA4);

        /// <summary>Soluk metin (pasif öğeler, ipuçları).</summary>
        public static readonly Color TextMuted = Hex(0xA2, 0xA7, 0x90);

        /// <summary>Koyu zemin üzerinde başlık rengi (kehribar).</summary>
        public static readonly Color TextHeader = Amber;

        /// <summary>Metin gölgesi / kontur rengi.</summary>
        public static readonly Color TextShadow = new Color(0f, 0f, 0f, 0.75f);

        // ------------------------------------------------------------------ Kontroller

        /// <summary>Düğme normal rengi.</summary>
        public static readonly Color ButtonNormal = Hex(0x3A, 0x44, 0x2C, 0xF2);

        /// <summary>Düğme üzerine gelme rengi.</summary>
        public static readonly Color ButtonHover = Hex(0x50, 0x5D, 0x3B, 0xFF);

        /// <summary>Düğme basılı rengi.</summary>
        public static readonly Color ButtonPressed = Hex(0x2A, 0x31, 0x1F, 0xFF);

        /// <summary>Devre dışı düğme rengi.</summary>
        public static readonly Color ButtonDisabled = Hex(0x2B, 0x2E, 0x26, 0x99);

        /// <summary>Kaydırıcı / ilerleme çubuğu yuva rengi.</summary>
        public static readonly Color Track = Hex(0x0E, 0x10, 0x0B, 0xD0);

        /// <summary>Kaydırıcı dolgu rengi.</summary>
        public static readonly Color SliderFill = Hex(0xB9, 0x1A, 0x22);

        /// <summary>Kaydırıcı tutamağı rengi.</summary>
        public static readonly Color SliderHandle = Hex(0xEC, 0xEB, 0xE0);

        /// <summary>Onay kutusu zemin rengi.</summary>
        public static readonly Color ToggleBox = Hex(0x0E, 0x10, 0x0B, 0xE0);

        // ------------------------------------------------------------------ Can rengi

        /// <summary>Can yüksek (&gt;%60).</summary>
        public static readonly Color HealthHigh = Hex(0xEC, 0xEB, 0xE0);

        /// <summary>Can orta (%30–60).</summary>
        public static readonly Color HealthMid = Amber;

        /// <summary>Can düşük (&lt;%30).</summary>
        public static readonly Color HealthLow = Hex(0xE3, 0x2A, 0x2A);

        // ------------------------------------------------------------------ Yazı boyutları

        /// <summary>Çok küçük yazı (dipnot, harita etiketleri).</summary>
        public const int FontTiny = 16;

        /// <summary>Küçük yazı (ipuçları, öldürme akışı).</summary>
        public const int FontSmall = 18;

        /// <summary>Normal gövde yazısı.</summary>
        public const int FontNormal = 22;

        /// <summary>Düğme / vurgulu satır yazısı.</summary>
        public const int FontMedium = 26;

        /// <summary>Bölüm başlığı.</summary>
        public const int FontLarge = 34;

        /// <summary>Ekran başlığı.</summary>
        public const int FontTitle = 52;

        /// <summary>Dev başlık (ana menü logosu, "KAZANAN TİM").</summary>
        public const int FontHuge = 88;

        /// <summary>HUD cephane sayacı gibi büyük rakamlar.</summary>
        public const int FontHudNumber = 44;

        // ------------------------------------------------------------------ Ölçüler

        /// <summary>Referans çözünürlük genişliği.</summary>
        public const float ReferenceWidth = 1920f;

        /// <summary>Referans çözünürlük yüksekliği.</summary>
        public const float ReferenceHeight = 1080f;

        /// <summary>Panel iç boşluğu (px).</summary>
        public const int Padding = 16;

        /// <summary>Liste öğeleri arası boşluk (px).</summary>
        public const float Spacing = 8f;

        /// <summary>Varsayılan düğme yüksekliği (px).</summary>
        public const float ButtonHeight = 56f;

        /// <summary>Varsayılan düğme genişliği (px).</summary>
        public const float ButtonWidth = 300f;

        /// <summary>Ayar satırı yüksekliği (kaydırıcı, onay kutusu) (px).</summary>
        public const float RowHeight = 40f;

        /// <summary>Köşe yarıçapı (px) — yuvarlatılmış panel sprite'ları için.</summary>
        public const int CornerRadius = 6;

        /// <summary>Vurgu şeridi kalınlığı (px).</summary>
        public const float AccentStripWidth = 4f;

        /// <summary>Standart geçiş/solma süresi (sn, ölçeksiz zaman).</summary>
        public const float FadeDuration = 0.18f;

        /// <summary>UI katmanı (Unity yerleşik "UI" katmanı).</summary>
        public const int UiLayer = 5;

        // ------------------------------------------------------------------ Yazı tipi

        private static Font _font;

        /// <summary>
        /// Ortak yazı tipi: yerleşik "LegacyRuntime.ttf" (dinamik, Türkçe karakterleri destekler).
        /// Bulunamazsa işletim sistemi yazı tipine (Arial/Helvetica) düşer. Önbelleklidir.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (_font != null)
                    return _font;

                try
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                catch (System.Exception)
                {
                    _font = null;
                }

                if (_font == null)
                {
                    try
                    {
                        _font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Helvetica", "Liberation Sans", "DejaVu Sans" }, 16);
                    }
                    catch (System.Exception)
                    {
                        _font = null;
                    }
                }

                return _font;
            }
        }

        // ------------------------------------------------------------------ Yardımcılar

        /// <summary>Rengi verilen alfa ile döndürür.</summary>
        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>Rengi <paramref name="amount"/> (0..1) oranında aydınlatır (beyaza doğru), alfa korunur.</summary>
        public static Color Lighten(Color color, float amount)
        {
            var a = color.a;
            var c = Color.Lerp(color, Color.white, Mathf.Clamp01(amount));
            c.a = a;
            return c;
        }

        /// <summary>Rengi <paramref name="amount"/> (0..1) oranında koyulaştırır (siyaha doğru), alfa korunur.</summary>
        public static Color Darken(Color color, float amount)
        {
            var a = color.a;
            var c = Color.Lerp(color, Color.black, Mathf.Clamp01(amount));
            c.a = a;
            return c;
        }

        /// <summary>Can oranına (0..1) göre renk: yüksekte kırık beyaz, ortada kehribar, düşükte kırmızı.</summary>
        public static Color HealthColor(float fraction01)
        {
            if (fraction01 > 0.6f)
                return HealthHigh;
            if (fraction01 > 0.3f)
                return Color.Lerp(HealthMid, HealthHigh, (fraction01 - 0.3f) / 0.3f);
            return Color.Lerp(HealthLow, HealthMid, Mathf.Clamp01(fraction01 / 0.3f));
        }

        /// <summary>Dost ise dost mavisi, değilse düşman kırmızısı.</summary>
        public static Color TeamColor(bool isAlly) => isAlly ? AllyBlue : EnemyRed;

        /// <summary>
        /// Tim numarasına göre sabit, ayırt edici renk (harita/skor tablosu; 12 renklik paletten döner, negatif → soluk).
        /// Dost/düşman ayrımı için <see cref="TeamColor(bool)"/> kullanın.
        /// </summary>
        public static Color TeamPaletteColor(int team)
        {
            if (team < 0)
                return TextMuted;
            return TeamPalette[team % TeamPalette.Length];
        }

        private static readonly Color[] TeamPalette =
        {
            Hex(0x3D, 0x9B, 0xFF), Hex(0xFF, 0x4A, 0x3D), Hex(0xF2, 0xA9, 0x00), Hex(0x5F, 0xC8, 0x4E),
            Hex(0xC0, 0x6B, 0xFF), Hex(0x2E, 0xD3, 0xC6), Hex(0xFF, 0x8A, 0x3D), Hex(0xE8, 0x6B, 0xB5),
            Hex(0xB8, 0xC4, 0x5A), Hex(0x8A, 0x9B, 0xFF), Hex(0xD4, 0xA3, 0x73), Hex(0x9E, 0xE8, 0x8E)
        };

        /// <summary>Bayt değerlerinden renk üretir (0..255).</summary>
        public static Color Hex(byte r, byte g, byte b, byte a = 0xFF) => new Color32(r, g, b, a);

        /// <summary>"#RRGGBB" veya "#RRGGBBAA" metnini renge çevirir; geçersizse <paramref name="fallback"/>.</summary>
        public static Color Parse(string html, Color fallback)
        {
            return !string.IsNullOrEmpty(html) && ColorUtility.TryParseHtmlString(html, out var c) ? c : fallback;
        }

        /// <summary>Rengi zengin metin etiketi için "#RRGGBBAA" biçimine çevirir.</summary>
        public static string ToHex(Color color) => "#" + ColorUtility.ToHtmlStringRGBA(color);

        /// <summary>WCAG göreli parlaklık (sRGB, 0..1).</summary>
        public static float RelativeLuminance(Color c)
        {
            static float Ch(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * Ch(c.r) + 0.7152f * Ch(c.g) + 0.0722f * Ch(c.b);
        }

        /// <summary>WCAG karşıtlık oranı (1..21); alfa yok sayılır, <paramref name="over"/> zeminin üstüne bindirilmiş kabul edilir.</summary>
        public static float ContrastRatio(Color fg, Color over)
        {
            var a = RelativeLuminance(fg);
            var b = RelativeLuminance(over);
            if (a < b) { var t = a; a = b; b = t; }
            return (a + 0.05f) / (b + 0.05f);
        }

        /// <summary>Metin okunabilir mi: karşıtlık ≥ 4,5 (WCAG AA).</summary>
        public static bool MeetsTextContrast(Color fg, Color bg) => ContrastRatio(fg, bg) >= 4.5f;

        /// <summary>Metni zengin metin renk etiketiyle sarar: &lt;color=#..&gt;text&lt;/color&gt;.</summary>
        public static string Colorize(string text, Color color) => "<color=" + ToHex(color) + ">" + text + "</color>";
    }
}
