using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Steamworks;
using Project.Core.Domain;
using Project.Presentation.Bootstrap;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Project.Platform
{
    /// <summary>Backend'e gönderilen Steam bileti türü.</summary>
    public enum SteamAuthTicketKind
    {
        /// <summary><c>GetAuthTicketForWebApi(identity)</c> — önerilen; backend <c>identity</c> göndermeli.</summary>
        WebApi = 0,

        /// <summary><c>GetAuthSessionTicket</c> (kimliksiz) — eski yöntem; backend identity göndermez.</summary>
        Session = 1,

        /// <summary><c>DEV:{steamid}:{persona}</c> — yalnızca editör / development build; backend <c>Steam:AllowDevTickets=true</c>.</summary>
        Dev = 2
    }

    /// <summary>Hex kodlu Steam bileti.</summary>
    public readonly struct SteamAuthTicket
    {
        public SteamAuthTicket(SteamAuthTicketKind kind, string value, string identity)
        {
            Kind = kind;
            Value = value ?? string.Empty;
            Identity = identity;
        }

        public SteamAuthTicketKind Kind { get; }
        public string Value { get; }

        /// <summary>WebApi biletinde kimlik dizesi; diğerlerinde null.</summary>
        public string Identity { get; }
    }

    /// <summary>Backend <c>/auth/steam</c> başarısız yanıtı.</summary>
    public sealed class SteamBackendAuthException : Exception
    {
        public SteamBackendAuthException(long statusCode, string body, string message)
            : base(message)
        {
            StatusCode = statusCode;
            Body = body ?? string.Empty;
        }

        /// <summary>HTTP kodu; 0 = sunucuya ulaşılamadı.</summary>
        public long StatusCode { get; }
        public string Body { get; }
    }

    /// <summary>
    /// Steamworks.NET üzerinden başlatma, kullanıcı (ad / SteamID / dil), başarımlar, rich presence ve
    /// backend <c>POST /auth/steam</c> kimlik doğrulaması.
    /// Yalnızca <c>STEAMWORKS_NET</c> tanımlıyken derlenir (asmdef <c>Project.Platform</c>).
    /// Ana oyun bu assembly'yi bilmez: servis kendini <see cref="RuntimeInitializeOnLoadMethod"/> ile kurar,
    /// rich presence'ı <see cref="SteamPresenceDriver"/> (GameContext yoklaması) ve backend girişi /
    /// başarım senkronunu <see cref="SteamOnlineBridge"/> yürütür. Tüm çağrılar ana iş parçacığından yapılmalıdır.
    /// </summary>
    public sealed class SteamPlatformService : MonoBehaviour
    {
        const int SessionTicketBufferSize = 1024;
        const float TicketTimeoutSeconds = 8f;
        const int HttpTimeoutSeconds = 15;

        static SteamPlatformService _instance;
        static bool _everInitialized;

        Callback<UserStatsReceived_t> _statsReceived;
        Callback<GetTicketForWebApiResponse_t> _webApiTicketReceived;
        Callback<GetAuthSessionTicketResponse_t> _sessionTicketReceived;

        HAuthTicket _activeTicket = HAuthTicket.Invalid;
        HAuthTicket _pendingWebApiHandle = HAuthTicket.Invalid;
        HAuthTicket _pendingSessionHandle = HAuthTicket.Invalid;
        volatile bool _webApiTicketDone;
        volatile bool _sessionTicketDone;
        byte[] _webApiTicketBytes;
        EResult _sessionTicketResult;
        bool _authInFlight;

        bool _initialized;
        bool _statsReady;
        readonly HashSet<string> _pendingAchievements = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _warnedApiNames = new HashSet<string>(StringComparer.Ordinal);
        string _lastPresenceSignature;

        /// <summary>Steam başarıyla başlatıldıysa servis; aksi halde null.</summary>
        public static SteamPlatformService Instance => _instance;

        /// <summary>Steam katmanı çalışıyor mu (kısayol).</summary>
        public static bool IsAvailable => _instance != null && _instance._initialized;

        public bool IsInitialized => _initialized;
        public bool StatsReady => _statsReady;
        public uint AppId { get; private set; }
        public string PersonaName { get; private set; } = string.Empty;
        public ulong SteamId { get; private set; }
        public string SteamIdString => SteamId == 0 ? string.Empty : SteamId.ToString();

        /// <summary>Steam istemci dili (ör. "turkish", "english"). Oyun içi dil varsayılanı için.</summary>
        public string GameLanguage { get; private set; } = string.Empty;

        /// <summary>Steam sunucularına bağlı mı (çevrimdışı modda false).</summary>
        public bool IsLoggedOn => _initialized && SafeLoggedOn();

        /// <summary>Backend'e Steam girişi tamamlandığında (ham AuthResponse JSON).</summary>
        public event Action<string> BackendAuthenticated;

        // ——— Yaşam döngüsü ———

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Domain reload kapalıyken (Enter Play Mode Options) önceki oturumun izleri temizlenir.
            _instance = null;
            _everInitialized = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (_instance != null)
                return;

            if (!ShouldRunSteam(out var reason))
            {
                Debug.Log("[Steam] Devre dışı: " + reason);
                return;
            }

            var go = new GameObject("[SteamPlatform]");
            DontDestroyOnLoad(go);
            var service = go.AddComponent<SteamPlatformService>();
            if (!service.Initialize())
            {
                Destroy(go);
                return;
            }

            _instance = service;
        }

        static bool ShouldRunSteam(out string reason)
        {
#if UNITY_SERVER
            reason = "dedicated server build (UNITY_SERVER)";
            return false;
#else
            if (UnityEngine.Application.isBatchMode)
            {
                reason = "batch mode (CI / test / headless)";
                return false;
            }

            if (SteamPlatformSettings.HasArg(SteamPlatformSettings.NoSteamArg))
            {
                reason = SteamPlatformSettings.NoSteamArg + " argümanı";
                return false;
            }

            reason = null;
            return true;
#endif
        }

        /// <summary>Steam'i başlatır. Steam istemcisi kapalıysa / DLL yoksa false döner; oyun Steam'siz devam eder.</summary>
        public bool Initialize()
        {
            if (_initialized)
                return true;

            if (_everInitialized)
            {
                Debug.LogWarning("[Steam] SteamAPI bu oturumda zaten başlatıldı; ikinci kez başlatılmıyor.");
                return false;
            }

            try
            {
                if (!Packsize.Test())
                {
                    ReportSetupProblem("Packsize uyumsuz — Steamworks.NET ile native steam_api64 sürümleri eşleşmiyor.");
                    return false;
                }

                if (!DllCheck.Test())
                {
                    ReportSetupProblem("DllCheck başarısız — steam_api64.dll eksik veya yanlış sürüm.");
                    return false;
                }

#if !UNITY_EDITOR
                // Oyun Steam dışından başlatıldıysa Steam üzerinden yeniden başlatır (yalnızca üretim AppID'si varsa).
                if (SteamPlatformSettings.HasProductionAppId &&
                    SteamAPI.RestartAppIfNecessary(new AppId_t(SteamPlatformSettings.ProductionAppId)))
                {
                    Debug.Log("[Steam] Oyun Steam üzerinden yeniden başlatılıyor.");
                    UnityEngine.Application.Quit();
                    return false;
                }
#endif

                var initResult = SteamAPI.InitEx(out var initError);
                if (initResult != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    Debug.LogWarning($"[Steam] Başlatılamadı ({initResult}): {initError}. Steam istemcisi açık mı? " +
                                     "Editörde proje kökünde steam_appid.txt (480) olmalı.");
                    return false;
                }

                _everInitialized = true;
                _initialized = true;

                _statsReceived = Callback<UserStatsReceived_t>.Create(OnUserStatsReceived);
                _webApiTicketReceived = Callback<GetTicketForWebApiResponse_t>.Create(OnWebApiTicketReceived);
                _sessionTicketReceived = Callback<GetAuthSessionTicketResponse_t>.Create(OnSessionTicketReceived);

                AppId = SteamUtils.GetAppID().m_AppId;
                SteamId = SteamUser.GetSteamID().m_SteamID;
                PersonaName = SteamFriends.GetPersonaName() ?? string.Empty;
                GameLanguage = SteamApps.GetCurrentGameLanguage() ?? string.Empty;

                // Steamworks SDK ≤1.60: istatistikler istenmeli (UserStatsReceived_t gelir).
                // SDK ≥1.61 (Steamworks.NET 2025.x): RequestCurrentStats kaldırıldı; Steam açılışta senkronlar.
                _statsReady = !TryRequestCurrentStats();

                GameSession.MatchStarting += OnMatchStarting;
                GameSession.MatchFinished += OnMatchFinished;

                Debug.Log($"[Steam] Hazır — {PersonaName} ({SteamId}), AppID {AppId}, dil '{GameLanguage}'.");
                return true;
            }
            catch (DllNotFoundException ex)
            {
                ReportSetupProblem("steam_api64 yüklenemedi: " + ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Steam] Başlatma hatası: " + ex.Message);
                return false;
            }
        }

        void Update()
        {
            if (!_initialized)
                return;

            SteamAPI.RunCallbacks();

        }

        void OnApplicationQuit()
        {
            Shutdown();
        }

        void OnDestroy()
        {
            Shutdown();
            if (_instance == this)
                _instance = null;
        }

        /// <summary>Biletleri iptal eder, callback'leri bırakır ve SteamAPI'yi kapatır.</summary>
        public void Shutdown()
        {
            if (!_initialized)
                return;

            try
            {
                GameSession.MatchStarting -= OnMatchStarting;
                GameSession.MatchFinished -= OnMatchFinished;
                CancelAuthTicket();
                _statsReceived?.Dispose();
                _webApiTicketReceived?.Dispose();
                _sessionTicketReceived?.Dispose();
                SteamAPI.Shutdown();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Steam] Kapatma hatası: " + ex.Message);
            }
            finally
            {
                _statsReceived = null;
                _webApiTicketReceived = null;
                _sessionTicketReceived = null;
                _initialized = false;
                _statsReady = false;
                _lastPresenceSignature = null;
            }
        }

        static void ReportSetupProblem(string message)
        {
            // Editörde uyarı (PlayMode testleri hata logunu başarısızlık sayar); build'de hata.
#if UNITY_EDITOR
            Debug.LogWarning("[Steam] " + message);
#else
            Debug.LogError("[Steam] " + message);
#endif
        }

        static bool SafeLoggedOn()
        {
            try
            {
                return SteamUser.BLoggedOn();
            }
            catch (Exception)
            {
                return false;
            }
        }

        static bool TryRequestCurrentStats()
        {
            var method = typeof(SteamUserStats).GetMethod(
                "RequestCurrentStats",
                BindingFlags.Public | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);
            if (method == null)
                return false;

            method.Invoke(null, null);
            return true;
        }

        void OnUserStatsReceived(UserStatsReceived_t data)
        {
            if (data.m_nGameID != SteamUtils.GetAppID().m_AppId)
                return;

            if (data.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogWarning($"[Steam] Başarım/istatistik verisi alınamadı: {data.m_eResult}");
                return;
            }

            _statsReady = true;
            if (_pendingAchievements.Count > 0)
            {
                var pending = new List<string>(_pendingAchievements);
                _pendingAchievements.Clear();
                UnlockAchievements(pending);
            }
        }

        // ——— Oyun kancaları (GameSession olayları) ———

        void OnMatchStarting(MatchConfig config)
        {
            SetMatchRichPresence(config != null ? config.MapName : null, config != null ? config.MaxPlayers : 0);
        }

        void OnMatchFinished(MatchResult result)
        {
            SetPresence(SteamPresenceState.MatchEnded);
            var ids = new List<string>(2);
            if (result.IsWinner)
                ids.Add("first_victory");
            if (result.Kills > 0)
                ids.Add("first_blood");
            if (ids.Count > 0)
                UnlockAchievements(ids);
        }

        // ——— Rich presence ———

        /// <summary>
        /// Arkadaş listesindeki durumu ayarlar. Aynı durum tekrar gönderilmez.
        /// Örnek (maç içi): <c>"Kuzgun Vadisi — 23 asker kaldı"</c>.
        /// </summary>
        public void SetPresence(SteamPresenceState state, string mapName = null, int soldiersRemaining = 0)
        {
            if (!_initialized)
                return;

            var signature = SteamRichPresenceFormat.Signature(state, mapName, soldiersRemaining);
            if (signature == _lastPresenceSignature)
                return;
            _lastPresenceSignature = signature;

            var inOperation = state != SteamPresenceState.Menu && state != SteamPresenceState.Training;
            var map = inOperation ? SteamRichPresenceFormat.NormalizeMapName(mapName) : string.Empty;
            var soldiers = state == SteamPresenceState.InMatch
                ? SteamRichPresenceFormat.SoldiersValue(soldiersRemaining)
                : string.Empty;

            // İkame anahtarları steam_display'den önce yazılır; boş değer anahtarı siler.
            SteamFriends.SetRichPresence(SteamRichPresenceFormat.KeyMap, map);
            SteamFriends.SetRichPresence(SteamRichPresenceFormat.KeySoldiers, soldiers);
            SteamFriends.SetRichPresence(SteamRichPresenceFormat.KeyStatus,
                SteamRichPresenceFormat.StatusText(state, mapName, soldiersRemaining));
            SteamFriends.SetRichPresence(SteamRichPresenceFormat.KeyDisplay, SteamRichPresenceFormat.TokenFor(state));
        }

        /// <summary>Örnek: <c>SetMatchRichPresence("Kuzgun Vadisi", 23)</c> → "Kuzgun Vadisi — 23 asker kaldı".</summary>
        public void SetMatchRichPresence(string mapDisplayName, int soldiersRemaining)
        {
            SetPresence(SteamPresenceState.InMatch, mapDisplayName, soldiersRemaining);
        }

        public void SetMenuRichPresence()
        {
            SetPresence(SteamPresenceState.Menu);
        }

        /// <summary>Steam arkadaş listesinde aynı timdekileri gruplar (boş id = grubu kaldır).</summary>
        public void SetSquadPresence(string squadId, int squadSize)
        {
            if (!_initialized)
                return;

            var has = !string.IsNullOrWhiteSpace(squadId) && squadSize > 0;
            SteamFriends.SetRichPresence(SteamRichPresenceFormat.KeyGroup, has ? squadId.Trim() : string.Empty);
            SteamFriends.SetRichPresence(SteamRichPresenceFormat.KeyGroupSize,
                has ? Mathf.Clamp(squadSize, 1, 10).ToString() : string.Empty);
        }

        public void ClearRichPresence()
        {
            if (!_initialized)
                return;

            SteamFriends.ClearRichPresence();
            _lastPresenceSignature = null;
        }

        // ——— Başarımlar ———

        /// <summary>
        /// Backend başarım id'sini Steam'de açar. Açıldıysa (ya da zaten açıksa) true.
        /// İstatistikler henüz gelmediyse kuyruğa alınır ve false döner.
        /// </summary>
        public bool UnlockAchievement(string backendAchievementId)
        {
            if (string.IsNullOrWhiteSpace(backendAchievementId))
                return false;

            UnlockAchievements(new[] { backendAchievementId });
            return IsAchievementUnlocked(backendAchievementId);
        }

        /// <summary>
        /// Birden çok backend başarımını açar; tek <c>StoreStats</c> çağrısı yapar.
        /// Dönen değer: bu çağrıda yeni açılan başarım sayısı.
        /// </summary>
        public int UnlockAchievements(IEnumerable<string> backendAchievementIds)
        {
            if (!_initialized || backendAchievementIds == null)
                return 0;

            if (!_statsReady)
            {
                foreach (var id in backendAchievementIds)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                        _pendingAchievements.Add(id.Trim());
                }

                return 0;
            }

            var newlySet = 0;
            foreach (var id in backendAchievementIds)
            {
                if (!SteamAchievementMap.TryGetSteamApiName(id, true, out var apiName))
                    continue;

                if (SteamUserStats.GetAchievement(apiName, out var achieved) && achieved)
                    continue;

                if (SteamUserStats.SetAchievement(apiName))
                {
                    newlySet++;
                }
                else if (_warnedApiNames.Add(apiName))
                {
                    Debug.LogWarning($"[Steam] Başarım açılamadı: {apiName} (backend '{id}'). Partner'da tanımlı mı? " +
                                     "Bkz. Scripts/Platform/Steamworks~/achievements_partner.csv");
                }
            }

            if (newlySet > 0)
                SteamUserStats.StoreStats();

            return newlySet;
        }

        public bool IsAchievementUnlocked(string backendAchievementId)
        {
            if (!_initialized || !_statsReady)
                return false;
            if (!SteamAchievementMap.TryGetSteamApiName(backendAchievementId, true, out var apiName))
                return false;
            return SteamUserStats.GetAchievement(apiName, out var achieved) && achieved;
        }

        // ——— Auth biletleri ———

        /// <summary>Web API bileti (önerilen). Callback gelene kadar bekler.</summary>
        public async Task<SteamAuthTicket> GetWebApiTicketAsync(CancellationToken ct = default)
        {
            EnsureInitialized();
            CancelAuthTicket();

            _webApiTicketBytes = null;
            _webApiTicketDone = false;
            var handle = SteamUser.GetAuthTicketForWebApi(SteamPlatformSettings.WebApiIdentity);
            if (handle == HAuthTicket.Invalid)
                throw new InvalidOperationException("Steam Web API bileti istenemedi.");

            _pendingWebApiHandle = handle;
            _activeTicket = handle;
            await WaitUntilAsync(() => _webApiTicketDone, "Steam Web API bileti", ct);

            var bytes = _webApiTicketBytes;
            if (bytes == null || bytes.Length == 0)
                throw new InvalidOperationException("Steam Web API bileti alınamadı.");

            return new SteamAuthTicket(SteamAuthTicketKind.WebApi, ToHex(bytes, bytes.Length), SteamPlatformSettings.WebApiIdentity);
        }

        /// <summary>Klasik oturum bileti (kimliksiz). Steam bileti doğrulanabilir hale gelene kadar bekler.</summary>
        public async Task<SteamAuthTicket> GetSessionTicketAsync(CancellationToken ct = default)
        {
            EnsureInitialized();
            CancelAuthTicket();

            var buffer = new byte[SessionTicketBufferSize];
            var identity = new SteamNetworkingIdentity();
            _sessionTicketDone = false;
            _sessionTicketResult = EResult.k_EResultNone;
            var handle = SteamUser.GetAuthSessionTicket(buffer, buffer.Length, out var size, ref identity);
            if (handle == HAuthTicket.Invalid || size == 0)
                throw new InvalidOperationException("Steam oturum bileti alınamadı.");

            _pendingSessionHandle = handle;
            _activeTicket = handle;
            await WaitUntilAsync(() => _sessionTicketDone, "Steam oturum bileti", ct);

            if (_sessionTicketResult != EResult.k_EResultOK)
                throw new InvalidOperationException("Steam oturum bileti reddedildi: " + _sessionTicketResult);

            return new SteamAuthTicket(SteamAuthTicketKind.Session, ToHex(buffer, (int)size), null);
        }

        /// <summary>Yalnızca editör / development build: backend <c>Steam:AllowDevTickets</c> için sahte bilet.</summary>
        public SteamAuthTicket GetDevTicket()
        {
            EnsureInitialized();
            return new SteamAuthTicket(SteamAuthTicketKind.Dev, $"DEV:{SteamId}:{PersonaName}", null);
        }

        void OnWebApiTicketReceived(GetTicketForWebApiResponse_t data)
        {
            if (data.m_hAuthTicket != _pendingWebApiHandle)
                return;

            if (data.m_eResult != EResult.k_EResultOK || data.m_cubTicket <= 0 || data.m_rgubTicket == null)
            {
                Debug.LogWarning($"[Steam] Web API bileti hatası: {data.m_eResult}");
                _webApiTicketBytes = null;
            }
            else
            {
                var length = Math.Min(data.m_cubTicket, data.m_rgubTicket.Length);
                var copy = new byte[length];
                Array.Copy(data.m_rgubTicket, copy, length);
                _webApiTicketBytes = copy;
            }

            _pendingWebApiHandle = HAuthTicket.Invalid;
            _webApiTicketDone = true;
        }

        void OnSessionTicketReceived(GetAuthSessionTicketResponse_t data)
        {
            if (data.m_hAuthTicket != _pendingSessionHandle)
                return;

            _sessionTicketResult = data.m_eResult;
            _pendingSessionHandle = HAuthTicket.Invalid;
            _sessionTicketDone = true;
        }

        /// <summary>Etkin bileti iptal eder (backend doğruladıktan sonra çağrılır).</summary>
        public void CancelAuthTicket()
        {
            if (_activeTicket == HAuthTicket.Invalid)
                return;

            try
            {
                if (_initialized)
                    SteamUser.CancelAuthTicket(_activeTicket);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Steam] Bilet iptal hatası: " + ex.Message);
            }

            _activeTicket = HAuthTicket.Invalid;
            _pendingWebApiHandle = HAuthTicket.Invalid;
            _pendingSessionHandle = HAuthTicket.Invalid;
        }

        // ——— Backend /auth/steam ———

        /// <summary>
        /// Steam biletiyle backend <c>POST /auth/steam</c> girişi yapar; başarılıysa ham <c>AuthResponse</c> JSON'u döner
        /// (accessToken, refreshToken, expiresAt, player).
        /// Sıra: WebApi bileti → (400/401/503) oturum bileti → (yalnızca editör/dev build) DEV bileti.
        /// Sunucuya ulaşılamazsa veya hesap yasaklıysa (403) tekrar denenmez.
        /// </summary>
        public async Task<string> AuthenticateWithBackendAsync(
            string backendBaseUrl,
            string region = SteamPlatformSettings.DefaultRegion,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(backendBaseUrl))
                throw new ArgumentException("backendBaseUrl gerekli.", nameof(backendBaseUrl));

            EnsureInitialized();
            if (_authInFlight)
                throw new InvalidOperationException("Steam girişi zaten sürüyor.");

            _authInFlight = true;
            try
            {
                var url = backendBaseUrl.TrimEnd('/') + SteamPlatformSettings.BackendAuthPath;
                Exception last = null;

                foreach (var kind in AuthAttemptOrder())
                {
                    ct.ThrowIfCancellationRequested();

                    SteamAuthTicket ticket;
                    try
                    {
                        ticket = await AcquireTicketAsync(kind, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        last = ex;
                        continue;
                    }

                    try
                    {
                        var body = BuildAuthRequestJson(ticket, PersonaName, region);
                        var response = await PostJsonAsync(url, body, ct);
                        if (response.Success)
                        {
                            BackendAuthenticated?.Invoke(response.Text);
                            return response.Text;
                        }

                        last = new SteamBackendAuthException(
                            response.StatusCode,
                            response.Text,
                            $"Steam girişi başarısız ({kind}, HTTP {response.StatusCode}): {response.Error}");

                        if (!ShouldTryNextTicket(response.StatusCode))
                            break;
                    }
                    finally
                    {
                        CancelAuthTicket();
                    }
                }

                throw last ?? new InvalidOperationException("Steam girişi yapılamadı.");
            }
            finally
            {
                _authInFlight = false;
            }
        }

        static IEnumerable<SteamAuthTicketKind> AuthAttemptOrder()
        {
            yield return SteamAuthTicketKind.WebApi;
            yield return SteamAuthTicketKind.Session;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            yield return SteamAuthTicketKind.Dev;
#endif
        }

        Task<SteamAuthTicket> AcquireTicketAsync(SteamAuthTicketKind kind, CancellationToken ct)
        {
            switch (kind)
            {
                case SteamAuthTicketKind.WebApi:
                    return GetWebApiTicketAsync(ct);
                case SteamAuthTicketKind.Session:
                    return GetSessionTicketAsync(ct);
                default:
                    return Task.FromResult(GetDevTicket());
            }
        }

        /// <summary>400/401: bilet türü kabul edilmedi; 503: Web API anahtarı yok (dev bileti işe yarayabilir).</summary>
        internal static bool ShouldTryNextTicket(long statusCode)
        {
            return statusCode == 400 || statusCode == 401 || statusCode == 503;
        }

        /// <summary>Backend <c>SteamAuthRequest(Ticket, PersonaName, Region)</c> + isteğe bağlı <c>identity</c>.</summary>
        internal static string BuildAuthRequestJson(SteamAuthTicket ticket, string personaName, string region)
        {
            var sb = new StringBuilder(ticket.Value.Length + 128);
            sb.Append("{\"ticket\":").Append(JsonString(ticket.Value));
            sb.Append(",\"personaName\":").Append(JsonString(string.IsNullOrWhiteSpace(personaName) ? null : personaName));
            sb.Append(",\"region\":").Append(JsonString(string.IsNullOrWhiteSpace(region) ? SteamPlatformSettings.DefaultRegion : region));
            sb.Append(",\"identity\":").Append(JsonString(ticket.Identity));
            sb.Append('}');
            return sb.ToString();
        }

        readonly struct HttpResult
        {
            public HttpResult(bool success, long statusCode, string text, string error)
            {
                Success = success;
                StatusCode = statusCode;
                Text = text ?? string.Empty;
                Error = error ?? string.Empty;
            }

            public bool Success { get; }
            public long StatusCode { get; }
            public string Text { get; }
            public string Error { get; }
        }

        static async Task<HttpResult> PostJsonAsync(string url, string json, CancellationToken ct)
        {
            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = HttpTimeoutSeconds;
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Accept", "application/json");

                var op = req.SendWebRequest();
                while (!op.isDone)
                {
                    if (ct.IsCancellationRequested)
                    {
                        req.Abort();
                        ct.ThrowIfCancellationRequested();
                    }

                    await Task.Yield();
                }

                var text = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
                var ok = req.result == UnityWebRequest.Result.Success;
                return new HttpResult(ok, req.responseCode, text, req.error);
            }
        }

        // ——— Yardımcılar ———

        void EnsureInitialized()
        {
            if (!_initialized)
                throw new InvalidOperationException("Steam başlatılmadı.");
        }

        static async Task WaitUntilAsync(Func<bool> done, string what, CancellationToken ct)
        {
            // Callback'ler Update içindeki SteamAPI.RunCallbacks ile gelir; burada yalnızca beklenir.
            var watch = Stopwatch.StartNew();
            while (!done())
            {
                ct.ThrowIfCancellationRequested();
                if (watch.Elapsed.TotalSeconds > TicketTimeoutSeconds)
                    throw new TimeoutException(what + " zaman aşımına uğradı.");
                await Task.Yield();
            }
        }

        internal static string JsonString(string value)
        {
            if (value == null)
                return "null";

            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }

            sb.Append('"');
            return sb.ToString();
        }

        internal static string ToHex(byte[] data, int length)
        {
            var sb = new StringBuilder(length * 2);
            for (var i = 0; i < length; i++)
                sb.Append(data[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
