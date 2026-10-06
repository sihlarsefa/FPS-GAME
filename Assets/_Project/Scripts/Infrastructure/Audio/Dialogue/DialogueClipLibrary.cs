using System;
using System.Collections.Generic;
using Project.Application.Dialogue;
using UnityEngine;

namespace Project.Infrastructure.Audio.Dialogue
{
    /// <summary>
    /// Ham örnek verisi + örnekleme hızı. <see cref="DirectClip"/> doluysa klip örneklenemedi (DecompressOnLoad değil):
    /// <see cref="Data"/> boştur ve klip olduğu gibi (birleştirme / kimlik DSP'si / telsiz DSP'si olmadan) çalınır.
    /// </summary>
    public sealed class VoiceSamples
    {
        public float[] Data;
        public int SampleRate;
        public bool Procedural;
        public AudioClip DirectClip;

        public bool IsDirect => DirectClip != null;
    }

    /// <summary>
    /// Diyalog v2 kaynakları: replik kitabı (Resources/Audio/dialogue_lines_v2.csv) ve ses klipleri
    /// (Resources/Audio/Voice/v2/&lt;ses&gt;/&lt;stres&gt;/&lt;id&gt;). Klip yoksa null döner; çağıran prosedürel yedeğe düşer.
    /// <para>
    /// Tembel + sınırlı: klip ilk kullanımda yüklenir, ≤ <see cref="CacheCapacity"/> girdilik LRU'da tutulur; ses bankı asla toplu yüklenmez.
    /// Örnekleme (<c>GetData</c>) YALNIZ <see cref="VoiceClipGuard"/> "okunabilir" derse yapılır (DecompressOnLoad + veri yüklü).
    /// Aksi hâlde birleştirme atlanır ve klip <see cref="VoiceSamples.DirectClip"/> olarak doğrudan çalınır: klip başına Unity uyarısı yok,
    /// zorla açma yok; yalnız tek özet satırı (<see cref="VoiceClipDiagnostics"/>).
    /// </para>
    /// </summary>
    public static class DialogueClipLibrary
    {
        public const string VoiceFolder = "Audio/Voice/v2/";
        public const int FallbackSampleRate = 22050;

        /// <summary>Bellekte tutulan en çok klip (LRU). Telsiz oynatıcısı da aynı sınırı kullanır.</summary>
        public const int CacheCapacity = 24;

        private const int MissingLimit = 2048;

        private sealed class Entry
        {
            /// <summary>Örneklenebilen klip: Data dolu. Örneklenemeyen klip: DirectClip dolu.</summary>
            public VoiceSamples Source;
            public ClipReadVerdict Verdict;
        }

        private static DialogueLineBook _book;
        private static readonly LruCache<string, Entry> Cache = new LruCache<string, Entry>(CacheCapacity, StringComparer.Ordinal);
        private static readonly HashSet<string> Missing = new HashSet<string>(StringComparer.Ordinal);

        public static DialogueLineBook Book
        {
            get
            {
                if (_book == null)
                    _book = LoadBook();
                return _book;
            }
        }

        public static void ReloadBook() => _book = LoadBook();

        private static DialogueLineBook LoadBook()
        {
            try
            {
                var asset = Resources.Load<TextAsset>(DialogueLineBook.ResourcePath);
                return DialogueLineBook.FromCsv(asset != null ? asset.text : null);
            }
            catch (Exception)
            {
                return new DialogueLineBook();
            }
        }

        /// <summary>Önbellekteki klip sayısı (her zaman ≤ <see cref="CacheCapacity"/>; teşhis/test).</summary>
        public static int CachedCount => Cache.Count;

        /// <summary>Klip var mı (tembel: yalnız bu klibi yükler; ses bankını taramaz).</summary>
        public static bool HasClip(string id)
        {
            return Resolve(id) != null;
        }

        /// <summary>Klipten örnekleri okur (sessizlik kırpılmış). Yoksa / örneklenemiyorsa null (örneklenemeyen klip için <see cref="Resolve(string)"/>).</summary>
        public static VoiceSamples GetSamples(string id)
        {
            return SamplesOnly(Resolve(id));
        }

        /// <summary>
        /// Ses kimlikli çözüm (dialogue v2): önce v2/&lt;ses&gt;/&lt;stres&gt;/&lt;id&gt; (konuşmacı seed'i kalıcı ses seçer), sonra düz v2/&lt;id&gt;,
        /// sonra eski Voice/&lt;id&gt;. Yalnız örneklenebilen klip döner; yoksa / örneklenemiyorsa null.
        /// </summary>
        public static VoiceSamples GetSamples(string id, int speakerSeed, VoiceStress stress)
        {
            return SamplesOnly(Resolve(id, speakerSeed, stress));
        }

