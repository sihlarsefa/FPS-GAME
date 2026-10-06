using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Loot;
using Project.Infrastructure.World;
using UnityEngine;
using UnityEngine.AI;

namespace Project.Infrastructure.AI
{
    /// <summary>
    /// Sahne kapsamlı bot koordinatörü (tek örnek, ilk bot oluşturulurken kendiliğinden kurulur):
    ///  • Servis önbelleği (bölge, maç, komuta zinciri, tim emirleri, topçu) — botlar her karede GameContext'e gitmez.
    ///  • Silah sesi halka tamponu (tek WeaponFiredEvent aboneliği; botlar kendi algı adımlarında okur).
    ///  • Tim istihbaratı: komutan çözümü, kama düzeni yönü ve takipçi sıraları, son düşman teması, yardım çağrısı.
    ///  • Yapay zekâ timi komutanlarının hedef seçimi (temas / bölge / yağma bölgesi) ve topçu çağrısı.
    ///  • NavMesh yol isteği bütçesi (kare başına sınırlı) ve yağma rezervasyonları (iki bot aynı eşyaya koşmasın).
    /// Yalnızca otoritede karar üretir; GC'siz çalışır (önceden ayrılmış tamponlar).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BotDirector : MonoBehaviour
    {
        public const int GunfireCapacity = 96;
        public const int DefaultPathRequestsPerFrame = 6;

        /// <summary>Tim temas bilgisinin "taze" sayıldığı süre (sn).</summary>
        public const float ContactMemorySeconds = 25f;

        private const float TeamRefreshInterval = 0.5f;
        private const float ServiceRefreshInterval = 1f;
        private const float ArtilleryCheckInterval = 4f;
        private const float ArtilleryMinDistance = 55f;
        private const float ArtilleryMaxDistance = 320f;
        private const float ArtilleryAllySafetyRadius = 42f;
        private const float ArtilleryChance = 0.4f;
        private const float ObjectiveReachedDistance = 14f;

        public struct GunfireRecord
        {
            public Vector3 Position;
            public PlayerId Shooter;
            public int Team;
            public float Time;
            public float Loudness;
        }

        internal sealed class TeamIntel
        {
            public int Team;
            public readonly List<BotController> Bots = new(12);
            public Combatant Commander;
            public float FormationYaw;
            public bool HasFormationYaw;

            public Vector3 ContactPosition;
            public float ContactTime = -999f;
            public PlayerId ContactEnemy = PlayerId.Invalid;

            public Vector3 HelpPosition;
            public float HelpTime = -999f;

            public Vector3 Objective;
            public bool HasObjective;
            public bool ObjectiveIsAttack;
            public float NextObjectiveTime;
            public int LastLocationIndex = -1;
            public int PreviousLocationIndex = -1;

            public float NextArtilleryCheck;
            public float Aggression = 0.5f;
        }

        private static BotDirector _instance;

        private readonly List<BotController> _bots = new(64);
        private readonly Dictionary<int, TeamIntel> _teams = new();
        private readonly List<TeamIntel> _teamList = new(16);
        private readonly GunfireRecord[] _gunfire = new GunfireRecord[GunfireCapacity];
        private readonly Dictionary<string, float> _loudnessCache = new(StringComparer.Ordinal);
        private readonly Dictionary<LootPickupComponent, BotController> _lootClaims = new();
        private readonly List<LootPickupComponent> _claimScratch = new(16);
        private readonly System.Random _rng = new(7177);

        private int _gunfireSequence;
        private int _pathFrame = -1;
        private int _pathRequestsThisFrame;
        private float _nextTeamRefresh;
        private float _nextServiceRefresh;
        private float _nextClaimCleanup;
        private object _servicesSeen;

        private IEventBus _bus;
        private Action<WeaponFiredEvent> _onWeaponFired;
        private Action<CommandTransferredEvent> _onCommandTransferred;
        private Action<SquadOrderIssuedEvent> _onSquadOrder;
        private Action<AirdropEvent> _onAirdrop;
        private Vector3 _airdropPosition;
        private bool _airdropActive;
        private float _airdropExpire;

        public static BotDirector Instance => _instance;

        /// <summary>Var olan koordinatörü döndürür ya da sahnede yenisini kurar.</summary>
        public static BotDirector GetOrCreate()
        {
            if (_instance != null)
                return _instance;

            var go = new GameObject("[BotDirector]");
            return go.AddComponent<BotDirector>();
        }

        public IReadOnlyList<BotController> Bots => _bots;

        /// <summary>Kare başına izin verilen NavMesh yol isteği (60 bot için ~6).</summary>
        public int MaxPathRequestsPerFrame { get; set; } = DefaultPathRequestsPerFrame;

        /// <summary>Şimdiye kadar kaydedilen toplam silah sesi (halka tamponun mutlak sırası).</summary>
        public int GunfireSequence => _gunfireSequence;

        // ------------------------------------------------------------------ servis önbelleği

        public IZoneService Zone { get; private set; }
        public IMatchService Match { get; private set; }
        public ITeamRelations Relations { get; private set; }
        public ChainOfCommandService Chain { get; private set; }
        public SquadOrderService Orders { get; private set; }
        public ArtilleryService Artillery { get; private set; }
        public MatchConfig Config { get; private set; }

        /// <summary>Maç bitti mi (Ending) — botlar ateşi keser.</summary>
        public bool MatchEnded
        {
            get
            {
                try
                {
                    return Match != null && Match.CurrentPhase == MatchPhase.Ending;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        // ------------------------------------------------------------------ yaşam döngüsü

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                enabled = false;
                Destroy(this);
                return;
            }

            _instance = this;
            _onWeaponFired = OnWeaponFired;
            _onCommandTransferred = OnCommandTransferred;
            _onSquadOrder = OnSquadOrder;
            _onAirdrop = OnAirdrop;
            RefreshServices(true);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (_instance == this)
                _instance = null;

            _bots.Clear();
            _teams.Clear();
            _teamList.Clear();
            _lootClaims.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        private void Update()
        {
            var now = Time.time;
            if (now >= _nextServiceRefresh || !ReferenceEquals(_servicesSeen, GameContext.Services))
                RefreshServices(false);

            if (!GameContext.HasAuthority)
                return;

            if (now >= _nextTeamRefresh)
            {
                _nextTeamRefresh = now + TeamRefreshInterval;
                for (var i = 0; i < _teamList.Count; i++)
                {
                    try
                    {
                        RefreshTeam(_teamList[i], now);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e, this);
                    }
                }
            }

            if (now >= _nextClaimCleanup)
            {
                _nextClaimCleanup = now + 10f;
                CleanupClaims();
            }
        }

        // ------------------------------------------------------------------ bot kaydı

        internal void Register(BotController bot)
        {
            if (bot == null || _bots.Contains(bot))
                return;

            _bots.Add(bot);
            var intel = GetOrCreateTeam(bot.Team);
            if (!intel.Bots.Contains(bot))
                intel.Bots.Add(bot);

            intel.Aggression = Mathf.Max(intel.Aggression, bot.Profile != null ? bot.Profile.AggressionChance : 0.5f);
            _nextTeamRefresh = 0f;
        }

        internal void Unregister(BotController bot)
        {
            if (bot == null)
                return;

            _bots.Remove(bot);
            if (_teams.TryGetValue(bot.Team, out var intel))
                intel.Bots.Remove(bot);

            ReleaseClaims(bot);
            _nextTeamRefresh = 0f;
        }

        internal TeamIntel GetTeam(int team) => _teams.TryGetValue(team, out var intel) ? intel : null;

        private TeamIntel GetOrCreateTeam(int team)
        {
            if (_teams.TryGetValue(team, out var intel))
                return intel;

            intel = new TeamIntel { Team = team, NextObjectiveTime = 0f, NextArtilleryCheck = Time.time + 20f };
            _teams[team] = intel;
            _teamList.Add(intel);
            return intel;
        }

        // ------------------------------------------------------------------ komuta / düzen

        /// <summary>Timin komutanı (komuta zinciri → yedek: insan oyuncu, sonra en kıdemli canlı bot).</summary>
        public Combatant GetCommander(int team)
        {
            var intel = GetTeam(team);
            if (intel == null)
                return ResolveCommander(team);

            if (intel.Commander == null || !intel.Commander.IsAlive)
                intel.Commander = ResolveCommander(team);

            return intel.Commander;
        }

        /// <summary>Kama düzeninin yönü (derece). Komutan hareket ettikçe yumuşakça güncellenir.</summary>
        public float GetFormationYaw(int team, Combatant leader)
        {
            var intel = GetTeam(team);
            if (intel != null && intel.HasFormationYaw)
                return intel.FormationYaw;

            return leader != null ? leader.transform.eulerAngles.y : 0f;
        }

        private Combatant ResolveCommander(int team)
        {
            var chain = Chain;
            if (chain != null)
            {
                try
                {
                    var id = chain.GetCommander(team);
                    if (id.IsValid && CombatantRegistry.TryGet(id, out var commander) && commander != null && commander.IsAlive)
                        return commander;
                }
                catch (Exception)
                {
                    // zincir hazır değil — yedek çözüm
                }
            }

            // Yedek: insan oyuncu > en yüksek rütbe > en küçük kimlik.
            Combatant best = null;
            var all = CombatantRegistry.All;
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !c.IsAlive || c.Team != team)
                    continue;

                if (best == null || IsBetterCommander(c, best))
                    best = c;
            }

            return best;
        }

