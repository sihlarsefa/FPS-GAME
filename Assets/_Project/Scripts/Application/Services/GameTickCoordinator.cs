using System.Collections.Generic;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>Sabit tick'te çalışan simülasyon servislerini sırayla ilerletir.</summary>
    public sealed class GameTickCoordinator : IGameTickService
    {
        private readonly List<IGameTickService> _tickables = new();

        public GameTickCoordinator(params IGameTickService[] tickables)
        {
            if (tickables != null)
                _tickables.AddRange(tickables);
        }

        public void Add(IGameTickService tickable)
        {
            if (tickable != null && !_tickables.Contains(tickable))
                _tickables.Add(tickable);
        }

        public void Remove(IGameTickService tickable) => _tickables.Remove(tickable);

        public void Tick(float deltaTime)
        {
            for (var i = 0; i < _tickables.Count; i++)
                _tickables[i].Tick(deltaTime);
        }
    }
}
