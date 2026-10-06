using System;
using System.Collections.Generic;

namespace Project.Core.Domain
{
    /// <summary>
    /// Çim demeti (kümesi) geometrisi: düz dizilerle (Unity bağımlılığı yok). 3 varyant: kısa, orta, uzun/ince.
    /// Köşe rengi: R = eğilme ağırlığı (boy²), G = bıçak başına rastgele faz, B = ortam kapanması (taban koyu), A = 1.
    /// Her bıçak 3 segment: 2 dörtgen (4 üçgen) + uç üçgeni = 7 köşe, 5 üçgen; çift yüzlü çizim shader'da (Cull Off).
    /// </summary>
    public sealed class GrassBladeGeometry
    {
        public float[] Positions;  // xyz
        public float[] Normals;    // xyz
        public float[] Uvs;        // xy
        public float[] Colors;     // rgba
        public int[] Indices;
        public int VertexCount => Positions.Length / 3;
        public float MaxHeight;
        public float Radius;

        public const int Segments = 3;

        /// <summary>variant 0 = kısa (7 bıçak), 1 = orta (9), 2 = uzun/ince (5). detailScale &lt; 1 bıçak sayısını azaltır (en az 3).</summary>
        public static GrassBladeGeometry Build(int variant, int seed, float detailScale = 1f)
        {
            variant = GrassRules.Clamp(variant, 0, GrassRules.VariantCount - 1);
            int blades; float hMin, hMax, wMin, wMax, spread, lean;
            switch (variant)
            {
                case 0: blades = 7; hMin = 0.35f; hMax = 0.6f; wMin = 0.05f; wMax = 0.075f; spread = 0.16f; lean = 0.12f; break;
                case 1: blades = 9; hMin = 0.5f; hMax = 0.9f; wMin = 0.045f; wMax = 0.07f; spread = 0.2f; lean = 0.2f; break;
                default: blades = 5; hMin = 0.8f; hMax = 1.2f; wMin = 0.035f; wMax = 0.055f; spread = 0.14f; lean = 0.3f; break;
            }

            blades = Math.Max(3, (int)Math.Round(blades * GrassRules.Clamp01(detailScale <= 0f ? 1f : detailScale)));
            var pos = new List<float>();
            var nrm = new List<float>();
            var uv = new List<float>();
            var col = new List<float>();
            var idx = new List<int>();
            float maxH = 0f, maxR = 0f;

            for (var b = 0; b < blades; b++)
            {
                float r0 = GrassRules.Hash01(b, variant, 1, seed);
                float r1 = GrassRules.Hash01(b, variant, 2, seed);
                float r2 = GrassRules.Hash01(b, variant, 3, seed);
                float r3 = GrassRules.Hash01(b, variant, 4, seed);
                float r4 = GrassRules.Hash01(b, variant, 5, seed);

                double a = r0 * Math.PI * 2.0;
                float rad = (float)Math.Sqrt(r1) * spread;
                float bx = (float)Math.Cos(a) * rad, bz = (float)Math.Sin(a) * rad;
                double yaw = r2 * Math.PI * 2.0;
                float fx = (float)Math.Cos(yaw), fz = (float)Math.Sin(yaw);   // bıçağın bakış yönü (yüzey normali)
                float rx = -fz, rz = fx;                                       // sağ vektör
                float h = GrassRules.Lerp(hMin, hMax, r3);
                float w = GrassRules.Lerp(wMin, wMax, r4);
                float leanAmt = lean * (0.4f + r0 * 0.8f);                     // bıçak yönünde öne eğim

                int baseV = pos.Count / 3;
                for (var s = 0; s <= Segments; s++)
                {
                    float t = s / (float)Segments;
                    float width = w * (1f - t * t * 0.85f);  // uca doğru incelir
                    float forward = leanAmt * t * t * h;
                    float y = h * t;
                    float cx = bx + fx * forward, cz = bz + fz * forward;
                    bool tip = s == Segments;
                    if (tip)
                    {
                        // uç tek köşe
                        AddVertex(pos, nrm, uv, col, cx, y, cz, fx, fz, 0.5f, t, t * t, r4, 0.35f + 0.65f * t);
                    }
                    else
                    {
                        AddVertex(pos, nrm, uv, col, cx - rx * width, y, cz - rz * width, fx, fz, 0f, t, t * t, r4, 0.35f + 0.65f * t);
                        AddVertex(pos, nrm, uv, col, cx + rx * width, y, cz + rz * width, fx, fz, 1f, t, t * t, r4, 0.35f + 0.65f * t);
                    }
                }

                // satırlar: s=0,1 -> 2 köşe; s=2 -> 2 köşe; s=3 -> uç (1 köşe)
                for (var s = 0; s < Segments; s++)
                {
                    int i0 = baseV + s * 2;       // alt sol
                    int i1 = i0 + 1;              // alt sağ
                    if (s < Segments - 1)
                    {
                        int i2 = i0 + 2, i3 = i0 + 3;
                        idx.Add(i0); idx.Add(i2); idx.Add(i1);
                        idx.Add(i1); idx.Add(i2); idx.Add(i3);
                    }
                    else
                    {
                        int tipV = baseV + Segments * 2;
                        idx.Add(i0); idx.Add(tipV); idx.Add(i1);
                    }
                }

                float top = h;
                if (top > maxH) maxH = top;
                float reach = rad + w + leanAmt * h;
                if (reach > maxR) maxR = reach;
            }

            return new GrassBladeGeometry
            {
                Positions = pos.ToArray(), Normals = nrm.ToArray(), Uvs = uv.ToArray(), Colors = col.ToArray(),
                Indices = idx.ToArray(), MaxHeight = maxH, Radius = maxR
            };
        }

        private static void AddVertex(List<float> pos, List<float> nrm, List<float> uv, List<float> col,
            float x, float y, float z, float nx, float nz, float u, float v, float bend, float phase, float ao)
        {
            pos.Add(x); pos.Add(y); pos.Add(z);
            nrm.Add(nx); nrm.Add(0.25f); nrm.Add(nz);
            uv.Add(u); uv.Add(v);
            col.Add(bend); col.Add(phase); col.Add(ao); col.Add(1f);
        }
    }
}
