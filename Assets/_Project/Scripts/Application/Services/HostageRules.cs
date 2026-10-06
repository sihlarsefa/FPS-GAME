using System;

namespace Project.Application.Services
{
    /// <summary>Bir rehinenin durumu.</summary>
    public enum HostageState
    {
        Captive = 0,
        Following = 1,
        Holding = 2,
        Extracted = 3,
        Dead = 4
    }

    /// <summary>Rehine Kurtarma sonucu.</summary>
    public enum HostageOutcome
    {
        InProgress = 0,
        Success = 1,
        HostageKilled = 2,
        TimeUp = 3,
        SquadLost = 4
    }

    /// <summary>
    /// "Rehine Kurtarma" kuralları (saf mantık, Unity'ye bağımlı değil): rehine durumları, 8 dk süre sınırı,
    /// rehine ölümü = başarısızlık, tüm rehineler tahliyeye ulaşınca başarı. Yeniden doğuş yoktur.
    /// </summary>
    public sealed class HostageRules
    {
        public const int DefaultHostageCount = 2;
        public const float DefaultDurationSeconds = 480f;
        public const float InteractRange = 2.6f;
        public const float ExtractionRadius = 16f;

        private readonly HostageState[] _states;

        public HostageRules(int hostageCount = DefaultHostageCount, float durationSeconds = DefaultDurationSeconds)
        {
            _states = new HostageState[Math.Max(1, hostageCount)];
            DurationSeconds = Math.Max(1f, durationSeconds);
        }

        public int HostageCount => _states.Length;
        public float DurationSeconds { get; }
        public float Elapsed { get; private set; }
        public HostageOutcome Outcome { get; private set; }
        public bool IsOver => Outcome != HostageOutcome.InProgress;
        public bool IsSuccess => Outcome == HostageOutcome.Success;
        public float TimeRemaining => Math.Max(0f, DurationSeconds - Elapsed);

        public HostageState GetState(int index) => InRange(index) ? _states[index] : HostageState.Dead;

        public int CountIn(HostageState state)
        {
            var n = 0;
            for (var i = 0; i < _states.Length; i++)
                if (_states[i] == state)
                    n++;
            return n;
        }

        /// <summary>Henüz bulunmuş (serbest bırakılmış) rehine sayısı (ölü/tahliye edilmiş dahil değil).</summary>
        public int FreedCount => CountIn(HostageState.Following) + CountIn(HostageState.Holding);

        /// <summary>İlk kez serbest bırakılan rehine oldu mu (helikopter çağrısı için)?</summary>
        public bool AnyFreed => FreedCount > 0 || CountIn(HostageState.Extracted) > 0;

        /// <summary>Süreyi ilerletir; süre dolarsa başarısızlık.</summary>
        public void Tick(float deltaTime)
        {
            if (IsOver || deltaTime <= 0f)
                return;

            Elapsed += deltaTime;
            if (Elapsed >= DurationSeconds)
                Finish(HostageOutcome.TimeUp);
        }

        /// <summary>Oyuncu rehineyle etkileşti (F): esir → takip; takip ↔ bekle. Yeni durumu döndürür.</summary>
        public HostageState Interact(int index)
        {
            if (IsOver || !InRange(index))
                return GetState(index);

            switch (_states[index])
            {
                case HostageState.Captive:
                case HostageState.Holding:
                    _states[index] = HostageState.Following;
                    break;
                case HostageState.Following:
                    _states[index] = HostageState.Holding;
                    break;
            }

            return _states[index];
        }

        /// <summary>Rehine öldü: görev başarısız.</summary>
        public void MarkDead(int index)
        {
            if (IsOver || !InRange(index) || _states[index] == HostageState.Dead || _states[index] == HostageState.Extracted)
                return;

            _states[index] = HostageState.Dead;
            Finish(HostageOutcome.HostageKilled);
        }

        /// <summary>Rehine tahliye aracına ulaştı. Hepsi tahliye olunca başarı.</summary>
        public void MarkExtracted(int index)
        {
            if (IsOver || !InRange(index))
                return;

            if (_states[index] != HostageState.Following && _states[index] != HostageState.Holding)
                return;

            _states[index] = HostageState.Extracted;
            if (CountIn(HostageState.Extracted) == _states.Length)
                Finish(HostageOutcome.Success);
        }

        /// <summary>Saldıran tim tamamen düştü (yeniden doğuş yok).</summary>
        public void SquadLost()
        {
            if (!IsOver)
                Finish(HostageOutcome.SquadLost);
        }

        /// <summary>Rehine tahliye bölgesinde (yatay mesafe) ve araç inmiş mi?</summary>
        public static bool CanExtract(float distanceToLandingZone, bool vehicleLanded) =>
            vehicleLanded && distanceToLandingZone <= ExtractionRadius;

        /// <summary>Oyuncu rehineyle etkileşebilecek kadar yakın mı?</summary>
        public static bool InInteractRange(float distance) => distance <= InteractRange;

        public static string OutcomeText(HostageOutcome outcome)
        {
            switch (outcome)
            {
                case HostageOutcome.Success: return "REHİNELER KURTARILDI";
                case HostageOutcome.HostageKilled: return "REHİNE ŞEHİT OLDU — GÖREV BAŞARISIZ";
                case HostageOutcome.TimeUp: return "SÜRE DOLDU — GÖREV BAŞARISIZ";
                case HostageOutcome.SquadLost: return "TİM DÜŞTÜ — GÖREV BAŞARISIZ";
                default: return "GÖREV SÜRÜYOR";
            }
        }

        private void Finish(HostageOutcome outcome)
        {
            if (IsOver)
                return;
            Outcome = outcome;
        }

        private bool InRange(int index) => index >= 0 && index < _states.Length;
    }
}
