using System;

namespace Project.Presentation.UI.Play
{
    public enum MatchPhase { Idle, Searching, ReadyCheck, Starting, Cancelled }

    public enum CancelReason { None, UserCancelled, ReadyTimeout, ReadyDeclined }

    /// <summary>
    /// Kaçırma (dodge) cezası: hazır kontrolünde reddeden/zaman aşımına uğrayan oyuncu bekletilir.
    /// Valorant/LoL benzeri artan ceza: 0, 30, 120, 300 sn (son 15 dk içindeki ihlal sayısına göre).
    /// </summary>
    public sealed class DodgePenalty
    {
        private static readonly float[] Steps = { 0f, 30f, 120f, 300f };
        public const float MemorySeconds = 900f;

        private float _lastOffence = -9999f;
        private int _count;
        private float _until;

        public int Offences => _count;

        /// <summary>İhlal kaydeder ve uygulanan ceza süresini (sn) döndürür.</summary>
        public float Register(float now)
        {
            if (now - _lastOffence > MemorySeconds) _count = 0;
            _lastOffence = now;
            var step = Steps[Math.Min(_count, Steps.Length - 1)];
            _count++;
            _until = now + step;
            return step;
        }

        public float Remaining(float now) => Math.Max(0f, _until - now);

        public bool IsBlocked(float now) => Remaining(now) > 0f;
    }

    /// <summary>
    /// Eşleşme akışı durum makinesi: Arama -> Hazır kontrolü -> Başlatma. Saf mantık, Unity'siz test edilir.
    /// Hazır kontrolü: 20 sn, takım arkadaşları rastgele anlarda kabul eder; biri reddederse oyuncu
    /// kabulü korunarak kuyruğa öncelikli (kısa) dönülür (Warzone/LoL "geri doldurma" davranışı).
    /// </summary>
    public sealed class MatchmakingFlow
    {
        public const float ReadySeconds = 20f;
        public const float StartCountdown = 3f;
        /// <summary>Takım arkadaşının reddetme olasılığı.</summary>
        public const float DeclineChance = 0.04f;
        /// <summary>Yeniden kuyruğa dönüşte bekleme çarpanı (öncelik).</summary>
        public const float RequeueFactor = 0.35f;

        private readonly QueueEstimator _estimator;
        private readonly PlayQueueInfo _info;
        private readonly int _hour;
        private readonly Random _rng;
        private float[] _acceptAt;
        private bool[] _willDecline;
        private float _foundAt;

        public MatchPhase Phase { get; private set; } = MatchPhase.Idle;
        public CancelReason Reason { get; private set; }
        public float SearchElapsed { get; private set; }
        public float ReadyElapsed { get; private set; }
        public float StartElapsed { get; private set; }
        public bool PlayerAccepted { get; private set; }
        public int Requeues { get; private set; }
        public PlayQueueKind Kind => _info.Kind;
        public int SquadSize => _info.SquadSize;

        public event Action<MatchPhase> PhaseChanged;
        /// <summary>Geri sayım bitti: oyun başlatılmalı.</summary>
        public event Action LaunchRequested;

        public MatchmakingFlow(QueueEstimator estimator, PlayQueueKind kind, int hour, int seed)
        {
            _estimator = estimator ?? new QueueEstimator();
            _info = PlayQueueModes.Get(kind);
            _hour = hour;
            _rng = new Random(seed);
        }

        public bool Active => Phase == MatchPhase.Searching || Phase == MatchPhase.ReadyCheck || Phase == MatchPhase.Starting;

        public float ReadyRemaining => Math.Max(0f, ReadySeconds - ReadyElapsed);

        public float StartRemaining => Math.Max(0f, StartCountdown - StartElapsed);

        /// <summary>Tahmini toplam süre (arama çubuğu için).</summary>
        public float Estimate => _estimator.Estimate(_info.Kind, _hour);

