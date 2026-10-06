using System;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure.Events;
using Project.Infrastructure.Network;
using Project.Infrastructure.Persistence;

namespace Project.Infrastructure.DI
{
    /// <summary>
    /// Sahne kapsamlı kompozisyon kökü: maçın tüm uygulama servislerini oluşturur ve <see cref="ServiceContainer"/>'a
    /// hem somut tipleriyle hem de arayüzleriyle kaydeder (CONTRACTS §2). Bootstrap sonucu <see cref="GameContext.Set"/>
    /// ile yayınlar; sahne kapanırken <see cref="ServiceContainer.DisposeAll"/> abonelikleri temizler.
    /// Kayıtlar:
    ///  IEventBus (+EventBus), IRandom (+SeededRandom), SimulationClock (+ISimulationClock), GameTickCoordinator (+IGameTickService),
    ///  MatchService (+IMatchService, ICombatantDirectory, ITeamRelations), ZoneService (+IZoneService), CombatService,
    ///  DamageableRegistry (+IDamageableRegistry), LootSpawnService (+ILootSpawnService), MatchStatsService, KillFeedService,
    ///  SquadOrderService, ArtilleryService, ChainOfCommandService, INetworkSession (+OfflineNetworkSession), SettingsService,
    ///  CareerStatsService, MatchConfig.
    /// Tick sırası: maç → bölge → topçu.
    /// </summary>
    public static class GameCompositionRoot
    {
        /// <summary>Simülasyon tick hızı (Hz).</summary>
        public const float TickRate = 30f;

        /// <summary>Yerel oyuncunun varsayılan kimliği (tim 0, slot 0).</summary>
        public static readonly PlayerId DefaultLocalPlayerId = new(1);

        /// <summary>
        /// Online adaptörü için ağ oturumu fabrikası (ör. Netcode istemci/adanmış sunucu oturumu). Atanmışsa
        /// <see cref="Build(MatchConfig, SettingsService, CareerStatsService, PlayerId)"/> oturumu bununla oluşturur
        /// (parametre: yerel oyuncu kimliği); null ise ya da hata verirse çevrimdışı oturum (her zaman otorite) kullanılır.
        /// </summary>
        public static Func<PlayerId, INetworkSession> NetworkSessionFactory { get; set; }

        public static ServiceContainer Build(MatchConfig config, SettingsService settings)
        {
            return Build(config, settings, null, DefaultLocalPlayerId);
        }

