using NUnit.Framework;
using UnityEngine;

namespace Project.EditorTools.Tests
{
    public sealed class AudioImportRulesTests
    {
        private const string Root = "Assets/_Project/Resources/Audio/";

        // Voice, süresi bilinmeyen çağrıda bellek açısından güvenli CompressedInMemory kalır; gerçek politika süreye göre (aşağıdaki testler).
        [TestCase("Voice/v2/yelda/sakin/test.wav", AudioClipLoadType.CompressedInMemory, AudioCompressionFormat.Vorbis, 0.55f, true)]
        [TestCase("Weapons/ar_mpt76/shot_1.wav", AudioClipLoadType.DecompressOnLoad, AudioCompressionFormat.ADPCM, 0.7f, true)]
        [TestCase("Weapons/_class/762/shot_1.wav", AudioClipLoadType.DecompressOnLoad, AudioCompressionFormat.ADPCM, 0.7f, true)]
        [TestCase("SFX/Impact/dirt_1.wav", AudioClipLoadType.DecompressOnLoad, AudioCompressionFormat.Vorbis, 0.5f, true)]
        [TestCase("Ambience/forest/wind.ogg", AudioClipLoadType.Streaming, AudioCompressionFormat.Vorbis, 0.5f, false)]
        public void CategoryAppliesRequiredPolicy(string suffix, AudioClipLoadType load, AudioCompressionFormat format, float quality, bool mono)
        {
            Assert.That(AudioImportRules.TrySettings(Root + suffix, out var settings, out var actualMono), Is.True);
            Assert.That(settings.loadType, Is.EqualTo(load));
            Assert.That(settings.compressionFormat, Is.EqualTo(format));
            Assert.That(settings.quality, Is.EqualTo(quality));
            Assert.That(actualMono, Is.EqualTo(mono));
        }

        // Kısa replik/parça (<=3,5 sn) GetData ile örneklenip birleştirilir: DecompressOnLoad (diskte Vorbis) + veri klip ile yüklü gelir.
        // Uzun konuşma ya da süresi bilinmeyen: CompressedInMemory (doğrudan çalınır).
        [TestCase(0.24f, AudioClipLoadType.DecompressOnLoad, true)]
        [TestCase(1.32f, AudioClipLoadType.DecompressOnLoad, true)]
        [TestCase(3.5f, AudioClipLoadType.DecompressOnLoad, true)]
        [TestCase(3.51f, AudioClipLoadType.CompressedInMemory, false)]
        [TestCase(4.48f, AudioClipLoadType.CompressedInMemory, false)]
        [TestCase(-1f, AudioClipLoadType.CompressedInMemory, false)]
        public void VoiceLoadTypeFollowsDuration(float seconds, AudioClipLoadType load, bool preload)
        {
            Assert.That(AudioImportRules.TrySettings(Root + "Voice/v2/dfki_er/sakin/open_sak_01.ogg", seconds, out var settings, out var mono), Is.True);
            Assert.That(settings.loadType, Is.EqualTo(load));
            Assert.That(settings.compressionFormat, Is.EqualTo(AudioCompressionFormat.Vorbis));
            Assert.That(settings.preloadAudioData, Is.EqualTo(preload));
            Assert.That(settings.quality, Is.EqualTo(0.55f));
            Assert.That(mono, Is.True);
        }

        [TestCase("Assets/ThirdParty/Voice/test.wav")]
        [TestCase("Assets/_Project/Resources/Audio/VoiceBackup/test.wav")]
        [TestCase("Assets/_Project/Resources/Audio/Music/test.wav")]
        [TestCase(null)]
        public void UnrelatedClipsAreNotModified(string path)
        {
            Assert.That(AudioImportRules.TrySettings(path, out _, out _), Is.False);
            Assert.That(AudioImportRules.TrySettings(path, 1f, out _, out _), Is.False);
        }
    }
}
