#if UNITY_EDITOR
using System;
using NUnit.Framework;
using Project.Infrastructure.World;

namespace Project.Tests.EditMode
{
    public sealed class TerrainErosionTests
    {
        private const int Res = 129;
        private const float Cell = 4f;

        private static float[] MakeMountain()
        {
            var h = new float[Res * Res];
            var c = (Res - 1) * 0.5f;
            for (var z = 0; z < Res; z++)
            for (var x = 0; x < Res; x++)
            {
                var dx = (x - c) / c; var dz = (z - c) / c;
                var r = (float)Math.Sqrt(dx * dx + dz * dz);
                h[z * Res + x] = 120f * Math.Max(0f, 1f - r) + 6f * (float)Math.Sin(x * 0.37) * (float)Math.Cos(z * 0.29) + 10f;
            }

            return h;
        }

        [Test]
        public void Erode_AyniTohumAyniSonuc_FarkliTohumFarkli()
        {
            var a = MakeMountain(); var b = MakeMountain(); var c = MakeMountain();
            var s = new TerrainErosionSettings { MaxDroplets = 8000 };
            TerrainErosion.Erode(a, Res, Cell, 0, 0, null, 7, s);
            TerrainErosion.Erode(b, Res, Cell, 0, 0, null, 7, s);
            TerrainErosion.Erode(c, Res, Cell, 0, 0, null, 8, s);
            CollectionAssert.AreEqual(a, b);
            var diff = 0; for (var i = 0; i < a.Length; i++) if (a[i] != c[i]) diff++;
            Assert.Greater(diff, 50);
        }

        [Test]
        public void Erode_DegisimSinirli_KorunanHucreDegismez_SonluDegerler()
        {
            var h = MakeMountain();
            var orig = (float[])h.Clone();
            var protect = new float[h.Length];
            for (var i = 0; i < Res * 20; i++) protect[i] = 1f; // ilk 20 satır korunur
            var s = new TerrainErosionSettings { MaxDroplets = 12000, MaxDelta = 2f };
            var maps = TerrainErosion.Erode(h, Res, Cell, 0, 0, protect, 3, s);
            var changed = 0;
            for (var i = 0; i < h.Length; i++)
            {
                Assert.IsFalse(float.IsNaN(h[i]) || float.IsInfinity(h[i]));
                Assert.LessOrEqual(Math.Abs(h[i] - orig[i]), 2.0001f);
                if (i < Res * 20) Assert.AreEqual(orig[i], h[i], 1e-5f);
                else if (Math.Abs(h[i] - orig[i]) > 1e-4f) changed++;
            }

            Assert.Greater(changed, 100);
            Assert.AreEqual(h.Length, maps.Flow.Length);
            foreach (var f in maps.Flow) Assert.IsTrue(f >= 0f && f <= 1.0001f);
            foreach (var cv in maps.Curvature) Assert.IsTrue(cv >= -1.0001f && cv <= 1.0001f);
        }

        [Test]
        public void Erode_GucSifirIken_YukseklikAyni()
        {
            var h = MakeMountain();
            var orig = (float[])h.Clone();
            TerrainErosion.Erode(h, Res, Cell, 0, 0, null, 1, new TerrainErosionSettings { Strength = 0f });
            CollectionAssert.AreEqual(orig, h);
        }

        [Test]
        public void Erode_ButcesiSinirli_Hizli()
        {
            var h = new float[513 * 513];
            for (var i = 0; i < h.Length; i++) h[i] = 40f + 30f * (float)Math.Sin((i % 513) * 0.05) + (i / 513) * 0.1f;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            TerrainErosion.Erode(h, 513, 2f, 0, 0, null, 5, TerrainErosionSettings.ForTier(3));
            sw.Stop();
            Assert.Less(sw.ElapsedMilliseconds, 6000, "Ultra aşındırma bütçesi (test makinesi toleranslı)");
        }

        [Test]
        public void IsilAsinma_DikBasamagi_Yumusatir()
        {
            var h = new float[Res * Res];
            for (var z = 0; z < Res; z++)
            for (var x = 0; x < Res; x++) h[z * Res + x] = x < Res / 2 ? 40f : 0f; // 40 m basamak
            var s = new TerrainErosionSettings { MaxDroplets = 0, ThermalIterations = 6, MaxDelta = 20f };
            TerrainErosion.Erode(h, Res, 2f, 0, 0, null, 1, s);
            var mid = Res / 2;
            Assert.Less(h[10 * Res + mid - 1], 40f);
            Assert.Greater(h[10 * Res + mid], 0f);
        }

