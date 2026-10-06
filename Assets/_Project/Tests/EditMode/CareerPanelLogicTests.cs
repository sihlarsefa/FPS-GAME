using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Services;
using Project.Presentation.UI;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class CareerPanelLogicTests
    {
        private static Dictionary<string, WeaponCareer> Weapons() => new Dictionary<string, WeaponCareer>
        {
            { "a", new WeaponCareer { Shots = 100, Hits = 50, Kills = 10, Headshots = 5, LongestKillDm = 1000 } },
            { "b", new WeaponCareer { Shots = 40, Hits = 40, Kills = 20, Headshots = 2, LongestKillDm = 500 } },
            { "c", new WeaponCareer() },
        };

        [Test]
        public void EmptyWeaponsSkippedAndSortedByKills()
        {
            var rows = CareerPanelLogic.BuildWeaponRows(Weapons(), CareerWeaponSort.Kills, true);
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("b", rows[0].Id);
        }

        [Test]
        public void SortByHeadshotAndLongest()
        {
            Assert.AreEqual("a", CareerPanelLogic.BuildWeaponRows(Weapons(), CareerWeaponSort.HeadshotPercent, true)[0].Id);
            Assert.AreEqual("a", CareerPanelLogic.BuildWeaponRows(Weapons(), CareerWeaponSort.Longest, true)[0].Id);
            Assert.AreEqual("b", CareerPanelLogic.BuildWeaponRows(Weapons(), CareerWeaponSort.Accuracy, true)[0].Id);
            Assert.AreEqual("a", CareerPanelLogic.BuildWeaponRows(Weapons(), CareerWeaponSort.Kills, false)[0].Id);
        }

        [Test]
        public void RingFillAndTier()
        {
            var d = CareerService.DefaultMedals()[0]; // 25/150/600
            Assert.AreEqual(0f, CareerPanelLogic.RingFill(d, 0));
            Assert.AreEqual(0.5f, CareerPanelLogic.RingFill(d, 12), 0.05f);
            Assert.AreEqual(MedalTier.Gumus, CareerPanelLogic.ForValue(d, 150));
            Assert.AreEqual(1f, CareerPanelLogic.RingFill(d, 9999));
        }

        [Test]
        public void TooltipShowsThresholds()
        {
            var t = CareerPanelLogic.MedalTooltip(CareerService.DefaultMedals()[0], 30);
            StringAssert.Contains("Bronz 25", t);
            StringAssert.Contains("Altın 600", t);
            Assert.AreEqual(0f, CareerPanelLogic.Rate(1, 0));
        }
    }
}
