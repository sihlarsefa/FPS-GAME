using Project.Presentation.Player;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>Oyun içi HUD (geçici iskelet — tam uygulama yazılıyor).</summary>
    public sealed class HudController : MonoBehaviour
    {
        private IPlayerHudSource _player;
        private Canvas _canvas;

        public RectTransform Root { get; private set; }

        public static HudController Create(IPlayerHudSource player)
        {
            var canvas = UiFactory.CreateCanvas("[HUD]", 10);
            var hud = canvas.gameObject.AddComponent<HudController>();
            hud._player = player;
            hud._canvas = canvas;
            hud.Root = UiFactory.CreateRect("HudRoot", canvas.transform);
            return hud;
        }

        public void SetVisible(bool visible)
        {
            if (Root != null)
                UiFactory.SetVisible(Root, visible);
        }

        public void ShowCenterMessage(string text, float seconds)
        {
        }
    }
}
