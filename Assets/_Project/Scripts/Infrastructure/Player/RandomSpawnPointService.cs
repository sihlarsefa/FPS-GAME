using System.Collections.Generic;
using Project.Core.Interfaces;
using UnityEngine;

namespace Project.Infrastructure.Player
{
    /// <summary>
    /// Verilen noktalardan rastgele doğma konumu seçer (eğitim alanı / yer doğuşu). Yok edilmiş Transform'ları atlar;
    /// IRandom verilirse tekrar üretilebilir (sunucu/test), yoksa UnityEngine.Random kullanılır. Aynı noktayı arka arkaya
    /// vermemeye çalışır.
    /// </summary>
    public sealed class RandomSpawnPointService : IPlayerSpawnService
    {
        private readonly Transform[] _spawnPoints;
        private readonly IReadOnlyList<Vector3> _positions;
        private readonly IRandom _random;
        private readonly List<int> _candidates = new List<int>();
        private int _lastIndex = -1;

        public RandomSpawnPointService(Transform[] spawnPoints)
            : this(spawnPoints, null)
        {
        }

        public RandomSpawnPointService(Transform[] spawnPoints, IRandom random)
        {
            _spawnPoints = spawnPoints;
            _random = random;
        }

        /// <summary>Sabit dünya konumlarından seçer (ör. WorldMetadata.GroundSpawnPoints).</summary>
        public RandomSpawnPointService(IReadOnlyList<Vector3> positions, IRandom random = null)
        {
            _positions = positions;
            _random = random;
        }

        /// <summary>Kullanılabilir nokta sayısı.</summary>
        public int Count
        {
            get
            {
                if (_positions != null)
                    return _positions.Count;
                if (_spawnPoints == null)
                    return 0;

                var count = 0;
                for (var i = 0; i < _spawnPoints.Length; i++)
                {
                    if (_spawnPoints[i] != null)
                        count++;
                }

                return count;
            }
        }

        public bool TryGetSpawnPosition(out float x, out float y, out float z)
        {
            x = y = z = 0f;
            if (!TryGetSpawn(out var position, out _))
                return false;

            x = position.x;
            y = position.y;
            z = position.z;
            return true;
        }

        /// <summary>Konum ve (Transform kaynaklıysa) yön ile birlikte seçer.</summary>
        public bool TryGetSpawn(out Vector3 position, out float yawDegrees)
        {
            position = Vector3.zero;
            yawDegrees = 0f;

            _candidates.Clear();
            var total = _positions != null ? _positions.Count : _spawnPoints != null ? _spawnPoints.Length : 0;
            for (var i = 0; i < total; i++)
            {
                if (_positions == null && _spawnPoints[i] == null)
                    continue;
                _candidates.Add(i);
            }

            if (_candidates.Count == 0)
                return false;

            // Mümkünse son verilen noktayı tekrar verme.
            if (_candidates.Count > 1 && _lastIndex >= 0)
                _candidates.Remove(_lastIndex);

            var pick = _candidates[NextIndex(_candidates.Count)];
            _lastIndex = pick;

            if (_positions != null)
            {
                position = _positions[pick];
                return true;
            }

            var point = _spawnPoints[pick];
            position = point.position;
            yawDegrees = point.eulerAngles.y;
            return true;
        }

        private int NextIndex(int count)
        {
            if (count <= 1)
                return 0;

            var index = _random != null ? _random.Next(0, count) : Random.Range(0, count);
            return Mathf.Clamp(index, 0, count - 1);
        }
    }
}
