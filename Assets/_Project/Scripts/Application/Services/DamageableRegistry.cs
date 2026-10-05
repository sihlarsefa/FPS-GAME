using System.Collections.Generic;
using Project.Core.Domain;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>
    /// Hasar alabilen hedeflerin (savaşanlar, eğitim hedefleri) PlayerId → IDamageable kaydı.
    /// CombatService isabetleri buradan çözer. Geçersiz kimlikli ya da null kayıtlar yok sayılır.
    /// Aynı kimlikle yeniden kayıt eskisinin yerini alır. Liste tabanlıdır: <see cref="Count"/> ve indeksleyici ile
    /// GC üretmeden yinelenebilir (her karede tarama yapan sistemler için). Silme sırası korumaz (sona taşıyarak siler).
    /// </summary>
    public sealed class DamageableRegistry : IDamageableRegistry
    {
        private readonly List<IDamageable> _items = new();
        private readonly List<int> _keys = new();             // _items ile paralel: kayıt anındaki kimlik
        private readonly Dictionary<int, int> _indexById = new();

        /// <summary>Kayıtlı tüm hedefler (canlı görünüm; yineleme sırasında kayıt değiştirmeyin).</summary>
        public IReadOnlyCollection<IDamageable> All => _items;

        /// <summary>Kayıtlı tüm hedefler, indeksle erişim için (for döngüsüyle GC'siz yineleme).</summary>
        public IReadOnlyList<IDamageable> Items => _items;

        public int Count => _items.Count;

        public IDamageable this[int index] => _items[index];

        public void Register(IDamageable damageable)
        {
            if (damageable == null || !damageable.OwnerId.IsValid)
                return;

            var key = damageable.OwnerId.Value;
            if (_indexById.TryGetValue(key, out var index))
            {
                _items[index] = damageable;
                return;
            }

            _indexById.Add(key, _items.Count);
            _items.Add(damageable);
            _keys.Add(key);
        }

        public void Unregister(IDamageable damageable)
        {
            if (damageable == null || !damageable.OwnerId.IsValid)
                return;

            var key = damageable.OwnerId.Value;
            if (!_indexById.TryGetValue(key, out var index))
                return;

            // Aynı kimlikle yeniden kayıt olmuş farklı bir nesneyi silme (ör. yeniden doğan eğitim hedefi).
            if (!ReferenceEquals(_items[index], damageable))
                return;

            RemoveAt(key, index);
        }

        /// <summary>Kimliğe göre kaydı siler (nesne referansı bilinmiyorsa).</summary>
        public bool Unregister(PlayerId id)
        {
            if (!id.IsValid || !_indexById.TryGetValue(id.Value, out var index))
                return false;

            RemoveAt(id.Value, index);
            return true;
        }

        public bool TryGet(PlayerId id, out IDamageable damageable)
        {
            if (id.IsValid && _indexById.TryGetValue(id.Value, out var index))
            {
                damageable = _items[index];
                return true;
            }

            damageable = null;
            return false;
        }

        public bool Contains(PlayerId id) => id.IsValid && _indexById.ContainsKey(id.Value);

        /// <summary>Kayıtlı hedefleri verilen listeye ekler (çağıran temizler; yineleme sırasında değişiklik için güvenli kopya).</summary>
        public void CopyTo(List<IDamageable> output)
        {
            if (output != null)
                output.AddRange(_items);
        }

        public void Clear()
        {
            _items.Clear();
            _keys.Clear();
            _indexById.Clear();
        }

        private void RemoveAt(int key, int index)
        {
            // Kayıt anındaki kimlik kullanılır: nesnenin OwnerId'si sonradan değişse de dizin tutarlı kalır.
            var last = _items.Count - 1;
            if (index != last)
            {
                var movedKey = _keys[last];
                _items[index] = _items[last];
                _keys[index] = movedKey;
                _indexById[movedKey] = index;
            }

            _items.RemoveAt(last);
            _keys.RemoveAt(last);
            _indexById.Remove(key);
        }
    }
}
