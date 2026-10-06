using System;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Presentation.Bootstrap;
using Project.Infrastructure.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>Saf yardımcılar (test edilebilir): OYNA nabzı ve görev seçimi.</summary>
    public static class MenuHomeMath
    {
        public const float PulsePeriod = 2.5f;

        /// <summary>0..1 nabız (2,5 sn periyot, kosinüs yumuşak).</summary>
        public static float Pulse(float time) => 0.5f - 0.5f * Mathf.Cos(time / PulsePeriod * Mathf.PI * 2f);

        /// <summary>Hedef ölçek: basılı 0.97, üstünde 1.03, değilse 1.</summary>
        public static float TargetScale(bool hover, bool down) => down ? 0.97f : (hover ? 1.03f : 1f);

        /// <summary>Başarım/görev ilerleme oranı (0..1).</summary>
        public static float Ratio(int progress, int target) => target <= 0 ? 0f : Mathf.Clamp01(progress / (float)target);
    }

    /// <summary>OYNA düğmesi: boşta kırmızı parıltı nabzı, üstünde 1,03 ölçek + parlama, basınca çökme.</summary>
    public sealed class MenuPlayJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform _rect;
        private Image _glow;
        private bool _hover;
        private bool _down;
        private float _scale = 1f;
        private float _hoverBlend;

        public static MenuPlayJuice Attach(RectTransform cta, Image glow)
        {
            var j = cta.gameObject.AddComponent<MenuPlayJuice>();
            j._rect = cta;
            j._glow = glow;
            return j;
        }

        public void OnPointerEnter(PointerEventData e) => _hover = true;
        public void OnPointerExit(PointerEventData e) { _hover = false; _down = false; }
        public void OnPointerDown(PointerEventData e) => _down = true;
        public void OnPointerUp(PointerEventData e) => _down = false;

        private void Update()
        {
            if (_rect == null) return;
            var dt = Time.unscaledDeltaTime;
            var focus = UnityEngine.EventSystems.EventSystem.current != null &&
                        UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == gameObject;
            var target = MenuHomeMath.TargetScale(_hover || focus, _down);
            _scale = Mathf.Lerp(_scale, target, 1f - Mathf.Exp(-16f * dt));
            _rect.localScale = new Vector3(_scale, _scale, 1f);
            _hoverBlend = Mathf.Lerp(_hoverBlend, (_hover || focus) ? 1f : 0f, 1f - Mathf.Exp(-10f * dt));

            if (_glow != null)
            {
                var pulse = MenuHomeMath.Pulse(Time.unscaledTime);
                var a = Mathf.Lerp(0.28f + 0.30f * pulse, 0.85f, _hoverBlend);
                if (_down) a *= 0.7f;
                _glow.color = UiTheme.WithAlpha(UiKitTokens.Accent, a);
                var s = 1f + 0.03f * pulse + 0.05f * _hoverBlend;
                _glow.rectTransform.localScale = new Vector3(s, s, 1f);
            }
        }

        private void OnDisable()
        {
            if (_rect != null) _rect.localScale = Vector3.one;
        }
    }

    /// <summary>OYNA sayfası görsel katmanı: çapraz bant, istatistik şeridi, günün görevi, alt çubuk.</summary>
    public static class MenuHomeVisuals
    {
        private static Texture2D _motif;
        private static Sprite _ring;

        /// <summary>Başlığın arkasına 12 derece eğik koyu kırmızı/siyah bant (prosedürel doku, ince gürültü).</summary>
        public static void BuildMotif(RectTransform page)
        {
            if (_motif == null)
            {
                const int w = 512, h = 96;
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "HomeMotif" };
                var px = new Color32[w * h];
                var rng = new System.Random(1204);
                for (var y = 0; y < h; y++)
                {
                    var v = (y + 0.5f) / h;
                    var edge = Mathf.Clamp01(Mathf.Min(v, 1f - v) * 6f);
                    var core = Mathf.Clamp01(1f - Mathf.Abs(v - 0.5f) * 2f);
                    for (var x = 0; x < w; x++)
                    {
                        var u = (x + 0.5f) / w;
                        var fade = Mathf.Clamp01(u * 5f) * Mathf.Clamp01((1f - u) * 2.2f);
                        var noise = (float)rng.NextDouble() * 0.12f - 0.06f;
                        // sol kırmızıdan sağ siyaha
                        var r = Mathf.Clamp01((0.50f - 0.35f * u) * (0.55f + 0.45f * core) + noise * 0.6f);
                        var g = Mathf.Clamp01(0.045f + noise * 0.15f);
                        var a = Mathf.Clamp01(edge * fade * (0.55f + 0.30f * core) + noise * 0.3f * edge);
                        // ince parlak kırmızı çizgi bandın alt kenarında
                        if (v < 0.05f || (v > 0.945f && v < 0.965f))
                        {
                            r = 0.83f; g = 0.23f; a = Mathf.Max(a, 0.75f * fade);
                        }
                        px[y * w + x] = new Color32((byte)(r * 255f), (byte)(g * 255f), (byte)(g * 255f), (byte)(a * 255f));
                    }
                }

                t.SetPixels32(px);
                t.Apply(false, false);
                _motif = t;
            }

            var raw = UiFactory.CreateRect("DiagonalMotif", page).gameObject.AddComponent<RawImage>();
            raw.texture = _motif;
            raw.raycastTarget = false;
            var rt = raw.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(-60f, -70f);
            rt.sizeDelta = new Vector2(900f, 150f);
            rt.localRotation = Quaternion.Euler(0f, 0f, -12f);
            rt.SetAsFirstSibling();
        }

        /// <summary>SON HAREKÂT: sıralama / leş / hasar. Veri yoksa tire.</summary>
        public static void BuildStatStrip(RectTransform page, float top)
        {
            string rank = "—", kills = "—", damage = "—";
            var win = false;
            try
            {
                if (GameSession.LastResult.HasValue)
                {
                    var r = GameSession.LastResult.Value;
                    var place = r.TeamPlacement > 0 ? r.TeamPlacement : r.Placement;
                    var total = r.TeamCount > 0 ? r.TeamCount : r.TotalPlayers;
                    win = r.IsWinner;
                    rank = "#" + (r.IsWinner ? 1 : place) + (total > 0 ? "/" + total : string.Empty);
                    kills = r.Kills.ToString();
                    damage = Mathf.RoundToInt(r.DamageDealt).ToString();
                }
                else
                {
                    var h = GameSession.Progress != null ? GameSession.Progress.History : null;
                    if (h != null && h.Count > 0)
                    {
                        var e = h[h.Count - 1];
                        win = e.Won;
                        rank = "#" + (e.Won ? 1 : e.Placement) + (e.TotalPlayers > 0 ? "/" + e.TotalPlayers : string.Empty);
                        kills = e.Kills.ToString();
                        damage = e.Damage.ToString();
                    }
                }
            }
            catch (Exception ex) { Debug.LogException(ex); }

            var card = UiFactory.Panel(page, UiKitTokens.Bg, UiSprites.ChamferRect);
            card.gameObject.name = "LastOperation";
            UiFactory.Anchor(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, top), new Vector2(620f, 92f));
            var accent = UiFactory.Image(card, null, win ? MenuRankInsignia.Gold : UiKitTokens.Accent);
            UiFactory.SetRect(accent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 8f), new Vector2(5f, -8f));

            var caption = UiFactory.Label(card, Loc.Get("menu.last_op", "SON HAREKÂT"), UiTheme.FontTiny, TextAnchor.MiddleLeft, UiTheme.TextMuted, FontStyle.Bold);
            UiFactory.SetRect(caption, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -30f), new Vector2(-20f, -8f));

            Cell(card, 0, "SIRALAMA", rank, win ? MenuRankInsignia.Gold : UiKitTokens.Text);
            Cell(card, 1, "LEŞ", kills, UiKitTokens.Text);
            Cell(card, 2, "HASAR", damage, UiKitTokens.Text);
        }

        private static void Cell(RectTransform card, int i, string caption, string value, Color color)
        {
            var w = 190f;
            var x = 24f + i * w;
            var v = UiFactory.Label(card, value, 30, TextAnchor.MiddleLeft, color, FontStyle.Bold);
            v.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(v, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, 6f), new Vector2(x + w - 10f, 46f));
            var c = UiFactory.Label(card, caption, UiTheme.FontTiny, TextAnchor.MiddleLeft, UiKitTokens.Accent, FontStyle.Bold);
            c.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(c, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, 44f), new Vector2(x + w - 10f, 62f));
        }

        /// <summary>GÜNÜN GÖREVİ: tamamlanmamış başarımlardan en ilerleyen biri + ilerleme çubuğu.</summary>
        public static void BuildDailyChallenge(RectTransform page, float top)
        {
            string title = "Henüz görev yok", desc = "İlk maçını oyna.";
            var progress = 0;
            var target = 0;
            try
            {
                var daily = DailyMissionsHost.Shared;
                var m = daily != null ? daily.ClosestToComplete() : null;
                if (m != null)
                {
                    title = DailyMissions.Describe(m); desc = "Ödül: +" + m.Tp + " TP, +" + m.Keys + " kozmetik anahtarı";
                    progress = m.Progress; target = m.Target;
                }
                else if (daily != null && daily.Today.Count > 0)
                {
                    title = "Bugünün görevleri tamam"; desc = "Yarın yeni görevler gelecek. Toplam " + daily.TotalTp + " TP, " + daily.TotalKeys + " anahtar.";
                }
                else
                {
                    var svc = GameSession.Achievements;
                    AchievementDefinition best = null;
                    var bestRatio = -1f;
                    var defs = svc != null ? svc.Definitions : null;
                    for (var i = 0; defs != null && i < defs.Count; i++)
                    {
                        var d = defs[i];
                        if (d == null || svc.IsUnlocked(d.id) || d.target <= 0) continue;
                        var r = MenuHomeMath.Ratio(svc.GetProgress(d), d.target);
                        if (r > bestRatio) { bestRatio = r; best = d; }
                    }
                    if (best != null) { title = best.title; desc = best.description; progress = svc.GetProgress(best); target = best.target; }
                }
            }
            catch (Exception ex) { Debug.LogException(ex); }

            var card = UiFactory.Panel(page, UiKitTokens.Bg, UiSprites.ChamferRect);
            card.gameObject.name = "DailyChallenge";
            UiFactory.Anchor(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, top), new Vector2(620f, 104f));
            var accent = UiFactory.Image(card, null, UiKitTokens.Accent);
            UiFactory.SetRect(accent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 8f), new Vector2(5f, -8f));

            var cap = UiFactory.Label(card, "GÜNÜN GÖREVİ", UiTheme.FontTiny, TextAnchor.MiddleLeft, UiKitTokens.Accent, FontStyle.Bold);
            UiFactory.SetRect(cap, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -28f), new Vector2(-20f, -8f));
            var t = UiFactory.Label(card, title, UiTheme.FontNormal, TextAnchor.MiddleLeft, UiKitTokens.Text, FontStyle.Bold);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(t, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -54f), new Vector2(-150f, -28f));
            var cnt = UiFactory.Label(card, target > 0 ? Mathf.Min(progress, target) + " / " + target : "— / —", UiTheme.FontNormal, TextAnchor.MiddleRight, UiKitTokens.Text, FontStyle.Bold);
            cnt.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(cnt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-150f, -54f), new Vector2(-16f, -28f));
            var d2 = UiFactory.Label(card, desc, UiTheme.FontSmall, TextAnchor.MiddleLeft, UiKitTokens.TextDim);
            d2.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(d2, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 22f), new Vector2(-16f, 44f));

            var bg = UiFactory.Image(card, null, new Color(1f, 1f, 1f, 0.08f));
            bg.raycastTarget = false;
            UiFactory.SetRect(bg, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 10f), new Vector2(-16f, 16f));
            var fill = UiFactory.Image(bg.rectTransform, null, UiKitTokens.Accent);
            fill.raycastTarget = false;
            var ratio = MenuHomeMath.Ratio(progress, target);
            UiFactory.SetRect(fill, Vector2.zero, new Vector2(Mathf.Max(0.001f, ratio), 1f), Vector2.zero, Vector2.zero);
        }

        /// <summary>Alt çubuk: sezon adı + seviye çipi (ince kırmızı ilerleme yayı).</summary>
        public static void BuildBottomBar(RectTransform page)
        {
            var season = "SEZON 1";
            var tier = 0;
            var level = 1;
            var prog = 0f;
            try
            {
                var s = SeasonPassPanel.Service;
                if (s != null)
                {
                    var n = s.Definition != null ? s.Definition.name : null;
                    season = string.IsNullOrEmpty(n) ? "SEZON " + (s.Definition != null ? s.Definition.season : 1) : n.ToUpperInvariant();
                    tier = s.CurrentTier;
                }

                var p = GameSession.Progress;
                if (p != null) { level = p.Level; prog = p.LevelProgress(); }
            }
            catch (Exception ex) { Debug.LogException(ex); }

            var bar = UiFactory.Panel(page, new Color(0.04f, 0.045f, 0.05f, 0.78f), UiSprites.ChamferRect);
            bar.gameObject.name = "SeasonBar";
            UiFactory.Anchor(bar, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 8f), new Vector2(620f, 64f));
            var line = UiFactory.Image(bar, null, UiKitTokens.Accent);
            line.raycastTarget = false;
            UiFactory.SetRect(line, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 2f));

            var cap = UiFactory.Label(bar, season, UiTheme.FontNormal, TextAnchor.MiddleLeft, UiKitTokens.Text, FontStyle.Bold);
            cap.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(cap, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(20f, 0f), new Vector2(-90f, -6f));
            var sub = UiFactory.Label(bar, "KADEME " + tier, UiTheme.FontTiny, TextAnchor.MiddleLeft, UiKitTokens.TextDim, FontStyle.Bold);
            UiFactory.SetRect(sub, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(20f, 6f), new Vector2(-90f, 0f));

            // Seviye çipi + yay
            var chip = UiFactory.CreateRect("LevelChip", bar);
            UiFactory.Anchor(chip, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(48f, 48f));
            var ringBg = UiFactory.Image(chip, Ring(), new Color(1f, 1f, 1f, 0.12f));
            ringBg.raycastTarget = false;
            UiFactory.Stretch(ringBg.rectTransform);
            var arc = UiFactory.Image(chip, Ring(), UiKitTokens.Accent);
            arc.raycastTarget = false;
            arc.type = Image.Type.Filled;
            arc.fillMethod = Image.FillMethod.Radial360;
            arc.fillOrigin = (int)Image.Origin360.Top;
            arc.fillClockwise = true;
            arc.fillAmount = Mathf.Clamp01(prog);
            UiFactory.Stretch(arc.rectTransform);
            var lv = UiFactory.Label(chip, level.ToString(), UiTheme.FontNormal, TextAnchor.MiddleCenter, UiKitTokens.Text, FontStyle.Bold);
            lv.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Stretch(lv.rectTransform);
        }

        /// <summary>OYNA düğmesi arkasına parıltı + juice + ENTER ipucu çipi ekler.</summary>
        public static void DecorateCta(RectTransform page, RectTransform cta)
        {
            var glow = UiFactory.Image(page, UiKitPanel.SoftShadow, UiTheme.WithAlpha(UiKitTokens.Accent, 0.3f));
            glow.raycastTarget = false;
            glow.type = Image.Type.Sliced;
            glow.gameObject.name = "PlayGlow";
            var g = glow.rectTransform;
            g.anchorMin = cta.anchorMin; g.anchorMax = cta.anchorMax; g.pivot = cta.pivot;
            g.anchoredPosition = cta.anchoredPosition;
            g.sizeDelta = cta.sizeDelta + new Vector2(60f, 50f);
            g.SetSiblingIndex(cta.GetSiblingIndex());
            MenuPlayJuice.Attach(cta, glow);

            var chip = UiFactory.Panel(page, new Color(1f, 1f, 1f, 0.08f), UiSprites.RoundedRect);
            chip.gameObject.name = "EnterHint";
            UiFactory.Anchor(chip, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(cta.anchoredPosition.x + cta.sizeDelta.x + 16f, cta.anchoredPosition.y - 20f), new Vector2(112f, 28f));
            var lab = UiFactory.Label(chip, "ENTER", UiTheme.FontTiny, TextAnchor.MiddleCenter, UiKitTokens.TextDim, FontStyle.Bold);
            lab.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Stretch(lab.rectTransform);
        }

        private static Sprite Ring()
        {
            if (_ring != null) return _ring;
            const int n = 96;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "HomeRing" };
            var px = new Color32[n * n];
            var c = (n - 1) * 0.5f;
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                var a = Mathf.Clamp01((1f - d) * c * 0.5f) * Mathf.Clamp01((d - 0.84f) * c * 0.5f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }

            t.SetPixels32(px);
            t.Apply(false, false);
            _ring = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _ring;
        }
    }
}
