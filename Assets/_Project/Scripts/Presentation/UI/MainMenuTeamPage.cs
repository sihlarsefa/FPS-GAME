using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Presentation.Bootstrap;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>Sayıyı 0'dan hedefe yumuşakça sayar (sayfa her açıldığında yeniden başlar).</summary>
    [DisallowMultipleComponent]
    public sealed class MenuCountUp : MonoBehaviour
    {
        private const float Duration = 0.9f;
        private Text _text;
        private int _target;
        private float _time;
        private Func<int, string> _format = v => MenuText.FormatThousands(v);

        public static MenuCountUp Attach(Text text, Func<int, string> format = null)
        {
            var c = text.gameObject.AddComponent<MenuCountUp>();
            c._text = text;
            if (format != null)
                c._format = format;
            return c;
        }

        public void SetTarget(int target)
        {
            _target = Mathf.Max(0, target);
            _time = 0f;
            Push(0f);
        }

        private void OnEnable() => _time = 0f;

        private void Update()
        {
            if (_text == null || _time >= Duration)
                return;
            _time += Time.unscaledDeltaTime;
            Push(Mathf.Clamp01(_time / Duration));
        }

        private void Push(float t) => _text.text = _format(MainMenuMotion.CountUp(_target, t));
    }

    /// <summary>
    /// TİM sayfası: rütbe apoleti ve ilerleme çubuğu, kariyer istatistik kutuları (sayaç animasyonlu) ve görev rolü seçici
    /// (rolün başlangıç teçhizatı gösterilir; seçim LoadoutSelection'a kaydedilir).
    /// </summary>
    public sealed class MenuTeamPage : MonoBehaviour
    {
        private static readonly TeamRole[] Roles =
        {
            TeamRole.Leader, TeamRole.Rifleman, TeamRole.Marksman, TeamRole.MachineGunner, TeamRole.Medic, TeamRole.Radioman, TeamRole.Grenadier
        };

        private RectTransform _insigniaHolder;
        private RectTransform _insignia;
        private MilitaryRank _shownRank = (MilitaryRank)(-1);
        private Text _name;
        private Text _rankLine;
        private UiProgressBar _bar;
        private Text _xpText;
        private readonly List<MenuNavItem> _roleItems = new List<MenuNavItem>(7);
        private Text _roleTitle;
        private Text _roleDesc;
        private Text _roleKit;
        private readonly List<(MenuCountUp count, Func<CareerStats, int> value)> _counters = new List<(MenuCountUp, Func<CareerStats, int>)>();
        private Text _kdText;
        private MainMenuController _menu;
        private SoldierPreviewCard _previewCard;

        public static string RoleDescription(TeamRole role)
        {
            switch (role)
            {
                case TeamRole.Leader: return "Timi yönlendirir; işaret ve emir komutasını en iyi kullanan rol. Dengeli teçhizat.";
                case TeamRole.Marksman: return "Uzak mesafe baskısı ve gözetleme. Keskin nişancı tüfeği, sakin ve sabırlı oyun.";
                case TeamRole.MachineGunner: return "Bastırma ateşi: yüksek şarjör kapasitesi ile tim ilerlerken düşmanı yerine mıhlar.";
                case TeamRole.Medic: return "Yaralıları kaldırır, tim sağlığını ayakta tutar. Fazla tıbbi malzeme taşır.";
                case TeamRole.Radioman: return "Topçu desteği çağırır, tim iletişimini taşır. Hareketli ve esnek.";
                case TeamRole.Grenadier: return "Siper temizler: fazla el bombası ve sis ile kapalı alanlarda öne çıkar.";
                default: return "Tim omurgası: çok yönlü piyade, her durumda hazır.";
            }
        }

        public static MenuTeamPage Create(RectTransform page, MainMenuController menu)
        {
            var host = page.gameObject.AddComponent<MenuTeamPage>();
            host._menu = menu;
            host.Build(page);
            return host;
        }

        private void Build(RectTransform page)
        {
            var title = UiFactory.Label(page, "TİM", 56, TextAnchor.UpperLeft, UiTheme.Text, FontStyle.Bold);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(title, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -70f), new Vector2(-300f, 0f));
            UiFactory.AddShadow(title, UiTheme.TextShadow, new Vector2(3f, -3f));
            var line = UiFactory.Image(page, null, UiTheme.Accent);
            UiFactory.SetRect(line, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, -80f), new Vector2(180f, -76f));

            var detail = UiFactory.Button(page, "AYRINTILI KARİYER  ›", () => { if (_menu != null) _menu.OpenCareer(); }, UiButtonStyle.Default);
            UiFactory.Anchor(detail, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -40f), new Vector2(340f, 50f));
            var detailLabel = UiFactory.GetButtonLabel(detail);
            if (detailLabel != null)
                detailLabel.fontSize = UiTheme.FontNormal;

            BuildRankBlock(page);
            BuildStats(page);
            BuildRoles(page);
        }

        private void BuildRankBlock(RectTransform page)
        {
            var card = UiFactory.Panel(page, UiTheme.PanelDark, UiSprites.ChamferRect);
            UiFactory.SetRect(card, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(-12f, -100f));

            _insigniaHolder = UiFactory.CreateRect("Insignia", card);
            UiFactory.Anchor(_insigniaHolder, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 8f), new Vector2(250f, 96f));
            _insignia = MenuRankInsignia.Create(_insigniaHolder, MilitaryRank.Er, 92f);

            _name = UiFactory.Label(card, string.Empty, UiTheme.FontLarge, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold);
            _name.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_name, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(290f, -58f), new Vector2(-16f, -14f));

            _rankLine = UiFactory.Label(card, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.Khaki, FontStyle.Bold);
            _rankLine.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_rankLine, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(290f, -92f), new Vector2(-16f, -58f));

            _bar = UiFactory.ProgressBar(card, MenuRankInsignia.Gold, UiTheme.Track);
            _bar.TrailEnabled = false;
            UiFactory.SetRect(_bar, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(290f, 40f), new Vector2(-24f, 56f));

            _xpText = UiFactory.Label(card, string.Empty, UiTheme.FontTiny, TextAnchor.MiddleLeft, UiTheme.TextDim);
            _xpText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_xpText, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(290f, 14f), new Vector2(-16f, 38f));
        }

        private void BuildStats(RectTransform page)
        {
            var header = UiFactory.Label(page, "KARİYER İSTATİSTİKLERİ", UiTheme.FontSmall, TextAnchor.UpperLeft, UiTheme.TextMuted, FontStyle.Bold);
            UiFactory.SetRect(header, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -286f), new Vector2(-12f, -260f));

            var grid = UiFactory.CreateRect("Stats", page);
            UiFactory.SetRect(grid, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(-12f, -296f));
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(178f, 104f);
            layout.spacing = new Vector2(10f, 10f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.childAlignment = TextAnchor.UpperLeft;

            AddTile(grid, "MAÇ", s => s.Matches);
            AddTile(grid, "ZAFER", s => s.Wins);
            AddTile(grid, "ETKİSİZ", s => s.Kills);
            AddTile(grid, "KAFADAN", s => s.Headshots);
            AddTile(grid, "TOPLAM HASAR", s => Mathf.RoundToInt(s.TotalDamage));
            AddTile(grid, "EN UZUN HAYATTA", s => Mathf.RoundToInt(s.LongestSurvivalSeconds), v => MenuText.FormatDuration(v));
            AddTile(grid, "EN İYİ SIRA", s => s.BestPlacement, v => v > 0 ? "#" + v : "—");
            var kd = AddTile(grid, "ETKİSİZ / MAÇ", s => 0, null, true);
            _kdText = kd;
        }

        private Text AddTile(RectTransform parent, string caption, Func<CareerStats, int> value, Func<int, string> format = null, bool staticText = false)
        {
            var tile = UiFactory.Panel(parent, UiTheme.WithAlpha(UiTheme.PanelDark, 0.9f), UiSprites.ChamferRect);
            tile.gameObject.name = "Tile_" + caption;

            var accent = UiFactory.Image(tile, null, UiTheme.WithAlpha(MenuRankInsignia.Gold, 0.8f));
            UiFactory.SetRect(accent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 10f), new Vector2(4f, -10f));

            var cap = UiFactory.Label(tile, caption, UiTheme.FontTiny, TextAnchor.UpperLeft, UiTheme.TextMuted, FontStyle.Bold);
            cap.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(cap, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -34f), new Vector2(-6f, -10f));

            var number = UiFactory.Label(tile, "0", 40, TextAnchor.LowerLeft, UiTheme.Text, FontStyle.Bold);
            number.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(number, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(16f, 10f), new Vector2(-6f, -34f));
            UiFactory.AddShadow(number, UiTheme.TextShadow, new Vector2(2f, -2f));

            if (!staticText)
                _counters.Add((MenuCountUp.Attach(number, format), value));
            return number;
        }

        private void BuildRoles(RectTransform page)
        {
            var header = UiFactory.Label(page, "GÖREV ROLÜ", UiTheme.FontSmall, TextAnchor.UpperLeft, UiTheme.TextMuted, FontStyle.Bold);
            UiFactory.SetRect(header, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(12f, -128f), new Vector2(0f, -100f));

            var list = UiFactory.VerticalList(page, 2f);
            UiFactory.SetRect(list, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(12f, -128f - 7 * 46f), new Vector2(292f, -132f));
            var vlg = list.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.childControlHeight = true;
                vlg.childForceExpandHeight = false;
            }

            for (var i = 0; i < Roles.Length; i++)
            {
                var role = Roles[i];
                var item = MenuNavItem.Create(list, MenuText.ToUpperTr(LoadoutCatalog.GetRoleName(role)), 26, 44f, UiTheme.TextDim, () => SelectRole(role));
                _roleItems.Add(item);
            }

            // Etkileşimli asker önizlemesi: rol listesinin sağında, rol panelinin üstünde.
            var cardHost = UiFactory.CreateRect("SoldierPreviewHost", page);
            UiFactory.SetRect(cardHost, new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(308f, 198f), new Vector2(0f, -100f));
            _previewCard = SoldierPreviewCard.Create(cardHost);
            UiFactory.SetRect(_previewCard.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var panel = UiFactory.Panel(page, UiTheme.WithAlpha(UiTheme.PanelDark, 0.9f), UiSprites.ChamferRect);
            UiFactory.SetRect(panel, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(12f, 0f), new Vector2(0f, 186f));

            _roleTitle = UiFactory.Label(panel, string.Empty, UiTheme.FontMedium, TextAnchor.UpperLeft, UiTheme.Text, FontStyle.Bold);
            _roleTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_roleTitle, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -44f), new Vector2(-14f, -10f));
            _roleDesc = UiFactory.Label(panel, string.Empty, UiTheme.FontSmall, TextAnchor.UpperLeft, UiTheme.TextDim);
            UiFactory.SetRect(_roleDesc, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(18f, 56f), new Vector2(-14f, -46f));
            _roleKit = UiFactory.Label(panel, string.Empty, UiTheme.FontSmall, TextAnchor.LowerLeft, UiTheme.Khaki, FontStyle.Bold);
            UiFactory.SetRect(_roleKit, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(18f, 10f), new Vector2(-14f, 52f));
        }

        private void SelectRole(TeamRole role)
        {
            LoadoutSelection.Shared.Role = role;
            RefreshRole();
            if (_previewCard != null && _previewCard.Preview != null)
                _previewCard.Preview.RebuildModel();
        }

        private void RefreshRole()
        {
            var role = LoadoutSelection.Shared.Role;
            for (var i = 0; i < _roleItems.Count; i++)
                _roleItems[i].Active = Roles[i] == role;

            var kit = LoadoutCatalog.For(role);
            _roleTitle.text = MenuText.ToUpperTr(kit.RoleName);
            _roleDesc.text = RoleDescription(role);
            var primary = WeaponCatalog.TryGet(kit.PrimaryWeaponId, out var p) ? p.DisplayName : kit.PrimaryWeaponId;
            var side = WeaponCatalog.TryGet(kit.SidearmId, out var s) ? s.DisplayName : kit.SidearmId;
            _roleKit.text = "BİRİNCİL  " + primary + "    TABANCA  " + side + "\nYELEK " + kit.VestLevel + "  ·  KASK " + kit.HelmetLevel + "  ·  SIRT ÇANTASI " + kit.BackpackLevel;
        }

        /// <summary>Sayfa her gösterildiğinde verileri tazeler ve sayaçları yeniden başlatır.</summary>
        public void Refresh()
        {
            var career = GameSession.Career;
            var stats = career != null && career.Current != null ? career.Current : new CareerStats();
            var xp = Mathf.Max(0, stats.Experience);
            var rank = RankCatalog.RankForExperience(xp);
            if (rank != _shownRank)
            {
                MenuRankInsignia.Rebuild(_insignia, rank);
                _shownRank = rank;
            }

            var name = SettingsService.DefaultPlayerName;
            var settings = GameSession.Settings;
            if (settings != null && settings.Current != null)
                name = SettingsService.SanitizeName(settings.Current.PlayerName);
            _name.text = RankCatalog.FormatName(rank, name);
            _rankLine.text = RankCatalog.GetName(rank) + "  ·  " + RankCatalog.GetCategory(rank);
            _bar.SetValue(0f, true);
            _bar.SetValue(RankCatalog.ProgressToNextRank(xp), false);
            _xpText.text = RankCatalog.TryGetNextRank(rank, out var next)
                ? MenuText.FormatThousands(xp) + " / " + MenuText.FormatThousands(RankCatalog.RequiredExperience(next)) + " TP  ·  sonraki: " + RankCatalog.GetShortName(next)
                : MenuText.FormatThousands(xp) + " TP  ·  en yüksek rütbe";

            for (var i = 0; i < _counters.Count; i++)
                _counters[i].count.SetTarget(_counters[i].value(stats));
            if (_kdText != null)
                _kdText.text = (stats.Kills / (float)Mathf.Max(1, stats.Matches)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            RefreshRole();
            if (_previewCard != null)
                _previewCard.SetIdentity(rank, _name.text);
        }

        private void OnEnable() => Refresh();
    }
}
