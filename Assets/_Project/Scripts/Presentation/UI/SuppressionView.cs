using Project.Infrastructure.Rendering;
using Project.Presentation.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Bastırma görünümü: yakın ıska mermileri ölçeri doldurur; ölçer kenar karartması + renk solması çizer.
    /// Tek kaynak Presentation/Player/Suppression (ayrı ölçer yok). Kendi alt-seviye overlay canvas'ına çizer, HUD'a dokunmaz.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SuppressionView : MonoBehaviour
    {
        private Image _vignette;
        private Image _desaturate;
        private Canvas _canvas;

        /// <summary>Güncel bastırma (0..1) — Suppression.Value'dan okunur.</summary>
        public float Meter => Suppression.Value;

        public static SuppressionView Create(GameObject host)
        {
            var view = host.AddComponent<SuppressionView>();
            view.Build();
            return view;
        }

        private void Build()
        {
            var canvasGo = new GameObject("SuppressionCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = -5;
            var root = (RectTransform)canvasGo.transform;
            _desaturate = HudBuild.FillImage("Desaturate", root, UiSprites.White, new Color(0.45f, 0.45f, 0.45f, 0f));
            _vignette = HudBuild.FillImage("Vignette", root, UiSprites.Vignette, new Color(0f, 0f, 0f, 0f));
            _desaturate.raycastTarget = false;
            _vignette.raycastTarget = false;
            _desaturate.enabled = false;
            _vignette.enabled = false;
        }

        /// <summary>Yakın ıska: ölçer Suppression.ReportNearMiss ile dolar (bkz. ateş kancası); burada ayrı ölçer yok.</summary>
        public void OnNearMiss(float distance)
        {
        }

        public void ResetMeter()
        {
            Apply(); // sıfırlamayı SuppressionDriver yapar (ölü oyuncu → Suppression.Clear)
        }

        public void Tick(float deltaTime)
        {
            if (Meter <= 0f && !_vignette.enabled)
                return;
            Apply();
        }

        private void Apply()
        {
            var v = ScreenEffectsMath.VignetteAlpha(Meter);
            var d = ScreenEffectsMath.DesaturationAlpha(Meter);
            var on = v > 0.003f;
            _vignette.enabled = on;
            _desaturate.enabled = on;
            if (!on)
                return;
            _vignette.color = new Color(0f, 0f, 0f, v);
            _desaturate.color = new Color(0.45f, 0.45f, 0.45f, d);
        }
    }
}
