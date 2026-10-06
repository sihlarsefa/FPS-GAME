using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Infrastructure.Pooling
{
    /// <summary>
    /// Prefab/fabrika tabanlı Component havuzu. Pasif nesneler gizli bir kök altında bekler;
    /// Get() etkinleştirir, Release() pasifleştirip köke geri alır. Kök tembel oluşturulur.
    /// </summary>
    public sealed class ComponentPool<T> where T : Component
    {
        private readonly ObjectPool<T> _pool;
        private readonly string _name;
        private readonly Func<T> _factory;
        private Transform _root;

        public ComponentPool(string name, Func<T> factory, int maxSize = 128)
        {
            _name = string.IsNullOrEmpty(name) ? typeof(T).Name : name;
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _pool = new ObjectPool<T>(Create, OnGet, OnRelease, OnDestroy, maxSize);
        }

        public static ComponentPool<T> FromPrefab(T prefab, int maxSize = 128)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            return new ComponentPool<T>(prefab.name + "Pool", () => Object.Instantiate(prefab), maxSize);
        }

        public PoolStats Stats => _pool.Stats;

        /// <summary>Verilen nesne havuzdan çıkar, etkin ve (isteğe bağlı) konumlanmış olur.</summary>
        public T Get(Vector3 position, Quaternion rotation)
        {
            var item = GetClean();
            if (item == null) return null;
            var t = item.transform;
            t.SetPositionAndRotation(position, rotation);
            return item;
        }

        public T Get() => GetClean();

        public bool Release(T item) => _pool.Release(item);

        public int Prewarm(int count) => _pool.Prewarm(count);

        public void Trim(int keep) => _pool.Trim(keep);

        public void Clear()
        {
            _pool.Clear();
            if (_root != null) { Object.Destroy(_root.gameObject); _root = null; }
        }

        private T GetClean()
        {
            // Dışarıdan yok edilmiş (Unity null) nesneleri atla; sınırlı deneme, sonsuz döngü yok.
            for (var i = 0; i < 4; i++)
            {
                var item = _pool.Get();
                if (item != null) return item;
                _pool.Forget();
            }
            return null;
        }

        private Transform Root()
        {
            if (_root == null)
            {
                var go = new GameObject("[Havuz] " + _name);
                go.SetActive(true);
                Object.DontDestroyOnLoad(go);
                _root = go.transform;
            }
            return _root;
        }

        private T Create()
        {
            var item = _factory();
            if (item != null) item.transform.SetParent(Root(), false);
            return item;
        }

        private static void OnGet(T item)
        {
            if (item != null) item.gameObject.SetActive(true);
        }

        private void OnRelease(T item)
        {
            if (item == null) return;
            item.gameObject.SetActive(false);
            item.transform.SetParent(Root(), false);
        }

        private static void OnDestroy(T item)
        {
            if (item != null) Object.Destroy(item.gameObject);
        }
    }
}
