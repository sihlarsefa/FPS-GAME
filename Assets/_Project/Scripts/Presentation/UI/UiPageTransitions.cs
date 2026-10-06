using System.Collections.Generic;
using Project.Infrastructure.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>Sayfa geçişi zamanlama matematiği (saf; test edilebilir).</summary>
    public static class UiPageTransitionMath
    {
        public const float WipeDuration = 0.2f;
        public const float OldFadeDuration = 0.1f;
        public const float ItemDuration = 0.18f;
        public const float ItemStagger = 0.03f;
        public const float ItemSlide = 12f;
        public const int MaxItems = 8;

        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>Yumuşak çıkış (cubic ease-out).</summary>
        public static float EaseOut(float t)
        {
            t = Clamp01(t);
            var u = 1f - t;
            return 1f - u * u * u;
        }

        /// <summary>Yumuşak giriş-çıkış.</summary>
        public static float EaseInOut(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>İleri +1 (soldan sağa), geri -1.</summary>
        public static float Direction(bool reverse) => reverse ? -1f : 1f;

        public static int ItemCount(int childCount) => childCount < 0 ? 0 : childCount > MaxItems ? MaxItems : childCount;

        public static float ItemDelay(int index) => (index < 0 ? 0 : index) * ItemStagger;

        public static float ItemProgress(float time, int index) => EaseOut((time - ItemDelay(index)) / ItemDuration);

        public static float ItemOffsetX(float time, int index, bool reverse) =>
            -Direction(reverse) * ItemSlide * (1f - ItemProgress(time, index)) * -1f;

        /// <summary>Eski içeriğin alfa değeri (1 -> 0, 0.1 sn).</summary>
        public static float OldAlpha(float time) => 1f - Clamp01(time / OldFadeDuration);

        /// <summary>Kırmızı çubuğun [min,max] yatay aralığı (0..1 alan oranı); ilerleme bitince (0,0).</summary>
        public static void WipeBand(float time, bool reverse, out float min, out float max)
        {
            var p = time / WipeDuration;
            if (p <= 0f || p >= 1f) { min = max = 0f; return; }
            var e = EaseInOut(p);
            const float width = 0.14f;
            var head = e * (1f + width);
            var lo = Clamp01(head - width);
            var hi = Clamp01(head);
            if (reverse) { min = 1f - hi; max = 1f - lo; }
            else { min = lo; max = hi; }
        }

        public static float TotalDuration(int itemCount)
        {
            var n = ItemCount(itemCount);
            var items = n == 0 ? 0f : ItemDelay(n - 1) + ItemDuration;
            return items > WipeDuration ? items : WipeDuration;
        }

        /// <summary>Geçişin atlanması gerekir mi (zaman durmuş ya da batch).</summary>
        public static bool ShouldSkip(float timeScale, bool batchMode) => batchMode || timeScale <= 0f;
    }

    /// <summary>
    /// Sayfa geçişi: kırmızı süpürme çubuğu + yeni içerikte kademeli (30 ms) kayarak belirme. Yalnızca görsel katman.
    /// </summary>
    public sealed class UiPageTransitions : MonoBehaviour
    {
        private RectTransform _area;
        private Image _bar;
        private RectTransform _barRect;
        private float _time = -1f;
        private bool _reverse;
        private readonly List<RectTransform> _items = new List<RectTransform>(UiPageTransitionMath.MaxItems);
        private readonly List<CanvasGroup> _groups = new List<CanvasGroup>(UiPageTransitionMath.MaxItems);
        private readonly List<Vector2> _bases = new List<Vector2>(UiPageTransitionMath.MaxItems);

        /// <summary>Alan içindeki sayfa için geçişi başlatır. reverse: geri (ESC) yönü.</summary>
        public static void Play(RectTransform area, RectTransform newPage, bool reverse)
        {
            if (area == null || newPage == null) return;
            if (UiPageTransitionMath.ShouldSkip(Time.timeScale, UnityEngine.Application.isBatchMode)) return;
            var t = area.GetComponent<UiPageTransitions>();
            if (t == null) t = area.gameObject.AddComponent<UiPageTransitions>();
            t.Begin(area, newPage, reverse);
        }

        private void Begin(RectTransform area, RectTransform page, bool reverse)
        {
            Finish();
            _area = area;
            _reverse = reverse;
            _items.Clear(); _groups.Clear(); _bases.Clear();
            var n = UiPageTransitionMath.ItemCount(page.childCount);
            for (var i = 0; i < n; i++)
            {
                var rt = page.GetChild(i) as RectTransform;
                if (rt == null) continue;
                _items.Add(rt);
                _groups.Add(UiFactory.EnsureCanvasGroup(rt));
                _bases.Add(rt.anchoredPosition);
            }

            if (_bar == null)
            {
                var r = UiFactory.CreateRect("PageWipe", area);
                _barRect = r;
                _bar = r.gameObject.AddComponent<Image>();
                _bar.color = UiKitTokens.Accent;
                _bar.raycastTarget = false;
                var le = r.gameObject.AddComponent<LayoutElement>();
                le.ignoreLayout = true;
            }
            _barRect.anchorMin = Vector2.zero;
            _barRect.anchorMax = Vector2.one;
            _barRect.offsetMin = _barRect.offsetMax = Vector2.zero;
            _barRect.SetAsLastSibling();
            _bar.enabled = true;
            _time = 0f;
            UiSounds.Play(UiSfx.Tab);
            Apply();
        }

        private void Update()
        {
            if (_time < 0f) return;
            _time += Time.unscaledDeltaTime;
            if (_time >= UiPageTransitionMath.TotalDuration(_items.Count)) { Finish(); return; }
            Apply();
        }

        private void Apply()
        {
            UiPageTransitionMath.WipeBand(_time, _reverse, out var lo, out var hi);
            if (_barRect != null)
            {
                _barRect.anchorMin = new Vector2(lo, 0f);
                _barRect.anchorMax = new Vector2(hi, 1f);
                _bar.enabled = hi > lo;
            }
            for (var i = 0; i < _items.Count; i++)
            {
                if (_items[i] == null) continue;
                var p = UiPageTransitionMath.ItemProgress(_time, i);
                _groups[i].alpha = p;
                _items[i].anchoredPosition = _bases[i] + new Vector2(UiPageTransitionMath.ItemOffsetX(_time, i, _reverse), 0f);
            }
        }

        private void Finish()
        {
            for (var i = 0; i < _items.Count; i++)
            {
                if (_items[i] == null) continue;
                _groups[i].alpha = 1f;
                _items[i].anchoredPosition = _bases[i];
            }
            _items.Clear(); _groups.Clear(); _bases.Clear();
            if (_bar != null) _bar.enabled = false;
            _time = -1f;
        }
    }
}
