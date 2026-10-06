using Project.Core.Domain;
using Project.Infrastructure.Rendering;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Hasar yönü göstergeleri: yerel oyuncu hasar aldığında saldırının geldiği yöne bakan kırmızı yay (nişangâh
    /// çevresinde). Kaynak dünya konumunda tutulur, oyuncu döndükçe yay yönünü günceller; 1,6 sn'de söner.
    /// Havuzlu (6 gösterge), aynı yönden gelen isabetler aynı göstergeyi tazeler.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamageIndicatorView : MonoBehaviour
    {
        private const int PoolSize = 6;
        private const float Radius = 130f;
        private const float Lifetime = 1.6f;
        private const float MergeAngle = 25f;
        // 192x64 dokunun 3:1 oranı korunur; HudVisualRules.MaxIndicatorPx (120) üst sınırı aşılmaz.
        private const float ArcWidth = 112f;
        private const float ArcHeight = 112f / 3f;

        private static Sprite _arcSprite;
        private static Sprite _splatSprite;
        private static Sprite _ringSprite;

        // Kan sıçraması: 4 ekran bölgesi (üst/sağ/alt/sol), 2 sn'de söner; zırh kırılma halkası.
        private readonly Image[] _splat = new Image[4];
        private readonly float[] _splatAge = { -1f, -1f, -1f, -1f };
        private readonly float[] _splatStrength = new float[4];
        private float _wipeAge = -1f;
        private Image _ring;
        private float _ringAge = -1f;

        private sealed class Indicator
        {
            public RectTransform Pivot;
            public Image Arc;
            public Vector3 Source;
            public float TimeLeft;
            public float Strength;
        }

        private HudContext _ctx;
        private readonly Indicator[] _pool = new Indicator[PoolSize];

        public RectTransform Root { get; private set; }

        public static DamageIndicatorView Create(RectTransform parent, HudContext context)
        {
            var root = HudBuild.Rect("DamageIndicators", parent, HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(400f, 400f));
            var view = root.gameObject.AddComponent<DamageIndicatorView>();
            view._ctx = context;
            view.Root = root;
            view.Build();
            return view;
        }

        private void Build()
        {
            HudBuild.PassiveGroup(Root);
            HudBuild.NestedCanvas(Root);

            BuildSplatters();
            var sprite = ArcSprite;
            for (var i = 0; i < PoolSize; i++)
            {
                var pivot = HudBuild.Rect("Indicator" + i, Root, HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(2f, 2f));
                var arc = HudBuild.Image("Arc", pivot, sprite, UiTheme.WithAlpha(UiTheme.HealthLow, 0f), HudBuild.Center,
                    new Vector2(0.5f, 0f), new Vector2(0f, Radius - 24f), new Vector2(ArcWidth, ArcHeight));
                arc.raycastTarget = false;
                _pool[i] = new Indicator { Pivot = pivot, Arc = arc };
                HudBuild.SetActive(pivot, false);
            }
        }

        /// <summary>Saldırı kaynağının dünya konumundan gösterge ekler.</summary>
        public void Show(Vector3 sourceWorld, float damage)
        {
            var origin = _ctx.Position;
            if (!HudVisualRules.IndicatorShouldShow(damage, origin, sourceWorld))
                return;
            var bearing = HudFormat.Bearing(origin, sourceWorld);
            var strength = Mathf.Clamp(0.45f + damage / 40f, 0.45f, 1f);

            // Yakın yönden gelen etkin bir gösterge varsa onu tazele.
            Indicator target = null;
            for (var i = 0; i < _pool.Length; i++)
            {
                var indicator = _pool[i];
                if (indicator.TimeLeft <= 0f)
                    continue;

                var existing = HudFormat.Bearing(origin, indicator.Source);
                if (Mathf.Abs(Mathf.DeltaAngle(existing, bearing)) <= MergeAngle)
                {
                    target = indicator;
                    break;
                }
            }

            if (target == null)
            {
                // Boş ya da en eski olanı kullan.
                var oldest = 0;
                for (var i = 0; i < _pool.Length; i++)
                {
                    if (_pool[i].TimeLeft <= 0f)
                    {
                        oldest = i;
                        break;
                    }

                    if (_pool[i].TimeLeft < _pool[oldest].TimeLeft)
                        oldest = i;
                }

                target = _pool[oldest];
                target.Strength = 0f;
            }

            AddSplatter(Mathf.DeltaAngle(_ctx.Yaw, bearing), damage);
            target.Source = sourceWorld;
            target.TimeLeft = Lifetime;
            // Aktif edilirken ilk karede eski/çöp alfa görünmesin.
            HudBuild.SetAlpha(target.Arc, 0f);
            target.Strength = Mathf.Max(target.Strength, strength);
            HudBuild.SetActive(target.Pivot, true);
        }

        private void BuildSplatters()
        {
            // Bölgeler tam ekran kenarlarına demirli; Root 400x400 merkezde olduğundan ebeveyni kökün ebeveyni yap.
            var parent = Root.parent != null ? (RectTransform)Root.parent : Root;
            var sprite = SplatSprite;
            for (var q = 0; q < 4; q++)
            {
                var img = HudBuild.Image("BloodSplat" + q, parent, sprite, new Color(0.62f, 0.03f, 0.03f, 0f),
                    HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(900f, 900f));
                var rt = img.rectTransform;
                // Merkez dışına kaydır: 0 üst, 1 sağ, 2 alt, 3 sol.
                var dir = q == 0 ? Vector2.up : q == 1 ? Vector2.right : q == 2 ? Vector2.down : Vector2.left;
                rt.anchoredPosition = new Vector2(dir.x * 900f, dir.y * 500f);
                rt.SetSiblingIndex(0);
                HudBuild.PassiveGroup(rt);
                img.raycastTarget = false;
                _splat[q] = img;
            }

            _ring = HudBuild.Image("ArmorBreakRing", Root, RingSprite, new Color(1f, 1f, 1f, 0f), HudBuild.Center,
                HudBuild.Center, Vector2.zero, new Vector2(120f, 120f));
            _ring.raycastTarget = false;
        }

        /// <summary>Hasar yönüne göre ekran bölgesine kan sıçraması ekler.</summary>
        public void AddSplatter(float relativeDeg, float damage)
        {
            var q = CombatScreenFxMath.QuadrantOf(relativeDeg);
            _splatAge[q] = 0f;
            _splatStrength[q] = CombatScreenFxMath.SplatterStrength(damage);
            _wipeAge = -1f;
        }

        /// <summary>İyileşme: lekeler temiz bir silmeyle hızla kaybolur.</summary>
        public void Heal()
        {
            _wipeAge = 0f;
        }

        /// <summary>Zırh kırıldı: beyaz parlama (CombatScreenFx) + genişleyen halka.</summary>
        public void ArmorBreak()
        {
            _ringAge = 0f;
            CombatScreenFx.NotifyArmorBreak();
        }

        private void TickFx(float dt)
        {
            var k = CombatScreenFx.Intensity;
            if (_wipeAge >= 0f)
                _wipeAge += dt;
            var wipe = CombatScreenFxMath.HealWipe(_wipeAge);
            for (var q = 0; q < 4; q++)
            {
                if (_splat[q] == null)
                    continue;
                if (_splatAge[q] >= 0f)
                {
                    _splatAge[q] += dt;
                    if (_splatAge[q] >= CombatScreenFxMath.SplatterLifetime || wipe <= 0f)
                        _splatAge[q] = -1f;
                }

                var a = _splatAge[q] < 0f ? 0f
                    : CombatScreenFxMath.SplatterAlpha(_splatAge[q], _splatStrength[q]) * wipe * Mathf.Min(1f, k);
                HudBuild.SetAlpha(_splat[q], a * 0.85f);
            }

            if (_ring != null)
            {
                if (_ringAge >= 0f)
                {
                    _ringAge += dt;
                    if (_ringAge >= CombatScreenFxMath.ArmorBreakDuration)
                        _ringAge = -1f;
                }

                var f = _ringAge < 0f ? 0f : CombatScreenFxMath.ArmorBreakFlash(_ringAge);
                var r = _ringAge < 0f ? 0f : CombatScreenFxMath.ArmorBreakRing(_ringAge);
                _ring.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(90f, 420f, r);
                HudBuild.SetAlpha(_ring, f * Mathf.Min(1f, k));
            }
        }

        private static Sprite SplatSprite
        {
            get
            {
                if (_splatSprite != null)
                    return _splatSprite;
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
                    { name = "HUD_BloodSplat", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                var px = new Color32[n * n];
                for (var y = 0; y < n; y++)
                for (var x = 0; x < n; x++)
                {
                    var dx = (x + 0.5f) / n * 2f - 1f;
                    var dy = (y + 0.5f) / n * 2f - 1f;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    // Düzensiz kenarlı leke: açıya bağlı gürültü + yumuşak düşüş.
                    var ang = Mathf.Atan2(dy, dx);
                    var edge = 0.75f + 0.12f * Mathf.Sin(ang * 5f + 1.3f) + 0.08f * Mathf.Sin(ang * 11f);
                    var a = Mathf.Clamp01((edge - r) / 0.45f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * a * 255f));
                }

                tex.SetPixels32(px);
                tex.Apply(false, true);
                _splatSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                _splatSprite.hideFlags = HideFlags.DontSave;
                return _splatSprite;
            }
        }

        private static Sprite RingSprite
        {
            get
            {
                if (_ringSprite != null)
                    return _ringSprite;
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
                    { name = "HUD_ArmorRing", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                var px = new Color32[n * n];
                for (var y = 0; y < n; y++)
                for (var x = 0; x < n; x++)
                {
                    var dx = (x + 0.5f) / n * 2f - 1f;
                    var dy = (y + 0.5f) / n * 2f - 1f;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    var a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.9f) / 0.07f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }

                tex.SetPixels32(px);
                tex.Apply(false, true);
                _ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                _ringSprite.hideFlags = HideFlags.DontSave;
                return _ringSprite;
            }
        }

        /// <summary>Tüm göstergeleri kapatır (ölüm/yeniden doğma).</summary>
        public void Clear()
        {
            for (var q = 0; q < 4; q++)
            {
                _splatAge[q] = -1f;
                if (_splat[q] != null)
                    HudBuild.SetAlpha(_splat[q], 0f);
            }

            _ringAge = -1f;
            for (var i = 0; i < _pool.Length; i++)
            {
                _pool[i].TimeLeft = 0f;
                HudBuild.SetAlpha(_pool[i].Arc, 0f);
                HudBuild.SetActive(_pool[i].Pivot, false);
            }
        }

        /// <summary>HUD denetleyicisi her karede çağırır.</summary>
        public void Tick(float deltaTime)
        {
            TickFx(deltaTime);
            var origin = _ctx.Position;
            var yaw = _ctx.Yaw;
            for (var i = 0; i < _pool.Length; i++)
            {
                var indicator = _pool[i];
                if (indicator.TimeLeft <= 0f)
                {
                    // Güvenlik: söndürülmüş gösterge asla açık kalmasın.
                    HudBuild.SetActive(indicator.Pivot, false);
                    continue;
                }

                indicator.TimeLeft -= deltaTime;
                if (indicator.TimeLeft <= 0f)
                {
                    HudBuild.SetAlpha(indicator.Arc, 0f);
                    HudBuild.SetActive(indicator.Pivot, false);
                    continue;
                }

                var bearing = HudFormat.Bearing(origin, indicator.Source);
                var relative = Mathf.DeltaAngle(yaw, bearing);
                HudBuild.SetRotation(indicator.Pivot, -relative);

                HudBuild.SetAlpha(indicator.Arc, HudVisualRules.IndicatorAlpha(indicator.TimeLeft, Lifetime, indicator.Strength));
            }
        }

        /// <summary>Yay dokusu: halka diliminden kesilmiş, uçlara ve kenarlara doğru yumuşayan kalın bant.</summary>
        private static Sprite ArcSprite
        {
            get
            {
                if (_arcSprite != null)
                    return _arcSprite;

                const int width = 192;
                const int height = 64;
                const float centerX = width * 0.5f;
                const float centerY = -230f;
                const float inner = 246f;
                const float outer = 280f;
                const float halfAngle = 23f;

                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "HUD_DamageArc",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave
                };

                var pixels = new Color32[width * height];
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var dx = x + 0.5f - centerX;
                        var dy = y + 0.5f - centerY;
                        var r = Mathf.Sqrt(dx * dx + dy * dy);
                        var angle = Mathf.Abs(Mathf.Atan2(dx, dy) * Mathf.Rad2Deg);

                        var radial = Mathf.Clamp01((r - inner) / 6f) * Mathf.Clamp01((outer - r) / 10f);
                        // İç kenar parlak, dış kenara doğru soluyor.
                        radial *= Mathf.Lerp(1f, 0.55f, Mathf.Clamp01((r - inner) / (outer - inner)));
                        var angular = Mathf.Clamp01((halfAngle - angle) / 9f);
                        var a = Mathf.Clamp01(radial * angular);
                        pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                _arcSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
                _arcSprite.name = "HUD_DamageArc";
                _arcSprite.hideFlags = HideFlags.DontSave;
                return _arcSprite;
            }
        }

        /// <summary>Olay kaynağını dünya konumuna çevirir (Float3 → Vector3).</summary>
        public static Vector3 ToVector(Float3 value) => new Vector3(value.X, value.Y, value.Z);
    }
}
