using System;
using Project.Application.Services;
using Project.Infrastructure.Characters;
using Project.Infrastructure.Persistence;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Project.Infrastructure.Localization;

namespace Project.Presentation.UI
{
    /// <summary>
    /// "SEZON" penceresi: yatay kademe şeridi (1–50), her kademede ücretsiz ödül + premium (yakında) ödül, mevcut kademe,
    /// tecrübe çubuğu ve talep düğmeleri. Yalnızca kozmetik. Ana menüye <see cref="MainMenuController.ExtraButtons"/> ile eklenir.
    /// </summary>
    public sealed class SeasonPassPanel : MonoBehaviour
    {
        private static SeasonPassService _service;
        private RectTransform _content;
        private Text _info;
        private Image _barFill;
        private ScrollRect _scroll;
        private int _popTier;
        private readonly System.Collections.Generic.HashSet<int> _justClaimed = new System.Collections.Generic.HashSet<int>();
        private bool _scrollToCurrent;

        [Serializable]
        private sealed class Root
        {
            public int season = 1;
            public string name;
            public int maxTier = 50;
            public int xpPerTier = 600;
            public System.Collections.Generic.List<SeasonRewardEntry> free;
            public System.Collections.Generic.List<SeasonRewardEntry> premium;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _service = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            const string label = "SEZON";
            var list = MainMenuController.ExtraButtons;
            for (var i = 0; i < list.Count; i++)
                if (list[i].label == label)
                    return;
            list.Add((label, root => Show(root)));
        }

        /// <summary>Sezon servisi (tembel kurulum; kozmetik servisiyle aynı depo).</summary>
        public static SeasonPassService Service
        {
            get
            {
                if (_service != null) return _service;
                try
                {
                    var asset = Resources.Load<TextAsset>("Progression/season1_pass");
                    var r = asset != null ? JsonUtility.FromJson<Root>(asset.text) : null;
                    var def = new SeasonPassDefinition();
                    if (r != null)
                    {
                        def.season = r.season; def.name = r.name; def.maxTier = r.maxTier; def.xpPerTier = r.xpPerTier;
                        if (r.free != null) def.free = r.free;
                        if (r.premium != null) def.premium = r.premium;
                    }
                    _service = new SeasonPassService(new PlayerPrefsSettingsStore(), CosmeticsRuntime.Service, def);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Sezon] Servis kurulamadı: " + e.Message);
                }
                return _service;
            }
        }

        /// <summary>Maç sonucundan sezon tecrübesi ekler (null-güvenli).</summary>
        public static int RecordMatch(Project.Core.Domain.MatchResult result)
        {
            try { return Service?.AddMatch(result) ?? 0; }
            catch (Exception e) { Debug.LogException(e); return 0; }
        }

        public static SeasonPassPanel Show(Transform parent)
        {
            var root = UiFactory.CreateRect("[Sezon]", parent);
            root.SetAsLastSibling();
            UiFactory.Stretch(root);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = UiKitTokens.Scrim;
            dim.raycastTarget = true;

            OverlayState.CosmeticsOpen = true;
            var panel = root.gameObject.AddComponent<SeasonPassPanel>();
            panel.Build(root);
            return panel;
        }

        private void Build(RectTransform root)
        {
            var parts = UiKitPanel.Window(root, new Vector2(1280f, 700f), Loc.Get("season.title", "SEZON KARTI"));
            var body = parts.Body;
            _info = parts.Subtitle;

            // İlerleme çubuğu (kademe içi değil, tüm sezon).
            var barBg = UiKitPanel.Card(body, UiKitTokens.SurfaceRaised, 6);
            UiFactory.SetRect(barBg, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -22f), new Vector2(0f, -6f));
            _barFill = UiFactory.Image(barBg, UiSprites.GetRoundedRect(6), UiKitTokens.Sand);
            _barFill.raycastTarget = false;
            _barFill.rectTransform.anchorMin = Vector2.zero;
            _barFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            _barFill.rectTransform.offsetMin = Vector2.zero;
            _barFill.rectTransform.offsetMax = Vector2.zero;

