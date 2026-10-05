using System;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure.Combat;
using UnityEngine;

namespace Project.Infrastructure.AI
{
    /// <summary>
    /// Botun duyuları ve hafızası (GC'siz, kademeli çalışır — BotController 0.2-0.3 sn'de bir Scan çağırır):
    ///  • Görme: yalnızca düşmanlar (ITeamRelations / Combatant.Team); görüş mesafesi (hedefin duruşuna göre kısalır) ve
    ///    görüş açısı içinde — 15 m içinde açı aranmaz; göz → hedef gövde/kafa ışını <see cref="GameLayers.LineOfSightMask"/>
    ///    ve <see cref="SmokeVolume.BlocksLineOfSight"/>. Işın bütçesi: tarama başına en yakın birkaç aday.
    ///  • İşitme: <see cref="BotDirector"/> silah sesi tamponu (düşman atışı, HearingDistance × ses şiddeti) → son duyulan nokta.
    ///  • Hasar: kaynak yönüne dönme isteği ve saldırganın son bilinen konumu.
    ///  • Müttefik farkındalığı: tim arkadaşlarının temas/yardım çağrıları.
    /// </summary>
    public sealed class BotPerception
    {
        public const float AlwaysAwareDistance = 15f;
        public const float HeardMemorySeconds = 8f;
        public const float AllyHelpMemorySeconds = 7f;
        public const float AllyHelpMaxDistance = 110f;

        private const int MaxCandidates = 5;
        private const float ReacquireGraceSeconds = 1.5f;
        private const float ContactReportInterval = 1f;

        private readonly Combatant[] _candidates = new Combatant[MaxCandidates];
        private readonly float[] _candidateSqr = new float[MaxCandidates];
        private readonly System.Random _rng;

        private Combatant _self;
        private int _gunfireCursor = -1;
        private float _nextContactReport;

        public BotPerception(int seed)
        {
            _rng = new System.Random(seed);
        }

        /// <summary>Şu an görülen (hedef alınan) düşman; yoksa null.</summary>
        public Combatant Target { get; private set; }

        public float TargetDistance { get; private set; }

        /// <summary>Hedefin kesintisiz görülmeye başladığı an (tepki süresi bundan sayılır).</summary>
        public float TargetAcquiredTime { get; private set; } = -999f;

        public float LastSeenTime { get; private set; } = -999f;
        public Combatant LastSeenEnemy { get; private set; }

        public bool HasLastKnownEnemyPosition { get; private set; }
        public Vector3 LastKnownEnemyPosition { get; private set; }

        public float LastHeardTime { get; private set; } = -999f;
        public Vector3 HeardPosition { get; private set; }

        public float LastDamagedTime { get; private set; } = -999f;
        public Vector3 DamageSourcePosition { get; private set; }
        public bool DamageSourceKnown { get; private set; }
        public PlayerId LastAttacker { get; private set; } = PlayerId.Invalid;

        /// <summary>Düşman kaynaklı son hasar zamanı (bölge/düşme hasarı hariç).</summary>
        public float LastEnemyDamageTime { get; private set; } = -999f;

        /// <summary>Hasar sonrası bu zamana kadar kaynağa dönülür.</summary>
        public float AlertUntil { get; private set; } = -999f;
        public Vector3 AlertPosition { get; private set; }

        public bool AllyNeedsHelp { get; private set; }
        public Vector3 AllyHelpPosition { get; private set; }

        /// <summary>Son taramada görülebilen düşman sayısı (en fazla aday sayısı kadar).</summary>
        public int VisibleEnemyCount { get; private set; }

        public void Bind(Combatant self)
        {
            _self = self;
        }

