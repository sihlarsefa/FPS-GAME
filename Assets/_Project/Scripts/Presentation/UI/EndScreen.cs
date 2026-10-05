using System;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure;
using Project.Infrastructure.Audio;
using Project.Presentation.Bootstrap;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private const float CountSeconds = 1.3f;
        private const float XpDelay = 1.1f;
        private const float XpSeconds = 1.8f;
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

            var background = UiFactory.Panel(root, new Color(0.03f, 0.035f, 0.025f, 0.88f));
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
            BuildButtons(_content);
        }

        private void BuildHeadline(RectTransform parent, Color accent)
        {
            var caption = UiFactory.Label(parent, "HAREKÂT SONU  ·  KUZGUN VADİSİ", UiTheme.FontNormal, TextAnchor.MiddleCenter, UiTheme.Khaki, FontStyle.Bold);
            caption.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(caption, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -96f), new Vector2(0f, -56f));

            string title;
            string subtitle;
            Color titleColor;
            var teamUpper = MenuText.ToUpperTr(TeamName());
            switch (_outcome)
            {
                case Outcome.Victory:
                    title = "ZAFER!";
                    subtitle = teamUpper + " HAREKÂTI KAZANDI";
                    titleColor = MenuRankInsignia.Gold;
                    break;
                case Outcome.TeamEliminated:
                    title = "TİMİN ELENDİ";
                    subtitle = teamUpper + " — " + MenuText.FormatPlacement(TeamPlacement(), TeamTotal()) + " SIRADA HAREKÂTTAN ÇEKİLDİ";
                    titleColor = UiTheme.AccentLight;
                    break;
                default:
                    title = "ŞEHİT DÜŞTÜN";
                    subtitle = string.IsNullOrWhiteSpace(_result.KillerName)
                        ? "TİMİN HAREKÂTA DEVAM EDİYOR"
                        : "SENİ ETKİSİZ BIRAKAN: " + MenuText.ToUpperTr(_result.KillerName.Trim());
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
            var badge = UiFactory.Panel(parent, UiTheme.PanelDark, UiSprites.ChamferRect);
            badge.gameObject.name = "Placement";
            UiFactory.Anchor(badge, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -344f), new Vector2(360f, 96f));
            var badgeBorder = UiFactory.Image(badge, UiSprites.GetRoundedRectOutline(UiTheme.CornerRadius), UiTheme.WithAlpha(accent, 0.8f));
            UiFactory.Stretch(badgeBorder);

            var placement = UiFactory.Label(badge, MenuText.FormatPlacement(TeamPlacement(), TeamTotal()), UiTheme.FontTitle, TextAnchor.MiddleCenter,
                _outcome == Outcome.Victory ? MenuRankInsignia.Gold : UiTheme.Text, FontStyle.Bold);
            placement.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(placement, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 26f), new Vector2(0f, -4f));

            var placementCaption = UiFactory.Label(badge, "TİM SIRALAMASI", UiTheme.FontTiny, TextAnchor.MiddleCenter, UiTheme.Khaki, FontStyle.Bold);
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
                Tile(row, "ETKİSİZ BIRAKMA", _result.Kills, v => MenuText.FormatThousands((long)v)),
                Tile(row, "VERİLEN HASAR", _result.DamageDealt, v => MenuText.FormatDamage(v)),
                Tile(row, "KAFADAN İSABET", _result.Headshots, v => MenuText.FormatThousands((long)v)),
                Tile(row, "İSABET ORANI", Mathf.Clamp01(_result.Accuracy) * 1000f, v => MenuText.FormatPercent(v / 1000f, true)),
                Tile(row, "HAYATTA KALMA", survival, v => MenuText.FormatDuration(v)),
                Tile(row, "TİM ETKİSİZ", Mathf.Max(_result.TeamKills, _result.Kills), v => MenuText.FormatThousands((long)v))
            };
        }

        private static StatTile Tile(Transform parent, string caption, float target, Func<float, string> format)
        {
            var tile = UiFactory.Panel(parent, UiTheme.WithAlpha(UiTheme.PanelDark, 0.92f), UiSprites.ChamferRect);
            tile.gameObject.name = "Tile_" + caption;
            UiFactory.LayoutSize(tile, 200f, 128f, 1f);
            var border = UiFactory.Image(tile, UiSprites.GetRoundedRectOutline(UiTheme.CornerRadius), UiTheme.WithAlpha(UiTheme.PanelBorder, 0.7f));
            UiFactory.Stretch(border);

            var value = UiFactory.Label(tile, format(0f), UiTheme.FontTitle - 6, TextAnchor.MiddleCenter, UiTheme.Text, FontStyle.Bold);
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(value, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 38f), new Vector2(0f, -10f));
            UiFactory.AddShadow(value, UiTheme.TextShadow, new Vector2(2f, -2f));

            var label = UiFactory.Label(tile, caption, UiTheme.FontTiny, TextAnchor.MiddleCenter, UiTheme.Khaki, FontStyle.Bold);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(label, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 12f), new Vector2(0f, 38f));

            return new StatTile { Value = value, Target = float.IsNaN(target) || float.IsInfinity(target) ? 0f : Mathf.Max(0f, target), Format = format };
        }

        private void BuildExperience(RectTransform parent)
        {
            var card = UiFactory.Panel(parent, UiTheme.WithAlpha(UiTheme.PanelDark, 0.95f), UiSprites.ChamferRect);
            card.gameObject.name = "Experience";
            UiFactory.Anchor(card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -620f), new Vector2(1400f, 230f));
            var border = UiFactory.Image(card, UiSprites.GetRoundedRectOutline(UiTheme.CornerRadius), UiTheme.WithAlpha(MenuRankInsignia.Gold, 0.45f));
            UiFactory.Stretch(border);

            // Sol: TP dökümü.
            var header = UiFactory.Label(card, "KAZANILAN TECRÜBE", UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.Khaki, FontStyle.Bold);
            UiFactory.SetRect(header, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -54f), new Vector2(32f + 420f, -18f));

            _xpGainedText = UiFactory.Label(card, "+0 TP", UiTheme.FontTitle, TextAnchor.MiddleLeft, MenuRankInsignia.Gold, FontStyle.Bold);
            _xpGainedText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_xpGainedText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -116f), new Vector2(32f + 420f, -54f));
            UiFactory.AddShadow(_xpGainedText, UiTheme.TextShadow, new Vector2(2f, -2f));

            var breakdown = UiFactory.Label(card, BuildBreakdown(), UiTheme.FontSmall, TextAnchor.UpperLeft, UiTheme.TextDim);
            breakdown.lineSpacing = 1.1f;
            UiFactory.SetRect(breakdown, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(32f, 14f), new Vector2(32f + 560f, -122f));

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

        private string BuildBreakdown()
        {
            var kills = Mathf.Max(0, _result.Kills);
            var headshots = Mathf.Max(0, _result.Headshots);
            var outlasted = Mathf.Max(0, _result.TeamCount - _result.TeamPlacement);
            var lines = "Etkisiz bırakma  " + kills + " × " + CareerStatsService.ExperiencePerKill + "  =  +" +
                        MenuText.FormatThousands((long)kills * CareerStatsService.ExperiencePerKill) +
                        "\nKafadan isabet  " + headshots + " × " + CareerStatsService.ExperiencePerHeadshot + "  =  +" +
                        MenuText.FormatThousands((long)headshots * CareerStatsService.ExperiencePerHeadshot) +
                        "\nGeride bırakılan tim  " + outlasted + " × " + CareerStatsService.ExperiencePerTeamOutlasted + "  =  +" +
                        MenuText.FormatThousands((long)outlasted * CareerStatsService.ExperiencePerTeamOutlasted);
            if (_result.IsWinner)
                lines += "\nZafer  +" + MenuText.FormatThousands(CareerStatsService.ExperienceForWin);
            return lines;
        }

        private void BuildButtons(RectTransform parent)
        {
            var row = UiFactory.HorizontalList(parent, 18f, 0, TextAnchor.MiddleCenter);
            row.gameObject.name = "Buttons";
            UiFactory.Anchor(row, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(720f, 64f));
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childForceExpandWidth = false;

            _restartButton = UiFactory.Button(row, "TEKRAR", OnRestartClicked, UiButtonStyle.Primary);
            UiFactory.LayoutSize(_restartButton, 330f, 64f);
            _menuButton = UiFactory.Button(row, "ANA MENÜ", OnMainMenuClicked, UiButtonStyle.Default);
            UiFactory.LayoutSize(_menuButton, 330f, 64f);
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

            if (!_animating)
                return;

            var elapsed = Time.unscaledTime - _openedAt;
            if (_group != null)
                _group.alpha = Mathf.Clamp01(elapsed / 0.35f);

            // İstatistikler sayarak artar.
            var countT = Ease(Mathf.Clamp01((elapsed - RevealDelay) / CountSeconds));
            if (_tiles != null)
            {
                for (var i = 0; i < _tiles.Length; i++)
                    UpdateTile(_tiles[i], countT);
            }

            // TP çubuğu.
            var xpT = Ease(Mathf.Clamp01((elapsed - RevealDelay - XpDelay) / XpSeconds));
            var xp = Mathf.RoundToInt(Mathf.Lerp(_xpBefore, _xpAfter, xpT));
            if (xp != _xpShown)
            {
                _xpShown = xp;
                UpdateRankVisual(xp, false);
                if (_xpGainedText != null)
                    _xpGainedText.text = "+" + MenuText.FormatThousands(Mathf.RoundToInt(_xpGained * xpT)) + " TP";
            }

            if (countT >= 1f && xpT >= 1f)
            {
                _animating = false;
                if (_group != null)
                    _group.alpha = 1f;
            }
        }

        private static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

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
                    : MenuText.FormatThousands(xp) + " TP  ·  en yüksek rütbe";
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

        private void OnDestroy()
        {
            if (_current == this)
                _current = null;
            _onRestart = null;
            _onMainMenu = null;
        }
    }
}
