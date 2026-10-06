using System;
using System.Collections.Generic;
using Project.Application.Replay;

namespace Project.Presentation.Replay
{
    /// <summary>
    /// Tekrar verisinde can kaydı yok; Hit olaylarındaki hasardan 0..1 tahmini can çıkarır (iyileşme bilinmez).
    /// Ölüm olayı sonrası 0. Saf mantık, Unity'den bağımsız.
    /// </summary>
    public sealed class ReplayHealthEstimator
    {
        private readonly Dictionary<int, List<float>> _times = new Dictionary<int, List<float>>();
        private readonly Dictionary<int, List<float>> _cumulative = new Dictionary<int, List<float>>();
        private readonly Dictionary<int, float> _deathTime = new Dictionary<int, float>();
        private readonly float _maxHealth;

        public ReplayHealthEstimator(IList<ReplayEvent> events, float maxHealth = 100f)
        {
            _maxHealth = maxHealth > 1f ? maxHealth : 100f;
            if (events == null)
                return;
            for (var i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.Target < 0)
                    continue;
                if (e.Type == ReplayEventType.Death)
                {
                    if (!_deathTime.ContainsKey(e.Target))
                        _deathTime[e.Target] = e.Time;
                }
                else if (e.Type == ReplayEventType.Hit && e.Value > 0f)
                {
                    if (!_times.TryGetValue(e.Target, out var t))
                    {
                        t = new List<float>();
                        _times[e.Target] = t;
                        _cumulative[e.Target] = new List<float>();
                    }
                    var c = _cumulative[e.Target];
                    t.Add(e.Time);
                    c.Add((c.Count > 0 ? c[c.Count - 1] : 0f) + e.Value);
                }
            }
        }

        /// <summary>0..1 can oranı.</summary>
        public float Fraction(int id, float time)
        {
            if (_deathTime.TryGetValue(id, out var d) && time >= d)
                return 0f;
            if (!_times.TryGetValue(id, out var t))
                return 1f;
            var lo = 0;
            var hi = t.Count;
            while (lo < hi)
            {
                var mid = (lo + hi) >> 1;
                if (t[mid] <= time) lo = mid + 1; else hi = mid;
            }
            if (lo == 0)
                return 1f;
            var damage = _cumulative[id][lo - 1];
            return Math.Max(0.02f, Math.Min(1f, 1f - damage / _maxHealth));
        }

        /// <summary>Zaman çizelgesinde bir sonraki ölüm için atlama zamanı (ölümden önce lead sn); yoksa -1.</summary>
        public static float NextKillSeek(IList<ReplayEvent> events, float now, float lead, int dir)
        {
            if (events == null)
                return -1f;
            var best = -1f;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type != ReplayEventType.Death)
                    continue;
                var seek = Math.Max(0f, events[i].Time - lead);
                if (dir >= 0)
                {
                    if (seek > now + 0.25f) return seek;
                }
                else if (seek < now - 0.25f)
                {
                    best = seek;
                }
            }
            return dir >= 0 ? -1f : best;
        }
    }
}
