using System;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>Envanter ekranı: sürükle-bırak (silah/teçhizat/yığın/yerdeki eşya → BIRAK, EL, çanta bölgeleri).</summary>
    public sealed partial class InventoryView
    {
        private struct DragPayload
        {
            public InventoryDragKind Kind;
            public int Slot;
            public ItemCategory Category;
            public string ItemId;
            public int Quantity;
            public int SpawnId;
            public bool Usable;
            public ItemCategory IconCategory;
            public WeaponCategory IconWeapon;
            public Color Tint;
        }

        private ZoneView _dropZone;
        private ZoneView _handZone;
        private DragPayload _drag;
        private RectTransform _ghost;
        private bool _dragging;
        private bool _zonesLit;
        private Vector2 _dragScreen;
        private Camera _dragCamera;

        private void AttachDrag(GameObject target, Action click, Func<DragPayload> payload, Action doubleClick = null)
        {
            var source = target.AddComponent<InventoryDragSource>();
            source.Clicked = click;
            source.DoubleClicked = doubleClick;
            source.CanDrag = () => payload().Kind != InventoryDragKind.None;
            source.DragBegan = e => BeginDrag(payload(), e);
            source.Dragged = MoveDrag;
            source.DragEnded = EndDrag;
        }

        private DragPayload MakeWeaponPayload(int slot)
        {
            var inventory = CurrentInventory();
            var weapon = inventory != null ? inventory.GetWeapon(slot) : null;
            if (weapon == null)
                return default;

            return new DragPayload
            {
                Kind = InventoryDragKind.Weapon,
                Slot = slot,
                ItemId = weapon.WeaponId,
                Quantity = 1,
                IconCategory = ItemCategory.Weapon,
                IconWeapon = weapon.Category,
                Tint = InventoryRarityRules.ColorOf(InventoryRarityRules.OfWeapon(weapon.Category))
            };
        }

        private DragPayload MakeGearPayload(GearRow row)
        {
            if (row == null || row.ShownItemId == null)
                return default;

            return new DragPayload
            {
                Kind = InventoryDragKind.Gear,
                Category = row.Category,
                ItemId = row.ShownItemId,
                Quantity = 1,
                IconCategory = row.Category,
                Tint = row.Strip != null ? row.Strip.color : Color.white
            };
        }

        private DragPayload MakeStackPayload(Tile tile)
        {
            if (tile == null || !tile.Visible || string.IsNullOrEmpty(tile.ItemId))
                return default;

            return new DragPayload
            {
                Kind = InventoryDragKind.Stack,
                ItemId = tile.ItemId,
                Quantity = tile.Quantity,
                Usable = tile.Usable,
                IconCategory = tile.Category,
                Tint = InventoryRarityRules.ColorOf(tile.Rarity)
            };
        }

        private DragPayload MakeGroundPayload(GroundRow row)
        {
            if (row == null || !row.Visible || row.SpawnId == 0)
                return default;

            return new DragPayload
            {
                Kind = InventoryDragKind.Ground,
                ItemId = row.ShownItemId,
                SpawnId = row.SpawnId,
                Quantity = 1,
                IconCategory = row.Category,
                IconWeapon = row.WeaponKind,
                Tint = InventoryRarityRules.ColorOf(row.Rarity)
            };
        }

        private void BeginDrag(DragPayload payload, PointerEventData eventData)
        {
            if (payload.Kind == InventoryDragKind.None || _panel == null)
                return;

            CancelDrag();
            _drag = payload;
            _dragging = true;
            _dragCamera = eventData.pressEventCamera;

            _ghost = UiFactory.CreateRect("DragGhost", _panel);
            UiFactory.Anchor(_ghost, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 64f));
            var group = _ghost.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.alpha = 0.9f;
            var background = UiFactory.Image(_ghost, UiSprites.ChamferRect, UiTheme.WithAlpha(UiTheme.PanelDark, 0.9f));
            UiFactory.Stretch(background);
            var holder = UiFactory.CreateRect("Icon", _ghost);
            UiFactory.Stretch(holder, 8f);
            var icon = InventoryIcons.Create(holder, payload.IconCategory, payload.ItemId, payload.IconWeapon);
            icon.Tint(payload.Tint);
            _ghost.SetAsLastSibling();

            MoveDrag(eventData);
            UiWidgets.PlaySound(SoundId.UiClick);
        }

        private void MoveDrag(PointerEventData eventData)
        {
            if (!_dragging || _ghost == null)
                return;

            _dragScreen = eventData.position;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)_panel, _dragScreen, _dragCamera, out var world))
                _ghost.position = world;
            UpdateDragVisuals();
        }

        private void EndDrag(PointerEventData eventData)
        {
            if (!_dragging)
                return;

            var payload = _drag;
            _dragScreen = eventData.position;
            var zone = HitZone(_dragScreen);
            CancelDrag();
            ExecuteDrop(payload, zone);
        }

        private void CancelDrag()
        {
            _dragging = false;
            _drag = default;
            if (_ghost != null)
            {
                UiFactory.DestroySafe(_ghost.gameObject);
                _ghost = null;
            }

            UpdateDragVisuals();
        }

        private InventoryDropZone HitZone(Vector2 screen)
        {
            if (Contains(_dropZone, screen))
                return InventoryDropZone.Drop;
            if (Contains(_handZone, screen))
                return InventoryDropZone.Hand;
            if (_backpackRect != null && RectTransformUtility.RectangleContainsScreenPoint(_backpackRect, screen, _dragCamera))
                return InventoryDropZone.Backpack;
            return InventoryDropZone.None;
        }

        private bool Contains(ZoneView zone, Vector2 screen)
        {
            return zone != null && zone.Rect != null && RectTransformUtility.RectangleContainsScreenPoint(zone.Rect, screen, _dragCamera);
        }

        private void ExecuteDrop(DragPayload payload, InventoryDropZone zone)
        {
            if (payload.Kind == InventoryDragKind.None)
                return;

            var intent = InventoryUiLogic.ResolveDrop(payload.Kind, zone, payload.Usable);
            switch (intent)
            {
                case InventoryDropIntent.EquipWeapon: EquipWeapon(payload.Slot); break;
                case InventoryDropIntent.DropWeapon: DropWeapon(payload.Slot); break;
                case InventoryDropIntent.DropGear: DropGear(payload.Category); break;
                case InventoryDropIntent.DropStack: DropStackQuantity(payload.ItemId, payload.Quantity); break;
                case InventoryDropIntent.UseStack: UseItem(payload.ItemId); break;
                case InventoryDropIntent.PickupGround: PickupGround(payload.SpawnId); break;
                default:
                    if (zone != InventoryDropZone.None && zone != InventoryDropZone.Backpack)
                        UiWidgets.PlaySound(SoundId.DryFire);
                    break;
            }
        }

        /// <summary>Sürüklerken uygun bölgeleri vurgular (üstündeki daha parlak); bitince eski rengine döner.</summary>
        private void UpdateDragVisuals()
        {
            if (_dropZone == null || _handZone == null)
                return;

            if (!_dragging)
            {
                if (_zonesLit)
                {
                    _zonesLit = false;
                    _dropZone.Background.color = _dropZone.Base;
                    _handZone.Background.color = _handZone.Base;
                    _dropZone.Label.color = UiTheme.TextDim;
                    _handZone.Label.color = UiTheme.TextDim;
                }

                return;
            }

            _zonesLit = true;
            var hover = HitZone(_dragScreen);
            LightZone(_dropZone, InventoryUiLogic.ResolveDrop(_drag.Kind, InventoryDropZone.Drop, _drag.Usable) != InventoryDropIntent.None, hover == InventoryDropZone.Drop);
            LightZone(_handZone, InventoryUiLogic.ResolveDrop(_drag.Kind, InventoryDropZone.Hand, _drag.Usable) != InventoryDropIntent.None, hover == InventoryDropZone.Hand);
        }

        private static void LightZone(ZoneView zone, bool valid, bool hovered)
        {
            var color = !valid ? UiTheme.WithAlpha(zone.Base, 0.35f) : hovered ? Color.Lerp(zone.Base, Color.white, 0.4f) : Color.Lerp(zone.Base, UiTheme.Amber, 0.25f);
            zone.Background.color = color;
            zone.Label.color = !valid ? UiTheme.TextMuted : hovered ? Color.white : UiTheme.Text;
        }
    }
}
