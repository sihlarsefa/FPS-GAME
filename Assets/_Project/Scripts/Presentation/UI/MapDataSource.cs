using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Transport;
using Project.Infrastructure.World;
using Project.Presentation.Player;
using UnityEngine;
using IServiceProvider = Project.Core.Interfaces.IServiceProvider;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Mini harita ve tam haritanın ortak veri kaynağı: dünya çerçevesi, yerel oyuncu, bölge/topçu/tim emri servisleri
    /// (GameContext hazır olmasa da güvenli; eksik servisler saniyede bir yeniden çözülür) ve timin intikal bilgisi
    /// (<see cref="MapMarkers"/> planı ya da sahnedeki tim aracı — arama seyrek yapılır). Her karede tahsis yapmaz.
    /// </summary>
    internal sealed class MapDataSource
    {
        private const float ResolveInterval = 1f;
        private const float TransportSearchInterval = 1.5f;
        private const float InitialTransportSearchWindow = 25f;

        private IPlayerHudSource _player;
        private IServiceProvider _services;
        private IZoneService _zone;
        private ArtilleryService _artillery;
        private SquadOrderService _orders;
        private IMatchService _match;
        private float _nextResolveTime;

        private TransportVehicle _transport;
        private bool _transportEverFound;
        private float _nextTransportSearch;
        private Vector3 _observedRouteStart;
        private bool _hasObservedRouteStart;

        public MapDataSource(IPlayerHudSource player)
        {
            _player = player;
            Frame = MapFrame.Default;
        }

        /// <summary>Yerel oyuncu kaynağı (yeniden bağlanabilir).</summary>
        public IPlayerHudSource Player
        {
            get => HasPlayer ? _player : null;
            set
            {
                if (ReferenceEquals(_player, value))
                    return;
                _player = value;
                _transport = null;
                _hasObservedRouteStart = false;
            }
        }

        /// <summary>Sahnedeki dünya verisi (yoksa null).</summary>
        public WorldMetadata World { get; private set; }

        /// <summary>Geçerli harita çerçevesi.</summary>
        public MapFrame Frame { get; private set; }

        /// <summary>Oyuncu kaynağı geçerli mi (yok edilmiş MonoBehaviour değil)?</summary>
        public bool HasPlayer => _player != null && !(_player is Object unityObject && unityObject == null);

        /// <summary>Oyuncunun savaşanı (yoksa null).</summary>
        public Combatant Self
        {
            get
            {
                if (!HasPlayer)
                    return null;
                var c = _player.Combatant;
                return c != null ? c : null;
            }
        }

        /// <summary>Oyuncunun timi (savaşan yoksa maç servisindeki yerel tim, o da yoksa 0).</summary>
        public int Team
        {
            get
            {
                var self = Self;
                if (self != null)
                    return self.Team;
                if (_match is MatchService matchService)
                {
                    var team = matchService.LocalTeam;
                    if (team >= 0)
                        return team;
                }

                return 0;
            }
        }

        /// <summary>Oyuncu konumu (oyuncu yoksa harita merkezi).</summary>
        public Vector3 PlayerPosition
        {
            get
            {
                if (!HasPlayer)
                    return new Vector3(Frame.Center.x, 0f, Frame.Center.y);
                var p = _player.Position;
                return MapMath.IsFinite(p) ? p : new Vector3(Frame.Center.x, 0f, Frame.Center.y);
            }
        }

        /// <summary>Oyuncu bakış sapması (derece, 0 = kuzey).</summary>
        public float PlayerYaw => HasPlayer ? _player.Yaw : 0f;

        /// <summary>Oyuncu ölü mü?</summary>
        public bool IsPlayerDead => HasPlayer && _player.IsDead;

        /// <summary>Oyuncu araçta / intikal aracında mı (mini harita uzaklaşır)?</summary>
        public bool IsPlayerMounted => HasPlayer && (_player.IsInVehicle || _player.DropState == DropState.InTransport);

        public IZoneService Zone => _zone;
        public ArtilleryService Artillery => _artillery;
        public SquadOrderService Orders => _orders;
        public IMatchService Match => _match;

        /// <summary>Kare başında çağrılır: dünya çerçevesini ve servisleri tazeler (ucuz).</summary>
        public void Refresh()
        {
            World = WorldMetadata.Instance;
            Frame = MapFrame.FromWorld(World);

            var services = GameContext.Services;
            if (!ReferenceEquals(services, _services))
            {
                _services = services;
                _zone = null;
                _artillery = null;
                _orders = null;
                _match = null;
                _nextResolveTime = 0f;
            }

            if (_services == null)
                return;

            if ((_zone == null || _artillery == null || _orders == null || _match == null) && Time.unscaledTime >= _nextResolveTime)
            {
                _nextResolveTime = Time.unscaledTime + ResolveInterval;
                if (_zone == null && GameContext.TryGet<IZoneService>(out var zone))
                    _zone = zone;
                if (_artillery == null && GameContext.TryGet<ArtilleryService>(out var artillery))
                    _artillery = artillery;
                if (_orders == null && GameContext.TryGet<SquadOrderService>(out var orders))
                    _orders = orders;
                if (_match == null && GameContext.TryGet<IMatchService>(out var match))
                    _match = match;
            }
        }

        /// <summary>Mavi bölge (mevcut) ve beyaz çember (sonraki). Bölge servisi yok/başlamamışsa false.</summary>
        public bool TryGetZones(out ZoneState current, out bool hasNext, out ZoneState next)
        {
            current = default;
            next = default;
            hasNext = false;
            if (_zone == null)
                return false;

            var stage = _zone.Stage;
            if (stage == ZoneStage.Idle && !_zone.IsActive)
                return false;

            current = _zone.CurrentZone;
            if (current.Radius <= 0.01f || float.IsNaN(current.Radius))
                return false;

            if (stage == ZoneStage.Waiting || stage == ZoneStage.Shrinking)
            {
                next = _zone.NextZone;
                hasNext = next.Radius > 0.01f && next.Radius < current.Radius - 0.25f;
            }

            return true;
        }

        /// <summary>Timin etkin topçu atışının hedefi ve ilk mermiye kalan süre.</summary>
        public bool TryGetArtillery(out Vector3 target, out float secondsUntilImpact)
        {
            target = default;
            secondsUntilImpact = 0f;
            if (_artillery == null)
                return false;

            var team = Team;
            if (!_artillery.TryGetActiveStrike(team, out var t))
                return false;

            target = MapMath.ToVector3(t);
            secondsUntilImpact = _artillery.GetSecondsUntilImpact(team);
            return MapMath.IsFinite(target);
        }

        /// <summary>Topçu bekleme süresi (sn; 0 = hazır). Oyuncu kaynağı, yoksa servis.</summary>
        public float ArtilleryCooldown
        {
            get
            {
                if (HasPlayer)
                {
                    var cd = _player.ArtilleryCooldown;
                    if (cd > 0f)
                        return cd;
                }

                return _artillery != null ? _artillery.GetCooldownRemaining(Team) : 0f;
            }
        }

        /// <summary>Timin geçerli emri ve hedefi (emir yoksa false).</summary>
        public bool TryGetSquadOrder(out SquadOrder order, out Vector3 target)
        {
            order = SquadOrder.Follow;
            target = default;
            if (_orders == null)
            {
                if (HasPlayer)
                    order = _player.CurrentOrder;
                return false;
            }

            if (!_orders.TryGetOrder(Team, out order, out var t))
                return false;

            target = MapMath.ToVector3(t);
            return true;
        }

        /// <summary>
        /// Timin intikal bilgisi: rota başlangıcı, iniş bölgesi (LZ), yöntem ve araç (bulunduysa). Oyuncu indikten ve
        /// araç ayrıldıktan sonra false döner (LZ/rota gizlenir).
        /// </summary>
        public bool TryGetInsertion(out Vector3 start, out Vector3 landingZone, out InsertionMethod method, out TransportVehicle transport)
        {
            start = default;
            landingZone = default;
            method = InsertionMethod.Helicopter;
            transport = FindTeamTransport();

            var mounted = HasPlayer && _player.DropState != DropState.Landed;
            var transportActive = transport != null && !transport.IsDeparting;
            if (!mounted && !transportActive)
                return false;

            if (MapMarkers.TryGetInsertion(Team, out var plan))
            {
                start = MapMath.ToVector3(plan.Start);
                landingZone = MapMath.ToVector3(plan.LandingZone);
                method = plan.Method;
                if (transport != null)
                    method = transport.Method;
                return true;
            }

            if (transport == null)
                return false;

            landingZone = transport.LandingZone;
            if (!MapMath.IsFinite(landingZone))
                return false;

            method = transport.Method;
            start = _hasObservedRouteStart ? _observedRouteStart : transport.transform.position;
            return true;
        }

        private TransportVehicle FindTeamTransport()
        {
            var team = Team;
            if (_transport != null && _transport.Team == team)
                return _transport;

            _transport = null;
            var now = Time.unscaledTime;
            if (now < _nextTransportSearch)
                return null;

            var mounted = HasPlayer && _player.DropState != DropState.Landed;
            var searchAllowed = mounted || (!_transportEverFound && Time.timeSinceLevelLoad < InitialTransportSearchWindow);
            if (!searchAllowed)
                return null;

            _nextTransportSearch = now + TransportSearchInterval;
            var all = Object.FindObjectsByType<TransportVehicle>(FindObjectsInactive.Exclude);
            if (all == null)
                return null;

            for (var i = 0; i < all.Length; i++)
            {
                var vehicle = all[i];
                if (vehicle == null || vehicle.Team != team)
                    continue;

                _transport = vehicle;
                _transportEverFound = true;
                _observedRouteStart = vehicle.transform.position;
                _hasObservedRouteStart = MapMath.IsFinite(_observedRouteStart);
                break;
            }

            return _transport;
        }
    }
}
