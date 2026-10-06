using System;
using Project.Application.Services;

namespace Project.Infrastructure.Weapons.Reload
{
    /// <summary>Doldurma evreleri (gerçek silah sırası): hazırlık, şarjör bırakma, şarjör çıkış, yenisini alma, yerleştirme, oturma, kurma, toparlanma.</summary>
    public enum ReloadPhase
    {
        Prep = 0,
        MagRelease,
        Fetch,
        Insert,
        Seat,
        Chamber,
        Recover
    }

    /// <summary>
    /// Doldurma evre zaman çizelgesi (saf, Unity bağımsız). Evre sınırları 0..1 normalize edilir ve
    /// ViewmodelPoseTimeline sabitleriyle (RifleMagOut/RifleMagIn...) hizalıdır; ses ipuçları bu sınırlara oturur.
    /// Taktik doldurmada (namluda fişek) Chamber evresi sıfır uzunluktadır; boşta sürgü/kızak bırakma evresi vardır.
    /// </summary>
    public sealed class ReloadPhasePlan
    {
        public const int PhaseCount = 7;

        /// <summary>Her evrenin bitiş zamanı (0..1), azalmayan.</summary>
        private readonly float[] _ends;

        public ReloadKind Kind { get; }
        public bool Empty { get; }
        public float TotalSeconds { get; }

        private ReloadPhasePlan(ReloadKind kind, bool empty, float totalSeconds, float[] ends)
        {
            Kind = kind; Empty = empty; TotalSeconds = totalSeconds; _ends = ends;
        }

        /// <summary>Gerçek silahta ölçülen referans süreler (sn): taktik / boş. Tüfek ~2.1/2.7, tabanca ~1.5/1.9.</summary>
        public static float ReferenceSeconds(ReloadKind kind, bool empty)
        {
            switch (kind)
            {
                case ReloadKind.Pistol: return empty ? 1.9f : 1.5f;
                case ReloadKind.BoltAction: return 2.8f;
                case ReloadKind.Machinegun: return empty ? 5.6f : 5.2f;
                case ReloadKind.Shotgun: return empty ? 3.4f : 2.8f;
                default: return empty ? 2.7f : 2.1f;
            }
        }

        /// <summary>Taktik doldurmanın boşa oranı (CoD/Battlefield: ~0.75-0.8); sürgülü ve pompalıda 1'e yakın.</summary>
        public static float TacticalRatio(ReloadKind kind) => ReferenceSeconds(kind, false) / ReferenceSeconds(kind, true);

        public static ReloadPhasePlan Build(ReloadKind kind, bool empty, float totalSeconds)
        {
            var total = Math.Max(0.1f, totalSeconds);
            float prep, release, fetch, insert, seat, chamber;
            switch (kind)
            {
                case ReloadKind.Pistol:
                    prep = ViewmodelPoseTimeline.PistolMagOut; release = 0.30f; fetch = 0.50f;
                    insert = ViewmodelPoseTimeline.PistolMagIn; seat = 0.72f; chamber = empty ? 0.84f : seat;
                    break;
                case ReloadKind.BoltAction:
                    prep = 0.14f; release = 0.30f; fetch = 0.50f; insert = 0.66f; seat = 0.72f; chamber = 0.92f;
                    break;
                case ReloadKind.Machinegun:
                    prep = 0.22f; release = 0.46f; fetch = 0.60f; insert = 0.64f; seat = 0.84f; chamber = empty ? 0.96f : seat;
                    break;
                case ReloadKind.Shotgun:
                    prep = 0.08f; release = 0.08f; fetch = 0.10f; insert = ViewmodelPoseTimeline.ShellLoadEnd; seat = 0.90f; chamber = 0.98f;
                    break;
                default:
                    prep = ViewmodelPoseTimeline.RifleMagOut; release = 0.30f; fetch = 0.52f;
                    insert = ViewmodelPoseTimeline.RifleMagIn; seat = 0.80f; chamber = empty ? 0.94f : seat;
                    break;
            }

            return new ReloadPhasePlan(kind, empty, total, new[] { prep, release, fetch, insert, seat, chamber, 1f });
        }

        public float PhaseStart(ReloadPhase p) => p == ReloadPhase.Prep ? 0f : _ends[(int)p - 1];
        public float PhaseEnd(ReloadPhase p) => _ends[(int)p];
        public float PhaseSeconds(ReloadPhase p) => (PhaseEnd(p) - PhaseStart(p)) * TotalSeconds;
        public bool PhaseExists(ReloadPhase p) => PhaseEnd(p) - PhaseStart(p) > 1e-4f;

