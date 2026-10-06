using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.Rendering.Grading;

namespace Project.Tests.EditMode
{
    public sealed class GradingPresetsTests
    {
        [Test]
        public void Lerp_Endpoints_VeOrta()
        {
            var a = GradingPresets.Base(GradeStage.Gunduz);
            var b = GradingPresets.Base(GradeStage.Gece);
            Assert.AreEqual(a.Temperature, GradingPresets.Lerp(a, b, 0f).Temperature, 0.0001f);
            Assert.AreEqual(b.Temperature, GradingPresets.Lerp(a, b, 1f).Temperature, 0.0001f);
            Assert.AreEqual((a.Saturation + b.Saturation) * 0.5f, GradingPresets.Lerp(a, b, 0.5f).Saturation, 0.0001f);
            Assert.AreEqual(b.Gain.z, GradingPresets.Lerp(a, b, 5f).Gain.z, 0.0001f);
        }

        [Test]
        public void TumOnAyarlar_SinirlarIcinde()
        {
            string[] maps = { MapCatalog.Kuzgun, MapCatalog.AyazGecidi, MapCatalog.MaviLiman, MapCatalog.KartalYaylasi, "bilinmeyen" };
            for (var st = 0; st <= 5; st++)
                foreach (var m in maps)
                {
                    var s = GradingPresets.For(m, (GradeStage)st);
                    Assert.IsTrue(s.Temperature >= -100f && s.Temperature <= 100f);
                    Assert.IsTrue(s.Saturation >= -100f && s.Saturation <= 100f);
                    Assert.IsTrue(s.Contrast >= -100f && s.Contrast <= 100f);
                    Assert.IsTrue(s.Lift.x >= 0f && s.Lift.x <= 2f && s.Gain.z <= 2f);
                }
        }

        [Test]
        public void Clamp_AsiriDegerleriKirpar()
        {
            var s = GradingPresets.Base(GradeStage.Gunduz);
            s.Temperature = 500f; s.Saturation = -900f; s.Gain = new UnityEngine.Vector4(9f, -3f, 1f, 5f);
            var c = GradingPresets.Clamp(s);
            Assert.AreEqual(100f, c.Temperature, 0.0001f);
            Assert.AreEqual(-100f, c.Saturation, 0.0001f);
            Assert.AreEqual(2f, c.Gain.x, 0.0001f);
            Assert.AreEqual(0f, c.Gain.y, 0.0001f);
            Assert.AreEqual(1f, c.Gain.w, 0.0001f);
        }

        [Test]
        public void Lobi_MaviSaatKirmiziVurgu()
        {
            var l = GradingPresets.For(MapCatalog.Kuzgun, GradeStage.Lobi);
            Assert.Less(l.Temperature, 0f);
            Assert.Greater(l.SplitHighlights.r, l.SplitHighlights.b);
            Assert.Greater(l.SplitShadows.b, l.SplitShadows.r);
        }

        [Test]
        public void StageFor_VeHaritaOfseti()
        {
            Assert.IsTrue(GradingPresets.StageFor(TimeOfDay.Aksam) == GradeStage.AltinSaat);
            Assert.IsTrue(GradingPresets.StageFor(TimeOfDay.Gece) == GradeStage.Gece);
            var k = GradingPresets.For(MapCatalog.Kuzgun, TimeOfDay.Gunduz);
            var a = GradingPresets.For(MapCatalog.AyazGecidi, TimeOfDay.Gunduz);
            Assert.Less(a.Temperature, k.Temperature);
        }
    }
}