            var scrollRoot = UiFactory.CreateRect("TierScroll", body);
            UiFactory.SetRect(scrollRoot, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 76f), new Vector2(0f, -34f));
            var hit = scrollRoot.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            scrollRoot.gameObject.AddComponent<RectMask2D>();
            _content = UiFactory.HorizontalList(scrollRoot, 12f, 6, TextAnchor.UpperLeft);
            _content.anchorMin = new Vector2(0f, 0f);
            _content.anchorMax = new Vector2(0f, 1f);
            _content.pivot = new Vector2(0f, 1f);
            _content.offsetMin = Vector2.zero;
            _content.offsetMax = Vector2.zero;
            var fitter = _content.gameObject.GetComponent<ContentSizeFitter>() ?? _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            _scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            _scroll.content = _content;
            _scroll.viewport = scrollRoot;
            _scroll.horizontal = true;
            _scroll.vertical = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 40f;
            UiKitScrollFade.AttachHorizontal(_scroll, UiKitTokens.Bg);

            var footer = UiFactory.HorizontalList(body, 14f, 0, TextAnchor.MiddleRight);
            UiFactory.SetRect(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 56f));
            var claimAll = UiKitButton.Create(footer, Loc.Get("season.claim_all", "HEPSİNİ AL"), () =>
            {
                _justClaimed.Clear();
                var svc = Service;
                if (svc != null)
                    for (var t = 1; t <= svc.MaxTier; t++)
                        if (svc.CanClaim(t)) _justClaimed.Add(t);
                try { svc?.ClaimAll(); } catch (Exception e) { Debug.LogException(e); }
                Refresh();
            }, UiKitButtonKind.Primary, 220f, 52f);
            UiFactory.FlexibleSpacer(footer);
            UiKitButton.Create(footer, Loc.Get("common.back", "GERİ"), Close, UiKitButtonKind.Default, 180f, 52f);

            Refresh();
            _scrollToCurrent = true;
        }

        public void Close()
        {
            OverlayState.CosmeticsOpen = false;
            if (this != null)
                Destroy(gameObject);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame))
            {
                OverlayState.ConsumeEscape();
                Close();
            }
        }

        private void OnDestroy() => OverlayState.CosmeticsOpen = false;

        private string NameOf(string id)
        {
            var cos = CosmeticsRuntime.Service;
            return cos != null && cos.TryGet(id, out var d) ? d.name : id;
        }

        private void Refresh()
        {
            if (_content == null) return;
            for (var i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);

            var s = Service;
            if (s == null)
            {
                _info.text = "Sezon verisi yüklenemedi.";
                return;
            }

            _info.text = (string.IsNullOrEmpty(s.Definition.name) ? "Sezon" : s.Definition.name) + "  |  Kademe " + s.CurrentTier + "/" + s.MaxTier
                + "  |  " + s.Xp + "/" + s.MaxXp + " SP  |  Yalnızca kozmetik";
            _barFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(s.Xp / (float)s.MaxXp), 1f);

            for (var t = 1; t <= s.MaxTier; t++)
                AddTier(_content, s, t);
            _popTier = 0;
            _justClaimed.Clear();
        }

        private void LateUpdate()
        {
            if (!_scrollToCurrent || _scroll == null || _content == null) return;
            _scrollToCurrent = false;
            var s = Service;
            if (s == null || s.MaxTier <= 1) return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            var range = _content.rect.width - _scroll.viewport.rect.width;
            if (range <= 1f) return;
            var target = Mathf.Max(0, Mathf.Max(1, s.CurrentTier) - 2) / (float)(s.MaxTier - 1);
            _scroll.horizontalNormalizedPosition = Mathf.Clamp01(target * _content.rect.width / range);
        }

        private RectTransform RewardCard(Transform parent, string caption, string cosmeticId, Color tint, bool dim, out UiRarity rarity)
        {
            rarity = UiRarity.Common;
            var card = UiKitPanel.Card(parent, UiKitTokens.SurfaceRaised, 8);
            UiFactory.LayoutSize(card, 176f, 128f);
            var cos = CosmeticsRuntime.Service;
            CosmeticDefinition def = null;
            if (!string.IsNullOrEmpty(cosmeticId) && cos != null) cos.TryGet(cosmeticId, out def);
            if (def != null) rarity = UiKitTokens.RarityOf(def.unlockMethod, def.unlockXp);
            var rc = UiKitTokens.RarityColor(rarity);

            var cap = UiFactory.Label(card, caption, UiKitTokens.FontCaption, TextAnchor.MiddleLeft, UiKitTokens.TextMuted, FontStyle.Bold);
            cap.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(cap, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -26f), new Vector2(-12f, -6f));

            if (def != null)
            {
                var sw = UiFactory.Image(card, UiSprites.GetRoundedRect(6), dim ? UiTheme.Darken(CosmeticsRuntime.PreviewColor(def), 0.5f) : CosmeticsRuntime.PreviewColor(def));
                sw.raycastTarget = false;
                UiFactory.SetRect(sw, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(12f, 12f), new Vector2(60f, -32f));
                var bar = UiFactory.Image(card, null, rc);
                bar.raycastTarget = false;
                UiFactory.SetRect(bar, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 3f));
            }
            var nm = UiFactory.Label(card, def != null ? def.name : "—", UiKitTokens.FontCaption + 1, TextAnchor.MiddleLeft, dim ? UiKitTokens.TextDim : UiKitTokens.Text, FontStyle.Bold);
            nm.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.SetRect(nm, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(68f, 12f), new Vector2(-8f, -32f));
            if (def != null && !dim) UiKitGlow.Attach(card, rc, rarity == UiRarity.Legendary);
            return card;
        }

        private void AddTier(Transform row, SeasonPassService s, int tier)
        {
            var reached = tier <= s.CurrentTier;
            var isCurrent = tier == s.CurrentTier || (s.CurrentTier == 0 && tier == 1);
            var col = UiKitPanel.Card(row, isCurrent ? UiKitTokens.Surface : UiTheme.WithAlpha(UiKitTokens.Surface, 0.7f), 12);
            UiFactory.LayoutSize(col, 200f, 520f);
            if (isCurrent)
            {
                var ring = UiFactory.Image(col, UiSprites.GetRoundedRectOutline(12), UiKitTokens.Sand);
                ring.raycastTarget = false;
                UiFactory.Stretch(ring);
            }
            var cl = col.gameObject.AddComponent<VerticalLayoutGroup>();
            cl.padding = new RectOffset(12, 12, 12, 12);
            cl.spacing = 8f;
            cl.childAlignment = TextAnchor.UpperCenter;
            cl.childControlWidth = true; cl.childControlHeight = false;
            cl.childForceExpandWidth = true; cl.childForceExpandHeight = false;

            var head = UiFactory.Label(col, (isCurrent ? "► " : "") + "KADEME " + tier, UiKitTokens.FontLabel, TextAnchor.MiddleCenter,
                reached ? UiKitTokens.Sand : UiKitTokens.TextMuted, FontStyle.Bold);
            head.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.LayoutSize(head, 170f, 34f);

            var free = s.FreeAt(tier);
            var fCard = RewardCard(col, Loc.Get("season.free", "ÜCRETSİZ"), free?.cosmeticId, UiKitTokens.Sand, free == null || !reached, out _);
            if (free != null)
            {
                var claimed = s.IsClaimed(tier);
                var captured = tier;
                var b = UiKitButton.Create(col, claimed ? Loc.Get("season.claimed", "ALINDI") : Loc.Get("season.claim", "AL"), () =>
                {
                    try { s.Claim(captured); } catch (Exception e) { Debug.LogException(e); }
                    _popTier = captured;
                    Refresh();
                }, claimed ? UiKitButtonKind.Ghost : UiKitButtonKind.Primary, 170f, 44f);
                b.interactable = s.CanClaim(tier);
                if (_popTier == tier || _justClaimed.Contains(tier))
                {
                    UiKitPop.Play(fCard, 0.5f);
                    UiKitPop.Play(b.GetComponent<RectTransform>(), 0.5f);
                }
            }
            else
            {
                UiFactory.Spacer(col, 44f);
            }

            var prem = s.PremiumAt(tier);
            RewardCard(col, Loc.Get("season.premium", "PREMİUM"), prem?.cosmeticId, UiKitTokens.Khaki, true, out _);
            if (prem != null)
            {
                var pb = UiKitButton.Create(col, Loc.Get("season.soon", "yakında"), () => { }, UiKitButtonKind.Ghost, 170f, 44f);
                pb.interactable = false;
            }
        }
    }
}
