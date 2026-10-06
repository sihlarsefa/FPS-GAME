#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Dialogue;
using Project.EditorTools;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Audio.Dialogue;
using UnityEngine;

namespace Project.Tests.EditMode
{
    /// <summary>
    /// Başlangıç takılması düzeltmesi (CompressedInMemory ses klibi + GetData uyarı seli): saf mantık testleri.
    /// LRU önbellek, örnekleme koruması (DecompressOnLoad + yüklü), tek özet satırı, doğrudan çalma perdesi,
    /// içe aktarma politikası (Voice &lt;=3,5 sn = DecompressOnLoad) ve WAV/OGG süre ayrıştırıcıları.
    /// </summary>
    [TestFixture]
    public sealed class VoiceClipPolicyTests
    {
        // ------------------------------------------------------------------ LRU

        [Test]
        public void Lru_NeverExceedsCapacity_AndKeepsMostRecent()
        {
            var cache = new LruCache<string, int>(24);
            for (var i = 0; i < 100; i++)
            {
                cache.Set("k" + i, i);
                Assert.LessOrEqual(cache.Count, 24);
            }

            Assert.AreEqual(24, cache.Count);
            for (var i = 0; i < 76; i++)
                Assert.IsFalse(cache.Contains("k" + i), "k" + i + " atılmış olmalı");
            for (var i = 76; i < 100; i++)
                Assert.IsTrue(cache.Contains("k" + i), "k" + i + " kalmalı");
        }

        [Test]
        public void Lru_TryGetPromotes_SoEvictionHitsTheLeastRecentlyUsed()
        {
            var cache = new LruCache<string, int>(3);
            cache.Set("a", 1);
            cache.Set("b", 2);
            cache.Set("c", 3);

            Assert.IsTrue(cache.TryGet("a", out var a));
            Assert.AreEqual(1, a);

            cache.Set("d", 4); // en eski artık b
            Assert.IsFalse(cache.Contains("b"));
            Assert.IsTrue(cache.Contains("a"));
            Assert.IsTrue(cache.Contains("c"));
            Assert.IsTrue(cache.Contains("d"));
        }

        [Test]
        public void Lru_SetReplacesValueAndPromotes()
        {
            var cache = new LruCache<string, int>(2);
            cache.Set("a", 1);
            cache.Set("b", 2);
            cache.Set("a", 10); // a en yeni; değer değişti, sayı artmadı
            Assert.AreEqual(2, cache.Count);

            cache.Set("c", 3); // b atılır
            Assert.IsFalse(cache.Contains("b"));
            Assert.IsTrue(cache.TryGet("a", out var a));
            Assert.AreEqual(10, a);
        }

        [Test]
        public void Lru_RemoveClearAndMissingKey()
        {
            var cache = new LruCache<string, int>(4);
            cache.Set("a", 1);
            cache.Set("b", 2);
            Assert.IsTrue(cache.Remove("a"));
            Assert.IsFalse(cache.Remove("a"));
            Assert.IsFalse(cache.TryGet("a", out var none));
            Assert.AreEqual(0, none);
            Assert.AreEqual(1, cache.Count);

            cache.Clear();
            Assert.AreEqual(0, cache.Count);
            Assert.IsFalse(cache.Contains("b"));
        }

        [Test]
        public void Lru_CapacityClampsToAtLeastOne()
        {
            var cache = new LruCache<string, int>(0);
            Assert.AreEqual(1, cache.Capacity);
            cache.Set("a", 1);
            cache.Set("b", 2);
            Assert.AreEqual(1, cache.Count);
            Assert.IsTrue(cache.Contains("b"));
        }

        // ------------------------------------------------------------------ koruma