        /// <summary>
        /// Düz çözüm (konuşmacı/stres yok): v2/&lt;id&gt;, sonra eski Voice/&lt;id&gt;. İlk bulunan klip karar verir:
        /// örneklenebiliyorsa örnekler, değilse <see cref="VoiceSamples.DirectClip"/> (doğrudan çalınır). Hiçbiri yoksa null.
        /// </summary>
        public static VoiceSamples Resolve(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            var s = ResolvePath(VoiceFolder + id);
            return s != null ? s : ResolvePath(VoiceV2Resolver.LegacyPath(id));
        }

        /// <summary>Ses kimlikli çözüm (bkz. <see cref="GetSamples(string,int,VoiceStress)"/>); örneklenemeyen klip için <see cref="VoiceSamples.DirectClip"/> döner.</summary>
        public static VoiceSamples Resolve(string id, int speakerSeed, VoiceStress stress)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            // Yollar tembel kurulur: çoğu klip ilk (konuşmacı sesi + stres) yolda bulunur.
            var s = ResolvePath(VoiceV2Resolver.ResourcePath(VoiceV2Resolver.VoiceForSpeaker(speakerSeed), stress, id));
            if (s != null)
                return s;
            s = ResolvePath(VoiceFolder + id);
            return s != null ? s : ResolvePath(VoiceV2Resolver.LegacyPath(id));
        }

        public static VoiceStress ToVoiceStress(DialogueStress stress)
        {
            switch (stress)
            {
                case DialogueStress.Panic: return VoiceStress.Panik;
                case DialogueStress.Combat: return VoiceStress.Catisma;
                default: return VoiceStress.Sakin;
            }
        }

        /// <summary>
        /// Doğrudan çalınan (örneklenemeyen) klip için perde: <see cref="VoiceDsp.ApplyIdentity"/> ile aynı oran (perde * hız^0.35, 0.5..2).
        /// Formant ve şiddet örnek verisi olmadan uygulanamaz.
        /// </summary>
        public static float DirectPitch(VoiceIdentity id)
        {
            var ratio = id.Pitch * (float)Math.Pow(Math.Max(0.5f, id.Speed), 0.35);
            return Math.Max(0.5f, Math.Min(2f, ratio));
        }

        private static VoiceSamples SamplesOnly(VoiceSamples resolved)
        {
            return resolved != null && !resolved.IsDirect ? resolved : null;
        }

