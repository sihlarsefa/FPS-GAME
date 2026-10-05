using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure;
using Project.Infrastructure.Combat;
using Project.Presentation.Player;
using UnityEngine;
using IServiceProvider = Project.Core.Interfaces.IServiceProvider;
using Object = UnityEngine.Object;

namespace Project.Presentation.UI
{
    /// <summary>
    /// HUD görünümlerinin ortak durumu: yerel oyuncu kaynağı, GameContext servisleri (yoksa null — her görünüm
    /// eksik servise dayanıklıdır), kamera ve tim listesi (rütbe sırasıyla, önbellekli adlarla).
    /// <see cref="HudController"/> her karede <see cref="Refresh"/> çağırır.
    /// </summary>
    public sealed class HudContext
    {
        /// <summary>Tim üyesi kaydı (önbellekli rütbeli ad).</summary>
        public struct SquadMember
        {
            public Combatant Combatant;
            public MilitaryRank Rank;
            public string BaseName;
            public string Name;
        }

        private static readonly Comparison<SquadMember> SquadOrder = CompareMembers;

        private readonly List<SquadMember> _squad = new List<SquadMember>(12);
        private readonly List<Combatant> _scratch = new List<Combatant>(16);
        private int _registryVersion = -1;
        private int _squadTeam = int.MinValue;
        private Camera _fallbackCamera;
        private float _nextCameraLookup;

        public HudContext(IPlayerHudSource player)
        {
            Player = player;
            PlayerController = player as PlayerController;
        }

        /// <summary>Yerel oyuncunun HUD kaynağı (null olabilir).</summary>
        public IPlayerHudSource Player { get; }

        /// <summary>Kaynak bir <see cref="Presentation.Player.PlayerController"/> ise o (ek bilgiler: intikal, emir, topçu).</summary>
        public PlayerController PlayerController { get; }

        // ------------------------------------------------------------------ servisler (null olabilir)

        public IServiceProvider BoundServices { get; private set; }
        public IEventBus Bus { get; private set; }
        public IMatchService Match { get; private set; }
        public MatchService MatchService { get; private set; }
        public IZoneService Zone { get; private set; }
        public MatchStatsService Stats { get; private set; }
        public ChainOfCommandService Chain { get; private set; }
        public ArtilleryService Artillery { get; private set; }
        public SquadOrderService Orders { get; private set; }
        public ITeamRelations Teams { get; private set; }
        public ICombatantDirectory Directory { get; private set; }
        public KillFeedService KillFeed { get; private set; }
        public SettingsService Settings { get; private set; }

        /// <summary>İstatistik servisi yoksa HitConfirmedEvent'lerden sayılan yerel öldürme sayısı.</summary>
        public int LocalKillsFallback { get; set; }

        // ------------------------------------------------------------------ kare durumu

        /// <summary>Oyuncu kaynağı geçerli mi (yok edilmiş bir MonoBehaviour değil)?</summary>
        public bool PlayerValid { get; private set; }

        /// <summary>Yerel savaşan (yoksa null).</summary>
        public Combatant Local { get; private set; }

        public PlayerId LocalId => Local != null ? Local.Id : PlayerId.Invalid;

        /// <summary>Yerel oyuncunun timi (bilinmiyorsa -1).</summary>
        public int LocalTeam { get; private set; } = -1;

        /// <summary>Yerel oyuncu hayatta ve HUD gösterilebilir mi?</summary>
        public bool LocalAlive { get; private set; }

        /// <summary>Oyuncu konumu (kaynak geçersizse son bilinen).</summary>
        public Vector3 Position { get; private set; }

        /// <summary>Bakış yönü (derece, 0..360).</summary>
        public float Yaw { get; private set; }

        /// <summary>Dünya kamerası (oyuncu kamerası, yoksa Camera.main).</summary>
        public Camera Camera { get; private set; }

        /// <summary>Tim üyeleri (yerel oyuncu dahil, ölüler dahil), rütbe sırasıyla.</summary>
        public IReadOnlyList<SquadMember> Squad => _squad;

        /// <summary>Tim listesi her değiştiğinde artar.</summary>
        public int SquadVersion { get; private set; }

        // ------------------------------------------------------------------ servis bağlama

