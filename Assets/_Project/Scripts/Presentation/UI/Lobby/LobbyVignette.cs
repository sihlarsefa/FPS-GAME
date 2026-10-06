using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI.Lobby
{
    /// <summary>
    /// Animasyonlu arka plan vinyeti: koyu kenar karartması + yavaş nabız atan kırmızı kenar ışıması.
    /// Düşük kalite kademesinde (kalite seviyesi 0-1) nabız durur, sabit vinyet kalır.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyVignette : MonoBehaviour
    {
        private Image _dark;
        private Image _red;
        private bool _animate = true;

        /// <summary>Ebeveynin tamamını kaplayan, ışın almayan vinyet kurar.</summary>
        public static LobbyVignette Create(Transform parent)
        {
            var root = UiFactory.CreateRect("LobbyVignette", parent);
            UiFactory.Stretch(root);
            var v = root.gameObject.AddComponent<LobbyVignette>();
            v._dark = UiFactory.Image(root, UiSprites.Vignette, new Color(0f, 0f, 0f, 0.55f));
            v._dark.raycastTarget = false;
            UiFactory.Stretch(v._dark);
            v._red = UiFactory.Image(root, UiSprites.Vignette, new Color(LobbyTheme.RedDeep.r, LobbyTheme.RedDeep.g, LobbyTheme.RedDeep.b, 0.2f));
            v._red.raycastTarget = false;
            UiFactory.Stretch(v._red);
            // ENTEGRASYON: MainMenuController.cs icinde ana canvas'in en arka cocugu olarak LobbyVignette.Create(canvas.transform) cagrilip SetAsFirstSibling yapilacak.
            v._animate = QualitySettings.GetQualityLevel() > 1;
            return v;
        }

        /// <summary>Nabız animasyonu açık mı (düşük kalitede kapatılır).</summary>
        public bool Animate
        {
            get => _animate;
            set => _animate = value;
        }

        private void Update()
        {
            var time = Time.unscaledTime;
            var pulse = _animate ? LobbyTheme.VignettePulse(time) : 0.5f;
            _dark.color = new Color(0f, 0f, 0f, _animate ? LobbyTheme.VignetteAlpha(time) : 0.55f);
            var rc = LobbyTheme.RedDeep;
            _red.color = new Color(rc.r, rc.g, rc.b, 0.08f + 0.22f * pulse);
        }
    }
}