        [Test]
        public void ProtectValue_YolDereYerlesimSuKenar()
        {
            const float inf = float.PositiveInfinity;
            Assert.AreEqual(1f, TerrainErosion.ProtectValue(0.9f, inf, 500f, 3f, 80f, 2f, 50), 1e-4f); // yerleşim
            Assert.AreEqual(1f, TerrainErosion.ProtectValue(0f, 0f, 500f, 3f, 80f, 2f, 50), 1e-4f);   // yol ekseni
            Assert.AreEqual(0f, TerrainErosion.ProtectValue(0f, 40f, 500f, 3f, 80f, 2f, 50), 1e-4f);  // serbest
            Assert.AreEqual(1f, TerrainErosion.ProtectValue(0f, inf, 1f, 3f, 80f, 2f, 50), 1e-4f);    // dere
            Assert.AreEqual(1f, TerrainErosion.ProtectValue(0f, inf, 500f, 3f, 2.5f, 2f, 50), 1e-4f); // su hattı
            Assert.AreEqual(1f, TerrainErosion.ProtectValue(0f, inf, 500f, 3f, 80f, 2f, 0), 1e-4f);   // kenar
        }

        [Test]
        public void ScreeVeCliff_Monoton()
        {
            Assert.Less(TerrainErosion.CliffWeight(20f), 0.01f);
            Assert.Greater(TerrainErosion.CliffWeight(55f), 0.99f);
            Assert.Greater(TerrainErosion.ScreeWeight(0.6f, 0f, 20f), 0.9f);
            Assert.AreEqual(0f, TerrainErosion.ScreeWeight(0.6f, 1f, 20f), 1e-4f); // uçurumun kendisi scree değil
            Assert.AreEqual(0f, TerrainErosion.ScreeWeight(0f, 0f, 20f), 1e-4f);
        }

        [Test]
        public void BoyamaKurallari_Sinirli()
        {
            Assert.Greater(TerrainPaintRules.GullyMud(0.9f, 4f, -0.5f), 0.3f);
            Assert.Less(TerrainPaintRules.GullyMud(0.9f, 35f, -0.5f), 0.01f);
            Assert.Greater(TerrainPaintRules.GullyGravel(0.9f, 14f, -0.5f), 0.1f);
            Assert.Greater(TerrainPaintRules.RidgeRock(0.9f, 30f), 0.5f);
            Assert.Less(TerrainPaintRules.RidgeRock(-0.9f, 30f), 0.01f);
            Assert.LessOrEqual(TerrainPaintRules.ScreeGravel(2f, 1f), 0.66f);
        }

        [Test]
        public void ColorMap_AraliklarVeYolIzi()
        {
            for (var i = -3; i <= 3; i++)
            {
                var v = i / 3f;
                TerrainColorMapMath.Evaluate(v, -v, v, v, 1f, 1f, 1f, 0f, 2, v, out var r, out var g, out var b, out var ao);
                Assert.IsTrue(r >= TerrainColorMapMath.TintMin && r <= TerrainColorMapMath.TintMax);
                Assert.IsTrue(g >= TerrainColorMapMath.TintMin && g <= TerrainColorMapMath.TintMax);
                Assert.IsTrue(b >= TerrainColorMapMath.TintMin && b <= TerrainColorMapMath.TintMax);
                Assert.IsTrue(ao >= TerrainColorMapMath.AoMin && ao <= 1f);
            }

            Assert.AreEqual(1f, TerrainColorMapMath.RoadDarkening(float.PositiveInfinity, 2, 0f));
            Assert.AreEqual(1f, TerrainColorMapMath.RoadDarkening(30f, 2, 0f), 1e-4f);
            Assert.Less(TerrainColorMapMath.RoadDarkening(-0.8f, 2, 1f), 0.95f); // toprak yolda iz
            Assert.Greater(TerrainColorMapMath.RoadDarkening(-0.8f, 1, 1f), 0.9f); // asfaltta çok hafif
        }
    }
}
#endif
