using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Localization;
using Project.Presentation.Bootstrap;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Kariyer penceresi: rütbe apoleti ve adı (TSK rütbe sınıfıyla), tecrübe puanı (TP) ve sonraki rütbeye ilerleme
    /// çubuğu, harekât istatistikleri (harekât, zafer, etkisiz bırakma, kafadan isabet, hasar, en iyi sıralama, en uzun
    /// hayatta kalma) ve tüm rütbe basamaklarının TP eşikleri. "KARİYERİ SIFIRLA" onaylı çalışır. Kariyer değişince
    /// kendini yeniler; kapanınca kendini yok eder.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class CareerPanel : MonoBehaviour
    {
        private const float WindowWidth = 1400f;
        private const float WindowHeight = 830f;
        private const float LadderRowHeight = 42f;

        private sealed class LadderRow
        {
            public MilitaryRank Rank;
            public Image Background;
            public Text Name;
            public Text Experience;
            public Image Marker;
        }

        private CareerStatsService _career;
        private Action _onClose;
        private bool _closed;
        private CanvasGroup _group;
        private float _fade;
        private MenuDialog _dialog;

        private RectTransform _window;
        private RectTransform _insignia;
        private MilitaryRank _shownRank = (MilitaryRank)(-1);
        private Text _rankName;
        private Text _rankCategory;
        private Text _playerName;
        private Text _experience;
        private UiProgressBar _progress;
        private Text _nextRank;
        private ScrollRect _ladderScroll;
        private AchievementListView _achievements;
        private bool _showAchievements;
        private readonly List<LadderRow> _ladder = new List<LadderRow>(24);
        private readonly Dictionary<string, Text> _stats = new Dictionary<string, Text>(12);
        private Button _backButton;
        private bool _scrollPending = true;

        /// <summary>Pencere açık mı?</summary>
        public bool IsOpen => !_closed;

        /// <summary>Üzerinde onay penceresi açık mı?</summary>
        public bool HasDialog => _dialog != null && _dialog.IsOpen;

        /// <summary>
        /// Kariyer penceresini oluşturur (ebeveyni doldurur, arkasını karartır).
        /// </summary>
        /// <param name="parent">Tuval veya tam ekran kök.</param>
        /// <param name="career">Kariyer servisi; null ise <see cref="GameSession.Career"/>.</param>
        /// <param name="onClose">Pencere kapanınca çağrılır.</param>
        public static CareerPanel Create(Transform parent, CareerStatsService career, Action onClose)
        {
            var root = UiFactory.CreateRect("[Kariyer]", parent);
            root.SetAsLastSibling();

            var dim = root.gameObject.AddComponent<Image>();
            dim.color = UiTheme.WithAlpha(UiTheme.Overlay, 0.55f);
            dim.raycastTarget = true;

            var panel = root.gameObject.AddComponent<CareerPanel>();
            panel._career = ResolveCareer(career);
            panel._onClose = onClose;
            panel._group = UiFactory.EnsureCanvasGroup(root);
            panel._group.alpha = 0f;
            panel.Build(root);
            panel.Refresh();

            if (panel._career != null)
                panel._career.Changed += panel.OnCareerChanged;
            panel.AttachDeep();
            return panel;
        }

        /// <summary>Pencereyi kapatır (açık onay penceresi de kapanır).</summary>
        public void Close()
        {
            if (_closed)
                return;

            _closed = true;
            if (_dialog != null)
            {
                _dialog.Dismiss();
                _dialog = null;
            }

            Unsubscribe();
            ClearSelection();
            var callback = _onClose;
            _onClose = null;

            gameObject.SetActive(false);
            UiFactory.DestroySafe(gameObject);

            if (callback == null)
                return;
            try
            {
                callback();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>Esc: önce onay penceresini, sonra pencereyi kapatır.</summary>
        public void Back()
        {
            if (_dialog != null && _dialog.IsOpen)
            {
                _dialog.Cancel();
                return;
            }

            if (DeepVisible)
            {
                SetDeepVisible(false);
                return;
            }

            Close();
        }

        /// <summary>Tüm görünümü kariyer verisinden yeniler.</summary>
        public void Refresh()
        {
            var stats = _career != null && _career.Current != null ? _career.Current : new CareerStats();
            var xp = Mathf.Max(0, stats.Experience);
            var rank = RankCatalog.RankForExperience(xp);

            // Rütbe.
            if (_insignia != null && rank != _shownRank)
                MenuRankInsignia.Rebuild(_insignia, rank);
            _shownRank = rank;

            if (_rankName != null)
            {
                _rankName.text = MenuText.ToUpperTr(RankCatalog.GetName(rank));
                _rankName.color = MenuRankInsignia.CategoryColor(rank) == UiTheme.TextDim ? UiTheme.Text : MenuRankInsignia.CategoryColor(rank);
            }

            if (_rankCategory != null)
                _rankCategory.text = RankCatalog.GetCategory(rank) + "  ·  " + RankCatalog.GetShortName(rank);

            if (_playerName != null)
                _playerName.text = RankCatalog.FormatName(rank, PlayerName());

            // Tecrübe.
            if (_experience != null)
                _experience.text = MenuText.FormatThousands(xp) + " TP";

            var hasNext = RankCatalog.TryGetNextRank(rank, out var next);
            var progress = RankCatalog.ProgressToNextRank(xp);
            if (_progress != null)
            {
                _progress.SetValue(progress, true);
                _progress.SetLabel(hasNext ? MenuText.FormatPercent(progress) : Loc.Get("career.max_rank", "EN YÜKSEK RÜTBE"), UiTheme.FontTiny);
            }

            if (_nextRank != null)
            {
                _nextRank.text = hasNext
                    ? Loc.Format("career.next_rank", "Sonraki rütbe: {0}  —  {1} TP kaldı", RankCatalog.GetName(next), MenuText.FormatThousands(RankCatalog.ExperienceToNextRank(xp)))
                    : Loc.Get("career.max_rank_msg", "En yüksek rütbeye ulaştın. Tebrikler komutanım!");
            }

            // İstatistikler.
            var winRate = stats.Matches > 0 ? stats.Wins / (float)stats.Matches : 0f;
            var headshotRate = stats.Kills > 0 ? stats.Headshots / (float)stats.Kills : 0f;
            var damagePerMatch = stats.Matches > 0 ? stats.TotalDamage / stats.Matches : 0f;
            SetStat("matches", MenuText.FormatThousands(stats.Matches));
            SetStat("wins", MenuText.FormatThousands(stats.Wins));
            SetStat("winRate", MenuText.FormatPercent(winRate, true));
            SetStat("kills", MenuText.FormatThousands(stats.Kills));
            SetStat("headshots", MenuText.FormatThousands(stats.Headshots));
            SetStat("headshotRate", MenuText.FormatPercent(headshotRate, true));
            SetStat("damage", MenuText.FormatDamage(stats.TotalDamage));
            SetStat("damagePerMatch", MenuText.FormatDamage(damagePerMatch));
            SetStat("best", stats.BestPlacement > 0 ? "#" + stats.BestPlacement : "—");
            SetStat("survival", MenuText.FormatDuration(stats.LongestSurvivalSeconds));

            // Rütbe basamakları.
            for (var i = 0; i < _ladder.Count; i++)
            {
                var row = _ladder[i];
                var current = row.Rank == rank;
                var achieved = (int)row.Rank <= (int)rank;
                if (row.Background != null)
                    row.Background.color = current ? UiTheme.WithAlpha(UiTheme.AccentDark, 0.55f) : achieved ? UiTheme.WithAlpha(UiTheme.PanelLight, 0.55f) : new Color(0f, 0f, 0f, 0.18f);
                if (row.Name != null)
                    row.Name.color = current ? UiTheme.Text : achieved ? UiTheme.TextDim : UiTheme.TextMuted;
                if (row.Experience != null)
                    row.Experience.color = current ? UiTheme.Amber : achieved ? UiTheme.Khaki : UiTheme.TextMuted;
                if (row.Marker != null)
                    row.Marker.enabled = current;
            }

            _scrollPending = true;
            RefreshDeep();
        }

        private static CareerStatsService ResolveCareer(CareerStatsService career)
        {
            if (career != null)
                return career;
            try
            {
                GameSession.EnsureInitialized();
                return GameSession.Career;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        private static string PlayerName()
        {
            try
            {
                var settings = GameSession.Settings;
                if (settings != null && settings.Current != null)
                    return SettingsService.SanitizeName(settings.Current.PlayerName);
            }
            catch (Exception)
            {
                // Varsayılan ad.
            }

            return SettingsService.DefaultPlayerName;
        }

        // ------------------------------------------------------------------ Kurulum

        private void Build(RectTransform root)
        {
            var window = UiFactory.Panel(root, UiTheme.Panel, UiSprites.ChamferRect);
            window.gameObject.name = "Window";
            UiFactory.Anchor(window, UiAnchor.Center, Vector2.zero, new Vector2(WindowWidth, WindowHeight));
            _window = window;

            var border = UiFactory.Image(window, UiSprites.GetRoundedRectOutline(UiTheme.CornerRadius), UiTheme.PanelBorder);
            UiFactory.Stretch(border);

            var stripe = UiFactory.Image(window, null, MenuRankInsignia.Gold);
            stripe.gameObject.name = "Stripe";
            UiFactory.SetRect(stripe, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f), Vector2.zero);

            var title = UiFactory.Label(window, Loc.Get("career.title", "KARİYER"), UiTheme.FontTitle, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(title, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -96f), new Vector2(-40f, -22f));
            UiFactory.AddShadow(title, UiTheme.TextShadow, new Vector2(2f, -2f));

            var subtitle = UiFactory.Label(window, Loc.Get("career.subtitle", "HAREKÂT KAYDI  ·  KUZGUN VADİSİ"), UiTheme.FontNormal, TextAnchor.MiddleRight, UiTheme.Khaki, FontStyle.Bold);
            subtitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(subtitle, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -96f), new Vector2(-40f, -22f));

            BuildRankCard(window);
            BuildStats(window);
            BuildLadder(window);
            _achievements = AchievementListView.Create(window, GameSession.Achievements);
            BuildDeep(window);

            // Alt çubuk.
            var rules = UiFactory.Label(window,
                Loc.Format("career.xp_rules",
                    "TP kazanımı:  etkisiz bırakma +{0}  ·  kafadan isabet +{1}  ·  geride bırakılan her tim +{2}  ·  zafer +{3}",
                    CareerStatsService.ExperiencePerKill,
                    CareerStatsService.ExperiencePerHeadshot,
                    CareerStatsService.ExperiencePerTeamOutlasted,
                    MenuText.FormatThousands(CareerStatsService.ExperienceForWin)),
                UiTheme.FontTiny, TextAnchor.MiddleLeft, UiTheme.TextMuted);
            rules.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(rules, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 26f), new Vector2(-830f, 26f + UiTheme.ButtonHeight));

            var row = UiFactory.HorizontalList(window, 14f, 0, TextAnchor.MiddleRight);
            row.gameObject.name = "Buttons";
            UiFactory.SetRect(row, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-810f, 26f), new Vector2(-40f, 26f + UiTheme.ButtonHeight));
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;

            UiFactory.FlexibleSpacer(row);
            var deep = UiFactory.Button(row, Loc.Get("career.btn.deep", "DERİN KARİYER"), () => SetDeepVisible(!DeepVisible), UiButtonStyle.Default);
            UiFactory.LayoutSize(deep, 230f, UiTheme.ButtonHeight);
            var toggle = UiFactory.Button(row, Loc.Get("career.btn.achievements", "BAŞARIMLAR"), ToggleAchievements, UiButtonStyle.Ghost);
            UiFactory.LayoutSize(toggle, 190f, UiTheme.ButtonHeight);
            var reset = UiFactory.Button(row, Loc.Get("career.btn.reset", "KARİYERİ SIFIRLA"), ConfirmReset, UiButtonStyle.Ghost);
            UiFactory.LayoutSize(reset, 230f, UiTheme.ButtonHeight);
            _backButton = UiFactory.Button(row, Loc.Get("career.btn.back", "GERİ"), Close, UiButtonStyle.Default);
            UiFactory.LayoutSize(_backButton, 140f, UiTheme.ButtonHeight);
        }

        private void BuildRankCard(RectTransform window)
        {
            var card = UiFactory.Panel(window, UiTheme.PanelDark, UiSprites.ChamferRect);
            card.gameObject.name = "RankCard";
            UiFactory.SetRect(card, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(40f, 110f), new Vector2(40f + 440f, -116f));
            var cardBorder = UiFactory.Image(card, UiSprites.GetRoundedRectOutline(UiTheme.CornerRadius), UiTheme.WithAlpha(MenuRankInsignia.Gold, 0.45f));
            UiFactory.Stretch(cardBorder);

            var caption = UiFactory.Label(card, Loc.Get("career.rank", "RÜTBE"), UiTheme.FontSmall, TextAnchor.MiddleCenter, UiTheme.Khaki, FontStyle.Bold);
            UiFactory.SetRect(caption, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -56f), new Vector2(0f, -20f));

            var holder = UiFactory.CreateRect("InsigniaHolder", card);
            UiFactory.Anchor(holder, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(260f, 100f));
            _insignia = MenuRankInsignia.Create(holder, MilitaryRank.Er, 92f);

            _rankName = UiFactory.Label(card, string.Empty, UiTheme.FontLarge, TextAnchor.MiddleCenter, UiTheme.Text, FontStyle.Bold);
            _rankName.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_rankName, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -226f), new Vector2(-16f, -180f));
            UiFactory.AddShadow(_rankName, UiTheme.TextShadow, new Vector2(2f, -2f));

            _rankCategory = UiFactory.Label(card, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleCenter, UiTheme.TextDim);
            UiFactory.SetRect(_rankCategory, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -258f), new Vector2(-16f, -228f));

            _playerName = UiFactory.Label(card, string.Empty, UiTheme.FontMedium, TextAnchor.MiddleCenter, UiTheme.Text);
            _playerName.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_playerName, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -304f), new Vector2(-16f, -266f));

            var divider = UiFactory.Image(card, null, UiTheme.WithAlpha(UiTheme.PanelBorder, 0.8f));
            UiFactory.SetRect(divider, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -324f), new Vector2(-30f, -322f));

            var xpCaption = UiFactory.Label(card, Loc.Get("career.xp", "TECRÜBE PUANI"), UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.Khaki, FontStyle.Bold);
            UiFactory.SetRect(xpCaption, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -372f), new Vector2(-30f, -340f));

            _experience = UiFactory.Label(card, string.Empty, UiTheme.FontLarge, TextAnchor.MiddleRight, UiTheme.Amber, FontStyle.Bold);
            _experience.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_experience, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -380f), new Vector2(-30f, -334f));

            _progress = UiFactory.ProgressBar(card, MenuRankInsignia.Gold, UiTheme.Track);
            _progress.TrailEnabled = false;
            UiFactory.SetRect(_progress, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -414f), new Vector2(-30f, -392f));
            _progress.SetSegments(10, UiTheme.WithAlpha(Color.black, 0.35f), 2f);

            _nextRank = UiFactory.Label(card, string.Empty, UiTheme.FontSmall, TextAnchor.UpperLeft, UiTheme.TextDim);
            UiFactory.SetRect(_nextRank, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(30f, 20f), new Vector2(-30f, -426f));
        }

        private void BuildStats(RectTransform window)
        {
            var column = UiFactory.VerticalList(window, 6f);
            column.gameObject.name = "Stats";
            UiFactory.SetRect(column, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(520f, 110f), new Vector2(520f + 400f, -116f));

            UiWidgets.Header(column, Loc.Get("career.stats", "İSTATİSTİKLER"), UiTheme.FontMedium);
            UiFactory.Spacer(column, 4f);
            AddStat(column, "matches", Loc.Get("career.stat.matches", "Harekât"));
            AddStat(column, "wins", Loc.Get("career.stat.wins", "Zafer"));
            AddStat(column, "winRate", Loc.Get("career.stat.win_rate", "Zafer oranı"));
            UiFactory.Divider(column, UiTheme.WithAlpha(UiTheme.PanelBorder, 0.6f));
            AddStat(column, "kills", Loc.Get("career.stat.kills", "Etkisiz bırakma"));
            AddStat(column, "headshots", Loc.Get("career.stat.headshots", "Kafadan isabet"));
            AddStat(column, "headshotRate", Loc.Get("career.stat.headshot_rate", "Kafadan isabet oranı"));
            UiFactory.Divider(column, UiTheme.WithAlpha(UiTheme.PanelBorder, 0.6f));
            AddStat(column, "damage", Loc.Get("career.stat.damage", "Toplam hasar"));
            AddStat(column, "damagePerMatch", Loc.Get("career.stat.damage_per_match", "Harekât başına hasar"));
            AddStat(column, "best", Loc.Get("career.stat.best", "En iyi tim sıralaması"));
            AddStat(column, "survival", Loc.Get("career.stat.survival", "En uzun hayatta kalma"));
        }

        private void AddStat(Transform parent, string key, string label)
        {
            UiWidgets.KeyValueRow(parent, label, "—", out var value);
            _stats[key] = value;
        }

        private void SetStat(string key, string value)
        {
            if (_stats.TryGetValue(key, out var text) && text != null)
                text.text = value;
        }

        private void BuildLadder(RectTransform window)
        {
            var header = UiFactory.CreateRect("LadderHeader", window);
            UiFactory.SetRect(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(960f, -164f), new Vector2(-40f, -116f));
            var headerList = UiFactory.VerticalList(header, 0f);
            UiWidgets.Header(headerList, Loc.Get("career.ladder", "RÜTBE BASAMAKLARI"), UiTheme.FontMedium);

            _ladderScroll = UiWidgets.ScrollList(window, out var content, 4f, 0);
            UiFactory.SetRect(_ladderScroll, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(960f, 110f), new Vector2(-30f, -172f));

            // Kıdemliden küçüğe: en üstte Albay.
            var ranks = RankCatalog.All;
            for (var i = ranks.Count - 1; i >= 0; i--)
            {
                var rank = ranks[i];
                var rowRect = UiFactory.CreateRect("Rank_" + rank, content);
                UiFactory.LayoutSize(rowRect, -1f, LadderRowHeight, 1f);

                var background = rowRect.gameObject.AddComponent<Image>();
                background.sprite = UiSprites.GetRoundedRect(4);
                background.type = Image.Type.Sliced;
                background.color = new Color(0f, 0f, 0f, 0.18f);
                background.raycastTarget = true;   // Kaydırma için.

                var marker = UiFactory.Image(rowRect, null, UiTheme.Accent);
                UiFactory.SetRect(marker, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 4f), new Vector2(4f, -4f));

                var iconHolder = UiFactory.CreateRect("Icon", rowRect);
                UiFactory.Anchor(iconHolder, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(72f, 28f));
                var icon = MenuRankInsignia.Create(iconHolder, rank, 26f);
                UiFactory.Anchor(icon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(26f * 2.6f, 26f));

                var name = UiFactory.Label(rowRect, RankCatalog.GetName(rank), UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.TextDim, FontStyle.Bold);
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiFactory.SetRect(name, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(96f, 0f), new Vector2(-110f, 0f));

                var xp = UiFactory.Label(rowRect, MenuText.FormatThousands(RankCatalog.RequiredExperience(rank)) + " TP", UiTheme.FontSmall,
                    TextAnchor.MiddleRight, UiTheme.TextMuted);
                xp.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiFactory.SetRect(xp, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(96f, 0f), new Vector2(-12f, 0f));

                _ladder.Add(new LadderRow { Rank = rank, Background = background, Name = name, Experience = xp, Marker = marker });
            }
        }

        // ------------------------------------------------------------------ Eylemler

        private void ToggleAchievements()
        {
            _showAchievements = !_showAchievements;
            if (_achievements != null)
                _achievements.SetVisible(_showAchievements);
            if (_ladderScroll != null)
                _ladderScroll.gameObject.SetActive(!_showAchievements);
            var ladderHeader = _window != null ? _window.Find("LadderHeader") : null;
            if (ladderHeader != null)
                ladderHeader.gameObject.SetActive(!_showAchievements);
        }

        private void ConfirmReset()
        {
            if (_dialog != null || _career == null)
                return;

            _dialog = MenuDialog.Show(transform,
                Loc.Get("career.reset.title", "Kariyeri sıfırla"),
                Loc.Get("career.reset.body", "Tüm harekât kayıtların, tecrübe puanın ve rütben silinecek. Bu işlem geri alınamaz."),
                Loc.Get("career.reset.confirm", "SIFIRLA"), DoReset,
                Loc.Get("career.reset.cancel", "VAZGEÇ"), OnDialogClosed, true);
        }

        private void DoReset()
        {
            _dialog = null;
            try
            {
                _career?.ResetCareer();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            Refresh();
            UiFactory.Select(_backButton);
        }

        private void OnDialogClosed()
        {
            _dialog = null;
            UiFactory.Select(_backButton);
        }

        private void OnCareerChanged(CareerStats stats)
        {
            if (!_closed)
                Refresh();
        }

        // ------------------------------------------------------------------ Yaşam döngüsü

        private void Start()
        {
            UiFactory.Select(_backButton);
        }

        private void Update()
        {
            if (_group != null && _fade < 1f)
            {
                _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / UiTheme.FadeDuration);
                _group.alpha = _fade;
            }

            if (_scrollPending)
            {
                _scrollPending = false;
                ScrollToCurrentRank();
            }
        }

        private void ScrollToCurrentRank()
        {
            if (_ladderScroll == null || _ladder.Count < 2)
                return;

            var index = 0;
            for (var i = 0; i < _ladder.Count; i++)
            {
                if (_ladder[i].Rank == _shownRank)
                {
                    index = i;
                    break;
                }
            }

            // İçerik yüksekliği (ContentSizeFitter) hesaplanmış olsun.
            Canvas.ForceUpdateCanvases();

            // Satır listesi yukarıdan aşağı; normalize konum 1 = en üst.
            _ladderScroll.verticalNormalizedPosition = Mathf.Clamp01(1f - index / (float)(_ladder.Count - 1));
        }

        private void Unsubscribe()
        {
            DetachDeep();
            if (_career != null)
                _career.Changed -= OnCareerChanged;
        }

        private void ClearSelection()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != null &&
                eventSystem.currentSelectedGameObject.transform.IsChildOf(transform))
                eventSystem.SetSelectedGameObject(null);
        }

        private void OnDestroy()
        {
            _closed = true;
            Unsubscribe();
            _onClose = null;
        }
    }
}
