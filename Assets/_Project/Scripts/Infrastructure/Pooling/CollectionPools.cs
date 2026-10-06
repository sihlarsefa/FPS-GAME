using System;
using System.Collections.Generic;

namespace Project.Infrastructure.Pooling
{
    /// <summary>
    /// Geçici List/HashSet/Dictionary için statik havuzlar; Update içinde "new List" yerine kullanılır.
    /// Tek iş parçacığı (Unity ana iş parçacığı) varsayar. using ile otomatik iade:
    /// <c>using (ListPool&lt;int&gt;.Get(out var l)) { ... }</c>
    /// </summary>
    public static class ListPool<T>
    {
        private const int MaxPooled = 32;
        private const int MaxCapacityKept = 4096;
        private static readonly Stack<List<T>> Free = new Stack<List<T>>();
        private static readonly Action<List<T>> ReleaseAction = Release; // önbellekli delege: Get başına ayırma yok

        public static int PooledCount => Free.Count;

        public static List<T> Get() => Free.Count > 0 ? Free.Pop() : new List<T>(16);

        public static PooledObject<List<T>> Get(out List<T> list)
        {
            list = Get();
            return new PooledObject<List<T>>(list, ReleaseAction);
        }

        public static void Release(List<T> list)
        {
            if (list == null) return;
            list.Clear();
            // Dev büyüyen listeleri havuzda tutma; bellek şişmesini önler.
            if (list.Capacity > MaxCapacityKept || Free.Count >= MaxPooled) return;
            Free.Push(list);
        }
    }

    public static class HashSetPool<T>
    {
        private const int MaxPooled = 16;
        private static readonly Stack<HashSet<T>> Free = new Stack<HashSet<T>>();
        private static readonly Action<HashSet<T>> ReleaseAction = Release;

        public static int PooledCount => Free.Count;
        public static HashSet<T> Get() => Free.Count > 0 ? Free.Pop() : new HashSet<T>();

        public static PooledObject<HashSet<T>> Get(out HashSet<T> set)
        {
            set = Get();
            return new PooledObject<HashSet<T>>(set, ReleaseAction);
        }

        public static void Release(HashSet<T> set)
        {
            if (set == null) return;
            set.Clear();
            if (Free.Count >= MaxPooled) return;
            Free.Push(set);
        }
    }

    public static class DictionaryPool<TKey, TValue>
    {
        private const int MaxPooled = 16;
        private static readonly Stack<Dictionary<TKey, TValue>> Free = new Stack<Dictionary<TKey, TValue>>();
        private static readonly Action<Dictionary<TKey, TValue>> ReleaseAction = Release;

        public static int PooledCount => Free.Count;
        public static Dictionary<TKey, TValue> Get() => Free.Count > 0 ? Free.Pop() : new Dictionary<TKey, TValue>();

        public static PooledObject<Dictionary<TKey, TValue>> Get(out Dictionary<TKey, TValue> dict)
        {
            dict = Get();
            return new PooledObject<Dictionary<TKey, TValue>>(dict, ReleaseAction);
        }

        public static void Release(Dictionary<TKey, TValue> dict)
        {
            if (dict == null) return;
            dict.Clear();
            if (Free.Count >= MaxPooled) return;
            Free.Push(dict);
        }
    }

    /// <summary>using bloğu çıkışında iade eden değer-tipi sarmalayıcı (ayırma yapmaz).</summary>
    public readonly struct PooledObject<T> : IDisposable where T : class
    {
        private readonly T _value;
        private readonly Action<T> _release;

        public PooledObject(T value, Action<T> release) { _value = value; _release = release; }
        public T Value => _value;
        public void Dispose() { _release?.Invoke(_value); }
    }
}
