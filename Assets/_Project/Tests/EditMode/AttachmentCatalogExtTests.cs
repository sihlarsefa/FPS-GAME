#if UNITY_EDITOR
using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public class AttachmentCatalogExtTests
    {
        [Test]
        public void Genisletme_Eklentileri_Katalogda()
        {
            Assert.IsTrue(AttachmentCatalog.IsAttachment(AttachmentCatalog.Ids.Scope8x));
            Assert.AreEqual(8f, AttachmentCatalog.Get(AttachmentCatalog.Ids.Scope8x).AdsZoom, 0.001f);
            Assert.AreEqual(0.2f, AttachmentCatalog.Get(AttachmentCatalog.Ids.FlashHider).MuzzleFlashMultiplier, 0.001f);
            Assert.AreEqual(0.8f, AttachmentCatalog.Get(AttachmentCatalog.Ids.Compensator).HorizontalRecoilMultiplier, 0.001f);
            Assert.AreEqual(0.9f, AttachmentCatalog.Get(AttachmentCatalog.Ids.AngledGrip).HorizontalRecoilMultiplier, 0.001f);
        }

        [Test]
        public void DikeyKabza_SadeceDikeyTepmeyiAzaltir()
        {
            var g = AttachmentCatalog.Get(ItemIds.VerticalGrip);
            Assert.AreEqual(0.85f, g.VerticalRecoilMultiplier, 0.001f);
            Assert.AreEqual(1f, g.RecoilMultiplier, 0.001f);
        }

        [Test]
        public void EtkiEtiketleri_Uretilir()
        {
            Assert.IsTrue(AttachmentCatalog.ExtraEffectTags(AttachmentCatalog.Get(AttachmentCatalog.Ids.Compensator)).Contains("-SES"));
            Assert.IsTrue(AttachmentCatalog.ExtraEffectTags(null) == string.Empty);
        }
    }
}
#endif