        [Test]
        public void Guard_ReadableOnlyWhenDecompressOnLoadAndLoaded()
        {
            Assert.AreEqual(ClipReadVerdict.Readable,
                VoiceClipGuard.Evaluate(AudioClipLoadType.DecompressOnLoad, AudioDataLoadState.Loaded, 1000));
            Assert.AreEqual(ClipReadVerdict.NotDecompressOnLoad,
                VoiceClipGuard.Evaluate(AudioClipLoadType.CompressedInMemory, AudioDataLoadState.Loaded, 1000));
            Assert.AreEqual(ClipReadVerdict.NotDecompressOnLoad,
                VoiceClipGuard.Evaluate(AudioClipLoadType.Streaming, AudioDataLoadState.Loaded, 1000));
            Assert.AreEqual(ClipReadVerdict.NotDecompressOnLoad,
                VoiceClipGuard.Evaluate(AudioClipLoadType.CompressedInMemory, AudioDataLoadState.Unloaded, 1000));
        }

        [TestCase(AudioDataLoadState.Unloaded)]
        [TestCase(AudioDataLoadState.Loading)]
        [TestCase(AudioDataLoadState.Failed)]
        public void Guard_DecompressOnLoadButNotLoaded_IsNotReadableYet(AudioDataLoadState state)
        {
            Assert.AreEqual(ClipReadVerdict.NotLoaded, VoiceClipGuard.Evaluate(AudioClipLoadType.DecompressOnLoad, state, 1000));
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void Guard_EmptyClip(int samples)
        {
            Assert.AreEqual(ClipReadVerdict.Empty, VoiceClipGuard.Evaluate(AudioClipLoadType.DecompressOnLoad, AudioDataLoadState.Loaded, samples));
        }

        // ------------------------------------------------------------------ tek özet satırı

        [Test]
        public void Diagnostics_ReturnsASingleSummaryLine_AndCountsDistinctClips()
        {
            VoiceClipDiagnostics.Reset();
            try
            {
                var first = VoiceClipDiagnostics.Note("Audio/Voice/v2/dfki_er/sakin/open_sak_01", ClipReadVerdict.NotDecompressOnLoad);
                Assert.IsNotNull(first);
                StringAssert.Contains("open_sak_01", first);
                StringAssert.Contains("DecompressOnLoad", first);
                StringAssert.DoesNotContain("\n", first);

                Assert.IsNull(VoiceClipDiagnostics.Note("Audio/Voice/v2/dfki_er/sakin/open_sak_01", ClipReadVerdict.NotDecompressOnLoad));
                Assert.IsNull(VoiceClipDiagnostics.Note("Audio/Voice/v2/yelda/panik/clk_03", ClipReadVerdict.NotDecompressOnLoad));
                Assert.IsNull(VoiceClipDiagnostics.Note(null, ClipReadVerdict.ReadFailed));

                Assert.AreEqual(3, VoiceClipDiagnostics.MismatchCount); // 2 yol + "?"
                Assert.IsTrue(VoiceClipDiagnostics.Reported);
            }
            finally
            {
                VoiceClipDiagnostics.Reset();
            }

            Assert.AreEqual(0, VoiceClipDiagnostics.MismatchCount);
            Assert.IsFalse(VoiceClipDiagnostics.Reported);
        }

        // ------------------------------------------------------------------ kütüphane (Unity yerel köprüsüne dokunmayan yollar)

        [Test]
        public void Library_CacheIsSmall()
        {
            Assert.LessOrEqual(DialogueClipLibrary.CacheCapacity, 24);
            Assert.GreaterOrEqual(DialogueClipLibrary.CacheCapacity, 8);
            DialogueClipLibrary.ClearCaches();
            Assert.AreEqual(0, DialogueClipLibrary.CachedCount);
        }

        [Test]
        public void Library_EmptyIdsAndPartsNeverLoadAnything()
        {
            Assert.IsNull(DialogueClipLibrary.Resolve(null));
            Assert.IsNull(DialogueClipLibrary.Resolve(string.Empty, 3, VoiceStress.Panik));
            Assert.IsNull(DialogueClipLibrary.GetSamples(string.Empty));
            Assert.IsNull(DialogueClipLibrary.GetSamples(null, 1, VoiceStress.Sakin));
            Assert.IsFalse(DialogueClipLibrary.HasClip(null));
            Assert.IsNull(DialogueClipLibrary.StitchParts(null, 0.05f, 1f));
            Assert.IsNull(DialogueClipLibrary.StitchParts(new List<string>(), 0.05f, 1f, 5, VoiceStress.Sakin));
            Assert.AreEqual(0, DialogueClipLibrary.CachedCount);
        }

        [Test]
        public void VoiceSamples_DirectFlagFollowsDirectClip()
        {
            var sampled = new VoiceSamples { Data = new float[4], SampleRate = 22050 };
            Assert.IsFalse(sampled.IsDirect);
            Assert.IsNull(sampled.DirectClip);
        }

        [Test]
        public void DirectPitch_MatchesTheIdentityResampleRatio()
        {
            var neutral = new VoiceIdentity(0, "n", 1f, 1f, 1f, 1f);
            Assert.AreEqual(1f, DialogueClipLibrary.DirectPitch(neutral), 0.0001f);

            var id = new VoiceIdentity(1, "x", 0.9f, 1f, 1.1f, 1f);
            var pitch = DialogueClipLibrary.DirectPitch(id);
            var input = new float[1000];
            for (var i = 0; i < input.Length; i++)
                input[i] = 0.5f;
            var processed = VoiceDsp.ApplyIdentity(input, 22050, id);
            Assert.AreEqual(input.Length / pitch, processed.Length, 1.5f, "doğrudan perde, ApplyIdentity yeniden örnekleme oranıyla aynı olmalı");

            Assert.AreEqual(2f, DialogueClipLibrary.DirectPitch(new VoiceIdentity(2, "hi", 3f, 1f, 1f, 1f)), 0.0001f);
            Assert.AreEqual(0.5f, DialogueClipLibrary.DirectPitch(new VoiceIdentity(3, "lo", 0.1f, 1f, 1f, 1f)), 0.0001f);
        }

        // ------------------------------------------------------------------ içe aktarma politikası (saf)

        [TestCase(0.2f, AudioClipLoadType.DecompressOnLoad)]
        [TestCase(1.32f, AudioClipLoadType.DecompressOnLoad)]
        [TestCase(3.5f, AudioClipLoadType.DecompressOnLoad)]
        [TestCase(3.51f, AudioClipLoadType.CompressedInMemory)]
        [TestCase(4.48f, AudioClipLoadType.CompressedInMemory)]
        [TestCase(60f, AudioClipLoadType.CompressedInMemory)]
        [TestCase(-1f, AudioClipLoadType.CompressedInMemory)]
        public void ImportPolicy_VoiceLoadTypeFollowsDuration(float seconds, AudioClipLoadType expected)
        {
            Assert.AreEqual(expected, AudioImportRules.VoiceLoadType(seconds));
        }

        [Test]
        public void ImportPolicy_UnknownOrNaNDurationStaysCompressed()
        {
            Assert.AreEqual(AudioClipLoadType.CompressedInMemory, AudioImportRules.VoiceLoadType(float.NaN));
            Assert.AreEqual(3.5f, AudioImportRules.VoiceDecompressMaxSeconds, 0.0001f);
        }

        [Test]
        public void ImportPolicy_VoicePathDetection()
        {
            Assert.IsTrue(AudioImportRules.IsVoicePath("Assets/_Project/Resources/Audio/Voice/v2/yelda/sakin/x.ogg"));
            Assert.IsTrue(AudioImportRules.IsVoicePath("Assets\\_Project\\Resources\\Audio\\Voice\\ack_roger.wav"));
            Assert.IsFalse(AudioImportRules.IsVoicePath("Assets/_Project/Resources/Audio/VoiceBackup/x.wav"));
            Assert.IsFalse(AudioImportRules.IsVoicePath("Assets/ThirdParty/Voice/x.wav"));
            Assert.IsFalse(AudioImportRules.IsVoicePath(null));
        }

        [Test]
        public void ImportPolicy_ShortVoiceIsDecompressVorbisPreloaded_LongVoiceIsCompressed()
        {
            const string path = "Assets/_Project/Resources/Audio/Voice/v2/dfki_er/sakin/open_sak_01.ogg";

            Assert.IsTrue(AudioImportRules.TrySettings(path, 1.2f, out var shortSettings, out var shortMono));
            Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, shortSettings.loadType);
            Assert.AreEqual(AudioCompressionFormat.Vorbis, shortSettings.compressionFormat);
            Assert.IsTrue(shortSettings.preloadAudioData);
            Assert.IsTrue(shortMono);

            Assert.IsTrue(AudioImportRules.TrySettings(path, 4.4f, out var longSettings, out _));
            Assert.AreEqual(AudioClipLoadType.CompressedInMemory, longSettings.loadType);
            Assert.AreEqual(AudioCompressionFormat.Vorbis, longSettings.compressionFormat);
            Assert.IsFalse(longSettings.preloadAudioData);

            // Süresiz (eski) çağrı: bellek açısından güvenli taraf.
            Assert.IsTrue(AudioImportRules.TrySettings(path, out var unknown, out _));
            Assert.AreEqual(AudioClipLoadType.CompressedInMemory, unknown.loadType);
        }

