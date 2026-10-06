using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>
    /// T-70 hava ikmali (maç ortası olayı). InMatch başlayınca saat işler; MatchConfig.AirdropFirstSeconds'ta ilk sandık
    /// duyurulur, sonra her AirdropIntervalSeconds'ta bir (en çok AirdropCount). Sandık: Announced → (paraşüt, DescentSeconds)
    /// → Landed → (OpenDelaySeconds) → Opened. Açılışta altyapı LootSpawnService.RollSupplyCrate ile yağmayı düşürür.
    /// Konum: bir sonraki güvenli çemberin içinde, merkezden yarıçapın %25–65'i uzakta (çatışmayı merkeze doğru çeker ama
    /// son çemberi tek noktaya kilitlemez), önceki sandıktan ≥ MinSeparation. Sonraki çember MinZoneRadius'tan küçükse
    /// kalan ikmaller iptal edilir. Durumsuz rastgelelik IRandom'dan gelir (tekrar üretilebilir). Sunucu otoritelidir.
    /// </summary>
    public sealed class AirdropService : IGameTickService, IDisposable
    {
        public const float MinSeparation = 120f;
        public const float RingMin = 0.25f;
        public const float RingMax = 0.65f;
        private const int PlacementAttempts = 12;
        private const float MapMarginFactor = 0.85f;

        public sealed class Drop
        {
            public int Id;
            public Float3 Position;
            public float AnnounceTime;
            public float LandTime;
            public float OpenTime;
            public AirdropStage Stage;
        }

        private readonly IEventBus _eventBus;
        private readonly IZoneService _zone;
        private readonly IRandom _random;
        private readonly Action<MatchPhaseChangedEvent> _onPhase;
        private readonly List<Drop> _drops = new();
        private readonly float _first;
        private readonly float _interval;
        private readonly int _count;
        private readonly float _descent;
        private readonly float _openDelay;
        private readonly float _minZoneRadius;
        private readonly float _mapHalf;
        private float _time;
        private int _scheduled;
        private bool _armed;
        private bool _cancelled;

        public AirdropService(IEventBus eventBus, IZoneService zone, IRandom random, MatchConfig config)
        {
            config ??= new MatchConfig();
            _eventBus = eventBus;
            _zone = zone;
            _random = random ?? new SeededRandom(0);
            _first = Math.Max(0f, config.AirdropFirstSeconds);
            _interval = Math.Max(1f, config.AirdropIntervalSeconds);
            _count = Math.Max(0, config.AirdropCount);
            _descent = Math.Max(0f, config.AirdropDescentSeconds);
            _openDelay = Math.Max(0f, config.AirdropOpenDelaySeconds);
            _minZoneRadius = Math.Max(0f, config.AirdropMinZoneRadius);
            _mapHalf = config.MapHalfSize > 0f ? config.MapHalfSize : 512f;

            _onPhase = OnPhaseChanged;
            _eventBus?.Subscribe(_onPhase);
        }

        public IReadOnlyList<Drop> Drops => _drops;
        public bool IsArmed => _armed;
        public float ElapsedSeconds => _time;

        /// <summary>Duyurulmuş ama henüz açılmamış sandık sayısı.</summary>
        public int ActiveCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _drops.Count; i++)
                    if (_drops[i].Stage != AirdropStage.Opened) n++;
                return n;
            }
        }

        /// <summary>Saati başlatır (InMatch'e geçişte otomatik). Tekrar çağrı yok sayılır.</summary>
        public void Start()
        {
            _armed = true;
        }

        public void Dispose() => _eventBus?.Unsubscribe(_onPhase);

        public void Tick(float deltaTime)
        {
            if (!_armed || !(deltaTime > 0f) || float.IsInfinity(deltaTime))
                return;

            // Saat adım adım ilerler: büyük dt'de sıra (duyuru → iniş → açılış) ve konum seçimi bozulmaz.
            var target = _time + deltaTime;
            var guard = 4 * (_count + 2);
            while (guard-- > 0)
            {
                var next = NextEventTime(target);
                if (next < 0f)
                    break;

                _time = next;
                Process();
            }

            _time = target;
            Process();
        }

        private float NextEventTime(float limit)
        {
            var best = float.MaxValue;
            if (!_cancelled && _scheduled < _count)
                best = Math.Min(best, _first + _scheduled * _interval);

            for (var i = 0; i < _drops.Count; i++)
            {
                var d = _drops[i];
                if (d.Stage == AirdropStage.Announced) best = Math.Min(best, d.LandTime);
                else if (d.Stage == AirdropStage.Landed) best = Math.Min(best, d.OpenTime);
            }

            return best <= limit && best >= _time ? best : -1f;
        }

        private void Process()
        {
            while (!_cancelled && _scheduled < _count && _time >= _first + _scheduled * _interval)
                Announce();

            for (var i = 0; i < _drops.Count; i++)
            {
                var d = _drops[i];
                if (d.Stage == AirdropStage.Announced && _time >= d.LandTime)
                {
                    d.Stage = AirdropStage.Landed;
                    _eventBus?.Publish(new AirdropEvent(d.Id, AirdropStage.Landed, d.Position, Math.Max(0f, d.OpenTime - _time)));
                }

                if (d.Stage == AirdropStage.Landed && _time >= d.OpenTime)
                {
                    d.Stage = AirdropStage.Opened;
                    _eventBus?.Publish(new AirdropEvent(d.Id, AirdropStage.Opened, d.Position, 0f));
                }
            }
        }

        private void Announce()
        {
            var next = _zone != null ? _zone.NextZone : new ZoneState(0f, 0f, _mapHalf * 0.5f, 0f);
            if (next.Radius < _minZoneRadius)
            {
                _cancelled = true;
                return;
            }

            var d = new Drop
            {
                Id = _scheduled,
                Position = PickPosition(next),
                AnnounceTime = _time,
                LandTime = _time + _descent,
                OpenTime = _time + _descent + _openDelay,
                Stage = AirdropStage.Announced
            };
            _scheduled++;
            _drops.Add(d);
            _eventBus?.Publish(new AirdropEvent(d.Id, AirdropStage.Announced, d.Position, _descent));
        }

        private Float3 PickPosition(ZoneState zone)
        {
            var bound = _mapHalf * MapMarginFactor;
            var best = new Float3(zone.CenterX, 0f, zone.CenterZ);
            for (var attempt = 0; attempt < PlacementAttempts; attempt++)
            {
                var angle = _random.NextFloat() * (float)(Math.PI * 2.0);
                var dist = zone.Radius * _random.Range(RingMin, RingMax);
                var x = Clamp(zone.CenterX + (float)Math.Cos(angle) * dist, -bound, bound);
                var z = Clamp(zone.CenterZ + (float)Math.Sin(angle) * dist, -bound, bound);
                best = new Float3(x, 0f, z);
                if (FarFromOthers(x, z))
                    break;
            }

            return best;
        }

        private bool FarFromOthers(float x, float z)
        {
            for (var i = 0; i < _drops.Count; i++)
            {
                var dx = x - _drops[i].Position.X;
                var dz = z - _drops[i].Position.Z;
                if (dx * dx + dz * dz < MinSeparation * MinSeparation)
                    return false;
            }

            return true;
        }

        private void OnPhaseChanged(MatchPhaseChangedEvent e)
        {
            if (e.CurrentPhase == MatchPhase.InMatch)
                Start();
            else if (e.CurrentPhase == MatchPhase.Ending || e.CurrentPhase == MatchPhase.Lobby)
                _armed = false;
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
