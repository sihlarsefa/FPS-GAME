using System.Collections.Generic;

namespace Project.Infrastructure.Audio
{
    public enum RadioPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// Telsiz spam engelleyici: tek hat (aktif replik bitmeden daha düşük/eşit öncelik kabul edilmez), kategori
    /// başına bekleme süresi ve düşük öncelik için son konuşmadan sonra sessizlik aralığı. Saf mantık.
    /// </summary>
    public sealed class RadioScheduler
    {
        public const float LowQuietGap = 4f;
        public const float NormalQuietGap = 1f;

        private readonly Dictionary<string, float> _categoryReady = new Dictionary<string, float>();
        private float _activeUntil = -999f;
        private float _lastEnd = -999f;
        private RadioPriority _activePriority;

        public bool TryAccept(float now, RadioPriority priority, string category, float categoryCooldown, float lineSeconds)
        {
            if (!string.IsNullOrEmpty(category) && _categoryReady.TryGetValue(category, out var ready) && now < ready)
                return false;

            if (now < _activeUntil)
            {
                // Hat meşgul: yalnızca kesinlikle daha yüksek öncelik araya girer.
                if (priority <= _activePriority)
                    return false;
            }
            else
            {
                var gap = priority == RadioPriority.Low ? LowQuietGap : priority == RadioPriority.Normal ? NormalQuietGap : 0f;
                if (now < _lastEnd + gap)
                    return false;
            }

            _activePriority = priority;
            _activeUntil = now + lineSeconds;
            _lastEnd = _activeUntil;
            if (!string.IsNullOrEmpty(category))
                _categoryReady[category] = now + categoryCooldown;
            return true;
        }

        public void Reset()
        {
            _categoryReady.Clear();
            _activeUntil = -999f;
            _lastEnd = -999f;
            _activePriority = RadioPriority.Low;
        }
    }
}
