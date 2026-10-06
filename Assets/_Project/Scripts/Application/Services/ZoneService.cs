using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>
    /// Mavi bölge faz planı. Start() ile faz 0 Waiting başlar; her fazda yeni güvenli çember mevcut çemberin TAMAMEN
    /// içinde ve merkezi harita sınırları içinde seçilir. Waiting → Shrinking (merkez ve yarıçap doğrusal enterpole) →
    /// sonraki faz. Son faz bitince Finished. Her aşama değişiminde ZoneStageChangedEvent yayınlar.
    /// Bölge dışı hasar = aktif fazın DamagePerSecond'ı (Start'tan önce 0).
    /// Ayrıntılar:
    ///  - Yeni çemberin merkezi |x|,|z| ≤ harita yarı boyutu × 0.8 aralığında seçilir; iki koşul birlikte sağlanamazsa
    ///    (ör. ilk çember haritanın dışında) "tamamen içinde" kuralı önceliklidir.
    ///  - Sonraki çember, fazın bekleme aşaması başlarken seçilir (NextZone, HUD'daki beyaz çember).
    ///  - Büyük tick adımları aşama sınırlarını doğru şekilde aşar (zamanlama deterministiktir).
    ///  - Faz planı boşsa Start() doğrudan Finished'a geçer ve Initialize'daki hasar kullanılır.
    /// </summary>
    public sealed class ZoneService : IZoneService, IGameTickService
    {
        private const float CenterBoundsFactor = 0.8f;
        private const int CenterSampleAttempts = 32;

        /// <summary>Arazi çıpaları yalnızca hedef yarıçapı bu değere eşit/küçük fazlarda devreye girer (final oyunu).</summary>
        public const float AnchorMaxTargetRadius = 280f;

        /// <summary>Çıpa puanlamasında değerlendirilen en çok aday merkez sayısı.</summary>
        private const int AnchorCandidateCount = 10;

        /// <summary>Çıpanın çember içinde sayılması için hedef yarıçapa eklenen pay (m).</summary>
        private const float AnchorReachMargin = 80f;

        private readonly ZonePhase[] _phases;
        private readonly IRandom _random;
        private readonly IEventBus _eventBus;
        private readonly float _mapHalfSize;

        private ZoneState _current;
        private ZoneState _next;
        private ZoneState _shrinkFrom;
        private ZoneStage _stage = ZoneStage.Idle;
        private int _phaseIndex;
        private float _stageRemaining;
        private float _stageDuration;
        private float _lerpDuration;
        private float _baseDamagePerSecond;
        private bool _active;
        private ZoneAnchor[] _anchors = Array.Empty<ZoneAnchor>();

        public ZoneService(ZonePhase[] phases, IRandom random, IEventBus eventBus, float mapHalfSize)
        {
            _phases = phases != null ? (ZonePhase[])phases.Clone() : MatchConfig.DefaultZonePhases();
            _random = random ?? new SeededRandom(0);
            _eventBus = eventBus;
            _mapHalfSize = mapHalfSize > 0f && !float.IsInfinity(mapHalfSize) ? mapHalfSize : 512f;

            // Varsayılan başlangıç çemberi tüm haritayı (köşeler dahil) kaplar.
            var initialRadius = _mapHalfSize * 1.45f;
            _current = new ZoneState(0f, 0f, initialRadius, 0f);
            _next = _current;
            _shrinkFrom = _current;
        }

        /// <summary>
        /// Arazi çıpaları (tepe, köy, kale, nehir geçidi...): final çemberleri (hedef yarıçap ≤ AnchorMaxTargetRadius)
        /// aday merkezlerden çıpaya en iyi oturanı seçer; böylece son çatışmalar açık düzlükte değil sipere/yüksekliğe denk gelir.
        /// "Mevcut çemberin tamamen içinde" ve harita sınırı kuralları aynen korunur. Boş/null = eski rastgele davranış.
        /// </summary>
        public void SetAnchors(IReadOnlyList<ZoneAnchor> anchors)
        {
            if (anchors == null || anchors.Count == 0)
            {
                _anchors = Array.Empty<ZoneAnchor>();
                return;
            }

            var list = new List<ZoneAnchor>(anchors.Count);
            for (var i = 0; i < anchors.Count; i++)
            {
                var a = anchors[i];
                if (IsFinite(a.X) && IsFinite(a.Z) && a.Weight > 0f && IsFinite(a.Weight))
                    list.Add(a);
            }

            _anchors = list.ToArray();
        }

        public int AnchorCount => _anchors.Length;

        public ZoneState CurrentZone => new(_current.CenterX, _current.CenterZ, _current.Radius, CurrentDamagePerSecond);

        /// <summary>Bir sonraki güvenli bölge (beyaz çember).</summary>
        public ZoneState NextZone => new(_next.CenterX, _next.CenterZ, _next.Radius, CurrentDamagePerSecond);

        public ZoneStage Stage => _stage;
        public int PhaseIndex => _phaseIndex;
        public int PhaseCount => _phases.Length;

        public float StageRemainingSeconds =>
            _stage == ZoneStage.Waiting || _stage == ZoneStage.Shrinking ? Math.Max(0f, _stageRemaining) : 0f;

        public float StageDurationSeconds =>
            _stage == ZoneStage.Waiting || _stage == ZoneStage.Shrinking ? _stageDuration : 0f;

        public bool IsActive => _active;

        /// <summary>Aşamanın ilerleme oranı (0..1); Waiting/Shrinking dışında 0.</summary>
        public float StageProgress01
        {
            get
            {
                if (_stage != ZoneStage.Waiting && _stage != ZoneStage.Shrinking)
                    return 0f;

                if (_stageDuration <= 0f)
                    return 1f;

                var t = 1f - _stageRemaining / _stageDuration;
                return t < 0f ? 0f : t > 1f ? 1f : t;
            }
        }

        public float MapHalfSize => _mapHalfSize;

        public float CurrentDamagePerSecond
        {
            get
            {
                if (!_active)
                    return 0f;

                if (_phases.Length == 0)
                    return _baseDamagePerSecond;

                var index = _phaseIndex < 0 ? 0 : _phaseIndex >= _phases.Length ? _phases.Length - 1 : _phaseIndex;
                return _phases[index].DamagePerSecond;
            }
        }

        public void Initialize(float centerX, float centerZ, float radius, float damagePerSecond)
        {
            if (!IsFinite(centerX)) centerX = 0f;
            if (!IsFinite(centerZ)) centerZ = 0f;
            if (!IsFinite(radius) || radius < 0f) radius = 0f;
            if (!IsFinite(damagePerSecond) || damagePerSecond < 0f) damagePerSecond = 0f;

            _baseDamagePerSecond = damagePerSecond;
            _current = new ZoneState(centerX, centerZ, radius, damagePerSecond);
            _next = _current;
            _shrinkFrom = _current;
            _phaseIndex = 0;
            _stageRemaining = 0f;
            _stageDuration = 0f;
            _lerpDuration = 0f;
            _active = false;
            SetStage(ZoneStage.Idle, 0f);
        }

        /// <summary>Faz planını başlatır (ilk bekleme). Zaten çalışıyorsa yok sayılır.</summary>
        public void Start()
        {
            if (_active)
                return;

            _active = true;

            if (_phases.Length == 0)
            {
                _phaseIndex = 0;
                _next = _current;
                SetStage(ZoneStage.Finished, 0f);
                return;
            }

            BeginPhase(0);
        }

        /// <summary>
        /// Elle anında daraltma (hata ayıklama / sunucu komutu). Merkez korunur; sonraki çember artık sığmıyorsa
        /// yeni çembere eşitlenir; daralma sürüyorsa enterpolasyon yeni durumdan kalan sürede devam eder.
        /// </summary>
        public void Shrink(float newRadius)
        {
            if (!IsFinite(newRadius))
                return;

            if (newRadius < 0f)
                newRadius = 0f;

            _current = new ZoneState(_current.CenterX, _current.CenterZ, newRadius, _current.DamagePerSecond);

            if (_stage != ZoneStage.Waiting && _stage != ZoneStage.Shrinking)
            {
                _next = _current;
                _shrinkFrom = _current;
                return;
            }

            if (!CircleInside(_next, _current))
                _next = new ZoneState(_current.CenterX, _current.CenterZ, Math.Min(_next.Radius, newRadius), _next.DamagePerSecond);

            if (_stage == ZoneStage.Shrinking)
            {
                _shrinkFrom = _current;
                _lerpDuration = Math.Max(0f, _stageRemaining);
            }
        }

        /// <summary>Güvenli bölgede mi? Bölge başlamadan önce her yer güvenlidir.</summary>
        public bool IsInsideZone(float x, float z) => !_active || _current.Contains(x, z);

        public float GetDamagePerSecond(float x, float z)
        {
            if (!_active || _current.Contains(x, z))
                return 0f;

            return CurrentDamagePerSecond;
        }

        /// <summary>Bir sonraki güvenli bölgeye kalan mesafe (içerideyse 0).</summary>
        public float DistanceToSafeZone(float x, float z)
        {
            var dx = x - _next.CenterX;
            var dz = z - _next.CenterZ;
            var distance = (float)Math.Sqrt(dx * dx + dz * dz) - _next.Radius;
            return distance > 0f ? distance : 0f;
        }

        public void Tick(float deltaTime)
        {
            if (!_active || !(deltaTime > 0f) || float.IsInfinity(deltaTime))
                return;

            var remaining = deltaTime;
            // Her yinelemede ya süre tüketilir ya da aşama ilerler; faz sayısı sonlu olduğundan döngü sonlanır.
            var guard = _phases.Length * 2 + 4;
            while ((_stage == ZoneStage.Waiting || _stage == ZoneStage.Shrinking) && guard-- > 0)
            {
                var step = remaining < _stageRemaining ? remaining : _stageRemaining;
                if (step < 0f)
                    step = 0f;

                _stageRemaining -= step;
                remaining -= step;

                if (_stage == ZoneStage.Shrinking)
                    UpdateShrinkInterpolation();

                if (_stageRemaining > 0f)
                    break;

                AdvanceStage();

                if (remaining <= 0f)
                    break;
            }
        }

        private void BeginPhase(int index)
        {
            _phaseIndex = index;
            var phase = _phases[index];
            _next = PickNextCircle(_current, phase);
            _shrinkFrom = _current;

            var wait = SanitizeDuration(phase.WaitSeconds);
            _stageRemaining = wait;
            SetStage(ZoneStage.Waiting, wait);
        }

        private void AdvanceStage()
        {
            if (_stage == ZoneStage.Waiting)
            {
                var shrink = SanitizeDuration(_phases[_phaseIndex].ShrinkSeconds);
                _shrinkFrom = _current;
                _lerpDuration = shrink;
                _stageRemaining = shrink;
                SetStage(ZoneStage.Shrinking, shrink);
                return;
            }

            if (_stage == ZoneStage.Shrinking)
            {
                _current = _next;
                _shrinkFrom = _current;

                if (_phaseIndex + 1 < _phases.Length)
                {
                    BeginPhase(_phaseIndex + 1);
                    return;
                }

                _stageRemaining = 0f;
                SetStage(ZoneStage.Finished, 0f);
            }
        }

        private void UpdateShrinkInterpolation()
        {
            float t;
            if (_lerpDuration <= 0f)
                t = 1f;
            else
            {
                t = 1f - _stageRemaining / _lerpDuration;
                t = t < 0f ? 0f : t > 1f ? 1f : t;
            }

            var cx = _shrinkFrom.CenterX + (_next.CenterX - _shrinkFrom.CenterX) * t;
            var cz = _shrinkFrom.CenterZ + (_next.CenterZ - _shrinkFrom.CenterZ) * t;
            var r = _shrinkFrom.Radius + (_next.Radius - _shrinkFrom.Radius) * t;
            _current = new ZoneState(cx, cz, r, _current.DamagePerSecond);
        }

        /// <summary>Yeni çember: mevcut çemberin tamamen içinde, merkezi harita sınırı × 0.8 içinde (tohumlu rastgele).</summary>
        private ZoneState PickNextCircle(ZoneState current, ZonePhase phase)
        {
            var targetRadius = phase.TargetRadius;
            if (!IsFinite(targetRadius) || targetRadius < 0f)
                targetRadius = 0f;

            var radius = Math.Min(targetRadius, current.Radius);
            var allowed = current.Radius - radius;
            // Kayan nokta payı: merkez kayması "tamamen içinde" koşulunu sayısal olarak bozmasın.
            allowed = Math.Max(0f, allowed * 0.999f);
            var bound = _mapHalfSize * CenterBoundsFactor;

            var useAnchors = _anchors.Length > 0 && radius <= AnchorMaxTargetRadius;
            var bestScore = -1f;
            var bestX = 0f;
            var bestZ = 0f;
            var found = 0;

            for (var attempt = 0; attempt < CenterSampleAttempts; attempt++)
            {
                // Disk içinde düzgün dağılım.
                var angle = _random.NextFloat() * (float)(Math.PI * 2.0);
                var distance = allowed * (float)Math.Sqrt(Clamp01(_random.NextFloat()));
                var x = current.CenterX + (float)Math.Cos(angle) * distance;
                var z = current.CenterZ + (float)Math.Sin(angle) * distance;
                if (Math.Abs(x) <= bound && Math.Abs(z) <= bound)
                {
                    if (!useAnchors)
                        return new ZoneState(x, z, radius, phase.DamagePerSecond);

                    var score = AnchorScore(x, z, radius);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestX = x;
                        bestZ = z;
                    }

                    if (++found >= AnchorCandidateCount)
                        break;
                }
            }

            if (found > 0)
                return new ZoneState(bestX, bestZ, radius, phase.DamagePerSecond);

            // Yedek: sınır kutusuna en yakın, izin verilen disk içindeki nokta.
            var px = Clamp(current.CenterX, -bound, bound);
            var pz = Clamp(current.CenterZ, -bound, bound);
            var ox = px - current.CenterX;
            var oz = pz - current.CenterZ;
            var offset = (float)Math.Sqrt(ox * ox + oz * oz);
            if (offset > allowed && offset > 1e-6f)
            {
                var scale = allowed / offset;
                px = current.CenterX + ox * scale;
                pz = current.CenterZ + oz * scale;
            }

            return new ZoneState(px, pz, radius, phase.DamagePerSecond);
        }

        /// <summary>Çemberin çıpaya uyumu: en iyi (ağırlık × yakınlık). Merkezi çıpaya yakın çember yüksek puan alır.</summary>
        private float AnchorScore(float x, float z, float radius)
        {
            var reach = radius + AnchorReachMargin;
            var best = 0f;
            for (var i = 0; i < _anchors.Length; i++)
            {
                var dx = x - _anchors[i].X;
                var dz = z - _anchors[i].Z;
                var d = (float)Math.Sqrt(dx * dx + dz * dz);
                var s = _anchors[i].Weight * Clamp01(1f - d / reach);
                if (s > best)
                    best = s;
            }

            return best;
        }

        private void SetStage(ZoneStage stage, float duration)
        {
            var changed = stage != _stage;
            _stage = stage;
            _stageDuration = duration;

            // Waiting → Waiting (sonraki faz) de bir aşama değişimidir.
            if (changed || stage == ZoneStage.Waiting)
                _eventBus?.Publish(new ZoneStageChangedEvent(_phaseIndex, _phases.Length, stage, duration));
        }

        private static bool CircleInside(ZoneState inner, ZoneState outer)
        {
            var dx = inner.CenterX - outer.CenterX;
            var dz = inner.CenterZ - outer.CenterZ;
            return (float)Math.Sqrt(dx * dx + dz * dz) + inner.Radius <= outer.Radius + 1e-3f;
        }

        private static float SanitizeDuration(float seconds) => IsFinite(seconds) && seconds > 0f ? seconds : 0f;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
        private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
    }
}
