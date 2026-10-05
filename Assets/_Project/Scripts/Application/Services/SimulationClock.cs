using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>
    /// Sabit tick saati (varsayılan 30 Hz). Advance(dt) biriken süreye göre bu karede çalıştırılacak tick sayısını
    /// döndürür (spiral-of-death'e karşı en fazla 5). Ağ senkronunda sunucu ile istemci aynı tick'i paylaşır.
    /// </summary>
    public sealed class SimulationClock : ISimulationClock
    {
        private const int MaxTicksPerAdvance = 5;
        private float _accumulator;

        public SimulationClock(float tickRate = 30f)
        {
            TickInterval = 1f / (tickRate > 0f && !float.IsInfinity(tickRate) ? tickRate : 30f);
        }

        public uint CurrentTick { get; private set; }
        public float TickInterval { get; }
        public float ElapsedSeconds { get; private set; }

        /// <summary>Kareler arası enterpolasyon oranı (0..1).</summary>
        public float Alpha => _accumulator >= TickInterval ? 1f : _accumulator / TickInterval;

        public int Advance(float deltaTime)
        {
            if (!(deltaTime > 0f) || float.IsInfinity(deltaTime))
                return 0;

            _accumulator += deltaTime;
            var ticks = 0;
            while (_accumulator >= TickInterval && ticks < MaxTicksPerAdvance)
            {
                _accumulator -= TickInterval;
                CurrentTick++;
                ElapsedSeconds += TickInterval;
                ticks++;
            }

            if (ticks == MaxTicksPerAdvance && _accumulator > TickInterval)
                _accumulator = 0f;

            return ticks;
        }

        /// <summary>Saati sıfırlar (yeni maç).</summary>
        public void Reset()
        {
            _accumulator = 0f;
            CurrentTick = 0;
            ElapsedSeconds = 0f;
        }
    }
}