        /// <summary>Servisleri GameContext'ten çözer. Bağlanan sağlayıcıyı döndürür (hazır değilse null).</summary>
        public void Bind(IServiceProvider services)
        {
            BoundServices = services;
            if (services == null)
            {
                ClearServices();
                return;
            }

            Bus = Resolve<IEventBus>(services);
            MatchService = Resolve<MatchService>(services);
            Match = Resolve<IMatchService>(services) ?? MatchService;
            Zone = Resolve<IZoneService>(services) ?? Resolve<ZoneService>(services);
            Stats = Resolve<MatchStatsService>(services);
            Chain = Resolve<ChainOfCommandService>(services);
            Artillery = Resolve<ArtilleryService>(services);
            Orders = Resolve<SquadOrderService>(services);
            Teams = Resolve<ITeamRelations>(services) ?? MatchService;
            Directory = Resolve<ICombatantDirectory>(services) ?? MatchService;
            KillFeed = Resolve<KillFeedService>(services);
            Settings = Resolve<SettingsService>(services);
        }

        public void ClearServices()
        {
            BoundServices = null;
            Bus = null;
            Match = null;
            MatchService = null;
            Zone = null;
            Stats = null;
            Chain = null;
            Artillery = null;
            Orders = null;
            Teams = null;
            Directory = null;
            KillFeed = null;
            Settings = null;
        }

