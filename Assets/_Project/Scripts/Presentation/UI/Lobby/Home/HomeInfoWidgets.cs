using System;
using System.Collections.Generic;
using Project.Application.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Presentation.UI.Lobby.Home
{
    /// <summary>
    /// Lobi ana sayfa bilgi katmani: haber karuseli, etkinlik geri sayimi, sezon mini widget'i.
    /// Hepsi sag sutunda (HomeRightColumn) durur; ekran ortasi karaktere birakilir.
    /// </summary>
    public static class HomeInfoWidgets
    {
        /// <summary>Test/sunucu icin saat kaynagi (varsayilan: UTC simdi).</summary>
        public static Func<DateTime> CalendarNow = () => DateTime.UtcNow;

        public const float CardWidth = 620f;
        public const float CarouselHeight = 132f;
        public const float MiniHeight = 84f;

        /// <summary>Sutuna karusel + (etkinlik | sezon) satirini yerlestirir. top: sutun ustunden negatif ofset.</summary>
        public static void Build(RectTransform column, float top)
        {
            try
            {
                BuildCarousel(column, top);
                var gap = 12f;
                var w = (CardWidth - gap) * 0.5f;
                var y = top - CarouselHeight - 14f;
                BuildEventCountdown(column, new Vector2(0f, y), new Vector2(w, MiniHeight));
                BuildSeasonMini(column, new Vector2(w + gap, y), new Vector2(w, MiniHeight));
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        // ------------------------------------------------------------------ Karusel

        public static RectTransform BuildCarousel(RectTransform column, float top)
        {
            var items = HomeNewsCatalog.Items();
            var card = UiFactory.Panel(column, UiKitTokens.Bg, UiSprites.ChamferRect);
            card.gameObject.name = "NewsCarousel";
            UiFactory.Anchor(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, top), new Vector2(CardWidth, CarouselHeight));
            var view = card.gameObject.AddComponent<HomeCarouselView>();
            view.Build(items);
            return card;
        }

        // ------------------------------------------------------------------ Etkinlik

        public static RectTransform BuildEventCountdown(RectTransform column, Vector2 pos, Vector2 size)
        {
            var card = UiFactory.Panel(column, UiKitTokens.Bg, UiSprites.ChamferRect);
            card.gameObject.name = "EventCountdown";
            UiFactory.Anchor(card, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);
            var accent = UiFactory.Image(card, null, UiKitTokens.Accent);
            accent.raycastTarget = false;
            UiFactory.SetRect(accent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 8f), new Vector2(5f, -8f));

            var cap = UiFactory.Label(card, "ETKİNLİK", UiTheme.FontTiny, TextAnchor.MiddleLeft, UiKitTokens.Accent, FontStyle.Bold);
            UiFactory.SetRect(cap, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -26f), new Vector2(-12f, -6f));
            var title = UiFactory.Label(card, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleLeft, UiKitTokens.Text, FontStyle.Bold);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(title, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(20f, 0f), new Vector2(-12f, -26f));
            var time = UiFactory.Label(card, string.Empty, UiTheme.FontNormal + 2, TextAnchor.MiddleLeft, UiKitTokens.Text, FontStyle.Bold);
            time.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(time, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(20f, 6f), new Vector2(-12f, 0f));
            var drv = card.gameObject.AddComponent<HomeEventCountdownView>();
            drv.Bind(title, time, accent);
            return card;
        }

        // ------------------------------------------------------------------ Sezon

        public static RectTransform BuildSeasonMini(RectTransform column, Vector2 pos, Vector2 size)
        {
            var seasonNo = 1;
            var tier = 0;
            var maxTier = 50;
            var tierProgress = 0f;
            var now = CalendarNow();
            var window = HomeCalendar.SeasonAt(now);
            try
            {
                var s = SeasonPassPanel.Service;
                if (s != null)
                {
                    seasonNo = s.Definition != null ? s.Definition.season : 1;
                    tier = s.CurrentTier; maxTier = s.MaxTier; tierProgress = s.TierProgress;
                }
            }
            catch (Exception e) { Debug.LogException(e); }

            var card = UiFactory.Panel(column, UiKitTokens.Bg, UiSprites.ChamferRect);
            card.gameObject.name = "SeasonMini";
            UiFactory.Anchor(card, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);
            var cap = UiFactory.Label(card, "SEZON " + seasonNo, UiTheme.FontTiny, TextAnchor.MiddleLeft, UiKitTokens.Accent, FontStyle.Bold);
            UiFactory.SetRect(cap, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -26f), new Vector2(-12f, -6f));
            var tierLabel = UiFactory.Label(card, "KADEME " + tier + " / " + maxTier, UiTheme.FontSmall, TextAnchor.MiddleLeft, UiKitTokens.Text, FontStyle.Bold);
            tierLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(tierLabel, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(16f, 0f), new Vector2(-12f, -26f));
            var left = UiFactory.Label(card, "Bitişe " + HomeCalendar.FormatRemaining(window.Remaining(now)), UiTheme.FontTiny, TextAnchor.MiddleLeft, UiKitTokens.TextDim);
            left.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(left, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(16f, 14f), new Vector2(-12f, 0f));

            // Kademe ici ilerleme (ince cubuk) + sezon zaman cizgisi (daha ince, soluk)
            var bg = UiFactory.Image(card, null, new Color(1f, 1f, 1f, 0.08f));
            bg.raycastTarget = false;
            UiFactory.SetRect(bg, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 8f), new Vector2(-12f, 13f));
            var fill = UiFactory.Image(bg.rectTransform, null, UiKitTokens.Accent);
            fill.raycastTarget = false;
            UiFactory.SetRect(fill, Vector2.zero, new Vector2(Mathf.Max(0.001f, Mathf.Clamp01(tierProgress)), 1f), Vector2.zero, Vector2.zero);
            var time = UiFactory.Image(card, null, new Color(1f, 1f, 1f, 0.35f));
            time.raycastTarget = false;
            UiFactory.SetRect(time, new Vector2(0f, 0f), new Vector2(Mathf.Max(0.001f, window.Elapsed01(now)), 0f), new Vector2(16f, 3f), new Vector2(-12f * 0f, 5f));
            return card;
        }
    }

    /// <summary>Karusel gorunumu: slaytlar CanvasGroup ile capraz solar, noktalar tiklanir, fare ustunde durur.</summary>
    public sealed class HomeCarouselView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private HomeCarouselModel _model;
        private CanvasGroup[] _slides;
        private Image[] _dots;
        private Image _progress;
        private Image _stripe;
        private IReadOnlyList<HomeNewsItem> _items;

        public void Build(IReadOnlyList<HomeNewsItem> items)
        {
            _items = items;
            var n = items != null ? items.Count : 0;
            _model = new HomeCarouselModel(n);
            _slides = new CanvasGroup[n];
            _dots = new Image[n];
            var rt = (RectTransform)transform;

            _stripe = UiFactory.Image(rt, null, n > 0 ? items[0].Accent : UiKitTokens.Accent);
            _stripe.raycastTarget = false;
            UiFactory.SetRect(_stripe, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 8f), new Vector2(5f, -8f));

            for (var i = 0; i < n; i++)
            {
                var it = items[i];
                var slide = UiFactory.CreateRect("Slide" + i, rt);
                UiFactory.Stretch(slide);
                var cg = slide.gameObject.AddComponent<CanvasGroup>();
                cg.blocksRaycasts = false;
                _slides[i] = cg;
                var tag = UiFactory.Label(slide, it.Tag, UiTheme.FontTiny, TextAnchor.MiddleLeft, it.Accent, FontStyle.Bold);
                UiFactory.SetRect(tag, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -32f), new Vector2(-20f, -10f));
                var title = UiFactory.Label(slide, it.Title, UiTheme.FontNormal + 4, TextAnchor.MiddleLeft, UiKitTokens.Text, FontStyle.Bold);
                title.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiFactory.SetRect(title, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -66f), new Vector2(-20f, -34f));
                var body = UiFactory.Label(slide, it.Body, UiTheme.FontSmall, TextAnchor.UpperLeft, UiKitTokens.TextDim);
                UiFactory.SetRect(body, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 22f), new Vector2(-24f, -70f));
            }

            for (var i = 0; i < n; i++)
            {
                var idx = i;
                var dot = UiFactory.Image(rt, null, new Color(1f, 1f, 1f, 0.25f));
                UiFactory.Anchor(dot, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f - (n - 1 - i) * 18f, -14f), new Vector2(10f, 10f));
                var btn = dot.gameObject.AddComponent<Button>();
                btn.targetGraphic = dot;
                btn.onClick.AddListener(() => _model.Select(idx));
                _dots[i] = dot;
            }

            _progress = UiFactory.Image(rt, null, UiTheme.WithAlpha(UiKitTokens.Accent, 0.8f));
            _progress.raycastTarget = false;
            UiFactory.SetRect(_progress, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 3f));
            Apply();
        }

        public void OnPointerEnter(PointerEventData e) { if (_model != null) _model.Paused = true; }
        public void OnPointerExit(PointerEventData e) { if (_model != null) _model.Paused = false; }
        private void OnDisable() { if (_model != null) _model.Paused = false; }

        private void Update()
        {
            if (_model == null || _model.Count == 0) return;
            _model.Tick(Time.unscaledDeltaTime);
            Apply();
        }

        private void Apply()
        {
            for (var i = 0; i < _slides.Length; i++)
                _slides[i].alpha = _model.AlphaOf(i);
            for (var i = 0; i < _dots.Length; i++)
                _dots[i].color = i == _model.Index ? UiKitTokens.Text : new Color(1f, 1f, 1f, 0.25f);
            if (_items != null && _model.Count > 0)
                _stripe.color = _items[_model.Index].Accent;
            var rect = _progress.rectTransform;
            rect.anchorMax = new Vector2(_model.Count > 1 ? _model.Progress : 0f, 0f);
        }
    }

    /// <summary>Etkinlik geri sayimi: saniyede bir metni gunceller; son saatte kirmizi.</summary>
    public sealed class HomeEventCountdownView : MonoBehaviour
    {
        private Text _title;
        private Text _time;
        private Image _accent;
        private float _next;

        public void Bind(Text title, Text time, Image accent)
        {
            _title = title; _time = time; _accent = accent;
            Refresh();
        }

        private void OnEnable() { if (_title != null) Refresh(); }

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            Refresh();
        }

        private void Refresh()
        {
            _next = Time.unscaledTime + 1f;
            var now = HomeInfoWidgets.CalendarNow();
            var ev = HomeCalendar.WeekendEventAt(now);
            var rem = ev.Remaining(now);
            var prefix = ev.Active ? "BİTİŞE " : "BAŞLAMAYA ";
            var title = ev.Title;
            var time = prefix + HomeCalendar.FormatRemaining(rem);
            if (_title.text != title) _title.text = title;
            if (_time.text != time) _time.text = time;
            var urgent = HomeCalendar.IsUrgent(rem);
            _time.color = urgent ? UiKitTokens.Accent : UiKitTokens.Text;
            _accent.color = ev.Active ? new Color(0.95f, 0.72f, 0.20f, 1f) : UiKitTokens.Accent;
        }
    }
}
