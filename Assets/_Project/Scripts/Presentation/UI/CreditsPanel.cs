using System.Text;
using Project.Application.Credits;
using Project.Infrastructure.Content;
using Project.Infrastructure.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>"YAPIMCILAR" ekranı: Resources/Credits.json'dan lisans/kredi satırları. Ana menüye ExtraButtons ile eklenir.</summary>
    public sealed class CreditsPanel : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            const string label = "YAPIMCILAR";
            var list = MainMenuController.ExtraButtons;
            for (var i = 0; i < list.Count; i++)
                if (list[i].label == label) return;
            list.Add((label, root => Show(root)));
        }

        public static CreditsPanel Show(Transform parent)
        {
            var root = UiFactory.CreateRect("[Yapimcilar]", parent);
            root.SetAsLastSibling();
            UiFactory.Stretch(root);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = UiTheme.WithAlpha(UiTheme.Overlay, 0.55f);
            dim.raycastTarget = true;
            var panel = root.gameObject.AddComponent<CreditsPanel>();
            panel.Build(root);
            return panel;
        }

        private void Build(RectTransform root)
        {
            var window = UiFactory.Panel(root, UiTheme.Panel, UiSprites.ChamferRect);
            UiFactory.Anchor(window, UiAnchor.Center, Vector2.zero, new Vector2(820f, 700f));
            var title = UiFactory.Label(window, Loc.Get("credits.title", "YAPIMCILAR"), UiTheme.FontTitle, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold);
            UiFactory.SetRect(title, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(36f, -88f), new Vector2(-36f, -24f));

            var scroll = UiWidgets.ScrollList(window, out var content, 6f, 0);
            UiFactory.SetRect(scroll, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(30f, 100f), new Vector2(-30f, -100f));

            var lines = CreditsLogic.BuildCreditLines(CreditsLoader.Load());
            if (lines.Count == 0) lines.Add(Loc.Get("credits.empty", "Kredi kaydı bulunamadı."));
            foreach (var l in lines)
            {
                var t = UiFactory.Label(content, l, UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.TextDim, FontStyle.Normal);
                UiFactory.LayoutSize(t, 740f, 36f);
            }

            var footer = UiFactory.HorizontalList(window, 14f, 0, TextAnchor.MiddleRight);
            UiFactory.SetRect(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 24f), new Vector2(-40f, 24f + UiTheme.ButtonHeight));
            UiFactory.FlexibleSpacer(footer);
            var back = UiFactory.Button(footer, Loc.Get("credits.back", "GERİ"), Close, UiButtonStyle.Default);
            UiFactory.LayoutSize(back, 180f, UiTheme.ButtonHeight);
        }

        public void Close() { if (this != null) Destroy(gameObject); }

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