        /// <summary>Şarjörün yuvadan ayrılmaya başladığı an (Prep sonu): bu andan itibaren silah şarjörsüzdür.</summary>
        public float MagOutTime => _ends[(int)ReloadPhase.Prep];

        /// <summary>Yeni şarjörün tam oturduğu an (Insert sonu): mermi bu anda gerçekten silaha geçer.</summary>
        public float SeatTime => _ends[(int)ReloadPhase.Insert];

        /// <summary>Boş doldurmada sürgü/kızak bırakma başlangıcı; taktikte Seat sonu ile aynı.</summary>
        public float ChamberStart => _ends[(int)ReloadPhase.Seat];

        public float RecoverStart => _ends[(int)ReloadPhase.Chamber];

        /// <summary>Kurma evresi var mı (boş doldurma, sürgülü, pompalı).</summary>
        public bool HasChamberPhase => PhaseExists(ReloadPhase.Chamber);

        public ReloadPhase PhaseAt(float t)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            for (var i = 0; i < PhaseCount; i++)
                if (t < _ends[i] && _ends[i] - (i == 0 ? 0f : _ends[i - 1]) > 1e-4f)
                    return (ReloadPhase)i;
            return ReloadPhase.Recover;
        }

        /// <summary>Evre içi ilerleme (0..1).</summary>
        public float PhaseProgress(float t)
        {
            var p = PhaseAt(t);
            var s = PhaseStart(p);
            var span = PhaseEnd(p) - s;
            return span <= 1e-6f ? 1f : Math.Max(0f, Math.Min(1f, (t - s) / span));
        }

        /// <summary>Evre geçişi için gereken zaman: ses ipucu zamanı (sn) = sınır * toplam süre.</summary>
        public float SecondsAt(float t) => t * TotalSeconds;

        /// <summary>Eylem sesi için ipucu zamanı (0..1): şarjör bırakma tık sesi, şarjör oturma, kurma darbesi.</summary>
        public float CueTime(ReloadCue cue)
        {
            switch (cue)
            {
                case ReloadCue.MagRelease: return MagOutTime;
                case ReloadCue.MagOut: return Math.Min(_ends[(int)ReloadPhase.MagRelease], MagOutTime + 0.08f);
                case ReloadCue.MagIn: return SeatTime;
                case ReloadCue.Slap: return Math.Min(ChamberStart, SeatTime + 0.03f);
                case ReloadCue.BoltRelease: return HasChamberPhase ? PhaseStart(ReloadPhase.Chamber) + (PhaseEnd(ReloadPhase.Chamber) - PhaseStart(ReloadPhase.Chamber)) * 0.35f : -1f;
                default: return -1f;
            }
        }
    }

    public enum ReloadCue { MagRelease, MagOut, MagIn, Slap, BoltRelease }

    /// <summary>Kare başına evre geçişlerini tespit eder (atlanan evreler dahil); olaylar ses/animator için.</summary>
    public sealed class ReloadPhaseTracker
    {
        private readonly ReloadPhasePlan _plan;
        private float _last = -1f;
        private int _lastPhase = -1;

        public ReloadPhaseTracker(ReloadPhasePlan plan)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        }

        public ReloadPhase Current => _lastPhase < 0 ? ReloadPhase.Prep : (ReloadPhase)_lastPhase;

        /// <summary>Yeni t'ye ilerler; girilen her yeni evre için callback çağırır. Dönüş: girilen evre sayısı.</summary>
        public int Advance(float t, Action<ReloadPhase> onEnter, Action<ReloadCue> onCue = null)
        {
            var entered = 0;
            if (t < _last) { _last = -1f; _lastPhase = -1; }
            var prev = _last;
            if (onCue != null)
            {
                foreach (ReloadCue cue in Enum.GetValues(typeof(ReloadCue)))
                {
                    var ct = _plan.CueTime(cue);
                    if (ct >= 0f && ct > prev && ct <= t && !(prev < 0f && ct <= 0f)) onCue(cue);
                }
            }

            for (var i = Math.Max(0, _lastPhase + 1); i < ReloadPhasePlan.PhaseCount; i++)
            {
                var p = (ReloadPhase)i;
                if (!_plan.PhaseExists(p)) continue;
                if (t >= _plan.PhaseStart(p))
                {
                    _lastPhase = i; entered++;
                    onEnter?.Invoke(p);
                }
                else break;
            }

            _last = t;
            return entered;
        }
    }
}
