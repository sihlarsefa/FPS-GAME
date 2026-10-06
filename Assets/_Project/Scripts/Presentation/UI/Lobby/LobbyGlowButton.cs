using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Presentation.UI.Lobby
{
    /// <summary>
    /// Düğme üzerine gelince kırmızı parlama (dış ışıma + üst kenar çizgisi) animasyonu. Hedef nesnenin altına
    /// "Glow" ve "Edge" görselleri ekler; fare/odak ile ease'li açılıp kapanır. Ölçeksiz zamanla çalışır.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyGlowButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        private Image _glow;
        private Image _edge;
        private float _hover;
        private float _pressed;
        private bool _over;
        private bool _focus;
        private bool _down;

        /// <summary>Verilen düğmeye parlama davranışı ekler (varsa mevcut olanı döndürür).</summary>
        public static LobbyGlowButton Attach(Button button)
        {
            var existing = button.GetComponent<LobbyGlowButton>();
            if (existing != null) return existing;
            var g = button.gameObject.AddComponent<LobbyGlowButton>();
            g.Build();
            return g;
        }

        private void Build()
        {
            _glow = UiFactory.Image(transform, UiSprites.SoftCircle, new Color(LobbyTheme.RedGlow.r, LobbyTheme.RedGlow.g, LobbyTheme.RedGlow.b, 0f));
            _glow.gameObject.name = "Glow";
            _glow.raycastTarget = false;
            UiFactory.SetRect(_glow, Vector2.zero, Vector2.one, new Vector2(-18f, -14f), new Vector2(18f, 14f));
            _glow.transform.SetAsFirstSibling();

            _edge = UiFactory.Image(transform, null, new Color(LobbyTheme.RedBright.r, LobbyTheme.RedBright.g, LobbyTheme.RedBright.b, 0f));
            _edge.gameObject.name = "Edge";
            _edge.raycastTarget = false;
            UiFactory.SetRect(_edge, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 2f));
        }

        public void OnPointerEnter(PointerEventData e) => _over = true;
        public void OnPointerExit(PointerEventData e) { _over = false; _down = false; }
        public void OnSelect(BaseEventData e) => _focus = true;
        public void OnDeselect(BaseEventData e) => _focus = false;
        public void OnPointerDown(PointerEventData e) => _down = true;
        public void OnPointerUp(PointerEventData e) => _down = false;

        private void OnDisable()
        {
            _over = _focus = _down = false;
            _hover = _pressed = 0f;
            Apply();
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _hover = LobbyTheme.StepToward(_hover, _over || _focus ? 1f : 0f, dt);
            _pressed = LobbyTheme.StepToward(_pressed, _down ? 1f : 0f, dt);
            Apply();
        }

        private void Apply()
        {
            if (_glow == null) return;
            var a = LobbyTheme.GlowAlpha(_hover, _pressed);
            var gc = LobbyTheme.RedGlow;
            _glow.color = new Color(gc.r, gc.g, gc.b, gc.a * a);
            var ec = LobbyTheme.RedBright;
            _edge.color = new Color(ec.r, ec.g, ec.b, a);
        }
    }
}