        /// <summary>
        /// Tam kurulum. settings/career null ise PlayerPrefs deposundan yüklenir; config null ise varsayılan yapılandırma.
        /// </summary>
        public static ServiceContainer Build(MatchConfig config, SettingsService settings, CareerStatsService career, PlayerId localPlayerId)
        {
            config ??= new MatchConfig();
            if (config.ZonePhases == null)
                config.ZonePhases = MatchConfig.DefaultZonePhases();

            var container = new ServiceContainer();

            // Ayarlar / kariyer (sahneler arası GameSession'dan gelir; yoksa burada yüklenir).
            ISettingsStore store = null;
            if (settings == null || career == null)
                store = CreateStore();

            if (settings == null)
            {
                settings = new SettingsService(store);
                SafeLoad(settings.Load);
            }

            if (career == null)
            {
                career = new CareerStatsService(store);
                SafeLoad(career.Load);
            }

            container.RegisterSingleton(settings);
            container.RegisterSingleton(career);
            container.RegisterSingleton(AchievementServiceProvider.GetOrCreate(store ?? CreateStore()));
            container.RegisterSingleton(config);

            // Ağ oturumu (çevrimdışı: her zaman otorite; online adaptörü fabrika ile verir).
            var network = CreateNetworkSession(localPlayerId);
            container.RegisterSingleton(network);
            if (network is OfflineNetworkSession offline)
                container.RegisterSingleton(offline);

            // Olay yolu ve rastgelelik. Alt sistemler ayrı tohum akışları kullanır ki biri diğerinin sırasını bozmasın.
            var seed = config.RandomSeed;
            var eventBus = new EventBus();
            container.RegisterSingleton<IEventBus>(eventBus);
            container.RegisterSingleton(eventBus);

            var random = new SeededRandom(seed);
            container.RegisterSingleton<IRandom>(random);
            container.RegisterSingleton(random);

            // Simülasyon saati.
            var clock = new SimulationClock(TickRate);
            container.RegisterSingleton(clock);
            container.RegisterSingleton<ISimulationClock>(clock);

            // Maç.
            var match = new MatchService(config, eventBus);
            container.RegisterSingleton(match);
            container.RegisterSingleton<IMatchService>(match);
            container.RegisterSingleton<ICombatantDirectory>(match);
            container.RegisterSingleton<ITeamRelations>(match);

            // Harekât alanı (mavi bölge).
            var zone = new ZoneService(config.ZonePhases, new SeededRandom(DeriveSeed(seed, 0x2F0E)), eventBus, config.MapHalfSize);
            var initialRadius = config.InitialZoneRadius > 0f ? config.InitialZoneRadius : config.MapHalfSize * 1.45f;
            var firstDamage = config.ZonePhases.Length > 0 ? config.ZonePhases[0].DamagePerSecond : 1f;
            zone.Initialize(0f, 0f, initialRadius, firstDamage);
            container.RegisterSingleton(zone);
            container.RegisterSingleton<IZoneService>(zone);

            // Çatışma.
            var damageables = new DamageableRegistry();
            container.RegisterSingleton(damageables);
            container.RegisterSingleton<IDamageableRegistry>(damageables);

            var combat = new CombatService(eventBus, damageables, match, config.FriendlyFire);
            container.RegisterSingleton(combat);

            // Yağma.
            var lootSpawn = new LootSpawnService();
            container.RegisterSingleton(lootSpawn);
            container.RegisterSingleton<ILootSpawnService>(lootSpawn);

            // İstatistik, öldürme akışı.
            var stats = new MatchStatsService(eventBus, match, match);
            container.RegisterSingleton(stats);

            var killFeed = new KillFeedService(eventBus, match);
            container.RegisterSingleton(killFeed);

            // Komuta: tim emirleri, topçu, komuta zinciri.
            var squadOrders = new SquadOrderService(eventBus);
            container.RegisterSingleton(squadOrders);

            var artillery = new ArtilleryService(eventBus, new SeededRandom(DeriveSeed(seed, 0xA271)), config.ArtilleryCooldownSeconds);
            container.RegisterSingleton(artillery);

            var chain = new ChainOfCommandService(eventBus);
            container.RegisterSingleton(chain);

            // İkmal düşürme (bot hedefleme ve sandık açılışı buna bağlı); InMatch'e geçişte kendi kendine silahlanır.
            var airdrop = new AirdropService(eventBus, zone, new SeededRandom(DeriveSeed(seed, 0xA1D0)), config);
            container.RegisterSingleton(airdrop);

            // Sabit tick: maç → bölge → topçu → ikmal düşürme.
            var tick = new GameTickCoordinator(match, zone, artillery, airdrop);
            container.RegisterSingleton(tick);
            container.RegisterSingleton<IGameTickService>(tick);

            return container;
        }

        /// <summary>Eski imza (oyuncu sayısı artık tim sayısından türetilir).</summary>
        [Obsolete("Build(MatchConfig, SettingsService) kullanın.")]
        public static ServiceContainer Build(MatchConfig matchConfig, int initialPlayerCount)
        {
            matchConfig ??= new MatchConfig();
            if (initialPlayerCount > 0)
            {
                var teams = Math.Max(2, (initialPlayerCount + matchConfig.TeamSize - 1) / Math.Max(1, matchConfig.TeamSize));
                matchConfig.WithTeams(teams, matchConfig.TeamSize);
            }

            return Build(matchConfig, (SettingsService)null);
        }

        /// <summary>Alt sistem için ana tohumdan türetilmiş tohum.</summary>
        public static int DeriveSeed(int seed, int salt)
        {
            unchecked
            {
                var h = seed * 486187739 + salt * 16777619;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return h & 0x7fffffff;
            }
        }

        private static INetworkSession CreateNetworkSession(PlayerId localPlayerId)
        {
            var factory = NetworkSessionFactory;
            if (factory != null)
            {
                try
                {
                    var session = factory(localPlayerId);
                    if (session != null)
                        return session;

                    UnityEngine.Debug.LogWarning("[GameCompositionRoot] Ağ oturumu fabrikası null döndürdü — çevrimdışı oturum kullanılıyor.");
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError("[GameCompositionRoot] Ağ oturumu oluşturulamadı — çevrimdışı oturum kullanılıyor: " + e.Message);
                    UnityEngine.Debug.LogException(e);
                }
            }

            return new OfflineNetworkSession(localPlayerId);
        }

        private static ISettingsStore CreateStore()
        {
            try
            {
                return new PlayerPrefsSettingsStore();
            }
            catch (Exception)
            {
                return null;
            }
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Etki alanı yeniden yüklemesi kapalıyken önceki oturumun fabrikası taşınmasın.
            NetworkSessionFactory = null;
        }

        private static void SafeLoad(Action load)
        {
            try
            {
                load();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }
    }
}
