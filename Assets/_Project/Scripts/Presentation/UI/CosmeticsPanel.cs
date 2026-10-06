using System;
using Project.Application.Services;
using Project.Infrastructure.Characters;
using Project.Infrastructure.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// "KOZMETİK" penceresi: yuva sekmeleri (kamuflaj, bere, kolluk, silah kaplaması), sahip olunan öğeyi kuşanma, kilitliler için
    /// gereken tecrübe. Yalnızca görsel; oynanışa etkisi yoktur. Ana menüye <see cref="MainMenuController.ExtraButtons"/> ile eklenir.
    /// </summary>
    public sealed class CosmeticsPanel : MonoBehaviour
    {
        private static readonly string[] Slots =
        {
            CosmeticsService.SlotCamo, CosmeticsService.SlotBeret, CosmeticsService.SlotArmband, CosmeticsService.SlotWeaponSkin
        };

        private static string[] SlotTitles => new[]
        {
            Loc.Get("cosmetics.slot.camo", "KAMUFLAJ"),
            Loc.Get("cosmetics.slot.beret", "BERE"),
            Loc.Get("cosmetics.slot.armband", "KOLLUK"),
            Loc.Get("cosmetics.slot.skin", "SİLAH KAPLAMASI")
        };

        private RectTransform _window;
        private RectTransform _content;
        private Text _info;
        private int _slot;
        private CosmeticsService _service;
        private CosmeticsPanelLogic.OwnFilter _own;
        private int _rarityFilter = -1;
        private string _selectedId;
        private RectTransform _detail;
        private Text _detailName, _detailRarity, _detailSource, _detailLock;
        private SoldierPreview _preview;
        private string _justEquipped;
        private float _pulseUntil;
        private RectTransform _pulseRect;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            const string label = "KOZMETİK"; // Loc: menu uses raw id; title via Loc in Show
            var list = MainMenuController.ExtraButtons;
            for (var i = 0; i < list.Count; i++)
                if (list[i].label == label)
                    return;
            list.Add((label, root => Show(root)));
        }

        public static CosmeticsPanel Show(Transform parent)
        {
            var root = UiFactory.CreateRect("[Kozmetik]", parent);
            root.SetAsLastSibling();
            UiFactory.Stretch(root);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = UiKitTokens.Scrim;
            dim.raycastTarget = true;

            OverlayState.CosmeticsOpen = true;
            var panel = root.gameObject.AddComponent<CosmeticsPanel>();
            panel._service = CosmeticsRuntime.Service;
            panel.Build(root);
            return panel;
        }

        private UiKitTabBar _tabBar;

        private void Build(RectTransform root)
        {
            var parts = UiKitPanel.Window(root, new Vector2(1000f, 760f), Loc.Get("cosmetics.title", "KOZMETİK"));
            _window = parts.Window;
            _info = parts.Subtitle;
            var body = parts.Body;

            var tabHost = UiFactory.CreateRect("Tabs", body);
            UiFactory.SetRect(tabHost, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -52f), Vector2.zero);
            _tabBar = UiKitTabBar.Create(tabHost, SlotTitles, false, i => { _slot = i; Refresh(); }, 220f, 48f);
            UiFactory.Stretch(_tabBar.GetComponent<RectTransform>());
            _tabBar.Select(0, false);

            var scroll = UiKitScrollFade.VerticalList(body, out _content, UiKitTokens.Bg, 0f, 4);
            UiFactory.SetRect(scroll, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 74f), new Vector2(-290f, -100f));
            // Izgara: içerik dikey listesini ızgaraya çevir.
            var vlg = _content.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) DestroyImmediate(vlg);
            var grid = _content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(200f, 188f);
            grid.spacing = new Vector2(14f, 14f);
            grid.padding = new RectOffset(4, 4, 4, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;

            BuildFilters(body);
            BuildDetail(body);

            var footer = UiFactory.HorizontalList(body, 14f, 0, TextAnchor.MiddleRight);
            UiFactory.SetRect(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 56f));
            UiFactory.FlexibleSpacer(footer);
            UiKitButton.Create(footer, Loc.Get("cosmetics.back", "GERİ"), Close, UiKitButtonKind.Default, 180f, 52f);

            Refresh();
        }

        private void BuildFilters(RectTransform body)
        {
            var row = UiFactory.HorizontalList(body, 8f, 0, TextAnchor.MiddleLeft);
            UiFactory.SetRect(row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -96f), new Vector2(-290f, -56f));
            foreach (var f in new[] { CosmeticsPanelLogic.OwnFilter.All, CosmeticsPanelLogic.OwnFilter.Owned, CosmeticsPanelLogic.OwnFilter.Locked })
            {
                var cf = f;
                UiKitButton.Create(row, CosmeticsPanelLogic.OwnFilterName(f), () => { _own = cf; Refresh(); }, UiKitButtonKind.Ghost, 96f, 34f);
            }
            UiFactory.FlexibleSpacer(row);
            for (var r = -1; r <= (int)UiRarity.Legendary; r++)
            {
                var cr = r;
                var label = r < 0 ? "HEPSİ" : UiKitTokens.RarityName((UiRarity)r);
                if (r == (int)UiRarity.Epic) label = "DESTANSI";
                UiKitButton.Create(row, label, () => { _rarityFilter = cr; Refresh(); }, UiKitButtonKind.Ghost, 78f, 34f);
            }
        }

        private void BuildDetail(RectTransform body)
        {
            _detail = UiFactory.CreateRect("Detay", body);
            UiFactory.SetRect(_detail, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-276f, 74f), new Vector2(0f, -62f));
            var bg = UiFactory.Image(_detail, UiSprites.GetRoundedRect(10), UiKitTokens.SurfaceRaised);
            bg.raycastTarget = false;
            UiFactory.Stretch(bg);

            var rawRt = UiFactory.CreateRect("Onizleme", _detail);
            UiFactory.SetRect(rawRt, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 150f), new Vector2(-8f, -8f));
            var raw = rawRt.gameObject.AddComponent<RawImage>();
            raw.color = new Color(1f, 1f, 1f, 0f);
            var fb = UiFactory.Label(_detail, "Önizleme yok", UiKitTokens.FontCaption, TextAnchor.MiddleCenter, UiKitTokens.TextMuted);
            UiFactory.SetRect(fb, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 150f), new Vector2(-8f, -8f));
            try { _preview = SoldierPreview.Create(raw, fb, 512); } catch (Exception e) { Debug.LogWarning("[Kozmetik] Önizleme: " + e.Message); }

            _detailRarity = DetailLabel(8f, 122f, 144f, UiKitTokens.FontCaption - 1, FontStyle.Bold);
            _detailName = DetailLabel(8f, 92f, 122f, UiKitTokens.FontBody - 2, FontStyle.Bold);
            _detailSource = DetailLabel(8f, 70f, 92f, UiKitTokens.FontCaption - 2, FontStyle.Bold);
            _detailLock = DetailLabel(8f, 6f, 70f, UiKitTokens.FontCaption - 2, FontStyle.Normal);
            _detailLock.horizontalOverflow = HorizontalWrapMode.Wrap;
            _detailLock.alignment = TextAnchor.UpperLeft;
        }

        private Text DetailLabel(float x, float yMin, float yMax, int size, FontStyle style)
        {
            var t = UiFactory.Label(_detail, "", size, TextAnchor.MiddleLeft, UiKitTokens.Text, style);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            UiFactory.SetRect(t, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(x, yMin), new Vector2(-x, yMax));
            return t;
        }

        private void UpdateDetail(int xp)
        {
            if (_detailName == null || _service == null)
                return;
            if (string.IsNullOrEmpty(_selectedId) || !_service.TryGet(_selectedId, out var d))
            {
                _detailName.text = "Bir öğe seç";
                _detailRarity.text = _detailSource.text = _detailLock.text = "";
                return;
            }
            var rarity = CosmeticsPanelLogic.RarityOf(d);
            var rc = UiKitTokens.RarityColor(rarity);
            _detailName.text = d.name;
            _detailRarity.text = UiKitTokens.RarityName(rarity);
            _detailRarity.color = rc;
            _detailSource.text = "KAYNAK: " + CosmeticsPanelLogic.SourceBadge(d);
            _detailSource.color = d.unlockMethod == CosmeticsService.MethodCareerXp ? UiKitTokens.Sand : UiKitTokens.TextMuted;
            var owned = _service.IsOwned(d.id);
            _detailLock.text = owned ? (string.IsNullOrEmpty(d.description) ? "Sahipsin." : d.description) : "" + CosmeticsPanelLogic.LockText(d, xp);
            _detailLock.color = owned ? UiKitTokens.TextDim : UiKitTokens.TextMuted;
        }

        public void Close()
        {
            OverlayState.CosmeticsOpen = false;
            if (this != null)
                Destroy(gameObject);
        }

        private void Update()
        {
            // Esc / gamepad geri: yalnızca bu paneli kapatır (ana menüye sızmaz).
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame))
            {
                OverlayState.ConsumeEscape();
                Close();
            }
        }

        private void LateUpdate()
        {
            if (_pulseRect == null)
                return;
            var left = _pulseUntil - Time.unscaledTime;
            if (left <= 0f)
            {
                _pulseRect.localScale = Vector3.one;
                _pulseRect = null;
                return;
            }
            var k = left / 0.45f; // 1 → 0
            _pulseRect.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(k * Mathf.PI));
        }

        private void OnDestroy() => OverlayState.CosmeticsOpen = false;

        private void Refresh()
        {
            if (_content == null)
                return;
            for (var i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);

            if (_service == null)
            {
                _info.text = "Kozmetik verisi yüklenemedi.";
                return;
            }

            var xp = CosmeticsRuntime.CurrentXp();
            _service.SyncUnlocks(xp);
            _info.text = "Tecrübe: " + xp + "  |  Sahip: " + _service.OwnedCount + "/" + _service.Items.Count + "  |  Yalnızca görsel";

            var slot = Slots[_slot];
            var equipped = _service.GetEquipped(slot);
            foreach (var d in _service.InSlot(slot))
                if (CosmeticsPanelLogic.Passes(d, _service.IsOwned(d.id), _own, _rarityFilter))
                    AddRow(d, equipped == d.id, xp);
            UpdateDetail(xp);
        }

        private void AddRow(CosmeticDefinition d, bool equipped, int xp)
        {
            var owned = _service.IsOwned(d.id);
            var rarity = CosmeticsPanelLogic.RarityOf(d);
            var rc = UiKitTokens.RarityColor(rarity);

            var card = UiKitPanel.Card(_content, equipped ? UiKitTokens.SurfaceHover : UiKitTokens.SurfaceRaised, 10);
            card.gameObject.name = "Item_" + d.id;
            var outline = UiFactory.Image(card, UiSprites.GetRoundedRectOutline(10), equipped ? UiKitTokens.Sand : UiTheme.WithAlpha(rc, owned ? 0.9f : 0.35f));
            outline.raycastTarget = false;
            UiFactory.Stretch(outline);
            if (owned) UiKitGlow.Attach(card, rc, rarity == UiRarity.Legendary);

            var sw = UiFactory.Image(card, UiSprites.GetRoundedRect(8), owned ? CosmeticsRuntime.PreviewColor(d) : UiTheme.Darken(CosmeticsRuntime.PreviewColor(d), 0.6f));
            sw.raycastTarget = false;
            UiFactory.SetRect(sw, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 78f), new Vector2(-12f, -12f));

            var rar = UiFactory.Label(card, UiKitTokens.RarityName(rarity), UiKitTokens.FontCaption - 2, TextAnchor.MiddleLeft, rc, FontStyle.Bold);
            rar.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(rar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -34f), new Vector2(-12f, -14f));

            var name = UiFactory.Label(card, d.name, UiKitTokens.FontCaption + 1, TextAnchor.MiddleLeft, owned ? UiKitTokens.Text : UiKitTokens.TextDim, FontStyle.Bold);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(name, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 52f), new Vector2(-12f, 76f));

            var badge = UiFactory.Label(card, CosmeticsPanelLogic.SourceBadge(d), UiKitTokens.FontCaption - 4, TextAnchor.MiddleRight, UiKitTokens.TextMuted, FontStyle.Bold);
            badge.horizontalOverflow = HorizontalWrapMode.Overflow;
            badge.raycastTarget = false;
            UiFactory.SetRect(badge, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -34f), new Vector2(-14f, -14f));

            var selId = d.id;
            var sel = card.gameObject.AddComponent<Button>();
            sel.transition = Selectable.Transition.None;
            sel.onClick.AddListener(() => { _selectedId = selId; UpdateDetail(CosmeticsRuntime.CurrentXp()); });
            if (_justEquipped == d.id)
            {
                _pulseRect = card;
                _pulseUntil = Time.unscaledTime + 0.45f;
                _justEquipped = null;
            }

            if (!owned)
            {
                var lockText = "" + CosmeticsPanelLogic.LockText(d, xp);
                var lk = UiFactory.Label(card, lockText, UiKitTokens.FontCaption - 2, TextAnchor.MiddleLeft, UiKitTokens.TextMuted);
                lk.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiFactory.SetRect(lk, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 10f), new Vector2(-12f, 48f));
            }
            else
            {
                var captured = d;
                var btn = UiKitButton.Create(card, equipped ? Loc.Get("cosmetics.equipped", "KUŞANILDI") : Loc.Get("cosmetics.equip", "KUŞAN"), () =>
                {
                    try { _selectedId = captured.id; _justEquipped = captured.id; _service.Equip(captured.id); } catch (Exception e) { Debug.LogException(e); }
                    Refresh();
                }, equipped ? UiKitButtonKind.Ghost : UiKitButtonKind.Primary, 188f, 36f);
                UiFactory.Anchor(btn, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(188f, 36f));
                btn.interactable = !equipped;
            }
            if (!string.IsNullOrEmpty(d.description)) UiKitTooltip.Attach(card, d.description);
        }
    }
}
