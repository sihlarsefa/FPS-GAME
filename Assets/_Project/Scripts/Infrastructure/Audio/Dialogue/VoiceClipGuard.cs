using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.Audio.Dialogue
{
    /// <summary>Bir ses klibinin örneklerinin (<c>AudioClip.GetData</c>) güvenle okunup okunamayacağı.</summary>
    public enum ClipReadVerdict
    {
        /// <summary>DecompressOnLoad + veri yüklü: GetData güvenli.</summary>
        Readable = 0,

        /// <summary>CompressedInMemory / Streaming: GetData klip başına uyarı basar ve başarısız olur. Klip doğrudan çalınır.</summary>
        NotDecompressOnLoad,

        /// <summary>DecompressOnLoad ama veri henüz yüklü değil (geçici durum): şimdilik doğrudan çalınır, sonra yeniden denenir.</summary>
        NotLoaded,

        /// <summary>Yüklü + DecompressOnLoad iken GetData yine de false döndü (beklenmeyen): klip doğrudan çalınır.</summary>
        ReadFailed,

        /// <summary>Klipte örnek yok.</summary>
        Empty,

        /// <summary>Klip yok.</summary>
        Missing
    }

    /// <summary>
    /// Örnekleme (birleştirme / kimlik DSP'si / telsiz DSP'si) yolunun koruması: <c>GetData</c> yalnızca
    /// <c>loadType == DecompressOnLoad</c> ve veri yüklüyken çağrılır. Aksi hâlde örnekleme ATLANIR ve klip doğrudan çalınır:
    /// ne Unity'nin "Cannot get data on compressed samples" uyarısı basılır ne de yükleme türü zorla değiştirilir.
    /// </summary>
    public static class VoiceClipGuard
    {
        /// <summary>Saf karar (Unity yerel köprüsü gerekmez; testlidir).</summary>
        public static ClipReadVerdict Evaluate(AudioClipLoadType loadType, AudioDataLoadState loadState, int samples)
        {
            if (samples <= 0)
                return ClipReadVerdict.Empty;
            if (loadType != AudioClipLoadType.DecompressOnLoad)
                return ClipReadVerdict.NotDecompressOnLoad;
            if (loadState != AudioDataLoadState.Loaded)
                return ClipReadVerdict.NotLoaded;
            return ClipReadVerdict.Readable;
        }

        /// <summary>Klibin üst verisinden karar verir. DecompressOnLoad ama boşaltılmış klip için tek, kısa, eşzamanlı yükleme dener.</summary>
        public static ClipReadVerdict Check(AudioClip clip)
        {
            if (clip == null)
                return ClipReadVerdict.Missing;

            try
            {
                var verdict = Evaluate(clip.loadType, clip.loadState, clip.samples);
                if (verdict == ClipReadVerdict.NotLoaded && clip.loadState == AudioDataLoadState.Unloaded)
                {
                    // "Preload Audio Data" kapalı ya da önceki örnekleme veriyi boşalttı: tür DEĞİŞMEZ, yalnız bu kısa klibin verisi yüklenir.
                    clip.LoadAudioData();
                    verdict = Evaluate(clip.loadType, clip.loadState, clip.samples);
                }

                return verdict;
            }
            catch (Exception)
            {
                return ClipReadVerdict.NotLoaded;
            }
        }
    }

    /// <summary>
    /// Örneklenemeyen klipler için TEK özet satırı: klip başına Unity uyarısı yerine ilk karşılaşmada tek satır basılır,
    /// sonrakiler yalnız sayılır (<see cref="MismatchCount"/>). Telsiz ve diyalog yolları aynı sayacı paylaşır.
    /// </summary>
    public static class VoiceClipDiagnostics
    {
        private const int MaxTracked = 4096;

        private static readonly HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);
        private static bool _reported;

        /// <summary>Örneklenemeyen (DecompressOnLoad olmayan / okunamayan) farklı klip sayısı (en çok 4096'ya kadar sayılır).</summary>
        public static int MismatchCount => Seen.Count;

        /// <summary>Özet satırı basıldı mı.</summary>
        public static bool Reported => _reported;

        /// <summary>
        /// Saf kısım: klibi (farklıysa) sayar; özet satırını YALNIZ ilk çağrıda döndürür, sonrakilerde null.
        /// </summary>
        public static string Note(string clipPath, ClipReadVerdict verdict)
        {
            var key = string.IsNullOrEmpty(clipPath) ? "?" : clipPath;
            if (Seen.Count < MaxTracked)
                Seen.Add(key);

            if (_reported)
                return null;
            _reported = true;
            return Summary(key, verdict);
        }

        /// <summary>Klibi sayar ve (yalnız ilk seferde) tek özet satırını konsola yazar.</summary>
        public static void Report(string clipPath, ClipReadVerdict verdict)
        {
            var line = Note(clipPath, verdict);
            if (line == null)
                return;
            try
            {
                Debug.LogWarning(line);
            }
            catch (Exception)
            {
                // Günlük süstür; hiçbir koşulda oyunu bozmamalı.
            }
        }

        /// <summary>Özet satırı metni (tek satır).</summary>
        public static string Summary(string examplePath, ClipReadVerdict verdict)
        {
            return "[SES] Ses klibi örneklenemiyor (" + verdict + "): ilk örnek \"" + examplePath
                   + "\". Bu ve benzeri klipler birleştirme/DSP olmadan doğrudan çalınır (klip başına uyarı yok, bu tek özet satırı). "
                   + "Düzeltme: HAREKÂT > İçerik > Ses İçe Aktarma Kurallarını Uygula (Voice <=3,5 sn = DecompressOnLoad).";
        }

        /// <summary>Sayaçları ve "basıldı" durumunu sıfırlar (testler / yeniden başlatma).</summary>
        public static void Reset()
        {
            Seen.Clear();
            _reported = false;
        }
    }
}
