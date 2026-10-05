using System;
using System.Globalization;
using Project.Core.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Presentation.Bootstrap
{
    /// <summary>
    /// Başsız (headless) / adanmış sunucu çalışma ayarları — Windows Dedicated Server build'i
    /// (<c>HAREKAT_Server.exe -batchmode -nographics -port 7777 ...</c>) bu sınıfla algılanır.
    /// Sunucuda arayüz, imleç, ses ortamı, post-processing ve yerel oyuncu kurulmaz; maç tamamen sunucu otoritesinde
    /// (bot timleri) simüle edilir ve bitince yeni tohumla yeniden başlar (ya da süreç kapanır).
    /// Komut satırı (hepsi isteğe bağlı, büyük/küçük harf duyarsız):
    ///  -server                 grafikli süreçte de sunucu modunu zorlar
    ///  -teams N                tim sayısı (2..8)
    ///  -seed N                 maç tohumu (0 = rastgele)
    ///  -difficulty easy|normal|hard (kolay|normal|zor)
    ///  -insertion heli|kirpi   oyuncu timinin intikal yöntemi
    ///  -prematch S             maç öncesi geri sayım (sn)
    ///  -norestart              maç bitince yeniden başlatma (boşta bekle)
    ///  -quitaftermatch         maç bitince süreci kapat
    ///  -maxmatches N           N maçtan sonra süreci kapat (0 = sınırsız)
    ///  -restartdelay S         maç sonu → yeni maç bekleme süresi (sn, varsayılan 15)
    ///  -tickrate N             sunucu kare/tick hızı (varsayılan 30)
    /// </summary>
    public static class ServerRuntime
    {
        public const float DefaultRestartDelaySeconds = 15f;
        public const int DefaultTickRate = 30;

        private static string[] _args;
        private static bool? _headless;
        private static bool _processConfigured;

        /// <summary>Grafik aygıtı olmayan süreç (UNITY_SERVER build'i, -batchmode ya da -nographics).</summary>
        public static bool IsHeadless
        {
            get
            {
                if (_headless.HasValue)
                    return _headless.Value;

                _headless = DetectHeadless();
                return _headless.Value;
            }
        }

        /// <summary>
        /// Adanmış sunucu modu: başsız süreç ya da <c>-server</c> argümanı. Editörde yalnızca editör <c>-server</c> argümanıyla
        /// açıldıysa (batch mode PlayMode testleri sunucu sayılmaz).
        /// </summary>
        public static bool IsDedicatedServer
        {
            get
            {
#if UNITY_EDITOR
                return HasArg("-server");
#else
                return IsHeadless || HasArg("-server");
#endif
            }
        }

        /// <summary>Maç bitince yeni maç başlatılsın mı (<c>-norestart</c> ile kapatılır).</summary>
        public static bool AutoRestart => !HasArg("-norestart");

        /// <summary>Maç bitince süreç kapansın mı (<c>-quitaftermatch</c>).</summary>
        public static bool QuitAfterMatch => HasArg("-quitaftermatch");

        /// <summary>Bu süreçte tamamlanan maç sayısı (yeniden başlatmalarda korunur).</summary>
        public static int MatchesPlayed { get; private set; }

        /// <summary>Kaç maçtan sonra süreç kapanır (0 = sınırsız).</summary>
        public static int MaxMatches => TryGetInt("-maxmatches", out var value) && value > 0 ? value : 0;

        /// <summary>Maç sonu ile yeni maç arasındaki bekleme (sn).</summary>
        public static float RestartDelaySeconds =>
            TryGetFloat("-restartdelay", out var value) && value >= 0f ? Mathf.Min(value, 600f) : DefaultRestartDelaySeconds;

        /// <summary>Sunucu kare hızı (= simülasyon tick hızı, varsayılan 30).</summary>
        public static int TickRate => TryGetInt("-tickrate", out var value) && value >= 10 && value <= 120 ? value : DefaultTickRate;

        /// <summary>Komut satırında bayrak var mı?</summary>
        public static bool HasArg(string name)
        {
            var args = Args;
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary><c>-ad değer</c> biçimindeki argümanın değeri.</summary>
        public static bool TryGetArg(string name, out string value)
        {
            var args = Args;
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    value = args[i + 1];
                    return !string.IsNullOrEmpty(value);
                }
            }

            value = null;
            return false;
        }

        public static bool TryGetInt(string name, out int value)
        {
            value = 0;
            return TryGetArg(name, out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryGetFloat(string name, out float value)
        {
            value = 0f;
            return TryGetArg(name, out var text) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                   && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>Komut satırındaki maç ayarlarını yapılandırmaya uygular (argüman yoksa hiçbir şey değişmez).</summary>
        public static void ApplyMatchOverrides(MatchConfig config)
        {
            if (config == null)
                return;

            if (TryGetInt("-teams", out var teams))
                config.WithTeams(Mathf.Clamp(teams, 2, 8), config.TeamSize);

            if (TryGetInt("-seed", out var seed) && seed != 0)
                config.RandomSeed = seed & 0x7fffffff;

            if (TryGetArg("-difficulty", out var difficulty) && TryParseDifficulty(difficulty, out var parsedDifficulty))
                config.Difficulty = parsedDifficulty;

            if (TryGetArg("-insertion", out var insertion) && TryParseInsertion(insertion, out var parsedInsertion))
                config.PlayerInsertion = parsedInsertion;

            if (TryGetFloat("-prematch", out var preMatch) && preMatch >= 0f)
                config.PreMatchDurationSeconds = Mathf.Min(preMatch, 300f);
        }

        /// <summary>
        /// Sunucu sürecini hazırlar (bir kez): sabit kare hızı (= tick hızı), VSync kapalı, arka planda çalışma, ses kapalı.
        /// </summary>
        public static void ConfigureProcess()
        {
            if (_processConfigured)
                return;

            _processConfigured = true;
            try
            {
                QualitySettings.vSyncCount = 0;
                UnityEngine.Application.targetFrameRate = TickRate;
                UnityEngine.Application.runInBackground = true;
                AudioListener.volume = 0f;
                AudioListener.pause = true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            Debug.Log("[Sunucu] Adanmış sunucu modu — tick " + TickRate + " Hz, yeniden başlatma: "
                      + (QuitAfterMatch ? "kapat" : AutoRestart ? RestartDelaySeconds.ToString("0", CultureInfo.InvariantCulture) + " sn" : "kapalı")
                      + (MaxMatches > 0 ? ", en çok " + MaxMatches + " maç" : string.Empty));
        }

        /// <summary>Maç bitti: sayaç artar; süreç kapanacaksa true.</summary>
        public static bool RegisterMatchFinished()
        {
            MatchesPlayed++;
            var max = MaxMatches;
            return QuitAfterMatch || (max > 0 && MatchesPlayed >= max);
        }

        /// <summary>Süreci kapatır (editörde Play Mode'u durdurmaz; yalnızca günlüğe yazar).</summary>
        public static void Quit()
        {
            Debug.Log("[Sunucu] Süreç kapatılıyor (" + MatchesPlayed + " maç tamamlandı).");
#if !UNITY_EDITOR
            UnityEngine.Application.Quit();
#endif
        }

        public static bool TryParseDifficulty(string text, out BotDifficulty difficulty)
        {
            switch ((text ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "0":
                case "easy":
                case "kolay":
                    difficulty = BotDifficulty.Easy;
                    return true;
                case "1":
                case "normal":
                case "orta":
                    difficulty = BotDifficulty.Normal;
                    return true;
                case "2":
                case "hard":
                case "zor":
                    difficulty = BotDifficulty.Hard;
                    return true;
                default:
                    difficulty = BotDifficulty.Normal;
                    return false;
            }
        }

        public static bool TryParseInsertion(string text, out InsertionMethod method)
        {
            switch ((text ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "0":
                case "heli":
                case "helicopter":
                case "helikopter":
                case "t70":
                case "t-70":
                    method = InsertionMethod.Helicopter;
                    return true;
                case "1":
                case "kirpi":
                case "armored":
                case "armoredvehicle":
                case "zirhli":
                case "zırhlı":
                    method = InsertionMethod.ArmoredVehicle;
                    return true;
                default:
                    method = InsertionMethod.Helicopter;
                    return false;
            }
        }

        private static string[] Args
        {
            get
            {
                if (_args != null)
                    return _args;

                try
                {
                    _args = Environment.GetCommandLineArgs() ?? Array.Empty<string>();
                }
                catch (Exception)
                {
                    _args = Array.Empty<string>();
                }

                return _args;
            }
        }

        private static bool DetectHeadless()
        {
#if UNITY_SERVER
            return true;
#else
            try
            {
                return UnityEngine.Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
            }
            catch (Exception)
            {
                return false;
            }
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _args = null;
            _headless = null;
            _processConfigured = false;
            MatchesPlayed = 0;
        }
    }
}
