using System;
using System.IO;
using Project.Infrastructure.Replay;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.Replay
{
    /// <summary>
    /// Ana menü "TEKRARLAR" paneli: kayıtlı maç tekrarlarını listeler ve seçileni izleyiciyle açar.
    /// <see cref="MainMenuController.ExtraButtons"/> ile kaydolur (MainMenuController'a dokunulmaz).
    /// </summary>
    public sealed class ReplayMenuPanel : MonoBehaviour
    {
        private RectTransform _content;
        private Text _info;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            const string label = "TEKRARLAR";
            var list = MainMenuController.ExtraButtons;
            for (var i = 0; i < list.Count; i++)
                if (list[i].label == label)
                    return;
            list.Add((label, root => Show(root)));
        }

        public static ReplayMenuPanel Show(Transform parent)
        {
            var root = UiFactory.CreateRect("[Tekrarlar]", parent);
            root.SetAsLastSibling();
            UiFactory.Stretch(root);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = UiKitTokens.Scrim;
            dim.raycastTarget = true;

            var panel = root.gameObject.AddComponent<ReplayMenuPanel>();
            panel.Build(root);
            return panel;
        }

        private void Build(RectTransform root)
        {
            var parts = UiKitPanel.Window(root, new Vector2(1000f, 740f), "TEKRARLAR");
            _info = parts.Subtitle;
            var body = parts.Body;

            var scroll = UiKitScrollFade.VerticalList(body, out _content, UiKitTokens.Bg, 10f, 2);
            UiFactory.SetRect(scroll, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 74f), Vector2.zero);

            var footer = UiFactory.HorizontalList(body, 14f, 0, TextAnchor.MiddleRight);
            UiFactory.SetRect(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 56f));
            UiFactory.FlexibleSpacer(footer);
            UiKitButton.Create(footer, "GERİ", Close, UiKitButtonKind.Default, 180f, 52f);

            Refresh();
        }

        private void Refresh()
        {
            var files = ReplayRecorder.ListFiles();
            _info.text = files.Count == 0
                ? "Henüz kayıtlı tekrar yok. Bir harekât oynayın; maç bitince otomatik kaydedilir."
                : files.Count + " tekrar  |  en yeni üstte  |  son 20 tekrar saklanır";

            for (var i = 0; i < files.Count; i++)
                AddRow(files[i]);
        }

        private void AddRow(string path)
        {
            var card = UiKitPanel.Card(_content, UiKitTokens.Surface, 10);
            UiFactory.LayoutSize(card, -1f, 96f, 1f);
            var outline = UiFactory.Image(card, UiSprites.GetRoundedRectOutline(10), UiKitTokens.Border);
            outline.raycastTarget = false;
            UiFactory.Stretch(outline);

            string when, map, detail, result = null;
            Color resultColor = UiKitTokens.TextDim;
            try
            {
                var data = ReplayRecorder.Load(path);
                if (data == null)
                {
                    when = Path.GetFileNameWithoutExtension(path);
                    map = "Okunamadı";
                    detail = "Bozuk dosya";
                }
                else
                {
                    var kills = data.Kills().Count;
                    when = data.RecordedAtUtcTicks > 0 ? new DateTime(data.RecordedAtUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("dd.MM.yyyy  HH:mm") : Path.GetFileNameWithoutExtension(path);
                    var seconds = Mathf.FloorToInt(data.Duration);
                    map = string.IsNullOrEmpty(data.MapId) ? "Harita" : data.MapId;
                    detail = "Süre " + (seconds / 60) + ":" + (seconds % 60).ToString("00") + "   ·   " + data.Players.Count + " asker   ·   " + kills + " ölüm";
                }
            }
            catch (Exception e)
            {
                when = Path.GetFileNameWithoutExtension(path);
                map = "Hata";
                detail = e.Message;
                result = "HATA";
                resultColor = UiKitTokens.Danger;
            }

            var bar = UiFactory.Image(card, null, resultColor == UiKitTokens.Danger ? UiKitTokens.Danger : UiKitTokens.Sand);
            bar.raycastTarget = false;
            UiFactory.SetRect(bar, new Vector2(0f, 0.15f), new Vector2(0f, 0.85f), new Vector2(0f, 0f), new Vector2(4f, 0f));

            var mapLabel = UiFactory.Label(card, MenuText.ToUpperTr(map), UiKitTokens.FontHeading - 4, TextAnchor.MiddleLeft, UiKitTokens.Text, FontStyle.Bold);
            mapLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(mapLabel, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(24f, 0f), new Vector2(-300f, -10f));

            var whenLabel = UiFactory.Label(card, when + (result != null ? "   ·   " + result : string.Empty), UiKitTokens.FontCaption + 1, TextAnchor.MiddleLeft, UiKitTokens.Sand, FontStyle.Bold);
            whenLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(whenLabel, new Vector2(0f, 0.28f), new Vector2(1f, 0.52f), new Vector2(24f, 0f), new Vector2(-300f, 0f));

            var detailLabel = UiFactory.Label(card, detail, UiKitTokens.FontCaption, TextAnchor.MiddleLeft, UiKitTokens.TextDim);
            detailLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(detailLabel, new Vector2(0f, 0f), new Vector2(1f, 0.3f), new Vector2(24f, 6f), new Vector2(-300f, 0f));

            var p = path;
            var play = UiKitButton.Create(card, "İZLE", () => Play(p), UiKitButtonKind.Primary, 120f, 48f);
            UiFactory.Anchor(play, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(120f, 48f));
            var del = UiKitButton.Create(card, "SİL", () => Remove(p), UiKitButtonKind.Danger, 110f, 48f);
            UiFactory.Anchor(del, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-146f, 0f), new Vector2(110f, 48f));
        }

        private void Play(string path)
        {
            if (!ReplayViewer.Open(path))
                _info.text = "Tekrar açılamadı (dosya bozuk ya da çok kısa).";
        }

        private void Remove(string path)
        {
            try { File.Delete(path); }
            catch (Exception e) { _info.text = "Silinemedi: " + e.Message; }
            for (var i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);
            Refresh();
        }

        public void Close()
        {
            if (this != null)
                Destroy(gameObject);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame))
            {
                OverlayState.ConsumeEscape();
                Close();
            }
        }
    }
}
