using System;
using System.Collections.Generic;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Infrastructure.Events
{
    /// <summary>
    /// Tip güvenli pub/sub. Yayın sırasında abone ekleme/çıkarma güvenlidir (anlık görüntü üzerinde dolaşır).
    /// Bir dinleyicideki istisna diğer dinleyicileri durdurmaz.
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        private sealed class HandlerList
        {
            public readonly List<Delegate> Handlers = new();
            public Delegate[] Snapshot = Array.Empty<Delegate>();
            public bool Dirty;
        }

        private readonly Dictionary<Type, HandlerList> _handlers = new();

        public void Publish<TEvent>(TEvent gameEvent) where TEvent : IGameEvent
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list))
                return;

            if (list.Dirty)
            {
                list.Snapshot = list.Handlers.ToArray();
                list.Dirty = false;
            }

            var snapshot = list.Snapshot;
            for (var i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    ((Action<TEvent>)snapshot[i]).Invoke(gameEvent);
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception);
                }
            }
        }

        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent
        {
            if (handler == null)
                return;

            var type = typeof(TEvent);
            if (!_handlers.TryGetValue(type, out var list))
            {
                list = new HandlerList();
                _handlers[type] = list;
            }

            list.Handlers.Add(handler);
            list.Dirty = true;
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent
        {
            if (handler == null || !_handlers.TryGetValue(typeof(TEvent), out var list))
                return;

            if (list.Handlers.Remove(handler))
                list.Dirty = true;
        }

        public void Clear() => _handlers.Clear();
    }
}
