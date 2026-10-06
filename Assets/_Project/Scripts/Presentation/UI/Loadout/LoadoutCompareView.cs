using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Donanım ekranı eklenti karşılaştırma paneli: odaklanan yuvanın tüm seçeneklerini mevcut kuruluma göre
    /// çubuk farklarıyla (yeşil +, kırmızı -) listeler; satıra tıklamak o eklentiyi kuşanır. Sıralama LoadoutStatCompare'dadır.
    /// </summary>
    public sealed class LoadoutCompareView : MonoBehaviour
    {
        private const int MaxRows = 6;
        private const float RowHeight = 46f;
        private const float HeaderHeight = 40f;
        private const float AutoHideSeconds = 9f;

        private static readonly string[] ShortLabels = { "HSR", "HIZ", "MNZ", "KNT", "HRK", "NŞN" };

        private sealed class RowView
        {
            public RectTransform Root;
            public Image Back;
            public Image CurrentBar;
            public Text Name;
            public Text Deltas;
            public Text Net;
            public Button Button;
            public string ItemId;
        }

        private readonly RowView[] _rows = new RowView[MaxRows];
        private RectTransform _panel;
        private Text _header;
        private Action<string> _onPick;
        private float _hideTimer;
        private bool _built;

        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

        /// <summary>Paneli verilen ebeveyne (sağ üst köşe) kurar.</summary>
        public static LoadoutCompareView Create(RectTransform parent)
        {
            var root = UiFactory.CreateRect("CompareView", parent);
            var view = root.gameObject.AddComponent<LoadoutCompareView>();
            view.Build(root);
            return view;
        }

        private void Build(RectTransform root)
        {
            _panel = root;
            UiFactory.SetRect(root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-380f, -24f - HeaderHeight - RowHeight * MaxRows), new Vector2(-18f, -24f));
            var back = UiFactory.Image(root, UiSprites.White, UiTheme.WithAlpha(UiTheme.PanelDark, 0.95f));
            UiFactory.Stretch(back);
            var edge = UiFactory.Image(root, UiSprites.White, UiTheme.Accent);
            edge.raycastTarget = false;
            UiFactory.SetRect(edge, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -3f), new Vector2(0f, 0f));

            _header = UiFactory.Label(root, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.Khaki, FontStyle.Bold);
            _header.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -HeaderHeight), new Vector2(-10f, -4f));

            for (var i = 0; i < MaxRows; i++)
                _rows[i] = BuildRow(root, i);
            _built = true;
            root.gameObject.SetActive(false);
        }

        private RowView BuildRow(RectTransform root, int index)
        {
            var r = new RowView();
            var top = -HeaderHeight - index * RowHeight;
            r.Root = UiFactory.CreateRect("Opt_" + index, root);
            UiFactory.SetRect(r.Root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(6f, top - RowHeight + 3f), new Vector2(-6f, top));
            r.Back = UiFactory.Image(r.Root, UiSprites.White, UiTheme.WithAlpha(UiTheme.Track, 0.9f));
            UiFactory.Stretch(r.Back);
            r.CurrentBar = UiFactory.Image(r.Root, UiSprites.White, UiTheme.Accent);
            r.CurrentBar.raycastTarget = false;
            UiFactory.SetRect(r.CurrentBar, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(4f, 0f));

            r.Name = UiFactory.Label(r.Root, string.Empty, UiTheme.FontSmall, TextAnchor.UpperLeft, UiTheme.Text, FontStyle.Bold);
            r.Name.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(r.Name, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-64f, -2f));
            r.Deltas = UiFactory.Label(r.Root, string.Empty, UiTheme.FontTiny - 2, TextAnchor.LowerLeft, UiTheme.TextDim, FontStyle.Bold);
            r.Deltas.horizontalOverflow = HorizontalWrapMode.Overflow;
            r.Deltas.supportRichText = true;
            UiFactory.SetRect(r.Deltas, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(12f, 2f), new Vector2(-4f, 0f));
            r.Net = UiFactory.Label(r.Root, string.Empty, 26, TextAnchor.MiddleRight, UiTheme.Text, FontStyle.Bold);
            r.Net.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(r.Net, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-62f, 0f), new Vector2(-8f, 0f));

            r.Button = r.Root.gameObject.AddComponent<Button>();
            r.Button.targetGraphic = r.Back;
            r.Button.transition = Selectable.Transition.None;
            r.Button.navigation = new Navigation { mode = Navigation.Mode.None };
            r.Button.onClick.AddListener(() => Pick(r));
            return r;
        }

        private void Pick(RowView row)
        {
            _hideTimer = AutoHideSeconds;
            _onPick?.Invoke(row.ItemId);
        }

        /// <summary>Paneli seçeneklerle doldurup açar. onPick(itemId) boş yuva için null verir.</summary>
        public void Show(string slotTitle, IReadOnlyList<SlotOption> options, Action<string> onPick)
        {
            if (!_built || options == null || options.Count == 0)
            {
                Hide();
                return;
            }

            _onPick = onPick;
            _header.text = slotTitle + " SEÇENEKLERİ  ·  fark mevcut kuruluma göre";
            var shown = Mathf.Min(options.Count, MaxRows);
            for (var i = 0; i < MaxRows; i++)
            {
                var row = _rows[i];
                row.Root.gameObject.SetActive(i < shown);
                if (i >= shown)
                    continue;
                Fill(row, options[i]);
            }

            // Paneli içerik yüksekliğine kırp.
            var h = HeaderHeight + shown * RowHeight + 6f;
            UiFactory.SetRect(_panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-380f, -24f - h), new Vector2(-18f, -24f));
            _panel.gameObject.SetActive(true);
            _hideTimer = AutoHideSeconds;
        }

        public void Hide()
        {
            if (_panel != null)
                _panel.gameObject.SetActive(false);
        }

        private static void Fill(RowView row, SlotOption opt)
        {
            row.ItemId = opt.ItemId;
            row.Name.text = MenuText.ToUpperTr(opt.Name) + (opt.IsCurrent ? "   (TAKILI)" : string.Empty);
            row.Name.color = opt.IsCurrent ? UiTheme.Accent : UiTheme.Text;
            row.CurrentBar.enabled = opt.IsCurrent;
            row.Back.color = UiTheme.WithAlpha(opt.IsCurrent ? UiTheme.PanelBorder : UiTheme.Track, opt.IsCurrent ? 0.55f : 0.9f);

            var cmp = opt.VsCurrent;
            if (opt.IsCurrent || cmp == null || cmp.IsIdentical)
            {
                row.Deltas.text = opt.IsCurrent ? "şu an takılı" : "fark yok";
                row.Net.text = string.Empty;
                return;
            }

            row.Deltas.text = DeltaLine(cmp);
            var net = cmp.OverallDelta;
            row.Net.text = net == 0 ? "=" : LoadoutStatCompare.Signed(net);
            row.Net.color = net > 0 ? UiTheme.Success : (net < 0 ? UiTheme.Danger : UiTheme.TextDim);
        }

        /// <summary>Renkli "KNT +8 HRK -3 ..." satırı (yalnız değişen çubuklar + şarjör/ağırlık).</summary>
        public static string DeltaLine(StatComparison cmp)
        {
            var sb = new StringBuilder(96);
            for (var i = 0; i < cmp.Bars.Length; i++)
            {
                var d = cmp.Bars[i];
                if (d.Points == 0)
                    continue;
                Append(sb, ShortLabels[i] + " " + LoadoutStatCompare.Signed(d.Points), d.Trend);
            }

            if (cmp.MagazineDelta != 0)
                Append(sb, "ŞRJ " + LoadoutStatCompare.Signed(cmp.MagazineDelta), cmp.MagazineDelta > 0 ? StatTrend.Better : StatTrend.Worse);
            var w = LoadoutStatCompare.WeightText(cmp.WeightDelta);
            if (w.Length > 0)
                Append(sb, w, LoadoutStatCompare.WeightTrend(cmp.WeightDelta));
            return sb.ToString();
        }

        private static void Append(StringBuilder sb, string text, StatTrend trend)
        {
            if (sb.Length > 0)
                sb.Append("  ");
            var c = trend == StatTrend.Better ? UiTheme.Success : (trend == StatTrend.Worse ? UiTheme.Danger : UiTheme.TextDim);
            sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(c)).Append('>').Append(text).Append("</color>");
        }

        private void Update()
        {
            if (!IsOpen)
                return;
            _hideTimer -= Time.unscaledDeltaTime;
            if (_hideTimer <= 0f)
                Hide();
        }
    }
}
