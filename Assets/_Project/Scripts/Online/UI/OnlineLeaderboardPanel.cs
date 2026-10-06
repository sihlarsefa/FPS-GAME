using System;
using System.Collections.Generic;
using Project.Online.Backend;
using Project.Online.Bootstrap;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Online.UI
{
    /// <summary>
    /// Sıralama paneli: lider tablosunu çeker, kaydırılabilir satırlar gösterir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OnlineLeaderboardPanel : MonoBehaviour
    {
        private const float WindowWidth = 720f;
        private const float WindowHeight = 700f;
        private const float RowHeight = 40f;

        private Action _onClosed;
        private bool _closed;
        private bool _busy;
        private CanvasGroup _group;
        private float _fade;

        private RectTransform _content;
        private Text _status;
        private Button _refreshButton;

        public bool IsOpen => !_closed && this != null;

        public static OnlineLeaderboardPanel Show(Transform parent, Action onClosed = null)
        {
            OnlineServices.Ensure();

            var root = OnlineUi.CreateDimRoot("[Online Sıralama]", parent, out var group);
            var panel = root.gameObject.AddComponent<OnlineLeaderboardPanel>();
            panel._onClosed = onClosed;
            panel._group = group;
            panel.Build(root);
            _ = panel.FetchAsync();
            return panel;
        }

        public void Close()
        {
            if (_closed)
                return;

            _closed = true;
            ClearSelection();
            var callback = _onClosed;
            _onClosed = null;

            gameObject.SetActive(false);
            UiFactory.DestroySafe(gameObject);

            if (callback == null)
                return;
            try { callback(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void Build(RectTransform root)
        {
            var window = OnlineUi.CreateWindow(root, WindowWidth, WindowHeight, "SIRALAMA");
            var body = OnlineUi.CreateBody(window, 110f, 100f);

            var header = UiFactory.HorizontalList(body, 8f, 0, TextAnchor.MiddleLeft);
            UiFactory.LayoutSize(header, -1f, 28f, 1f);
            AddHeaderCell(header, "#", 48f);
            AddHeaderCell(header, "OYUNCU", 220f, 1f);
            AddHeaderCell(header, "TP", 80f);
            AddHeaderCell(header, "ELO", 80f);

            var scrollRoot = UiFactory.CreateRect("ScrollHost", body);
            UiFactory.LayoutSize(scrollRoot, -1f, 420f, 1f);
            var scroll = UiWidgets.ScrollList(scrollRoot, out _content, 4f, 4);
            UiFactory.Stretch(scroll);

            _refreshButton = UiFactory.Button(body, "YENİLE", () => { _ = FetchAsync(); }, UiButtonStyle.Default);
            UiFactory.LayoutSize(_refreshButton, -1f, UiTheme.ButtonHeight, 1f);

            _status = OnlineUi.StatusLabel(body);
            OnlineUi.CreateFooter(window, out _, Close, "GERİ");
        }

        private static void AddHeaderCell(Transform parent, string text, float width, float flex = -1f)
        {
            var label = UiFactory.Label(parent, text, UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.TextMuted, FontStyle.Bold);
            UiFactory.LayoutSize(label, width, 28f, flex);
        }

        private async System.Threading.Tasks.Task FetchAsync()
        {
            if (_busy || _closed)
                return;

            _busy = true;
            OnlineUi.SetStatus(_status, "Yükleniyor…", UiTheme.TextDim);
            UiWidgets.SetInteractable(_refreshButton, false);

            try
            {
                var rows = await OnlineServices.Client.GetLeaderboardAsync("experience", 50);
                if (_closed)
                    return;
                RebuildRows(rows);
                OnlineUi.SetStatus(_status,
                    rows.Count == 0 ? "Henüz kayıt yok." : rows.Count + " oyuncu",
                    UiTheme.TextDim);
            }
            catch (BackendApiException ex)
            {
                if (!_closed)
                    OnlineUi.SetStatus(_status, ex.TurkishMessage, UiTheme.Accent);
            }
            catch (Exception ex)
            {
                if (!_closed)
                    OnlineUi.SetStatus(_status, ex.Message, UiTheme.Accent);
            }
            finally
            {
                _busy = false;
                if (!_closed)
                    UiWidgets.SetInteractable(_refreshButton, true);
            }
        }

        private void RebuildRows(IReadOnlyList<LeaderboardRow> rows)
        {
            if (_content == null)
                return;

            for (var i = _content.childCount - 1; i >= 0; i--)
                UiFactory.DestroySafe(_content.GetChild(i).gameObject);

            if (rows == null)
                return;

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var line = UiFactory.HorizontalList(_content, 8f, 0, TextAnchor.MiddleLeft);
                UiFactory.LayoutSize(line, -1f, RowHeight, 1f);

                var bg = line.gameObject.AddComponent<Image>();
                bg.color = i % 2 == 0
                    ? UiTheme.WithAlpha(UiTheme.Panel, 0.35f)
                    : UiTheme.WithAlpha(UiTheme.Overlay, 0.2f);
                bg.raycastTarget = false;

                AddCell(line, row.Rank.ToString(), 48f, UiTheme.Amber);
                AddCell(line, string.IsNullOrEmpty(row.Username) ? "—" : row.Username, 220f, UiTheme.Text, 1f);
                AddCell(line, row.Value.ToString(), 80f, UiTheme.Text);
                AddCell(line, row.Elo.ToString(), 80f, UiTheme.TextMuted);
            }
        }

        private static void AddCell(Transform parent, string text, float width, Color color, float flex = -1f)
        {
            var label = UiFactory.Label(parent, text, UiTheme.FontSmall, TextAnchor.MiddleLeft, color);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.LayoutSize(label, width, RowHeight, flex);
        }

        private void Update()
        {
            if (_group == null || _fade >= 1f)
                return;
            _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / UiTheme.FadeDuration);
            _group.alpha = _fade;
        }

        private void ClearSelection()
        {
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null &&
                es.currentSelectedGameObject.transform.IsChildOf(transform))
                es.SetSelectedGameObject(null);
        }
    }
}
