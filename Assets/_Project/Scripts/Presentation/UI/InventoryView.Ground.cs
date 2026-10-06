using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Localization;
using Project.Infrastructure.Loot;
using Project.Presentation.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>Envanter ekranı: oyuncunun çevresindeki yerdeki eşya listesi (yakından uzağa, nadirlik rengiyle), alma ve sürükleyerek alma.</summary>
    public sealed partial class InventoryView
    {
        private const int MaxGroundRows = 24;

        private sealed class GroundRow
        {
            public RectTransform Root;
            public Image Background;
            public Image Strip;
            public Image Focus;
            public RectTransform IconHolder;
            public InventoryIcon Icon;
            public Text Name;
            public Text Sub;
            public Button Take;
            public int SpawnId;
            public string ShownItemId;
            public bool Visible;
            public bool Reachable;
            public InventoryRarity Rarity;
            public ItemCategory Category;
            public WeaponCategory WeaponKind;
            public string Title;
        }

        private readonly List<GroundRow> _groundRows = new List<GroundRow>(12);
        private readonly List<LootPickupComponent> _groundBuffer = new List<LootPickupComponent>(32);
        private readonly List<InventoryGroundEntry> _groundEntries = new List<InventoryGroundEntry>(32);
        private ScrollRect _groundScroll;
        private RectTransform _groundContent;
        private Text _groundEmpty;
        private Text _groundHeader;
        private int _groundVisible;
        private float _nextGround;
        private int _shownGroundHeaderKey = int.MinValue;

        private void BuildGround(RectTransform column, float y, float inner)
        {
            _groundHeader = UiFactory.Label(column, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.TextHeader, FontStyle.Bold);
            _groundHeader.supportRichText = true;
            _groundHeader.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_groundHeader, UiTheme.Padding, y, inner, 22f);

            var holder = UiFactory.CreateRect("GroundHolder", column);
            UiFactory.SetRect(holder, UiTheme.Padding, y + 24f, inner, 184f);
            _groundScroll = UiWidgets.ScrollList(holder, out _groundContent, 6f, 0);

            _groundEmpty = UiFactory.Label(holder, Loc.Get("inv.ground.empty", "Yakında yerde eşya yok"), UiTheme.FontSmall, TextAnchor.MiddleCenter, UiTheme.TextMuted, FontStyle.Bold);
            _groundEmpty.enabled = true;
        }

        private GroundRow CreateGroundRow(int index)
        {
            var row = new GroundRow();
            var root = UiFactory.Panel(_groundContent, CardColor, UiSprites.ChamferRect);
            root.gameObject.name = "Ground" + index;
            UiFactory.LayoutSize(root, -1f, GroundRowHeight, 1f);
            row.Root = root;
            row.Background = root.GetComponent<Image>();
            row.Background.raycastTarget = true;
            row.Strip = CreateStrip(root);

            row.IconHolder = UiFactory.CreateRect("IconHolder", root);
            UiFactory.SetRect(row.IconHolder, 14f, 5f, 48f, 40f);

            row.Name = UiFactory.Label(root, string.Empty, UiTheme.FontSmall, TextAnchor.UpperLeft, UiTheme.Text, FontStyle.Bold);
            row.Name.supportRichText = true;
            row.Name.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(row.Name, 70f, 5f, 360f, 22f);

            row.Sub = UiFactory.Label(root, string.Empty, UiTheme.FontTiny, TextAnchor.UpperLeft, UiTheme.TextMuted);
            row.Sub.supportRichText = true;
            row.Sub.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(row.Sub, 70f, 27f, 360f, 20f);

            row.Take = SmallButton(root, Loc.Get("inv.take", "AL"), () => PickupGround(row.SpawnId), UiButtonStyle.Primary, 84f, 34f);
            UiFactory.Anchor(row.Take, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(84f, 34f));

            row.Focus = CreateFocus(root);

            var captured = row;
            AttachDrag(root.gameObject, () => SetFocus(FocusArea.Ground, _groundRows.IndexOf(captured)),
                () => MakeGroundPayload(captured),
                () => PickupGround(captured.SpawnId));

            root.gameObject.SetActive(false);
            return row;
        }

        private void UpdateGround(IPlayerHudSource player, InventoryService inventory)
        {
            if (Time.unscaledTime < _nextGround)
                return;
            _nextGround = Time.unscaledTime + GroundRefreshInterval;

            _groundBuffer.Clear();
            _groundEntries.Clear();
            var alive = player != null && !player.IsDead;
            var origin = player != null ? player.Position : Vector3.zero;
            if (player != null && inventory != null && MapMath.IsFinite(origin))
                LootRegistry.FindInRadius(origin, GroundRadius, _groundBuffer);

            for (var i = 0; i < _groundBuffer.Count && _groundEntries.Count < MaxGroundRows; i++)
            {
                var pickup = _groundBuffer[i];
                if (pickup == null || !pickup.IsAvailable)
                    continue;

                var item = pickup.Item;
                _groundEntries.Add(new InventoryGroundEntry
                {
                    SpawnId = pickup.SpawnId,
                    Distance = Vector3.Distance(origin, pickup.transform.position),
                    Rarity = GroundRarity(item)
                });
            }

            InventoryUiLogic.SortGround(_groundEntries);

            for (var i = 0; i < _groundEntries.Count; i++)
            {
                while (_groundRows.Count <= i)
                    _groundRows.Add(CreateGroundRow(_groundRows.Count));

                var entry = _groundEntries[i];
                if (LootRegistry.TryGet(entry.SpawnId, out var pickup) && pickup != null)
                    BindGroundRow(_groundRows[i], pickup, entry, alive);
            }

            for (var i = _groundEntries.Count; i < _groundRows.Count; i++)
            {
                var row = _groundRows[i];
                row.SpawnId = 0;
                if (row.Visible)
                {
                    row.Visible = false;
                    row.Root.gameObject.SetActive(false);
                }
            }

            var changed = _groundVisible != _groundEntries.Count;
            _groundVisible = _groundEntries.Count;
            _groundEmpty.enabled = _groundVisible == 0;

            var headerKey = _groundVisible;
            if (headerKey != _shownGroundHeaderKey)
            {
                _shownGroundHeaderKey = headerKey;
                _groundHeader.text = Loc.Get("inv.ground", "YERDEKİ EŞYALAR") + "  " + UiTheme.Colorize("(" + UiWidgets.Number(_groundVisible) + ")  yakından uzağa", UiTheme.TextMuted);
            }

            if (_focusArea == FocusArea.Ground)
            {
                if (_groundVisible == 0)
                    SetFocus(FocusArea.Left, _lastLeft);
                else if (_focusIndex >= _groundVisible)
                    SetFocus(FocusArea.Ground, _groundVisible - 1);
                else if (changed)
                    ApplyFocusVisual();
            }
        }

        private void BindGroundRow(GroundRow row, LootPickupComponent pickup, InventoryGroundEntry entry, bool alive)
        {
            var item = pickup.Item;
            if (!row.Visible)
            {
                row.Visible = true;
                row.Root.gameObject.SetActive(true);
            }

            row.SpawnId = entry.SpawnId;
            row.Rarity = entry.Rarity;
            row.Category = item.Category;

            var tint = InventoryRarityRules.ColorOf(entry.Rarity);
            if (!string.Equals(row.ShownItemId, item.ItemId, StringComparison.Ordinal))
            {
                row.ShownItemId = item.ItemId;
                row.WeaponKind = GroundWeaponCategory(item);
                row.Strip.color = tint;
                row.Icon = ReplaceIcon(row.IconHolder, row.Icon, item.Category, item.ItemId, row.WeaponKind, tint);
                row.Title = !string.IsNullOrEmpty(item.DisplayName) ? item.DisplayName : pickup.DisplayName;
            }

            var quantity = item.Quantity > 1 ? "  ×" + UiWidgets.Number(item.Quantity) : string.Empty;
            UiFactory.SetText(row.Name, UiTheme.Colorize(row.Title, tint) + quantity);

            var reachable = entry.Distance <= LootPickupComponent.MaxPickupDistance + pickup.RingRadius;
            row.Reachable = reachable;
            UiFactory.SetText(row.Sub, InventoryUiLogic.FormatDistance(entry.Distance) + "  ·  " + ItemCatalog.GetCategoryName(item.Category)
                                       + "  ·  " + InventoryRarityRules.NameOf(entry.Rarity) + (reachable ? string.Empty : "  ·  " + UiTheme.Colorize("uzak", UiTheme.Amber)));
            var canTake = alive && reachable;
            if (row.Take.interactable != canTake)
                UiWidgets.SetInteractable(row.Take, canTake);
        }

        private static WeaponCategory GroundWeaponCategory(LootItemData item)
        {
            if (item.Category != ItemCategory.Weapon)
                return WeaponCategory.None;
            return WeaponCatalog.TryGet(item.ItemId, out var definition) && definition != null ? definition.Category : WeaponCategory.AssaultRifle;
        }

        private static InventoryRarity GroundRarity(LootItemData item)
        {
            if (item.Category == ItemCategory.Weapon)
                return InventoryRarityRules.OfWeapon(GroundWeaponCategory(item));
            var level = ItemCatalog.TryGet(item.ItemId, out var definition) && definition != null ? definition.Level : 0;
            return InventoryRarityRules.Of(item.Category, item.ItemId, level);
        }

        /// <summary>Yerdeki eşyayı alır (otoritede doğrudan, otoritesiz istemcide ağ isteği olarak).</summary>
        private void PickupGround(int spawnId)
        {
            var player = Player;
            if (player == null || player.IsDead || spawnId == 0)
                return;

            if (ForwardIfRemote(InventoryActionRequest.PickupLoot(spawnId)))
                return;

            if (!LootRegistry.TryGet(spawnId, out var pickup) || pickup == null)
            {
                _nextGround = 0f;
                return;
            }

            var result = pickup.PickupBy(player.Combatant);
            if (!result.Accepted)
            {
                UiWidgets.PlaySound(SoundId.DryFire);
                return;
            }

            _dirty = true;
            _nextGround = 0f;
        }
    }
}
