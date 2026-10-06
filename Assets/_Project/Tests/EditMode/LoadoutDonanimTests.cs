using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Presentation.UI;
using UnityEngine;

namespace Project.Tests
{
    public sealed class LoadoutDonanimTests
    {
        [Test]
        public void SixBars_InRange_AndAdsSpeedReactsToScope()
        {
            Assert.AreEqual(LoadoutStats.BarCount, LoadoutStats.Labels.Length);
            var rifle = WeaponCatalog.Get(WeaponIds.Mpt76);
            var plain = LoadoutStats.Compute(rifle);
            var scoped = LoadoutStats.Compute(rifle, new[] { ItemIds.Scope4x });
            Assert.That(LoadoutStats.Value(plain, 5), Is.InRange(0f, 1f));
            Assert.Less(scoped.AdsSpeed, plain.AdsSpeed);
        }

        [TestCase(0.50f, 0.55f, 5, "+5")]
        [TestCase(0.50f, 0.47f, -3, "-3")]
        [TestCase(0.50f, 0.50f, 0, "")]
        public void DeltaText_FormatsSignedPoints(float before, float after, int points, string text)
        {
            var p = LoadoutStats.DeltaPoints(before, after);
            Assert.AreEqual(points, p);
            Assert.AreEqual(text, LoadoutStats.DeltaText(p));
        }

        [Test]
        public void AttachmentEffects_ListGoodBeforeBad()
        {
            var fx = LoadoutStats.AttachmentEffects(AttachmentCatalog.Get(ItemIds.Scope4x));
            Assert.IsTrue(fx.Contains("-NİŞAN"));
            Assert.AreEqual(string.Empty, LoadoutStats.AttachmentEffects(null));
        }

        [Test]
        public void TintColor_KeepsDarkPartsAndTintsBrightOnes()
        {
            var tint = new Color(0.4f, 0.5f, 0.2f);
            var dark = LoadoutPreview.TintColor(new Color(0.01f, 0.01f, 0.01f), tint);
            Assert.Less(Mathf.Abs(dark.r - 0.01f), 0.02f);
            var bright = LoadoutPreview.TintColor(Color.white, tint);
            Assert.Less(bright.b, bright.g);
        }
    }
}
