using System;
using Project.Application.Services;
using Project.Core.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Alt orta: can çubuğu (hasar izi, renk can oranına göre), üstünde takviye (boost) çubuğu (5 bölmeli) ve solunda
    /// kask / yelek / çanta kutucukları (seviye + dayanıklılık). Değerler değişmedikçe arayüze dokunmaz.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudVitalsView : MonoBehaviour
    {
        public const float Width = 640f;
        public const float Height = 74f;
        public const float BottomMargin = 26f;

        private const float ChipSize = 50f;
        private const float ChipSpacing = 6f;
        private const float BarHeight = 22f;
        private const float BoostHeight = 7f;

        private sealed class Chip
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Background;
            public Text Level;
            public Image Durability;
            public int ShownLevel = int.MinValue;
            public float ShownDurability = -1f;
        }

        private HudContext _ctx;
        private UiProgressBar _health;
        private UiProgressBar _boost;
        private CanvasGroup _boostGroup;
        private Text _healthLabel;
        private Chip _helmet;
        private Chip _vest;
        private Chip _backpack;

        private int _shownHealth = int.MinValue;
        private int _shownHealthColorBucket = -1;
        private float _shownBoost = -1f;
        private float _boostAlpha;

        public RectTransform Root { get; private set; }

        public static HudVitalsView Create(RectTransform parent, HudContext context)
        {
            var root = HudBuild.Rect("Vitals", parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, BottomMargin), new Vector2(Width, Height));
            var view = root.gameObject.AddComponent<HudVitalsView>();
            view._ctx = context;
            view.Root = root;
            view.Build();
            return view;
        }

        private void Build()
        {
            HudBuild.PassiveGroup(Root);

            // Kutucuklar (sol): kask, yelek, çanta.
            var x = 0f;
            _helmet = CreateChip("KASK", x);
            x += ChipSize + ChipSpacing;
            _vest = CreateChip("YELEK", x);
            x += ChipSize + ChipSpacing;
            _backpack = CreateChip("ÇANTA", x);
            x += ChipSize + ChipSpacing + 4f;

            var barWidth = Width - x;

            // Can çubuğu çerçevesi.
            var frame = HudBuild.Image("HealthFrame", Root, UiSprites.White, UiTheme.WithAlpha(Color.black, 0.55f),
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x - 2f, -2f), new Vector2(barWidth + 4f, BarHeight + 4f));
            frame.transform.SetAsFirstSibling();

            _health = UiFactory.ProgressBar(Root, UiTheme.HealthHigh, UiTheme.WithAlpha(UiTheme.Track, 0.85f));
            _health.gameObject.name = "Health";
            UiFactory.Anchor(_health, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, 0f), new Vector2(barWidth, BarHeight));
            _health.TrailDelay = 0.4f;
            _health.TrailSpeed = 0.6f;
            _health.SetValue(1f, true);

            _healthLabel = HudBuild.Text("HealthValue", _health.transform, string.Empty, UiTheme.FontTiny, TextAnchor.MiddleRight,
                UiTheme.WithAlpha(Color.black, 0.85f), FontStyle.Bold, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-6f, 0f), new Vector2(60f, BarHeight), false);

            // Takviye çubuğu (can çubuğunun üstünde, ince, 5 bölme: %40/%60/%80 eşiklerini gösterir).
            _boost = UiFactory.ProgressBar(Root, UiTheme.Boost, UiTheme.WithAlpha(UiTheme.Track, 0.7f));
            _boost.gameObject.name = "Boost";
            UiFactory.Anchor(_boost, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, BarHeight + 6f), new Vector2(barWidth, BoostHeight));
            _boost.TrailEnabled = false;
            _boost.SetSegments(5, UiTheme.WithAlpha(Color.black, 0.8f), 2f);
            _boost.SetValue(0f, true);
            _boostGroup = HudBuild.PassiveGroup(_boost, 0f);

            var boostLabel = HudBuild.Text("BoostLabel", _boost.transform, "TAKVİYE", 12, TextAnchor.LowerLeft,
                UiTheme.WithAlpha(UiTheme.Boost, 0.9f), FontStyle.Bold, new Vector2(0f, 1f), new Vector2(0f, 0f),
                new Vector2(0f, 2f), new Vector2(120f, 16f));
            boostLabel.gameObject.name = "BoostLabel";
        }

        private Chip CreateChip(string title, float x)
        {
            var chip = new Chip();
            chip.Root = HudBuild.Rect(title, Root, Vector2.zero, Vector2.zero, new Vector2(x, 0f), new Vector2(ChipSize, ChipSize));
            chip.Group = HudBuild.PassiveGroup(chip.Root, 0.4f);

            chip.Background = HudBuild.FillImage("Bg", chip.Root, UiSprites.ChamferRect, UiTheme.WithAlpha(UiTheme.PanelDark, 0.85f));
            var border = HudBuild.FillImage("Border", chip.Root, UiSprites.RoundedRectOutline, UiTheme.WithAlpha(UiTheme.PanelBorder, 0.9f));
            border.type = UiSprites.HasBorder(UiSprites.RoundedRectOutline) ? Image.Type.Sliced : Image.Type.Simple;

            HudBuild.Text("Title", chip.Root, title, 11, TextAnchor.UpperCenter, UiTheme.TextDim, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(ChipSize, 14f), false);

            chip.Level = HudBuild.Text("Level", chip.Root, HudFormat.Dash, UiTheme.FontMedium, TextAnchor.MiddleCenter, UiTheme.Text,
                FontStyle.Bold, HudBuild.Center, HudBuild.Center, new Vector2(0f, -3f), new Vector2(ChipSize, 30f));

            var track = HudBuild.Image("DurabilityTrack", chip.Root, UiSprites.White, UiTheme.WithAlpha(Color.black, 0.6f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(ChipSize - 10f, 4f));
            chip.Durability = HudBuild.Image("Durability", track.transform, UiSprites.White, UiTheme.Armor,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            UiFactory.Stretch(chip.Durability);
            chip.Durability.type = Image.Type.Filled;
            chip.Durability.fillMethod = Image.FillMethod.Horizontal;
            chip.Durability.fillOrigin = (int)Image.OriginHorizontal.Left;
            chip.Durability.fillAmount = 0f;
            return chip;
        }

        /// <summary>HUD denetleyicisi her karede çağırır.</summary>
        public void Tick(float deltaTime)
        {
            var local = _ctx.Local;
            if (local == null || !local.IsInitialized)
                return;

            // Can.
            var state = local.State;
            var max = state.Max > 0f ? state.Max : 100f;
            var current = Mathf.Max(0f, state.Current);
            var fraction = Mathf.Clamp01(current / max);
            var shown = Mathf.CeilToInt(current);
            if (shown != _shownHealth)
            {
                _shownHealth = shown;
                _health.SetValue(fraction, false);
                UiFactory.SetText(_healthLabel, current > 0f ? UiWidgets.Number(shown) : string.Empty);
            }

            var bucket = Mathf.Clamp(Mathf.FloorToInt(fraction * 50f), 0, 50);
            if (bucket != _shownHealthColorBucket)
            {
                _shownHealthColorBucket = bucket;
                _health.FillColor = UiTheme.HealthColor(fraction);
            }

            // Takviye.
            var boost = local.Boost;
            var boostValue = boost != null ? boost.Normalized : 0f;
            if (Mathf.Abs(boostValue - _shownBoost) > 0.001f)
            {
                _shownBoost = boostValue;
                _boost.SetValue(boostValue, true);
            }

            var targetAlpha = boostValue > 0.001f ? 1f : 0f;
            _boostAlpha = Mathf.MoveTowards(_boostAlpha, targetAlpha, deltaTime * 4f);
            HudBuild.SetAlpha(_boostGroup, _boostAlpha);

            // Zırh.
            var inventory = local.Inventory;
            UpdateArmorChip(_helmet, inventory != null ? inventory.Helmet : null);
            UpdateArmorChip(_vest, inventory != null ? inventory.Vest : null);
            UpdateBackpackChip(inventory);
        }

        private static void UpdateArmorChip(Chip chip, ArmorPiece piece)
        {
            var level = piece != null ? Mathf.Clamp(piece.Level, 0, 9) : 0;
            var durability = piece != null ? Mathf.Clamp01(piece.DurabilityNormalized) : 0f;

            if (level != chip.ShownLevel)
            {
                chip.ShownLevel = level;
                UiFactory.SetText(chip.Level, level > 0 ? UiWidgets.Number(level) : HudFormat.Dash);
                chip.Group.alpha = level > 0 ? 1f : 0.4f;
                chip.ShownDurability = -1f;
            }

            if (Math.Abs(durability - chip.ShownDurability) < 0.004f)
                return;

            chip.ShownDurability = durability;
            HudBuild.SetFill(chip.Durability, durability);
            var color = durability > 0.5f ? UiTheme.Armor : durability > 0.25f ? UiTheme.Amber : UiTheme.HealthLow;
            UiFactory.SetColor(chip.Durability, color);
            UiFactory.SetColor(chip.Level, level > 0 && piece != null && piece.IsBroken ? UiTheme.HealthLow : UiTheme.Text);
        }

        private void UpdateBackpackChip(InventoryService inventory)
        {
            var level = inventory != null ? Mathf.Clamp(inventory.BackpackLevel, 0, 9) : 0;
            var load = inventory != null ? Mathf.Clamp01(inventory.LoadFraction) : 0f;
            var chip = _backpack;

            if (level != chip.ShownLevel)
            {
                chip.ShownLevel = level;
                UiFactory.SetText(chip.Level, level > 0 ? UiWidgets.Number(level) : HudFormat.Dash);
                chip.Group.alpha = level > 0 ? 1f : 0.4f;
                chip.ShownDurability = -1f;
            }

            // Çanta çubuğu doluluğu (yük) gösterir.
            if (Math.Abs(load - chip.ShownDurability) < 0.004f)
                return;

            chip.ShownDurability = load;
            HudBuild.SetFill(chip.Durability, load);
            UiFactory.SetColor(chip.Durability, load >= 0.95f ? UiTheme.HealthLow : load > 0.75f ? UiTheme.Amber : UiTheme.Khaki);
        }
    }
}
