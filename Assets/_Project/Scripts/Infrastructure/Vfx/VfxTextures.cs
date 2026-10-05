using UnityEngine;

namespace Project.Infrastructure.Vfx
{
    /// <summary>
    /// Efektlere özel prosedürel dokular (bir kez üretilir, GPU'ya yüklendikten sonra CPU kopyası bırakılır).
    /// Tüm dokular beyaz RGB + alfa (ya da koyu tonlu) olarak üretilir; renk malzeme/parçacık rengiyle verilir.
    /// </summary>
    internal static class VfxTextures
    {
        private static Texture2D _softDot;
        private static Texture2D _puff;
        private static Texture2D _chunk;
        private static Texture2D _ring;
        private static Texture2D _beam;
        private static Texture2D _holeGeneric;
        private static Texture2D _holeMetal;
        private static Texture2D _scorch;
        private static Texture2D _splat;

        /// <summary>Yumuşak parlak nokta (alev, kıvılcım parıltısı).</summary>
        public static Texture2D SoftDot => _softDot != null ? _softDot : _softDot = BuildSoftDot(64);

        /// <summary>Gürültülü yumuşak bulut (duman, toz).</summary>
        public static Texture2D Puff => _puff != null ? _puff : _puff = BuildPuff(64, 1337);

        /// <summary>Düşük poligonlu düzensiz parça (toprak, kıymık, enkaz, damla).</summary>
        public static Texture2D Chunk => _chunk != null ? _chunk : _chunk = BuildChunk(32);

        /// <summary>İnce halka (su halkası, şok dalgası).</summary>
        public static Texture2D Ring => _ring != null ? _ring : _ring = BuildRing(64);

        /// <summary>Uzunlamasına ışın (mermi izi). U = boy, V = en.</summary>
        public static Texture2D Beam => _beam != null ? _beam : _beam = BuildBeam(64, 16);

        /// <summary>Beton/ahşap/toprak mermi deliği: koyu çekirdek + açık renk kırık krater.</summary>
        public static Texture2D HoleGeneric => _holeGeneric != null ? _holeGeneric : _holeGeneric = BuildHole(64, 71, false);

        /// <summary>Metal mermi deliği: koyu çekirdek + parlak kazınmış kenar.</summary>
        public static Texture2D HoleMetal => _holeMetal != null ? _holeMetal : _holeMetal = BuildHole(64, 113, true);

        /// <summary>Patlama yanık izi.</summary>
        public static Texture2D Scorch => _scorch != null ? _scorch : _scorch = BuildScorch(128, 909);

        /// <summary>Kan sıçraması: düzensiz merkez lekesi + uydu damlalar.</summary>
        public static Texture2D Splat => _splat != null ? _splat : _splat = BuildSplat(64, 4242);

