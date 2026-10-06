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
    /// <summary>
    /// Derin kariyer görünümü (CareerService): rütbe başlığı, kariyer + sezon seviyesi, 30 madalya ızgarası (bronz/gümüş/altın,
    /// kilitliler soluk, ilerleme halkası, eşik ipucu), silah tablosu (sıralanabilir), harita istatistikleri ve maç geçmişi + sıralama grafiği.
    /// </summary>
    public sealed partial class CareerPanel
    {
        private static readonly string[] DeepTabs = { "MADALYALAR", "SİLAHLAR", "HARİTALAR", "MAÇ GEÇMİŞİ" };
        private const int MedalColumns = 6;

        private CareerService _deep;
        private RectTransform _deepRoot;
        private RectTransform _deepContent;
        private UiKitTabBar _deepTabBar;
        private int _deepTab;
        private CareerWeaponSort _weaponSort = CareerWeaponSort.Kills;
        private bool _weaponDesc = true;
        private Text _deepRank, _deepCareerLevel, _deepSeasonLevel, _deepMedalCount;
        private UiProgressBar _careerBar, _seasonBar;
        private RectTransform _deepInsignia;
        private MilitaryRank _deepShownRank = (MilitaryRank)(-1);

        private bool DeepVisible => _deepRoot != null && _deepRoot.gameObject.activeSelf;

        private void AttachDeep()
        {
            try { _deep = GameSession.Progress; } catch (Exception) { _deep = null; }
            if (_deep != null) _deep.Changed += OnDeepChanged;
            RefreshDeep();
        }

        private void DetachDeep()
        {
            if (_deep != null) _deep.Changed -= OnDeepChanged;
            _deep = null;
        }

        private void OnDeepChanged()
        {
            if (!_closed) RefreshDeep();
        }

        private void SetDeepVisible(bool visible)
        {
            if (_deepRoot == null) return;
            _deepRoot.gameObject.SetActive(visible);
            if (visible)
            {
                _deepRoot.SetAsLastSibling();
                RefreshDeep();
            }
        }

        private void BuildDeep(RectTransform window)
        {
            _deepRoot = UiFactory.Panel(window, UiKitTokens.Surface, UiSprites.ChamferRect);
            _deepRoot.gameObject.name = "Deep";
            UiFactory.SetRect(_deepRoot, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(30f, 100f), new Vector2(-30f, -112f));
            var border = UiFactory.Image(_deepRoot, UiSprites.GetRoundedRectOutline(UiTheme.CornerRadius), UiKitTokens.Border);
            border.raycastTarget = false;
            UiFactory.Stretch(border);

            // Başlık: rütbe + iki seviye.
            var holder = UiFactory.CreateRect("Insignia", _deepRoot);
            UiFactory.Anchor(holder, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(110f, -52f), new Vector2(200f, 80f));
            _deepInsignia = MenuRankInsignia.Create(holder, MilitaryRank.Er, 64f);

            _deepRank = UiFactory.Label(_deepRoot, string.Empty, UiTheme.FontLarge, TextAnchor.MiddleLeft, UiKitTokens.Text, FontStyle.Bold);
            _deepRank.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_deepRank, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(230f, -50f), new Vector2(620f, -8f));
            _deepMedalCount = UiFactory.Label(_deepRoot, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleLeft, UiKitTokens.TextDim);
            _deepMedalCount.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_deepMedalCount, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(230f, -88f), new Vector2(620f, -52f));

            BuildLevelCard("KARİYER SEVİYESİ", 660f, out _deepCareerLevel, out _careerBar);
            BuildLevelCard("SEZON SEVİYESİ", 1000f, out _deepSeasonLevel, out _seasonBar);

            _deepTabBar = UiKitTabBar.Create(_deepRoot, DeepTabs, false, i => { _deepTab = i; RenderDeepTab(); }, 220f, 40f);
            UiFactory.SetRect(_deepTabBar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -146f), new Vector2(-20f, -100f));

            var body = UiFactory.CreateRect("Body", _deepRoot);
            UiFactory.SetRect(body, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(16f, 12f), new Vector2(-16f, -152f));
            _deepContent = body;
            _deepTabBar.Select(0, false);
            _deepRoot.gameObject.SetActive(false);
        }

        private void BuildLevelCard(string caption, float x, out Text value, out UiProgressBar bar)
        {
            var cap = UiFactory.Label(_deepRoot, caption, UiTheme.FontTiny, TextAnchor.MiddleLeft, UiKitTokens.Sand, FontStyle.Bold);
            cap.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(cap, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -34f), new Vector2(x + 300f, -8f));
            value = UiFactory.Label(_deepRoot, "1", UiTheme.FontLarge, TextAnchor.MiddleLeft, UiKitTokens.Text, FontStyle.Bold);
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(value, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -66f), new Vector2(x + 300f, -34f));
            bar = UiFactory.ProgressBar(_deepRoot, UiKitTokens.Sand, UiTheme.Track);
            bar.TrailEnabled = false;
            UiFactory.SetRect(bar, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -92f), new Vector2(x + 300f, -76f));
        }

        private void RefreshDeep()
        {
            if (_deepRoot == null) return;
            var stats = _career != null && _career.Current != null ? _career.Current : new CareerStats();
            var rank = RankCatalog.RankForExperience(Mathf.Max(0, stats.Experience));
            if (_deepInsignia != null && rank != _deepShownRank) MenuRankInsignia.Rebuild(_deepInsignia, rank);
            _deepShownRank = rank;
            if (_deepRank != null) _deepRank.text = MenuText.ToUpperTr(RankCatalog.GetName(rank));

            var seasonProgress = RankCatalog.ProgressToNextRank(Mathf.Max(0, stats.Experience));
            if (_deepSeasonLevel != null) _deepSeasonLevel.text = CareerPanelLogic.SeasonLevelText((int)rank, RankCatalog.All.Count);
            if (_seasonBar != null) _seasonBar.SetValue(seasonProgress, true);

            if (_deepCareerLevel != null)
                _deepCareerLevel.text = _deep != null ? _deep.Level + " / " + CareerService.MaxLevel : "—";
            if (_careerBar != null) _careerBar.SetValue(_deep != null ? _deep.LevelProgress() : 0f, true);
            if (_deepMedalCount != null)
                _deepMedalCount.text = _deep != null ? "Madalya: " + _deep.EarnedMedalCount() + " / " + _deep.Medals.Count + "   ·   TP: " + MenuText.FormatThousands(_deep.Experience) : string.Empty;

            if (DeepVisible) RenderDeepTab();
        }

        private void RenderDeepTab()
        {
            if (_deepContent == null) return;
            for (var i = _deepContent.childCount - 1; i >= 0; i--)
            {
                var child = _deepContent.GetChild(i).gameObject;
                child.SetActive(false);
                UiFactory.DestroySafe(child);
            }

            if (_deep == null)
            {
                var none = UiFactory.Label(_deepContent, "Kariyer servisi yok", UiTheme.FontMedium, TextAnchor.MiddleCenter, UiKitTokens.TextDim);
                UiFactory.Stretch(none);
                return;
            }

            switch (_deepTab)
            {
                case 0: RenderMedals(); break;
                case 1: RenderWeapons(); break;
                case 2: RenderMaps(); break;
                default: RenderHistory(); break;
            }
        }

        // ------------------------------------------------------------------ Madalyalar

        private static Color TierColor(MedalTier t)
        {
            switch (t)
            {
                case MedalTier.Bronz: return new Color(0.80f, 0.50f, 0.20f, 1f);
                case MedalTier.Gumus: return new Color(0.78f, 0.80f, 0.84f, 1f);
                case MedalTier.Altin: return new Color(0.98f, 0.80f, 0.25f, 1f);
                default: return new Color(0.35f, 0.37f, 0.39f, 1f);
            }
        }

        private void RenderMedals()
        {
            var scroll = UiWidgets.ScrollList(_deepContent, out var content, 10f, 0);
            UiFactory.Stretch(scroll);
            var medals = _deep.Medals;
            RectTransform row = null;
            for (var i = 0; i < medals.Count; i++)
            {
                if (i % MedalColumns == 0)
                {
                    row = UiFactory.HorizontalList(content, 10f, 0, TextAnchor.UpperLeft);
                    UiFactory.LayoutSize(row, -1f, 172f, 1f);
                    var hg = row.GetComponent<HorizontalLayoutGroup>();
                    hg.childForceExpandWidth = true;
                    hg.childControlWidth = true;
                }
                MedalCell(row, medals[i]);
            }
        }

        private void MedalCell(RectTransform row, MedalDefinition d)
        {
            var value = _deep.GetMetric(d.Metric);
            var tier = _deep.GetTier(d);
            var locked = tier == MedalTier.None;
            var color = TierColor(tier);

            var cell = UiFactory.Panel(row, locked ? UiTheme.WithAlpha(UiKitTokens.SurfaceRaised, 0.55f) : UiKitTokens.SurfaceRaised, UiSprites.GetRoundedRect(8));
            UiFactory.LayoutSize(cell, -1f, 172f, 1f);
            cell.GetComponent<Image>().raycastTarget = true;

            var ringHolder = UiFactory.CreateRect("Ring", cell);
            UiFactory.Anchor(ringHolder, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(84f, 84f));
            var track = UiFactory.Image(ringHolder, UiSprites.Ring, new Color(1f, 1f, 1f, 0.1f));
            track.raycastTarget = false;
            UiFactory.Stretch(track);
            var fill = UiFactory.Image(ringHolder, UiSprites.Ring, UiTheme.WithAlpha(color, locked ? 0.55f : 1f));
            fill.raycastTarget = false;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            fill.fillAmount = CareerPanelLogic.RingFill(d, value);
            UiFactory.Stretch(fill);

            var glyph = UiFactory.Label(ringHolder, locked ? "—" : tier == MedalTier.Bronz ? "I" : tier == MedalTier.Gumus ? "II" : "III",
                UiTheme.FontMedium, TextAnchor.MiddleCenter, color, FontStyle.Bold);
            glyph.raycastTarget = false;
            UiFactory.Stretch(glyph);

            var title = UiFactory.Label(cell, d.Title, UiTheme.FontSmall, TextAnchor.MiddleCenter, locked ? UiKitTokens.TextMuted : UiKitTokens.Text, FontStyle.Bold);
            title.raycastTarget = false;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(title, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(4f, 34f), new Vector2(-4f, 62f));

            var sub = UiFactory.Label(cell, CareerPanelLogic.TierName(tier) + "  ·  " + value, UiTheme.FontTiny, TextAnchor.MiddleCenter, UiKitTokens.TextDim);
            sub.raycastTarget = false;
            sub.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(sub, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(4f, 8f), new Vector2(-4f, 34f));

            UiKitTooltip.Attach(cell, CareerPanelLogic.MedalTooltip(d, value));
        }

        // ------------------------------------------------------------------ Tablolar

        private static Text Cell(Transform parent, string text, float width, TextAnchor anchor, Color color, bool bold = false)
        {
            var t = UiFactory.Label(parent, text, UiTheme.FontSmall, anchor, color, bold ? FontStyle.Bold : FontStyle.Normal);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.LayoutSize(t, width, 34f, width <= 0f ? 1f : -1f);
            return t;
        }

        private RectTransform TableRow(Transform parent, Color background, float height = 38f)
        {
            var row = UiFactory.Panel(parent, background, UiSprites.GetRoundedRect(4));
            UiFactory.LayoutSize(row, -1f, height, 1f);
            var list = UiFactory.HorizontalList(row, 8f, 0, TextAnchor.MiddleLeft);
            UiFactory.Stretch(list, 14f, 0f, 14f, 0f);
            return list;
        }

        private void RenderWeapons()
        {
            var root = UiFactory.VerticalList(_deepContent, 4f);
            UiFactory.Stretch(root);

            var head = TableRow(root, UiKitTokens.SurfaceRaised, 42f);
            Cell(head, "SİLAH", 0f, TextAnchor.MiddleLeft, UiKitTokens.Sand, true);
            SortHeader(head, "ETKİSİZ", CareerWeaponSort.Kills);
            SortHeader(head, "KAFA %", CareerWeaponSort.HeadshotPercent);
            SortHeader(head, "İSABET %", CareerWeaponSort.Accuracy);
            SortHeader(head, "EN UZUN (m)", CareerWeaponSort.Longest);

            var scroll = UiWidgets.ScrollList(root, out var content, 3f, 0);
            UiFactory.LayoutSize(scroll, -1f, 400f, 1f, 1f);
            var rows = CareerPanelLogic.BuildWeaponRows(_deep.Weapons, _weaponSort, _weaponDesc);
            if (rows.Count == 0)
                Cell(content, "Henüz silah kaydı yok. Harekâta katıl!", 0f, TextAnchor.MiddleLeft, UiKitTokens.TextDim);
            foreach (var r in rows)
            {
                var line = TableRow(content, UiTheme.WithAlpha(UiKitTokens.SurfaceRaised, 0.5f));
                Cell(line, WeaponCatalog.GetDisplayName(r.Id), 0f, TextAnchor.MiddleLeft, UiKitTokens.Text, true);
                Cell(line, r.Kills.ToString(), 170f, TextAnchor.MiddleRight, UiKitTokens.Text);
                Cell(line, r.HeadshotPercent.ToString("0") + "%", 170f, TextAnchor.MiddleRight, UiKitTokens.Text);
                Cell(line, r.Accuracy.ToString("0") + "%", 170f, TextAnchor.MiddleRight, UiKitTokens.Text);
                Cell(line, r.LongestMeters.ToString("0.0"), 170f, TextAnchor.MiddleRight, UiKitTokens.Sand);
            }
        }

        private void SortHeader(Transform parent, string label, CareerWeaponSort sort)
        {
            var active = _weaponSort == sort;
            var arrow = active ? (_weaponDesc ? " ▼" : " ▲") : string.Empty;
            var b = UiFactory.Button(parent, label + arrow, () =>
            {
                if (_weaponSort == sort) _weaponDesc = !_weaponDesc;
                else { _weaponSort = sort; _weaponDesc = true; }
                RenderDeepTab();
            }, UiButtonStyle.Ghost);
            UiFactory.LayoutSize(b, 170f, 34f);
        }

        private void RenderMaps()
        {
            var root = UiFactory.VerticalList(_deepContent, 4f);
            UiFactory.Stretch(root);
            var head = TableRow(root, UiKitTokens.SurfaceRaised, 42f);
            Cell(head, "HARİTA", 0f, TextAnchor.MiddleLeft, UiKitTokens.Sand, true);
            foreach (var h in new[] { "HAREKÂT", "ZAFER", "ZAFER %", "ETKİSİZ", "EN İYİ" })
                Cell(head, h, 140f, TextAnchor.MiddleRight, UiKitTokens.Sand, true);

            var any = false;
            foreach (var kv in _deep.Maps)
            {
                var m = kv.Value;
                if (m == null || m.Matches <= 0) continue;
                any = true;
                var line = TableRow(root, UiTheme.WithAlpha(UiKitTokens.SurfaceRaised, 0.5f), 42f);
                Cell(line, kv.Key, 0f, TextAnchor.MiddleLeft, UiKitTokens.Text, true);
                Cell(line, m.Matches.ToString(), 140f, TextAnchor.MiddleRight, UiKitTokens.Text);
                Cell(line, m.Wins.ToString(), 140f, TextAnchor.MiddleRight, UiKitTokens.Text);
                Cell(line, (CareerPanelLogic.Rate(m.Wins, m.Matches) * 100f).ToString("0") + "%", 140f, TextAnchor.MiddleRight, UiKitTokens.Text);
                Cell(line, m.Kills.ToString(), 140f, TextAnchor.MiddleRight, UiKitTokens.Text);
                Cell(line, m.BestPlacement > 0 ? "#" + m.BestPlacement : "—", 140f, TextAnchor.MiddleRight, UiKitTokens.Sand);
            }

            if (!any)
                Cell(root, "Henüz harita kaydı yok.", 0f, TextAnchor.MiddleLeft, UiKitTokens.TextDim);
        }

        // ------------------------------------------------------------------ Geçmiş

        private void RenderHistory()
        {
            var root = UiFactory.VerticalList(_deepContent, 8f);
            UiFactory.Stretch(root);

            var spark = UiFactory.Panel(root, UiKitTokens.SurfaceRaised, UiSprites.GetRoundedRect(8));
            UiFactory.LayoutSize(spark, -1f, 120f, 1f);
            var cap = UiFactory.Label(spark, "SIRALAMA GRAFİĞİ  " + _deep.PlacementSparkline(), UiTheme.FontTiny, TextAnchor.MiddleLeft, UiKitTokens.Sand, FontStyle.Bold);
            cap.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(cap, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -30f), new Vector2(-16f, -4f));

            var series = _deep.PlacementSeries();
            var history = _deep.History;
            var barsArea = UiFactory.CreateRect("Bars", spark);
            UiFactory.SetRect(barsArea, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(16f, 8f), new Vector2(-16f, -34f));
            for (var i = 0; i < series.Length; i++)
            {
                var x0 = i / (float)CareerService.HistoryCapacity;
                var x1 = (i + 0.8f) / CareerService.HistoryCapacity;
                var bar = UiFactory.Image(barsArea, null, history[i].Won ? UiKitTokens.Sand : UiKitTokens.Khaki);
                bar.raycastTarget = false;
                UiFactory.SetRect(bar, new Vector2(x0, 0f), new Vector2(x1, Mathf.Max(0.04f, series[i])), Vector2.zero, Vector2.zero);
            }

            var head = TableRow(root, UiKitTokens.SurfaceRaised, 38f);
            Cell(head, "SIRA", 0f, TextAnchor.MiddleLeft, UiKitTokens.Sand, true);
            foreach (var h in new[] { "ETKİSİZ", "KAFA", "HASAR", "HAYATTA" })
                Cell(head, h, 150f, TextAnchor.MiddleRight, UiKitTokens.Sand, true);

            var scroll = UiWidgets.ScrollList(root, out var content, 3f, 0);
            UiFactory.LayoutSize(scroll, -1f, 260f, 1f, 1f);
            if (history.Count == 0)
                Cell(content, "Henüz harekât geçmişi yok.", 0f, TextAnchor.MiddleLeft, UiKitTokens.TextDim);
            for (var i = history.Count - 1; i >= 0; i--)
            {
                var e = history[i];
                var line = TableRow(content, e.Won ? UiTheme.WithAlpha(UiKitTokens.SandDark, 0.35f) : UiTheme.WithAlpha(UiKitTokens.SurfaceRaised, 0.5f));
                Cell(line, (e.Won ? "ZAFER  " : string.Empty) + "#" + e.Placement + " / " + e.TotalPlayers, 0f, TextAnchor.MiddleLeft, e.Won ? UiKitTokens.Sand : UiKitTokens.Text, true);
                Cell(line, e.Kills.ToString(), 150f, TextAnchor.MiddleRight, UiKitTokens.Text);
                Cell(line, e.Headshots.ToString(), 150f, TextAnchor.MiddleRight, UiKitTokens.Text);
                Cell(line, MenuText.FormatDamage(e.Damage), 150f, TextAnchor.MiddleRight, UiKitTokens.Text);
                Cell(line, MenuText.FormatDuration(e.SurvivalSeconds), 150f, TextAnchor.MiddleRight, UiKitTokens.TextDim);
            }
        }
    }
}
