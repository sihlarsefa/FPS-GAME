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
            // TSK dijital kamuflaj (ASKER_REFERANSI): zemin haki, kahve leke, zeytin leke, bej benek. Siyah/pembe/mor ton yok.
            new Color(0.42f, 0.41f, 0.25f), new Color(0.31f, 0.22f, 0.14f), new Color(0.27f, 0.31f, 0.18f), new Color(0.66f, 0.58f, 0.42f)
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
            new Color(0.33f, 0.34f, 0.21f), // Orman: zeytin-haki (üniformadan bir ton koyu)
            new Color(0.3f, 0.31f, 0.28f),  // Dağ: gri-yeşil
            new Color(0.55f, 0.47f, 0.34f), // Çöl: coyote
            new Color(0.16f, 0.17f, 0.18f)  // Şehir: koyu gri
        };

        /// <summary>Bordo bere (#6B1F2A): komutan/lobi askeri.</summary>
        public static readonly Color BeretBordo = new Color(0.42f, 0.12f, 0.165f);

        /// <summary>Bere kenar bandı ve yuvarlak arması (mat siyah).</summary>
        public static readonly Color BeretTrim = new Color(0.03f, 0.03f, 0.035f);

        /// <summary>Taktik eldiven: mat haki-bej.</summary>
        public static readonly Color GloveColor = new Color(0.44f, 0.4f, 0.27f);

        /// <summary>Bot: bej süet görünümü (hafif tozlu, mat).</summary>
        public static readonly Color BootColor = new Color(0.6f, 0.5f, 0.36f);

        /// <summary>Kol bandı (mavi): mat kumaş.</summary>
        public static readonly Color ArmbandMatBlue = new Color(0.07f, 0.2f, 0.58f);

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

        /// <summary>Kozmetik bere rengi kullanılsın mı?</summary>
        public bool HasBeretColor;

        /// <summary>Kozmetik bere rengi (<see cref="HasBeretColor"/> true ise).</summary>
        public Color BeretColor;

        /// <summary>Bıyık (görsel çeşitlilik).</summary>
        public bool Mustache;

        /// <summary>Yüz örtüsü (balaklava / şemagh). Bıyık örtülüyken çizilmez.</summary>
        public FaceCoverKind FaceCover;

        /// <summary>Kamuflaj dokusu tohumu (aynı tohum + renkler = paylaşılan malzeme).</summary>
        public int CamoSeed = 101;

        /// <summary>Palet indeksi (0 Orman, 1 Dağ, 2 Çöl, 3 Şehir).</summary>
        public int PaletteIndex;

        /// <summary>Kamuflaj desen türü (varsayılan: dijital blok). Kozmetik kamuflajlar <see cref="ApplyCosmetic"/> ile değiştirir.</summary>
        public CamoPatternKind CamoPattern = CamoPatternKind.Digital;

        /// <summary>Yüz boyası stili (None: yok).</summary>
        public FacePaintKind FacePaint = FacePaintKind.None;

        /// <summary>Kask örtüsü varyantı (None: yok).</summary>
        public HelmetCoverKind HelmetCover = HelmetCoverKind.None;

        /// <summary>Gili (keskin nişancı örtü) türü (None: yok).</summary>
        public GhillieKind Ghillie = GhillieKind.None;

        /// <summary>Savaş yıpranması 0..1 (0 = temiz). Doku 0/.33/.66/1 seviyesine nicemlenir; bkz. <see cref="WearRules"/>.</summary>
        public float Wear;

        /// <summary>Tim numarası (ForTeam ile üretildiyse; değilse -1).</summary>
        public int Team = -1;

        /// <summary>Düşman okunurluk görünümü uygulandı mı (<see cref="MakeEnemy"/>): koyu ton + kırmızı çapraz kolluk/kask bandı.</summary>
        public bool IsEnemy;

        /// <summary>Düşman üniforması koyulaştırma çarpanı (zemin/arazi ile albedo ayrımı).</summary>
        public const float EnemyDarken = 0.82f;

        /// <summary>Düşman bandı rengi: tüm paletlerden (yeşil/gri/bej) ayrışan doygun kırmızı.</summary>
        public static readonly Color EnemyBandColor = new Color(0.9f, 0.06f, 0.05f);

        /// <summary>
        /// Bu görünümü düşman okunurluğuna çevirir (kendini döndürür): üniforma tonu hafif koyu, kolluk sabit kırmızı;
        /// SoldierModel kolluk/kask bandı için <see cref="EnemyBandPixels"/> çapraz şerit dokusunu kullanabilir
        /// (renk körlüğü: şekil farkı). Kamuflaj deseni/tohumu değişmez. Tekrar çağrı idempotent.
        /// </summary>
        public SoldierLook MakeEnemy()
        {
            if (IsEnemy)
                return this;
            IsEnemy = true;
            CamoA = Darken(CamoA); CamoB = Darken(CamoB); CamoC = Darken(CamoC); CamoD = Darken(CamoD);
            Gear = Darken(Gear);
            Armband = EnemyBandColor;
            return this;
        }

        /// <summary>Tim görünümü + düşman okunurluğu (tek çağrı).</summary>
        public static SoldierLook ForEnemyTeam(int team, System.Random rng) => ForTeam(team, rng).MakeEnemy();

        /// <summary>Renk koyulaştırma; en açık kanalı yumuşakça kısar (kar/çöl gibi açık paletlerde ek ayrım).</summary>
        public static Color Darken(Color c)
        {
            var lum = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            var k = EnemyDarken - Mathf.Clamp01(lum - 0.5f) * 0.12f;
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }

        /// <summary>
        /// Düşman bandı dokusu (size x size, y*size+x): zemin kırmızı, 45 derece açılı koyu-beyaz çapraz şeritler. Renk körleri için
        /// dolu dikdörtgen kolluktan şekille ayrılır; döşenebilir. Dönen dizi her çağrıda yenidir.
        /// </summary>
        public static Color32[] EnemyBandPixels(int size)
        {
            size = size < 8 ? 8 : size > 256 ? 256 : size;
            var px = new Color32[size * size];
            var red = new Color32(230, 15, 13, 255);
            var white = new Color32(245, 245, 240, 255);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var t = ((x + y) % size) / (float)size * 3f; // 3 şerit/periyot (döşenebilir)
                    var f = t - Mathf.Floor(t);
                    px[y * size + x] = f > 0.4f && f < 0.7f ? white : red;
                }
            }

            return px;
        }

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
                look.FaceCover = SoldierDetailRules.FaceCoverFor(palette, (float)rng.NextDouble());
            }
            else
            {
                look.Skin = SkinTones[safeTeam % SkinTones.Length];
                look.Mustache = (safeTeam & 1) == 0;
                look.FaceCover = SoldierDetailRules.FaceCoverFor(palette, (safeTeam * 0.37f) % 1f);
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
                Beret = Beret, Mustache = Mustache, CamoSeed = CamoSeed, PaletteIndex = PaletteIndex, Team = Team,
                HasBeretColor = HasBeretColor, BeretColor = BeretColor, FaceCover = FaceCover,
                CamoPattern = CamoPattern, FacePaint = FacePaint, HelmetCover = HelmetCover, Ghillie = Ghillie, IsEnemy = IsEnemy, Wear = Wear
            };
        }

        /// <summary>
        /// Kozmetik kimliğini (kamuflaj / yüz boyası / kask örtüsü / gili) görünüme uygular. Tanınmayan kimlik için false döner
        /// ve görünüm değişmez. Gili yalnızca <paramref name="sniper"/> true ise uygulanır.
        /// </summary>
        public bool ApplyCosmetic(string id, bool sniper = true)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            if (CamoCatalog.TryGet(id, out var camo))
            {
                CamoA = camo.Palette[0]; CamoB = camo.Palette[1]; CamoC = camo.Palette[2]; CamoD = camo.Palette[3];
                Gear = camo.Gear;
                CamoPattern = camo.Pattern;
                CamoSeed = 101 + camo.Index * 37;
                return true;
            }

            if (CosmeticLooks.FacePaintById.TryGetValue(id, out var fp)) { FacePaint = fp; return true; }
            if (CosmeticLooks.HelmetCoverById.TryGetValue(id, out var hc)) { HelmetCover = hc; return true; }
            if (CosmeticLooks.GhillieById.TryGetValue(id, out var gh))
            {
                Ghillie = sniper ? gh : GhillieKind.None;
                return true;
            }

            return false;
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

    // ======================================================================== Kozmetik görünüm türleri

    /// <summary>Kamuflaj desen aileleri (her biri farklı bir üretim kuralı; bkz. <see cref="CamoPatternPixels"/>).</summary>
    public enum CamoPatternKind { Digital = 0, Dune, Arctic, WoodlandDigital, MultiCam, Night, Stone, Steppe, City }

    /// <summary>Yüz boyası stilleri (yüz dokusunun üstüne yarı saydam bindirme).</summary>
    public enum FacePaintKind { None = 0, Stripes, WoodlandBlotch, NightBlack, SkullHalf, DesertDash, CrescentStar }

    /// <summary>Kask örtüsü varyantları.</summary>
    public enum HelmetCoverKind { None = 0, CamoFabric, ScrimNet, SnowCover, DesertTan }

    /// <summary>Keskin nişancı gili (örtü) türleri; renk <see cref="GhillieBits"/> ile gelir.</summary>
    public enum GhillieKind { None = 0, Woodland, Desert, Snow }

    /// <summary>Bir kamuflaj kozmetiğinin tanımı: desen ailesi + 4 renkli palet + teçhizat rengi + silah kaplama kimliği.</summary>
    public sealed class CamoDef
    {
        public string Id;
        public int Index;
        public CamoPatternKind Pattern;
        public Color[] Palette;
        public Color Gear;
        /// <summary>Aynı palette silah kaplaması (weapon_skin yuvası) kimliği; yoksa null.</summary>
        public string WrapId;
    }

    /// <summary>
    /// Yeni kamuflaj kataloğu (8 desen): kimlikler kararlıdır, yalnızca eklenir. Silah kaplamaları aynı paleti kullanır
    /// (<see cref="WrapTint"/> = palet A rengi; <see cref="CamoPatternPixels"/> ile aynı desen silah dokusuna da basılabilir).
    /// </summary>
    public static class CamoCatalog
    {
        private static CamoDef Def(int i, string id, CamoPatternKind k, Color gear, params Color[] pal)
            => new CamoDef { Id = id, Index = i, Pattern = k, Palette = pal, Gear = gear, WrapId = "wrap_" + id.Substring(5) };

        public static readonly CamoDef[] All =
        {
            // çöl: kum tepesi bantları
            Def(0, "camo_dune", CamoPatternKind.Dune, new Color(0.58f, 0.48f, 0.34f),
                new Color(0.80f, 0.70f, 0.52f), new Color(0.68f, 0.56f, 0.38f), new Color(0.55f, 0.44f, 0.30f), new Color(0.90f, 0.84f, 0.68f)),
            // kar
            Def(1, "camo_arctic", CamoPatternKind.Arctic, new Color(0.62f, 0.66f, 0.70f),
                new Color(0.80f, 0.83f, 0.86f), new Color(0.62f, 0.67f, 0.72f), new Color(0.42f, 0.47f, 0.52f), new Color(0.96f, 0.97f, 0.98f)),
            // orman dijital
            Def(2, "camo_woodland_digital", CamoPatternKind.WoodlandDigital, new Color(0.20f, 0.24f, 0.15f),
                new Color(0.28f, 0.36f, 0.20f), new Color(0.18f, 0.24f, 0.12f), new Color(0.38f, 0.30f, 0.18f), new Color(0.07f, 0.09f, 0.06f)),
            // multicam benzeri
            Def(3, "camo_multi", CamoPatternKind.MultiCam, new Color(0.40f, 0.36f, 0.26f),
                new Color(0.60f, 0.56f, 0.40f), new Color(0.42f, 0.44f, 0.28f), new Color(0.50f, 0.38f, 0.26f), new Color(0.72f, 0.68f, 0.52f)),
            // gece
            Def(4, "camo_midnight", CamoPatternKind.Night, new Color(0.08f, 0.09f, 0.12f),
                new Color(0.09f, 0.11f, 0.15f), new Color(0.05f, 0.07f, 0.10f), new Color(0.12f, 0.14f, 0.18f), new Color(0.02f, 0.03f, 0.04f)),
            // taş
            Def(5, "camo_slate", CamoPatternKind.Stone, new Color(0.30f, 0.31f, 0.30f),
                new Color(0.46f, 0.46f, 0.44f), new Color(0.34f, 0.35f, 0.34f), new Color(0.24f, 0.25f, 0.25f), new Color(0.60f, 0.59f, 0.55f)),
            // bozkır
            Def(6, "camo_steppe", CamoPatternKind.Steppe, new Color(0.46f, 0.42f, 0.24f),
                new Color(0.62f, 0.56f, 0.32f), new Color(0.48f, 0.44f, 0.24f), new Color(0.36f, 0.34f, 0.20f), new Color(0.74f, 0.68f, 0.44f)),
            // şehir
            Def(7, "camo_city", CamoPatternKind.City, new Color(0.17f, 0.18f, 0.20f),
                new Color(0.50f, 0.51f, 0.53f), new Color(0.32f, 0.33f, 0.36f), new Color(0.16f, 0.17f, 0.19f), new Color(0.68f, 0.70f, 0.72f))
        };

        public static bool TryGet(string id, out CamoDef def)
        {
            def = null;
            if (string.IsNullOrEmpty(id))
                return false;
            for (var i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    def = All[i];
                    return true;
                }
            }

            return false;
        }

        /// <summary>Silah kaplaması kimliğinden (wrap_*) kamuflaj tanımı.</summary>
        public static bool TryGetByWrap(string wrapId, out CamoDef def)
        {
            def = null;
            if (string.IsNullOrEmpty(wrapId))
                return false;
            for (var i = 0; i < All.Length; i++)
            {
                if (All[i].WrapId == wrapId)
                {
                    def = All[i];
                    return true;
                }
            }

            return false;
        }

        /// <summary>Silah kaplaması baskın tonu (palet A); tanınmazsa false.</summary>
        public static bool WrapTint(string wrapId, out Color tint)
        {
            tint = Color.white;
            if (!TryGetByWrap(wrapId, out var d))
                return false;
            tint = d.Palette[0];
            return true;
        }
    }

    /// <summary>Kimlik -> görünüm türü eşlemeleri (yüz boyası, kask örtüsü, gili).</summary>
    public static class CosmeticLooks
    {
        public static readonly System.Collections.Generic.Dictionary<string, FacePaintKind> FacePaintById =
            new System.Collections.Generic.Dictionary<string, FacePaintKind>
            {
                { "face_none", FacePaintKind.None },
                { "face_stripes", FacePaintKind.Stripes },
                { "face_blotch", FacePaintKind.WoodlandBlotch },
                { "face_nightblack", FacePaintKind.NightBlack },
                { "face_skull", FacePaintKind.SkullHalf },
                { "face_dash", FacePaintKind.DesertDash },
                { "face_crescent", FacePaintKind.CrescentStar }
            };

        public static readonly System.Collections.Generic.Dictionary<string, HelmetCoverKind> HelmetCoverById =
            new System.Collections.Generic.Dictionary<string, HelmetCoverKind>
            {
                { "helmet_none", HelmetCoverKind.None },
                { "helmet_camo", HelmetCoverKind.CamoFabric },
                { "helmet_scrim", HelmetCoverKind.ScrimNet },
                { "helmet_snow", HelmetCoverKind.SnowCover },
                { "helmet_tan", HelmetCoverKind.DesertTan }
            };

        public static readonly System.Collections.Generic.Dictionary<string, GhillieKind> GhillieById =
            new System.Collections.Generic.Dictionary<string, GhillieKind>
            {
                { "ghillie_none", GhillieKind.None },
                { "ghillie_woodland", GhillieKind.Woodland },
                { "ghillie_desert", GhillieKind.Desert },
                { "ghillie_snow", GhillieKind.Snow }
            };

        /// <summary>Kask örtüsü ana rengi (None: şeffaf).</summary>
        public static Color HelmetCoverColor(HelmetCoverKind k)
        {
            switch (k)
            {
                case HelmetCoverKind.CamoFabric: return new Color(0.30f, 0.34f, 0.20f);
                case HelmetCoverKind.ScrimNet: return new Color(0.26f, 0.29f, 0.18f);
                case HelmetCoverKind.SnowCover: return new Color(0.92f, 0.94f, 0.96f);
                case HelmetCoverKind.DesertTan: return new Color(0.70f, 0.60f, 0.42f);
                default: return new Color(0f, 0f, 0f, 0f);
            }
        }

        /// <summary>Ağ örtüde (scrim) kaskın üstüne eklenecek yaprak/bez şerit sayısı (model kurucu için).</summary>
        public static int HelmetFoliageCount(HelmetCoverKind k) => k == HelmetCoverKind.ScrimNet ? 9 : k == HelmetCoverKind.CamoFabric ? 3 : 0;

        /// <summary>Yüz boyası ana ve ikincil rengi.</summary>
        public static void FacePaintColors(FacePaintKind k, out Color primary, out Color secondary)
        {
            switch (k)
            {
                case FacePaintKind.Stripes: primary = new Color(0.05f, 0.06f, 0.04f); secondary = new Color(0.22f, 0.3f, 0.14f); break;
                case FacePaintKind.WoodlandBlotch: primary = new Color(0.18f, 0.26f, 0.12f); secondary = new Color(0.32f, 0.24f, 0.14f); break;
                case FacePaintKind.NightBlack: primary = new Color(0.03f, 0.03f, 0.04f); secondary = new Color(0.08f, 0.09f, 0.11f); break;
                case FacePaintKind.SkullHalf: primary = new Color(0.92f, 0.92f, 0.88f); secondary = new Color(0.04f, 0.04f, 0.04f); break;
                case FacePaintKind.DesertDash: primary = new Color(0.5f, 0.38f, 0.22f); secondary = new Color(0.78f, 0.68f, 0.48f); break;
                case FacePaintKind.CrescentStar: primary = new Color(0.85f, 0.1f, 0.12f); secondary = new Color(0.96f, 0.96f, 0.96f); break;
                default: primary = secondary = new Color(0f, 0f, 0f, 0f); break;
            }
        }
    }

    /// <summary>Gili parçası: gövde üzerinde normalize konum (u 0..1 yatay, v 0..1 dikey), uzunluk (m), eğim (derece) ve renk.</summary>
    public struct GhilliePiece
    {
        public float U, V, Length, TiltDeg;
        public Color Color;
    }

    /// <summary>Keskin nişancı gili parçalarını deterministik üretir (SoldierModel her parçayı ince şerit/küp olarak yerleştirir).</summary>
    public static class GhillieBits
    {
        public static Color[] Palette(GhillieKind k)
        {
            switch (k)
            {
                case GhillieKind.Woodland: return new[] { new Color(0.22f, 0.3f, 0.14f), new Color(0.3f, 0.26f, 0.14f), new Color(0.14f, 0.2f, 0.1f), new Color(0.38f, 0.36f, 0.2f) };
                case GhillieKind.Desert: return new[] { new Color(0.62f, 0.52f, 0.34f), new Color(0.5f, 0.42f, 0.28f), new Color(0.72f, 0.64f, 0.46f), new Color(0.4f, 0.34f, 0.22f) };
                case GhillieKind.Snow: return new[] { new Color(0.9f, 0.92f, 0.95f), new Color(0.76f, 0.8f, 0.84f), new Color(0.96f, 0.97f, 0.98f), new Color(0.6f, 0.65f, 0.7f) };
                default: return new Color[0];
            }
        }

        /// <summary>count parça; None için boş dizi. Aynı (tür, count, seed) aynı sonucu verir.</summary>
        public static GhilliePiece[] Generate(GhillieKind kind, int count, int seed)
        {
            if (kind == GhillieKind.None || count <= 0)
                return new GhilliePiece[0];
            var pal = Palette(kind);
            var rng = new System.Random(seed * 7919 + (int)kind * 104729);
            var res = new GhilliePiece[count];
            for (var i = 0; i < count; i++)
            {
                var v = (float)rng.NextDouble();
                // omuz/sırt/kaskta yoğun: v büyük = yukarı
                res[i] = new GhilliePiece
                {
                    U = (float)rng.NextDouble(),
                    V = v,
                    Length = 0.08f + (float)rng.NextDouble() * (v > 0.6f ? 0.22f : 0.14f),
                    TiltDeg = ((float)rng.NextDouble() - 0.5f) * 80f,
                    Color = pal[rng.Next(pal.Length)]
                };
            }

            return res;
        }
    }

    /// <summary>
    /// Saf piksel üretici (Unity doku nesnesi gerektirmez): 8 kamuflaj ailesi ve 6 yüz boyası. Tüm desenler döşenebilir
    /// (kenar uyumlu) ve (tür, palet, tohum, boyut) için deterministiktir. <see cref="BuildTexture"/> Texture2D'ye çevirir.
    /// </summary>
    public static class CamoPatternPixels
    {
        /// <summary>size x size, satır sırası aşağıdan yukarı olmayan düz Color32 dizisi (y*size+x).</summary>
        public static Color32[] Generate(CamoPatternKind kind, Color a, Color b, Color c, Color d, int seed, int size)
        {
            size = size < 8 ? 8 : size > 512 ? 512 : size;
            var px = new Color32[size * size];
            var inv = 1f / size;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var u = x * inv;
                    var v = y * inv;
                    px[y * size + x] = Pick(kind, u, v, a, b, c, d, seed, size);
                }
            }

            return px;
        }

        public static Texture2D BuildTexture(CamoPatternKind kind, Color a, Color b, Color c, Color d, int seed, int size = 128)
        {
            var px = Generate(kind, a, b, c, d, seed, size);
            size = (int)Mathf.Sqrt(px.Length);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "Camo_" + kind,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            tex.SetPixels32(px);
            tex.Apply(true, false);
            return tex;
        }

        private static Color32 Pick(CamoPatternKind kind, float u, float v, Color a, Color b, Color c, Color d, int seed, int size)
        {
            float n;
            switch (kind)
            {
                case CamoPatternKind.Dune:
                {
                    // yatay dalgalı bantlar: v ekseninde sinüs + gürültü sapması
                    var warp = Fbm(u, v, 3, 2, seed) * 0.35f;
                    n = Mathf.Repeat(v * 5f + warp * 3f + Mathf.Sin((u + warp) * Mathf.PI * 4f) * 0.18f, 1f);
                    return To(n < 0.45f ? a : n < 0.7f ? b : n < 0.9f ? c : d);
                }
                case CamoPatternKind.Arctic:
                {
                    n = Fbm(u, v, 5, 3, seed);
                    return To(n > 0.72f ? c : n > 0.6f ? b : n > 0.46f ? a : d);
                }
                case CamoPatternKind.WoodlandDigital:
                case CamoPatternKind.Night:
                {
                    var cells = kind == CamoPatternKind.Night ? 40 : 32;
                    var cu = Mathf.Floor(u * cells) / cells;
                    var cv = Mathf.Floor(v * cells) / cells;
                    n = Fbm(cu, cv, 4, 3, seed) * 0.8f + Hash01((int)(cu * cells), (int)(cv * cells), seed + 5) * 0.2f;
                    return To(n < 0.4f ? a : n < 0.58f ? b : n < 0.78f ? c : d);
                }
                case CamoPatternKind.MultiCam:
                {
                    n = Fbm(u, v, 3, 2, seed);
                    var m = Fbm(u + 0.37f, v + 0.19f, 6, 2, seed + 11);
                    if (m > 0.7f) return To(d); // açık benekler
                    return To(n < 0.4f ? a : n < 0.58f ? b : c);
                }
                case CamoPatternKind.Stone:
                    return To(Voronoi(u, v, 7, seed, a, b, c, d));
                case CamoPatternKind.Steppe:
                {
                    // dikey ot çizgileri
                    n = Noise(u * 28f, v * 2f, 28, 2, seed) * 0.7f + Fbm(u, v, 4, 4, seed + 3) * 0.3f;
                    return To(n < 0.35f ? c : n < 0.55f ? b : n < 0.8f ? a : d);
                }
                case CamoPatternKind.City:
                {
                    // 8x16 dikdörtgen blok ızgarası
                    var bx = Mathf.FloorToInt(u * 8f);
                    var by = Mathf.FloorToInt(v * 16f);
                    var h = Hash01(bx, by, seed);
                    return To(h < 0.4f ? a : h < 0.7f ? b : h < 0.9f ? c : d);
                }
                default:
                {
                    var cu = Mathf.Floor(u * 32f);
                    var cv = Mathf.Floor(v * 32f);
                    var h = Hash01((int)cu, (int)cv, seed);
                    return To(h < 0.45f ? a : h < 0.7f ? b : h < 0.9f ? c : d);
                }
            }
        }

        /// <summary>Yüz boyası bindirmesi: u,v 0..1 (yüz UV'si); alfa 0 = boyasız. Simetriktir (u=0.5 eksenli) — Skull ve Dash hariç.</summary>
        public static Color32 FacePaintPixel(FacePaintKind kind, float u, float v)
        {
            if (kind == FacePaintKind.None)
                return new Color32(0, 0, 0, 0);
            CosmeticLooks.FacePaintColors(kind, out var p, out var s);
            var du = Mathf.Abs(u - 0.5f);
            switch (kind)
            {
                case FacePaintKind.Stripes:
                {
                    // yanaklarda çapraz üç çizgi
                    var t = Mathf.Repeat((du * 4f + v * 2.2f) * 3f, 1f);
                    if (v > 0.2f && v < 0.62f && du > 0.08f && t < 0.28f) return To(p, 0.9f);
                    if (v > 0.2f && v < 0.62f && du > 0.08f && t > 0.5f && t < 0.62f) return To(s, 0.7f);
                    break;
                }
                case FacePaintKind.WoodlandBlotch:
                {
                    var n = Fbm(u, v, 3, 3, 77);
                    if (n > 0.62f) return To(p, 0.85f);
                    if (n < 0.28f) return To(s, 0.75f);
                    break;
                }
                case FacePaintKind.NightBlack:
                {
                    // gözaltı ve alın bandı + burun köprüsü
                    if (v > 0.42f && v < 0.58f) return To(p, 0.95f);
                    if (v > 0.8f) return To(s, 0.8f);
                    if (du < 0.04f && v > 0.3f && v < 0.8f) return To(p, 0.8f);
                    break;
                }
                case FacePaintKind.SkullHalf:
                {
                    // alt yarı: çene/diş şeritleri, göz çukurları
                    var eye = Mathf.Sqrt((du - 0.17f) * (du - 0.17f) + (v - 0.6f) * (v - 0.6f)) < 0.09f;
                    if (eye) return To(s, 0.95f);
                    if (v < 0.4f)
                    {
                        var tooth = Mathf.Repeat(u * 10f, 1f) < 0.12f && v > 0.1f && v < 0.34f;
                        return tooth ? To(s, 0.9f) : To(p, 0.85f);
                    }

                    break;
                }
                case FacePaintKind.DesertDash:
                {
                    // asimetrik: sol yanakta kısa yatay çizgiler
                    if (u < 0.5f && v > 0.25f && v < 0.6f && Mathf.Repeat(v * 14f, 1f) < 0.42f && u > 0.12f) return To(p, 0.85f);
                    if (u >= 0.5f && v > 0.35f && v < 0.5f && u < 0.88f && Mathf.Repeat(u * 9f, 1f) < 0.3f) return To(s, 0.7f);
                    break;
                }
                case FacePaintKind.CrescentStar:
                {
                    // iki yanakta kırmızı hilal (iki daire farkı) + beyaz yıldız noktası
                    var cx = 0.5f + (u < 0.5f ? -0.2f : 0.2f);
                    var dx = u - cx;
                    var dy = v - 0.42f;
                    var outer = Mathf.Sqrt(dx * dx + dy * dy) < 0.11f;
                    var inner = Mathf.Sqrt((dx - 0.04f) * (dx - 0.04f) + dy * dy) < 0.09f;
                    if (outer && !inner) return To(p, 0.95f);
                    if (Mathf.Sqrt((dx - 0.06f) * (dx - 0.06f) + dy * dy) < 0.022f) return To(s, 0.95f);
                    break;
                }
            }

            return new Color32(0, 0, 0, 0);
        }

        public static Color32[] FacePaintPixels(FacePaintKind kind, int size)
        {
            size = size < 8 ? 8 : size > 512 ? 512 : size;
            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    px[y * size + x] = FacePaintPixel(kind, (x + 0.5f) / size, (y + 0.5f) / size);
            return px;
        }

        // ---- yardımcılar
        private static Color32 To(Color c) => new Color32(B(c.r), B(c.g), B(c.b), 255);
        private static Color32 To(Color c, float alpha) => new Color32(B(c.r), B(c.g), B(c.b), B(alpha));
        private static byte B(float f) => (byte)Mathf.Clamp(Mathf.RoundToInt(f * 255f), 0, 255);

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>Döşenebilir değer gürültüsü: (u,v)*(fx,fy) ızgarası, periyot fx/fy.</summary>
        private static float Noise(float x, float y, int px, int py, int seed)
        {
            var ix = Mathf.FloorToInt(x);
            var iy = Mathf.FloorToInt(y);
            var fx = x - ix;
            var fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            var x0 = Mod(ix, px); var x1 = Mod(ix + 1, px);
            var y0 = Mod(iy, py); var y1 = Mod(iy + 1, py);
            var a = Hash01(x0, y0, seed); var b = Hash01(x1, y0, seed);
            var c = Hash01(x0, y1, seed); var d = Hash01(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static int Mod(int a, int m) => ((a % m) + m) % m;

        /// <summary>Döşenebilir fBm (0..1): taban frekans baseFreq, oktav sayısı oct.</summary>
        private static float Fbm(float u, float v, int baseFreq, int oct, int seed)
        {
            var sum = 0f; var amp = 0.5f; var norm = 0f; var f = baseFreq;
            for (var i = 0; i < oct; i++)
            {
                sum += Noise(u * f, v * f, f, f, seed + i * 31) * amp;
                norm += amp;
                amp *= 0.5f;
                f *= 2;
            }

            return sum / norm;
        }

        private static Color Voronoi(float u, float v, int cells, int seed, Color a, Color b, Color c, Color d)
        {
            var gx = u * cells; var gy = v * cells;
            var ix = Mathf.FloorToInt(gx); var iy = Mathf.FloorToInt(gy);
            var best = 9f; var second = 9f; var id = 0f;
            for (var oy = -1; oy <= 1; oy++)
            {
                for (var ox = -1; ox <= 1; ox++)
                {
                    var cx = ix + ox; var cy = iy + oy;
                    var wx = Mod(cx, cells); var wy = Mod(cy, cells);
                    var px = cx + Hash01(wx, wy, seed) ;
                    var py = cy + Hash01(wx, wy, seed + 9);
                    var dist = (gx - px) * (gx - px) + (gy - py) * (gy - py);
                    if (dist < best) { second = best; best = dist; id = Hash01(wx, wy, seed + 21); }
                    else if (dist < second) second = dist;
                }
            }

            if (Mathf.Sqrt(second) - Mathf.Sqrt(best) < 0.12f) return c; // çatlak çizgisi
            return id < 0.45f ? a : id < 0.8f ? b : d;
        }
    }
}