        private static VoiceSamples ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            var entry = LoadEntry(path);
            return entry != null ? entry.Source : null;
        }

        private static Entry LoadEntry(string path)
        {
            if (Missing.Contains(path))
                return null;

            if (Cache.TryGet(path, out var cached))
            {
                var source = cached.Source;
                if (source != null && source.Data != null)
                    return cached; // örnek verisi: Unity nesnesine bağlı değil

                if (source != null && source.DirectClip != null)
                {
                    if (cached.Verdict != ClipReadVerdict.NotLoaded)
                        return cached;
                    return Classify(path, source.DirectClip); // geçici durum (veri yükleniyordu): yeniden değerlendir
                }

                Cache.Remove(path); // klip boşaltılmış: yeniden yükle
            }

            AudioClip clip;
            try
            {
                clip = Resources.Load<AudioClip>(path);
            }
            catch (Exception)
            {
                clip = null;
            }

            if (clip == null)
            {
                AddMissing(path);
                return null;
            }

            return Classify(path, clip);
        }

        private static Entry Classify(string path, AudioClip clip)
        {
            var verdict = VoiceClipGuard.Check(clip);
            if (verdict == ClipReadVerdict.Empty || verdict == ClipReadVerdict.Missing)
            {
                AddMissing(path);
                return null;
            }

            if (verdict == ClipReadVerdict.Readable)
            {
                var samples = ReadSamples(clip);
                if (samples != null)
                {
                    ReleaseData(clip); // örnekler kopyalandı: açılmış PCM bellekte kalmasın (LRU sınırı gerçek olsun)
                    var sampled = new Entry { Source = samples, Verdict = ClipReadVerdict.Readable };
                    Cache.Set(path, sampled);
                    return sampled;
                }

                verdict = ClipReadVerdict.ReadFailed;
            }

            // Örneklenemeyen klip: klip başına uyarı yok (GetData çağrılmadı); tek özet satırı + doğrudan çalma.
            if (verdict != ClipReadVerdict.NotLoaded)
                VoiceClipDiagnostics.Report(path, verdict);

            var direct = new Entry
            {
                Source = new VoiceSamples { DirectClip = clip, SampleRate = clip.frequency },
                Verdict = verdict
            };
            Cache.Set(path, direct);
            return direct;
        }

        private static VoiceSamples ReadSamples(AudioClip clip)
        {
            try
            {
                var frames = clip.samples;
                var channels = Math.Max(1, clip.channels);
                var raw = new float[frames * channels];
                if (!clip.GetData(raw, 0))
                    return null;

                var mono = raw;
                if (channels > 1)
                {
                    mono = new float[frames];
                    for (var i = 0; i < mono.Length; i++)
                    {
                        var sum = 0f;
                        for (var c = 0; c < channels; c++)
                            sum += raw[i * channels + c];
                        mono[i] = sum / channels;
                    }
                }

                return new VoiceSamples { Data = VoiceDsp.TrimSilence(mono), SampleRate = clip.frequency };
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void ReleaseData(AudioClip clip)
        {
            try
            {
                clip.UnloadAudioData();
            }
            catch (Exception)
            {
            }
        }

        private static void AddMissing(string path)
        {
            if (Missing.Count >= MissingLimit)
                Missing.Clear();
            Missing.Add(path);
        }

        /// <summary>Parçaların hepsi örneklenebilir klip olarak varsa birleştirir; biri eksik ya da örneklenemiyorsa null (çağıran bütün satıra/yedeğe düşer).</summary>
        public static VoiceSamples StitchParts(IList<string> partIds, float gapSeconds, float speed)
        {
            return StitchParts(partIds, gapSeconds, speed, -1, VoiceStress.Sakin, false);
        }

        /// <summary>
        /// Ses kimlikli birleştirme: parçalar konuşmacının v2 sesinden (yoksa eski yoldan) okunur. Bir parça yoksa ya da örneklenemiyorsa
        /// (DecompressOnLoad değil) birleştirme ATLANIR (null): klip başına uyarı / zorla açma yok.
        /// </summary>
        public static VoiceSamples StitchParts(IList<string> partIds, float gapSeconds, float speed, int speakerSeed, VoiceStress stress, bool voiced = true)
        {
            if (partIds == null || partIds.Count == 0)
                return null;

            var parts = new List<float[]>(partIds.Count);
            var rate = 0;
            for (var i = 0; i < partIds.Count; i++)
            {
                var s = voiced ? Resolve(partIds[i], speakerSeed, stress) : Resolve(partIds[i]);
                if (s == null || s.IsDirect || s.Data == null)
                    return null;
                if (rate == 0)
                    rate = s.SampleRate;
                else if (rate != s.SampleRate)
                    return null; // karışık hız: güvenli tarafta kal
                parts.Add(s.Data);
            }

            return new VoiceSamples { Data = VoiceDsp.Stitch(parts, rate, gapSeconds, speed), SampleRate = rate };
        }

        public static AudioClip ToClip(string name, float[] data, int sampleRate)
        {
            if (data == null || data.Length == 0)
                return null;
            var clip = AudioClip.Create(name, data.Length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static void ClearCaches()
        {
            Cache.Clear();
            Missing.Clear();
        }
    }

    /// <summary>Mevcut AudioClip'i telsiz DSP'sinden geçirir (v1 telsiz oynatıcısı bu yolu kullanır).</summary>
    public static class RadioClipProcessor
    {
        /// <summary>
        /// İşlenmiş klip ya da null. Klip örneklenemiyorsa (DecompressOnLoad değil / yüklü değil) <c>GetData</c> ÇAĞRILMAZ ve null döner:
        /// çağıran klibi doğrudan (eski süzgeç zinciriyle) çalar.
        /// </summary>
        public static AudioClip TryProcess(AudioClip source, int seed, float signalQuality, DialogueStress stress, bool speakerInCombat)
        {
            if (source == null || source.samples <= 0)
                return null;

            var verdict = VoiceClipGuard.Check(source);
            if (verdict != ClipReadVerdict.Readable)
            {
                if (verdict != ClipReadVerdict.NotLoaded)
                    VoiceClipDiagnostics.Report(source.name, verdict);
                return null;
            }

            try
            {
                var raw = new float[source.samples * source.channels];
                if (!source.GetData(raw, 0))
                    return null;

                var mono = raw;
                if (source.channels > 1)
                {
                    mono = new float[source.samples];
                    for (var i = 0; i < mono.Length; i++)
                        mono[i] = raw[i * source.channels];
                }

                var settings = RadioDspSettings.For(signalQuality, stress, speakerInCombat);
                var processed = RadioDsp.Process(mono, source.frequency, settings, (uint)seed);
                return DialogueClipLibrary.ToClip(source.name + "_radio", processed, source.frequency);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
