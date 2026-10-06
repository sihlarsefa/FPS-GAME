using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>Su için prosedürel döngüsel dokular: iki normal harita, nötr gri detay ve kıyı köpüğü.</summary>
    public static class WaterSurfaceTextures
    {
        private static Texture2D _normalA, _normalB, _gray, _foam;

        public static Texture2D NormalA => _normalA != null ? _normalA : _normalA = BuildNormal(128, 6, 3, 11, 2.2f);
        public static Texture2D NormalB => _normalB != null ? _normalB : _normalB = BuildNormal(128, 9, 3, 53, 1.6f);

        public static Texture2D Gray
        {
            get
            {
                if (_gray == null)
                {
                    _gray = new Texture2D(2, 2, TextureFormat.RGBA32, false, true) { name = "HK_WaterGray", wrapMode = TextureWrapMode.Repeat };
                    var px = new Color32[4];
                    for (var i = 0; i < 4; i++) px[i] = new Color32(128, 128, 128, 255);
                    _gray.SetPixels32(px);
                    _gray.Apply(false, true);
                }

                return _gray;
            }
        }

        /// <summary>Kıyı köpüğü: beyaz, alfa kırık gürültü.</summary>
        public static Texture2D Foam
        {
            get
            {
                if (_foam == null)
                {
                    const int n = 128;
                    _foam = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "HK_Foam", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
                    var px = new Color32[n * n];
                    for (var y = 0; y < n; y++)
                    for (var x = 0; x < n; x++)
                    {
                        var d = SkyWaterRules.TileableFbm(x / (float)n, y / (float)n, 6, 4, 91);
                        var a = Mathf.Clamp01(0.45f + (d - 0.35f) * 1.8f);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }

                    _foam.SetPixels32(px);
                    _foam.Apply(true, true);
                }

                return _foam;
            }
        }

        private static Texture2D BuildNormal(int n, int period, int octaves, int seed, float strength)
        {
            var h = new float[n * n];
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
                h[y * n + x] = SkyWaterRules.TileableFbm(x / (float)n, y / (float)n, period, octaves, seed);

            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { name = "HK_WaterNormal" + seed, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var dx = h[y * n + (x + 1) % n] - h[y * n + (x + n - 1) % n];
                var dy = h[((y + 1) % n) * n + x] - h[((y + n - 1) % n) * n + x];
                var v = new Vector3(-dx * strength * n * 0.12f, -dy * strength * n * 0.12f, 1f).normalized;
                px[y * n + x] = new Color32((byte)Mathf.RoundToInt((v.x * 0.5f + 0.5f) * 255f), (byte)Mathf.RoundToInt((v.y * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((v.z * 0.5f + 0.5f) * 255f), 255);
            }

            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }
    }
}
