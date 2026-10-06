using System;
using System.Collections.Generic;
using Project.Core.Domain;

namespace Project.Application.Services
{
    /// <summary>
    /// İHA keşfi saf mantığı: tim başına bekleme süresi ve menzil içi düşman işaretleme. Zaman çağıran tarafından verilir
    /// (Unity'ye bağımlı değildir).
    /// </summary>
    public sealed class ReconDroneService
    {
        public const float DefaultCooldown = 120f;
        public const float DefaultDuration = 12f;
        public const float DefaultRadius = 60f;
        public const float DefaultAltitude = 80f;

        private readonly Dictionary<int, float> _readyAt = new();

        public ReconDroneService(float cooldown = DefaultCooldown, float duration = DefaultDuration, float radius = DefaultRadius)
        {
            Cooldown = cooldown > 0f ? cooldown : 0f;
            Duration = duration > 0f ? duration : DefaultDuration;
            Radius = radius > 0f ? radius : DefaultRadius;
        }

        public float Cooldown { get; }
        public float Duration { get; }
        public float Radius { get; }

        public float GetCooldownRemaining(int team, float now)
        {
            if (!_readyAt.TryGetValue(team, out var readyAt))
                return 0f;
            var r = readyAt - now;
            return r > 0f ? r : 0f;
        }

        public bool IsReady(int team, float now) => GetCooldownRemaining(team, now) <= 0f;

        /// <summary>Hazırsa bekleme süresini başlatır.</summary>
        public bool TryLaunch(int team, float now)
        {
            if (!IsReady(team, now))
                return false;
            _readyAt[team] = now + Cooldown;
            return true;
        }

        public void Reset() => _readyAt.Clear();

        /// <summary>
        /// Merkez çevresinde (yatay mesafe) yarıçap içindeki canlı, düşman tim konumlarının indekslerini ekler.
        /// </summary>
        public int SelectEnemies(Float3 center, int ownTeam, IReadOnlyList<Float3> positions, IReadOnlyList<int> teams,
            IReadOnlyList<bool> alive, List<int> result)
        {
            if (result == null || positions == null || teams == null)
                return 0;

            var count = Math.Min(positions.Count, teams.Count);
            var r2 = Radius * Radius;
            var added = 0;
            for (var i = 0; i < count; i++)
            {
                if (teams[i] == ownTeam)
                    continue;
                if (alive != null && i < alive.Count && !alive[i])
                    continue;
                var dx = positions[i].X - center.X;
                var dz = positions[i].Z - center.Z;
                if (float.IsNaN(dx) || float.IsNaN(dz))
                    continue;
                if (dx * dx + dz * dz <= r2)
                {
                    result.Add(i);
                    added++;
                }
            }

            return added;
        }
    }
}