        /// <summary>Hazır olan oyuncu sayısı (kendisi dahil).</summary>
        public int ReadyCount
        {
            get
            {
                if (Phase != MatchPhase.ReadyCheck && Phase != MatchPhase.Starting) return 0;
                var n = PlayerAccepted ? 1 : 0;
                for (var i = 0; _acceptAt != null && i < _acceptAt.Length; i++)
                    if (!_willDecline[i] && ReadyElapsed >= _acceptAt[i]) n++;
                if (Phase == MatchPhase.Starting) return _info.SquadSize;
                return n;
            }
        }

        /// <summary>Slot i hazır mı (0 = oyuncu).</summary>
        public bool SlotReady(int slot)
        {
            if (slot == 0) return PlayerAccepted;
            if (_acceptAt == null || slot - 1 >= _acceptAt.Length) return false;
            return Phase == MatchPhase.Starting || (!_willDecline[slot - 1] && ReadyElapsed >= _acceptAt[slot - 1]);
        }

        public void Start()
        {
            if (!_info.UsesQueue)
            {
                SetPhase(MatchPhase.Idle);
                LaunchRequested?.Invoke();
                return;
            }

            Requeues = 0;
            PlayerAccepted = false;
            BeginSearch(1f);
        }

        private void BeginSearch(float factor)
        {
            SearchElapsed = 0f;
            _foundAt = _estimator.SampleWait(_info.Kind, _hour, (float)_rng.NextDouble()) * factor;
            SetPhase(MatchPhase.Searching);
        }

        private void BeginReadyCheck()
        {
            ReadyElapsed = 0f;
            var others = Math.Max(0, _info.SquadSize - 1);
            _acceptAt = new float[others];
            _willDecline = new bool[others];
            for (var i = 0; i < others; i++)
            {
                _acceptAt[i] = 0.8f + (float)_rng.NextDouble() * 9f;
                _willDecline[i] = Requeues < 2 && _rng.NextDouble() < DeclineChance;
            }
            SetPhase(MatchPhase.ReadyCheck);
        }

        /// <summary>Oyuncu hazırım der (hazır kontrolünde).</summary>
        public bool Accept()
        {
            if (Phase != MatchPhase.ReadyCheck || PlayerAccepted) return false;
            PlayerAccepted = true;
            return true;
        }

        /// <summary>Oyuncu iptal/ret eder. Kuyrukta bedelsiz, hazır kontrolünde ceza doğurur.</summary>
        public void Cancel()
        {
            if (Phase == MatchPhase.Searching) Finish(CancelReason.UserCancelled);
            else if (Phase == MatchPhase.ReadyCheck) Finish(CancelReason.ReadyDeclined);
        }

        private void Finish(CancelReason reason)
        {
            Reason = reason;
            SetPhase(MatchPhase.Cancelled);
        }

        private void SetPhase(MatchPhase phase)
        {
            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            switch (Phase)
            {
                case MatchPhase.Searching:
                    SearchElapsed += dt;
                    if (SearchElapsed >= _foundAt) BeginReadyCheck();
                    break;
                case MatchPhase.ReadyCheck:
                    ReadyElapsed += dt;
                    for (var i = 0; i < _willDecline.Length; i++)
                    {
                        if (_willDecline[i] && ReadyElapsed >= _acceptAt[i])
                        {
                            Requeues++;
                            BeginSearch(RequeueFactor);
                            return;
                        }
                    }
                    if (PlayerAccepted && AllOthersReady())
                    {
                        StartElapsed = 0f;
                        SetPhase(MatchPhase.Starting);
                    }
                    else if (ReadyElapsed >= ReadySeconds)
                    {
                        if (PlayerAccepted) { Requeues++; BeginSearch(RequeueFactor); }
                        else Finish(CancelReason.ReadyTimeout);
                    }
                    break;
                case MatchPhase.Starting:
                    StartElapsed += dt;
                    if (StartElapsed >= StartCountdown)
                    {
                        _estimator.Observe(_info.Kind, SearchElapsed);
                        SetPhase(MatchPhase.Idle);
                        LaunchRequested?.Invoke();
                    }
                    break;
            }
        }

        private bool AllOthersReady()
        {
            for (var i = 0; i < _acceptAt.Length; i++)
                if (ReadyElapsed < _acceptAt[i]) return false;
            return true;
        }
    }
}