        private static T Resolve<T>(IServiceProvider services) where T : class
        {
            try
            {
                return services.TryResolve<T>(out var service) ? service : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ------------------------------------------------------------------ kare yenileme

        /// <summary>Kare başına durum (oyuncu, kamera, tim listesi). Tahsis yapmaz (tim değişmedikçe).</summary>
        public void Refresh()
        {
            PlayerValid = IsAlive(Player);

            Combatant local = null;
            if (PlayerValid)
            {
                try
                {
                    local = Player.Combatant;
                }
                catch (Exception)
                {
                    local = null;
                }
            }

            if (local == null)
            {
                var registered = CombatantRegistry.LocalPlayer;
                if (registered != null)
                    local = registered;
            }

            Local = local != null ? local : null;
            LocalTeam = Local != null && Local.IsInitialized ? Local.Team : -1;

            var dead = true;
            if (PlayerValid)
            {
                try
                {
                    dead = Player.IsDead;
                    Position = Player.Position;
                    Yaw = Mathf.Repeat(Player.Yaw, 360f);
                }
                catch (Exception)
                {
                    dead = true;
                }
            }
            else if (Local != null)
            {
                dead = !Local.IsAlive;
                Position = Local.transform.position;
                Yaw = Mathf.Repeat(Local.transform.eulerAngles.y, 360f);
            }

            LocalAlive = !dead && Local != null && Local.IsAlive;

            ResolveCamera();
            RefreshSquad();
        }

        private void ResolveCamera()
        {
            Camera cam = null;
            if (PlayerValid)
            {
                try
                {
                    var controller = Player.CameraController;
                    if (controller != null)
                        cam = controller.Camera;
                }
                catch (Exception)
                {
                    cam = null;
                }
            }

            if (cam == null || !cam.isActiveAndEnabled)
            {
                if (_fallbackCamera == null || !_fallbackCamera.isActiveAndEnabled)
                {
                    if (Time.unscaledTime >= _nextCameraLookup)
                    {
                        _nextCameraLookup = Time.unscaledTime + 1f;
                        _fallbackCamera = Camera.main;
                    }
                }

                cam = _fallbackCamera;
            }

            Camera = cam != null ? cam : null;
        }

        private void RefreshSquad()
        {
            var version = CombatantRegistry.Version;
            if (version == _registryVersion && LocalTeam == _squadTeam)
            {
                UpdateNames();
                return;
            }

            _registryVersion = version;
            _squadTeam = LocalTeam;
            _squad.Clear();

            if (LocalTeam >= 0)
            {
                _scratch.Clear();
                CombatantRegistry.GetTeam(LocalTeam, _scratch);
                for (var i = 0; i < _scratch.Count; i++)
                {
                    var c = _scratch[i];
                    if (c == null || !c.IsInitialized)
                        continue;

                    _squad.Add(new SquadMember
                    {
                        Combatant = c,
                        Rank = c.Rank,
                        BaseName = c.DisplayName,
                        Name = SafeRankedName(c)
                    });
                }

                _scratch.Clear();
                _squad.Sort(SquadOrder);
            }

            SquadVersion++;
        }

        private void UpdateNames()
        {
            for (var i = 0; i < _squad.Count; i++)
            {
                var member = _squad[i];
                var c = member.Combatant;
                if (c == null)
                    continue;

                if (member.Rank == c.Rank && ReferenceEquals(member.BaseName, c.DisplayName))
                    continue;

                member.Rank = c.Rank;
                member.BaseName = c.DisplayName;
                member.Name = SafeRankedName(c);
                _squad[i] = member;
                SquadVersion++;
            }
        }

        private static int CompareMembers(SquadMember a, SquadMember b)
        {
            var ca = a.Combatant;
            var cb = b.Combatant;
            if (ca == null || cb == null)
                return ca == null ? (cb == null ? 0 : 1) : -1;

            var byRank = ((int)cb.Rank).CompareTo((int)ca.Rank);
            if (byRank != 0)
                return byRank;

            var byLeader = (ca.Role == TeamRole.Leader ? 0 : 1).CompareTo(cb.Role == TeamRole.Leader ? 0 : 1);
            if (byLeader != 0)
                return byLeader;

            return ca.Id.Value.CompareTo(cb.Id.Value);
        }

        // ------------------------------------------------------------------ yardımcılar

        /// <summary>Bir arayüz nesnesi yok edilmiş Unity nesnesi değilse true.</summary>
        public static bool IsAlive(object source)
        {
            if (source == null)
                return false;
            if (source is Object unityObject)
                return unityObject != null;
            return true;
        }

        /// <summary>Rütbeli ad ("Yzb. Ahmet Yılmaz"); hata olursa düz ad.</summary>
        public static string SafeRankedName(Combatant c)
        {
            if (c == null)
                return string.Empty;

            try
            {
                var name = c.RankedName;
                return string.IsNullOrEmpty(name) ? c.DisplayName ?? string.Empty : name;
            }
            catch (Exception)
            {
                return c.DisplayName ?? string.Empty;
            }
        }

        /// <summary>Kimliğin görünen adı: tim listesi önbelleği → kayıt (rütbeli) → dizin → "Asker N".</summary>
        public string NameOf(PlayerId id)
        {
            if (!id.IsValid)
                return "Bilinmeyen";

            for (var i = 0; i < _squad.Count; i++)
            {
                var c = _squad[i].Combatant;
                if (c != null && c.Id == id)
                    return _squad[i].Name;
            }

            if (CombatantRegistry.TryGet(id, out var combatant))
                return SafeRankedName(combatant);

            if (Directory != null)
            {
                try
                {
                    var name = Directory.GetDisplayName(id);
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }
                catch (Exception)
                {
                    // yedeğe düş
                }
            }

            return "Asker " + id.Value;
        }

        public bool IsLocal(PlayerId id)
        {
            if (!id.IsValid)
                return false;
            if (Local != null && Local.Id == id)
                return true;
            return false;
        }

        /// <summary>Kimlik yerel oyuncunun timinden mi?</summary>
        public bool IsAlly(PlayerId id)
        {
            if (!id.IsValid || LocalTeam < 0)
                return false;

            if (CombatantRegistry.TryGet(id, out var c))
                return c.Team == LocalTeam;

            if (Teams != null)
            {
                try
                {
                    return Teams.GetTeam(id) == LocalTeam;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>Timin şu anki komutanı (komuta zinciri yoksa hayattaki en kıdemli üye).</summary>
        public PlayerId CommanderId
        {
            get
            {
                if (LocalTeam < 0)
                    return PlayerId.Invalid;

                if (Chain != null)
                {
                    try
                    {
                        var id = Chain.GetCommander(LocalTeam);
                        if (id.IsValid)
                            return id;
                    }
                    catch (Exception)
                    {
                        // yedeğe düş
                    }
                }

                for (var i = 0; i < _squad.Count; i++)
                {
                    var c = _squad[i].Combatant;
                    if (c != null && c.IsAlive)
                        return c.Id;
                }

                return PlayerId.Invalid;
            }
        }

        /// <summary>Timin adı ("Kartal Timi"); bilinmiyorsa boş.</summary>
        public string TeamName(int team)
        {
            if (team < 0)
                return string.Empty;

            if (Teams != null)
            {
                try
                {
                    return Teams.GetTeamName(team) ?? string.Empty;
                }
                catch (Exception)
                {
                    return string.Empty;
                }
            }

            return (team + 1) + ". Tim";
        }

        /// <summary>Yerel oyuncunun öldürme sayısı (istatistik servisi, yoksa yedek sayaç).</summary>
        public int LocalKills
        {
            get
            {
                var id = LocalId;
                if (Stats != null && id.IsValid)
                {
                    try
                    {
                        if (Stats.TryGet(id, out var stats) && stats != null)
                            return Mathf.Max(stats.Kills, 0);
                        return 0;
                    }
                    catch (Exception)
                    {
                        // yedeğe düş
                    }
                }

                return LocalKillsFallback;
            }
        }

        /// <summary>Silah/kaynak kimliğinin Türkçe adı.</summary>
        public static string WeaponName(string sourceId, bool environmental)
        {
            if (string.IsNullOrEmpty(sourceId))
                return environmental ? "Çevre" : "Silah";

            try
            {
                var name = WeaponCatalog.GetDisplayName(sourceId);
                return string.IsNullOrEmpty(name) ? sourceId : name;
            }
            catch (Exception)
            {
                return sourceId;
            }
        }
    }
}
