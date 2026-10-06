using System;
using System.Collections.Generic;

namespace Project.Infrastructure.Audio.Dialogue
{
    /// <summary>
    /// Küçük, saf (Unity'siz) LRU önbellek: en çok <see cref="Capacity"/> girdi tutar; taşınca en eski kullanılan atılır.
    /// Ses bankı (8 binden fazla klip) hiçbir zaman toplu yüklenmez: klipler ilk kullanımda tembel yüklenir ve bu sınırla
    /// bellekte küçük kalır (diyalog + telsiz önbellekleri ≤ <see cref="DialogueClipLibrary.CacheCapacity"/> klip).
    /// </summary>
    public sealed class LruCache<TKey, TValue>
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _map;
        // First = en son kullanılan, Last = en eski (atılacak).
        private readonly LinkedList<KeyValuePair<TKey, TValue>> _order = new LinkedList<KeyValuePair<TKey, TValue>>();

        public LruCache(int capacity, IEqualityComparer<TKey> comparer = null)
        {
            _capacity = Math.Max(1, capacity);
            _map = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>(_capacity + 1, comparer);
        }

        public int Capacity => _capacity;

        public int Count => _map.Count;

        /// <summary>Anahtar varsa değeri verir ve girdiyi "en yeni" yapar.</summary>
        public bool TryGet(TKey key, out TValue value)
        {
            if (_map.TryGetValue(key, out var node))
            {
                if (!ReferenceEquals(node, _order.First))
                {
                    _order.Remove(node);
                    _order.AddFirst(node);
                }

                value = node.Value.Value;
                return true;
            }

            value = default(TValue);
            return false;
        }

        /// <summary>Girdiyi sırasını değiştirmeden sorgular (test/teşhis için).</summary>
        public bool Contains(TKey key) => _map.ContainsKey(key);

        /// <summary>Ekler ya da değiştirir (her iki durumda "en yeni"); kapasite aşılırsa en eski girdiler atılır.</summary>
        public void Set(TKey key, TValue value)
        {
            if (_map.TryGetValue(key, out var existing))
            {
                _order.Remove(existing);
                _map.Remove(key);
            }

            _map[key] = _order.AddFirst(new KeyValuePair<TKey, TValue>(key, value));
            while (_map.Count > _capacity)
            {
                var oldest = _order.Last;
                _order.RemoveLast();
                _map.Remove(oldest.Value.Key);
            }
        }

        public bool Remove(TKey key)
        {
            if (!_map.TryGetValue(key, out var node))
                return false;
            _order.Remove(node);
            _map.Remove(key);
            return true;
        }

        public void Clear()
        {
            _map.Clear();
            _order.Clear();
        }
    }
}
