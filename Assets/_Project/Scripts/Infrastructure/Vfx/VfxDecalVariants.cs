using UnityEngine;

namespace Project.Infrastructure.Vfx
{
    /// <summary>
    /// Yüzey başına çok çeşitli mermi deliği dokuları: koyu çekirdek + merkezden kaymış yumuşak karartma halesi
    /// (normal haritası hissi) + yüzeye özgü detay (beton kırığı, ahşap kıymığı, toprak krateri, metal çukur, kar ıslaklığı).
    /// Dokular tembel üretilir ve paylaşılan malzemelere sarılır.
    /// </summary>
    internal static class VfxDecalVariants
    {
        public const int VariantCount = 3;
        private const int Size = 64;

        private static readonly Material[][] Cache = new Material[5][];

        /// <summary>Verilen yüzey için çıkartma kümesi (Concrete=0, Wood=1, Dirt=2, Metal=3, Snow=4).</summary>
        public static int SlotFor(SurfaceKind surface)
        {
            switch (surface)
            {
                case SurfaceKind.Wood: return 1;
                case SurfaceKind.Dirt: return 2;
                case SurfaceKind.Metal: return 3;
                case SurfaceKind.Snow: return 4;
                default: return 0;
            }
        }

        public static Material Get(SurfaceKind surface, int roll)
        {
            try
            {
                if (Project.Infrastructure.Content.ContentOverrides.TryGetDecal(surface, out var overrideMaterial) && overrideMaterial != null)
                    return overrideMaterial;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[VFX] DecalSetOverride okunamadı, prosedürel doku kullanılıyor: " + e.Message);
            }

            var slot = SlotFor(surface);
            var variant = Mathf.Abs(roll) % VariantCount;
            var row = Cache[slot] ?? (Cache[slot] = new Material[VariantCount]);
            var material = row[variant];
            if (material != null)
                return material;

            var texture = BuildTexture(slot, 1000 + slot * 97 + variant * 31);
            material = VfxMaterials.CreateDecal("VFX_Hole_" + slot + "_" + variant, texture);
            row[variant] = material;
            return material ?? VfxMaterials.DecalFor(surface);
        }

        public static void ResetCache()
        {
            for (var i = 0; i < Cache.Length; i++)
                Cache[i] = null;
        }

        /// <summary>Saf piksel üretimi (test edilebilir): 64x64 RGBA32.</summary>
        public static Color32[] BuildPixels(int slot, int seed)
        {
            var pixels = new Color32[Size * Size];
            var rng = new VfxRandom((uint)seed);
            var shift = new Vector2(rng.Range(-0.12f, 0.12f), rng.Range(-0.12f, 0.12f));
            var coreR = slot == 3 ? rng.Range(0.1f, 0.14f) : rng.Range(0.14f, 0.2f);
            var phase = rng.Range(0f, 6.28f);
            const int rays = 9;
            var rayAngle = new float[rays];
            var rayLen = new float[rays];
            for (var i = 0; i < rays; i++)
            {
                rayAngle[i] = rng.Range(-Mathf.PI, Mathf.PI);
                rayLen[i] = rng.Range(0.35f, 0.95f);
            }

            var half = Size * 0.5f;
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var dx = (x + 0.5f - half) / half;
                    var dy = (y + 0.5f - half) / half;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    var a = Mathf.Atan2(dy, dx);
                    var jag = 0.05f * Mathf.Sin(7f * a + phase) + 0.035f * Mathf.Sin(13f * a + phase * 1.7f);
                    var rr = r + jag;

                    // Merkezden kaymış karartma hale (ışık yönü hissi).
                    var hr = Mathf.Sqrt((dx - shift.x) * (dx - shift.x) + (dy - shift.y) * (dy - shift.y)) + jag * 0.6f;
                    var halo = Mathf.Pow(Mathf.Clamp01(1f - hr / 0.98f), 2f);

                    float shade;
                    float alpha;
                    var tint = Color.white;
                    switch (slot)
                    {
                        case 1: // ahşap: kıymık ışınları
                            shade = 0.22f; alpha = halo * 0.45f; tint = new Color(0.8f, 0.6f, 0.38f);
                            for (var i = 0; i < rays; i++)
                            {
                                var da = Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, rayAngle[i] * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                                if (r < rayLen[i] && r > coreR && da * r < 0.03f)
                                {
                                    shade = 0.8f; alpha = Mathf.Max(alpha, 0.85f * (1f - r / rayLen[i]));
                                }
                            }

                            break;
                        case 2: // toprak: yumuşak koyu krater + hafif nemli halka
                            shade = 0.14f; alpha = halo * 0.7f; tint = new Color(0.55f, 0.42f, 0.3f);
                            break;
                        case 3: // metal: küçük çukur + parlak kazınmış kenar
                            {
                                var rim = Mathf.Exp(-Mathf.Pow((rr - coreR - 0.1f) / 0.06f, 2f));
                                shade = Mathf.Lerp(0.18f, 0.95f, rim); alpha = Mathf.Clamp01(rim * 0.95f + halo * 0.25f);
                                tint = new Color(0.9f, 0.9f, 0.95f);
                                break;
                            }
                        case 4: // kar: ıslak koyu halka, mavimsi
                            shade = 0.3f; alpha = halo * 0.55f; tint = new Color(0.55f, 0.62f, 0.72f);
                            break;
                        default: // beton: açık kırık kenar + çatlaklar
                            {
                                var chip = Mathf.Exp(-Mathf.Pow((rr - coreR - 0.12f) / 0.1f, 2f));
                                shade = Mathf.Lerp(0.2f, 0.82f, chip); alpha = Mathf.Clamp01(halo * 0.5f + chip * 0.5f);
                                tint = new Color(0.82f, 0.8f, 0.76f);
                                for (var i = 0; i < rays; i++)
                                {
                                    var da = Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, rayAngle[i] * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                                    var w = 0.035f * (1f - r / rayLen[i]);
                                    if (r < rayLen[i] && da * r < w)
                                    {
                                        shade = 0.1f; alpha = Mathf.Max(alpha, 0.8f * (1f - r / rayLen[i]));
                                    }
                                }

                                break;
                            }
                    }

                    if (rr < coreR)
                    {
                        shade = 0.02f;
                        alpha = 1f;
                    }

                    // Kenarlarda tam şeffaflık (quad sınırında kesik görünmesin).
                    alpha *= Mathf.Clamp01((1f - r) / 0.08f);
                    var c = new Color(shade * tint.r, shade * tint.g, shade * tint.b, Mathf.Clamp01(alpha));
                    pixels[y * Size + x] = c;
                }
            }

            return pixels;
        }

        private static Texture2D BuildTexture(int slot, int seed)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = "VFX_HoleVariant",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(BuildPixels(slot, seed));
            texture.Apply(true, true);
            return texture;
        }
    }
}
