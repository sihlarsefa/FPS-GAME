using UnityEngine;

namespace Project.Infrastructure.Weapons
{
    /// <summary>Dürbün ekran katmanı (IMGUI): vinyet/göz kutusu gölgesi, retikül, lens kiri. Yalnız dürbünde etkin.</summary>
    public sealed class ScopeOverlay : MonoBehaviour
    {
        public float Alpha;
        public bool FullVignette;
        public Texture2D Reticle;
        public float ReticleScale = 1f;
        public Vector2 ReticleOffset;
        public Vector2 EyeShift;
        public float EyeDarkness;
        public float DirtAlpha;
        public float ReticleBrightness = 1f;

        private void OnGUI()
        {
            if (Alpha <= 0.01f || Event.current.type != EventType.Repaint)
                return;

            var w = (float)Screen.width;
            var h = (float)Screen.height;
            var prev = GUI.color;
            var prevDepth = GUI.depth;
            GUI.depth = 20;

            if (FullVignette)
            {
                // Dairenin dışı siyah: merkez kare + kenar dolguları.
                var side = Mathf.Min(w, h) * 1.0f;
                var r = new Rect((w - side) * 0.5f, (h - side) * 0.5f, side, side);
                GUI.color = new Color(1f, 1f, 1f, Alpha);
                GUI.DrawTexture(r, ScopeTextures.Vignette());
                GUI.color = new Color(0f, 0f, 0f, Alpha);
                if (r.x > 0f)
                {
                    GUI.DrawTexture(new Rect(0f, 0f, r.x, h), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.xMax, 0f, w - r.xMax, h), Texture2D.whiteTexture);
                }
            }

            if (EyeDarkness > 0.01f)
            {
                var side = Mathf.Min(w, h) * (FullVignette ? 0.95f : 1.25f);
                var cx = w * 0.5f + EyeShift.x * side * 0.12f;
                var cy = h * 0.5f + EyeShift.y * side * 0.12f;
                var r = new Rect(cx - side * 0.5f, cy - side * 0.5f, side, side);
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(EyeDarkness) * Alpha);
                GUI.DrawTexture(r, ScopeTextures.EyeBox());
            }

            if (DirtAlpha > 0.01f)
            {
                GUI.color = new Color(1f, 1f, 1f, DirtAlpha * Alpha);
                GUI.DrawTexture(new Rect(0f, 0f, w, h), ScopeTextures.Dirt(), ScaleMode.StretchToFill);
            }

            if (Reticle != null)
            {
                var s = Mathf.Min(w, h) * 0.9f * ReticleScale;
                var r = new Rect(w * 0.5f - s * 0.5f + ReticleOffset.x, h * 0.5f - s * 0.5f + ReticleOffset.y, s, s);
                var b = ReticleBrightness;
                GUI.color = new Color(b, b, b, Alpha);
                GUI.DrawTexture(r, Reticle);
            }

            GUI.color = prev;
            GUI.depth = prevDepth;
        }
    }
}
