#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Audio;

namespace Project.Tests.EditMode
{
    public class VoiceV2ResolverTests
    {
        [Test]
        public void Path_IsBuilt()
        {
            Assert.AreEqual("Audio/Voice/v2/yelda/panik/contact_front",
                VoiceV2Resolver.ResourcePath("yelda", VoiceStress.Panik, "contact_front"));
            Assert.AreEqual("Audio/Voice/contact_front", VoiceV2Resolver.LegacyPath("contact_front"));
        }

        [Test]
        public void Speaker_IsStableAndNegativeSafe()
        {
            Assert.AreEqual(VoiceV2Resolver.VoiceForSpeaker(7), VoiceV2Resolver.VoiceForSpeaker(7));
            Assert.IsNotNull(VoiceV2Resolver.VoiceForSpeaker(-13));
        }

        [Test]
        public void Stress_Escalates()
        {
            Assert.AreEqual(VoiceStress.Sakin, VoiceV2Resolver.StressFor(1f, 0f, false, false));
            Assert.AreEqual(VoiceStress.Catisma, VoiceV2Resolver.StressFor(1f, 0f, true, false));
            Assert.AreEqual(VoiceStress.Panik, VoiceV2Resolver.StressFor(0.2f, 0.8f, true, false));
        }
    }
}
#endif