        private static bool IsBetterCommander(Combatant a, Combatant b)
        {
            if (a.IsBot != b.IsBot)
                return !a.IsBot;

            if (a.Rank != b.Rank)
                return a.Rank > b.Rank;

            if ((a.Role == TeamRole.Leader) != (b.Role == TeamRole.Leader))
                return a.Role == TeamRole.Leader;

            return a.Id.Value < b.Id.Value;
        }

        private void RefreshTeam(TeamIntel intel, float now)
        {
            intel.Commander = ResolveCommander(intel.Team);
            var commander = intel.Commander;

            // Takipçi sırası: komuta zinciri sırası (varsa), yoksa kayıt sırası.
            var followerIndex = 0;
            for (var i = 0; i < intel.Bots.Count; i++)
            {
                var bot = intel.Bots[i];
                if (bot == null || bot.Combatant == null || !bot.Combatant.IsAlive)
                    continue;

                if (commander != null && ReferenceEquals(bot.Combatant, commander))
                {
                    bot.FormationIndex = 0;
                    continue;
                }

                var index = -1;
                if (Chain != null)
                {
                    try
                    {
                        index = Chain.GetChainIndex(bot.Combatant.Id);
                    }
                    catch (Exception)
                    {
                        index = -1;
                    }
                }

                bot.FormationIndex = index > 0 ? index : ++followerIndex;
                if (index > followerIndex)
                    followerIndex = index;
            }

            // Düzen yönü: komutanın hareket yönü (dururken son yön korunur).
            if (commander != null)
            {
                var v = commander.Velocity;
                v.y = 0f;
                if (v.sqrMagnitude > 1.2f)
                {
                    var target = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
                    intel.FormationYaw = intel.HasFormationYaw
                        ? Mathf.MoveTowardsAngle(intel.FormationYaw, target, 70f)
                        : target;
                    intel.HasFormationYaw = true;
                }
                else if (!intel.HasFormationYaw)
                {
                    intel.FormationYaw = commander.transform.eulerAngles.y;
                    intel.HasFormationYaw = true;
                }
            }

            // Yapay zekâ komutanı: hedef ve topçu.
            if (commander != null && commander.IsBot && commander.DropState == DropState.Landed)
            {
                UpdateObjective(intel, commander, now);
                if (now >= intel.NextArtilleryCheck)
                {
                    intel.NextArtilleryCheck = now + ArtilleryCheckInterval;
                    TryCallArtillery(intel, now);
                    TryLaunchRecon(intel, now);
                    TryCallAttackHeli(intel, commander, now);
                }
            }
        }

