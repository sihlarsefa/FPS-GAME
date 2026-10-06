using System;

namespace Project.Infrastructure.Pooling
{
    /// <summary>Sabit kapasiteli dairesel tampon; dolunca en eskiyi ezer. Ekleme/okuma ayırma yapmaz.</summary>
    public sealed class RingBuffer<T>
    {
        private readonly T[] _items;
        private int _head; // sonraki yazım indeksi
        private int _count;

        public RingBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _items = new T[capacity];
        }

        public int Capacity => _items.Length;
        public int Count => _count;
        public bool IsFull => _count == _items.Length;

        /// <summary>0 = en eski, Count-1 = en yeni.</summary>
        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
                var start = _count == _items.Length ? _head : 0;
                return _items[(start + index) % _items.Length];
            }
        }

        public T Newest => _count == 0 ? default : _items[(_head - 1 + _items.Length) % _items.Length];

        /// <summary>Ekler; tampon doluysa ezilen en eski öğeyi out ile verir.</summary>
        public bool Add(T item, out T evicted)
        {
            var full = _count == _items.Length;
            evicted = full ? _items[_head] : default;
            _items[_head] = item;
            _head = (_head + 1) % _items.Length;
            if (!full) _count++;
            return full;
        }

        public void Add(T item) { Add(item, out _); }

        public void Clear()
        {
            Array.Clear(_items, 0, _items.Length);
            _head = 0; _count = 0;
        }

        /// <summary>Eskiden yeniye sırayla hedef diziye kopyalar; kopyalanan sayıyı döner.</summary>
        public int CopyTo(T[] dest)
        {
            if (dest == null) throw new ArgumentNullException(nameof(dest));
            var n = Math.Min(dest.Length, _count);
            for (var i = 0; i < n; i++) dest[i] = this[i];
            return n;
        }
    }
}
