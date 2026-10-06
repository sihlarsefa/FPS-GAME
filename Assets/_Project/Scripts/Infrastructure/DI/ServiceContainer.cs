using System;
using System.Collections.Generic;
using Project.Core.Interfaces;
using IServiceProvider = Project.Core.Interfaces.IServiceProvider;

namespace Project.Infrastructure.DI
{
    /// <summary>Hafif DI konteyneri: aynı örnek birden çok arayüz tipiyle kaydedilebilir.</summary>
    public sealed class ServiceContainer : IServiceRegistry, IServiceProvider
    {
        private readonly Dictionary<Type, object> _singletons = new();
        private readonly Dictionary<Type, Type> _transients = new();

        public void RegisterSingleton<TInterface, TImplementation>()
            where TInterface : class
            where TImplementation : class, TInterface, new()
        {
            _singletons[typeof(TInterface)] = new TImplementation();
        }

        public void RegisterSingleton<TInterface>(TInterface instance) where TInterface : class
        {
            _singletons[typeof(TInterface)] = instance;
        }

        public void RegisterTransient<TInterface, TImplementation>()
            where TInterface : class
            where TImplementation : class, TInterface, new()
        {
            _transients[typeof(TInterface)] = typeof(TImplementation);
        }

        public T Resolve<T>() where T : class
        {
            if (TryResolve<T>(out var service))
                return service;

            throw new InvalidOperationException($"Service not registered: {typeof(T).Name}");
        }

        public bool TryResolve<T>(out T service) where T : class
        {
            var type = typeof(T);

            if (_singletons.TryGetValue(type, out var singleton))
            {
                service = (T)singleton;
                return true;
            }

            if (_transients.TryGetValue(type, out var implType))
            {
                service = (T)Activator.CreateInstance(implType);
                return true;
            }

            service = null;
            return false;
        }

        /// <summary>IDisposable kayıtlı tekilleri bir kez serbest bırakır (sahne kapanırken).</summary>
        public void DisposeAll()
        {
            var disposed = new HashSet<object>();
            foreach (var instance in _singletons.Values)
            {
                if (instance is IDisposable disposable && disposed.Add(instance))
                {
                    // Bir servisin hatası diğerlerinin serbest bırakılmasını (olay abonelikleri) engellemesin.
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogException(e);
                    }
                }
            }

            _singletons.Clear();
            _transients.Clear();
        }
    }
}
