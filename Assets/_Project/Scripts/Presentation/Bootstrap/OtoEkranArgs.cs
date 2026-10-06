using System;
using System.Globalization;
using System.Text;

namespace Project.Presentation.Bootstrap
{
    /// <summary>Otomatik ekran görüntüsü (görsel QA) çalışma türü.</summary>
    public enum OtoEkranMode
    {
        Yok = 0,

        /// <summary>-otoekran &lt;dir&gt;: AAA_Benchmark, Vitrin planları + kalite kademeleri.</summary>
        Benchmark = 1,

        /// <summary>-otoekran-harita &lt;sahne&gt; &lt;dir&gt;: harita sahnesi, 5 sabit bakış noktası.</summary>
        Harita = 2,

        /// <summary>-otoses &lt;dir&gt;: AAA_Benchmark'ta 25 sn betikli ses dizisi + ses_rapor.csv.</summary>
        Ses = 3
    }

    /// <summary>Çözümlenmiş otomatik ekran görüntüsü seçenekleri.</summary>
    public sealed class OtoEkranOptions
    {
        public OtoEkranMode Mode;
        public string OutDir = string.Empty;
        public string Scene = string.Empty;
        public float TimeoutSeconds = OtoEkranArgs.DefaultTimeoutSeconds;
        public float SettleSeconds = OtoEkranArgs.DefaultSettleSeconds;
        public int Width = OtoEkranArgs.DefaultWidth;
        public int Height = OtoEkranArgs.DefaultHeight;
    }

    /// <summary>
    /// Yerleşik oyuncu komut satırı: <c>-otoekran &lt;outDir&gt;</c>, <c>-otoekran-harita &lt;sahne&gt; &lt;outDir&gt;</c>,
    /// isteğe bağlı <c>-otoekran-sure &lt;sn&gt;</c> (sert çıkış, varsayılan 120), <c>-otoekran-bekle &lt;sn&gt;</c> (TAA oturma, varsayılan 2.5)
    /// ve <c>-otoekran-coz &lt;genişlik&gt; &lt;yükseklik&gt;</c> (çekim çözünürlüğü; tüm otoekran modlarında varsayılan 3840x2160/4K).
    /// Saf mantık: sahne/Unity nesnesine dokunmaz (EditMode testli).
    /// </summary>
    public static class OtoEkranArgs
    {
        public const float DefaultTimeoutSeconds = 120f;
        public const float DefaultSettleSeconds = 2.5f;
        // 4K varsayılan: yakınlaştırmada pikselleşme olmasın diye tüm otoekran çekimleri UHD yapılır.
        public const int DefaultWidth = 3840;
        public const int DefaultHeight = 2160;
        public const string FlagBenchmark = "-otoekran";
        public const string FlagMap = "-otoekran-harita";
        public const string FlagAudio = "-otoses";
        public const float DefaultAudioTimeoutSeconds = 90f;
        public const string FlagTimeout = "-otoekran-sure";
        public const string FlagSettle = "-otoekran-bekle";
        public const string FlagResolution = "-otoekran-coz";

        /// <summary>Argümanlardan seçenekleri çözer. Bayrak yoksa ya da değer eksikse false (seçenekler Yok).</summary>
        public static bool TryParse(string[] args, out OtoEkranOptions options)
        {
            return TryParse(args, out options, out _);
        }

