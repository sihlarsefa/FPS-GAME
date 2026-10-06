using Project.Application.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>Kariyer penceresindeki başarım listesi (kaydırılabilir; açık olanlar vurgulanır).</summary>
    public sealed class AchievementListView
    {
        public ScrollRect Scroll { get; private set; }
        public Text Header { get; private set; }

        private AchievementService _service;
        private RectTransform _content;
        private RectTransform _headerRoot;

        public static AchievementListView Create(RectTransform window, AchievementService service)
        {
            var view = new AchievementListView { _service = service };

            var header = UiFactory.CreateRect("AchievementHeader", window);
            UiFactory.SetRect(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(960f, -164f), new Vector2(-40f, -116f));
            view._headerRoot = header;
            var headerList = UiFactory.VerticalList(header, 0f);
            view.Header = UiWidgets.Header(headerList, "BAŞARIMLAR", UiTheme.FontMedium);

            view.Scroll = UiWidgets.ScrollList(window, out var content, 4f, 0);
            UiFactory.SetRect(view.Scroll, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(960f, 110f), new Vector2(-30f, -172f));
            view._content = content;
            view.Refresh();
            view.SetVisible(false);
            return view;
        }

        public void SetVisible(bool visible)
        {
            if (Scroll != null) Scroll.gameObject.SetActive(visible);
            if (_headerRoot != null) _headerRoot.gameObject.SetActive(visible);
            if (visible) Refresh();
        }

        public void Refresh()
        {
            if (_content == null)
                return;
            for (var i = _content.childCount - 1; i >= 0; i--)
                Object.Destroy(_content.GetChild(i).gameObject);

            if (_service == null)
            {
                AddRow("Başarım servisi yok", "", 0f, false);
                return;
            }

            if (Header != null)
                Header.text = "BAŞARIMLAR  " + _service.UnlockedCount + " / " + _service.Definitions.Count;

            // Açılanlar önce.
            for (var pass = 0; pass < 2; pass++)
            {
                foreach (var d in _service.Definitions)
                {
                    var open = _service.IsUnlocked(d.id);
                    if ((pass == 0) != open)
                        continue;
                    var progress = _service.GetProgress(d);
                    AddRow(d.title, d.description + "  (" + progress + "/" + d.target + ")", d.target > 0 ? progress / (float)d.target : 0f, open);
                }
            }
        }

        private void AddRow(string title, string desc, float fraction, bool unlocked)
        {
            var row = UiFactory.CreateRect("Ach", _content);
            UiFactory.LayoutSize(row, -1f, 64f, 1f);
            var bg = row.gameObject.AddComponent<Image>();
            bg.sprite = UiSprites.GetRoundedRect(4);
            bg.type = Image.Type.Sliced;
            bg.color = unlocked ? new Color(0.45f, 0.4f, 0.1f, 0.35f) : new Color(0f, 0f, 0f, 0.18f);
            bg.raycastTarget = true;

            var name = UiFactory.Label(row, title, UiTheme.FontSmall, TextAnchor.MiddleLeft, unlocked ? UiTheme.Text : UiTheme.TextDim, FontStyle.Bold);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(name, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(16f, 0f), new Vector2(-12f, -4f));
            var d = UiFactory.Label(row, desc, UiTheme.FontTiny, TextAnchor.MiddleLeft, UiTheme.TextMuted);
            d.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(d, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(16f, 4f), new Vector2(-12f, 0f));
        }
    }
}
