using System.Collections.Generic;
using NUnit.Framework;
using Project.Infrastructure.Audio.Foley;

namespace Project.Tests
{
    public sealed class ClipCoverageReportTests
    {
        private static Dictionary<string, HashSet<string>> Idx() => ClipCoverageReport.IndexFolders(new[]
        {
            "Audio/Weapons/_class/rifle556/bang_1.wav",
            "Audio/Weapons/_class/rifle556/tail_outdoor_2.wav",
            "Audio/Weapons/_class/rifle556/bang_1.wav.meta",
            "Audio/Weapons/ak/supp_1.ogg",
            "Audio\\Weapons\\ak\\thump_1.wav",
        });

        [Test]
        public void ClassAndWeaponSources()
        {
            var i = Idx();
            Assert.AreEqual(ClipSource.Class, ClipCoverageReport.SourceOf(i, "ak", "rifle556", "bang"));
            Assert.AreEqual(ClipSource.Weapon, ClipCoverageReport.SourceOf(i, "ak", "rifle556", "supp"));
            Assert.AreEqual(ClipSource.Weapon, ClipCoverageReport.SourceOf(i, "ak", "rifle556", "thump"));
            Assert.AreEqual(ClipSource.None, ClipCoverageReport.SourceOf(i, "ak", "rifle556", "mech"));
        }

        [Test]
        public void MetaIgnoredAndRowFormatted()
        {
            var i = Idx();
            Assert.AreEqual(2, i["Audio/Weapons/_class/rifle556"].Count);
            StringAssert.StartsWith("ak | sınıf | - | silah | sınıf | - | silah | gerçek+prosedürel",
                ClipCoverageReport.FormatRow(i, "ak", "rifle556"));
            StringAssert.EndsWith("prosedürel", ClipCoverageReport.FormatRow(i, "x", "none"));
        }

        [Test]
        public void EmptyLayersFlagged()
        {
            var e = ClipCoverageReport.EmptyLayers(Idx(), new[] { new KeyValuePair<string, string>("ak", "rifle556") });
            CollectionAssert.AreEqual(new[] { "mech", "distant" }, e);
        }
    }
}
