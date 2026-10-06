using Project.Application.Catalogs;
using Project.Core.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// TSK rütbe işaretinin stilize omuz apoleti hâli (UI sprite'larıyla): erbaşlar kırmızı çavuş şeritleri, uzman erbaşlar
    /// altın şerit + çubuk, astsubaylar altın çubuk/yıldız, subaylar yıldız (üst rütbelerde altın palamut çubuğu).
    /// Görsel bir yaklaşımdır; tam ad için <see cref="RankCatalog.GetName"/> yazısı yanında gösterilir.
    /// </summary>
    public static class MenuRankInsignia
    {
        /// <summary>Altın işaret rengi.</summary>
        public static readonly Color Gold = UiTheme.Hex(0xE2, 0xB8, 0x4A);

        /// <summary>Gümüş işaret rengi (asteğmen).</summary>
        public static readonly Color Silver = UiTheme.Hex(0xC9, 0xCF, 0xD4);

        /// <summary>Erbaş şerit kırmızısı.</summary>
        public static readonly Color EnlistedRed = UiTheme.Hex(0xD2, 0x1E, 0x26);

        private static readonly Color BoardColor = UiTheme.Hex(0x2C, 0x34, 0x22, 0xFF);
        private static readonly Color BoardEdge = UiTheme.Hex(0x6B, 0x74, 0x4E, 0xFF);

        private enum Symbol
        {
            Star,
            SmallStar,
            Chevron,
            Bar,
            Wreath
        }

        /// <summary>
        /// Apolet oluşturur (genişlik ≈ 2.6 × yükseklik). Ebeveyn merkezinde; kökü döndürür.
        /// </summary>
        public static RectTransform Create(Transform parent, MilitaryRank rank, float height)
        {
            height = Mathf.Max(16f, height);
            var root = UiFactory.CreateRect("RankInsignia", parent);
            UiFactory.Anchor(root, UiAnchor.Center, Vector2.zero, new Vector2(height * 2.6f, height));
            UiFactory.LayoutSize(root, height * 2.6f, height);
            Rebuild(root, rank);
            return root;
        }

        /// <summary>Var olan apoleti yeni rütbeye göre yeniden çizer.</summary>
        public static void Rebuild(RectTransform root, MilitaryRank rank)
        {
            if (root == null)
                return;

            UiFactory.ClearChildren(root);
            var height = root.rect.height > 1f ? root.rect.height : Mathf.Max(16f, root.sizeDelta.y);

            // LX4 vektör apoleti; üretilemezse aşağıdaki sprite tabanlı çizim kullanılır.
            Sprite art = null;
            try { art = EmblemArt.GetApoletSprite(rank); }
            catch (System.Exception e) { Debug.LogWarning("[RütbeApoleti] sanat: " + e.Message); }
            if (art != null)
            {
                var img = UiFactory.Image(root, art, Color.white);
                img.gameObject.name = "ApoletArt";
                img.preserveAspect = true;
                img.raycastTarget = false;
                UiFactory.Stretch(img);
                return;
            }

            var board = UiFactory.Panel(root, BoardColor, UiSprites.GetRoundedRect(Mathf.Clamp((int)(height * 0.18f), 3, 12)));
            board.gameObject.name = "Board";
            board.GetComponent<Image>().raycastTarget = false;

            var edge = UiFactory.Image(board, UiSprites.GetRoundedRectOutline(Mathf.Clamp((int)(height * 0.18f), 3, 12)), BoardEdge);
            UiFactory.Stretch(edge);

            // Apolet düğmesi (sol uç).
            var button = UiFactory.Image(board, UiSprites.Circle, UiTheme.Darken(Gold, 0.25f));
            button.gameObject.name = "Button";
            var buttonSize = height * 0.28f;
            UiFactory.Anchor(button, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(height * 0.32f, 0f), new Vector2(buttonSize, buttonSize));

            var row = UiFactory.HorizontalList(board, height * 0.06f, 0, TextAnchor.MiddleCenter);
            row.gameObject.name = "Symbols";
            UiFactory.Stretch(row, height * 0.6f, height * 0.12f, height * 0.18f, height * 0.12f);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;

            var symbolSize = height * 0.62f;
            var kind = RankCatalog.GetCategoryKind(rank);
            var value = (int)rank;

            switch (kind)
            {
                case RankCategory.Officer:
                    BuildOfficer(row, rank, symbolSize);
                    break;
                case RankCategory.NonCommissioned:
                {
                    var tier = value - (int)MilitaryRank.AstsubayCavus;   // 0..5
                    if (tier >= 3)
                        AddSymbol(row, Symbol.Star, symbolSize, Gold);
                    var bars = tier % 3 + 1;
                    for (var i = 0; i < bars; i++)
                        AddSymbol(row, Symbol.Bar, symbolSize, Gold);
                    break;
                }
                case RankCategory.Specialist:
                {
                    var chevrons = rank == MilitaryRank.UzmanCavus ? 2 : 1;
                    for (var i = 0; i < chevrons; i++)
                        AddSymbol(row, Symbol.Chevron, symbolSize, Gold);
                    AddSymbol(row, Symbol.Bar, symbolSize, Gold);
                    break;
                }
                default:
                    switch (rank)
                    {
                        case MilitaryRank.Onbasi:
                            AddSymbol(row, Symbol.Chevron, symbolSize, EnlistedRed);
                            break;
                        case MilitaryRank.Cavus:
                            AddSymbol(row, Symbol.Chevron, symbolSize, EnlistedRed);
                            AddSymbol(row, Symbol.Chevron, symbolSize, EnlistedRed);
                            break;
                        case MilitaryRank.SozlesmeliEr:
                            AddSymbol(row, Symbol.Bar, symbolSize, EnlistedRed);
                            break;
                        default:
                            // Er: işaretsiz apolet, yalnızca ince şerit.
                            AddSymbol(row, Symbol.Bar, symbolSize, UiTheme.WithAlpha(UiTheme.TextMuted, 0.6f));
                            break;
                    }

                    break;
            }
        }

        /// <summary>Rütbe kategorisine göre vurgu rengi (subay/astsubay altın, uzman haki, erbaş kırmızı).</summary>
        public static Color CategoryColor(MilitaryRank rank)
        {
            switch (RankCatalog.GetCategoryKind(rank))
            {
                case RankCategory.Officer:
                case RankCategory.NonCommissioned:
                    return Gold;
                case RankCategory.Specialist:
                    return UiTheme.Khaki;
                default:
                    return UiTheme.TextDim;
            }
        }

        private static void BuildOfficer(Transform row, MilitaryRank rank, float size)
        {
            switch (rank)
            {
                case MilitaryRank.Astegmen:
                    AddSymbol(row, Symbol.SmallStar, size, Silver);
                    break;
                case MilitaryRank.Tegmen:
                    AddSymbol(row, Symbol.Star, size, Gold);
                    break;
                case MilitaryRank.Ustegmen:
                    AddSymbol(row, Symbol.Star, size, Gold);
                    AddSymbol(row, Symbol.Star, size, Gold);
                    break;
                case MilitaryRank.Yuzbasi:
                    AddSymbol(row, Symbol.Star, size, Gold);
                    AddSymbol(row, Symbol.Star, size, Gold);
                    AddSymbol(row, Symbol.Star, size, Gold);
                    break;
                default:
                {
                    // Binbaşı / Yarbay / Albay: palamut (altın çubuk) + 1/2/3 yıldız.
                    var stars = Mathf.Clamp((int)rank - (int)MilitaryRank.Yuzbasi, 1, 3);
                    AddSymbol(row, Symbol.Wreath, size, Gold);
                    for (var i = 0; i < stars; i++)
                        AddSymbol(row, Symbol.Star, size, Gold);
                    break;
                }
            }
        }

        private static void AddSymbol(Transform row, Symbol symbol, float size, Color color)
        {
            Sprite sprite;
            float width;
            float height;
            switch (symbol)
            {
                case Symbol.SmallStar:
                    sprite = UiSprites.Star;
                    width = height = size * 0.75f;
                    break;
                case Symbol.Chevron:
                    sprite = UiSprites.Chevron;
                    width = size * 0.9f;
                    height = size;
                    break;
                case Symbol.Bar:
                    sprite = UiSprites.GetRoundedRect(2);
                    width = size * 0.22f;
                    height = size * 0.95f;
                    break;
                case Symbol.Wreath:
                    sprite = UiSprites.GetRoundedRect(3);
                    width = size * 0.34f;
                    height = size;
                    break;
                default:
                    sprite = UiSprites.Star;
                    width = height = size;
                    break;
            }

            var image = UiFactory.Image(row, sprite, color);
            image.gameObject.name = symbol.ToString();
            image.preserveAspect = symbol == Symbol.Star || symbol == Symbol.SmallStar || symbol == Symbol.Chevron;
            UiFactory.LayoutSize(image, width, height);
            UiFactory.AddShadow(image, UiTheme.TextShadow, new Vector2(1f, -1f));
        }
    }
}
