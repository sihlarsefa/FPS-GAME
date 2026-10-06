using System;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI.Lobby
{
    /// <summary>Sağ alttaki büyük kırmızı OYNA düğmesi; arkasında nabız atan kırmızı parlama (düşük kalitede sabit).</summary>
    [DisallowMultipleComponent]
    public sealed class LobbyPlayButton : MonoBehaviour
    {
        private Image _pulse;
        private bool _animate = true;

        public static Button Create(RectTransform parent, Action onClick)
        {
            var root = UiFactory.CreateRect("LobbyPlayButton", parent);
            UiFactory.Anchor(root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-48f, 48f), new Vector2(320f, 150f));
            var comp = root.gameObject.AddComponent<LobbyPlayButton>();
            comp._animate = QualitySettings.GetQualityLevel() > 1;
            comp._pulse = UiFactory.Image(root, UiSprites.SoftCircle, new Color(LobbyTheme.Red.r, LobbyTheme.Red.g, LobbyTheme.Red.b, 0.3f));
            comp._pulse.raycastTarget = false;
            UiFactory.SetRect(comp._pulse, Vector2.zero, Vector2.one, new Vector2(-24f, -18f), new Vector2(24f, 18f));

            var button = UiFactory.Button(root, "OYNA  ›", () => onClick?.Invoke(), UiButtonStyle.Primary);
            UiFactory.Stretch(button);
            var colors = button.colors;
            colors.normalColor = LobbyTheme.Red;
            colors.highlightedColor = LobbyTheme.RedBright;
            colors.selectedColor = LobbyTheme.RedBright;
            colors.pressedColor = LobbyTheme.RedDeep;
            button.colors = colors;
            var label = UiFactory.GetButtonLabel(button);
            if (label != null) label.fontSize = 44;
            LobbyGlowButton.Attach(button);
            return button;
        }

        private void Update()
        {
            var p = _animate ? LobbyTheme.PlayPulse(Time.unscaledTime) : 0.5f;
            var c = LobbyTheme.Red;
            _pulse.color = new Color(c.r, c.g, c.b, 0.12f + 0.4f * p);
            _pulse.rectTransform.localScale = Vector3.one * (1f + 0.04f * p);
        }
    }
}
