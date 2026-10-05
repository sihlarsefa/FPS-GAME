using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using UnityEngine;
using IServiceProvider = Project.Core.Interfaces.IServiceProvider;

namespace Project.Infrastructure.Combat
{
    /// <summary>
    /// Çatışma altyapısının ortak servis erişimi. Açıkça verilen servisler (BallisticsSystem.Create / Configure)
    /// önceliklidir; yoksa GameContext'ten çözülür ve kapsayıcı değişene kadar önbelleğe alınır.
    /// GameContext hazır değilse tüm özellikler null döner (çağıranlar null-güvenlidir).
    /// </summary>
    internal static class CombatContext
    {
        private static IServiceProvider _resolvedFor;
        private static CombatService _combat;
        private static IEventBus _eventBus;
        private static MatchConfig _config;
        private static ITeamRelations _teams;

        private static CombatService _combatOverride;
        private static IEventBus _eventBusOverride;

        public static CombatService Combat
        {
            get
            {
                if (_combatOverride != null)
                    return _combatOverride;

                Refresh();
                if (_combat == null && _resolvedFor != null)
                    _resolvedFor.TryResolve(out _combat);

                return _combat;
            }
        }

        public static IEventBus EventBus
        {
            get
            {
                if (_eventBusOverride != null)
                    return _eventBusOverride;

                Refresh();
                if (_eventBus == null && _resolvedFor != null)
                    _resolvedFor.TryResolve(out _eventBus);

                return _eventBus;
            }
        }

        public static MatchConfig Config
        {
            get
            {
                Refresh();
                if (_config == null && _resolvedFor != null)
                {
                    if (!_resolvedFor.TryResolve(out _config) && _resolvedFor.TryResolve<IMatchService>(out var match) && match != null)
                        _config = match.Config;
                }

                return _config;
            }
        }

        public static ITeamRelations Teams
        {
            get
            {
                Refresh();
                if (_teams == null && _resolvedFor != null)
                    _resolvedFor.TryResolve(out _teams);

                if (_teams == null)
                {
                    var combat = Combat;
                    if (combat != null)
                        return combat.Teams;
                }

                return _teams;
            }
        }

        /// <summary>
        /// Dost ateşi açık mı? MatchConfig ya da CombatService açık diyorsa açık (ikisi de yoksa kapalı).
        /// Açıkken mermiler müttefik vuruş kutularına da çarpar; hasar kararını yine CombatService verir.
        /// </summary>
        public static bool FriendlyFire
        {
            get
            {
                var config = Config;
                if (config != null && config.FriendlyFire)
                    return true;

                var combat = Combat;
                return combat != null && combat.FriendlyFire;
            }
        }

        public static void SetOverrides(CombatService combat, IEventBus eventBus)
        {
            _combatOverride = combat;
            _eventBusOverride = eventBus;
        }

        /// <summary>Verilen örnekler hâlâ geçerli override ise temizler (sahne kapanırken).</summary>
        public static void ClearOverrides(CombatService combat, IEventBus eventBus)
        {
            if (ReferenceEquals(_combatOverride, combat))
                _combatOverride = null;

            if (ReferenceEquals(_eventBusOverride, eventBus))
                _eventBusOverride = null;
        }

        /// <summary>Savaşanın timi; kayıtta yoksa ITeamRelations, o da yoksa -1.</summary>
        public static int ResolveTeam(PlayerId id)
        {
            if (!id.IsValid)
                return -1;

            if (CombatantRegistry.TryGet(id, out var combatant) && combatant != null)
                return combatant.Team;

            var teams = Teams;
            if (teams == null)
                return -1;

            try
            {
                return teams.GetTeam(id);
            }
            catch (System.Exception)
            {
                return -1;
            }
        }

        private static void Refresh()
        {
            var services = GameContext.Services;
            if (ReferenceEquals(services, _resolvedFor))
                return;

            _resolvedFor = services;
            _combat = null;
            _eventBus = null;
            _config = null;
            _teams = null;
        }

        public static Float3 ToFloat3(Vector3 v) => new(v.x, v.y, v.z);

        public static Vector3 ToVector3(Float3 v) => new(v.X, v.Y, v.Z);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _resolvedFor = null;
            _combat = null;
            _eventBus = null;
            _config = null;
            _teams = null;
            _combatOverride = null;
            _eventBusOverride = null;
        }
    }
}
