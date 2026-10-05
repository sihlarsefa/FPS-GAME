namespace Project.Core.Interfaces
{
    public interface IEventBus
    {
        void Publish<TEvent>(TEvent gameEvent) where TEvent : Events.IGameEvent;
        void Subscribe<TEvent>(System.Action<TEvent> handler) where TEvent : Events.IGameEvent;
        void Unsubscribe<TEvent>(System.Action<TEvent> handler) where TEvent : Events.IGameEvent;
    }
}
