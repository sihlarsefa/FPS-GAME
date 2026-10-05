using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.Loot
{
    /// <summary>
    /// Yerdeki (alınabilir) eşyaların sahne kapsamlı kaydı. Eşyalar hareket etmediği için kayıt anındaki konuma göre
    /// 8 m'lik uzamsal ızgarada tutulur: yakın arama (bot yağması, etkileşim) tüm listeyi taramaz.
    /// FindLookTarget önce InteractMask ışını (duvarlar engeller), sonra açısal koni + görüş hattı ile bakılan eşyayı bulur.
    /// Sorgular bellek ayırmaz.
    /// </summary>
    public static class LootRegistry
    {
        /// <summary>Izgara hücre kenarı (m).</summary>
        public const float CellSize = 8f;

        /// <summary>Işın ıskalarsa kullanılan bakış konisinin yarı açısı (derece).</summary>
        public const float LookConeDegrees = 9f;

        private const int MaxConeCandidates = 6;

        private static readonly List<LootPickupComponent> _all = new(1024);
        private static readonly Dictionary<int, LootPickupComponent> _bySpawnId = new(1024);
        private static readonly Dictionary<long, List<LootPickupComponent>> _cells = new(1024);
        private static readonly Stack<List<LootPickupComponent>> _cellListPool = new(64);
        private static readonly RaycastHit[] _hits = new RaycastHit[24];
        private static readonly List<Candidate> _candidates = new(32);
        private static readonly IComparer<Candidate> CandidateComparer = new CandidateByScore();

        private static LootPickupComponent _focused;
        private static float _externalFocusTime = float.NegativeInfinity;

        private readonly struct Candidate
        {
            public readonly LootPickupComponent Pickup;
            public readonly float Score;

            public Candidate(LootPickupComponent pickup, float score)
            {
                Pickup = pickup;
                Score = score;
            }
        }

        private sealed class CandidateByScore : IComparer<Candidate>
        {
            public int Compare(Candidate x, Candidate y) => x.Score.CompareTo(y.Score);
        }

        /// <summary>Kayıtlı tüm eşyalar (yalnızca etkin ve alınabilir olanlar kayıtlıdır).</summary>
        public static IReadOnlyList<LootPickupComponent> All => _all;

        public static int Count => _all.Count;

        /// <summary>Yerel oyuncunun şu an baktığı (vurgulanan) eşya.</summary>
        public static LootPickupComponent Focused => _focused != null ? _focused : null;

        /// <summary>
        /// Odak son 0.35 s içinde dışarıdan (SetFocus) ayarlandıysa true: otomatik odak sürücüsü (LootFocusDriver) bu
        /// sürede devre dışı kalır, böylece oyuncu etkileşim kodu hangi eşyayı hedeflediyse halka onda görünür.
        /// </summary>
        public static bool HasExternalFocusOwner => Time.unscaledTime - _externalFocusTime < 0.35f;

        /// <summary>SpawnId ile kayıtlı eşyayı bulur (ağ eşlemesi / sunucu doğrulaması için).</summary>
        public static bool TryGet(int spawnId, out LootPickupComponent pickup)
        {
            if (spawnId > 0 && _bySpawnId.TryGetValue(spawnId, out pickup) && pickup != null && pickup.SpawnId == spawnId)
                return true;

            pickup = null;
            return false;
        }

        /// <summary>Kaydeder ya da (zaten kayıtlıysa) ızgaradaki konumunu günceller.</summary>
        public static void Register(LootPickupComponent pickup)
        {
            if (pickup == null)
                return;

            var position = pickup.transform.position;
            var key = KeyOf(position);
            if (IsRegistered(pickup))
            {
                pickup.RegisteredPosition = position;
                if (pickup.CellKey != key)
                {
                    RemoveFromCell(pickup);
                    AddToCell(pickup, key);
                }

                UpdateSpawnId(pickup);
                return;
            }

            pickup.RegistryIndex = _all.Count;
            pickup.RegisteredPosition = position;
            _all.Add(pickup);
            AddToCell(pickup, key);
            UpdateSpawnId(pickup);
        }

        public static void Unregister(LootPickupComponent pickup)
        {
            if (ReferenceEquals(pickup, null))
                return;

            if (ReferenceEquals(_focused, pickup))
                ApplyFocus(null);

            if (!IsRegistered(pickup))
            {
                pickup.RegistryIndex = -1;
                return;
            }

            RemoveFromCell(pickup);
            RemoveSpawnId(pickup);
            var index = pickup.RegistryIndex;
            var last = _all.Count - 1;
            if (index != last)
            {
                var moved = _all[last];
                _all[index] = moved;
                if (!ReferenceEquals(moved, null))
                    moved.RegistryIndex = index;
            }

            _all.RemoveAt(last);
            pickup.RegistryIndex = -1;
        }

        /// <summary>
        /// Yarıçap içindeki en yakın alınabilir eşya (filtre isteğe bağlı). Filtre yalnızca o ana kadarki en yakından
        /// daha yakın adaylar için çağrılır.
        /// </summary>
        public static LootPickupComponent FindNearest(Vector3 position, float radius, Func<LootPickupComponent, bool> filter = null)
        {
            if (radius <= 0f || _all.Count == 0)
                return null;

            LootPickupComponent best = null;
            var bestSqr = radius * radius;

            if (UseLinearScan(radius))
            {
                for (var i = 0; i < _all.Count; i++)
                    Consider(_all[i], position, filter, ref best, ref bestSqr);
                return best;
            }

            CellRange(position, radius, out var minX, out var maxX, out var minZ, out var maxZ);
            for (var cx = minX; cx <= maxX; cx++)
            {
                for (var cz = minZ; cz <= maxZ; cz++)
                {
                    if (!_cells.TryGetValue(Key(cx, cz), out var list))
                        continue;

                    for (var i = 0; i < list.Count; i++)
                        Consider(list[i], position, filter, ref best, ref bestSqr);
                }
            }

            return best;
        }

        /// <summary>Yarıçap içindeki tüm alınabilir eşyaları output'a ekler (sırasız). Eklenen sayıyı döndürür.</summary>
        public static int FindInRadius(Vector3 position, float radius, List<LootPickupComponent> output,
            Func<LootPickupComponent, bool> filter = null)
        {
            if (output == null || radius <= 0f || _all.Count == 0)
                return 0;

            var added = 0;
            var sqr = radius * radius;
            if (UseLinearScan(radius))
            {
                for (var i = 0; i < _all.Count; i++)
                    added += Collect(_all[i], position, sqr, filter, output);
                return added;
            }

            CellRange(position, radius, out var minX, out var maxX, out var minZ, out var maxZ);
            for (var cx = minX; cx <= maxX; cx++)
            {
                for (var cz = minZ; cz <= maxZ; cz++)
                {
                    if (!_cells.TryGetValue(Key(cx, cz), out var list))
                        continue;

                    for (var i = 0; i < list.Count; i++)
                        added += Collect(list[i], position, sqr, filter, output);
                }
            }

            return added;
        }

        /// <summary>
        /// Bakılan eşya: önce InteractMask ışını (tetik kutuları dahil; duvar/araç engeller), ıskalarsa
        /// <see cref="LookConeDegrees"/> konisindeki en iyi aday (görüş hattı kontrolüyle). Yoksa null.
        /// </summary>
        public static LootPickupComponent FindLookTarget(Vector3 origin, Vector3 direction, float maxDistance)
        {
            if (maxDistance <= 0f || direction.sqrMagnitude < 1e-8f)
                return null;

            direction.Normalize();

            // 1) Doğrudan ışın.
            var mask = GameLayers.InteractMask | GameLayers.LineOfSightMask;
            var count = Physics.RaycastNonAlloc(origin, direction, _hits, maxDistance, mask, QueryTriggerInteraction.Collide);
            LootPickupComponent rayTarget = null;
            var rayDistance = float.MaxValue;
            var blockDistance = maxDistance;
            for (var i = 0; i < count; i++)
            {
                var hit = _hits[i];
                var collider = hit.collider;
                if (collider == null)
                    continue;

                if (collider.gameObject.layer == GameLayers.Loot)
                {
                    if (hit.distance < rayDistance && collider.TryGetComponent<LootPickupComponent>(out var pickup) && pickup.IsAvailable)
                    {
                        rayDistance = hit.distance;
                        rayTarget = pickup;
                    }

                    continue;
                }

                // Loot dışı tetikler (bölge, duman vb.) ve başlangıçta içinde olunan çarpıştırıcılar engellemez.
                if (collider.isTrigger || hit.distance <= 0f)
                    continue;

                if (hit.distance < blockDistance)
                    blockDistance = hit.distance;
            }

            for (var i = 0; i < count; i++)
                _hits[i] = default;

            if (rayTarget != null && rayDistance <= blockDistance + 0.05f)
                return rayTarget;

            // 2) Açısal koni (küçük eşyalara tam nişan almak gerekmesin).
            return FindInCone(origin, direction, Mathf.Min(maxDistance, blockDistance + 0.6f));
        }

        /// <summary>
        /// Odak (vurgu) eşyasını ayarlar; parlak halka yalnızca bu eşyada görünür. null → kaldır. Oyuncu etkileşim kodu
        /// her taramada çağırırsa otomatik odak sürücüsü devre dışı kalır.
        /// </summary>
        public static void SetFocus(LootPickupComponent pickup)
        {
            _externalFocusTime = Time.unscaledTime;
            ApplyFocus(pickup);
        }

        /// <summary>Odağı ayarlar (sahiplik işaretlemeden; LootFocusDriver kullanır).</summary>
        internal static void ApplyFocus(LootPickupComponent pickup)
        {
            if (pickup != null && !pickup.IsAvailable)
                pickup = null;

            if (ReferenceEquals(_focused, pickup) && pickup != null)
            {
                WorldItemVisuals.ShowFocus(pickup);
                return;
            }

            _focused = pickup;
            WorldItemVisuals.ShowFocus(pickup);
        }

        /// <summary>Kaydı temizler (sahne değişimi). Eşyaları yok etmez.</summary>
        public static void Clear()
        {
            for (var i = 0; i < _all.Count; i++)
            {
                var pickup = _all[i];
                if (!ReferenceEquals(pickup, null))
                {
                    pickup.RegistryIndex = -1;
                    pickup.RegisteredSpawnId = 0;
                }
            }

            _all.Clear();
            _bySpawnId.Clear();
            foreach (var pair in _cells)
            {
                pair.Value.Clear();
                _cellListPool.Push(pair.Value);
            }

            _cells.Clear();
            _focused = null;
            _externalFocusTime = float.NegativeInfinity;
            WorldItemVisuals.ShowFocus(null);
        }

        // ------------------------------------------------------------------ Internals

        private static LootPickupComponent FindInCone(Vector3 origin, Vector3 direction, float maxDistance)
        {
            if (_all.Count == 0 || maxDistance <= 0f)
                return null;

            _candidates.Clear();
            var cosLimit = Mathf.Cos(LookConeDegrees * Mathf.Deg2Rad);
            var maxSqr = maxDistance * maxDistance;
            var center = origin + direction * (maxDistance * 0.5f);
            var searchRadius = maxDistance * 0.5f + 1f;

            if (UseLinearScan(searchRadius))
            {
                for (var i = 0; i < _all.Count; i++)
                    ConsiderCone(_all[i], origin, direction, cosLimit, maxSqr);
            }
            else
            {
                CellRange(center, searchRadius, out var minX, out var maxX, out var minZ, out var maxZ);
                for (var cx = minX; cx <= maxX; cx++)
                {
                    for (var cz = minZ; cz <= maxZ; cz++)
                    {
                        if (!_cells.TryGetValue(Key(cx, cz), out var list))
                            continue;

                        for (var i = 0; i < list.Count; i++)
                            ConsiderCone(list[i], origin, direction, cosLimit, maxSqr);
                    }
                }
            }

            if (_candidates.Count == 0)
                return null;

            if (_candidates.Count > 1)
                _candidates.Sort(CandidateComparer);

            var tests = Mathf.Min(_candidates.Count, MaxConeCandidates);
            LootPickupComponent result = null;
            for (var i = 0; i < tests; i++)
            {
                var pickup = _candidates[i].Pickup;
                var target = pickup.FocusPoint;
                if (!Physics.Linecast(origin, target, GameLayers.LineOfSightMask, QueryTriggerInteraction.Ignore))
                {
                    result = pickup;
                    break;
                }
            }

            _candidates.Clear();
            return result;
        }

        private static void ConsiderCone(LootPickupComponent pickup, Vector3 origin, Vector3 direction, float cosLimit, float maxSqr)
        {
            if (pickup == null || !pickup.IsAvailable)
                return;

            var toTarget = pickup.FocusPoint - origin;
            var sqr = toTarget.sqrMagnitude;
            if (sqr > maxSqr || sqr < 1e-6f)
                return;

            var distance = Mathf.Sqrt(sqr);
            var cos = Vector3.Dot(toTarget, direction) / distance;

            // Yakındaki eşyalar ekranda büyük görünür: açısal sınırı eşyanın boyutuna göre genişlet.
            var sizeAngle = Mathf.Atan2(pickup.RingRadius, distance);
            var limit = Mathf.Min(cosLimit, Mathf.Cos(Mathf.Min(sizeAngle, 25f * Mathf.Deg2Rad)));
            if (cos < limit)
                return;

            var angle = Mathf.Acos(Mathf.Clamp(cos, -1f, 1f));
            // Skor: açı (radyan) + mesafeye küçük ceza (aynı doğrultuda yakın olan önce).
            _candidates.Add(new Candidate(pickup, angle + distance * 0.004f));
        }

        private static void Consider(LootPickupComponent pickup, Vector3 position, Func<LootPickupComponent, bool> filter,
            ref LootPickupComponent best, ref float bestSqr)
        {
            if (pickup == null || !pickup.IsAvailable)
                return;

            var sqr = (pickup.RegisteredPosition - position).sqrMagnitude;
            if (sqr > bestSqr)
                return;

            if (filter != null && !filter(pickup))
                return;

            bestSqr = sqr;
            best = pickup;
        }

        private static int Collect(LootPickupComponent pickup, Vector3 position, float sqrRadius,
            Func<LootPickupComponent, bool> filter, List<LootPickupComponent> output)
        {
            if (pickup == null || !pickup.IsAvailable)
                return 0;

            if ((pickup.RegisteredPosition - position).sqrMagnitude > sqrRadius)
                return 0;

            if (filter != null && !filter(pickup))
                return 0;

            output.Add(pickup);
            return 1;
        }

        private static void UpdateSpawnId(LootPickupComponent pickup)
        {
            var id = pickup.SpawnId;
            if (pickup.RegisteredSpawnId == id)
                return;

            RemoveSpawnId(pickup);
            if (id > 0)
            {
                _bySpawnId[id] = pickup;
                pickup.RegisteredSpawnId = id;
            }
        }

        private static void RemoveSpawnId(LootPickupComponent pickup)
        {
            var id = pickup.RegisteredSpawnId;
            if (id > 0 && _bySpawnId.TryGetValue(id, out var current) && ReferenceEquals(current, pickup))
                _bySpawnId.Remove(id);

            pickup.RegisteredSpawnId = 0;
        }

        private static bool IsRegistered(LootPickupComponent pickup)
        {
            var index = pickup.RegistryIndex;
            return index >= 0 && index < _all.Count && ReferenceEquals(_all[index], pickup);
        }

        /// <summary>Hücre sayısı eşya sayısından fazlaysa doğrusal tarama daha ucuzdur.</summary>
        private static bool UseLinearScan(float radius)
        {
            var span = Mathf.CeilToInt(radius * 2f / CellSize) + 1;
            return span > 64 || span * span >= _all.Count;
        }

        private static void CellRange(Vector3 position, float radius, out int minX, out int maxX, out int minZ, out int maxZ)
        {
            minX = Mathf.FloorToInt((position.x - radius) / CellSize);
            maxX = Mathf.FloorToInt((position.x + radius) / CellSize);
            minZ = Mathf.FloorToInt((position.z - radius) / CellSize);
            maxZ = Mathf.FloorToInt((position.z + radius) / CellSize);
        }

        private static long KeyOf(Vector3 position) =>
            Key(Mathf.FloorToInt(position.x / CellSize), Mathf.FloorToInt(position.z / CellSize));

        private static long Key(int cx, int cz) => ((long)cx << 32) | (uint)cz;

        private static void AddToCell(LootPickupComponent pickup, long key)
        {
            if (!_cells.TryGetValue(key, out var list))
            {
                list = _cellListPool.Count > 0 ? _cellListPool.Pop() : new List<LootPickupComponent>(8);
                _cells[key] = list;
            }

            list.Add(pickup);
            pickup.CellKey = key;
        }

        private static void RemoveFromCell(LootPickupComponent pickup)
        {
            if (!_cells.TryGetValue(pickup.CellKey, out var list))
                return;

            for (var i = 0; i < list.Count; i++)
            {
                if (!ReferenceEquals(list[i], pickup))
                    continue;

                var last = list.Count - 1;
                list[i] = list[last];
                list.RemoveAt(last);
                break;
            }

            if (list.Count == 0)
            {
                _cells.Remove(pickup.CellKey);
                _cellListPool.Push(list);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _all.Clear();
            _bySpawnId.Clear();
            _cells.Clear();
            _cellListPool.Clear();
            _candidates.Clear();
            _focused = null;
            _externalFocusTime = float.NegativeInfinity;
        }
    }
}
