using Project.Application.Services;
using Project.Core.Interfaces;
using Project.Infrastructure;
using UnityEngine;

namespace Project.Presentation.Bootstrap
{
    /// <summary>
    /// Sabit tick döngüsü: her karede <see cref="SimulationClock.Advance"/> ile biriken süreyi tick'lere böler ve her tick'te
    /// <see cref="IGameTickService"/>'i (GameTickCoordinator: maç → bölge → topçu) sabit adımla ilerletir.
    /// Oyun duraklatıldığında (Time.timeScale = 0) simülasyon da durur. Yalnızca otoritede (çevrimdışı: her zaman) çalışır.
    /// Servisler <see cref="Initialize"/> ile verilmezse GameContext'ten çözülür.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameLoopPresenter : MonoBehaviour
    {
        /// <summary>Tek karede işlenecek en uzun süre (takılmalarda simülasyon patlamasın).</summary>
        private const float MaxFrameDelta = 0.25f;

        private SimulationClock _clock;
        private IGameTickService _tick;
        private IMatchService _match;
        private bool _resolved;

        /// <summary>true iken simülasyon ilerlemez (ör. maç sonu ekranı).</summary>
        public bool Paused { get; set; }

        /// <summary>Simülasyon hızı çarpanı (hata ayıklama). 1 = gerçek zaman.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>Bu karede çalıştırılan tick sayısı.</summary>
        public int TicksThisFrame { get; private set; }

        /// <summary>Başlangıçtan beri toplam tick sayısı.</summary>
        public uint CurrentTick => _clock != null ? _clock.CurrentTick : 0u;

        public SimulationClock Clock => _clock;

        /// <summary>Servisleri açıkça bağlar (bootstrap çağırır).</summary>
        public void Initialize(SimulationClock clock, IGameTickService tick, IMatchService match = null)
        {
            _clock = clock;
            _tick = tick;
            _match = match;
            _resolved = _clock != null && _tick != null;
        }

        /// <summary>Eski API: maçı başlatır (Lobby → PreMatch).</summary>
        public void StartMatch()
        {
            ResolveIfNeeded();
            if (_match is MatchService service)
                service.Begin();
            else
                _match?.TransitionTo(Core.Domain.MatchPhase.PreMatch);
        }

        private void Start()
        {
            ResolveIfNeeded();
        }

        private void Update()
        {
            TicksThisFrame = 0;
            if (Paused)
                return;

            if (!_resolved && !ResolveIfNeeded())
                return;

            if (!GameContext.HasAuthority)
                return;

            var dt = Time.deltaTime;
            if (!(dt > 0f))
                return;

            if (dt > MaxFrameDelta)
                dt = MaxFrameDelta;

            if (SpeedMultiplier > 0f && !Mathf.Approximately(SpeedMultiplier, 1f))
                dt *= SpeedMultiplier;

            var ticks = _clock.Advance(dt);
            var step = _clock.TickInterval;
            for (var i = 0; i < ticks; i++)
            {
                try
                {
                    _tick.Tick(step);
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e, this);
                }
            }

            TicksThisFrame = ticks;
        }

        private bool ResolveIfNeeded()
        {
            if (_resolved)
                return true;

            if (!GameContext.IsReady)
                return false;

            if (_clock == null)
                GameContext.TryGet(out _clock);

            if (_tick == null)
            {
                if (GameContext.TryGet<GameTickCoordinator>(out var coordinator))
                    _tick = coordinator;
                else
                    GameContext.TryGet(out _tick);
            }

            if (_match == null)
                GameContext.TryGet(out _match);

            if (_clock == null && _tick != null)
                _clock = new SimulationClock();

            _resolved = _clock != null && _tick != null;
            return _resolved;
        }
    }
}
