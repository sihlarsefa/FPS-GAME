using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Dürbün görünümü (IsScoped): ekranı kaplayan siyah maske ve ortasında dairesel mercek (kenarı yumuşak, iç kenarda
    /// hafif kararma), mil-dot artı (ince orta çizgiler, kalın dış direkler, her yönde 4 mil noktası) ve büyütme yazısı.
    /// Mercek çapı ekran yüksekliğidir; yan boşluklar siyah panellerle doldurulur. Hızlı açılır/kapanır.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScopeOverlayView : MonoBehaviour
    {
        private const int MilDots = 4;
        private const float FadeInSpeed = 12f;
        private const float FadeOutSpeed = 18f;

        private static Sprite _lensMask;

        private HudContext _ctx;
        private CanvasGroup _group;
        private RectTransform _lens;
        private RectTransform _left;
        private RectTransform _right;
        private RectTransform _reticle;
        private Text _zoomText;
        private float _alpha;
        private float _shownHeight = -1f;
        private float _shownWidth = -1f;
        private float _shownZoom = -1f;

        public RectTransform Root { get; private set; }

        /// <summary>Dürbün görünümü açık mı (son kare)?</summary>
        public bool IsShowing => _alpha > 0.01f;

        public static ScopeOverlayView Create(RectTransform parent, HudContext context)
        {
            var root = HudBuild.Fill("ScopeOverlay", parent);
            var view = root.gameObject.AddComponent<ScopeOverlayView>();
            view._ctx = context;
            view.Root = root;
            view.Build();
            return view;
        }

        private void Build()
        {
            _group = HudBuild.PassiveGroup(Root, 0f);

            _lens = HudBuild.Rect("Lens", Root, HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(1080f, 1080f));
            HudBuild.FillImage("Mask", _lens, LensMask, Color.black);

            _left = HudBuild.Fill("Left", Root);
            HudBuild.FillImage("Fill", _left, UiSprites.White, Color.black);
            _right = HudBuild.Fill("Right", Root);
            HudBuild.FillImage("Fill", _right, UiSprites.White, Color.black);

            _reticle = HudBuild.Rect("Reticle", _lens, HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(1000f, 1000f));
            BuildReticle(_reticle);

            _zoomText = HudBuild.Text("Zoom", _lens, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleCenter,
                UiTheme.WithAlpha(UiTheme.Text, 0.75f), FontStyle.Bold, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 70f), new Vector2(120f, 24f));

            Layout(1920f, 1080f);
            HudBuild.SetActive(Root, false);
        }

        /// <summary>Mil-dot artı: koordinatlar 1000 birimlik kare içinde (yarıçap ≈ 470).</summary>
        private static void BuildReticle(RectTransform reticle)
        {
            const float radius = 470f;
            const float postStart = 190f;
            const float thin = 1.6f;
            const float thick = 7f;
            var ink = new Color(0.02f, 0.02f, 0.02f, 0.95f);

            // İnce orta çizgiler (merkezde küçük boşluk yok — klasik mil-dot).
            HudBuild.Image("ThinH", reticle, UiSprites.White, ink, Vector2.zero, new Vector2(postStart * 2f, thin));
            HudBuild.Image("ThinV", reticle, UiSprites.White, ink, new Vector2(0f, postStart * 0.5f), new Vector2(thin, postStart));
            HudBuild.Image("ThinVDown", reticle, UiSprites.White, ink, new Vector2(0f, -postStart * 0.5f), new Vector2(thin, postStart));

            // Kalın dış direkler (sol, sağ, alt; üstte ince devam).
            var postLength = radius - postStart;
            HudBuild.Image("PostLeft", reticle, UiSprites.White, ink, new Vector2(-(postStart + postLength * 0.5f), 0f), new Vector2(postLength, thick));
            HudBuild.Image("PostRight", reticle, UiSprites.White, ink, new Vector2(postStart + postLength * 0.5f, 0f), new Vector2(postLength, thick));
            HudBuild.Image("PostBottom", reticle, UiSprites.White, ink, new Vector2(0f, -(postStart + postLength * 0.5f)), new Vector2(thick, postLength));
            HudBuild.Image("ThinTop", reticle, UiSprites.White, ink, new Vector2(0f, postStart + postLength * 0.5f), new Vector2(thin, postLength));

            // Mil noktaları.
            var spacing = postStart / (MilDots + 1);
            for (var i = 1; i <= MilDots; i++)
            {
                var d = spacing * i;
                var size = 6.5f;
                HudBuild.Image("DotL" + i, reticle, UiSprites.Circle, ink, new Vector2(-d, 0f), new Vector2(size, size));
                HudBuild.Image("DotR" + i, reticle, UiSprites.Circle, ink, new Vector2(d, 0f), new Vector2(size, size));
                HudBuild.Image("DotU" + i, reticle, UiSprites.Circle, ink, new Vector2(0f, d), new Vector2(size, size));
                HudBuild.Image("DotD" + i, reticle, UiSprites.Circle, ink, new Vector2(0f, -d), new Vector2(size, size));
            }

            // Merkez kırmızı nokta (düşük ışıkta seçilebilirlik).
            HudBuild.Image("Center", reticle, UiSprites.Circle, new Color(0.9f, 0.1f, 0.08f, 0.9f), Vector2.zero, new Vector2(4f, 4f));
        }

        private void Layout(float width, float height)
        {
            _shownWidth = width;
            _shownHeight = height;

            var diameter = Mathf.Max(64f, height);
            _lens.sizeDelta = new Vector2(diameter, diameter);
            var scale = diameter / 1000f;
            _reticle.localScale = new Vector3(scale, scale, 1f);

            var side = Mathf.Max(0f, (width - diameter) * 0.5f) + 2f;
            UiFactory.SetRect(_left, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(side, 0f));
            UiFactory.SetRect(_right, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-side, 0f), Vector2.zero);
        }

        /// <summary>HUD denetleyicisi her karede çağırır.</summary>
        public void Tick(float deltaTime, bool hidden)
        {
            var scoped = false;
            var zoom = 1f;
            if (!hidden && _ctx.PlayerValid && _ctx.LocalAlive)
            {
                try
                {
                    scoped = _ctx.Player.IsScoped;
                    zoom = _ctx.Player.ScopeZoom;
                }
                catch (System.Exception)
                {
                    scoped = false;
                }
            }

            var target = scoped ? 1f : 0f;
            _alpha = Mathf.MoveTowards(_alpha, target, deltaTime * (scoped ? FadeInSpeed : FadeOutSpeed));
            var visible = _alpha > 0.001f;
            HudBuild.SetActive(Root, visible);
            if (!visible)
                return;

            HudBuild.SetAlpha(_group, _alpha);

            var rect = Root.rect;
            if (Mathf.Abs(rect.width - _shownWidth) > 0.5f || Mathf.Abs(rect.height - _shownHeight) > 0.5f)
                Layout(rect.width, rect.height);

            if (Mathf.Abs(zoom - _shownZoom) > 0.01f)
            {
                _shownZoom = zoom;
                UiFactory.SetText(_zoomText, zoom > 1.05f ? HudFormat.Zoom(zoom) : string.Empty);
            }
        }

        /// <summary>Mercek maskesi: içi saydam daire (kenarı yumuşak, iç kenarda hafif kararma), dışı siyah.</summary>
        private static Sprite LensMask
        {
            get
            {
                if (_lensMask != null)
                    return _lensMask;

                const int size = 512;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "HUD_ScopeMask",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave
                };

                var pixels = new Color32[size * size];
                var c = size * 0.5f;
                const float edge = 0.485f;
                const float soft = 0.012f;
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var dx = (x + 0.5f - c) / size;
                        var dy = (y + 0.5f - c) / size;
                        var r = Mathf.Sqrt(dx * dx + dy * dy);
                        float a;
                        if (r >= edge)
                        {
                            a = 1f;
                        }
                        else if (r >= edge - soft)
                        {
                            a = Mathf.SmoothStep(0.55f, 1f, (r - (edge - soft)) / soft);
                        }
                        else
                        {
                            // İç kenar gölgesi (mercek tüpü).
                            var t = Mathf.Clamp01((r - 0.36f) / (edge - soft - 0.36f));
                            a = t * t * 0.55f;
                        }

                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                _lensMask = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
                _lensMask.name = "HUD_ScopeMask";
                _lensMask.hideFlags = HideFlags.DontSave;
                return _lensMask;
            }
        }
    }
}
