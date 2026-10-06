using System.Collections.Generic;
using UnityEngine;

namespace Project.Online.Sim
{
    /// <summary>İlgi alanı kuralı: gözlemciye 500 m içindekiler veya aynı tim üyeleri replike edilir.</summary>
    public static class InterestRules
    {
        public const float Radius = 500f;
        /// <summary>Görünürken çıkış yarıçapı (histerezis: sınırda aç-kapa titremesini önler).</summary>
        public const float ExitRadius = 530f;

        public static bool ShouldReplicate(bool currentlyVisible, Vector3 observer, Vector3 target, int observerTeam, int targetTeam)
        {
            if (observerTeam >= 0 && observerTeam == targetTeam)
                return true;

            var r = currentlyVisible ? ExitRadius : Radius;
            return (observer - target).sqrMagnitude <= r * r;
        }
    }

    /// <summary>
    /// İstemci başına bant genişliği bütçesi (jeton kovası, bayt/sn). Kritik mesajlar (ölüm, atış sonucu)
    /// bütçeyi aşsa da gider (borçlanır); kritik olmayanlar (konum güncellemesi) bütçe yoksa düşer.
    /// </summary>
    public sealed class BandwidthBudget
    {
        public const int DefaultBytesPerSecond = 48_000;
        private const float MaxDebtSeconds = 1f;

        private sealed class Client
        {
            public float Tokens;
            public long Sent;
            public long Dropped;
            public long WindowBytes;
            public float WindowStart;
        }

        private readonly Dictionary<ulong, Client> _clients = new();
        private readonly float _bytesPerSecond;
        private readonly float _burstSeconds;
        private float _lastRefill = float.NaN;

        public BandwidthBudget(int bytesPerSecond = DefaultBytesPerSecond, float burstSeconds = 0.25f)
        {
            _bytesPerSecond = Mathf.Max(100, bytesPerSecond);
            _burstSeconds = Mathf.Max(0.05f, burstSeconds);
        }

        public int BytesPerSecond => (int)_bytesPerSecond;
        public int ClientCount => _clients.Count;

        public void Register(ulong clientId)
        {
            if (!_clients.ContainsKey(clientId))
                _clients[clientId] = new Client { Tokens = _bytesPerSecond * _burstSeconds };
        }

        public void Remove(ulong clientId) => _clients.Remove(clientId);

        /// <summary>Zamanı ilerletip jetonları doldurur (sunucu saatiyle çağrılır).</summary>
        public void Refill(float now)
        {
            if (float.IsNaN(_lastRefill))
            {
                _lastRefill = now;
                return;
            }

            var dt = Mathf.Max(0f, now - _lastRefill);
            _lastRefill = now;
            var cap = _bytesPerSecond * _burstSeconds;
            foreach (var c in _clients.Values)
                c.Tokens = Mathf.Min(cap, c.Tokens + dt * _bytesPerSecond);
        }

        public bool TryConsume(ulong clientId, int bytes, float now, bool critical = false)
        {
            Refill(now);
            if (!_clients.TryGetValue(clientId, out var c))
            {
                Register(clientId);
                c = _clients[clientId];
            }

            if (bytes <= 0)
                return true;

            if (c.Tokens >= bytes)
            {
                c.Tokens -= bytes;
            }
            else if (critical && c.Tokens - bytes >= -_bytesPerSecond * MaxDebtSeconds)
            {
                c.Tokens -= bytes;
            }
            else
            {
                c.Dropped += bytes;
                return false;
            }

            c.Sent += bytes;
            if (now - c.WindowStart >= 1f)
            {
                c.WindowStart = now;
                c.WindowBytes = 0;
            }

            c.WindowBytes += bytes;
            return true;
        }

        public long TotalSent(ulong clientId) => _clients.TryGetValue(clientId, out var c) ? c.Sent : 0;
        public long TotalDropped(ulong clientId) => _clients.TryGetValue(clientId, out var c) ? c.Dropped : 0;
        /// <summary>Kalan jeton / kapasite (0..1+).</summary>
        public float Headroom(ulong clientId) =>
            _clients.TryGetValue(clientId, out var c) ? c.Tokens / (_bytesPerSecond * _burstSeconds) : 1f;
    }
}
