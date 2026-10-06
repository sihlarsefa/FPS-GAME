using System;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Localization;
using Project.Presentation.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>Envanter ekranı: odak/gezinme (klavye, gamepad), hızlı eylem çubuğu, kalibre özeti, simge ve eklenti yuvası güncellemeleri.</summary>
    public sealed partial class InventoryView
    {
        private enum FocusArea
        {
            None = 0,
            /// <summary>Sol sütun: 0..2 silah yuvaları, 3..5 yelek/kask/çanta.</summary>
            Left = 1,
            Grid = 2,
            Ground = 3
        }

        private sealed class AmmoChip
        {
            public AmmoType Type;
            public Image Background;
            public Text Label;
            public Text Value;
        }

        private const float StickThreshold = 0.6f;
        private const float StickRepeat = 0.22f;

        private readonly AmmoChip[] _ammoChips = new AmmoChip[InventoryUiLogic.AmmoOrder.Length];
        private FocusArea _focusArea = FocusArea.None;
        private int _focusIndex;
        private int _lastLeft;
        private int _lastGrid;
        private int _selectedTile = -1;
        private string _selectedItemId;
        private int _shownQuickKey = int.MinValue;
        private int _shownAmmoKey = int.MinValue;
        private float _nextStick;

        private RectTransform _quickRoot;
        private Text _quickLabel;
        private Button _quickUse;
        private Button _quickOne;
        private Button _quickHalf;
        private Button _quickAll;
        private Text _quickOneLabel;

        // ================================================================== Hızlı eylem çubuğu

        private void BuildQuickBar(RectTransform column, float y, float inner)
        {
            _quickRoot = UiFactory.Panel(column, UiTheme.PanelDark, UiSprites.ChamferRect);
            _quickRoot.gameObject.name = "QuickBar";
            _quickRoot.GetComponent<Image>().raycastTarget = false;
            UiFactory.SetRect(_quickRoot, UiTheme.Padding, y, inner, 36f);

            _quickLabel = UiFactory.Label(_quickRoot, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold);
            _quickLabel.supportRichText = true;
            _quickLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_quickLabel, 10f, 0f, inner - 350f, 36f);

            _quickAll = QuickButton(Loc.Get("inv.quick.all", "HEPSİ"), () => QuickDropSelected(InventoryQuickAction.DropAll), UiButtonStyle.Danger, 80f, -4f);
            _quickHalf = QuickButton(Loc.Get("inv.quick.half", "YARISI"), () => QuickDropSelected(InventoryQuickAction.DropHalf), UiButtonStyle.Default, 84f, -90f);
            _quickOne = QuickButton("1 BIRAK", () => QuickDropSelected(InventoryQuickAction.DropOne), UiButtonStyle.Default, 78f, -180f);
            _quickOneLabel = UiFactory.GetButtonLabel(_quickOne);
            _quickUse = QuickButton(Loc.Get("inv.use", "KULLAN"), () => QuickPrimary(SelectedTile()), UiButtonStyle.Primary, 90f, -264f);
            _quickRoot.gameObject.SetActive(false);
        }

        private Button QuickButton(string label, Action onClick, UiButtonStyle style, float width, float right)
        {
            var button = SmallButton(_quickRoot, label, onClick, style, width, 30f);
            UiFactory.Anchor(button, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(right, 0f), new Vector2(width, 30f));
            return button;
        }

        private Tile SelectedTile()
        {
            return _selectedTile >= 0 && _selectedTile < _tiles.Count && _tiles[_selectedTile].Visible ? _tiles[_selectedTile] : null;
        }

        private void SelectTile(int index)
        {
            if (index < 0 || index >= _tiles.Count || !_tiles[index].Visible)
                return;

            _selectedTile = index;
            _selectedItemId = _tiles[index].ItemId;
            SetFocus(FocusArea.Grid, index);
            _shownQuickKey = int.MinValue;
        }

        private void QuickPrimary(Tile tile)
        {
            if (tile != null && tile.Usable)
                UseItem(tile.ItemId);
        }

        private void QuickDropSelected(InventoryQuickAction action)
        {
            QuickDrop(SelectedTile(), action);
        }

        private void QuickDrop(Tile tile, InventoryQuickAction action)
        {
            var inventory = CurrentInventory();
            if (tile == null || inventory == null)
                return;

            var quantity = InventoryUiLogic.SplitQuantity(inventory.GetCount(tile.ItemId), action, tile.Chunk);
            DropStackQuantity(tile.ItemId, quantity);
        }

        private void UpdateQuickBar(IPlayerHudSource player)
        {
            var tile = SelectedTile();
            if (tile == null)
            {
                if (_quickRoot.gameObject.activeSelf)
                    _quickRoot.gameObject.SetActive(false);
                _shownQuickKey = -1;
                return;
            }

            if (!_quickRoot.gameObject.activeSelf)
            {
                _quickRoot.gameObject.SetActive(true);
                _shownQuickKey = int.MinValue;
            }

            var itemUse = player != null ? player.ItemUse : null;
            var alive = player != null && !player.IsDead;
            var canUse = tile.Usable && alive && itemUse != null && itemUse.CanUse(tile.ItemId)
                         && !string.Equals(itemUse.CurrentItemId, tile.ItemId, StringComparison.Ordinal);
            if (_quickUse.interactable != canUse)
                UiWidgets.SetInteractable(_quickUse, canUse);

            var key = (tile.ItemId.GetHashCode() * 31 + tile.Quantity) * 8 + (tile.Usable ? 1 : 0);
            if (key == _shownQuickKey)
                return;

            _shownQuickKey = key;
            var tint = InventoryRarityRules.ColorOf(tile.Rarity);
            _quickLabel.text = UiTheme.Colorize(tile.Name.text, tint) + "  ·  "
                               + Loc.Format("inv.weight", "Ağırlık {0}", MapMath.FormatDecimal(tile.UnitWeight * tile.Quantity, 1)) + tile.Detail;
            SetActive(_quickUse, tile.Usable);
            SetActive(_quickOne, tile.Quantity > tile.Chunk);
            if (_quickOneLabel != null)
                _quickOneLabel.text = UiWidgets.Number(tile.Chunk) + " BIRAK";
            SetActive(_quickHalf, tile.Quantity >= 3 && InventoryUiLogic.SplitQuantity(tile.Quantity, InventoryQuickAction.DropHalf) < tile.Quantity);
        }

        // ================================================================== Kalibre özeti

        private void UpdateAmmoRow(InventoryService inventory)
        {
            var active = inventory != null && inventory.ActiveWeapon != null ? inventory.ActiveWeapon.Definition.AmmoType : AmmoType.None;
            var key = active == AmmoType.None ? 0 : (int)active + 1;
            var infinite = inventory != null && inventory.InfiniteAmmo;
            if (infinite)
                key += 100;
            for (var i = 0; i < _ammoChips.Length; i++)
            {
                var count = inventory != null && !infinite ? Mathf.Clamp(inventory.GetAmmo(_ammoChips[i].Type), 0, 9999) : 0;
                key = key * 31 + count + 1;
            }

            if (key == _shownAmmoKey)
                return;
            _shownAmmoKey = key;

            for (var i = 0; i < _ammoChips.Length; i++)
            {
                var chip = _ammoChips[i];
                var count = inventory != null && !infinite ? Mathf.Clamp(inventory.GetAmmo(chip.Type), 0, 9999) : 0;
                var isActive = chip.Type == active && active != AmmoType.None;
                chip.Value.text = infinite ? "∞" : UiWidgets.Number(count);
                chip.Value.color = infinite ? UiTheme.Text : count <= 0 ? UiTheme.TextMuted : count < 30 && isActive ? UiTheme.Amber : UiTheme.Text;
                chip.Label.color = isActive ? UiTheme.Amber : UiTheme.TextMuted;
                chip.Background.color = isActive ? ActiveCardColor : SlotEmptyColor;
            }
        }

        // ================================================================== Simge ve eklenti yuvaları

        private static InventoryIcon ReplaceIcon(RectTransform holder, InventoryIcon old, ItemCategory category, string itemId, WeaponCategory weapon, Color tint)
        {
            if (old != null && old.Root != null)
            {
                old.Root.gameObject.SetActive(false);
                UiFactory.DestroySafe(old.Root.gameObject);
            }

            if (category == ItemCategory.None || holder == null)
                return null;

            var icon = InventoryIcons.Create(holder, category, itemId, weapon);
            icon.Tint(tint);
            return icon;
        }

        private static void UpdateChips(WeaponCard card, WeaponRuntimeService weapon)
        {
            var key = 0;
            if (weapon != null)
            {
                for (var s = 0; s < card.Chips.Length; s++)
                {
                    var id = weapon.GetAttachment((AttachmentSlot)s);
                    key = key * 31 + (id != null ? (id.GetHashCode() | 1) : 0);
                }

                key |= 1;
            }

            if (key == card.ShownAttachKey)
                return;
            card.ShownAttachKey = key;

            for (var s = 0; s < card.Chips.Length; s++)
            {
                var chip = card.Chips[s];
                var id = weapon != null ? weapon.GetAttachment(chip.Slot) : null;
                if (id == null)
                {
                    chip.Background.color = SlotEmptyColor;
                    chip.Label.text = SlotName(chip.Slot);
                    chip.Label.color = UiTheme.WithAlpha(UiTheme.TextMuted, weapon != null ? 1f : 0.45f);
                    continue;
                }

                var definition = AttachmentCatalog.Get(id);
                var tint = InventoryRarityRules.ColorOf(InventoryRarityRules.Of(ItemCategory.Attachment, id));
                chip.Background.color = Color.Lerp(SlotEmptyColor, tint, 0.38f);
                chip.Label.text = definition != null ? definition.DisplayName : id;
                chip.Label.color = UiTheme.Text;
            }
        }

        // ================================================================== Odak / gezinme

        private void SetFocus(FocusArea area, int index)
        {
            _focusArea = area;
            _focusIndex = Mathf.Max(0, index);
            if (area == FocusArea.Left)
                _lastLeft = _focusIndex;
            else if (area == FocusArea.Grid)
                _lastGrid = _focusIndex;
            ApplyFocusVisual();
        }

        private void ApplyFocusVisual()
        {
            for (var i = 0; i < _weapons.Length; i++)
                SetActive(_weapons[i]?.Focus, _focusArea == FocusArea.Left && _focusIndex == i);
            SetActive(_vestRow?.Focus, _focusArea == FocusArea.Left && _focusIndex == 3);
            SetActive(_helmetRow?.Focus, _focusArea == FocusArea.Left && _focusIndex == 4);
            SetActive(_backpackRow?.Focus, _focusArea == FocusArea.Left && _focusIndex == 5);
            for (var i = 0; i < _tiles.Count; i++)
                SetActive(_tiles[i].Focus, _tiles[i].Visible && (_focusArea == FocusArea.Grid && _focusIndex == i || _selectedTile == i));
            for (var i = 0; i < _groundRows.Count; i++)
                SetActive(_groundRows[i].Focus, _groundRows[i].Visible && _focusArea == FocusArea.Ground && _focusIndex == i);

            if (_focusArea == FocusArea.Grid && _focusIndex < _tiles.Count)
                EnsureVisible(_gridScroll, _tiles[_focusIndex].Root);
            else if (_focusArea == FocusArea.Ground && _focusIndex < _groundRows.Count)
                EnsureVisible(_groundScroll, _groundRows[_focusIndex].Root);
        }

        private static readonly Vector3[] ViewCorners = new Vector3[4];
        private static readonly Vector3[] ItemCorners = new Vector3[4];

        private static void EnsureVisible(ScrollRect scroll, RectTransform item)
        {
            if (scroll == null || item == null || scroll.viewport == null || scroll.content == null || !item.gameObject.activeInHierarchy)
                return;

            scroll.viewport.GetWorldCorners(ViewCorners);
            item.GetWorldCorners(ItemCorners);
            var scale = Mathf.Max(0.0001f, scroll.content.lossyScale.y);
            var position = scroll.content.anchoredPosition;
            if (ItemCorners[1].y > ViewCorners[1].y)
                position.y -= (ItemCorners[1].y - ViewCorners[1].y) / scale;
            else if (ItemCorners[0].y < ViewCorners[0].y)
                position.y += (ViewCorners[0].y - ItemCorners[0].y) / scale;
            else
                return;

            scroll.content.anchoredPosition = position;
        }

        private int VisibleTileCount()
        {
            var count = 0;
            for (var i = 0; i < _tiles.Count; i++)
            {
                if (_tiles[i].Visible)
                    count++;
            }

            return count;
        }

        private void MoveFocus(int dx, int dy)
        {
            var tiles = VisibleTileCount();
            var ground = _groundVisible;

            if (_focusArea == FocusArea.None)
            {
                SetFocus(FocusArea.Left, Mathf.Clamp(_lastLeft, 0, LeftFocusCount - 1));
                return;
            }

            switch (_focusArea)
            {
                case FocusArea.Left:
                    if (dy != 0)
                        SetFocus(FocusArea.Left, Mathf.Clamp(_focusIndex + dy, 0, LeftFocusCount - 1));
                    else if (dx > 0)
                    {
                        if (tiles > 0)
                            SetFocus(FocusArea.Grid, Mathf.Clamp(_lastGrid, 0, tiles - 1));
                        else if (ground > 0)
                            SetFocus(FocusArea.Ground, 0);
                    }

                    break;
                case FocusArea.Grid:
                {
                    if (tiles == 0)
                    {
                        SetFocus(FocusArea.Left, _lastLeft);
                        break;
                    }

                    var column = _focusIndex % InventoryUiLogic.GridColumns;
                    if (dx < 0 && column == 0)
                    {
                        SetFocus(FocusArea.Left, _lastLeft);
                        break;
                    }

                    var next = InventoryUiLogic.NavigateGrid(_focusIndex, tiles, InventoryUiLogic.GridColumns, dx, dy);
                    if (dy > 0 && next == _focusIndex && ground > 0)
                    {
                        SetFocus(FocusArea.Ground, 0);
                        break;
                    }

                    SelectTile(next);
                    break;
                }
                case FocusArea.Ground:
                    if (dx < 0)
                        SetFocus(FocusArea.Left, _lastLeft);
                    else if (dy < 0 && _focusIndex == 0 && tiles > 0)
                        SelectTile(Mathf.Clamp(_lastGrid, 0, tiles - 1));
                    else if (dy != 0)
                        SetFocus(FocusArea.Ground, Mathf.Clamp(_focusIndex + dy, 0, Mathf.Max(0, ground - 1)));
                    break;
            }

            UiWidgets.PlaySound(SoundId.UiClick);
        }

        private void FocusPrimary()
        {
            switch (_focusArea)
            {
                case FocusArea.Left:
                    if (_focusIndex < InventoryService.WeaponSlotCount)
                        EquipWeapon(_focusIndex);
                    break;
                case FocusArea.Grid:
                    QuickPrimary(_focusIndex < _tiles.Count ? _tiles[_focusIndex] : null);
                    break;
                case FocusArea.Ground:
                    if (_focusIndex < _groundRows.Count && _groundRows[_focusIndex].Visible)
                        PickupGround(_groundRows[_focusIndex].SpawnId);
                    break;
            }
        }

        private void FocusDrop(InventoryQuickAction action)
        {
            switch (_focusArea)
            {
                case FocusArea.Left:
                    if (_focusIndex < InventoryService.WeaponSlotCount)
                        DropWeapon(_focusIndex);
                    else
                        DropGear(_focusIndex == 3 ? ItemCategory.Armor : _focusIndex == 4 ? ItemCategory.Helmet : ItemCategory.Backpack);
                    break;
                case FocusArea.Grid:
                    if (_focusIndex < _tiles.Count)
                        QuickDrop(_tiles[_focusIndex], action);
                    break;
            }
        }

        private void HandleInput()
        {
            var keyboard = Keyboard.current;
            var pad = Gamepad.current;
            if (keyboard == null && pad == null)
                return;

            var dx = 0;
            var dy = 0;
            var primary = false;
            var dropAll = false;
            var dropOne = false;
            var dropHalf = false;

            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) dx = -1;
                else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) dx = 1;
                else if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) dy = -1;
                else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) dy = 1;

                primary = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame;
                dropAll = keyboard.deleteKey.wasPressedThisFrame || keyboard.gKey.wasPressedThisFrame;
                dropOne = keyboard.qKey.wasPressedThisFrame;
                dropHalf = keyboard.xKey.wasPressedThisFrame;
            }

            if (pad != null)
            {
                if (pad.dpad.left.wasPressedThisFrame) dx = -1;
                else if (pad.dpad.right.wasPressedThisFrame) dx = 1;
                else if (pad.dpad.up.wasPressedThisFrame) dy = -1;
                else if (pad.dpad.down.wasPressedThisFrame) dy = 1;

                var stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude < 0.09f)
                    _nextStick = 0f;
                else if (stick.magnitude >= StickThreshold && Time.unscaledTime >= _nextStick && dx == 0 && dy == 0)
                {
                    _nextStick = Time.unscaledTime + StickRepeat;
                    if (Mathf.Abs(stick.x) > Mathf.Abs(stick.y))
                        dx = stick.x > 0f ? 1 : -1;
                    else
                        dy = stick.y > 0f ? -1 : 1;
                }

                primary |= pad.buttonSouth.wasPressedThisFrame;
                dropAll |= pad.buttonWest.wasPressedThisFrame;
                dropOne |= pad.rightShoulder.wasPressedThisFrame;
                dropHalf |= pad.buttonNorth.wasPressedThisFrame;
            }

            if (dx != 0 || dy != 0)
                MoveFocus(dx, dy);
            if (primary)
                FocusPrimary();
            if (dropAll)
                FocusDrop(InventoryQuickAction.DropAll);
            else if (dropHalf)
                FocusDrop(InventoryQuickAction.DropHalf);
            else if (dropOne)
                FocusDrop(InventoryQuickAction.DropOne);
        }
    }
}
