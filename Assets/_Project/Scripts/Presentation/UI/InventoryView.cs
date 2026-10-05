using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Combat;
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
    public sealed class InventoryView : MonoBehaviour
    {
        private const float BodyWidth = 1280f;
        private const float BodyHeight = 860f;
        private const float HeaderHeight = 56f;
        private const float ColumnTop = 66f;
        private const float ColumnHeight = BodyHeight - ColumnTop - 40f;
        private const float LeftWidth = 620f;
        private const float RightLeft = 640f;
        private const float RightWidth = BodyWidth - RightLeft;
        private const float WeaponCardHeight = 108f;
        private const float GearRowHeight = 70f;
        private const float ItemRowHeight = 54f;
        private const float DynamicRefreshInterval = 0.15f;
        private const float DropDistance = 1.3f;

        private static readonly Color CardColor = UiTheme.WithAlpha(UiTheme.PanelLight, 0.92f);
        private static readonly Color ActiveCardColor = UiTheme.Hex(0x45, 0x40, 0x22, 0xF0);
        private static readonly Color MedicalColor = UiTheme.Hex(0xE0, 0x4B, 0x4B);
        private static readonly Color ThrowableColor = UiTheme.Hex(0x9A, 0xA5, 0x78);

        private sealed class WeaponCard
        {
            public int Slot;
            public Image Background;
            public Text Type;
            public Text Name;
            public Text Info;
            public Text Badge;
            public Button Equip;
            public Button Drop;
            public string ShownWeaponId;
            public int ShownKey = int.MinValue;
            public int ShownBadgeKey = int.MinValue;
        }

        private sealed class GearRow
        {
            public ItemCategory Category;
            public string TypeName;
            public Text Type;
            public Text Name;
            public Text Info;
            public UiProgressBar Bar;
            public Button Drop;
            public string ShownItemId;
            public int ShownKey = int.MinValue;
        }

        private sealed class ItemRow
        {
            public RectTransform Root;
            public Image Strip;
            public Text Name;
            public Text Sub;
            public Text Count;
            public Button Use;
            public Button DropChunk;
            public Text DropChunkLabel;
            public Button DropAll;
            public string ItemId;
            public int Quantity;
            public int Chunk;
            public bool Usable;
            public bool Visible;
        }

        private IPlayerHudSource _player;
        private RectTransform _panel;
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
        private RectTransform _listContent;
        private Text _emptyList;
        private readonly List<ItemRow> _rows = new List<ItemRow>(16);
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
            InvalidateShown();
            Refresh();
        }

        /// <summary>Envanteri kapatır ve önceki imleç/girdi durumunu geri yükler.</summary>
        public void Close()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
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

            var title = UiFactory.Label(header, "ENVANTER", UiTheme.FontMedium, TextAnchor.MiddleLeft, UiTheme.TextHeader, FontStyle.Bold);
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

            _equipmentRoot = UiFactory.VerticalList(column, 8f, UiTheme.Padding);
            _equipmentRoot.gameObject.name = "List";

            UiWidgets.Header(_equipmentRoot, "SİLAHLAR", UiTheme.FontNormal);
            for (var slot = 0; slot < _weapons.Length; slot++)
                _weapons[slot] = CreateWeaponCard(_equipmentRoot, slot);

            UiFactory.Spacer(_equipmentRoot, 4f);
            UiWidgets.Header(_equipmentRoot, "KORUYUCU TEÇHİZAT", UiTheme.FontNormal);
            _vestRow = CreateGearRow(_equipmentRoot, ItemCategory.Armor, "YELEK");
            _helmetRow = CreateGearRow(_equipmentRoot, ItemCategory.Helmet, "KASK");
            _backpackRow = CreateGearRow(_equipmentRoot, ItemCategory.Backpack, "ÇANTA");

            _emptyEquipment = UiFactory.Label(column, "Envanter bilgisi yok", UiTheme.FontNormal, TextAnchor.MiddleCenter, UiTheme.TextMuted, FontStyle.Bold);
            _emptyEquipment.enabled = false;
        }

        private WeaponCard CreateWeaponCard(RectTransform parent, int slot)
        {
            var card = new WeaponCard { Slot = slot };
            var root = UiFactory.Panel(parent, CardColor, UiSprites.ChamferRect);
            root.gameObject.name = "WeaponSlot" + (slot + 1);
            UiFactory.LayoutSize(root, -1f, WeaponCardHeight, 1f);
            card.Background = root.GetComponent<Image>();
            card.Background.raycastTarget = false;

            var keyBox = UiFactory.Panel(root, UiTheme.Hex(0xEC, 0xEB, 0xE0, 0xE6), UiSprites.GetRoundedRect(4));
            keyBox.gameObject.name = "Key";
            keyBox.GetComponent<Image>().raycastTarget = false;
            UiFactory.SetRect(keyBox, 12f, 12f, 34f, 34f);
            var keyText = UiFactory.Label(keyBox, UiWidgets.Number(slot + 1), UiTheme.FontNormal, TextAnchor.MiddleCenter, UiTheme.Background, FontStyle.Bold);
            keyText.horizontalOverflow = HorizontalWrapMode.Overflow;

            card.Type = UiFactory.Label(root, slot == InventoryService.SidearmSlot ? "TABANCA" : "ANA SİLAH", UiTheme.FontTiny, TextAnchor.UpperLeft, UiTheme.TextMuted, FontStyle.Bold);
            card.Type.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(card.Type, 58f, 10f, 300f, 20f);

            card.Name = UiFactory.Label(root, string.Empty, UiTheme.FontMedium, TextAnchor.UpperLeft, UiTheme.Text, FontStyle.Bold);
            card.Name.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(card.Name, 58f, 28f, 330f, 34f);
            UiFactory.AddShadow(card.Name, UiTheme.TextShadow, new Vector2(1f, -1f));

            card.Info = UiFactory.Label(root, string.Empty, UiTheme.FontTiny + 2, TextAnchor.UpperLeft, UiTheme.TextDim);
            card.Info.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(card.Info, 58f, 72f, 400f, 24f);

            card.Badge = UiFactory.Label(root, string.Empty, UiTheme.FontTiny, TextAnchor.UpperRight, UiTheme.Amber, FontStyle.Bold);
            card.Badge.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Anchor(card.Badge, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-128f, -10f), new Vector2(220f, 20f));

            // Sağda dikey düğme sütunu: KUŞAN üstte, BIRAK altta.
            card.Equip = SmallButton(root, "KUŞAN", () => EquipWeapon(card.Slot), UiButtonStyle.Default, 104f, 40f);
            UiFactory.Anchor(card.Equip, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -10f), new Vector2(104f, 40f));

            card.Drop = SmallButton(root, "BIRAK", () => DropWeapon(card.Slot), UiButtonStyle.Danger, 104f, 40f);
            UiFactory.Anchor(card.Drop, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 10f), new Vector2(104f, 40f));
            return card;
        }

        private GearRow CreateGearRow(RectTransform parent, ItemCategory category, string type)
        {
            var row = new GearRow { Category = category };
            var root = UiFactory.Panel(parent, CardColor, UiSprites.ChamferRect);
            root.gameObject.name = "Gear_" + type;
            UiFactory.LayoutSize(root, -1f, GearRowHeight, 1f);
            root.GetComponent<Image>().raycastTarget = false;

            row.Type = UiFactory.Label(root, type, UiTheme.FontTiny, TextAnchor.UpperLeft, UiTheme.TextMuted, FontStyle.Bold);
            row.Type.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(row.Type, 14f, 8f, 240f, 20f);
            row.TypeName = type;

            row.Name = UiFactory.Label(root, string.Empty, UiTheme.FontNormal, TextAnchor.UpperLeft, UiTheme.Text, FontStyle.Bold);
            row.Name.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(row.Name, 14f, 30f, 240f, 30f);

            row.Bar = UiFactory.ProgressBar(root, UiTheme.Armor, UiTheme.Track);
            row.Bar.TrailEnabled = false;
            UiFactory.SetRect(row.Bar, 262f, 22f, 190f, 12f);

            row.Info = UiFactory.Label(root, string.Empty, UiTheme.FontTiny, TextAnchor.UpperLeft, UiTheme.TextDim, FontStyle.Bold);
            row.Info.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(row.Info, 262f, 40f, 200f, 20f);

            row.Drop = SmallButton(root, "BIRAK", () => DropGear(row.Category), UiButtonStyle.Danger, 104f, 38f);
            UiFactory.Anchor(row.Drop, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(104f, 38f));
            return row;
        }

        private void BuildBackpack(RectTransform body)
        {
            var column = UiFactory.Panel(body, UiTheme.Panel, UiSprites.ChamferRect);
            column.gameObject.name = "Backpack";
            UiFactory.SetRect(column, RightLeft, ColumnTop, RightWidth, ColumnHeight);

            var header = UiWidgets.Header(column, "SIRT ÇANTASI", UiTheme.FontNormal);
            UiFactory.SetRect(header.transform.parent as RectTransform, UiTheme.Padding, UiTheme.Padding, RightWidth - UiTheme.Padding * 2f, UiTheme.FontNormal + 18f);

            // Yük / kapasite.
            var capacityLabel = UiFactory.Label(column, "YÜK", UiTheme.FontTiny, TextAnchor.MiddleLeft, UiTheme.TextMuted, FontStyle.Bold);
            UiFactory.SetRect(capacityLabel, UiTheme.Padding, 62f, 60f, 20f);
            _capacityBar = UiFactory.ProgressBar(column, UiTheme.HealthHigh, UiTheme.Track);
            _capacityBar.TrailEnabled = false;
            _capacityBar.SetSegments(4, UiTheme.WithAlpha(UiTheme.Background, 0.6f), 2f);
            UiFactory.SetRect(_capacityBar, UiTheme.Padding + 52f, 66f, RightWidth - UiTheme.Padding * 2f - 52f - 170f, 14f);
            _capacityText = UiFactory.Label(column, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleRight, UiTheme.Text, FontStyle.Bold);
            _capacityText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_capacityText, RightWidth - UiTheme.Padding - 160f, 60f, 160f, 26f);

            // Yığın listesi.
            var listHolder = UiFactory.CreateRect("ListHolder", column);
            UiFactory.Stretch(listHolder, UiTheme.Padding, 96f, UiTheme.Padding, 84f);
            UiWidgets.ScrollList(listHolder, out _listContent, 6f, 0);

            _emptyList = UiFactory.Label(listHolder, "Çanta boş", UiTheme.FontNormal, TextAnchor.MiddleCenter, UiTheme.TextMuted, FontStyle.Bold);
            _emptyList.enabled = false;

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
            UiFactory.SetRect(_useBar, 12f, 38f, RightWidth - UiTheme.Padding * 2f - 24f - 120f, 10f);
            var cancel = SmallButton(_usePanel, "İPTAL", CancelUse, UiButtonStyle.Default, 104f, 38f);
            UiFactory.Anchor(cancel, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(104f, 38f));
            _usePanel.gameObject.SetActive(false);
        }

        private static void BuildFooter(RectTransform body)
        {
            var footer = UiFactory.CreateRect("Footer", body);
            UiFactory.SetRect(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 32f));
            var hint = UiFactory.Label(footer,
                UiTheme.Colorize("[Tab]", UiTheme.Amber) + " / " + UiTheme.Colorize("[Esc]", UiTheme.Amber) + " Kapat     "
                + UiTheme.Colorize("[H]", UiTheme.Amber) + " İyileş     " + UiTheme.Colorize("[J]", UiTheme.Amber) + " Takviye",
                UiTheme.FontSmall, TextAnchor.MiddleCenter, UiTheme.TextDim, FontStyle.Bold);
            hint.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private static Button SmallButton(Transform parent, string label, Action onClick, UiButtonStyle style, float width, float height)
        {
            var button = UiFactory.Button(parent, label, onClick, style);
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

        private ItemRow CreateItemRow(int index)
        {
            var row = new ItemRow();
            var root = UiFactory.Panel(_listContent, CardColor, UiSprites.ChamferRect);
            root.gameObject.name = "Item" + index;
            root.GetComponent<Image>().raycastTarget = false;
            UiFactory.LayoutSize(root, -1f, ItemRowHeight, 1f);
            row.Root = root;

            row.Strip = UiFactory.Image(root, null, UiTheme.Khaki);
            row.Strip.gameObject.name = "Strip";
            UiFactory.SetRect(row.Strip, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 6f), new Vector2(5f, -6f));

            row.Name = UiFactory.Label(root, string.Empty, UiTheme.FontSmall, TextAnchor.UpperLeft, UiTheme.Text, FontStyle.Bold);
            row.Name.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(row.Name, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -30f), new Vector2(-370f, -6f));

            row.Sub = UiFactory.Label(root, string.Empty, UiTheme.FontTiny, TextAnchor.UpperLeft, UiTheme.TextMuted);
            row.Sub.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(row.Sub, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -50f), new Vector2(-370f, -30f));

            row.Count = UiFactory.Label(root, string.Empty, UiTheme.FontNormal, TextAnchor.MiddleRight, UiTheme.Text, FontStyle.Bold);
            row.Count.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Anchor(row.Count, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-296f, 0f), new Vector2(70f, 30f));

            row.DropAll = SmallButton(root, "BIRAK", () => DropStack(row, false), UiButtonStyle.Danger, 88f, 36f);
            UiFactory.Anchor(row.DropAll, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(88f, 36f));

            row.DropChunk = SmallButton(root, "BIRAK", () => DropStack(row, true), UiButtonStyle.Default, 88f, 36f);
            UiFactory.Anchor(row.DropChunk, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-102f, 0f), new Vector2(88f, 36f));
            row.DropChunkLabel = UiFactory.GetButtonLabel(row.DropChunk);
            if (row.DropChunkLabel != null)
                row.DropChunkLabel.fontSize = UiTheme.FontTiny;

            row.Use = SmallButton(root, "KULLAN", () => UseItem(row), UiButtonStyle.Primary, 92f, 36f);
            UiFactory.Anchor(row.Use, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-196f, 0f), new Vector2(92f, 36f));

            root.gameObject.SetActive(false);
            return row;
        }

        // ================================================================== Kare döngüsü

        private void LateUpdate()
        {
            if (!_built || !IsOpen)
                return;

            MapOverlayInput.Maintain();
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
                UpdateVitals(combatant);
                UpdateUseButtons(player);
            }

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
            }

            if (_vestRow != null) { _vestRow.ShownKey = int.MinValue; _vestRow.ShownItemId = null; }
            if (_helmetRow != null) { _helmetRow.ShownKey = int.MinValue; _helmetRow.ShownItemId = null; }
            if (_backpackRow != null) { _backpackRow.ShownKey = int.MinValue; _backpackRow.ShownItemId = null; }
            _shownVitalsKey = int.MinValue;
            _shownCapacityKey = int.MinValue;
            _shownUseKey = int.MinValue;
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
                if (weapon == null)
                {
                    card.Name.text = "— Boş —";
                    card.Name.color = UiTheme.TextMuted;
                    card.Info.text = card.Slot == InventoryService.SidearmSlot ? "Tabanca yuvası" : "Ana silah yuvası";
                }
                else
                {
                    card.Name.text = WeaponName(weapon);
                    card.Name.color = UiTheme.Text;
                }

                SetActive(card.Drop, weapon != null);
            }

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
                card.Info.text = "Şarjör " + UiWidgets.Number(weapon.CurrentAmmo) + "/" + UiWidgets.Number(weapon.MagazineSize)
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
                    card.Badge.text = "ŞARJÖR DEĞİŞİYOR %" + UiWidgets.Number(badgeKey - 1000);
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
                case FireMode.Burst: return "SERİ";
                case FireMode.Auto: return "OTO";
                default: return "TEK";
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
                row.Name.text = row.Category == ItemCategory.Armor ? "Yelek yok" : row.Category == ItemCategory.Helmet ? "Kask yok" : "Çanta yok";
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
            }

            if (piece != null)
            {
                SetActive(row.Bar, true);
                var fraction = piece.DurabilityNormalized;
                row.Bar.SetValue(fraction, true);
                row.Bar.FillColor = piece.IsBroken ? UiTheme.Danger : fraction < 0.3f ? UiTheme.Amber : UiTheme.Armor;
                row.Info.text = piece.IsBroken
                    ? UiTheme.Colorize("KIRIK", UiTheme.Danger)
                    : "Dayanıklılık " + UiWidgets.Number(durability) + "/" + UiWidgets.Number(Mathf.CeilToInt(piece.MaxDurability));
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
            _stacks.Clear();
            if (inventory != null)
                inventory.GetStacks(_stacks);

            for (var i = 0; i < _stacks.Count; i++)
            {
                while (_rows.Count <= i)
                    _rows.Add(CreateItemRow(_rows.Count));

                BindRow(_rows[i], _stacks[i].Key, _stacks[i].Value);
            }

            for (var i = _stacks.Count; i < _rows.Count; i++)
            {
                var row = _rows[i];
                row.ItemId = null;
                row.Quantity = 0;
                if (row.Visible)
                {
                    row.Visible = false;
                    row.Root.gameObject.SetActive(false);
                }
            }

            _emptyList.enabled = _stacks.Count == 0;
            if (_emptyList.enabled)
                _emptyList.text = inventory == null ? "Envanter bilgisi yok" : "Çanta boş";
        }

        private void BindRow(ItemRow row, string itemId, int quantity)
        {
            var sameItem = string.Equals(row.ItemId, itemId, StringComparison.Ordinal);
            if (sameItem && row.Quantity == quantity && row.Visible)
                return;

            row.ItemId = itemId;
            row.Quantity = quantity;
            if (!row.Visible)
            {
                row.Visible = true;
                row.Root.gameObject.SetActive(true);
            }

            ItemCatalog.TryGet(itemId, out var definition);
            var category = definition != null ? definition.Category : ItemCategory.None;

            if (!sameItem)
            {
                row.Name.text = definition != null ? definition.DisplayName : itemId;
                row.Strip.color = CategoryColor(category);
                row.Usable = category == ItemCategory.Medical || category == ItemCategory.Boost;
                SetActive(row.Use, row.Usable);
            }

            var weight = definition != null ? definition.Weight * quantity : 0f;
            row.Sub.text = "Ağırlık " + MapMath.FormatDecimal(weight, 1)
                           + (category == ItemCategory.Medical && definition.HealAmount > 0f
                               ? "  ·  +" + UiWidgets.Number(Mathf.RoundToInt(definition.HealAmount)) + " can"
                               : string.Empty)
                           + (category == ItemCategory.Boost && definition.BoostAmount > 0f
                               ? "  ·  +" + UiWidgets.Number(Mathf.RoundToInt(definition.BoostAmount)) + " takviye"
                               : string.Empty);
            row.Count.text = "×" + UiWidgets.Number(quantity);

            var chunk = definition != null ? Mathf.Max(1, definition.PickupQuantity) : 1;
            if (category == ItemCategory.Throwable || category == ItemCategory.Medical || category == ItemCategory.Boost)
                chunk = 1;
            row.Chunk = chunk;
            var showChunk = quantity > chunk;
            SetActive(row.DropChunk, showChunk);
            if (showChunk && row.DropChunkLabel != null)
                row.DropChunkLabel.text = UiWidgets.Number(chunk) + " BIRAK";
        }

        private static Color CategoryColor(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Ammunition: return UiTheme.Khaki;
                case ItemCategory.Medical: return MedicalColor;
                case ItemCategory.Boost: return UiTheme.Boost;
                case ItemCategory.Throwable: return ThrowableColor;
                default: return UiTheme.TextDim;
            }
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
            _capacityText.text = inventory.IsOverweight ? UiTheme.Colorize("AŞIRI YÜK  " + text, UiTheme.Danger) : text;
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
            _vitals.text = "Sağlık " + UiTheme.Colorize(UiWidgets.Number(health) + "/" + UiWidgets.Number(max), UiTheme.HealthColor(fraction))
                           + "     Takviye " + UiTheme.Colorize(UiWidgets.Number(boost), UiTheme.Boost);
        }

        private void UpdateUseButtons(IPlayerHudSource player)
        {
            var itemUse = player != null ? player.ItemUse : null;
            var alive = player != null && !player.IsDead;
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (!row.Visible || !row.Usable || row.Use == null)
                    continue;

                var canUse = alive && itemUse != null && itemUse.CanUse(row.ItemId)
                             && !string.Equals(itemUse.CurrentItemId, row.ItemId, StringComparison.Ordinal);
                if (row.Use.interactable != canUse)
                    UiWidgets.SetInteractable(row.Use, canUse);
            }
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
            _useText.text = "Kullanılıyor: " + itemUse.CurrentItemName + "   " + UiTheme.Colorize(MapMath.FormatDecimal(Mathf.Max(0f, itemUse.RemainingSeconds), 1) + " sn", UiTheme.Amber);
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

        private void DropStack(ItemRow row, bool chunkOnly)
        {
            var inventory = CurrentInventory();
            if (inventory == null || row == null || string.IsNullOrEmpty(row.ItemId) || !CanDropNow())
                return;

            var itemId = row.ItemId;
            var count = inventory.GetCount(itemId);
            if (count <= 0)
                return;

            var quantity = chunkOnly ? Mathf.Min(count, Mathf.Max(1, row.Chunk)) : count;
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

        private void UseItem(ItemRow row)
        {
            var player = Player;
            if (player == null || row == null || string.IsNullOrEmpty(row.ItemId) || player.IsDead)
                return;

            var itemUse = player.ItemUse;
            if (itemUse == null)
                return;

            if (!itemUse.CanUse(row.ItemId))
            {
                UiWidgets.PlaySound(SoundId.DryFire);
                return;
            }

            if (ForwardIfRemote(InventoryActionRequest.Use(row.ItemId)))
                return;

            if (itemUse.TryBegin(row.ItemId))
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
                    controller.Notify("Araçtayken eşya bırakılamaz", 2f);
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
