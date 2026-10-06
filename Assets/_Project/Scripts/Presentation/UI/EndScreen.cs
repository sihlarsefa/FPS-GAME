using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Characters;
using Project.Presentation.Bootstrap;
using Project.Infrastructure.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Maç sonu ekranı (kendi tuvali, çizim sırası 70). Zaferde "ZAFER! — KARTAL TİMİ HAREKÂTI KAZANDI"; yenilgide timin
    /// tamamı elendiyse "TİMİN ELENDİ", yalnızca oyuncu düştüyse "ŞEHİT DÜŞTÜN". Tim sıralaması #x/N, etkisiz bırakma,
    /// verilen hasar, kafadan isabet, isabet oranı, hayatta kalma süresi ve tim toplamı sayarak belirir; kazanılan TP
    /// dökümü ve rütbe ilerleme çubuğu (terfi varsa "TERFİ!" bandı) canlandırılır. "TEKRAR" ve "ANA MENÜ" düğmeleri.
    /// <para>Ölçeksiz zamanla çalışır, imleci serbest tutar. Kariyer kaydı çağıran tarafından yapılmışsa (GameSession.RecordResult)
    /// gerçek TP/rütbe değişimi gösterilir; yapılmamışsa TP sonuçtan hesaplanır.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EndScreen : MonoBehaviour
    {
        /// <summary>Tuval çizim sırası (HUD, harita ve duraklatma menüsünün üstünde).</summary>
        public const int SortOrder = 70;

        private const float RevealDelay = 0.35f;
        private const float CountSeconds = MatchEndProgression.CountSeconds;   // Tüm sayılar 0'dan 0,6 sn'de dolar.
        private const float RowStart = 0.9f;
        private const float RowStep = 0.4f;
        private const float BarSeconds = 1.2f;
        private const float ToastIn = 0.3f;
        private const float ToastHold = 2.2f;
        private const float ToastOut = 0.25f;
        private static readonly Color Bronze = new Color(0.804f, 0.498f, 0.196f, 1f);
        private const string DefaultTeamName = "Kartal Timi";

        private enum Outcome
        {
            Victory,
            TeamEliminated,
            Fallen
        }

        private sealed class StatTile
        {
            public Text Value;
            public float Target;
            public Func<float, string> Format;
            public int Shown = int.MinValue;
        }

        private static EndScreen _current;

        private MatchResult _result;
        private Action _onRestart;
        private Action _onMainMenu;
        private Outcome _outcome;

        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _content;
        private Button _restartButton;
        private Button _menuButton;
        private StatTile[] _tiles;

        private int _xpGained;
        private int _xpBefore;
        private int _xpAfter;
        private MilitaryRank _rankBefore;
        private MilitaryRank _rankShown;
        private RectTransform _insignia;
        private Text _rankName;
        private Text _xpText;
        private Text _xpGainedText;
        private UiProgressBar _xpBar;
        private RectTransform _promotion;
        private Text _promotionText;
        private bool _promotionShown;
        private int _xpShown = int.MinValue;

        // Dökümü sırayla beliren TP satırları.
        private sealed class XpRow
        {
            public CanvasGroup Group;
            public Text Value;
            public int Amount;
            public bool Revealed;
        }

        private List<XpLine> _lines;
        private XpRow[] _rows;
        private float _rowsEnd;
        private RectTransform _badge;
        private CanvasGroup _buttonsGroup;
        private int _buttonsReadyFrame = int.MaxValue;
        private Text _skipHint;
        private float _nextTick;

        // Sezon kartı şeridi.
        private SeasonPassService _season;
        private int _seasonBefore;
        private int _seasonAfter;
        private int _seasonGained;
        private int _seasonTierShown;
        private int _seasonShown = int.MinValue;
        private UiProgressBar _seasonBar;
        private Text _seasonTitle;
        private Text _seasonGainText;
        private float _flashUntil;

        // Açılış bildirimleri kuyruğu.
        private readonly Queue<string> _toasts = new Queue<string>();
        private RectTransform _toastRect;
        private Text _toastText;
        private float _toastStart = -1f;
        private bool _toastActive;

        private float _openedAt;
        private bool _acted;
        private bool _animating = true;

        /// <summary>Açık maç sonu ekranı (yoksa null).</summary>
        public static EndScreen Current => _current;

        /// <summary>Gösterilen sonuç.</summary>
        public MatchResult Result => _result;

        /// <summary>
        /// Maç sonu ekranını gösterir. Açık bir maç sonu ekranı varsa onu kapatıp yenisini açar.
        /// </summary>
        /// <param name="result">Maç sonucu.</param>
        /// <param name="onRestart">"TEKRAR" — null ise <see cref="GameSession.Restart"/>.</param>
        /// <param name="onMainMenu">"ANA MENÜ" — null ise <see cref="GameSession.ReturnToMainMenu"/>.</param>
        public static EndScreen Show(MatchResult result, Action onRestart, Action onMainMenu)
        {
            if (_current != null)
            {
                var previous = _current;
                _current = null;
                UiFactory.DestroySafe(previous.gameObject);
            }

            var canvas = UiFactory.CreateCanvas("[Maç Sonu]", SortOrder);
            var screen = canvas.gameObject.AddComponent<EndScreen>();
            screen._canvas = canvas;
            screen._result = result;
            screen._onRestart = onRestart;
            screen._onMainMenu = onMainMenu;
            screen._outcome = DetermineOutcome(result);
            screen.ResolveExperience();
            screen.Build((RectTransform)canvas.transform);
            _current = screen;

            UiFactory.SetCursorFree(true);
            PlayOutcomeSound(screen._outcome);
            if (screen._outcome == Outcome.Victory)
                BootstrapUtility.Try(() => MatchIntro.PlayVictory(screen.TeamName()), "MatchIntro.PlayVictory");
            return screen;
        }

        // ------------------------------------------------------------------ Sonuç çözümleme

        private static Outcome DetermineOutcome(MatchResult result)
        {
            if (result.IsWinner)
                return Outcome.Victory;

            // Canlı maç durumundan tim elendi mi bak (sahne hâlâ yüklü).
            try
            {
                if (GameContext.IsReady && GameContext.TryGet<MatchService>(out var match) && match != null)
                {
                    var team = match.LocalTeam;
                    if (team >= 0)
                        return match.IsTeamAlive(team) ? Outcome.Fallen : Outcome.TeamEliminated;
                }
            }
            catch (Exception)
            {
                // Maç servisi yoksa sonuç verisine düş.
            }

            // Tim sıralaması kesinleştiyse (son tim değil) ve oyuncu ölmediyse de tim elenmiştir; aksi halde şehit.
            if (!string.IsNullOrEmpty(result.KillerName))
                return Outcome.Fallen;
            return result.TeamPlacement > 1 ? Outcome.TeamEliminated : Outcome.Fallen;
        }

        private void ResolveExperience()
        {
            var computed = CareerStatsService.ComputeExperience(_result);
            _xpGained = computed;

            CareerStatsService career = null;
            try
            {
                career = GameSession.Career;
            }
            catch (Exception)
            {
                career = null;
            }

            if (career != null && career.Current != null && GameSession.LastResult.HasValue && SameResult(GameSession.LastResult.Value, _result))
            {
                // Sonuç kariyere işlendi: gerçek değişimi göster.
                _xpGained = Mathf.Max(0, career.LastExperienceGained);
                _xpAfter = Mathf.Max(0, career.Current.Experience);
                _xpBefore = Mathf.Max(0, _xpAfter - _xpGained);
            }
            else
            {
                var current = career != null && career.Current != null ? Mathf.Max(0, career.Current.Experience) : 0;
                _xpBefore = current;
                _xpAfter = current + computed;
            }

            _rankBefore = RankCatalog.RankForExperience(_xpBefore);
            _rankShown = _rankBefore;
            _lines = MatchEndProgression.CareerBreakdown(_result);
            ResolveSeason();
            BuildToastQueue(career);
        }

        private void ResolveSeason()
        {
            try
            {
                _season = SeasonPassPanel.Service;
            }
            catch (Exception)
            {
                _season = null;
            }

            if (_season == null)
                return;

            _seasonGained = SeasonPassService.XpForMatch(_result);
            var recorded = GameSession.LastResult.HasValue && SameResult(GameSession.LastResult.Value, _result);
            if (recorded)
            {
                _seasonAfter = _season.Xp;
                _seasonBefore = Mathf.Max(0, _seasonAfter - _seasonGained);
            }
            else
            {
                _seasonBefore = _season.Xp;
                _seasonAfter = Mathf.Min(_season.MaxXp, _seasonBefore + _seasonGained);
            }

            _seasonGained = _seasonAfter - _seasonBefore;
            _seasonTierShown = _season.TierForXp(_seasonBefore);
        }

        private void BuildToastQueue(CareerStatsService career)
        {
            try
            {
                var rankAfter = RankCatalog.RankForExperience(_xpAfter);
                if (rankAfter > _rankBefore)
                    _toasts.Enqueue("TERFİ  ·  " + RankCatalog.GetName(rankAfter));

                CosmeticsService cosmetics = null;
                try { cosmetics = CosmeticsRuntime.Service; } catch (Exception) { cosmetics = null; }

                if (cosmetics != null)
                {
                    var unlocks = MatchEndProgression.CareerUnlocks(cosmetics.Items, _xpBefore, _xpAfter);
                    for (var i = 0; i < unlocks.Count; i++)
                        _toasts.Enqueue("YENİ KOZMETİK  ·  " + unlocks[i].name);
                }

                if (_season != null)
                {
                    var tiers = MatchEndProgression.TiersCrossed(_seasonBefore, _seasonAfter, _season.Definition.xpPerTier, _season.MaxTier);
                    for (var i = 0; i < tiers.Count; i++)
                    {
                        var reward = _season.FreeAt(tiers[i]);
                        var text = "SEZON KADEMESİ " + tiers[i];
                        if (reward != null && cosmetics != null && cosmetics.TryGet(reward.cosmeticId, out var def))
                            text += "  ·  Ödül: " + def.name;
                        _toasts.Enqueue(text);
                    }
                }

                var progress = GameSession.Progress;
                if (progress != null && GameSession.LastResult.HasValue && SameResult(GameSession.LastResult.Value, _result))
                {
                    var after = progress.Level;
                    var before = CareerService.LevelForXp(Mathf.Max(0, progress.Experience - CareerService.MatchXp(_result)));
                    if (after > before)
                        _toasts.Enqueue("KARİYER SEVİYESİ " + after);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[EndScreen] Bildirim kuyruğu kurulamadı: " + e.Message);
            }
        }

        private static bool SameResult(MatchResult a, MatchResult b)
        {
            return a.IsWinner == b.IsWinner && a.Kills == b.Kills && a.Headshots == b.Headshots && a.Placement == b.Placement &&
                   a.TeamPlacement == b.TeamPlacement && Mathf.Approximately(a.SurvivalSeconds, b.SurvivalSeconds) &&
                   Mathf.Approximately(a.DamageDealt, b.DamageDealt);
        }

        private string TeamName()
        {
            return string.IsNullOrWhiteSpace(_result.TeamName) ? DefaultTeamName : _result.TeamName.Trim();
        }

        private int TeamPlacement()
        {
            if (_result.IsWinner)
                return 1;
            return _result.TeamPlacement > 0 ? _result.TeamPlacement : _result.Placement;
        }

        private int TeamTotal()
        {
            if (_result.TeamCount > 0)
                return _result.TeamCount;
            return _result.TotalPlayers > 0 ? _result.TotalPlayers : 0;
        }

        // ------------------------------------------------------------------ Kurulum

        private void Build(RectTransform root)
        {
            _group = UiFactory.EnsureCanvasGroup(root);
            _group.alpha = 0f;

            var victory = _outcome == Outcome.Victory;
            var accent = victory ? MenuRankInsignia.Gold : UiTheme.Accent;

            var background = UiFactory.Panel(root, new Color(0.03f, 0.035f, 0.025f, victory ? 0.6f : 0.88f));
            background.gameObject.name = "Background";

            var glow = UiFactory.Image(root, UiSprites.VerticalGradient, UiTheme.WithAlpha(victory ? MenuRankInsignia.Gold : UiTheme.AccentDark, 0.28f));
            glow.gameObject.name = "Glow";
            glow.rectTransform.localScale = new Vector3(1f, -1f, 1f);   // Üstte yoğun.
            UiFactory.SetRect(glow, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -520f), Vector2.zero);

            var vignette = UiFactory.Image(root, UiSprites.Vignette, new Color(0f, 0f, 0f, 0.75f));
            UiFactory.Stretch(vignette);

            var flag = UiFactory.Image(root, UiSprites.TurkishFlag, new Color(1f, 1f, 1f, victory ? 0.08f : 0.04f));
            flag.preserveAspect = true;
            UiFactory.Anchor(flag, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(900f, 600f));

            var stripe = UiFactory.Image(root, null, accent);
            UiFactory.SetRect(stripe, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f), Vector2.zero);

            _content = UiFactory.CreateRect("Content", root);

            BuildHeadline(_content, accent);
            BuildStats(_content);
            BuildExperience(_content);
            BuildSeason(_content);
            BuildButtons(_content);
            BuildToast(root);
        }

        private void BuildHeadline(RectTransform parent, Color accent)
        {
            var caption = UiFactory.Label(parent, Loc.Get("end.caption", "HAREKÂT SONU  ·  KUZGUN VADİSİ"), UiTheme.FontNormal, TextAnchor.MiddleCenter, UiTheme.Khaki, FontStyle.Bold);
            caption.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(caption, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -96f), new Vector2(0f, -56f));

            string title;
            string subtitle;
            Color titleColor;
            var teamUpper = MenuText.ToUpperTr(TeamName());
            switch (_outcome)
            {
                case Outcome.Victory:
                    title = Loc.Get("end.title.victory", "ZAFER!");
                    subtitle = Loc.Format("end.subtitle.won", "{0} HAREKÂTI KAZANDI", teamUpper);
                    titleColor = MenuRankInsignia.Gold;
                    break;
                case Outcome.TeamEliminated:
                    title = Loc.Get("end.title.team_down", "TİMİN ELENDİ");
                    subtitle = Loc.Format("end.subtitle.eliminated", "{0} — {1} SIRADA HAREKÂTTAN ÇEKİLDİ", teamUpper, MenuText.FormatPlacement(TeamPlacement(), TeamTotal()));
                    titleColor = UiTheme.AccentLight;
                    break;
                default:
                    title = Loc.Get("match.msg.kia", "ŞEHİT DÜŞTÜN");
                    subtitle = string.IsNullOrWhiteSpace(_result.KillerName)
                        ? Loc.Get("end.subtitle.team_continues", "TİMİN HAREKÂTA DEVAM EDİYOR")
                        : Loc.Format("end.subtitle.killed_by", "SENİ ETKİSİZ BIRAKAN: {0}", MenuText.ToUpperTr(_result.KillerName.Trim()));
                    titleColor = UiTheme.AccentLight;
                    break;
            }

            var titleText = UiFactory.Label(parent, title, 120, TextAnchor.MiddleCenter, titleColor, FontStyle.Bold);
            titleText.gameObject.name = "Title";
            titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            titleText.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.SetRect(titleText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -250f), new Vector2(0f, -100f));
            UiFactory.AddShadow(titleText, new Color(0f, 0f, 0f, 0.85f), new Vector2(4f, -4f));

            var subtitleText = UiFactory.Label(parent, subtitle, UiTheme.FontLarge + 4, TextAnchor.MiddleCenter, UiTheme.Text, FontStyle.Bold);
            subtitleText.gameObject.name = "Subtitle";
            subtitleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(subtitleText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -306f), new Vector2(0f, -256f));
            UiFactory.AddShadow(subtitleText, UiTheme.TextShadow, new Vector2(2f, -2f));

            var underline = UiFactory.Image(parent, null, accent);
            UiFactory.Anchor(underline, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -318f), new Vector2(360f, 6f));

            // Tim sıralaması rozeti.
            var badge = UiKitPanel.Card(parent, UiKitTokens.Surface, 14);
            badge.gameObject.name = "Placement";
            UiKitPanel.AddSoftShadow(badge, 22f, 0.5f, -6f);
            UiFactory.Anchor(badge, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -344f), new Vector2(360f, 96f));
            _badge = badge;
            var podium = MatchEndProgression.PodiumClass(TeamPlacement(), _result.IsWinner);
            var placeColor = podium == 1 ? MenuRankInsignia.Gold : podium == 2 ? MenuRankInsignia.Silver : podium == 3 ? Bronze : UiTheme.Text;
            var badgeBorder = UiFactory.Image(badge, UiSprites.GetRoundedRectOutline(14), UiTheme.WithAlpha(podium > 0 ? placeColor : accent, 0.9f));
            badgeBorder.raycastTarget = false;
            UiFactory.Stretch(badgeBorder);

            var placement = UiFactory.Label(badge, MenuText.FormatPlacement(TeamPlacement(), TeamTotal()), UiTheme.FontTitle, TextAnchor.MiddleCenter,
                placeColor, FontStyle.Bold);
            placement.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(placement, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 26f), new Vector2(0f, -4f));

            var placementCaption = UiFactory.Label(badge, _result.IsWinner ? Loc.Get("end.victory_caption", "ZAFER") : Loc.Get("end.team_rank", "TİM SIRALAMASI"), UiTheme.FontTiny, TextAnchor.MiddleCenter, podium > 0 ? placeColor : UiTheme.Khaki, FontStyle.Bold);
            UiFactory.SetRect(placementCaption, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 6f), new Vector2(0f, 28f));
        }

        private void BuildStats(RectTransform parent)
        {
            var row = UiFactory.HorizontalList(parent, 14f, 0, TextAnchor.MiddleCenter);
            row.gameObject.name = "Stats";
            UiFactory.Anchor(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -468f), new Vector2(1400f, 128f));
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            var survival = Mathf.Max(0f, _result.SurvivalSeconds);
            _tiles = new[]
            {
                Tile(row, Loc.Get("end.kills", "ETKİSİZ BIRAKMA"), _result.Kills, v => MenuText.FormatThousands((long)v)),
                Tile(row, Loc.Get("end.damage", "VERİLEN HASAR"), _result.DamageDealt, v => MenuText.FormatDamage(v)),
                Tile(row, Loc.Get("end.headshots", "KAFADAN İSABET"), _result.Headshots, v => MenuText.FormatThousands((long)v)),
                Tile(row, Loc.Get("end.accuracy", "İSABET ORANI"), Mathf.Clamp01(_result.Accuracy) * 1000f, v => MenuText.FormatPercent(v / 1000f, true)),
                Tile(row, Loc.Get("end.survival", "HAYATTA KALMA"), survival, v => MenuText.FormatDuration(v)),
                Tile(row, Loc.Get("end.team_kills", "TİM ETKİSİZ"), Mathf.Max(_result.TeamKills, _result.Kills), v => MenuText.FormatThousands((long)v))
            };
        }

        private static StatTile Tile(Transform parent, string caption, float target, Func<float, string> format)
        {
            var tile = UiKitPanel.Card(parent, UiTheme.WithAlpha(UiKitTokens.Surface, 0.95f), 12);
            tile.gameObject.name = "Tile_" + caption;
            UiFactory.LayoutSize(tile, 200f, 128f, 1f);
            UiKitPanel.AddSoftShadow(tile, 18f, 0.45f, -4f);
            var border = UiFactory.Image(tile, UiSprites.GetRoundedRectOutline(12), UiKitTokens.Border);
            border.raycastTarget = false;
            UiFactory.Stretch(border);
            var topBar = UiFactory.Image(tile, null, UiKitTokens.Sand);
            topBar.raycastTarget = false;
            UiFactory.SetRect(topBar, new Vector2(0.18f, 1f), new Vector2(0.82f, 1f), new Vector2(0f, -3f), Vector2.zero);

            var value = UiFactory.Label(tile, format(0f), UiTheme.FontTitle - 6, TextAnchor.MiddleCenter, UiKitTokens.Text, FontStyle.Bold);
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(value, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 38f), new Vector2(0f, -10f));
            UiFactory.AddShadow(value, UiTheme.TextShadow, new Vector2(2f, -2f));

            var label = UiFactory.Label(tile, caption, UiTheme.FontTiny, TextAnchor.MiddleCenter, UiKitTokens.Sand, FontStyle.Bold);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(label, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 12f), new Vector2(0f, 38f));

            return new StatTile { Value = value, Target = float.IsNaN(target) || float.IsInfinity(target) ? 0f : Mathf.Max(0f, target), Format = format };
        }

        private void BuildExperience(RectTransform parent)
        {
            var card = UiKitPanel.Card(parent, UiTheme.WithAlpha(UiKitTokens.Surface, 0.97f), 14);
            card.gameObject.name = "Experience";
            UiKitPanel.AddSoftShadow(card, 26f, 0.5f, -6f);
            UiFactory.Anchor(card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -620f), new Vector2(1400f, 230f));
            var border = UiFactory.Image(card, UiSprites.GetRoundedRectOutline(14), UiTheme.WithAlpha(MenuRankInsignia.Gold, 0.5f));
            border.raycastTarget = false;
            UiFactory.Stretch(border);

            // Sol: TP dökümü.
            var header = UiFactory.Label(card, Loc.Get("end.xp", "KAZANILAN TECRÜBE"), UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.Khaki, FontStyle.Bold);
            UiFactory.SetRect(header, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -54f), new Vector2(32f + 420f, -18f));

            _xpGainedText = UiFactory.Label(card, "+0 TP", UiTheme.FontTitle, TextAnchor.MiddleLeft, MenuRankInsignia.Gold, FontStyle.Bold);
            _xpGainedText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_xpGainedText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -116f), new Vector2(32f + 420f, -54f));
            UiFactory.AddShadow(_xpGainedText, UiTheme.TextShadow, new Vector2(2f, -2f));

            BuildBreakdownRows(card);

            var divider = UiFactory.Image(card, null, UiTheme.WithAlpha(UiTheme.PanelBorder, 0.8f));
            UiFactory.SetRect(divider, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(620f, 20f), new Vector2(622f, -20f));

            // Sağ: rütbe ilerlemesi.
            var holder = UiFactory.CreateRect("Insignia", card);
            UiFactory.Anchor(holder, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(660f, -30f), new Vector2(190f, 74f));
            _insignia = MenuRankInsignia.Create(holder, _rankBefore, 70f);

            _rankName = UiFactory.Label(card, string.Empty, UiTheme.FontLarge, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold);
            _rankName.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_rankName, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(870f, -72f), new Vector2(-32f, -26f));

            _xpText = UiFactory.Label(card, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.TextDim);
            _xpText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_xpText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(870f, -104f), new Vector2(-32f, -74f));

            _xpBar = UiFactory.ProgressBar(card, MenuRankInsignia.Gold, UiTheme.Track);
            _xpBar.TrailEnabled = false;
            UiFactory.SetRect(_xpBar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(660f, -146f), new Vector2(-32f, -124f));
            _xpBar.SetSegments(10, UiTheme.WithAlpha(Color.black, 0.35f), 2f);

            _promotion = UiFactory.Panel(card, UiTheme.WithAlpha(MenuRankInsignia.Gold, 0.18f), UiSprites.GetRoundedRect(4));
            _promotion.gameObject.name = "Promotion";
            UiFactory.SetRect(_promotion, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(660f, 18f), new Vector2(-32f, 64f));
            _promotionText = UiFactory.Label(_promotion, string.Empty, UiTheme.FontMedium, TextAnchor.MiddleCenter, MenuRankInsignia.Gold, FontStyle.Bold);
            _promotionText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _promotion.gameObject.SetActive(false);

            UpdateRankVisual(_xpBefore, true);
        }

        private void BuildBreakdownRows(RectTransform card)
        {
            _rows = new XpRow[_lines.Count];
            for (var i = 0; i < _lines.Count; i++)
            {
                var row = UiFactory.CreateRect("XpRow" + i, card);
                var top = -124f - 25f * i;
                UiFactory.SetRect(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, top - 24f), new Vector2(32f + 560f, top));
                var group = UiFactory.EnsureCanvasGroup(row);
                group.alpha = 0f;

                var label = UiFactory.Label(row, _lines[i].Label, UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.TextDim);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiFactory.SetRect(label, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-130f, 0f));

                var value = UiFactory.Label(row, "+0", UiTheme.FontSmall, TextAnchor.MiddleRight, _lines[i].Amount > 0 ? UiTheme.Text : UiTheme.TextDim, FontStyle.Bold);
                value.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiFactory.SetRect(value, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-120f, 0f), new Vector2(0f, 0f));

                _rows[i] = new XpRow { Group = group, Value = value, Amount = _lines[i].Amount };
            }

            _rowsEnd = RowStart + RowStep * _rows.Length + CountSeconds * 0.5f;
        }

        private void BuildSeason(RectTransform parent)
        {
            if (_season == null)
                return;

            var card = UiKitPanel.Card(parent, UiTheme.WithAlpha(UiKitTokens.Surface, 0.97f), 12);
            card.gameObject.name = "Season";
            UiKitPanel.AddSoftShadow(card, 18f, 0.45f, -4f);
            UiFactory.Anchor(card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -862f), new Vector2(1400f, 72f));
            var border = UiFactory.Image(card, UiSprites.GetRoundedRectOutline(12), UiTheme.WithAlpha(UiTheme.Accent, 0.7f));
            border.raycastTarget = false;
            UiFactory.Stretch(border);

            _seasonTitle = UiFactory.Label(card, string.Empty, UiTheme.FontMedium, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold);
            _seasonTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_seasonTitle, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(32f, 8f), new Vector2(32f + 420f, -8f));

            _seasonBar = UiFactory.ProgressBar(card, UiTheme.Accent, UiTheme.Track);
            _seasonBar.TrailEnabled = false;
            UiFactory.SetRect(_seasonBar, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(500f, 26f), new Vector2(-250f, -26f));

            _seasonGainText = UiFactory.Label(card, "+0 SEZON TP", UiTheme.FontMedium, TextAnchor.MiddleRight, UiTheme.AccentLight, FontStyle.Bold);
            _seasonGainText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_seasonGainText, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-230f, 8f), new Vector2(-24f, -8f));

            UpdateSeason(0f, false);
        }

        private void BuildToast(RectTransform parent)
        {
            var toast = UiKitPanel.Card(parent, UiTheme.WithAlpha(UiKitTokens.Surface, 0.98f), 12);
            toast.gameObject.name = "Toast";
            UiKitPanel.AddSoftShadow(toast, 18f, 0.5f, -4f);
            UiFactory.Anchor(toast, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(560f, 70f), new Vector2(480f, 84f));
            var border = UiFactory.Image(toast, UiSprites.GetRoundedRectOutline(12), UiTheme.WithAlpha(MenuRankInsignia.Gold, 0.9f));
            border.raycastTarget = false;
            UiFactory.Stretch(border);
            var bar = UiFactory.Image(toast, null, UiTheme.Accent);
            bar.raycastTarget = false;
            UiFactory.SetRect(bar, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 8f), new Vector2(5f, -8f));
            _toastText = UiFactory.Label(toast, string.Empty, UiTheme.FontMedium, TextAnchor.MiddleLeft, MenuRankInsignia.Gold, FontStyle.Bold);
            _toastText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.SetRect(_toastText, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(22f, 6f), new Vector2(-14f, -6f));
            _toastRect = toast;
            toast.gameObject.SetActive(false);
        }

        private void BuildButtons(RectTransform parent)
        {
            var row = UiFactory.HorizontalList(parent, 18f, 0, TextAnchor.MiddleCenter);
            row.gameObject.name = "Buttons";
            UiFactory.Anchor(row, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(720f, 64f));
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childForceExpandWidth = false;

            _restartButton = UiKitButton.Create(row, Loc.Get("end.btn.restart", "TEKRAR"), OnRestartClicked, UiKitButtonKind.Primary, 330f, 64f);
            _menuButton = UiKitButton.Create(row, Loc.Get("end.btn.main_menu", "ANA MENÜ"), OnMainMenuClicked, UiKitButtonKind.Default, 330f, 64f);

            _buttonsGroup = UiFactory.EnsureCanvasGroup(row);
            _buttonsGroup.alpha = 0f;
            _buttonsGroup.interactable = false;
            _buttonsGroup.blocksRaycasts = false;

            _skipHint = UiFactory.Label(parent, Loc.Get("end.skip_hint", "BOŞLUK  ·  ATLA"), UiTheme.FontTiny, TextAnchor.MiddleCenter, UiTheme.TextDim, FontStyle.Bold);
            _skipHint.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Anchor(_skipHint, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(400f, 30f));
        }

        // ------------------------------------------------------------------ Canlandırma

        private void Start()
        {
            _openedAt = Time.unscaledTime;
            var eventSystem = EventSystem.current;
            if (eventSystem != null && _restartButton != null)
                eventSystem.SetSelectedGameObject(_restartButton.gameObject);
        }

        private void Update()
        {
            // Menü açıkken imleç serbest kalsın (oyun sistemleri kilitlemeye çalışabilir).
            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible)
                Cursor.visible = true;

            var kb = Keyboard.current;
            if ((_animating || _toastActive || _toasts.Count > 0) && kb != null && kb.spaceKey.wasPressedThisFrame)
                Skip();

            if (_animating)
                Animate();
            else
                PumpToasts();
        }

        /// <summary>Boşluk: tüm canlandırmaları anında bitirir (ölçüleri son değere getirir).</summary>
        private void Skip()
        {
            _openedAt = Time.unscaledTime - 1000f;
            if (!_animating && _toastActive)
                _toastStart -= 100f;
        }

        private void Animate()
        {
            var elapsed = Time.unscaledTime - _openedAt;
            if (_group != null)
                _group.alpha = Mathf.Clamp01(elapsed / 0.35f);

            // İstatistikler sayarak artar (0 → değer, 0,6 sn).
            var countT = MatchEndProgression.EaseOut(MatchEndProgression.CountT(elapsed, RevealDelay));
            if (_tiles != null)
            {
                for (var i = 0; i < _tiles.Length; i++)
                    UpdateTile(_tiles[i], countT);
            }

            // Sıralama rozeti: hafif büyüyüp yerine oturur.
            if (_badge != null)
            {
                var pop = MatchEndProgression.EaseOut(MatchEndProgression.CountT(elapsed, 0.15f, 0.4f));
                _badge.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, pop);
            }

            // TP döküm satırları sırayla belirir ve sayarak dolar.
            var rowsDone = true;
            if (_rows != null)
            {
                for (var i = 0; i < _rows.Length; i++)
                {
                    var row = _rows[i];
                    var start = RowStart + RowStep * i;
                    var t = MatchEndProgression.CountT(elapsed, start);
                    row.Group.alpha = Mathf.Clamp01((elapsed - start) / 0.18f);
                    row.Value.text = "+" + MenuText.FormatThousands(Mathf.RoundToInt(row.Amount * MatchEndProgression.EaseOut(t)));
                    if (!row.Revealed && elapsed >= start)
                    {
                        row.Revealed = true;
                        UiSounds.Play(UiSfx.Tab);
                    }

                    if (t < 1f)
                        rowsDone = false;
                }
            }

            // TP + rütbe çubuğu.
            var xpT = MatchEndProgression.EaseOut(MatchEndProgression.CountT(elapsed, _rowsEnd, BarSeconds));
            var xp = Mathf.RoundToInt(Mathf.Lerp(_xpBefore, _xpAfter, xpT));
            if (xp != _xpShown)
            {
                _xpShown = xp;
                UpdateRankVisual(xp, false);
                if (_xpGainedText != null)
                    _xpGainedText.text = "+" + MenuText.FormatThousands(Mathf.RoundToInt(_xpGained * xpT)) + " TP";
            }

            // Sezon çubuğu (kademe atlayınca parlar).
            var seasonT = MatchEndProgression.EaseOut(MatchEndProgression.CountT(elapsed, _rowsEnd + 0.3f, BarSeconds));
            UpdateSeason(seasonT, true);

            if (xpT > 0f && (xpT < 1f || seasonT < 1f) && Time.unscaledTime >= _nextTick)
            {
                _nextTick = Time.unscaledTime + 0.07f;
                UiSounds.Play(UiSfx.Hover);
            }

            if (countT >= 1f && rowsDone && xpT >= 1f && seasonT >= 1f)
            {
                _animating = false;
                if (_group != null)
                    _group.alpha = 1f;
                if (_badge != null)
                    _badge.localScale = Vector3.one;
                if (_skipHint != null)
                    _skipHint.gameObject.SetActive(false);
                _buttonsReadyFrame = Time.frameCount + 1;
                if (_buttonsGroup != null)
                    _buttonsGroup.alpha = 1f;
            }
        }

        private void LateUpdate()
        {
            // Devam düğmeleri canlandırma bittikten bir kare sonra tıklanabilir (Boşluk atlama gönderimi tetiklemesin).
            if (_buttonsGroup != null && !_animating && Time.frameCount >= _buttonsReadyFrame && !_buttonsGroup.interactable)
            {
                _buttonsGroup.interactable = true;
                _buttonsGroup.blocksRaycasts = true;
            }
        }

        private void UpdateSeason(float t, bool sound)
        {
            if (_seasonBar == null || _season == null)
                return;

            var xp = Mathf.RoundToInt(Mathf.Lerp(_seasonBefore, _seasonAfter, t));
            var perTier = Mathf.Max(1, _season.Definition.xpPerTier);
            var tier = _season.TierForXp(xp);
            if (xp != _seasonShown)
            {
                _seasonShown = xp;
                _seasonBar.SetValue(tier >= _season.MaxTier ? 1f : (xp % perTier) / (float)perTier, true);
                if (_seasonGainText != null)
                    _seasonGainText.text = "+" + MenuText.FormatThousands(Mathf.RoundToInt(_seasonGained * t)) + " SEZON TP";
            }

            if (tier > _seasonTierShown)
            {
                _seasonTierShown = tier;
                _flashUntil = Time.unscaledTime + 0.6f;
                if (sound)
                    UiSounds.Play(UiSfx.MatchFound);
            }

            var flash = Mathf.Clamp01((_flashUntil - Time.unscaledTime) / 0.6f);
            if (_seasonBar.Fill != null)
                _seasonBar.Fill.color = Color.Lerp(UiTheme.Accent, Color.white, flash);
            if (_seasonTitle != null)
            {
                var up = flash > 0f;
                _seasonTitle.text = up ? "KADEME ATLADI!  ·  " + tier : "SEZON KARTI  ·  KADEME " + tier;
                _seasonTitle.color = up ? UiTheme.AccentLight : UiTheme.Text;
            }
        }

        /// <summary>Açılış bildirimleri tek tek kayarak girer, bekler, çıkar.</summary>
        private void PumpToasts()
        {
            if (_toastRect == null)
                return;

            var now = Time.unscaledTime;
            if (!_toastActive)
            {
                if (_toasts.Count == 0)
                    return;
                _toastText.text = _toasts.Dequeue();
                _toastRect.gameObject.SetActive(true);
                _toastActive = true;
                _toastStart = now;
                UiSounds.Play(UiSfx.Press);
            }

            var t = now - _toastStart;
            const float offscreen = 560f;
            const float shown = -40f;
            float x;
            if (t < ToastIn)
                x = Mathf.Lerp(offscreen, shown, MatchEndProgression.EaseOut(t / ToastIn));
            else if (t < ToastIn + ToastHold)
                x = shown;
            else if (t < ToastIn + ToastHold + ToastOut)
                x = Mathf.Lerp(shown, offscreen, (t - ToastIn - ToastHold) / ToastOut);
            else
            {
                _toastActive = false;
                _toastRect.gameObject.SetActive(false);
                return;
            }

            var pos = _toastRect.anchoredPosition;
            pos.x = x;
            _toastRect.anchoredPosition = pos;
        }

        private static void UpdateTile(StatTile tile, float t)
        {
            if (tile == null || tile.Value == null)
                return;

            var value = tile.Target * t;
            var key = Mathf.RoundToInt(value * 10f);
            if (key == tile.Shown)
                return;

            tile.Shown = key;
            tile.Value.text = tile.Format(t >= 1f ? tile.Target : value);
        }

        private void UpdateRankVisual(int xp, bool force)
        {
            var rank = RankCatalog.RankForExperience(xp);
            if (force || rank != _rankShown)
            {
                if (_insignia != null && rank != _rankShown)
                    MenuRankInsignia.Rebuild(_insignia, rank);
                _rankShown = rank;

                if (_rankName != null)
                {
                    _rankName.text = MenuText.ToUpperTr(RankCatalog.GetName(rank));
                    var color = MenuRankInsignia.CategoryColor(rank);
                    _rankName.color = color == UiTheme.TextDim ? UiTheme.Text : color;
                }

                // Birden çok rütbe atlanırsa bant doğrudan son rütbeyi gösterir.
                if (!force && rank > _rankBefore && !_promotionShown)
                    ShowPromotion(RankCatalog.RankForExperience(_xpAfter));
            }

            var hasNext = RankCatalog.TryGetNextRank(rank, out var next);
            if (_xpBar != null)
                _xpBar.SetValue(RankCatalog.ProgressToNextRank(xp), true);
            if (_xpText != null)
            {
                _xpText.text = hasNext
                    ? MenuText.FormatThousands(xp) + " / " + MenuText.FormatThousands(RankCatalog.RequiredExperience(next)) + " TP  ·  sonraki: " + RankCatalog.GetName(next)
                    : Loc.Format("end.max_rank", "{0} TP  ·  en yüksek rütbe", MenuText.FormatThousands(xp));
            }
        }

        private void ShowPromotion(MilitaryRank rank)
        {
            _promotionShown = true;
            if (_promotion == null)
                return;

            _promotion.gameObject.SetActive(true);
            if (_promotionText != null)
                _promotionText.text = "TERFİ!  " + RankCatalog.GetName(_rankBefore) + "  →  " + RankCatalog.GetName(rank);

            try
            {
                GameAudio.Play2D(SoundId.UiConfirm, 1f, 0.8f);
            }
            catch (Exception)
            {
                // Ses yoksa sessiz.
            }
        }

        private static void PlayOutcomeSound(Outcome outcome)
        {
            try
            {
                if (outcome == Outcome.Victory)
                    GameAudio.Play2D(SoundId.KillConfirm, 0.9f, 0.75f);
                else
                    GameAudio.Play2D(SoundId.RadioBeep, 0.7f, 0.85f);
            }
            catch (Exception)
            {
                // Ses sistemi yoksa sessiz.
            }
        }

        // ------------------------------------------------------------------ Düğmeler

        private void OnRestartClicked()
        {
            if (_acted)
                return;
            Act(_onRestart, GameSession.Restart, "TEKRAR");
        }

        private void OnMainMenuClicked()
        {
            if (_acted)
                return;
            Act(_onMainMenu, GameSession.ReturnToMainMenu, "ANA MENÜ");
        }

        private void Act(Action callback, Action fallback, string context)
        {
            _acted = true;
            SetButtonsInteractable(false);
            Time.timeScale = 1f;

            try
            {
                if (callback != null)
                    callback();
                else
                    fallback();
            }
            catch (Exception e)
            {
                Debug.LogError("[EndScreen] " + context + " başarısız: " + e.Message);
                Debug.LogException(e);
            }

            // Sahne yüklemesi başlamadıysa (hata / sahne yok) düğmeler yeniden kullanılabilsin.
            if (this != null && !GameSession.IsLoading)
            {
                _acted = false;
                SetButtonsInteractable(true);
            }
        }

        private void SetButtonsInteractable(bool interactable)
        {
            UiWidgets.SetInteractable(_restartButton, interactable);
            UiWidgets.SetInteractable(_menuButton, interactable);
        }

        private void OnEnable() => Loc.LanguageChanged += OnLanguageChanged;
        private void OnDisable() => Loc.LanguageChanged -= OnLanguageChanged;

        private void OnLanguageChanged(string code)
        {
            UiFactory.SetButtonLabel(_restartButton, Loc.Get("end.btn.restart", "TEKRAR"));
            UiFactory.SetButtonLabel(_menuButton, Loc.Get("end.btn.main_menu", "ANA MENÜ"));
        }

        private void OnDestroy()
        {
            if (_current == this)
                _current = null;
            _onRestart = null;
            _onMainMenu = null;
        }
    }
}
