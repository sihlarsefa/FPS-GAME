using System;

namespace Project.Core.Domain
{
    /// <summary>
    /// Çatışma (hızlı maç) kuralları: iki tim, öldürme skoru, takviye dalgası zamanlayıcısı ve bitiş kararı.
    /// Saf mantık — Unity'ye bağımlı değildir.
    /// </summary>
    public sealed class SkirmishRules
    {
        public const int TeamCount = 2;
        public const int DefaultKillTarget = 50;
        public const float DefaultDurationSeconds = 600f;
        public const float DefaultWaveIntervalSeconds = 20f;

        private readonly int[] _kills = new int[TeamCount];
        private float _waveTimer;

        public SkirmishRules(int killTarget = DefaultKillTarget, float durationSeconds = DefaultDurationSeconds,
            float waveIntervalSeconds = DefaultWaveIntervalSeconds)
        {
            KillTarget = Math.Max(1, killTarget);
            DurationSeconds = Math.Max(1f, durationSeconds);
            WaveIntervalSeconds = Math.Max(1f, waveIntervalSeconds);
            _waveTimer = WaveIntervalSeconds;
        }

        public int KillTarget { get; }
        public float DurationSeconds { get; }
        public float WaveIntervalSeconds { get; }
        public float Elapsed { get; private set; }
        public bool IsOver { get; private set; }

        /// <summary>Kazanan tim (-1 = berabere / henüz bitmedi).</summary>
        public int WinnerTeam { get; private set; } = -1;

        public float TimeRemaining => Math.Max(0f, DurationSeconds - Elapsed);
        public float SecondsToNextWave => _waveTimer;

        public int GetKills(int team) => team >= 0 && team < TeamCount ? _kills[team] : 0;

        /// <summary>Düşman öldürmesi ekler (takım içi/geçersiz tim yok sayılır). Hedefe ulaşan takım maçı kazanır.</summary>
        public void RegisterKill(int killerTeam, int victimTeam)
        {
            if (IsOver || killerTeam < 0 || killerTeam >= TeamCount || victimTeam < 0 || victimTeam >= TeamCount || killerTeam == victimTeam)
                return;

            _kills[killerTeam]++;
            if (_kills[killerTeam] >= KillTarget)
                Finish(killerTeam);
        }

        /// <summary>Zamanı ilerletir; takviye dalgası zamanı gelince true döner (dalga başına en fazla bir kez). Süre dolunca maç biter.</summary>
        public bool Tick(float deltaTime)
        {
            if (IsOver || !(deltaTime > 0f) || float.IsInfinity(deltaTime))
                return false;

            Elapsed += deltaTime;
            if (Elapsed >= DurationSeconds)
            {
                Finish(_kills[0] > _kills[1] ? 0 : _kills[1] > _kills[0] ? 1 : -1);
                return false;
            }

            _waveTimer -= deltaTime;
            if (_waveTimer > 0f)
                return false;

            _waveTimer += WaveIntervalSeconds;
            if (_waveTimer <= 0f)
                _waveTimer = WaveIntervalSeconds;
            return true;
        }

        /// <summary>Dış kural (ör. Konvoy Koruma) maçı bitirir; zaten bittiyse yok sayılır.</summary>
        public void ForceFinish(int winner)
        {
            if (!IsOver)
                Finish(winner);
        }

        private void Finish(int winner)
        {
            IsOver = true;
            WinnerTeam = winner;
        }
    }
}
