using System;
using Project.Infrastructure.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>Açılış jeneriğinin saf zaman çizelgesi (Unity'siz, test edilebilir). Toplam 1,75 s.</summary>
    public static class MenuIntroTimeline
    {
        public const float LineEnd = 0.40f;
        public const float TitleStart = 0.40f;
        public const float TitleEnd = 0.95f;
        public const float TaglineStart = 0.95f;
        public const float TaglineEnd = 1.25f;
        public const float PulseStart = 1.25f;
        public const float PulseEnd = 1.45f;
        public const float FadeStart = 1.45f;
        public const float Total = 1.75f;

        public static float Seg(float t, float a, float b) => b <= a ? (t >= b ? 1f : 0f) : Mathf.Clamp01((t - a) / (b - a));
        private static float EaseOut(float x) => 1f - (1f - x) * (1f - x) * (1f - x);

        /// <summary>Kırmızı çizginin yatay doluluğu 0..1.</summary>
        public static float LineProgress(float t) => EaseOut(Seg(t, 0f, LineEnd));
        public static float TitleAlpha(float t) => Seg(t, TitleStart, TitleEnd);
        /// <summary>Harf aralığı çarpanı: geniş (1.9) -> sıkı (1.0).</summary>
        public static float LetterSpacing(float t) => 1f + 0.9f * (1f - EaseOut(Seg(t, TitleStart, TitleEnd + 0.1f)));
        public static float TaglineAlpha(float t) => Seg(t, TaglineStart, TaglineEnd);
        /// <summary>Çizgi kalınlık çarpanı: tek vuruş (1 -> 2.6 -> 1).</summary>
        public static float PulseScale(float t)
        {
            var p = Seg(t, PulseStart, PulseEnd);
            return p <= 0f || p >= 1f ? 1f : 1f + 1.6f * Mathf.Sin(p * Mathf.PI);
        }
        public static bool ThudDue(float t) => t >= PulseStart;
        /// <summary>Siyah örtü opaklığı: 1 -> 0 (menüyü açığa çıkarır).</summary>
        public static float CoverAlpha(float t) => 1f - Seg(t, FadeStart, Total);
        public static float ContentAlpha(float t) => 1f - Seg(t, FadeStart, Total);
        public static bool IsDone(float t) => t >= Total;
    }

    /// <summary>
    /// İlk açılış jeneriği (oturum başına bir kez): siyah ekran, kırmızı çizgi, HAREKÂT, slogan, vuruş, açılış.
    /// Space / tık / Esc / Enter anında atlar. Zamanlama gerçek zamanlıdır.
    /// </summary>
    public sealed class MenuIntro : MonoBehaviour
    {
        public const int SortOrder = 200;
        private const string Title = "HAREKÂT";
        private const string Tagline = "KUZGUN VADİSİ SENİ BEKLİYOR";
        private static readonly Color Red = new Color(0xD4 / 255f, 0x3A / 255f, 0x2E / 255f, 1f);

        private static bool _played;

        private Image _cover;
        private RectTransform _line;
        private Image _lineImg;
        private Text[] _letters;
        private Text _tag;
        private CanvasGroup _group;
        private float _t;
        private bool _thud;

        /// <summary>Menü kurulumunda çağrılır; oturumda yalnızca ilk çağrıda oynar.</summary>
        public static void PlayOnce()
        {
            if (_played || !UnityEngine.Application.isPlaying) return;
            _played = true;
            var canvas = UiFactory.CreateCanvas("[Açılış Jeneriği]", SortOrder);
            var intro = canvas.gameObject.AddComponent<MenuIntro>();
            intro.Build(canvas.transform);
        }

        private void Build(Transform root)
        {
            _group = UiFactory.EnsureCanvasGroup(root);
            _cover = UiFactory.Panel(root, Color.black).GetComponent<Image>();
            UiFactory.Stretch(_cover);
            _cover.raycastTarget = true;

            var lineRt = UiFactory.CreateRect("Çizgi", root);
            _line = lineRt;
            _lineImg = lineRt.gameObject.AddComponent<Image>();
            _lineImg.color = Red;
            _lineImg.raycastTarget = false;
            _line.anchorMin = _line.anchorMax = new Vector2(0.5f, 0.5f);
            _line.pivot = new Vector2(0.5f, 0.5f);
            _line.anchoredPosition = Vector2.zero;
            _line.sizeDelta = new Vector2(0f, 2f);

            _letters = new Text[Title.Length];
            for (var i = 0; i < Title.Length; i++)
            {
                var t = UiFactory.Label(root, Title[i].ToString(), 96, TextAnchor.LowerCenter, Color.white, FontStyle.Bold);
                t.raycastTarget = false;
                var rt = t.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(90f, 120f);
                _letters[i] = t;
            }

            _tag = UiFactory.Label(root, Tagline, 22, TextAnchor.UpperCenter, new Color(0.78f, 0.78f, 0.8f, 1f));
            _tag.raycastTarget = false;
            var tr = _tag.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(900f, 40f);
            tr.anchoredPosition = new Vector2(0f, -22f);

            Apply(0f);
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            if (SkipPressed() || MenuIntroTimeline.IsDone(_t))
            {
                Destroy(gameObject);
                return;
            }
            if (!_thud && MenuIntroTimeline.ThudDue(_t))
            {
                _thud = true;
                UiSounds.Play(UiSfx.Press);
            }
            Apply(_t);
        }

        private static bool SkipPressed()
        {
            try
            {
                var k = Keyboard.current;
                if (k != null && (k.spaceKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame)) return true;
                var m = Mouse.current;
                return m != null && m.leftButton.wasPressedThisFrame;
            }
            catch (Exception) { return false; }
        }

        private void Apply(float t)
        {
            var a = MenuIntroTimeline.ContentAlpha(t);
            var c = _cover.color; c.a = MenuIntroTimeline.CoverAlpha(t); _cover.color = c;
            _cover.raycastTarget = c.a > 0.01f;

            const float maxW = 760f;
            _line.sizeDelta = new Vector2(maxW * MenuIntroTimeline.LineProgress(t), 2f * MenuIntroTimeline.PulseScale(t));
            var lc = Red; lc.a = a; _lineImg.color = lc;

            var spacing = MenuIntroTimeline.LetterSpacing(t);
            var adv = 66f * spacing;
            var n = _letters.Length;
            var ta = MenuIntroTimeline.TitleAlpha(t) * a;
            for (var i = 0; i < n; i++)
            {
                var rt = _letters[i].rectTransform;
                rt.anchoredPosition = new Vector2((i - (n - 1) * 0.5f) * adv, 10f);
                var col = _letters[i].color; col.a = ta; _letters[i].color = col;
            }
            var tc = _tag.color; tc.a = MenuIntroTimeline.TaglineAlpha(t) * a; _tag.color = tc;
        }
    }
}
