using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.Audio
{
    /// <summary>Gecikmeli çalınacak tek bir akustik ses olayı.</summary>
    public struct AcousticEvent
    {
        public float DueTime;
        public SoundId Id;
        public Vector3 Position;
        public float Volume;
        public float Pitch;
        public float CutoffHz;
        public float MinDistance;
        public float MaxDistance;
    }

    /// <summary>Saf gecikmeli olay kuyruğu (sınırlı kapasite; dolunca en sessiz olay atılır).</summary>
    public sealed class AcousticQueue
    {
        private readonly List<AcousticEvent> _items;
        private readonly int _capacity;

        public AcousticQueue(int capacity = 48)
        {
            _capacity = capacity < 1 ? 1 : capacity;
            _items = new List<AcousticEvent>(_capacity);
        }

        public int Count => _items.Count;

        public bool Enqueue(AcousticEvent e)
        {
            if (!(e.Volume > 0.001f))
                return false;
            if (_items.Count >= _capacity)
            {
                var min = 0;
                for (var i = 1; i < _items.Count; i++)
                    if (_items[i].Volume < _items[min].Volume) min = i;
                if (_items[min].Volume >= e.Volume)
                    return false;
                _items.RemoveAt(min);
            }
            _items.Add(e);
            return true;
        }

        /// <summary>Vakti gelenleri (DueTime &lt;= now) çıkış listesine taşır; eski olaylar (2 sn+) atılır.</summary>
        public void PopDue(float now, List<AcousticEvent> output)
        {
            for (var i = _items.Count - 1; i >= 0; i--)
            {
                var e = _items[i];
                if (e.DueTime > now)
                    continue;
                _items.RemoveAt(i);
                if (now - e.DueTime <= 2f)
                    output.Add(e);
            }
            output.Sort((a, b) => a.DueTime.CompareTo(b.DueTime));
        }

        public void Clear() => _items.Clear();
    }

    /// <summary>Saniye başına ışın bütçesi (jeton kovası).</summary>
    public sealed class RayBudget
    {
        private readonly float _perSecond;
        private readonly float _max;
        private float _tokens;
        private float _last;
        private bool _init;

        public RayBudget(float perSecond, float burst)
        {
            _perSecond = perSecond;
            _max = burst;
            _tokens = burst;
        }

        public bool TryConsume(float now, int count)
        {
            if (!_init) { _init = true; _last = now; }
            _tokens = Mathf.Min(_max, _tokens + Mathf.Max(0f, now - _last) * _perSecond);
            _last = now;
            if (_tokens < count)
                return false;
            _tokens -= count;
            return true;
        }
    }
}