        // ------------------------------------------------------------------ istihbarat

        /// <summary>Bir tim üyesi düşman gördü: tim temas noktası ve yardım çağrısı güncellenir.</summary>
        internal void ReportContact(int team, Vector3 enemyPosition, PlayerId enemy, float now)
        {
            var intel = GetOrCreateTeam(team);
            intel.ContactPosition = enemyPosition;
            intel.ContactTime = now;
            intel.ContactEnemy = enemy;
            intel.HelpPosition = enemyPosition;
            intel.HelpTime = now;
        }

        /// <summary>Oyuncu tim ping'i (düşman işareti): tim botları konumu araştırmaya gelir.</summary>
        public void ReportPing(int team, Vector3 enemyPosition, float now)
        {
            var intel = GetOrCreateTeam(team);
            intel.ContactPosition = enemyPosition;
            intel.ContactTime = now;
            intel.HelpPosition = enemyPosition;
            intel.HelpTime = now;
        }

        /// <summary>Bir tim üyesi vuruldu: yakındaki tim arkadaşları yardıma gelir.</summary>
        internal void ReportUnderFire(int team, Vector3 victimPosition, Vector3 threatPosition, bool knowsThreat, float now)
        {
            var intel = GetOrCreateTeam(team);
            intel.HelpPosition = knowsThreat ? threatPosition : victimPosition;
            intel.HelpTime = now;
            if (knowsThreat && now - intel.ContactTime > 2f)
            {
                intel.ContactPosition = threatPosition;
                intel.ContactTime = now;
            }
        }

