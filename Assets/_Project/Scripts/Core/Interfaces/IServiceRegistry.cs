namespace Project.Core.Interfaces
{
    public interface IServiceRegistry
    {
        void RegisterSingleton<TInterface, TImplementation>()
            where TInterface : class
            where TImplementation : class, TInterface, new();

        void RegisterSingleton<TInterface>(TInterface instance) where TInterface : class;
        void RegisterTransient<TInterface, TImplementation>()
            where TInterface : class
            where TImplementation : class, TInterface, new();
    }
}
