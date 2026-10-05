using System;

namespace Project.Platform
{
    /// <summary>
    /// Steam yapılandırma sabitleri ve komut satırı bayrakları (Steamworks API'sine dokunmaz; saf C#).
    /// AppID Partner'dan alındığında yalnızca <see cref="ProductionAppId"/> güncellenir — bkz. README §2.
    /// </summary>
    public static class SteamPlatformSettings
    {
        /// <summary>Valve'ın herkese açık test uygulaması (Spacewar). Yerel geliştirme / editör.</summary>
        public const uint DevelopmentAppId = 480;

        /// <summary>
        /// Steamworks Partner'da HAREKÂT'a atanan AppID. <c>0</c> = henüz alınmadı:
        /// bu durumda <c>RestartAppIfNecessary</c> atlanır ve <see cref="DevelopmentAppId"/> kullanılır.
        /// </summary>
        public const uint ProductionAppId = 0;

        /// <summary>
        /// <c>GetAuthTicketForWebApi</c> kimlik dizesi. Backend <c>ISteamUserAuth/AuthenticateUserTicket</c> çağrısında
        /// aynı değeri <c>identity</c> parametresi olarak göndermelidir (kanca: Docs/FAZ3_KANCALAR.md).
        /// </summary>
        public const string WebApiIdentity = "harekat";

        /// <summary>Backend <c>POST /auth/steam</c> göreli yolu.</summary>
        public const string BackendAuthPath = "/auth/steam";

        /// <summary>Steam girişinde backend'e gönderilen varsayılan bölge (SteamAuthRequest.Region).</summary>
        public const string DefaultRegion = "tr";

        /// <summary>Steam katmanını tamamen kapatır (ör. Steam'siz QA build'i).</summary>
        public const string NoSteamArg = "-nosteam";

        /// <summary>Steam açık kalır ama backend'e otomatik Steam girişi yapılmaz.</summary>
        public const string NoSteamLoginArg = "-nosteamlogin";

        /// <summary>Maç bitince backend'in sonucu işlemesi için Steam başarım senkronundan önce beklenen süre.</summary>
        public const float AchievementSyncDelayAfterMatchSeconds = 10f;

        /// <summary>Rich presence yoklama aralığı (sn). Steam yalnızca değişiklik olunca çağrılır.</summary>
        public const float PresencePollIntervalSeconds = 1f;

        /// <summary>Etkin AppID: üretim atanmışsa o, değilse Spacewar.</summary>
        public static uint EffectiveAppId => ProductionAppId != 0 ? ProductionAppId : DevelopmentAppId;

        /// <summary>Üretim AppID'si tanımlı mı (RestartAppIfNecessary yalnızca o zaman anlamlı).</summary>
        public static bool HasProductionAppId => ProductionAppId != 0;

        /// <summary>Komut satırında bayrak var mı (büyük/küçük harf duyarsız).</summary>
        public static bool HasArg(string name) => HasArg(SafeArgs(), name);

        public static bool HasArg(string[] args, string name)
        {
            if (args == null || string.IsNullOrEmpty(name))
                return false;

            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static string[] SafeArgs()
        {
            try
            {
                return Environment.GetCommandLineArgs();
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
        }
    }
}
