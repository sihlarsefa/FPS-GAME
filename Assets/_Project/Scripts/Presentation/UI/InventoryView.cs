using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Localization;
using Project.Infrastructure.Loot;
using Project.Presentation.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Envanter ekranı (Tab). Solda teçhizat: üç silah yuvası (ad, şarjör/yedek mermi, kalibre, atış modu, elde olan
    /// işaretli; "KUŞAN" / "BIRAK"), çelik yelek / kask (seviye + dayanıklılık çubuğu) ve sırt çantası (kapasite
    /// katkısı) — her biri bırakılabilir. Sağda sırt çantası: yük/kapasite çubuğu ve yığın listesi (adet, ağırlık;
    /// tıbbi malzeme/takviye için "KULLAN", bir paket ya da tümünü "BIRAK"). Bırakılan eşyalar
    /// <see cref="LootSpawner.SpawnDropped"/> ile oyuncunun önüne düşer (yalnızca otorite tarafında). Altta eşya kullanım
    /// ilerlemesi ve "İPTAL". Açıkken imleç serbesttir, oyun girdisi kapalıdır; kapanınca önceki durum geri gelir.
    /// Envanter <see cref="InventoryService.Changed"/> ile kirlenir; satırlar havuzlanır, yazılar yalnızca değişince
    /// güncellenir. Kapalıyken hiçbir iş yapmaz.
    /// </summary>
    public sealed partial class InventoryView : MonoBehaviour
    {
        private const float BodyWidth = 1280f;
        private const float BodyHeight = 860f;
        private const float HeaderHeight = 56f;
        private const float ColumnTop = 66f;
        private const float ColumnHeight = BodyHeight - ColumnTop - 40f;
        private const float LeftWidth = 620f;
        private const float RightLeft = 640f;
        private const float RightWidth = BodyWidth - RightLeft;
        private const float WeaponCardHeight = 124f;
        private const float GearRowHeight = 56f;
        private const float ZoneHeight = 38f;
        private const float TileWidth = 112f;
        private const float TileHeight = 88f;
        private const float TileSpacing = 6f;
        private const float GroundRowHeight = 50f;
        private const float GroundRadius = 12f;
        private const float GroundRefreshInterval = 0.25f;
        private const float DynamicRefreshInterval = 0.15f;
        private const float DropDistance = 1.3f;
        private const int LeftFocusCount = InventoryService.WeaponSlotCount + 3;

        private static readonly Color CardColor = UiTheme.WithAlpha(UiTheme.PanelLight, 0.92f);
        private static readonly Color ActiveCardColor = UiTheme.Hex(0x45, 0x40, 0x22, 0xF0);
        private static readonly Color SlotEmptyColor = UiTheme.Hex(0x1B, 0x20, 0x14, 0xE0);
        private static readonly Color ZoneDropColor = UiTheme.Hex(0x6A, 0x1C, 0x1C, 0xC0);
        private static readonly Color ZoneHandColor = UiTheme.Hex(0x2C, 0x4A, 0x24, 0xC0);

        private sealed class AttachmentChip
        {
            public Image Background;
            public Text Label;
            public AttachmentSlot Slot;
        }

        private sealed class WeaponCard
        {
            public int Slot;
            public Image Background;
            public Image Strip;
            public Image Focus;
            public RectTransform IconHolder;
            public InventoryIcon Icon;
            public Text Type;
            public Text Name;
            public Text Info;
            public Text Badge;
            public Button Equip;
            public Button Drop;
            public AttachmentChip[] Chips;
            public string ShownWeaponId;
            public int ShownKey = int.MinValue;
            public int ShownBadgeKey = int.MinValue;
            public int ShownAttachKey = int.MinValue;
        }

        private sealed class GearRow
        {
            public ItemCategory Category;
            public string TypeName;
            public Image Strip;
            public Image Focus;
            public RectTransform IconHolder;
            public InventoryIcon Icon;
            public Text Type;
            public Text Name;
            public Text Info;
            public UiProgressBar Bar;
            public Button Drop;
            public string ShownItemId;
            public int ShownKey = int.MinValue;
        }

        private sealed class Tile
        {
            public RectTransform Root;
            public Image Strip;
            public Image Focus;
            public RectTransform IconHolder;
            public InventoryIcon Icon;
            public Text Name;
            public Text Count;
            public string ItemId;
            public int Quantity;
            public int Chunk;
            public bool Usable;
            public bool Visible;
            public InventoryRarity Rarity;
            public ItemCategory Category;
            public float UnitWeight;
            public string Detail;
        }

        private sealed class ZoneView
        {
            public RectTransform Rect;
            public Image Background;
            public Text Label;
            public Color Base;
        }

        private IPlayerHudSource _player;
        private RectTransform _panel;
        private RectTransform _backpackRect;
        private Text _subtitle;
        private Text _vitals;
        private Text _emptyEquipment;
        private RectTransform _equipmentRoot;
        private readonly WeaponCard[] _weapons = new WeaponCard[InventoryService.WeaponSlotCount];
        private GearRow _vestRow;
        private GearRow _helmetRow;
        private GearRow _backpackRow;
        private UiProgressBar _capacityBar;
        private Text _capacityText;
        private RectTransform _gridContent;
        private ScrollRect _gridScroll;
        private Text _emptyList;
        private readonly List<Tile> _tiles = new List<Tile>(24);
        private RectTransform _usePanel;
        private Text _useText;
        private UiProgressBar _useBar;

        private InventoryService _inventory;
        private readonly List<KeyValuePair<string, int>> _stacks = new List<KeyValuePair<string, int>>(16);
        private bool _dirty = true;
        private float _nextDynamicRefresh;
        private int _shownVitalsKey = int.MinValue;
        private int _shownCapacityKey = int.MinValue;
        private int _shownUseKey = int.MinValue;
        private Combatant _shownCombatant;
        private bool _shownNoInventory;
        private bool _built;

        /// <summary>Envanter ekranını tuvale ekler (kapalı başlar). <paramref name="canvasRoot"/> null ise kendi tuvalini oluşturur.</summary>
        public static InventoryView Create(Transform canvasRoot, IPlayerHudSource player)
        {
            var parent = canvasRoot;
            if (parent == null)
            {
                var canvas = UiFactory.CreateCanvas("InventoryCanvas", 20);
                parent = canvas.transform;
            }

            var root = UiFactory.CreateRect("Inventory", parent);
            var view = root.gameObject.AddComponent<InventoryView>();
            view.Build(player);
            return view;
        }

        /// <summary>Envanter açık mı?</summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// Otoritesiz istemcide (Windows dedicated server'a bağlı çevrimiçi oyun) envanter eylemleri yerelde uygulanmaz;
        /// bu olayla ağ katmanına (F2-6 Netcode) iletilir, sunucu doğrulayıp uygular. Çevrimdışı/sunucuda tetiklenmez.
        /// </summary>
        public event Action<InventoryActionRequest> RemoteActionRequested;

        /// <summary>Yerel oyuncu kaynağı (yeniden doğma / izleyici için değiştirilebilir).</summary>
        public IPlayerHudSource Player
        {
            get => HasPlayer ? _player : null;
            set
            {
                if (ReferenceEquals(_player, value))
                    return;
                _player = value;
                _dirty = true;
            }
        }

        /// <summary>Envanteri açar/kapatır.</summary>
        public void Toggle()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }

        /// <summary>Envanteri açar (imleç serbest, oyun girdisi kapalı).</summary>
        public void Open()
        {
            if (IsOpen || !_built)
                return;

            IsOpen = true;
            _panel.gameObject.SetActive(true);
            MapOverlayInput.Acquire(this, Player);
            UiWidgets.PlaySound(SoundId.UiClick);

            _dirty = true;
            _nextDynamicRefresh = 0f;
            _nextGround = 0f;
            InvalidateShown();
            Refresh();
        }

        /// <summary>Envanteri kapatır ve önceki imleç/girdi durumunu geri yükler.</summary>
        public void Close()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            CancelDrag();
            if (_panel != null)
                _panel.gameObject.SetActive(false);
            MapOverlayInput.Release(this);
            UiFactory.ClearSelection();
        }

        private bool HasPlayer => _player != null && !(_player is UnityEngine.Object unityObject && unityObject == null);

        // ================================================================== Kurulum

        private void Build(IPlayerHudSource player)
        {
            _player = player;

            var root = (RectTransform)transform;
            UiFactory.Stretch(root);

            _panel = UiFactory.CreateRect("Panel", root);
            var backdrop = _panel.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.03f, 0.04f, 0.02f, 0.82f);
            backdrop.raycastTarget = true;

            var body = UiFactory.CreateRect("Body", _panel);
            UiFactory.Anchor(body, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BodyWidth, BodyHeight));

            BuildHeader(body);
            BuildEquipment(body);
            BuildBackpack(body);
            BuildFooter(body);

            _built = true;
            _panel.gameObject.SetActive(false);
            IsOpen = false;
        }

        private void BuildHeader(RectTransform body)
        {
            var header = UiFactory.Panel(body, UiTheme.PanelDark, UiSprites.ChamferRect);
            header.gameObject.name = "Header";
            UiFactory.SetRect(header, 0f, 0f, BodyWidth, HeaderHeight);

            var accent = UiFactory.Image(header, null, UiTheme.Accent);
            accent.gameObject.name = "Accent";
            UiFactory.SetRect(accent, new Vector2(0f, 0.18f), new Vector2(0f, 0.82f), new Vector2(10f, 0f), new Vector2(16f, 0f));

            var title = UiFactory.Label(header, Loc.Get("inv.title", "ENVANTER"), UiTheme.FontMedium, TextAnchor.MiddleLeft, UiTheme.TextHeader, FontStyle.Bold);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Stretch(title, 30f, 0f, 0f, 0f);
            UiFactory.AddShadow(title, UiTheme.TextShadow, new Vector2(1f, -1f));

            _subtitle = UiFactory.Label(header, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleCenter, UiTheme.Khaki, FontStyle.Bold);
            _subtitle.horizontalOverflow = HorizontalWrapMode.Overflow;

            _vitals = UiFactory.Label(header, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleRight, UiTheme.Text, FontStyle.Bold);
            _vitals.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Stretch(_vitals, 0f, 0f, 20f, 0f);
        }

        private void BuildEquipment(RectTransform body)
        {
            var column = UiFactory.Panel(body, UiTheme.Panel, UiSprites.ChamferRect);
            column.gameObject.name = "Equipment";
            UiFactory.SetRect(column, 0f, ColumnTop, LeftWidth, ColumnHeight);

            _equipmentRoot = UiFactory.VerticalList(column, 6f, UiTheme.Padding);
            _equipmentRoot.gameObject.name = "List";

            UiWidgets.Header(_equipmentRoot, Loc.Get("inv.weapons", "SİLAHLAR"), UiTheme.FontNormal);
            for (var slot = 0; slot < _weapons.Length; slot++)
                _weapons[slot] = CreateWeaponCard(_equipmentRoot, slot);

            UiWidgets.Header(_equipmentRoot, Loc.Get("inv.armor", "KORUYUCU TEÇHİZAT"), UiTheme.FontNormal);
            _vestRow = CreateGearRow(_equipmentRoot, ItemCategory.Armor, "YELEK", 0);
            _helmetRow = CreateGearRow(_equipmentRoot, ItemCategory.Helmet, "KASK", 1);
            _backpackRow = CreateGearRow(_equipmentRoot, ItemCategory.Backpack, Loc.Get("inv.backpack_slot", "ÇANTA"), 2);

            BuildZones(_equipmentRoot);

            _emptyEquipment = UiFactory.Label(column, Loc.Get("inv.empty", "Envanter bilgisi yok"), UiTheme.FontNormal, TextAnchor.MiddleCenter, UiTheme.TextMuted, FontStyle.Bold);
            _emptyEquipment.enabled = false;
        }

        private Image CreateFocus(RectTransform parent)
        {
            var image = UiFactory.Image(parent, UiSprites.GetRoundedRectOutline(6), UiTheme.Amber);
            image.gameObject.name = "Focus";
            UiFactory.Stretch(image.rectTransform, -2f);
            image.gameObject.SetActive(false);
            return image;
        }

        private static Image CreateStrip(RectTransform parent)
        {
            var strip = UiFactory.Image(parent, null, UiTheme.Khaki);
            strip.gameObject.name = "Strip";
            UiFactory.SetRect(strip, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 6f), new Vector2(5f, -6f));
            return strip;
        }

        private WeaponCard CreateWeaponCard(RectTransform parent, int slot)
        {
            var card = new WeaponCard { Slot = slot };
            var root = UiFactory.Panel(parent, CardColor, UiSprites.ChamferRect);
            root.gameObject.name = "WeaponSlot" + (slot + 1);
            UiFactory.LayoutSize(root, -1f, WeaponCardHeight, 1f);
            card.Background = root.GetComponent<Image>();
            card.Background.raycastTarget = true;
            card.Strip = CreateStrip(root);

            var keyBox = UiFactory.Panel(root, UiTheme.Hex(0xEC, 0xEB, 0xE0, 0xE6), UiSprites.GetRoundedRect(4));
            keyBox.gameObject.name = "Key";
            keyBox.GetComponent<Image>().raycastTarget = false;
            UiFactory.SetRect(keyBox, 14f, 10f, 30f, 30f);
            var keyText = UiFactory.Label(keyBox, UiWidgets.Number(slot + 1), UiTheme.FontNormal, TextAnchor.MiddleCenter, UiTheme.Background, FontStyle.Bold);
            keyText.horizontalOverflow = HorizontalWrapMode.Overflow;

            card.IconHolder = UiFactory.CreateRect("IconHolder", root);
            UiFactory.SetRect(card.IconHolder, 54f, 8f, 96f, 50f);

            card.Type = UiFactory.Label(root, slot == InventoryService.SidearmSlot ? Loc.Get("inv.sidearm", "TABANCA") : Loc.Get("inv.primary", "ANA SİLAH"), UiTheme.FontTiny, TextAnchor.UpperLeft, UiTheme.TextMuted, FontStyle.Bold);
            card.Type.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(card.Type, 158f, 8f, 240f, 20f);

            card.Name = UiFactory.Label(root, string.Empty, UiTheme.FontMedium, TextAnchor.UpperLeft, UiTheme.Text, FontStyle.Bold);
            card.Name.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(card.Name, 158f, 26f, 250f, 34f);
            UiFactory.AddShadow(card.Name, UiTheme.TextShadow, new Vector2(1f, -1f));

            card.Info = UiFactory.Label(root, string.Empty, UiTheme.FontTiny + 2, TextAnchor.UpperLeft, UiTheme.TextDim);
            card.Info.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(card.Info, 54f, 64f, 430f, 22f);

            card.Badge = UiFactory.Label(root, string.Empty, UiTheme.FontTiny, TextAnchor.UpperRight, UiTheme.Amber, FontStyle.Bold);
            card.Badge.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Anchor(card.Badge, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-112f, -10f), new Vector2(220f, 20f));

            // Eklenti yuvaları (görsel): Nişan / Namlu / Tutamak / Şarjör / Dipçik.
            card.Chips = new AttachmentChip[AttachmentCatalog.SlotCount];
            for (var s = 0; s < card.Chips.Length; s++)
                card.Chips[s] = CreateChip(root, (AttachmentSlot)s, 54f + s * 83f, 92f);

            // Sağda dikey düğme sütunu: KUŞAN üstte, BIRAK altta.
            card.Equip = SmallButton(root, Loc.Get("inv.equip", "KUŞAN"), () => EquipWeapon(card.Slot), UiButtonStyle.Default, 88f, 36f);
            UiFactory.Anchor(card.Equip, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -10f), new Vector2(88f, 36f));

            card.Drop = SmallButton(root, Loc.Get("inv.drop", "BIRAK"), () => DropWeapon(card.Slot), UiButtonStyle.Danger, 88f, 36f);
            UiFactory.Anchor(card.Drop, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 10f), new Vector2(88f, 36f));

            card.Focus = CreateFocus(root);

            var index = slot;
            AttachDrag(root.gameObject, () => SetFocus(FocusArea.Left, index),
                () => MakeWeaponPayload(card.Slot));
            return card;
        }

        private static AttachmentChip CreateChip(RectTransform parent, AttachmentSlot slot, float x, float y)
        {
            var chip = new AttachmentChip { Slot = slot };
            var root = UiFactory.Panel(parent, SlotEmptyColor, UiSprites.GetRoundedRect(4));
            root.gameObject.name = "Att_" + slot;
            root.GetComponent<Image>().raycastTarget = false;
            UiFactory.SetRect(root, x, y, 80f, 22f);
            chip.Background = root.GetComponent<Image>();
            chip.Label = UiFactory.Label(root, SlotName(slot), 14, TextAnchor.MiddleCenter, UiTheme.TextMuted, FontStyle.Bold);
            chip.Label.resizeTextForBestFit = true;
            chip.Label.resizeTextMinSize = 8;
            chip.Label.resizeTextMaxSize = 11;
            chip.Label.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.Stretch(chip.Label, 2f, 0f, 2f, 0f);
            return chip;
        }

        private static string SlotName(AttachmentSlot slot)
        {
            switch (slot)
            {
                case AttachmentSlot.Sight: return "NİŞAN";
                case AttachmentSlot.Muzzle: return "NAMLU";
                case AttachmentSlot.Grip: return "TUTAMAK";
                case AttachmentSlot.Magazine: return "ŞARJÖR";
                default: return "DİPÇİK";
            }
        }

        private GearRow CreateGearRow(RectTransform parent, ItemCategory category, string type, int gearIndex)
        {
            var row = new GearRow { Category = category };
            var root = UiFactory.Panel(parent, CardColor, UiSprites.ChamferRect);
            root.gameObject.name = "Gear_" + type;
            UiFactory.LayoutSize(root, -1f, GearRowHeight, 1f);
            root.GetComponent<Image>().raycastTarget = true;
            row.Strip = CreateStrip(root);

            row.IconHolder = UiFactory.CreateRect("IconHolder", root);
            UiFactory.SetRect(row.IconHolder, 14f, 6f, 44f, 44f);

            row.Type = UiFactory.Label(root, type, UiTheme.FontTiny, TextAnchor.UpperLeft, UiTheme.TextMuted, FontStyle.Bold);
            row.Type.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(row.Type, 68f, 5f, 220f, 20f);
            row.TypeName = type;

            row.Name = UiFactory.Label(root, string.Empty, UiTheme.FontNormal, TextAnchor.UpperLeft, UiTheme.Text, FontStyle.Bold);
            row.Name.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(row.Name, 68f, 24f, 220f, 28f);

            row.Bar = UiFactory.ProgressBar(root, UiTheme.Armor, UiTheme.Track);
            row.Bar.TrailEnabled = false;
            UiFactory.SetRect(row.Bar, 296f, 14f, 184f, 14f);

            row.Info = UiFactory.Label(root, string.Empty, UiTheme.FontTiny, TextAnchor.UpperLeft, UiTheme.TextDim, FontStyle.Bold);
            row.Info.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(row.Info, 296f, 32f, 190f, 20f);

            row.Drop = SmallButton(root, Loc.Get("inv.drop", "BIRAK"), () => DropGear(row.Category), UiButtonStyle.Danger, 88f, 34f);
            UiFactory.Anchor(row.Drop, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(88f, 34f));

            row.Focus = CreateFocus(root);

            var index = InventoryService.WeaponSlotCount + gearIndex;
            AttachDrag(root.gameObject, () => SetFocus(FocusArea.Left, index), () => MakeGearPayload(row));
            return row;
        }

        private void BuildZones(RectTransform parent)
        {
            var holder = UiFactory.CreateRect("Zones", parent);
            UiFactory.LayoutSize(holder, -1f, ZoneHeight, 1f);
            _dropZone = CreateZone(holder, "ZoneDrop", Loc.Get("inv.zone.drop", "BIRAK  ·  sürükle-bırak"), 0f, 0.5f, ZoneDropColor);
            _handZone = CreateZone(holder, "ZoneHand", Loc.Get("inv.zone.hand", "EL  ·  kuşan / kullan"), 0.5f, 1f, ZoneHandColor);
        }

        private static ZoneView CreateZone(RectTransform holder, string name, string label, float x0, float x1, Color color)
        {
            var zone = new ZoneView { Base = color };
            var rect = UiFactory.Panel(holder, color, UiSprites.ChamferRect);
            rect.gameObject.name = name;
            UiFactory.SetRect(rect, new Vector2(x0, 0f), new Vector2(x1, 1f), new Vector2(x0 > 0f ? 3f : 0f, 0f), new Vector2(x1 < 1f ? -3f : 0f, 0f));
            zone.Rect = rect;
            zone.Background = rect.GetComponent<Image>();
            zone.Background.raycastTarget = false;
            zone.Label = UiFactory.Label(rect, label, UiTheme.FontSmall, TextAnchor.MiddleCenter, UiTheme.TextDim, FontStyle.Bold);
            zone.Label.horizontalOverflow = HorizontalWrapMode.Overflow;
            return zone;
        }

        private void BuildBackpack(RectTransform body)
        {
            var column = UiFactory.Panel(body, UiTheme.Panel, UiSprites.ChamferRect);
            column.gameObject.name = "Backpack";
            UiFactory.SetRect(column, RightLeft, ColumnTop, RightWidth, ColumnHeight);
            _backpackRect = column;
            const float inner = RightWidth - UiTheme.Padding * 2f;

            var header = UiWidgets.Header(column, Loc.Get("inv.backpack", "SIRT ÇANTASI"), UiTheme.FontNormal);
            UiFactory.SetRect(header.transform.parent as RectTransform, UiTheme.Padding, UiTheme.Padding, inner, UiTheme.FontNormal + 18f);

            // Yük / kapasite.
            var capacityLabel = UiFactory.Label(column, Loc.Get("inv.load", "YÜK"), UiTheme.FontTiny, TextAnchor.MiddleLeft, UiTheme.TextMuted, FontStyle.Bold);
            UiFactory.SetRect(capacityLabel, UiTheme.Padding, 56f, 60f, 20f);
            _capacityBar = UiFactory.ProgressBar(column, UiTheme.HealthHigh, UiTheme.Track);
            _capacityBar.TrailEnabled = false;
            _capacityBar.SetSegments(4, UiTheme.WithAlpha(UiTheme.Background, 0.6f), 2f);
            UiFactory.SetRect(_capacityBar, UiTheme.Padding + 52f, 60f, inner - 52f - 170f, 14f);
            _capacityText = UiFactory.Label(column, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleRight, UiTheme.Text, FontStyle.Bold);
            _capacityText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_capacityText, RightWidth - UiTheme.Padding - 160f, 54f, 160f, 26f);

            // Kalibre özeti.
            BuildAmmoRow(column, 86f, inner);

            // Izgara.
            var gridHolder = UiFactory.CreateRect("GridHolder", column);
            UiFactory.SetRect(gridHolder, UiTheme.Padding, 124f, inner, TileHeight * 3f + TileSpacing * 2f + 4f);
            _gridScroll = UiWidgets.ScrollList(gridHolder, out _gridContent, TileSpacing, 0);
            ConvertToGrid(_gridContent);

            _emptyList = UiFactory.Label(gridHolder, Loc.Get("inv.empty", "Çanta boş"), UiTheme.FontNormal, TextAnchor.MiddleCenter, UiTheme.TextMuted, FontStyle.Bold);
            _emptyList.enabled = false;

            // Hızlı eylemler.
            BuildQuickBar(column, 124f + TileHeight * 3f + TileSpacing * 2f + 10f, inner);

            // Yerdeki eşyalar.
            BuildGround(column, 124f + TileHeight * 3f + TileSpacing * 2f + 52f, inner);

            // Eşya kullanım durumu.
            _usePanel = UiFactory.Panel(column, UiTheme.PanelDark, UiSprites.ChamferRect);
            _usePanel.gameObject.name = "UseStatus";
            _usePanel.GetComponent<Image>().raycastTarget = false;
            UiFactory.SetRect(_usePanel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(UiTheme.Padding, 14f), new Vector2(-UiTheme.Padding, 74f));
            _useText = UiFactory.Label(_usePanel, string.Empty, UiTheme.FontSmall, TextAnchor.UpperLeft, UiTheme.Text, FontStyle.Bold);
            _useText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_useText, 12f, 8f, 400f, 24f);
            _useBar = UiFactory.ProgressBar(_usePanel, UiTheme.Success, UiTheme.Track);
            _useBar.TrailEnabled = false;
            UiFactory.SetRect(_useBar, 12f, 38f, inner - 24f - 120f, 10f);
            var cancel = SmallButton(_usePanel, Loc.Get("inv.cancel", "İPTAL"), CancelUse, UiButtonStyle.Default, 104f, 38f);
            UiFactory.Anchor(cancel, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(104f, 38f));
            _usePanel.gameObject.SetActive(false);
        }

        private static void ConvertToGrid(RectTransform content)
        {
            var vertical = content.GetComponent<VerticalLayoutGroup>();
            if (vertical != null)
                DestroyImmediate(vertical); // Aynı nesnede iki yerleşim grubu olamaz: Destroy ertelenir, anında kaldırılmalı.

            var grid = content.GetComponent<GridLayoutGroup>();
            if (grid == null)
                grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(TileWidth, TileHeight);
            grid.spacing = new Vector2(TileSpacing, TileSpacing);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = InventoryUiLogic.GridColumns;
        }

        private static void BuildFooter(RectTransform body)
        {
            var footer = UiFactory.CreateRect("Footer", body);
            UiFactory.SetRect(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 32f));
            var hint = UiFactory.Label(footer,
                UiTheme.Colorize("[Tab]", UiTheme.Amber) + " Kapat   "
                + UiTheme.Colorize("[Ok/WASD]", UiTheme.Amber) + " Gezin   "
                + UiTheme.Colorize("[Enter]", UiTheme.Amber) + " Kuşan/Kullan/Al   "
                + UiTheme.Colorize("[Del]", UiTheme.Amber) + " Bırak   "
                + UiTheme.Colorize("[Q]", UiTheme.Amber) + " Paket   "
                + UiTheme.Colorize("[X]", UiTheme.Amber) + " Yarısı   "
                + UiTheme.Colorize("[Sürükle]", UiTheme.Amber) + " Bırak/El/Çanta   "
                + UiTheme.Colorize(Project.Infrastructure.Input.InputBindings.Bracket(BindAction.Heal), UiTheme.Amber) + " İyileş   " + UiTheme.Colorize(Project.Infrastructure.Input.InputBindings.Bracket(BindAction.Boost), UiTheme.Amber) + " Takviye",
                UiTheme.FontSmall, TextAnchor.MiddleCenter, UiTheme.TextDim, FontStyle.Bold);
            hint.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private static Button SmallButton(Transform parent, string label, Action onClick, UiButtonStyle style, float width, float height)
        {
            // Tıklamadan sonra seçimi bırak: aksi hâlde Boşluk/Enter "gönder" ile aynı düğme yeniden tetiklenir.
            var button = UiFactory.Button(parent, label, () =>
            {
                onClick?.Invoke();
                UiFactory.ClearSelection();
            }, style);
            UiFactory.LayoutSize(button, width, height);
            var text = UiFactory.GetButtonLabel(button);
            if (text != null)
            {
                text.fontSize = UiTheme.FontSmall;
                UiFactory.Stretch(text, 6f, 0f, 6f, 0f);
            }

            // Üzerine gelmek seçmesin: aksi hâlde Boşluk/Enter "gönder" ile seçili düğme (ör. BIRAK) tetiklenebilir.
            var feedback = button.GetComponent<UiButtonFeedback>();
            if (feedback != null)
                feedback.SelectOnHover = false;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        private Tile CreateTile(int index)
        {
            var tile = new Tile();
            var root = UiFactory.Panel(_gridContent, CardColor, UiSprites.ChamferRect);
            root.gameObject.name = "Tile" + index;
            root.GetComponent<Image>().raycastTarget = true;
            tile.Root = root;

            tile.Strip = UiFactory.Image(root, null, UiTheme.Khaki);
            tile.Strip.gameObject.name = "Rarity";
            UiFactory.SetRect(tile.Strip, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(4f, 0f), new Vector2(-4f, 4f));

            tile.IconHolder = UiFactory.CreateRect("IconHolder", root);
            UiFactory.Anchor(tile.IconHolder, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(58f, 46f));

            tile.Count = UiFactory.Label(root, string.Empty, UiTheme.FontSmall, TextAnchor.UpperRight, UiTheme.Text, FontStyle.Bold);
            tile.Count.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Anchor(tile.Count, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-7f, -4f), new Vector2(56f, 20f));
            UiFactory.AddShadow(tile.Count, UiTheme.TextShadow, new Vector2(1f, -1f));

            tile.Name = UiFactory.Label(root, string.Empty, UiTheme.FontTiny, TextAnchor.MiddleCenter, UiTheme.TextDim, FontStyle.Bold);
            tile.Name.resizeTextForBestFit = true;
            tile.Name.resizeTextMinSize = 8;
            tile.Name.resizeTextMaxSize = UiTheme.FontTiny;
            tile.Name.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.Anchor(tile.Name, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(TileWidth - 8f, 28f));

            tile.Focus = CreateFocus(root);

            var captured = tile;
            AttachDrag(root.gameObject, () => SelectTile(_tiles.IndexOf(captured)), () => MakeStackPayload(captured),
                () => { var i = _tiles.IndexOf(captured); SelectTile(i); QuickPrimary(captured); });

            root.gameObject.SetActive(false);
            return tile;
        }

        private void BuildAmmoRow(RectTransform column, float y, float inner)
        {
            var count = InventoryUiLogic.AmmoOrder.Length;
            var gap = 6f;
            var width = (inner - gap * (count - 1)) / count;
            for (var i = 0; i < count; i++)
            {
                var chip = new AmmoChip { Type = InventoryUiLogic.AmmoOrder[i] };
                var root = UiFactory.Panel(column, SlotEmptyColor, UiSprites.ChamferRect);
                root.gameObject.name = "Ammo_" + chip.Type;
                root.GetComponent<Image>().raycastTarget = false;
                UiFactory.SetRect(root, UiTheme.Padding + i * (width + gap), y, width, 30f);
                chip.Background = root.GetComponent<Image>();
                chip.Label = UiFactory.Label(root, InventoryUiLogic.CaliberLabel(chip.Type), UiTheme.FontTiny, TextAnchor.MiddleLeft, UiTheme.TextMuted, FontStyle.Bold);
                chip.Label.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiFactory.Stretch(chip.Label, 10f, 0f, 0f, 0f);
                chip.Value = UiFactory.Label(root, "0", UiTheme.FontSmall, TextAnchor.MiddleRight, UiTheme.Text, FontStyle.Bold);
                chip.Value.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiFactory.Stretch(chip.Value, 0f, 0f, 10f, 0f);
                _ammoChips[i] = chip;
            }
        }

        // ================================================================== Kare döngüsü

        private void LateUpdate()
        {
            if (!_built || !IsOpen)
                return;

            MapOverlayInput.Maintain();
            HandleInput();
            Refresh();
        }

        private void Refresh()
        {
            var player = Player;
            var combatant = player != null ? player.Combatant : null;
            var inventory = player != null ? player.Inventory : null;
            Bind(inventory);

            var noInventory = inventory == null;
            if (noInventory != _shownNoInventory)
            {
                _shownNoInventory = noInventory;
                _emptyEquipment.enabled = noInventory;
                _equipmentRoot.gameObject.SetActive(!noInventory);
                _dirty = true;
            }

            if (!ReferenceEquals(combatant, _shownCombatant))
            {
                _shownCombatant = combatant;
                UiFactory.SetText(_subtitle, combatant != null ? combatant.RankedName : string.Empty);
            }

            if (_dirty)
            {
                _dirty = false;
                RebuildStacks(inventory);
                InvalidateShown();
                _nextDynamicRefresh = 0f;
            }

            if (Time.unscaledTime >= _nextDynamicRefresh)
            {
                _nextDynamicRefresh = Time.unscaledTime + DynamicRefreshInterval;
                if (inventory != null)
                {
                    for (var i = 0; i < _weapons.Length; i++)
                        UpdateWeaponCard(_weapons[i], inventory);
                    UpdateGearRow(_vestRow, inventory);
                    UpdateGearRow(_helmetRow, inventory);
                    UpdateGearRow(_backpackRow, inventory);
                }

                UpdateCapacity(inventory);
                UpdateAmmoRow(inventory);
                UpdateVitals(combatant);
                UpdateQuickBar(player);
            }

            UpdateGround(player, inventory);
            UpdateDragVisuals();

            UpdateUseStatus(player);
        }

        private void Bind(InventoryService inventory)
        {
            if (ReferenceEquals(inventory, _inventory))
                return;

            if (_inventory != null)
                _inventory.Changed -= OnInventoryChanged;
            _inventory = inventory;
            if (_inventory != null)
                _inventory.Changed += OnInventoryChanged;
            _dirty = true;
        }

        private void OnInventoryChanged()
        {
            _dirty = true;
        }

        private void InvalidateShown()
        {
            for (var i = 0; i < _weapons.Length; i++)
            {
                if (_weapons[i] == null)
                    continue;
                _weapons[i].ShownKey = int.MinValue;
                _weapons[i].ShownBadgeKey = int.MinValue;
                _weapons[i].ShownWeaponId = null;
                _weapons[i].ShownAttachKey = int.MinValue;
            }

            if (_vestRow != null) { _vestRow.ShownKey = int.MinValue; _vestRow.ShownItemId = null; }
            if (_helmetRow != null) { _helmetRow.ShownKey = int.MinValue; _helmetRow.ShownItemId = null; }
            if (_backpackRow != null) { _backpackRow.ShownKey = int.MinValue; _backpackRow.ShownItemId = null; }
            _shownVitalsKey = int.MinValue;
            _shownCapacityKey = int.MinValue;
            _shownUseKey = int.MinValue;
            _shownQuickKey = int.MinValue;
            _shownAmmoKey = int.MinValue;
        }

        // ------------------------------------------------------------------ Silahlar

        private void UpdateWeaponCard(WeaponCard card, InventoryService inventory)
        {
            var weapon = inventory.GetWeapon(card.Slot);
            var weaponId = weapon != null ? weapon.WeaponId : null;
            var active = weapon != null && inventory.ActiveSlot == card.Slot;

            if (!string.Equals(weaponId, card.ShownWeaponId, StringComparison.Ordinal) || card.ShownKey == int.MinValue)
            {
                card.ShownWeaponId = weaponId;
                card.ShownKey = int.MinValue;
                card.ShownAttachKey = int.MinValue;
                if (weapon == null)
                {
                    card.Name.text = Loc.Get("inv.empty_dash", "— Boş —");
                    card.Name.color = UiTheme.TextMuted;
                    card.Type.text = card.Slot == InventoryService.SidearmSlot ? Loc.Get("inv.sidearm", "TABANCA") : Loc.Get("inv.primary", "ANA SİLAH");
                    card.Info.text = card.Slot == InventoryService.SidearmSlot ? Loc.Get("inv.slot.sidearm", "Tabanca yuvası") : Loc.Get("inv.slot.primary", "Ana silah yuvası");
                    card.Strip.color = UiTheme.WithAlpha(UiTheme.PanelBorder, 0.6f);
                    card.Icon = ReplaceIcon(card.IconHolder, card.Icon, ItemCategory.None, null, WeaponCategory.None, Color.white);
                }
                else
                {
                    card.Name.text = WeaponName(weapon);
                    card.Name.color = UiTheme.Text;
                    var rarity = InventoryRarityRules.OfWeapon(weapon.Category);
                    var tint = InventoryRarityRules.ColorOf(rarity);
                    card.Strip.color = tint;
                    card.Type.text = (card.Slot == InventoryService.SidearmSlot ? Loc.Get("inv.sidearm", "TABANCA") : Loc.Get("inv.primary", "ANA SİLAH"))
                                     + "  ·  " + UiTheme.Colorize(InventoryRarityRules.NameOf(rarity), tint);
                    card.Icon = ReplaceIcon(card.IconHolder, card.Icon, ItemCategory.Weapon, weaponId, weapon.Category, tint);
                }

                SetActive(card.Drop, weapon != null);
            }

            UpdateChips(card, weapon);
            SetActive(card.Equip, weapon != null && !active);
            var background = active ? ActiveCardColor : CardColor;
            if (card.Background.color != background)
                card.Background.color = background;

            if (weapon == null)
            {
                if (card.ShownBadgeKey != 0)
                {
                    card.ShownBadgeKey = 0;
                    card.Badge.text = string.Empty;
                }

                return;
            }

            // Bilgi satırı: şarjör, yedek, kalibre, atış modu.
            var infinite = weapon.HasInfiniteReserve || inventory.InfiniteAmmo;
            var reserve = infinite ? -1 : Mathf.Clamp(weapon.ReserveAmmo, 0, 9999);
            var key = ((Mathf.Clamp(weapon.CurrentAmmo, 0, 999) * 10000 + (reserve + 1)) * 4 + (int)weapon.CurrentFireMode);
            if (key != card.ShownKey)
            {
                card.ShownKey = key;
                card.Info.text = Loc.Format("inv.mag", "Şarjör {0}/{1}", UiWidgets.Number(weapon.CurrentAmmo), UiWidgets.Number(weapon.MagazineSize))
                                 + "  ·  Yedek " + (infinite ? "∞" : UiWidgets.Number(reserve))
                                 + "  ·  " + AmmoName(weapon.Definition.AmmoType)
                                 + "  ·  " + FireModeName(weapon.CurrentFireMode);
            }

            // Rozet: şarjör değiştirme ilerlemesi / elde.
            var badgeKey = weapon.IsReloading ? 1000 + Mathf.RoundToInt(weapon.ReloadProgress * 100f) : active ? 1 : 2;
            if (badgeKey != card.ShownBadgeKey)
            {
                card.ShownBadgeKey = badgeKey;
                if (weapon.IsReloading)
                {
                    card.Badge.color = UiTheme.Amber;
                    card.Badge.text = Loc.Format("inv.reloading", "ŞARJÖR DEĞİŞİYOR %{0}", UiWidgets.Number(badgeKey - 1000));
                }
                else if (active)
                {
                    card.Badge.color = UiTheme.Amber;
                    card.Badge.text = "ELDE";
                }
                else
                {
                    card.Badge.text = string.Empty;
                }
            }
        }

        private static string WeaponName(WeaponRuntimeService weapon)
        {
            var definition = weapon.Definition;
            if (definition != null && !string.IsNullOrEmpty(definition.DisplayName))
                return definition.DisplayName;
            return ItemCatalog.GetDisplayName(weapon.WeaponId);
        }

        private static string AmmoName(AmmoType type)
        {
            switch (type)
            {
                case AmmoType.Mm9: return "9 mm";
                case AmmoType.Mm556: return "5.56 mm";
                case AmmoType.Mm762: return "7.62 mm";
                case AmmoType.Gauge12: return "12 kalibre";
                default: return "-";
            }
        }

        private static string FireModeName(FireMode mode)
        {
            switch (mode)
            {
                case FireMode.Burst: return Loc.Get("inv.fire.burst", "SERİ");
                case FireMode.Auto: return "OTO";
                default: return Loc.Get("inv.fire.semi", "TEK");
            }
        }

        // ------------------------------------------------------------------ Koruyucu teçhizat

        private void UpdateGearRow(GearRow row, InventoryService inventory)
        {
            string itemId;
            ArmorPiece piece = null;
            switch (row.Category)
            {
                case ItemCategory.Armor:
                    piece = inventory.Vest;
                    itemId = piece != null ? piece.ItemId : null;
                    break;
                case ItemCategory.Helmet:
                    piece = inventory.Helmet;
                    itemId = piece != null ? piece.ItemId : null;
                    break;
                default:
                    itemId = inventory.BackpackLevel > 0 ? inventory.BackpackItemId : null;
                    break;
            }

            var durability = piece != null ? Mathf.CeilToInt(piece.Durability) : 0;
            var key = itemId == null ? -1 : durability;
            if (string.Equals(itemId, row.ShownItemId, StringComparison.Ordinal) && key == row.ShownKey)
                return;

            var idChanged = !string.Equals(itemId, row.ShownItemId, StringComparison.Ordinal) || row.ShownKey == int.MinValue;
            row.ShownItemId = itemId;
            row.ShownKey = key;

            if (itemId == null)
            {
                row.Strip.color = UiTheme.WithAlpha(UiTheme.PanelBorder, 0.6f);
                row.Icon = ReplaceIcon(row.IconHolder, row.Icon, ItemCategory.None, null, WeaponCategory.None, Color.white);
                row.Name.text = row.Category == ItemCategory.Armor ? Loc.Get("inv.no_vest", "Yelek yok") : row.Category == ItemCategory.Helmet ? Loc.Get("inv.no_helmet", "Kask yok") : Loc.Get("inv.no_pack", "Çanta yok");
                row.Name.color = UiTheme.TextMuted;
                row.Type.text = row.TypeName;
                row.Info.text = string.Empty;
                SetActive(row.Bar, false);
                SetActive(row.Drop, false);
                return;
            }

            if (idChanged)
            {
                row.Name.text = ItemCatalog.GetDisplayName(itemId);
                row.Name.color = UiTheme.Text;
                SetActive(row.Drop, true);
                var level = piece != null ? piece.Level : inventory.BackpackLevel;
                var gearTint = InventoryRarityRules.ColorOf(InventoryRarityRules.Of(row.Category, itemId, level));
                row.Strip.color = gearTint;
                row.Icon = ReplaceIcon(row.IconHolder, row.Icon, row.Category, itemId, WeaponCategory.None, gearTint);
            }

            if (piece != null)
            {
                SetActive(row.Bar, true);
                var fraction = piece.DurabilityNormalized;
                row.Bar.SetValue(fraction, true);
                var state = InventoryUiLogic.DurabilityState(fraction, piece.IsBroken);
                row.Bar.FillColor = state == 2 ? UiTheme.Danger : state == 1 ? UiTheme.Amber : UiTheme.Armor;
                row.Info.text = state == 2
                    ? UiTheme.Colorize("KIRIK", UiTheme.Danger)
                    : Loc.Format("inv.durability", "Dayanıklılık {0}/{1}", UiWidgets.Number(durability), UiWidgets.Number(Mathf.CeilToInt(piece.MaxDurability)))
                      + "  %" + UiWidgets.Number(Mathf.RoundToInt(fraction * 100f));
                if (idChanged)
                {
                    row.Type.text = row.Category == ItemCategory.Armor
                        ? row.TypeName + "  ·  +" + UiWidgets.Number(Mathf.RoundToInt(InventoryService.VestCapacityBonus)) + " kapasite"
                        : row.TypeName + "  ·  Seviye " + UiWidgets.Number(piece.Level);
                }
            }
            else
            {
                SetActive(row.Bar, false);
                var capacity = ItemCatalog.TryGet(itemId, out var definition) && definition != null ? definition.Capacity : 0f;
                row.Type.text = row.TypeName + "  ·  Seviye " + UiWidgets.Number(inventory.BackpackLevel);
                row.Info.text = capacity > 0f ? "+" + UiWidgets.Number(Mathf.RoundToInt(capacity)) + " kapasite" : string.Empty;
            }
        }

        // ------------------------------------------------------------------ Sırt çantası

        private void RebuildStacks(InventoryService inventory)
        {
            var selectedId = _selectedItemId;
            _stacks.Clear();
            if (inventory != null)
                inventory.GetStacks(_stacks);
            _stacks.Sort(CompareStacks);

            for (var i = 0; i < _stacks.Count; i++)
            {
                while (_tiles.Count <= i)
                    _tiles.Add(CreateTile(_tiles.Count));

                BindTile(_tiles[i], _stacks[i].Key, _stacks[i].Value);
            }

            for (var i = _stacks.Count; i < _tiles.Count; i++)
            {
                var tile = _tiles[i];
                tile.ItemId = null;
                tile.Quantity = 0;
                if (tile.Visible)
                {
                    tile.Visible = false;
                    tile.Root.gameObject.SetActive(false);
                }
            }

            _emptyList.enabled = _stacks.Count == 0;
            if (_emptyList.enabled)
                _emptyList.text = inventory == null ? Loc.Get("inv.no_data", "Envanter bilgisi yok") : Loc.Get("inv.empty", "Çanta boş");

            // Seçim eşya kimliğiyle korunur (sıra değişse de aynı eşya seçili kalır).
            _selectedTile = -1;
            if (selectedId != null)
            {
                for (var i = 0; i < _stacks.Count; i++)
                {
                    if (string.Equals(_stacks[i].Key, selectedId, StringComparison.Ordinal))
                    {
                        _selectedTile = i;
                        break;
                    }
                }
            }

            if (_selectedTile < 0)
                _selectedItemId = null;
            if (_focusArea == FocusArea.Grid)
            {
                if (_stacks.Count == 0)
                    _focusArea = FocusArea.None;
                else
                    _focusIndex = Mathf.Clamp(_selectedTile >= 0 ? _selectedTile : _focusIndex, 0, _stacks.Count - 1);
            }

            _shownQuickKey = int.MinValue;
            ApplyFocusVisual();
        }

        /// <summary>Izgara sırası: kategori (mermi, tıbbi, takviye, fırlatılabilir, eklenti, teçhizat), sonra nadirlik (yüksek önce), sonra ad.</summary>
        private static int CompareStacks(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
        {
            ItemCatalog.TryGet(a.Key, out var da);
            ItemCatalog.TryGet(b.Key, out var db);
            var ca = da != null ? CategoryOrder(da.Category) : 99;
            var cb = db != null ? CategoryOrder(db.Category) : 99;
            if (ca != cb)
                return ca.CompareTo(cb);
            var ra = da != null ? InventoryRarityRules.Of(da.Category, a.Key, da.Level) : InventoryRarity.Common;
            var rb = db != null ? InventoryRarityRules.Of(db.Category, b.Key, db.Level) : InventoryRarity.Common;
            if (ra != rb)
                return rb.CompareTo(ra);
            return string.CompareOrdinal(a.Key, b.Key);
        }

        private static int CategoryOrder(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Ammunition: return 0;
                case ItemCategory.Medical: return 1;
                case ItemCategory.Boost: return 2;
                case ItemCategory.Throwable: return 3;
                case ItemCategory.Attachment: return 4;
                case ItemCategory.Equipment: return 5;
                default: return 6;
            }
        }

        private void BindTile(Tile tile, string itemId, int quantity)
        {
            var sameItem = string.Equals(tile.ItemId, itemId, StringComparison.Ordinal);
            if (sameItem && tile.Quantity == quantity && tile.Visible)
                return;

            tile.ItemId = itemId;
            tile.Quantity = quantity;
            if (!tile.Visible)
            {
                tile.Visible = true;
                tile.Root.gameObject.SetActive(true);
            }

            ItemCatalog.TryGet(itemId, out var definition);
            var category = definition != null ? definition.Category : ItemCategory.None;

            if (!sameItem)
            {
                tile.Category = category;
                tile.Name.text = definition != null ? definition.DisplayName : itemId;
                tile.Rarity = InventoryRarityRules.Of(category, itemId, definition != null ? definition.Level : 0);
                var tint = InventoryRarityRules.ColorOf(tile.Rarity);
                tile.Strip.color = tint;
                tile.Icon = ReplaceIcon(tile.IconHolder, tile.Icon, category, itemId, WeaponCategory.None, tint);
                tile.Usable = category == ItemCategory.Medical || category == ItemCategory.Boost;
                tile.UnitWeight = definition != null ? definition.Weight : 0f;
                var detail = string.Empty;
                if (category == ItemCategory.Medical && definition != null && definition.HealAmount > 0f)
                    detail = "  ·  +" + UiWidgets.Number(Mathf.RoundToInt(definition.HealAmount)) + " can";
                else if (category == ItemCategory.Boost && definition != null && definition.BoostAmount > 0f)
                    detail = "  ·  +" + UiWidgets.Number(Mathf.RoundToInt(definition.BoostAmount)) + " takviye";
                tile.Detail = detail;
            }

            tile.Count.text = "×" + UiWidgets.Number(quantity);

            var chunk = definition != null ? Mathf.Max(1, definition.PickupQuantity) : 1;
            if (category == ItemCategory.Throwable || category == ItemCategory.Medical || category == ItemCategory.Boost)
                chunk = 1;
            tile.Chunk = chunk;
        }

        private void UpdateCapacity(InventoryService inventory)
        {
            if (inventory == null)
            {
                if (_shownCapacityKey != -1)
                {
                    _shownCapacityKey = -1;
                    _capacityBar.SetValue(0f, true);
                    _capacityText.text = "-";
                }

                return;
            }

            var weight = inventory.CurrentWeight;
            var capacity = inventory.Capacity;
            var key = Mathf.RoundToInt(weight * 10f) * 10000 + Mathf.RoundToInt(capacity);
            if (key == _shownCapacityKey)
                return;

            _shownCapacityKey = key;
            var fraction = capacity > 0f ? weight / capacity : 1f;
            _capacityBar.SetValue(fraction, true);
            _capacityBar.FillColor = inventory.IsOverweight || fraction >= 0.95f ? UiTheme.Danger : fraction >= 0.75f ? UiTheme.Amber : UiTheme.HealthHigh;
            var text = MapMath.FormatDecimal(weight, 1) + " / " + UiWidgets.Number(Mathf.RoundToInt(capacity));
            _capacityText.text = inventory.IsOverweight ? UiTheme.Colorize(Loc.Format("inv.overweight", "AŞIRI YÜK  {0}", text), UiTheme.Danger) : text;
        }

        private void UpdateVitals(Combatant combatant)
        {
            var health = combatant != null && combatant.Health != null ? Mathf.CeilToInt(combatant.Health.Current) : -1;
            var max = combatant != null && combatant.Health != null ? Mathf.CeilToInt(combatant.Health.Max) : 0;
            var boost = combatant != null && combatant.Boost != null ? Mathf.RoundToInt(combatant.Boost.Value) : 0;
            var key = (health + 1) * 1000000 + max * 1000 + boost;
            if (key == _shownVitalsKey)
                return;

            _shownVitalsKey = key;
            if (health < 0)
            {
                _vitals.text = string.Empty;
                return;
            }

            var fraction = max > 0 ? health / (float)max : 0f;
            _vitals.text = Loc.Format("inv.health", "Sağlık {0}/{1}", UiTheme.Colorize(UiWidgets.Number(health), UiTheme.HealthColor(fraction)), UiWidgets.Number(max))
                           + "     " + Loc.Get("inv.boost", "Takviye") + " " + UiTheme.Colorize(UiWidgets.Number(boost), UiTheme.Boost);
        }

        private void UpdateUseStatus(IPlayerHudSource player)
        {
            var itemUse = player != null ? player.ItemUse : null;
            var using_ = itemUse != null && itemUse.IsUsing;
            SetActive(_usePanel, using_);
            if (!using_)
            {
                _shownUseKey = int.MinValue;
                return;
            }

            _useBar.SetValue(itemUse.Progress, true);
            var key = Mathf.CeilToInt(itemUse.RemainingSeconds * 10f);
            if (key == _shownUseKey)
                return;

            _shownUseKey = key;
            _useText.text = Loc.Format("inv.using", "Kullanılıyor: {0}   {1} sn", itemUse.CurrentItemName, UiTheme.Colorize(MapMath.FormatDecimal(Mathf.Max(0f, itemUse.RemainingSeconds), 1), UiTheme.Amber));
        }

        // ================================================================== Eylemler

        private void EquipWeapon(int slot)
        {
            var inventory = CurrentInventory();
            if (inventory == null || inventory.GetWeapon(slot) == null)
                return;

            if (ForwardIfRemote(InventoryActionRequest.Equip(slot)))
                return;

            var itemUse = Player != null ? Player.ItemUse : null;
            if (itemUse != null && itemUse.IsUsing)
                itemUse.Cancel();

            inventory.ActiveWeapon?.CancelReload();
            if (inventory.SetActiveSlot(slot))
                _nextDynamicRefresh = 0f;
        }

        private void DropWeapon(int slot)
        {
            var inventory = CurrentInventory();
            if (inventory == null || !CanDropNow())
                return;

            var weapon = inventory.GetWeapon(slot);
            if (weapon == null)
                return;

            if (ForwardIfRemote(InventoryActionRequest.DropWeapon(slot)))
                return;

            weapon.CancelReload();
            var loot = inventory.DropWeapon(slot);
            SpawnDropped(loot);
        }

        private void DropGear(ItemCategory category)
        {
            var inventory = CurrentInventory();
            if (inventory == null || !CanDropNow())
                return;

            if (ForwardIfRemote(InventoryActionRequest.DropEquipment(category)))
                return;

            if (inventory.TryDropEquipment(category, out var dropped))
                SpawnDropped(dropped);
        }

        private void DropStackQuantity(string itemId, int quantity)
        {
            var inventory = CurrentInventory();
            if (inventory == null || string.IsNullOrEmpty(itemId) || quantity <= 0 || !CanDropNow())
                return;

            var count = inventory.GetCount(itemId);
            if (count <= 0)
                return;

            quantity = Mathf.Min(quantity, count);
            if (ForwardIfRemote(InventoryActionRequest.DropStack(itemId, quantity)))
                return;

            // Kullanılan eşyanın tamamı bırakılıyorsa kullanım iptal.
            var itemUse = Player != null ? Player.ItemUse : null;
            if (itemUse != null && itemUse.IsUsing && quantity >= count
                && string.Equals(itemUse.CurrentItemId, itemId, StringComparison.Ordinal))
                itemUse.Cancel();

            if (inventory.TryDropStack(itemId, quantity, out var dropped))
                SpawnDropped(dropped);
        }

        private void UseItem(string itemId)
        {
            var player = Player;
            if (player == null || string.IsNullOrEmpty(itemId) || player.IsDead)
                return;

            var itemUse = player.ItemUse;
            if (itemUse == null)
                return;

            if (!itemUse.CanUse(itemId))
            {
                UiWidgets.PlaySound(SoundId.DryFire);
                return;
            }

            if (ForwardIfRemote(InventoryActionRequest.Use(itemId)))
                return;

            if (itemUse.TryBegin(itemId))
                _nextDynamicRefresh = 0f;
            else
                UiWidgets.PlaySound(SoundId.DryFire);
        }

        private void CancelUse()
        {
            var itemUse = Player != null ? Player.ItemUse : null;
            if (itemUse == null || !itemUse.IsUsing)
                return;

            if (ForwardIfRemote(InventoryActionRequest.Cancel()))
                return;

            itemUse.Cancel();
        }

        /// <summary>
        /// Otoritesiz istemcide (dedicated server'a bağlı) eylemi uygulamaz; <see cref="RemoteActionRequested"/> ile ağ
        /// katmanına iletir ve true döner. Otorite (sunucu / çevrimdışı) tarafında false döner — eylem yerelde uygulanır.
        /// </summary>
        private bool ForwardIfRemote(InventoryActionRequest request)
        {
            bool authority;
            try
            {
                authority = GameContext.HasAuthority;
            }
            catch (Exception)
            {
                authority = true;
            }

            if (authority)
                return false;

            var handler = RemoteActionRequested;
            if (handler == null)
            {
                Debug.LogWarning("[Envanter] Otorite yok ve ağ işleyicisi bağlı değil; eylem yok sayıldı: " + request);
                return true;
            }

            try
            {
                handler(request);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            return true;
        }

        /// <summary>
        /// Eşya bırakılabilir mi? Araçta/intikalde ya da ölüyken bırakılamaz (oyuncuya bildirilir).
        /// </summary>
        private bool CanDropNow()
        {
            var player = Player;
            if (player == null || player.IsDead)
                return false;

            if (player.IsInVehicle || player.DropState != DropState.Landed)
            {
                if (player is PlayerController controller && controller != null)
                    controller.Notify(Loc.Get("inv.notify.vehicle_drop", "Araçtayken eşya bırakılamaz"), 2f);
                UiWidgets.PlaySound(SoundId.DryFire);
                return false;
            }

            return true;
        }

        private InventoryService CurrentInventory()
        {
            var player = Player;
            return player != null ? player.Inventory : null;
        }

        private void SpawnDropped(LootItemData item)
        {
            if (!item.IsValid)
                return;

            var player = Player;
            var position = player != null ? player.Position : transform.position;
            if (player != null)
            {
                var direction = MapMath.YawToDirection(player.Yaw);
                position += new Vector3(direction.x, 0f, direction.y) * DropDistance;
            }

            if (!MapMath.IsFinite(position))
                position = Vector3.zero;

            LootPickupComponent spawned = null;
            try
            {
                spawned = LootSpawner.SpawnDropped(item, position);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            if (spawned == null)
                Debug.LogWarning("[Envanter] Bırakılan eşya yere konamadı: " + item.ItemId);
            else
                UiWidgets.PlaySound(SoundId.Pickup);

            _dirty = true;
        }

        // ================================================================== Yardımcılar

        private static void SetActive(Component component, bool active)
        {
            if (component != null && component.gameObject.activeSelf != active)
                component.gameObject.SetActive(active);
        }

        private void OnDisable()
        {
            if (IsOpen)
            {
                IsOpen = false;
                if (_panel != null)
                    _panel.gameObject.SetActive(false);
                MapOverlayInput.Release(this);
            }
        }

        private void OnDestroy()
        {
            MapOverlayInput.Release(this);
            RemoteActionRequested = null;
            if (_inventory != null)
            {
                _inventory.Changed -= OnInventoryChanged;
                _inventory = null;
            }
        }
    }
}