        /// <summary>Tüm hafızayı siler (ölüm / yeniden doğma).</summary>
        public void Reset()
        {
            Target = null;
            LastSeenEnemy = null;
            TargetDistance = 0f;
            TargetAcquiredTime = -999f;
            LastSeenTime = -999f;
            HasLastKnownEnemyPosition = false;
            LastHeardTime = -999f;
            LastDamagedTime = -999f;
            LastEnemyDamageTime = -999f;
            DamageSourceKnown = false;
            LastAttacker = PlayerId.Invalid;
            AlertUntil = -999f;
            AllyNeedsHelp = false;
            VisibleEnemyCount = 0;
            _gunfireCursor = -1;
        }

        /// <summary>Araştırma noktasına varıldı: duyulan/son bilinen konum hafızası tüketilir.</summary>
        public void ClearInvestigation()
        {
            LastHeardTime = -999f;
            HasLastKnownEnemyPosition = false;
            AllyNeedsHelp = false;
        }

        public bool IsEnemy(Combatant other, ITeamRelations relations)
        {
            if (other == null || _self == null || ReferenceEquals(other, _self))
                return false;

            if (other.Team != _self.Team)
            {
                if (relations == null)
                    return true;

                try
                {
                    var a = relations.GetTeam(_self.Id);
                    var b = relations.GetTeam(other.Id);
                    if (a >= 0 && b >= 0)
                        return a != b;
                }
                catch (Exception)
                {
                    // ilişki servisi hazır değil — Combatant.Team yeterli
                }

                return true;
            }

            return false;
        }

        // ------------------------------------------------------------------ görme

        /// <summary>Görüş taraması. eye = göz konumu, forward = bakış yönü (yatay).</summary>
        public void Scan(Vector3 eye, Vector3 forward, BotDifficultyProfile profile, ITeamRelations relations, BotDirector director, float now)
        {
            if (_self == null)
                return;

            var viewDistance = profile != null ? Mathf.Max(20f, profile.ViewDistance) : 200f;
            var fov = profile != null ? Mathf.Clamp(profile.FieldOfViewDegrees, 30f, 360f) : 120f;
            var cosHalfFov = Mathf.Cos(fov * 0.5f * Mathf.Deg2Rad);
            var alwaysSqr = AlwaysAwareDistance * AlwaysAwareDistance;

            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f)
                forward = Vector3.forward;
            forward.Normalize();

            var count = 0;
            var previous = Target;
            var previousValid = previous != null && previous.IsAlive && previous.IsTargetable;

            var all = CombatantRegistry.All;
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !c.IsAlive || !c.IsTargetable || !IsEnemy(c, relations))
                    continue;

                var offset = c.transform.position - eye;
                var sqr = offset.sqrMagnitude;

                var effective = viewDistance * StanceVisibility(c);
                if (sqr > effective * effective)
                    continue;

                if (sqr > alwaysSqr && !ReferenceEquals(c, previous))
                {
                    var flat = new Vector3(offset.x, 0f, offset.z);
                    var flatMag = flat.magnitude;
                    if (flatMag > 0.01f && Vector3.Dot(forward, flat / flatMag) < cosHalfFov)
                        continue;
                }

