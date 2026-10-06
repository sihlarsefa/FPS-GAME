using NUnit.Framework;
using Project.Application.Credits;

namespace Project.Tests.EditMode
{
    public sealed class CreditsTests
    {
        private static LicenseRecord R(string id, string lic, string author = "A", string url = "http://x") =>
            new LicenseRecord { id = id, name = id, author = author, license = lic, url = url, folder = "Assets/ThirdParty/" + id };

        [Test]
        public void CcBy_RequiresAttribution_NcDoesNot()
        {
            Assert.IsTrue(CreditsLogic.IsAttributionRequired("CC-BY-4.0"));
            Assert.IsTrue(CreditsLogic.IsAttributionRequired("cc by 3.0"));
            Assert.IsFalse(CreditsLogic.IsAttributionRequired("CC-BY-NC-4.0"));
            Assert.IsFalse(CreditsLogic.IsAttributionRequired("CC0"));
        }

        [Test]
        public void CreditLine_ContainsParts()
        {
            var l = CreditsLogic.BuildCreditLine(R("Kirpi", "CC-BY-4.0", "Ali"));
            Assert.IsTrue(l.Contains("Kirpi"));
            Assert.IsTrue(l.Contains("Ali"));
            Assert.IsTrue(l.Contains("CC-BY-4.0"));
            Assert.IsNull(CreditsLogic.BuildCreditLine(new LicenseRecord()));
        }

        [Test]
        public void Audit_CatchesNcAndEditorial()
        {
            var f = new CreditsFile { records = new[] { R("a", "CC-BY-NC-4.0"), R("b", "Editorial"), R("c", "CC0") } };
            var findings = CreditsLogic.Audit(f);
            Assert.AreEqual(2, CreditsLogic.CountErrors(findings));
        }

        [Test]
        public void Audit_CcByWithoutAuthor_IsError()
        {
            var f = new CreditsFile { records = new[] { R("a", "CC-BY-4.0", "") } };
            Assert.AreEqual(1, CreditsLogic.CountErrors(CreditsLogic.Audit(f)));
        }

        [Test]
        public void Unregistered_FoldersDetected()
        {
            var f = new CreditsFile { records = new[] { R("Mixamo", "Mixamo") } };
            var miss = CreditsLogic.FindUnregisteredFolders(new[] { "Assets/ThirdParty/Mixamo", "Assets/ThirdParty/Foo" }, f);
            Assert.AreEqual(1, miss.Count);
            Assert.AreEqual("Assets/ThirdParty/Foo", miss[0]);
        }

        [Test]
        public void Lines_AttributionOnlyFilters()
        {
            var f = new CreditsFile { records = new[] { R("a", "CC-BY-4.0"), R("b", "CC0") } };
            Assert.AreEqual(1, CreditsLogic.BuildCreditLines(f, true).Count);
            Assert.AreEqual(2, CreditsLogic.BuildCreditLines(f, false).Count);
        }

#if UNITY_EDITOR
        [Test]
        public void Loader_ParsesJson_AndSurvivesGarbage()
        {
            var f = Project.Infrastructure.Content.CreditsLoader.Parse("{\"version\":1,\"records\":[{\"id\":\"x\",\"name\":\"X\",\"license\":\"CC0\"}]}");
            Assert.AreEqual(1, f.records.Length);
            Assert.AreEqual(0, Project.Infrastructure.Content.CreditsLoader.Parse("???").records.Length);
        }
#endif
    }
}
