using NUnit.Framework;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public class MapGradeTests
    {
        [Test]
        public void Maps_HaveDistinctTemperature()
        {
            var k = MapGradeTable.For(MapCatalog.Kuzgun);
            var a = MapGradeTable.For(MapCatalog.AyazGecidi);
            var l = MapGradeTable.For(MapCatalog.MaviLiman);
            Assert.Greater(k.Temperature, 0f);
            Assert.Less(a.Temperature, 0f);
            Assert.Greater(l.Saturation, k.Saturation);
        }

        [Test]
        public void UnknownMap_FallsBackToKuzgun()
        {
            Assert.AreEqual(MapGradeTable.For(MapCatalog.Kuzgun).Temperature, MapGradeTable.For("???").Temperature);
        }

        [Test]
        public void Kelvin_WarmIsRedderThanCold()
        {
            var warm = MapGradeTable.KelvinToRgb(3500f);
            var cold = MapGradeTable.KelvinToRgb(9000f);
            Assert.Greater(warm[0], warm[2]);
            Assert.Greater(cold[2], cold[0] - 0.01f);
            Assert.GreaterOrEqual(warm[1], 0f);
            Assert.LessOrEqual(warm[0], 1f);
        }

        [Test]
        public void AdsBlur_OffIsFar_OnIsNear()
        {
            Assert.AreEqual(1000f, MapGradeTable.AdsBlurEnd(0f));
            Assert.Less(MapGradeTable.AdsBlurEnd(1f), 2f);
        }
    }
}