                InsertCandidate(c, sqr, ref count);
            }

            // Önce mevcut hedefi doğrula (yapışkanlık), sonra en yakın adaylar.
            Combatant visible = null;
            var visibleSqr = float.MaxValue;
            var visibleCount = 0;

            if (previousValid && ContainsCandidate(previous, count) && HasLineOfSight(eye, previous))
            {
                visible = previous;
                visibleSqr = (previous.transform.position - eye).sqrMagnitude;
                visibleCount = 1;
            }

            for (var i = 0; i < count; i++)
            {
                var c = _candidates[i];
                if (ReferenceEquals(c, previous))
                    continue;

                // Mevcut hedeften belirgin şekilde yakın değilse ışın harcama.
                if (visible != null && _candidateSqr[i] > visibleSqr * 0.3f)
                {
                    continue;
                }

                if (!HasLineOfSight(eye, c))
                    continue;

                visibleCount++;
                if (_candidateSqr[i] < visibleSqr)
                {
                    visible = c;
                    visibleSqr = _candidateSqr[i];
                }
            }

            for (var i = 0; i < count; i++)
                _candidates[i] = null;

            VisibleEnemyCount = visibleCount;

            if (visible != null)
            {
                var isNew = !ReferenceEquals(visible, previous) && !(ReferenceEquals(visible, LastSeenEnemy) && now - LastSeenTime < ReacquireGraceSeconds);
                if (isNew || now - LastSeenTime > ReacquireGraceSeconds)
                    TargetAcquiredTime = now;

                Target = visible;
                TargetDistance = Mathf.Sqrt(visibleSqr);
                LastSeenTime = now;
                LastSeenEnemy = visible;
                LastKnownEnemyPosition = visible.transform.position;
                HasLastKnownEnemyPosition = true;

                if (director != null && now >= _nextContactReport)
                {
                    _nextContactReport = now + ContactReportInterval;
                    director.ReportContact(_self.Team, LastKnownEnemyPosition, visible.Id, now);
                }
            }
            else
            {
                Target = null;
                TargetDistance = 0f;
            }

            UpdateAllyAwareness(director, now);
        }

        private void InsertCandidate(Combatant c, float sqr, ref int count)
        {
            var index = count;
            if (count == MaxCandidates)
            {
                if (sqr >= _candidateSqr[MaxCandidates - 1])
                    return;
                index = MaxCandidates - 1;
            }
            else
            {
                count++;
            }

            while (index > 0 && _candidateSqr[index - 1] > sqr)
            {
                _candidates[index] = _candidates[index - 1];
                _candidateSqr[index] = _candidateSqr[index - 1];
                index--;
            }

            _candidates[index] = c;
            _candidateSqr[index] = sqr;
        }

        private bool ContainsCandidate(Combatant c, int count)
        {
            for (var i = 0; i < count; i++)
            {
                if (ReferenceEquals(_candidates[i], c))
                    return true;
            }

            return false;
        }

        /// <summary>Göz → hedef gövdesi (olmazsa kafası) görüş hattı; dünya, araç ve sis engeller.</summary>
        public static bool HasLineOfSight(Vector3 eye, Combatant target)
        {
            if (target == null)
                return false;

            var body = target.AimPoint != null ? target.AimPoint.position : target.transform.position + Vector3.up * 1.15f;
            if (IsClear(eye, body))
                return true;

            var head = target.EyePosition;
            return IsClear(eye, head);
        }

        public static bool IsClear(Vector3 from, Vector3 to)
        {
            if (Physics.Linecast(from, to, GameLayers.LineOfSightMask, QueryTriggerInteraction.Ignore))
                return false;

            return !SmokeVolume.BlocksLineOfSight(from, to);
        }

        private static float StanceVisibility(Combatant c)
        {
            float factor;
            switch (c.Stance)
            {
                case Stance.Crouching:
                    factor = 0.8f;
                    break;
                case Stance.Prone:
                    factor = 0.55f;
                    break;
                default:
                    factor = 1f;
                    break;
            }

            // Hareket eden hedef daha kolay fark edilir.
            var v = c.Velocity;
            if (v.x * v.x + v.z * v.z > 4f)
                factor = Mathf.Min(1.1f, factor + 0.2f);

            return factor;
        }

        // ------------------------------------------------------------------ işitme

        /// <summary>Koordinatördeki yeni silah seslerini işler (düşman atışları).</summary>
        public void ProcessHearing(BotDirector director, Vector3 position, float hearingDistance, ITeamRelations relations, float now)
        {
            if (director == null || _self == null)
                return;

            var latest = director.GunfireSequence;
            if (_gunfireCursor < 0 || latest - _gunfireCursor > BotDirector.GunfireCapacity)
                _gunfireCursor = Mathf.Max(0, latest - BotDirector.GunfireCapacity);

            var bestSqr = float.MaxValue;
            var heard = false;
            var heardPosition = Vector3.zero;
            var heardTime = 0f;

            for (var seq = _gunfireCursor; seq < latest; seq++)
            {
                if (!director.TryGetGunfire(seq, out var record))
                    continue;

                if (record.Shooter == _self.Id)
                    continue;

                if (now - record.Time > HeardMemorySeconds)
                    continue;

                if (!IsEnemyShot(record, relations))
                    continue;

                var range = hearingDistance * record.Loudness;
                var sqr = (record.Position - position).sqrMagnitude;
                if (sqr > range * range || sqr >= bestSqr)
                    continue;

                bestSqr = sqr;
                heard = true;
                heardPosition = record.Position;
                heardTime = record.Time;
            }

            _gunfireCursor = latest;

            if (!heard)
                return;

            // Uzaktan duyulan sesin konumu belirsizdir.
            var distance = Mathf.Sqrt(bestSqr);
            var error = distance * 0.08f;
            heardPosition.x += ((float)_rng.NextDouble() - 0.5f) * 2f * error;
            heardPosition.z += ((float)_rng.NextDouble() - 0.5f) * 2f * error;

            LastHeardTime = Mathf.Max(LastHeardTime, heardTime);
            HeardPosition = heardPosition;

            // Hedef yokken yakındaki silah sesine kısaca kulak kabart.
            if (Target == null && distance < 60f && now > AlertUntil)
            {
                AlertPosition = heardPosition;
                AlertUntil = now + 1.2f;
            }
        }

        private bool IsEnemyShot(in BotDirector.GunfireRecord record, ITeamRelations relations)
        {
            if (record.Team >= 0)
                return record.Team != _self.Team;

            if (CombatantRegistry.TryGet(record.Shooter, out var shooter) && shooter != null)
                return IsEnemy(shooter, relations);

            return true;
        }

        // ------------------------------------------------------------------ hasar

        /// <summary>Hasar alındı: kaynağa dönülür, saldırgan düşmansa son bilinen konumu güncellenir.</summary>
        public void OnDamaged(in DamageInfo damage, Vector3 selfPosition, ITeamRelations relations, BotDirector director, float now)
        {
            LastDamagedTime = now;
            LastAttacker = damage.AttackerId;

            Combatant attacker = null;
            if (damage.AttackerId.IsValid && CombatantRegistry.TryGet(damage.AttackerId, out var a) && a != null && !ReferenceEquals(a, _self))
                attacker = a;

            var known = false;
            var source = selfPosition;
            if (damage.HasSourcePosition)
            {
                source = new Vector3(damage.SourcePosition.X, damage.SourcePosition.Y, damage.SourcePosition.Z);
                known = true;
            }
            else if (attacker != null)
            {
                source = attacker.transform.position;
                known = true;
            }

            DamageSourceKnown = known;
            DamageSourcePosition = source;

            if (!known)
                return;

            var enemyAttacker = attacker == null || IsEnemy(attacker, relations);
            if (!enemyAttacker)
                return; // dost ateşi: dönme, hedefleme

            AlertPosition = attacker != null ? attacker.transform.position : source;
            AlertUntil = now + 2f;
            LastEnemyDamageTime = now;
            if (Target == null)
            {
                LastKnownEnemyPosition = AlertPosition;
                HasLastKnownEnemyPosition = true;
            }

            director?.ReportUnderFire(_self.Team, selfPosition, AlertPosition, true, now);
        }

        // ------------------------------------------------------------------ müttefik

        private void UpdateAllyAwareness(BotDirector director, float now)
        {
            AllyNeedsHelp = false;
            if (director == null || _self == null || Target != null)
                return;

            if (!director.TryGetHelpRequest(_self.Team, now, AllyHelpMemorySeconds, out var help))
                return;

            var position = _self.transform.position;
            var d = BotDirector.FlatDistance(position, help);
            if (d > AllyHelpMaxDistance || d < 6f)
                return;

            AllyNeedsHelp = true;
            AllyHelpPosition = help;
        }
    }
}