        private static Texture2D NewTexture(string name, int width, int height, bool mipmaps)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipmaps)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0,
                hideFlags = HideFlags.DontSave
            };
            return texture;
        }

        private static Texture2D Finish(Texture2D texture, Color32[] pixels)
        {
            texture.SetPixels32(pixels);
            texture.Apply(texture.mipmapCount > 1, true);
            return texture;
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
        }

        private static float Radius01(int x, int y, int size)
        {
            var half = size * 0.5f;
            var dx = (x + 0.5f - half) / half;
            var dy = (y + 0.5f - half) / half;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static float Angle(int x, int y, int size)
        {
            var half = size * 0.5f;
            return Mathf.Atan2(y + 0.5f - half, x + 0.5f - half);
        }

        private static Texture2D BuildSoftDot(int size)
        {
            var texture = NewTexture("VFX_SoftDot", size, size, true);
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var r = Radius01(x, y, size);
                    var falloff = Mathf.Clamp01(1f - r);
                    var alpha = falloff * falloff * (3f - 2f * falloff);
                    pixels[y * size + x] = new Color32(255, 255, 255, ToByte(alpha));
                }
            }

            return Finish(texture, pixels);
        }

        private static Texture2D BuildPuff(int size, int seed)
        {
            var texture = NewTexture("VFX_Puff", size, size, true);
            var pixels = new Color32[size * size];
            var ox = seed * 0.37f;
            var oy = seed * 0.61f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var u = (float)x / size;
                    var v = (float)y / size;
                    var noise = Mathf.PerlinNoise(ox + u * 4f, oy + v * 4f) * 0.65f
                                + Mathf.PerlinNoise(ox + 17f + u * 9f, oy + 3f + v * 9f) * 0.35f;
                    var r = Radius01(x, y, size) * (0.85f + noise * 0.3f);
                    var falloff = Mathf.Clamp01(1f - r);
                    var alpha = falloff * falloff * (3f - 2f * falloff) * (0.65f + noise * 0.45f);
                    var shade = 0.82f + noise * 0.18f;
                    var c = ToByte(shade);
                    pixels[y * size + x] = new Color32(c, c, c, ToByte(alpha));
                }
            }

            return Finish(texture, pixels);
        }

        private static Texture2D BuildChunk(int size)
        {
            var texture = NewTexture("VFX_Chunk", size, size, true);
            var pixels = new Color32[size * size];
            var edge = 1.5f / (size * 0.5f);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var r = Radius01(x, y, size);
                    var a = Angle(x, y, size);
                    // Düzensiz altıgen benzeri profil — düşük poligon parça hissi.
                    var profile = 0.72f + 0.12f * Mathf.Sin(3f * a + 0.7f) + 0.08f * Mathf.Sin(5f * a + 2.1f);
                    var alpha = Mathf.Clamp01((profile - r) / edge);
                    // Sol üstten hafif aydınlatma: hacim hissi.
                    var light = 0.75f + 0.25f * Mathf.Clamp(((float)y / size) - ((float)x / size) + 0.5f, 0f, 1f);
                    var c = ToByte(light);
                    pixels[y * size + x] = new Color32(c, c, c, ToByte(alpha));
                }
            }

            return Finish(texture, pixels);
        }

        private static Texture2D BuildRing(int size)
        {
            var texture = NewTexture("VFX_Ring", size, size, true);
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var r = Radius01(x, y, size);
                    var d = (r - 0.8f) / 0.09f;
                    var alpha = Mathf.Exp(-d * d) + Mathf.Clamp01(0.8f - r) * 0.12f;
                    if (r > 1f)
                        alpha = 0f;
                    pixels[y * size + x] = new Color32(255, 255, 255, ToByte(alpha));
                }
            }

            return Finish(texture, pixels);
        }

        private static Texture2D BuildBeam(int width, int height)
        {
            var texture = NewTexture("VFX_Beam", width, height, false);
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                var v = ((y + 0.5f) / height) * 2f - 1f;
                var across = Mathf.Exp(-v * v * 5f);
                var core = Mathf.Exp(-v * v * 30f);
                for (var x = 0; x < width; x++)
                {
                    var u = (x + 0.5f) / width;
                    var taper = Mathf.SmoothStep(0f, 1f, u / 0.06f) * Mathf.SmoothStep(0f, 1f, (1f - u) / 0.06f);
                    var alpha = across * taper;
                    var c = ToByte(0.75f + 0.25f * core);
                    pixels[y * width + x] = new Color32(c, c, c, ToByte(alpha));
                }
            }

            return Finish(texture, pixels);
        }

        private static Texture2D BuildHole(int size, int seed, bool metal)
        {
            var texture = NewTexture(metal ? "VFX_HoleMetal" : "VFX_HoleGeneric", size, size, true);
            var pixels = new Color32[size * size];
            var rng = new VfxRandom((uint)seed);
            const int crackCount = 7;
            var crackAngles = new float[crackCount];
            var crackLengths = new float[crackCount];
            for (var i = 0; i < crackCount; i++)
            {
                crackAngles[i] = rng.Range(-Mathf.PI, Mathf.PI);
                crackLengths[i] = rng.Range(0.55f, 0.95f);
            }

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var r = Radius01(x, y, size);
                    var a = Angle(x, y, size);
                    var jag = 0.04f * Mathf.Sin(7f * a + seed) + 0.03f * Mathf.Sin(11f * a + seed * 0.3f);
                    var rr = r + jag;
                    float shade;
                    float alpha;

                    if (rr < 0.24f)
                    {
                        shade = 0.03f;
                        alpha = 1f;
                    }
                    else if (metal)
                    {
                        // Parlak kazınmış metal kenar + hızlı sönüm.
                        var rim = Mathf.Exp(-Mathf.Pow((rr - 0.32f) / 0.07f, 2f));
                        shade = Mathf.Lerp(0.2f, 0.95f, rim);
                        alpha = Mathf.Clamp01(rim + Mathf.Clamp01(1f - (rr - 0.24f) / 0.35f) * 0.45f);
                    }
                    else
                    {
                        // Açık renk kırık krater + koyu is halkası + çatlaklar.
                        var crater = Mathf.Clamp01(1f - (rr - 0.24f) / 0.5f);
                        shade = Mathf.Lerp(0.25f, 0.85f, Mathf.Clamp01((rr - 0.24f) / 0.18f));
                        alpha = crater * crater * 0.9f;

                        for (var i = 0; i < crackCount; i++)
                        {
                            var da = Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, crackAngles[i] * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                            var width = 0.05f * (1f - r / crackLengths[i]);
                            if (r < crackLengths[i] && da * r < width)
                            {
                                shade = 0.12f;
                                alpha = Mathf.Max(alpha, 0.85f * (1f - r / crackLengths[i]));
                            }
                        }
                    }

                    if (r >= 1f)
                        alpha = 0f;

                    var c = ToByte(shade);
                    pixels[y * size + x] = new Color32(c, c, c, ToByte(alpha));
                }
            }

            return Finish(texture, pixels);
        }

        private static Texture2D BuildScorch(int size, int seed)
        {
            var texture = NewTexture("VFX_Scorch", size, size, true);
            var pixels = new Color32[size * size];
            var ox = seed * 0.13f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var u = (float)x / size;
                    var v = (float)y / size;
                    var a = Angle(x, y, size);
                    var noise = Mathf.PerlinNoise(ox + u * 6f, ox + v * 6f);
                    var r = Radius01(x, y, size) * (0.9f + 0.12f * Mathf.Sin(5f * a + 1.3f) + noise * 0.2f);
                    var falloff = Mathf.Clamp01(1f - Mathf.InverseLerp(0.25f, 1f, r));
                    var alpha = falloff * (0.7f + 0.3f * noise);
                    var c = ToByte(0.05f + noise * 0.08f);
                    pixels[y * size + x] = new Color32(c, c, c, ToByte(alpha));
                }
            }

            return Finish(texture, pixels);
        }

        private static Texture2D BuildSplat(int size, int seed)
        {
            var texture = NewTexture("VFX_Splat", size, size, true);
            var pixels = new Color32[size * size];
            var rng = new VfxRandom((uint)seed);
            const int dropCount = 9;
            var dropX = new float[dropCount];
            var dropY = new float[dropCount];
            var dropR = new float[dropCount];
            for (var i = 0; i < dropCount; i++)
            {
                var angle = rng.Range(-Mathf.PI, Mathf.PI);
                var distance = rng.Range(0.45f, 0.85f);
                dropX[i] = Mathf.Cos(angle) * distance;
                dropY[i] = Mathf.Sin(angle) * distance;
                dropR[i] = rng.Range(0.04f, 0.1f);
            }

            var edge = 2f / size;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var half = size * 0.5f;
                    var px = (x + 0.5f - half) / half;
                    var py = (y + 0.5f - half) / half;
                    var r = Mathf.Sqrt(px * px + py * py);
                    var a = Mathf.Atan2(py, px);
                    var profile = 0.38f + 0.06f * Mathf.Sin(5f * a + 1.1f) + 0.04f * Mathf.Sin(9f * a + 2.7f)
                                  + 0.05f * Mathf.PerlinNoise(seed * 0.1f + px * 3f, py * 3f);
                    var alpha = Mathf.Clamp01((profile - r) / edge);

                    for (var i = 0; i < dropCount; i++)
                    {
                        var dx = px - dropX[i];
                        var dy = py - dropY[i];
                        var d = Mathf.Sqrt(dx * dx + dy * dy);
                        alpha = Mathf.Max(alpha, Mathf.Clamp01((dropR[i] - d) / edge));
                    }

                    if (r >= 1f)
                        alpha = 0f;

                    // Merkez daha koyu (kuruyan kan), kenar biraz açık.
                    var shade = Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(r / 0.45f));
                    var c = ToByte(shade);
                    pixels[y * size + x] = new Color32(c, c, c, ToByte(alpha * 0.92f));
                }
            }

            return Finish(texture, pixels);
        }
    }

    /// <summary>Küçük, tahsissiz xorshift RNG (UnityEngine.Random global durumunu bozmamak için).</summary>
    internal struct VfxRandom
    {
        private uint _state;

        public VfxRandom(uint seed)
        {
            _state = seed == 0u ? 0x9E3779B9u : seed;
        }

        public uint NextUInt()
        {
            var x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>[0, 1)</summary>
        public float Value()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        public float Range(float min, float max)
        {
            return min + (max - min) * Value();
        }
    }
}
