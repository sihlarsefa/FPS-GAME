using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>Türkçe trafik/askeri tabela metinleri ve renkleri + tek dokuda (atlas) birleştirme.</summary>
    public static class RoadsideSignSet
    {
        public const int CellW = 256;
        public const int CellH = 64;

        public struct SignDef
        {
            public string Text;
            public Color32 Bg;
            public Color32 Fg;
        }

        public static readonly SignDef[] Defs =
        {
            new SignDef { Text = "KUZGUN 12", Bg = new Color32(20, 62, 130, 255), Fg = new Color32(245, 245, 245, 255) },
            new SignDef { Text = "DİKKAT ASKERİ BÖLGE", Bg = new Color32(235, 190, 20, 255), Fg = new Color32(15, 15, 15, 255) },
            new SignDef { Text = "YAVAŞ", Bg = new Color32(245, 245, 245, 255), Fg = new Color32(200, 30, 30, 255) },
            new SignDef { Text = "MAYIN TEHLİKESİ", Bg = new Color32(170, 25, 25, 255), Fg = new Color32(245, 245, 245, 255) }
        };

        public static int Count => Defs.Length;

        /// <summary>Atlas pikselleri (altta kind 0; Unity dokusu alttan başlar, satırlar çevrilir). Boyut CellW x CellH*Count.</summary>
        public static Color32[] BuildAtlasPixels()
        {
            var w = CellW;
            var h = CellH * Defs.Length;
            var atlas = new Color32[w * h];
            for (var k = 0; k < Defs.Length; k++)
            {
                var cell = SignGlyphs.RenderCell(Defs[k].Text, CellW, CellH, Defs[k].Bg, Defs[k].Fg);
                for (var y = 0; y < CellH; y++)
                {
                    var dstRow = k * CellH + (CellH - 1 - y);
                    for (var x = 0; x < CellW; x++)
                        atlas[dstRow * w + x] = cell[y * CellW + x];
                }
            }

            return atlas;
        }

        /// <summary>Tür için dikey UV aralığı (v0, v1).</summary>
        public static Vector2 VRange(int kind)
        {
            var n = Defs.Length;
            kind = Mathf.Clamp(kind, 0, n - 1);
            return new Vector2(kind / (float)n, (kind + 1) / (float)n);
        }

        private static Texture2D _atlas;

        public static Texture2D Atlas()
        {
            if (_atlas != null)
                return _atlas;
            var tex = new Texture2D(CellW, CellH * Defs.Length, TextureFormat.RGBA32, true) { name = "RoadsideSignAtlas" };
            tex.SetPixels32(BuildAtlasPixels());
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 4;
            tex.Apply(true, false);
            _atlas = tex;
            return tex;
        }
    }
}
