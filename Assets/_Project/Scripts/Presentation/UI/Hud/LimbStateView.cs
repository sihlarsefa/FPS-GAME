using System;
using Project.Application.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Uzuv göstergesi saf kuralları (Unity'siz sayılar + Türkçe açıklamalar). Sayılar <see cref="LimbDamageRules"/>'tan türer, böylece
    /// oyun kuralı değişince HUD metni kendiliğinden uyar. Açıklamalar HUD'da kısa alt yazı (<c>Caption</c>) olarak, ayrıntılı cümleler
    /// (<c>Tooltip</c>) fare/odak ipucu isteyen ekranlar için sunulur.
    /// </summary>
    public static class LimbStateRules
    {
        /// <summary>Bacak yarası ne kadar hız keser (0..1; ör. 0,25).</summary>
        public static float LegSlowFraction(int wounds) => 1f - LimbDamageRules.MoveSpeedMultiplier(wounds);

        /// <summary>Kol yarası ek sarsıntı oranı (ör. 0,30 / 0,60).</summary>
        public static float ArmSwayBonus(int wounds) => LimbDamageRules.SwayMultiplier(wounds) - 1f;

        /// <summary>Kol yarası ADS süresi uzaması oranı.</summary>
        public static float ArmAdsBonus(int wounds) => LimbDamageRules.AdsTimeMultiplier(wounds) - 1f;

        public static int Percent(float fraction) => (int)Math.Round(fraction * 100f);

        public static string LegCaption(int wounds) => "-%" + Percent(LegSlowFraction(wounds)) + " HIZ";

        public static string ArmCaption(int wounds) => "+%" + Percent(ArmSwayBonus(wounds)) + " SARSINTI";

        public static string BleedCaption(float remainingSeconds) =>
            "-" + (int)LimbDamageRules.BleedTickDamage + " CAN/" + (int)LimbDamageRules.BleedTickSeconds + "sn · " +
            Math.Max(1, (int)Math.Ceiling(remainingSeconds)) + "sn";

        public static string LegTooltip(int wounds) =>
            "Bacak yarası ×" + wounds + ": hareket hızı -%" + Percent(LegSlowFraction(wounds)) + " (en çok -%" +
            Percent(LimbDamageRules.LegSlowCap) + "). Sargı Bezi ile tedavi et.";

        public static string ArmTooltip(int wounds) =>
            "Kol yarası ×" + wounds + ": nişan sarsıntısı +%" + Percent(ArmSwayBonus(wounds)) + ", nişan alma (ADS) +%" +
            Percent(ArmAdsBonus(wounds)) + " yavaş. Sargı Bezi ile tedavi et.";

        public static string BleedTooltip(float remainingSeconds) =>
            "Kanama: " + (int)LimbDamageRules.BleedTickSeconds + " sn'de " + (int)LimbDamageRules.BleedTickDamage + " can, kalan " +
            Math.Max(1, (int)Math.Ceiling(remainingSeconds)) + " sn. Sargı Bezi ile durdur.";

        /// <summary>Bir yara sayısının vurgu rengi anahtarı: 0 yok, 1 uyarı (amber), 2+ ağır (kırmızı).</summary>
        public static int Severity(int wounds) => wounds <= 0 ? 0 : wounds == 1 ? 1 : 2;

        /// <summary>Kanama damlasının nabzı (0,55..1): damla "atar".</summary>
        public static float BleedPulse(float time) => 0.775f + 0.225f * (float)Math.Sin(time * 5.2f);

        /// <summary>Damla silüeti: alt yuvarlak + yukarı sivrilen koni. u,v 0..1 (v = alttan).</summary>
        public static bool InsideDrop(float u, float v)
        {
            const float cy = 0.34f, r = 0.27f, apex = 0.94f;
            var dx = u - 0.5f;
            if (dx * dx + (v - cy) * (v - cy) <= r * r)
                return true;
            if (v < cy || v > apex)
                return false;
            return Math.Abs(dx) <= r * (1f - (v - cy) / (apex - cy));
        }
    }

    /// <summary>
    /// HUD uzuv göstergesi: vitals çubuğunun solunda, yalnız yara/kanama varken beliren üç küçük kutu — BACAK (yara sayısı + hız kaybı),
    /// KOL (sarsıntı artışı) ve KANAMA (nabız gibi atan damla + kalan süre). Altlarında kısa Türkçe açıklama. Kaynak:
    /// <c>Combatant.Limbs</c> (sunucu otoriteli LimbDamageState). Kendi kendini kurar ve kendi Update'iyle çalışır; HUD kökünün
    /// ölçek/opaklığını izler. ENTEGRASYON HudController: <c>Make("LimbStateView", () =&gt; LimbStateView.Create(_playerLayer, _ctx))</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LimbStateView : MonoBehaviour
    {
        private const float ChipWidth = 66f;
        private const float ChipHeight = 52f;
        private const float ChipSpacing = 6f;

        private sealed class Chip
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Text Caption;
            public Image[] Parts;
            public Image[] Pips;
            public int ShownKey = int.MinValue;
        }

        private static Sprite _drop;

        private HudContext _ctx;
        private Chip _leg;
        private Chip _arm;
        private Chip _bleed;
        private Image _dropImage;
        private float _time;

        public RectTransform Root { get; private set; }

        public static LimbStateView Create(RectTransform parent, HudContext context)
        {
            var width = ChipWidth * 3f + ChipSpacing * 2f;
            var root = HudBuild.Rect("LimbState", parent, new Vector2(0.5f, 0f), new Vector2(1f, 0f),
                new Vector2(-(HudVitalsView.Width * 0.5f + 14f), HudVitalsView.BottomMargin), new Vector2(width, ChipHeight));
            var view = root.gameObject.AddComponent<LimbStateView>();
            view._ctx = context;
            view.Root = root;
            view.Build();
            return view;
        }

        private static Sprite DropSprite
        {
            get
            {
                if (_drop != null)
                    return _drop;

                const int n = 32;
                var texture = new Texture2D(n, n, TextureFormat.RGBA32, false)
                {
                    name = "HUD_Drop",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave
                };

                var pixels = new Color32[n * n];
                for (var y = 0; y < n; y++)
                {
                    for (var x = 0; x < n; x++)
                    {
                        var hits = 0;
                        for (var sy = 0; sy < 3; sy++)
                            for (var sx = 0; sx < 3; sx++)
                                if (LimbStateRules.InsideDrop((x + (sx + 0.5f) / 3f) / n, (y + (sy + 0.5f) / 3f) / n))
                                    hits++;

                        pixels[y * n + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 9));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                _drop = Sprite.Create(texture, new UnityEngine.Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
                _drop.name = "HUD_Drop";
                _drop.hideFlags = HideFlags.DontSave;
                return _drop;
            }
        }

        private void Build()
        {
            HudBuild.PassiveGroup(Root);
            var x = 0f;
            _leg = CreateChip("BACAK", x);
            // Bacak: iki dikey çubuk (sol/sağ bacak).
            _leg.Parts = new[]
            {
                HudBuild.Image("LegL", _leg.Root, UiSprites.White, UiTheme.TextDim, HudBuild.Center, HudBuild.Center, new Vector2(-7f, 2f), new Vector2(8f, 22f)),
                HudBuild.Image("LegR", _leg.Root, UiSprites.White, UiTheme.TextDim, HudBuild.Center, HudBuild.Center, new Vector2(7f, 2f), new Vector2(8f, 22f))
            };

            x += ChipWidth + ChipSpacing;
            _arm = CreateChip("KOL", x);
            // Kol: dikey üst kol + yatay ön kol ("L").
            _arm.Parts = new[]
            {
                HudBuild.Image("UpperArm", _arm.Root, UiSprites.White, UiTheme.TextDim, HudBuild.Center, HudBuild.Center, new Vector2(-6f, 5f), new Vector2(8f, 18f)),
                HudBuild.Image("ForeArm", _arm.Root, UiSprites.White, UiTheme.TextDim, HudBuild.Center, HudBuild.Center, new Vector2(3f, -3f), new Vector2(18f, 8f))
            };

            x += ChipWidth + ChipSpacing;
            _bleed = CreateChip("KANAMA", x);
            _dropImage = HudBuild.Image("Drop", _bleed.Root, DropSprite, UiTheme.HealthLow, HudBuild.Center, HudBuild.Center,
                new Vector2(0f, 2f), new Vector2(26f, 26f));
            _bleed.Parts = new[] { _dropImage };
            for (var i = 0; i < _bleed.Pips.Length; i++)
                _bleed.Pips[i].gameObject.SetActive(false); // kanamanın yara sayısı yok
            _bleed.Pips = new Image[0];
        }

        private Chip CreateChip(string title, float x)
        {
            var chip = new Chip();
            chip.Root = HudBuild.Rect(title, Root, Vector2.zero, Vector2.zero, new Vector2(x, 0f), new Vector2(ChipWidth, ChipHeight));
            chip.Group = HudBuild.PassiveGroup(chip.Root, 0f);
            HudBuild.FillImage("Bg", chip.Root, UiSprites.ChamferRect, UiTheme.WithAlpha(UiTheme.PanelDark, 0.85f));
            var border = HudBuild.FillImage("Border", chip.Root, UiSprites.RoundedRectOutline, UiTheme.WithAlpha(UiTheme.PanelBorder, 0.9f));
            border.type = UiSprites.HasBorder(UiSprites.RoundedRectOutline) ? Image.Type.Sliced : Image.Type.Simple;

            HudBuild.Text("Title", chip.Root, title, 12, TextAnchor.UpperLeft, UiTheme.TextDim, FontStyle.Bold,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(5f, -3f), new Vector2(ChipWidth - 10f, 14f), false);

            // Yara sayısı noktaları (en çok 2) sağ üstte.
            chip.Pips = new Image[2];
            for (var i = 0; i < chip.Pips.Length; i++)
            {
                chip.Pips[i] = HudBuild.Image("Pip" + i, chip.Root, UiSprites.Circle, UiTheme.WithAlpha(Color.white, 0.25f),
                    new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f - i * 9f, -6f), new Vector2(7f, 7f));
            }

            chip.Caption = HudBuild.Text("Caption", chip.Root, string.Empty, 12, TextAnchor.LowerCenter, UiTheme.Text, FontStyle.Bold,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(ChipWidth + 20f, 14f));
            return chip;
        }

        private void Update()
        {
            if (_leg == null || _arm == null || _bleed == null)
            {
                enabled = false; // kurulum yarım kaldı
                return;
            }

            var dt = Time.unscaledDeltaTime;
            _time += dt;

            var local = _ctx != null ? _ctx.Local : null;
            var limbs = local != null && local.IsAlive ? local.Limbs : null;
            var leg = limbs != null ? limbs.LegWounds : 0;
            var arm = limbs != null ? limbs.ArmWounds : 0;
            var bleeding = limbs != null && limbs.IsBleeding;
            var bleedLeft = bleeding ? limbs.BleedRemaining : 0f;

            ApplyWound(_leg, leg, LimbStateRules.LegCaption(Mathf.Max(1, leg)), dt);
            ApplyWound(_arm, arm, LimbStateRules.ArmCaption(Mathf.Max(1, arm)), dt);

            // Kanama: damla nabız gibi atar; kalan süre saniye değişince yazılır.
            var bleedKey = bleeding ? Mathf.Max(1, Mathf.CeilToInt(bleedLeft)) : 0;
            if (bleedKey != _bleed.ShownKey)
            {
                _bleed.ShownKey = bleedKey;
                if (bleeding)
                    UiFactory.SetText(_bleed.Caption, LimbStateRules.BleedCaption(bleedLeft));
            }

            _bleed.Group.alpha = Mathf.MoveTowards(_bleed.Group.alpha, bleeding ? 1f : 0f, dt * 4f);
            if (bleeding && _dropImage != null)
                HudBuild.SetAlpha(_dropImage, LimbStateRules.BleedPulse(_time));
        }

        private void ApplyWound(Chip chip, int wounds, string caption, float dt)
        {
            if (wounds != chip.ShownKey)
            {
                chip.ShownKey = wounds;
                if (wounds > 0)
                    UiFactory.SetText(chip.Caption, caption);

                var severity = LimbStateRules.Severity(wounds);
                var tint = severity >= 2 ? UiTheme.HealthLow : severity == 1 ? UiTheme.Amber : UiTheme.TextDim;
                for (var i = 0; i < chip.Parts.Length; i++)
                {
                    // 1 yarada ilk parça, 2 yarada hepsi vurgulanır.
                    var hurt = wounds > i;
                    chip.Parts[i].color = hurt ? tint : UiTheme.WithAlpha(UiTheme.TextDim, 0.7f);
                }

                for (var i = 0; i < chip.Pips.Length; i++)
                    chip.Pips[i].color = i < wounds ? tint : UiTheme.WithAlpha(Color.white, 0.25f);
            }

            chip.Group.alpha = Mathf.MoveTowards(chip.Group.alpha, wounds > 0 ? 1f : 0f, dt * 4f);
        }
    }
}
