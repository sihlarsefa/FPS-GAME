using System;
using Project.Application.Catalogs;
using Project.Application.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Silah bilgisi: alt ortada etkin silahın adı, şarjör / yedek mermi (sınırsızsa "∞"), ateş modu (TEK/SERİ/OTO);
    /// sağ altta (mini haritanın solunda) 3 silah yuvası ve el bombası / sis / tedavi / takviye sayıları.
    /// Sayılar önbellekli metinlerle yazılır (atış sırasında çöp üretmez).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudWeaponView : MonoBehaviour
    {
        private string _tagG = "", _tagT = "", _tagH = "", _tagJ = "";
        private const float AmmoWidth = 460f;
        private const float AmmoHeight = 76f;
        private const float SlotWidth = 300f;
        private const float SlotHeight = 40f;
        private const float SlotSpacing = 5f;
        private const float MinimapReserve = 24f + 268f + 18f;

        private sealed class Slot
        {
            public RectTransform Root;
            public Image Background;
            public Image Accent;
            public Text Key;
            public Text Name;
            public Text Magazine;
            public Text Reserve;
            public WeaponRuntimeService Weapon;
            public bool Active;
            public bool Initialized;
            public int ShownMagazine = int.MinValue;
            public int ShownReserve = int.MinValue;
        }

        private HudContext _ctx;
        private RectTransform _ammoRoot;
        private CanvasGroup _ammoGroup;
        private Text _weaponName;
        private Text _magazine;
        private Text _reserve;
        private Image _fireModeBox;
        private Text _fireMode;
        private Text _reloadHint;
        private Text _magCheck;
        private float _lastActivity;
        private float _magCheckUntil;
        private RectTransform _slotsRoot;
        private readonly Slot[] _slots = new Slot[InventoryService.WeaponSlotCount];
        private Text _throwables;
        private Text _consumables;

        private WeaponRuntimeService _shownWeapon;
        private bool _hasShownWeapon;
        private int _shownMagazine = int.MinValue;
        private int _shownReserve = int.MinValue;
        private bool _shownInfinite;
        private int _shownFireMode = -1;
        private bool _shownReloading;
        private int _shownActiveSlot = int.MinValue;
        private InventoryService _inventory;
        private bool _inventoryDirty = true;
        private float _nextConsumableRefresh;
        private int _frag = -1;
        private int _smoke = -1;
        private int _heal = -1;
        private int _boostItems = -1;
        private readonly Action _onInventoryChanged;

        public HudWeaponView()
        {
            _onInventoryChanged = OnInventoryChanged;
        }

        public RectTransform Root { get; private set; }

        public static HudWeaponView Create(RectTransform parent, HudContext context)
        {
            var root = HudBuild.Fill("Weapons", parent);
            var view = root.gameObject.AddComponent<HudWeaponView>();
            view._ctx = context;
            view.Root = root;
            view.Build();
            return view;
        }

        private void Build()
        {
            HudBuild.PassiveGroup(Root);

            // ---------------------------------------------------------- alt orta: mermi
            var ammoY = HudVitalsView.BottomMargin + HudVitalsView.Height + 8f;
            _ammoRoot = HudBuild.Rect("Ammo", Root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, ammoY),
                new Vector2(AmmoWidth, AmmoHeight));
            _ammoGroup = HudBuild.PassiveGroup(_ammoRoot);

            _weaponName = HudBuild.Text("WeaponName", _ammoRoot, string.Empty, UiTheme.FontSmall, TextAnchor.LowerCenter,
                UiTheme.TextDim, FontStyle.Bold, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f),
                new Vector2(AmmoWidth, 22f));

            _magazine = HudBuild.Text("Magazine", _ammoRoot, string.Empty, UiTheme.FontHudNumber, TextAnchor.MiddleRight,
                UiTheme.Text, FontStyle.Bold, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-6f, 0f),
                new Vector2(170f, 52f));

            _reserve = HudBuild.Text("Reserve", _ammoRoot, string.Empty, UiTheme.FontMedium, TextAnchor.MiddleLeft,
                UiTheme.TextDim, FontStyle.Bold, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(18f, 2f),
                new Vector2(110f, 44f));

            var slash = HudBuild.Text("Slash", _ammoRoot, "/", UiTheme.FontMedium, TextAnchor.MiddleCenter, UiTheme.TextMuted,
                FontStyle.Bold, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(6f, 4f), new Vector2(20f, 44f));
            slash.gameObject.name = "Slash";

            _fireModeBox = HudBuild.Image("FireModeBox", _ammoRoot, UiSprites.ChamferRect, UiTheme.WithAlpha(UiTheme.PanelDark, 0.85f),
                new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(136f, 12f), new Vector2(64f, 26f));
            _fireMode = HudBuild.Text("FireMode", _fireModeBox.transform, string.Empty, UiTheme.FontTiny, TextAnchor.MiddleCenter,
                UiTheme.Amber, FontStyle.Bold, HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(64f, 26f), false);

            _reloadHint = HudBuild.Text("Reload", _ammoRoot, Project.Infrastructure.Input.InputBindings.Bracket(BindAction.Reload) + " ŞARJÖR DEĞİŞTİR", UiTheme.FontTiny, TextAnchor.MiddleLeft,
                UiTheme.Amber, FontStyle.Bold, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(136f, 42f), new Vector2(200f, 20f));
            HudBuild.SetActive(_reloadHint, false);

            _magCheck = HudBuild.Text("MagCheck", _ammoRoot, string.Empty, UiTheme.FontTiny, TextAnchor.MiddleCenter, UiTheme.Amber,
                FontStyle.Bold, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(AmmoWidth, 20f));
            HudBuild.SetActive(_magCheck, false);

            // ---------------------------------------------------------- sağ alt: yuvalar
            var slotsHeight = InventoryService.WeaponSlotCount * (SlotHeight + SlotSpacing) + 44f;
            _slotsRoot = HudBuild.Rect("Slots", Root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-MinimapReserve, 24f),
                new Vector2(SlotWidth, slotsHeight));

            for (var i = 0; i < _slots.Length; i++)
                _slots[i] = CreateSlot(i, slotsHeight);

            _throwables = HudBuild.Text("Throwables", _slotsRoot, string.Empty, UiTheme.FontTiny, TextAnchor.LowerRight,
                UiTheme.TextDim, FontStyle.Bold, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-4f, 20f),
                new Vector2(SlotWidth, 18f));
            _consumables = HudBuild.Text("Consumables", _slotsRoot, string.Empty, UiTheme.FontTiny, TextAnchor.LowerRight,
                UiTheme.TextDim, FontStyle.Bold, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-4f, 0f),
                new Vector2(SlotWidth, 18f));
        }

        private Slot CreateSlot(int index, float totalHeight)
        {
            var slot = new Slot();
            var y = -(index * (SlotHeight + SlotSpacing));
            slot.Root = HudBuild.Rect("Slot" + (index + 1), _slotsRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y),
                new Vector2(SlotWidth, SlotHeight));

            slot.Background = HudBuild.FillImage("Bg", slot.Root, UiSprites.ChamferRect, UiTheme.WithAlpha(UiTheme.PanelDark, 0.6f));
            slot.Accent = HudBuild.Image("Accent", slot.Root, UiSprites.White, UiTheme.Accent, new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), Vector2.zero, new Vector2(UiTheme.AccentStripWidth, SlotHeight - 8f));
            slot.Accent.enabled = false;

            slot.Key = HudBuild.Text("Key", slot.Root, UiWidgets.Number(index + 1), UiTheme.FontSmall, TextAnchor.MiddleCenter,
                UiTheme.TextMuted, FontStyle.Bold, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f),
                new Vector2(22f, SlotHeight));

            slot.Name = HudBuild.Text("Name", slot.Root, "BOŞ", UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.TextMuted,
                FontStyle.Bold, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(150f, SlotHeight));

            slot.Reserve = HudBuild.Text("Reserve", slot.Root, string.Empty, UiTheme.FontTiny, TextAnchor.MiddleLeft,
                UiTheme.TextMuted, FontStyle.Normal, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-6f, -1f),
                new Vector2(44f, SlotHeight));
            slot.Magazine = HudBuild.Text("Magazine", slot.Root, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleRight,
                UiTheme.Text, FontStyle.Bold, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-54f, 0f),
                new Vector2(50f, SlotHeight));
            return slot;
        }

        private void OnInventoryChanged()
        {
            _inventoryDirty = true;
        }

        private void BindInventory(InventoryService inventory)
        {
            if (ReferenceEquals(inventory, _inventory))
                return;

            if (_inventory != null)
                _inventory.Changed -= _onInventoryChanged;

            _inventory = inventory;
            if (_inventory != null)
                _inventory.Changed += _onInventoryChanged;

            _inventoryDirty = true;
            for (var i = 0; i < _slots.Length; i++)
                _slots[i].Initialized = false;
        }

        private void OnDestroy()
        {
            if (_inventory != null)
                _inventory.Changed -= _onInventoryChanged;
            _inventory = null;
        }

        /// <summary>HUD denetleyicisi her karede çağırır.</summary>
        public void Tick(float deltaTime)
        {
            InventoryService inventory = null;
            WeaponRuntimeService active = null;
            if (_ctx.PlayerValid)
            {
                try
                {
                    inventory = _ctx.Player.Inventory;
                    active = _ctx.Player.ActiveWeapon;
                }
                catch (Exception)
                {
                    inventory = null;
                    active = null;
                }
            }

            if (inventory == null && _ctx.Local != null)
                inventory = _ctx.Local.Inventory;
            if (active == null && inventory != null)
                active = inventory.ActiveWeapon;

            BindInventory(inventory);
            UpdateAmmo(active);
            UpdateSlots(inventory, active);

            if (_inventoryDirty || Time.unscaledTime >= _nextConsumableRefresh)
            {
                _inventoryDirty = false;
                _nextConsumableRefresh = Time.unscaledTime + 0.5f;
                UpdateConsumables(inventory);
            }
        }

        private void UpdateAmmo(WeaponRuntimeService weapon)
        {
            if (!_hasShownWeapon || !ReferenceEquals(weapon, _shownWeapon))
            {
                _lastActivity = Time.unscaledTime;
                _hasShownWeapon = true;
                _shownWeapon = weapon;
                _shownMagazine = int.MinValue;
                _shownReserve = int.MinValue;
                _shownFireMode = -1;
                _shownReloading = !(weapon != null && weapon.IsReloading);
                UiFactory.SetText(_weaponName, weapon != null ? SafeName(weapon) : "YUMRUK");
            }

            if (weapon == null)
            {
                UiFactory.SetText(_magazine, HudFormat.Dash);
                UiFactory.SetText(_reserve, HudFormat.Dash);
                UiFactory.SetColor(_magazine, UiTheme.TextMuted);
                HudBuild.SetActive(_fireModeBox, false);
                HudBuild.SetActive(_reloadHint, false);
                _ammoGroup.alpha = 0.75f * HudRules.AmmoAlpha(Time.unscaledTime - _lastActivity);
                return;
            }

            HudBuild.SetActive(_fireModeBox, true);
            UpdateIdleFadeAndMagCheck(weapon);

            var magazine = Mathf.Max(0, weapon.CurrentAmmo);
            var infinite = weapon.HasInfiniteReserve || weapon.ReserveAmmo < 0;
            var reserve = infinite ? -1 : Mathf.Max(0, weapon.ReserveAmmo);
            var reloading = weapon.IsReloading;

            if (magazine != _shownMagazine || reloading != _shownReloading)
            {
                _lastActivity = Time.unscaledTime;
                _shownMagazine = magazine;
                _shownReloading = reloading;
                UiFactory.SetText(_magazine, UiWidgets.Number(magazine));

                var size = weapon.MagazineSize;
                Color color;
                if (reloading)
                    color = UiTheme.TextMuted;
                else if (magazine <= 0)
                    color = UiTheme.HealthLow;
                else if (size > 0 && magazine <= Mathf.Max(1, size / 4))
                    color = UiTheme.Amber;
                else
                    color = UiTheme.Text;
                UiFactory.SetColor(_magazine, color);
            }

            if (reserve != _shownReserve || infinite != _shownInfinite)
            {
                _shownReserve = reserve;
                _shownInfinite = infinite;
                UiFactory.SetText(_reserve, infinite ? HudFormat.Infinity : UiWidgets.Number(reserve));
                UiFactory.SetColor(_reserve, !infinite && reserve <= 0 ? UiTheme.HealthLow : UiTheme.TextDim);
            }

            var mode = (int)weapon.CurrentFireMode;
            if (mode != _shownFireMode)
            {
                _lastActivity = Time.unscaledTime;
                _shownFireMode = mode;
                UiFactory.SetText(_fireMode, HudFormat.FireMode(weapon.CurrentFireMode));
                var modes = weapon.FireModes;
                _fireModeBox.color = UiTheme.WithAlpha(modes != null && modes.Length > 1 ? UiTheme.PanelLight : UiTheme.PanelDark, 0.85f);
            }

            var showHint = !reloading && magazine <= 0 && (infinite || reserve > 0);
            if (showHint)
            {
                var label = Project.Infrastructure.Input.InputBindings.Bracket(BindAction.Reload) + " ŞARJÖR DEĞİŞTİR";
                if (_reloadHint.text != label)
                    _reloadHint.text = label;
            }
            HudBuild.SetActive(_reloadHint, showHint);
        }

        /// <summary>Boştayken sayaç solar; şarjör kontrol tuşu (Inspect) yaklaşık doluluğu gösterir.</summary>
        private void UpdateIdleFadeAndMagCheck(WeaponRuntimeService weapon)
        {
            var now = Time.unscaledTime;
            var aiming = false;
            try
            {
                aiming = _ctx.Player != null && _ctx.Player.IsAiming;
            }
            catch (Exception)
            {
                aiming = false;
            }

            if (aiming || weapon.IsReloading)
                _lastActivity = now;

            bool check;
            try
            {
                check = Project.Infrastructure.Input.InputBindings.Pressed(BindAction.Inspect);
            }
            catch (Exception)
            {
                check = false;
            }

            if (check)
            {
                _magCheckUntil = now + HudRules.MagCheckSeconds;
                _lastActivity = now;
            }

            var showCheck = now < _magCheckUntil;
            if (showCheck)
            {
                var label = "ŞARJÖR: " + HudRules.MagCheckLabel(Mathf.Max(0, weapon.CurrentAmmo), weapon.MagazineSize);
                if (_magCheck.text != label)
                    _magCheck.text = label;
            }

            HudBuild.SetActive(_magCheck, showCheck);
            _ammoGroup.alpha = HudRules.AmmoAlpha(now - _lastActivity);
        }

        private void UpdateSlots(InventoryService inventory, WeaponRuntimeService active)
        {
            var activeSlot = inventory != null ? inventory.ActiveSlot : -1;
            var activeChanged = activeSlot != _shownActiveSlot;
            _shownActiveSlot = activeSlot;

            for (var i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                var weapon = inventory != null ? inventory.GetWeapon(i) : null;
                var isActive = weapon != null && (i == activeSlot || ReferenceEquals(weapon, active));

                if (!slot.Initialized || !ReferenceEquals(weapon, slot.Weapon) || activeChanged || isActive != slot.Active)
                {
                    slot.Initialized = true;
                    slot.Weapon = weapon;
                    slot.Active = isActive;
                    slot.ShownMagazine = int.MinValue;
                    slot.ShownReserve = int.MinValue;

                    UiFactory.SetText(slot.Name, weapon != null ? SafeName(weapon) : "BOŞ");
                    UiFactory.SetColor(slot.Name, weapon == null ? UiTheme.TextMuted : isActive ? UiTheme.Text : UiTheme.TextDim);
                    UiFactory.SetColor(slot.Key, isActive ? UiTheme.Amber : UiTheme.TextMuted);
                    slot.Background.color = isActive
                        ? UiTheme.WithAlpha(UiTheme.PanelLight, 0.85f)
                        : UiTheme.WithAlpha(UiTheme.PanelDark, weapon != null ? 0.6f : 0.35f);
                    slot.Accent.enabled = isActive;

                    if (weapon == null)
                    {
                        UiFactory.SetText(slot.Magazine, string.Empty);
                        UiFactory.SetText(slot.Reserve, string.Empty);
                    }
                }

                if (weapon == null)
                    continue;

                var magazine = Mathf.Max(0, weapon.CurrentAmmo);
                if (magazine != slot.ShownMagazine)
                {
                    slot.ShownMagazine = magazine;
                    UiFactory.SetText(slot.Magazine, UiWidgets.Number(magazine));
                    UiFactory.SetColor(slot.Magazine, magazine <= 0 ? UiTheme.HealthLow : isActive ? UiTheme.Text : UiTheme.TextDim);
                }

                var infinite = weapon.HasInfiniteReserve;
                var reserve = infinite ? -1 : Mathf.Max(0, weapon.ReserveAmmo);
                if (reserve != slot.ShownReserve)
                {
                    slot.ShownReserve = reserve;
                    UiFactory.SetText(slot.Reserve, infinite ? "/ " + HudFormat.Infinity : "/ " + UiWidgets.Number(reserve));
                }
            }
        }

        private void UpdateConsumables(InventoryService inventory)
        {
            var frag = inventory != null ? inventory.GetCount(ItemIds.FragGrenade) : 0;
            var smoke = inventory != null ? inventory.GetCount(ItemIds.SmokeGrenade) : 0;
            var heal = inventory != null
                ? inventory.GetCount(ItemIds.Bandage) + inventory.GetCount(ItemIds.FirstAid) + inventory.GetCount(ItemIds.MedKit)
                : 0;
            var boost = inventory != null
                ? inventory.GetCount(ItemIds.EnergyDrink) + inventory.GetCount(ItemIds.Painkiller)
                : 0;

            var tagG = Project.Infrastructure.Input.InputBindings.Bracket(BindAction.Grenade);
            var tagT = Project.Infrastructure.Input.InputBindings.Bracket(BindAction.Smoke);
            var tagH = Project.Infrastructure.Input.InputBindings.Bracket(BindAction.Heal);
            var tagJ = Project.Infrastructure.Input.InputBindings.Bracket(BindAction.Boost);
            var tagsChanged = tagG != _tagG || tagT != _tagT || tagH != _tagH || tagJ != _tagJ;
            _tagG = tagG; _tagT = tagT; _tagH = tagH; _tagJ = tagJ;

            if (frag != _frag || smoke != _smoke || tagsChanged)
            {
                _frag = frag;
                _smoke = smoke;
                UiFactory.SetText(_throwables, _tagG + " El Bombası " + UiWidgets.Number(frag) + "    " + _tagT + " Sis " + UiWidgets.Number(smoke));
                UiFactory.SetColor(_throwables, frag + smoke > 0 ? UiTheme.TextDim : UiTheme.TextMuted);
            }

            if (heal != _heal || boost != _boostItems || tagsChanged)
            {
                _heal = heal;
                _boostItems = boost;
                UiFactory.SetText(_consumables, _tagH + " Tedavi " + UiWidgets.Number(heal) + "    " + _tagJ + " Takviye " + UiWidgets.Number(boost));
                UiFactory.SetColor(_consumables, heal + boost > 0 ? UiTheme.TextDim : UiTheme.TextMuted);
            }
        }

        private static string SafeName(WeaponRuntimeService weapon)
        {
            var definition = weapon.Definition;
            if (definition == null)
                return "Silah";

            if (!string.IsNullOrEmpty(definition.DisplayName))
                return definition.DisplayName;

            return HudContext.WeaponName(definition.WeaponId, false);
        }
    }
}