        [Test]
        public void ImportPolicy_OtherCategoriesAreUnaffectedByDuration()
        {
            Assert.IsTrue(AudioImportRules.TrySettings("Assets/_Project/Resources/Audio/Weapons/x/shot_1.wav", 9f, out var weapon, out _));
            Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, weapon.loadType);
            Assert.IsTrue(AudioImportRules.TrySettings("Assets/_Project/Resources/Audio/Ambience/x/wind.ogg", 0.5f, out var amb, out _));
            Assert.AreEqual(AudioClipLoadType.Streaming, amb.loadType);
            Assert.IsFalse(AudioImportRules.TrySettings("Assets/_Project/Resources/Audio/Music/x.wav", 1f, out _, out _));
        }

        // ------------------------------------------------------------------ süre ayrıştırıcıları

        [Test]
        public void WavDuration_PcmMono22k()
        {
            // 22.05 kHz, 16 bit, mono: 44100 bayt/sn. 66150 bayt = 1.5 sn.
            var header = WavHeader(22050, 1, 16, 66150, false, false);
            Assert.IsTrue(AudioFileDuration.TryParseWav(header, header.Length, header.Length + 66150, out var seconds));
            Assert.AreEqual(1.5f, seconds, 0.001f);
        }

        [Test]
        public void WavDuration_StreamedZeroDataSizeUsesFileLength_AndSkipsExtraChunks()
        {
            var header = WavHeader(44100, 2, 16, 176400, true, true); // 176400 bayt/sn: 1 sn
            Assert.IsTrue(AudioFileDuration.TryParseWav(header, header.Length, header.Length + 176400, out var seconds));
            Assert.AreEqual(1f, seconds, 0.001f);
        }

