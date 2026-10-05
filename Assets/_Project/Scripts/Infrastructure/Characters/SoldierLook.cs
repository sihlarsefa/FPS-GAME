using UnityEngine;

namespace Project.Infrastructure.Characters
{
    /// <summary>
    /// Bir askerin görünüm verisi: dijital kamuflaj paleti (4 renk), ten rengi, teçhizat rengi, tim kolluğu rengi ve
    /// bordo bere (tim komutanı). <see cref="ForTeam"/> tim başına palet seçer:
    /// 0 Orman (yeşil), 1 Dağ (gri), 2 Çöl (bej), 3 Şehir (gri-siyah), sonra palet döngüsü (kolluk rengi tim başına farklıdır).
    /// <see cref="SoldierModel.Build"/> bu veriyi malzemelere çevirir; aynı tim aynı kamuflaj malzemesini paylaşır.
    /// </summary>
    public sealed class SoldierLook
    {
        // ------------------------------------------------------------------ Paletler
        // 0 Orman / 2 Çöl / 3 Şehir MaterialLibrary'deki TSK paletleriyle aynıdır (editörde üretilen malzemelerle uyumlu).
        private static readonly Color[] WoodlandCamo =
        {
            new Color(0.36f, 0.38f, 0.24f), new Color(0.36f, 0.27f, 0.18f), new Color(0.18f, 0.24f, 0.14f), new Color(0.08f, 0.08f, 0.07f)
        };

        private static readonly Color[] MountainCamo =
        {
            new Color(0.47f, 0.47f, 0.44f), new Color(0.35f, 0.36f, 0.33f), new Color(0.25f, 0.27f, 0.23f), new Color(0.62f, 0.62f, 0.58f)
        };

        private static readonly Color[] DesertCamo =
        {
            new Color(0.74f, 0.66f, 0.5f), new Color(0.62f, 0.52f, 0.36f), new Color(0.5f, 0.42f, 0.3f), new Color(0.82f, 0.76f, 0.62f)
        };

        private static readonly Color[] UrbanCamo =
        {
            new Color(0.55f, 0.56f, 0.56f), new Color(0.35f, 0.36f, 0.37f), new Color(0.18f, 0.19f, 0.2f), new Color(0.72f, 0.73f, 0.74f)
        };

        private static readonly Color[][] CamoPalettes = { WoodlandCamo, MountainCamo, DesertCamo, UrbanCamo };

        /// <summary>Palet başına teçhizat (yelek/çanta/kask) rengi.</summary>
        private static readonly Color[] GearColors =
        {
            new Color(0.25f, 0.27f, 0.18f), // Orman: haki yeşil
            new Color(0.3f, 0.31f, 0.28f),  // Dağ: gri-yeşil
            new Color(0.55f, 0.47f, 0.34f), // Çöl: coyote
            new Color(0.16f, 0.17f, 0.18f)  // Şehir: koyu gri
        };

        /// <summary>Palet adları (Türkçe arayüz/hata ayıklama için).</summary>
        public static readonly string[] PaletteNames = { "Orman", "Dağ", "Çöl", "Şehir" };

        /// <summary>Tim kolluğu renkleri: 0 Mavi, 1 Kırmızı, 2 Sarı, 3 Yeşil (MaterialLibrary kolluk renkleri), sonra ek renkler.</summary>
        private static readonly Color[] ArmbandColors =
        {
            new Color(0.1f, 0.3f, 0.85f),   // Mavi kuvvetler
            new Color(0.85f, 0.1f, 0.1f),   // Kırmızı kuvvetler
            new Color(0.95f, 0.8f, 0.1f),   // Sarı
            new Color(0.2f, 0.75f, 0.2f),   // Yeşil
            new Color(0.95f, 0.5f, 0.08f),  // Turuncu
            new Color(0.1f, 0.8f, 0.85f),   // Turkuaz
            new Color(0.6f, 0.2f, 0.85f),   // Mor
            new Color(0.95f, 0.95f, 0.95f), // Beyaz
            new Color(0.95f, 0.3f, 0.65f),  // Pembe
            new Color(0.55f, 0.95f, 0.15f), // Fıstık yeşili
            new Color(0.45f, 0.28f, 0.12f), // Kahverengi
            new Color(0.2f, 0.2f, 0.25f)    // Lacivert-siyah
        };

        /// <summary>Türk askerleri için gerçekçi ten tonları.</summary>
        private static readonly Color[] SkinTones =
        {
            new Color(0.85f, 0.66f, 0.52f),
            new Color(0.8f, 0.6f, 0.45f),
            new Color(0.74f, 0.55f, 0.41f),
            new Color(0.88f, 0.7f, 0.58f),
            new Color(0.66f, 0.48f, 0.35f),
            new Color(0.58f, 0.42f, 0.31f)
        };

