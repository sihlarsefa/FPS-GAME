namespace Project.Core.Interfaces
{
    public interface IServiceProvider
    {
        T Resolve<T>() where T : class;
        bool TryResolve<T>(out T service) where T : class;
    }
}