        internal bool TryGetHelpRequest(int team, float now, float maxAge, out Vector3 position)
        {
            if (_teams.TryGetValue(team, out var intel) && now - intel.HelpTime <= maxAge)
            {
                position = intel.HelpPosition;
                return true;
            }

            position = default;
            return false;
        }

        internal bool TryGetContact(int team, float now, float maxAge, out Vector3 position)
        {
            if (_teams.TryGetValue(team, out var intel) && now - intel.ContactTime <= maxAge)
            {
                position = intel.ContactPosition;
                return true;
            }

            position = default;
            return false;
        }

        /// <summary>Yapay zekâ timi komutanının güncel hedefi.</summary>
        public bool TryGetObjective(int team, out Vector3 objective)
        {
            if (_teams.TryGetValue(team, out var intel) && intel.HasObjective)
            {
                objective = intel.Objective;
                return true;
            }

            objective = default;
            return false;
        }

        /// <summary>Komutan hedefe vardı / ulaşamadı: kısa süre içinde yeni hedef seçilir.</summary>
        internal void InvalidateObjective(int team)
        {
            if (_teams.TryGetValue(team, out var intel))
                intel.NextObjectiveTime = Mathf.Min(intel.NextObjectiveTime, Time.time + 0.5f);
        }

        // ------------------------------------------------------------------ silah sesi tamponu

        /// <summary>Mutlak sıradaki silah sesi kaydı (tampondan taşmışsa false).</summary>
        public bool TryGetGunfire(int sequence, out GunfireRecord record)
        {
            if (sequence < 0 || sequence >= _gunfireSequence || sequence < _gunfireSequence - GunfireCapacity)
            {
                record = default;
                return false;
            }

            record = _gunfire[sequence % GunfireCapacity];
            return true;
        }

        /// <summary>Doğrudan kayıt (olay veri yolu yoksa ya da test için).</summary>
        public void RecordGunfire(Vector3 position, PlayerId shooter, int team, float loudness)
        {
            var index = _gunfireSequence % GunfireCapacity;
            _gunfire[index] = new GunfireRecord
            {
                Position = position,
                Shooter = shooter,
                Team = team,
                Time = Time.time,
                Loudness = loudness > 0f ? loudness : 1f
            };
            _gunfireSequence++;
        }

        private void OnWeaponFired(WeaponFiredEvent e)
        {
            Vector3 position;
            var team = -1;
            Combatant shooter = null;
            if (CombatantRegistry.TryGet(e.ShooterId, out var c) && c != null)
            {
                shooter = c;
                team = c.Team;
            }

            if (e.HasOrigin)
                position = new Vector3(e.Origin.X, e.Origin.Y, e.Origin.Z);
            else if (shooter != null)
                position = shooter.transform.position;
            else
                return;

            if (team < 0 && Relations != null)
            {
                try
                {
                    team = Relations.GetTeam(e.ShooterId);
                }
                catch (Exception)
                {
                    team = -1;
                }
            }

            RecordGunfire(position, e.ShooterId, team, LoudnessOf(e.WeaponId));
        }