        // ------------------------------------------------------------------ Veri

        /// <summary>Kamuflaj zemin rengi (~%45).</summary>
        public Color CamoA = WoodlandCamo[0];

        /// <summary>Kamuflaj leke rengi 1.</summary>
        public Color CamoB = WoodlandCamo[1];

        /// <summary>Kamuflaj leke rengi 2.</summary>
        public Color CamoC = WoodlandCamo[2];

        /// <summary>Kamuflaj küçük benek rengi.</summary>
        public Color CamoD = WoodlandCamo[3];

        public Color Skin = SkinTones[0];

        /// <summary>Yelek, çanta, kask ve kemer rengi.</summary>
        public Color Gear = GearColors[0];

        /// <summary>Tim kolluğu rengi (her iki kolda).</summary>
        public Color Armband = ArmbandColors[0];

        /// <summary>Bordo bere (tim komutanı). Kaskın yerine bere görünür.</summary>
        public bool Beret;

        /// <summary>Bıyık (görsel çeşitlilik).</summary>
        public bool Mustache;

        /// <summary>Kamuflaj dokusu tohumu (aynı tohum + renkler = paylaşılan malzeme).</summary>
        public int CamoSeed = 101;

        /// <summary>Palet indeksi (0 Orman, 1 Dağ, 2 Çöl, 3 Şehir).</summary>
        public int PaletteIndex;

        /// <summary>Tim numarası (ForTeam ile üretildiyse; değilse -1).</summary>
        public int Team = -1;

        /// <summary>Varsayılan görünüm (Orman kamuflajı, mavi kolluk). Her çağrı yeni bir örnek döndürür.</summary>
        public static SoldierLook Default => new SoldierLook();

        /// <summary>
        /// Tim görünümü: palet tim numarasına göre döner (0 Orman, 1 Dağ, 2 Çöl, 3 Şehir, 4 → Orman ...), kolluk rengi tim
        /// başına farklıdır. rng yalnızca kişisel çeşitlilik için (ten, bıyık) kullanılır; null olabilir.
        /// Tim komutanı için çağıran <see cref="Beret"/> = true yapar (SoldierModel, sahibi Leader ise otomatik takar).
        /// </summary>
        public static SoldierLook ForTeam(int team, System.Random rng)
        {
            var safeTeam = team < 0 ? 0 : team;
            var palette = safeTeam % CamoPalettes.Length;
            var cycle = safeTeam / CamoPalettes.Length;
            var camo = CamoPalettes[palette];

            var look = new SoldierLook
            {
                PaletteIndex = palette,
                Team = team,
                CamoA = Vary(camo[0], cycle),
                CamoB = Vary(camo[1], cycle),
                CamoC = Vary(camo[2], cycle),
                CamoD = Vary(camo[3], cycle),
                Gear = Vary(GearColors[palette], cycle),
                Armband = ArmbandFor(safeTeam),
                CamoSeed = 101 + palette * 101 + cycle * 7
            };

            if (rng != null)
            {
                look.Skin = SkinTones[rng.Next(SkinTones.Length)];
                look.Mustache = rng.NextDouble() < 0.45;
            }
            else
            {
                look.Skin = SkinTones[safeTeam % SkinTones.Length];
                look.Mustache = (safeTeam & 1) == 0;
            }

            return look;
        }

        /// <summary>Tim kolluk rengi (ilk 12 tim sabit palet, sonrası altın oranlı ton dağılımı).</summary>
        public static Color ArmbandFor(int team)
        {
            if (team < 0)
                team = 0;

            if (team < ArmbandColors.Length)
                return ArmbandColors[team];

            var hue = Mathf.Repeat(0.11f + team * 0.618034f, 1f);
            return Color.HSVToRGB(hue, 0.85f, 0.9f);
        }

        /// <summary>Derin kopya (çağıran değiştirebilir).</summary>
        public SoldierLook Clone()
        {
            return new SoldierLook
            {
                CamoA = CamoA, CamoB = CamoB, CamoC = CamoC, CamoD = CamoD, Skin = Skin, Gear = Gear, Armband = Armband,
                Beret = Beret, Mustache = Mustache, CamoSeed = CamoSeed, PaletteIndex = PaletteIndex, Team = Team
            };
        }

        /// <summary>Palet döngüsündeki timler için hafif ton farkı (aynı paleti kullanan timler ayırt edilebilsin).</summary>
        private static Color Vary(Color c, int cycle)
        {
            if (cycle <= 0)
                return c;

            var shade = 1f - 0.07f * (cycle % 3);
            var warm = (cycle & 1) == 1 ? 0.025f : -0.015f;
            return new Color(
                Mathf.Clamp01(c.r * shade + warm),
                Mathf.Clamp01(c.g * shade),
                Mathf.Clamp01(c.b * shade - warm),
                1f);
        }
    }
}
