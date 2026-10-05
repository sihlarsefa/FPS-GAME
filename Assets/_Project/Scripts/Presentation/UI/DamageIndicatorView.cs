using Project.Core.Domain;
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
        private const float Radius = 150f;
        private const float Lifetime = 1.6f;
        private const float MergeAngle = 25f;

        private static Sprite _arcSprite;

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

            var sprite = ArcSprite;
            for (var i = 0; i < PoolSize; i++)
            {
                var pivot = HudBuild.Rect("Indicator" + i, Root, HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(2f, 2f));
                var arc = HudBuild.Image("Arc", pivot, sprite, UiTheme.WithAlpha(UiTheme.HealthLow, 0f), HudBuild.Center,
                    new Vector2(0.5f, 0f), new Vector2(0f, Radius - 24f), new Vector2(180f, 64f));
                _pool[i] = new Indicator { Pivot = pivot, Arc = arc };
                HudBuild.SetActive(pivot, false);
            }
        }

        /// <summary>Saldırı kaynağının dünya konumundan gösterge ekler.</summary>
        public void Show(Vector3 sourceWorld, float damage)
        {
            var origin = _ctx.Position;
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

            target.Source = sourceWorld;
            target.TimeLeft = Lifetime;
            target.Strength = Mathf.Max(target.Strength, strength);
            HudBuild.SetActive(target.Pivot, true);
        }

        /// <summary>Tüm göstergeleri kapatır (ölüm/yeniden doğma).</summary>
        public void Clear()
        {
            for (var i = 0; i < _pool.Length; i++)
            {
                _pool[i].TimeLeft = 0f;
                HudBuild.SetActive(_pool[i].Pivot, false);
            }
        }

        /// <summary>HUD denetleyicisi her karede çağırır.</summary>
        public void Tick(float deltaTime)
        {
            var origin = _ctx.Position;
            var yaw = _ctx.Yaw;
            for (var i = 0; i < _pool.Length; i++)
            {
                var indicator = _pool[i];
                if (indicator.TimeLeft <= 0f)
                    continue;

                indicator.TimeLeft -= deltaTime;
                if (indicator.TimeLeft <= 0f)
                {
                    HudBuild.SetActive(indicator.Pivot, false);
                    continue;
                }

                var bearing = HudFormat.Bearing(origin, indicator.Source);
                var relative = Mathf.DeltaAngle(yaw, bearing);
                HudBuild.SetRotation(indicator.Pivot, -relative);

                var t = indicator.TimeLeft / Lifetime;
                var alpha = indicator.Strength * (t > 0.6f ? 1f : t / 0.6f);
                HudBuild.SetAlpha(indicator.Arc, alpha * 0.9f);
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
