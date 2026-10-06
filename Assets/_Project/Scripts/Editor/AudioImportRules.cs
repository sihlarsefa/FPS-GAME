using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>Only project-owned runtime audio is affected; third-party originals retain their settings.</summary>
    public sealed class AudioImportRules : AssetPostprocessor
    {
        private const string Root = "Assets/_Project/Resources/Audio/";
        private const string VoiceRoot = Root + "Voice/";

        /// <summary>
        /// Voice clips up to this length (barks and the parts the dialogue system stitches) import as DecompressOnLoad so
        /// <c>AudioClip.GetData</c> works (still Vorbis on disk); longer voice stays CompressedInMemory (decoded PCM is ~10x the memory).
        /// </summary>
        public const float VoiceDecompressMaxSeconds = 3.5f;

        /// <summary>Bump when the policy in <see cref="TrySettings(string,float,out AudioImporterSampleSettings,out bool)"/> changes: Unity re-imports the project's audio once.</summary>
        public const uint RulesVersion = 2;

        public override uint GetVersion() => RulesVersion;

        /// <summary>Policy with unknown duration: Voice stays CompressedInMemory (memory-safe); see the duration overload.</summary>
        public static bool TrySettings(string path, out AudioImporterSampleSettings settings, out bool mono)
        {
            return TrySettings(path, -1f, out settings, out mono);
        }

        /// <summary>
        /// Voice load type from the source clip length: 0..<see cref="VoiceDecompressMaxSeconds"/> s => DecompressOnLoad (sample-readable,
        /// stitchable); longer or unknown (negative/NaN) => CompressedInMemory (played directly, never sampled).
        /// </summary>
        public static AudioClipLoadType VoiceLoadType(float durationSeconds)
        {
            return durationSeconds >= 0f && durationSeconds <= VoiceDecompressMaxSeconds
                ? AudioClipLoadType.DecompressOnLoad
                : AudioClipLoadType.CompressedInMemory;
        }

        public static bool IsVoicePath(string path)
        {
            return !string.IsNullOrEmpty(path)
                   && path.Replace('\\', '/').StartsWith(VoiceRoot, StringComparison.OrdinalIgnoreCase);
        }

        /// <param name="durationSeconds">Source clip length in seconds (only used for Voice); negative = unknown.</param>
        public static bool TrySettings(string path, float durationSeconds, out AudioImporterSampleSettings settings, out bool mono)
        {
            settings = new AudioImporterSampleSettings
            {
                sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
                quality = 0.5f
            };
            mono = false;
            if (string.IsNullOrEmpty(path)) return false;
            path = path.Replace('\\', '/');
            if (path.StartsWith(VoiceRoot, StringComparison.OrdinalIgnoreCase))
            {
                // Short barks/parts are read with GetData and stitched at runtime: DecompressOnLoad (Vorbis on disk) and loaded
                // together with the clip object. Long voice stays compressed in memory and is played as-is.
                settings.loadType = VoiceLoadType(durationSeconds);
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.55f;
                settings.preloadAudioData = settings.loadType == AudioClipLoadType.DecompressOnLoad;
                mono = true;
                return true;
            }
            if (path.StartsWith(Root + "Weapons/", StringComparison.OrdinalIgnoreCase))
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.ADPCM;
                settings.quality = 0.7f; // ADPCM'de yok sayılır; Vorbis'e dönülürse 0.7 kalır.
                mono = true;
                return true;
            }
            if (path.StartsWith(Root + "SFX/", StringComparison.OrdinalIgnoreCase))
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                mono = true;
                return true;
            }
            if (path.StartsWith(Root + "Ambience/", StringComparison.OrdinalIgnoreCase))
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                return true;
            }
            return false;
        }

        private static bool _warnedUnknownDuration;

        private void OnPreprocessAudio()
        {
            var voice = IsVoicePath(assetPath);
            var duration = voice ? AudioFileDuration.ProbeAsset(assetPath) : -1f;
            if (voice && duration < 0f && !_warnedUnknownDuration)
            {
                // Tek uyarı (işlem başına): süre okunamazsa Voice güvenli tarafta CompressedInMemory kalır (çalışma anında doğrudan çalınır).
                _warnedUnknownDuration = true;
                Debug.LogWarning("[HAREKÂT] Ses süresi okunamadı, CompressedInMemory bırakıldı (ilk örnek: " + assetPath + ").");
            }

            if (!TrySettings(assetPath, duration, out var settings, out var mono)) return;
            var importer = (AudioImporter)assetImporter;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = mono;
            // Prevent old desktop overrides from silently defeating the common policy.
            importer.ClearSampleSettingOverride("Standalone");
            importer.loadInBackground = settings.loadType == AudioClipLoadType.Streaming;
        }

        [MenuItem("HAREKÂT/İçerik/Ses İçe Aktarma Kurallarını Uygula")]
        public static void ReimportProjectAudio()
        {
            var count = 0;
            var voiceDecompress = 0;
            var voiceCompressed = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root.TrimEnd('/') }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var voice = IsVoicePath(path);
                    var duration = voice ? AudioFileDuration.ProbeAsset(path) : -1f;
                    if (!TrySettings(path, duration, out var settings, out _)) continue;
                    if (voice)
                    {
                        if (settings.loadType == AudioClipLoadType.DecompressOnLoad) voiceDecompress++;
                        else voiceCompressed++;
                    }
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    count++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            Debug.Log("[HAREKÂT] Ses içe aktarma kuralları: " + count + " klip (Voice: " + voiceDecompress + " DecompressOnLoad <="
                      + VoiceDecompressMaxSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " sn, "
                      + voiceCompressed + " CompressedInMemory).");
        }
    }

    /// <summary>
    /// Ses dosyası süresini kaynak dosyanın başlığından okur (içe aktarmadan önce de çalışır): WAV = RIFF fmt/data,
    /// OGG Vorbis = tanıtım başlığındaki örnekleme hızı + son sayfanın granule konumu. Ayrıştırıcılar saf bayt dizisi mantığıdır (testli).
    /// </summary>
    public static class AudioFileDuration
    {
        private const int HeadBytes = 65536;
        private const int TailBytes = 70000; // bir Ogg sayfası en çok ~65 KB

        /// <summary>Varlık yolunun (Assets/...) süresi (sn). Okunamayan/desteklenmeyen biçimde -1.</summary>
        public static float ProbeAsset(string assetPath)
        {
            var full = ResolveFullPath(assetPath);
            return full != null && TryReadSeconds(full, out var seconds) ? seconds : -1f;
        }

        private static string ResolveFullPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            try
            {
                var viaCurrentDir = Path.GetFullPath(assetPath);
                if (File.Exists(viaCurrentDir)) return viaCurrentDir;
            }
            catch (Exception)
            {
            }

            try
            {
                var viaDataPath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", assetPath));
                if (File.Exists(viaDataPath)) return viaDataPath;
            }
            catch (Exception)
            {
            }

            return null;
        }

        /// <summary>Dosyanın yalnız başını (ve OGG için sonunu) okuyarak süreyi verir.</summary>
        public static bool TryReadSeconds(string fullPath, out float seconds)
        {
            seconds = -1f;
            try
            {
                using (var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var length = fs.Length;
                    var head = new byte[(int)Math.Min(length, HeadBytes)];
                    var headRead = ReadFully(fs, head);

                    if (Tag(head, headRead, 0, "RIFF"))
                        return TryParseWav(head, headRead, length, out seconds);

                    if (Tag(head, headRead, 0, "OggS"))
                    {
                        var tailLength = (int)Math.Min(length, TailBytes);
                        var tail = new byte[tailLength];
                        fs.Seek(length - tailLength, SeekOrigin.Begin);
                        var tailRead = ReadFully(fs, tail);
                        return TryParseOgg(head, headRead, tail, tailRead, out seconds);
                    }
                }
            }
            catch (Exception)
            {
            }

            seconds = -1f;
            return false;
        }

        /// <summary>
        /// RIFF/WAVE: "fmt " bayt hızı + "data" boyutu. Akış WAV'larında data boyutu 0 ya da dosyadan büyük olabilir: dosya sonuna kadar sayılır.
        /// <paramref name="length"/> = <paramref name="head"/> içindeki geçerli bayt, <paramref name="fileLength"/> = dosya boyutu.
        /// </summary>
        public static bool TryParseWav(byte[] head, int length, long fileLength, out float seconds)
        {
            seconds = -1f;
            if (head == null || length < 12 || !Tag(head, length, 0, "RIFF") || !Tag(head, length, 8, "WAVE"))
                return false;

            long byteRate = 0;
            long pos = 12;
            while (pos + 8 <= length)
            {
                var at = (int)pos;
                var size = ReadU32(head, at + 4);
                var body = at + 8;
                if (Tag(head, length, at, "fmt "))
                {
                    if (body + 16 > length) return false;
                    long channels = ReadU16(head, body + 2);
                    var rate = ReadU32(head, body + 4);
                    byteRate = ReadU32(head, body + 8);
                    long bits = ReadU16(head, body + 14);
                    if (byteRate <= 0 && rate > 0 && channels > 0 && bits > 0)
                        byteRate = rate * channels * ((bits + 7) / 8);
                }
                else if (Tag(head, length, at, "data"))
                {
                    if (byteRate <= 0) return false;
                    var available = fileLength - body;
                    var bytes = size == 0 || size > available ? available : size;
                    if (bytes <= 0) return false;
                    seconds = (float)((double)bytes / byteRate);
                    return true;
                }

                pos = body + size + (size & 1);
            }

            return false;
        }

        /// <summary>
        /// OGG Vorbis: ilk sayfadaki tanıtım başlığından örnekleme hızı, <paramref name="tail"/> içindeki son sayfanın granule konumundan
        /// toplam örnek sayısı. Opus ve diğer akışlar için false.
        /// </summary>
        public static bool TryParseOgg(byte[] head, int headLength, byte[] tail, int tailLength, out float seconds)
        {
            seconds = -1f;
            if (head == null || tail == null || headLength < 28 || !Tag(head, headLength, 0, "OggS"))
                return false;

            var packet = 27 + head[26]; // sayfa başlığı + segment tablosu
            if (packet + 16 > headLength || head[packet] != 1 || !Tag(head, headLength, packet + 1, "vorbis"))
                return false;
            var rate = ReadU32(head, packet + 12);
            if (rate <= 0) return false;

            // Son sayfa: sondan geriye "OggS" ara (sürüm 0, başlık bayrakları <= 7); granule < 0 ise (tamamlanmış paket yok) bir öncekine bak.
            for (var i = Math.Min(tailLength, tail.Length) - 14; i >= 0; i--)
            {
                if (!Tag(tail, tailLength, i, "OggS") || tail[i + 4] != 0 || tail[i + 5] > 7)
                    continue;
                var granule = ReadI64(tail, i + 6);
                if (granule < 0)
                    continue;
                seconds = (float)((double)granule / rate);
                return seconds > 0f;
            }

            return false;
        }

        private static int ReadFully(Stream stream, byte[] buffer)
        {
            var total = 0;
            while (total < buffer.Length)
            {
                var n = stream.Read(buffer, total, buffer.Length - total);
                if (n <= 0) break;
                total += n;
            }

            return total;
        }

        private static bool Tag(byte[] b, int length, int offset, string tag)
        {
            if (b == null || offset < 0 || offset + tag.Length > length || offset + tag.Length > b.Length) return false;
            for (var i = 0; i < tag.Length; i++)
                if (b[offset + i] != (byte)tag[i]) return false;
            return true;
        }

        private static int ReadU16(byte[] b, int o) => b[o] | (b[o + 1] << 8);

        private static long ReadU32(byte[] b, int o) => b[o] | ((long)b[o + 1] << 8) | ((long)b[o + 2] << 16) | ((long)b[o + 3] << 24);

        private static long ReadI64(byte[] b, int o)
        {
            long v = 0;
            for (var i = 7; i >= 0; i--)
                v = (v << 8) | b[o + i];
            return v;
        }
    }
}