        /// <summary>Bayrak argümanlarından herhangi biri var mı (çözümlenemese bile günlük için).</summary>
        public static bool HasAnyFlag(string[] args)
        {
            if (args == null)
                return false;
            for (var i = 0; i < args.Length; i++)
                if (args[i] != null && string.Equals(args[i], FlagAudio, StringComparison.OrdinalIgnoreCase))
                    return true;
            for (var i = 0; i < args.Length; i++)
                if (args[i] != null && args[i].StartsWith("-otoekran", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(args[i], "-otoekran-menu", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(args[i], FlagResolution, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>Sahne adını normalleştirir: yol/uzantı ("Assets/.../KuzgunVadisi.unity") atılır.</summary>
        public static string NormalizeScene(string scene)
        {
            if (string.IsNullOrWhiteSpace(scene))
                return string.Empty;
            var s = scene.Trim().Trim('"', '\'');
            var cut = Math.Max(s.LastIndexOf('/'), s.LastIndexOf('\\'));
            if (cut >= 0)
                s = s.Substring(cut + 1);
            if (s.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, s.Length - 6);
            return s;
        }

        /// <summary>Çözümleme + başarısızlık nedeni (reason boş değilse neden false döndüğünü açıklar).</summary>
        public static bool TryParse(string[] args, out OtoEkranOptions options, out string reason)
        {
            options = new OtoEkranOptions();
            reason = string.Empty;
            if (args == null || args.Length == 0)
            {
                reason = "argüman yok";
                return false;
            }

            var audioIndex = IndexOf(args, FlagAudio);
            var mapIndex = IndexOf(args, FlagMap);
            if (audioIndex >= 0 && mapIndex < 0)
            {
                if (!IsValue(args, audioIndex + 1))
                {
                    reason = FlagAudio + " için klasör değeri eksik";
                    return false;
                }

                options.Mode = OtoEkranMode.Ses;
                options.OutDir = args[audioIndex + 1].Trim();
                options.TimeoutSeconds = DefaultAudioTimeoutSeconds;
            }
            else if (mapIndex >= 0)
            {
                if (!IsValue(args, mapIndex + 1) || !IsValue(args, mapIndex + 2))
                {
                    reason = FlagMap + " için <sahne> <klasör> değerleri eksik/geçersiz";
                    return false;
                }

                var a = args[mapIndex + 1].Trim();
                var b = args[mapIndex + 2].Trim();
                // Ters sıra toleransı: ilk değer yol, ikincisi sahne adı gibi görünüyorsa değiştir.
                if (LooksLikePath(a) && !LooksLikePath(b))
                {
                    var t = a; a = b; b = t;
                }

                options.Mode = OtoEkranMode.Harita;
                options.Scene = NormalizeScene(a);
                options.OutDir = b;
                if (options.Scene.Length == 0)
                {
                    reason = "sahne adı boş";
                    return false;
                }
            }
            else
            {
                var index = IndexOf(args, FlagBenchmark);
                if (index < 0 || !IsValue(args, index + 1))
                {
                    reason = index < 0 ? "bayrak yok" : FlagBenchmark + " için klasör değeri eksik";
                    return false;
                }
                options.Mode = OtoEkranMode.Benchmark;
                options.OutDir = args[index + 1].Trim();
            }

            if (options.Mode == OtoEkranMode.Ses && (args == null || IndexOf(args, FlagTimeout) < 0))
                options.TimeoutSeconds = DefaultAudioTimeoutSeconds;
            if (TryGetFloat(args, FlagTimeout, out var timeout))
                options.TimeoutSeconds = Math.Min(Math.Max(timeout, 20f), 900f);
            if (TryGetFloat(args, FlagSettle, out var settle))
                options.SettleSeconds = Math.Min(Math.Max(settle, 0.2f), 20f);
            ParseResolution(args, out options.Width, out options.Height);
            return true;
        }

        /// <summary>
        /// <c>-otoekran-coz &lt;genişlik&gt; &lt;yükseklik&gt;</c>: çekim çözünürlüğü. Bayrak yoksa ya da değerler bozuksa
        /// 4K varsayılanı (3840x2160). Sınırlar: genişlik 640–7680, yükseklik 360–4320. Menü modu dahil tüm modlarda geçerli.
        /// </summary>
        public static void ParseResolution(string[] args, out int width, out int height)
        {
            width = DefaultWidth;
            height = DefaultHeight;
            if (args == null)
                return;
            var i = IndexOf(args, FlagResolution);
            if (i < 0 || !IsValue(args, i + 1) || !IsValue(args, i + 2))
                return;
            if (int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var w)
                && int.TryParse(args[i + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var h))
            {
                width = Math.Min(Math.Max(w, 640), 7680);
                height = Math.Min(Math.Max(h, 360), 4320);
            }
        }

        /// <summary>Plan/bakış adını dosya adına uygun ASCII'ye çevirir ("Silah yakın plan" → "silah_yakin_plan").</summary>
        public static string Slug(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "plan";
            var sb = new StringBuilder(text.Length);
            var lastUnderscore = true;
            foreach (var raw in text.Trim())
            {
                var c = Fold(raw);
                if (c >= 'a' && c <= 'z' || c >= '0' && c <= '9')
                {
                    sb.Append(c);
                    lastUnderscore = false;
                }
                else if (!lastUnderscore)
                {
                    sb.Append('_');
                    lastUnderscore = true;
                }
            }

            while (sb.Length > 0 && sb[sb.Length - 1] == '_')
                sb.Length--;
            return sb.Length == 0 ? "plan" : sb.ToString();
        }

        /// <summary>"01_silah_yakin_plan.png" biçimli dosya adı (sıra 1 tabanlı).</summary>
        public static string FileName(int oneBasedIndex, string name)
        {
            return oneBasedIndex.ToString("00", CultureInfo.InvariantCulture) + "_" + Slug(name) + ".png";
        }

        private static char Fold(char c)
        {
            switch (c)
            {
                case 'İ': case 'ı': case 'I': case 'i': return 'i';
                case 'Ş': case 'ş': return 's';
                case 'Ğ': case 'ğ': return 'g';
                case 'Ü': case 'ü': return 'u';
                case 'Ö': case 'ö': return 'o';
                case 'Ç': case 'ç': return 'c';
                case 'Â': case 'â': case 'Î': case 'î': case 'Û': case 'û':
                    return c == 'Â' || c == 'â' ? 'a' : c == 'Î' || c == 'î' ? 'i' : 'u';
                default: return char.ToLowerInvariant(c);
            }
        }

        private static int IndexOf(string[] args, string flag)
        {
            for (var i = 0; i < args.Length; i++)
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        private static bool LooksLikePath(string v)
        {
            return v.IndexOf('/') >= 0 || v.IndexOf('\\') >= 0;
        }

        private static bool IsValue(string[] args, int i)
        {
            return i < args.Length && !string.IsNullOrWhiteSpace(args[i]) && !args[i].StartsWith("-", StringComparison.Ordinal);
        }

        private static bool TryGetFloat(string[] args, string flag, out float value)
        {
            value = 0f;
            var i = IndexOf(args, flag);
            return i >= 0 && i + 1 < args.Length
                   && float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