        [Test]
        public void WavDuration_RejectsGarbage()
        {
            Assert.IsFalse(AudioFileDuration.TryParseWav(null, 0, 0, out _));
            Assert.IsFalse(AudioFileDuration.TryParseWav(new byte[20], 20, 20, out _));
            var noData = WavHeader(22050, 1, 16, 0, false, false);
            Assert.IsFalse(AudioFileDuration.TryParseWav(noData, 36, 36, out _), "data parçası yok");
        }

        [Test]
        public void OggDuration_FromLastPageGranule()
        {
            var first = OggFirstPage(22050, 1, "vorbis");
            var tail = Concat(OggPage(11025, 4), OggPage(33075, 6));
            Assert.IsTrue(AudioFileDuration.TryParseOgg(first, first.Length, tail, tail.Length, out var seconds));
            Assert.AreEqual(1.5f, seconds, 0.0005f); // 33075 / 22050
        }

        [Test]
        public void OggDuration_SkipsLastPageWithoutCompletedPacket()
        {
            var first = OggFirstPage(44100, 1, "vorbis");
            var tail = Concat(OggPage(44100, 4), OggPage(-1, 4)); // son sayfa granule = -1
            Assert.IsTrue(AudioFileDuration.TryParseOgg(first, first.Length, tail, tail.Length, out var seconds));
            Assert.AreEqual(1f, seconds, 0.0005f);
        }

