using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>Tim emirleri (takip, mevzi, taarruz, toplan). Tim başına son emri ve hedefini tutar; SquadOrderIssuedEvent yayınlar.</summary>
    public sealed class SquadOrderService
    {
        private struct OrderEntry
        {
            public SquadOrder Order;
            public Float3 Target;
            public int Revision;
            public bool Active;
        }

        private readonly IEventBus _eventBus;
        private readonly Dictionary<int, OrderEntry> _orders = new();
        private readonly List<int> _teamBuffer = new(8);

        public SquadOrderService(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        /// <summary>Yerel bildirim (olay veri yoluna ek olarak).</summary>
        public event Action<SquadOrderIssuedEvent> OrderIssued;

        public void Issue(int team, SquadOrder order, Float3 target)
        {
            _orders.TryGetValue(team, out var entry);
            entry.Order = order;
            entry.Target = target;
            entry.Revision++;
            entry.Active = true;
            _orders[team] = entry;

            var issued = new SquadOrderIssuedEvent(team, order, target);
            _eventBus?.Publish(issued);
            OrderIssued?.Invoke(issued);
        }

        public bool TryGetOrder(int team, out SquadOrder order, out Float3 target)
        {
            if (_orders.TryGetValue(team, out var entry) && entry.Active)
            {
                order = entry.Order;
                target = entry.Target;
                return true;
            }

            order = SquadOrder.Follow;
            target = Float3.Zero;
            return false;
        }

        public bool HasOrder(int team) => _orders.TryGetValue(team, out var entry) && entry.Active;

        /// <summary>Timin emir sayacı: her yeni emirde artar (botlar yeni emri bununla fark eder). Emir yoksa 0.</summary>
        public int GetRevision(int team) => _orders.TryGetValue(team, out var entry) ? entry.Revision : 0;

        /// <summary>Timin emrini kaldırır (botlar varsayılan davranışa döner). Olay yayınlamaz.</summary>
        public void ClearOrder(int team)
        {
            if (!_orders.TryGetValue(team, out var entry) || !entry.Active)
                return;

            entry.Active = false;
            entry.Revision++;
            _orders[team] = entry;
        }

        /// <summary>
        /// Tüm timlerin emirlerini kaldırır (yeni maç). Sayaçlar sıfırlanmaz, artar: bir botun en son gördüğü sayaç,
        /// sonraki yeni emrin sayacıyla yanlışlıkla eşleşmesin.
        /// </summary>
        public void ClearAll()
        {
            _teamBuffer.Clear();
            foreach (var pair in _orders)
            {
                if (pair.Value.Active)
                    _teamBuffer.Add(pair.Key);
            }

            for (var i = 0; i < _teamBuffer.Count; i++)
                ClearOrder(_teamBuffer[i]);

            _teamBuffer.Clear();
        }
    }

    /// <summary>
    /// Telsizle topçu desteği: tim başına bekleme süresi; çağrıldığında ~6 sn gecikmeyle hedef çevresine (r=18 m)
    /// 8 mermi zamanlar. Tick() düşme zamanı gelen mermileri DueImpacts'e koyar; Altyapı katmanı bunları patlatır.
    /// Çağrıda ve her mermi düşüşünde ArtilleryStrikeEvent yayınlar.
    /// <para>Kendi saati yalnızca Tick ile ilerler: GameTickCoordinator'a eklenmelidir (IGameTickService).
    /// Etkin atışlar (TryGetActiveStrike / IsInDangerZone) harita işareti ve botların kaçınması içindir.</para>
    /// </summary>
    public sealed class ArtilleryService : IGameTickService
    {
        public const int ShellsPerStrike = 8;
        public const float SpreadRadius = 18f;
        public const float DelaySeconds = 6f;
        public const float ShellDamage = 120f;
        public const float ShellRadius = 9f;

        /// <summary>Ardışık mermiler arası süre aralığı (sn).</summary>
        public const float ShellIntervalMin = 0.35f;
        public const float ShellIntervalMax = 0.75f;

        /// <summary>DueImpacts okunmazsa sınırsız büyümesin diye üst sınır (en eskiler atılır).</summary>
        public const int MaxQueuedImpacts = 256;

        /// <summary>Son merminin düşüşünden sonra atışın "etkin" sayıldığı ek süre (sn).</summary>
        public const float StrikeLingerSeconds = 1.5f;

        private struct PendingShell
        {
            public float LandTime;
            public Float3 Position;
            public int Team;
            public PlayerId Caller;
        }

        private struct ActiveStrike
        {
            public int Team;
            public PlayerId Caller;
            public Float3 Target;
            public float FirstImpactTime;
            public float EndTime;
        }

        private readonly IEventBus _eventBus;
        private readonly IRandom _random;
        private readonly Dictionary<int, float> _readyAt = new();
        private readonly List<PendingShell> _pending = new(ShellsPerStrike * 4);
        private readonly List<ActiveStrike> _strikes = new(8);
        private readonly List<PendingShell> _landedBuffer = new(ShellsPerStrike * 2);
        private float _time;

        public ArtilleryService(IEventBus eventBus, IRandom random, float cooldownSeconds)
        {
            _eventBus = eventBus;
            _random = random ?? new SeededRandom(Environment.TickCount);
            CooldownSeconds = cooldownSeconds > 0f && !float.IsNaN(cooldownSeconds) ? cooldownSeconds : 0f;
        }

        public float CooldownSeconds { get; }

        /// <summary>Henüz düşmemiş mermi sayısı (tüm timler).</summary>
        public int PendingShellCount => _pending.Count;

        /// <summary>Bu tick'te düşmesi gereken mermiler (çağıran okuyup Clear eder). Düşüş sırasına göre sıralıdır.</summary>
        public List<ArtilleryImpact> DueImpacts { get; } = new();

        /// <summary>Servisin kendi saati (Tick ile ilerler, sn).</summary>
        public float ElapsedSeconds => _time;

        /// <summary>Henüz bitmemiş (mermisi düşmekte ya da yolda olan) atış sayısı.</summary>
        public int ActiveStrikeCount => _strikes.Count;

        public float GetCooldownRemaining(int team)
        {
            if (!_readyAt.TryGetValue(team, out var readyAt))
                return 0f;

            var remaining = readyAt - _time;
            return remaining > 0f ? remaining : 0f;
        }

        public bool IsReady(int team) => GetCooldownRemaining(team) <= 0f;

        public bool TryCall(int team, PlayerId caller, Float3 target)
        {
            if (!IsReady(team))
                return false;

            if (float.IsNaN(target.X) || float.IsNaN(target.Y) || float.IsNaN(target.Z))
                return false;

            if (float.IsInfinity(target.X) || float.IsInfinity(target.Y) || float.IsInfinity(target.Z))
                return false;

            if (CooldownSeconds > 0f)
                _readyAt[team] = _time + CooldownSeconds;

            var firstLandTime = _time + DelaySeconds;
            var landTime = firstLandTime;
            for (var i = 0; i < ShellsPerStrike; i++)
            {
                if (i > 0)
                    landTime += _random.Range(ShellIntervalMin, ShellIntervalMax);

                InsertPending(new PendingShell
                {
                    LandTime = landTime,
                    Position = RandomPointInDisk(target, SpreadRadius),
                    Team = team,
                    Caller = caller
                });
            }

            _strikes.Add(new ActiveStrike
            {
                Team = team,
                Caller = caller,
                Target = target,
                FirstImpactTime = firstLandTime,
                EndTime = landTime + StrikeLingerSeconds
            });

            _eventBus?.Publish(new ArtilleryStrikeEvent(team, caller, target, false));
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime > 0f && !float.IsNaN(deltaTime) && !float.IsInfinity(deltaTime))
                _time += deltaTime;

            // _pending düşüş zamanına göre sıralı: baştan düşenleri al (sıra korunur, GC yok).
            var landed = 0;
            while (landed < _pending.Count && _pending[landed].LandTime <= _time)
                landed++;

            if (landed > 0)
            {
                _landedBuffer.Clear();
                for (var i = 0; i < landed; i++)
                    _landedBuffer.Add(_pending[i]);
                _pending.RemoveRange(0, landed);

                for (var i = 0; i < _landedBuffer.Count; i++)
                {
                    var shell = _landedBuffer[i];
                    if (DueImpacts.Count >= MaxQueuedImpacts)
                        DueImpacts.RemoveAt(0);

                    DueImpacts.Add(new ArtilleryImpact(shell.Position, shell.Team, shell.Caller));
                }

                // Olaylar, mermiler listeden çıkarıldıktan sonra yayınlanır: dinleyici TryCall/Reset çağırsa da tutarlı kalır.
                for (var i = 0; i < _landedBuffer.Count; i++)
                {
                    var shell = _landedBuffer[i];
                    _eventBus?.Publish(new ArtilleryStrikeEvent(shell.Team, shell.Caller, shell.Position, true));
                }

                _landedBuffer.Clear();
            }

            for (var i = _strikes.Count - 1; i >= 0; i--)
            {
                if (_strikes[i].EndTime <= _time)
                    _strikes.RemoveAt(i);
            }
        }

        /// <summary>Timin etkin (henüz bitmemiş) en son atışının hedefi — harita işareti için.</summary>
        public bool TryGetActiveStrike(int team, out Float3 target)
        {
            for (var i = _strikes.Count - 1; i >= 0; i--)
            {
                if (_strikes[i].Team != team)
                    continue;

                target = _strikes[i].Target;
                return true;
            }

            target = Float3.Zero;
            return false;
        }

        /// <summary>
        /// Nokta, etkin bir atışın tehlike alanında mı (yayılım + patlama yarıçapı + pay, yatay)? Botların kaçınması için.
        /// excludeTeam ≥ 0 ise o timin kendi atışları dikkate alınmaz.
        /// </summary>
        public bool IsInDangerZone(Float3 position, float margin = 0f, int excludeTeam = -1)
        {
            if (_strikes.Count == 0)
                return false;

            var radius = SpreadRadius + ShellRadius + (margin > 0f ? margin : 0f);
            var radiusSqr = radius * radius;
            for (var i = 0; i < _strikes.Count; i++)
            {
                var strike = _strikes[i];
                if (excludeTeam >= 0 && strike.Team == excludeTeam)
                    continue;

                var dx = position.X - strike.Target.X;
                var dz = position.Z - strike.Target.Z;
                if (dx * dx + dz * dz <= radiusSqr)
                    return true;
            }

            return false;
        }

        /// <summary>Timin etkin atışının ilk mermisine kalan süre (sn); atış yoksa ya da düşmeye başladıysa 0.</summary>
        public float GetSecondsUntilImpact(int team)
        {
            for (var i = _strikes.Count - 1; i >= 0; i--)
            {
                if (_strikes[i].Team != team)
                    continue;

                var remaining = _strikes[i].FirstImpactTime - _time;
                return remaining > 0f ? remaining : 0f;
            }

            return 0f;
        }

        /// <summary>Bekleyen mermileri ve bekleme sürelerini sıfırlar (yeni maç).</summary>
        public void Reset()
        {
            _pending.Clear();
            _readyAt.Clear();
            _strikes.Clear();
            _landedBuffer.Clear();
            DueImpacts.Clear();
            _time = 0f;
        }

        /// <summary>Bekleyen mermi listesini düşüş zamanına göre sıralı tutar (eşitlerde ekleme sırası korunur).</summary>
        private void InsertPending(PendingShell shell)
        {
            var index = _pending.Count;
            while (index > 0 && _pending[index - 1].LandTime > shell.LandTime)
                index--;

            _pending.Insert(index, shell);
        }

        private Float3 RandomPointInDisk(Float3 center, float radius)
        {
            var r = radius * (float)Math.Sqrt(_random.NextFloat());
            var angle = _random.NextFloat() * (float)(Math.PI * 2.0);
            return new Float3(center.X + (float)Math.Sin(angle) * r, center.Y, center.Z + (float)Math.Cos(angle) * r);
        }
    }

    public readonly struct ArtilleryImpact
    {
        public Float3 Position { get; }
        public int Team { get; }
        public PlayerId CallerId { get; }

        public ArtilleryImpact(Float3 position, int team, PlayerId callerId)
        {
            Position = position;
            Team = team;
            CallerId = callerId;
        }
    }

    /// <summary>
    /// İntikal planı: her tim için harita kenarında bir başlangıç ve iç bölgede bir iniş noktası (LZ) seçer; timler
    /// birbirinden uzak sektörlere (açıya göre ~eşit) dağıtılır. LZ kenardan 160-300 m içeride ve |x|,|z| &lt; 400 m'dir
    /// (küçük haritalarda orantılı). Oyuncu timi (0) verilen yöntemi kullanır; diğer timler sırayla (rastgele fazla)
    /// helikopter / zırhlı araç. Zırhlı araç başlangıcı kenarın hemen içinde yerde; helikopter başlangıcı kenarın dışında.
    /// Start/LandingZone Y = 0 (yüksekliği Altyapı katmanı zemine/irtifaya göre belirler). Zırhlı araç rotası
    /// Start→LZ düz hattıdır.
    /// </summary>
    public static class InsertionPlanner
    {
        public const float ReferenceHalfSize = 512f;
        public const float MinInwardDistance = 160f;
        public const float MaxInwardDistance = 300f;
        public const float MaxLandingZoneCoordinate = 400f;

        /// <summary>Helikopter başlangıcının kenar dışındaki mesafesi.</summary>
        public const float HelicopterStartOutside = 60f;

        /// <summary>Zırhlı araç başlangıcının kenardan içerideki mesafesi.</summary>
        public const float VehicleStartInside = 25f;

        public static TeamInsertion[] Plan(int teamCount, float mapHalfSize, IRandom random, InsertionMethod playerTeamMethod)
        {
            if (teamCount <= 0)
                return Array.Empty<TeamInsertion>();

            if (mapHalfSize <= 1f || float.IsNaN(mapHalfSize) || float.IsInfinity(mapHalfSize))
                mapHalfSize = ReferenceHalfSize;

            random ??= new SeededRandom(teamCount * 7919 + (int)mapHalfSize);

            var scale = mapHalfSize < ReferenceHalfSize ? mapHalfSize / ReferenceHalfSize : 1f;
            var inwardMin = MinInwardDistance * scale;
            var inwardMax = MaxInwardDistance * scale;
            var limit = Math.Min(MaxLandingZoneCoordinate, mapHalfSize * (MaxLandingZoneCoordinate / ReferenceHalfSize)) - 1f;
            var minSeparation = teamCount > 1
                ? Math.Min(220f * scale, (float)(2.0 * (mapHalfSize * 0.5f) * Math.Sin(Math.PI / teamCount)))
                : 0f;

            // Sektörleri karıştır: oyuncu timi her seferinde aynı komşuya sahip olmasın.
            var sectors = new int[teamCount];
            for (var i = 0; i < teamCount; i++)
                sectors[i] = i;
            for (var i = teamCount - 1; i > 0; i--)
            {
                var j = random.Next(0, i + 1);
                (sectors[i], sectors[j]) = (sectors[j], sectors[i]);
            }

            var sectorWidth = Math.PI * 2.0 / teamCount;
            var baseAngle = random.NextFloat() * Math.PI * 2.0;
            var otherMethod = playerTeamMethod == InsertionMethod.Helicopter ? InsertionMethod.ArmoredVehicle : InsertionMethod.Helicopter;
            var phase = random.Next(0, 2);

            var result = new TeamInsertion[teamCount];
            for (var team = 0; team < teamCount; team++)
            {
                InsertionMethod method;
                if (team == 0)
                    method = playerTeamMethod;
                else
                    method = (team + phase) % 2 == 1 ? otherMethod : playerTeamMethod;

                Float3 start = default;
                Float3 landingZone = default;
                var bestScore = -1f;
                for (var attempt = 0; attempt < 8; attempt++)
                {
                    var jitter = (random.NextFloat() - 0.5) * sectorWidth * 0.5;
                    var angle = baseAngle + sectors[team] * sectorWidth + jitter;
                    var dirX = (float)Math.Sin(angle);
                    var dirZ = (float)Math.Cos(angle);
                    var edgeDistance = mapHalfSize / Math.Max(Math.Abs(dirX), Math.Abs(dirZ));
                    var edge = new Float3(dirX * edgeDistance, 0f, dirZ * edgeDistance);

                    var inward = random.Range(inwardMin, inwardMax);
                    var lzX = Clamp(edge.X - dirX * inward, -limit, limit);
                    var lzZ = Clamp(edge.Z - dirZ * inward, -limit, limit);
                    var candidateLz = new Float3(lzX, 0f, lzZ);

                    var outward = method == InsertionMethod.Helicopter ? HelicopterStartOutside * scale : -VehicleStartInside * scale;
                    var candidateStart = new Float3(edge.X + dirX * outward, 0f, edge.Z + dirZ * outward);

                    var separation = NearestDistance(result, team, candidateLz);
                    if (separation > bestScore)
                    {
                        bestScore = separation;
                        start = candidateStart;
                        landingZone = candidateLz;
                    }

                    if (separation >= minSeparation)
                        break;
                }

                result[team] = new TeamInsertion(team, method, start, landingZone);
            }

            return result;
        }

        private static float NearestDistance(TeamInsertion[] planned, int count, Float3 point)
        {
            var nearest = float.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var d = Float3.DistanceXZ(planned[i].LandingZone, point);
                if (d < nearest)
                    nearest = d;
            }

            return nearest;
        }

        private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
    }

    public readonly struct TeamInsertion
    {
        public int Team { get; }
        public InsertionMethod Method { get; }
        public Float3 Start { get; }
        public Float3 LandingZone { get; }

        public TeamInsertion(int team, InsertionMethod method, Float3 start, Float3 landingZone)
        {
            Team = team;
            Method = method;
            Start = start;
            LandingZone = landingZone;
        }

        /// <summary>Başlangıçtan LZ'ye yatay yön (birim).</summary>
        public Float3 Direction => (LandingZone - Start).Flat.Normalized;

        /// <summary>Başlangıçtan LZ'ye yatay mesafe.</summary>
        public float Distance => Float3.DistanceXZ(Start, LandingZone);

        /// <summary>Başlangıçtan LZ'ye bakış açısı (Y ekseni, derece; 0 = +Z).</summary>
        public float HeadingDegrees
        {
            get
            {
                var d = LandingZone - Start;
                return (float)(Math.Atan2(d.X, d.Z) * 180.0 / Math.PI);
            }
        }
    }
}