        private float LoudnessOf(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId))
                return 1f;

            if (_loudnessCache.TryGetValue(weaponId, out var cached))
                return cached;

            var loudness = 1f;
            if (WeaponCatalog.TryGet(weaponId, out var def) && def != null)
            {
                switch (def.Category)
                {
                    case WeaponCategory.Pistol: loudness = 0.65f; break;
                    case WeaponCategory.Smg: loudness = 0.8f; break;
                    case WeaponCategory.Shotgun: loudness = 1f; break;
                    case WeaponCategory.Dmr: loudness = 1.15f; break;
                    case WeaponCategory.Lmg: loudness = 1.25f; break;
                    case WeaponCategory.Sniper: loudness = 1.45f; break;
                    default: loudness = 1f; break;
                }
            }

            _loudnessCache[weaponId] = loudness;
            return loudness;
        }

        private void OnCommandTransferred(CommandTransferredEvent e)
        {
            if (_teams.TryGetValue(e.Team, out var intel))
            {
                intel.Commander = null;
                intel.NextObjectiveTime = 0f;
            }

            _nextTeamRefresh = 0f;
            for (var i = 0; i < _bots.Count; i++)
            {
                var bot = _bots[i];
                if (bot != null && bot.Team == e.Team)
                    bot.RequestDecision();
            }
        }

        private void OnSquadOrder(SquadOrderIssuedEvent e)
        {
            for (var i = 0; i < _bots.Count; i++)
            {
                var bot = _bots[i];
                if (bot != null && bot.Team == e.Team)
                    bot.RequestDecision();
            }
        }

        // ------------------------------------------------------------------ yol bütçesi

        /// <summary>Bu karede bir NavMesh yol isteği yapılabilir mi (bütçe tüketilir).</summary>
        public bool TryConsumePathRequest()
        {
            var frame = Time.frameCount;
            if (frame != _pathFrame)
            {
                _pathFrame = frame;
                _pathRequestsThisFrame = 0;
            }

            if (_pathRequestsThisFrame >= Mathf.Max(1, MaxPathRequestsPerFrame))
                return false;

            _pathRequestsThisFrame++;
            return true;
        }

        // ------------------------------------------------------------------ yağma rezervasyonu

        public bool IsClaimedByOther(LootPickupComponent loot, BotController bot)
        {
            if (loot == null)
                return false;

            return _lootClaims.TryGetValue(loot, out var owner) && owner != null && owner != bot &&
                   owner.Combatant != null && owner.Combatant.IsAlive;
        }

        public void Claim(LootPickupComponent loot, BotController bot)
        {
            if (loot == null || bot == null)
                return;

            _lootClaims[loot] = bot;
        }

        public void ReleaseClaim(LootPickupComponent loot, BotController bot)
        {
            if (loot == null)
                return;

            if (_lootClaims.TryGetValue(loot, out var owner) && owner == bot)
                _lootClaims.Remove(loot);
        }

        public void ReleaseClaims(BotController bot)
        {
            if (_lootClaims.Count == 0)
                return;

            _claimScratch.Clear();
            foreach (var pair in _lootClaims)
            {
                if (pair.Value == bot || pair.Value == null)
                    _claimScratch.Add(pair.Key);
            }

            for (var i = 0; i < _claimScratch.Count; i++)
                _lootClaims.Remove(_claimScratch[i]);

            _claimScratch.Clear();
        }

        private void CleanupClaims()
        {
            if (_lootClaims.Count == 0)
                return;

            _claimScratch.Clear();
            foreach (var pair in _lootClaims)
            {
                if (pair.Key == null || !pair.Key.IsAvailable || pair.Value == null ||
                    pair.Value.Combatant == null || !pair.Value.Combatant.IsAlive)
                    _claimScratch.Add(pair.Key);
            }

            for (var i = 0; i < _claimScratch.Count; i++)
                _lootClaims.Remove(_claimScratch[i]);

            _claimScratch.Clear();
        }

        // ------------------------------------------------------------------ komutan hedefi

        private void UpdateObjective(TeamIntel intel, Combatant commander, float now)
        {
            var position = commander.transform.position;
            var reached = intel.HasObjective && FlatDistance(position, intel.Objective) < ObjectiveReachedDistance;

            // Bölge dışına düşen hedef geçersizdir.
            var zone = Zone;
            var zoneActive = zone != null && SafeZoneActive(zone);
            if (intel.HasObjective && zoneActive && !SafeNextZoneContains(zone, intel.Objective))
                reached = true;

            if (intel.HasObjective && !reached && now < intel.NextObjectiveTime)
                return;

            ChooseObjective(intel, commander, position, now, zoneActive);
        }

        private void ChooseObjective(TeamIntel intel, Combatant commander, Vector3 position, float now, bool zoneActive)
        {
            var zone = Zone;

            // 1) Taze düşman teması → taarruz (tim saldırganlığı ile).
            if (now - intel.ContactTime < ContactMemorySeconds &&
                FlatDistance(position, intel.ContactPosition) < 260f &&
                NextFloat() < Mathf.Clamp01(0.35f + intel.Aggression * 0.6f) &&
                (!zoneActive || SafeNextZoneContains(zone, intel.ContactPosition)))
            {
                SetObjective(intel, intel.ContactPosition, true, now, 15f, 25f);
                return;
            }

            // 2) Sonraki güvenli bölgenin dışındaysa bölgeye intikal.
            if (zoneActive && !SafeNextZoneContains(zone, position))
            {
                SetObjective(intel, PointInsideNextZone(zone, position, 0.55f), false, now, 20f, 30f);
                return;
            }

            // 2b) Duyurulan ikmal sandığı (yakın ve bölge içindeyse; her tim değil, saldırgan olanlar).
            if (_airdropActive && Time.time > _airdropExpire)
                _airdropActive = false;
            if (_airdropActive && FlatDistance(position, _airdropPosition) < 320f &&
                (!zoneActive || SafeNextZoneContains(zone, _airdropPosition)) &&
                NextFloat() < Mathf.Clamp01(0.25f + intel.Aggression * 0.5f))
            {
                SetObjective(intel, _airdropPosition, false, now, 20f, 40f);
                return;
            }

            // 3) Adlandırılmış yerleşim (yağma bölgesi).
            if (TryPickLocation(intel, position, zoneActive, out var location))
            {
                SetObjective(intel, location, false, now, 40f, 70f);
                return;
            }

            // 4) Yerdeki yağma kümesi.
            if (TryPickLootPoint(position, zoneActive, out var lootPoint))
            {
                SetObjective(intel, lootPoint, false, now, 30f, 50f);
                return;
            }

            // 5) Rastgele keşif noktası (bölge içinde).
            var angle = NextFloat() * Mathf.PI * 2f;
            var distance = 60f + NextFloat() * 90f;
            var candidate = position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * distance;
            if (zoneActive && !SafeNextZoneContains(zone, candidate))
                candidate = PointInsideNextZone(zone, position, 0.5f);

            SetObjective(intel, ClampToMap(candidate), false, now, 25f, 40f);
        }

        private void SetObjective(TeamIntel intel, Vector3 point, bool attack, float now, float minSeconds, float maxSeconds)
        {
            if (NavMesh.SamplePosition(point, out var hit, 30f, NavMesh.AllAreas))
                point = hit.position;

            intel.Objective = point;
            intel.HasObjective = true;
            intel.ObjectiveIsAttack = attack;
            intel.NextObjectiveTime = now + minSeconds + NextFloat() * (maxSeconds - minSeconds);
        }

        private bool TryPickLocation(TeamIntel intel, Vector3 position, bool zoneActive, out Vector3 result)
        {
            result = default;
            WorldMetadata world;
            try
            {
                world = WorldMetadata.Instance;
            }
            catch (Exception)
            {
                return false;
            }

            if (world == null || world.Locations == null || world.Locations.Count == 0)
                return false;

            var locations = world.Locations;
            var bestIndex = -1;
            var bestScore = float.MaxValue;
            var zone = Zone;
            for (var i = 0; i < locations.Count; i++)
            {
                var location = locations[i];
                if (location == null || i == intel.LastLocationIndex || i == intel.PreviousLocationIndex)
                    continue;

                var center = new Vector3(location.Center.x, position.y, location.Center.y);
                if (zoneActive && !SafeNextZoneContains(zone, center))
                    continue;

                var distance = FlatDistance(position, center);
                if (distance < location.Radius * 0.6f || distance > 420f)
                    continue;

                // Yakın ve büyük yerleşimler tercih edilir; biraz rastgelelik timleri dağıtır.
                var score = distance * (location.IsMajor ? 0.8f : 1f) * (0.75f + NextFloat() * 0.6f);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
                return false;

            var chosen = locations[bestIndex];
            intel.PreviousLocationIndex = intel.LastLocationIndex;
            intel.LastLocationIndex = bestIndex;

            var offsetAngle = NextFloat() * Mathf.PI * 2f;
            var offset = new Vector3(Mathf.Sin(offsetAngle), 0f, Mathf.Cos(offsetAngle)) * (chosen.Radius * 0.35f * NextFloat());
            var y = position.y;
            try
            {
                y = world.SampleGroundHeight(new Vector3(chosen.Center.x, 0f, chosen.Center.y));
            }
            catch (Exception)
            {
                // yükseklik bilinmiyor — NavMesh örneklemesi düzeltir
            }

            result = new Vector3(chosen.Center.x, y, chosen.Center.y) + offset;
            return true;
        }

        private bool TryPickLootPoint(Vector3 position, bool zoneActive, out Vector3 result)
        {
            result = default;
            IReadOnlyList<LootPickupComponent> all;
            try
            {
                all = LootRegistry.All;
            }
            catch (Exception)
            {
                return false;
            }

            if (all == null || all.Count == 0)
                return false;

            var zone = Zone;
            var found = false;
            var bestScore = float.MaxValue;
            var samples = Mathf.Min(16, all.Count);
            for (var s = 0; s < samples; s++)
            {
                var loot = all[_rng.Next(0, all.Count)];
                if (loot == null || !loot.IsAvailable)
                    continue;

                var p = loot.transform.position;
                var distance = FlatDistance(position, p);
                if (distance < 30f || distance > 300f)
                    continue;

                if (zoneActive && !SafeNextZoneContains(zone, p))
                    continue;

                var score = distance * (0.7f + NextFloat() * 0.6f);
                if (score < bestScore)
                {
                    bestScore = score;
                    result = p;
                    found = true;
                }
            }

            return found;
        }

        // ------------------------------------------------------------------ topçu

        private void TryCallArtillery(TeamIntel intel, float now)
        {
            var artillery = Artillery;
            if (artillery == null || MatchEnded)
                return;

            if (now - intel.ContactTime > 4f)
                return;

            bool ready;
            try
            {
                ready = artillery.IsReady(intel.Team);
            }
            catch (Exception)
            {
                return;
            }

            if (!ready || NextFloat() > ArtilleryChance)
                return;

            // Çağıran: hayatta bir telsizci, yoksa tim komutanı (Leader görevi).
            BotController caller = null;
            for (var i = 0; i < intel.Bots.Count; i++)
            {
                var bot = intel.Bots[i];
                if (bot == null || bot.Combatant == null || !bot.Combatant.IsAlive || bot.Combatant.IsDowned || !bot.HasLanded)
                    continue;

                if (bot.Combatant.Role == TeamRole.Radioman)
                {
                    caller = bot;
                    break;
                }

                if (bot.Combatant.Role == TeamRole.Leader && caller == null)
                    caller = bot;
            }

            if (caller == null)
                return;

            if (CallArtilleryAt(intel.Team, caller, intel.ContactPosition))
                intel.ContactTime -= 6f; // aynı temasa tekrar çağrı yapılmasın
        }

        /// <summary>Topçu hazır mı (servis yoksa/hata verirse false). Son adam kararı için.</summary>
        internal bool IsArtilleryReady(int team)
        {
            var artillery = Artillery;
            if (artillery == null)
                return false;

            try
            {
                return artillery.IsReady(team);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Son kalan komutan topçu çağırır (RC2 v3: tek kalan asker, tehdit var, topçu hazır). Telsizci/lider seçimi atlanır;
        /// mesafe (55-320 m) ve dost güvenliği (42 m) kuralları normal çağrıyla aynıdır. Kabul edildiyse true.
        /// </summary>
        internal bool TryCallArtilleryLastMan(BotController caller, Vector3 target)
        {
            if (caller == null || caller.Combatant == null || !caller.Combatant.IsAlive || MatchEnded || !IsArtilleryReady(caller.Team))
                return false;

            return CallArtilleryAt(caller.Team, caller, target);
        }

        /// <summary>Ortak son adım: menzil, dost güvenliği (hedef çevresinde tim arkadaşı yok), ardından topçu servisi çağrısı.</summary>
        private bool CallArtilleryAt(int team, BotController caller, Vector3 target)
        {
            var artillery = Artillery;
            if (artillery == null || caller == null || caller.Combatant == null)
                return false;

            var distance = FlatDistance(caller.transform.position, target);
            if (distance < ArtilleryMinDistance || distance > ArtilleryMaxDistance)
                return false;

            // Dost güvenliği: hedefin çevresinde hiçbir tim arkadaşı olmamalı.
            var safetySqr = ArtilleryAllySafetyRadius * ArtilleryAllySafetyRadius;
            var all = CombatantRegistry.All;
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !c.IsAlive || c.Team != team)
                    continue;

                var d = c.transform.position - target;
                d.y = 0f;
                if (d.sqrMagnitude < safetySqr)
                    return false;
            }

            try
            {
                return artillery.TryCall(team, caller.Combatant.Id, new Float3(target.x, target.y, target.z));
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                return false;
            }
        }

        /// <summary>YZ komutan T-129 ATAK desteğini yakın (≤15 sn) bir temas noktasına çağırır.</summary>
        private void TryCallAttackHeli(TeamIntel intel, Combatant commander, float now)
        {
            if (MatchEnded || now - intel.ContactTime > 15f || NextFloat() > 0.5f)
                return;

            try
            {
                Project.Infrastructure.Support.SupportAbilitySystem.TryAiCall(intel.Team, commander, intel.ContactPosition);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private void TryLaunchRecon(TeamIntel intel, float now)
        {
            if (MatchEnded || now - intel.ContactTime > 20f || NextFloat() > 0.15f)
                return;

            try
            {
                if (!Project.Infrastructure.Drone.ReconDroneSystem.IsReady(intel.Team))
                    return;

                BotController caller = null;
                for (var i = 0; i < intel.Bots.Count; i++)
                {
                    var bot = intel.Bots[i];
                    if (bot == null || bot.Combatant == null || !bot.Combatant.IsAlive || bot.Combatant.IsDowned || !bot.HasLanded)
                        continue;
                    if (bot.Combatant.Role == TeamRole.Radioman) { caller = bot; break; }
                    if (bot.Combatant.Role == TeamRole.Leader && caller == null) caller = bot;
                }

                if (caller == null)
                    return;

                Project.Infrastructure.Drone.ReconDroneSystem.TryLaunch(intel.Team, caller.transform.position, intel.ContactPosition);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        // ------------------------------------------------------------------ servisler / olaylar

        private void RefreshServices(bool force)
        {
            _nextServiceRefresh = Time.time + ServiceRefreshInterval;
            var services = GameContext.Services;
            var changed = !ReferenceEquals(services, _servicesSeen);
            _servicesSeen = services;

            if (force || changed || Zone == null)
                Zone = Resolve<IZoneService>();
            if (force || changed || Match == null)
                Match = Resolve<IMatchService>();
            if (force || changed || Relations == null)
                Relations = Resolve<ITeamRelations>();
            if (force || changed || Chain == null)
                Chain = Resolve<ChainOfCommandService>();
            if (force || changed || Orders == null)
                Orders = Resolve<SquadOrderService>();
            if (force || changed || Artillery == null)
                Artillery = Resolve<ArtilleryService>();
            if (force || changed || Config == null)
                Config = Resolve<MatchConfig>();

            var bus = Resolve<IEventBus>();
            if (!ReferenceEquals(bus, _bus))
            {
                Unsubscribe();
                _bus = bus;
                if (_bus != null)
                {
                    _bus.Subscribe(_onWeaponFired);
                    _bus.Subscribe(_onCommandTransferred);
                    _bus.Subscribe(_onSquadOrder);
                    _bus.Subscribe(_onAirdrop);
                }
            }
        }

        /// <summary>İkmal duyurusu/inişi: botlar sandığa yönelir; açıldıktan 90 sn sonra ilgi biter.</summary>
        private void OnAirdrop(AirdropEvent e)
        {
            _airdropPosition = new Vector3(e.Position.X, 0f, e.Position.Z);
            if (e.Stage == AirdropStage.Opened)
            {
                _airdropExpire = Time.time + 90f;
                return;
            }

            _airdropActive = true;
            _airdropExpire = Time.time + e.SecondsToNextStage + 120f;
        }

        private void Unsubscribe()
        {
            if (_bus == null)
                return;

            try
            {
                _bus.Unsubscribe(_onWeaponFired);
                _bus.Unsubscribe(_onCommandTransferred);
                _bus.Unsubscribe(_onSquadOrder);
                _bus.Unsubscribe(_onAirdrop);
            }
            catch (Exception)
            {
                // veri yolu kapanmış olabilir
            }

            _bus = null;
        }

        private static T Resolve<T>() where T : class
        {
            try
            {
                return GameContext.TryGet<T>(out var service) ? service : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ------------------------------------------------------------------ yardımcılar

        internal float NextFloat() => (float)_rng.NextDouble();

        internal static float FlatDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        internal static bool SafeZoneActive(IZoneService zone)
        {
            try
            {
                return zone != null && zone.IsActive && zone.Stage != ZoneStage.Idle;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool SafeNextZoneContains(IZoneService zone, Vector3 p)
        {
            try
            {
                return zone == null || zone.NextZone.Radius <= 0f || zone.NextZone.Contains(p.x, p.z);
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>Sonraki güvenli bölge içinde, mevcut konum tarafında bir nokta (fraction = merkeze göre yarıçap oranı).</summary>
        internal Vector3 PointInsideNextZone(IZoneService zone, Vector3 from, float fraction)
        {
            ZoneState next;
            try
            {
                next = zone.NextZone;
            }
            catch (Exception)
            {
                return from;
            }

            var center = new Vector3(next.CenterX, from.y, next.CenterZ);
            var toSelf = from - center;
            toSelf.y = 0f;
            var dir = toSelf.sqrMagnitude > 1f ? toSelf.normalized : new Vector3(0f, 0f, 1f);

            // Yan sapma: bütün timler aynı noktaya yığılmasın.
            var jitter = (NextFloat() - 0.5f) * 1.2f;
            dir = Quaternion.Euler(0f, jitter * Mathf.Rad2Deg, 0f) * dir;
            var radius = Mathf.Max(0f, next.Radius) * Mathf.Clamp01(fraction) * (0.6f + NextFloat() * 0.4f);
            return ClampToMap(center + dir * radius);
        }

        internal Vector3 ClampToMap(Vector3 p)
        {
            var half = Config != null && Config.MapHalfSize > 10f ? Config.MapHalfSize : 512f;
            half -= 8f;
            p.x = Mathf.Clamp(p.x, -half, half);
            p.z = Mathf.Clamp(p.z, -half, half);
            return p;
        }
    }
}