        [Test]
        public void OggDuration_RejectsNonVorbisAndGarbage()
        {
            var opus = OggFirstPage(48000, 1, "OpusHd");
            var tail = OggPage(48000, 4);
            Assert.IsFalse(AudioFileDuration.TryParseOgg(opus, opus.Length, tail, tail.Length, out _));
            Assert.IsFalse(AudioFileDuration.TryParseOgg(null, 0, tail, tail.Length, out _));
            Assert.IsFalse(AudioFileDuration.TryParseOgg(new byte[40], 40, tail, tail.Length, out _));

            var vorbis = OggFirstPage(22050, 1, "vorbis");
            Assert.IsFalse(AudioFileDuration.TryParseOgg(vorbis, vorbis.Length, new byte[100], 100, out _), "son sayfa bulunamadı");
        }

        // ---- sentetik başlıklar

        private static byte[] WavHeader(int sampleRate, int channels, int bits, int dataBytes, bool zeroDataSize, bool extraChunk)
        {
            var b = new List<byte>();
            Str(b, "RIFF");
            U32(b, (uint)(36 + dataBytes));
            Str(b, "WAVE");
            Str(b, "fmt ");
            U32(b, 16);
            U16(b, 1);
            U16(b, (ushort)channels);
            U32(b, (uint)sampleRate);
            U32(b, (uint)(sampleRate * channels * bits / 8));
            U16(b, (ushort)(channels * bits / 8));
            U16(b, (ushort)bits);
            if (extraChunk)
            {
                Str(b, "LIST");
                U32(b, 5);
                Str(b, "abcde");
                b.Add(0); // tek boyutlu parça dolgusu
            }

            Str(b, "data");
            U32(b, zeroDataSize ? 0u : (uint)dataBytes);
            return b.ToArray();
        }

        private static byte[] OggFirstPage(int sampleRate, int channels, string codecTag)
        {
            var b = new List<byte>();
            Str(b, "OggS");
            b.Add(0);
            b.Add(2); // BOS
            for (var i = 0; i < 8; i++) b.Add(0); // granule 0
            U32(b, 1); U32(b, 0); U32(b, 0); // seri, sıra, crc
            b.Add(1);   // 1 segment
            b.Add(30);  // segment uzunluğu
            b.Add(1);   // paket türü: tanıtım
            Str(b, codecTag);
            U32(b, 0);  // sürüm
            b.Add((byte)channels);
            U32(b, (uint)sampleRate);
            U32(b, 0); U32(b, 0); U32(b, 0); // bitrate max/nominal/min
            b.Add(0xB8);
            b.Add(1);   // framing
            return b.ToArray();
        }

        private static byte[] OggPage(long granule, int payload)
        {
            var b = new List<byte>();
            Str(b, "OggS");
            b.Add(0);
            b.Add(0);
            for (var i = 0; i < 8; i++) b.Add((byte)(granule >> (8 * i)));
            U32(b, 1); U32(b, 1); U32(b, 0);
            b.Add(1);
            b.Add((byte)payload);
            for (var i = 0; i < payload; i++) b.Add(0x55);
            return b.ToArray();
        }

        private static byte[] Concat(byte[] a, byte[] c)
        {
            var r = new byte[a.Length + c.Length];
            System.Array.Copy(a, 0, r, 0, a.Length);
            System.Array.Copy(c, 0, r, a.Length, c.Length);
            return r;
        }

        private static void Str(List<byte> b, string s)
        {
            for (var i = 0; i < s.Length; i++) b.Add((byte)s[i]);
        }

        private static void U32(List<byte> b, uint v)
        {
            b.Add((byte)v); b.Add((byte)(v >> 8)); b.Add((byte)(v >> 16)); b.Add((byte)(v >> 24));
        }

        private static void U16(List<byte> b, ushort v)
        {
            b.Add((byte)v); b.Add((byte)(v >> 8));
        }
    }
}
#endif
